using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Items;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Overland
{
    /// <summary>
    /// Phase 24 (ADR-0114): the country's landmarks, generated from the region's seed and geography (engine-free,
    /// deterministic; never saved). They follow the roads, rivers and hills: waystones where roads meet and along long roads,
    /// cairns on hilltops, a travellers' camp midway on a long road, an abandoned camp in the woods, a ruined steading in the
    /// open, a watch post where a faction's road leaves the region. Wrecks are not generated: one lies where a real caravan was
    /// lost (a <see cref="WorldEventType.CaravanLost"/> on one of the region's roads). The ancient realm gets none (no lore).
    /// </summary>
    public static class LandmarkGenesis
    {
        public const int MaxWaystones = 6;
        public const int MaxCairns = 4;
        public const int MaxCamps = 3;
        public const int MaxAbandoned = 2;
        public const int MaxSteadings = 2;

        public static List<OverlandLandmark> Generate(WorldData w, OverlandTerrain terrain, RegionId region)
        {
            var list = new List<OverlandLandmark>();
            var map = terrain.Map;
            var settlements = w.Settlements.FindAll(s => s.RegionId == region);
            var roadDistance = RoadDistance(map);
            int SettleDist(MapCoord c)
            {
                var best = int.MaxValue;
                foreach (var s in settlements) best = Math.Min(best, Math.Max(Math.Abs(s.Position.X - c.X), Math.Abs(s.Position.Z - c.Z)));
                return best;
            }

            bool FarFromOthers(MapCoord c, int cells)
            {
                foreach (var l in list) if (Math.Max(Math.Abs(l.Cell.X - c.X), Math.Abs(l.Cell.Z - c.Z)) < cells) return false;
                return true;
            }

            void Add(LandmarkKind kind, MapCoord cell, string name, string text, string routeId = null, FactionId faction = default)
            {
                if (!terrain.Cell(cell.X, cell.Z).Passable)
                {
                    // Never on a mountain face or in the water: the nearest walkable cell beside it.
                    var (ax, az) = OverlandQueries.Approach(terrain, cell, 4);
                    cell = OverlandScale.CellAt(map, ax, az);
                    if (!terrain.Cell(cell.X, cell.Z).Passable) return;
                }

                var n = list.FindAll(l => l.Kind == kind).Count;
                var (x, z) = OverlandScale.CellCentre(cell);
                list.Add(new OverlandLandmark
                {
                    Id = "landmark:" + region + ":" + kind.ToString().ToLowerInvariant() + ":" + n.ToString(CultureInfo.InvariantCulture), Kind = kind, Name = name, RealmId = RealmIds.Surface,
                    RegionId = region, Cell = cell, X = x, Z = z, Text = text, RouteId = routeId, FactionId = faction,
                });
            }

            // ----- Waystones: at junctions, then along long roads (never in a town's shadow) -----
            var onRoads = new Dictionary<MapCoord, List<(RoadPath road, int index)>>();
            foreach (var road in map.Roads)
                for (var i = 0; i < road.Cells.Count; i++)
                {
                    if (!onRoads.TryGetValue(road.Cells[i], out var l)) onRoads[road.Cells[i]] = l = new List<(RoadPath, int)>();
                    l.Add((road, i));
                }

            var junctions = new List<MapCoord>();
            foreach (var pair in onRoads)
            {
                var distinct = new HashSet<RoadPath>();
                foreach (var (road, _) in pair.Value) distinct.Add(road);
                if (distinct.Count >= 2 && SettleDist(pair.Key) >= 7) junctions.Add(pair.Key);
            }

            junctions.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
            foreach (var j in junctions)
            {
                if (list.Count >= MaxWaystones || !FarFromOthers(j, 10)) continue;
                var cell = Beside(terrain, j);
                Add(LandmarkKind.Waystone, cell, "Waystone", Inscription(w, map, onRoads[j]), RouteOf(w, onRoads[j][0].road));
            }

            foreach (var road in map.Roads)
                for (var i = 40; i < road.Cells.Count - 10; i += 40)
                {
                    if (list.FindAll(l => l.Kind == LandmarkKind.Waystone).Count >= MaxWaystones) break;
                    var c = road.Cells[i];
                    if (SettleDist(c) < 8 || !FarFromOthers(c, 14)) continue;
                    Add(LandmarkKind.Waystone, Beside(terrain, c), "Waystone", Inscription(w, map, onRoads[c]), RouteOf(w, road));
                }

            // ----- Cairns: hilltops, far apart, off the road -----
            var hills = new List<(MapCoord c, float e)>();
            for (var z = 3; z < map.Depth - 3; z++)
                for (var x = 3; x < map.Width - 3; x++)
                {
                    var cell = terrain.Cell(x, z);
                    if (!cell.Passable || cell.Road || (cell.Biome != Biome.Hills && cell.Biome != Biome.Rocky && cell.Biome != Biome.Heath)) continue;
                    var e = map.GetElevation(x, z);
                    if (e < 0.5f || roadDistance[z * map.Width + x] < 4) continue;
                    var top = true;
                    for (var dz = -3; dz <= 3 && top; dz++)
                        for (var dx = -3; dx <= 3; dx++)
                            if ((dx != 0 || dz != 0) && map.GetElevation(x + dx, z + dz) > e) { top = false; break; }
                    if (top) hills.Add((new MapCoord(x, z), e));
                }

            hills.Sort((a, b) => b.e != a.e ? b.e.CompareTo(a.e) : (a.c.Z != b.c.Z ? a.c.Z.CompareTo(b.c.Z) : a.c.X.CompareTo(b.c.X)));
            var cairns = 0;
            foreach (var (c, _) in hills)
            {
                if (cairns >= MaxCairns || !FarFromOthers(c, 24)) continue;
                Add(LandmarkKind.Cairn, c, "Hilltop cairn", "A cairn of piled stones on the hilltop. From the top you can see a long way around.");
                cairns++;
            }

            // ----- A travellers' camp midway along each long road -----
            var camps = 0;
            foreach (var road in map.Roads)
            {
                if (camps >= MaxCamps || road.Cells.Count < 24) continue;
                var mid = road.Cells[road.Cells.Count / 2];
                if (SettleDist(mid) < 10 || !FarFromOthers(mid, 8)) continue;
                var c = Beside(terrain, mid);
                if (c.Equals(mid)) continue;
                Add(LandmarkKind.TravellersCamp, c, "Travellers' camp", "A ring of blackened stones and flattened grass beside the road, where travellers stop for the night. A safe place to rest.", RouteOf(w, road));
                camps++;
            }

            // ----- Abandoned camps in the woods and hills, off the road -----
            var candidates = new List<(MapCoord c, float r)>();
            var steadings = new List<(MapCoord c, float r)>();
            for (var z = 2; z < map.Depth - 2; z++)
                for (var x = 2; x < map.Width - 2; x++)
                {
                    var cell = terrain.Cell(x, z);
                    if (!cell.Passable || cell.Road || cell.Crossing) continue;
                    var d = roadDistance[z * map.Width + x];
                    var c = new MapCoord(x, z);
                    if ((cell.Biome == Biome.Forest || cell.Biome == Biome.DenseForest || cell.Biome == Biome.Hills) && d >= 5 && d <= 14 && SettleDist(c) >= 12)
                        candidates.Add((c, terrain.Random01(x, z, 901)));
                    if ((cell.Biome == Biome.Grassland || cell.Biome == Biome.Meadow || cell.Biome == Biome.Valley) && d >= 3 && d <= 12 && SettleDist(c) >= 15)
                        steadings.Add((c, terrain.Random01(x, z, 907)));
                }

            candidates.Sort((a, b) => a.r.CompareTo(b.r));
            var abandoned = 0;
            foreach (var (c, _) in candidates)
            {
                if (abandoned >= MaxAbandoned || !FarFromOthers(c, 14)) continue;
                Add(LandmarkKind.AbandonedCamp, c, "Abandoned camp", "A camp left in a hurry: a cold fire pit, a torn bedroll, a pot on its side. Whoever slept here did not come back for their things.");
                abandoned++;
            }

            steadings.Sort((a, b) => a.r.CompareTo(b.r));
            var ruined = 0;
            foreach (var (c, _) in steadings)
            {
                if (ruined >= MaxSteadings || !FarFromOthers(c, 14)) continue;
                Add(LandmarkKind.RuinedHomestead, c, "Ruined steading", "The walls of a farmstead, roofless and overgrown. No one has lived here for years.");
                ruined++;
            }

            // ----- Watch posts where a faction's road leaves the region -----
            var here = w.FindRegion(region);
            var seen = new HashSet<RegionId>();
            foreach (var route in w.Routes)
            {
                if (!route.IsCrossing || here == null) continue;
                SettlementId mine;
                RegionId other;
                if (route.RegionA == region) { mine = route.A; other = route.RegionB; }
                else if (route.RegionB == region) { mine = route.B; other = route.RegionA; }
                else continue;
                if (!seen.Add(other)) continue;
                var s = w.FindSettlement(mine);
                var there = w.FindRegion(other);
                if (s == null || there == null || s.FactionId.IsEmpty) continue;
                var faction = w.FindFaction(s.FactionId);
                if (faction == null || faction.IsDissolved) continue;
                var edge = OverlandQueries.EdgeCell(terrain, s.Position, there.AtlasX - here.AtlasX, there.AtlasZ - here.AtlasZ);
                var post = Inward(terrain, edge, 5);
                if (!FarFromOthers(post, 3)) continue;
                Add(LandmarkKind.WatchPost, post, "Watch post of " + faction.Name,
                    $"A wooden watch post where the road leaves {here.Name} for {there.Name}, kept by {faction.Name}. Their patrols keep this stretch of road quieter.", route.RouteId, faction.FactionId);
            }

            return list;
        }

        /// <summary>Cells to the nearest road (4-neighbour steps; large where there is no road).</summary>
        private static int[] RoadDistance(RegionMap map)
        {
            var d = new int[map.Width * map.Depth];
            for (var i = 0; i < d.Length; i++) d[i] = int.MaxValue;
            var q = new Queue<MapCoord>();
            foreach (var road in map.Roads)
                foreach (var c in road.Cells)
                    if (map.InBounds(c) && d[c.Z * map.Width + c.X] != 0) { d[c.Z * map.Width + c.X] = 0; q.Enqueue(c); }
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                var v = d[c.Z * map.Width + c.X] + 1;
                foreach (var n in new[] { new MapCoord(c.X + 1, c.Z), new MapCoord(c.X - 1, c.Z), new MapCoord(c.X, c.Z + 1), new MapCoord(c.X, c.Z - 1) })
                {
                    if (!map.InBounds(n) || d[n.Z * map.Width + n.X] <= v) continue;
                    d[n.Z * map.Width + n.X] = v;
                    q.Enqueue(n);
                }
            }

            return d;
        }

        /// <summary>A walkable cell beside a road cell (off the road itself), or the cell when there is none.</summary>
        private static MapCoord Beside(OverlandTerrain t, MapCoord c)
        {
            foreach (var n in new[] { new MapCoord(c.X + 1, c.Z), new MapCoord(c.X, c.Z + 1), new MapCoord(c.X - 1, c.Z), new MapCoord(c.X, c.Z - 1) })
            {
                if (!t.Map.InBounds(n)) continue;
                var cell = t.Cell(n.X, n.Z);
                if (cell.Passable && !cell.Road && !cell.Crossing) return n;
            }

            return c;
        }

        private static MapCoord Inward(OverlandTerrain t, MapCoord edge, int steps)
        {
            var map = t.Map;
            var dx = edge.X <= 1 ? 1 : edge.X >= map.Width - 2 ? -1 : 0;
            var dz = edge.Z <= 1 ? 1 : edge.Z >= map.Depth - 2 ? -1 : 0;
            for (var i = steps; i >= 0; i--)
            {
                var c = new MapCoord(edge.X + dx * i, edge.Z + dz * i);
                if (map.InBounds(c) && t.Cell(c.X, c.Z).Passable) return Beside(t, c);
            }

            return edge;
        }

        private static string RouteOf(WorldData w, RoadPath road) => w.FindRoute(RouteSegmentData.IdFor(road.From, road.To))?.RouteId;

        /// <summary>
        /// What a waystone says: each way along the roads through it, the settlement it leads to, its direction and how long on
        /// foot (a cell of road is a quarter of an hour, ADR-0110). The figures are the road's own; nothing is guessed.
        /// </summary>
        private static string Inscription(WorldData w, RegionMap map, List<(RoadPath road, int index)> through)
        {
            var lines = new List<string>();
            var named = new HashSet<SettlementId>();
            foreach (var (road, i) in through)
            {
                void Way(SettlementId to, int cells)
                {
                    var s = w.FindSettlement(to);
                    if (s == null || !named.Add(to)) return;
                    var here = road.Cells[i];
                    lines.Add($"{s.Name}, {Bearing(here, s.Position)}, {TravelRules.Duration(cells * 0.25)} on foot");
                }

                Way(road.From, i);
                Way(road.To, road.Cells.Count - 1 - i);
            }

            return lines.Count == 0 ? "The letters are worn away." : "Carved on the stone: " + string.Join(" · ", lines) + ".";
        }

        public static string Bearing(MapCoord from, MapCoord to)
        {
            var dx = to.X - from.X;
            var dz = to.Z - from.Z;
            var a = Math.Atan2(dx, dz) * 180 / Math.PI; // 0 = north (+z)
            if (a < 0) a += 360;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return names[(int)Math.Round(a / 45) % 8];
        }
    }

    /// <summary>Phase 24: the landmarks of a region (generated, cached by the session; wrecks from world events), and their state.</summary>
    public static class Landmarks
    {
        /// <summary>Every landmark of a region of a realm: the generated ones and the wrecks of real lost caravans.</summary>
        public static List<OverlandLandmark> InRegion(WorldSession session, WorldData w, string realmId, RegionId region)
        {
            var list = new List<OverlandLandmark>();
            if (!RealmIds.IsSurface(realmId)) return list; // the ancient realm: nothing is invented
            list.AddRange(session.GeneratedLandmarks(region));
            list.AddRange(Wrecks(session, w, region));
            return list;
        }

        /// <summary>A wreck for each caravan lost on one of the region's roads, at the road's middle.</summary>
        public static List<OverlandLandmark> Wrecks(WorldSession session, WorldData w, RegionId region)
        {
            var list = new List<OverlandLandmark>();
            foreach (var e in w.WorldEvents)
            {
                if (e.Type != WorldEventType.CaravanLost || string.IsNullOrEmpty(e.RouteId)) continue;
                var route = w.FindRoute(e.RouteId);
                if (route == null || (route.RegionA != region && route.RegionB != region)) continue;
                var metres = (int)(TravelRules.Km(route) * 1000 * (route.IsCrossing ? (route.RegionA == region ? 0.3 : 0.7) : 0.5));
                if (!OverlandRoads.PointOf(session, w, route, route.A, metres, out var r, out var x, out var z) || r != region) continue;
                var terrain = session.GetOverlandTerrain(RealmIds.Surface, region);
                // Beside the road, not on it.
                var cell = OverlandScale.CellAt(terrain.Map, x, z);
                var (ax, az) = OverlandQueries.Approach(terrain, cell, 1);
                var (wx, wz) = (ax + 7f, az + 4f);
                if (!terrain.Walkable(wx, wz)) (wx, wz) = (ax, az);
                list.Add(new OverlandLandmark
                {
                    Id = "landmark:wreck:" + e.EventId, Kind = LandmarkKind.Wreck, Name = "A wrecked wagon", RealmId = RealmIds.Surface, RegionId = region,
                    Cell = OverlandScale.CellAt(terrain.Map, wx, wz), X = wx, Z = wz, RouteId = route.RouteId, EventId = e.EventId,
                    Text = "A wagon lies broken beside the road, its load scattered and its team gone. A caravan was lost here.",
                });
            }

            return list;
        }

        public static OverlandLandmark Find(WorldSession session, WorldData w, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("landmark:wreck:", StringComparison.Ordinal))
            {
                var e = w.WorldEvents.Find(x => x.EventId == id.Substring("landmark:wreck:".Length));
                var route = e == null ? null : w.FindRoute(e.RouteId);
                if (route == null) return null;
                foreach (var region in new[] { route.RegionA, route.RegionB })
                    foreach (var l in Wrecks(session, w, region))
                        if (l.Id == id) return l;
                return null;
            }

            var parts = id.Split(':');
            if (parts.Length < 4 || !Guid.TryParse(parts[1], out var g)) return null;
            return session.GeneratedLandmarks(new RegionId(g)).Find(l => l.Id == id);
        }

        public static bool IsFound(WorldData w, string id) => w.Overland?.FindLandmark(id)?.Found == true;

        public static LandmarkStateData State(WorldData w, string id)
        {
            var s = w.Overland.FindLandmark(id);
            if (s != null) return s;
            s = new LandmarkStateData { LandmarkId = id };
            w.Overland.Landmarks.Add(s);
            return s;
        }

        /// <summary>The container a searched landmark leaves (its key).</summary>
        public static string CacheKey(string landmarkId) => "cache:" + landmarkId;
    }

    /// <summary>Phase 24: the player's standing with a faction (-100..100) and how it reads.</summary>
    public static class Standings
    {
        public static int Faction(WorldData w, FactionId faction)
        {
            var social = w?.Player?.Social;
            if (social == null) return 0;
            foreach (var s in social.FactionStandings) if (s.FactionId == faction) return s.Standing;
            return 0;
        }

        public static string Words(int standing) =>
            standing >= 40 ? "They know you as a friend." : standing >= 10 ? "They think well of you." : standing > -10 ? "You are a stranger to them." : standing > -40 ? "They do not trust you." : "They count you an enemy.";
    }

    /// <summary>Phase 24: use a landmark the player stands at: read a waystone, look out from a cairn, search a camp, a steading or a wreck.</summary>
    public sealed class UseLandmarkCommand : ICommand
    {
        public UseLandmarkCommand(string landmarkId) { LandmarkId = landmarkId; }
        public string LandmarkId { get; }
    }

    public static class LandmarkCommandHandlers
    {
        public const float ReachMetres = 12f;

        /// <summary>A cairn's view: places and landmarks within this distance are found (before weather).</summary>
        public const float CairnViewMetres = 650f;

        public static void Register(CommandRegistry commands) => commands.Register<UseLandmarkCommand>(new UseHandler());

        private sealed class UseHandler : ICommandHandler<UseLandmarkCommand>
        {
            public CommandResult Handle(UseLandmarkCommand c, CommandContext context)
            {
                var refused = OverlandCommandHandlers.Ready(context, out var player);
                if (refused != null) return refused;
                if (!player.IsOverland) return CommandResult.InvalidState($"{player.Name} is not out in the country.");
                var world = context.Committed;
                var l = Landmarks.Find(context.Session, world, c.LandmarkId);
                if (l == null || l.RegionId != player.RegionId) return CommandResult.NotFound("There is no such landmark here.");
                var dx = player.Pose.X - l.X;
                var dz = player.Pose.Z - l.Z;
                if (dx * dx + dz * dz > ReachMetres * ReachMetres * 4) return CommandResult.InvalidState($"{l.Name} is {Math.Sqrt(dx * dx + dz * dz):0} m away: walk there first.");
                var w = context.Edit();
                var state = Landmarks.State(w, l.Id);
                var now = QuestClock.Now(w);
                if (!state.Found) { state.Found = true; state.FoundAt = now; }
                switch (l.Kind)
                {
                    case LandmarkKind.Waystone:
                        return CommandResult.Ok(l.Text);
                    case LandmarkKind.Cairn:
                        return CommandResult.Ok(LookOut(context, w, l));
                    case LandmarkKind.TravellersCamp:
                        return CommandResult.Ok(l.Text + " Rest here (the Rest button) and the night will pass undisturbed.");
                    case LandmarkKind.WatchPost:
                    {
                        return CommandResult.Ok(l.Text + " " + Standings.Words(Standings.Faction(w, l.FactionId)));
                    }

                    default:
                        return Search(context, w, l, state);
                }
            }
        }

        /// <summary>From a cairn: what lies within view is found (places, gateways, landmarks), and the view is recorded once.</summary>
        private static string LookOut(CommandContext context, WorldData w, OverlandLandmark l)
        {
            var reach = CairnViewMetres * GroundConditions.Of(w, l.RegionId, w.MinuteOfDay).SightFactor * (TravelRules.IsNight(w.MinuteOfDay) ? 0.5f : 1f);
            var found = new List<string>();
            foreach (var place in w.Places)
            {
                if (place.RegionId != l.RegionId || place.ParentKind != Domain.Places.PlaceParentKind.Region || place.Discovery >= Domain.Places.PlaceDiscovery.Discovered) continue;
                var (x, z) = OverlandScale.CellCentre(place.Cell);
                if ((x - l.X) * (x - l.X) + (z - l.Z) * (z - l.Z) > reach * reach) continue;
                place.Discovery = Domain.Places.PlaceDiscovery.Discovered;
                place.DiscoveredAt = QuestClock.Now(w);
                found.Add(place.Name);
            }

            foreach (var other in Landmarks.InRegion(context.Session, w, l.RealmId, l.RegionId))
            {
                if (other.Id == l.Id || Landmarks.IsFound(w, other.Id)) continue;
                if ((other.X - l.X) * (other.X - l.X) + (other.Z - l.Z) * (other.Z - l.Z) > reach * reach) continue;
                var s = Landmarks.State(w, other.Id);
                s.Found = true;
                s.FoundAt = QuestClock.Now(w);
                found.Add(other.Name.ToLowerInvariant());
            }

            var view = found.Count == 0 ? "Nothing you have not seen already." : "You make out: " + string.Join(", ", found) + ".";
            if (reach < CairnViewMetres * 0.7f) view += " The weather and the light hide much of the view.";
            return "From the cairn you look out over the country. " + view;
        }

        /// <summary>A camp, a steading or a wreck searched (once): what was left is put in a cache the player takes from; a wreck is evidence.</summary>
        private static CommandResult Search(CommandContext context, WorldData w, OverlandLandmark l, LandmarkStateData state)
        {
            var key = Landmarks.CacheKey(l.Id);
            var lines = new List<string>();
            if (l.Kind == LandmarkKind.Wreck && w.WorldEvents.Find(e => e.EventId == l.EventId) is WorldEventData ev)
            {
                // Seeing it is knowing it: the loss is confirmed, and recorded as a discovery.
                WorldEvents.PlayerLearns(ev, EventKnowledge.Confirmed, QuestClock.Now(w), "a wreck on the road");
                var d = new DiscoveryData
                {
                    Key = "wreck:" + ev.EventId, Text = "A caravan was lost on the road: " + WorldEvents.Telling(w, ev), Source = "landmark:" + l.Id,
                    Category = DiscoveryCategory.Economic, Certainty = DiscoveryCertainty.Confirmed, Major = false,
                };
                if (QuestRuntime.Discover(w, d, QuestRuntime.Reporter(context))) context.Quest(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = d.Key });
                lines.Add("The tracks and the scattered load tell what happened: " + WorldEvents.Telling(w, ev) + ".");
            }

            if (state.Searched)
            {
                lines.Insert(0, w.FindContainer(key) is ContainerData left && (left.Items.Count > 0 || left.Coin > 0) ? "You have searched here; what is left is still here." : "You have searched here already: there is nothing more.");
                return CommandResult.Ok(string.Join(" ", lines));
            }

            state.Searched = true;
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "landmark-search:" + l.Id);
            var loot = LootTables.Roll("old_cache", new LootContext { MaxRarity = ItemRarity.Common }, seed);
            var box = new ContainerData
            {
                Key = key, Kind = ContainerKind.Cache, Name = "What was left at the " + l.Name.ToLowerInvariant(), PlayerAccess = true, AcceptsDeposits = false,
                Coin = loot.Coin / 2, SourceKey = "table:old_cache",
            };
            var n = 0;
            foreach (var drop in loot.Items)
            {
                var def = ItemCatalog.Find(drop.ItemId);
                if (def == null || n >= 3) continue; // a few things, not a hoard
                var s = ItemStacks.Create(new ItemStackId(ItemGenesis.StackGuid(seed, n++)), def, Math.Max(1, drop.Quantity / 2), "Found at " + l.Name.ToLowerInvariant());
                s.Condition = drop.Condition;
                box.Items.Add(s);
            }

            if (box.Items.Count == 0 && box.Coin == 0) lines.Insert(0, "You search carefully, but whatever was here is long gone.");
            else
            {
                if (w.FindContainer(key) == null) w.Containers.Add(box);
                lines.Insert(0, "You search and find something left behind.");
            }

            return CommandResult.Ok(string.Join(" ", lines));
        }
    }
}
