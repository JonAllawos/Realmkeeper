using System;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Phase 23 (ADR-0111): the campaign world's realms, made once, deterministically from the world's seed:
    /// - the surface realm (the atlas's regions);
    /// - the ancient realm (Campaign Bible §5) with one prototype region: geography only, from its own seed, with a provisional
    ///   environment profile and no inhabitants (they are undecided);
    /// - one prototype gateway between the home region and that region, dormant (the campaign opens it).
    /// Idempotent: a world that has them is left alone, and nothing the player changed is regenerated.
    /// </summary>
    public static class RealmGenesis
    {
        public const string PrototypeGatewayId = "gateway:prototype-1";

        public static bool Ensure(WorldData world, RegionMap homeMap, RegionGenerator generator)
        {
            if (world == null || world.Regions.Count == 0 || homeMap == null) return false;
            var changed = false;
            if (world.FindRealm(RealmIds.Surface) == null)
            {
                world.Realms.Add(new RealmData { RealmId = RealmIds.Surface, Name = "The known world", Kind = RealmKind.Surface, Profile = RealmProfiles.Surface, Seed = world.Seed, Visited = true });
                changed = true;
            }

            var ancient = world.FindRealm(RealmIds.Ancient);
            if (ancient == null)
            {
                var seed = unchecked((long)SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "realm:ancient"));
                ancient = new RealmData
                {
                    RealmId = RealmIds.Ancient, Name = "The ancient world (working name)", Kind = RealmKind.Ancient, Profile = RealmProfiles.AncientPrototype, Seed = seed,
                };
                var regionSeed = unchecked((long)SeedMixer.Combine(unchecked((ulong)seed), "region:0"));
                var kinds = new[] { RegionKind.Highlands, RegionKind.Woodland, RegionKind.Lowlands };
                ancient.Regions.Add(new RealmRegionData
                {
                    RegionId = new RegionId(GuidFrom(regionSeed)), Name = "Prototype region (unnamed)", Seed = regionSeed,
                    Kind = kinds[(int)((regionSeed & 0x7fffffff) % kinds.Length)], MountainDirection = (int)((regionSeed >> 8 & 0x7fffffff) % 8),
                });
                world.Realms.Add(ancient);
                changed = true;
            }

            if (world.FindGateway(PrototypeGatewayId) == null && ancient.Regions.Count > 0)
            {
                var home = world.Regions[0];
                var far = ancient.Regions[0];
                var farMap = generator.GenerateGeography(far.Seed, far.MountainDirection, far.RegionId);
                world.Gateways.Add(new GatewayData
                {
                    GatewayId = PrototypeGatewayId, Name = "An ancient gateway (prototype)", State = GatewayState.Dormant,
                    A = new GatewayEnd { RealmId = RealmIds.Surface, RegionId = home.RegionId, Cell = SurfaceSite(world, home.RegionId, homeMap) },
                    B = new GatewayEnd { RealmId = RealmIds.Ancient, RegionId = far.RegionId, Cell = CentralSite(farMap) },
                });
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// The surface end: walkable hill or plain, off the roads, as far as possible from every settlement and place of the
        /// region (an ancient thing in the back country), ties broken by position.
        /// </summary>
        private static MapCoord SurfaceSite(WorldData world, RegionId region, RegionMap map)
        {
            var terrain = new OverlandTerrain(map, world.FindRegion(region)?.Kind ?? RegionKind.Vale, world.Seed);
            MapCoord best = new MapCoord(map.Width / 2, map.Depth / 2);
            var bestScore = -1;
            for (var z = 10; z < map.Depth - 10; z++)
                for (var x = 10; x < map.Width - 10; x++)
                {
                    var c = terrain.Cell(x, z);
                    if (!c.Passable || c.Road || (c.Biome != Biome.Hills && c.Biome != Biome.Heath && c.Biome != Biome.Grassland && c.Biome != Biome.Meadow)) continue;
                    var nearest = int.MaxValue;
                    foreach (var s in world.Settlements)
                        if (s.RegionId == region) nearest = Math.Min(nearest, Math.Abs(s.Position.X - x) + Math.Abs(s.Position.Z - z));
                    foreach (var p in world.Places)
                        if (p.RegionId == region && p.ParentKind == Realmkeeper.Domain.Places.PlaceParentKind.Region) nearest = Math.Min(nearest, Math.Abs(p.Cell.X - x) + Math.Abs(p.Cell.Z - z));
                    if (nearest > bestScore) { bestScore = nearest; best = new MapCoord(x, z); }
                }

            return best;
        }

        /// <summary>The far end: the walkable cell nearest the region's centre.</summary>
        private static MapCoord CentralSite(RegionMap map)
        {
            var terrain = new OverlandTerrain(map, RegionKind.Vale, 1);
            for (var r = 0; r < map.Width / 2; r++)
                for (var dz = -r; dz <= r; dz++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var x = map.Width / 2 + dx;
                        var z = map.Depth / 2 + dz;
                        if (map.InBounds(x, z) && terrain.Cell(x, z).Passable && !map.IsWater(x, z)) return new MapCoord(x, z);
                    }

            return new MapCoord(map.Width / 2, map.Depth / 2);
        }

        private static Guid GuidFrom(long seed)
        {
            var a = BitConverter.GetBytes(seed);
            var b = BitConverter.GetBytes(SeedMixer.Combine(unchecked((ulong)seed), "guid"));
            var bytes = new byte[16];
            Array.Copy(a, 0, bytes, 0, 8);
            Array.Copy(b, 0, bytes, 8, 8);
            return new Guid(bytes);
        }
    }
}
