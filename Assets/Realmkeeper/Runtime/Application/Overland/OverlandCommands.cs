using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Overland
{
    /// <summary>
    /// Phase 23 (ADR-0110): go out into the country. From a settlement: just outside it, on its road. From the world map:
    /// beside the last settlement visited (or the region's first). The settlement or place the player then enters from the
    /// country returns them to it when they leave.
    /// </summary>
    public sealed class EnterOverlandCommand : ICommand
    {
        public EnterOverlandCommand() { }

        /// <summary>From the world map: out into the country by <paramref name="by"/>, a settlement of the player's region (as entering one from the map).</summary>
        public EnterOverlandCommand(SettlementId by) { By = by; }

        /// <summary>The settlement to walk out by; empty means the one the player is in or last visited.</summary>
        public SettlementId By { get; }
    }

    /// <summary>Phase 23: leave the country for the world map (the surface only; the region is kept for the map and journeys).</summary>
    public sealed class LeaveOverlandCommand : ICommand
    {
    }

    /// <summary>Phase 24 (ADR-0113): take the halted journey up again from where the player left the road (within <see cref="OverlandRoads.RejoinMetres"/>).</summary>
    public sealed class RejoinJourneyCommand : ICommand
    {
    }

    /// <summary>Phase 24 (ADR-0113): mark a known destination on the compass ("settlement:{id}", "place:{id}", "landmark:{id}", "gateway:{id}", "journey"); null clears it.</summary>
    public sealed class SetWaypointCommand : ICommand
    {
        public SetWaypointCommand(string key) { Key = key; }
        public string Key { get; }
    }

    /// <summary>Phase 23: records where the player stands in the country (the clock is advanced separately, by the travel rules).</summary>
    public sealed class WalkOverlandCommand : ICommand
    {
        public WalkOverlandCommand(LocalPose pose) { Pose = pose; }
        public LocalPose Pose { get; }
    }

    /// <summary>Phase 23: go into a settlement standing in the country (the player must be at its approach).</summary>
    public sealed class EnterSettlementFromOverlandCommand : ICommand
    {
        public EnterSettlementFromOverlandCommand(SettlementId settlementId) { SettlementId = settlementId; }
        public SettlementId SettlementId { get; }
    }

    /// <summary>Phase 23: take the way on into a neighbouring region (the player must be at the exit; the crossing must be open).</summary>
    public sealed class CrossRegionCommand : ICommand
    {
        public CrossRegionCommand(RegionId to) { To = to; }
        public RegionId To { get; }
    }

    /// <summary>Phase 23 (ADR-0111): pass through an open gateway to its other end, in another realm.</summary>
    public sealed class UseGatewayCommand : ICommand
    {
        public UseGatewayCommand(string gatewayId) { GatewayId = gatewayId; }
        public string GatewayId { get; }
    }

    /// <summary>
    /// Phase 23 (ADR-0111): set a gateway's state. This is the campaign's hook (restoring the containment structures opens
    /// or seals the gateways, Campaign Bible §6); the game does not open a gateway by itself.
    /// </summary>
    public sealed class SetGatewayStateCommand : ICommand
    {
        public SetGatewayStateCommand(string gatewayId, GatewayState state)
        {
            GatewayId = gatewayId;
            State = state;
        }

        public string GatewayId { get; }
        public GatewayState State { get; }
    }

    public static class OverlandCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<EnterOverlandCommand>(new EnterHandler());
            commands.Register<WalkOverlandCommand>(new WalkHandler());
            commands.Register<LeaveOverlandCommand>(new LeaveHandler());
            commands.Register<CrossRegionCommand>(new CrossHandler());
            commands.Register<UseGatewayCommand>(new GatewayHandler());
            commands.Register<SetGatewayStateCommand>(new GatewayStateHandler());
            commands.Register<RejoinJourneyCommand>(new RejoinHandler());
            commands.Register<SetWaypointCommand>(new WaypointHandler());
            LandmarkCommandHandlers.Register(commands);
            OverlandEncounters.Register(commands);
        }

        public static CommandResult Ready(CommandContext context, out PlayerCharacterData player)
        {
            player = null;
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            player = context.Committed.Player;
            if (player == null) return CommandResult.NotFound("This world has no player character.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("A fight is going on.");
            return null;
        }

        /// <summary>The site of this kind and id in the player's country, when the player stands within reach and it can be used; else null with the reason.</summary>
        public static OverlandSite SiteInReach(CommandContext context, PlayerCharacterData player, OverlandSiteKind kind, string id, out CommandResult refused)
        {
            refused = null;
            if (!player.IsOverland) { refused = CommandResult.InvalidState($"{player.Name} is not out in the country."); return null; }
            var terrain = context.Session.GetOverlandTerrain(player.RealmId, player.RegionId);
            var site = terrain == null ? null : OverlandQueries.Sites(context.Committed, RealmIds.IsSurface(player.RealmId) ? RealmIds.Surface : player.RealmId, player.RegionId, terrain).Find(s => s.Kind == kind && s.Id == id);
            if (site == null) { refused = CommandResult.NotFound("There is no such way here."); return null; }
            var dx = player.Pose.X - site.X;
            var dz = player.Pose.Z - site.Z;
            if (dx * dx + dz * dz > OverlandQueries.ReachMetres * OverlandQueries.ReachMetres * 4) { refused = CommandResult.InvalidState($"{site.Name} is {Math.Sqrt(dx * dx + dz * dz):0} m away: walk there first."); return null; }
            if (!site.Usable) { refused = CommandResult.InvalidState(site.Why ?? "That way is closed."); return null; }
            return site;
        }

        /// <summary>A pose at a point of a region, on the ground, facing <paramref name="yaw"/>.</summary>
        private static LocalPose PoseAt(OverlandTerrain terrain, float x, float z, float yaw) => new LocalPose(x, terrain.HeightAt(x, z), z, yaw);

        private sealed class EnterHandler : ICommandHandler<EnterOverlandCommand>
        {
            public CommandResult Handle(EnterOverlandCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (player.IsOverland) return CommandResult.InvalidState($"{player.Name} is already out in the country.");
                if (player.IsInPlace) return CommandResult.InvalidState($"Leave {context.Committed.FindPlace(player.PlaceId)?.Name ?? "the place"} first.");
                var world = context.Committed;
                var journey = world.Journey;

                // Phase 24 (ADR-0113): on the road (a journey under way, halted, or a road position), the country is entered
                // where the party stands on the road: the journey halts there (its time and place kept), and can be taken up again.
                if (command.By.IsEmpty && Atlas.TravelCommandHandlers.OnTheRoad(world) && !player.IsInSettlement)
                {
                    if (journey.EncounterId != null && journey.EncounterId.StartsWith("threat:", StringComparison.Ordinal))
                        return CommandResult.InvalidState("Outlaws bar the road: deal with them first.");
                    var (seg, from, metres) = Atlas.TravelCommandHandlers.Position(world);
                    if (seg != null && OverlandRoads.PointOf(context.Session, world, seg, from, metres, out var onRegion, out var rx, out var rz))
                    {
                        var rt = context.Session.GetOverlandTerrain(RealmIds.Surface, onRegion);
                        var ew = context.Edit();
                        var ep = ew.Player;
                        if (ew.Journey.Status == JourneyStatus.Travelling) { ew.Journey.Status = JourneyStatus.Halted; ew.Journey.Reason = "you went on foot"; }
                        ep.LocationKind = CharacterLocationKind.Overland;
                        ep.RealmId = string.Empty;
                        ep.RegionId = onRegion;
                        ep.SettlementId = default;
                        ep.ReturnToOverland = false;
                        ep.Pose = PoseAt(rt, rx, rz, ep.Pose.Yaw);
                        if (ew.Journey.Status == JourneyStatus.Halted || ew.Journey.Status == JourneyStatus.Interrupted) ep.Waypoint ??= "journey";
                        Discover(context.Session, ew, ep, rt);
                        var dest = ew.FindSettlement(ew.Journey.DestinationId)?.Name;
                        return CommandResult.Ok(ew.Journey.Status == JourneyStatus.Abandoned
                            ? $"{ep.Name} leaves the road on foot."
                            : $"{ep.Name} leaves the road on foot. The journey to {dest} waits where you left it.");
                    }
                }

                if (journey != null && journey.Status != JourneyStatus.Arrived && journey.Status != JourneyStatus.Abandoned && !player.IsInSettlement)
                    return CommandResult.InvalidState($"{player.Name} is on a journey: arrive or give it up first.");

                SettlementData settlement;
                if (!command.By.IsEmpty)
                {
                    settlement = world.FindSettlement(command.By);
                    if (settlement == null) return CommandResult.Invalid("No such settlement.");
                    if (player.IsInSettlement && player.SettlementId != command.By)
                        return CommandResult.InvalidState($"{player.Name} must leave {world.FindSettlement(player.SettlementId)?.Name} first.");
                    // As entering a settlement from the map (ADR-0078): another region is reached by travelling there.
                    if (world.Regions.Count > 1 && settlement.RegionId != Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(world))
                        return CommandResult.InvalidState(settlement.Name + " lies in " + world.FindRegion(settlement.RegionId)?.Name + ": travel there first.");
                    if (journey != null && journey.Status == JourneyStatus.Abandoned && journey.SegmentIndex < journey.Segments.Count && (journey.MetresIntoSegment > 0 || journey.Nodes[journey.SegmentIndex] != settlement.SettlementId))
                        return CommandResult.InvalidState(player.Name + " is out on the road: travel to " + settlement.Name + " first.");
                }
                else settlement = world.FindSettlement(player.IsInSettlement ? player.SettlementId : player.LastSettlementId);

                if (settlement == null)
                {
                    var region = world.Regions.Count > 0 ? world.Regions[0].RegionId : default;
                    settlement = world.Settlements.Find(s => s.RegionId == region);
                }

                if (settlement == null) return CommandResult.InvalidState("There is no country to go out into.");
                var terrain = context.Session.GetOverlandTerrain(RealmIds.Surface, settlement.RegionId);
                if (terrain == null) return CommandResult.InvalidState("The country of " + settlement.Name + " has no map.");
                var (x, z) = OverlandQueries.Gate(terrain, settlement.Position); // at its way in, outside the town
                var w = context.Edit();
                var p = w.Player;
                p.LocationKind = CharacterLocationKind.Overland;
                p.RealmId = string.Empty;
                p.RegionId = settlement.RegionId;
                p.SettlementId = default;
                p.LastSettlementId = settlement.SettlementId;
                p.ReturnToOverland = false;
                p.Pose = PoseAt(terrain, x, z, p.Pose.Yaw);
                Discover(context.Session, w, p, terrain);
                return CommandResult.Ok($"{p.Name} goes out into the country by {settlement.Name}.");
            }
        }

        private sealed class LeaveHandler : ICommandHandler<LeaveOverlandCommand>
        {
            public CommandResult Handle(LeaveOverlandCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (!player.IsOverland) return CommandResult.InvalidState($"{player.Name} is not out in the country.");
                if (!RealmIds.IsSurface(player.RealmId)) return CommandResult.InvalidState("There is no map of this realm: go back through the gateway.");
                var world = context.Committed;
                var terrain = context.Session.GetOverlandTerrain(RealmIds.Surface, player.RegionId);
                var target = MapPoint(context.Session, world, player, terrain);
                var w = context.Edit();
                var p = w.Player;
                p.LocationKind = CharacterLocationKind.WorldMap;
                p.ReturnToOverland = false;
                if (p.Waypoint == "journey") p.Waypoint = null;

                // Phase 24 (ADR-0113): the map shows only positions a journey can start from. Back at the halted journey's
                // place, it waits there; by a town, the player is at the town; else at the nearest road, as a road position.
                switch (target.Kind)
                {
                    case MapPointKind.Journey:
                        return CommandResult.Ok($"{p.Name} is back on the road. The journey waits.");
                    case MapPointKind.Settlement:
                        p.LastSettlementId = target.Settlement;
                        if (w.Journey != null && w.Journey.Status != JourneyStatus.Arrived)
                        {
                            w.Journey.Status = JourneyStatus.Abandoned;
                            w.Journey.Reason = "you left the road";
                            w.Journey.SegmentIndex = w.Journey.Segments.Count;
                        }

                        return CommandResult.Ok($"{p.Name} turns to the map, by {w.FindSettlement(target.Settlement)?.Name}.");
                    default:
                    {
                        var now = QuestClock.Now(w);
                        var other = target.Segment.Other(target.From);
                        var j = new JourneyData
                        {
                            JourneyId = "journey:foot:" + now.ToString(System.Globalization.CultureInfo.InvariantCulture), OriginId = target.From, DestinationId = other,
                            Mode = TravelMode.OnFoot, Status = JourneyStatus.Abandoned, StartedAt = now, Reason = "you came back to the road on foot", MetresIntoSegment = target.Metres,
                        };
                        j.Nodes.Add(target.From);
                        j.Nodes.Add(other);
                        j.Segments.Add(target.Segment.RouteId);
                        w.Journey = j;
                        return CommandResult.Ok($"{p.Name} makes for the road and turns to the map. Plan a journey from here, or walk on.");
                    }
                }
            }
        }

        private sealed class WalkHandler : ICommandHandler<WalkOverlandCommand>
        {
            public CommandResult Handle(WalkOverlandCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (!player.IsOverland) return CommandResult.InvalidState($"{player.Name} is not out in the country.");
                if (!command.Pose.IsFinite) return CommandResult.Invalid("The position is not a number.");
                var terrain = context.Session.GetOverlandTerrain(player.RealmId, player.RegionId);
                if (terrain == null) return CommandResult.InvalidState("This country has no map.");
                if (!OverlandScale.Inside(terrain.Map, command.Pose.X, command.Pose.Z)) return CommandResult.Invalid("That is outside the region: take a way on.");
                // The ground decides; a position on impassable ground (a mountain, a lake) is refused and the old one kept.
                if (!terrain.Walkable(command.Pose.X, command.Pose.Z)) return CommandResult.InvalidState("No one can stand there.");
                // Phase 24 (ADR-0115): a ford in flood cannot be waded (a bridge still crosses).
                var cell = terrain.CellAt(command.Pose.X, command.Pose.Z);
                if (GroundConditions.IsFord(cell) && RealmIds.IsSurface(player.RealmId) && GroundConditions.Of(context.Committed, player.RegionId, context.Committed.MinuteOfDay).FordsFlooded)
                    return CommandResult.InvalidState("The ford is in flood: the water is too high to wade. Cross by a bridge, or wait for it to fall.");
                var w = context.Edit();
                var p = w.Player;
                p.Pose = command.Pose;
                var found = Discover(context.Session, w, p, terrain);
                var met = OverlandEncounters.Check(context.Session, w, p); // Phase 24 (ADR-0114): a half-hour bucket may bring an encounter
                var said = found.Count == 0 ? null : "Found: " + string.Join(", ", found) + ".";
                if (met != null) said = (said == null ? string.Empty : said + " ") + met.Text;
                return CommandResult.Ok(said);
            }
        }

        /// <summary>
        /// Unknown places, gateways and landmarks within sight become known, as walking up to them finds them. Phase 24: sight
        /// is shortened by fog, heavy rain and snow (<see cref="GroundConditions.SightFactor"/>) and by night.
        /// </summary>
        private static List<string> Discover(WorldSession session, WorldData w, PlayerCharacterData p, OverlandTerrain terrain)
        {
            var found = new List<string>();
            var weather = RealmIds.IsSurface(p.RealmId) ? GroundConditions.Of(w, p.RegionId, w.MinuteOfDay) : GroundConditions.Calm;
            var reach = SightMetres(w, p, terrain, weather);
            foreach (var l in Landmarks.InRegion(session, w, RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId, p.RegionId))
            {
                if (Landmarks.IsFound(w, l.Id)) continue;
                var dx = l.X - p.Pose.X;
                var dz = l.Z - p.Pose.Z;
                if (dx * dx + dz * dz > reach * reach) continue;
                var s = Landmarks.State(w, l.Id);
                s.Found = true;
                s.FoundAt = QuestClock.Now(w);
                found.Add(l.Name.ToLowerInvariant());
            }
            bool Near(MapCoord c)
            {
                var (x, z) = OverlandScale.CellCentre(c);
                var dx = x - p.Pose.X;
                var dz = z - p.Pose.Z;
                return dx * dx + dz * dz <= reach * reach;
            }

            if (RealmIds.IsSurface(p.RealmId))
            {
                foreach (var place in w.Places)
                {
                    if (place.RegionId != p.RegionId || place.ParentKind != PlaceParentKind.Region || place.Category == PlaceCategory.Interior) continue;
                    if (place.Discovery >= PlaceDiscovery.Discovered || !Near(place.Cell)) continue;
                    place.Discovery = PlaceDiscovery.Discovered;
                    place.DiscoveredAt = QuestClock.Now(w);
                    found.Add(place.Name);
                }

                var region = w.FindRegion(p.RegionId);
                if (region != null && region.Discovery != RegionDiscovery.Visited) region.Discovery = RegionDiscovery.Visited;
            }
            else
            {
                var realm = w.FindRealm(p.RealmId);
                if (realm != null) { realm.Visited = true; var r = realm.FindRegion(p.RegionId); if (r != null) r.Visited = true; }
            }

            foreach (var g in w.Gateways)
            {
                if (g.Discovered || !g.TryEndIn(RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId, p.RegionId, out var end, out _) || !Near(end.Cell)) continue;
                g.Discovered = true;
                found.Add(g.Name);
            }

            return found;
        }

        /// <summary>
        /// Phase 24: how far the player notices things now: <see cref="OverlandQueries.NoticeMetres"/>, more from high ground,
        /// less in fog, heavy rain or snow, half at night.
        /// </summary>
        public static float SightMetres(WorldData w, PlayerCharacterData p, OverlandTerrain terrain, GroundConditions weather) =>
            OverlandQueries.NoticeMetres * (1f + terrain.CellAt(p.Pose.X, p.Pose.Z).SightBonus) * (weather?.SightFactor ?? 1f) * (TravelRules.IsNight(w.MinuteOfDay) ? 0.5f : 1f);

        public enum MapPointKind
        {
            /// <summary>At the halted journey's place on the road.</summary>
            Journey = 0,

            /// <summary>By a settlement (at its way in).</summary>
            Settlement = 1,

            /// <summary>On the nearest road, as a road position.</summary>
            Road = 2,
        }

        public struct MapPointTarget
        {
            public MapPointKind Kind;
            public SettlementId Settlement;
            public RouteSegmentData Segment;
            public SettlementId From;
            public int Metres;
            public float X;
            public float Z;
        }

        /// <summary>Within this distance of a settlement's way in, turning to the map puts the player at that settlement.</summary>
        public const float SettlementNearMetres = 400f;

        /// <summary>
        /// Phase 24 (ADR-0113): where turning to the map puts the player, and the point in the country they walk to first (the
        /// presentation charges that walk's time): the halted journey's place when within <see cref="OverlandRoads.RejoinMetres"/>,
        /// a settlement when its way in is nearer than any road (or within <see cref="SettlementNearMetres"/>), else the nearest road.
        /// </summary>
        public static MapPointTarget MapPoint(WorldSession session, WorldData w, PlayerCharacterData p, OverlandTerrain terrain)
        {
            var t = new MapPointTarget { Kind = MapPointKind.Settlement, Settlement = p.LastSettlementId, X = p.Pose.X, Z = p.Pose.Z };
            if (terrain == null) return t;
            if (OverlandRoads.HaltedPoint(session, w, out var hx, out var hz) && Dist(p, hx, hz) <= OverlandRoads.RejoinMetres)
                return new MapPointTarget { Kind = MapPointKind.Journey, X = hx, Z = hz };
            var bestSettle = float.MaxValue;
            foreach (var s in w.Settlements)
            {
                if (s.RegionId != p.RegionId) continue;
                var (gx, gz) = OverlandQueries.Gate(terrain, s.Position);
                var d = Dist(p, gx, gz);
                if (d < bestSettle) { bestSettle = d; t.Settlement = s.SettlementId; t.X = gx; t.Z = gz; }
            }

            if (OverlandRoads.NearestRoad(w, terrain, p.Pose.X, p.Pose.Z, out var seg, out var from, out var metres, out var rx, out var rz))
            {
                var roadDist = Dist(p, rx, rz);
                if (roadDist < bestSettle && bestSettle > SettlementNearMetres)
                    return new MapPointTarget { Kind = MapPointKind.Road, Segment = seg, From = from, Metres = metres, X = rx, Z = rz };
            }

            return t;
        }

        private static float Dist(PlayerCharacterData p, float x, float z) => (float)Math.Sqrt((x - p.Pose.X) * (x - p.Pose.X) + (z - p.Pose.Z) * (z - p.Pose.Z));

        private sealed class RejoinHandler : ICommandHandler<RejoinJourneyCommand>
        {
            public CommandResult Handle(RejoinJourneyCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (!player.IsOverland) return CommandResult.InvalidState($"{player.Name} is not out in the country.");
                var world = context.Committed;
                var j = world.Journey;
                if (j == null || (j.Status != JourneyStatus.Halted && j.Status != JourneyStatus.Interrupted)) return CommandResult.InvalidState("There is no journey waiting on the road.");
                if (j.EncounterId != null && j.EncounterId.StartsWith("threat:", StringComparison.Ordinal)) return CommandResult.InvalidState("Outlaws bar the road: deal with them first.");
                if (!OverlandRoads.HaltedPoint(context.Session, world, out var hx, out var hz)) return CommandResult.InvalidState("The journey waits in another region.");
                var d = Dist(player, hx, hz);
                if (d > OverlandRoads.RejoinMetres) return CommandResult.InvalidState($"The place you left the road is {Waypoints.Distance(d)} away: walk back to it first.");
                if (j.SegmentIndex < j.Segments.Count && !(j.Back && j.SegmentIndex == 0) && world.FindRoute(j.Segments[j.SegmentIndex]) is RouteSegmentData seg && !seg.Passable)
                    return CommandResult.InvalidState("The road ahead is closed: choose another way from the map.");
                var w = context.Edit();
                var p = w.Player;
                p.LocationKind = CharacterLocationKind.WorldMap;
                p.ReturnToOverland = false;
                if (p.Waypoint == "journey") p.Waypoint = null;
                w.Journey.Status = JourneyStatus.Travelling;
                w.Journey.Reason = null;
                w.Journey.EncounterId = null;
                return CommandResult.Ok($"{p.Name} takes to the road again, for {w.FindSettlement(w.Journey.DestinationId)?.Name}.");
            }
        }

        private sealed class WaypointHandler : ICommandHandler<SetWaypointCommand>
        {
            public CommandResult Handle(SetWaypointCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (string.IsNullOrEmpty(command.Key))
                {
                    if (player.Waypoint == null) return CommandResult.InvalidState("Nothing is marked.");
                    context.Edit().Player.Waypoint = null;
                    return CommandResult.Ok("The mark is cleared.");
                }

                // Only what the player knows can be marked: an undiscovered place is never revealed by trying.
                var target = Waypoints.Resolve(context.Session, context.Committed, command.Key, out var why);
                if (target == null) return CommandResult.InvalidState(why);
                context.Edit().Player.Waypoint = command.Key;
                return CommandResult.Ok($"Marked: {target.Value.name}.");
            }
        }

        private sealed class CrossHandler : ICommandHandler<CrossRegionCommand>
        {
            public CommandResult Handle(CrossRegionCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                if (!RealmIds.IsSurface(player.RealmId)) return CommandResult.InvalidState("There is no road on from here.");
                var site = SiteInReach(context, player, OverlandSiteKind.RegionExit, command.To.ToString(), out refused);
                if (site == null) return refused;
                var world = context.Committed;
                var from = world.FindRegion(player.RegionId);
                var to = world.FindRegion(command.To);
                var terrain = context.Session.GetOverlandTerrain(RealmIds.Surface, to.RegionId);
                if (terrain == null) return CommandResult.InvalidState(to.Name + " has no map.");
                // Arrive at the matching way on, on the far side.
                var back = OverlandQueries.Exits(world, to.RegionId, terrain).Find(e => e.Id == from.RegionId.ToString());
                if (back == null) return CommandResult.InvalidState("No road leads back from " + to.Name + ".");
                var w = context.Edit();
                var p = w.Player;
                p.RegionId = to.RegionId;
                // Phase 24 (ADR-0113): leaving the region on foot leaves a halted journey behind; plan anew from the map.
                if (w.Journey != null && (w.Journey.Status == JourneyStatus.Halted || w.Journey.Status == JourneyStatus.Interrupted))
                {
                    w.Journey.Status = JourneyStatus.Abandoned;
                    w.Journey.Reason = "you left the road for " + to.Name;
                    w.Journey.SegmentIndex = w.Journey.Segments.Count;
                }

                if (p.Waypoint == "journey") p.Waypoint = null;
                var inward = Inward(terrain, back.Cell);
                p.Pose = PoseAt(terrain, inward.x, inward.z, p.Pose.Yaw);
                Discover(context.Session, w, p, terrain);
                return CommandResult.Ok($"{p.Name} crosses from {from.Name} into {to.Name}.");
            }
        }

        /// <summary>A point a few cells in from an edge cell, on walkable ground (so arriving does not stand on the way back).</summary>
        private static (float x, float z) Inward(OverlandTerrain terrain, MapCoord edge)
        {
            var map = terrain.Map;
            var dx = edge.X <= 1 ? 1 : edge.X >= map.Width - 2 ? -1 : 0;
            var dz = edge.Z <= 1 ? 1 : edge.Z >= map.Depth - 2 ? -1 : 0;
            for (var i = 3; i >= 0; i--)
            {
                var c = new MapCoord(edge.X + dx * i, edge.Z + dz * i);
                if (map.InBounds(c) && terrain.Cell(c.X, c.Z).Passable) return OverlandScale.CellCentre(c);
            }

            return OverlandScale.CellCentre(edge);
        }

        private sealed class GatewayHandler : ICommandHandler<UseGatewayCommand>
        {
            public CommandResult Handle(UseGatewayCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var site = SiteInReach(context, player, OverlandSiteKind.Gateway, command.GatewayId, out refused);
                if (site == null) return refused;
                var g = context.Committed.FindGateway(command.GatewayId);
                var here = RealmIds.IsSurface(player.RealmId) ? RealmIds.Surface : player.RealmId;
                if (!g.TryEndIn(here, player.RegionId, out _, out var other)) return CommandResult.InvalidState("This gateway does not stand here.");
                var terrain = context.Session.GetOverlandTerrain(other.RealmId, other.RegionId);
                if (terrain == null) return CommandResult.InvalidState("The far side has no map.");
                var w = context.Edit();
                var p = w.Player;
                p.RealmId = RealmIds.IsSurface(other.RealmId) ? string.Empty : other.RealmId;
                p.RegionId = other.RegionId;
                var (x, z) = OverlandQueries.Approach(terrain, other.Cell, 2);
                p.Pose = PoseAt(terrain, x + 6f, z, p.Pose.Yaw);
                if (!terrain.Walkable(p.Pose.X, p.Pose.Z)) p.Pose = PoseAt(terrain, x, z, p.Pose.Yaw);
                Discover(context.Session, w, p, terrain);
                var realm = w.FindRealm(RealmIds.IsSurface(other.RealmId) ? RealmIds.Surface : other.RealmId);
                return CommandResult.Ok($"{p.Name} passes through {g.Name} into {realm?.Name ?? "another world"}.");
            }
        }

        private sealed class GatewayStateHandler : ICommandHandler<SetGatewayStateCommand>
        {
            public CommandResult Handle(SetGatewayStateCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var g = context.Committed.FindGateway(command.GatewayId);
                if (g == null) return CommandResult.NotFound("There is no such gateway.");
                if (!Enum.IsDefined(typeof(GatewayState), command.State)) return CommandResult.Invalid("Unknown gateway state.");
                if (g.State == command.State) return CommandResult.InvalidState($"{g.Name} is already {command.State.ToString().ToLowerInvariant()}.");
                var p = context.Committed.Player;
                if (command.State != GatewayState.Open && p != null && !RealmIds.IsSurface(p.RealmId))
                    return CommandResult.InvalidState("The gateway cannot close while the player is on the far side.");
                context.Edit().FindGateway(command.GatewayId).State = command.State;
                return CommandResult.Ok($"{g.Name} is now {command.State.ToString().ToLowerInvariant()}.");
            }
        }
    }
}
