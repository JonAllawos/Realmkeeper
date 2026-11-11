using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// Rules a character must satisfy (Phase 6; levels 2–20 since Phase 7). <see cref="CheckSheet"/> checks any
    /// character's RPG record — the player's, a companion's, an NPC's — and <see cref="Check"/> adds what only
    /// the player has (names shown on the player, the look, the starting kit). Used by <see cref="WorldValidator"/>
    /// on every commit and load, and by the creation and advancement commands before they apply anything, so an
    /// invalid character can never be saved.
    /// </summary>
    public static class CharacterValidation
    {
        /// <summary>Every problem with the player's character; empty when it is valid.</summary>
        public static IReadOnlyList<string> Check(PlayerCharacterData player)
        {
            var s = player?.Rpg;
            if (s == null) return new[] { "The player has no character record." };
            var errors = new List<string>(CheckSheet(s));
            if (s.State != CreationState.Complete || errors.Count > 0) return errors;
            var rules = new CharacterRules(s);
            if (rules.Species == null || rules.Background == null) return errors;
            if (player.Species != rules.Species.Name) errors.Add("The player's species name does not match their species.");
            if (player.Background != rules.Background.Name) errors.Add("The player's background name does not match their background.");
            if (!rules.Species.Appearance.AllowsBeard && player.Appearance?.BeardId != null) errors.Add($"{rules.Species.Name} cannot be shown with a beard.");
            return errors;
        }

        /// <summary>Phase 9: the prepared list fits the class, level and book; the preparation window and rest log are in range.</summary>
        private static void CheckSpellcasting(CharacterSheetData s, List<string> errors)
        {
            var sc = s.Spellcasting;
            if (sc == null) { errors.Add("The character has no spellcasting record."); return; }
            if (sc.PreparationChanges < -1 || sc.PreparationChanges > 1) errors.Add("The preparation window is out of range.");
            if (sc.RestLog.Count > SpellcastingData.MaxRestLog) errors.Add("More than " + SpellcastingData.MaxRestLog + " rests are kept.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in sc.RestLog) if (string.IsNullOrEmpty(r.Key) || !keys.Add(r.Key)) errors.Add("A rest is recorded twice.");
            if (s.State != CreationState.Complete) { if (sc.Prepared.Count > 0) errors.Add("A character not yet created has prepared spells."); return; }
            var rules = new CharacterRules(s);
            if (rules.Class == null) return;
            foreach (var e in new Realmkeeper.Domain.Magic.SpellAccess(rules).StoredPreparedProblems()) errors.Add(e);
        }

        /// <summary>Phase 8: death saves 0–3 each; stable and dead only at 0 hit points, and never both.</summary>
        private static void CheckDeathState(CharacterSheetData s, List<string> errors)
        {
            if (s.DeathSaveSuccesses < 0 || s.DeathSaveSuccesses > 3 || s.DeathSaveFailures < 0 || s.DeathSaveFailures > 3) errors.Add("Death saves are between 0 and 3.");
            if (s.IsStable && s.IsDead) errors.Add("A character cannot be both stable and dead.");
            if ((s.IsStable || s.IsDead) && s.CurrentHp > 0) errors.Add("Only a character at 0 hit points can be stable or dead.");
            if (s.CurrentHp > 0 && (s.DeathSaveSuccesses > 0 || s.DeathSaveFailures > 0)) errors.Add("A conscious character has no death saves.");
            if (!s.IsDead && s.DeathSaveFailures >= 3) errors.Add("Three failed death saves means death.");
        }

        /// <summary>Every problem with a character record (any character); empty when it is valid.</summary>
        public static IReadOnlyList<string> CheckSheet(CharacterSheetData s)
        {
            var errors = new List<string>();
            if (s == null)
            {
                errors.Add("There is no character record.");
                return errors;
            }

            if (s.Title != null && s.Title.Length > CharacterSheetData.MaxTitleLength) errors.Add($"A title is at most {CharacterSheetData.MaxTitleLength} characters.");
            if (s.History != null && s.History.Length > CharacterSheetData.MaxHistoryLength) errors.Add($"A history is at most {CharacterSheetData.MaxHistoryLength} characters.");
            CheckExperience(s, errors);
            CheckDeathState(s, errors);
            CheckSpellcasting(s, errors);

            if (s.State != CreationState.Complete)
            {
                // Legacy and pending characters have nothing assigned, nothing granted and nothing invented.
                // They may already hold experience (awards are kept and count once the character is created).
                if (s.SpeciesId != null || s.BackgroundId != null || s.ClassId != null || s.SubclassId != null)
                    errors.Add("A character not yet created has a species, background or class.");
                if (s.Choices.Count > 0) errors.Add("A character not yet created has creation choices.");
                if (s.StartingEquipmentGranted) errors.Add("Starting equipment was granted before the character was created.");
                if (s.Level != 1 || s.Advancements.Count > 0) errors.Add("A character not yet created has levels.");
                return errors;
            }

            var rules = new CharacterRules(s);
            if (rules.Species == null) errors.Add($"Unknown species '{s.SpeciesId}'.");
            if (rules.Background == null) errors.Add($"Unknown background '{s.BackgroundId}'.");
            if (rules.Class == null) errors.Add($"Unknown class '{s.ClassId}'.");
            if (errors.Count > 0) return errors;

            if (s.Level < 1 || s.Level > AbilityRules.MaxLevel)
            {
                errors.Add($"Level {s.Level} is impossible.");
                return errors;
            }

            if (s.Level > ProgressionRules.LevelFor(s.Experience)) errors.Add($"Level {s.Level} needs {ProgressionRules.ExperienceFor(s.Level)} experience (has {s.Experience}).");
            if (s.RulesVersion < 6 || s.RulesVersion > ProgressionRules.CurrentRulesVersion) errors.Add($"Unknown rules version {s.RulesVersion}.");
            CheckAdvancements(s, errors);

            var scoreError = AbilityRules.Check(s.Method, s.BaseScores);
            if (scoreError != null) errors.Add(scoreError);
            foreach (var a in AbilityRules.All)
                if (rules.BaseScore(a) + rules.SpeciesBonus(a) > AbilityRules.MaxScore) errors.Add($"{a} exceeds {AbilityRules.MaxScore}.");

            CheckSubclass(s, rules, errors);
            var known = new HashSet<string>(StringComparer.Ordinal);
            CheckCreationChoices(rules, errors, known);
            CheckLevelChoices(s, rules, errors, known);
            foreach (var key in s.Choices.Keys) if (!known.Contains(key)) errors.Add($"Unknown creation choice or premature level choice '{key}'.");

            var max = rules.MaxHp;
            if (max < 1) errors.Add("Maximum hit points must be at least 1.");
            if (s.CurrentHp < 0 || s.CurrentHp > max) errors.Add($"Hit points {s.CurrentHp} are outside 0–{max}.");
            if (s.TemporaryHp < 0) errors.Add("Temporary hit points cannot be negative.");

            var available = rules.ResourceMaximums();
            foreach (var pair in s.Resources)
            {
                if (!available.TryGetValue(pair.Key, out var cap)) errors.Add($"Unknown resource '{pair.Key}' at level {s.Level}.");
                else if (pair.Value < 0 || pair.Value > cap) errors.Add($"{rules.FindResource(pair.Key).Name} uses {pair.Value} are outside 0–{cap}.");
            }

            foreach (var pair in available)
                if (!s.Resources.ContainsKey(pair.Key)) errors.Add($"The resource '{pair.Key}' is missing.");

            if (!s.StartingEquipmentGranted) errors.Add("A created character must have received their starting equipment.");
            return errors;
        }

        private static void CheckExperience(CharacterSheetData s, List<string> errors)
        {
            if (s.Experience < 0) errors.Add("Experience cannot be negative.");
            if (s.Experience > ProgressionRules.MaxExperience) errors.Add($"Experience is at most {ProgressionRules.MaxExperience}.");
            long sum = 0;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in s.ExperienceLog)
            {
                if (a == null) { errors.Add("An experience award is empty."); continue; }
                if (string.IsNullOrEmpty(a.Key)) errors.Add("An experience award has no key.");
                else if (!keys.Add(a.Key)) errors.Add($"The experience award '{a.Key}' is recorded twice.");
                if (a.Amount < 1 || a.Amount > ProgressionRules.MaxAward) errors.Add($"The experience award '{a.Key}' of {a.Amount} is outside 1–{ProgressionRules.MaxAward}.");
                sum += a.Amount;
            }

            if (sum != s.Experience) errors.Add($"Experience {s.Experience} does not match its awards ({sum}).");
        }

        private static void CheckAdvancements(CharacterSheetData s, List<string> errors)
        {
            if (s.Advancements.Count != s.Level - 1)
            {
                errors.Add($"A level-{s.Level} character needs {s.Level - 1} advancement records (has {s.Advancements.Count}).");
                return;
            }

            for (var i = 0; i < s.Advancements.Count; i++)
                if (s.Advancements[i] == null || s.Advancements[i].Level != i + 2) errors.Add($"Advancement record {i + 1} should be for level {i + 2}.");
        }

        private static void CheckSubclass(CharacterSheetData s, CharacterRules rules, List<string> errors)
        {
            var cls = rules.Class;
            if (s.Level < cls.SubclassLevel)
            {
                if (s.SubclassId != null) errors.Add($"A subclass is chosen at level {cls.SubclassLevel}.");
                return;
            }

            var sub = RpgCatalog.FindSubclass(s.SubclassId);
            if (s.SubclassId == null) errors.Add($"A level-{s.Level} {cls.Name} must have chosen a {cls.SubclassTitle}.");
            else if (sub == null || sub.ClassId != cls.Id) errors.Add($"'{s.SubclassId}' is not a {cls.Name} subclass.");
        }

        // ----- Level 1 (creation) -----------------------------------------------------------------------

        private static void CheckCreationChoices(CharacterRules rules, List<string> errors, HashSet<string> known)
        {
            var s = rules.Sheet;
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
                if (chosen.Count == 0 && c.IntroducedInRules > s.RulesVersion) continue; // outstanding: made at the next advancement
                if (!c.CountAllowed(chosen.Count)) { errors.Add($"{c.Name}: choose {c.Count} (got {chosen.Count})."); continue; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var k in chosen)
                {
                    if (!seen.Add(k)) { errors.Add($"{c.Name}: '{k}' is chosen twice."); continue; }
                    switch (c.Kind)
                    {
                        case ChoiceKind.Option:
                            if (FindOption(c, k) == null) errors.Add($"{c.Name}: '{k}' is not an option.");
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
        }

        // ----- Levels 2–20 (Phase 7) --------------------------------------------------------------------

        private static void CheckLevelChoices(CharacterSheetData s, CharacterRules rules, List<string> errors, HashSet<string> known)
        {
            foreach (var slot in rules.ChoiceSlots) known.Add(slot.Choice.Id);
            for (var level = 2; level <= s.Level; level++)
            {
                var any = false;
                foreach (var slot in rules.ChoiceSlots)
                {
                    if (slot.Level != level || slot.Choice.Kind == ChoiceKind.Subclass) continue;
                    any = true;
                    var c = slot.Choice;
                    var chosen = s.Chosen(c.Id);
                    if (chosen.Count == 0 && c.IntroducedInRules > s.RulesVersion) continue;
                    if (!c.CountAllowed(chosen.Count)) errors.Add($"Level {level} {c.Name}: choose {c.Count} (got {chosen.Count}).");
                }

                if (!any) continue;
                foreach (var pair in PickProblems(s, level))
                    foreach (var p in pair.Value) errors.Add($"Level {level} {p}");
            }
        }

        /// <summary>
        /// Why the picks made at one level (2–20) are not allowed, by choice ID (counts are not checked here). The
        /// sheet must already hold the picks and be at that level or higher. Shared by validation, the advancement
        /// window (to show which options are available) and <see cref="CharacterAdvancement.SuggestChoices"/>.
        /// </summary>
        public static Dictionary<string, List<string>> PickProblems(CharacterSheetData s, int level)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var view = s.Clone();
            view.Level = Math.Max(level, s.Level);
            var slots = CharacterProgression.Choices(view, view.Level);
            var pools = new Dictionary<string, List<(string value, int level)>>(StringComparer.Ordinal);
            var feats = new List<(string feat, string distinct, int level)>();
            foreach (var slot in slots)
            {
                if (slot.Choice.Pool != null)
                {
                    if (!pools.TryGetValue(slot.Choice.Pool, out var list)) pools[slot.Choice.Pool] = list = new List<(string, int)>();
                    foreach (var v in s.Chosen(slot.Choice.Id)) list.Add((v, slot.Level));
                }

                if (slot.Choice.Kind == ChoiceKind.Feat && slot.Level < level)
                {
                    var picked = s.Chosen(slot.Choice.Id);
                    var feat = picked.Count == 1 ? RpgCatalog.FindFeat(picked[0]) : null;
                    if (feat != null) feats.Add((feat.Id, feat.RepeatDistinct != null ? FirstOrNull(s.Chosen(slot.Choice.Id + ":" + feat.RepeatDistinct)) : null, slot.Level));
                }
            }

            var before = s.Clone();
            before.Level = level - 1;
            var prior = new CharacterRules(before);
            var newSkills = new HashSet<string>(StringComparer.Ordinal);
            var newLanguages = new HashSet<string>(StringComparer.Ordinal);
            var newExpertise = new HashSet<string>(StringComparer.Ordinal);
            var increases = new int[6];
            foreach (var slot in slots)
            {
                if (slot.Level != level || slot.Choice.Kind == ChoiceKind.Subclass) continue;
                var c = slot.Choice;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var k in s.Chosen(c.Id))
                {
                    string problem;
                    if (!seen.Add(k) && !(c.Kind == ChoiceKind.AbilityIncrease && c.AllowRepeat)) problem = $"'{k}' is chosen twice.";
                    else problem = CheckPick(s, prior, c, k, level, pools, newSkills, newLanguages, newExpertise, increases, feats);
                    if (problem == null && c.Pool != null && pools.TryGetValue(c.Pool, out var pool))
                    {
                        var count = 0;
                        foreach (var (v, _) in pool) if (v == k) count++;
                        if (count > 1) problem = $"{CharacterAdvancement.NameOf(c, k)} is already taken.";
                    }

                    if (problem == null) continue;
                    if (!result.TryGetValue(c.Id, out var list)) result[c.Id] = list = new List<string>();
                    list.Add($"{c.Name}: {problem}");
                }
            }

            return result;
        }

        /// <summary>Why one pick is not allowed at a level (null when it is). Shared with the advancement window.</summary>
        public static string CheckPick(CharacterSheetData s, CharacterRules prior, ChoiceDefinition c, string k, int level,
            IReadOnlyDictionary<string, List<(string value, int level)>> pools, HashSet<string> newSkills, HashSet<string> newLanguages,
            HashSet<string> newExpertise, int[] increases, List<(string feat, string distinct, int level)> feats)
        {
            switch (c.Kind)
            {
                case ChoiceKind.Option:
                {
                    var o = FindOption(c, k);
                    if (o == null) return $"'{k}' is not an option.";
                    var picks = new HashSet<string>(StringComparer.Ordinal);
                    if (c.Pool != null && pools != null && pools.TryGetValue(c.Pool, out var pool))
                        foreach (var (v, l) in pool) if (l <= level && v != k) picks.Add(v);
                    var unmet = prior.Unmet(o.Prerequisites, level, picks);
                    return unmet.Count > 0 ? $"{o.Name} needs {string.Join(" and ", unmet)}." : null;
                }

                case ChoiceKind.Skill:
                    if (!SkillRules.TryParse(k, out var sk) || (c.Skills.Count > 0 && !Contains(c.Skills, sk))) return $"'{k}' is not allowed.";
                    if (prior.SkillRank(sk) != ProficiencyRank.None || !newSkills.Add(k)) return $"you are already proficient in {SkillRules.Name(sk)}.";
                    return null;

                case ChoiceKind.Expertise:
                    if (!SkillRules.TryParse(k, out var ex) || (c.Skills.Count > 0 && !Contains(c.Skills, ex))) return $"'{k}' is not allowed.";
                    if (prior.SkillRank(ex) == ProficiencyRank.None && !newSkills.Contains(k)) return $"you must be proficient in {SkillRules.Name(ex)}.";
                    if (prior.SkillRank(ex) == ProficiencyRank.Expertise || !newExpertise.Add(k)) return $"you already have expertise in {SkillRules.Name(ex)}.";
                    return null;

                case ChoiceKind.Language:
                    if (RpgCatalog.FindLanguage(k) == null) return $"'{k}' is not a language.";
                    foreach (var l in prior.Languages) if (l == k) return $"you already know {RpgCatalog.FindLanguage(k).Name}.";
                    return newLanguages.Add(k) ? null : $"{RpgCatalog.FindLanguage(k).Name} is chosen twice.";

                case ChoiceKind.Feat:
                {
                    var feat = RpgCatalog.FindFeat(k);
                    if (feat == null) return $"'{k}' is not a feat.";
                    if ((feat.Category & c.FeatCategories) == 0) return $"{feat.Name} is not offered here.";
                    var unmet = prior.Unmet(feat.Prerequisites, level, null);
                    if (unmet.Count > 0) return $"{feat.Name} needs {string.Join(" and ", unmet)}.";
                    var distinct = feat.RepeatDistinct != null && s != null ? FirstOrNull(s.Chosen(c.Id + ":" + feat.RepeatDistinct)) : null;
                    foreach (var (f, d, l) in feats)
                    {
                        if (f != feat.Id || l >= level) continue;
                        if (!feat.Repeatable) return $"{feat.Name} can be taken only once.";
                        if (feat.RepeatDistinct != null && d == distinct) return $"{feat.Name} was already taken with that choice.";
                    }

                    feats?.Add((feat.Id, distinct, level));
                    return null;
                }

                case ChoiceKind.AbilityIncrease:
                    if (!Enum.TryParse<Ability>(k, out var a) || !Enum.IsDefined(typeof(Ability), a)) return $"'{k}' is not an ability.";
                    if (c.Abilities.Count > 0 && !ContainsAbility(c.Abilities, a)) return $"{a} cannot be raised by this choice.";
                    increases[(int)a]++;
                    if (prior.Score(a) + increases[(int)a] > c.Limit) return $"{a} would pass {c.Limit}.";
                    return null;

                default:
                    return null;
            }
        }

        public static string EquipmentChoiceId(EquipmentGroup g) => "equipment:" + g.Id;

        public static ChoiceOption FindOption(ChoiceDefinition c, string id)
        {
            foreach (var o in c.Options) if (o.Id == id) return o;
            return null;
        }

        private static string FirstOrNull(IReadOnlyList<string> list) => list.Count > 0 ? list[0] : null;

        private static bool Contains(IReadOnlyList<Skill> list, Skill s)
        {
            foreach (var x in list) if (x == s) return true;
            return false;
        }

        private static bool ContainsAbility(IReadOnlyList<Ability> list, Ability a)
        {
            foreach (var x in list) if (x == a) return true;
            return false;
        }
    }
}
