using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Character;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Magic
{
    /// <summary>Phase 9 through the session: spell picks at creation and level-up, preparation, rests and time, out-of-fight healing, persistence and migration, and the caster AI.</summary>
    public sealed class SpellProgressionTests
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
        private CharacterSheetData Sheet => W.Player.Rpg;

        private void Create(string cls) => CombatTests.Create(_s, cls, CombatTests.Scores);

        private void AdvanceTo(int level)
        {
            var need = ProgressionRules.ExperienceFor(level) - Sheet.Experience;
            if (need > 0) Assert.That(_s.Dispatch(new AwardExperienceCommand(need, ExperienceSource.Milestone, null, "test")).IsSuccess, Is.True);
            while (Sheet.Level < level)
            {
                var r = _s.Dispatch(new AdvanceCharacterCommand(CharacterAdvancement.SuggestChoices(Sheet, Sheet.Level + 1)));
                Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
            }
        }

        // ===== Picks =======================================================================================

        [TestCase("bard", 2, 4, "class:spells")]
        [TestCase("cleric", 3, 0, null)]
        [TestCase("druid", 2, 0, null)]
        [TestCase("sorcerer", 4, 2, "class:spells")]
        [TestCase("warlock", 2, 2, "class:spells")]
        [TestCase("wizard", 3, 6, "class:spellbook")]
        [TestCase("paladin", 0, 0, null)]
        [TestCase("ranger", 0, 0, null)]
        public void EveryCaster_HasItsSpellPicks_FromTheClassTable(string cls, int cantrips, int spells, string spellChoice)
        {
            var c = RpgCatalog.FindClass(cls);
            var cantripChoice = c.Choices.FirstOrDefault(x => x.Id == "class:cantrips");
            Assert.That(cantripChoice?.Count ?? 0, Is.EqualTo(cantrips));
            if (spellChoice != null) Assert.That(c.Choices.Single(x => x.Id == spellChoice).Count, Is.EqualTo(spells));
            foreach (var ch in c.Choices.Concat(c.LevelChoices).Where(x => x.Pool == "cantrips" || x.Pool == "spells" || x.Pool == "spellbook"))
            {
                Assert.That(ch.UpTo, Is.True);
                Assert.That(ch.IntroducedInRules, Is.EqualTo(8));
                Assert.That(ch.Options.All(o => SpellCatalog.OnList(SpellCatalog.Find(o.Id), cls)), Is.True, "Only the class's own spells.");
            }

            foreach (var none in new[] { "barbarian", "fighter", "monk", "rogue" })
                Assert.That(RpgCatalog.FindClass(none).Choices.Concat(RpgCatalog.FindClass(none).LevelChoices).Any(x => x.Pool == "spells" || x.Pool == "cantrips"), Is.False);
        }

        [Test]
        public void LevelUp_AddsSpellPicks_Atomically_AndOpenPicksCanBeMadeLater()
        {
            Create("wizard");
            var book = new SpellAccess(new CharacterRules(Sheet)).Spellbook.Count;
            AdvanceTo(5);
            var access = new SpellAccess(new CharacterRules(Sheet));
            Assert.That(access.MaxSpellLevel, Is.EqualTo(3));
            Assert.That(access.Spellbook.Count, Is.GreaterThan(book), "Level-ups wrote spells into the book.");
            Assert.That(access.Spellbook.Select(id => SpellCatalog.Find(id).Level).Max(), Is.LessThanOrEqualTo(3));
            Assert.That(CharacterValidation.CheckSheet(Sheet), Is.Empty);

            var view = _s.GetSpellbook();
            var pick = view.OpenPicks.FirstOrDefault();
            if (pick != null)
            {
                var option = pick.Options[0];
                Assert.That(_s.Dispatch(new ChooseSpellCommand(pick.ChoiceId, option.Id)).IsSuccess, Is.True);
                Assert.That(Sheet.Chosen(pick.ChoiceId), Does.Contain(option.Id));
                Assert.That(_s.Dispatch(new ChooseSpellCommand(pick.ChoiceId, option.Id)).IsSuccess, Is.False, "Not twice.");
            }

            Assert.That(_s.Dispatch(new ChooseSpellCommand("class:cantrips", "sacred_flame")).IsSuccess, Is.False, "A cleric cantrip is not on the wizard list.");
        }

        [Test]
        public void ChoosingASpell_RefusesSpellsAboveThePicksLevel()
        {
            Create("bard");
            var open = _s.GetSpellbook().OpenPicks.Single(p => p.ChoiceId == "class:spells");
            Assert.That(open.Options.All(o => o.Level <= 1), Is.True, "Level-1 picks offer level-1 spells only.");
            Assert.That(_s.Dispatch(new ChooseSpellCommand("class:spells", "shatter")).IsSuccess, Is.False);
            Assert.That(_s.Dispatch(new ChooseSpellCommand("class:spells", "healing_word")).IsSuccess, Is.True);
        }

        // ===== Preparation =================================================================================

        [Test]
        public void Preparation_FollowsTheClassRules_AndTheLongRestWindow()
        {
            Create("cleric");
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds" })).IsSuccess, Is.True, "A new character can prepare.");
            var five = _s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds", "guiding_bolt", "inflict_wounds", "shield_of_faith" }));
            Assert.That(five.IsSuccess, Is.False, "Five is above the level-1 limit of four.");
            Assert.That(five.Message, Does.Contain("At most 4"), "Refused by the preparation rule itself (the sheet validator would also refuse it).");
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "magic_missile" })).IsSuccess, Is.False, "Not a cleric spell.");
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "hold_person" })).IsSuccess, Is.False, "Level 2 at character level 1.");
            Sheet.Spellcasting.PreparationChanges.ToString();
            var w = W;
            // A fight closes the window; adding stays allowed, replacing waits for a long rest.
            CombatTests.EnterTown(_s);
            Assert.That(_s.Dispatch(new StartPracticeEncounterCommand(false)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "bless" })).IsSuccess, Is.False, "Not during a fight.");
            Assert.That(_s.Dispatch(new EndEncounterCommand(_s.GetEncounter().Id, false)).IsSuccess, Is.True);
            Assert.That(Sheet.Spellcasting.PreparationChanges, Is.EqualTo(0));
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds", "guiding_bolt" })).IsSuccess, Is.True, "Adding while there is room.");
            var swap = _s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds", "inflict_wounds" }));
            Assert.That(swap.IsSuccess, Is.False);
            Assert.That(swap.Message, Does.Contain("long rest"));
            Sheet.ToString();
            var heal = _s.LongRest();
            if (heal.IsSuccess)
            {
                Assert.That(Sheet.Spellcasting.PreparationChanges, Is.EqualTo(-1));
                Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds", "inflict_wounds" })).IsSuccess, Is.True);
            }
        }

        // ===== Rests =======================================================================================

        [Test]
        public void ShortRest_PassesAnHour_SpendsHitDice_AndRecoversShortRestResources()
        {
            Create("warlock");
            AdvanceTo(3);
            Assert.That(_s.Dispatch(new SpendResourceCommand("pact_slots", 2)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-10)).IsSuccess, Is.True);
            var hp = Sheet.CurrentHp;
            var dice = Sheet.Resources["hit_dice"];
            var before = W.CurrentSimulationDate.ToDayNumber() * 1440 + W.MinuteOfDay;
            var r = _s.ShortRest(2);
            Assert.That(r.IsSuccess, Is.True, r.Message);
            var after = W.CurrentSimulationDate.ToDayNumber() * 1440 + W.MinuteOfDay;
            Assert.That(after - before, Is.EqualTo(60), "An hour of world time.");
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(dice - 2));
            Assert.That(Sheet.CurrentHp, Is.GreaterThanOrEqualTo(hp));
            Assert.That(Sheet.Resources["pact_slots"], Is.EqualTo(2), "Pact slots return on a short rest.");
            Assert.That(Sheet.Spellcasting.RestLog.Last().Kind, Is.EqualTo(RestKind.Short));
            Assert.That(_s.ShortRest(99).IsSuccess, Is.False, "More hit dice than left.");
            var key = Sheet.Spellcasting.RestLog.Last().Key;
            Assert.That(_s.Dispatch(new FinishRestCommand(RestKind.Short, 0, key)).IsSuccess, Is.False, "The same rest never applies twice.");
        }

        [Test]
        public void LongRest_RestoresEverything_OncePer24Hours()
        {
            Create("barbarian");
            AdvanceTo(3);
            Assert.That(_s.Dispatch(new SpendResourceCommand("rage", 2)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new SpendResourceCommand("hit_dice", 3)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-5)).IsSuccess, Is.True);
            var rage = Sheet.Resources["rage"];
            Assert.That(_s.ShortRest().IsSuccess, Is.True);
            Assert.That(Sheet.Resources["rage"], Is.EqualTo(rage + 1), "One use of Rage returns on a short rest.");
            var date = W.CurrentSimulationDate;
            var minute = W.MinuteOfDay;
            var r = _s.LongRest();
            Assert.That(r.IsSuccess, Is.True, r.Message);
            var rules = new CharacterRules(Sheet);
            Assert.That(Sheet.CurrentHp, Is.EqualTo(rules.MaxHp));
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(3), "All hit dice back (SRD 5.2.1).");
            Assert.That(Sheet.Resources["rage"], Is.EqualTo(rules.ResourceMax(rules.FindResource("rage"))));
            Assert.That((W.CurrentSimulationDate.ToDayNumber() - date.ToDayNumber()) * 1440 + W.MinuteOfDay - minute, Is.EqualTo(480), "Eight hours of world time.");
            var again = _s.LongRest();
            Assert.That(again.IsSuccess, Is.False);
            Assert.That(again.Message, Does.Contain("24 hours"));
            Assert.That(CharacterValidation.Check(W.Player), Is.Empty);
        }

        [Test]
        public void Rests_AreRefusedDuringAFight_AndAtZeroHitPointsForALongRest()
        {
            Create("fighter");
            CombatTests.EnterTown(_s);
            Assert.That(_s.Dispatch(new StartPracticeEncounterCommand(false)).IsSuccess, Is.True);
            Assert.That(_s.ShortRest().Message, Is.EqualTo(WorldSession.EncounterBlocksTime));
            Assert.That(_s.LongRest().IsSuccess, Is.False);
            Assert.That(_s.Dispatch(new EndEncounterCommand(_s.GetEncounter().Id, false)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-Sheet.CurrentHp)).IsSuccess, Is.True);
            Assert.That(_s.LongRest().Message, Does.Contain("at least 1 hit point"));
        }

        [Test]
        public void ArcaneRecovery_ReturnsSlots_OnAShortRest_OncePerLongRest()
        {
            Create("wizard");
            AdvanceTo(3);
            Assert.That(_s.Dispatch(new SpendResourceCommand("spell_slots_2", 2)).IsSuccess, Is.True);
            Assert.That(_s.ShortRest().IsSuccess, Is.True);
            Assert.That(Sheet.Resources["spell_slots_2"], Is.EqualTo(1), "Half of level 3, rounded up: one level-2 slot.");
            Assert.That(Sheet.Resources["arcane_recovery"], Is.EqualTo(0));
            Assert.That(_s.ShortRest().IsSuccess, Is.True);
            Assert.That(Sheet.Resources["spell_slots_2"], Is.EqualTo(1), "Once per long rest.");
        }

        [Test]
        public void HealingOutsideAFight_SpendsASlot_AndDoesNotMoveTheClock()
        {
            Create("cleric");
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "cure_wounds" })).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-5)).IsSuccess, Is.True);
            var minute = W.MinuteOfDay;
            var r = _s.Dispatch(new CastHealingOutsideCombatCommand("cure_wounds", 1));
            Assert.That(r.IsSuccess, Is.True, r.Message);
            Assert.That(Sheet.Resources["spell_slots_1"], Is.EqualTo(1));
            Assert.That(W.MinuteOfDay, Is.EqualTo(minute));
            Assert.That(_s.Dispatch(new CastHealingOutsideCombatCommand("bless", 1)).IsSuccess, Is.False, "Only healing outside fights.");
        }

        // ===== Persistence =================================================================================

        [Test]
        public void AFightWithConcentrationAndEffects_SurvivesSaveAndLoad()
        {
            Create("cleric");
            Assert.That(_s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "shield_of_faith" })).IsSuccess, Is.True);
            CombatTests.EnterTown(_s);
            Assert.That(_s.Dispatch(new StartPracticeEncounterCommand(true, true)).IsSuccess, Is.True);
            var snap = _s.GetEncounter();
            for (var i = 0; i < 12 && !snap.IsPlayersTurn; i++)
            {
                Assert.That(_s.Dispatch(new RunAiTurnCommand(snap.Id, snap.Sequence)).IsSuccess, Is.True);
                snap = _s.GetEncounter(true);
                if (snap.Status != EncounterStatus.Active) Assert.Inconclusive("The fight ended before the player's turn.");
            }

            var me = snap.Player;
            var r = _s.Dispatch(new CombatActionCommand(snap.Id, me.Id, new CastSpellAction("bless", 1, new[] { me.Id }), snap.Sequence));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join(" ", r.Errors));
            var w = W;
            Assert.That(w.ActiveEncounter.Effects, Is.Not.Empty);
            Assert.That(w.ActiveEncounter.Combatants.Single(c => c.Kind == CombatantKind.Player).Concentration?.SpellId, Is.EqualTo("bless"));
            Assert.That(w.ActiveEncounter.Combatants.Single(c => c.CreatureId == "cult_fanatic").Resources["spell_slots_1"], Is.GreaterThan(0));
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(w);
            var loaded = serializer.Deserialize(json);
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(JObject.Parse(serializer.Serialize(loaded.World))["world"].ToString(), Is.EqualTo(JObject.Parse(json)["world"].ToString()));
            var e = loaded.World.ActiveEncounter;
            Assert.That(e.Effects.Count, Is.EqualTo(w.ActiveEncounter.Effects.Count));
            Assert.That(e.Combatants.Single(c => c.Kind == CombatantKind.Player).Concentration.SpellId, Is.EqualTo("bless"));

            bool Loads(Action<JObject> change)
            {
                var doc = JObject.Parse(json);
                change(doc);
                return serializer.Deserialize(doc.ToString()).IsSuccess;
            }

            Assert.That(Loads(d => d["world"]["encounters"][0]["effects"][0]["targetId"] = Guid.NewGuid().ToString()), Is.False, "An effect on no one.");
            Assert.That(Loads(d => d["world"]["encounters"][0]["effects"][0]["kind"] = "Flying"), Is.False, "An unknown effect.");
            Assert.That(Loads(d => d["world"]["player"]["rpg"]["spellcasting"]["prepared"] = new JArray("magic_missile")), Is.False, "A wizard spell prepared by a cleric.");
            Assert.That(Loads(d => d["world"]["player"]["rpg"]["spellcasting"]["preparationChanges"] = 5), Is.False);
        }

        [Test]
        public void PhaseEightSave_MigratesToV11_KeepingEverything()
        {
            Create("druid");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-3)).IsSuccess, Is.True);
            var original = W;
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(original));
            Phase2Worlds.StripToV10(doc);
            Assert.That(doc["world"]["player"]["rpg"]["spellcasting"], Is.Null);
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var s = loaded.World.Player.Rpg;
            Assert.That(loaded.World.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(s.Spellcasting.Prepared, Is.Empty);
            Assert.That(s.Spellcasting.PreparationChanges, Is.EqualTo(-1));
            Assert.That(s.CurrentHp, Is.EqualTo(original.Player.Rpg.CurrentHp));
            Assert.That(s.Experience, Is.EqualTo(original.Player.Rpg.Experience));
            Assert.That(s.Resources, Is.EqualTo(original.Player.Rpg.Resources));
            Assert.That(loaded.World.Structures.Count, Is.EqualTo(original.Structures.Count));
            Assert.That(loaded.World.HistoricalEvents.Count, Is.EqualTo(original.HistoricalEvents.Count));
        }

        // ===== The caster AI ===============================================================================

        [Test]
        public void TheCasterAi_ProposesValidSpells_Deterministically_AndHealsTheHurt()
        {
            var w = SpellTests.Caster(_s, "fighter");
            var n = 0;
            Guid G(string p) => new Guid(++n, 0, 0, new byte[8]);
            var enc = CombatEngine.Create(w, new EncounterSetup
            {
                Name = "Casters", Kind = EncounterKind.Other, Participants =
                {
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "acolyte", Side = "temple", Position = new GridPos(0, 0) },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "guard", Side = "temple", Position = new GridPos(1, 0), Name = "Temple guard" },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "cult_fanatic", Side = "cult", Position = new GridPos(4, 6) },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "cult", Position = new GridPos(5, 6) },
                },
            }, G, out var errors);
            Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var e = new CombatEngine(w, enc);
            e.Begin();
            var guard = enc.Combatants.First(c => c.Name == "Temple guard");
            guard.CurrentHp = 1;
            var acolyte = enc.Combatants.First(c => c.CreatureId == "acolyte");
            enc.TurnIndex = enc.Order.IndexOf(acolyte.CombatantId);
            acolyte.Turn = new TurnResources();
            var proposal = CombatAi.Next(e, acolyte);
            Assert.That(proposal, Is.InstanceOf<CastSpellAction>());
            Assert.That(((CastSpellAction)proposal).SpellId, Is.EqualTo("cure_wounds"));
            Assert.That(((CastSpellAction)proposal).Targets, Has.Member(guard.CombatantId));
            Assert.That(CombatAi.Next(e, acolyte).Describe, Is.EqualTo(proposal.Describe), "Same state, same proposal.");

            var spells = 0;
            for (var step = 0; step < 3000 && enc.IsActive; step++)
            {
                var actor = enc.Active;
                var p = CombatAi.Next(e, actor);
                Assert.That(e.Unavailable(actor, p), Is.Null, $"{actor.Name}: {p.Describe}");
                if (p is CastSpellAction) spells++;
                Assert.That(e.Act(actor.CombatantId, p).Ok, Is.True);
            }

            Assert.That(enc.IsActive, Is.False, "The fight ends.");
            Assert.That(spells, Is.GreaterThan(1), "The casters cast.");
            Assert.That(enc.Combatants.All(c => c.Resources.Values.All(v => v >= 0)), Is.True);
        }

        [Test]
        public void AResident_CanFightAsACaster_WithoutBeingCopied()
        {
            Create("fighter");
            CombatTests.EnterTown(_s);
            var w = W;
            var resident = w.Npcs.First(npc => !npc.IsDeceased && npc.HomeSettlementId == w.Player.SettlementId && npc.Role == NpcRole.Elder);
            var setup = new EncounterSetup
            {
                Name = "Elder's stand", Kind = EncounterKind.Other, SettlementId = w.Player.SettlementId, Participants =
                {
                    new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player },
                    new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = resident.NpcId, CreatureId = "acolyte", Side = "player", Position = new GridPos(-1, 0) },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "bandits", Position = new GridPos(3, 5) },
                },
            };
            Assert.That(_s.Dispatch(new StartEncounterCommand(setup)).IsSuccess, Is.True);
            var e = W.ActiveEncounter;
            var npc = e.Combatants.Single(c => c.Kind == CombatantKind.Npc);
            Assert.That(npc.Name, Is.EqualTo(resident.Name));
            Assert.That(npc.SourceId, Is.EqualTo(resident.NpcId.ToString()));
            Assert.That(npc.Resources["spell_slots_1"], Is.EqualTo(3));
            Assert.That(W.Npcs.Count(x => x.Name == resident.Name), Is.EqualTo(1), "Never copied.");
        }
    }
}
