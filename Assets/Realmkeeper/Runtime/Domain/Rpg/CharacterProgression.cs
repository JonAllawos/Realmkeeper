using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>A choice a character has made or must make, and what grants it (Phase 7).</summary>
    public readonly struct ChoiceSlot
    {
        public ChoiceSlot(ChoiceDefinition choice, string source)
        {
            Choice = choice;
            Source = source;
        }

        public ChoiceDefinition Choice { get; }
        public string Source { get; }
        public int Level => Choice.Level;
    }

    /// <summary>
    /// The shape of a character's progression (Phase 7): which choices exist up to a level (species and class
    /// creation choices, class level choices, the subclass, subclass choices, feats and each feat's own
    /// choices). One enumeration serves the rules evaluator, validation, advancement and the UI, for any
    /// <see cref="CharacterSheetData"/> — the player's or anyone else's. Class skills and starting equipment
    /// stay with creation (Phase 6) and are not listed here.
    /// </summary>
    public static class CharacterProgression
    {
        public static string SubclassChoiceId(int level) => $"lv{level}:subclass";

        /// <summary>Every choice up to and including <paramref name="upToLevel"/>, in level order.</summary>
        public static IReadOnlyList<ChoiceSlot> Choices(CharacterSheetData sheet, int upToLevel)
        {
            var list = new List<ChoiceSlot>();
            var species = RpgCatalog.FindSpecies(sheet.SpeciesId);
            var cls = RpgCatalog.FindClass(sheet.ClassId);
            if (species != null)
                foreach (var c in species.Choices) list.Add(new ChoiceSlot(c, species.Name));
            if (cls == null) return list;
            foreach (var c in cls.Choices) list.Add(new ChoiceSlot(c, cls.Name + " 1"));

            var sub = RpgCatalog.FindSubclass(sheet.SubclassId);
            if (sub != null && sub.ClassId != cls.Id) sub = null;
            for (var level = 2; level <= Math.Min(AbilityRules.MaxLevel, upToLevel); level++)
            {
                foreach (var c in cls.LevelChoices)
                    if (c.Level == level) AddWithFeat(list, sheet, c, cls.Name + " " + level);
                if (level == cls.SubclassLevel) list.Add(new ChoiceSlot(SubclassChoice(cls), cls.Name + " " + level));
                if (sub != null)
                    foreach (var c in sub.Choices)
                        if (c.Level == level) AddWithFeat(list, sheet, c, sub.Name + " " + level);
            }

            return list;
        }

        /// <summary>The choices made on reaching exactly <paramref name="level"/> (with a picked feat's own choices).</summary>
        public static IReadOnlyList<ChoiceSlot> ChoicesAt(CharacterSheetData sheet, int level)
        {
            var list = new List<ChoiceSlot>();
            foreach (var slot in Choices(sheet, level)) if (slot.Level == level) list.Add(slot);
            return list;
        }

        /// <summary>Choices the sheet may still lack because a later rules version introduced them (made at the next advancement).</summary>
        public static IReadOnlyList<ChoiceSlot> Outstanding(CharacterSheetData sheet)
        {
            var list = new List<ChoiceSlot>();
            if (sheet.State != CreationState.Complete) return list;
            foreach (var slot in Choices(sheet, sheet.Level))
                if (slot.Choice.IntroducedInRules > sheet.RulesVersion && slot.Choice.Kind != ChoiceKind.Subclass && sheet.Chosen(slot.Choice.Id).Count == 0)
                    list.Add(slot);
            return list;
        }

        private static void AddWithFeat(List<ChoiceSlot> list, CharacterSheetData sheet, ChoiceDefinition c, string source)
        {
            list.Add(new ChoiceSlot(c, source));
            if (c.Kind != ChoiceKind.Feat) return;
            var picked = sheet.Chosen(c.Id);
            var feat = picked.Count == 1 ? RpgCatalog.FindFeat(picked[0]) : null;
            if (feat == null) return;
            foreach (var fc in feat.Choices) list.Add(new ChoiceSlot(FeatSubChoice(c, feat, fc), source + ": " + feat.Name));
        }

        /// <summary>A feat's own choice, stored under "{feat choice}:{local}" at the feat choice's level.</summary>
        public static ChoiceDefinition FeatSubChoice(ChoiceDefinition parent, FeatDefinition feat, ChoiceDefinition local) => new ChoiceDefinition
        {
            Id = parent.Id + ":" + local.Id,
            Name = feat.Name + ": " + local.Name,
            Prompt = local.Prompt,
            Kind = local.Kind,
            Count = local.Count,
            Options = local.Options,
            Skills = local.Skills,
            Level = parent.Level,
            Pool = local.Pool,
            IntroducedInRules = parent.IntroducedInRules,
            FeatCategories = local.FeatCategories,
            Abilities = local.Abilities,
            AllowRepeat = local.AllowRepeat,
            Limit = local.Limit,
        };

        private static readonly Dictionary<string, ChoiceDefinition> SubclassChoices = new Dictionary<string, ChoiceDefinition>(StringComparer.Ordinal);

        /// <summary>The subclass choice of a class, as a choice definition (its value lives in <c>SubclassId</c>).</summary>
        public static ChoiceDefinition SubclassChoice(ClassDefinition cls)
        {
            lock (SubclassChoices)
            {
                if (SubclassChoices.TryGetValue(cls.Id, out var cached)) return cached;
                var options = new List<ChoiceOption>();
                foreach (var s in RpgCatalog.SubclassesOf(cls.Id)) options.Add(new ChoiceOption(s.Id, s.Name, s.Description));
                var c = new ChoiceDefinition
                {
                    Id = SubclassChoiceId(cls.SubclassLevel), Level = cls.SubclassLevel, Name = cls.SubclassTitle, Kind = ChoiceKind.Subclass,
                    Prompt = $"Choose your {cls.SubclassTitle}.", Options = options,
                };
                SubclassChoices[cls.Id] = c;
                return c;
            }
        }

        /// <summary>A readable statement of a prerequisite.</summary>
        public static string Describe(Prerequisite p)
        {
            switch (p.Kind)
            {
                case PrerequisiteKind.MinLevel: return $"level {p.Value}+";
                case PrerequisiteKind.AbilityAtLeast: return string.Join(" or ", Names(p.Abilities)) + $" {p.Value}+";
                case PrerequisiteKind.HasFeature: return "the " + p.Key + " feature";
                case PrerequisiteKind.PoolPick: return "the " + p.Key.Replace('_', ' ') + " invocation";
                case PrerequisiteKind.Spellcasting: return "the ability to cast spells";
                case PrerequisiteKind.FightingStyleFeature: return "a Fighting Style feature";
                case PrerequisiteKind.NotFightingStyle: return "not already knowing this fighting style";
                default: return p.Kind.ToString();
            }
        }

        private static IEnumerable<string> Names(IReadOnlyList<Ability> abilities)
        {
            foreach (var a in abilities) yield return a.ToString();
        }
    }
}
