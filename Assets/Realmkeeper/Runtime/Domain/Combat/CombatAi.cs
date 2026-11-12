using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// How a computer-controlled combatant weighs its options (Phase 8 baseline). Kept as data so personality, faction,
    /// morale and relationships can tune it later without changing the decision service.
    /// </summary>
    public sealed class CombatAiProfile
    {
        /// <summary>Below this share of its hit points it tries to get away (Disengage and step back, or Dodge if it cannot move).</summary>
        public double RetreatBelow { get; set; } = 0.25;

        /// <summary>Whether it keeps attacking someone already down (baseline: no — it turns to those still fighting).</summary>
        public bool FinishTheFallen { get; set; }

        public static readonly CombatAiProfile Default = new CombatAiProfile();
    }

    /// <summary>
    /// The baseline combat decision service (Phase 8). It reads the encounter and proposes the next action of the
    /// active computer-controlled combatant; the engine validates and resolves it, so the AI can never bypass a rule.
    /// Deterministic: the same state gives the same proposal (ties broken by distance, hit points, then ID).
    ///
    /// Order of thought: stand up if prone; get away when badly hurt; attack someone in reach (the weakest);
    /// otherwise shoot when no enemy is next to it; otherwise close in (Dash if too far); otherwise end the turn.
    /// </summary>
    public static partial class CombatAi
    {
        private static readonly (int dx, int dy)[] Directions = { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1) };

        public static CombatAction Next(CombatEngine engine, CombatantData actor, CombatAiProfile profile = null)
        {
            profile = profile ?? CombatAiProfile.Default;
            var e = engine.Encounter;
            if (actor.State != VitalState.Conscious || engine.Incapacitated(actor) && engine.MovementLeft(actor) == 0) return new EndTurnAction();
            var stats = engine.Stats(actor);
            var t = actor.Turn;

            if (engine.Has(actor, ConditionType.Prone) && engine.Unavailable(actor, new StandAction()) == null) return new StandAction();

            var targets = new List<CombatantData>();
            foreach (var c in e.Combatants)
                if (CombatEngine.Hostile(actor, c) && (c.State == VitalState.Conscious || profile.FinishTheFallen && c.State == VitalState.Dying)) targets.Add(c);
            if (targets.Count == 0) return new EndTurnAction();
            targets.Sort((a, b) =>
            {
                var d = actor.Position.DistanceFeet(a.Position).CompareTo(actor.Position.DistanceFeet(b.Position));
                if (d != 0) return d;
                d = engine.Hp(a).CompareTo(engine.Hp(b));
                return d != 0 ? d : a.CombatantId.CompareTo(b.CombatantId);
            });

            // Phase 10: creatures behave like what they are (flee, hold ground, hunt in packs); characters use the baseline.
            var behaviour = BehaviorChoice(engine, actor, targets, ref profile);
            if (behaviour != null) return behaviour;

            var adjacentEnemy = false;
            foreach (var c in targets) if (c.Position.IsAdjacent(actor.Position) && !engine.Incapacitated(c)) adjacentEnemy = true;

            // Badly hurt: get away (Disengage, then step back), or Dodge when it cannot move.
            var hurt = engine.Hp(actor) <= Math.Max(1, (int)(engine.MaxHp(actor) * profile.RetreatBelow));
            if (hurt && adjacentEnemy)
            {
                if (!t.ActionUsed) return engine.Speed(actor) > 0 ? (CombatAction)new DisengageAction() : new DodgeAction();
                if (t.Disengaged && engine.MovementLeft(actor) > 0)
                {
                    var away = StepAway(engine, actor, targets);
                    if (away != null) return away;
                }

                return new EndTurnAction();
            }

            // Phase 9: spells first when they pay (healing the hurt, a blessing, damage or control worth a slot).
            var spell = SpellChoice(engine, actor, targets, profile);
            if (spell != null) return spell;

            var canAttack = !t.ActionUsed || t.AttacksLeft > 0;
            var melee = stats.BestMelee();
            if (canAttack && melee != null)
            {
                CombatantData best = null;
                foreach (var c in targets)
                    if (actor.Position.DistanceFeet(c.Position) <= melee.Reach && engine.CoverOf(actor, c).HasLineOfEffect && (best == null || engine.Hp(c) < engine.Hp(best))) best = c;
                if (best != null) return new AttackAction(best.CombatantId, melee.Id);
            }

            var ranged = stats.BestRanged();
            if (ranged != null && engine.AttackNotReady(actor, ranged) != null) ranged = null; // Phase 10: a recharging attack
            if (canAttack && ranged != null && !adjacentEnemy)
            {
                CombatantData best = null;
                foreach (var c in targets)
                {
                    var d = actor.Position.DistanceFeet(c.Position);
                    if (d > ranged.LongRange) continue;
                    if (!engine.CoverOf(actor, c).HasLineOfEffect) continue; // Phase 14: no shots through walls
                    if (best == null || (d <= ranged.Range) && actor.Position.DistanceFeet(best.Position) > ranged.Range) best = c;
                }

                if (best != null && actor.Position.DistanceFeet(best.Position) <= ranged.Range) return new AttackAction(best.CombatantId, ranged.Id);
            }

            // Close in on the nearest enemy.
            if (melee != null && engine.MovementLeft(actor) > 0 && !t.Disengaged)
            {
                var path = PathToward(engine, actor, targets[0], melee.Reach);
                if (path.Count > 0) return new MoveAction(path.ToArray());
            }

            if (!t.ActionUsed && melee != null && actor.Position.DistanceFeet(targets[0].Position) > melee.Reach && engine.Speed(actor) > 0) return new DashAction();
            // Phase 14: a shooter with nobody in clear line closes in until it has one.
            if (ranged != null && melee == null && engine.MovementLeft(actor) > 0 && !t.Disengaged && !targets.Exists(c => engine.CoverOf(actor, c).HasLineOfEffect))
            {
                var seek = PathToward(engine, actor, targets[0], 5);
                if (seek.Count > 0) return new MoveAction(seek.ToArray());
            }

            if (canAttack && ranged != null && actor.Position.DistanceFeet(targets[0].Position) <= ranged.LongRange && !adjacentEnemy && engine.CoverOf(actor, targets[0]).HasLineOfEffect)
                return new AttackAction(targets[0].CombatantId, ranged.Id);
            return new EndTurnAction();
        }

        /// <summary>A greedy path toward a target (each step the free square closest to it) until within reach or out of movement.</summary>
        public static List<GridPos> PathToward(CombatEngine engine, CombatantData actor, CombatantData target, int reach)
        {
            if (engine.Encounter.Blocked.Count > 0 || engine.Encounter.Doors.Count > 0) return PathAround(engine, actor, target, reach); // Phase 13: walls need a real route
            var path = new List<GridPos>();
            var at = actor.Position;
            var left = engine.MovementLeft(actor);
            for (var guard = 0; guard < 60 && at.DistanceFeet(target.Position) > reach; guard++)
            {
                GridPos? best = null;
                foreach (var (dx, dy) in Directions)
                {
                    var n = new GridPos(at.X + dx, at.Y + dy, at.Z);
                    if (engine.IsBlocked(n)) continue;
                    var who = engine.At(n);
                    if (who != null && who != actor) continue;
                    if (n.DistanceFeet(target.Position) >= at.DistanceFeet(target.Position)) continue;
                    if (best == null || n.DistanceFeet(target.Position) < best.Value.DistanceFeet(target.Position)) best = n;
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

        /// <summary>
        /// Phase 13 (ADR-0064): the shortest route around walls (breadth-first over free squares, within 24 squares) to a
        /// square in reach of the target, cut to the movement left. Deterministic: neighbours are tried in a fixed order.
        /// </summary>
        public static List<GridPos> PathAround(CombatEngine engine, CombatantData actor, CombatantData target, int reach)
        {
            var start = actor.Position;
            // Already where it can strike from: nowhere to go (otherwise it would shuffle between squares in reach).
            if (start.DistanceFeet(target.Position) <= reach && engine.CoverOf(start, target.Position).HasLineOfEffect) return new List<GridPos>();
            var parent = new Dictionary<GridPos, GridPos>();
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            parent[start] = start;
            GridPos? goal = null;
            while (queue.Count > 0 && goal == null)
            {
                var at = queue.Dequeue();
                foreach (var (dx, dy) in Directions)
                {
                    var n = new GridPos(at.X + dx, at.Y + dy, at.Z);
                    if (parent.ContainsKey(n) || Math.Abs(n.X - start.X) > 24 || Math.Abs(n.Y - start.Y) > 24) continue;
                    if (engine.IsBlocked(n)) continue;
                    var who = engine.At(n);
                    if (who != null && who != actor) continue;
                    parent[n] = at;
                    // Phase 14 (ADR-0067): a square it can strike from — in reach and with a clear line (not across a wall's corner).
                    if (n.DistanceFeet(target.Position) <= reach && engine.CoverOf(n, target.Position).HasLineOfEffect) { goal = n; break; }
                    queue.Enqueue(n);
                }
            }

            var path = new List<GridPos>();
            if (goal == null) return path;
            for (var p = goal.Value; !p.Equals(start); p = parent[p]) path.Insert(0, p);
            var left = engine.MovementLeft(actor);
            var cut = new List<GridPos>();
            foreach (var p in path)
            {
                var cost = engine.StepCost(actor, p);
                if (cost > left) break;
                left -= cost;
                cut.Add(p);
            }

            return cut;
        }

        private static MoveAction StepAway(CombatEngine engine, CombatantData actor, List<CombatantData> enemies)
        {
            GridPos? best = null;
            var bestScore = int.MinValue;
            foreach (var (dx, dy) in Directions)
            {
                var n = new GridPos(actor.Position.X + dx, actor.Position.Y + dy, actor.Position.Z);
                if (engine.At(n) != null || engine.IsBlocked(n) || engine.StepCost(actor, n) > engine.MovementLeft(actor)) continue;
                var nearest = int.MaxValue;
                foreach (var en in enemies) nearest = Math.Min(nearest, n.DistanceFeet(en.Position));
                if (nearest > bestScore) { bestScore = nearest; best = n; }
            }

            return best == null || bestScore <= 5 ? null : new MoveAction(best.Value);
        }
    }
}
