using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>A planned move for one combatant to one square: the route, its cost, and whether Dash is needed.</summary>
    public sealed class MovePlan
    {
        /// <summary>The square asked for.</summary>
        public GridPos Destination { get; set; }

        /// <summary>The cheapest route there (excluding the start square); empty when there is none.</summary>
        public List<GridPos> Path { get; } = new List<GridPos>();

        /// <summary>Feet the whole route costs (difficult terrain and crawling counted).</summary>
        public int Cost { get; set; }

        /// <summary>Feet of movement left before moving.</summary>
        public int Left { get; set; }

        /// <summary>How many squares of <see cref="Path"/> can be walked with the movement left (the rest needs Dash).</summary>
        public int ReachableSteps { get; set; }

        /// <summary>The route is longer than the movement left.</summary>
        public bool NeedsDash { get; set; }

        /// <summary>Dash (the action is still free) would add enough movement to arrive.</summary>
        public bool DashReaches { get; set; }

        /// <summary>Squares of difficult terrain along the route.</summary>
        public int DifficultSquares { get; set; }

        /// <summary>Enemies whose reach the route leaves (each may take an opportunity attack), unless the mover has disengaged.</summary>
        public List<string> Provokes { get; } = new List<string>();

        /// <summary>Why the destination cannot be reached at all (null when a route exists).</summary>
        public string Problem { get; set; }

        public bool HasRoute => Problem == null && Path.Count > 0;

        /// <summary>Reachable now, with the movement left.</summary>
        public bool Reachable => HasRoute && !NeedsDash;

        public int LeftAfter => Math.Max(0, Left - Cost);
    }

    /// <summary>
    /// Combat Overhaul: the cheapest route for a combatant from where it stands to a square, over the encounter's own
    /// rules — <see cref="CombatEngine.StepCost"/> (difficult terrain, prone), <see cref="CombatEngine.IsBlocked"/> (walls,
    /// closed doors), hostile combatants (which block), and squares no one may end on. Diagonal steps never cut a wall's
    /// corner. A planner only: the move it proposes is still checked by <see cref="CombatEngine.Unavailable"/> and carried
    /// out by the engine. Deterministic: ties go to the first neighbour in a fixed order.
    /// </summary>
    public static class CombatPaths
    {
        /// <summary>How far (in squares) from the mover the planner looks.</summary>
        public const int Radius = 30;

        private static readonly (int X, int Y)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        public static MovePlan Plan(CombatEngine engine, CombatantData mover, GridPos to)
        {
            var plan = new MovePlan { Destination = to, Left = engine.MovementLeft(mover) };
            var from = mover.Position;
            if (to.Equals(from)) { plan.Problem = "You are already there."; return plan; }
            if (engine.Speed(mover) == 0) { plan.Problem = WhyNoSpeed(engine, mover); return plan; }
            if (Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) > Radius) { plan.Problem = "That is outside the fight."; return plan; }
            if (engine.IsBlocked(to)) { plan.Problem = "A wall or a shut door is there."; return plan; }
            var there = engine.At(to);
            if (there != null && there != mover) { plan.Problem = $"{there.Name} stands there."; return plan; }

            // Dijkstra over the 8-neighbour grid (costs 5, 10 or 15 ft a square).
            var dist = new Dictionary<GridPos, int> { [from] = 0 };
            var prev = new Dictionary<GridPos, GridPos>();
            var open = new SortedSet<(int Cost, int Order, GridPos Pos)>(Comparer<(int Cost, int Order, GridPos Pos)>.Create((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.Order.CompareTo(b.Order)));
            var order = 0;
            open.Add((0, order++, from));
            while (open.Count > 0)
            {
                var cur = open.Min;
                open.Remove(cur);
                if (cur.Pos.Equals(to)) break;
                if (cur.Cost > dist[cur.Pos]) continue;
                foreach (var (dx, dy) in Neighbours)
                {
                    var n = new GridPos(cur.Pos.X + dx, cur.Pos.Y + dy, cur.Pos.Z);
                    if (Math.Max(Math.Abs(n.X - from.X), Math.Abs(n.Y - from.Y)) > Radius) continue;
                    if (engine.IsBlocked(n)) continue;
                    if (dx != 0 && dy != 0 && (engine.IsBlocked(new GridPos(cur.Pos.X + dx, cur.Pos.Y, cur.Pos.Z)) || engine.IsBlocked(new GridPos(cur.Pos.X, cur.Pos.Y + dy, cur.Pos.Z)))) continue; // no squeezing past a corner
                    var occupant = engine.At(n);
                    if (occupant != null && occupant != mover && CombatEngine.Hostile(mover, occupant) && occupant.State == VitalState.Conscious) continue;
                    var c = cur.Cost + engine.StepCost(mover, n);
                    if (dist.TryGetValue(n, out var known) && known <= c) continue;
                    dist[n] = c;
                    prev[n] = cur.Pos;
                    open.Add((c, order++, n));
                }
            }

            if (!dist.ContainsKey(to)) { plan.Problem = "There is no way there: walls, shut doors or enemies are in the way."; return plan; }
            for (var at = to; !at.Equals(from); at = prev[at]) plan.Path.Insert(0, at);
            plan.Cost = dist[to];

            var spent = 0;
            foreach (var step in plan.Path)
            {
                var cost = engine.StepCost(mover, step);
                if (engine.Encounter.DifficultTerrain.Contains(step)) plan.DifficultSquares++;
                if (spent + cost <= plan.Left) { spent += cost; plan.ReachableSteps++; }
                else break;
            }

            plan.NeedsDash = plan.Cost > plan.Left;
            plan.DashReaches = plan.NeedsDash && !mover.Turn.ActionUsed && plan.Cost <= plan.Left + engine.Speed(mover);
            if (!mover.Turn.Disengaged) Provoked(engine, mover, plan);
            return plan;
        }

        /// <summary>Enemies whose reach the route leaves, in the order it leaves them.</summary>
        private static void Provoked(CombatEngine engine, CombatantData mover, MovePlan plan)
        {
            var at = mover.Position;
            foreach (var step in plan.Path)
            {
                foreach (var enemy in engine.Encounter.Combatants)
                {
                    if (!CombatEngine.Hostile(enemy, mover) || !engine.CanReact(enemy) || plan.Provokes.Contains(enemy.Name)) continue;
                    var reach = engine.Stats(enemy).MeleeReach;
                    if (at.DistanceFeet(enemy.Position) <= reach && step.DistanceFeet(enemy.Position) > reach) plan.Provokes.Add(enemy.Name);
                }

                at = step;
            }
        }

        private static string WhyNoSpeed(CombatEngine engine, CombatantData c)
        {
            if (c.State != VitalState.Conscious) return $"{c.Name} cannot move: not conscious.";
            foreach (var t in new[] { ConditionType.Grappled, ConditionType.Restrained, ConditionType.Paralyzed, ConditionType.Petrified })
                if (engine.Has(c, t)) return $"{c.Name} cannot move: {t.ToString().ToLowerInvariant()} (speed 0).";
            return $"{c.Name} cannot move (speed 0).";
        }
    }
}
