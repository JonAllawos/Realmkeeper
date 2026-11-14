using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.World
{
    /// <summary>
    /// Phase 25: what the interface overhaul added below the screens, tested through the session.
    /// <list type="bullet">
    /// <item><b>The bestiary record (ADR-0116).</b> Only creatures met in a fight are known; defeat reveals their strength.
    /// It survives a save, and is seeded once for an older save.</item>
    /// <item><b>The notice rule.</b> Notices are derived from the world as the player knows it. They are never raised for
    /// what was already known, or for a load. They are bounded.</item>
    /// <item><b>Save schema v27 and its validation.</b></item>
    /// </list>
    /// </summary>
    public sealed class Phase25InterfaceTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private void Ok<T>(T c) where T : ICommand
        {
            var r = _s.Dispatch(c);
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors ?? Array.Empty<string>()));
        }

        private void StartPractice(bool lethal = false)
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            Ok(new StartPracticeEncounterCommand(lethal));
        }

        /// <summary>Plays the fight to its end: the player attacks whoever is in reach (else ends the turn); the AI takes its turns.</summary>
        private void PlayOut()
        {
            var snap = _s.GetEncounter();
            for (var step = 0; step < 800 && snap != null && snap.Status == EncounterStatus.Active; step++)
            {
                if (snap.IsPlayersTurn)
                {
                    var attack = snap.PlayerOptions.FirstOrDefault(o => o.Action is AttackAction && o.Unavailable == null && o.Label.StartsWith(snap.Player.Weapon, StringComparison.Ordinal));
                    Ok(new CombatActionCommand(snap.Id, snap.Player.Id, attack?.Action ?? new EndTurnAction(), snap.Sequence));
                }
                else Ok(new RunAiTurnCommand(snap.Id, snap.Sequence));

                snap = _s.GetEncounter(true);
            }

            if (snap != null && snap.Status == EncounterStatus.Active) Ok(new EndEncounterCommand(snap.Id, true));
        }

        // ----- The bestiary --------------------------------------------------------------------------------------

        [Test]
        public void Bestiary_IsEmpty_UntilAFight_ThenKnowsOnlyWhatWasSeen()
        {
            CombatTests.Create(_s, "fighter");
            Assert.That(_s.GetBestiary().Entries, Is.Empty, "Nothing is known before a fight.");
            CombatTests.EnterTown(_s);
            Ok(new StartPracticeEncounterCommand(false));
            var hostile = W.ActiveEncounter.Combatants.Where(c => c.CreatureId != null && c.Side != W.ActiveEncounter.Combatants.First(p => p.Kind == CombatantKind.Player).Side)
                .Select(c => c.CreatureId).Distinct().ToList();
            Assert.That(hostile, Is.Not.Empty);
            var b = _s.GetBestiary();
            Assert.That(b.Entries.Select(e => e.CreatureId), Is.EquivalentTo(hostile), "Exactly the kinds the player faces.");
            foreach (var e in b.Entries)
            {
                Assert.That(e.Met, Is.EqualTo(1));
                Assert.That(e.Defeated, Is.Zero);
                Assert.That(e.Studied, Is.False);
                Assert.That(e.ArmourClass, Is.Null, "Its strength is not known before one is defeated.");
                Assert.That(e.HitPoints, Is.Null);
                Assert.That(e.Attacks, Is.Empty);
                Assert.That(e.Name, Is.EqualTo(CreatureCatalog.Find(e.CreatureId).Name));
            }

            var allies = W.ActiveEncounter.Combatants.Where(c => c.Kind == CombatantKind.Npc && c.CreatureId != null && !hostile.Contains(c.CreatureId)).Select(c => c.CreatureId);
            foreach (var a in allies) Assert.That(b.Entries.Any(e => e.CreatureId == a), Is.False, "The player's own side is not studied.");
        }

        [Test]
        public void Bestiary_DefeatedKinds_ShowTheirStrength_FromTheCatalogue()
        {
            StartPractice();
            PlayOut();
            var fight = W.Encounters.Last();
            var playerSide = fight.Combatants.First(c => c.Kind == CombatantKind.Player).Side;
            var fallen = fight.Combatants.Where(c => c.CreatureId != null && c.Side != playerSide && c.State != VitalState.Conscious).GroupBy(c => c.CreatureId).ToDictionary(g => g.Key, g => g.Count());
            var b = _s.GetBestiary();
            foreach (var e in b.Entries)
            {
                fallen.TryGetValue(e.CreatureId, out var n);
                Assert.That(e.Defeated, Is.EqualTo(n), e.Name);
                if (n == 0) { Assert.That(e.ArmourClass, Is.Null); continue; }
                var d = CreatureCatalog.Find(e.CreatureId);
                Assert.That(e.ArmourClass, Does.StartWith(d.ArmorClass.ToString()));
                Assert.That(e.HitPoints, Does.StartWith(d.MaxHp.ToString()));
                Assert.That(e.Attacks.Count, Is.EqualTo(d.Attacks.Count));
                Assert.That(e.Challenge, Does.StartWith(d.ChallengeRating));
            }

            Assert.That(b.Studied, Is.EqualTo(b.Entries.Count(e => e.Defeated > 0)));
        }

        [Test]
        public void KnownCreatures_SurviveSaving_AndAnOlderSave_IsSeededOnce_FromTheFightsItHolds()
        {
            StartPractice();
            PlayOut();
            var before = W.Player.KnownCreatures.Select(k => (k.CreatureId, k.Met, k.Defeated)).OrderBy(x => x.CreatureId).ToList();
            Assert.That(before, Is.Not.Empty);
            Assert.That(_s.Execute(new SaveWorldCommand("p25")).IsSuccess, Is.True);
            Assert.That(_s.Execute(new LoadWorldCommand("p25")).IsSuccess, Is.True);
            Assert.That(W.Player.KnownCreatures.Select(k => (k.CreatureId, k.Met, k.Defeated)).OrderBy(x => x.CreatureId), Is.EqualTo(before));
            Assert.That(W.SchemaVersion, Is.EqualTo(27));

            // A v26 save has no record: it is made from the fights the save still holds, once.
            var path = Path.Combine(_dir, "p25" + JsonWorldStore.Extension);
            var doc = JObject.Parse(File.ReadAllText(path));
            doc["schemaVersion"] = 26;
            ((JObject)doc["world"]["player"]).Remove("knownCreatures");
            File.WriteAllText(Path.Combine(_dir, "old" + JsonWorldStore.Extension), doc.ToString());
            Assert.That(_s.Execute(new LoadWorldCommand("old")).IsSuccess, Is.True);
            Assert.That(W.Player.KnownCreatures.Select(k => (k.CreatureId, k.Met, k.Defeated)).OrderBy(x => x.CreatureId), Is.EqualTo(before), "Seeded from the recorded fight.");
            Assert.That(WorldValidator.Validate(W).IsValid, Is.True);
            Assert.That(_s.Execute(new SaveWorldCommand("old")).IsSuccess, Is.True);
            Assert.That(_s.Execute(new LoadWorldCommand("old")).IsSuccess, Is.True);
            Assert.That(W.Player.KnownCreatures.Select(k => (k.CreatureId, k.Met, k.Defeated)).OrderBy(x => x.CreatureId), Is.EqualTo(before), "Not counted twice.");
        }

        [Test]
        public void Validator_RefusesAnUnknownOrRepeatedCreature()
        {
            CombatTests.Create(_s, "fighter");
            var w = W;
            w.Player.KnownCreatures.Add(new KnownCreatureData { CreatureId = "no_such_beast", Met = 1 });
            Assert.That(WorldValidator.Validate(w).Errors, Has.Some.Contains("not in the creature catalogue"));
            w = W;
            var id = CreatureCatalog.All.First().Id;
            w.Player.KnownCreatures.Add(new KnownCreatureData { CreatureId = id, Met = 1 });
            w.Player.KnownCreatures.Add(new KnownCreatureData { CreatureId = id, Met = 1 });
            Assert.That(WorldValidator.Validate(w).Errors, Has.Some.Contains("empty or repeated"));
        }

        // ----- Notices -------------------------------------------------------------------------------------------

        [Test]
        public void Notices_ComeFromRealChanges_AndNotFromWhatWasAlreadyKnown()
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            var first = _s.GetNoticeSnapshot();
            Assert.That(first.IsValid, Is.True);
            Assert.That(PlayerNotices.Compare(first, _s.GetNoticeSnapshot()), Is.Empty, "Nothing changed, nothing to say.");

            // Taking something off and putting it back on: two equipment notices from the real command.
            var chest = W.Player.Equipment[EquipmentSlot.Chest];
            Ok(new UnequipItemCommand(EquipmentSlot.Chest));
            var off = _s.GetNoticeSnapshot();
            var n = PlayerNotices.Compare(first, off);
            Assert.That(n.Select(x => x.Kind), Does.Contain("equipment"));
            Assert.That(n.Select(x => x.Kind), Does.Not.Contain("item"), "Taking something off is not receiving it.");
            Assert.That(n.Single(x => x.Kind == "equipment").Title, Does.StartWith("Taken off: " + ItemCatalog.Find(chest.ItemId).Name));
            Ok(new EquipItemCommand(chest.StackId));
            n = PlayerNotices.Compare(off, _s.GetNoticeSnapshot());
            Assert.That(n.Single(x => x.Kind == "equipment").Title, Is.EqualTo("Equipped: " + ItemCatalog.Find(chest.ItemId).Name));

            // Experience enough for level 2: one notice, important, and it says nothing is chosen for the player.
            var before = _s.GetNoticeSnapshot();
            Ok(new AwardExperienceCommand(300, ExperienceSource.Milestone, reason: "test", awardKey: "p25-notice"));
            n = PlayerNotices.Compare(before, _s.GetNoticeSnapshot());
            var level = n.Single(x => x.Kind == "level");
            Assert.That(level.Important, Is.True);
            Assert.That(level.Title, Is.EqualTo("You can advance to level 2"));
            Assert.That(level.Body, Does.Contain("nothing is chosen for you"));

            // A fight: the creatures met become bestiary notices.
            before = _s.GetNoticeSnapshot();
            Ok(new StartPracticeEncounterCommand(false));
            n = PlayerNotices.Compare(before, _s.GetNoticeSnapshot());
            Assert.That(n.Where(x => x.Kind == "bestiary").Select(x => x.Title), Is.Not.Empty);
            Assert.That(n.Where(x => x.Kind == "bestiary").All(x => x.Title.StartsWith("Bestiary: ", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Notices_AreNotRaised_ForALoad_OrAcrossWorlds()
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            Assert.That(_s.Execute(new SaveWorldCommand("p25n")).IsSuccess, Is.True);
            var chest = W.Player.Equipment[EquipmentSlot.Chest];
            Ok(new UnequipItemCommand(EquipmentSlot.Chest));
            var before = _s.GetNoticeSnapshot();
            Assert.That(_s.Execute(new LoadWorldCommand("p25n")).IsSuccess, Is.True); // the tunic is worn again in the save
            Assert.That(W.Player.Equipment.ContainsKey(EquipmentSlot.Chest), Is.True);
            Assert.That(PlayerNotices.Compare(before, _s.GetNoticeSnapshot()), Is.Empty, "A load is not news.");
            Assert.That(chest, Is.Not.Null);
        }

        [Test]
        public void Notices_AreBounded_AndNeverMentionQuestsThePlayerHasNotFound()
        {
            CombatTests.Create(_s, "fighter");
            var snap = _s.GetNoticeSnapshot();
            foreach (var q in W.Quests.Where(q => q.State == Realmkeeper.Domain.Quests.QuestState.Unavailable || q.State == Realmkeeper.Domain.Quests.QuestState.Available))
                Assert.That(snap.Quests.ContainsKey(q.InstanceId), Is.False, "An undiscovered quest is not the player's.");

            // Many items at once: at most MaxPerChange notices, the last one summing up the rest.
            var before = _s.GetNoticeSnapshot();
            var after = _s.GetNoticeSnapshot();
            var ids = ItemCatalog.Items.Take(20).Select(d => d.Id).ToList();
            foreach (var id in ids) after.Items[id] = 1 + (before.Items.TryGetValue(id, out var had) ? had : 0);
            var n = PlayerNotices.Compare(before, after);
            Assert.That(n.Count, Is.EqualTo(PlayerNotices.MaxPerChange));
            Assert.That(n.Last().Title, Does.EndWith("more changes"));
        }

        [Test]
        public void NoticeSnapshot_IsCheap_ForAFullWorld()
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            NoticeSnapshot last = null;
            for (var i = 0; i < 50; i++)
            {
                var s = _s.GetNoticeSnapshot();
                if (last != null) _s.FillNoticeEvents(last, s);
                PlayerNotices.Compare(last ?? s, s);
                last = s;
            }

            var ms = watch.Elapsed.TotalMilliseconds / 50;
            TestContext.WriteLine($"Notice snapshot and comparison: {ms:0.000} ms each.");
            Assert.That(ms, Is.LessThan(25), "Taken twice a second in exploration, so it must stay small.");
        }
    }
}
