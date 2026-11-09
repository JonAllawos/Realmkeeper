using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Suspends or resumes one trade route. A suspended route sends no new shipments; a
    /// shipment already on the road still arrives (and is paid for), so nothing is lost or
    /// created. Recorded in the chronicle.
    /// </summary>
    public sealed class SetTradeRouteSuspendedCommand : ICommand
    {
        public SetTradeRouteSuspendedCommand(TradeRouteId tradeRouteId, bool suspended)
        {
            TradeRouteId = tradeRouteId;
            Suspended = suspended;
        }

        public TradeRouteId TradeRouteId { get; }
        public bool Suspended { get; }
    }

    internal sealed class SetTradeRouteSuspendedHandler : ICommandHandler<SetTradeRouteSuspendedCommand>
    {
        public CommandResult Handle(SetTradeRouteSuspendedCommand command, CommandContext context)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            if (command.TradeRouteId.IsEmpty) return CommandResult.Invalid("A trade route ID is required.");
            var world = context.Committed;
            var route = world.FindTradeRoute(command.TradeRouteId);
            if (route == null) return CommandResult.NotFound($"No trade route has ID {command.TradeRouteId}.");

            var origin = world.FindSettlement(route.OriginId)?.Name ?? route.OriginId.ToString();
            var destination = world.FindSettlement(route.DestinationId)?.Name ?? route.DestinationId.ToString();
            if (route.IsSuspended == command.Suspended)
            {
                return CommandResult.Invalid(command.Suspended
                    ? $"The route from {origin} to {destination} is already suspended."
                    : $"The route from {origin} to {destination} is already trading.");
            }

            var edit = context.Edit().FindTradeRoute(route.TradeRouteId);
            edit.IsSuspended = command.Suspended;
            var onRoad = false;
            foreach (var s in world.Shipments)
            {
                if (s.TradeRouteId == route.TradeRouteId) onRoad = true;
            }

            if (command.Suspended && !onRoad) edit.Status = TradeRouteStatus.Suspended;
            if (!command.Suspended && edit.Status == TradeRouteStatus.Suspended) edit.Status = TradeRouteStatus.Active;

            context.ReportEvent(command.Suspended ? HistoricalEventType.TradeRouteSuspended : HistoricalEventType.TradeRouteResumed,
                command.Suspended ? EventSeverity.Warning : EventSeverity.Info, EventImportance.Notable,
                new[] { route.OriginId, route.DestinationId },
                new Dictionary<string, string> { ["route"] = route.TradeRouteId.ToString() });

            return CommandResult.Ok(command.Suspended
                ? $"Trade from {origin} to {destination} is suspended{(onRoad ? "; the shipment on the road will still arrive" : string.Empty)}."
                : $"Trade from {origin} to {destination} resumes at the next departure check.");
        }
    }
}
