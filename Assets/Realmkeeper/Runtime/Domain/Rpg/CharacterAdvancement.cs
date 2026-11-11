using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>The choices made for one level-up (Phase 7): level, choices by ID, and the subclass if this is its level.</summary>
    public sealed class LevelChoices
    {
        public int Level { get; set; }
        public Dictionary<string, List<string>> Choices { get; set; } = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public string SubclassId { get; set; }

        public LevelChoices Clone()
        {
            var c = new LevelChoices { Level = Level, SubclassId = SubclassId };
            foreach (var pair in Choices) c.Choices[pair.Key] = new List<string>(pair.Value);
            return c;
        }
    }

    /// <summary>What one level-up would change (Phase 7), for review before confirming.</summary>
    public sealed class AdvancementPreview
    {
        public int FromLevel { get; set; }
        public int ToLevel { get; set; }
        public int HitPointsBefore { get; set; }
        public int HitPointsAfter { get; set; }
        public int HitPointGain => HitPointsAfter - HitPointsBefore;
        public Breakdown HitPointsBreakdown { get; set; }
        public int ProficiencyBefore { get; set; }
        public int ProficiencyAfter { get; set; }
        public List<FeatureEntry> NewFeatures { get; } = new List<FeatureEntry>();
        public List<FeatureEntry> ReplacedFeatures { get; } = new List<FeatureEntry>();
        public List<(string name, int before, int after)> Resources { get; } = new List<(string, int, int)>();
        public List<(string name, string before, string after)> Scalings { get; } = new List<(string, string, string)>();
        public List<(Ability ability, int before, int after)> Scores { get; } = new List<(Ability, int, int)>();

        /// <summary>Choices this level asks for (outstanding older choices first, then this level's, with a picked feat's own choices).</summary>
        public List<ChoiceSlot> Choices { get; } = new List<ChoiceSlot>();

        public CharacterRules After { get; set; }
    }

    /// <summary>
    /// Advancement (Phase 7), for any character sheet. Experience only makes a character eligible; a level is
    /// gained by applying it, with every choice the level asks for, in one transaction. Several levels can be
    /// applied together, each validated against the state the previous one left. Choices already made are
    /// never overwritten, so nothing is granted twice. Maximum hit points and resource maximums are derived;
    /// the current values rise by the same amount the maximums do (nothing is reset or refilled).
    /// </summary>
    public static class CharacterAdvancement
    {
        /// <summary>Levels the character's experience allows beyond their current level (0 when none or not created).</summary>
        public static int LevelsAvailable(CharacterSheetData s)
        {
            if (s == null || s.State != CreationState.Complete || RpgCatalog.FindClass(s.ClassId) == null) return 0;
            return Math.Max(0, Math.Min(AbilityRules.MaxLevel, ProgressionRules.LevelFor(s.Experience)) - s.Level);
        }

        public static bool CanAdvance(CharacterSheetData s) => LevelsAvailable(s) > 0;

        /// <summary>The choices one level-up asks for, given the picks so far (a picked feat adds its own choices).</summary>
        public static IReadOnlyList<ChoiceSlot> RequiredChoices(CharacterSheetData s, LevelChoices draft)
        {
            var next = Draft(s, draft);
            var list = new List<ChoiceSlot>();
            if (draft.Level == s.Level + 1) list.AddRange(CharacterProgression.Outstanding(Clone(s, s.Level)));
            list.AddRange(CharacterProgression.ChoicesAt(next, draft.Level));
            return list;
        }

        /// <summary>What the level-up would change with the picks so far (not validated: for showing consequences).</summary>
        public static AdvancementPreview Preview(CharacterSheetData s, LevelChoices draft)
        {
            var before = new CharacterRules(s);
            var next = Draft(s, draft);
            var after = new CharacterRules(next);
            var p = new AdvancementPreview
            {
                FromLevel = s.Level, ToLevel = draft.Level, HitPointsBefore = before.MaxHp, HitPointsAfter = after.MaxHp, HitPointsBreakdown = after.MaxHitPoints(),
                ProficiencyBefore = before.ProficiencyBonus, ProficiencyAfter = after.ProficiencyBonus, After = after,
            };
            var had = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in before.ActiveFeatures) had.Add(f.Source + "|" + f.Feature.Id);
            var still = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in after.ActiveFeatures)
            {
                still.Add(f.Feature.Id);
                if (!had.Contains(f.Source + "|" + f.Feature.Id)) p.NewFeatures.Add(f);
            }

            foreach (var f in before.ActiveFeatures) if (!still.Contains(f.Feature.Id)) p.ReplacedFeatures.Add(f);
            var oldMax = before.ResourceMaximums();
            foreach (var r in after.ResourceDefinitions)
            {
                var newMax = after.ResourceMax(r);
                var oldValue = oldMax.TryGetValue(r.Id, out var o) ? o : 0;
                if (newMax != oldValue) p.Resources.Add((r.Name, oldValue, newMax));
            }

            if (after.Class != null)
                foreach (var sc in after.Class.Scalings)
                    if (sc.At(s.Level) != sc.At(draft.Level)) p.Scalings.Add((sc.Name, sc.Text(s.Level), sc.Text(draft.Level)));
            foreach (var a in AbilityRules.All)
                if (before.Score(a) != after.Score(a)) p.Scores.Add((a, before.Score(a), after.Score(a)));
            p.Choices.AddRange(RequiredChoices(s, draft));
            return p;
        }

        /// <summary>
        /// Applies one or more level-ups in order. Returns the advanced sheet, or null with the reasons when any
        /// step is invalid (the input is never changed).
        /// </summary>
        public static CharacterSheetData Apply(CharacterSheetData s, IReadOnlyList<LevelChoices> steps, WorldDate date, out List<string> errors)
        {
            errors = new List<string>();
            if (s == null || s.State != CreationState.Complete) { errors.Add("Only a created character can advance."); return null; }
            if (steps == null || steps.Count == 0) { errors.Add("Nothing to advance."); return null; }
            var next = s.Clone();
            foreach (var step in steps)
            {
                if (step == null) { errors.Add("A level-up is empty."); return null; }
                var problems = Step(next, step, date);
                if (problems.Count > 0)
                {
                    foreach (var p in problems) errors.Add($"Level {step.Level}: {p}");
                    return null;
                }
            }

            foreach (var e in CharacterValidation.CheckSheet(next)) errors.Add(e);
            return errors.Count == 0 ? next : null;
        }

        private static List<string> Step(CharacterSheetData next, LevelChoices step, WorldDate date)
        {
            var problems = new List<string>();
            var cls = RpgCatalog.FindClass(next.ClassId);
            if (step.Level != next.Level + 1) { problems.Add($"the next level is {next.Level + 1}."); return problems; }
            if (step.Level > AbilityRules.MaxLevel) { problems.Add($"level {AbilityRules.MaxLevel} is the highest."); return problems; }
            if (ProgressionRules.LevelFor(next.Experience) < step.Level)
            {
                problems.Add($"needs {ProgressionRules.ExperienceFor(step.Level)} experience (has {next.Experience}).");
                return problems;
            }

            var before = new CharacterRules(next);
            var oldMax = before.MaxHp;
            var oldResources = before.ResourceMaximums();
            var outstanding = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in CharacterProgression.Outstanding(next)) outstanding.Add(slot.Choice.Id);

            next.Level = step.Level;
            if (step.Level == cls.SubclassLevel) next.SubclassId = step.SubclassId;
            else if (step.SubclassId != null) problems.Add($"a {cls.SubclassTitle} is chosen at level {cls.SubclassLevel}.");

            foreach (var pair in step.Choices)
            {
                if (pair.Value == null || pair.Value.Count == 0) continue;
                if (next.Choices.ContainsKey(pair.Key)) { problems.Add($"'{pair.Key}' was already chosen; choices are never changed or repeated."); continue; }
                next.Choices[pair.Key] = new List<string>(pair.Value);
            }

            var allowed = new HashSet<string>(outstanding, StringComparer.Ordinal);
            var required = new List<ChoiceSlot>();
            foreach (var slot in CharacterProgression.ChoicesAt(next, step.Level))
            {
                allowed.Add(slot.Choice.Id);
                required.Add(slot);
            }

            foreach (var key in step.Choices.Keys)
                if (!allowed.Contains(key) && step.Choices[key] != null && step.Choices[key].Count > 0) problems.Add($"'{key}' is not a choice at this level.");
            foreach (var slot in required)
            {
                if (slot.Choice.Kind == ChoiceKind.Subclass)
                {
                    if (next.SubclassId == null) problems.Add($"choose your {slot.Choice.Name}.");
                    continue;
                }

                if (!slot.Choice.CountAllowed(next.Chosen(slot.Choice.Id).Count)) problems.Add($"{slot.Choice.Name}: choose {slot.Choice.Count}.");
            }

            foreach (var id in outstanding)
                if (next.Chosen(id).Count == 0) problems.Add($"a choice from an earlier level is still open ({id}); make it with this level-up.");
            if (problems.Count > 0) return problems;
            next.RulesVersion = ProgressionRules.CurrentRulesVersion;

            var after = new CharacterRules(next);
            var newMax = after.MaxHp;
            next.CurrentHp = Math.Max(0, Math.Min(newMax, next.CurrentHp + (newMax - oldMax)));
            var resources = new Dictionary<string, int>(StringComparer.Ordinal);
            var changes = new List<string>();
            foreach (var pair in after.ResourceMaximums())
            {
                var max = pair.Value;
                if (oldResources.TryGetValue(pair.Key, out var oldCap))
                {
                    var current = next.Resources.TryGetValue(pair.Key, out var c) ? c : 0;
                    resources[pair.Key] = Math.Max(0, Math.Min(max, current + (max - oldCap)));
                    if (max != oldCap) changes.Add($"{after.FindResource(pair.Key).Name} {oldCap}→{max}");
                }
                else
                {
                    resources[pair.Key] = max;
                    changes.Add($"{after.FindResource(pair.Key).Name} {max}");
                }
            }

            next.Resources = resources;
            next.Advancements.Add(new AdvancementRecord { Level = step.Level, Date = date, HitPointsGained = newMax - oldMax, Summary = Summary(before, after, step, changes, newMax - oldMax) });
            return problems;
        }

        private static string Summary(CharacterRules before, CharacterRules after, LevelChoices step, List<string> resources, int hp)
        {
            var parts = new List<string> { $"+{hp.ToString(CultureInfo.InvariantCulture)} hit points" };
            if (step.SubclassId != null) parts.Add(RpgCatalog.FindSubclass(step.SubclassId)?.Name ?? step.SubclassId);
            var had = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in before.ActiveFeatures) had.Add(f.Feature.Id);
            foreach (var f in after.ActiveFeatures)
                if (!had.Contains(f.Feature.Id) && !CharacterRules.IsChoiceSlot(f.Feature) && f.Feature.Kind != FeatureKind.Choice) parts.Add(f.Feature.Name);
            foreach (var slot in CharacterProgression.ChoicesAt(after.Sheet, step.Level))
            {
                var c = slot.Choice;
                if (c.Kind == ChoiceKind.Subclass) continue;
                var picked = after.Sheet.Chosen(c.Id);
                if (picked.Count == 0) continue;
                if (c.Kind == ChoiceKind.AbilityIncrease)
                {
                    // "Charisma +2" rather than "Charisma, Charisma".
                    var counts = new List<string>();
                    foreach (var a in AbilityRules.All)
                    {
                        var n = 0;
                        foreach (var v in picked) if (v == a.ToString()) n++;
                        if (n > 0) counts.Add(a + " +" + n.ToString(CultureInfo.InvariantCulture));
                    }

                    parts.Add(string.Join(", ", counts));
                    continue;
                }

                var names = new List<string>();
                foreach (var v in picked) names.Add(NameOf(c, v));
                parts.Add((c.Kind == ChoiceKind.Feat ? "Feat" : c.Name) + ": " + string.Join(", ", names));
            }

            parts.AddRange(resources);
            return string.Join("; ", parts);
        }

        /// <summary>
        /// Fills every choice the next level asks for with the first allowed pick, deterministically: the subclass's
        /// first option, Ability Score Improvement into the class's primary ability (then Constitution), and catalog
        /// order elsewhere. This is the hook for characters nobody steers — companions and NPCs advancing through
        /// the same rules — and for tests. <paramref name="prefer"/> may rank candidates for a choice.
        /// </summary>
        public static LevelChoices SuggestChoices(CharacterSheetData s, int level, Func<ChoiceDefinition, IEnumerable<string>> prefer = null)
        {
            var draft = new LevelChoices { Level = level };
            for (var pass = 0; pass < 6; pass++)
            {
                var changed = false;
                foreach (var slot in RequiredChoices(s, draft))
                {
                    var c = slot.Choice;
                    if (c.Kind == ChoiceKind.Subclass)
                    {
                        if (draft.SubclassId == null && c.Options.Count > 0) { draft.SubclassId = c.Options[0].Id; changed = true; }
                        continue;
                    }

                    if (!draft.Choices.TryGetValue(c.Id, out var list)) draft.Choices[c.Id] = list = new List<string>();
                    if (list.Count >= c.Count) continue;
                    changed = true;
                    foreach (var candidate in Candidates(s, c, prefer))
                    {
                        if (list.Count >= c.Count) break;
                        list.Add(candidate);
                        if (c.Level >= 2)
                        {
                            var view = Draft(s, draft);
                            view.Level = Math.Max(view.Level, c.Level);
                            if (CharacterValidation.PickProblems(view, c.Level).ContainsKey(c.Id)) list.RemoveAt(list.Count - 1);
                        }
                        else if (CharacterValidation.FindOption(c, candidate) == null) list.RemoveAt(list.Count - 1);
                    }
                }

                if (!changed) break;
            }

            return draft;
        }

        private static IEnumerable<string> Candidates(CharacterSheetData s, ChoiceDefinition c, Func<ChoiceDefinition, IEnumerable<string>> prefer)
        {
            if (prefer != null)
                foreach (var v in prefer(c) ?? Array.Empty<string>()) yield return v;
            switch (c.Kind)
            {
                case ChoiceKind.Option:
                    foreach (var o in c.Options) yield return o.Id;
                    break;
                case ChoiceKind.Skill:
                case ChoiceKind.Expertise:
                    foreach (var sk in c.Skills.Count > 0 ? c.Skills : SkillRules.All) yield return sk.ToString();
                    break;
                case ChoiceKind.Language:
                    foreach (var l in RpgCatalog.Languages) yield return l.Id;
                    break;
                case ChoiceKind.Feat:
                    yield return "ability_score_improvement";
                    foreach (var f in RpgCatalog.Feats) if ((f.Category & c.FeatCategories) != 0) yield return f.Id;
                    break;
                case ChoiceKind.AbilityIncrease:
                    var cls = RpgCatalog.FindClass(s.ClassId);
                    var order = new List<Ability>();
                    if (cls != null) order.AddRange(cls.PrimaryAbilities);
                    order.Add(Ability.Constitution);
                    order.AddRange(AbilityRules.All);
                    foreach (var a in order)
                        if (c.Abilities.Count == 0 || Contains(c.Abilities, a))
                            for (var i = 0; i < (c.AllowRepeat ? c.Count : 1); i++) yield return a.ToString();
                    break;
            }
        }

        private static bool Contains(IReadOnlyList<Ability> list, Ability a)
        {
            foreach (var x in list) if (x == a) return true;
            return false;
        }

        /// <summary>The display name of a pick (option, feat, language, skill or ability).</summary>
        public static string NameOf(ChoiceDefinition c, string value)
        {
            switch (c.Kind)
            {
                case ChoiceKind.Option: return CharacterValidation.FindOption(c, value)?.Name ?? value;
                case ChoiceKind.Feat: return RpgCatalog.FindFeat(value)?.Name ?? value;
                case ChoiceKind.Language: return RpgCatalog.FindLanguage(value)?.Name ?? value;
                case ChoiceKind.Skill:
                case ChoiceKind.Expertise: return SkillRules.TryParse(value, out var sk) ? SkillRules.Name(sk) : value;
                case ChoiceKind.Subclass: return RpgCatalog.FindSubclass(value)?.Name ?? value;
                default: return value;
            }
        }

        private static CharacterSheetData Draft(CharacterSheetData s, LevelChoices draft)
        {
            var next = Clone(s, draft.Level);
            if (draft.SubclassId != null) next.SubclassId = draft.SubclassId;
            foreach (var pair in draft.Choices)
                if (pair.Value != null && pair.Value.Count > 0 && !next.Choices.ContainsKey(pair.Key)) next.Choices[pair.Key] = new List<string>(pair.Value);
            return next;
        }

        private static CharacterSheetData Clone(CharacterSheetData s, int level)
        {
            var c = s.Clone();
            c.Level = level;
            return c;
        }
    }
}
