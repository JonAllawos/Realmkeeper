using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Atlas
{
    public sealed class AtlasFactionShare
    {
        public string Name { get; set; }
        public int Settlements { get; set; }
        public int SharePercent { get; set; }
    }

    public sealed class AtlasRegionView
    {
        public RegionId RegionId { get; set; }
        public string Name { get; set; }
        public RegionKind Kind { get; set; }
        public RegionDiscovery Discovery { get; set; }
        public SimulationTier Tier { get; set; }
        public int AtlasX { get; set; }
        public int AtlasZ { get; set; }
        public int Width { get; set; }
        public int Depth { get; set; }
        public List<string> Landmarks { get; } = new List<string>();
        public int Population { get; set; }
        public int Settlements { get; set; }
        public int Prosperity { get; set; }
        public int Stability { get; set; }

        /// <summary>Days of food in the region's stores at today's eating (all its settlements together).</summary>
        public int FoodDays { get; set; }
        public int Shortages { get; set; }
        public int CaravansOnRoad { get; set; }

        /// <summary>Outlaws on its roads (as far as the player has seen or heard).</summary>
        public int KnownOutlaws { get; set; }
        public List<AtlasFactionShare> Factions { get; } = new List<AtlasFactionShare>();
    }

    public sealed class AtlasSettlementView
    {
        public SettlementId SettlementId { get; set; }
        public string Name { get; set; }
        public SettlementType Type { get; set; }
        public RegionId RegionId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public string Faction { get; set; }
        public FactionId FactionId { get; set; }
        public int Population { get; set; }
        public int Prosperity { get; set; }
        public int Stability { get; set; }
        public bool Shortage { get; set; }
        public bool IsHere { get; set; }
    }

    public sealed class AtlasRouteView
    {
        public string RouteId { get; set; }
        public SettlementId A { get; set; }
        public SettlementId B { get; set; }
        public RouteKind Kind { get; set; }
        public bool Crossing { get; set; }
        public double Km { get; set; }
        public double Hours { get; set; }
        public RouteCondition Condition { get; set; }
        public bool HasBridge { get; set; }
        public BridgeState Bridge { get; set; }
        public bool Passable { get; set; }
        public float AX { get; set; }
        public float AZ { get; set; }
        public float BX { get; set; }
        public float BZ { get; set; }
    }

    public sealed class AtlasGroupView
    {
        public string GroupId { get; set; }
        public TravelGroupKind Kind { get; set; }
        public string Name { get; set; }
        public string Faction { get; set; }
        public string Leader { get; set; }
        public int Members { get; set; }
        public int Strength { get; set; }
        public TravelGroupState State { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cargo { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public List<string> History { get; } = new List<string>();
    }

    public sealed class AtlasEventView
    {
        public string EventId { get; set; }
        public WorldEventType Type { get; set; }
        public string Telling { get; set; }
        public EventKnowledge Known { get; set; }

        /// <summary>What the player believes: the state and severity as they heard or saw it, "as of" when.</summary>
        public WorldEventState KnownState { get; set; }
        public int KnownSeverity { get; set; }
        public string KnownFrom { get; set; }
        public string KnownAt { get; set; }

        /// <summary>True when the player can tell their news is old (they are where it happens and see otherwise).</summary>
        public bool Outdated { get; set; }
        public List<string> History { get; } = new List<string>();

        /// <summary>The settlements it touches: its own, or the two ends of its road.</summary>
        public List<SettlementId> Touches { get; } = new List<SettlementId>();
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class JourneyView
    {
        public string Destination { get; set; }
        public JourneyStatus Status { get; set; }
        public string Reason { get; set; }
        public bool ThreatPending { get; set; }
        public int Toll { get; set; }
        public double HoursLeft { get; set; }
        public double KmLeft { get; set; }
        public int PercentDone { get; set; }
        public string NextStop { get; set; }
        public List<string> Stops { get; } = new List<string>();
        public int HoursTravelled { get; set; }
    }

    public sealed class TravelOptionView
    {
        public int Index { get; set; }
        public double Hours { get; set; }
        public double Km { get; set; }
        public string Duration { get; set; }

        /// <summary>When the party would arrive walking in daylight (06:00–20:00), from now.</summary>
        public string Arrival { get; set; }
        public List<string> Via { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
        public List<string> Segments { get; } = new List<string>();
    }

    public sealed class AtlasView
    {
        public List<AtlasRegionView> Regions { get; } = new List<AtlasRegionView>();
        public List<AtlasSettlementView> Settlements { get; } = new List<AtlasSettlementView>();
        public List<AtlasRouteView> Routes { get; } = new List<AtlasRouteView>();
        public List<AtlasGroupView> Groups { get; } = new List<AtlasGroupView>();
        public List<AtlasEventView> Events { get; } = new List<AtlasEventView>();
        public float PlayerX { get; set; }
        public float PlayerZ { get; set; }
        public RegionId PlayerRegion { get; set; }
        public JourneyView Journey { get; set; }
        public List<string> Party { get; } = new List<string>();
        public string Clock { get; set; }
    }

    /// <summary>
    /// Phase 16: the world atlas as the player may see it, read from the one world. Regions they have not heard of are
    /// blank; settlements and roads show once their region is known; groups only where the player can see them (their
    /// own region); events only as far as the player knows them (rumoured or confirmed, with when and from whom, and
    /// whether the news is old). Coordinates are atlas cells (a region is 128 across).
    /// </summary>
    public static class AtlasQueries
    {
        public const int RegionCells = 128;

        public static (float x, float z) At(WorldData w, SettlementData s)
        {
            var r = w.FindRegion(s.RegionId);
            return ((r?.AtlasX ?? 0) * RegionCells + s.Position.X, (r?.AtlasZ ?? 0) * RegionCells + s.Position.Z);
        }

        public static (float x, float z) Along(WorldData w, RouteSegmentData r, SettlementId from, double fraction)
        {
            var a = w.FindSettlement(from);
            var b = w.FindSettlement(r.Other(from));
            if (a == null || b == null) return (0, 0);
            var pa = At(w, a);
            var pb = At(w, b);
            var t = (float)Math.Max(0, Math.Min(1, fraction));
            return (pa.x + (pb.x - pa.x) * t, pa.z + (pb.z - pa.z) * t);
        }

        public static AtlasView View(WorldData w)
        {
            var v = new AtlasView();
            if (w == null) return v;
            var now = QuestClock.Now(w);
            v.Clock = TravelRules.Clock(now);
            v.PlayerRegion = SimulationTiers.PlayersRegion(w);
            var known = new HashSet<RegionId>();
            foreach (var r in w.Regions)
            {
                var rv = new AtlasRegionView
                {
                    RegionId = r.RegionId, Name = r.Discovery == RegionDiscovery.Unknown ? "Uncharted lands" : r.Name, Kind = r.Kind, Discovery = r.Discovery,
                    Tier = SimulationTiers.TierOf(w, r.RegionId), AtlasX = r.AtlasX, AtlasZ = r.AtlasZ, Width = r.Width, Depth = r.Depth,
                };
                if (r.Discovery != RegionDiscovery.Unknown) { rv.Landmarks.AddRange(r.Landmarks); known.Add(r.RegionId); }
                if (r.Discovery >= RegionDiscovery.Discovered || w.Regions.Count == 1) Economy(w, r, rv);
                v.Regions.Add(rv);
            }

            if (w.Regions.Count == 1 && w.Regions.Count > 0) known.Add(w.Regions[0].RegionId);
            var p = w.Player;
            foreach (var s in w.Settlements)
            {
                if (!known.Contains(s.RegionId)) continue;
                var (x, z) = At(w, s);
                v.Settlements.Add(new AtlasSettlementView
                {
                    SettlementId = s.SettlementId, Name = s.Name, Type = s.SettlementType, RegionId = s.RegionId, X = x, Z = z, Faction = w.FindFaction(s.FactionId)?.Name, FactionId = s.FactionId,
                    Population = s.Population, Prosperity = s.Prosperity, Stability = s.Stability, Shortage = s.IsInFoodShortage, IsHere = p != null && (p.IsInSettlement ? p.SettlementId == s.SettlementId : !p.IsInPlace && !TravelCommandHandlers.OnTheRoad(w) && p.LastSettlementId == s.SettlementId),
                });
            }

            foreach (var r in w.Routes)
            {
                var a = w.FindSettlement(r.A);
                var b = w.FindSettlement(r.B);
                if (a == null || b == null || !known.Contains(a.RegionId) || !known.Contains(b.RegionId)) continue;
                var pa = At(w, a);
                var pb = At(w, b);
                v.Routes.Add(new AtlasRouteView
                {
                    RouteId = r.RouteId, A = r.A, B = r.B, Kind = r.Kind, Crossing = r.IsCrossing, Km = TravelRules.Km(r), Hours = TravelRules.Hours(r), Condition = r.Condition,
                    HasBridge = r.HasBridge, Bridge = r.Bridge, Passable = r.Passable, AX = pa.x, AZ = pa.z, BX = pb.x, BZ = pb.z,
                });
            }

            foreach (var g in w.Groups)
            {
                if (!g.IsActive) continue;
                var (x, z, region) = GroupAt(w, g, now);
                if (region != v.PlayerRegion) continue; // only what can be seen: the player's own region
                var gv = new AtlasGroupView
                {
                    GroupId = g.GroupId, Kind = g.Kind, Name = g.Name, Faction = w.FindFaction(g.FactionId)?.Name, Leader = w.FindNpc(g.LeaderNpcId)?.Name, Members = g.Members,
                    Strength = g.Strength, State = g.State, From = w.FindSettlement(g.OriginId)?.Name, To = w.FindSettlement(g.DestinationId)?.Name, X = x, Z = z,
                };
                if (g.Kind == TravelGroupKind.Caravan && w.Shipments.Find(s => s.ShipmentId == g.ShipmentId) is ShipmentData sh) gv.Cargo = sh.Quantity + " " + sh.ResourceTypeId;
                gv.History.AddRange(g.History);
                v.Groups.Add(gv);
            }

            foreach (var e in w.WorldEvents)
            {
                if (e.Known == EventKnowledge.Unknown) continue;
                var ev = new AtlasEventView
                {
                    EventId = e.EventId, Type = e.Type, Telling = WorldEvents.Telling(w, e), Known = e.Known, KnownState = e.KnownState, KnownSeverity = e.KnownSeverity, KnownFrom = e.KnownFrom,
                    KnownAt = TravelRules.Clock(e.KnownAt), Outdated = e.KnowledgeOutdated && Observable(w, e),
                };
                if (e.Known == EventKnowledge.Confirmed && !e.KnowledgeOutdated) ev.History.AddRange(e.History);
                if (w.FindSettlement(e.SettlementId) is SettlementData es) { (ev.X, ev.Z) = At(w, es); ev.Touches.Add(es.SettlementId); }
                else if (w.FindRoute(e.RouteId) is RouteSegmentData er) { (ev.X, ev.Z) = Along(w, er, er.A, 0.5); ev.Touches.Add(er.A); ev.Touches.Add(er.B); }
                v.Events.Add(ev);
            }

            (v.PlayerX, v.PlayerZ) = PlayerAt(w);
            v.Journey = Journey(w);
            foreach (var m in Simulation.Party.CompanionLoyalty.Present(w)) v.Party.Add(m.Name);
            return v;
        }

        /// <summary>Where the player can check their news: in the settlement it concerns, or on the road it concerns.</summary>
        private static bool Observable(WorldData w, WorldEventData e)
        {
            var p = w.Player;
            if (p == null) return false;
            if (p.IsInSettlement && e.SettlementId == p.SettlementId) return true;
            var j = w.Journey;
            return j != null && j.SegmentIndex < j.Segments.Count && j.Segments[j.SegmentIndex] == e.RouteId;
        }

        private static void Economy(WorldData w, RegionData r, AtlasRegionView rv)
        {
            long food = 0, eats = 0;
            int prosperity = 0, stability = 0, n = 0;
            foreach (var s in w.Settlements)
            {
                if (s.RegionId != r.RegionId) continue;
                n++;
                rv.Population += s.Population;
                prosperity += s.Prosperity;
                stability += s.Stability;
                food += s.ResourceInventory.Get(ResourceTypes.Food);
                eats += Math.Max(1, s.Population / 10);
                if (s.IsInFoodShortage) rv.Shortages++;
            }

            rv.Settlements = n;
            if (n > 0) { rv.Prosperity = prosperity / n; rv.Stability = stability / n; }
            rv.FoodDays = eats > 0 ? (int)(food / eats) : 0;
            foreach (var g in w.Groups)
            {
                if (!g.IsActive) continue;
                var o = w.FindSettlement(g.OriginId);
                if (g.Kind == TravelGroupKind.Caravan && o?.RegionId == r.RegionId) rv.CaravansOnRoad++;
                if (g.Kind == TravelGroupKind.Bandits && g.Strength > 0 && w.FindRoute(g.StationRouteId)?.RegionA == r.RegionId && w.WorldEvents.Exists(e => e.GroupId == g.GroupId && e.Known != EventKnowledge.Unknown))
                    rv.KnownOutlaws += g.Strength;
            }

            var counts = new Dictionary<FactionId, int>();
            foreach (var s in w.Settlements)
                if (s.RegionId == r.RegionId && !s.FactionId.IsEmpty) counts[s.FactionId] = (counts.TryGetValue(s.FactionId, out var c) ? c : 0) + s.Population;
            var keys = new List<FactionId>(counts.Keys);
            keys.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
            foreach (var f in keys)
            {
                var settlements = 0;
                foreach (var s in w.Settlements) if (s.RegionId == r.RegionId && s.FactionId == f) settlements++;
                rv.Factions.Add(new AtlasFactionShare { Name = w.FindFaction(f)?.Name, Settlements = settlements, SharePercent = rv.Population > 0 ? counts[f] * 100 / rv.Population : 0 });
            }
        }

        /// <summary>A group's place on the atlas now, and the region it is in.</summary>
        public static (float x, float z, RegionId region) GroupAt(WorldData w, TravelingGroupData g, long now)
        {
            if (g.Segments.Count == 0) { var o = w.FindSettlement(g.OriginId); var p = o == null ? (0f, 0f) : At(w, o); return (p.Item1, p.Item2, o?.RegionId ?? default); }
            if (g.State == TravelGroupState.Stationed || g.State == TravelGroupState.Halted || g.DueAt <= g.DepartedAt)
            {
                var r = w.FindRoute(g.StationRouteId ?? g.Segments[0]) ?? w.FindRoute(g.Segments[0]);
                var (x, z) = Along(w, r, r.A, g.State == TravelGroupState.Halted ? 0.25 : 0.5);
                return (x, z, r.RegionA);
            }

            var t = Math.Max(0, Math.Min(1, (now - g.DepartedAt) / (double)(g.DueAt - g.DepartedAt)));
            double total = 0;
            foreach (var id in g.Segments) total += w.FindRoute(id) is RouteSegmentData s ? TravelRules.Km(s) : 0;
            var target = total * t;
            for (var i = 0; i < g.Segments.Count; i++)
            {
                var s = w.FindRoute(g.Segments[i]);
                if (s == null) continue;
                var km = TravelRules.Km(s);
                if (target <= km || i == g.Segments.Count - 1)
                {
                    var from = g.Nodes.Count > i ? g.Nodes[i] : s.A;
                    var f = km <= 0 ? 0 : target / km;
                    var (x, z) = Along(w, s, from, f);
                    return (x, z, RoutePlanner.RegionAt(w, s, from, (int)(Math.Min(target, km) * 1000)));
                }

                target -= km;
            }

            var d = w.FindSettlement(g.DestinationId);
            var pd = d == null ? (0f, 0f) : At(w, d);
            return (pd.Item1, pd.Item2, d?.RegionId ?? default);
        }

        public static (float x, float z) PlayerAt(WorldData w)
        {
            var p = w.Player;
            if (p == null) return (0, 0);
            if (TravelCommandHandlers.OnTheRoad(w))
            {
                var (seg, from, metres) = TravelCommandHandlers.Position(w);
                if (seg != null) return Along(w, seg, from, metres / (TravelRules.Km(seg) * 1000));
            }

            var s = w.FindSettlement(p.IsInSettlement ? p.SettlementId : p.LastSettlementId) ?? (w.Journey != null ? w.FindSettlement(w.Journey.DestinationId) : null);
            if (s == null) return (0, 0);
            return At(w, s);
        }

        public static JourneyView Journey(WorldData w)
        {
            var j = w.Journey;
            if (j == null || j.Status == JourneyStatus.Arrived) return null;
            var v = new JourneyView
            {
                Destination = w.FindSettlement(j.DestinationId)?.Name, Status = j.Status, Reason = j.Reason, HoursTravelled = j.HoursTravelled,
                ThreatPending = j.EncounterId != null && j.EncounterId.StartsWith("threat:", StringComparison.Ordinal),
            };
            if (v.ThreatPending && w.FindGroup(j.EncounterId.Substring(7)) is TravelingGroupData band) v.Toll = TravelCommandHandlers.Toll(band);
            double total = 0, done = 0, hoursLeft = 0;
            for (var i = 0; i < j.Segments.Count; i++)
            {
                var s = w.FindRoute(j.Segments[i]);
                if (s == null) continue;
                var km = TravelRules.Km(s);
                total += km;
                if (i < j.SegmentIndex) done += km;
                else
                {
                    var part = i == j.SegmentIndex ? j.MetresIntoSegment / 1000.0 : 0;
                    done += part;
                    hoursLeft += TravelRules.Hours(s) * Math.Max(0, km - part) / Math.Max(0.001, km);
                }
            }

            v.KmLeft = Math.Max(0, total - done);
            v.HoursLeft = hoursLeft;
            v.PercentDone = total > 0 ? (int)Math.Round(done * 100 / total) : 0;
            for (var i = 0; i < j.Nodes.Count; i++) v.Stops.Add(w.FindSettlement(j.Nodes[i])?.Name);
            v.NextStop = j.SegmentIndex + 1 < j.Nodes.Count ? w.FindSettlement(j.Nodes[j.SegmentIndex + 1])?.Name : v.Destination;
            return v;
        }

        public static List<TravelOptionView> Options(WorldData w, SettlementId destination)
        {
            var list = new List<TravelOptionView>();
            var plans = TravelCommandHandlers.PlansFrom(w, destination);
            for (var i = 0; i < plans.Count; i++)
            {
                var p = plans[i];
                var o = new TravelOptionView { Index = i, Hours = p.Hours, Km = p.Km, Duration = TravelRules.Duration(p.Hours), Arrival = Arrival(w, p.Hours) };
                for (var k = 1; k < p.Nodes.Count - 1; k++) o.Via.Add(w.FindSettlement(p.Nodes[k])?.Name);
                o.Notes.AddRange(p.Notes);
                o.Segments.AddRange(p.Segments);
                list.Add(o);
            }

            return list;
        }

        /// <summary>Arrival if the party walks only in daylight (06:00–20:00) from now.</summary>
        public static string Arrival(WorldData w, double hours)
        {
            if (double.IsInfinity(hours)) return "never: the way is closed";
            var t = QuestClock.Now(w);
            var left = hours * 60;
            for (var guard = 0; guard < 400 && left > 0; guard++)
            {
                var m = (int)(t % 1440);
                if (TravelRules.IsNight(m)) { t += m >= TravelRules.NightFallsAt ? 1440 - m + TravelRules.DayStartsAt : TravelRules.DayStartsAt - m; continue; }
                var today = Math.Min(left, TravelRules.NightFallsAt - m);
                t += (long)Math.Ceiling(today);
                left -= today;
            }

            return TravelRules.Clock(t);
        }
    }
}

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>Phase 16: the world atlas as the player may see it.</summary>
        public Atlas.AtlasView GetAtlas() => HasWorld ? Atlas.AtlasQueries.View(_world) : new Atlas.AtlasView();

        /// <summary>Phase 16: the ways to a settlement from where the party is (best first), with estimates from the route data.</summary>
        public List<Atlas.TravelOptionView> GetTravelOptions(Domain.Identifiers.SettlementId destination) =>
            HasWorld ? Atlas.AtlasQueries.Options(_world, destination) : new List<Atlas.TravelOptionView>();
    }
}

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>Phase 16: the settlements of the region whose map is shown (all of them in a single-region world).</summary>
        public IReadOnlyList<SettlementSummary> GetSettlementsInView()
        {
            var all = GetSettlements();
            if (!HasWorld || _world.Regions.Count <= 1 || Map == null || Map.RegionId.IsEmpty) return all;
            var list = new List<SettlementSummary>();
            foreach (var s in all) if (s.RegionId == Map.RegionId) list.Add(s);
            return list;
        }
    }
}
