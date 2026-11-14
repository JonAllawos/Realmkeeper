using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application.Overland
{
    /// <summary>What an overland site is.</summary>
    public enum OverlandSiteKind
    {
        Settlement = 0,
        Place = 1,
        Gateway = 2,

        /// <summary>The road on into a neighbouring region (a loading transition).</summary>
        RegionExit = 3,
    }

    /// <summary>A destination in the country: where it stands, what it leads to, and whether the player knows it.</summary>
    public sealed class OverlandSite
    {
        public OverlandSiteKind Kind { get; set; }

        /// <summary>The settlement, place or gateway id, or the neighbouring region's id for an exit.</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public MapCoord Cell { get; set; }

        /// <summary>Where it stands, in region metres (the entrance or way on).</summary>
        public float X { get; set; }

        public float Z { get; set; }

        /// <summary>Shown on maps and the compass (discovered, or a settlement of a known region).</summary>
        public bool Known { get; set; }

        /// <summary>Whether it can be used now (an open gateway, a passable crossing), and why not.</summary>
        public bool Usable { get; set; } = true;

        public string Why { get; set; }

        /// <summary>A short kind word for the HUD ("town", "cave", "gateway", "road on").</summary>
        public string KindText { get; set; }
    }

    /// <summary>Phase 23 (ADR-0110): the player's country as the overland view needs it.</summary>
    public sealed class OverlandView
    {
        public string RealmId { get; set; }
        public string RealmName { get; set; }
        public string Profile { get; set; }
        public RegionId RegionId { get; set; }
        public string RegionName { get; set; }
        public RegionKind RegionKind { get; set; }
        public OverlandTerrain Terrain { get; set; }
        public LocalPose Pose { get; set; }
        public List<OverlandSite> Sites { get; } = new List<OverlandSite>();

        /// <summary>The roads of the region, as map cells (for the ground's road surface).</summary>
        public IReadOnlyList<RoadPath> Roads => Terrain?.Map.Roads ?? Array.Empty<RoadPath>();
    }

    /// <summary>Phase 23 (ADR-0110, ADR-0111): the country's terrain, sites and exits, from the world and its geography.</summary>
    public static class OverlandQueries
    {
        /// <summary>Distance at which an unknown place or gateway is noticed by someone walking the country.</summary>
        public const float NoticeMetres = 140f;

        /// <summary>Distance within which a site can be used (entered, passed).</summary>
        public const float ReachMetres = 30f;

        public static OverlandSite Site(OverlandView view, OverlandSiteKind kind, string id) => view?.Sites.Find(s => s.Kind == kind && s.Id == id);

        /// <summary>Every site of a region of a realm.</summary>
        public static List<OverlandSite> Sites(WorldData world, string realmId, RegionId region, OverlandTerrain terrain)
        {
            var list = new List<OverlandSite>();
            if (RealmIds.IsSurface(realmId))
            {
                var r = world.FindRegion(region);
                var regionKnown = r != null && r.Discovery != RegionDiscovery.Unknown;
                foreach (var s in world.Settlements)
                {
                    if (s.RegionId != region) continue;
                    var at = Gate(terrain, s.Position);
                    list.Add(new OverlandSite
                    {
                        Kind = OverlandSiteKind.Settlement, Id = s.SettlementId.ToString(), Name = s.Name, Cell = s.Position, X = at.x, Z = at.z, Known = regionKnown,
                        KindText = s.SettlementType.ToString().ToLowerInvariant(),
                    });
                }

                foreach (var p in world.Places)
                {
                    if (p.RegionId != region || p.ParentKind != PlaceParentKind.Region || p.Category == PlaceCategory.Interior) continue;
                    var at = Approach(terrain, p.Cell, 2);
                    list.Add(new OverlandSite
                    {
                        Kind = OverlandSiteKind.Place, Id = p.PlaceId, Name = p.Discovery >= PlaceDiscovery.Discovered ? p.Name : "Something off the road", Cell = p.Cell, X = at.x, Z = at.z,
                        Known = p.Discovery >= PlaceDiscovery.Discovered, KindText = p.Category.ToString().ToLowerInvariant(),
                    });
                }

                foreach (var exit in Exits(world, region, terrain)) list.Add(exit);
            }

            foreach (var g in world.Gateways)
            {
                if (!g.TryEndIn(RealmIds.IsSurface(realmId) ? RealmIds.Surface : realmId, region, out var end, out _)) continue;
                var at = Approach(terrain, end.Cell, 2);
                list.Add(new OverlandSite
                {
                    Kind = OverlandSiteKind.Gateway, Id = g.GatewayId, Name = g.Name, Cell = end.Cell, X = at.x, Z = at.z, Known = g.Discovered || !RealmIds.IsSurface(realmId),
                    Usable = g.State == GatewayState.Open, Why = g.State == GatewayState.Open ? null : g.State == GatewayState.Sealed ? "It is sealed." : "It is dormant: nothing answers.", KindText = "gateway",
                });
            }

            return list;
        }

        /// <summary>
        /// The ways on into neighbouring regions: one per region joined to this one by a crossing route. It stands on this
        /// region's edge toward the neighbour, at the walkable edge cell nearest the crossing's settlement here.
        /// </summary>
        public static List<OverlandSite> Exits(WorldData world, RegionId region, OverlandTerrain terrain)
        {
            var list = new List<OverlandSite>();
            var here = world.FindRegion(region);
            if (here == null || terrain == null) return list;
            var seen = new HashSet<RegionId>();
            foreach (var route in world.Routes)
            {
                if (!route.IsCrossing) continue;
                RegionId other;
                SettlementId mine;
                if (route.RegionA == region) { other = route.RegionB; mine = route.A; }
                else if (route.RegionB == region) { other = route.RegionA; mine = route.B; }
                else continue;
                if (!seen.Add(other)) continue;
                var there = world.FindRegion(other);
                if (there == null) continue;
                var cell = EdgeCell(terrain, world.FindSettlement(mine)?.Position ?? new MapCoord(terrain.Map.Width / 2, terrain.Map.Depth / 2), there.AtlasX - here.AtlasX, there.AtlasZ - here.AtlasZ);
                var (x, z) = OverlandScale.CellCentre(cell);
                list.Add(new OverlandSite
                {
                    Kind = OverlandSiteKind.RegionExit, Id = other.ToString(), Name = "The way on to " + there.Name, Cell = cell, X = x, Z = z, Known = true,
                    Usable = route.Passable, Why = route.Passable ? null : "The crossing is closed" + (string.IsNullOrEmpty(route.BlockedBy) ? "." : ": " + route.BlockedBy + "."), KindText = route.Kind.ToString().ToLowerInvariant(),
                });
            }

            return list;
        }

        /// <summary>The walkable cell on the edge facing (dx, dz) nearest to <paramref name="toward"/>; the edge midpoint when none is walkable.</summary>
        public static MapCoord EdgeCell(OverlandTerrain terrain, MapCoord toward, int dx, int dz)
        {
            var map = terrain.Map;
            var best = new MapCoord(dx > 0 ? map.Width - 1 : dx < 0 ? 0 : map.Width / 2, dz > 0 ? map.Depth - 1 : dz < 0 ? 0 : map.Depth / 2);
            var bestD = int.MaxValue;
            for (var i = 0; i < Math.Max(map.Width, map.Depth); i++)
            {
                MapCoord c;
                if (dx != 0) c = new MapCoord(dx > 0 ? map.Width - 2 : 1, Math.Min(map.Depth - 1, i));
                else c = new MapCoord(Math.Min(map.Width - 1, i), dz > 0 ? map.Depth - 2 : 1);
                if (!map.InBounds(c) || !terrain.Cell(c.X, c.Z).Passable) continue;
                var d = Math.Abs(c.X - toward.X) + Math.Abs(c.Z - toward.Z);
                if (d < bestD) { bestD = d; best = c; }
            }

            return best;
        }

        /// <summary>Cells from a settlement's centre to its way in from the country (240 m: outside the largest town's walls).</summary>
        public const int GateCells = 6;

        /// <summary>
        /// A settlement's way in from the country: a road cell <see cref="GateCells"/> from its centre (where a road leaves the
        /// town), else walkable ground at that distance, else the nearest walkable cell. Deterministic (scan order breaks ties).
        /// </summary>
        public static (float x, float z) Gate(OverlandTerrain terrain, MapCoord centre)
        {
            if (terrain == null) return OverlandScale.CellCentre(centre);
            MapCoord? road = null, ground = null;
            for (var r = GateCells; r <= GateCells + 2 && road == null; r++)
                for (var dz = -r; dz <= r; dz++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        var c = new MapCoord(centre.X + dx, centre.Z + dz);
                        if (!terrain.Map.InBounds(c)) continue;
                        var o = terrain.Cell(c.X, c.Z);
                        if (!o.Passable) continue;
                        if (o.Road && road == null) road = c;
                        if (ground == null) ground = c;
                    }

            if (road != null) return OverlandScale.CellCentre(road.Value);
            return ground != null ? OverlandScale.CellCentre(ground.Value) : Approach(terrain, centre, 3);
        }

        /// <summary>A standing point beside a site: the centre of the nearest walkable cell within <paramref name="radius"/>, preferring roads.</summary>
        public static (float x, float z) Approach(OverlandTerrain terrain, MapCoord cell, int radius)
        {
            if (terrain == null) return OverlandScale.CellCentre(cell);
            MapCoord? best = null;
            var bestScore = float.MaxValue;
            for (var dz = -radius; dz <= radius; dz++)
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var c = new MapCoord(cell.X + dx, cell.Z + dz);
                    if (!terrain.Map.InBounds(c)) continue;
                    var o = terrain.Cell(c.X, c.Z);
                    if (!o.Passable) continue;
                    var score = Math.Abs(dx) + Math.Abs(dz) - (o.Road ? 0.5f : 0f);
                    if (score < bestScore) { bestScore = score; best = c; }
                }

            return OverlandScale.CellCentre(best ?? cell);
        }
    }
}
