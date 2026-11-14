using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Application.Places
{
    /// <summary>What can be done to something in a place (ADR-0064). Only actions with real behaviour exist.</summary>
    public enum PlaceAction
    {
        Inspect = 0,
        Search = 1,
        Open = 2,
        Close = 3,
        Unlock = 4,
        Lock = 5,
        Pick = 6,
        Force = 7,
        Disarm = 8,
        Read = 9,
        Listen = 10,
        Activate = 11,
        Free = 12,
        Attack = 13,

        // Phase 14: parley with outlaws, fire, theft (ADR-0069, ADR-0071).
        PayToll = 14,
        Persuade = 15,
        Intimidate = 16,
        Burn = 17,
        Steal = 18,
        StealQuietly = 19,

        // Phase 18 (ADR-0089): the place mechanisms.
        Clear = 20,
        Illuminate = 21,
        Mend = 22,
    }

    /// <summary>Go into a place: from the map (a wilderness place the player knows of) or from its settlement. A locked way in needs an approach.</summary>
    public sealed class EnterPlaceCommand : ICommand
    {
        public EnterPlaceCommand(string placeId, string entranceId = "entrance", PlaceAction? approach = null)
        {
            PlaceId = placeId;
            EntranceId = entranceId ?? "entrance";
            Approach = approach;
        }

        public string PlaceId { get; }
        public string EntranceId { get; }

        /// <summary>For a locked way in: <see cref="PlaceAction.Pick"/> or <see cref="PlaceAction.Force"/>.</summary>
        public PlaceAction? Approach { get; }
    }

    /// <summary>Leave the place by the way in the player is standing at.</summary>
    public sealed class ExitPlaceCommand : ICommand
    {
    }

    /// <summary>Step into a room from the one the player is in (or record where they stand in it).</summary>
    public sealed class MoveInPlaceCommand : ICommand
    {
        public MoveInPlaceCommand(string roomId, LocalPose pose)
        {
            RoomId = roomId;
            Pose = pose;
        }

        public string RoomId { get; }
        public LocalPose Pose { get; }
    }

    /// <summary>Climb or descend: stairs or a ladder from the room the player is in.</summary>
    public sealed class TraversePlaceCommand : ICommand
    {
        public TraversePlaceCommand(string connectionId) { ConnectionId = connectionId; }
        public string ConnectionId { get; }
    }

    /// <summary>Act on a feature, a door or passage, a room (Search) or a group (Attack).</summary>
    public sealed class PlaceActionCommand : ICommand
    {
        public PlaceActionCommand(string targetId, PlaceAction action)
        {
            TargetId = targetId;
            Action = action;
        }

        public string TargetId { get; }
        public PlaceAction Action { get; }
    }

    /// <summary>The player stepped where a trap is (sent by the scene when they walk onto its cell).</summary>
    public sealed class SpringTrapCommand : ICommand
    {
        public SpringTrapCommand(string featureId) { FeatureId = featureId; }
        public string FeatureId { get; }
    }

    /// <summary>Phase 13 (ADR-0062 … ADR-0066): the place commands.</summary>
    public static class PlaceCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<EnterPlaceCommand>(new EnterHandler());
            commands.Register<ExitPlaceCommand>(new ExitHandler());
            commands.Register<MoveInPlaceCommand>(new MoveHandler());
            commands.Register<TraversePlaceCommand>(new TraverseHandler());
            commands.Register<PlaceActionCommand>(new ActionHandler());
            commands.Register<SpringTrapCommand>(new TrapHandler());
            commands.Register<AttackPersonCommand>(new AttackPersonHandler()); // Phase 14
        }

        private static CommandResult Ready(CommandContext context, bool inPlace, out PlayerCharacterData p)
        {
            p = null;
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            p = context.Committed.Player;
            if (p == null) return CommandResult.NotFound("This world has no player character.");
            if (p.Rpg == null || p.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
            if (p.Rpg.IsDead) return CommandResult.InvalidState(p.Name + " is dead.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("Not during a fight.");
            if (inPlace && !p.IsInPlace) return CommandResult.InvalidState(p.Name + " is not inside a place.");
            return null;
        }

        /// <summary>
        /// The common way in (wilderness places and building interiors alike): the player stands inside the entrance; a
        /// visit begins; followers come too; what the player knows rises to Visited; the entrance room is arrived in.
        /// </summary>
        public static CommandResult EnterAt(CommandContext context, WorldData w, PlaceData place, PlaceConnection entrance)
        {
            var p = w.Player;
            var report = QuestRuntime.Reporter(context);
            var first = place.Visits == 0;
            place.Visits++;
            place.LastVisitedAt = QuestClock.Now(w);
            if (place.Category != PlaceCategory.Interior) p.ReturnToOverland = p.IsOverland; // Phase 23: from the country, back to it on leaving
            p.LocationKind = CharacterLocationKind.Place;
            p.PlaceId = place.PlaceId;
            if (!place.SettlementId.IsEmpty) p.LastSettlementId = place.SettlementId;
            p.SettlementId = place.Category == PlaceCategory.Interior ? place.SettlementId : default;
            p.RegionId = place.RegionId;
            p.Pose = PlaceRules.EntrancePose(place, entrance);
            PlaceKnowledge.Raise(w, place, PlaceDiscovery.Visited, report, "entered");
            context.Quest(new QuestEvent { Kind = QuestEventKind.PlaceDiscovered, Subject = place.PlaceId });
            context.Quest(new QuestEvent { Kind = QuestEventKind.PlaceEntered, Subject = place.PlaceId, Settlement = place.SettlementId });
            if (first && place.Category != PlaceCategory.Interior)
                context.ReportEvent(HistoricalEventType.PlaceEntered, EventSeverity.Info, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                    new Dictionary<string, string> { ["place"] = place.Name, ["by"] = p.Name });
            var followers = PlacePresence.Followers(w).Count;
            var room = place.Layout.Room(entrance.A);
            PlaceFollowers.Enter(w, place, room.Id); // Phase 14 (ADR-0070): they come in with the player and keep a room of their own
            var said = PlaceRules.Arrive(context, w, place, room, entrance.Id);
            var name = place.Category == PlaceCategory.Interior ? place.Name : place.Name;
            return CommandResult.Ok($"{p.Name} enters {name}{(followers > 0 ? $", with {followers} following" : string.Empty)}. {room.Name}." + (said.Length > 0 ? " " + said : string.Empty));
        }

        private sealed class EnterHandler : ICommandHandler<EnterPlaceCommand>
        {
            public CommandResult Handle(EnterPlaceCommand c, CommandContext context)
            {
                var refused = Ready(context, false, out var player);
                if (refused != null) return refused;
                var committed = context.Committed;
                var place = committed.FindPlace(c.PlaceId);
                if (place == null) return CommandResult.NotFound("There is no such place.");
                if (player.IsInPlace) return CommandResult.InvalidState($"{player.Name} must leave {committed.FindPlace(player.PlaceId)?.Name} first.");
                if (place.Category == PlaceCategory.Interior)
                {
                    if (!player.IsInSettlement || player.SettlementId != place.SettlementId) return CommandResult.InvalidState($"{place.Name} is in {committed.FindSettlement(place.SettlementId)?.Name}.");
                }
                else if (place.Discovery < PlaceDiscovery.Discovered) return CommandResult.InvalidState(place.Discovery == PlaceDiscovery.Rumored
                    ? $"You have only heard of {place.Name}: you do not know where it lies."
                    : "You know of no such place.");

                var entrance = place.Layout.Connection(c.EntranceId);
                if (entrance == null || entrance.Kind != ConnectionKind.Entrance) return CommandResult.NotFound("There is no such way in.");
                var w = context.Edit();
                var wp = w.FindPlace(place.PlaceId);
                var we = wp.Layout.Connection(entrance.Id);
                if (we.FindDc > 0 && !(wp.Variables.TryGetValue("entrance:" + we.Id, out var known) && known == "known") && !(w.Player.Social?.Knows("fort_side_door") ?? false))
                    return CommandResult.InvalidState("You know of no other way in.");

                var door = wp.DoorOf(we);
                if (door == DoorState.Locked || door == DoorState.Barred)
                {
                    if (c.Approach != PlaceAction.Pick && c.Approach != PlaceAction.Force) return CommandResult.InvalidState("That way in is locked. Pick the lock or force it.");
                    var limit = PlaceRules.AttemptsLeft(wp, we.Id, c.Approach.ToString(), out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    if (c.Approach == PlaceAction.Pick && !HasItem(w.Player, "thieves_tools")) return CommandResult.InvalidState("You need thieves' tools to pick a lock.");
                    var roll = c.Approach == PlaceAction.Pick ? PlaceRules.Check(w, Skill.SleightOfHand, we.LockDc, key) : PlaceRules.Check(w, Skill.Athletics, we.LockDc + 2, key);
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(wp, we.Id, c.Approach.ToString());
                        // A failed attempt still costs nothing but the try: committed so it is remembered (no other change).
                        return CommandResult.Ok("It holds. " + roll.Text + ".");
                    }

                    wp.Doors[we.Id] = c.Approach == PlaceAction.Force ? DoorState.Broken : DoorState.Open;
                    wp.Note(w.Player.Name + (c.Approach == PlaceAction.Force ? " forced " : " picked ") + "the " + we.Id.Replace('_', ' ') + ".");
                    var result = EnterAt(context, w, wp, we);
                    return CommandResult.Ok(roll.Text + ". " + result.Message);
                }

                return EnterAt(context, w, wp, we);
            }
        }

        private sealed class ExitHandler : ICommandHandler<ExitPlaceCommand>
        {
            public CommandResult Handle(ExitPlaceCommand c, CommandContext context)
            {
                var refused = Ready(context, true, out var player);
                if (refused != null) return refused;
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                if (place == null) return CommandResult.NotFound("The place the player is in does not exist.");
                PlaceConnection way = null;
                foreach (var e in place.Layout.Connections)
                    if (e.Kind == ConnectionKind.Entrance && e.A == w.Player.PlaceRoom) way = e;
                if (way == null) return CommandResult.InvalidState("There is no way out from here: go back to the way you came in.");
                var held = PlaceRules.Held(w);
                if (held != null) return CommandResult.InvalidState(held);
                if (place.DoorOf(way) == DoorState.Locked || place.DoorOf(way) == DoorState.Barred) place.Doors[way.Id] = DoorState.Open; // opened from the inside

                var p = w.Player;
                var behind = PlaceFollowers.Leave(w, place, p.PlaceRoom); // Phase 14 (ADR-0070): only those with the player leave
                p.PlaceId = null;
                p.PlaceRoom = null;
                p.PlaceFloor = 0;
                string where;
                if (place.Category == PlaceCategory.Interior)
                {
                    p.LocationKind = CharacterLocationKind.Settlement;
                    p.SettlementId = place.SettlementId;
                    p.Pose = place.ExitPose;
                    where = w.FindSettlement(place.SettlementId)?.Name ?? "the street";
                    var home = PlaceRules.Arrive(context, w, place.SettlementId);
                    if (home.Count > 0) where += " — " + string.Join(", ", home) + " is home";
                }
                else if (p.ReturnToOverland && context.Session.GetOverlandTerrain(Realmkeeper.Domain.World.RealmIds.Surface, place.RegionId) is Realmkeeper.Domain.Geography.OverlandTerrain terrain)
                {
                    // Phase 23 (ADR-0110): entered from the country, so out into it again, beside the way in.
                    var (x, z) = Realmkeeper.Application.Overland.OverlandQueries.Approach(terrain, place.Cell, 2);
                    p.LocationKind = CharacterLocationKind.Overland;
                    p.SettlementId = default;
                    p.RegionId = place.RegionId;
                    p.Pose = new LocalPose(x, terrain.HeightAt(x, z), z, p.Pose.Yaw);
                    p.ReturnToOverland = false;
                    where = "the country around it";
                }
                else
                {
                    p.LocationKind = CharacterLocationKind.WorldMap;
                    p.SettlementId = default;
                    p.RegionId = default;
                    where = "the open country";
                }

                return CommandResult.Ok($"{p.Name} leaves {place.Name} for {where}." + (behind.Count > 0 ? $" {string.Join(" and ", behind)} stay{(behind.Count == 1 ? "s" : string.Empty)} behind, waiting." : string.Empty));
            }
        }

        private sealed class MoveHandler : ICommandHandler<MoveInPlaceCommand>
        {
            public CommandResult Handle(MoveInPlaceCommand c, CommandContext context)
            {
                var refused = Ready(context, true, out var player);
                if (refused != null) return refused;
                if (!c.Pose.IsFinite) return CommandResult.Invalid("That is not a place to stand.");
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                var from = PlaceRules.CurrentRoom(w);
                var to = place?.Layout.Room(c.RoomId);
                if (to == null) return CommandResult.NotFound("There is no such room here.");
                if (from != null && to.Id == from.Id)
                {
                    w.Player.Pose = c.Pose;
                    var caught = PlaceFollowers.Follow(w, place, to.Id); // Phase 14: a follower whose way opened catches up as soon as it does
                    return caught.Count > 0 ? CommandResult.Ok(string.Join(" ", caught)) : CommandResult.Ok();
                }

                if (from == null || to.Floor != from.Floor) return CommandResult.InvalidState("You can't get there from here.");
                PlaceConnection way = null;
                string blocked = null;
                foreach (var x in place.Layout.Connections)
                {
                    if (x.IsVertical || x.Kind == ConnectionKind.Entrance) continue;
                    if (!((x.A == from.Id && x.B == to.Id) || (x.B == from.Id && x.A == to.Id))) continue;
                    var why = PlaceRules.Blocks(place, x, from.Id);
                    if (why == null) { way = x; break; }
                    blocked = why;
                }

                if (way == null) return CommandResult.InvalidState(blocked ?? "You can't get there from here.");
                var held = PlaceRules.Held(w);
                if (held != null) return CommandResult.InvalidState(held);
                if (way.Kind == ConnectionKind.Bridge && place.BridgeOf(way) == BridgeState.Damaged)
                {
                    // Phase 14 (ADR-0071): a damaged bridge holds only for the sure-footed; a fall brings it down for good.
                    var limit = PlaceRules.AttemptsLeft(place, way.Id, "cross", out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = PlaceRules.Check(w, Domain.Rpg.Skill.Acrobatics, 12, key);
                    if (!roll.Success)
                    {
                        place.Bridges[way.Id] = BridgeState.Destroyed;
                        var fall = PlaceHarm.Apply(w, new HarmSpec { Source = "fall:bridge", Name = "a fall from the bridge in " + place.Name, Damage = new Domain.World.Dice(1, 6), DamageType = DamageType.Bludgeoning }, key, out _);
                        place.Note(w.Player.Name + " brought down the bridge in " + place.Name + ".");
                        context.ReportEvent(HistoricalEventType.BridgeCollapsed, EventSeverity.Warning, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                            new Dictionary<string, string> { ["place"] = place.Name, ["by"] = w.Player.Name });
                        return CommandResult.Ok($"{roll.Text}. Halfway over, the planks give way and the bridge goes down into the channel. {fall} You drag yourself out on the side you started from — the bridge is gone.");
                    }

                    w.Player.Pose = c.Pose;
                    var across = PlaceRules.Arrive(context, w, place, to, way.Id);
                    var with = PlaceFollowers.Follow(w, place, to.Id);
                    return CommandResult.Ok($"{roll.Text}. The bridge sways and creaks, but holds. {to.Name}." + (across.Length > 0 ? " " + across : string.Empty) + (with.Count > 0 ? " " + string.Join(" ", with) : string.Empty));
                }

                w.Player.Pose = c.Pose;
                var said = PlaceRules.Arrive(context, w, place, to, way.Id);
                var follow = PlaceFollowers.Follow(w, place, to.Id); // Phase 14 (ADR-0070)
                return CommandResult.Ok(to.Name + (said.Length > 0 ? ". " + said : ".") + (follow.Count > 0 ? " " + string.Join(" ", follow) : string.Empty));
            }
        }

        private sealed class TraverseHandler : ICommandHandler<TraversePlaceCommand>
        {
            public CommandResult Handle(TraversePlaceCommand c, CommandContext context)
            {
                var refused = Ready(context, true, out var player);
                if (refused != null) return refused;
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                var x = place?.Layout.Connection(c.ConnectionId);
                if (x == null || !x.IsVertical) return CommandResult.NotFound("There are no stairs or ladder there.");
                if (!x.Joins(w.Player.PlaceRoom)) return CommandResult.InvalidState("You are not at those " + (x.Kind == ConnectionKind.Ladder ? "ladder rungs." : "stairs."));
                var why = PlaceRules.Blocks(place, x, w.Player.PlaceRoom) ?? PlaceRules.Held(w);
                if (why != null) return CommandResult.InvalidState(why);
                var up = x.A == w.Player.PlaceRoom;
                var to = place.Layout.Room(up ? x.B : x.A);
                w.Player.Pose = up ? PlaceRules.PoseAt(place, to.Floor, x.ToX, x.ToZ, 180f) : PlaceRules.PoseAt(place, to.Floor, x.Rect.X, x.Rect.Z, 180f);
                var verb = x.Kind == ConnectionKind.Ladder ? "climb" : "take the stairs";
                var above = place.Layout.Floors.Find(f => f.Index == to.Floor);
                var said = PlaceRules.Arrive(context, w, place, to, x.Id);
                var follow = PlaceFollowers.Follow(w, place, to.Id); // Phase 14 (ADR-0070)
                return CommandResult.Ok($"You {verb} to the {above?.Name?.ToLowerInvariant() ?? "next floor"}: {to.Name}." + (said.Length > 0 ? " " + said : string.Empty) + (follow.Count > 0 ? " " + string.Join(" ", follow) : string.Empty));
            }
        }

        private sealed class TrapHandler : ICommandHandler<SpringTrapCommand>
        {
            public CommandResult Handle(SpringTrapCommand c, CommandContext context)
            {
                var refused = Ready(context, true, out var player);
                if (refused != null) return refused;
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                var f = place?.Layout.Feature(c.FeatureId);
                if (f == null || f.Kind != FeatureKind.Trap) return CommandResult.NotFound("There is no trap there.");
                if (f.Room != w.Player.PlaceRoom) return CommandResult.InvalidState("That trap is in another room.");
                var trap = TrapCatalog.Find(f.Get("trap"));
                var s = place.StateOf(f.Id);
                if (s.Has(FeatureFlags.Disarmed)) return CommandResult.Ok("The trap is disarmed.");
                var visit = place.Visits.ToString(CultureInfo.InvariantCulture);
                var sprungKey = "sprung:" + f.Id;
                if (s.Has(FeatureFlags.Triggered) && (trap.Reset == TrapReset.Once || (place.Variables.TryGetValue(sprungKey, out var last) && last == visit)))
                    return CommandResult.Ok("Nothing happens: it is spent" + (trap.Reset == TrapReset.Rearms ? " until it resets." : "."));
                return Spring(context, w, place, f, trap, "stepped");
            }
        }

        /// <summary>A trap goes off: a save, damage (halved on success), an alarm; it is seen now and spent (or resets later).</summary>
        public static CommandResult Spring(CommandContext context, WorldData w, PlaceData place, PlaceFeature f, TrapDefinition trap, string how)
        {
            var s = place.StateOf(f.Id);
            var visit = place.Visits.ToString(CultureInfo.InvariantCulture);
            s.Flags |= FeatureFlags.Triggered | FeatureFlags.Found;
            s.ChangedAt = QuestClock.Now(w);
            place.Variables["sprung:" + f.Id] = visit;
            var text = trap.Sprung;
            if (!trap.Damage.IsEmpty || trap.Inflicts.HasValue)
                text += " " + PlaceHarm.Apply(w, PlaceRules.Harm(trap, place), place.PlaceId + ":" + f.Id + ":" + visit, out _); // Phase 14: the shared rules (ADR-0068)

            if (trap.Alarm)
            {
                foreach (var g in place.Layout.Groups) place.GroupOf(g.Id).Alerted = true;
                text += " Somewhere, something is now awake.";
            }

            place.Note($"{w.Player.Name} set off the {trap.Name.ToLowerInvariant()} ({how}).");
            context.ReportEvent(HistoricalEventType.TrapSprung, EventSeverity.Warning, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                new Dictionary<string, string> { ["place"] = place.Name, ["trap"] = trap.Name, ["by"] = w.Player.Name });
            return CommandResult.Ok(text);
        }

        public static bool HasItem(PlayerCharacterData p, string itemId)
        {
            foreach (var s in p.Inventory) if (s.ItemId == itemId) return true;
            foreach (var pair in p.Equipment) if (pair.Value?.ItemId == itemId) return true;
            return false;
        }

        private sealed class ActionHandler : ICommandHandler<PlaceActionCommand>
        {
            public CommandResult Handle(PlaceActionCommand c, CommandContext context)
            {
                var refused = Ready(context, true, out var player);
                if (refused != null) return refused;
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                if (place == null) return CommandResult.NotFound("The place the player is in does not exist.");
                var room = PlaceRules.CurrentRoom(w);
                if (room == null) return CommandResult.InvalidState("Step into a room first.");
                return PlaceActions.Do(context, w, place, room, c.TargetId, c.Action);
            }
        }
    }
}

namespace Realmkeeper.Application.Places
{
    using Realmkeeper.Domain.Identifiers;
    using Realmkeeper.Simulation.Places;

    /// <summary>Phase 14 (ADR-0069): attack a person in the room — an assault, with everything that follows from it.</summary>
    public sealed class AttackPersonCommand : ICommand
    {
        public AttackPersonCommand(NpcId npcId) { NpcId = npcId; }
        public NpcId NpcId { get; }
    }

    internal sealed class AttackPersonHandler : ICommandHandler<AttackPersonCommand>
    {
        public CommandResult Handle(AttackPersonCommand c, CommandContext context)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            var committed = context.Committed;
            if (committed.ActiveEncounter != null) return CommandResult.InvalidState("Not during a fight.");
            if (!committed.Player.IsInPlace) return CommandResult.InvalidState("There is nobody here to attack.");
            var w = context.Edit();
            var place = PlaceRules.Current(w);
            var room = PlaceRules.CurrentRoom(w);
            var npc = w.FindNpc(c.NpcId);
            if (place == null || room == null || npc == null || npc.IsDeceased) return CommandResult.NotFound("They are not here.");
            if (w.FindEscort(npc.NpcId) is Domain.Places.EscortData e && (e.State == Domain.Places.EscortState.Following || e.State == Domain.Places.EscortState.Waiting))
                return CommandResult.InvalidState(npc.Name + " is in your care.");
            foreach (var p in IndoorSchedule.Everyone(w, place, Simulation.Quests.QuestClock.Now(w)))
                if (p.Npc.NpcId == npc.NpcId && p.Room == room.Id) return PlaceEncounters.StartAssault(context, w, place, room, npc, p.X, p.Z);
            return CommandResult.InvalidState(npc.Name + " is not in this room.");
        }
    }
}
