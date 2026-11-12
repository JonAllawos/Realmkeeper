using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>SRD 5.2.1 cover: none, half (+2 to AC and Dexterity saves), three-quarters (+5), total (cannot be targeted directly).</summary>
    public enum CoverLevel
    {
        None = 0,
        Half = 1,
        ThreeQuarters = 2,
        Total = 3,
    }

    /// <summary>Cover between two squares, with what gives it (for the log and the targeting prompt).</summary>
    public sealed class CoverResult
    {
        public CoverLevel Level { get; set; }

        /// <summary>"a wall", "a closed door", "Goblin 2", "a table" … (empty for none).</summary>
        public string By { get; set; } = string.Empty;

        /// <summary>Corner lines blocked (0–4) from the attacker's best corner; for the explanation.</summary>
        public int LinesBlocked { get; set; }

        public bool HasLineOfEffect => Level != CoverLevel.Total;

        public int AcBonus => Level == CoverLevel.Half ? 2 : Level == CoverLevel.ThreeQuarters ? 5 : 0;

        /// <summary>The same bonus applies to Dexterity saving throws against an effect from the other side.</summary>
        public int DexSaveBonus => AcBonus;

        public string Describe() => Level == CoverLevel.None ? "no cover" :
            (Level == CoverLevel.Half ? "half cover (+2 AC and Dexterity saves)" : Level == CoverLevel.ThreeQuarters ? "three-quarters cover (+5 AC and Dexterity saves)" : "total cover (no clear line)") +
            (string.IsNullOrEmpty(By) ? string.Empty : " from " + By);
    }

    /// <summary>What stands on the grid: walls (block lines and movement), low obstacles (half cover), and occupied squares.</summary>
    public interface ISightGrid
    {
        /// <summary>A wall, rock or closed door: blocks sight and effect.</summary>
        bool IsWall(int x, int y);

        /// <summary>What the wall is, for explanations ("a wall", "a closed door").</summary>
        string WallName(int x, int y);

        /// <summary>A low obstacle (furniture, rubble, a low wall): gives half cover, never blocks.</summary>
        bool IsLowCover(int x, int y);

        string LowCoverName(int x, int y);

        /// <summary>Someone standing there (not the two ends): gives half cover, never blocks.</summary>
        string CreatureAt(int x, int y);
    }

    /// <summary>
    /// Phase 14 (ADR-0067): the one line-of-sight and cover function, shared by attacks, spells, area effects, the AI,
    /// witnesses and the scene. Squares are unit cells; corners are lattice points. A line is blocked when its open
    /// segment passes through the inside of a wall square, runs along the seam between two wall squares, or squeezes
    /// through a lattice point between two diagonally touching walls. A line that only grazes a wall's edge or corner
    /// is clear. Everything is integer arithmetic, so the same squares always give the same answer, both ways.
    ///
    /// Cover uses corner lines: from each corner of the attacker's square to the four corners of the target's square,
    /// the attacker's best corner counts. Walls blocking 0 lines: no cover; 1–2: half; 3: three-quarters; 4: total.
    /// A line crossing a creature or a low obstacle (and no wall) makes the cover at least half.
    /// </summary>
    public static class GridSight
    {
        /// <summary>Cover of <paramref name="target"/> against <paramref name="from"/>. Different floors: total.</summary>
        public static CoverResult Cover(ISightGrid grid, GridPos from, GridPos target)
        {
            if (from.Z != target.Z) return new CoverResult { Level = CoverLevel.Total, LinesBlocked = 4, By = "a floor between" };
            if (from.X == target.X && from.Y == target.Y) return new CoverResult();
            CoverResult best = null;
            var corners = new[] { (0, 0), (1, 0), (0, 1), (1, 1) };
            foreach (var (ax, ay) in corners)
            {
                var px = (from.X + ax) * 2;
                var py = (from.Y + ay) * 2;
                var walls = 0;
                var soft = 0;
                string wallBy = null, softBy = null;
                foreach (var (tx, ty) in corners)
                {
                    var qx = (target.X + tx) * 2;
                    var qy = (target.Y + ty) * 2;
                    var r = Trace(grid, px, py, qx, qy, from, target);
                    if (r.Wall != null) { walls++; wallBy = wallBy == null ? r.Wall : Named(wallBy, r.Wall); }
                    else if (r.Soft != null) { soft++; softBy = softBy ?? r.Soft; }
                }

                var level = walls >= 4 ? CoverLevel.Total : walls == 3 ? CoverLevel.ThreeQuarters : walls >= 1 ? CoverLevel.Half : CoverLevel.None;
                var by = wallBy;
                if (level == CoverLevel.None && soft > 0) { level = CoverLevel.Half; by = softBy; }
                var candidate = new CoverResult { Level = level, LinesBlocked = walls, By = level == CoverLevel.None ? string.Empty : by ?? string.Empty };
                if (best == null || (int)candidate.Level < (int)best.Level || (candidate.Level == best.Level && candidate.LinesBlocked < best.LinesBlocked)) best = candidate;
                if (best.Level == CoverLevel.None) break;
            }

            return best;
        }

        /// <summary>Line of sight (for seeing a creature or a point): anything short of total cover.</summary>
        public static bool CanSee(ISightGrid grid, GridPos from, GridPos to) => Cover(grid, from, to).HasLineOfEffect;

        /// <summary>
        /// Line of effect from the centre of one square to the centre of another (an area's point of origin to a square
        /// of the area): only walls count. SRD 5.2.1: a spell's area spreads only along unblocked straight lines.
        /// </summary>
        public static bool CentreLineClear(ISightGrid grid, GridPos from, GridPos to)
        {
            if (from.Z != to.Z) return false;
            if (from.X == to.X && from.Y == to.Y) return true;
            return Trace(grid, from.X * 2 + 1, from.Y * 2 + 1, to.X * 2 + 1, to.Y * 2 + 1, from, to).Wall == null;
        }

        private struct TraceResult
        {
            public string Wall;
            public string Soft;
        }

        /// <summary>
        /// Walks the open segment (px,py)→(qx,qy) in doubled coordinates (square (x,y) spans [2x,2x+2]); the ends'
        /// squares are not obstacles to themselves.
        /// </summary>
        private static TraceResult Trace(ISightGrid grid, int px, int py, int qx, int qy, GridPos a, GridPos b)
        {
            var r = new TraceResult();
            int dx = qx - px, dy = qy - py;
            bool Ends(int x, int y) => (x == a.X && y == a.Y) || (x == b.X && y == b.Y);
            bool Wall(int x, int y) => !Ends(x, y) && grid.IsWall(x, y);

            void Square(int x, int y)
            {
                if (r.Wall != null || Ends(x, y)) return;
                if (grid.IsWall(x, y)) { r.Wall = grid.WallName(x, y); return; }
                if (r.Soft != null) return;
                var c = grid.CreatureAt(x, y);
                if (c != null) r.Soft = c;
                else if (grid.IsLowCover(x, y)) r.Soft = grid.LowCoverName(x, y);
            }

            // Two wall squares either side of a seam, or diagonally touching at a point the line squeezes through.
            void Pinch(int x1, int y1, int x2, int y2)
            {
                if (r.Wall == null && Wall(x1, y1) && Wall(x2, y2)) r.Wall = Named(grid.WallName(x1, y1), grid.WallName(x2, y2));
            }

            // At an end that is a corner of its own square: the line leaves (or arrives) on the far side of a corner
            // sealed by the two squares beside that square, i.e. it squeezes out between them.
            void EndSeal(int lx, int ly, GridPos own, int vx, int vy)
            {
                if (lx % 2 != 0 || ly % 2 != 0) return;
                int cx = lx / 2, cy = ly / 2;
                int dxq = 2 * cx - 1 - own.X, dyq = 2 * cy - 1 - own.Y; // the diagonally opposite square
                if (!Wall(dxq, own.Y) || !Wall(own.X, dyq)) return;
                var qxs = own.X >= cx ? 1 : -1;
                var qys = own.Y >= cy ? 1 : -1;
                if (vx * qxs >= 0 && vy * qys >= 0) return; // it goes into (or along) its own square: inside the seal
                r.Wall = r.Wall ?? grid.WallName(dxq, own.Y);
            }

            if (dx == 0 && dy == 0)
            {
                // A shared corner of two diagonal neighbours: the other two squares there pinch it shut.
                if (px % 2 == 0 && py % 2 == 0 && a.X != b.X && a.Y != b.Y)
                {
                    int cx = px / 2, cy = py / 2;
                    Pinch(cx - 1, cy - 1, cx, cy);
                    Pinch(cx - 1, cy, cx, cy - 1);
                }

                return r;
            }

            EndSeal(px, py, a, dx, dy);
            EndSeal(qx, qy, b, -dx, -dy);
            if (r.Wall != null) return r;

            // Every parameter t in (0,1) where the segment crosses an even line (a square edge), as n / den.
            long den = (long)Math.Max(Math.Abs(dx), 1) * Math.Max(Math.Abs(dy), 1) * 2;
            var events = new List<long> { 0, den };
            void Crossings(int start, int delta)
            {
                if (delta == 0) return;
                var step = delta > 0 ? 1 : -1;
                var first = start % 2 == 0 ? start + 2 * step : start + step;
                for (var v = first; step > 0 ? v < start + delta : v > start + delta; v += 2 * step)
                    events.Add((long)(v - start) * den / delta);
            }

            Crossings(px, dx);
            Crossings(py, dy);
            events.Sort();

            long last = -1;
            for (var i = 0; i < events.Count; i++)
            {
                var t = events[i];
                if (t == last) continue;
                if (t > 0 && t < den)
                {
                    // A lattice point passed on the way: squeezing between diagonal walls blocks the line.
                    var X = px * den + dx * t;
                    var Y = py * den + dy * t;
                    if (X % (2 * den) == 0 && Y % (2 * den) == 0)
                    {
                        var cx = (int)(X / (2 * den));
                        var cy = (int)(Y / (2 * den));
                        if (dx == 0 || dy == 0) { Pinch(cx - 1, cy, cx, cy - 1); Pinch(cx - 1, cy - 1, cx, cy); }
                        else if ((dx > 0) == (dy > 0)) Pinch(cx - 1, cy, cx, cy - 1);
                        else Pinch(cx - 1, cy - 1, cx, cy);
                    }
                }

                if (last >= 0)
                {
                    // The piece between the previous crossing and this one lies in one square, or on one seam.
                    var mx = 2 * px * den + dx * (last + t);
                    var my = 2 * py * den + dy * (last + t);
                    var size = 4 * den;
                    var sx = (int)FloorDiv(mx, size);
                    var sy = (int)FloorDiv(my, size);
                    if (dx == 0 && mx % size == 0) Pinch(sx - 1, sy, sx, sy);
                    else if (dy == 0 && my % size == 0) Pinch(sx, sy - 1, sx, sy);
                    else Square(sx, sy);
                }

                if (r.Wall != null) return r;
                last = t;
            }

            return r;
        }

        /// <summary>Prefer the more telling name ("a closed door" over "a wall").</summary>
        private static string Named(string a, string b) => a == "a wall" && b != null ? b : a;

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }

    /// <summary>The fight's own grid as a sight grid: blocked squares, closed doors, low cover and who stands where.</summary>
    public sealed class EncounterSightGrid : ISightGrid
    {
        private readonly HashSet<(int, int)> _walls = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _doors = new HashSet<(int, int)>();
        private readonly Dictionary<(int, int), string> _low = new Dictionary<(int, int), string>();
        private readonly Dictionary<(int, int), string> _people = new Dictionary<(int, int), string>();

        public EncounterSightGrid(EncounterData e, CombatantData ignoreA = null, CombatantData ignoreB = null)
        {
            foreach (var p in e.Blocked) _walls.Add((p.X, p.Y));
            foreach (var d in e.Doors)
                if (!d.Open)
                    foreach (var p in d.Squares) { _walls.Add((p.X, p.Y)); _doors.Add((p.X, p.Y)); }
            foreach (var c in e.LowCover) _low[(c.X, c.Y)] = string.IsNullOrEmpty(c.Name) ? "an obstacle" : c.Name;
            foreach (var c in e.Combatants)
            {
                if (c == ignoreA || c == ignoreB || c.State == VitalState.Dead || c.State == VitalState.Fled) continue;
                _people[(c.Position.X, c.Position.Y)] = c.Name;
            }
        }

        public bool IsWall(int x, int y) => _walls.Contains((x, y));
        public string WallName(int x, int y) => _doors.Contains((x, y)) ? "a closed door" : "a wall";
        public bool IsLowCover(int x, int y) => _low.ContainsKey((x, y));
        public string LowCoverName(int x, int y) => _low.TryGetValue((x, y), out var n) ? n : "an obstacle";
        public string CreatureAt(int x, int y) => _people.TryGetValue((x, y), out var n) ? n : null;
    }
}
