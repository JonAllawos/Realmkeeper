using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>Phase 6: the rules catalogs, ability and skill maths, character creation through the session, persistence and migration.</summary>
    public sealed class CharacterTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private static string World(string json)
        {
            var w = (JObject)JObject.Parse(json)["world"];
            return w.ToString();
        }

        private string Json() => World(new JsonWorldSerializer().Serialize(W));

        /// <summary>A valid human fighter with the soldier background.</summary>
        private static CharacterCreationChoices Fighter()
        {
            var draft = new CharacterDraft();
            draft.Choices.Name = "Wren Ashdown";
            draft.Choices.Title = "the Steady";
            draft.SetBackground("soldier");
            draft.SetClass("fighter");
            Assert.That(draft.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Perception)), Is.True);
            Assert.That(draft.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Survival)), Is.True);
            Assert.That(draft.Toggle("species:skillful", nameof(Skill.Insight)), Is.True);
            Assert.That(draft.Toggle("class:fighting_style", "defense"), Is.True);
            return draft.Clean();
        }

        private void AssertRejectedAndUnchanged(CharacterCreationChoices c, string expect)
        {
            var before = Json();
            var r = _s.Dispatch(new CreateCharacterCommand(c));
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Message + " " + string.Join(" ", r.Errors), Does.Contain(expect));
            Assert.That(Json(), Is.EqualTo(before), "A refused creation changes nothing.");
        }

        // ----- Catalogs --------------------------------------------------------------------------------

        [Test]
        public void Catalogs_HoldTheTwelveOfEach_WithUniqueIds_AndValidate()
        {
            Assert.That(RpgCatalog.Validate(), Is.Empty, string.Join("\n", RpgCatalog.Validate()));
            Assert.That(RpgCatalog.Species.Select(s => s.Name), Is.EqualTo(new[]
                { "Human", "High Elf", "Wood Elf", "Dwarf", "Halfling", "Gnome", "Orc", "Half-Orc", "Tiefling", "Dragonborn", "Aasimar", "Goliath" }));
            Assert.That(RpgCatalog.Backgrounds.Select(b => b.Name), Is.EquivalentTo(new[]
                { "Soldier", "Noble", "Criminal", "Acolyte", "Artisan", "Merchant", "Outlander", "Scholar", "Sailor", "Entertainer", "Hermit", "Folk Hero" }));
            Assert.That(RpgCatalog.Classes.Select(c => c.Name), Is.EqualTo(new[]
                { "Barbarian", "Bard", "Cleric", "Druid", "Fighter", "Monk", "Paladin", "Ranger", "Rogue", "Sorcerer", "Warlock", "Wizard" }));
            foreach (var list in new[] { RpgCatalog.Species.Select(s => s.Id), RpgCatalog.Backgrounds.Select(b => b.Id), RpgCatalog.Classes.Select(c => c.Id) })
                Assert.That(list.Distinct().Count(), Is.EqualTo(12));
            Assert.That(RpgCatalog.FindSpecies("aasimar").Source, Is.EqualTo(ContentSource.RealmkeeperOriginal), "Aasimar is not in any SRD.");
            Assert.That(RpgCatalog.Species.Single(s => s.Appearance.HasFinishedModel).Id, Is.EqualTo("human"), "Only Human has finished art.");
        }

        [Test]
        public void Classes_DifferMeaningfullyAtLevelOne()
        {
            var classes = RpgCatalog.Classes;
            Assert.That(classes.Select(c => c.HitDie).Distinct().OrderBy(d => d), Is.EqualTo(new[] { 6, 8, 10, 12 }));
            Assert.That(classes.Select(c => string.Join(",", c.SavingThrows)).Distinct().Count(), Is.GreaterThanOrEqualTo(6), "Saving throws vary.");
            Assert.That(classes.Count(c => c.Spellcasting != null), Is.EqualTo(8), "Bard, cleric, druid, paladin, ranger, sorcerer, warlock, wizard cast spells at level 1.");
            foreach (var c in classes)
            {
                Assert.That(c.Features, Is.Not.Empty, c.Name);
                Assert.That(c.Equipment, Is.Not.Empty, c.Name);
                var signature = $"{c.HitDie}|{string.Join(",", c.SavingThrows)}|{string.Join(",", c.ArmorProficiencies)}|{string.Join(",", c.Features.Select(f => f.Id))}|{c.SkillChoices}";
                Assert.That(classes.Count(o => $"{o.HitDie}|{string.Join(",", o.SavingThrows)}|{string.Join(",", o.ArmorProficiencies)}|{string.Join(",", o.Features.Select(f => f.Id))}|{o.SkillChoices}" == signature),
                    Is.EqualTo(1), c.Name + " is unlike every other class.");
            }
        }

        // ----- Abilities, skills, saves, hit points ---------------------------------------------------

        [TestCase(1, -5)]
        [TestCase(3, -4)]
        [TestCase(8, -1)]
        [TestCase(9, -1)]
        [TestCase(10, 0)]
        [TestCase(11, 0)]
        [TestCase(12, 1)]
        [TestCase(15, 2)]
        [TestCase(17, 3)]
        [TestCase(20, 5)]
        public void AbilityModifier_IsFloorOfHalfScoreAboveTen(int score, int modifier) =>
            Assert.That(AbilityRules.Modifier(score), Is.EqualTo(modifier));

        [Test]
        public void ProficiencyBonus_FollowsLevel()
        {
            Assert.That(new[] { 1, 4, 5, 8, 9, 13, 17, 20 }.Select(AbilityRules.ProficiencyBonus), Is.EqualTo(new[] { 2, 2, 3, 3, 4, 5, 6, 6 }));
        }

        [Test]
        public void StandardArray_AndPointBuy_AreValidated()
        {
            Assert.That(AbilityRules.Check(AbilityMethod.StandardArray, new[] { 8, 10, 12, 13, 14, 15 }), Is.Null);
            Assert.That(AbilityRules.Check(AbilityMethod.StandardArray, new[] { 15, 15, 13, 12, 10, 8 }), Is.Not.Null, "Each value once.");
            Assert.That(AbilityRules.Check(AbilityMethod.PointBuy, new[] { 15, 15, 15, 8, 8, 8 }), Is.Null, "27 points exactly.");
            Assert.That(AbilityRules.Check(AbilityMethod.PointBuy, new[] { 13, 13, 13, 12, 12, 12 }), Is.Null, "15 + 12 = 27.");
            Assert.That(AbilityRules.Check(AbilityMethod.PointBuy, new[] { 15, 15, 15, 9, 8, 8 }), Does.Contain("28 of 27"));
            Assert.That(AbilityRules.Check(AbilityMethod.PointBuy, new[] { 16, 8, 8, 8, 8, 8 }), Does.Contain("8–15"));
            Assert.That(AbilityRules.Check(AbilityMethod.PointBuy, new[] { 7, 8, 8, 8, 8, 8 }), Does.Contain("8–15"));
            Assert.That(AbilityRules.Check(AbilityMethod.None, new[] { 10, 10, 10, 10, 10, 10 }), Is.Not.Null, "No free entry.");
            Assert.That(AbilityRules.PointCost(14), Is.EqualTo(7));
            Assert.That(AbilityRules.PointCost(15), Is.EqualTo(9));
        }

        [Test]
        public void Rules_DeriveScoresSkillsSavesAndHitPoints_FromChoices()
        {
            var sheet = CharacterCommandHandlers.BuildSheet(Fighter(), out var errors);
            Assert.That(errors, Is.Empty);
            var rules = new CharacterRules(sheet);
            Assert.That(rules.Score(Ability.Strength), Is.EqualTo(16), "15 + 1 (human).");
            Assert.That(rules.Modifier(Ability.Strength), Is.EqualTo(3));
            Assert.That(rules.Score(Ability.Constitution), Is.EqualTo(14), "13 + 1 (Strength and Dexterity take the top of the array).");
            Assert.That(rules.SaveProficient(Ability.Strength) && rules.SaveProficient(Ability.Constitution), Is.True);
            Assert.That(rules.SaveProficient(Ability.Dexterity), Is.False);
            Assert.That(rules.SaveBonus(Ability.Strength).Total, Is.EqualTo(3 + 2));
            Assert.That(rules.SaveBonus(Ability.Strength).Text, Is.EqualTo("Strength +3, proficiency +2"));
            Assert.That(rules.SkillRank(Skill.Athletics), Is.EqualTo(ProficiencyRank.Proficient), "Soldier grants Athletics.");
            Assert.That(rules.SkillSources(Skill.Athletics), Has.Member("Soldier"));
            Assert.That(rules.SkillRank(Skill.Insight), Is.EqualTo(ProficiencyRank.Proficient), "Human: Skillful.");
            Assert.That(rules.SkillBonus(Skill.Perception).Total, Is.EqualTo(rules.Modifier(Ability.Wisdom) + 2));
            Assert.That(rules.SkillRank(Skill.Arcana), Is.EqualTo(ProficiencyRank.None));
            Assert.That(rules.MaxHp, Is.EqualTo(10 + 2), "d10 maximum + Constitution +2.");
            Assert.That(rules.ToolProficiencies, Has.Member("dice_set"));
            Assert.That(rules.ArmorProficiencies, Has.Member("heavy"));
            Assert.That(rules.Languages, Has.Member("common"));
        }

        [Test]
        public void TypedEffects_HaveRealConsequences()
        {
            CharacterRules Rules(string species, string cls, Action<CharacterDraft> extra = null)
            {
                var d = new CharacterDraft();
                d.SetSpecies(species);
                d.SetBackground("hermit");
                d.SetClass(cls);
                extra?.Invoke(d);
                var sheet = CharacterCommandHandlers.BuildSheet(d.Clean(), out _);
                return new CharacterRules(sheet);
            }

            var dwarf = Rules("dwarf", "barbarian");
            Assert.That(dwarf.MaxHp, Is.EqualTo(12 + dwarf.Modifier(Ability.Constitution) + 1), "d12 + Constitution + dwarven toughness.");
            Assert.That(dwarf.Speed, Is.EqualTo(25));
            Assert.That(dwarf.Resistances, Has.Member("poison"));
            Assert.That(dwarf.Darkvision, Is.EqualTo(60));
            Assert.That(Rules("wood_elf", "ranger").Speed, Is.EqualTo(35), "Fleet of foot.");
            Assert.That(Rules("goliath", "fighter").CarryPercent, Is.EqualTo(200), "Powerful build.");
            Assert.That(Rules("orc", "fighter").Darkvision, Is.EqualTo(120));
            var dragonborn = Rules("dragonborn", "paladin", d => d.Toggle("species:ancestry", "cold"));
            Assert.That(dragonborn.Resistances, Has.Member("cold"));
            Assert.That(dragonborn.Features.Select(f => f.feature.Name), Has.Some.StartsWith("Draconic Ancestry").And.Some.Contains("(cold)"), "A chosen species option is listed as a feature.");
            var protector = Rules("human", "cleric", d => d.Toggle("class:divine_order", "protector"));
            Assert.That(protector.Features.Count(f => f.feature.Name.Contains("Protector")), Is.EqualTo(1), "A chosen class option is listed once as a feature.");
            Assert.That(protector.ArmorProficiencies, Has.Member("heavy"));

            var rogue = Rules("halfling", "rogue", d =>
            {
                d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Stealth));
                d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Perception));
                Assert.That(d.Toggle("class:expertise", nameof(Skill.Stealth)), Is.True);
                Assert.That(d.Toggle("class:expertise", nameof(Skill.Arcana)), Is.False, "Expertise only in a proficient skill.");
            });
            Assert.That(rogue.SkillRank(Skill.Stealth), Is.EqualTo(ProficiencyRank.Expertise));
            Assert.That(rogue.SkillBonus(Skill.Stealth).Total, Is.EqualTo(rogue.Modifier(Ability.Dexterity) + 4), "Expertise doubles proficiency.");

            var monk = Rules("human", "monk");
            Assert.That(monk.ArmorClass(new PlayerCharacterData()).Total,
                Is.EqualTo(10 + monk.Modifier(Ability.Dexterity) + monk.Modifier(Ability.Wisdom)), "Unarmoured defence: Dexterity + Wisdom.");
            var wizard = Rules("high_elf", "wizard");
            Assert.That(wizard.SpellSaveDc().Total, Is.EqualTo(8 + 2 + wizard.Modifier(Ability.Intelligence)));
            Assert.That(wizard.ResourceDefinitions.Select(r => r.Id), Has.Member("spell_slots_1").And.Member("arcane_recovery"));
            var bard = Rules("tiefling", "bard");
            Assert.That(bard.ResourceMax(bard.FindResource("bardic_inspiration")), Is.EqualTo(Math.Max(1, bard.Modifier(Ability.Charisma))));
        }

        // ----- Creation through the session -----------------------------------------------------------

        [Test]
        public void NewWorld_PlayerAwaitsCreation_AndCreationGrantsTheKitOnce_KeepingIdentityAndBelongings()
        {
            var before = W.Player;
            Assert.That(before.Rpg.State, Is.EqualTo(CreationState.Pending));
            Assert.That(_s.GetCharacter().NeedsCreation, Is.True);
            var tunics = before.Equipment.Values.Concat(before.Inventory).Count(i => i.ItemId == "tunic_linen");

            var r = _s.Dispatch(new CreateCharacterCommand(Fighter()));
            Assert.That(r.IsSuccess, Is.True, r.ToString());
            var p = W.Player;
            Assert.That(p.CharacterId, Is.EqualTo(before.CharacterId), "Same identity.");
            Assert.That(p.Pose, Is.EqualTo(before.Pose));
            Assert.That(p.LocationKind, Is.EqualTo(before.LocationKind));
            Assert.That(p.Name, Is.EqualTo("Wren Ashdown"));
            Assert.That(p.Species, Is.EqualTo("Human"));
            Assert.That(p.Background, Is.EqualTo("Soldier"));
            Assert.That(p.Rpg.State, Is.EqualTo(CreationState.Complete));
            Assert.That(p.Rpg.Level, Is.EqualTo(1));
            Assert.That(p.Rpg.Experience, Is.Zero);
            Assert.That(p.Rpg.CurrentHp, Is.EqualTo(new CharacterRules(p.Rpg).MaxHp), "Starts at full health.");
            Assert.That(p.Rpg.Resources["second_wind"], Is.EqualTo(2));
            Assert.That(p.Purse, Is.EqualTo(before.Purse + RpgCatalog.FindBackground("soldier").Coin), "Background coin added to the purse.");

            // The kit: chain shirt and sword-and-shield worn; the tunic put away, not duplicated; crossbow and dice packed.
            Assert.That(p.Equipped(EquipmentSlot.Chest).ItemId, Is.EqualTo("chain_shirt"));
            Assert.That(p.Equipped(EquipmentSlot.MainHand).ItemId, Is.EqualTo("sword_arming"));
            Assert.That(p.Equipped(EquipmentSlot.OffHand).ItemId, Is.EqualTo("shield_round"));
            Assert.That(p.Equipment.Values.Concat(p.Inventory).Count(i => i.ItemId == "tunic_linen"), Is.EqualTo(tunics), "Belongings are kept, not duplicated.");
            Assert.That(p.Inventory.Select(i => i.ItemId), Has.Member("light_crossbow").And.Member("dice_set").And.Member("dagger_iron"));
            Assert.That(W.HistoricalEvents.Last().EventType, Is.EqualTo(HistoricalEventType.PlayerCharacterCreated));
            Assert.That(EventFormatter.Format(W.HistoricalEvents.Last(), id => null), Does.Contain("Wren Ashdown, a human soldier, set out as a fighter"));

            // Never twice.
            var json = Json();
            var again = _s.Dispatch(new CreateCharacterCommand(Fighter()));
            Assert.That(again.Status, Is.EqualTo(CommandStatus.InvalidSessionState));
            Assert.That(Json(), Is.EqualTo(json), "No second kit; nothing changes.");
            Assert.That(W.Player.Inventory.Count(i => i.ItemId == "light_crossbow"), Is.EqualTo(1));
        }

        [Test]
        public void InvalidCreations_AreRefused_AndChangeNothing()
        {
            var c = Fighter();
            c.SpeciesId = "minotaur";
            AssertRejectedAndUnchanged(c, "species");

            c = Fighter();
            c.Choices[ClassDefinition.SkillChoiceId] = new List<string> { nameof(Skill.Perception) };
            AssertRejectedAndUnchanged(c, "chooses 2 skills");

            c = Fighter();
            c.Choices[ClassDefinition.SkillChoiceId] = new List<string> { nameof(Skill.Perception), nameof(Skill.Arcana) };
            AssertRejectedAndUnchanged(c, "not a Fighter skill");

            c = Fighter();
            c.Choices[ClassDefinition.SkillChoiceId] = new List<string> { nameof(Skill.Perception), nameof(Skill.Perception) };
            AssertRejectedAndUnchanged(c, "chosen twice");

            c = Fighter();
            c.Choices[ClassDefinition.SkillChoiceId] = new List<string> { nameof(Skill.Perception), nameof(Skill.Athletics) };
            AssertRejectedAndUnchanged(c, "already granted");

            c = Fighter();
            c.Method = AbilityMethod.PointBuy;
            c.BaseScores = new[] { 15, 15, 15, 15, 8, 8 };
            AssertRejectedAndUnchanged(c, "of 27 points");

            c = Fighter();
            c.Choices.Remove("class:fighting_style");
            AssertRejectedAndUnchanged(c, "Fighting Style");

            c = Fighter();
            c.Choices["equipment:weapon"] = new List<string> { "a_dragon" };
            AssertRejectedAndUnchanged(c, "not an option");

            c = Fighter();
            c.Name = " ";
            AssertRejectedAndUnchanged(c, "name");

            c = Fighter();
            c.SpeciesId = "dragonborn";
            c.Choices.Remove("species:skillful");
            c.Choices["species:ancestry"] = new List<string> { "fire" };
            c.Appearance = new PlayerAppearanceData { Frame = NpcSex.Male, HairId = "Hair_SimpleParted", BeardId = "Hair_Beard" };
            AssertRejectedAndUnchanged(c, "beard");
        }

        [Test]
        public void CreatedCharacter_SurvivesSaveAndLoadExactly()
        {
            Assert.That(_s.Dispatch(new CreateCharacterCommand(Fighter())).IsSuccess, Is.True);
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);
            var back = serializer.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(World(serializer.Serialize(back.World)), Is.EqualTo(World(json)), "Exact round trip.");
            var s = back.World.Player.Rpg;
            Assert.That(s.ClassId, Is.EqualTo("fighter"));
            Assert.That(s.Chosen("class:fighting_style"), Is.EqualTo(new[] { "defense" }));
            Assert.That(s.BaseScores, Is.EqualTo(W.Player.Rpg.BaseScores));
            Assert.That(s.StartingEquipmentGranted, Is.True);
            Assert.That(back.World.Player.Inventory.Count(i => i.ItemId == "light_crossbow"), Is.EqualTo(1), "Loading grants nothing again.");
            Assert.That(JObject.Parse(json)["world"]["player"]["rpg"]["maxHp"], Is.Null, "Derived values are not saved.");

            var corrupt = JObject.Parse(json);
            ((JObject)corrupt["world"]["player"]).Remove("rpg");
            Assert.That(serializer.Deserialize(corrupt.ToString()).IsSuccess, Is.False, "A v8 save without a character record is refused, not silently re-created.");
        }

        [Test]
        public void Version7Save_MigratesToALegacyCharacter_KeepingEverything()
        {
            var serializer = new JsonWorldSerializer();
            var original = Phase2Worlds.Generated(2468);
            original.Player.Purse = 77;
            original.Player.Appearance.HairColour = 4;
            original.Player.Name = "Old Traveller";
            var doc = JObject.Parse(serializer.Serialize(original));
            Phase2Worlds.StripToV7(doc);
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var p = loaded.World.Player;
            Assert.That(loaded.World.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(p.Rpg.State, Is.EqualTo(CreationState.Legacy), "Nothing assigned or invented.");
            Assert.That(p.Rpg.SpeciesId, Is.Null);
            Assert.That(p.Rpg.ClassId, Is.Null);
            Assert.That(p.Rpg.StartingEquipmentGranted, Is.False);
            Assert.That(p.CharacterId, Is.EqualTo(original.Player.CharacterId));
            Assert.That(p.Name, Is.EqualTo("Old Traveller"));
            Assert.That(p.Purse, Is.EqualTo(77));
            Assert.That(p.Appearance.HairColour, Is.EqualTo(4));
            Assert.That(p.Pose, Is.EqualTo(original.Player.Pose));
            Assert.That(p.Inventory.Select(i => i.StackId), Is.EqualTo(original.Player.Inventory.Select(i => i.StackId)));
            Assert.That(p.Equipment.Values.Select(i => i.StackId), Is.EquivalentTo(original.Player.Equipment.Values.Select(i => i.StackId)));
            p.Rpg.State = CreationState.Pending;
            Assert.That(World(serializer.Serialize(loaded.World)), Is.EqualTo(World(serializer.Serialize(original))), "Only the character record was added.");

            // Once only: loading the migrated save again changes nothing.
            p.Rpg.State = CreationState.Legacy;
            var again = serializer.Deserialize(serializer.Serialize(loaded.World));
            Assert.That(World(serializer.Serialize(again.World)), Is.EqualTo(World(serializer.Serialize(loaded.World))));
        }

        [Test]
        public void LegacyPlayer_CanCompleteCreationOnce_WithoutLosingWhatTheyHad()
        {
            var w = W;
            w.Player.Rpg.State = CreationState.Legacy;
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, "legacy" + JsonWorldStore.Extension);
            File.WriteAllText(path, new JsonWorldSerializer().Serialize(w));
            Assert.That(_s.Execute(new LoadWorldCommand("legacy")).IsSuccess, Is.True);
            var stacks = W.Player.Inventory.Select(i => i.StackId).ToList();
            Assert.That(_s.Dispatch(new CreateCharacterCommand(Fighter())).IsSuccess, Is.True);
            Assert.That(stacks.All(id => W.Player.Inventory.Any(i => i.StackId == id) || W.Player.Equipment.Values.Any(i => i.StackId == id)), Is.True,
                "Everything carried before is still owned.");
            Assert.That(W.Player.Rpg.State, Is.EqualTo(CreationState.Complete));
        }

        // ----- The creator's draft --------------------------------------------------------------------

        [Test]
        public void Draft_KeepsChoicesConsistent_AndSaysWhatEachStepNeeds()
        {
            var d = new CharacterDraft();
            Assert.That(d.Problems(CreationStep.Class), Has.Member("Choose a class."));
            d.SetClass("rogue");
            Assert.That(d.Picked("equipment:weapon"), Is.EqualTo(new[] { "rapier" }), "Each kit group starts on its first option.");
            Assert.That(d.Choices.BaseScores[(int)Ability.Dexterity], Is.EqualTo(15), "Scores arranged for the class.");
            Assert.That(d.Problems(CreationStep.Skills), Has.Some.Contains("Choose 4 Rogue skills"));
            foreach (var s in new[] { Skill.Stealth, Skill.Acrobatics, Skill.Perception, Skill.Insight }) Assert.That(d.Toggle(ClassDefinition.SkillChoiceId, s.ToString()), Is.True);
            Assert.That(d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Deception)), Is.False, "No fifth skill.");
            Assert.That(d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Arcana)), Is.False, "Not a rogue skill.");

            // A background that grants a chosen skill removes it from the picks.
            d.SetBackground("criminal");
            Assert.That(d.Picked(ClassDefinition.SkillChoiceId), Does.Not.Contain(nameof(Skill.Stealth)), "Criminal grants Stealth.");
            Assert.That(d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Stealth)), Is.False);

            // Point buy within the budget.
            d.SetMethod(AbilityMethod.PointBuy);
            Assert.That(d.PointsLeft, Is.EqualTo(27));
            for (var i = 0; i < 7; i++) Assert.That(d.Adjust(Ability.Dexterity, 1), Is.True);
            Assert.That(d.Adjust(Ability.Dexterity, 1), Is.False, "15 is the most point buy allows.");
            Assert.That(d.PointsLeft, Is.EqualTo(18));
            Assert.That(d.Problems(CreationStep.Abilities), Is.Empty);

            // A new class clears the old class's choices.
            d.SetClass("wizard");
            Assert.That(d.Picked(ClassDefinition.SkillChoiceId), Is.Empty);
            Assert.That(d.Choices.Choices.ContainsKey("class:expertise"), Is.False);
            Assert.That(d.Back(), Is.False, "Already at the first step.");
            d.GoTo(CreationStep.Review);
            Assert.That(d.Next(), Is.False);
            Assert.That(d.Problems(CreationStep.Review), Is.Not.Empty);
        }

        [Test]
        public void EveryClassAndSpecies_CanBeCreated_FromItsDefaults()
        {
            foreach (var cls in RpgCatalog.Classes)
            foreach (var species in RpgCatalog.Species)
            {
                var d = new CharacterDraft();
                d.SetSpecies(species.Id);
                d.SetBackground(RpgCatalog.Backgrounds[(cls.Id.Length + species.Id.Length) % 12].Id);
                d.SetClass(cls.Id);
                foreach (var c in species.Choices.Concat(cls.Choices).Where(c => c.Kind == ChoiceKind.Option)) d.Toggle(c.Id, c.Options[0].Id);
                foreach (var s in cls.SkillOptions) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
                foreach (var c in species.Choices.Where(c => c.Kind == ChoiceKind.Skill)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
                foreach (var c in cls.Choices.Where(c => c.Kind == ChoiceKind.Expertise)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
                if (!species.Appearance.AllowsBeard) d.Choices.Appearance.BeardId = null;
                Assert.That(d.Problems(CreationStep.Review), Is.Empty, $"{species.Name} {cls.Name}");
            }
        }

        // ----- Effects on the world and validator mutations -------------------------------------------

        [Test]
        public void MerchantBackground_BargainsInShops_AndGoliathCarriesMore()
        {
            var w = W;
            var shop = w.Shops.First(s => s.Stock.Count > 0);
            var item = ItemCatalog.Find(shop.Stock[0].ItemId);
            var before = CommerceMath.SellingPrice(w, shop, item);
            var d = new CharacterDraft();
            d.SetSpecies("goliath");
            d.SetBackground("merchant");
            d.SetClass("fighter");
            d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Perception));
            d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Survival));
            d.Toggle("class:fighting_style", "archery");
            Assert.That(_s.Dispatch(d.ToCommand()).IsSuccess, Is.True);
            var after = CommerceMath.SellingPrice(W, W.FindShop(shop.ShopId), item);
            Assert.That(after.Factors.Select(f => f.ToString()), Has.Member("Trade connections -5%"));
            Assert.That(before.Factors.Select(f => f.Label), Has.No.Member("Trade connections"));
            Assert.That(PlayerMovement.UnencumberedFor(W.Player), Is.EqualTo(2 * PlayerMovement.UnencumberedGrams));
            Assert.That(_s.GetPlayerHub().EncumbranceGrams, Is.EqualTo(50000));
        }

        [Test]
        public void Validator_RejectsBrokenCharacters()
        {
            Assert.That(_s.Dispatch(new CreateCharacterCommand(Fighter())).IsSuccess, Is.True);

            void Broken(Action<CharacterSheetData> breakIt, string expect)
            {
                var w = W;
                breakIt(w.Player.Rpg);
                var v = WorldValidator.Validate(w);
                Assert.That(v.IsValid, Is.False, expect);
                Assert.That(string.Join("; ", v.Errors), Does.Contain(expect));
            }

            Assert.That(WorldValidator.Validate(W).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(W).Errors));
            Broken(s => s.SpeciesId = "unicorn", "Unknown species");
            Broken(s => s.ClassId = "necromancer", "Unknown class");
            Broken(s => s.BackgroundId = "astronaut", "Unknown background");
            Broken(s => s.Level = 0, "impossible");
            Broken(s => s.Level = 21, "impossible");
            Broken(s => s.BaseScores[0] = 18, "standard array");
            Broken(s => s.Choices[ClassDefinition.SkillChoiceId] = new List<string> { "Survival", "Survival" }, "twice");
            Broken(s => s.CurrentHp = 99, "Hit points");
            Broken(s => s.Resources["second_wind"] = 9, "Second Wind");
            Broken(s => s.Resources["ki"] = 1, "Unknown resource");
            Broken(s => s.StartingEquipmentGranted = false, "starting equipment");
            Broken(s => s.Choices["class:surprise"] = new List<string> { "x" }, "Unknown creation choice");
            Broken(s => { s.State = CreationState.Pending; }, "not yet created");
        }
    }
}
