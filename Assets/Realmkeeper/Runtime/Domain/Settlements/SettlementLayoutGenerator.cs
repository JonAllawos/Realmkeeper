using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>
    /// Phase 10's settlement generator (version 2, ADR-0047). Terrain first: the plan reads the settlement's identity
    /// (economy, people, wealth, guard, water) and its ground (the region map around it), lays out the land, cuts the
    /// waterways, then routes the main roads along the land toward the settlement's real road connections (bridging
    /// where they must cross water), sets out districts, streets, a wall where the place is walled, the world's own
    /// places (keeping their site keys), the rest of the buildings by district, and the farms, pastures, orchards and
    /// gardens its farming supports. Deterministic per (world seed, settlement); the result is persisted and never
    /// regenerated.
    /// </summary>
    public static class SettlementLayoutGenerator
    {
        private sealed class Context
        {
            public WorldData World;
            public RegionMap Map;
            public SettlementData Settlement;
            public SettlementLayoutData Plan;
            public Rng Rng;
            public int Next;
            public readonly List<(PlanPoint a, PlanPoint b, float half)> Segments = new List<(PlanPoint, PlanPoint, float)>();
            public readonly List<(PlanPoint c, float w, float d, float yaw)> Footprints = new List<(PlanPoint, float, float, float)>();
            public string Key(string prefix) => prefix + "-" + (Next++).ToString(CultureInfo.InvariantCulture);
        }

        public static SettlementLayoutData Generate(WorldData world, RegionMap map, SettlementData s)
        {
            if (world == null || map == null || s == null) throw new ArgumentNullException(world == null ? nameof(world) : map == null ? nameof(map) : nameof(s));
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "settlement-layout-v2:" + s.SettlementId);
            var ctx = new Context { World = world, Map = map, Settlement = s, Rng = new Rng(seed) };
            var plan = ctx.Plan = new SettlementLayoutData { SettlementId = s.SettlementId, Seed = seed };
            plan.Identity = Identify(world, map, s);
            var size = s.SettlementType >= SettlementType.Town ? 2 : s.SettlementType == SettlementType.Village ? 1 : 0;
            plan.HalfExtent = size == 2 ? 200f : size == 1 ? 150f : 110f;
            plan.PlazaRadius = size == 2 ? 18f : size == 1 ? 14f : 11f;
            plan.CoreRadius = size == 2 ? 105f : size == 1 ? 70f : 40f;
            plan.HasWalls = size == 2;

            var exits = Exits(world, map, s);
            Ground(ctx, exits);
            Water(ctx, exits);
            MainRoads(ctx, exits);
            Districts(ctx, exits);
            Anchors(ctx, exits);
            Streets(ctx, exits);
            DistrictLanes(ctx);
            if (plan.HasWalls) Walls(ctx);
            Sites(ctx);
            Fill(ctx);
            Farms(ctx);
            Gardens(ctx);
            Landmarks(ctx);
            Features(ctx);
            Arrival(ctx, exits);
            return plan;
        }

        // ===== Identity ====================================================================================

        public static SettlementIdentityData Identify(WorldData world, RegionMap map, SettlementData s)
        {
            int Workers(ProductionSourceType t)
            {
                var n = 0;
                foreach (var p in s.Economy?.Production ?? new List<ProductionAssignment>()) if (p.SourceType == t) n += p.WorkerCapacity;
                return n;
            }

            var all = 0;
            foreach (var p in s.Economy?.Production ?? new List<ProductionAssignment>()) all += p.WorkerCapacity;
            var food = Workers(ProductionSourceType.Farming) + Workers(ProductionSourceType.Fishing) + Workers(ProductionSourceType.Hunting);
            var riverCells = int.MaxValue;
            foreach (var r in map.Rivers) foreach (var c in r) riverCells = Math.Min(riverCells, Math.Max(Math.Abs(c.X - s.Position.X), Math.Abs(c.Z - s.Position.Z)));
            var water = riverCells <= 10 ? WaterAccess.River : Workers(ProductionSourceType.Fishing) > 0 ? WaterAccess.Stream : WaterAccess.Well;

            float lo = float.MaxValue, hi = float.MinValue;
            for (var dx = -10; dx <= 10; dx++)
                for (var dz = -10; dz <= 10; dz++)
                {
                    if (!map.InBounds(s.Position.X + dx, s.Position.Z + dz) || map.IsWater(s.Position.X + dx, s.Position.Z + dz)) continue;
                    var e = map.GetWorldHeight(s.Position.X + dx, s.Position.Z + dz);
                    lo = Math.Min(lo, e);
                    hi = Math.Max(hi, e);
                }

            var relief = hi - lo;
            var terrain = relief > 6f ? "hilly" : relief > 2.5f ? "rolling" : "flat";
            var guards = 0;
            foreach (var n in world.Npcs) if (!n.IsDeceased && n.HomeSettlementId == s.SettlementId && n.Role == NpcRole.Guard) guards++;
            var coin = s.Economy?.LifetimeProduced != null ? s.Economy.LifetimeProduced.Get(ResourceTypes.Coin) : 0;
            var wealth = Clamp(s.Prosperity + Math.Min(25, coin / 400), 0, 100);
            var security = Clamp(guards * 14 + s.Stability / 2 + (s.SettlementType >= SettlementType.Town ? 20 : 0), 0, 100);
            var agriculture = all <= 0 ? 0 : Clamp(food * 100 / all, 0, 100);
            var town = s.SettlementType >= SettlementType.Town;
            var archetype = town ? (terrain == "hilly" ? LayoutArchetype.Hillside : LayoutArchetype.PlannedTown)
                : s.SettlementType == SettlementType.Village ? (water == WaterAccess.River ? LayoutArchetype.RiverCrossing : LayoutArchetype.OrganicVillage)
                : water == WaterAccess.River ? LayoutArchetype.RiverCrossing : LayoutArchetype.Farmstead;
            var spec = s.Economy?.Specialization ?? SettlementSpecialization.None;
            var spec2 = s.Economy?.SecondarySpecialization ?? SettlementSpecialization.None;
            var forestry = spec == SettlementSpecialization.Forestry || spec2 == SettlementSpecialization.Forestry;
            var stone = spec == SettlementSpecialization.Quarrying || spec2 == SettlementSpecialization.Quarrying || town && wealth >= 50;
            var architecture = stone ? "stone and slate, with plastered timber houses" : forestry ? "timber frame and thatch" : "plaster, timber and thatch";
            var growth = town ? "old" : s.SettlementType == SettlementType.Village ? "established" : "growing";
            var id = new SettlementIdentityData
            {
                Archetype = archetype, PrimaryEconomy = Economy(spec), SecondaryEconomy = spec2 == SettlementSpecialization.None ? null : Economy(spec2), Water = water,
                Terrain = terrain, Wealth = wealth, Security = security, Agriculture = agriculture, Architecture = architecture, Growth = growth,
            };
            id.Summary = $"{Article(Describe(archetype))} {Describe(archetype)}: {id.PrimaryEconomy}{(id.SecondaryEconomy != null ? " and " + id.SecondaryEconomy : string.Empty)}, " +
                         $"{(water == WaterAccess.River ? "on a river" : water == WaterAccess.Stream ? "beside a stream" : "around its wells")}, {terrain} ground, built in {architecture}.";
            return id;
        }

        private static string Economy(SettlementSpecialization s) =>
            s == SettlementSpecialization.Agricultural ? "farming" : s == SettlementSpecialization.Forestry ? "forestry" : s == SettlementSpecialization.Quarrying ? "quarrying"
            : s == SettlementSpecialization.Mining ? "mining" : s == SettlementSpecialization.Trade ? "trade" : "mixed work";

        public static string Describe(LayoutArchetype a) =>
            a == LayoutArchetype.PlannedTown ? "walled market town" : a == LayoutArchetype.Hillside ? "hillside town" : a == LayoutArchetype.RiverCrossing ? "river-crossing settlement"
            : a == LayoutArchetype.OrganicVillage ? "village grown around its green" : a == LayoutArchetype.Roadside ? "roadside settlement" : a == LayoutArchetype.Fortified ? "fortified settlement"
            : "farmstead hamlet";

        private static string Article(string s) => "aeiou".IndexOf(char.ToLowerInvariant(s[0])) >= 0 ? "An" : "A";

        // ===== Exits: the settlement's real road connections =========================================================

        /// <summary>The bearings (radians, local frame: +X east, +Z north) of the roads that leave the settlement, and where they go.</summary>
        private static List<(float angle, SettlementId toward)> Exits(WorldData world, RegionMap map, SettlementData s)
        {
            var list = new List<(float, SettlementId)>();
            foreach (var road in map.Roads)
            {
                if (road.Cells.Count < 2) continue;
                var from = road.From == s.SettlementId;
                if (!from && road.To != s.SettlementId) continue;
                var k = Math.Min(6, road.Cells.Count - 1);
                var cell = from ? road.Cells[k] : road.Cells[road.Cells.Count - 1 - k];
                list.Add(((float)Math.Atan2(cell.Z - s.Position.Z, cell.X - s.Position.X), from ? road.To : road.From));
            }

            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            // Regional roads that share their first miles leave by the same way: one road out, not two on top of each other.
            for (var i = list.Count - 1; i > 0; i--)
                if (AngleGap(list[i].Item1, list[i - 1].Item1) < 0.45f) list.RemoveAt(i);
            if (list.Count > 2 && AngleGap(list[list.Count - 1].Item1, list[0].Item1) < 0.45f) list.RemoveAt(list.Count - 1);
            if (list.Count == 0) list.Add((-(float)Math.PI / 2, default));
            if (list.Count == 1) list.Add((list[0].Item1 + (float)Math.PI, default)); // a track out the other side
            return list;
        }

        // ===== Ground ========================================================================================

        private static void Ground(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            var t = p.Terrain;
            var s = ctx.Settlement;
            var map = ctx.Map;
            t.NoiseSeed = ctx.Rng.NextULong();
            t.NoiseAmplitude = p.Identity.Terrain == "hilly" ? 2.4f : p.Identity.Terrain == "rolling" ? 1.5f : 0.9f;
            float E(int dx, int dz) => map.InBounds(s.Position.X + dx, s.Position.Z + dz) ? map.GetWorldHeight(s.Position.X + dx, s.Position.Z + dz) : map.GetWorldHeight(s.Position.X, s.Position.Z);
            var gx = E(6, 0) - E(-6, 0);
            var gz = E(0, 6) - E(0, -6);
            t.SlopeDirection = (float)Math.Atan2(gz, gx);
            t.SlopePer100 = Math.Min(5f, 1.2f + (float)Math.Sqrt(gx * gx + gz * gz) * 0.6f);
            t.LevelRadius = p.PlazaRadius + (ctx.Settlement.SettlementType >= SettlementType.Town ? 14f : 10f);

            // The high ground: where the keep, the chapel or the watchtower stands — away from the roads.
            var away = AwayFrom(exits);
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var dist = size == 2 ? 78f : size == 1 ? 55f : 38f; // the town's keep stands between the ring street and the walls
            dist = Math.Max(dist, t.LevelRadius * 1.8f + (size == 2 ? 20f : 9f) + 1f); // its top clear of the square's levelling
            var height = size == 2 ? (p.Identity.Archetype == LayoutArchetype.Hillside ? 18f : 12f) : size == 1 ? 5f : 6f;
            t.Hills.Add(new HillData
            {
                Center = new PlanPoint((float)Math.Cos(away) * dist, (float)Math.Sin(away) * dist), Radius = size == 2 ? 75f : size == 1 ? 42f : 32f,
                Height = height, Plateau = size == 2 ? 20f : 9f,
            });

            // Rises in the countryside.
            for (var i = 0; i < 2 + size; i++)
            {
                var a = (float)(ctx.Rng.Next01() * Math.PI * 2);
                var r = p.CoreRadius + 30f + (float)ctx.Rng.Next01() * (p.HalfExtent - p.CoreRadius - 40f);
                t.Hills.Add(new HillData { Center = new PlanPoint((float)Math.Cos(a) * r, (float)Math.Sin(a) * r), Radius = 30f + (float)ctx.Rng.Next01() * 30f, Height = 2f + (float)ctx.Rng.Next01() * (p.Identity.Terrain == "hilly" ? 9f : 4f), Plateau = 0f });
            }
        }

        /// <summary>The bearing furthest from every road exit (where high ground and quiet districts go).</summary>
        private static float AwayFrom(List<(float angle, SettlementId toward)> exits)
        {
            var best = 0f;
            var bestGap = -1f;
            for (var i = 0; i < 72; i++)
            {
                var a = (float)(i * Math.PI * 2 / 72);
                var gap = float.MaxValue;
                foreach (var e in exits) gap = Math.Min(gap, AngleGap(a, e.angle));
                if (gap > bestGap) { bestGap = gap; best = a; }
            }

            return best;
        }

        private static float AngleGap(float a, float b)
        {
            var d = Math.Abs(a - b) % (float)(Math.PI * 2);
            return d > Math.PI ? (float)(Math.PI * 2) - d : d;
        }

        // ===== Water =========================================================================================

        private static void Water(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            if (p.Identity.Water == WaterAccess.Well || p.Identity.Water == WaterAccess.Pond) return;
            var river = p.Identity.Water == WaterAccess.River;

            // Its course: across the land, clear of the square, on the low side (opposite the high ground).
            var hill = p.Terrain.Hills[0].Center;
            var low = (float)Math.Atan2(-hill.Z, -hill.X);
            var offset = p.HasWalls ? p.PlazaRadius + 62f : river ? p.PlazaRadius + 34f : p.PlazaRadius + 30f; // a town's water runs outside its ring street
            var direction = low + (float)Math.PI / 2;
            if (river)
            {
                // A river keeps the course the region gives it.
                var s = ctx.Settlement;
                MapCoord? first = null, last = null;
                foreach (var r in ctx.Map.Rivers)
                    foreach (var c in r)
                        if (Math.Max(Math.Abs(c.X - s.Position.X), Math.Abs(c.Z - s.Position.Z)) <= 14) { if (first == null) first = c; last = c; }
                if (first != null && last != null && !first.Value.Equals(last.Value)) direction = (float)Math.Atan2(last.Value.Z - first.Value.Z, last.Value.X - first.Value.X);
            }

            var dx = (float)Math.Cos(direction);
            var dz = (float)Math.Sin(direction);
            // The course passes the square at the offset, square to its own direction, on the side away from the high ground.
            var sx = -dz;
            var sz = dx;
            if (sx * hill.X + sz * hill.Z > 0) { sx = -sx; sz = -sz; }
            var nx = sx * offset;
            var nz = sz * offset;
            var w = new WaterwayData { Key = river ? "river" : "stream", IsRiver = river, Width = river ? 12f : 4.5f, Depth = river ? 2.4f : 1.1f };
            var span = p.HalfExtent * 1.45f;
            var amplitude = river ? 10f : 7f;
            var phase = (float)(ctx.Rng.Next01() * Math.PI * 2);
            for (var i = 0; i <= 12; i++)
            {
                var u = -span + 2 * span * i / 12f;
                var wiggle = amplitude * (float)Math.Sin(phase + i * 0.9f);
                var x = nx + dx * u - dz * wiggle;
                var z = nz + dz * u + dx * wiggle;
                // Keep the square dry whatever the wiggle does.
                var r = (float)Math.Sqrt(x * x + z * z);
                if (r < p.PlazaRadius + w.Width + SettlementTerrain.BankWidth + 6f)
                {
                    var push = (p.PlazaRadius + w.Width + SettlementTerrain.BankWidth + 6f) / Math.Max(1f, r);
                    x *= push;
                    z *= push;
                }

                w.Line.Add(new PlanPoint(x, z));
            }

            p.Waterways.Add(w);
        }

        // ===== Roads =========================================================================================

        private static float Bearing(PlanPoint from, PlanPoint to) => (float)Math.Atan2(to.Z - from.Z, to.X - from.X);

        /// <summary>
        /// A road that follows the land: from the start it steps toward its goal, each step taking the heading (within
        /// 40° of the goal's bearing) with the least climb; where it must cross water it goes straight over, and the
        /// crossing is recorded for a bridge.
        /// </summary>
        private static RoadData Route(Context ctx, string key, RoadKind kind, float width, PlanPoint start, PlanPoint goal, float step = 6f)
        {
            var p = ctx.Plan;
            var road = new RoadData { Key = key, Kind = kind, Width = width };
            road.Line.Add(start);
            var at = start;
            var inWater = false;
            for (var guard = 0; guard < 400 && at.DistanceTo(goal) > step; guard++)
            {
                var bearing = Bearing(at, goal);
                PlanPoint best = default;
                var bestCost = float.MaxValue;
                foreach (var turn in new[] { 0f, -0.25f, 0.25f, -0.5f, 0.5f, -0.7f, 0.7f })
                {
                    var h = bearing + turn;
                    var next = new PlanPoint(at.X + (float)Math.Cos(h) * step, at.Z + (float)Math.Sin(h) * step);
                    var climb = Math.Abs(p.HeightAt(next.X, next.Z) - p.HeightAt(at.X, at.Z));
                    var cost = climb * climb * 6f + Math.Abs(turn) * 2.2f;
                    if (next.DistanceTo(goal) > step && Blocked(p, next, width)) cost += 80f; // go round what stands there
                    if (!inWater && SettlementTerrain.InWater(p, next.X, next.Z, 1f)) cost += turn == 0f ? 4f : 60f; // cross straight, or not at all
                    if (cost < bestCost) { bestCost = cost; best = next; }
                }

                inWater = SettlementTerrain.InWater(p, best.X, best.Z, 1.5f);

                at = best;
                road.Line.Add(at);
            }

            road.Line.Add(goal);
            return road;
        }

        /// <summary>True when a road of this width at a point would cut into a building already placed.</summary>
        private static bool Blocked(SettlementLayoutData p, PlanPoint at, float width)
        {
            foreach (var b in p.Buildings)
                if (b.Function != BuildingFunction.Gatehouse && at.DistanceTo(b.Center) < width / 2 + (float)Math.Sqrt(b.Width * b.Width + b.Depth * b.Depth) / 2 + 1f) return true;
            return false;
        }

        /// <summary>True when any stretch of a road cuts into a building already placed.</summary>
        private static bool Crosses(SettlementLayoutData p, RoadData road)
        {
            foreach (var b in p.Buildings)
            {
                if (b.Function == BuildingFunction.Gatehouse) continue;
                if (SettlementLayoutValidator.Distance(road, b.Center) < road.Width / 2 + (float)Math.Sqrt(b.Width * b.Width + b.Depth * b.Depth) / 2 + 0.5f) return true;
            }

            return false;
        }

        private static void AddBridge(Context ctx, RoadData road, PlanPoint from, PlanPoint to)
        {
            var p = ctx.Plan;
            WaterwayData water = null;
            var best = float.MaxValue;
            foreach (var w in p.Waterways)
            {
                var d = SettlementTerrain.DistanceToLine(w, (from.X + to.X) / 2, (from.Z + to.Z) / 2, out _, out _);
                if (d < best) { best = d; water = w; }
            }

            if (water == null) return;
            var deck = Math.Max(p.HeightAt(from.X, from.Z), p.HeightAt(to.X, to.Z)) + 0.15f;
            var small = !water.IsRiver && road.Kind != RoadKind.Main;
            p.Bridges.Add(new BridgeData
            {
                Key = ctx.Key("bridge"), RoadKey = road.Key, WaterwayKey = water.Key, From = from, To = to, Width = Math.Max(2.2f, road.Width),
                DeckHeight = deck, IsFord = small && road.Kind == RoadKind.FarmTrack, Stone = water.IsRiver || p.Identity.Wealth >= 55,
            });
        }

        /// <summary>Adds a road to the plan with the bridges its route needed (a road left out leaves no bridge behind).</summary>
        private static void AddRoad(Context ctx, RoadData road)
        {
            ctx.Plan.Roads.Add(road);
            for (var i = 0; i + 1 < road.Line.Count; i++) ctx.Segments.Add((road.Line[i], road.Line[i + 1], road.Width / 2f));
            TrimWetEnds(p0: ctx.Plan, road);
            BridgeCrossings(ctx, road);
        }

        /// <summary>An open road never starts or ends in the water: wet points at either end are dropped.</summary>
        private static void TrimWetEnds(SettlementLayoutData p0, RoadData road)
        {
            if (road.Line.Count < 3 || road.Line[0].DistanceTo(road.Line[road.Line.Count - 1]) < 0.01f) return; // a loop
            while (road.Line.Count > 2 && SettlementTerrain.InWater(p0, road.Line[0].X, road.Line[0].Z, 0.75f)) road.Line.RemoveAt(0);
            while (road.Line.Count > 2 && SettlementTerrain.InWater(p0, road.Line[road.Line.Count - 1].X, road.Line[road.Line.Count - 1].Z, 0.75f)) road.Line.RemoveAt(road.Line.Count - 1);
        }

        /// <summary>
        /// Every place a road crosses water gets its bridge (or ford): the road is walked a metre at a time, and each wet
        /// stretch is spanned from the last dry point before it to the first dry point after it.
        /// </summary>
        private static void BridgeCrossings(Context ctx, RoadData road)
        {
            var p = ctx.Plan;
            if (p.Waterways.Count == 0) return;
            var wet = false;
            var lastDry = road.Line[0];
            var from = lastDry;
            for (var i = 0; i + 1 < road.Line.Count; i++)
            {
                var a = road.Line[i];
                var b = road.Line[i + 1];
                var n = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b)));
                for (var j = i == 0 ? 0 : 1; j <= n; j++)
                {
                    var t = j / (float)n;
                    var pt = new PlanPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                    var w = SettlementTerrain.InWater(p, pt.X, pt.Z, 0.75f);
                    if (w && !wet) { wet = true; from = lastDry; }
                    else if (!w && wet) { wet = false; AddBridge(ctx, road, from, pt); }
                    if (!w) lastDry = pt;
                }
            }
        }

        /// <summary>True when a road already passes within <paramref name="distance"/> metres of a point.</summary>
        private static bool Served(SettlementLayoutData p, PlanPoint at, float distance)
        {
            foreach (var r in p.Roads) if (SettlementLayoutValidator.Distance(r, at) <= distance) return true;
            return false;
        }

        private static void MainRoads(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var width = size == 2 ? 6f : size == 1 ? 5f : 4f;
            var surface = size == 2 ? RoadSurface.Gravel : size == 1 ? RoadSurface.PackedEarth : RoadSurface.Dirt;
            for (var i = 0; i < exits.Count; i++)
            {
                var a = exits[i].angle;
                var start = new PlanPoint((float)Math.Cos(a) * p.PlazaRadius, (float)Math.Sin(a) * p.PlazaRadius);
                var goal = new PlanPoint((float)Math.Cos(a) * (p.HalfExtent - 2f), (float)Math.Sin(a) * (p.HalfExtent - 2f));
                // A road does not leave the plan in the water: turn its end a little onto dry land.
                foreach (var turn in new[] { 0.12f, -0.12f, 0.25f, -0.25f, 0.4f, -0.4f, 0.6f, -0.6f })
                {
                    if (!SettlementTerrain.InWater(p, goal.X, goal.Z, 4f)) break;
                    goal = new PlanPoint((float)Math.Cos(a + turn) * (p.HalfExtent - 2f), (float)Math.Sin(a + turn) * (p.HalfExtent - 2f));
                }

                var road = Route(ctx, "road-" + i.ToString(CultureInfo.InvariantCulture), RoadKind.Main, width, start, goal);
                road.Surface = surface;
                road.Toward = exits[i].toward;
                AddRoad(ctx, road);
            }
        }

        // ===== Districts =====================================================================================

        private static void Districts(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            var hill = p.Terrain.Hills[0];
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            void Add(string key, DistrictType type, string name, PlanPoint at, float r) => p.Districts.Add(new DistrictData { Key = key, Type = type, Name = name, Center = Dry(p, at), Radius = r });
            PlanPoint Polar(float angle, float r) => new PlanPoint((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r);
            var waterSide = p.Waterways.Count > 0 ? Bearing(new PlanPoint(0, 0), p.Waterways[0].Line[p.Waterways[0].Line.Count / 2]) : AwayFrom(exits) + (float)Math.PI;
            var road0 = exits[0].angle;
            var road1 = exits.Count > 1 ? exits[1].angle : road0 + (float)Math.PI;

            if (size == 2)
            {
                Add("market", DistrictType.Market, "Market Square", new PlanPoint(0, 0), p.PlazaRadius + 26f);
                Add("upper", DistrictType.Civic, "Keep Hill", hill.Center, 45f);
                Add("craft", DistrictType.Craft, "Smiths' Row", Polar(Mid(road0, waterSide), 62f), 38f);
                Add("waterside", DistrictType.Waterfront, "Millside", Polar(waterSide, 70f), 34f);
                Add("lanes", DistrictType.WorkingResidential, "The Lanes", Polar(Mid(road1, waterSide), 62f), 40f);
                Add("high", DistrictType.WealthyResidential, "High Street", Polar(Mid(road1, Bearing(new PlanPoint(0, 0), hill.Center)), 58f), 36f);
                Add("chapel", DistrictType.Religious, "Shrine Close", Polar(Mid(road0, Bearing(new PlanPoint(0, 0), hill.Center)), 50f), 26f);
                Add("fields", DistrictType.Agricultural, "Town Fields", Polar(road0 + 2.2f, 160f), 80f);
                Add("outskirts", DistrictType.Outskirts, "Outskirts", Polar(road0 + (float)Math.PI, 150f), 70f);
            }
            else if (size == 1)
            {
                Add("green", DistrictType.Green, "Village Green", new PlanPoint(0, 0), p.PlazaRadius + 16f);
                Add("churchside", DistrictType.Religious, "Chapel Rise", hill.Center, 26f);
                Add("roadside", DistrictType.Residential, "Roadside", Polar(road0, 45f), 30f);
                Add("backlane", DistrictType.Residential, "Back Lane", Polar(road1, 45f), 30f);
                Add("forge", DistrictType.Craft, "Forge End", Polar(Mid(road0, road1 + (float)Math.PI), 48f), 22f);
                if (p.Waterways.Count > 0) Add("brookside", DistrictType.Waterfront, "Brookside", Polar(waterSide, 50f), 26f);
                Add("farms", DistrictType.Agricultural, "Farms", Polar(road0 + 1.6f, 110f), 60f);
            }
            else
            {
                Add("green", DistrictType.Green, "The Green", new PlanPoint(0, 0), p.PlazaRadius + 12f);
                Add("rise", DistrictType.Military, "Watch Rise", hill.Center, 18f);
                if (p.Waterways.Count > 0) Add("riverside", DistrictType.Waterfront, "Riverside", Polar(waterSide, 42f), 22f);
                Add("farms", DistrictType.Agricultural, "Farmsteads", Polar(road0 + 1.4f, 70f), 50f);
            }
        }

        /// <summary>A district centre on dry ground: pulled back from any water toward the square.</summary>
        private static PlanPoint Dry(SettlementLayoutData p, PlanPoint at)
        {
            for (var i = 0; i < 20 && SettlementTerrain.InWater(p, at.X, at.Z, SettlementTerrain.BankWidth + 6f); i++) at = new PlanPoint(at.X * 0.9f, at.Z * 0.9f);
            return at;
        }

        private static float Mid(float a, float b)
        {
            var x = Math.Cos(a) + Math.Cos(b);
            var z = Math.Sin(a) + Math.Sin(b);
            return Math.Abs(x) + Math.Abs(z) < 1e-4 ? a + (float)Math.PI / 2 : (float)Math.Atan2(z, x);
        }

        // ===== Streets =======================================================================================

        private static void Streets(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var lane = size == 2 ? 4f : 3f;
            var origin = new PlanPoint(0, 0);
            if (size == 2)
            {
                // A ring street around the market, and a street out to each district; a winding way up Keep Hill.
                var ring = new RoadData { Key = "ring", Kind = RoadKind.Secondary, Width = 4.5f, Surface = p.Identity.Wealth >= 50 ? RoadSurface.Paved : RoadSurface.Gravel };
                const int n = 64;
                // The loop starts and ends on dry ground (a crossing is then a stretch within it, bridged like any other).
                var turnFrom = 0f;
                for (var k = 0; k < n; k++)
                {
                    var ka = (float)(k * Math.PI * 2 / n);
                    if (!SettlementTerrain.InWater(p, (float)Math.Cos(ka) * 52f, (float)Math.Sin(ka) * 52f, 3f)) { turnFrom = ka; break; }
                }

                for (var i = 0; i <= n; i++)
                {
                    var a = turnFrom + (float)(i * Math.PI * 2 / n);
                    ring.Line.Add(new PlanPoint((float)Math.Cos(a) * 52f, (float)Math.Sin(a) * 52f));
                }

                AddRoad(ctx, ring);
                foreach (var d in p.Districts)
                {
                    if (d.Type == DistrictType.Market || d.Type == DistrictType.Agricultural || d.Type == DistrictType.Outskirts) continue;
                    var from = new PlanPoint((float)Math.Cos(Bearing(origin, d.Center)) * 52f, (float)Math.Sin(Bearing(origin, d.Center)) * 52f);
                    var kind = d.Type == DistrictType.Civic ? RoadKind.Secondary : RoadKind.Lane;
                    if (SettlementTerrain.InWater(p, from.X, from.Z, 3f)) continue; // the ring is on a bridge there
                    var stop = Short(from, d.Center, d.Type == DistrictType.Civic ? 16f : 6f);
                    if (Served(p, d.Center, 14f) && d.Type != DistrictType.Civic) continue; // a road already runs through it
                    if (from.DistanceTo(stop) < 6f) continue;
                    var r = Route(ctx, "street-" + d.Key, kind, kind == RoadKind.Lane ? lane : 4.5f, from, stop, 5f);
                    r.Surface = d.Type == DistrictType.WealthyResidential || d.Type == DistrictType.Civic ? RoadSurface.Paved : RoadSurface.Gravel;
                    AddRoad(ctx, r);
                }

                // The market streets: the square's edges are paved.
                foreach (var road in p.Roads) if (road.Kind == RoadKind.Main) road.Surface = p.Identity.Wealth >= 45 ? RoadSurface.Paved : RoadSurface.Gravel;
                RebuildSegments(ctx);
            }
            else
            {
                // Lanes from the green to each district; a loop round the green in a hamlet.
                foreach (var d in p.Districts)
                {
                    if (d.Type == DistrictType.Green || d.Type == DistrictType.Agricultural) continue;
                    if (Served(p, d.Center, 16f)) continue; // a road already runs through it
                    // Leave the green toward the district — or a little to one side, where the hall stands in the way.
                    RoadData r = null;
                    foreach (var shift in new[] { 0f, 0.35f, -0.35f, 0.7f, -0.7f })
                    {
                        var ba = Bearing(origin, d.Center) + shift;
                        var start = new PlanPoint((float)Math.Cos(ba) * p.PlazaRadius, (float)Math.Sin(ba) * p.PlazaRadius);
                        var tryRoad = Route(ctx, "lane-" + d.Key, RoadKind.Lane, lane, start, Short(start, d.Center, d.Type == DistrictType.Religious || d.Type == DistrictType.Military ? 12f : 4f), 5f);
                        if (!Crosses(p, tryRoad)) { r = tryRoad; break; }
                    }

                    if (r == null) continue;
                    r.Surface = size == 1 ? RoadSurface.PackedEarth : RoadSurface.Dirt;
                    AddRoad(ctx, r);
                }

                if (size == 1)
                {
                    // A back lane linking the two residential ends behind the green.
                    var a = Find(p, "roadside");
                    var b = Find(p, "backlane");
                    if (a != null && b != null && a.Center.DistanceTo(b.Center) > 20f)
                    {
                        var mid = new PlanPoint((a.Center.X + b.Center.X) / 2 + (a.Center.Z - b.Center.Z) * 0.35f, (a.Center.Z + b.Center.Z) / 2 - (a.Center.X - b.Center.X) * 0.35f);
                        var r1 = Route(ctx, "backlane-a", RoadKind.Lane, lane, a.Center, mid, 5f);
                        var r2 = Route(ctx, "backlane-b", RoadKind.Lane, lane, mid, b.Center, 5f);
                        r1.Surface = r2.Surface = RoadSurface.Dirt;
                        if (!Crosses(p, r1) && !Crosses(p, r2))
                        {
                            AddRoad(ctx, r1);
                            AddRoad(ctx, r2);
                        }
                    }
                }
            }
        }

        /// <summary>A point short of a goal by some metres (so a street ends in front of what stands there).</summary>
        private static PlanPoint Short(PlanPoint from, PlanPoint to, float by)
        {
            var d = from.DistanceTo(to);
            if (d <= by + 1f) return from;
            var k = (d - by) / d;
            return new PlanPoint(from.X + (to.X - from.X) * k, from.Z + (to.Z - from.Z) * k);
        }

        /// <summary>
        /// The lanes a district is built along: two or three through its middle at different angles (more in the busy
        /// districts), following the land. They give the district its frontage; buildings line them.
        /// </summary>
        private static void DistrictLanes(Context ctx)
        {
            var p = ctx.Plan;
            foreach (var d in p.Districts)
            {
                int n;
                switch (d.Type)
                {
                    case DistrictType.Market: case DistrictType.WorkingResidential: case DistrictType.Residential: n = 3; break;
                    case DistrictType.Craft: case DistrictType.WealthyResidential: case DistrictType.Waterfront: case DistrictType.Green: n = 2; break;
                    case DistrictType.Civic: case DistrictType.Religious: n = 1; break;
                    default: continue;
                }

                var baseAngle = (float)(ctx.Rng.Next01() * Math.PI);
                for (var k = 0; k < n; k++)
                {
                    var a = baseAngle + (float)(k * Math.PI / n);
                    var half = d.Radius * 0.95f;
                    var from = new PlanPoint(d.Center.X - (float)Math.Cos(a) * half, d.Center.Z - (float)Math.Sin(a) * half);
                    var to = new PlanPoint(d.Center.X + (float)Math.Cos(a) * half, d.Center.Z + (float)Math.Sin(a) * half);
                    if (Math.Sqrt(d.Center.X * d.Center.X + d.Center.Z * d.Center.Z) < p.PlazaRadius + 4f && d.Type == DistrictType.Market) { from = Short(new PlanPoint(0, 0), from, -p.PlazaRadius); }
                    if (SettlementTerrain.InWater(p, from.X, from.Z, 4f) || SettlementTerrain.InWater(p, to.X, to.Z, 4f)) continue;
                    if (Math.Abs(from.X) > p.HalfExtent - 8 || Math.Abs(from.Z) > p.HalfExtent - 8 || Math.Abs(to.X) > p.HalfExtent - 8 || Math.Abs(to.Z) > p.HalfExtent - 8) continue;
                    var lane = Route(ctx, "lane-" + d.Key + "-" + k.ToString(CultureInfo.InvariantCulture), RoadKind.Lane, d.Type == DistrictType.Market ? 4.5f : 3.2f, from, to, 5f);
                    lane.Surface = d.Type == DistrictType.Market ? RoadSurface.Stone : d.Type == DistrictType.WealthyResidential || d.Type == DistrictType.Civic ? RoadSurface.Paved : p.Identity.Wealth >= 50 ? RoadSurface.Gravel : RoadSurface.PackedEarth;
                    if (d.Type == DistrictType.Market) lane.Kind = RoadKind.MarketStreet;
                    if (Crosses(p, lane)) continue; // it would run through the hall or the landmark
                    if (!SettlementLayoutValidator.Joins(p, lane))
                    {
                        // A lane off on its own is joined to the nearest street by a short link from its nearer end.
                        var end = lane.Line[0].DistanceTo(new PlanPoint(0, 0)) <= lane.Line[lane.Line.Count - 1].DistanceTo(new PlanPoint(0, 0)) ? lane.Line[0] : lane.Line[lane.Line.Count - 1];
                        var link = Route(ctx, lane.Key + "-link", RoadKind.Lane, lane.Width, end, Nearest(ctx, end, out _), 5f);
                        link.Surface = lane.Surface;
                        if (Crosses(p, link)) continue;
                        AddRoad(ctx, lane);
                        AddRoad(ctx, link);
                        continue;
                    }

                    AddRoad(ctx, lane);
                }
            }
        }

        private static void RebuildSegments(Context ctx)
        {
            ctx.Segments.Clear();
            foreach (var r in ctx.Plan.Roads)
                for (var i = 0; i + 1 < r.Line.Count; i++) ctx.Segments.Add((r.Line[i], r.Line[i + 1], r.Width / 2f));
        }

        private static DistrictData Find(SettlementLayoutData p, string key)
        {
            foreach (var d in p.Districts) if (d.Key == key) return d;
            return null;
        }

        // ===== Walls =========================================================================================

        private static void Walls(Context ctx)
        {
            var p = ctx.Plan;
            const int n = 32;
            for (var i = 0; i < n; i++)
            {
                var a = (float)(i * Math.PI * 2 / n);
                p.Wall.Add(new PlanPoint((float)Math.Cos(a) * p.CoreRadius, (float)Math.Sin(a) * p.CoreRadius));
            }

            foreach (var r in p.Roads)
            {
                if (r.Kind != RoadKind.Main) continue;
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var a = r.Line[i];
                    var b = r.Line[i + 1];
                    var ra = (float)Math.Sqrt(a.X * a.X + a.Z * a.Z);
                    var rb = (float)Math.Sqrt(b.X * b.X + b.Z * b.Z);
                    if ((ra - p.CoreRadius) * (rb - p.CoreRadius) > 0) continue;
                    var t = (p.CoreRadius - ra) / Math.Max(0.001f, rb - ra);
                    p.Gates.Add(new PlanPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t));
                    break;
                }
            }
        }

        /// <summary>True when a point is on the wall's line (where nothing may be built).</summary>
        private static bool OnWall(SettlementLayoutData p, PlanPoint c, float radius)
        {
            if (!p.HasWalls) return false;
            var r = (float)Math.Sqrt(c.X * c.X + c.Z * c.Z);
            return Math.Abs(r - p.CoreRadius) < radius + 4f;
        }

        // ===== Plots and buildings ===========================================================================

        /// <summary>The ground under a footprint: its highest and lowest corner (and centre).</summary>
        private static (float hi, float lo) Under(SettlementLayoutData p, PlanPoint c, float w, float d, float yaw)
        {
            float hi = float.MinValue, lo = float.MaxValue;
            foreach (var (lx, lz) in new[] { (0f, 0f), (-w / 2, -d / 2), (w / 2, -d / 2), (-w / 2, d / 2), (w / 2, d / 2) })
            {
                var pt = Local(c, yaw, lx, lz);
                var h = p.HeightAt(pt.X, pt.Z);
                hi = Math.Max(hi, h);
                lo = Math.Min(lo, h);
            }

            return (hi, lo);
        }

        /// <summary>The yaw (degrees) of a building at <paramref name="at"/> whose door — direction (sin yaw, −cos yaw) — faces <paramref name="toward"/>.</summary>
        public static float FaceYaw(PlanPoint at, PlanPoint toward)
        {
            var theta = Math.Atan2(toward.Z - at.Z, toward.X - at.X) * 180.0 / Math.PI;
            var yaw = (float)(theta + 90.0);
            return (yaw % 360f + 360f) % 360f;
        }

        /// <summary>A point in a building's frame (door on local −Z) in plan coordinates.</summary>
        public static PlanPoint Local(PlanPoint c, float yaw, float lx, float lz)
        {
            var r = yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            return new PlanPoint(c.X + lx * cos - lz * sin, c.Z + lx * sin + lz * cos);
        }

        /// <summary>
        /// Whether a footprint may stand here: inside the land, off the square, the water, the wall line and every road,
        /// clear of other footprints, and on ground it can be levelled on (at most <paramref name="maxStep"/> metres of plinth).
        /// </summary>
        private static bool Fits(Context ctx, PlanPoint c, float w, float d, float yaw, float maxStep, float margin = 1.2f)
        {
            var p = ctx.Plan;
            var radius = (float)Math.Sqrt(w * w + d * d) / 2f;
            if (Math.Abs(c.X) > p.HalfExtent - radius - 3 || Math.Abs(c.Z) > p.HalfExtent - radius - 3) return false;
            if (Math.Sqrt(c.X * c.X + c.Z * c.Z) < p.PlazaRadius + radius + 2f) return false;
            if (SettlementTerrain.InWater(p, c.X, c.Z, radius + 3f)) return false;
            if (OnWall(p, c, radius)) return false;
            foreach (var (a, b, half) in ctx.Segments)
            {
                // Cheap reject first: the stretch is nowhere near.
                if (SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, c.X, c.Z, out _, out _) > radius + half + 0.6f) continue;
                if (Touches(c, w, d, yaw, a, b, half + 0.6f)) return false;
            }

            foreach (var (oc, ow, od, oyaw) in ctx.Footprints)
            {
                var or = (float)Math.Sqrt(ow * ow + od * od) / 2f;
                if (c.DistanceTo(oc) > radius + or + margin) continue;
                if (Overlap(c, w + margin, d + margin, yaw, oc, ow + margin, od + margin, oyaw)) return false;
            }

            var (hi, lo) = Under(p, c, w, d, yaw);
            return hi - lo <= maxStep;
        }

        /// <summary>Whether two footprints (rectangles about their centres, turned by their yaws) overlap: separating axes.</summary>
        public static bool Overlap(PlanPoint ac, float aw, float ad, float ayaw, PlanPoint bc, float bw, float bd, float byaw)
        {
            var ar = ayaw * Math.PI / 180.0;
            var br = byaw * Math.PI / 180.0;
            var axes = new[]
            {
                ((float)Math.Cos(ar), (float)Math.Sin(ar)), (-(float)Math.Sin(ar), (float)Math.Cos(ar)),
                ((float)Math.Cos(br), (float)Math.Sin(br)), (-(float)Math.Sin(br), (float)Math.Cos(br)),
            };
            var dx = bc.X - ac.X;
            var dz = bc.Z - ac.Z;
            foreach (var (nx, nz) in axes)
            {
                float Extent(float yawR, float w, float d)
                {
                    var ux = (float)Math.Cos(yawR);
                    var uz = (float)Math.Sin(yawR);
                    return Math.Abs(ux * nx + uz * nz) * w / 2 + Math.Abs(-uz * nx + ux * nz) * d / 2;
                }

                if (Math.Abs(dx * nx + dz * nz) > Extent((float)ar, aw, ad) + Extent((float)br, bw, bd)) return false;
            }

            return true;
        }

        /// <summary>
        /// True when a road stretch from <paramref name="a"/> to <paramref name="b"/> comes within <paramref name="clearance"/>
        /// of a footprint (a rectangle about <paramref name="c"/>): the stretch is taken into the footprint's frame and
        /// measured against the box exactly.
        /// </summary>
        private static bool Touches(PlanPoint c, float w, float d, float yaw, PlanPoint a, PlanPoint b, float clearance)
        {
            var r = yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            (float x, float z) ToLocal(PlanPoint q) => ((q.X - c.X) * cos + (q.Z - c.Z) * sin, -(q.X - c.X) * sin + (q.Z - c.Z) * cos);
            var (ax, az) = ToLocal(a);
            var (bx, bz) = ToLocal(b);
            var hw = w / 2 + clearance;
            var hd = d / 2 + clearance;
            // Sample the stretch finely (stretches are a few metres): any sample inside the grown box touches.
            var len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            var n = Math.Max(1, (int)Math.Ceiling(len / 0.5f));
            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                var x = ax + (bx - ax) * t;
                var z = az + (bz - az) * t;
                if (Math.Abs(x) <= hw && Math.Abs(z) <= hd) return true;
            }

            return false;
        }

        private static BuildingData Place(Context ctx, string key, BuildingFunction f, string district, PlanPoint c, float w, float d, float h, float yaw, bool site = false)
        {
            var p = ctx.Plan;
            var (hi, lo) = Under(p, c, w, d, yaw);
            var wealth = p.Identity.Wealth;
            var stoneDistrict = district == "upper" || district == "market" || district == "high";
            var b = new BuildingData
            {
                Key = key, Function = f, DistrictKey = district, Center = c, Yaw = yaw, Width = w, Depth = d, Height = h, Storeys = h > 5.6f ? 2 : 1,
                FloorHeight = hi + 0.1f, Foundation = hi - lo + 0.3f, IsSite = site, LocationKey = site ? key : null,
                Roof = f == BuildingFunction.Barn || f == BuildingFunction.Stable || f == BuildingFunction.Warehouse ? RoofShape.Gable : ctx.Rng.Next01() < (wealth >= 55 ? 0.35 : 0.15) ? RoofShape.Hip : RoofShape.Gable,
                Walls = f == BuildingFunction.Keep || f == BuildingFunction.Watchtower || f == BuildingFunction.Gatehouse || f == BuildingFunction.Religious ? WallMaterial.Stone
                    : stoneDistrict && ctx.Rng.Next01() < 0.55 ? WallMaterial.Stone : f == BuildingFunction.Barn || f == BuildingFunction.Shed || f == BuildingFunction.Stable ? WallMaterial.Timber
                    : ctx.Rng.Next01() < 0.5 ? WallMaterial.Plaster : WallMaterial.Timber,
                Palette = ctx.Rng.Int(6), Chimney = f != BuildingFunction.Barn && f != BuildingFunction.Shed && ctx.Rng.Next01() < 0.7, Porch = ctx.Rng.Next01() < 0.25,
            };
            p.Buildings.Add(b);
            ctx.Footprints.Add((c, w, d, yaw));
            return b;
        }

        /// <summary>
        /// Plots along a district's streets: walk each road through the district at the district's spacing, both sides,
        /// facing the street, and keep the first that fits. Returns the plot, or null.
        /// </summary>
        private static (PlanPoint c, float yaw)? Plot(Context ctx, DistrictData district, float w, float d, float spacing, float maxStep, float setback = 1.5f)
        {
            var p = ctx.Plan;
            var stride = Math.Max(2.5f, spacing / 3f); // look between the regular plots too, so gaps fill
            foreach (var road in p.Roads)
            {
                // Walk the whole street by distance (its points are a few metres apart).
                var next = stride / 2;
                var walked = 0f;
                for (var i = 0; i + 1 < road.Line.Count; i++)
                {
                    var a = road.Line[i];
                    var b = road.Line[i + 1];
                    var len = a.DistanceTo(b);
                    if (len < 0.01f) continue;
                    var dx = (b.X - a.X) / len;
                    var dz = (b.Z - a.Z) / len;
                    for (; next < walked + len; next += stride)
                    {
                        var s = next - walked;
                        var at = new PlanPoint(a.X + dx * s, a.Z + dz * s);
                        if (district != null && at.DistanceTo(district.Center) > district.Radius * 1.1f) continue;
                        foreach (var side in new[] { 1f, -1f })
                        {
                            var off = road.Width / 2 + setback + d / 2;
                            var c = new PlanPoint(at.X - dz * off * side, at.Z + dx * off * side);
                            var yaw = FaceYaw(c, at); // the door faces the street
                            if (Fits(ctx, c, w, d, yaw, maxStep)) return (c, yaw);
                        }
                    }

                    walked += len;
                }
            }

            return null;
        }

        /// <summary>
        /// The two buildings every street is laid out around, placed before the streets so none runs through them: the hall
        /// (the tavern) facing the square from its quietest side — away from the roads, the water and the high ground — and
        /// the high ground's building (the keep, the chapel or the watchtower) on the hill's top.
        /// </summary>
        private static void Anchors(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var center = Find(p, size == 2 ? "market" : "green");
            var hill = p.Terrain.Hills[0];
            var origin = new PlanPoint(0, 0);
            var hillBearing = Bearing(origin, hill.Center);
            var reach = p.PlazaRadius + 8f;
            var bestAngle = 0f;
            var bestGap = -1f;
            for (var i = 0; i < 72; i++)
            {
                var a = (float)(i * Math.PI * 2 / 72);
                var at = new PlanPoint((float)Math.Cos(a) * reach, (float)Math.Sin(a) * reach);
                if (SettlementTerrain.InWater(p, at.X, at.Z, 10f)) continue;
                var gap = AngleGap(a, hillBearing) * 0.6f;
                foreach (var e in exits) gap = Math.Min(gap, AngleGap(a, e.angle));
                if (gap > bestGap + 1e-4f) { bestGap = gap; bestAngle = a; }
            }

            var hallAt = new PlanPoint((float)Math.Cos(bestAngle) * reach, (float)Math.Sin(bestAngle) * reach);
            var hall = Place(ctx, SettlementSites.Hall, BuildingFunction.Tavern, center.Key, hallAt, 12f, 10f, 6f, FaceYaw(hallAt, origin), true);
            hall.Roof = RoofShape.Gable;

            var landmarkLocation = KindOf(ctx, SettlementSites.Landmark);
            var landmarkFunction = landmarkLocation == LocationKind.Worship ? BuildingFunction.Religious : landmarkLocation == LocationKind.GuardPost ? BuildingFunction.Watchtower : BuildingFunction.Keep;
            var lw = landmarkFunction == BuildingFunction.Keep ? 14f : landmarkFunction == BuildingFunction.Religious ? 9f : 6f;
            var ld = landmarkFunction == BuildingFunction.Keep ? 14f : landmarkFunction == BuildingFunction.Religious ? 14f : 6f;
            var lh = landmarkFunction == BuildingFunction.Keep ? 16f : landmarkFunction == BuildingFunction.Religious ? 8f : 11f;
            Place(ctx, SettlementSites.Landmark, landmarkFunction, p.DistrictAt(hill.Center)?.Key, hill.Center, lw, ld, lh, FaceYaw(hill.Center, origin), true);
            Mill(ctx);
        }

        /// <summary>A mill on the water where the place farms grain and has a stream or river — on either bank, facing it.</summary>
        private static void Mill(Context ctx)
        {
            var p = ctx.Plan;
            if (p.Waterways.Count == 0 || p.Identity.Agriculture < 40) return;
            var w = p.Waterways[0];
            for (var i = 1; i + 1 < w.Line.Count; i++)
            {
                var at = w.Line[i];
                var r = (float)Math.Sqrt(at.X * at.X + at.Z * at.Z);
                if (r > p.CoreRadius + 30f || r < p.PlazaRadius + 30f) continue;
                var dir = Bearing(w.Line[i - 1], w.Line[i + 1]);
                var off = w.Width / 2 + SettlementTerrain.BankWidth + 4f;
                foreach (var side in new[] { 1f, -1f })
                {
                    var c = new PlanPoint(at.X - (float)Math.Sin(dir) * off * side, at.Z + (float)Math.Cos(dir) * off * side);
                    if (p.HasWalls && Math.Abs((float)Math.Sqrt(c.X * c.X + c.Z * c.Z) - 52f) < 12f) continue; // clear of the ring street
                    var yaw = FaceYaw(c, at); // the mill faces its water
                    if (!Fits(ctx, c, 9f, 8f, yaw, 2.4f)) continue;
                    Place(ctx, "mill", BuildingFunction.Mill, p.DistrictAt(c)?.Key, c, 9f, 8f, 6.5f, yaw);
                    return;
                }
            }
        }

        /// <summary>The world's own places, each in the district its function belongs to, keeping its site key.</summary>
        private static void Sites(Context ctx)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var center = Find(p, size == 2 ? "market" : "green");

            // The granary: by the square's working side.
            var craft = Find(p, "craft") ?? Find(p, "forge") ?? center;
            var g = Plot(ctx, craft, 9f, 7f, 10f, 2.5f) ?? Plot(ctx, null, 9f, 7f, 10f, 3f);
            if (g != null) Place(ctx, SettlementSites.Granary, BuildingFunction.Granary, craft.Key, g.Value.c, 9f, 7f, 5f, g.Value.yaw, true);

            // Houses the world uses (shops, smithy, shrine, homes), in order, each in its district.
            for (var i = 0; i < SettlementSites.HouseCount(ctx.Settlement.SettlementType); i++)
            {
                var key = SettlementSites.House(i);
                var kind = KindOf(ctx, key);
                DistrictData district;
                BuildingFunction f;
                switch (kind)
                {
                    case LocationKind.Shop: district = center; f = BuildingFunction.Shop; break;
                    case LocationKind.Workshop: district = craft; f = BuildingFunction.Smithy; break;
                    case LocationKind.Worship: district = Find(p, "chapel") ?? Find(p, "churchside") ?? center; f = BuildingFunction.Religious; break;
                    default: district = ResidentialFor(p, i); f = BuildingFunction.Home; break;
                }

                var w = 7f + (float)ctx.Rng.Next01() * 1.5f;
                var d = 8f + (float)ctx.Rng.Next01() * 2f;
                var spot = Plot(ctx, district, w, d, 11f, 2.2f) ?? Plot(ctx, null, w, d, 11f, 2.5f);
                if (spot == null) continue; // reported by the validator
                Place(ctx, key, f, p.DistrictAt(spot.Value.c)?.Key, spot.Value.c, w, d, 4.2f + (float)ctx.Rng.Next01() * (size == 2 ? 2.6f : 1.2f), spot.Value.yaw, true);
            }
        }

        private static DistrictData ResidentialFor(SettlementLayoutData p, int i)
        {
            var list = new List<DistrictData>();
            foreach (var d in p.Districts)
                if (d.Type == DistrictType.Residential || d.Type == DistrictType.WorkingResidential || d.Type == DistrictType.WealthyResidential || d.Type == DistrictType.Waterfront) list.Add(d);
            return list.Count == 0 ? p.Districts[0] : list[i % list.Count];
        }

        private static LocationKind? KindOf(Context ctx, string key)
        {
            foreach (var l in ctx.World.Locations) if (l.SettlementId == ctx.Settlement.SettlementId && l.LayoutKey == key) return l.Kind;
            return null;
        }

        /// <summary>The rest of the buildings: each district filled to its density with what such a district holds.</summary>
        private static void Fill(Context ctx)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            foreach (var d in p.Districts)
            {
                int target;
                float spacing;
                BuildingFunction[] mix;
                switch (d.Type)
                {
                    case DistrictType.Market: target = 14; spacing = 9f; mix = new[] { BuildingFunction.Shop, BuildingFunction.LargeHome, BuildingFunction.Shop, BuildingFunction.MarketHall }; break;
                    case DistrictType.Civic: target = 5; spacing = 12f; mix = new[] { BuildingFunction.Civic, BuildingFunction.LargeHome, BuildingFunction.GuardPost }; break;
                    case DistrictType.Craft: target = size == 2 ? 12 : 4; spacing = 12f; mix = new[] { BuildingFunction.Workshop, BuildingFunction.Warehouse, BuildingFunction.Home, BuildingFunction.Shed }; break;
                    case DistrictType.Waterfront: target = size == 2 ? 8 : 3; spacing = 12f; mix = new[] { BuildingFunction.Warehouse, BuildingFunction.Home, BuildingFunction.Workshop }; break;
                    case DistrictType.WorkingResidential: target = 22; spacing = 9.5f; mix = new[] { BuildingFunction.Home, BuildingFunction.Cottage, BuildingFunction.Home, BuildingFunction.Shed }; break;
                    case DistrictType.WealthyResidential: target = 12; spacing = 14f; mix = new[] { BuildingFunction.LargeHome, BuildingFunction.LargeHome, BuildingFunction.Stable }; break;
                    case DistrictType.Residential: target = size == 1 ? 12 : 8; spacing = 12f; mix = new[] { BuildingFunction.Home, BuildingFunction.Cottage, BuildingFunction.Shed }; break;
                    case DistrictType.Religious: target = 3; spacing = 12f; mix = new[] { BuildingFunction.Home, BuildingFunction.Cottage }; break;
                    case DistrictType.Green: target = size == 1 ? 8 : 5; spacing = 11f; mix = new[] { BuildingFunction.Cottage, BuildingFunction.Home }; break;
                    case DistrictType.Military: target = 1; spacing = 10f; mix = new[] { BuildingFunction.Shed }; break;
                    default: continue; // farms are laid out by Farms
                }

                var misses = 0;
                for (var i = 0; i < target && misses < 4; i++)
                {
                    var f = mix[i % mix.Length];
                    var (w, dd, h) = Size(ctx, f, d.Type);
                    var spot = Plot(ctx, d, w, dd, spacing, f == BuildingFunction.Shed ? 1.4f : 2.2f);
                    if (spot == null) { misses++; continue; } // a smaller building may still fit
                    misses = 0;
                    Place(ctx, ctx.Key("b"), f, d.Key, spot.Value.c, w, dd, h, spot.Value.yaw);
                }
            }

            if (p.HasWalls)
                foreach (var gate in p.Gates)
                {
                    var yaw = FaceYaw(gate, new PlanPoint(0, 0));
                    var b = new BuildingData
                    {
                        Key = ctx.Key("gatehouse"), Function = BuildingFunction.Gatehouse, DistrictKey = p.DistrictAt(gate)?.Key, Center = gate, Yaw = yaw,
                        Width = 12f, Depth = 6f, Height = 7f, Storeys = 2, FloorHeight = p.HeightAt(gate.X, gate.Z), Foundation = 0.4f, Walls = WallMaterial.Stone, Roof = RoofShape.Flat,
                    };
                    p.Buildings.Add(b);
                }
        }

        private static (float w, float d, float h) Size(Context ctx, BuildingFunction f, DistrictType district)
        {
            float R(float lo, float hi) => lo + (float)ctx.Rng.Next01() * (hi - lo);
            switch (f)
            {
                case BuildingFunction.LargeHome: return (R(8.5f, 11f), R(9f, 12f), R(6f, 8.5f));
                case BuildingFunction.Cottage: return (R(5.5f, 7f), R(6f, 7.5f), R(3.4f, 4.2f));
                case BuildingFunction.Shop: return (R(7f, 9f), R(8f, 10f), R(5.5f, 7.5f));
                case BuildingFunction.MarketHall: return (R(14f, 18f), R(9f, 11f), R(6f, 7f));
                case BuildingFunction.Civic: return (R(12f, 15f), R(10f, 12f), R(7f, 8.5f));
                case BuildingFunction.Workshop: return (R(8f, 10f), R(8f, 11f), R(4.5f, 5.5f));
                case BuildingFunction.Warehouse: return (R(10f, 14f), R(9f, 12f), R(5f, 6.5f));
                case BuildingFunction.GuardPost: return (R(6f, 7f), R(6f, 7f), R(5f, 6f));
                case BuildingFunction.Stable: return (R(9f, 12f), R(6f, 7f), R(3.8f, 4.4f));
                case BuildingFunction.Barn: return (R(12f, 16f), R(9f, 11f), R(5.5f, 7f));
                case BuildingFunction.Farmhouse: return (R(8f, 10f), R(8f, 10f), R(4.5f, 6f));
                case BuildingFunction.Shed: return (R(3.5f, 5f), R(3.5f, 5f), R(2.6f, 3.2f));
                default: return (R(6.5f, 8.5f), R(7.5f, 10f), district == DistrictType.WorkingResidential ? R(4.5f, 7f) : R(4f, 6f));
            }
        }

        // ===== Farms, pastures, orchards, gardens ===========================================================

        private static void Farms(Context ctx)
        {
            var p = ctx.Plan;
            var size = ctx.Settlement.SettlementType >= SettlementType.Town ? 2 : ctx.Settlement.SettlementType == SettlementType.Village ? 1 : 0;
            var farmsteads = Math.Max(2, Math.Min(10, (size == 2 ? 6 : size == 1 ? 6 : 5) * Math.Max(30, p.Identity.Agriculture) / 70));
            var inner = p.HasWalls ? p.CoreRadius + 24f : p.CoreRadius + 10f;
            var outer = p.HalfExtent - 26f;
            var crops = new[] { "wheat", "barley", "oats", "beans", "turnips" };
            var stock = new List<string> { "cattle", "sheep", "goat", "draft_horse" };
            var made = 0;
            for (var attempt = 0; attempt < farmsteads * 12 && made < farmsteads; attempt++)
            {
                var a = (float)(ctx.Rng.Next01() * Math.PI * 2);
                var r = inner + (float)ctx.Rng.Next01() * (outer - inner);
                var c = new PlanPoint((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
                var near = Nearest(ctx, c, out _);
                var yaw = FaceYaw(c, near); // the farmhouse faces the road its track runs to
                if (!Fits(ctx, c, 9f, 9f, yaw, 1.8f, 30f)) continue;
                var farm = ctx.Key("farm");
                // The track first: a farm whose way to the road would cut through a building is not built here.
                var track = Route(ctx, farm + "-track", RoadKind.FarmTrack, 2.6f, Local(c, yaw, 0, -7f), near, 5f);
                track.Surface = RoadSurface.Dirt;
                if (Crosses(p, track)) continue;
                var house = Place(ctx, farm, BuildingFunction.Farmhouse, "farms", c, 9f, 9f, 5f, yaw);
                house.DistrictKey = p.DistrictAt(c)?.Key;
                AddRoad(ctx, track);
                var barnAt = Local(c, yaw, 13f, 2f);
                if (Fits(ctx, barnAt, 13f, 10f, yaw, 1.8f, 0.5f)) Place(ctx, farm + "-barn", BuildingFunction.Barn, house.DistrictKey, barnAt, 13f, 10f, 6f, yaw);
                var shedAt = Local(c, yaw, -8f, 4f);
                if (Fits(ctx, shedAt, 4f, 4f, yaw, 1.4f, 0.5f)) Place(ctx, farm + "-shed", BuildingFunction.Shed, house.DistrictKey, shedAt, 4f, 4f, 2.8f, yaw);
                var yardAt = Local(c, yaw, 3f, 13f);
                if (!SettlementTerrain.InWater(p, yardAt.X, yardAt.Z, 6f)) p.Fields.Add(new FieldData { Key = farm + "-yard", Kind = FieldKind.Yard, Center = yardAt, Width = 22f, Depth = 9f, Yaw = yaw, Crop = "chickens", OwnerKey = farm, Fenced = true });

                // Its land: crops on the level, a pasture on the slope, sometimes an orchard.
                var fields = 2 + ctx.Rng.Int(2);
                for (var f = 0; f < fields + 2; f++)
                {
                    var fa = a + (float)(ctx.Rng.Next01() - 0.5) * 1.2f;
                    var fr = r + 18f + (float)ctx.Rng.Next01() * 34f;
                    var fc = new PlanPoint((float)Math.Cos(fa) * fr, (float)Math.Sin(fa) * fr);
                    var fw = 34f + (float)ctx.Rng.Next01() * 22f;
                    var fd = 22f + (float)ctx.Rng.Next01() * 16f;
                    var fyaw = (float)(90.0 - fa * 180.0 / Math.PI) + (float)ctx.Rng.Next01() * 20f;
                    var kind = f < fields ? (ctx.Rng.Next01() < 0.18 ? FieldKind.Fallow : FieldKind.Crop) : f == fields ? FieldKind.Pasture : FieldKind.Orchard;
                    if (kind == FieldKind.Orchard && ctx.Rng.Next01() < 0.4) continue;
                    var step = kind == FieldKind.Pasture ? 7f : kind == FieldKind.Orchard ? 5f : 3.2f;
                    if (!FieldFits(ctx, fc, fw, fd, fyaw, step)) continue;
                    var crop = kind == FieldKind.Pasture ? stock[(made + f) % stock.Count] : kind == FieldKind.Orchard ? "apples" : kind == FieldKind.Fallow ? "fallow" : crops[ctx.Rng.Int(crops.Length)];
                    p.Fields.Add(new FieldData { Key = ctx.Key("field"), Kind = kind, Center = fc, Width = fw, Depth = fd, Yaw = fyaw, Crop = crop, OwnerKey = farm, Fenced = kind == FieldKind.Pasture || kind == FieldKind.Orchard });
                    ctx.Footprints.Add((fc, fw, fd, fyaw));
                }

                made++;
            }
        }

        private static bool FieldFits(Context ctx, PlanPoint c, float w, float d, float yaw, float maxStep)
        {
            var p = ctx.Plan;
            if (Math.Abs(c.X) > p.HalfExtent - Math.Max(w, d) / 2 - 2 || Math.Abs(c.Z) > p.HalfExtent - Math.Max(w, d) / 2 - 2) return false;
            if (Math.Sqrt(c.X * c.X + c.Z * c.Z) < (p.HasWalls ? p.CoreRadius + 12f : p.CoreRadius) + Math.Max(w, d) / 2) return false;
            for (var i = -1; i <= 1; i++)
                for (var j = -1; j <= 1; j++)
                {
                    var pt = Local(c, yaw, i * w / 2, j * d / 2);
                    if (SettlementTerrain.InWater(p, pt.X, pt.Z, 3f)) return false;
                    foreach (var (a, b, half) in ctx.Segments)
                        if (SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, pt.X, pt.Z, out _, out _) < half + 1.5f) return false;
                }

            var radius = (float)Math.Sqrt(w * w + d * d) / 2f;
            foreach (var (oc, ow, od, _) in ctx.Footprints)
                if (c.DistanceTo(oc) < (radius + (float)Math.Sqrt(ow * ow + od * od) / 2f) * 0.72f) return false;
            var (hi, lo) = Under(p, c, w, d, yaw);
            return hi - lo <= maxStep;
        }

        private static PlanPoint Nearest(Context ctx, PlanPoint at, out RoadData road)
        {
            road = null;
            var best = float.MaxValue;
            var point = at;
            foreach (var r in ctx.Plan.Roads)
            {
                if (r.Kind == RoadKind.FarmTrack) continue;
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var d = SettlementTerrain.SegmentDistance(r.Line[i].X, r.Line[i].Z, r.Line[i + 1].X, r.Line[i + 1].Z, at.X, at.Z, out var px, out var pz);
                    if (SettlementTerrain.InWater(ctx.Plan, px, pz, 2f)) continue; // not onto a bridge
                    if (d < best) { best = d; point = new PlanPoint(px, pz); road = r; }
                }
            }

            return point;
        }

        /// <summary>Gardens behind homes in the residential districts: vegetables, herbs and a fence (about half the homes).</summary>
        private static void Gardens(Context ctx)
        {
            var p = ctx.Plan;
            foreach (var b in new List<BuildingData>(p.Buildings))
            {
                if (b.Function != BuildingFunction.Home && b.Function != BuildingFunction.Cottage && b.Function != BuildingFunction.LargeHome) continue;
                var d = Find(p, b.DistrictKey ?? string.Empty);
                if (d == null || d.Type == DistrictType.Market || ctx.Rng.Next01() < 0.45) continue;
                var c = Local(b.Center, b.Yaw, 0, b.Depth / 2 + 5f);
                var w = b.Width + 1f;
                if (!FieldFits(ctx, c, w, 8f, b.Yaw, 1.6f) && !GardenFits(ctx, c, w, 8f, b.Yaw)) continue;
                p.Fields.Add(new FieldData { Key = ctx.Key("garden"), Kind = FieldKind.Garden, Center = c, Width = w, Depth = 8f, Yaw = b.Yaw, Crop = ctx.Rng.Next01() < 0.5 ? "vegetables" : "herbs", OwnerKey = b.Key, Fenced = true });
                ctx.Footprints.Add((c, w, 8f, b.Yaw));
            }
        }

        private static bool GardenFits(Context ctx, PlanPoint c, float w, float d, float yaw)
        {
            var p = ctx.Plan;
            if (SettlementTerrain.InWater(p, c.X, c.Z, 3f) || OnWall(p, c, w / 2)) return false;
            foreach (var (a, b, half) in ctx.Segments)
                if (SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, c.X, c.Z, out _, out _) < half + d / 2 + 0.5f) return false;
            var radius = (float)Math.Sqrt(w * w + d * d) / 2f;
            foreach (var (oc, ow, od, _) in ctx.Footprints)
                if (c.DistanceTo(oc) < (radius + (float)Math.Sqrt(ow * ow + od * od) / 2f) * 0.8f && c.DistanceTo(oc) > 0.01f) return false;
            var (hi, lo) = Under(p, c, w, d, yaw);
            return hi - lo <= 1.8f;
        }

        // ===== Landmarks and features =======================================================================

        private static void Landmarks(Context ctx)
        {
            var p = ctx.Plan;
            var name = ctx.Settlement.Name;
            var landmark = p.FindBuilding(SettlementSites.Landmark);
            if (landmark != null)
            {
                var kind = landmark.Function == BuildingFunction.Keep ? "keep" : landmark.Function == BuildingFunction.Religious ? "chapel" : "watchtower";
                p.Landmarks.Add(new LandmarkData
                {
                    Key = "lm-" + kind, Kind = kind, Name = (kind == "keep" ? name + " Keep" : kind == "chapel" ? "the chapel on the rise" : "the watchtower"), FeatureKey = landmark.Key,
                    Position = landmark.Center, Why = "the settlement's " + (kind == "keep" ? "seat of government" : kind == "chapel" ? "place of worship" : "lookout") + ", on its highest ground",
                });
            }

            var mill = p.FindBuilding("mill");
            if (mill != null) p.Landmarks.Add(new LandmarkData { Key = "lm-mill", Kind = "watermill", Name = name + " Mill", FeatureKey = mill.Key, Position = mill.Center, Why = "grinds the grain its farms grow, on its " + (p.Waterways[0].IsRiver ? "river" : "stream") });

            if (p.Bridges.Count > 0)
            {
                var b = p.Bridges[0];
                foreach (var x in p.Bridges) if (x.Width > b.Width || x.Stone && !b.Stone) b = x;
                p.Landmarks.Add(new LandmarkData
                {
                    Key = "lm-bridge", Kind = "bridge", Name = (b.Stone ? "the stone bridge" : "the footbridge"), FeatureKey = b.Key,
                    Position = new PlanPoint((b.From.X + b.To.X) / 2, (b.From.Z + b.To.Z) / 2), Why = "carries the road over the " + (p.Waterways[0].IsRiver ? "river" : "stream"),
                });
            }

            if (p.HasWalls && p.Gates.Count > 0)
                p.Landmarks.Add(new LandmarkData { Key = "lm-gate", Kind = "gatehouse", Name = "the town gate", Position = p.Gates[0], Why = "where the main road passes the walls" });
            var large = ctx.Settlement.SettlementType >= SettlementType.Town;
            p.Landmarks.Add(large
                ? new LandmarkData { Key = "lm-cross", Kind = "market cross", Name = "the market cross", Position = new PlanPoint(0, 0), Why = "the heart of the market, by the well" }
                : new LandmarkData { Key = "lm-oak", Kind = "great oak", Name = "the old oak on the green", Position = new PlanPoint(p.PlazaRadius * 0.55f, -p.PlazaRadius * 0.45f), Why = "where people meet" });
        }

        private static void Features(Context ctx)
        {
            var p = ctx.Plan;
            var large = ctx.Settlement.SettlementType >= SettlementType.Town;
            p.Features.Add(new FeatureData { Kind = "well", Position = new PlanPoint(0, 0) });
            var stalls = large ? 8 : ctx.Settlement.SettlementType == SettlementType.Village ? 4 : 2;
            for (var i = 0; i < stalls; i++)
            {
                var a = (float)((45.0 + 360.0 * i / stalls) * Math.PI / 180.0);
                p.Features.Add(new FeatureData { Kind = "stall", Position = new PlanPoint((float)Math.Cos(a) * (p.PlazaRadius - 3f), (float)Math.Sin(a) * (p.PlazaRadius - 3f)), Yaw = 225f - (float)(360.0 * i / stalls) });
            }

            if (!large) p.Features.Add(new FeatureData { Kind = "great_tree", Position = new PlanPoint(p.PlazaRadius * 0.55f, -p.PlazaRadius * 0.45f) });

            // Woods: beyond the farms where the ground is free (forestry settlements keep more).
            var woods = ctx.Settlement.Economy?.Specialization == SettlementSpecialization.Forestry || ctx.Settlement.Economy?.SecondarySpecialization == SettlementSpecialization.Forestry ? 260 : 170;
            for (var i = 0; i < woods; i++)
            {
                var x = (float)(ctx.Rng.Next01() * 2 - 1) * (p.HalfExtent - 4f);
                var z = (float)(ctx.Rng.Next01() * 2 - 1) * (p.HalfExtent - 4f);
                var r = (float)Math.Sqrt(x * x + z * z);
                if (r < p.CoreRadius + 14f) continue;
                if (!TreeFree(ctx, new PlanPoint(x, z))) continue;
                p.Features.Add(new FeatureData { Kind = "tree", Position = new PlanPoint(x, z), Yaw = (float)ctx.Rng.Next01() * 360f });
            }

            // Orchards' rows and pastures' troughs are dressed by presentation from the fields themselves.
            if (large)
                foreach (var r in p.Roads)
                {
                    if (r.Kind != RoadKind.Main && r.Key != "ring") continue;
                    for (var i = 2; i < r.Line.Count; i += 3)
                    {
                        var a = r.Line[i - 1];
                        var b = r.Line[i];
                        var len = Math.Max(0.01f, a.DistanceTo(b));
                        var lamp = new PlanPoint(b.X - (b.Z - a.Z) / len * (r.Width / 2 + 0.7f), b.Z + (b.X - a.X) / len * (r.Width / 2 + 0.7f));
                        if (Math.Sqrt(lamp.X * lamp.X + lamp.Z * lamp.Z) > p.CoreRadius) continue;
                        if (!SettlementTerrain.InWater(p, lamp.X, lamp.Z, 1f)) p.Features.Add(new FeatureData { Kind = "lamp", Position = lamp });
                    }
                }
        }

        private static bool TreeFree(Context ctx, PlanPoint c)
        {
            var p = ctx.Plan;
            if (SettlementTerrain.InWater(p, c.X, c.Z, 2f)) return false;
            foreach (var (a, b, half) in ctx.Segments)
                if (SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, c.X, c.Z, out _, out _) < half + 3f) return false;
            foreach (var (oc, ow, od, _) in ctx.Footprints)
                if (c.DistanceTo(oc) < (float)Math.Sqrt(ow * ow + od * od) / 2f + 2f) return false;
            return true;
        }

        private static void Arrival(Context ctx, List<(float angle, SettlementId toward)> exits)
        {
            var p = ctx.Plan;
            RoadData road = null;
            foreach (var r in p.Roads) if (r.Kind == RoadKind.Main) { road = r; break; }
            var end = road.Line[road.Line.Count - 1];
            var inner = road.Line[Math.Max(0, road.Line.Count - 3)];
            p.Arrival = new PlanPoint(end.X + (inner.X - end.X) * 0.3f, end.Z + (inner.Z - end.Z) * 0.3f);
            var toward = Math.Atan2(inner.Z - end.Z, inner.X - end.X);
            p.ArrivalYaw = (float)(90.0 - toward * 180.0 / Math.PI);
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>The generator's own stream (never a simulation stream).</summary>
        private sealed class Rng
        {
            private ulong _state;

            public Rng(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

            public ulong NextULong()
            {
                _state += 0x9E3779B97F4A7C15UL;
                return SeedMixer.Mix(_state);
            }

            public double Next01() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

            public int Int(int n) => n <= 1 ? 0 : (int)(NextULong() % (ulong)n);
        }
    }
}
