using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Atlas
{
    /// <summary>Phase 16: set out for a settlement along a planned route (option 0 is the best; 1 and 2 the alternatives).</summary>
    public sealed class StartJourneyCommand : ICommand
    {
        public StartJourneyCommand(SettlementId destination, int option = 0) { Destination = destination; Option = option; }
        public SettlementId Destination { get; }
        public int Option { get; }
    }

    /// <summary>Phase 16: one hour of the journey (the session advances the clock after it).</summary>
    public sealed class TravelHourCommand : ICommand { }

    public sealed class HaltJourneyCommand : ICommand { }

    public sealed class ResumeJourneyCommand : ICommand { }

    /// <summary>Phase 16: gives up the journey; the party stays where it is on the road and can set out anywhere from there.</summary>
    public sealed class AbandonJourneyCommand : ICommand { }

    public enum RoadChoice
    {
        Fight = 0,
        SlipPast = 1,
        PayToll = 2,
    }

    /// <summary>
    /// Phase 17: the party leaves the ground of a fight on the road once it is over (won, lost or withdrawn from) and
    /// stands on the road again, the journey halted where it was. Travel goes on only when the player resumes it.
    /// </summary>
    public sealed class LeaveRoadFightCommand : ICommand { }

    /// <summary>Phase 16: what the party does about outlaws barring the road: fight, try to slip past (Stealth), or pay their toll.</summary>
    public sealed class ResolveRoadThreatCommand : ICommand
    {
        public ResolveRoadThreatCommand(RoadChoice choice) { Choice = choice; }
        public RoadChoice Choice { get; }
    }

    /// <summary>
    /// Phase 16 (ADR-0078): travel as a world action. A journey follows planned segments of the route network; each hour
    /// the party walks the segment's pace (half at night), crosses into the next region past a crossing's midpoint, and
    /// may meet what the road holds: outlaws working that road (a seeded roll per hour of the journey, from their numbers,
    /// the dark and the party's size), or a way that has closed. Nothing is skipped: a threat stops the journey until it
    /// is settled, a closed way until the party chooses another. The party is the player's: active followers walk with
    /// them (no second party model). Arrival puts the player at the settlement, ready to enter it.
    /// </summary>
    public static class TravelCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<StartJourneyCommand>(new StartHandler());
            commands.Register<TravelHourCommand>(new HourHandler());
            commands.Register<HaltJourneyCommand>(new HaltHandler());
            commands.Register<ResumeJourneyCommand>(new ResumeHandler());
            commands.Register<AbandonJourneyCommand>(new AbandonHandler());
            commands.Register<ResolveRoadThreatCommand>(new ThreatHandler());
            commands.Register<LeaveRoadFightCommand>(new LeaveFightHandler()); // Phase 17
        }

        /// <summary>
        /// Phase 17 (ADR-0086): the fight on the road the party is in, or has just finished and not yet left: the journey
        /// names it (its encounter ID), and the player is on the road. Null otherwise. The ground is the settlement at the
        /// nearer end of the road, the same end the encounter was drawn near.
        /// </summary>
        public static (SettlementId ground, string routeId, Domain.Combat.EncounterData encounter)? RoadFight(WorldData w)
        {
            var j = w?.Journey;
            if (j == null || j.EncounterId == null || j.EncounterId.StartsWith("threat:", StringComparison.Ordinal) || !OnTheRoad(w)) return null;
            Domain.Combat.EncounterData fight = null;
            foreach (var e in w.Encounters) if (e.EncounterId.ToString() == j.EncounterId) fight = e;
            if (fight == null) return null;
            var (seg, from, metres) = Position(w);
            var near = seg == null ? from : metres * 2 < TravelRules.Km(seg) * 1000 ? from : seg.Other(from);
            return (near, seg?.RouteId, fight);
        }

        private sealed class LeaveFightHandler : ICommandHandler<LeaveRoadFightCommand>
        {
            public CommandResult Handle(LeaveRoadFightCommand c, CommandContext context)
            {
                var w = context.Committed;
                var fight = RoadFight(w);
                if (fight == null) return CommandResult.InvalidState("There is no fight on the road to leave.");
                if (fight.Value.encounter.IsActive) return CommandResult.InvalidState("The fight is not over: finish it, or withdraw.");
                var j = context.Edit().Journey;
                j.EncounterId = null;
                j.Status = JourneyStatus.Halted;
                j.Reason = "after a fight on the road";
                return CommandResult.Ok("You gather yourselves on the road. Travel on when you are ready.");
            }
        }

        public static bool OnTheRoad(WorldData w) => w.Journey != null && w.Journey.Status != JourneyStatus.Arrived && w.Player != null && !w.Player.IsInSettlement && !w.Player.IsInPlace;

        /// <summary>Where the party stands now: at a settlement (the segment's start, 0 m), or partway along a segment.</summary>
        public static (RouteSegmentData segment, SettlementId from, int metres) Position(WorldData w)
        {
            var j = w.Journey;
            if (j == null || j.SegmentIndex >= j.Segments.Count) return (null, j?.DestinationId ?? default, 0);
            return (w.FindRoute(j.Segments[j.SegmentIndex]), j.Nodes[j.SegmentIndex], j.MetresIntoSegment);
        }

        /// <summary>
        /// The plans from where the party is: from its settlement, or from partway along a road (either way back, then on).
        /// Cheapest first; at most three.
        /// </summary>
        public static List<RoutePlan> PlansFrom(WorldData w, SettlementId destination)
        {
            var p = w.Player;
            if (p == null) return new List<RoutePlan>();
            if (!OnTheRoad(w))
            {
                var from = p.IsInSettlement ? p.SettlementId : !p.LastSettlementId.IsEmpty ? p.LastSettlementId : w.Journey?.DestinationId ?? default;
                return from.IsEmpty ? new List<RoutePlan>() : RoutePlanner.Alternatives(w, from, destination);
            }

            var (seg, start, metres) = Position(w);
            if (seg == null) return new List<RoutePlan>();
            var plans = new List<RoutePlan>();
            var km = TravelRules.Km(seg);
            // A road that closed under them (a bridge fell, a blockade) is closed ahead of where they stand: the way they
            // came stays open. Walking back already, the way they came is the far end of this segment's record.
            var cameFrom = w.Journey.Back && w.Journey.SegmentIndex == 0 ? seg.Other(start) : start;
            foreach (var (end, done) in new[] { (seg.Other(start), metres), (start, (int)(km * 1000) - metres) })
            {
                var back = !seg.Passable;
                if (back && end != cameFrom) continue; // not through the closure
                var remainingHours = (back ? TravelRules.WalkingHours(seg) : TravelRules.Hours(seg)) * Math.Max(0, km * 1000 - done) / Math.Max(1, km * 1000);
                if (end == destination)
                {
                    var direct = new RoutePlan { Hours = remainingHours, Km = Math.Max(0, km - done / 1000.0), Back = back };
                    direct.Nodes.Add(seg.Other(end));
                    direct.Nodes.Add(end);
                    direct.Segments.Add(seg.RouteId);
                    if (back) direct.Notes.Add("back the way you came: the road ahead is closed");
                    plans.Add(direct);
                    continue;
                }

                foreach (var rest in RoutePlanner.Alternatives(w, end, destination))
                {
                    if (rest.Segments.Contains(seg.RouteId)) continue;
                    var whole = new RoutePlan { Back = back };
                    whole.Nodes.Add(seg.Other(end));
                    whole.Nodes.AddRange(rest.Nodes);
                    whole.Segments.Add(seg.RouteId);
                    whole.Segments.AddRange(rest.Segments);
                    whole.Hours = remainingHours + rest.Hours;
                    whole.Km = Math.Max(0, km - done / 1000.0) + rest.Km;
                    if (back) whole.Notes.Add("back the way you came: the road ahead is closed");
                    whole.Notes.AddRange(rest.Notes);
                    plans.Add(whole);
                }
            }

            plans.RemoveAll(x => double.IsInfinity(x.Hours)); // only ways that can be walked
            plans.Sort((a, b) => a.Hours.CompareTo(b.Hours));
            return plans.Count > 3 ? plans.GetRange(0, 3) : plans;
        }

        private sealed class StartHandler : ICommandHandler<StartJourneyCommand>
        {
            public CommandResult Handle(StartJourneyCommand c, CommandContext context)
            {
                var w = context.Committed;
                var p = w?.Player;
                if (p == null) return CommandResult.InvalidState("There is no active world.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                if (p.IsInPlace) return CommandResult.InvalidState($"{p.Name} must leave {w.FindPlace(p.PlaceId)?.Name ?? "the place"} first.");
                if (p.Rpg?.IsDead == true) return CommandResult.InvalidState($"{p.Name} is dead.");
                var dest = w.FindSettlement(c.Destination);
                if (dest == null) return CommandResult.NotFound("No such settlement.");
                if (p.IsInSettlement && p.SettlementId == c.Destination) return CommandResult.Invalid($"You are already in {dest.Name}.");
                if (w.Journey?.Status == JourneyStatus.Interrupted && w.Journey.EncounterId != null && w.Journey.EncounterId.StartsWith("threat:", StringComparison.Ordinal))
                    return CommandResult.InvalidState("Outlaws bar the road: deal with them first (fight, slip past, or pay).");
                var plans = PlansFrom(w, c.Destination);
                if (plans.Count == 0) return CommandResult.Invalid($"No open way leads to {dest.Name} from here.");
                if (c.Option < 0 || c.Option >= plans.Count) return CommandResult.Invalid("There is no such route.");
                var plan = plans[c.Option];
                if (double.IsInfinity(plan.Hours)) return CommandResult.Invalid("That way is closed.");

                var edit = context.Edit();
                var ep = edit.Player;
                var keepMetres = OnTheRoad(w) ? Position(w) : (null, default(SettlementId), 0);
                var number = edit.HistoricalEvents.Count + edit.Encounters.Count;
                var j = new JourneyData
                {
                    JourneyId = "journey:" + QuestClock.Now(edit).ToString(CultureInfo.InvariantCulture) + ":" + c.Destination, OriginId = plan.Nodes[0], DestinationId = c.Destination,
                    Mode = TravelMode.OnFoot, Status = JourneyStatus.Travelling, StartedAt = QuestClock.Now(edit),
                };
                j.Nodes.AddRange(plan.Nodes);
                j.Segments.AddRange(plan.Segments);
                j.Back = plan.Back;
                if (keepMetres.Item1 != null && plan.Segments.Count > 0 && plan.Segments[0] == keepMetres.Item1.RouteId)
                    j.MetresIntoSegment = plan.Nodes[0] == keepMetres.Item2 ? keepMetres.Item3 : (int)(TravelRules.Km(keepMetres.Item1) * 1000) - keepMetres.Item3;
                edit.Journey = j;
                if (ep.IsInSettlement)
                {
                    ep.LastSettlementId = ep.SettlementId;
                    ep.SettlementId = default;
                    ep.LocationKind = CharacterLocationKind.WorldMap;
                }

                ep.RegionId = edit.FindSettlement(plan.Nodes[0])?.RegionId ?? ep.RegionId;
                var via = plan.Notes.Count > 0 ? " On the way: " + string.Join("; ", plan.Notes) + "." : string.Empty;
                return CommandResult.Ok($"You set out for {dest.Name}: {plan.Km:0} km, about {TravelRules.Duration(plan.Hours)} on foot.{via}");
            }
        }

        private sealed class HourHandler : ICommandHandler<TravelHourCommand>
        {
            public CommandResult Handle(TravelHourCommand c, CommandContext context)
            {
                var w = context.Committed;
                var j = w?.Journey;
                if (j == null || j.Status != JourneyStatus.Travelling) return CommandResult.InvalidState("You are not travelling.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                var edit = context.Edit();
                j = edit.Journey;
                var p = edit.Player;
                var night = TravelRules.IsNight(edit.MinuteOfDay);
                var said = new List<string>();
                var left = 60; // minutes of walking this hour
                var checkedThreat = false;
                string slowedBy = null;
                while (left > 0 && j.SegmentIndex < j.Segments.Count)
                {
                    var seg = edit.FindRoute(j.Segments[j.SegmentIndex]);
                    var from = j.Nodes[j.SegmentIndex];
                    var walkingBack = j.Back && j.SegmentIndex == 0;
                    if (seg == null || (!seg.Passable && !walkingBack))
                    {
                        j.Status = JourneyStatus.Interrupted;
                        j.Reason = seg == null ? "the way is lost" : Closed(edit, seg);
                        return CommandResult.Ok($"You cannot go on: {j.Reason}. Choose another way.");
                    }

                    // Outlaws working this road: one check per hour of the journey (stable key: the journey and the hour).
                    if (!checkedThreat)
                    {
                        checkedThreat = true;
                        var band = GroupSystem.BandOn(edit, seg.RouteId);
                        if (band != null && !j.Cleared.Contains(band.GroupId))
                        {
                            var party = 1 + Simulation.Party.CompanionLoyalty.Present(edit).Count;
                            var chance = Math.Max(0, Math.Min(40, 4 + band.Strength * 4 + (night ? 6 : 0) - party * 2));
                            var roll = NpcHash.Roll(NpcHash.Of(edit.Seed, "road-threat", j.JourneyId, j.HoursTravelled), 100);
                            if (roll < chance)
                            {
                                j.Status = JourneyStatus.Interrupted;
                                j.EncounterId = "threat:" + band.GroupId;
                                j.Reason = $"{band.Strength} outlaws bar the road between {Name(edit, seg.A)} and {Name(edit, seg.B)}";
                                if (WorldEvents.Open(edit, "bandits:" + band.PopulationId) is WorldEventData ev) WorldEvents.PlayerLearns(ev, EventKnowledge.Confirmed, QuestClock.Now(edit), "seen for yourself");
                                return CommandResult.Ok($"{j.Reason}. Fight, try to slip past, or pay their toll of {Toll(band)} coin.");
                            }
                        }
                    }

                    // Phase 21: the weather where the party walks slows them.
                    var here = edit.Environment?.Find(p.RegionId);
                    var weatherFactor = TravelRules.WeatherFactor(seg, here?.At(edit.MinuteOfDay), here?.SnowCover ?? 0, out var weatherWhy);
                    if (weatherWhy != null && slowedBy == null) slowedBy = weatherWhy;
                    var perMinute = TravelRules.MetresPerHour(seg, night, walkingBack) * weatherFactor / 60.0;
                    var length = (int)Math.Round(TravelRules.Km(seg) * 1000);
                    var need = length - j.MetresIntoSegment;
                    var minutes = (int)Math.Ceiling(need / Math.Max(0.001, perMinute));
                    var wasRegion = p.RegionId;
                    if (minutes <= left)
                    {
                        left -= minutes;
                        j.MetresIntoSegment = 0;
                        j.SegmentIndex++;
                        var reached = edit.FindSettlement(j.Nodes[j.SegmentIndex]);
                        p.RegionId = reached?.RegionId ?? p.RegionId;
                        p.LastSettlementId = reached?.SettlementId ?? p.LastSettlementId;
                        if (j.SegmentIndex < j.Segments.Count) said.Add("You pass through " + reached?.Name + ".");
                    }
                    else
                    {
                        j.MetresIntoSegment += (int)Math.Round(left * perMinute);
                        left = 0;
                        p.RegionId = RoutePlanner.RegionAt(edit, seg, from, j.MetresIntoSegment);
                    }

                    if (p.RegionId != wasRegion) Entered(edit, p.RegionId, said);
                }

                j.HoursTravelled++;
                if (j.SegmentIndex >= j.Segments.Count) return Arrive(context, edit, said);
                var (now, at, m) = Position(edit);
                said.Insert(0, $"An hour on the road{(night ? " in the dark (half pace)" : string.Empty)}{(slowedBy != null ? ", slowed by " + slowedBy : string.Empty)}: {m / 1000.0:0.0} km along the way from {Name(edit, at)} to {Name(edit, now?.Other(at) ?? at)}.");
                return CommandResult.Ok(string.Join(" ", said));
            }
        }

        /// <summary>Entering a region: it is visited; its neighbours are known by name.</summary>
        private static void Entered(WorldData w, RegionId id, List<string> said)
        {
            var r = w.FindRegion(id);
            if (r == null) return;
            if (r.Discovery != RegionDiscovery.Visited) said.Add($"You cross into {r.Name}.");
            r.Discovery = RegionDiscovery.Visited;
            foreach (var n in r.Neighbours)
                if (w.FindRegion(n) is RegionData nr && nr.Discovery < RegionDiscovery.Discovered) nr.Discovery = RegionDiscovery.Discovered;
        }

        private static CommandResult Arrive(CommandContext context, WorldData w, List<string> said)
        {
            var j = w.Journey;
            var p = w.Player;
            var dest = w.FindSettlement(j.DestinationId);
            j.Status = JourneyStatus.Arrived;
            j.Reason = null;
            p.LastSettlementId = j.DestinationId;
            p.RegionId = dest?.RegionId ?? p.RegionId;
            Entered(w, p.RegionId, said);
            var now = QuestClock.Now(w);

            // The talk of the place: what its people know of the world reaches the player as rumour.
            var heard = 0;
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen || e.Known == EventKnowledge.Confirmed && !e.KnowledgeOutdated) continue;
                foreach (var n in w.Npcs)
                    if (!n.IsDeceased && n.HomeSettlementId == j.DestinationId && NpcMind.Find(n, KnowledgeTopic.RecentEvent, e.EventId) != null)
                    {
                        WorldEvents.PlayerLearns(e, EventKnowledge.Rumored, now, "the talk in " + dest?.Name);
                        heard++;
                        break;
                    }
            }

            context.ReportEvent(HistoricalEventType.JourneyMilestone, EventSeverity.Info, EventImportance.Minor, dest == null ? Array.Empty<SettlementId>() : new[] { dest.SettlementId },
                new Dictionary<string, string> { ["player"] = p.Name, ["what"] = "arrived in " + dest?.Name + " after " + j.HoursTravelled + " hours on the road" }, null, Array.Empty<NpcId>());
            said.Add($"You arrive in {dest?.Name} after {j.HoursTravelled} hours on the road.{(heard > 0 ? $" You hear talk of {heard} matter{(heard == 1 ? string.Empty : "s")} in the wider world." : string.Empty)}");
            return CommandResult.Ok(string.Join(" ", said));
        }

        private static string Closed(WorldData w, RouteSegmentData r)
        {
            var where = $"between {Name(w, r.A)} and {Name(w, r.B)}";
            return r.HasBridge && r.Bridge == BridgeState.Destroyed ? "the bridge " + where + " is down" : "the road " + where + " is barred";
        }

        private static string Name(WorldData w, SettlementId id) => w.FindSettlement(id)?.Name ?? "?";

        public static int Toll(TravelingGroupData band) => Math.Max(5, band.Strength * 5);

        private sealed class HaltHandler : ICommandHandler<HaltJourneyCommand>
        {
            public CommandResult Handle(HaltJourneyCommand c, CommandContext context)
            {
                var j = context.Committed?.Journey;
                if (j == null || j.Status != JourneyStatus.Travelling) return CommandResult.InvalidState("You are not travelling.");
                var e = context.Edit().Journey;
                e.Status = JourneyStatus.Halted;
                e.Reason = "you stopped to rest";
                return CommandResult.Ok("You stop by the road.");
            }
        }

        private sealed class ResumeHandler : ICommandHandler<ResumeJourneyCommand>
        {
            public CommandResult Handle(ResumeJourneyCommand c, CommandContext context)
            {
                var w = context.Committed;
                var j = w?.Journey;
                if (j == null || (j.Status != JourneyStatus.Halted && j.Status != JourneyStatus.Interrupted)) return CommandResult.InvalidState("There is no journey to resume.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Finish the fight first.");
                if (w.Player?.Rpg?.IsDead == true) return CommandResult.InvalidState("The dead do not travel.");
                if (j.EncounterId != null && j.EncounterId.StartsWith("threat:", StringComparison.Ordinal)) return CommandResult.InvalidState("Outlaws bar the road: fight, slip past, or pay.");
                if (j.SegmentIndex < j.Segments.Count && !(j.Back && j.SegmentIndex == 0) && w.FindRoute(j.Segments[j.SegmentIndex]) is RouteSegmentData seg && !seg.Passable)
                    return CommandResult.InvalidState($"{Closed(w, seg)}: choose another way.");
                var e = context.Edit().Journey;
                e.Status = JourneyStatus.Travelling;
                e.Reason = null;
                e.EncounterId = null;
                return CommandResult.Ok("You take to the road again.");
            }
        }

        private sealed class AbandonHandler : ICommandHandler<AbandonJourneyCommand>
        {
            public CommandResult Handle(AbandonJourneyCommand c, CommandContext context)
            {
                var j = context.Committed?.Journey;
                if (j == null || j.Status == JourneyStatus.Arrived || j.Status == JourneyStatus.Abandoned) return CommandResult.InvalidState("There is no journey to give up.");
                if (j.EncounterId != null && j.EncounterId.StartsWith("threat:", StringComparison.Ordinal)) return CommandResult.InvalidState("Outlaws bar the road: deal with them first.");
                var e = context.Edit().Journey;
                e.Status = JourneyStatus.Abandoned;
                e.Reason = "you gave it up";
                return CommandResult.Ok("You give up the journey. Choose where to go from here.");
            }
        }

        private sealed class ThreatHandler : ICommandHandler<ResolveRoadThreatCommand>
        {
            public CommandResult Handle(ResolveRoadThreatCommand c, CommandContext context)
            {
                var w = context.Committed;
                var j = w?.Journey;
                if (j == null || j.Status != JourneyStatus.Interrupted || j.EncounterId == null || !j.EncounterId.StartsWith("threat:", StringComparison.Ordinal))
                    return CommandResult.InvalidState("Nothing bars the road.");
                var band = w.FindGroup(j.EncounterId.Substring(7));
                var edit = context.Edit();
                var ej = edit.Journey;
                if (band == null || band.Strength <= 0)
                {
                    ej.Status = JourneyStatus.Travelling;
                    ej.EncounterId = null;
                    ej.Reason = null;
                    return CommandResult.Ok("The road is clear after all.");
                }

                switch (c.Choice)
                {
                    case RoadChoice.PayToll:
                    {
                        var toll = Toll(band);
                        if (edit.Player.Purse < toll) return CommandResult.Invalid($"Their toll is {toll} coin; you have {edit.Player.Purse}.");
                        edit.Player.Purse -= toll;
                        ej.Cleared.Add(band.GroupId);
                        ej.Status = JourneyStatus.Travelling;
                        ej.EncounterId = null;
                        ej.Reason = null;
                        edit.FindGroup(band.GroupId)?.Note($"{TravelRules.Clock(QuestClock.Now(edit))} took {toll} coin from travellers.");
                        return CommandResult.Ok($"You pay {toll} coin and they let you pass.");
                    }

                    case RoadChoice.SlipPast:
                    {
                        var p = edit.Player;
                        var bonus = 0;
                        if (p.Rpg != null && p.Rpg.State == CreationState.Complete) bonus = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).SkillBonus(Skill.Stealth).Total;
                        var d20 = 1 + NpcHash.Roll(NpcHash.Of(edit.Seed, "slip-past", ej.JourneyId, ej.HoursTravelled), 20);
                        var dc = 10 + band.Strength;
                        if (d20 + bonus >= dc)
                        {
                            ej.Cleared.Add(band.GroupId);
                            ej.Status = JourneyStatus.Travelling;
                            ej.EncounterId = null;
                            ej.Reason = null;
                            return CommandResult.Ok($"Stealth {d20} + {bonus} = {d20 + bonus} against {dc}: you slip past them in the brush.");
                        }

                        var fought = Fight(context, edit, band);
                        return fought.IsSuccess ? CommandResult.Ok($"Stealth {d20} + {bonus} = {d20 + bonus} against {dc}: they see you. " + fought.Message) : fought;
                    }

                    default:
                        return Fight(context, edit, band);
                }
            }
        }

        /// <summary>The fight with the band, through the one engine: drawn from the band itself near the settlement at that end of the road.</summary>
        private static CommandResult Fight(CommandContext context, WorldData edit, TravelingGroupData band)
        {
            var j = edit.Journey;
            var (seg, from, metres) = Position(edit);
            var near = seg == null ? from : metres * 2 < TravelRules.Km(seg) * 1000 ? from : seg.Other(from);
            var pop = edit.CreaturePopulations.Find(x => x.PopulationId == band.PopulationId);
            var attempt = 0;
            foreach (var e in edit.Encounters) if (e.Origin != null) attempt++;
            var map = context.Map != null && context.Map.RegionId == edit.FindSettlement(near)?.RegionId ? context.Map : null;
            if (map == null) return CommandResult.InvalidState("The region's geography is not loaded.");
            var plan = EncounterGenerator.Plan(edit, map, new EncounterRequest { Near = near, Purpose = EncounterPurpose.Road, Attempt = attempt, Seek = pop?.CreatureId, Population = pop?.PopulationId });
            if (plan == null || !plan.IsValid)
            {
                j.Status = JourneyStatus.Travelling;
                j.EncounterId = null;
                j.Reason = null;
                return CommandResult.Ok("They melt into the trees before you reach them.");
            }

            var r = WorldEncounters.StartPlan(context, plan);
            if (!r.IsSuccess) return r;
            j.Cleared.Add(band.GroupId);
            j.EncounterId = edit.ActiveEncounter?.EncounterId.ToString();
            j.Reason = "a fight on the road";
            return CommandResult.Ok("You fight. " + r.Message);
        }
    }
}
