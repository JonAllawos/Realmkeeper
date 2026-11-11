using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Fauna;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 10 (ADR-0043, ADR-0044): named people carry character records — sheets for those whose work is fighting,
    /// healing or keeping order, by the same rules as the player's — and can be promoted, gain experience once per key
    /// and advance; the bestiary is data, validated, with its traits applied in combat; wildlife lives as regional
    /// populations that breed, are hunted and preyed upon, and settlements keep herds.
    /// </summary>
    public sealed class LivingWorldTests
    {
        private string _dir;
        private WorldSession _s;
        private int _guids;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            _guids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        // ----- Named people ---------------------------------------------------------------------------------

        [Test]
        public void EveryLivingNamedPerson_HasARecord_AndTheFightersHaveSoundSheets()
        {
            var w = W;
            var living = w.Npcs.Where(n => !n.IsDeceased).ToList();
            Assert.That(living.All(n => n.Character != null), Is.True);
            var sheets = living.Where(n => n.Character.HasSheet).ToList();
            Assert.That(sheets, Is.Not.Empty);
            foreach (var n in sheets)
            {
                Assert.That(CharacterValidation.CheckSheet(n.Character.Sheet), Is.Empty, n.Name);
                Assert.That(n.Character.Sheet.State, Is.EqualTo(CreationState.Complete));
                Assert.That(NpcArchetypeCatalog.Find(n.Character.ArchetypeId).ClassId, Is.Not.Null, $"{n.Name} has a sheet because their archetype fights.");
            }

            foreach (var n in living.Where(n => n.Role == NpcRole.Guard)) Assert.That(n.Character.HasSheet, Is.True, $"{n.Name} is a guard.");
            Assert.That(NpcArchetypeCatalog.Validate(), Is.Empty);
        }

        [Test]
        public void Records_AreDeterministic_AndSurviveSaveAndLoadExactly()
        {
            var serializer = new JsonWorldSerializer();
            string Records(WorldData w) => new JArray(((JArray)JObject.Parse(serializer.Serialize(w))["world"]["npcs"]).Select(n => n["character"])).ToString();
            Assert.That(Records(Phase2Worlds.Generated(777)), Is.EqualTo(Records(Phase2Worlds.Generated(777))));
            var w = W;
            var back = serializer.Deserialize(serializer.Serialize(w));
            Assert.That(back.Status, Is.EqualTo(LoadStatus.Success), string.Join("; ", back.Errors));
            Assert.That(Records(back.World), Is.EqualTo(Records(w)));
        }

        [Test]
        public void Promotion_GivesASheet_KeepingWhoTheyAre_Once()
        {
            var civilian = W.Npcs.First(n => !n.IsDeceased && n.Character != null && !n.Character.HasSheet);
            var before = W.FindNpc(civilian.NpcId);
            var result = _s.Dispatch(new PromoteNpcCommand(civilian.NpcId, "fighter"));
            Assert.That(result.IsSuccess, Is.True, result.Message + " " + string.Join("; ", result.Errors ?? new System.Collections.Generic.List<string>()));
            var after = W.FindNpc(civilian.NpcId);
            Assert.That(after.Character.HasSheet, Is.True);
            Assert.That(after.Character.Sheet.ClassId, Is.EqualTo("fighter"));
            Assert.That(after.Tier, Is.EqualTo(NpcTier.Notable));
            Assert.That(after.Name, Is.EqualTo(before.Name));
            Assert.That(after.HomeSettlementId, Is.EqualTo(before.HomeSettlementId));
            Assert.That(after.Memories.Count, Is.GreaterThanOrEqualTo(before.Memories.Count));
            Assert.That(after.Character.Gear.All(), Is.EquivalentTo(before.Character.Gear.All()), "They keep what they carried.");
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.NpcPromoted && e.NpcIds.Contains(civilian.NpcId)), Is.True);
            Assert.That(_s.Dispatch(new PromoteNpcCommand(civilian.NpcId)).IsSuccess, Is.False, "Promotion happens once.");
            Assert.That(_s.Dispatch(new PromoteNpcCommand(civilian.NpcId, "no_such_class")).IsSuccess, Is.False);
        }

        [Test]
        public void Experience_IsAwardedOncePerKey_AndAdvancementUsesTheSharedRules()
        {
            var guard = W.Npcs.First(n => !n.IsDeceased && n.Character != null && n.Character.HasSheet);
            var level = guard.Character.Sheet.Level;
            var needed = ProgressionRules.ExperienceFor(level + 1) - guard.Character.Sheet.Experience;
            Assert.That(_s.Dispatch(new AdvanceNpcCommand(guard.NpcId)).IsSuccess, Is.False, "No level without the experience.");
            Assert.That(_s.Dispatch(new AwardNpcExperienceCommand(guard.NpcId, needed, "test:bandits", ExperienceSource.Encounter, "drove off bandits")).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new AwardNpcExperienceCommand(guard.NpcId, needed, "test:bandits", ExperienceSource.Encounter, "again")).IsSuccess, Is.False, "Once per key.");
            Assert.That(W.FindNpc(guard.NpcId).Character.Sheet.Experience, Is.EqualTo(guard.Character.Sheet.Experience + needed));
            var advance = _s.Dispatch(new AdvanceNpcCommand(guard.NpcId));
            Assert.That(advance.IsSuccess, Is.True, advance.Message + " " + string.Join("; ", advance.Errors ?? new System.Collections.Generic.List<string>()));
            var sheet = W.FindNpc(guard.NpcId).Character.Sheet;
            Assert.That(sheet.Level, Is.EqualTo(level + 1));
            Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty);
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.NpcLevelAdvanced), Is.True);
            var view = _s.GetNpcCharacter(guard.NpcId);
            Assert.That(view.Level, Is.EqualTo(level + 1));
            Assert.That(view.ArmorClassText, Does.Not.Contain("Realmkeeper."), "The armour class is explained in words, not a type name.");
            Assert.That(view.ArmorClassText, Is.Not.Empty);
        }

        [Test]
        public void ADamagedRecord_IsRefusedOnLoad()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            var npc = ((JArray)doc["world"]["npcs"]).First(n => n["character"]?["sheet"] != null && n["character"]["sheet"].Type != JTokenType.Null);
            npc["character"]["archetypeId"] = "no_such_archetype";
            Assert.That(serializer.Deserialize(doc.ToString()).Status, Is.Not.EqualTo(LoadStatus.Success));
        }

        // ----- The bestiary ---------------------------------------------------------------------------------

        [Test]
        public void TheBestiary_IsValid_AndCoversItsCategories()
        {
            Assert.That(CreatureCatalog.Validate(), Is.Empty);
            foreach (var id in new[] { "deer", "boar", "black_bear", "brown_bear", "dire_wolf", "giant_rat", "giant_spider", "goat", "riding_horse", "draft_horse", "mastiff",
                         "goblin", "highwayman", "bandit_captain", "cultist", "veteran", "skeleton", "zombie", "ogre", "troll", "rabbit", "fox", "cattle", "sheep", "chicken" })
                Assert.That(CreatureCatalog.Find(id), Is.Not.Null, id);
            var categories = CreatureCatalog.All.Select(c => c.Category).Distinct().ToList();
            Assert.That(categories.Count, Is.GreaterThanOrEqualTo(4), "Beasts, humanoids, undead, giants and more.");
            Assert.That(CreatureCatalog.All.All(c => c.MaxHp > 0 && c.ArmorClass > 0), Is.True);
        }

        private Guid NextGuid(string purpose) => new Guid(++_guids, 0, 0, new byte[8]);

        private CombatEngine Begin(WorldData w, string creature)
        {
            var setup = new EncounterSetup
            {
                Name = "Trait test", Kind = EncounterKind.Other,
                Participants =
                {
                    new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = creature, Side = "foes", Position = new GridPos(1, 0) },
                },
            };
            var enc = CombatEngine.Create(w, setup, NextGuid, out var errors);
            Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var e = new CombatEngine(w, enc);
            e.Begin();
            return e;
        }

        private static CombatantData Foe(CombatEngine e) => e.Encounter.Combatants.First(c => c.Kind != CombatantKind.Player);

        private void StartTurnOf(CombatEngine e, CombatantData c)
        {
            var other = e.Encounter.Combatants.First(x => x != c);
            e.Encounter.TurnIndex = e.Encounter.Order.IndexOf(other.CombatantId);
            other.Turn = new TurnResources();
            var r = e.Act(other.CombatantId, new EndTurnAction());
            Assert.That(e.Encounter.Order[e.Encounter.TurnIndex], Is.EqualTo(c.CombatantId), r?.Error);
        }

        [Test]
        public void UndeadFortitude_DoesNotSaveFromRadiantDamage()
        {
            Combat.CombatTests.Create(_s, "fighter");
            var w = W;
            var e = Begin(w, "zombie");
            var z = Foe(e);
            e.ApplyDamage(z, z.CurrentHp + 3, DamageType.Radiant, false, "test");
            Assert.That(z.State, Is.EqualTo(VitalState.Dead));
        }

        [Test]
        public void ATroll_Regenerates_UnlessFireOrAcidSearedIt()
        {
            Combat.CombatTests.Create(_s, "fighter");
            var w = W;
            var e = Begin(w, "troll");
            var t = Foe(e);
            e.ApplyDamage(t, 30, DamageType.Slashing, false, "test");
            var hurt = t.CurrentHp;
            StartTurnOf(e, t);
            Assert.That(t.CurrentHp, Is.EqualTo(Math.Min(t.MaxHp, hurt + 10)), "Regeneration: 10 at the start of its turn.");

            e.ApplyDamage(t, 12, DamageType.Fire, false, "test");
            var seared = t.CurrentHp;
            StartTurnOf(e, t);
            Assert.That(t.CurrentHp, Is.EqualTo(seared), "Fire stops its next regeneration.");

            e.ApplyDamage(t, t.CurrentHp + 5, DamageType.Fire, false, "test");
            Assert.That(t.State, Is.Not.EqualTo(VitalState.Dead), "At 0 it collapses, not dies, while it might still regenerate.");
            StartTurnOf(e, t);
            Assert.That(t.State, Is.EqualTo(VitalState.Dead), "Seared by fire at 0, it dies at the start of its turn.");
        }

        // ----- Wildlife and herds ---------------------------------------------------------------------------

        [Test]
        public void Wildlife_IsPopulated_DeterministicallyAndWithinBounds()
        {
            var a = Phase2Worlds.Generated(31337);
            var b = Phase2Worlds.Generated(31337);
            Assert.That(a.CreaturePopulations, Is.Not.Empty);
            Assert.That(a.CreaturePopulations.Select(p => p.PopulationId + p.CreatureId + p.Count), Is.EqualTo(b.CreaturePopulations.Select(p => p.PopulationId + p.CreatureId + p.Count)));
            foreach (var p in a.CreaturePopulations)
            {
                Assert.That(CreatureCatalog.Find(p.CreatureId), Is.Not.Null);
                Assert.That(p.Count, Is.InRange(0, Math.Min(p.Capacity, FaunaBounds.MaxPopulation)));
            }

            Assert.That(a.CreaturePopulations.Select(p => p.CreatureId), Has.Member("deer").And.Member("bandit"));
            Assert.That(a.Livestock, Is.Not.Empty);
        }

        [Test]
        public void AWeek_BreedsUpToCapacity_WolvesTakeDeer_AndHungryTownsEatTheirHerds()
        {
            var w = Phase2Worlds.Generated(123456789);
            var today = w.CurrentSimulationDate;
            for (var i = 0; i < 60; i++) FaunaSystem.Week(w, today);
            foreach (var p in w.CreaturePopulations) Assert.That(p.Count, Is.LessThanOrEqualTo(p.Capacity), "Never past what the land carries.");

            var deer = w.CreaturePopulations.First(p => p.CreatureId == "deer");
            deer.Count = deer.Capacity;
            var wolves = new CreaturePopulationData
            {
                PopulationId = "test-wolves", RegionId = deer.RegionId, CreatureId = "wolf", Habitat = deer.Habitat, Center = deer.Center, RangeCells = 4, Count = 6, Capacity = 6,
            };
            w.CreaturePopulations.Add(wolves);
            var deaths = deer.Deaths;
            FaunaSystem.Week(w, today);
            Assert.That(deer.Deaths, Is.GreaterThanOrEqualTo(deaths + 3), "Six wolves take three deer a week.");

            var herd = w.Livestock.First(h => h.Count >= 10);
            var settlement = w.FindSettlement(herd.SettlementId);
            var count = herd.Count;
            settlement.IsInFoodShortage = true;
            FaunaSystem.Week(w, today);
            Assert.That(herd.Count, Is.LessThan(count), "A hungry settlement slaughters some of its herd.");
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(w).Errors));
        }

        [Test]
        public void ImpossibleWildlife_IsRefusedOnLoad()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["world"]["creaturePopulations"][0]["count"] = -4;
            Assert.That(serializer.Deserialize(doc.ToString()).Status, Is.Not.EqualTo(LoadStatus.Success));
            var doc2 = JObject.Parse(serializer.Serialize(W));
            doc2["world"]["livestock"][0]["creatureId"] = "dragon";
            Assert.That(serializer.Deserialize(doc2.ToString()).Status, Is.Not.EqualTo(LoadStatus.Success));
        }
    }
}
