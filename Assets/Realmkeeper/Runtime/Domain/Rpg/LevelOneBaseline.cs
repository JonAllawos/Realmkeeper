using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// Phase 22 (ADR-0109): what a newly created character must be, and nothing more. Every new player character is
    /// checked against it before creation commits, and the test matrix runs it over every class, species, lineage and
    /// background. It recomputes the level 1 figures from the class table and the sheet's own picks rather than trusting
    /// the derived values alone, so a later-level feature, slot, spell, choice or resource cannot slip into creation.
    ///
    /// The baseline:
    /// - level 1, no advancement records, no subclass, no level-up in progress, made under the current rules;
    /// - the experience carried from before creation (0 for every new game since Phase 22, owner decision D4);
    /// - proficiency +2; maximum hit points = the hit die's maximum + the Constitution modifier + any per-level bonus, at
    ///   least 1; current hit points full; no temporary hit points; no death saves;
    /// - every active feature is a level 1 feature (class features listed at level 1; species traits whose level is 1);
    /// - no choice for a later level ("lv2:…");
    /// - every resource full at its level 1 maximum, including hit dice; spell slots exactly the level 1 row of the class's
    ///   table (no 2nd-level slot), pact slots 1;
    /// - cantrip picks are cantrips; spells known, in the spellbook or prepared are cantrips or 1st-level spells, within
    ///   the level 1 counts;
    /// - the starting equipment granted once.
    /// </summary>
    public static class LevelOneBaseline
    {
        public static IReadOnlyList<string> Check(CharacterSheetData s, int carriedExperience = 0)
        {
            var errors = new List<string>();
            if (s == null) { errors.Add("There is no character record."); return errors; }
            if (s.State != CreationState.Complete) { errors.Add("The character is not created."); return errors; }
            var rules = new CharacterRules(s);
            if (rules.Class == null || rules.Species == null || rules.Background == null) { errors.Add("The species, background or class is unknown."); return errors; }

            if (s.Level != 1) errors.Add($"A new character is level 1 (is {s.Level}).");
            if (s.Experience != carriedExperience) errors.Add($"A new character has {carriedExperience} experience (has {s.Experience}).");
            if (s.Advancements.Count > 0) errors.Add("A new character has no advancement records.");
            if (s.SubclassId != null) errors.Add($"A new character has no {rules.Class.SubclassTitle} (chosen at level {rules.Class.SubclassLevel}).");
            if (s.PendingAdvancement != null) errors.Add("A new character has no level-up in progress.");
            if (s.RulesVersion != ProgressionRules.CurrentRulesVersion) errors.Add($"A new character is made under rules {ProgressionRules.CurrentRulesVersion} (is {s.RulesVersion}).");
            foreach (var key in s.Choices.Keys)
                if (key.StartsWith("lv", StringComparison.Ordinal)) errors.Add($"A new character has a later level's choice '{key}'.");

            // Proficiency and hit points, from the class table.
            if (rules.ProficiencyBonus != 2) errors.Add($"Proficiency at level 1 is +2 (is +{rules.ProficiencyBonus}).");
            var expected = rules.Class.HitDie + rules.Modifier(Ability.Constitution);
            foreach (var e in rules.Effects)
                if (e.Effect.Kind == EffectKind.MaxHpPerLevel) expected += e.Effect.Value;
            expected = Math.Max(1, expected);
            if (rules.MaxHp != expected) errors.Add($"Maximum hit points at level 1 are {expected} (d{rules.Class.HitDie} maximum + Constitution + per-level bonuses); the sheet derives {rules.MaxHp}.");
            if (s.CurrentHp != rules.MaxHp) errors.Add($"A new character starts at full hit points ({rules.MaxHp}; has {s.CurrentHp}).");
            if (s.TemporaryHp != 0) errors.Add("A new character has no temporary hit points.");
            if (s.DeathSaveSuccesses != 0 || s.DeathSaveFailures != 0 || s.IsStable || s.IsDead) errors.Add("A new character is alive and well.");

            // Features: level 1 only.
            foreach (var f in rules.ActiveFeatures)
            {
                if (f.Level > 1) errors.Add($"'{f.Feature.Name}' ({f.Source}) is a level {f.Level} feature.");
                if (f.Feature.MinLevel > 1) errors.Add($"'{f.Feature.Name}' arrives at level {f.Feature.MinLevel}.");
            }

            // Resources: present, full, at their level 1 maximum; slots exactly the level 1 row.
            var max = rules.ResourceMaximums();
            foreach (var pair in max)
            {
                if (!s.Resources.TryGetValue(pair.Key, out var have)) errors.Add($"The resource '{pair.Key}' is missing.");
                else if (have != pair.Value) errors.Add($"'{pair.Key}' starts full at {pair.Value} (has {have}).");
            }

            foreach (var key in s.Resources.Keys)
                if (!max.ContainsKey(key)) errors.Add($"'{key}' is not a level 1 resource.");
            var sc = rules.Class.Spellcasting;
            for (var slot = 2; slot <= 9; slot++)
                if (s.Resources.ContainsKey("spell_slots_" + slot)) errors.Add($"A level 1 character has no level {slot} spell slots.");
            if (sc != null && (sc.Progression == CasterProgression.Full || sc.Progression == CasterProgression.Half))
            {
                var want = RpgCatalog.SlotsAt(sc.Progression, 1, 1);
                var has = s.Resources.TryGetValue("spell_slots_1", out var n) ? n : 0;
                if (has != want) errors.Add($"{rules.Class.Name} has {want} 1st-level slots at level 1 (has {has}).");
            }

            if (sc != null && sc.Progression == CasterProgression.Pact && (!s.Resources.TryGetValue("pact_slots", out var pact) || pact != 1))
                errors.Add("A level 1 warlock has one pact slot.");

            // Spells: nothing above 1st level, within the level 1 counts.
            CheckSpells(s, rules, "class:cantrips", true, rules.CantripsKnown, errors);
            CheckSpells(s, rules, "class:spells", false, rules.PreparedSpells, errors);
            CheckSpells(s, rules, "class:spellbook", false, 6, errors);
            var always = new HashSet<string>(RpgCatalog.AlwaysPrepared(rules), StringComparer.Ordinal);
            var prepared = 0;
            foreach (var id in s.Spellcasting?.Prepared ?? new List<string>())
            {
                var spell = SpellCatalog.Find(id);
                if (spell == null) { errors.Add($"Prepared spell '{id}' is unknown."); continue; }
                if (spell.Level > 1) errors.Add($"{spell.Name} (level {spell.Level}) is prepared at level 1.");
                if (!always.Contains(id) && !spell.IsCantrip) prepared++;
            }

            if (sc != null && prepared > rules.PreparedSpells) errors.Add($"{prepared} spells are prepared; level 1 allows {rules.PreparedSpells}.");
            if (!s.StartingEquipmentGranted) errors.Add("A new character has received their starting equipment.");
            return errors;
        }

        private static void CheckSpells(CharacterSheetData s, CharacterRules rules, string choiceId, bool cantrips, int limit, List<string> errors)
        {
            var picked = s.Chosen(choiceId);
            if (picked.Count > limit) errors.Add($"{picked.Count} picks in '{choiceId}'; level 1 allows {limit}.");
            foreach (var id in picked)
            {
                var spell = SpellCatalog.Find(id);
                if (spell == null) { errors.Add($"'{id}' in '{choiceId}' is not a spell."); continue; }
                if (cantrips && !spell.IsCantrip) errors.Add($"{spell.Name} is not a cantrip.");
                if (!cantrips && spell.Level > 1) errors.Add($"{spell.Name} (level {spell.Level}) is chosen at level 1.");
            }
        }
    }
}
