using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>A creature a spell or feature could be aimed at, with why not (null when it can be).</summary>
    public sealed class TargetChoice
    {
        public CombatantId Id { get; set; }
        public string Name { get; set; }
        public int Distance { get; set; }
        public bool Hostile { get; set; }
        public string Problem { get; set; }
    }

    /// <summary>A spell the player could cast now (Phase 9): what it costs, how it is aimed, at whom, and why not.</summary>
    public sealed class SpellCastOption
    {
        public SpellDefinition Spell { get; set; }

        /// <summary>Phase 11: the option's key (the spell ID; "spell@item" for a wand's cast).</summary>
        public string Key { get; set; }

        /// <summary>Phase 11: the item whose charges cast it ("Wand of Magic Missiles"), or null.</summary>
        public string FromItem { get; set; }
        public string Explanation { get; set; }
        public IReadOnlyList<int> CastLevels { get; set; } = new List<int>();

        /// <summary>A feature resource that can pay instead of a slot (Favoured Enemy), or null.</summary>
        public string FreeWith { get; set; }

        public bool AtWill { get; set; }
        public bool NeedsCreatures => Spell.Targeting == SpellTargeting.Creatures;
        public bool NeedsPoint => Spell.Targeting == SpellTargeting.Point || Spell.Targeting == SpellTargeting.FromSelf;
        public int MaxTargets { get; set; }
        public IReadOnlyList<TargetChoice> Targets { get; set; } = new List<TargetChoice>();

        /// <summary>The sorcerer's Metamagic options that can apply, with their cost.</summary>
        public IReadOnlyList<(string id, int cost)> Metamagic { get; set; } = new List<(string, int)>();

        /// <summary>Why it cannot be cast now (null when it can, with a suitable target).</summary>
        public string Unavailable { get; set; }

        public string CostText { get; set; }
    }

    /// <summary>A class feature the player could use now (Phase 9).</summary>
    public sealed class FeatureOption
    {
        public CombatFeatureDefinition Feature { get; set; }
        public string Cost { get; set; }
        public string Unavailable { get; set; }
        public IReadOnlyList<TargetChoice> Targets { get; set; } = new List<TargetChoice>();
        public IReadOnlyList<(string id, string label)> Options { get; set; } = new List<(string, string)>();

        /// <summary>Phase 20A: aimed at a square (Cloud's Jaunt): the window asks for one on the grid.</summary>
        public bool NeedsPoint { get; set; }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 20A: the save DC and attack bonus of a non-caster's innate spells (0 and 0 when it has none).</summary>
        private static (int dc, int attack) InnateFigures(CombatStats s)
        {
            var rules = s.Rules;
            if (rules == null || rules.InnateSpells.Count == 0) return (0, 0);
            var a = rules.InnateSpells[0].ability;
            return (rules.InnateSpellSaveDc(a).Total, rules.InnateSpellAttack(a).Total);
        }

        private static List<string> EffectLines(CombatEngine engine, CombatantData c)
        {
            var list = new List<string>();
            foreach (var x in engine.EffectsOn(c))
            {
                if (x.Kind == CombatEffectKind.Condition) continue; // shown as the condition itself
                var from = engine.Encounter.Find(x.SourceId);
                var line = x.Name + (from != null && from != c ? $" (from {from.Name})" : string.Empty);
                if (!list.Contains(line)) list.Add(line);
            }

            return list;
        }

        /// <summary>Phase 10: a creature's bestiary entry in a line (for the combat inspector).</summary>
        private static string BestiaryText(CreatureDefinition d)
        {
            if (d == null) return null;
            var traits = new List<string>();
            foreach (var t in d.Traits) traits.Add(t.Name);
            var source = d.Source == Domain.Rpg.ContentSource.Srd51 ? "SRD 5.1" : d.Source == Domain.Rpg.ContentSource.Srd521 ? "SRD 5.2.1" : "Realmkeeper original";
            return d.Category + " · challenge " + d.ChallengeRating + " (" + d.Xp + " XP) · " + d.Behavior.Kind + ": " + d.Behavior.Summary +
                   (traits.Count > 0 ? " · Traits: " + string.Join(", ", traits) : string.Empty) + " · " + source;
        }

        private static string ResourceText(CombatEngine engine, CombatantData c)
        {
            var parts = new List<string>();
            var rules = engine.Stats(c).Rules;
            var map = rules != null ? rules.Sheet.Resources : c.Resources;
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                var name = rules?.FindResource(k)?.Name ?? (k.StartsWith("spell_slots_", StringComparison.Ordinal) ? "level " + k.Substring(12) + " slots" : k);
                var max = rules != null && rules.FindResource(k) != null ? "/" + rules.ResourceMax(rules.FindResource(k)).ToString(CultureInfo.InvariantCulture) : string.Empty;
                parts.Add($"{name} {map[k]}{max}");
            }

            return string.Join(" · ", parts);
        }

        private static List<TargetChoice> Targets(CombatEngine engine, CombatantData actor, Func<CombatantData, string> problem)
        {
            var list = new List<TargetChoice>();
            foreach (var c in engine.Encounter.Combatants)
            {
                if (c.State == VitalState.Dead || c.State == VitalState.Fled) continue;
                list.Add(new TargetChoice { Id = c.CombatantId, Name = c.Name, Distance = actor.Position.DistanceFeet(c.Position), Hostile = CombatEngine.Hostile(actor, c), Problem = problem(c) });
            }

            list.Sort((a, b) => a.Problem == null != (b.Problem == null) ? (a.Problem == null ? -1 : 1) : a.Distance.CompareTo(b.Distance));
            return list;
        }

        private static List<SpellCastOption> SpellOptions(CombatEngine engine, CombatantData player)
        {
            var list = new List<SpellCastOption>();
            var nearestFoe = (CombatantData)null;
            foreach (var c in engine.Encounter.Combatants)
                if (CombatEngine.Hostile(player, c) && c.State == VitalState.Conscious && (nearestFoe == null || player.Position.DistanceFeet(c.Position) < player.Position.DistanceFeet(nearestFoe.Position))) nearestFoe = c;
            foreach (var s in engine.SpellsOf(player))
            {
                var levels = engine.CastLevels(player, s);
                var free = engine.FreeCastResource(player, s);
                var atWill = s.IsCantrip || s.Id == "mage_armor" && levels.Count == 0 && engine.SpellProblem(player, new CastSpellAction(s.Id, 0, new[] { player.CombatantId })) == null;
                var level = atWill ? 0 : free != null ? s.Level : levels.Count > 0 ? levels[0] : s.Level;
                var o = new SpellCastOption
                {
                    Key = s.Id, Spell = s, Explanation = Explain(s), CastLevels = levels, FreeWith = free, AtWill = atWill, MaxTargets = engine.MaxTargets(s, Math.Max(level, s.Level), false),
                    CostText = atWill ? (s.IsCantrip ? (engine.InnateAbility(player, s) is Ability innate ? "innate cantrip (" + innate + ")" : "cantrip") : "at will") : free != null ? "free (Favoured Enemy) or a slot" : levels.Count == 0 ? "no slot left" : "slot level " + string.Join("/", levels),
                };
                if (s.Targeting == SpellTargeting.Creatures)
                    o.Targets = Targets(engine, player, c => engine.SpellProblem(player, new CastSpellAction(s.Id, level, new[] { c.CombatantId }, null, null, free != null && levels.Count == 0 ? free : null)));
                GridPos? point = null;
                if (s.Targeting == SpellTargeting.Point) point = s.Effects.Count > 0 && s.Effects[0].Kind == CombatEffectKind.Teleport ? new GridPos(player.Position.X, player.Position.Y + 2) : nearestFoe?.Position ?? player.Position;
                if (s.Targeting == SpellTargeting.FromSelf) point = nearestFoe?.Position ?? new GridPos(player.Position.X + 1, player.Position.Y);
                var probeTargets = new List<CombatantId>();
                if (s.Targeting == SpellTargeting.Creatures)
                {
                    foreach (var t in o.Targets) if (t.Problem == null) { probeTargets.Add(t.Id); break; }
                    if (probeTargets.Count == 0 && o.Targets.Count > 0) probeTargets.Add(o.Targets[0].Id);
                }

                o.Unavailable = engine.SpellProblem(player, new CastSpellAction(s.Id, level, probeTargets, point, null, free != null && levels.Count == 0 ? free : null));
                var meta = new List<(string, int)>();
                foreach (var m in CombatEngine.SupportedMetamagic)
                    if (engine.SpellProblem(player, new CastSpellAction(s.Id, level, probeTargets, point, new[] { m })) == null) meta.Add((m, m == "quickened" || m == "heightened" ? 2 : 1));
                o.Metamagic = meta;
                list.Add(o);
            }

            // Phase 11 (ADR-0052): spells cast from items' charges (a wand), one level per charge spent.
            var p = engine.World.Player;
            if (player.Kind == CombatantKind.Player && p != null)
            {
                var held = new List<ItemStackData>(p.Inventory);
                held.AddRange(p.Equipment.Values);
                foreach (var stack in held)
                {
                    var def = ItemCatalog.Find(stack.ItemId);
                    var spell = def?.Magic?.SpellId != null ? SpellCatalog.Find(def.Magic.SpellId) : null;
                    if (spell == null) continue;
                    var free = CombatEngine.ItemCast + stack.StackId;
                    var levels = new List<int>();
                    for (var l = spell.Level; l <= Math.Min(9, spell.Level + Math.Max(0, stack.Charges) - 1); l++) levels.Add(l);
                    var o = new SpellCastOption
                    {
                        Key = spell.Id + "@" + stack.StackId, FromItem = def.Name, Spell = spell, Explanation = Explain(spell), CastLevels = levels, FreeWith = free,
                        MaxTargets = engine.MaxTargets(spell, spell.Level, false), CostText = Math.Max(0, stack.Charges) + " charge(s): 1 per spell level",
                    };
                    var level = levels.Count > 0 ? levels[0] : spell.Level;
                    if (spell.Targeting == SpellTargeting.Creatures)
                        o.Targets = Targets(engine, player, c => engine.SpellProblem(player, new CastSpellAction(spell.Id, level, new[] { c.CombatantId }, null, null, free)));
                    var probe = new List<CombatantId>();
                    foreach (var t in o.Targets) if (t.Problem == null) { probe.Add(t.Id); break; }
                    o.Unavailable = engine.SpellProblem(player, new CastSpellAction(spell.Id, level, probe, null, null, free));
                    list.Add(o);
                }
            }

            return list;
        }

        private static List<FeatureOption> FeatureOptions(CombatEngine engine, CombatantData player)
        {
            var list = new List<FeatureOption>();
            foreach (var (f, why) in engine.FeaturesOf(player))
            {
                // Self features without options are offered with the other bonus actions.
                if (f.Activation == FeatureActivation.BonusAction && f.Targeting == FeatureTargeting.Self && f.Options.Count == 0 && f.Id != "patient_defense" && f.Id != "step_of_the_wind") continue;
                var o = new FeatureOption
                {
                    Feature = f, Options = f.Id == "patient_defense" || f.Id == "step_of_the_wind" ? new List<(string, string)> { ("free", "Free"), ("focus", "With 1 focus point") } :
                        f.Id == "divine_spark" ? new List<(string, string)> { ("heal", "Heal an ally"), ("harm", "Harm a foe") } : new List<(string, string)>(f.Options),
                    Cost = (f.Activation == FeatureActivation.Action ? "action" : f.Activation == FeatureActivation.BonusAction ? "bonus action" : f.Activation == FeatureActivation.AfterHit ? "after a hit" : "no action") +
                           (f.ResourceId != null ? $", {(f.ResourceCost > 0 ? f.ResourceCost.ToString(CultureInfo.InvariantCulture) + " " : string.Empty)}{engine.Stats(player).Rules.FindResource(f.ResourceId)?.Name ?? f.ResourceId}" : string.Empty),
                    Unavailable = why,
                };
                if (f.Targeting == FeatureTargeting.Ally || f.Targeting == FeatureTargeting.Enemy || f.Targeting == FeatureTargeting.Either)
                {
                    var option = o.Options.Count > 0 ? o.Options[0].id : null;
                    o.Targets = Targets(engine, player, c => engine.FeatureProblem(player, new UseFeatureAction(f.Id, c.CombatantId, f.Targeting == FeatureTargeting.Either ? (CombatEngine.Hostile(player, c) ? "harm" : "heal") : option)));
                    foreach (var t in o.Targets) if (t.Problem == null) { o.Unavailable = null; break; }
                    if (o.Targets.Count > 0 && o.Targets[0].Problem != null) o.Unavailable = o.Targets[0].Problem;
                }

                if (f.Targeting == FeatureTargeting.Point) // Phase 20A: the square is chosen on the grid; everything else is checked now
                {
                    o.NeedsPoint = true;
                    if (why != null && why.StartsWith("Choose a square", StringComparison.Ordinal)) o.Unavailable = null;
                }

                list.Add(o);
            }

            return list;
        }
    }
}
