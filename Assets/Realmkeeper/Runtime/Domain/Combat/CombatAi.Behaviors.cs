using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 10: creatures behave like what they are (ADR-0044). The stat block's behaviour profile picks the plan;
    /// the engine still validates every proposal. Characters (the player, sheet-holders) and anything without a profile
    /// use the Phase 8 baseline. Deterministic: ties by distance, hit points, then ID.
    /// </summary>
    public static partial class CombatAi
    {
        /// <summary>The behaviour's proposal, or null to let the baseline decide (with the creature's flee threshold).</summary>
        private static CombatAction BehaviorChoice(CombatEngine engine, CombatantData actor, List<CombatantData> targets, ref CombatAiProfile profile)
        {
            if (actor.CreatureId == null || actor.FormCreatureId != null) return null;
            var def = CreatureCatalog.Find(actor.CreatureId);
            if (def == null) return null;
            var b = def.Behavior;
            profile = new CombatAiProfile { RetreatBelow = b.FleeBelow, FinishTheFallen = profile.FinishTheFallen };
            var t = actor.Turn;
            var stats = engine.Stats(actor);
            var hp = engine.Hp(actor) / (double)Math.Max(1, engine.MaxHp(actor));
            var adjacent = Adjacent(engine, actor, targets);

            switch (b.Kind)
            {
                case CreatureBehavior.Skittish:
                case CreatureBehavior.Passive:
                {
                    // Cornered (nowhere farther to go) and with an enemy next to it: it fights back.
                    var away = PathAway(engine, actor, targets);
                    if (adjacent != null && away.Count == 0 && (!t.ActionUsed || t.AttacksLeft > 0) && stats.BestMelee() is AttackOption bite)
                        return new AttackAction(adjacent.CombatantId, bite.Id);
                    return Escape(engine, actor, targets) ?? new EndTurnAction();
                }

                case CreatureBehavior.Mindless:
                    // The nearest living thing; never flees.
                    profile = new CombatAiProfile { RetreatBelow = 0, FinishTheFallen = profile.FinishTheFallen };
                    return Close(engine, actor, targets[0]) ?? new EndTurnAction();

                case CreatureBehavior.Territorial:
                {
                    if (hp <= b.FleeBelow && b.FleeBelow > 0) return Escape(engine, actor, targets) ?? new EndTurnAction();
                    CombatantData intruder = null;
                    foreach (var c in targets)
                        if (c.Position.DistanceFeet(actor.Anchor) <= b.TerritoryFeet && (intruder == null || c.Position.DistanceFeet(actor.Position) < intruder.Position.DistanceFeet(actor.Position))) intruder = c;
                    if (intruder != null) return Close(engine, actor, intruder) ?? new EndTurnAction();
                    // Nobody on its ground: back to it, or stand watch.
                    if (actor.Position.DistanceFeet(actor.Anchor) > 5 && engine.MovementLeft(actor) > 0 && !t.Disengaged)
                    {
                        var home = PathTo(engine, actor, actor.Anchor);
                        if (home.Count > 0) return new MoveAction(home.ToArray());
                    }

                    return t.ActionUsed ? new EndTurnAction() : new DodgeAction();
                }

                case CreatureBehavior.Pack:
                case CreatureBehavior.Scavenging:
                {
                    var pack = 0;
                    var standing = 0;
                    foreach (var c in engine.Encounter.Combatants)
                        if (c.Side == actor.Side && c.CreatureId == actor.CreatureId)
                        {
                            pack++;
                            if (c.State == VitalState.Conscious) standing++;
                        }

                    var broken = standing * 2 < pack || hp <= b.FleeBelow && b.FleeBelow > 0;
                    if (broken) return Escape(engine, actor, targets) ?? new EndTurnAction();
                    // One quarry: the enemy most packmates already stand next to; then the weakest; then the nearest.
                    CombatantData quarry = null;
                    var bestPack = -1;
                    foreach (var c in targets)
                    {
                        var around = 0;
                        foreach (var m in engine.Encounter.Combatants)
                            if (m != actor && m.Side == actor.Side && m.State == VitalState.Conscious && m.Position.IsAdjacent(c.Position)) around++;
                        if (b.Kind == CreatureBehavior.Scavenging && (engine.Has(c, ConditionType.Prone) || engine.Hp(c) * 2 < engine.MaxHp(c))) around += 2;
                        if (around > bestPack || around == bestPack && engine.Hp(c) < engine.Hp(quarry))
                        {
                            quarry = c;
                            bestPack = around;
                        }
                    }

                    return Close(engine, actor, quarry ?? targets[0]) ?? new EndTurnAction();
                }

                case CreatureBehavior.Predatory:
                {
                    if (hp <= b.FleeBelow && b.FleeBelow > 0) return Escape(engine, actor, targets) ?? new EndTurnAction();
                    // The weakest; a ranged hold (a web) first on prey that is not yet held.
                    var prey = targets[0];
                    foreach (var c in targets) if (engine.Hp(c) < engine.Hp(prey)) prey = c;
                    if (!t.ActionUsed)
                        foreach (var o in stats.Attacks)
                        {
                            if (o.Melee || o.OnHit == null || engine.AttackNotReady(actor, o) != null || engine.Has(prey, o.OnHit.Value)) continue;
                            var d = actor.Position.DistanceFeet(prey.Position);
                            if (d > 5 && d <= o.Range && engine.Unavailable(actor, new AttackAction(prey.CombatantId, o.Id)) == null) return new AttackAction(prey.CombatantId, o.Id);
                        }

                    return Close(engine, actor, prey) ?? new EndTurnAction();
                }

                default:
                    // Intelligent and guarding: the baseline, but a badly hurt creature with nobody close leaves the fight.
                    if (b.FleeBelow > 0 && hp <= b.FleeBelow && engine.Unavailable(actor, new FleeAction()) == null) return new FleeAction();
                    return null;
            }
        }

        private static CombatantData Adjacent(CombatEngine engine, CombatantData actor, List<CombatantData> targets)
        {
            foreach (var c in targets) if (c.Position.IsAdjacent(actor.Position) && !engine.Incapacitated(c)) return c;
            return null;
        }

        /// <summary>Getting away: leave the fight when free to; otherwise disengage, run, or dash to run further.</summary>
        private static CombatAction Escape(CombatEngine engine, CombatantData actor, List<CombatantData> targets)
        {
            var t = actor.Turn;
            if (engine.Unavailable(actor, new FleeAction()) == null) return new FleeAction();
            var adjacent = Adjacent(engine, actor, targets) != null;
            if (adjacent && !t.Disengaged && engine.Unavailable(actor, new DisengageAction()) == null) return new DisengageAction();
            if (engine.MovementLeft(actor) > 0 && (!adjacent || t.Disengaged))
            {
                var away = PathAway(engine, actor, targets);
                if (away.Count > 0) return new MoveAction(away.ToArray());

                // Phase 13: cornered among walls (nowhere further to run), it turns and fights rather than dash in place for ever.
                if ((engine.Encounter.Blocked.Count > 0 || engine.Encounter.Doors.Count > 0) && targets.Count > 0) return Close(engine, actor, Nearest(actor, targets));
            }

            if (!t.ActionUsed && engine.Speed(actor) > 0 && !adjacent && engine.Encounter.Blocked.Count == 0 && engine.Encounter.Doors.Count == 0) return new DashAction();
            if ((engine.Encounter.Blocked.Count > 0 || engine.Encounter.Doors.Count > 0) && targets.Count > 0 && !t.ActionUsed) return Close(engine, actor, Nearest(actor, targets));
            return null;
        }

        private static CombatantData Nearest(CombatantData actor, List<CombatantData> targets)
        {
            CombatantData best = null;
            foreach (var c in targets) if (best == null || actor.Position.DistanceFeet(c.Position) < actor.Position.DistanceFeet(best.Position)) best = c;
            return best;
        }

        /// <summary>Attack the target when in reach (a recharging attack only when ready); otherwise close in, or dash.</summary>
        private static CombatAction Close(CombatEngine engine, CombatantData actor, CombatantData target)
        {
            var t = actor.Turn;
            var stats = engine.Stats(actor);
            var canAttack = !t.ActionUsed || t.AttacksLeft > 0;
            var d = actor.Position.DistanceFeet(target.Position);
            if (canAttack)
                foreach (var o in stats.Attacks)
                    if (o.Melee && o.Kind == AttackKind.Weapon && d <= o.Reach && engine.AttackNotReady(actor, o) == null) return new AttackAction(target.CombatantId, o.Id);
            var melee = stats.BestMelee();
            if (melee != null && engine.MovementLeft(actor) > 0 && !t.Disengaged && d > melee.Reach)
            {
                var path = PathToward(engine, actor, target, melee.Reach);
                if (path.Count > 0) return new MoveAction(path.ToArray());
            }

            if (canAttack)
                foreach (var o in stats.Attacks)
                    if (!o.Melee && d <= o.Range && engine.AttackNotReady(actor, o) == null && engine.Unavailable(actor, new AttackAction(target.CombatantId, o.Id)) == null) return new AttackAction(target.CombatantId, o.Id);
            if (!t.ActionUsed && melee != null && d > melee.Reach && engine.Speed(actor) > 0) return new DashAction();
            return null;
        }

        /// <summary>A greedy path away from every enemy (each step the free square farthest from the nearest one).</summary>
        public static List<GridPos> PathAway(CombatEngine engine, CombatantData actor, List<CombatantData> threats)
        {
            var path = new List<GridPos>();
            var at = actor.Position;
            var left = engine.MovementLeft(actor);
            int Nearest(GridPos p)
            {
                var n = int.MaxValue;
                foreach (var c in threats) n = Math.Min(n, p.DistanceFeet(c.Position));
                return n;
            }

            for (var guard = 0; guard < 30; guard++)
            {
                GridPos? best = null;
                foreach (var (dx, dy) in Directions)
                {
                    var n = new GridPos(at.X + dx, at.Y + dy, at.Z);
                    if (engine.IsBlocked(n)) continue;
                    var who = engine.At(n);
                    if (who != null && who != actor) continue;
                    if (Nearest(n) <= Nearest(at)) continue;
                    if (best == null || Nearest(n) > Nearest(best.Value)) best = n;
                }

                if (best == null) break;
                var cost = engine.StepCost(actor, best.Value);
                if (cost > left) break;
                left -= cost;
                at = best.Value;
                path.Add(at);
            }

            return path;
        }

        /// <summary>A greedy path to a square (home ground).</summary>
        private static List<GridPos> PathTo(CombatEngine engine, CombatantData actor, GridPos goal)
        {
            var path = new List<GridPos>();
            var at = actor.Position;
            var left = engine.MovementLeft(actor);
            for (var guard = 0; guard < 30 && !at.Equals(goal); guard++)
            {
                GridPos? best = null;
                foreach (var (dx, dy) in Directions)
                {
                    var n = new GridPos(at.X + dx, at.Y + dy, at.Z);
                    if (engine.IsBlocked(n)) continue;
                    var who = engine.At(n);
                    if (who != null && who != actor) continue;
                    if (n.DistanceFeet(goal) >= at.DistanceFeet(goal)) continue;
                    if (best == null || n.DistanceFeet(goal) < best.Value.DistanceFeet(goal)) best = n;
                }

                if (best == null) break;
                var cost = engine.StepCost(actor, best.Value);
                if (cost > left) break;
                left -= cost;
                at = best.Value;
                path.Add(at);
            }

            return path;
        }
    }
}
