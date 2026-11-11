using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// The character rules catalog (Phase 6): species, backgrounds, classes, tools and languages. Data in
    /// code, never saved: saves refer to definitions by stable ID. <see cref="Validate"/> checks the whole
    /// catalog (unique IDs, known items, tools, languages, skills and resources).
    /// </summary>
    public static partial class RpgCatalog
    {
        private static readonly Lazy<IReadOnlyList<SpeciesDefinition>> SpeciesList = new Lazy<IReadOnlyList<SpeciesDefinition>>(BuildSpecies);
        private static readonly Lazy<IReadOnlyList<BackgroundDefinition>> BackgroundList = new Lazy<IReadOnlyList<BackgroundDefinition>>(BuildBackgrounds);
        private static readonly Lazy<IReadOnlyList<ClassDefinition>> ClassList = new Lazy<IReadOnlyList<ClassDefinition>>(BuildClasses);
        private static readonly Lazy<IReadOnlyList<ToolDefinition>> ToolList = new Lazy<IReadOnlyList<ToolDefinition>>(BuildTools);
        private static readonly Lazy<IReadOnlyList<LanguageDefinition>> LanguageList = new Lazy<IReadOnlyList<LanguageDefinition>>(BuildLanguages);

        public static IReadOnlyList<SpeciesDefinition> Species => SpeciesList.Value;
        public static IReadOnlyList<BackgroundDefinition> Backgrounds => BackgroundList.Value;
        public static IReadOnlyList<ClassDefinition> Classes => ClassList.Value;
        public static IReadOnlyList<ToolDefinition> Tools => ToolList.Value;
        public static IReadOnlyList<LanguageDefinition> Languages => LanguageList.Value;

        public static SpeciesDefinition FindSpecies(string id) => Find(Species, id, s => s.Id);
        public static BackgroundDefinition FindBackground(string id) => Find(Backgrounds, id, b => b.Id);
        public static ClassDefinition FindClass(string id) => Find(Classes, id, c => c.Id);
        public static ToolDefinition FindTool(string id) => Find(Tools, id, t => t.Id);
        public static LanguageDefinition FindLanguage(string id) => Find(Languages, id, l => l.Id);

        private static T Find<T>(IReadOnlyList<T> list, string id, Func<T, string> key) where T : class
        {
            if (id == null) return null;
            foreach (var x in list) if (key(x) == id) return x;
            return null;
        }

        public static string SourceText(ContentSource s) =>
            s == ContentSource.Srd521 ? "SRD 5.2.1 (CC BY 4.0)" : s == ContentSource.Srd51 ? "SRD 5.1 (CC BY 4.0)" : "Realmkeeper original";

        /// <summary>Every problem in the catalog; empty when it is consistent.</summary>
        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            Unique(errors, "species", Species, s => s.Id);
            Unique(errors, "background", Backgrounds, b => b.Id);
            Unique(errors, "class", Classes, c => c.Id);
            Unique(errors, "tool", Tools, t => t.Id);
            Unique(errors, "language", Languages, l => l.Id);

            foreach (var t in Tools)
                if (t.ItemId != null && !ItemCatalog.Exists(t.ItemId)) errors.Add($"Tool {t.Id} names unknown item {t.ItemId}.");

            foreach (var s in Species)
            {
                if (string.IsNullOrEmpty(s.Name) || string.IsNullOrEmpty(s.Description) || string.IsNullOrEmpty(s.Lore)) errors.Add($"Species {s.Id} lacks text.");
                if (s.Speed <= 0) errors.Add($"Species {s.Id} has no speed.");
                foreach (var l in s.Languages) if (FindLanguage(l) == null) errors.Add($"Species {s.Id}: unknown language {l}.");
                foreach (var pair in s.AbilityBonuses) if (pair.Value < 0 || pair.Value > 2) errors.Add($"Species {s.Id}: bonus {pair.Value} out of range.");
                var resources = new HashSet<string>();
                foreach (var r in s.Resources) resources.Add(r.Id);
                foreach (var f in s.Traits) CheckEffects(errors, "species " + s.Id, f.Effects, resources);
                foreach (var c in s.Choices) CheckChoice(errors, "species " + s.Id, c, resources);
                if (s.Appearance == null || s.Appearance.HeightScale <= 0.3f || s.Appearance.HeightScale > 1.5f) errors.Add($"Species {s.Id}: bad appearance contract.");
            }

            foreach (var b in Backgrounds)
            {
                if (string.IsNullOrEmpty(b.Name) || string.IsNullOrEmpty(b.Description) || string.IsNullOrEmpty(b.Lore)) errors.Add($"Background {b.Id} lacks text.");
                if (b.Skills.Count != 2 || b.Skills[0] == b.Skills[1]) errors.Add($"Background {b.Id} must grant two different skills.");
                foreach (var t in b.Tools) if (FindTool(t) == null) errors.Add($"Background {b.Id}: unknown tool {t}.");
                foreach (var l in b.Languages) if (FindLanguage(l) == null) errors.Add($"Background {b.Id}: unknown language {l}.");
                foreach (var k in b.Equipment) CheckKit(errors, "background " + b.Id, k);
                if (b.Coin < 0) errors.Add($"Background {b.Id}: negative coin.");
                if (b.Feature == null) errors.Add($"Background {b.Id} has no feature.");
                else CheckEffects(errors, "background " + b.Id, b.Feature.Effects, new HashSet<string>());
            }

            foreach (var c in Classes)
            {
                if (string.IsNullOrEmpty(c.Name) || string.IsNullOrEmpty(c.Description) || string.IsNullOrEmpty(c.Identity)) errors.Add($"Class {c.Id} lacks text.");
                if (c.HitDie != 6 && c.HitDie != 8 && c.HitDie != 10 && c.HitDie != 12) errors.Add($"Class {c.Id}: hit die d{c.HitDie}.");
                if (c.SavingThrows.Count != 2 || c.SavingThrows[0] == c.SavingThrows[1]) errors.Add($"Class {c.Id} must have two saving throws.");
                if (c.PrimaryAbilities.Count == 0) errors.Add($"Class {c.Id} has no primary ability.");
                if (c.SkillChoices < 1 || c.SkillChoices > c.SkillOptions.Count) errors.Add($"Class {c.Id}: {c.SkillChoices} skills from {c.SkillOptions.Count}.");
                if (c.Features.Count == 0) errors.Add($"Class {c.Id} has no level-one features.");
                if (c.Equipment.Count == 0) errors.Add($"Class {c.Id} has no starting equipment.");
                foreach (var t in c.Tools) if (FindTool(t) == null) errors.Add($"Class {c.Id}: unknown tool {t}.");
                var resources = new HashSet<string>();
                foreach (var r in c.Resources) if (!resources.Add(r.Id)) errors.Add($"Class {c.Id}: duplicate resource {r.Id}.");
                foreach (var f in c.Features) CheckEffects(errors, "class " + c.Id, f.Effects, resources);
                foreach (var ch in c.Choices) CheckChoice(errors, "class " + c.Id, ch, resources);
                foreach (var g in c.Equipment)
                {
                    if (g.Options.Count == 0) errors.Add($"Class {c.Id}: equipment group {g.Id} is empty.");
                    foreach (var o in g.Options) foreach (var k in o.Items) CheckKit(errors, $"class {c.Id} kit {g.Id}/{o.Id}", k);
                }

                if ((c.Spellcasting != null) != resources.Contains("spell_slots_1")) errors.Add($"Class {c.Id}: spellcasting and spell slots disagree.");
            }

            return errors;
        }

        private static void Unique<T>(List<string> errors, string what, IReadOnlyList<T> list, Func<T, string> key)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var x in list)
            {
                var id = key(x);
                if (string.IsNullOrEmpty(id)) errors.Add($"A {what} has no ID.");
                else if (!seen.Add(id)) errors.Add($"Duplicate {what} ID {id}.");
            }
        }

        private static void CheckKit(List<string> errors, string where, KitItem k)
        {
            var item = ItemCatalog.Find(k.ItemId);
            if (item == null) errors.Add($"{where}: unknown item {k.ItemId}.");
            else if (k.Quantity < 1) errors.Add($"{where}: quantity {k.Quantity}.");
            else if (k.Equip && !item.IsEquippable) errors.Add($"{where}: {k.ItemId} cannot be equipped.");
        }

        private static void CheckChoice(List<string> errors, string where, ChoiceDefinition c, HashSet<string> resources)
        {
            if (string.IsNullOrEmpty(c.Id) || c.Count < 1) errors.Add($"{where}: bad choice {c.Id}.");
            if (c.Kind == ChoiceKind.Option && c.Options.Count < c.Count) errors.Add($"{where}: choice {c.Id} has too few options.");
            foreach (var o in c.Options) CheckEffects(errors, where + " " + c.Id, o.Effects, resources);
        }

        private static void CheckEffects(List<string> errors, string where, IReadOnlyList<RuleEffect> effects, HashSet<string> resources)
        {
            foreach (var e in effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.SkillProficiency:
                        if (!SkillRules.TryParse(e.Key, out _)) errors.Add($"{where}: unknown skill {e.Key}.");
                        break;
                    case EffectKind.SaveProficiency:
                    case EffectKind.UnarmoredDefense:
                        if (!Enum.TryParse<Ability>(e.Key, out _)) errors.Add($"{where}: unknown ability {e.Key}.");
                        break;
                    case EffectKind.ToolProficiency:
                        if (FindTool(e.Key) == null) errors.Add($"{where}: unknown tool {e.Key}.");
                        break;
                    case EffectKind.Language:
                        if (FindLanguage(e.Key) == null) errors.Add($"{where}: unknown language {e.Key}.");
                        break;
                    case EffectKind.Resource:
                        if (!resources.Contains(e.Key)) errors.Add($"{where}: undefined resource {e.Key}.");
                        break;
                    case EffectKind.Darkvision:
                    case EffectKind.CarryCapacityPercent:
                        if (e.Value <= 0) errors.Add($"{where}: {e.Kind} needs a positive value.");
                        break;
                    case EffectKind.Resistance:
                    case EffectKind.SaveAdvantage:
                    case EffectKind.WeaponProficiency:
                    case EffectKind.ArmorProficiency:
                    case EffectKind.InnateMagic:
                    case EffectKind.CombatRule:
                    case EffectKind.SocialFeature:
                        if (string.IsNullOrEmpty(e.Key)) errors.Add($"{where}: {e.Kind} needs a key.");
                        break;
                }
            }
        }
    }
}
