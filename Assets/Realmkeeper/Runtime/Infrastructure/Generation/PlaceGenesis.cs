using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Phase 13 (ADR-0062): puts the wilderness places into the world, once. Each lies on land near a settlement whose
    /// country suits it (a mine in the hills, a fort by a road), off the roads and away from other places. Its layout is
    /// generated (or authored) now and saved, its containers filled, its people bound: the mine's captive is a real
    /// resident, the fort's garrison are new persistent people of its faction. Deterministic from the world seed; a
    /// world that already has its places is never changed.
    /// </summary>
    public static class PlaceGenesis
    {
        public const int MinDistance = 6;
        public const int MaxDistance = 22;

        /// <summary>The Phase 13 definitions (made together, once).</summary>
        public static readonly string[] Phase13 = { PlaceCatalog.Mine, PlaceCatalog.Crypt, PlaceCatalog.Fort, PlaceCatalog.Cave, PlaceCatalog.Ruin };

        /// <summary>
        /// Ensures the world's places. Each generation of definitions is ensured on its own (ADR-0071): the Phase 13 five
        /// together, once; the Phase 14 places chosen by the world's context, once. A world that has a generation's places
        /// is never changed by it again, so a Phase 13 save gains the new places exactly once and keeps everything else.
        /// </summary>
        public static bool Ensure(WorldData world, RegionMap map, IRandomSourceFactory random)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (map == null || world.Settlements.Count == 0) return false;
            var changed = EnsurePhase13(world, map, random);
            changed |= EnsurePhase14(world, map);
            return changed;
        }

        private static bool Has(WorldData world, IEnumerable<string> definitions)
        {
            var ids = new HashSet<string>(definitions, StringComparer.Ordinal);
            foreach (var p in world.Places) if (p != null && p.ParentKind == PlaceParentKind.Region && ids.Contains(p.DefinitionId)) return true;
            return false;
        }

        private static bool EnsurePhase13(WorldData world, RegionMap map, IRandomSourceFactory random)
        {
            if (Has(world, Phase13)) return false; // made once
            var report = QuestGenesis.Direct(world);
            var made = new List<PlaceData>();
            Make(world, map, made, PlaceCatalog.Mine, s => true, t => t == TerrainType.Hills || t == TerrainType.Mountain, near: false);
            Make(world, map, made, PlaceCatalog.Crypt, s => s.SettlementType == SettlementType.Village, t => t == TerrainType.Plains || t == TerrainType.Forest, near: true);
            Make(world, map, made, PlaceCatalog.Fort, s => !s.FactionId.IsEmpty, t => t == TerrainType.Plains || t == TerrainType.Hills || t == TerrainType.Forest, near: true, byRoad: true);
            Make(world, map, made, PlaceCatalog.Cave, s => true, t => t == TerrainType.Forest || t == TerrainType.Hills, near: false);
            Make(world, map, made, PlaceCatalog.Ruin, s => true, t => t == TerrainType.Forest || t == TerrainType.Plains, near: false);
            if (made.Count == 0) return false;

            foreach (var p in made)
            {
                world.Places.Add(p);
                PlaceContents.Ensure(world, p);
                p.Threat = PlaceThreat.Of(p);
            }

            var mine = made.Find(p => p.DefinitionId == PlaceCatalog.Mine);
            if (mine != null) BindCaptive(world, mine, report);
            var fort = made.Find(p => p.DefinitionId == PlaceCatalog.Fort);
            if (fort != null) Garrison(world, fort, random);
            return true;
        }

        /// <summary>
        /// Phase 14 (ADR-0071): the five new places, chosen by the world's context — a catacomb near a village or town, a
        /// temple near a settlement with a faction (who built it), a bandit hideout above a road two trading settlements
        /// share, old works near a town, an abandoned hamlet in open country. Among those the world allows, three to five
        /// are taken (by the world seed), so worlds differ in which places they hold. Once any is made, the choice is
        /// settled: none is added later.
        /// </summary>
        private static bool EnsurePhase14(WorldData world, RegionMap map)
        {
            if (Has(world, Phase14Places.All)) return false;
            var eligible = new List<string>();
            var routeFor = (TradeRouteFinder)null;
            foreach (var id in Phase14Places.All)
            {
                switch (id)
                {
                    case Phase14Places.Catacomb: if (world.Settlements.Exists(s => s.SettlementType == SettlementType.Village || s.SettlementType == SettlementType.Town)) eligible.Add(id); break;
                    case Phase14Places.Temple: if (world.Settlements.Exists(s => !s.FactionId.IsEmpty)) eligible.Add(id); break;
                    case Phase14Places.Hideout:
                        routeFor = new TradeRouteFinder(world);
                        if (routeFor.Route != null || world.Settlements.Count >= 2) eligible.Add(id);
                        break;
                    case Phase14Places.Works: if (world.Settlements.Exists(s => s.SettlementType == SettlementType.Town || s.SettlementType == SettlementType.City)) eligible.Add(id); break;
                    case Phase14Places.Hamlet: eligible.Add(id); break;
                }
            }

            if (eligible.Count == 0) return false;
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "phase14-places");
            eligible.Sort((a, b) =>
            {
                var ha = SeedMixer.Mix(SeedMixer.Combine(seed, a));
                var hb = SeedMixer.Mix(SeedMixer.Combine(seed, b));
                return ha != hb ? ha.CompareTo(hb) : string.CompareOrdinal(a, b);
            });
            var count = Math.Min(eligible.Count, 3 + (int)(SeedMixer.Mix(seed) % 3UL));
            var made = new List<PlaceData>();
            foreach (var p in world.Places) if (p != null && p.ParentKind == PlaceParentKind.Region) made.Add(p); // keep their distance from the old places
            var start = made.Count;
            for (var i = 0; i < eligible.Count && made.Count - start < count; i++) // one that cannot be sited gives way to the next
            {
                var id = eligible[i];
                switch (id)
                {
                    case Phase14Places.Catacomb:
                        Make(world, map, made, id, s => s.SettlementType == SettlementType.Village || s.SettlementType == SettlementType.Town, t => t == TerrainType.Plains || t == TerrainType.Forest || t == TerrainType.Hills, near: true);
                        break;
                    case Phase14Places.Temple:
                        Make(world, map, made, id, s => !s.FactionId.IsEmpty, t => t == TerrainType.Hills || t == TerrainType.Plains || t == TerrainType.Forest, near: false);
                        break;
                    case Phase14Places.Hideout:
                    {
                        var route = routeFor?.Route;
                        Make(world, map, made, id, s => route == null || s.SettlementId == route.OriginId, t => t == TerrainType.Forest || t == TerrainType.Hills || t == TerrainType.Plains, near: true, byRoad: true);
                        var den = made.Count > 0 ? made[made.Count - 1] : null;
                        if (den != null && den.DefinitionId == id && route != null) den.Variables["route"] = route.TradeRouteId.ToString();
                        break;
                    }

                    case Phase14Places.Works:
                        Make(world, map, made, id, s => s.SettlementType == SettlementType.Town || s.SettlementType == SettlementType.City, t => t != TerrainType.Mountain, near: true);
                        break;
                    case Phase14Places.Hamlet:
                        Make(world, map, made, id, s => true, t => t == TerrainType.Plains || t == TerrainType.Forest, near: false);
                        break;
                }
            }

            if (made.Count == start) return false;
            for (var i = start; i < made.Count; i++)
            {
                var p = made[i];
                if (p.DefinitionId == Phase14Places.Temple && world.FindSettlement(p.SettlementId) is SettlementData anchor && !anchor.FactionId.IsEmpty)
                    p.Variables["built_by"] = anchor.FactionId.ToString(); // the faction whose fane it was (ADR-0071)
                world.Places.Add(p);
                PlaceContents.Ensure(world, p);
                p.Threat = PlaceThreat.Of(p);
            }

            Phase14Quests.Bind(world, made.GetRange(start, made.Count - start), QuestGenesis.Direct(world));
            return true;
        }

        /// <summary>The trade route a hideout preys on: the first active route between two settlements (by ID), or none.</summary>
        private sealed class TradeRouteFinder
        {
            public TradeRouteFinder(WorldData world)
            {
                foreach (var r in world.TradeRoutes)
                    if (r != null && (Route == null || r.TradeRouteId.CompareTo(Route.TradeRouteId) < 0)) Route = r;
            }

            public Realmkeeper.Domain.Economy.TradeRouteData Route { get; }
        }

        private static void Make(WorldData world, RegionMap map, List<PlaceData> made, string definitionId, Func<SettlementData, bool> anchorOk,
            Func<TerrainType, bool> terrainOk, bool near, bool byRoad = false)
        {
            var def = PlaceCatalog.Find(definitionId);
            var archetype = ArchetypeCatalog.Find(def?.ArchetypeId);
            if (def == null || archetype == null) return;
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "place:" + definitionId);

            // Settlements in a stable order; the first that suits and has a site in reach is the anchor.
            var anchors = new List<SettlementData>();
            bool OnMap(SettlementData s) => map.RegionId.IsEmpty || s.RegionId == map.RegionId; // Phase 16: positions are cells of their own region
            foreach (var s in world.Settlements) if (s != null && OnMap(s) && anchorOk(s)) anchors.Add(s);
            if (anchors.Count == 0) foreach (var s in world.Settlements) if (s != null && OnMap(s)) anchors.Add(s);
            anchors.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            var start = (int)(SeedMixer.Mix(seed) % (ulong)anchors.Count);
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[(start + i) % anchors.Count];
                if (!Site(world, map, made, anchor, terrainOk, near, byRoad, seed, out var cell)) continue;
                var id = "place:" + definitionId;
                var place = new PlaceData
                {
                    PlaceId = id, DefinitionId = def.Id, Name = def.Name, Description = def.Description, Category = archetype.Category, ArchetypeId = archetype.Id,
                    ParentKind = PlaceParentKind.Region, ParentId = anchor.RegionId.ToString(), RegionId = anchor.RegionId, SettlementId = anchor.SettlementId, Cell = cell,
                    Seed = seed, GeneratorVersion = ArchetypeCatalog.GeneratorVersion, Discovery = PlaceDiscovery.Unknown,
                    FactionId = definitionId == PlaceCatalog.Fort ? anchor.FactionId : default,
                };
                place.Layout = def.Authored != null ? def.Authored() : PlaceGenerator.Generate(archetype, seed, def);
                made.Add(place);
                return;
            }
        }

        private static bool Site(WorldData world, RegionMap map, List<PlaceData> made, SettlementData anchor, Func<TerrainType, bool> terrainOk, bool near, bool byRoad, ulong seed,
            out MapCoord cell)
        {
            var candidates = new List<MapCoord>();
            var lo = near ? MinDistance : MinDistance + 4;
            var hi = near ? MinDistance + 8 : MaxDistance;
            for (var x = anchor.Position.X - hi; x <= anchor.Position.X + hi; x++)
                for (var z = anchor.Position.Z - hi; z <= anchor.Position.Z + hi; z++)
                {
                    if (!map.InBounds(x, z) || map.IsWater(x, z) || map.IsRoad(x, z) || !terrainOk(map.GetTerrain(x, z))) continue;
                    var d = Math.Max(Math.Abs(x - anchor.Position.X), Math.Abs(z - anchor.Position.Z));
                    if (d < lo || d > hi) continue;
                    var clear = true;
                    foreach (var s in world.Settlements)
                        if ((map.RegionId.IsEmpty || s.RegionId == map.RegionId) && Math.Max(Math.Abs(x - s.Position.X), Math.Abs(z - s.Position.Z)) < MinDistance - 1) clear = false;
                    foreach (var p in made)
                        if (Math.Max(Math.Abs(x - p.Cell.X), Math.Abs(z - p.Cell.Z)) < MinDistance) clear = false;
                    if (!clear) continue;
                    if (byRoad && !NearRoad(map, x, z, 2)) continue;
                    candidates.Add(new MapCoord(x, z));
                }

            if (candidates.Count == 0)
            {
                cell = default;
                return false;
            }

            cell = candidates[(int)(SeedMixer.Mix(SeedMixer.Combine(seed, "site")) % (ulong)candidates.Count)];
            return true;
        }

        private static bool NearRoad(RegionMap map, int x, int z, int r)
        {
            for (var dx = -r; dx <= r; dx++)
                for (var dz = -r; dz <= r; dz++)
                    if (map.InBounds(x + dx, z + dz) && map.IsRoad(x + dx, z + dz)) return true;
            return false;
        }

        /// <summary>The person held in the mine: a resident of its settlement who plays no part in another quest; then the quest that asks for them.</summary>
        private static void BindCaptive(WorldData world, PlaceData mine, QuestReporter report)
        {
            var captiveFeature = mine.Layout.Features.Find(f => f.Kind == FeatureKind.Captive);
            if (captiveFeature == null) return;
            var busy = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in world.Quests) foreach (var v in q.Bindings.Values) busy.Add(v);
            var candidates = new List<NpcData>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId != mine.SettlementId || busy.Contains(n.NpcId.ToString())) continue;
                if (n.Role == NpcRole.Elder || n.Role == NpcRole.Guard || n.Role == NpcRole.Innkeeper || n.Role == NpcRole.Merchant) continue;
                candidates.Add(n);
            }

            if (candidates.Count < 2)
                foreach (var n in world.Npcs)
                    if (!n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == mine.SettlementId && !busy.Contains(n.NpcId.ToString()) && !candidates.Contains(n)) candidates.Add(n);
            if (candidates.Count < 2) return;
            candidates.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
            var pick = (int)(SeedMixer.Mix(SeedMixer.Combine(mine.Seed, "captive")) % (ulong)candidates.Count);
            var captive = candidates[pick];
            var giver = candidates[(pick + 1) % candidates.Count];
            captiveFeature.Data["npc"] = captive.NpcId.ToString();
            world.Escorts.Add(new EscortData
            {
                NpcId = captive.NpcId, State = EscortState.Captive, PlaceId = mine.PlaceId, Room = captiveFeature.Room, Destination = captive.HomeSettlementId, ChangedAt = QuestClock.Now(world),
            });

            var def = QuestCatalog.Find(QuestCatalog.LostBelow);
            var given = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["giver"] = giver.NpcId.ToString(), ["captive"] = captive.NpcId.ToString(), ["place"] = mine.PlaceId, ["settlement"] = mine.SettlementId.ToString(),
            };
            var quest = QuestLifecycle.Create(world, def, given, "authored", null, report, out _);
            if (quest != null)
                foreach (var e in world.Escorts)
                    if (e.NpcId == captive.NpcId) e.QuestInstanceId = quest.InstanceId;
        }

        /// <summary>The fort's garrison: new persistent people of the fort's faction (never copies of anyone), one per post.</summary>
        private static void Garrison(WorldData world, PlaceData fort, IRandomSourceFactory random)
        {
            var anchor = world.FindSettlement(fort.SettlementId);
            if (anchor == null) return;
            var ids = random.Create(SeedMixer.Combine(fort.Seed, "garrison-ids"));
            var names = new PersonNameGenerator(random.Create(SeedMixer.Combine(fort.Seed, "garrison-names")));
            var added = new List<NpcData>();
            foreach (var g in fort.Layout.Groups)
                foreach (var m in g.Members)
                {
                    var npc = new NpcData
                    {
                        NpcId = new NpcId(DeterministicGuid.Next(ids)), Name = names.NextFullName(NpcRole.Guard), HomeSettlementId = anchor.SettlementId,
                        FactionId = fort.FactionId, Role = NpcRole.Guard,
                    };
                    for (var tries = 0; tries < 20 && world.Npcs.Exists(x => x.Name == npc.Name); tries++) npc.Name = names.NextFullName(NpcRole.Guard);
                    world.Npcs.Add(npc);
                    m.NpcId = npc.NpcId;
                    added.Add(npc);
                }

            if (added.Count == 0) return;
            anchor.Population = Math.Max(anchor.Population, CountNamed(world, anchor.SettlementId)); // named people never outnumber the population
            Simulation.Npcs.NpcGenesis.EnsurePeople(world);
            foreach (var g in fort.Layout.Groups)
                if (g.Id == "captain")
                    foreach (var m in g.Members)
                        if (world.FindNpc(m.NpcId) is NpcData c) c.Tier = NpcTier.Notable;
            NpcCharacterGenesis.Ensure(world);
        }

        private static int CountNamed(WorldData world, SettlementId s)
        {
            var n = 0;
            foreach (var x in world.Npcs) if (x.HomeSettlementId == s && !x.IsDeceased) n++;
            return n;
        }
    }
}
