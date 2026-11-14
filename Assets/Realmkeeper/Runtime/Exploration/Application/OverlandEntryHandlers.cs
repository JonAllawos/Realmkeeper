using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Application
{
    /// <summary>
    /// Phase 23 (ADR-0110): a settlement entered from the country, at its own arrival point (the layout's), with the visit
    /// and quest event the map's way in records. Leaving it returns to the country (<see cref="PlayerCharacterData.ReturnToOverland"/>).
    /// </summary>
    internal sealed class EnterSettlementFromOverlandHandler : ICommandHandler<EnterSettlementFromOverlandCommand>
    {
        public CommandResult Handle(EnterSettlementFromOverlandCommand command, CommandContext context)
        {
            var refused = OverlandCommandHandlers.Ready(context, out var player);
            if (refused != null) return refused;
            var site = OverlandCommandHandlers.SiteInReach(context, player, OverlandSiteKind.Settlement, command.SettlementId.ToString(), out refused);
            if (site == null) return refused;
            var world = context.Committed;
            var settlement = world.FindSettlement(command.SettlementId);
            var layout = ExplorationModule.LayoutFor(world, settlement);
            var w = context.Edit();
            var p = w.Player;
            p.LocationKind = CharacterLocationKind.Settlement;
            p.SettlementId = settlement.SettlementId;
            p.RegionId = settlement.RegionId;
            p.LastSettlementId = settlement.SettlementId;
            p.ReturnToOverland = true;
            p.Pose = layout.ArrivalPose;
            QuestRuntime.Visit(w, settlement.SettlementId);
            context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = Realmkeeper.Simulation.Quests.QuestEventKind.ReachedSettlement, Subject = settlement.SettlementId.ToString(), Settlement = settlement.SettlementId });
            var home = Realmkeeper.Application.Places.PlaceRules.Arrive(context, w, settlement.SettlementId);
            return CommandResult.Ok($"{p.Name} enters {settlement.Name} from the country." + (home.Count > 0 ? " " + string.Join(", ", home) + " is home at last." : string.Empty));
        }
    }
}
