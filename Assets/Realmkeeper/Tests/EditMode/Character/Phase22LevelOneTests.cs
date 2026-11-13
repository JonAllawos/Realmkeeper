using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 22 (ADR-0109): every new character is exactly level 1. The matrix builds every class with every species and
    /// lineage option (each background in turn), and every background with every class, through the creator's own draft
    /// and the command's sheet builder, and holds each to <see cref="LevelOneBaseline"/> and the validator. Mutation tests
    /// show the baseline catches each kind of leak; the creation command refuses later-level choices and a subclass;
    /// the tiefling's level 3 and 5 legacy spells stay locked until their levels; and the legacy's ability is chosen.
    /// </summary>
    public sealed class Phase22LevelOneTests
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

        // ----- Building every combination ---------------------------------------------------------------

        /// <summary>
        /// A complete draft: the class's skills, every species and class choice filled to its count (spells too, as many as
        /// level 1 allows), the given lineage option where the species has one. Null-safe for species without lineages.
        /// </summary>
        internal static CharacterDraft Full(string classId, string species, string background, string lineage = null)
        {
            var d = new CharacterDraft();
            d.Choices.Name = "Matrix " + classId;
            d.SetSpecies(species);
            d.SetBackground(background);
            d.SetClass(classId);
            var cls = RpgCatalog.FindClass(classId);
            var sp = RpgCatalog.FindSpecies(species);
            foreach (var c in sp.Choices.Concat(cls.Choices))
            {
                if (c.Kind == ChoiceKind.Option && lineage != null && c.Options.Any(o => o.Id == lineage)) { d.Toggle(c.Id, lineage); continue; }
                IEnumerable<string> candidates;
                switch (c.Kind)
                {
                    case ChoiceKind.Option: candidates = c.Options.Select(o => o.Id); break;
                    case ChoiceKind.Skill:
                    case ChoiceKind.Expertise: candidates = (c.Skills.Count > 0 ? c.Skills : SkillRules.All).Select(s => s.ToString()); break;
                    case ChoiceKind.Language: candidates = RpgCatalog.Languages.Select(l => l.Id); break;
                    default: continue;
                }

                foreach (var v in candidates)
                {
                    if (d.Picked(c.Id).Count >= c.Count) break;
                    d.Toggle(c.Id, v);
                }
            }

            foreach (var s in cls.SkillOptions) if (d.Picked(ClassDefinition.SkillChoiceId).Count < cls.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var c in cls.Choices.Where(c => c.Kind == ChoiceKind.Expertise))
                foreach (var s in SkillRules.All) if (d.Picked(c.Id).Count < c.Count) d.Toggle(c.Id, s.ToString());
            if (!sp.Appearance.AllowsBeard) d.Choices.Appearance.BeardId = null;
            return d;
        }

        private static IEnumerable<(string species, string lineage)> SpeciesAndLineages()
        {
            foreach (var sp in RpgCatalog.Species)
            {
                var lineage = sp.Choices.FirstOrDefault(c => c.Kind == ChoiceKind.Option && c.Id != RpgCatalog.LegacyAbilityChoiceId);
                if (lineage == null) { yield return (sp.Id, null); continue; }
                foreach (var o in lineage.Options) yield return (sp.Id, o.Id);
            }
        }

        private static List<string> Problems(CharacterDraft d, string label)
        {
            var problems = new List<string>();
            foreach (var p in d.Problems(CreationStep.Review)) problems.Add(label + ": draft: " + p);
            var sheet = CharacterCommandHandlers.BuildSheet(d.Clean(), out var errors);
            foreach (var e in errors) problems.Add(label + ": build: " + e);
            if (sheet == null) return problems;
            foreach (var e in CharacterValidation.CheckSheet(sheet)) problems.Add(label + ": valid: " + e);
            foreach (var e in LevelOneBaseline.Check(sheet)) problems.Add(label + ": level 1: " + e);
            return problems;
        }

        [Test]
        public void EveryClass_WithEverySpeciesAndLineage_AndEveryBackground_IsExactlyLevelOne()
        {
            var problems = new List<string>();
            var backgrounds = RpgCatalog.Backgrounds.Select(b => b.Id).ToList();
            var built = 0;
            var i = 0;
            foreach (var cls in RpgCatalog.Classes)
                foreach (var (species, lineage) in SpeciesAndLineages())
                {
                    var bg = backgrounds[i++ % backgrounds.Count];
                    problems.AddRange(Problems(Full(cls.Id, species, bg, lineage), $"{cls.Id}/{species}{(lineage != null ? "(" + lineage + ")" : string.Empty)}/{bg}"));
                    built++;
                }

            foreach (var bg in backgrounds)
                foreach (var cls in RpgCatalog.Classes)
                {
                    problems.AddRange(Problems(Full(cls.Id, "human", bg), $"{cls.Id}/human/{bg}"));
                    built++;
                }

            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(40)));
            Assert.That(built, Is.GreaterThan(RpgCatalog.Classes.Count * RpgCatalog.Species.Count));
        }

        [Test]
        public void EachClass_StartsWithItsLevelOneTable_AndNothingFromLevelTwo()
        {
            foreach (var cls in RpgCatalog.Classes)
            {
                var sheet = CharacterCommandHandlers.BuildSheet(Full(cls.Id, "human", "hermit").Clean(), out _);
                var r = new CharacterRules(sheet);
                Assert.That(sheet.Level, Is.EqualTo(1), cls.Id);
                Assert.That(sheet.Experience, Is.EqualTo(0), cls.Id);
                Assert.That(r.ProficiencyBonus, Is.EqualTo(2), cls.Id);
                Assert.That(r.MaxHp, Is.EqualTo(Math.Max(1, cls.HitDie + r.Modifier(Ability.Constitution))), cls.Id + ": the hit die's maximum + Constitution.");
                Assert.That(sheet.CurrentHp, Is.EqualTo(r.MaxHp), cls.Id);
                Assert.That(sheet.SubclassId, Is.Null, cls.Id);
                Assert.That(r.ActiveFeatures.All(f => f.Level == 1), Is.True, cls.Id + ": " + string.Join(", ", r.ActiveFeatures.Where(f => f.Level > 1).Select(f => f.Feature.Name)));
                Assert.That(sheet.Choices.Keys.Any(k => k.StartsWith("lv", StringComparison.Ordinal)), Is.False, cls.Id);
                Assert.That(sheet.Resources.Keys.Any(k => k.StartsWith("spell_slots_", StringComparison.Ordinal) && k != "spell_slots_1"), Is.False, cls.Id + ": no slot above 1st.");
                Assert.That(sheet.Resources.TryGetValue("hit_dice", out var hd) ? hd : -1, Is.EqualTo(1), cls.Id + ": one hit die.");
                var sc = cls.Spellcasting;
                if (sc == null) { Assert.That(sheet.Resources.ContainsKey("spell_slots_1") || sheet.Resources.ContainsKey("pact_slots"), Is.False, cls.Id); continue; }
                if (sc.Progression == CasterProgression.Pact) Assert.That(sheet.Resources["pact_slots"], Is.EqualTo(1), cls.Id);
                else Assert.That(sheet.Resources["spell_slots_1"], Is.EqualTo(2), cls.Id + ": SRD 5.2.1, two 1st-level slots at level 1.");
                foreach (var key in new[] { "class:spells", "class:spellbook" })
                    foreach (var id in sheet.Chosen(key)) Assert.That(SpellCatalog.Find(id).Level, Is.LessThanOrEqualTo(1), cls.Id + " " + id);
                foreach (var id in sheet.Chosen("class:cantrips")) Assert.That(SpellCatalog.Find(id).IsCantrip, Is.True, cls.Id + " " + id);
            }
        }

        // ----- The baseline catches each kind of leak -----------------------------------------------------

        private static CharacterSheetData Wizard() => CharacterCommandHandlers.BuildSheet(Full("wizard", "human", "scholar").Clean(), out _);

        [Test]
        public void TheBaseline_CatchesEveryKindOfLeak()
        {
            var good = Wizard();
            Assert.That(LevelOneBaseline.Check(good), Is.Empty, string.Join("; ", LevelOneBaseline.Check(good)));
            var leaks = new (string what, Action<CharacterSheetData> change, string expect)[]
            {
                ("a higher level", s => s.Level = 2, "level 1"),
                ("experience", s => s.Experience = 300, "experience"),
                ("an advancement record", s => s.Advancements.Add(new AdvancementRecord { Level = 2 }), "advancement"),
                ("a subclass", s => s.SubclassId = "evoker", "Arcane Tradition"),
                ("a later level's choice", s => s.Choices["lv4:feat"] = new List<string> { "alert" }, "later level"),
                ("a level-up in progress", s => s.PendingAdvancement = new LevelChoices { Level = 2 }, "level-up"),
                ("a 2nd-level slot", s => s.Resources["spell_slots_2"] = 2, "level 2 spell slots"),
                ("an extra 1st-level slot", s => s.Resources["spell_slots_1"] = 3, "spell_slots_1"),
                ("a resource not full", s => s.Resources["hit_dice"] = 0, "starts full"),
                ("a 2nd-level spell in the book", s => s.Choices["class:spellbook"] = new List<string> { "misty_step" }, "chosen at level 1"),
                ("a 2nd-level spell prepared", s => s.Spellcasting.Prepared.Add("misty_step"), "prepared at level 1"),
                ("a non-cantrip as a cantrip", s => s.Choices["class:cantrips"] = new List<string> { "magic_missile" }, "not a cantrip"),
                ("hurt", s => s.CurrentHp = 1, "full hit points"),
                ("old rules", s => s.RulesVersion = 9, "rules"),
                ("no kit", s => s.StartingEquipmentGranted = false, "starting equipment"),
            };
            foreach (var (what, change, expect) in leaks)
            {
                var bad = good.Clone();
                change(bad);
                var problems = LevelOneBaseline.Check(bad);
                Assert.That(problems, Has.Some.Contains(expect), what + ": " + string.Join("; ", problems));
            }
        }

        [Test]
        public void Creation_RefusesLaterLevelChoices_ASubclass_AndAPresetLevel()
        {
            var choices = Full("fighter", "human", "soldier").Clean();
            choices.Choices["lv4:feat"] = new List<string> { "alert" };
            var r = _s.Dispatch(new CreateCharacterCommand(choices));
            Assert.That(r.IsSuccess, Is.False, "A template carrying a level 4 feat is refused.");
            Assert.That(string.Join(" ", r.Errors), Does.Contain("lv4:feat"));
            choices.Choices.Remove("lv4:feat");
            choices.Choices["lv3:subclass"] = new List<string> { "champion" };
            Assert.That(_s.Dispatch(new CreateCharacterCommand(choices)).IsSuccess, Is.False, "A subclass cannot ride in with creation.");
            choices.Choices.Remove("lv3:subclass");

            var ok = _s.Dispatch(new CreateCharacterCommand(choices));
            Assert.That(ok.IsSuccess, Is.True, ok.Message + " " + string.Join("; ", ok.Errors));
            var s = W.Player.Rpg;
            Assert.That(LevelOneBaseline.Check(s), Is.Empty, string.Join("; ", LevelOneBaseline.Check(s)));
            Assert.That(s.Level, Is.EqualTo(1));
            Assert.That(s.Experience, Is.EqualTo(0));
            Assert.That(W.Player.Equipment.Count, Is.GreaterThan(0), "The kit is worn.");
            Assert.That(_s.GetCharacter().CanAdvance, Is.False, "A new character is never eligible at once.");
        }

        [Test]
        public void AnOlderSave_ThatEarnedExperienceBeforeCreation_KeepsIt()
        {
            // An older save could hold experience earned while pending (before Phase 22, D4). It is kept, never deleted.
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg["experience"] = 400;
            rpg["experienceLog"] = new JArray(new JObject { ["key"] = "story:prologue", ["source"] = "Story", ["sourceId"] = "prologue", ["amount"] = 400, ["reason"] = "before Phase 22" });
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "pending" + JsonWorldStore.Extension), doc.ToString());
            Assert.That(_s.Execute(new LoadWorldCommand("pending")).IsSuccess, Is.True);
            Assert.That(W.Player.Rpg.Experience, Is.EqualTo(400));
            var r = _s.Dispatch(new CreateCharacterCommand(Full("rogue", "human", "criminal").Clean()));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
            var sheet = W.Player.Rpg;
            Assert.That(sheet.Level, Is.EqualTo(1), "Still created at level 1.");
            Assert.That(sheet.Experience, Is.EqualTo(400), "The experience it had is kept.");
            Assert.That(LevelOneBaseline.Check(sheet, 400), Is.Empty);
            Assert.That(_s.GetCharacter().CanAdvance, Is.True, "It may advance through the level-up, never automatically.");
        }

        // ----- Species traits by level; the tiefling's legacy ---------------------------------------------

        [Test]
        public void TheTieflingsLegacySpells_StayLockedUntilLevelsThreeAndFive()
        {
            var sheet = CharacterCommandHandlers.BuildSheet(Full("fighter", "tiefling", "hermit", "infernal").Clean(), out _);
            var r = new CharacterRules(sheet);
            Assert.That(r.ActiveFeatures.Select(f => f.Feature.Id), Has.No.Member("legacy_spell_3").And.No.Member("legacy_spell_5"));
            Assert.That(r.LockedFeatures.Where(f => f.Feature.Id.StartsWith("legacy_spell", StringComparison.Ordinal)).Select(f => f.Level), Is.EqualTo(new[] { 3, 5 }));
            Assert.That(LevelOneBaseline.Check(sheet), Is.Empty);

            sheet.Level = 3;
            r = new CharacterRules(sheet);
            Assert.That(r.ActiveFeatures.Single(f => f.Feature.Id == "legacy_spell_3").Level, Is.EqualTo(3), "Arrives at level 3.");
            Assert.That(r.ActiveFeatures.Select(f => f.Feature.Id), Has.No.Member("legacy_spell_5"));
            Assert.That(CharacterRules.Needs(r.ActiveFeatures.Single(f => f.Feature.Id == "legacy_spell_3").Feature) & IntegrationNeed.Magic, Is.EqualTo(IntegrationNeed.Magic),
                "Not yet cast by the engine, and marked so.");
            sheet.Level = 5;
            Assert.That(new CharacterRules(sheet).ActiveFeatures.Select(f => f.Feature.Id), Has.Member("legacy_spell_5"));
        }

        [Test]
        public void TheLegacySpellsAppear_InTheLevelUpThatBringsThem()
        {
            Assert.That(_s.Dispatch(new CreateCharacterCommand(Full("fighter", "tiefling", "hermit", "chthonic").Clean())).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new AwardExperienceCommand(ProgressionRules.ExperienceFor(3), ExperienceSource.Milestone, "to3")).IsSuccess, Is.True);
            var s = W.Player.Rpg;
            var to2 = CharacterAdvancement.SuggestChoices(s, 2);
            Assert.That(CharacterAdvancement.Preview(s, to2).NewFeatures.Select(f => f.Feature.Id), Has.No.Member("legacy_spell_3"));
            Assert.That(_s.Dispatch(new AdvanceCharacterCommand(to2)).IsSuccess, Is.True);
            s = W.Player.Rpg;
            var to3 = CharacterAdvancement.SuggestChoices(s, 3);
            Assert.That(CharacterAdvancement.Preview(s, to3).NewFeatures.Select(f => f.Feature.Id), Has.Member("legacy_spell_3"), "Shown as new at level 3.");
        }

        [TestCase("intelligence", Ability.Intelligence)]
        [TestCase("wisdom", Ability.Wisdom)]
        [TestCase("charisma", Ability.Charisma)]
        public void TheTieflingChoosesItsLegacysAbility(string pick, Ability expected)
        {
            var d = Full("fighter", "tiefling", "hermit", "infernal");
            d.Choices.Choices.Remove(RpgCatalog.LegacyAbilityChoiceId);
            d.Toggle(RpgCatalog.LegacyAbilityChoiceId, pick);
            Assert.That(d.Picked(RpgCatalog.LegacyAbilityChoiceId), Is.EqualTo(new[] { pick }));
            var r = new CharacterRules(CharacterCommandHandlers.BuildSheet(d.Clean(), out _));
            Assert.That(r.InnateSpells.Single().ability, Is.EqualTo(expected));
            Assert.That(r.InnateSpellSaveDc(expected).Total, Is.EqualTo(8 + 2 + r.Modifier(expected)));

            var none = Full("fighter", "tiefling", "hermit", "infernal");
            none.Choices.Choices.Remove(RpgCatalog.LegacyAbilityChoiceId);
            Assert.That(none.Problems(CreationStep.Species), Has.Some.Contains("Legacy Spellcasting Ability"), "A new tiefling must choose it.");
        }

        [Test]
        public void ATieflingMadeBeforeRules10_KeepsCharisma_AndChoosesAtItsNextLevelUp()
        {
            var sheet = CharacterCommandHandlers.BuildSheet(Full("fighter", "tiefling", "hermit", "abyssal").Clean(), out _);
            sheet.Choices.Remove(RpgCatalog.LegacyAbilityChoiceId);
            sheet.RulesVersion = 9;
            Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty, "Valid as it was.");
            Assert.That(new CharacterRules(sheet).InnateAbility, Is.EqualTo(Ability.Charisma));
            Assert.That(CharacterProgression.Outstanding(sheet).Select(o => o.Choice.Id), Has.Member(RpgCatalog.LegacyAbilityChoiceId), "Asked with the next level-up.");
        }

        // ----- Goliath ancestry against spells (Phase 20A backfill) ---------------------------------------

        [Test]
        public void StonesEndurance_IsOfferedAgainstASpell_AndStormsThunderAnswersOne()
        {
            foreach (var ancestry in new[] { "stone", "storm" })
            {
                var dir = Path.Combine(_dir, ancestry);
                Directory.CreateDirectory(dir);
                var s = Phase2Worlds.Session(dir);
                var created = s.Dispatch(new CreateCharacterCommand(Full("fighter", "goliath", "soldier", ancestry).Clean()));
                Assert.That(created.IsSuccess, Is.True, created.Message + " " + string.Join("; ", created.Errors));
                var w = s.CurrentWorldSnapshotForTests();
                var guids = 0;
                var enc = CombatEngine.Create(w, new EncounterSetup
                {
                    Name = "Phase 22", Kind = EncounterKind.Other,
                    Participants = new List<ParticipantSetup>
                    {
                        new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player },
                        new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "acolyte", Side = "foes", Position = new GridPos(3, 0) },
                    },
                }, _ => new Guid(++guids, 0, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x22 }), out var errors);
                Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
                w.Encounters.Add(enc);
                var e = new CombatEngine(w, enc);
                e.Begin();
                var p = enc.Combatants.Single(c => c.Kind == CombatantKind.Player);
                var acolyte = enc.Combatants.Single(c => c.Kind == CombatantKind.Creature);
                var asked = false;
                for (var i = 0; i < 60 && !asked; i++)
                {
                    w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp;
                    acolyte.MaxHp = acolyte.CurrentHp = 300;
                    enc.TurnIndex = enc.Order.IndexOf(acolyte.CombatantId);
                    acolyte.Turn = new TurnResources();
                    p.Turn = new TurnResources();
                    var o = e.Act(acolyte.CombatantId, new CastSpellAction("sacred_flame", 0, new[] { p.CombatantId }));
                    if (o.Pending == null) continue;
                    asked = true;
                    Assert.That(o.Pending.Opportunity.Kind, Is.EqualTo(ancestry == "stone" ? ReactionKind.StonesEndurance : ReactionKind.StormsThunder), ancestry);
                    Assert.That(o.Pending.Opportunity.Trigger, Does.Contain("Sacred Flame").Or.Contain(acolyte.Name), ancestry);
                    var uses = w.Player.Rpg.Resources["stones_endurance"];
                    var before = e.Hp(acolyte);
                    var answer = e.Respond(o.Pending.Id, true);
                    Assert.That(answer.Ok, Is.True, answer.Error);
                    Assert.That(w.Player.Rpg.Resources["stones_endurance"], Is.EqualTo(uses - 1), ancestry + ": one ancestry use.");
                    if (ancestry == "storm") Assert.That(e.Hp(acolyte), Is.LessThan(before), "1d8 thunder back at the caster.");
                    else Assert.That(enc.Log.Any(l => l.Text.Contains("Stone's Endurance")), Is.True);
                }

                Assert.That(asked, Is.True, ancestry + ": a damaging spell offers the reaction.");
            }
        }
    }
}
