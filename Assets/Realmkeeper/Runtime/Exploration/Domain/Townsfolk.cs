using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    /// <summary>Phase 27: what an unnamed townsperson is doing in the street.</summary>
    public enum TownsfolkActivity
    {
        Errand = 0,
        Shopping = 1,
        Carrying = 2,
        Working = 3,
        Gathering = 4,
        Travelling = 5,
        Patrolling = 6,
        Guarding = 7,
        Farming = 8,
    }

    /// <summary>The settlement figures the street population is drawn from (all authoritative, read from the world).</summary>
    public sealed class TownsfolkInput
    {
        public int Population { get; set; }
        public SettlementType Type { get; set; }
        public int Prosperity { get; set; } = 50;

        /// <summary>The settlement's workers by trade (its production assignments' worker capacity).</summary>
        public IReadOnlyList<(ProductionSourceType source, int workers)> Labour { get; set; } = Array.Empty<(ProductionSourceType, int)>();

        /// <summary>Named residents already out in the street this hour (they are part of the same population).</summary>
        public int NamedOut { get; set; }

        public int MinuteOfDay { get; set; }
        public ulong Seed { get; set; }
    }

    /// <summary>One unnamed townsperson out this hour: who they look like, what they do, and their way through the streets.</summary>
    public sealed class TownsfolkSpec
    {
        /// <summary>Stable within the settlement and day ("tf-12"): the same person keeps the same looks.</summary>
        public string Key { get; set; }

        /// <summary>The trade their clothes show (the appearance rules' role).</summary>
        public NpcRole Look { get; set; }

        public TownsfolkActivity Activity { get; set; }

        /// <summary>Their way along the roads, there and back (one point for someone who stays put).</summary>
        public List<LocalPoint> Route { get; set; } = new List<LocalPoint>();

        /// <summary>Metres a second while walking.</summary>
        public float Speed { get; set; }

        /// <summary>Seconds they pause at each end.</summary>
        public float Pause { get; set; }

        /// <summary>Where in the cycle they start (seconds), so people are spread along their ways.</summary>
        public float Phase { get; set; }

        /// <summary>How far they idle about a fixed spot (for those who stay put).</summary>
        public float Wander { get; set; }

        /// <summary>The district or place they belong to (for the busy-versus-quiet measure).</summary>
        public string Where { get; set; }
    }

    /// <summary>
    /// Phase 27 (ADR-0119): the settlement's unnamed population in the street. Named residents are a tracked subset of
    /// <c>SettlementData.Population</c> (ADR-0009); the rest are counted but have no records. This plans who of them is
    /// out, as a share of the authoritative population by the hour, and what they do, from the settlement's own labour.
    /// Nothing is stored: the same settlement, day and hour always give the same people. The presentation shows the nearest
    /// as full figures; the rest stay abstract (positions only).
    /// </summary>
    public static class TownsfolkPlanner
    {
        /// <summary>
        /// People out in the streets per thousand inhabitants by hour of day. Peak midday about 6–7 %, near nobody at night.
        /// Most of a medieval town's people are indoors, in fields or at work at any time; this is a design figure.
        /// </summary>
        private static readonly int[] PerMilleByHour =
        {
            3, 2, 2, 2, 3, 8, 20, 38, 50, 58, 62, 64, 68, 64, 60, 58, 54, 48, 38, 28, 18, 10, 6, 4,
        };

        /// <summary>Hard ceiling on planned people, whatever the population (the largest city stays plannable and walkable).</summary>
        public const int MaxPlanned = 160;

        public static int PerMille(int minuteOfDay) => PerMilleByHour[((minuteOfDay / 60) % 24 + 24) % 24];

        /// <summary>How many unnamed townsfolk are out now (after the named who are already out).</summary>
        public static int OnStreets(TownsfolkInput input)
        {
            if (input == null || input.Population <= 0) return 0;
            var raw = (int)Math.Round(input.Population * PerMille(input.MinuteOfDay) / 1000.0);
            return Math.Max(0, Math.Min(MaxPlanned, raw - input.NamedOut));
        }

        /// <summary>The town watch on duty: about one in two hundred and fifty inhabitants, at least one where there is a town.</summary>
        public static int Watch(TownsfolkInput input)
        {
            if (input == null || input.Population < 120) return 0;
            var night = input.MinuteOfDay < 6 * 60 || input.MinuteOfDay >= 21 * 60;
            var n = Math.Max(1, Math.Min(10, input.Population / 250));
            return night ? Math.Max(1, n / 2) : n;
        }

        public static List<TownsfolkSpec> Plan(LocalEnvironmentLayout layout, TownsfolkInput input)
        {
            var specs = new List<TownsfolkSpec>();
            if (layout == null || input == null) return specs;
            var rng = new Random((int)((input.Seed ^ (ulong)(input.MinuteOfDay / 60) * 0x9E3779B97F4A7C15UL) & 0x7FFFFFFF));
            var spots = Spots.Of(layout);
            if (spots.Market.Count == 0) return specs;

            // The mix of people out, from the settlement's labour (workers by trade), and the hour.
            var farming = 0; var trade = 0; var heavy = 0; var total = 0;
            foreach (var (source, workers) in input.Labour)
            {
                total += Math.Max(0, workers);
                if (source == ProductionSourceType.Farming || source == ProductionSourceType.Fishing || source == ProductionSourceType.Hunting) farming += workers;
                else if (source == ProductionSourceType.Trade) trade += workers;
                else heavy += workers;
            }

            double Share(int part) => total > 0 ? part / (double)total : 0.0;
            var hour = input.MinuteOfDay / 60;
            var working = hour >= 7 && hour < 18;
            var count = OnStreets(input);
            var made = 0;
            for (var tries = 0; made < count && tries < count * 3; tries++) // a person whose way is not along the streets is planned again
            {
                var roll = rng.NextDouble();
                TownsfolkSpec s;
                // Shares: people about on errands and at the market are most of any street; then workers by the town's labour.
                if (roll < 0.34) s = Walk(layout, rng, spots, "tf", NpcRole.Resident, TownsfolkActivity.Shopping, spots.Homes, spots.Market, 1.15f);
                else if (roll < 0.46) s = Gathering(rng, spots, "tf");
                else if (roll < 0.46 + 0.22 * Math.Max(0.3, Share(trade) + 0.2) && working) s = Walk(layout, rng, spots, "tf", NpcRole.Labourer, TownsfolkActivity.Carrying, spots.Stores, spots.Market, 1.0f);
                else if (roll < 0.74 && working && spots.Workshops.Count > 0) s = Working(rng, spots, "tf", Share(heavy) > 0.2 ? NpcRole.Smith : NpcRole.Labourer);
                else if (roll < 0.74 + 0.16 * Math.Max(0.2, Share(farming)) && working && spots.Gates.Count > 0) s = Walk(layout, rng, spots, "tf", NpcRole.Farmer, TownsfolkActivity.Farming, spots.Gates, spots.Market, 1.1f);
                else if (roll < 0.9 && spots.Gates.Count > 0 && input.Type >= SettlementType.Village) s = Walk(layout, rng, spots, "tf", NpcRole.Resident, TownsfolkActivity.Travelling, spots.Gates, spots.Market, 1.3f);
                else s = Walk(layout, rng, spots, "tf", rng.Next(6) == 0 ? NpcRole.Elder : NpcRole.Resident, TownsfolkActivity.Errand, spots.Homes, spots.Homes, 1.1f);
                if (s == null) continue;
                s.Key = "tf-" + made++;
                specs.Add(s);
            }

            // The watch: patrols between the gates and the square, and a post at each gate by day.
            var watch = Watch(input);
            for (var g = 0; g < watch; g++)
            {
                if (g < spots.Gates.Count && hour >= 6 && hour < 21)
                {
                    var post = spots.Gates[g];
                    specs.Add(new TownsfolkSpec { Key = "watch-post-" + g, Look = NpcRole.Guard, Activity = TownsfolkActivity.Guarding, Route = { post }, Wander = 1.2f, Where = "gate" });
                    continue;
                }

                var from = spots.Gates.Count > 0 ? spots.Gates[g % spots.Gates.Count] : spots.Market[g % spots.Market.Count];
                var to = spots.Civic.Count > 0 ? spots.Civic[g % spots.Civic.Count] : spots.Market[(g * 3) % spots.Market.Count];
                var route = LocalPaths.Route(layout, from, null, to, null);
                if (route.Count < 2 || !AlongStreets(layout, route)) continue;
                specs.Add(new TownsfolkSpec { Key = "watch-" + g, Look = NpcRole.Guard, Activity = TownsfolkActivity.Patrolling, Route = route, Speed = 1.05f, Pause = 6f, Phase = (float)rng.NextDouble() * 200f, Where = "patrol" });
            }

            return specs;
        }

        private static TownsfolkSpec Walk(LocalEnvironmentLayout layout, Random rng, Spots spots, string key, NpcRole look, TownsfolkActivity activity, List<LocalPoint> fromSet, List<LocalPoint> toSet, float speed)
        {
            if (fromSet.Count == 0 || toSet.Count == 0) return null;
            var from = fromSet[rng.Next(fromSet.Count)];
            var to = toSet[rng.Next(toSet.Count)];
            if (Dist(from, to) < 6f) to = spots.Market[rng.Next(spots.Market.Count)];
            var route = LocalPaths.Route(layout, Jitter(from, rng, 1.2f), null, Jitter(to, rng, 2.2f), null);
            if (!AlongStreets(layout, route)) return null; // the road search found no way and drew a straight line: not a walk anyone takes
            if (route.Count < 2) return null;
            // People walk a little off the centre line, each to their own side.
            var side = (float)(rng.NextDouble() - 0.5) * 2.2f;
            var offset = new List<LocalPoint>(route.Count);
            for (var i = 0; i < route.Count; i++)
            {
                var a = route[Math.Max(0, i - 1)];
                var b = route[Math.Min(route.Count - 1, i + 1)];
                var dx = b.X - a.X;
                var dz = b.Z - a.Z;
                var len = (float)Math.Sqrt(dx * dx + dz * dz);
                offset.Add(len < 1e-3f ? route[i] : new LocalPoint(route[i].X - dz / len * side, route[i].Z + dx / len * side));
            }

            return new TownsfolkSpec
            {
                Key = key, Look = look, Activity = activity, Route = offset, Speed = speed * (0.85f + (float)rng.NextDouble() * 0.3f),
                Pause = 4f + (float)rng.NextDouble() * 10f, Phase = (float)rng.NextDouble() * 600f, Where = activity == TownsfolkActivity.Shopping ? "market" : "street",
            };
        }

        private static TownsfolkSpec Gathering(Random rng, Spots spots, string key)
        {
            var at = spots.Market[rng.Next(spots.Market.Count)];
            return new TownsfolkSpec { Key = key, Look = rng.Next(4) == 0 ? NpcRole.Elder : NpcRole.Resident, Activity = TownsfolkActivity.Gathering, Route = { Jitter(at, rng, 2.5f) }, Wander = 0.8f, Where = "market" };
        }

        private static TownsfolkSpec Working(Random rng, Spots spots, string key, NpcRole look)
        {
            var at = spots.Workshops[rng.Next(spots.Workshops.Count)];
            return new TownsfolkSpec { Key = key, Look = look, Activity = TownsfolkActivity.Working, Route = { Jitter(at, rng, 1.0f) }, Wander = 1.5f, Where = "craft" };
        }

        /// <summary>
        /// Where someone on a route is after <paramref name="seconds"/>: there and back along the route, pausing at each end.
        /// A pure function of time, so everyone can be placed without being simulated frame by frame.
        /// </summary>
        public static LocalPoint PositionAt(TownsfolkSpec s, double seconds, out float headingDegrees, out bool walking)
        {
            headingDegrees = 0f;
            walking = false;
            if (s.Route.Count == 1 || s.Speed <= 0f) return s.Route[0];
            var length = 0f;
            for (var i = 0; i + 1 < s.Route.Count; i++) length += Dist(s.Route[i], s.Route[i + 1]);
            if (length < 0.01f) return s.Route[0];
            var walk = length / s.Speed;
            var cycle = 2 * (walk + s.Pause);
            var t = (seconds + s.Phase) % cycle;
            if (t < 0) t += cycle;
            float along;
            var back = false;
            if (t < s.Pause) along = 0f;
            else if (t < s.Pause + walk) { along = (float)(t - s.Pause) * s.Speed; walking = true; }
            else if (t < 2 * s.Pause + walk) along = length;
            else { along = length - (float)(t - 2 * s.Pause - walk) * s.Speed; walking = true; back = true; }

            for (var i = 0; i + 1 < s.Route.Count; i++)
            {
                var a = s.Route[i];
                var b = s.Route[i + 1];
                var seg = Dist(a, b);
                if (along <= seg || i + 2 == s.Route.Count)
                {
                    var k = seg > 0 ? Math.Min(1f, along / seg) : 0f;
                    var dx = b.X - a.X;
                    var dz = b.Z - a.Z;
                    if (back) { dx = -dx; dz = -dz; }
                    headingDegrees = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                    return new LocalPoint(a.X + (b.X - a.X) * k, a.Z + (b.Z - a.Z) * k);
                }

                along -= seg;
            }

            return s.Route[s.Route.Count - 1];
        }

        /// <summary>Every leg but the first and last (door or gate to the street) lies along a road or across the square.</summary>
        public static bool AlongStreets(LocalEnvironmentLayout layout, List<LocalPoint> route)
        {
            for (var i = 1; i + 2 < route.Count; i++)
            {
                var a = route[i];
                var b = route[i + 1];
                var len = Dist(a, b);
                for (var k = 0.25f; k < 1f; k += 0.25f)
                {
                    var p = new LocalPoint(a.X + (b.X - a.X) * k, a.Z + (b.Z - a.Z) * k);
                    if (Math.Sqrt(p.X * p.X + p.Z * p.Z) < layout.PlazaRadius + 1f) continue;
                    var near = false;
                    foreach (var s in layout.Streets)
                        if (SettlementTerrain.SegmentDistance(s.From.X, s.From.Z, s.To.X, s.To.Z, p.X, p.Z, out _, out _) < s.Width / 2f + 1.5f) { near = true; break; }
                    if (!near && len > 4f) return false;
                }
            }

            return true;
        }

        private static LocalPoint Jitter(LocalPoint p, Random rng, float r) => new LocalPoint(p.X + (float)(rng.NextDouble() * 2 - 1) * r, p.Z + (float)(rng.NextDouble() * 2 - 1) * r);

        private static float Dist(LocalPoint a, LocalPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        /// <summary>The places people come and go between, from the layout (doors, the square, the gates).</summary>
        private sealed class Spots
        {
            public readonly List<LocalPoint> Market = new List<LocalPoint>();
            public readonly List<LocalPoint> Homes = new List<LocalPoint>();
            public readonly List<LocalPoint> Stores = new List<LocalPoint>();
            public readonly List<LocalPoint> Workshops = new List<LocalPoint>();
            public readonly List<LocalPoint> Gates = new List<LocalPoint>();
            public readonly List<LocalPoint> Civic = new List<LocalPoint>();

            public static Spots Of(LocalEnvironmentLayout layout)
            {
                var s = new Spots();
                // The square's ring, and the doors of shops (the market is where people go).
                for (var k = 0; k < 10; k++)
                {
                    var a = k * Math.PI * 2 / 10;
                    var r = Math.Max(3f, layout.PlazaRadius * 0.7f);
                    s.Market.Add(new LocalPoint((float)Math.Cos(a) * r, (float)Math.Sin(a) * r));
                }

                foreach (var b in layout.Buildings)
                {
                    var door = LocalPaths.DoorOutside(b);
                    if (b.Fit != ShopFit.None) { s.Market.Add(door); if (b.Fit == ShopFit.Smithy || b.Fit == ShopFit.Carpentry || b.Fit == ShopFit.Leatherworks) s.Workshops.Add(door); continue; }
                    switch (b.Function)
                    {
                        case BuildingFunction.Home: case BuildingFunction.Cottage: case BuildingFunction.LargeHome: s.Homes.Add(door); break;
                        case BuildingFunction.Warehouse: case BuildingFunction.Granary: s.Stores.Add(door); break; // town stores (a farm's barn is out in its fields)
                        case BuildingFunction.Workshop: case BuildingFunction.Smithy: case BuildingFunction.Mill: s.Workshops.Add(door); break;
                        case BuildingFunction.Civic: case BuildingFunction.Religious: case BuildingFunction.Keep: case BuildingFunction.GuardPost: s.Civic.Add(door); break;
                        case BuildingFunction.Tavern: case BuildingFunction.MarketHall: s.Market.Add(door); break;
                    }
                }

                if (layout.Plan != null)
                    foreach (var g in layout.Plan.Gates)
                    {
                        // Just inside the gate.
                        var len = Math.Max(1f, (float)Math.Sqrt(g.X * g.X + g.Z * g.Z));
                        s.Gates.Add(new LocalPoint(g.X - g.X / len * 6f, g.Z - g.Z / len * 6f));
                    }

                if (s.Gates.Count == 0) s.Gates.Add(LocalPaths.LeavingPoint(layout));
                if (s.Stores.Count == 0) s.Stores.AddRange(s.Market);
                return s;
            }
        }
    }
}
