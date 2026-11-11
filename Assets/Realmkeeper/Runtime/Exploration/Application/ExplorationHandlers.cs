using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;

namespace Realmkeeper.Exploration.Application
{
    /// <summary>
    /// Registers the exploration command handlers with a session. Called once per session by the
    /// composition (the Unity hosting bridge, or tests). Nothing in the core references this module.
    /// </summary>
    public static class ExplorationModule
    {
        public static void Register(WorldSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var commands = session.Commands;
            commands.Register<EnterSettlementCommand>(new EnterSettlementHandler());
            commands.Register<EnterBuildingCommand>(new EnterBuildingHandler()); // Phase 13
            commands.Register<ExitSettlementCommand>(new ExitSettlementHandler());
            commands.Register<SetPlayerPoseCommand>(new SetPlayerPoseHandler());
            commands.Register<SetCameraPerspectiveCommand>(new SetCameraPerspectiveHandler());
            commands.Register<TalkToResidentCommand>(new TalkToResidentHandler());
            commands.Register<PostLabourNoticeCommand>(new PostLabourNoticeHandler());
        }

        /// <summary>The local environment of a settlement, derived from authoritative world data (never saved).</summary>
        public static LocalEnvironmentLayout LayoutFor(WorldData world, SettlementData settlement)
        {
            // The people who are here this hour (Phase 5), each at the site of where they are; shops fitted out.
            var residents = new List<ResidentInput>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || Simulation.Places.PlacePresence.IsAway(world, n)) continue; // Phase 13: held, following or posted elsewhere
                var now = Simulation.Npcs.NpcActivityPlan.Now(world, n);
                var placeId = now.IsKnown ? now.Location : n.CurrentLocationId;
                var place = placeId.IsEmpty ? null : world.FindLocation(placeId);
                var here = place?.SettlementId ?? n.HomeSettlementId;
                if (here == settlement.SettlementId) residents.Add(new ResidentInput(n.NpcId, n.Role, place?.LayoutKey, place?.Kind));
            }

            var shops = new Dictionary<string, ShopFit>(StringComparer.Ordinal);
            foreach (var s in world.Shops)
            {
                if (s.SettlementId != settlement.SettlementId) continue;
                var key = world.FindLocation(s.LocationId)?.LayoutKey;
                if (key != null) shops[key] = FitFor(s.Categories);
            }

