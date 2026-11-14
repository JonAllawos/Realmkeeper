using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The Hub's "Spells &amp; rest" section (Phase 9): the spellbook — spellcasting ability, save DC and attack, slots
    /// (pact slots apart), cantrips, known, spellbook and prepared spells with search and filters, inspection of any
    /// spell (casting time, range, components, duration, concentration, effects, implementation status), preparation
    /// and open picks where the rules allow — then rests (short with hit dice, long) and the class features as combat
    /// uses them, each with its status. It shows the session's <see cref="SpellbookView"/>; the controller dispatches.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        private string _spellSearch = string.Empty;
        private int _spellLevel = -1;
        private string _spellType = "All";
        private bool _spellAll;
        private string _inspected;
        private int _restDice;
        private List<string> _preparing;

        /// <summary>The spellbook to show (set by the controller before showing).</summary>
        public SpellbookView Spellbook { get; set; }

        public string InspectedSpell => _inspected;

        public event Action<IReadOnlyList<string>> PrepareRequested;
        public event Action<string, string> ChooseSpellRequested;
        public event Action<int> ShortRestRequested;
        public event Action LongRestRequested;
        public event Action<string, int> HealOutsideRequested;

        public void InspectSpell(string id)
        {
            _inspected = id;
            if (Section == "Spells & rest") Render();
        }

        public void SetSpellFilter(string search = null, int? level = null, string type = null, bool? all = null)
        {
            if (search != null) _spellSearch = search;
            if (level.HasValue) _spellLevel = level.Value;
            if (type != null) _spellType = type;
            if (all.HasValue) _spellAll = all.Value;
            if (Section == "Spells & rest") Render();
        }

        private void Spells()
        {
            var b = Spellbook;
            if (b == null) { _content.Add(Text("Create your character first.", "xk-w-empty")); return; }

            if (b.IsCaster)
            {
                _content.Add(Text($"{b.ClassName} spellcasting · {b.Ability}", "xk-w-heading"));
                var stats = Row("xk-cc-wrap");
                stats.name = "xk-sp-stats";
                stats.Add(Pair("Spell save DC", b.SaveDc.ToString(CultureInfo.InvariantCulture)));
                stats.Add(Pair("Spell attack", (b.AttackBonus >= 0 ? "+" : string.Empty) + b.AttackBonus));
                stats.Add(Pair("Highest spell level", b.MaxSpellLevel.ToString(CultureInfo.InvariantCulture)));
                _content.Add(stats);
                _content.Add(Text($"DC: {b.SaveDcText} · attack: {b.AttackText}", "xk-w-note"));
                var slots = Row("xk-sp-slots");
                slots.name = "xk-sp-slots";
                foreach (var (level, left, max) in b.Slots)
                {
                    var pips = new string('●', left) + new string('○', Math.Max(0, max - left));
                    var cell = Text($"{(b.UsesPactMagic ? $"Pact slots (level {level})" : "Level " + level)}  {pips}  {left}/{max}", "xk-sp-slot");
                    cell.name = "xk-sp-slot-" + level;
                    slots.Add(cell);
                }

                _content.Add(slots);
                _content.Add(Text(b.KeepingText + (b.PreparationText.Length > 0 ? " " + b.PreparationText + "." : string.Empty), "xk-w-note"));
                SpellPicks(b);
                PreparationEditor(b);
                SpellList(b);
                Inspector(b);
            }
            else _content.Add(Text($"{b.ClassName}s do not cast spells.", "xk-w-empty"));

            Rests(b);
            FeatureStatus();
        }

        private void SpellPicks(SpellbookView b)
        {
            if (b.OpenPicks.Count == 0) return;
            _content.Add(Text("Open spell picks", "xk-w-heading"));
            foreach (var pick in b.OpenPicks)
            {
                _content.Add(Text($"{pick.Name}: {pick.Remaining} left to choose", "xk-w-key"));
                var row = Row("xk-cc-wrap");
                foreach (var o in pick.Options)
                {
                    var p = pick;
                    var s = o;
                    var btn = new Button(() => ChooseSpellRequested?.Invoke(p.ChoiceId, s.Id)) { text = $"{s.Name} ({(s.Level == 0 ? "cantrip" : "level " + s.Level)})", name = $"xk-sp-pick-{p.ChoiceId.Replace(':', '-')}-{s.Id}" };
                    btn.AddToClassList("xk-button");
                    btn.AddToClassList("xk-w-choice");
                    btn.tooltip = s.Definition.Description + " — " + s.StatusText;
                    row.Add(btn);
                }

                _content.Add(row);
            }
        }

        private void PreparationEditor(SpellbookView b)
        {
            if (b.Keeping == SpellKeeping.Known || b.Keeping == SpellKeeping.None) return;
            if (_preparing == null) _preparing = new List<string>(b.Prepared);
            _content.Add(Text($"Prepare spells ({_preparing.Count} of {b.PreparedLimit})", "xk-w-heading"));
            var row = Row("xk-cc-wrap");
            row.name = "xk-sp-prepare";
            foreach (var s in b.Preparable)
            {
                var id = s.Id;
                var on = _preparing.Contains(id);
                var btn = new Button(() =>
                {
                    if (_preparing.Contains(id)) _preparing.Remove(id);
                    else _preparing.Add(id);
                    Render();
                }) { text = $"{s.Name} ({s.Level})", name = "xk-sp-prep-" + id };
                btn.AddToClassList("xk-button");
                btn.AddToClassList("xk-w-choice");
                btn.EnableInClassList("xk-w-choice--on", on);
                btn.tooltip = s.EffectsText;
                row.Add(btn);
            }

            _content.Add(row);
            var actions = Row("xk-cc-wrap");
            var apply = new Button(() => { PrepareRequested?.Invoke(new List<string>(_preparing)); _preparing = null; }) { text = "Save prepared spells", name = "xk-sp-prepare-save" };
            apply.AddToClassList("xk-button");
            apply.AddToClassList("xk-button--primary");
            apply.SetEnabled(!SameSet(_preparing, b.Prepared));
            actions.Add(apply);
            var reset = new Button(() => { _preparing = null; Render(); }) { text = "Undo changes", name = "xk-sp-prepare-undo" };
            reset.AddToClassList("xk-button");
            actions.Add(reset);
            _content.Add(actions);
        }

        private static bool SameSet(List<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var x in b) if (!a.Contains(x)) return false;
            return true;
        }

        private void SpellList(SpellbookView b)
        {
            _content.Add(Text("Spells", "xk-w-heading"));
            var filters = Row("xk-cc-wrap");
            var search = new TextField { value = _spellSearch, name = "xk-sp-search" };
            search.AddToClassList("xk-sp-search");
            search.RegisterValueChangedCallback(e => { _spellSearch = e.newValue ?? string.Empty; Render(); });
            filters.Add(search);
            foreach (var (label, level) in new[] { ("All", -1), ("Cantrips", 0), ("1", 1), ("2", 2), ("3", 3), ("4", 4), ("5", 5) })
            {
                var l = level;
                var btn = new Button(() => SetSpellFilter(level: l)) { text = label, name = "xk-sp-level-" + (level < 0 ? "all" : level.ToString(CultureInfo.InvariantCulture)) };
                btn.AddToClassList("xk-button");
                btn.AddToClassList("xk-w-choice");
                btn.EnableInClassList("xk-w-choice--on", _spellLevel == level);
                filters.Add(btn);
            }

            foreach (var type in new[] { "All", "Damage", "Healing", "Control", "Support", "Utility" })
            {
                var t = type;
                var btn = new Button(() => SetSpellFilter(type: t)) { text = type, name = "xk-sp-type-" + type.ToLowerInvariant() };
                btn.AddToClassList("xk-button");
                btn.AddToClassList("xk-w-choice");
                btn.EnableInClassList("xk-w-choice--on", _spellType == type);
                filters.Add(btn);
            }

            var scope = new Button(() => SetSpellFilter(all: !_spellAll)) { text = _spellAll ? "Showing: whole class list" : "Showing: your spells", name = "xk-sp-scope" };
            scope.AddToClassList("xk-button");
            filters.Add(scope);
            _content.Add(filters);

            var list = new List<SpellView>();
            list.AddRange(b.Cantrips);
            list.AddRange(b.Spells);
            if (_spellAll) foreach (var s in b.Preparable) if (!list.Exists(x => x.Id == s.Id)) list.Add(s);
            var shown = 0;
            var box = Column("xk-sp-list");
            box.name = "xk-sp-list";
            foreach (var s in list)
            {
                if (_spellLevel >= 0 && s.Level != _spellLevel) continue;
                if (_spellSearch.Length > 0 && s.Name.IndexOf(_spellSearch, StringComparison.OrdinalIgnoreCase) < 0 && s.Definition.School.ToString().IndexOf(_spellSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_spellType != "All" && TypeOf(s.Definition) != _spellType) continue;
                var id = s.Id;
                var tags = new List<string>();
                if (s.Level == 0) tags.Add("cantrip");
                if (s.Known && s.Level > 0) tags.Add("known");
                if (s.InBook) tags.Add("in book");
                if (s.Prepared) tags.Add("prepared");
                if (s.Always) tags.Add("always prepared");
                var row = new Button(() => InspectSpell(id)) { text = $"{s.Name} — {s.Definition.LevelText}{(tags.Count > 0 ? " · " + string.Join(", ", tags) : string.Empty)} · {s.StatusText}", name = "xk-sp-spell-" + id };
                row.AddToClassList("xk-sp-row");
                row.EnableInClassList("xk-sp-row--selected", id == _inspected);
                row.EnableInClassList("xk-sp-row--inactive", !s.Definition.Castable);
                Realmkeeper.UI.RkIcons.Lead(row, TypeIcon(TypeOf(s.Definition)), "m", s.Prepared || s.Always || s.Level == 0 ? "accent" : null); // Phase 25: the kind of spell at a glance (also written in the filter)
                box.Add(row);
                shown++;
            }

            if (shown == 0) box.Add(Text("No spells match.", "xk-w-empty"));
            _content.Add(box);
        }

        /// <summary>Phase 25: an icon for each kind of spell.</summary>
        public static string TypeIcon(string type) => type switch { "Damage" => "fire", "Healing" => "heal", "Control" => "concentration", "Support" => "ac", _ => "spell" };

        public static string TypeOf(SpellDefinition s)
        {
            if (s.DealsDamage) return "Damage";
            if (s.Heals) return "Healing";
            foreach (var e in s.Effects)
                if (e.Kind == CombatEffectKind.Condition || e.Kind == CombatEffectKind.AttackersHaveAdvantage || e.Kind == CombatEffectKind.AttackAndSaveDie && e.Value < 0) return "Control";
            foreach (var e in s.Effects)
                if (e.Kind == CombatEffectKind.ArmorClassBonus || e.Kind == CombatEffectKind.AttackAndSaveDie || e.Kind == CombatEffectKind.SpeedMultiplier || e.Kind == CombatEffectKind.HitPointMaximumBonus || e.Kind == CombatEffectKind.ArmorClassBase) return "Support";
            return "Utility";
        }

        private void Inspector(SpellbookView b)
        {
            var s = _inspected != null ? SpellCatalog.Find(_inspected) : null;
            if (s == null) return;
            var box = Column("xk-sp-detail");
            box.name = "xk-sp-detail";
            box.Add(Text(s.Name, "xk-cc-name"));
            box.Add(Text(s.LevelText + (s.Ritual ? " (ritual)" : string.Empty), "xk-w-value"));
            foreach (var (k, v) in new[]
                     {
                         ("Casting time", s.CastingTime == CastingTime.BonusAction ? "Bonus action" : s.CastingTime == CastingTime.Reaction ? "Reaction" : s.CastingTime == CastingTime.Longer ? "1 minute or more" : "Action"),
                         ("Range", s.RangeText),
                         ("Targets", s.Targeting == SpellTargeting.Self ? "yourself" : s.Targeting == SpellTargeting.Creatures ? $"up to {s.MaxTargets} creature{(s.MaxTargets == 1 ? string.Empty : "s")}" : s.Area + " area"),
                         ("Components", s.ComponentsText),
                         ("Duration", s.DurationText),
                         ("Concentration", s.Concentration ? "Yes" : "No"),
                         ("Effects", WorldSession.Explain(s)),
                         ("At higher levels", s.Upcast ?? "—"),
                         ("Classes", string.Join(", ", s.Classes)),
                         ("Status", s.Status + (s.StatusNote != null ? ": " + s.StatusNote : string.Empty)),
                         ("Source", s.Source == ContentSource.Srd521 ? "SRD 5.2.1 (CC BY 4.0); wording Realmkeeper's own" : s.Source.ToString()),
                     })
                box.Add(Pair(k, v));
            box.Add(Text(s.Description, "xk-w-desc"));
            if (s.Heals && new List<SpellView>(b.Spells).Exists(x => x.Id == s.Id && x.Ready))
                foreach (var (level, left, _) in b.Slots)
                    if (level >= s.Level && left > 0)
                    {
                        var l = level;
                        var heal = new Button(() => HealOutsideRequested?.Invoke(s.Id, l)) { text = $"Cast on yourself now (level {level} slot)", name = "xk-sp-heal-" + level };
                        heal.AddToClassList("xk-button");
                        box.Add(heal);
                    }

            _content.Add(box);
        }

        private void Rests(SpellbookView b)
        {
            _content.Add(Text("Rest", "xk-w-heading"));
            _content.Add(Text($"Hit dice: {b.HitDiceLeft} left (d{b.HitDie} + Constitution each on a short rest). A short rest takes an hour; a long rest eight, once per 24 hours. The world goes on while you rest.", "xk-w-note"));
            var row = Row("xk-cc-wrap");
            var minus = new Button(() => { _restDice = Math.Max(0, _restDice - 1); Render(); }) { text = "−", name = "xk-rest-dice-minus" };
            minus.AddToClassList("xk-button");
            var plus = new Button(() => { _restDice = Math.Min(b.HitDiceLeft, _restDice + 1); Render(); }) { text = "+", name = "xk-rest-dice-plus" };
            plus.AddToClassList("xk-button");
            _restDice = Math.Min(_restDice, b.HitDiceLeft);
            row.Add(minus);
            row.Add(Text($"spend {_restDice} hit di{(_restDice == 1 ? "e" : "ce")}", "xk-w-key"));
            row.Add(plus);
            var shortRest = new Button(() => ShortRestRequested?.Invoke(_restDice)) { text = "Short rest (1 hour)", name = "xk-rest-short" };
            shortRest.AddToClassList("xk-button");
            shortRest.SetEnabled(b.ShortRestProblem == null);
            shortRest.tooltip = b.ShortRestProblem ?? "Spend the chosen hit dice; short-rest resources return.";
            row.Add(shortRest);
            var longRest = new Button(() => LongRestRequested?.Invoke()) { text = "Long rest (8 hours)", name = "xk-rest-long" };
            longRest.AddToClassList("xk-button");
            longRest.AddToClassList("xk-button--primary");
            longRest.SetEnabled(b.LongRestProblem == null);
            longRest.tooltip = b.LongRestProblem ?? "Hit points, hit dice, spell slots and every resource return.";
            row.Add(longRest);
            _content.Add(row);
            if (b.ShortRestProblem != null) _content.Add(Text("Short rest: " + b.ShortRestProblem, "xk-cc-warn"));
            if (b.LongRestProblem != null) _content.Add(Text("Long rest: " + b.LongRestProblem, "xk-cc-warn"));
            for (var i = b.Rests.Count - 1; i >= 0 && i >= b.Rests.Count - 3; i--)
                _content.Add(Text($"{(b.Rests[i].Kind == RestKind.Long ? "Long" : "Short")} rest: {b.Rests[i].Summary}", "xk-w-ledger-line"));
        }

        private void FeatureStatus()
        {
            var sheet = Sheet;
            if (sheet?.Rules == null) return;
            var rows = new List<VisualElement>();
            foreach (var f in CombatFeatureCatalog.All)
            {
                if (f.RequiresFeature == null || !sheet.Rules.HasFeature(f.RequiresFeature)) continue;
                var how = f.Activation == FeatureActivation.Automatic ? "automatic" : f.Activation == FeatureActivation.BonusAction ? "bonus action" : f.Activation == FeatureActivation.Action ? "action" : f.Activation == FeatureActivation.AfterHit ? "after a hit" : "no action";
                var status = f.Status == ImplementationStatus.Functional ? "Functional" : f.Status == ImplementationStatus.Partial ? "Partly simulated" : f.Status.ToString();
                var row = Pair(f.Name, $"{how} · {status}{(f.StatusNote != null ? " — " + f.StatusNote : string.Empty)}");
                row.name = "xk-sp-feature-" + f.Id;
                rows.Add(row);
            }

            if (rows.Count == 0) return;
            _content.Add(Text("Class features in combat", "xk-w-heading"));
            foreach (var r in rows) _content.Add(r);
            _content.Add(Text("Features not listed here are shown on the sheet with what they wait for.", "xk-w-note"));
        }
    }
}
