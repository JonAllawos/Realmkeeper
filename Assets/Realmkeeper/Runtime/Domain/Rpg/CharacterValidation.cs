using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// Rules a character must satisfy (Phase 6). Used by <see cref="WorldValidator"/> on every commit and load,
    /// and by the creation command before it applies anything, so an invalid character can never be saved.
    /// </summary>
    public static class CharacterValidation
    {
        /// <summary>Every problem with the player's character; empty when it is valid.</summary>
        public static IReadOnlyList<string> Check(PlayerCharacterData player)
        {
            var errors = new List<string>();
            var s = player?.Rpg;
            if (s == null)
            {
                errors.Add("The player has no character record.");
                return errors;
            }

            if (s.Title != null && s.Title.Length > CharacterSheetData.MaxTitleLength) errors.Add($"A title is at most {CharacterSheetData.MaxTitleLength} characters.");
            if (s.History != null && s.History.Length > CharacterSheetData.MaxHistoryLength) errors.Add($"A history is at most {CharacterSheetData.MaxHistoryLength} characters.");

            if (s.State != CreationState.Complete)
            {
                // Legacy and pending characters have nothing assigned, nothing granted and nothing invented.
                if (s.SpeciesId != null || s.BackgroundId != null || s.ClassId != null || s.SubclassId != null)
                    errors.Add("A character not yet created has a species, background or class.");
                if (s.Choices.Count > 0) errors.Add("A character not yet created has creation choices.");
                if (s.StartingEquipmentGranted) errors.Add("Starting equipment was granted before the character was created.");
                if (s.Level != 1 || s.Experience != 0) errors.Add("A character not yet created has levels or experience.");
                return errors;
            }

            var rules = new CharacterRules(s);
            if (rules.Species == null) errors.Add($"Unknown species '{s.SpeciesId}'.");
            if (rules.Background == null) errors.Add($"Unknown background '{s.BackgroundId}'.");
            if (rules.Class == null) errors.Add($"Unknown class '{s.ClassId}'.");
            if (errors.Count > 0) return errors;

            if (s.Level < 1 || s.Level > AbilityRules.MaxLevel) errors.Add($"Level {s.Level} is impossible.");
            if (s.Experience < 0) errors.Add("Experience cannot be negative.");
            if (s.SubclassId != null && s.Level < rules.Class.SubclassLevel) errors.Add($"A subclass is chosen at level {rules.Class.SubclassLevel}.");

            var scoreError = AbilityRules.Check(s.Method, s.BaseScores);
            if (scoreError != null) errors.Add(scoreError);
            foreach (var a in AbilityRules.All)
                if (rules.BaseScore(a) + rules.SpeciesBonus(a) > AbilityRules.MaxScore) errors.Add($"{a} exceeds {AbilityRules.MaxScore}.");

            CheckChoices(rules, errors);

            var max = rules.MaxHp;
            if (max < 1) errors.Add("Maximum hit points must be at least 1.");
            if (s.CurrentHp < 0 || s.CurrentHp > max) errors.Add($"Hit points {s.CurrentHp} are outside 0–{max}.");
            if (s.TemporaryHp < 0) errors.Add("Temporary hit points cannot be negative.");

            foreach (var pair in s.Resources)
            {
                var r = rules.FindResource(pair.Key);
                if (r == null) errors.Add($"Unknown resource '{pair.Key}'.");
                else if (pair.Value < 0 || pair.Value > rules.ResourceMax(r)) errors.Add($"{r.Name} uses {pair.Value} are outside 0–{rules.ResourceMax(r)}.");
            }

            if (!s.StartingEquipmentGranted) errors.Add("A created character must have received their starting equipment.");
            if (player.Species != rules.Species.Name) errors.Add("The player's species name does not match their species.");
            if (player.Background != rules.Background.Name) errors.Add("The player's background name does not match their background.");
            if (!rules.Species.Appearance.AllowsBeard && player.Appearance?.BeardId != null) errors.Add($"{rules.Species.Name} cannot be shown with a beard.");
            return errors;
        }

        private static void CheckChoices(CharacterRules rules, List<string> errors)
        {
            var s = rules.Sheet;
            var known = new HashSet<string>(StringComparer.Ordinal);
            var fixedSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in rules.GrantedSkills()) fixedSkills.Add(k);

            // Class skills: the class's number, from its list, no repeats, none already granted.
            known.Add(ClassDefinition.SkillChoiceId);
            var classSkills = s.Chosen(ClassDefinition.SkillChoiceId);
            if (classSkills.Count != rules.Class.SkillChoices) errors.Add($"{rules.Class.Name} chooses {rules.Class.SkillChoices} skills (got {classSkills.Count}).");
            var picked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in classSkills)
            {
                if (!SkillRules.TryParse(k, out var skill) || !Contains(rules.Class.SkillOptions, skill)) errors.Add($"'{k}' is not a {rules.Class.Name} skill.");
                else if (!picked.Add(k)) errors.Add($"{k} is chosen twice.");
                else if (fixedSkills.Contains(k)) errors.Add($"{k} is already granted by your species or background; choose another.");
            }

            var all = new List<ChoiceDefinition>();
            all.AddRange(rules.Species.Choices);
            all.AddRange(rules.Class.Choices);
            foreach (var c in all)
            {
                known.Add(c.Id);
                var chosen = s.Chosen(c.Id);
                if (chosen.Count != c.Count) { errors.Add($"{c.Name}: choose {c.Count} (got {chosen.Count})."); continue; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var k in chosen)
                {
                    if (!seen.Add(k)) { errors.Add($"{c.Name}: '{k}' is chosen twice."); continue; }
                    switch (c.Kind)
                    {
                        case ChoiceKind.Option:
                            var ok = false;
                            foreach (var o in c.Options) if (o.Id == k) ok = true;
                            if (!ok) errors.Add($"{c.Name}: '{k}' is not an option.");
                            break;
                        case ChoiceKind.Skill:
                            if (!SkillRules.TryParse(k, out var sk) || (c.Skills.Count > 0 && !Contains(c.Skills, sk))) errors.Add($"{c.Name}: '{k}' is not allowed.");
                            else if (fixedSkills.Contains(k) || picked.Contains(k)) errors.Add($"{c.Name}: {k} is already proficient; choose another.");
                            picked.Add(k);
                            break;
                        case ChoiceKind.Expertise:
                            if (!SkillRules.TryParse(k, out var ex) || rules.SkillRank(ex) == ProficiencyRank.None) errors.Add($"{c.Name}: you must be proficient in '{k}'.");
                            break;
                    }
                }
            }

            // Starting equipment: one option in every class group.
            foreach (var g in rules.Class.Equipment)
            {
                var id = EquipmentChoiceId(g);
                known.Add(id);
                var chosen = s.Chosen(id);
                if (chosen.Count != 1) { errors.Add($"Starting equipment '{g.Id}': choose one option."); continue; }
                var ok = false;
                foreach (var o in g.Options) if (o.Id == chosen[0]) ok = true;
                if (!ok) errors.Add($"Starting equipment '{g.Id}': '{chosen[0]}' is not an option.");
            }

            foreach (var key in s.Choices.Keys) if (!known.Contains(key)) errors.Add($"Unknown creation choice '{key}'.");
        }

        public static string EquipmentChoiceId(EquipmentGroup g) => "equipment:" + g.Id;

        private static bool Contains(IReadOnlyList<Skill> list, Skill s)
        {
            foreach (var x in list) if (x == s) return true;
            return false;
        }
    }
}