            var plan = world.FindLayout(settlement.SettlementId);
            if (plan == null) return LocalEnvironmentGenerator.Generate(world.Seed, settlement.SettlementId, settlement.RegionId, settlement.SettlementType, residents, shops);
            var layout = LocalEnvironmentGenerator.FromPlan(plan.Clone(), world.Seed, settlement.SettlementId, settlement.RegionId, settlement.SettlementType, residents, shops);
            foreach (var h in world.Livestock) if (h.SettlementId == settlement.SettlementId) layout.Herds.Add(new LayoutHerd { CreatureId = h.CreatureId, Count = h.Count, PastureKey = h.PastureKey });
            return layout;
        }

        /// <summary>Same as <see cref="LayoutFor(WorldData, SettlementData)"/>, through session queries (for presentation).</summary>
        public static LocalEnvironmentLayout LayoutFor(WorldSession session, SettlementSummary settlement)
        {
            var residents = new List<ResidentInput>();
            foreach (var n in session.GetPresenceIn(settlement.SettlementId)) residents.Add(new ResidentInput(n.NpcId, n.Role, n.LayoutKey, n.LocationKind));
            var shops = new Dictionary<string, ShopFit>(StringComparer.Ordinal);
            foreach (var s in session.GetShops(settlement.SettlementId)) if (s.LayoutKey != null) shops[s.LayoutKey] = FitFor(s.Categories);
            var plan = session.GetSettlementLayout(settlement.SettlementId);
            if (plan == null) return LocalEnvironmentGenerator.Generate(session.GetOverview().Seed, settlement.SettlementId, settlement.RegionId, settlement.SettlementType, residents, shops);
            var layout = LocalEnvironmentGenerator.FromPlan(plan, session.GetOverview().Seed, settlement.SettlementId, settlement.RegionId, settlement.SettlementType, residents, shops);
            foreach (var h in session.GetHerds(settlement.SettlementId)) layout.Herds.Add(new LayoutHerd { CreatureId = h.CreatureId, Count = h.Count, PastureKey = h.PastureKey });
            return layout;
        }

        /// <summary>How a shop is fitted out: a smithy for arms and armour, a merchant's counter otherwise.</summary>
        public static ShopFit FitFor(IEnumerable<ShopCategory> categories)
        {
            foreach (var c in categories) if (c == ShopCategory.WeaponsAndArmour) return ShopFit.Smithy;
            return ShopFit.General;
        }

        internal static PlayerCharacterData RequirePlayer(CommandContext context, out CommandResult failure)
        {
            failure = null;
            if (!context.HasWorld) { failure = CommandResult.InvalidState("There is no active world."); return null; }
            var player = context.Committed.Player;
            if (player == null) failure = CommandResult.NotFound("This world has no player character.");
            return player;
        }
    }

    internal sealed class EnterSettlementHandler : ICommandHandler<EnterSettlementCommand>
    {
        public CommandResult Handle(EnterSettlementCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (command.SettlementId.IsEmpty) return CommandResult.Invalid("A settlement ID is required.");

            var world = context.Committed;
            var settlement = world.FindSettlement(command.SettlementId);
            if (settlement == null) return CommandResult.NotFound($"No settlement has ID {command.SettlementId}.");
            if (settlement.RegionId.IsEmpty) return CommandResult.Invalid($"'{settlement.Name}' belongs to no region.");
            if (world.FindRegion(settlement.RegionId) == null)
                return CommandResult.NotFound($"'{settlement.Name}' references region {settlement.RegionId}, which does not exist.");
            if (player.IsInPlace) return CommandResult.InvalidState($"{player.Name} must leave {world.FindPlace(player.PlaceId)?.Name ?? "the place"} first."); // Phase 13
            if (player.IsInSettlement)
            {
                return player.SettlementId == settlement.SettlementId
                    ? CommandResult.InvalidState($"{player.Name} is already in {settlement.Name}.")
                    : CommandResult.InvalidState($"{player.Name} must leave {world.FindSettlement(player.SettlementId)?.Name} first.");
            }

            var layout = ExplorationModule.LayoutFor(world, settlement);
            var p = context.Edit().Player;
            p.LocationKind = CharacterLocationKind.Settlement;
            p.SettlementId = settlement.SettlementId;
            p.RegionId = settlement.RegionId;
            p.Pose = layout.ArrivalPose;
            QuestRuntime.Visit(context.Edit(), settlement.SettlementId); // Phase 12: visited, and known
            context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = Realmkeeper.Simulation.Quests.QuestEventKind.ReachedSettlement, Subject = settlement.SettlementId.ToString(), Settlement = settlement.SettlementId });
            p.LastSettlementId = settlement.SettlementId;
            var home = Realmkeeper.Application.Places.PlaceRules.Arrive(context, context.Edit(), settlement.SettlementId); // Phase 13: an escort brought home
            return CommandResult.Ok($"{player.Name} enters {settlement.Name}." + (home.Count > 0 ? " " + string.Join(", ", home) + " is home at last." : string.Empty));
        }
    }

    internal sealed class ExitSettlementHandler : ICommandHandler<ExitSettlementCommand>
    {
        public CommandResult Handle(ExitSettlementCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("A fight is going on: withdraw from it or finish it before leaving."); // Phase 8

            var name = context.Committed.FindSettlement(player.SettlementId)?.Name;
            var p = context.Edit().Player;
            p.LocationKind = CharacterLocationKind.WorldMap;
            p.SettlementId = default;
            p.RegionId = default;
            return CommandResult.Ok($"{player.Name} leaves {name} for the world map.");
        }
    }

    internal sealed class SetPlayerPoseHandler : ICommandHandler<SetPlayerPoseCommand>
    {
        public CommandResult Handle(SetPlayerPoseCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
            if (command.SettlementId != player.SettlementId)
                return CommandResult.Invalid("The position refers to a different settlement than the one the character is in.");
            if (!command.Pose.IsFinite) return CommandResult.Invalid("The position is not a finite number.");

            var settlement = context.Committed.FindSettlement(player.SettlementId);
            var layout = ExplorationModule.LayoutFor(context.Committed, settlement);
            if (!layout.Contains(command.Pose.X, command.Pose.Z) || command.Pose.Y < -10f || command.Pose.Y > 100f)
                return CommandResult.Invalid($"Position {command.Pose} is outside {settlement.Name}.");

            context.Edit().Player.Pose = command.Pose;
            return CommandResult.Ok();
        }
    }

    internal sealed class SetCameraPerspectiveHandler : ICommandHandler<SetCameraPerspectiveCommand>
    {
        public CommandResult Handle(SetCameraPerspectiveCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!Enum.IsDefined(typeof(CameraPerspective), command.Perspective)) return CommandResult.Invalid("Unknown camera perspective.");
            if (player.Perspective == command.Perspective) return CommandResult.Ok();
            context.Edit().Player.Perspective = command.Perspective;
            return CommandResult.Ok(command.Perspective == CameraPerspective.FirstPerson ? "First person" : "Third person");
        }
    }

    internal sealed class TalkToResidentHandler : ICommandHandler<TalkToResidentCommand>
    {
        public CommandResult Handle(TalkToResidentCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
            if (command.NpcId.IsEmpty) return CommandResult.Invalid("A resident ID is required.");

            var world = context.Committed;
            var npc = world.FindNpc(command.NpcId);
            if (npc == null) return CommandResult.NotFound($"No resident has ID {command.NpcId}.");
            if (npc.IsDeceased) return CommandResult.InvalidState($"{npc.Name} is dead.");
            var at = npc.CurrentLocationId.IsEmpty ? null : world.FindLocation(npc.CurrentLocationId);
            if ((at?.SettlementId ?? npc.HomeSettlementId) != player.SettlementId) return CommandResult.Invalid($"{npc.Name} is not in this settlement today.");
            var settlement = world.FindSettlement(player.SettlementId);

            var edit = context.Edit().FindNpc(npc.NpcId);
            var firstMeeting = !edit.HasMetPlayer;
            edit.ConversationCount++;
            if (firstMeeting)
            {
                edit.MetPlayerOn = world.CurrentSimulationDate;
                context.ReportEvent(HistoricalEventType.PlayerMetResident, EventSeverity.Info, EventImportance.Minor,
                    new[] { settlement.SettlementId },
                    new Dictionary<string, string>
                    {
                        ["npc"] = npc.NpcId.ToString(),
                        ["name"] = npc.Name,
                        ["role"] = npc.Role.ToString(),
                        ["player"] = player.Name,
                    });
            }

            return CommandResult.Ok(ResidentDialogue.Line(npc, settlement, world, firstMeeting, context.Rules));
        }
    }

    internal sealed class PostLabourNoticeHandler : ICommandHandler<PostLabourNoticeCommand>
    {
        public CommandResult Handle(PostLabourNoticeCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
            if (command.SettlementId != player.SettlementId) return CommandResult.Invalid("Notices can only be posted in the settlement the character is in.");
            if (!Enum.IsDefined(typeof(ProductionSourceType), command.Focus)) return CommandResult.Invalid("Unknown production source.");

            var settlement = context.Committed.FindSettlement(command.SettlementId);
            if (settlement == null) return CommandResult.NotFound($"No settlement has ID {command.SettlementId}.");
            if (settlement.Economy.FindProduction(command.Focus) == null)
                return CommandResult.Invalid($"{settlement.Name} has no {command.Focus.ToString().ToLowerInvariant()} to favour.");
            if (settlement.Economy.LabourFocus == command.Focus)
                return CommandResult.Invalid($"A notice favouring {command.Focus.ToString().ToLowerInvariant()} is already posted in {settlement.Name}.");

            context.Edit().FindSettlement(settlement.SettlementId).Economy.LabourFocus = command.Focus;
            context.ReportEvent(HistoricalEventType.LabourNoticePosted, EventSeverity.Info, EventImportance.Notable,
                new[] { settlement.SettlementId },
                new Dictionary<string, string> { ["focus"] = command.Focus.ToString(), ["player"] = player.Name });
            return CommandResult.Ok($"Notice posted: {settlement.Name} will favour {command.Focus.ToString().ToLowerInvariant()} from tomorrow.");
        }
    }

    /// <summary>What residents say: built from authoritative settlement data, never invented numbers.</summary>
    internal static class ResidentDialogue
    {
        public static string Line(NpcData npc, SettlementData s, WorldData world, bool firstMeeting, Simulation.Systems.EconomyRules rules)
        {
            var greeting = firstMeeting ? $"\"Well met, stranger. I am {npc.Name}.\" " : $"\"Back again?\" says {npc.Name}. ";
            var food = s.FoodReserves;
            var demand = Math.Max(1, rules.DailyDemand(ResourceTypes.Food, s.Population, s.Economy.TotalWorkers));
            switch (npc.Role)
            {
                case NpcRole.Elder:
                    return greeting + $"\"{s.Name} counts {N(s.Population)} souls. Folk are {(s.Stability >= 60 ? "settled" : "uneasy")}, and trade is {(s.Prosperity >= 60 ? "good" : "slow")}.\"";
                case NpcRole.Merchant:
                    return greeting + $"\"Coin in the treasury: {N(s.ResourceInventory.Get(ResourceTypes.Coin))}. The roads keep my stall stocked.\"";
                case NpcRole.Farmer:
                case NpcRole.Innkeeper:
                    return greeting + $"\"The granary holds {N(food)} food, enough for about {N(food / demand)} days.\"";
                case NpcRole.Guard:
                    return greeting + "\"Keep to the streets and mind the carts at the gate.\"";
                case NpcRole.Smith:
                    return greeting + $"\"Iron's {(s.ResourceInventory.Get(ResourceTypes.Iron) < 20 ? "scarce" : "plentiful")} this season.\"";
                default:
                    return greeting + $"\"Today is {world.CurrentSimulationDate}. Plenty of work about.\"";
            }
        }

        private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
