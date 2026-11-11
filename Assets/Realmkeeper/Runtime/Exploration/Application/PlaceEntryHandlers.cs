using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Simulation.Places;

namespace Realmkeeper.Exploration.Application
{
    /// <summary>
    /// Phase 13 (ADR-0063): go into a building through its door. The interior is planned from the building's function and
    /// footprint the first time anyone enters, then saved; leaving puts the character back outside the same door.
    /// </summary>
    public sealed class EnterBuildingCommand : ICommand
    {
        public EnterBuildingCommand(string layoutKey) { LayoutKey = layoutKey; }
        public string LayoutKey { get; }
    }

    internal sealed class EnterBuildingHandler : ICommandHandler<EnterBuildingCommand>
    {
        public CommandResult Handle(EnterBuildingCommand command, CommandContext context)
        {
            var player = ExplorationModule.RequirePlayer(context, out var failure);
            if (player == null) return failure;
            if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("Not during a fight.");
            if (player.Rpg == null || player.Rpg.State != Realmkeeper.Domain.Rpg.CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
            var world = context.Committed;
            var settlement = world.FindSettlement(player.SettlementId);
            var layout = ExplorationModule.LayoutFor(world, settlement);
            var building = layout.Buildings.Find(b => b.Key == command.LayoutKey);
            if (building == null) return CommandResult.NotFound("There is no such building here.");
            if (building.Kind == BuildingKind.Hall || building.Fit != ShopFit.None || building.Function == null || !InteriorGenerator.HasInterior(building.Function.Value))
                return CommandResult.InvalidState("That building is entered from the street (or not at all).");

            var w = context.Edit();
            var id = InteriorId(settlement.SettlementId, building.Key);
            var place = w.FindPlace(id);
            if (place == null)
            {
                LocationData location = null;
                foreach (var l in w.Locations) if (l.SettlementId == settlement.SettlementId && l.LayoutKey == building.Key) location = l;
                var door = LocalPaths.DoorOutside(building);
                var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), id);
                place = new PlaceData
                {
                    PlaceId = id, DefinitionId = "interior", Name = location?.Name ?? Label(building.Function.Value) + " in " + settlement.Name,
                    Description = "Inside " + (location?.Name ?? "a building") + ", " + settlement.Name + ".", Category = PlaceCategory.Interior,
                    ArchetypeId = "interior:" + InteriorGenerator.Plan(building.Function.Value), ParentKind = PlaceParentKind.Building,
                    ParentId = location != null ? location.LocationId.ToString() : building.Key, RegionId = settlement.RegionId, SettlementId = settlement.SettlementId,
                    LocationId = location?.LocationId ?? default, LayoutKey = building.Key, Cell = settlement.Position, Seed = seed, GeneratorVersion = Realmkeeper.Domain.Places.ArchetypeCatalog.GeneratorVersion,
                    ExitPose = new LocalPose(door.X, LocalPaths.StandingHeight(layout, door.X, door.Z), door.Z, LocalPaths.FacingOut(building)),
                    Layout = InteriorGenerator.Generate(new InteriorRequest
                    {
                        Function = building.Function.Value, WidthMetres = building.Width, DepthMetres = building.Depth, Storeys = building.Storeys,
                        Wealth = w.FindLayout(settlement.SettlementId)?.Identity?.Wealth ?? 50,
                    }, seed),
                    Discovery = PlaceDiscovery.Visited,
                };
                w.Places.Add(place);
                PlaceContents.Ensure(w, place);
            }

            var entrance = place.Layout.Entrance;
            return PlaceCommandHandlers.EnterAt(context, w, place, entrance);
        }

        public static string InteriorId(Realmkeeper.Domain.Identifiers.SettlementId s, string key) => "interior:" + s + ":" + key;

        private static string Label(Realmkeeper.Domain.Settlements.BuildingFunction f)
        {
            switch (f)
            {
                case Realmkeeper.Domain.Settlements.BuildingFunction.GuardPost: return "Guardhouse";
                case Realmkeeper.Domain.Settlements.BuildingFunction.Religious: return "Chapel";
                case Realmkeeper.Domain.Settlements.BuildingFunction.LargeHome: return "Large house";
                default: return f.ToString();
            }
        }
    }
}
