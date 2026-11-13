using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The encounter panel's magic (Phase 9): a Spells section (each spell with its cost and why it cannot be cast now)
    /// and a Features section (Rage, Bardic Inspiration, Lay on Hands, Flurry of Blows …), and the cast composer: pick a
    /// slot level, the targets (or click the grid to aim an area, previewed on the squares), Metamagic, then Cast. The
    /// composer only collects choices; the engine validates the cast and explains a refusal.
    /// </summary>
    public sealed partial class CombatWindow
    {
        private SpellCastOption _cast;
        private int _castLevel;
        private bool _castFree;
        private readonly List<CombatantId> _castTargets = new List<CombatantId>();

        /// <summary>Which panel sections the player folded away; kept while the window lives, so a refresh does not reopen them.</summary>
        private readonly HashSet<string> _folded = new HashSet<string>();

        private Foldout Fold(string text, string name)
        {
            var fold = new Foldout { text = text, value = !_folded.Contains(name), name = name };
            fold.RegisterValueChangedCallback(e =>
            {
                if (e.target != fold) return;
                if (e.newValue) _folded.Remove(name);
                else _folded.Add(name);
            });
            return fold;
        }

        private GridPos? _castPoint;
        private readonly List<string> _castMeta = new List<string>();
        private readonly HashSet<string> _preview = new HashSet<string>();

        public event Action<CastSpellAction> CastRequested;
        public event Action<UseFeatureAction> FeatureRequested;

        public bool Composing => _cast != null;

        /// <summary>Phase 20A: a feature waiting for its square (Cloud's Jaunt); a grid click uses it instead of moving.</summary>
        private string _featureAim;

        public bool AimingFeature => _featureAim != null;

        /// <summary>Starts aiming a feature at a square (as its "Choose a square" button does).</summary>
        public void BeginFeatureAim(string featureId)
        {
            _featureAim = featureId;
            if (Snapshot != null) Show(Snapshot);
        }
        public string ComposingSpell => _cast?.Spell.Id;
        private bool NeedsPoint => _cast != null && _cast.NeedsPoint;
        private bool InPreview(GridPos p) => _preview.Contains(p.X + "," + p.Y);

        /// <summary>Starts composing a cast (as clicking a spell does).</summary>
        public void BeginCast(string spellId)
        {
            var s = Snapshot;
            if (s == null) return;
            foreach (var o in s.PlayerSpells)
                if (o.Key == spellId)
                {
                    _cast = o;
                    _castFree = o.FromItem != null || o.FreeWith != null && o.CastLevels.Count == 0; // Phase 11: a wand always pays with charges
                    _castLevel = o.AtWill || _castFree ? (o.AtWill ? 0 : o.Spell.Level) : o.CastLevels.Count > 0 ? o.CastLevels[0] : o.Spell.Level;
                    _castTargets.Clear();
                    _castPoint = null;
                    _castMeta.Clear();
                    _preview.Clear();
                    if (o.Spell.Targeting == SpellTargeting.Creatures)
                        foreach (var t in o.Targets) if (t.Problem == null) { _castTargets.Add(t.Id); break; }
                    Show(s);
                    return;
                }
        }

        public void CancelCast()
        {
            _cast = null;
            _castTargets.Clear();
            _castPoint = null;
            _castMeta.Clear();
            _preview.Clear();
        }

        /// <summary>Aims the composed area spell at a square (as clicking the grid does), previewing the squares it covers.</summary>
        public void Aim(GridPos p)
        {
            if (_featureAim != null) // Phase 20A: a feature aimed at a square (Cloud's Jaunt) is used at once
            {
                var id = _featureAim;
                _featureAim = null;
                FeatureRequested?.Invoke(new UseFeatureAction(id, default, null, 0, p));
                return;
            }

            if (_cast == null || Snapshot?.Player == null) return;
            _castPoint = p;
            _preview.Clear();
            var s = _cast.Spell;
            if (s.HasArea)
                foreach (var q in CombatEngine.AreaSquares(Snapshot.Player.Position, p, s.Area, s.AreaSize, s.Targeting == SpellTargeting.FromSelf)) _preview.Add(q.X + "," + q.Y);
            else _preview.Add(p.X + "," + p.Y);
            Show(Snapshot);
        }

        public void ToggleTarget(CombatantId id)
        {
            if (_cast == null) return;
            if (_castTargets.Contains(id) && !(_cast.Spell.Instances > 1 || _cast.Spell.ScalesByInstances)) _castTargets.Remove(id);
            else if (_castTargets.Count < Math.Max(1, _cast.MaxTargets)) _castTargets.Add(id);
            else if (_cast.MaxTargets == 1) { _castTargets.Clear(); _castTargets.Add(id); }
            Show(Snapshot);
        }

        public CastSpellAction PendingCast() => _cast == null ? null :
            new CastSpellAction(_cast.Spell.Id, _castLevel, _castTargets, _castPoint, _castMeta, _castFree ? _cast.FreeWith : null);

        private void FillMagic(EncounterSnapshot s)
        {
            if (s.PlayerSpells.Count > 0)
            {
                var innateOnly = !s.Player.IsCaster; // Phase 20A: a legacy cantrip, not class spellcasting
                var fold = Fold($"{(innateOnly ? "Innate spells" : "Spells")} (save DC {s.Player.SpellSaveDc}, attack {(s.Player.SpellAttack >= 0 ? "+" : string.Empty)}{s.Player.SpellAttack})", "xk-combat-fold-spells");
                fold.AddToClassList("xk-combat-fold");
                for (var i = 0; i < s.PlayerSpells.Count; i++)
                {
                    var o = s.PlayerSpells[i];
                    var id = o.Key;
                    var row = TradeWindow.Column("xk-combat-option");
                    var b = new Button(() => BeginCast(id)) { text = $"{o.Spell.Name}{(o.FromItem != null ? " (" + o.FromItem + ")" : string.Empty)} · {(o.Spell.IsCantrip ? "cantrip" : "level " + o.Spell.Level)} · {o.CostText}", name = "xk-combat-spell-" + id };
                    b.AddToClassList("xk-button");
                    b.AddToClassList("xk-combat-act");
                    b.tooltip = o.Explanation;
                    var blocked = o.Unavailable != null && !(o.NeedsCreatures && o.Targets.Count > 0 && o.Targets[0].Problem == null);
                    b.SetEnabled(!blocked);
                    row.Add(b);
                    if (blocked) row.Add(TradeWindow.Text(o.Unavailable, "xk-combat-why"));
                    fold.Add(row);
                }

                _actions.Add(fold);
            }

            if (s.PlayerFeatures.Count > 0)
            {
                var fold = Fold("Features", "xk-combat-fold-features"); // Phase 20A: class and species features (the giant ancestries)
                fold.AddToClassList("xk-combat-fold");
                foreach (var f in s.PlayerFeatures)
                {
                    var row = TradeWindow.Column("xk-combat-option");
                    row.Add(TradeWindow.Text($"{f.Feature.Name} · {f.Cost}{(f.Feature.Status != ImplementationStatus.Functional ? " · partly simulated" : string.Empty)}", "xk-combat-economy"));
                    var line = TradeWindow.Row("xk-combat-feature-row");
                    var feature = f.Feature;
                    if (f.Targets.Count > 0)
                    {
                        foreach (var t in f.Targets)
                        {
                            if (t.Problem != null) continue;
                            var target = t.Id;
                            var option = feature.Id == "divine_spark" ? (t.Hostile ? "harm" : "heal") : f.Options.Count > 0 ? f.Options[0].id : null;
                            var b = new Button(() => FeatureRequested?.Invoke(new UseFeatureAction(feature.Id, target, option))) { text = t.Name, name = $"xk-combat-feature-{feature.Id}-{t.Name.Replace(' ', '-')}" };
                            b.AddToClassList("xk-button");
                            line.Add(b);
                        }
                    }
                    else if (f.NeedsPoint) // Phase 20A: Cloud's Jaunt is aimed at a square on the grid
                    {
                        if (_featureAim == feature.Id)
                        {
                            line.Add(TradeWindow.Text($"Click a free square within {feature.Range} ft on the field.", "xk-combat-aim-hint"));
                            var cancel = new Button(() => { _featureAim = null; Show(Snapshot); }) { text = "Cancel", name = "xk-combat-feature-" + feature.Id + "-cancel" };
                            cancel.AddToClassList("xk-button");
                            line.Add(cancel);
                        }
                        else
                        {
                            var b = new Button(() => { _featureAim = feature.Id; Show(Snapshot); }) { text = "Choose a square", name = "xk-combat-feature-" + feature.Id };
                            b.AddToClassList("xk-button");
                            b.SetEnabled(f.Unavailable == null);
                            line.Add(b);
                        }
                    }
                    else if (f.Options.Count > 0)
                    {
                        foreach (var (oid, label) in f.Options)
                        {
                            var option = oid;
                            var b = new Button(() => FeatureRequested?.Invoke(new UseFeatureAction(feature.Id, default, option))) { text = label, name = $"xk-combat-feature-{feature.Id}-{oid}" };
                            b.AddToClassList("xk-button");
                            line.Add(b);
                        }
                    }
                    else
                    {
                        var b = new Button(() => FeatureRequested?.Invoke(new UseFeatureAction(feature.Id))) { text = "Use", name = "xk-combat-feature-" + feature.Id };
                        b.AddToClassList("xk-button");
                        b.SetEnabled(f.Unavailable == null);
                        line.Add(b);
                    }

                    row.Add(line);
                    if (f.Unavailable != null) row.Add(TradeWindow.Text(f.Unavailable, "xk-combat-why"));
                    fold.Add(row);
                }

                _actions.Add(fold);
            }
        }

        private void FillComposer(EncounterSnapshot s)
        {
            var o = _cast;
            var spell = o.Spell;
            _actions.Add(TradeWindow.Text($"Casting {spell.Name}", "xk-w-title"));
            _actions.Add(TradeWindow.Text(o.Explanation, "xk-combat-economy"));
            _actions.Add(TradeWindow.Text($"{(spell.CastingTime == CastingTime.BonusAction ? "Bonus action" : "Action")} · {spell.RangeText} · {spell.DurationText}{(spell.Concentration ? " · concentration" + (s.Player.Concentration != null ? $" (ends {s.Player.Concentration})" : string.Empty) : string.Empty)}", "xk-combat-economy"));

            if (!o.AtWill)
            {
                var levels = TradeWindow.Row("xk-combat-feature-row");
                levels.Add(TradeWindow.Text("Slot:", "xk-w-key"));
                foreach (var l in o.CastLevels)
                {
                    var level = l;
                    var b = new Button(() => { _castLevel = level; _castFree = _cast?.FromItem != null; Show(Snapshot); }) { text = "Level " + level, name = "xk-combat-cast-level-" + level };
                    b.AddToClassList("xk-button");
                    b.AddToClassList("xk-w-choice");
                    b.EnableInClassList("xk-w-choice--on", (o.FromItem != null || !_castFree) && _castLevel == level);
                    levels.Add(b);
                }

                if (o.FreeWith != null && o.FromItem == null)
                {
                    var b = new Button(() => { _castFree = true; _castLevel = spell.Level; Show(Snapshot); }) { text = "Free (Favoured Enemy)", name = "xk-combat-cast-free" };
                    b.AddToClassList("xk-button");
                    b.AddToClassList("xk-w-choice");
                    b.EnableInClassList("xk-w-choice--on", _castFree);
                    levels.Add(b);
                }

                _actions.Add(levels);
            }

            if (o.NeedsCreatures)
            {
                _actions.Add(TradeWindow.Text(spell.Instances > 1 || spell.ScalesByInstances ? "Targets (each click adds a dart, ray or beam):" : $"Targets (up to {o.MaxTargets}):", "xk-w-key"));
                var row = TradeWindow.Row("xk-cc-wrap");
                foreach (var t in o.Targets)
                {
                    var id = t.Id;
                    var count = 0;
                    foreach (var x in _castTargets) if (x == id) count++;
                    var b = new Button(() => ToggleTarget(id)) { text = $"{t.Name} ({t.Distance} ft){(count > 1 ? " ×" + count : string.Empty)}", name = "xk-combat-cast-target-" + t.Name.Replace(' ', '-') };
                    b.AddToClassList("xk-button");
                    b.AddToClassList("xk-w-choice");
                    b.EnableInClassList("xk-w-choice--on", count > 0);
                    b.SetEnabled(t.Problem == null);
                    b.tooltip = t.Problem ?? string.Empty;
                    row.Add(b);
                }

                _actions.Add(row);
            }
            else if (o.NeedsPoint)
                _actions.Add(TradeWindow.Text(_castPoint == null ? "Click a square on the field to aim." : $"Aimed at {_castPoint}: the shaded squares are affected.", "xk-combat-aim-hint"));

            if (o.Metamagic.Count > 0)
            {
                var row = TradeWindow.Row("xk-cc-wrap");
                row.Add(TradeWindow.Text("Metamagic:", "xk-w-key"));
                foreach (var (m, cost) in o.Metamagic)
                {
                    var id = m;
                    var b = new Button(() => { if (!_castMeta.Remove(id)) _castMeta.Add(id); Show(Snapshot); }) { text = $"{char.ToUpperInvariant(m[0])}{m.Substring(1)} ({cost})", name = "xk-combat-cast-meta-" + m };
                    b.AddToClassList("xk-button");
                    b.AddToClassList("xk-w-choice");
                    b.EnableInClassList("xk-w-choice--on", _castMeta.Contains(id));
                    row.Add(b);
                }

                _actions.Add(row);
            }

            var go = TradeWindow.Row("xk-cc-wrap");
            var cast = new Button(() => { var a = PendingCast(); CancelCast(); CastRequested?.Invoke(a); }) { text = "Cast", name = "xk-combat-cast-confirm" };
            cast.AddToClassList("xk-button");
            cast.AddToClassList("xk-button--primary");
            cast.SetEnabled(!o.NeedsCreatures || _castTargets.Count > 0);
            cast.SetEnabled(cast.enabledSelf && (!o.NeedsPoint || _castPoint != null));
            go.Add(cast);
            var cancel = new Button(() => { CancelCast(); Show(Snapshot); }) { text = "Cancel", name = "xk-combat-cast-cancel" };
            cancel.AddToClassList("xk-button");
            go.Add(cancel);
            _actions.Add(go);
        }
    }
}
