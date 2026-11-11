using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The level-up window (Phase 7): an overview of what the new level brings (hit points, features, resources,
    /// class-table columns, proficiency), one page per choice the level asks for (subclass, feat and its own
    /// choices, skills, expertise, languages, invocations, Metamagic, options — and any choice an earlier rules
    /// version left open), and a review. Pages keep their picks when you move back and forth; unavailable
    /// options say why. It edits an <see cref="AdvancementDraft"/> and raises <see cref="ConfirmRequested"/>;
    /// nothing is granted until the controller dispatches the command.
    /// </summary>
    public sealed class CharacterAdvancementWindow
    {
        private readonly VisualElement _steps;
        private readonly VisualElement _content;
        private readonly Label _title;
        private readonly Label _summary;
        private readonly Label _problems;
        private readonly Button _back;
        private readonly Button _next;
        private readonly Button _confirm;

        public CharacterAdvancementWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-levelup" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-creator");
            Root.AddToClassList("xk-hidden");

            var head = Row("xk-w-head");
            _title = Text("Advance", "xk-w-title");
            _title.name = "xk-lv-title";
            _title.style.flexGrow = 1;
            head.Add(_title);
            var close = Button("Later (Esc)", "xk-lv-close", () => CloseRequested?.Invoke());
            head.Add(close);
            head.style.flexShrink = 0;
            Root.Add(head);

            _steps = Row("xk-cc-steps");
            _steps.style.flexShrink = 0;
            Root.Add(_steps);

            var body = Row("xk-hub-body");
            _content = new ScrollView { name = "xk-lv-content" };
            _content.AddToClassList("xk-hub-content");
            body.Add(_content);
            var side = Column("xk-cc-side");
            _summary = Text("-", "xk-cc-summary");
            _summary.name = "xk-lv-summary";
            side.Add(_summary);
            body.Add(side);
            Root.Add(body);

            var foot = Row("xk-cc-foot");
            foot.style.flexShrink = 0;
            _problems = Text(string.Empty, "xk-cc-problems");
            _problems.name = "xk-lv-problems";
            _problems.style.flexGrow = 1;
            foot.Add(_problems);
            _back = Button("Back", "xk-lv-back", () => { Draft?.Back(); Render(); });
            _next = Button("Next", "xk-lv-next", () => { Draft?.Next(); Render(); });
            _confirm = Button("Confirm", "xk-lv-confirm", () => { if (Draft != null) ConfirmRequested?.Invoke(Draft.ToCommand()); });
            _confirm.AddToClassList("xk-button--primary");
            foot.Add(_back);
            foot.Add(_next);
            foot.Add(_confirm);
            Root.Add(foot);
            hudRoot.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public AdvancementDraft Draft { get; private set; }
        public CharacterSnapshot Character { get; private set; }
        public VisualElement Content => _content;
        public string Problems => _problems.text;

        public event Action CloseRequested;
        public event Action<AdvanceCharacterCommand> ConfirmRequested;

        public void Open(CharacterSnapshot character, string message = null)
        {
            Character = character;
            Draft = new AdvancementDraft(character.Player.Rpg);
            Root.RemoveFromClassList("xk-hidden");
            Render();
            if (message != null) ShowMessage(message, false);
        }

        public void Hide() => Root.AddToClassList("xk-hidden");

        public void ShowMessage(string text, bool error)
        {
            _problems.text = text;
            _problems.EnableInClassList("xk-message--error", error);
        }

        /// <summary>Selects a page by its title (tests and keyboard use).</summary>
        public bool GoTo(string title)
        {
            var pages = Draft.Pages;
            for (var i = 0; i < pages.Count; i++)
                if (pages[i].Title == title)
                {
                    Draft.GoTo(i);
                    Render();
                    return true;
                }

            return false;
        }

        public void Render()
        {
            if (Draft == null) return;
            var rules = Character.Rules;
            _title.text = $"Advance to level {Draft.Level}: {rules.Class.Name}";
            var pages = Draft.Pages;
            _steps.Clear();
            for (var i = 0; i < pages.Count; i++)
            {
                var index = i;
                var b = Button(i + 1 + "  " + pages[i].Title, "xk-lv-step-" + i, () => { Draft.GoTo(index); Render(); });
                b.AddToClassList("xk-cc-step");
                b.EnableInClassList("xk-cc-step--active", i == Draft.PageIndex);
                b.EnableInClassList("xk-cc-step--done", !pages[i].IsReview && Draft.PageComplete(pages[i]));
                _steps.Add(b);
            }

            _content.Clear();
            var page = Draft.Page;
            if (page.IsOverview) Overview();
            else if (page.IsReview) Review();
            else Choice(page.Choice.Value);

            var problems = Draft.PageProblems(page);
            _problems.text = problems.Count == 0 ? (page.IsReview ? $"Ready. Confirming grants level {Draft.Level} once." : "✓ Nothing more is needed here.") : string.Join("  ·  ", problems);
            _problems.EnableInClassList("xk-message--error", problems.Count > 0);
            _back.SetEnabled(Draft.PageIndex > 0);
            _next.EnableInClassList("xk-hidden", page.IsReview);
            _confirm.EnableInClassList("xk-hidden", !page.IsReview);
            _confirm.text = $"Confirm level {Draft.Level}";
            _confirm.SetEnabled(Draft.Problems().Count == 0);
            Summarise();
        }

        // ----- Pages -----------------------------------------------------------------------------------------

        private void Overview()
        {
            var p = Draft.Preview();
            var c = Character;
            _content.Add(Text($"{c.FullName} has {c.Experience.ToString("N0", CultureInfo.InvariantCulture)} experience: enough for level {Draft.Level}. Review what it brings, make its choices, then confirm.", "xk-w-note"));
            var cards = Row("xk-cc-wrap");
            cards.name = "xk-lv-cards";
            foreach (var (k, v) in new[]
                     {
                         ("Level", $"{p.FromLevel} → {p.ToLevel}"),
                         ("Hit points +" + p.HitPointGain, $"{p.HitPointsBefore} → {p.HitPointsAfter}"),
                         ("Proficiency", p.ProficiencyBefore == p.ProficiencyAfter ? "+" + p.ProficiencyAfter : $"+{p.ProficiencyBefore} → +{p.ProficiencyAfter}"),
                         ("Hit dice", $"{p.ToLevel} × d{c.Rules.HitDie}"),
                     })
            {
                var card = Column("xk-cc-card");
                card.Add(Text(v, "xk-cc-card-value"));
                card.Add(Text(k, "xk-cc-card-label"));
                cards.Add(card);
            }

            _content.Add(cards);
            _content.Add(Text($"Hit points: d{c.Rules.HitDie} average {c.Rules.HitDie / 2 + 1} + Constitution {Breakdown.Signed(c.Rules.Modifier(Ability.Constitution))}, plus any bonuses. Current hit points rise by the same amount; nothing is healed or refilled beyond that.", "xk-w-desc"));

            _content.Add(Text("New features", "xk-w-heading"));
            if (p.NewFeatures.Count == 0) _content.Add(Text("No new features at this level beyond its choices.", "xk-w-empty"));
            foreach (var f in p.NewFeatures) _content.Add(Feature(f.Feature, f.Source, p.After));
            foreach (var f in p.ReplacedFeatures) _content.Add(Pair("Replaced", f.Feature.Name + " (superseded by its improved form)"));

            if (p.Resources.Count > 0 || p.Scalings.Count > 0)
            {
                _content.Add(Text("Resources and class table", "xk-w-heading"));
                foreach (var (name, before, after) in p.Resources) _content.Add(Pair(name, before == 0 ? $"new: {after}" : $"{before} → {after}"));
                foreach (var (name, before, after) in p.Scalings) _content.Add(Pair(name, $"{before} → {after}"));
            }

            var outstanding = Character.Outstanding;
            if (outstanding.Count > 0)
            {
                _content.Add(Text("Open choices", "xk-w-heading"));
                _content.Add(Text("Your character was created before these choices existed; make them with this level-up.", "xk-cc-warn"));
                foreach (var o in outstanding) _content.Add(Pair(o.Choice.Name, o.Choice.Prompt));
            }

            if (p.Choices.Count > 0)
            {
                _content.Add(Text("Choices", "xk-w-heading"));
                foreach (var slot in p.Choices) _content.Add(Pair(slot.Choice.Name, slot.Choice.Prompt));
            }
        }

        private VisualElement Feature(FeatureDefinition f, string source, CharacterRules rules)
        {
            var box = Column("xk-cc-choice");
            box.Add(Text(f.Name + (f.ScalingId != null && rules.Class?.FindScaling(f.ScalingId) != null ? $"  ({rules.Class.FindScaling(f.ScalingId).Text(rules.Level)})" : string.Empty), "xk-w-name"));
            box.Add(Text(f.Description, "xk-w-desc"));
            var needs = CharacterRules.Needs(f);
            if (needs != IntegrationNeed.None) box.Add(Text("Takes full effect when " + NeedsText(needs) + " arrive" + (Count(needs) == 1 ? "s" : string.Empty) + ".", "xk-cc-source"));
            box.Add(Text(source, "xk-cc-source"));
            return box;
        }

        public static string NeedsText(IntegrationNeed n)
        {
            var parts = new List<string>();
            if ((n & IntegrationNeed.Combat) != 0) parts.Add("combat");
            if ((n & IntegrationNeed.Magic) != 0) parts.Add("spells");
            if ((n & IntegrationNeed.Rest) != 0) parts.Add("rests");
            if ((n & IntegrationNeed.Dialogue) != 0) parts.Add("skill checks in conversation");
            if ((n & IntegrationNeed.Exploration) != 0) parts.Add("exploration checks");
            return string.Join(" and ", parts);
        }

        private static int Count(IntegrationNeed n)
        {
            var c = 0;
            foreach (IntegrationNeed v in Enum.GetValues(typeof(IntegrationNeed))) if (v != IntegrationNeed.None && (n & v) != 0) c++;
            return c;
        }

        private void Choice(ChoiceSlot slot)
        {
            var c = slot.Choice;
            _content.Add(Text(c.Prompt + "  (" + slot.Source + ")", "xk-w-note"));
            switch (c.Kind)
            {
                case ChoiceKind.Subclass:
                    foreach (var o in c.Options)
                    {
                        var sub = RpgCatalog.FindSubclass(o.Id);
                        var box = Option(c, o.Id, sub.Name, sub.Description + "  ·  " + RpgCatalog.SourceText(sub.Source));
                        var levels = new List<string>();
                        foreach (var pair in sub.Features)
                        {
                            var names = new List<string>();
                            foreach (var f in pair.Value) names.Add(f.Name);
                            levels.Add(pair.Key + ": " + string.Join(", ", names));
                        }

                        levels.Sort((x, y) => int.Parse(x.Substring(0, x.IndexOf(':'))).CompareTo(int.Parse(y.Substring(0, y.IndexOf(':')))));
                        box.Add(Text(string.Join("  ·  ", levels), "xk-w-desc"));
                        _content.Add(box);
                    }

                    _content.Add(Text("Only the SRD subclass is offered for each class; further subclasses are deferred.", "xk-cc-source"));
                    break;

                case ChoiceKind.Option:
                    foreach (var o in c.Options)
                    {
                        var needs = o.Needs | CharacterRules.NeedsOf(o.Effects);
                        var prereq = new List<string>();
                        foreach (var p in o.Prerequisites) prereq.Add(CharacterProgression.Describe(p));
                        _content.Add(Option(c, o.Id, o.Name, o.Description + (prereq.Count > 0 ? "  ·  needs " + string.Join(", ", prereq) : string.Empty) +
                                                         (needs != IntegrationNeed.None ? "  ·  full effect with " + NeedsText(needs) : string.Empty)));
                    }

                    break;

                case ChoiceKind.Feat:
                    foreach (var f in RpgCatalog.Feats)
                    {
                        if ((f.Category & c.FeatCategories) == 0) continue;
                        var prereq = new List<string>();
                        foreach (var p in f.Prerequisites) prereq.Add(CharacterProgression.Describe(p));
                        _content.Add(Option(c, f.Id, f.Name + "  [" + f.Category + "]", f.Description + (prereq.Count > 0 ? "  ·  needs " + string.Join(", ", prereq) : string.Empty) +
                                                                                       (f.Repeatable ? "  ·  repeatable" : string.Empty)));
                    }

                    _content.Add(Text("Feats from SRD 5.2.1. A feat's own choices follow on the next pages.", "xk-cc-source"));
                    break;

                case ChoiceKind.Skill:
                case ChoiceKind.Expertise:
                    foreach (var sk in c.Skills.Count > 0 ? c.Skills : SkillRules.All)
                    {
                        var rank = Character.Rules.SkillRank(sk);
                        var row = Option(c, sk.ToString(), SkillRules.Name(sk) + " (" + SkillRules.AbilityOf(sk).ToString().Substring(0, 3) + ")",
                            "now " + Breakdown.Signed(Character.Rules.SkillBonus(sk).Total) + (rank == ProficiencyRank.Expertise ? " · expertise" : rank == ProficiencyRank.Proficient ? " · proficient" : string.Empty));
                        _content.Add(row);
                    }

                    break;

                case ChoiceKind.Language:
                    foreach (var l in RpgCatalog.Languages) _content.Add(Option(c, l.Id, l.Name, l.Description));
                    break;

                case ChoiceKind.AbilityIncrease:
                    var picks = Draft.Picked(c.Id);
                    _content.Add(Text($"{picks.Count} of {c.Count} point{(c.Count == 1 ? string.Empty : "s")} placed. No score may pass {c.Limit}.", "xk-w-desc"));
                    foreach (var a in c.Abilities.Count > 0 ? c.Abilities : AbilityRules.All)
                    {
                        var ability = a;
                        var added = 0;
                        foreach (var p in picks) if (p == a.ToString()) added++;
                        var row = Row("xk-w-row");
                        row.name = "xk-lv-ability-" + a;
                        var name = Text(a.ToString(), "xk-w-name");
                        name.style.width = 200;
                        name.style.flexGrow = 0;
                        row.Add(name);
                        var score = Character.Rules.Score(a);
                        row.Add(Text(added > 0 ? $"{score} → {score + added}" : score.ToString(CultureInfo.InvariantCulture), "xk-cc-col"));
                        var minus = Button("−", "xk-lv-minus-" + a, () => { Draft.Lower(c, ability.ToString()); Render(); });
                        minus.SetEnabled(added > 0);
                        var why = Draft.Unavailable(c, a.ToString());
                        var plus = Button("+", "xk-lv-plus-" + a, () => { Draft.Toggle(c, ability.ToString()); Render(); });
                        plus.SetEnabled(why == null);
                        row.Add(minus);
                        row.Add(plus);
                        if (why != null && added == 0 && !why.StartsWith(AdvancementDraft.FullPrefix, StringComparison.Ordinal)) row.Add(Text(why, "xk-w-desc"));
                        _content.Add(row);
                    }

                    break;
            }
        }

        /// <summary>One option row: a pick button, its description, and why it is unavailable if it is.</summary>
        private VisualElement Option(ChoiceDefinition c, string value, string name, string description)
        {
            var box = Column("xk-cc-choice");
            var row = Row("xk-w-row");
            var picked = Draft.IsPicked(c, value);
            var why = Draft.Unavailable(c, value);
            var b = Button(picked ? "✓ " + name : name, "xk-lv-pick-" + c.Id.Replace(':', '-') + "-" + value, () => { Draft.Toggle(c, value); Render(); });
            b.AddToClassList("xk-w-choice");
            b.EnableInClassList("xk-w-choice--on", picked);
            b.SetEnabled(why == null);
            row.Add(b);
            row.Add(Text(description, "xk-w-desc"));
            box.Add(row);
            if (why != null && !why.StartsWith(AdvancementDraft.FullPrefix, StringComparison.Ordinal)) box.Add(Text("Unavailable: " + why, "xk-cc-source"));
            return box;
        }

        private void Review()
        {
            var p = Draft.Preview();
            _content.Add(Text($"Level {p.FromLevel} → {p.ToLevel} · hit points {p.HitPointsBefore} → {p.HitPointsAfter} · proficiency +{p.ProficiencyAfter}", "xk-cc-name"));
            _content.Add(Pair("Hit points", p.HitPointsBreakdown.Text));
            var features = new List<string>();
            foreach (var f in p.NewFeatures) features.Add(f.Feature.Name);
            _content.Add(Pair("Features", features.Count == 0 ? "None" : string.Join(", ", features)));
            if (Draft.Choices.SubclassId != null) _content.Add(Pair(Character.Rules.Class.SubclassTitle, RpgCatalog.FindSubclass(Draft.Choices.SubclassId)?.Name));
            foreach (var slot in p.Choices)
            {
                if (slot.Choice.Kind == ChoiceKind.Subclass) continue;
                var names = new List<string>();
                foreach (var v in Draft.Picked(slot.Choice.Id)) names.Add(CharacterAdvancement.NameOf(slot.Choice, v));
                _content.Add(Pair(slot.Choice.Name, names.Count == 0 ? "— not chosen —" : string.Join(", ", names)));
            }

            foreach (var (a, before, after) in p.Scores) _content.Add(Pair(a.ToString(), $"{before} → {after} (modifier {Breakdown.Signed(AbilityRules.Modifier(after))})"));
            foreach (var (name, before, after) in p.Resources) _content.Add(Pair(name, before == 0 ? $"new: {after}" : $"{before} → {after}"));
            foreach (var (name, before, after) in p.Scalings) _content.Add(Pair(name, $"{before} → {after}"));
            var problems = Draft.Problems();
            if (problems.Count > 0)
            {
                _content.Add(Text("Still needed", "xk-w-heading"));
                foreach (var e in problems) _content.Add(Text("• " + e, "xk-cc-warn"));
            }
        }

        private void Summarise()
        {
            var c = Character;
            var p = Draft.Preview();
            var next = ProgressionRules.NextThreshold(Draft.Level);
            _summary.text = $"{c.FullName}\n{c.Rules.Species.Name} · {c.Rules.Class.Name} {p.FromLevel} → {p.ToLevel}\n" +
                            $"HP {p.HitPointsBefore} → {p.HitPointsAfter} · proficiency +{p.ProficiencyAfter}\n" +
                            $"XP {c.Experience.ToString("N0", CultureInfo.InvariantCulture)}" + (next != null ? $" · level {Draft.Level + 1} at {next.Value.ToString("N0", CultureInfo.InvariantCulture)}" : string.Empty) +
                            (c.LevelsAvailable > 1 ? $"\n{c.LevelsAvailable - 1} more level{(c.LevelsAvailable > 2 ? "s" : string.Empty)} after this one." : string.Empty);
        }

        private static VisualElement Pair(string key, string value)
        {
            var row = Row("xk-w-pair");
            row.Add(Text(key, "xk-w-key"));
            row.Add(Text(value ?? "-", "xk-w-value"));
            return row;
        }

        private static Button Button(string text, string name, Action act)
        {
            var b = new UnityEngine.UIElements.Button(act) { text = text, name = name };
            b.AddToClassList("xk-button");
            return b;
        }
    }
}
