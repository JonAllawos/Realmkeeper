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
        private readonly Dictionary<RegionId, List<OverlandLandmark>> _landmarks = new Dictionary<RegionId, List<OverlandLandmark>>();

        /// <summary>Phase 24 (ADR-0114): a surface region's generated landmarks (deterministic; cached for the session, never saved).</summary>
        public List<OverlandLandmark> GeneratedLandmarks(RegionId region)
        {
            if (!HasWorld) return new List<OverlandLandmark>();
            if (_landmarks.TryGetValue(region, out var l)) return l;
            var terrain = GetOverlandTerrain(RealmIds.Surface, region);
            l = terrain == null ? new List<OverlandLandmark>() : LandmarkGenesis.Generate(_world, terrain, region);
            _landmarks[region] = l;
            return l;
        }

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

        // ----- Phase 24 queries (read-only; nothing here changes the world) ---------------------------------------

        /// <summary>A landmark as the country shows it, with what the player has done there.</summary>
        public sealed class LandmarkView
        {
            public OverlandLandmark Landmark { get; set; }
            public bool Found { get; set; }
            public bool Searched { get; set; }
        }

        /// <summary>Phase 24: the landmarks of the player's region (all of them; the view decides what an unfound one shows).</summary>
        public List<LandmarkView> GetLandmarks()
        {
            var list = new List<LandmarkView>();
            var p = _world?.Player;
            if (p == null || !p.IsOverland) return list;
            foreach (var l in Landmarks.InRegion(this, _world, RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId, p.RegionId))
            {
                var s = _world.Overland.FindLandmark(l.Id);
                list.Add(new LandmarkView { Landmark = l, Found = s?.Found == true, Searched = s?.Searched == true });
            }

            return list;
        }

        /// <summary>Phase 24: the world's travelling groups now in the player's region, where they are on its roads.</summary>
        public List<TravellerView> GetTravellers()
        {
            var p = _world?.Player;
            if (p == null || !p.IsOverland || !RealmIds.IsSurface(p.RealmId)) return new List<TravellerView>();
            return Travellers.InRegion(this, _world, p.RegionId);
        }

        /// <summary>Phase 24: the compass's mark, resolved (null when there is none).</summary>
        public WaypointView GetWaypoint() => HasWorld ? Waypoints.View(this, _world) : null;

        /// <summary>Phase 24: the ground where the player is (calm off the surface, or with no weather).</summary>
        public GroundConditions GetGroundConditions()
        {
            var p = _world?.Player;
            if (p == null || !RealmIds.IsSurface(p.RealmId)) return GroundConditions.Calm;
            return GroundConditions.Of(_world, p.IsOverland ? p.RegionId : Simulation.Atlas.SimulationTiers.PlayersRegion(_world), _world.MinuteOfDay);
        }

        /// <summary>Phase 24: the overland encounter waiting on the player's choice, or null (a copy).</summary>
        public OverlandEncounterData GetPendingOverlandEncounter() => _world?.Overland?.Pending?.Clone();

        /// <summary>Phase 24: the encounters met in the country, newest first (copies).</summary>
        public List<OverlandEncounterData> GetOverlandEncounters()
        {
            var list = new List<OverlandEncounterData>();
            if (_world?.Overland == null) return list;
            foreach (var e in _world.Overland.Encounters) list.Insert(0, e.Clone());
            return list;
        }

        /// <summary>
        /// Phase 24 (ADR-0113): where turning to the map would put the player, in the country (region metres): the halted
        /// journey's place, a settlement's way in, or the nearest road. The presentation charges the walk there.
        /// </summary>
        public (float x, float z, string label)? GetMapPoint()
        {
            var p = _world?.Player;
            if (p == null || !p.IsOverland || !RealmIds.IsSurface(p.RealmId)) return null;
            var t = OverlandCommandHandlers.MapPoint(this, _world, p, GetOverlandTerrain(RealmIds.Surface, p.RegionId));
            var label = t.Kind == OverlandCommandHandlers.MapPointKind.Journey ? "the journey's place on the road"
                : t.Kind == OverlandCommandHandlers.MapPointKind.Road ? "the nearest road"
                : "the way into " + _world.FindSettlement(t.Settlement)?.Name;
            return (t.X, t.Z, label);
        }

        /// <summary>Phase 24: whether a fight is going on (cheap: no copy of the world).</summary>
        public bool HasActiveFight => _world?.ActiveEncounter != null;

        /// <summary>Phase 24: the creature populations (copies): where each lives and how many (encounter sources).</summary>
        public List<CreaturePopulationData> GetCreaturePopulations()
        {
            var list = new List<CreaturePopulationData>();
            if (_world == null) return list;
            foreach (var p in _world.CreaturePopulations) list.Add(p.Clone());
            return list;
        }

        /// <summary>Phase 24: whether a halted journey waits within reach of the player in the country (and its point).</summary>
        public (float x, float z)? GetHaltedJourneyPoint() =>
            HasWorld && _world.Player?.IsOverland == true && OverlandRoads.HaltedPoint(this, _world, out var x, out var z) ? (x, z) : ((float, float)?)null;
    }
}
