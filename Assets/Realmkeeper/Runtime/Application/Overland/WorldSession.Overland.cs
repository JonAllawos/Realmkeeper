using System.Collections.Generic;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        private readonly Dictionary<RegionId, RegionMap> _realmMaps = new Dictionary<RegionId, RegionMap>();
        private readonly Dictionary<RegionId, OverlandTerrain> _terrains = new Dictionary<RegionId, OverlandTerrain>();

        /// <summary>
        /// Phase 23 (ADR-0111): a region's geography in any realm: the surface's from <see cref="MapFor"/>, another realm's
        /// generated from its seed (geography only) the first time and kept for the session. Derived data; never saved.
        /// </summary>
        public RegionMap MapForRealm(string realmId, RegionId region)
        {
            if (!HasWorld) return null;
            if (RealmIds.IsSurface(realmId)) return MapFor(region);
            if (_realmMaps.TryGetValue(region, out var m)) return m;
            var r = _world.FindRealm(realmId)?.FindRegion(region);
            if (r == null) return null;
            m = _generator.GenerateGeography(r.Seed, r.MountainDirection, r.RegionId);
            _realmMaps[region] = m;
            return m;
        }

        /// <summary>Phase 23 (ADR-0110): the walkable country of a region (cached for the session).</summary>
        public OverlandTerrain GetOverlandTerrain(string realmId, RegionId region)
        {
            if (!HasWorld) return null;
            if (_terrains.TryGetValue(region, out var t)) return t;
            var map = MapForRealm(realmId, region);
            if (map == null) return null;
            RegionKind kind;
            long seed;
            if (RealmIds.IsSurface(realmId))
            {
                var r = _world.FindRegion(region);
                kind = r?.Kind ?? RegionKind.Vale;
                seed = r != null && r.Seed != 0 ? r.Seed : _world.Seed;
            }
            else
            {
                var r = _world.FindRealm(realmId)?.FindRegion(region);
                kind = r?.Kind ?? RegionKind.Vale;
                seed = r?.Seed ?? _world.Seed;
            }

            t = new OverlandTerrain(map, kind, seed);
            _terrains[region] = t;
            return t;
        }

        /// <summary>Phase 23: the country the player is walking, with its sites (null when the player is not overland).</summary>
        public OverlandView GetOverland()
        {
            var p = _world?.Player;
            if (p == null || !p.IsOverland) return null;
            var terrain = GetOverlandTerrain(p.RealmId, p.RegionId);
            if (terrain == null) return null;
            var realm = _world.FindRealm(RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId);
            var view = new OverlandView
            {
                RealmId = RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId, RealmName = realm?.Name ?? "The known world", Profile = realm?.Profile ?? RealmProfiles.Surface,
                RegionId = p.RegionId, Terrain = terrain, Pose = p.Pose,
            };
            if (RealmIds.IsSurface(p.RealmId))
            {
                var r = _world.FindRegion(p.RegionId);
                view.RegionName = r?.Name;
                view.RegionKind = r?.Kind ?? RegionKind.Vale;
            }
            else
            {
                var r = realm?.FindRegion(p.RegionId);
                view.RegionName = r?.Name;
                view.RegionKind = r?.Kind ?? RegionKind.Vale;
            }

            view.Sites.AddRange(OverlandQueries.Sites(_world, view.RealmId, p.RegionId, terrain));
            return view;
        }
    }
}
