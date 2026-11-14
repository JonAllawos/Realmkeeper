using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application.Overland
{
    /// <summary>Phase 23 (ADR-0110): what one region holds, measured on its walkable country.</summary>
    public sealed class RegionScale
    {
        public string Name { get; set; }
        public string Realm { get; set; }
        public RegionKind Kind { get; set; }
        public float SizeKm { get; set; }
        public float AreaKm2 { get; set; }
        public float WalkablePercent { get; set; }
        public float RoadKm { get; set; }
        public int Settlements { get; set; }
        public int Places { get; set; }
        public int Gateways { get; set; }
        public int Exits { get; set; }
        public int Crossings { get; set; }
        public Dictionary<Biome, int> Biomes { get; } = new Dictionary<Biome, int>();

        /// <summary>Mean straight-line distance (km) from a settlement to its nearest other destination in the region.</summary>
        public float MeanNearestKm { get; set; }

        /// <summary>Game hours to walk the region's width on open grassland at the travel rules.</summary>
        public float CrossingHours { get; set; }

        public int Destinations => Settlements + Places + Gateways;
    }

    /// <summary>
    /// Phase 23 (ADR-0110): measures a generated world's walkable scale, region by region, for the scale targets and their
    /// tests: playable area, walkable share, biome mix, road length, crossings (bridges, fords, passes), destinations and how
    /// far apart they lie, and walking time. Engine-free; reads the session's own geography.
    /// </summary>
    public static class WorldScaleSurvey
    {
        public static List<RegionScale> Measure(WorldSession session)
        {
            var list = new List<RegionScale>();
            var world = session.CurrentWorldSnapshotForTests();
            foreach (var r in world.Regions) list.Add(Measure(world, RealmIds.Surface, r.RegionId, r.Name, r.Kind, session.GetOverlandTerrain(RealmIds.Surface, r.RegionId)));
            foreach (var realm in world.Realms)
                foreach (var r in realm.Regions)
                    list.Add(Measure(world, realm.RealmId, r.RegionId, r.Name, r.Kind, session.GetOverlandTerrain(realm.RealmId, r.RegionId)));
            return list;
        }

        public static RegionScale Measure(WorldData world, string realmId, Realmkeeper.Domain.Identifiers.RegionId region, string name, RegionKind kind, OverlandTerrain t)
        {
            var map = t.Map;
            var s = new RegionScale { Name = name, Realm = realmId, Kind = kind, SizeKm = t.SizeX / 1000f, AreaKm2 = t.SizeX * t.SizeZ / 1e6f };
            var walkable = 0;
            var road = 0;
            for (var z = 0; z < map.Depth; z++)
                for (var x = 0; x < map.Width; x++)
                {
                    var c = t.Cell(x, z);
                    if (c.Passable) walkable++;
                    if (c.Road) road++;
                    if (c.Crossing) s.Crossings++;
                    s.Biomes[c.Biome] = (s.Biomes.TryGetValue(c.Biome, out var n) ? n : 0) + 1;
                }

            s.WalkablePercent = 100f * walkable / (map.Width * map.Depth);
            s.RoadKm = road * OverlandScale.MetresPerCell / 1000f;
            var sites = OverlandQueries.Sites(world, realmId, region, t);
            var points = new List<(float x, float z, OverlandSiteKind k)>();
            foreach (var site in sites)
            {
                switch (site.Kind)
                {
                    case OverlandSiteKind.Settlement: s.Settlements++; break;
                    case OverlandSiteKind.Place: s.Places++; break;
                    case OverlandSiteKind.Gateway: s.Gateways++; break;
                    case OverlandSiteKind.RegionExit: s.Exits++; break;
                }

                if (site.Kind != OverlandSiteKind.RegionExit) points.Add((site.X, site.Z, site.Kind));
            }

            double sum = 0;
            var counted = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                if (a.k != OverlandSiteKind.Settlement) continue;
                var best = double.MaxValue;
                for (var j = 0; j < points.Count; j++)
                {
                    var b = points[j];
                    if (i == j) continue;
                    best = Math.Min(best, Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)));
                }

                if (best < double.MaxValue) { sum += best; counted++; }
            }

            s.MeanNearestKm = counted == 0 ? 0f : (float)(sum / counted / 1000.0);
            s.CrossingHours = (float)(t.SizeX * OverlandScale.TravelKmPerMetre / TravelRules.FootKmPerHour * 1.25);
            return s;
        }

        /// <summary>A plain-text table of the survey (for logs and reports).</summary>
        public static string Describe(IEnumerable<RegionScale> regions)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            float area = 0, roads = 0;
            int settlements = 0, places = 0, gateways = 0;
            sb.AppendLine("Region | realm | kind | size km | area km² | walkable % | road km | crossings | settlements | places | gateways | exits | mean nearest km | hours across");
            foreach (var r in regions)
            {
                sb.AppendLine(string.Format(ci, "{0} | {1} | {2} | {3:0.00} | {4:0.0} | {5:0} | {6:0.0} | {7} | {8} | {9} | {10} | {11} | {12:0.00} | {13:0.0}",
                    r.Name, r.Realm, r.Kind, r.SizeKm, r.AreaKm2, r.WalkablePercent, r.RoadKm, r.Crossings, r.Settlements, r.Places, r.Gateways, r.Exits, r.MeanNearestKm, r.CrossingHours));
                area += r.AreaKm2;
                roads += r.RoadKm;
                settlements += r.Settlements;
                places += r.Places;
                gateways += r.Gateways;
            }

            sb.AppendLine(string.Format(ci, "Total: {0:0.0} km² walkable country, {1:0.0} km of road, {2} settlements, {3} places, {4} gateways.", area, roads, settlements, places, gateways));
            return sb.ToString();
        }
    }
}
