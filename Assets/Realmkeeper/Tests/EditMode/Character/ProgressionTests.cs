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
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>Phase 7: experience, advancement through levels 1–20 for all twelve classes, feats, subclasses, resources, the rules engine, persistence and migration.</summary>
    public sealed class ProgressionTests
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
        private CharacterSheetData Sheet => W.Player.Rpg;
        private string Json() => ((JObject)JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"]).ToString();

        // ----- Helpers -----------------------------------------------------------------------------------

        /// <summary>A valid level-1 character of the class (human, first options everywhere), optionally with base scores.</summary>
        public static CharacterCreationChoices Choices(string classId, string species = "human", string background = "hermit", int[] scores = null)
        {
            var cls = RpgCatalog.FindClass(classId);
            var sp = RpgCatalog.FindSpecies(species);
            var d = new CharacterDraft();
            d.Choices.Name = "Tester " + classId;
            d.SetSpecies(species);
            d.SetBackground(background);
            d.SetClass(classId);
            if (scores != null)
            {
                d.SetMethod(AbilityMethod.PointBuy);
                for (var i = 0; i < 6; i++) d.Choices.BaseScores[i] = scores[i];
            }

            foreach (var c in sp.Choices.Concat(cls.Choices).Where(c => c.Kind == ChoiceKind.Option)) d.Toggle(c.Id, c.Options[0].Id);
            foreach (var s in cls.SkillOptions) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var c in sp.Choices.Where(c => c.Kind == ChoiceKind.Skill)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
            foreach (var c in cls.Choices.Where(c => c.Kind == ChoiceKind.Expertise)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
            if (!sp.Appearance.AllowsBeard) d.Choices.Appearance.BeardId = null;
            Assert.That(d.Problems(CreationStep.Review), Is.Empty, classId);
            return d.Clean();
        }

        private void Create(string classId, string species = "human", int[] scores = null)
        {
            var r = _s.Dispatch(new CreateCharacterCommand(Choices(classId, species, "hermit", scores)));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
        }

        private void Award(int toLevel)
        {
            var need = ProgressionRules.ExperienceFor(toLevel) - Sheet.Experience;
            if (need > 0) Assert.That(_s.Dispatch(new AwardExperienceCommand(need, ExperienceSource.Milestone, null, "test")).IsSuccess, Is.True);
        }

        private void AdvanceTo(int level)
        {
            Award(level);
            while (Sheet.Level < level)
            {
                var choices = CharacterAdvancement.SuggestChoices(Sheet, Sheet.Level + 1);
                var r = _s.Dispatch(new AdvanceCharacterCommand(choices));
                Assert.That(r.IsSuccess, Is.True, $"{Sheet.ClassId} → {Sheet.Level + 1}: {r.Message} {string.Join("; ", r.Errors)}");
            }
        }

        private static CharacterSheetData StandaloneSheet(string classId, string species = "human")
        {
            var c = Choices(classId, species);
            var sheet = CharacterCommandHandlers.BuildSheet(c, out var errors);
            Assert.That(errors, Is.Empty);
            return sheet;
        }

        private static CharacterSheetData AdvanceSheet(CharacterSheetData sheet, int level)
        {
            sheet.Experience = 0;
            sheet.ExperienceLog.Clear();
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "test", Source = ExperienceSource.Other, Amount = ProgressionRules.ExperienceFor(level) });
            sheet.Experience = ProgressionRules.ExperienceFor(level);
            while (sheet.Level < level)
            {
                var next = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, sheet.Level + 1) }, default, out var errors);
                Assert.That(next, Is.Not.Null, $"{sheet.ClassId} → {sheet.Level + 1}: " + string.Join("; ", errors));
                sheet = next;
            }

            return sheet;
        }

        // ----- Experience --------------------------------------------------------------------------------

        [Test]
        public void ExperienceThresholds_AreTheSrdTable()
        {
            Assert.That(ProgressionRules.Thresholds, Is.EqualTo(new[] { 0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000, 85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000 }));
            Assert.That(ProgressionRules.LevelFor(0), Is.EqualTo(1));
            Assert.That(ProgressionRules.LevelFor(299), Is.EqualTo(1));
            Assert.That(ProgressionRules.LevelFor(300), Is.EqualTo(2));
            Assert.That(ProgressionRules.LevelFor(6499), Is.EqualTo(4));
            Assert.That(ProgressionRules.LevelFor(355000), Is.EqualTo(20));
            Assert.That(ProgressionRules.LevelFor(9000000), Is.EqualTo(20));
            Assert.That(ProgressionRules.NextThreshold(20), Is.Null);
            foreach (var (level, bonus) in new[] { (1, 2), (4, 2), (5, 3), (8, 3), (9, 4), (13, 5), (17, 6), (20, 6) })
                Assert.That(AbilityRules.ProficiencyBonus(level), Is.EqualTo(bonus), $"level {level}");
        }

        [Test]
        public void Experience_IsAwardedThroughTheLedger_OnceperKey_AndNeverLevelsUpByItself()
        {
            Create("fighter");
            var before = Json();
            foreach (var bad in new[] { 0, -5, ProgressionRules.MaxAward + 1 })
                Assert.That(_s.Dispatch(new AwardExperienceCommand(bad, ExperienceSource.Quest, "q1")).IsSuccess, Is.False, $"amount {bad}");
            Assert.That(Json(), Is.EqualTo(before), "Refused awards change nothing.");

            var r = _s.Dispatch(new AwardExperienceCommand(200, ExperienceSource.Quest, "rescue-the-miller", "Rescued the miller"));
            Assert.That(r.IsSuccess, Is.True, r.Message);
            var dup = _s.Dispatch(new AwardExperienceCommand(200, ExperienceSource.Quest, "rescue-the-miller"));
            Assert.That(dup.IsSuccess, Is.False, "The same quest never pays twice.");
            Assert.That(dup.Message, Does.Contain("already given"));
            Assert.That(_s.Dispatch(new AwardExperienceCommand(50, ExperienceSource.Encounter, null, "Wolves")).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new AwardExperienceCommand(50, ExperienceSource.Encounter, null, "Wolves again")).IsSuccess, Is.True, "Unkeyed awards are separate events.");
            Assert.That(_s.Dispatch(new AwardExperienceCommand(10, ExperienceSource.Discovery, null, null, "discovery:old-mill")).IsSuccess, Is.True);

            var s = Sheet;
            Assert.That(s.Experience, Is.EqualTo(310));
            Assert.That(s.ExperienceLog.Sum(a => a.Amount), Is.EqualTo(s.Experience));
            Assert.That(s.ExperienceLog.Select(a => a.Key).Distinct().Count(), Is.EqualTo(s.ExperienceLog.Count));
            Assert.That(s.ExperienceLog[0].Key, Is.EqualTo("quest:rescue-the-miller"));
            Assert.That(s.ExperienceLog[0].Reason, Is.EqualTo("Rescued the miller"));
            Assert.That(s.ExperienceLog[0].Date, Is.EqualTo(W.CurrentSimulationDate));
            Assert.That(s.Level, Is.EqualTo(1), "Experience makes a character eligible; it does not level them.");
            Assert.That(_s.GetCharacter().CanAdvance, Is.True);
            Assert.That(_s.GetCharacter().NextLevelExperience, Is.EqualTo(300));
        }

        [Test]
        public void Experience_BeforeCreation_IsKept_AndCountsOnceCreated()
        {
            Assert.That(Sheet.State, Is.EqualTo(CreationState.Pending));
            Assert.That(_s.Dispatch(new AwardExperienceCommand(400, ExperienceSource.Story, "prologue")).IsSuccess, Is.True);
            Assert.That(_s.GetCharacter().CanAdvance, Is.False, "Nothing to advance before creation.");
            Assert.That(_s.Dispatch(new AdvanceCharacterCommand(new LevelChoices { Level = 2 })).IsSuccess, Is.False);
            Create("rogue");
            Assert.That(Sheet.Experience, Is.EqualTo(400));
            Assert.That(Sheet.ExperienceLog.Single().Key, Is.EqualTo("story:prologue"));
            Assert.That(_s.GetCharacter().CanAdvance, Is.True);
        }

        // ----- Advancement -------------------------------------------------------------------------------

        [Test]
        public void Advancement_NeedsExperience_IsAtomic_AndKeepsDamageAndSpentResources()
        {
            Create("fighter", "human", new[] { 15, 10, 14, 10, 10, 10 });
            var refused = _s.Dispatch(new AdvanceCharacterCommand(new LevelChoices { Level = 2 }));
            Assert.That(refused.IsSuccess, Is.False);
            Assert.That(refused.Message, Does.Contain("300"));

            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-5)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new SpendResourceCommand("second_wind")).IsSuccess, Is.True);
            var hpBefore = Sheet.CurrentHp;
            var maxBefore = new CharacterRules(Sheet).MaxHp;
            Award(2);
            var before = Json();
            Assert.That(_s.Dispatch(new AdvanceCharacterCommand(new LevelChoices { Level = 3 })).IsSuccess, Is.False, "Levels are taken in order.");
            Assert.That(Json(), Is.EqualTo(before));

            var r = _s.Dispatch(new AdvanceCharacterCommand(new LevelChoices { Level = 2 }));
            Assert.That(r.IsSuccess, Is.True, r.Message + string.Join("; ", r.Errors));
            var rules = new CharacterRules(Sheet);
            var con = rules.Modifier(Ability.Constitution);
            Assert.That(rules.MaxHp, Is.EqualTo(maxBefore + 6 + con), "d10 average (6) + Constitution.");
            Assert.That(Sheet.CurrentHp, Is.EqualTo(hpBefore + 6 + con), "Current hit points rise by the gain; damage is not healed.");
            Assert.That(Sheet.Resources["second_wind"], Is.EqualTo(1), "Spent uses stay spent.");
            Assert.That(Sheet.Resources["action_surge"], Is.EqualTo(1), "A new resource arrives full.");
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(2));
            Assert.That(Sheet.Advancements.Single().Level, Is.EqualTo(2));
            Assert.That(Sheet.Advancements[0].HitPointsGained, Is.EqualTo(6 + con));
            Assert.That(Sheet.Advancements[0].Summary, Does.Contain("Action Surge"));
            Assert.That(W.HistoricalEvents.Last().EventType, Is.EqualTo(HistoricalEventType.PlayerLevelAdvanced));
            Assert.That(rules.Features.Select(f => f.feature.Name), Has.Member("Action Surge").And.Member("Tactical Mind"));

            var again = _s.Dispatch(new AdvanceCharacterCommand(new LevelChoices { Level = 2 }));
            Assert.That(again.IsSuccess, Is.False, "A level is gained once.");
        }

        [Test]
        public void SeveralLevels_ApplyInOneTransaction_EachWithItsChoices()
        {
            Create("cleric");
            Award(5);
            var steps = new List<LevelChoices>();
            var sheet = Sheet;
            for (var level = 2; level <= 5; level++)
            {
                var c = CharacterAdvancement.SuggestChoices(sheet, level);
                steps.Add(c);
                sheet = CharacterAdvancement.Apply(sheet, new[] { c }, default, out var e);
                Assert.That(sheet, Is.Not.Null, string.Join("; ", e));
            }

            var missingSubclass = steps.Select(s => s.Clone()).ToList();
            missingSubclass[1].SubclassId = null;
            var before = Json();
            var bad = _s.Dispatch(new AdvanceCharacterCommand(missingSubclass.ToArray()));
            Assert.That(bad.IsSuccess, Is.False);
            Assert.That(string.Join(" ", bad.Errors), Does.Contain("Divine Domain"));
            Assert.That(Json(), Is.EqualTo(before), "A refused multi-level advance changes nothing.");

            var ok = _s.Dispatch(new AdvanceCharacterCommand(steps.ToArray()));
            Assert.That(ok.IsSuccess, Is.True, string.Join("; ", ok.Errors));
            Assert.That(Sheet.Level, Is.EqualTo(5));
            Assert.That(Sheet.SubclassId, Is.EqualTo("life"));
            Assert.That(Sheet.Advancements.Select(a => a.Level), Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(Sheet.Choices.ContainsKey("lv4:feat"), Is.True);
            Assert.That(Sheet.Resources["spell_slots_3"], Is.EqualTo(2));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.PlayerLevelAdvanced), Is.EqualTo(1), "One chronicle entry for the transaction.");
        }

        [Test]
        public void ChoicesAreNeverRepeatedOrOverwritten()
        {
            var s = AdvanceSheet(StandaloneSheet("rogue"), 6);
            s.ExperienceLog.Add(new ExperienceAward { Key = "more", Amount = 50000 });
            s.Experience += 50000;
            var replay = new LevelChoices { Level = 7 };
            replay.Choices["lv6:expertise"] = new List<string>(s.Chosen("lv6:expertise"));
            Assert.That(CharacterAdvancement.Apply(s, new[] { replay }, default, out var errors), Is.Null);
            Assert.That(string.Join(" ", errors), Does.Contain("already chosen"));
            var same = CharacterAdvancement.Apply(s, new[] { new LevelChoices { Level = 6 } }, default, out var again);
            Assert.That(same, Is.Null, "A level already held cannot be applied again.");
            Assert.That(string.Join(" ", again), Does.Contain("the next level is 7"), "Refused by the advancement rule itself, not only by the validator.");
            Assert.That(CharacterAdvancement.Apply(s, new[] { new LevelChoices { Level = 8 } }, default, out again), Is.Null, "Levels are not skipped.");
            var premature = CharacterAdvancement.SuggestChoices(s, 7);
            premature.Choices["lv8:feat"] = new List<string> { "alert" };
            Assert.That(CharacterAdvancement.Apply(s, new[] { premature }, default, out errors), Is.Null);
            Assert.That(string.Join(" ", errors), Does.Contain("not a choice at this level"));
        }

        // ----- All twelve classes ----------------------------------------------------------------------

        [Test]
        public void EveryClass_AdvancesFromOneToTwenty_ValidAtEveryLevel()
        {
            foreach (var cls in RpgCatalog.Classes)
            {
                var sheet = StandaloneSheet(cls.Id, cls.Id == "monk" ? "wood_elf" : "human");
                sheet.ExperienceLog.Add(new ExperienceAward { Key = "all", Amount = ProgressionRules.ExperienceFor(20) });
                sheet.Experience = ProgressionRules.ExperienceFor(20);
                var hp = new CharacterRules(sheet).MaxHp;
                for (var level = 2; level <= 20; level++)
                {
                    var next = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, level) }, default, out var errors);
                    Assert.That(next, Is.Not.Null, $"{cls.Name} {level}: {string.Join("; ", errors)}");
                    Assert.That(CharacterValidation.CheckSheet(next), Is.Empty);
                    var rules = new CharacterRules(next);
                    Assert.That(rules.MaxHp, Is.GreaterThan(hp), $"{cls.Name} {level} gains hit points");
                    hp = rules.MaxHp;
                    sheet = next;
                }

                var final = new CharacterRules(sheet);
                Assert.That(sheet.SubclassId, Is.Not.Null.And.EqualTo(RpgCatalog.SubclassesOf(cls.Id)[0].Id), cls.Name);
                Assert.That(final.ProficiencyBonus, Is.EqualTo(6));
                Assert.That(sheet.Choices.Keys.Count(k => k.EndsWith(":feat", StringComparison.Ordinal) || k.EndsWith(":boon", StringComparison.Ordinal)), Is.EqualTo(cls.FeatLevels.Count + 1), $"{cls.Name} feats");
                Assert.That(sheet.Resources.Keys, Is.EquivalentTo(final.ResourceMaximums().Keys), $"{cls.Name} resources");
                foreach (var pair in final.ResourceMaximums()) Assert.That(sheet.Resources[pair.Key], Is.EqualTo(pair.Value), $"{cls.Name} {pair.Key} full");
                foreach (var sub in RpgCatalog.SubclassesOf(cls.Id))
                    foreach (var pair in sub.Features)
                        foreach (var f in pair.Value) Assert.That(final.HasFeature(f.Id) || final.ActiveFeatures.Any(a => a.Feature.Replaces == f.Id), $"{cls.Name}: {f.Name}");
            }
        }

        [Test]
        public void ClassProgressions_AreDistinct()
        {
            var signatures = RpgCatalog.Classes.Select(c => string.Join(",", Enumerable.Range(2, 19)
                .SelectMany(l => c.LaterFeatures.TryGetValue(l, out var fs) ? fs.Select(f => l + ":" + f.Id) : Enumerable.Empty<string>()))).ToList();
            Assert.That(signatures.Distinct().Count(), Is.EqualTo(12));
            foreach (var c in RpgCatalog.Classes)
                Assert.That(c.LaterFeatures.Values.Sum(v => v.Count(f => !f.Id.StartsWith("feat_", StringComparison.Ordinal) && f.Id != "epic_boon" && f.Id != "subclass")), Is.GreaterThanOrEqualTo(4), c.Name + " (the wizard has the fewest: four, per SRD 5.2.1)");
        }

        [TestCase("barbarian", 9, "rage", 4)]
        [TestCase("barbarian", 17, "rage", 6)]
        [TestCase("fighter", 2, "action_surge", 1)]
        [TestCase("fighter", 17, "action_surge", 2)]
        [TestCase("fighter", 13, "indomitable", 2)]
        [TestCase("fighter", 10, "second_wind", 4)]
        [TestCase("cleric", 6, "channel_divinity", 3)]
        [TestCase("cleric", 18, "channel_divinity", 4)]
        [TestCase("druid", 17, "wild_shape", 4)]
        [TestCase("monk", 11, "focus_points", 11)]
        [TestCase("paladin", 11, "channel_divinity", 3)]
        [TestCase("paladin", 7, "lay_on_hands", 35)]
        [TestCase("ranger", 13, "favored_enemy", 5)]
        [TestCase("sorcerer", 9, "sorcery_points", 9)]
        [TestCase("warlock", 11, "pact_slots", 3)]
        [TestCase("warlock", 17, "pact_slots", 4)]
        [TestCase("wizard", 17, "spell_slots_9", 1)]
        [TestCase("wizard", 5, "spell_slots_3", 2)]
        [TestCase("paladin", 9, "spell_slots_3", 2)]
        [TestCase("ranger", 17, "spell_slots_5", 1)]
        [TestCase("bard", 12, "spell_slots_6", 1)]
        public void ResourceMaximums_FollowTheClassTables(string cls, int level, string resource, int expected)
        {
            var rules = new CharacterRules(AdvanceSheet(StandaloneSheet(cls), level));
            Assert.That(rules.ResourceMax(rules.FindResource(resource)), Is.EqualTo(expected));
        }

        [Test]
        public void ClassMilestones_ScaleAndReplaceFeatures()
        {
            var rogue = new CharacterRules(AdvanceSheet(StandaloneSheet("rogue"), 19));
            Assert.That(rogue.Scaling("sneak_attack"), Is.EqualTo(10));
            Assert.That(rogue.SaveProficient(Ability.Wisdom) && rogue.SaveProficient(Ability.Charisma), Is.True, "Slippery Mind at 15.");
            Assert.That(rogue.ProficientCheckFloor, Is.EqualTo(10), "Reliable Talent at 7.");
            Assert.That(rogue.Sheet.Chosen("lv6:expertise").Count, Is.EqualTo(2));

            var fighter = new CharacterRules(AdvanceSheet(StandaloneSheet("fighter"), 20));
            Assert.That(fighter.HasFeature("three_extra_attacks"), Is.True);
            Assert.That(fighter.HasFeature("two_extra_attacks") || fighter.HasFeature("extra_attack"), Is.False, "Replaced features stop applying.");
            Assert.That(fighter.HasFeature("superior_critical") && !fighter.HasFeature("improved_critical"), Is.True, "Champion's critical improves.");
            Assert.That(fighter.Sheet.Choices.Keys.Count(k => k.EndsWith(":feat", StringComparison.Ordinal)), Is.EqualTo(6), "Fighters gain extra feats at 6 and 14.");

            var monk = new CharacterRules(AdvanceSheet(StandaloneSheet("monk"), 17));
            Assert.That(monk.Scaling("martial_arts"), Is.EqualTo(12));
            Assert.That(AbilityRules.All.All(monk.SaveProficient), Is.True, "Disciplined Survivor at 14.");
            Assert.That(monk.Speed, Is.EqualTo(30 + 25), "Unarmoured Movement +25 at 14–17.");
            Assert.That(new CharacterRules(monk.Sheet, new Loadout(true, false, false)).Speed, Is.EqualTo(30), "Armour removes it.");

            var barbarian = new CharacterRules(AdvanceSheet(StandaloneSheet("barbarian", "human"), 20));
            Assert.That(barbarian.Score(Ability.Strength), Is.LessThanOrEqualTo(25));
            Assert.That(barbarian.Score(Ability.Strength), Is.GreaterThan(20), "Primal Champion passes 20.");
            Assert.That(barbarian.InitiativeRoll.HasAdvantage, Is.True, "Feral Instinct.");
            Assert.That(barbarian.Speed, Is.EqualTo(40), "Fast Movement.");

            var bard = new CharacterRules(AdvanceSheet(StandaloneSheet("bard"), 5));
            Assert.That(bard.RecoveryOf(bard.FindResource("bardic_inspiration")), Is.EqualTo(ResourceRecovery.ShortRest), "Font of Inspiration.");
            Assert.That(bard.Scaling("bardic_die"), Is.EqualTo(8));
            var untrained = SkillRules.All.First(sk => bard.SkillRank(sk) == ProficiencyRank.None);
            Assert.That(bard.SkillBonus(untrained).Parts.Any(p => p.label.Contains("Jack of All Trades") && p.value == 1), Is.True);

            var warlock = new CharacterRules(AdvanceSheet(StandaloneSheet("warlock"), 18));
            var invocations = warlock.ChoiceSlots.Where(c => c.Choice.Pool == "invocation").SelectMany(c => warlock.Sheet.Chosen(c.Choice.Id)).ToList();
            Assert.That(invocations.Count, Is.EqualTo(10), "Ten invocations by 18.");
            Assert.That(invocations.Distinct().Count(), Is.EqualTo(10));
            Assert.That(warlock.Scaling("pact_slot_level"), Is.EqualTo(5));
            Assert.That(warlock.SpellSlotText, Is.EqualTo("4 pact slots of level 5"));

            var wizard = new CharacterRules(AdvanceSheet(StandaloneSheet("wizard"), 2));
            Assert.That(wizard.Sheet.Chosen("lv2:scholar").Count, Is.EqualTo(1));
            Assert.That(wizard.SkillRank((Skill)Enum.Parse(typeof(Skill), wizard.Sheet.Chosen("lv2:scholar")[0])), Is.EqualTo(ProficiencyRank.Expertise));

            var paladin = new CharacterRules(AdvanceSheet(StandaloneSheet("paladin"), 6));
            var cha = Math.Max(1, paladin.Modifier(Ability.Charisma));
            Assert.That(paladin.SaveBonus(Ability.Dexterity).Parts.Any(p => p.label.Contains("Aura of Protection") && p.value == cha), Is.True);
        }

        // ----- Hit points -----------------------------------------------------------------------------

        [TestCase("wizard", 8, 5)]
        [TestCase("wizard", 15, 9)]
        [TestCase("barbarian", 15, 20)]
        [TestCase("fighter", 12, 11)]
        [TestCase("cleric", 14, 7)]
        public void HitPoints_FollowTheHitDieAndConstitution(string cls, int con, int level)
        {
            var scores = new[] { 13, 13, con, 10, 12, 10 };
            var sheet = AdvanceSheet(CharacterCommandHandlers.BuildSheet(WithScores(Choices(cls), scores), out _), level);
            var rules = new CharacterRules(sheet);
            var die = RpgCatalog.FindClass(cls).HitDie;
            var conMod = rules.Modifier(Ability.Constitution);
            var expected = die + (die / 2 + 1) * (level - 1) + conMod * level;
            Assert.That(rules.MaxHp, Is.EqualTo(expected), rules.MaxHitPoints().Text);
            Assert.That(sheet.Advancements.Sum(a => a.HitPointsGained) + new CharacterRules(StandaloneSheetWith(cls, scores)).MaxHp, Is.EqualTo(rules.MaxHp), "Recorded gains add up (ability improvements raise Constitution retroactively).");
        }

        private static CharacterCreationChoices WithScores(CharacterCreationChoices c, int[] scores)
        {
            c.Method = AbilityMethod.PointBuy;
            c.BaseScores = (int[])scores.Clone();
            return c;
        }

        private static CharacterSheetData StandaloneSheetWith(string cls, int[] scores) => CharacterCommandHandlers.BuildSheet(WithScores(Choices(cls), scores), out _);

        [Test]
        public void HitPoints_DamageHealingAndTemporary_FollowTheRules()
        {
            Create("barbarian");
            var max = new CharacterRules(Sheet).MaxHp;
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(5, true)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(3, true)).IsSuccess, Is.True);
            Assert.That(Sheet.TemporaryHp, Is.EqualTo(5), "Temporary hit points do not stack: the higher stays.");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-7)).IsSuccess, Is.True);
            Assert.That(Sheet.TemporaryHp, Is.EqualTo(0));
            Assert.That(Sheet.CurrentHp, Is.EqualTo(max - 2), "Damage takes temporary hit points first.");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-500)).IsSuccess, Is.True);
            Assert.That(Sheet.CurrentHp, Is.EqualTo(0), "Hit points stop at 0.");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(1000)).IsSuccess, Is.True);
            Assert.That(Sheet.CurrentHp, Is.EqualTo(max), "Healing stops at the maximum.");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(0)).IsSuccess, Is.False);
        }

        // ----- Ability Score Improvements and feats -------------------------------------------------------

        private static CharacterSheetData AtLevel4WithFeat(string cls, string feat, Dictionary<string, List<string>> extra, out List<string> errors, int[] scores = null)
        {
            var sheet = AdvanceSheet(scores == null ? StandaloneSheet(cls) : StandaloneSheetWith(cls, scores), 3);
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "to4", Amount = 5000 });
            sheet.Experience += 5000;
            var step = new LevelChoices { Level = 4 };
            step.Choices["lv4:feat"] = new List<string> { feat };
            foreach (var pair in extra) step.Choices[pair.Key] = pair.Value;
            return CharacterAdvancement.Apply(sheet, new[] { step }, default, out errors);
        }

        [Test]
        public void AbilityScoreImprovement_RaisesScores_UpToTwenty()
        {
            var plusTwo = AtLevel4WithFeat("fighter", "ability_score_improvement", new Dictionary<string, List<string>> { ["lv4:feat:abilities"] = new List<string> { "Strength", "Strength" } }, out var e1, new[] { 15, 10, 14, 10, 10, 10 });
            Assert.That(plusTwo, Is.Not.Null, string.Join("; ", e1));
            var r = new CharacterRules(plusTwo);
            Assert.That(r.Score(Ability.Strength), Is.EqualTo(15 + 1 + 2), "Human +1, then +2.");
            Assert.That(r.ScoreBreakdown(Ability.Strength).Text, Does.Contain("Ability Score Improvement"));

            var split = AtLevel4WithFeat("fighter", "ability_score_improvement", new Dictionary<string, List<string>> { ["lv4:feat:abilities"] = new List<string> { "Strength", "Constitution" } }, out var e2, new[] { 15, 10, 14, 10, 10, 10 });
            Assert.That(split, Is.Not.Null, string.Join("; ", e2));
            Assert.That(new CharacterRules(split).Score(Ability.Constitution), Is.EqualTo(16));

            // A dwarf with Constitution 15 + 2 = 17 cannot reach 21 … but can reach 19; three points is one too many.
            var tooHigh = AtLevel4WithFeat("fighter", "ability_score_improvement", new Dictionary<string, List<string>> { ["lv4:feat:abilities"] = new List<string> { "Constitution", "Constitution", "Constitution" } }, out var e3);
            Assert.That(tooHigh, Is.Null);
            Assert.That(string.Join(" ", e3), Does.Contain("choose 2"));

            var past = AdvanceSheet(StandaloneSheetWith("barbarian", new[] { 15, 10, 15, 10, 8, 8 }), 16);
            var str = new CharacterRules(past).Score(Ability.Strength);
            Assert.That(str, Is.EqualTo(20), "Repeated improvements stop at 20.");
            var view = past.Clone();
            view.Choices["lv16:feat:abilities"] = new List<string> { "Strength", "Strength" };
            Assert.That(CharacterValidation.PickProblems(view, 16).Values.SelectMany(v => v).Any(p => p.Contains("would pass 20")), Is.True);
        }

        [Test]
        public void Feats_CheckPrerequisites_RepeatsAndFightingStyles()
        {
            var weak = AtLevel4WithFeat("wizard", "grappler", new Dictionary<string, List<string>> { ["lv4:feat:abilities"] = new List<string> { "Strength" } }, out var e1, new[] { 8, 10, 14, 15, 13, 10 });
            Assert.That(weak, Is.Null);
            Assert.That(string.Join(" ", e1), Does.Contain("Strength or Dexterity 13+"));
            var strong = AtLevel4WithFeat("fighter", "grappler", new Dictionary<string, List<string>> { ["lv4:feat:abilities"] = new List<string> { "Strength" } }, out var e2, new[] { 15, 10, 14, 10, 10, 10 });
            Assert.That(strong, Is.Not.Null, string.Join("; ", e2));

            var alert = AtLevel4WithFeat("rogue", "alert", new Dictionary<string, List<string>>(), out var e3);
            Assert.That(alert, Is.Not.Null, string.Join("; ", e3));
            var ar = new CharacterRules(alert);
            Assert.That(ar.Initiative().Total, Is.EqualTo(ar.Modifier(Ability.Dexterity) + 2), "Alert adds the proficiency bonus to initiative.");
            alert.ExperienceLog.Add(new ExperienceAward { Key = "to8", Amount = 40000 });
            alert.Experience += 40000;
            var toEight = alert;
            for (var l = 5; l <= 7; l++) toEight = CharacterAdvancement.Apply(toEight, new[] { CharacterAdvancement.SuggestChoices(toEight, l) }, default, out _);
            var twice = new LevelChoices { Level = 8 };
            twice.Choices["lv8:feat"] = new List<string> { "alert" };
            Assert.That(CharacterAdvancement.Apply(toEight, new[] { twice }, default, out var e4), Is.Null);
            Assert.That(string.Join(" ", e4), Does.Contain("only once"));

            var skilled = new LevelChoices { Level = 8 };
            skilled.Choices["lv8:feat"] = new List<string> { "magic_initiate" };
            skilled.Choices["lv8:feat:list"] = new List<string> { "wizard" };
            Assert.That(CharacterAdvancement.Apply(toEight, new[] { skilled }, default, out var e5), Is.Not.Null, string.Join("; ", e5));

            var style = AtLevel4WithFeat("wizard", "fs_archery", new Dictionary<string, List<string>>(), out var e6);
            Assert.That(style, Is.Null);
            Assert.That(string.Join(" ", e6), Does.Contain("not offered here"), "Only classes with a Fighting Style feature are offered style feats.");
            var dup = AtLevel4WithFeat("fighter", "fs_archery", new Dictionary<string, List<string>>(), out var e7);
            Assert.That(dup, Is.Null, "The fighter already took Archery at level 1 (its first option).");
            Assert.That(string.Join(" ", e7), Does.Contain("fighting style"));
            var other = AtLevel4WithFeat("fighter", "fs_two_weapon", new Dictionary<string, List<string>>(), out var e8);
            Assert.That(other, Is.Not.Null, string.Join("; ", e8));
        }

        [Test]
        public void EpicBoon_RaisesAScoreToThirty_AndGrantsItsEffect()
        {
            var sheet = AdvanceSheet(StandaloneSheet("wizard"), 18);
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "to19", Amount = 40000 });
            sheet.Experience += 40000;
            var step = new LevelChoices { Level = 19 };
            step.Choices["lv19:boon"] = new List<string> { "boon_truesight" };
            step.Choices["lv19:boon:abilities"] = new List<string> { "Intelligence" };
            var before = new CharacterRules(sheet).Score(Ability.Intelligence);
            var after = CharacterAdvancement.Apply(sheet, new[] { step }, default, out var errors);
            Assert.That(after, Is.Not.Null, string.Join("; ", errors));
            var rules = new CharacterRules(after);
            Assert.That(rules.Score(Ability.Intelligence), Is.EqualTo(before + 1));
            Assert.That(rules.Senses, Has.Member(("truesight", 60)));
            var early = AtLevel4WithFeat("wizard", "boon_fate", new Dictionary<string, List<string>>(), out var e2);
            Assert.That(early, Is.Null, "Epic boons are not offered at level 4.");
        }

        // ----- Subclasses --------------------------------------------------------------------------------

        [Test]
        public void Subclass_IsChosenAtItsLevel_ForItsClassOnly_AndGrantsItsFeatures()
        {
            var sheet = AdvanceSheet(StandaloneSheet("sorcerer"), 2);
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "x", Amount = 100000 });
            sheet.Experience += 100000;
            var wrong = CharacterAdvancement.SuggestChoices(sheet, 3);
            wrong.SubclassId = "champion";
            Assert.That(CharacterAdvancement.Apply(sheet, new[] { wrong }, default, out var e1), Is.Null);
            Assert.That(string.Join(" ", e1), Does.Contain("not a Sorcerer subclass"));
            var none = CharacterAdvancement.SuggestChoices(sheet, 3);
            none.SubclassId = null;
            Assert.That(CharacterAdvancement.Apply(sheet, new[] { none }, default, out _), Is.Null);
            var early = AdvanceSheet(StandaloneSheet("sorcerer"), 1);
            early.ExperienceLog.Add(new ExperienceAward { Key = "y", Amount = 300 });
            early.Experience += 300;
            Assert.That(CharacterAdvancement.Apply(early, new[] { new LevelChoices { Level = 2, SubclassId = "draconic" } }, default, out _), Is.Null);

            var draconic = AdvanceSheet(StandaloneSheet("sorcerer"), 6);
            var rules = new CharacterRules(draconic);
            Assert.That(rules.Subclass.Id, Is.EqualTo("draconic"));
            Assert.That(rules.MaxHitPoints().Parts.Any(p => p.label.Contains("Draconic Resilience") && p.value == 6), Is.True, "+1 hit point per sorcerer level.");
            Assert.That(rules.ArmorClass(null).Text, Does.Contain("Charisma"), "Draconic Resilience: 10 + Dexterity + Charisma unarmoured.");
            Assert.That(rules.Resistances, Has.Count.EqualTo(1), "Elemental Affinity grants the chosen resistance.");

            var lore = new CharacterRules(AdvanceSheet(StandaloneSheet("bard"), 3));
            Assert.That(lore.Sheet.Chosen("lv3:lore_skills"), Has.Count.EqualTo(3));
            foreach (var s in lore.Sheet.Chosen("lv3:lore_skills")) Assert.That(lore.SkillRank((Skill)Enum.Parse(typeof(Skill), s)), Is.Not.EqualTo(ProficiencyRank.None));

            var openHand = new CharacterRules(AdvanceSheet(StandaloneSheet("monk", "wood_elf"), 6));
            Assert.That(openHand.FindResource("wholeness_of_body"), Is.Not.Null, "Subclass resources arrive at their level.");
        }

        // ----- Outstanding choices (rules v7) -------------------------------------------------------------

        [Test]
        public void PhaseSixDruid_StaysValid_AndMakesItsPrimalOrderAtTheNextLevel()
        {
            var druid = StandaloneSheet("druid");
            druid.Choices.Remove("class:primal_order");
            druid.RulesVersion = 6;
            Assert.That(CharacterValidation.CheckSheet(druid), Is.Empty, "A Phase 6 druid without Primal Order is valid.");
            Assert.That(CharacterProgression.Outstanding(druid).Select(c => c.Choice.Id), Is.EqualTo(new[] { "class:primal_order" }));
            var newer = druid.Clone();
            newer.RulesVersion = 7;
            Assert.That(CharacterValidation.CheckSheet(newer), Is.Not.Empty, "A rules-v7 druid must have it.");

            druid.ExperienceLog.Add(new ExperienceAward { Key = "x", Amount = 300 });
            druid.Experience = 300;
            Assert.That(CharacterAdvancement.Apply(druid, new[] { new LevelChoices { Level = 2 } }, default, out var errors), Is.Null);
            Assert.That(string.Join(" ", errors), Does.Contain("class:primal_order"));
            var suggested = CharacterAdvancement.SuggestChoices(druid, 2);
            Assert.That(suggested.Choices["class:primal_order"], Is.EqualTo(new[] { "magician" }));
            var advanced = CharacterAdvancement.Apply(druid, new[] { suggested }, default, out errors);
            Assert.That(advanced, Is.Not.Null, string.Join("; ", errors));
            Assert.That(advanced.RulesVersion, Is.EqualTo(ProgressionRules.CurrentRulesVersion));
            Assert.That(new CharacterRules(advanced).SkillBonus(Skill.Nature).Parts.Any(p => p.label.Contains("Primal Order")), Is.True, "Magician adds Wisdom to Nature.");
        }

        // ----- Resources -----------------------------------------------------------------------------------

        [Test]
        public void Resources_SpendRestoreAndRecover_ThroughCommands()
        {
            Create("warlock");
            AdvanceTo(3);
            var before = Json();
            Assert.That(_s.Dispatch(new SpendResourceCommand("rage")).IsSuccess, Is.False, "Not a warlock resource.");
            Assert.That(_s.Dispatch(new SpendResourceCommand("pact_slots", 3)).IsSuccess, Is.False, "Only two slots.");
            Assert.That(_s.Dispatch(new SpendResourceCommand("pact_slots", 0)).IsSuccess, Is.False);
            Assert.That(Json(), Is.EqualTo(before), "Refused spending changes nothing.");
            Assert.That(_s.Dispatch(new SpendResourceCommand("pact_slots", 2)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new SpendResourceCommand("magical_cunning")).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new SpendResourceCommand("hit_dice", 3)).IsSuccess, Is.True);
            Assert.That(Sheet.Resources["pact_slots"], Is.EqualTo(0));

            Assert.That(_s.Dispatch(new RecoverResourcesCommand(ResourceRecovery.ShortRest)).IsSuccess, Is.True);
            Assert.That(Sheet.Resources["pact_slots"], Is.EqualTo(2), "Pact slots return on a short rest.");
            Assert.That(Sheet.Resources["magical_cunning"], Is.EqualTo(0), "Long-rest resources do not.");
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(0));
            Assert.That(_s.Dispatch(new RecoverResourcesCommand(ResourceRecovery.LongRest)).IsSuccess, Is.True);
            Assert.That(Sheet.Resources["magical_cunning"], Is.EqualTo(1));
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(1), "Half the hit dice (3 / 2, rounded down) return on a long rest.");
            Assert.That(_s.Dispatch(new RestoreResourceCommand("hit_dice")).IsSuccess, Is.True);
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(3));
            Assert.That(_s.Dispatch(new RestoreResourceCommand("hit_dice", 5)).IsSuccess, Is.True);
            Assert.That(Sheet.Resources["hit_dice"], Is.EqualTo(3), "Restoring stops at the maximum.");
        }

        // ----- Rules engine --------------------------------------------------------------------------------

        [Test]
        public void RulesEngine_ExplainsStacksAndConditions()
        {
            var cleric = StandaloneSheet("cleric");
            cleric.Choices["class:divine_order"] = new List<string> { "thaumaturge" };
            var c = new CharacterRules(cleric);
            var arcana = c.SkillBonus(Skill.Arcana);
            Assert.That(arcana.Parts.Any(p => p.label.Contains("Thaumaturge") && p.value == Math.Max(1, c.Modifier(Ability.Wisdom))), Is.True, arcana.Text);

            var champion = new CharacterRules(AdvanceSheet(StandaloneSheet("fighter"), 3));
            Assert.That(champion.InitiativeRoll.HasAdvantage, Is.True, "Remarkable Athlete.");
            Assert.That(champion.SkillRoll(Skill.Athletics).HasAdvantage, Is.True);
            Assert.That(champion.Passive(Skill.Athletics).Total, Is.EqualTo(10 + champion.SkillBonus(Skill.Athletics).Total + 5), "Advantage adds 5 to a passive score.");
            Assert.That(champion.SkillRoll(Skill.Stealth).HasAdvantage, Is.False);

            var barbarian = new CharacterRules(AdvanceSheet(StandaloneSheet("barbarian"), 2));
            var dex = barbarian.SaveRoll(Ability.Dexterity);
            Assert.That(dex.HasAdvantage, Is.False, "Danger Sense has a condition: it is situational.");
            Assert.That(dex.Situational.Single(), Does.Contain("Danger Sense"));

            // Defence: +1 AC only while wearing armour (the fighter took it at level 1).
            var fighter = CharacterCommandHandlers.BuildSheet(Choices("fighter"), out _);
            fighter.Choices["class:fighting_style"] = new List<string> { "defense" };
            var p = new PlayerCharacterData { Rpg = fighter };
            var unarmoured = new CharacterRules(fighter).ArmorClass(p).Total;
            p.Equipment[EquipmentSlot.Chest] = new ItemStackData { ItemId = "chain_shirt", Quantity = 1 };
            var armoured = new CharacterRules(fighter).ArmorClass(p);
            var dexMod = new CharacterRules(fighter).Modifier(Ability.Dexterity);
            Assert.That(armoured.Total, Is.EqualTo(13 + Math.Min(dexMod, 2) + 1), "Phase 8 armour: chain shirt 13 + Dexterity (max 2) + Defence. " + armoured.Text);
            Assert.That(unarmoured, Is.EqualTo(10 + dexMod), "No armour: 10 + Dexterity, and Defence needs armour.");
            Assert.That(armoured.Text, Does.Contain("Fighting Style"));

            // Stacking: the same feature never counts twice; two sources of advantage plus one disadvantage cancel.
            var r = new RollMode();
            r.Advantage.Add("a");
            r.Advantage.Add("b");
            r.Disadvantage.Add("c");
            Assert.That(r.HasAdvantage || r.HasDisadvantage, Is.False);
            Assert.That(r.Text, Does.Contain("cancel"));
        }

        // ----- Persistence and migration -------------------------------------------------------------------

        [Test]
        public void AdvancedCharacter_SurvivesSaveAndLoadExactly()
        {
            Create("warlock");
            AdvanceTo(9);
            Assert.That(_s.Dispatch(new SpendResourceCommand("pact_slots")).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(4, true)).IsSuccess, Is.True);
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);
            var back = serializer.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(((JObject)JObject.Parse(serializer.Serialize(back.World))["world"]).ToString(), Is.EqualTo(((JObject)JObject.Parse(json)["world"]).ToString()), "Exact round trip.");
            var s = back.World.Player.Rpg;
            Assert.That(s.Level, Is.EqualTo(9));
            Assert.That(s.SubclassId, Is.EqualTo("fiend"));
            Assert.That(s.Choices.Keys.Where(k => k.StartsWith("lv", StringComparison.Ordinal)), Is.EquivalentTo(Sheet.Choices.Keys.Where(k => k.StartsWith("lv", StringComparison.Ordinal))));
            Assert.That(s.Resources["pact_slots"], Is.EqualTo(1));
            Assert.That(s.TemporaryHp, Is.EqualTo(4));
            Assert.That(s.Advancements.Count, Is.EqualTo(8));
            Assert.That(s.ExperienceLog.Sum(a => a.Amount), Is.EqualTo(s.Experience));
            var doc = JObject.Parse(json);
            Assert.That(doc["world"]["player"]["rpg"]["maxHp"], Is.Null, "Derived values are not saved.");
            Assert.That(doc["schemaVersion"].Value<int>(), Is.EqualTo(WorldData.CurrentSchemaVersion)); // v9 when written (Phase 7); the current schema since

            var tampered = JObject.Parse(json);
            ((JArray)tampered["world"]["player"]["rpg"]["experienceLog"]).RemoveAt(0);
            Assert.That(serializer.Deserialize(tampered.ToString()).IsSuccess, Is.False, "A ledger that does not add up is refused.");
        }

        /// <summary>Rewrites a current save of a created character to the v8 (Phase 6) layout.</summary>
        public static void StripToV8(JObject doc)
        {
            Phase2Worlds.StripToV9(doc);
            doc["schemaVersion"] = 8;
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg.Remove("rulesVersion");
            rpg.Remove("experienceLog");
            rpg.Remove("advancements");
            var resources = (JObject)rpg["resources"];
            resources?.Remove("hit_dice");
            if (resources?["pact_slots"] != null)
            {
                resources["spell_slots_1"] = resources["pact_slots"];
                resources.Remove("pact_slots");
            }
        }

        [TestCase("warlock")]
        [TestCase("druid")]
        [TestCase("fighter")]
        public void PhaseSixSave_MigratesKeepingEveryChoice_AndEntersProgression(string cls)
        {
            Create(cls);
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-2)).IsSuccess, Is.True);
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            StripToV8(doc);
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg["choices"] = JObject.FromObject(((JObject)rpg["choices"]).Properties().Where(pr => pr.Name != "class:primal_order" && pr.Name != "class:invocations").ToDictionary(pr => pr.Name, pr => pr.Value));
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var p = loaded.World.Player;
            var original = W.Player;
            Assert.That(p.CharacterId, Is.EqualTo(original.CharacterId));
            Assert.That(p.Name, Is.EqualTo(original.Name));
            Assert.That(p.Purse, Is.EqualTo(original.Purse));
            Assert.That(p.Pose, Is.EqualTo(original.Pose));
            Assert.That(p.Inventory.Select(i => i.StackId), Is.EqualTo(original.Inventory.Select(i => i.StackId)));
            Assert.That(p.Equipment.Keys, Is.EquivalentTo(original.Equipment.Keys));
            Assert.That(p.Rpg.ClassId, Is.EqualTo(cls));
            Assert.That(p.Rpg.BaseScores, Is.EqualTo(original.Rpg.BaseScores));
            Assert.That(p.Rpg.CurrentHp, Is.EqualTo(original.Rpg.CurrentHp), "Damage taken is kept.");
            Assert.That(p.Rpg.Chosen(ClassDefinition.SkillChoiceId), Is.EqualTo(original.Rpg.Chosen(ClassDefinition.SkillChoiceId)));
            Assert.That(p.Rpg.RulesVersion, Is.EqualTo(6));
            Assert.That(p.Rpg.Resources["hit_dice"], Is.EqualTo(1));
            Assert.That(p.Rpg.Experience, Is.EqualTo(0));
            Assert.That(loaded.World.HistoricalEvents.Count, Is.EqualTo(W.HistoricalEvents.Count), "The chronicle is untouched.");
            if (cls == "warlock") Assert.That(p.Rpg.Resources.ContainsKey("pact_slots") && !p.Rpg.Resources.ContainsKey("spell_slots_1"), Is.True);
            var outstanding = CharacterProgression.Outstanding(p.Rpg).Select(o => o.Choice.Id).ToList();
            Assert.That(outstanding, Is.EqualTo(cls == "warlock" ? new[] { "class:invocations" } : cls == "druid" ? new[] { "class:primal_order" } : Array.Empty<string>()));

            // The migrated character enters progression: award, then advance with the open choice made.
                var sheet = p.Rpg;
                sheet.ExperienceLog.Add(new ExperienceAward { Key = "q", Amount = 300 });
                sheet.Experience = 300;
                var advanced = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, 2) }, default, out var errors);
                Assert.That(advanced, Is.Not.Null, string.Join("; ", errors));
                Assert.That(CharacterProgression.Outstanding(advanced), Is.Empty);
        }

        [Test]
        public void PhaseFiveSave_StillLoadsAsLegacy_WithTheProgressionRecord()
        {
            var serializer = new JsonWorldSerializer();
            var original = Phase2Worlds.Generated(97531);
            var doc = JObject.Parse(serializer.Serialize(original));
            Phase2Worlds.StripToV7(doc);
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var s = loaded.World.Player.Rpg;
            Assert.That(s.State, Is.EqualTo(CreationState.Legacy));
            Assert.That(s.RulesVersion, Is.EqualTo(ProgressionRules.CurrentRulesVersion));
            Assert.That(s.ExperienceLog, Is.Empty);
            Assert.That(s.Resources, Is.Empty, "Nothing granted before creation.");
            Assert.That(loaded.World.Player.CharacterId, Is.EqualTo(original.Player.CharacterId));
        }

        [Test]
        public void InvalidSheets_AreRejectedByTheValidator()
        {
            var s = AdvanceSheet(StandaloneSheet("fighter"), 4);
            CharacterSheetData Broken(Action<CharacterSheetData> change)
            {
                var c = s.Clone();
                change(c);
                return c;
            }

            Assert.That(CharacterValidation.CheckSheet(s), Is.Empty);
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Level = 5)), Has.Some.Contains("needs 6500 experience"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Experience += 1)), Has.Some.Contains("does not match its awards"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.ExperienceLog.Add(c.ExperienceLog[0].Clone()))), Has.Some.Contains("recorded twice"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.SubclassId = null)), Has.Some.Contains("must have chosen"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.SubclassId = "thief")), Has.Some.Contains("not a Fighter subclass"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Choices.Remove("lv4:feat"))), Has.Some.Contains("Ability Score Improvement or feat"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Choices["lv6:feat"] = new List<string> { "alert" })), Has.Some.Contains("premature"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Resources["action_surge"] = 5)), Has.Some.Contains("outside"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Resources.Remove("hit_dice"))), Has.Some.Contains("missing"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.Advancements.RemoveAt(0))), Has.Some.Contains("advancement records"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.CurrentHp = 999)), Has.Some.Contains("outside"));
            Assert.That(CharacterValidation.CheckSheet(Broken(c => c.RulesVersion = 99)), Has.Some.Contains("rules version"));
        }

        // ----- NPC and companion readiness ----------------------------------------------------------------

        [Test]
        public void AnyCharacterSheet_AdvancesThroughTheSameServices()
        {
            // A sheet that belongs to no player (a future companion or named NPC): created, advanced, spent and recovered
            // through the domain services alone, and valid throughout.
            var companion = AdvanceSheet(StandaloneSheet("paladin", "dwarf"), 7);
            Assert.That(CharacterValidation.CheckSheet(companion), Is.Empty);
            Assert.That(CharacterVitals.Spend(companion, "lay_on_hands", 10), Is.Null);
            Assert.That(CharacterVitals.Spend(companion, "lay_on_hands", 1000), Does.Contain("only"));
            Assert.That(CharacterVitals.Recover(companion, ResourceRecovery.LongRest), Is.Not.Empty);
            Assert.That(companion.Resources["lay_on_hands"], Is.EqualTo(35));
            var rules = new CharacterRules(companion);
            Assert.That(rules.Subclass.Id, Is.EqualTo("devotion"));
            Assert.That(rules.MaxHp, Is.EqualTo(10 + 6 * 6 + rules.Modifier(Ability.Constitution) * 7 + 7), "Dwarven toughness included.");
        }
        // ----- The level-up draft behind the window ---------------------------------------------------------

        [Test]
        public void AdvancementDraft_ListsPagesExplainsAndKeepsChoices()
        {
            var warlock = AdvanceSheet(StandaloneSheet("warlock"), 4);
            warlock.ExperienceLog.Add(new ExperienceAward { Key = "x", Amount = 10000 });
            warlock.Experience += 10000;
            var draft = new AdvancementDraft(warlock);
            Assert.That(draft.Level, Is.EqualTo(5));
            Assert.That(draft.Pages.First().IsOverview && draft.Pages.Last().IsReview, Is.True);
            var invocations = draft.Required.Single(s => s.Choice.Id == "lv5:invocations").Choice;
            Assert.That(warlock.Chosen("lv2:invocations"), Has.No.Member("pact_of_the_chain"));
            Assert.That(draft.Unavailable(invocations, "investment_of_the_chain_master"), Does.Contain("pact of the chain"), "Prerequisites are explained.");
            Assert.That(draft.Unavailable(invocations, "witch_sight"), Does.Contain("level 15"));
            var taken = warlock.Chosen("class:invocations")[0];
            Assert.That(draft.Unavailable(invocations, taken), Does.Contain("already taken"));
            Assert.That(draft.Toggle(invocations, "ascendant_step"), Is.True);
            Assert.That(draft.Toggle(invocations, "gaze_of_two_minds"), Is.True);
            Assert.That(draft.Toggle(invocations, "one_with_shadows"), Is.False, "A full choice refuses more.");
            Assert.That(draft.Unavailable(invocations, "one_with_shadows"), Does.StartWith(AdvancementDraft.FullPrefix));
            draft.Next();
            draft.Back();
            Assert.That(draft.Picked("lv5:invocations"), Is.EqualTo(new[] { "ascendant_step", "gaze_of_two_minds" }), "Moving between pages keeps picks.");
            Assert.That(draft.Problems(), Is.Empty);

            var fighter = AdvanceSheet(StandaloneSheet("fighter"), 3);
            fighter.ExperienceLog.Add(new ExperienceAward { Key = "x", Amount = 5000 });
            fighter.Experience += 5000;
            var f = new AdvancementDraft(fighter);
            var feat = f.Required.Single(s => s.Choice.Kind == ChoiceKind.Feat).Choice;
            Assert.That(f.Toggle(feat, "ability_score_improvement"), Is.True);
            var points = f.Required.Single(s => s.Choice.Kind == ChoiceKind.AbilityIncrease).Choice;
            Assert.That(f.Pages.Select(p => p.Title), Has.Member(points.Name), "A picked feat adds its own page.");
            Assert.That(f.Toggle(points, "Strength") && f.Toggle(points, "Strength"), Is.True);
            Assert.That(f.Problems(), Is.Empty);
            Assert.That(f.Toggle(feat, "alert"), Is.True, "Swapping the feat …");
            Assert.That(f.Picked(points.Id), Is.Empty, "… drops the old feat's choices.");
            var cmd = f.ToCommand();
            Assert.That(cmd.Levels.Single().Choices["lv4:feat"], Is.EqualTo(new[] { "alert" }));
            Assert.That(CharacterAdvancement.Apply(fighter, cmd.Levels, default, out var e), Is.Not.Null, string.Join("; ", e));
        }
    }
}
