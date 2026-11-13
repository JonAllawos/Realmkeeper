using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Application
{
    /// <summary>A page of the level-up window: the overview, one required choice, or the review.</summary>
    public readonly struct AdvancementPage
    {
        public AdvancementPage(string title, ChoiceSlot? choice)
        {
            Title = title;
            Choice = choice;
        }

        public string Title { get; }
        public ChoiceSlot? Choice { get; }
        public bool IsOverview => Choice == null && Title == AdvancementDraft.OverviewTitle;
        public bool IsReview => Choice == null && Title == AdvancementDraft.ReviewTitle;
    }

    /// <summary>
    /// One level-up being prepared (Phase 7), engine-free: the pages (overview, each choice the level asks for —
    /// including choices a picked feat adds — and the review), the picks so far, what each option would cost or
    /// why it is unavailable, and the problems left. Nothing is applied until <see cref="ToCommand"/> is
    /// dispatched. Works on a copy of any character sheet.
    /// </summary>
    public sealed class AdvancementDraft
    {
        public const string OverviewTitle = "Overview";
        public const string ReviewTitle = "Review";

        public AdvancementDraft(CharacterSheetData sheet)
        {
            Sheet = sheet?.Clone() ?? throw new ArgumentNullException(nameof(sheet));
            // Phase 22 (ADR-0109): pick up where the player left off — the picks kept on the sheet for this level.
            var kept = Sheet.PendingAdvancement;
            Choices = kept != null && kept.Level == Sheet.Level + 1 ? kept.Clone() : new LevelChoices { Level = Sheet.Level + 1 };
            Resumed = kept != null && kept.Level == Sheet.Level + 1;
        }

        /// <summary>Phase 22: the draft began from picks kept on the sheet (the player had started this level-up before).</summary>
        public bool Resumed { get; }

        /// <summary>Phase 22: the command that keeps these picks on the sheet (to resume later).</summary>
        public SaveAdvancementDraftCommand ToSaveCommand() => new SaveAdvancementDraftCommand(Clean());

        /// <summary>Phase 22: choices still to make (a subclass counts as one).</summary>
        public int OpenChoices => CharacterAdvancement.OpenChoices(Sheet, Choices);

        public CharacterSheetData Sheet { get; }
        public LevelChoices Choices { get; }
        public int Level => Choices.Level;
        public int PageIndex { get; private set; }

        public IReadOnlyList<ChoiceSlot> Required => CharacterAdvancement.RequiredChoices(Sheet, Choices);

        public IReadOnlyList<AdvancementPage> Pages
        {
            get
            {
                var pages = new List<AdvancementPage> { new AdvancementPage(OverviewTitle, null) };
                foreach (var slot in Required) pages.Add(new AdvancementPage(slot.Choice.Name, slot));
                pages.Add(new AdvancementPage(ReviewTitle, null));
                return pages;
            }
        }

        public AdvancementPage Page => Pages[Math.Min(PageIndex, Pages.Count - 1)];

        public void GoTo(int index) => PageIndex = Math.Max(0, Math.Min(Pages.Count - 1, index));
        public void Next() => GoTo(PageIndex + 1);
        public void Back() => GoTo(PageIndex - 1);

        public AdvancementPreview Preview() => CharacterAdvancement.Preview(Sheet, Choices);

        public IReadOnlyList<string> Picked(string choiceId) =>
            Choices.Choices.TryGetValue(choiceId, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Picks or unpicks a value. A single-pick choice swaps; a full choice refuses more. Changing a feat clears its own choices.</summary>
        public bool Toggle(ChoiceDefinition c, string value)
        {
            if (c.Kind == ChoiceKind.Subclass)
            {
                Choices.SubclassId = Choices.SubclassId == value ? null : value;
                ClearOrphans();
                return true;
            }

            if (!Choices.Choices.TryGetValue(c.Id, out var list)) Choices.Choices[c.Id] = list = new List<string>();
            if (c.Kind != ChoiceKind.AbilityIncrease && list.Remove(value))
            {
                ClearOrphans();
                return true;
            }

            if (c.Count == 1 && c.Kind != ChoiceKind.AbilityIncrease) list.Clear();
            if (list.Count >= c.Count) return false;
            list.Add(value);
            ClearOrphans();
            return true;
        }

        /// <summary>Removes one point from an ability increase.</summary>
        public bool Lower(ChoiceDefinition c, string ability) => Choices.Choices.TryGetValue(c.Id, out var list) && list.Remove(ability);

        public bool IsPicked(ChoiceDefinition c, string value) => c.Kind == ChoiceKind.Subclass ? Choices.SubclassId == value : Contains(Picked(c.Id), value);

        /// <summary>The start of the reason given when a choice simply has all its picks (the window shows no line for it).</summary>
        public const string FullPrefix = "Full:";

        /// <summary>
        /// Why a value cannot be picked now (null when it can, or when it is already picked). A rule that forbids the
        /// value (a prerequisite, a cap, a repeat) is reported before the choice merely being full.
        /// </summary>
        public string Unavailable(ChoiceDefinition c, string value)
        {
            if (IsPicked(c, value) && c.Kind != ChoiceKind.AbilityIncrease) return null;
            if (c.Kind == ChoiceKind.Subclass) return null;
            var trial = new AdvancementDraft(Sheet);
            foreach (var pair in Choices.Choices) trial.Choices.Choices[pair.Key] = new List<string>(pair.Value);
            trial.Choices.SubclassId = Choices.SubclassId;
            if (!trial.Choices.Choices.TryGetValue(c.Id, out var list)) trial.Choices.Choices[c.Id] = list = new List<string>();
            var full = false;
            if (c.Count == 1 && c.Kind != ChoiceKind.AbilityIncrease) list.Clear();
            else if (list.Count >= c.Count) full = true;
            list.Add(value);
            var rule = RuleProblem(trial, c, value);
            if (rule != null) return rule;
            return full ? $"{FullPrefix} {c.Name} already has {c.Count}; unpick one first." : null;
        }

        private string RuleProblem(AdvancementDraft trial, ChoiceDefinition c, string value)
        {
            if (c.Level < 2) return CharacterValidation.FindOption(c, value) == null ? "Not an option." : null;
            var view = trial.Sheet.Clone();
            view.Level = Level;
            if (trial.Choices.SubclassId != null) view.SubclassId = trial.Choices.SubclassId;
            foreach (var pair in trial.Choices.Choices) if (pair.Value.Count > 0 && !view.Choices.ContainsKey(pair.Key)) view.Choices[pair.Key] = pair.Value;
            var problems = CharacterValidation.PickProblems(view, c.Level);
            if (!problems.TryGetValue(c.Id, out var mine)) return null;
            var text = mine[mine.Count - 1];
            var colon = text.IndexOf(": ", StringComparison.Ordinal);
            return colon >= 0 ? text.Substring(colon + 2) : text;
        }

        /// <summary>What is still missing or wrong (empty when the level-up can be confirmed).</summary>
        public IReadOnlyList<string> Problems()
        {
            var result = new List<string>();
            CharacterAdvancement.Apply(Sheet, new[] { Clean() }, default, out var errors);
            foreach (var e in errors)
            {
                var text = e.StartsWith("Level " + Level + ": ", StringComparison.Ordinal) ? e.Substring(("Level " + Level + ": ").Length) : e;
                if (!result.Contains(text)) result.Add(text);
            }

            return result;
        }

        /// <summary>Problems on one page (its choice's count and picks).</summary>
        public IReadOnlyList<string> PageProblems(AdvancementPage page)
        {
            if (page.Choice == null) return page.IsReview ? Problems() : Array.Empty<string>();
            var c = page.Choice.Value.Choice;
            var list = new List<string>();
            if (c.Kind == ChoiceKind.Subclass)
            {
                if (Choices.SubclassId == null) list.Add($"Choose your {c.Name}.");
                return list;
            }

            var picked = Picked(c.Id).Count;
            if (!c.CountAllowed(picked)) list.Add($"Choose {c.Count} ({picked} chosen).");
            return list;
        }

        public bool PageComplete(AdvancementPage page) => page.Choice != null ? PageProblems(page).Count == 0 : !page.IsReview || Problems().Count == 0;

        public LevelChoices Clean()
        {
            var c = new LevelChoices { Level = Level, SubclassId = Choices.SubclassId };
            foreach (var pair in Choices.Choices) if (pair.Value.Count > 0) c.Choices[pair.Key] = new List<string>(pair.Value);
            return c;
        }

        public AdvanceCharacterCommand ToCommand() => new AdvanceCharacterCommand(Clean());

        /// <summary>Drops picks for choices that no longer exist (a feat's own choices after the feat changed).</summary>
        private void ClearOrphans()
        {
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in CharacterAdvancement.RequiredChoices(Sheet, Choices)) live.Add(slot.Choice.Id);
            foreach (var key in new List<string>(Choices.Choices.Keys)) if (!live.Contains(key)) Choices.Choices.Remove(key);
        }

        private static bool Contains(IReadOnlyList<string> list, string v)
        {
            foreach (var x in list) if (x == v) return true;
            return false;
        }
    }
}
