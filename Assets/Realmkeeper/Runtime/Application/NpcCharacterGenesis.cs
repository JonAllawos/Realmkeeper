using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Gives named people their mechanical side (Phase 10, ADR-0043). Sheets are made exactly as the player's are:
    /// the creation draft (species, background, class, skills, options, kit), the same validation, then level-ups
    /// through <see cref="CharacterAdvancement"/> with picks the archetype prefers. Deterministic per person; it never
    /// touches identity, relationships, schedules or history, and never replaces an existing record.
    /// </summary>
    public static class NpcCharacterGenesis
    {
        /// <summary>Generates the record of every living named person who has none. Returns how many were made.</summary>
        public static int Ensure(WorldData world)
        {
            var made = 0;
            foreach (var npc in world.Npcs)
            {
                if (npc.IsDeceased || npc.Character != null) continue;
                var archetype = NpcArchetypeCatalog.For(world, npc);
                npc.Character = Make(world, npc, archetype, archetype.ClassId, out _);
                made++;
            }

            return made;
        }

        /// <summary>
        /// The record for one person from an archetype. With a class (the archetype's, or a promotion's) the person gets
        /// a sheet at the archetype's level; otherwise a lightweight record. Null sheet plus errors when the rules refuse.
        /// </summary>
        public static NpcCharacterData Make(WorldData world, NpcData npc, NpcArchetypeDefinition a, string classId, out List<string> errors)
        {
            errors = new List<string>();
            var record = new NpcCharacterData
            {
                ArchetypeId = a.Id,
                Occupation = a.Occupation,
                Gear = a.Gear.Clone(),
                Purse = a.Purse,
            };
            if (classId == null) return record;
            var settlement = world.FindSettlement(npc.HomeSettlementId);
            var hash = NpcHash.Of(world.Seed, "npc-sheet", npc.NpcId.ToString());
            var level = NpcArchetypeCatalog.LevelFor(a, settlement, hash);
            record.Sheet = BuildSheet(npc, a, classId, level, world.CurrentSimulationDate, errors);
            if (record.Sheet == null) return record;
            record.SheetSince = world.CurrentSimulationDate;
            if (classId != a.ClassId) record.Occupation = a.Occupation + " (" + (RpgCatalog.FindClass(classId)?.Name ?? classId).ToLowerInvariant() + ")";
            return record;
        }

        /// <summary>A complete, validated sheet for a person: level 1 through the creation draft, then each level up.</summary>
        public static CharacterSheetData BuildSheet(NpcData npc, NpcArchetypeDefinition a, string classId, int level, WorldDate today, List<string> errors)
        {
            var cls = RpgCatalog.FindClass(classId);
            if (cls == null) { errors.Add($"Unknown class '{classId}'."); return null; }
            var d = new CharacterDraft();
            d.Choices.Name = npc.Name;
            d.SetSpecies(a.SpeciesId);
            d.SetBackground(a.BackgroundId);
            d.SetClass(classId);
            ArrangeScores(d, a, cls);

            foreach (var c in Choices(d))
            {
                var wanted = c.Count;
                foreach (var v in Candidates(a, c))
                {
                    if (d.Picked(c.Id).Count >= wanted) break;
                    if (!d.Picked(c.Id).Contains(v)) d.Toggle(c.Id, v);
                }
            }

            foreach (var v in Prefer(a, SkillRules.All)) if (d.Picked(ClassDefinition.SkillChoiceId).Count < cls.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, v);
            foreach (var s in cls.SkillOptions) if (d.Picked(ClassDefinition.SkillChoiceId).Count < cls.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());

            var sheet = CharacterCommandHandlers.BuildSheet(d.Clean(), out var buildErrors);
            if (sheet == null) { errors.AddRange(buildErrors); return null; }
            sheet.Title = a.Occupation;
            sheet.CreatedOn = today;
            var problems = CharacterValidation.CheckSheet(sheet);
            if (problems.Count > 0) { errors.AddRange(problems); return null; }

            if (level > 1)
            {
                sheet = AdvanceTo(sheet, a, level, today, "genesis:" + npc.NpcId, errors);
                if (sheet == null) return null;
            }

            Prepare(sheet, a);
            sheet.CurrentHp = new CharacterRules(sheet).MaxHp;
            return sheet;
        }

        /// <summary>
        /// Advances a sheet to a level through the shared rules (the experience it needs is recorded under one key, so
        /// generation never doubles it), choosing what the archetype prefers. Null with the reasons when refused.
        /// </summary>
        public static CharacterSheetData AdvanceTo(CharacterSheetData sheet, NpcArchetypeDefinition a, int level, WorldDate today, string key, List<string> errors)
        {
            var need = ProgressionRules.ExperienceFor(level) - sheet.Experience;
            if (need > 0)
            {
                sheet.ExperienceLog.Add(new ExperienceAward { Key = key, Source = ExperienceSource.Milestone, Amount = need, Date = today, Reason = "experience before the story began" });
                sheet.Experience += need;
            }

            return AdvanceAvailable(sheet, a, today, errors);
        }

        /// <summary>Takes every level the sheet's experience allows, with the archetype's preferred picks: the advanced sheet (a validated copy), or null with the reasons.</summary>
        public static CharacterSheetData AdvanceAvailable(CharacterSheetData sheet, NpcArchetypeDefinition a, WorldDate today, List<string> errors)
        {
            while (CharacterAdvancement.CanAdvance(sheet))
            {
                var next = sheet.Level + 1;
                var draft = CharacterAdvancement.SuggestChoices(sheet, next, c => PreferFor(a, c));
                if (a?.SubclassId != null && draft.SubclassId != null && RpgCatalog.FindSubclass(a.SubclassId)?.ClassId == sheet.ClassId) draft.SubclassId = a.SubclassId;
                var advanced = CharacterAdvancement.Apply(sheet, new[] { draft }, today, out var stepErrors);
                if (advanced == null) { errors.AddRange(stepErrors); return null; }
                sheet = advanced;
            }

            return sheet;
        }

        /// <summary>Prepared casters prepare the archetype's spells as far as the limit allows; others keep their known spells.</summary>
        public static void Prepare(CharacterSheetData sheet, NpcArchetypeDefinition a)
        {
            var rules = new CharacterRules(sheet);
            if (rules.Class?.Spellcasting == null) return;
            var keeping = RpgCatalog.KeepingOf(rules.Class);
            if (keeping != SpellKeeping.Prepared && keeping != SpellKeeping.PreparedOneSwap && keeping != SpellKeeping.Spellbook) return;
            var access = new SpellAccess(rules);
            var list = new List<string>();
            foreach (var id in a?.Prepare ?? Array.Empty<string>())
            {
                var candidate = new List<string>(list) { id };
                if (access.PreparationProblem(candidate) == null) list = candidate;
            }

            sheet.Spellcasting.Prepared = list;
        }

        private static IEnumerable<string> PreferFor(NpcArchetypeDefinition a, ChoiceDefinition c)
        {
            if (a == null) yield break;
            foreach (var p in a.Prefer) yield return p;
        }

        private static IEnumerable<ChoiceDefinition> Choices(CharacterDraft d)
        {
            if (d.Species != null) foreach (var c in d.Species.Choices) if (c.Kind != ChoiceKind.Expertise) yield return c;
            if (d.Class != null) foreach (var c in d.Class.Choices) if (c.Kind != ChoiceKind.Expertise) yield return c;
            if (d.Class != null) foreach (var c in d.Class.Choices) if (c.Kind == ChoiceKind.Expertise) yield return c;
        }

        private static IEnumerable<string> Candidates(NpcArchetypeDefinition a, ChoiceDefinition c)
        {
            foreach (var p in a.Prefer) yield return p;
            switch (c.Kind)
            {
                case ChoiceKind.Option:
                    foreach (var o in c.Options) yield return o.Id;
                    break;
                case ChoiceKind.Skill:
                case ChoiceKind.Expertise:
                    foreach (var s in c.Skills.Count > 0 ? c.Skills : SkillRules.All) yield return s.ToString();
                    break;
            }
        }

        private static IEnumerable<string> Prefer(NpcArchetypeDefinition a, IEnumerable<Skill> skills)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in skills) names.Add(s.ToString());
            foreach (var p in a.Prefer) if (names.Contains(p)) yield return p;
        }

        /// <summary>The standard array in the archetype's order, then the class's primary abilities, Constitution and the rest.</summary>
        private static void ArrangeScores(CharacterDraft d, NpcArchetypeDefinition a, ClassDefinition cls)
        {
            var order = new List<Ability>();
            foreach (var x in a.ScoreOrder) if (!order.Contains(x)) order.Add(x);
            foreach (var x in cls.PrimaryAbilities) if (!order.Contains(x)) order.Add(x);
            foreach (var x in new[] { Ability.Constitution, Ability.Dexterity, Ability.Wisdom, Ability.Strength, Ability.Charisma, Ability.Intelligence })
                if (!order.Contains(x)) order.Add(x);
            d.SetMethod(AbilityMethod.StandardArray);
            for (var i = 0; i < 6; i++) d.Choices.BaseScores[(int)order[i]] = AbilityRules.StandardArray[i];
        }

        /// <summary>A short line about a person's sheet for logs and reports.</summary>
        public static string Describe(NpcData npc)
        {
            var c = npc.Character;
            if (c == null) return npc.Name + ": no record";
            if (!c.HasSheet) return $"{npc.Name}: {c.Occupation} (lightweight)";
            var r = new CharacterRules(c.Sheet, c.Gear.Loadout);
            return string.Format(CultureInfo.InvariantCulture, "{0}: {1}, {2} {3}, HP {4}/{5}", npc.Name, c.Occupation, r.Class?.Name, c.Sheet.Level, c.Sheet.CurrentHp, r.MaxHp);
        }
    }
}
