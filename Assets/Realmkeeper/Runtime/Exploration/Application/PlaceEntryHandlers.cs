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
        public EnterBuildingCommand(string layoutKey, PlaceAction? approach = null)
        {
            LayoutKey = layoutKey;
            Approach = approach;
        }

        public string LayoutKey { get; }

        /// <summary>Phase 14 (ADR-0069): at night a home or shop is locked — Pick or Force gets in, and that is breaking in.</summary>
        public PlaceAction? Approach { get; }
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

            var locked = LockedNow(world, building.Function.Value);
            if (locked && command.Approach != PlaceAction.Pick && command.Approach != PlaceAction.Force)
                return CommandResult.InvalidState("The door is locked for the night. Pick the lock, or force it — either way, it is breaking in.");
            if (locked && command.Approach == PlaceAction.Pick && !PlaceCommandHandlers.HasItem(player, "thieves_tools")) return CommandResult.InvalidState("You need thieves' tools to pick a lock.");
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
            if (locked)
            {
                var limit = PlaceRules.AttemptsLeft(place, "street_door", command.Approach.ToString(), out var key);
                if (limit != null) return CommandResult.InvalidState(limit);
                var force = command.Approach == PlaceAction.Force;
                var roll = force ? PlaceRules.Check(w, Realmkeeper.Domain.Rpg.Skill.Athletics, 15, key) : PlaceRules.Check(w, Realmkeeper.Domain.Rpg.Skill.SleightOfHand, 13, key);
                if (!roll.Success)
                {
                    PlaceRules.Failed(place, "street_door", command.Approach.ToString());
                    return CommandResult.Ok(roll.Text + ". The door holds.");
                }

                var room = place.Layout.Room(entrance.A);
                var cell = PlaceRules.CellOf(PlaceRules.EntrancePose(place, entrance));
                var witnesses = CrimeRules.Witnesses(w, place, room, cell.X, cell.Z, force);
                CrimeRules.Record(context, w, force ? Realmkeeper.Domain.World.CrimeKind.PropertyDamage : Realmkeeper.Domain.World.CrimeKind.UnauthorizedEntry, place, room, default, default,
                    (force ? "forced the door of " : "broke into ") + place.Name, witnesses);
                var entered = PlaceCommandHandlers.EnterAt(context, w, place, entrance);
                var seen = witnesses.Count > 0 ? " " + witnesses[0].Npc.Name + (witnesses[0].How == "woken by the noise" ? " wakes at the noise and sees you" : " sees you come in") + "." : " Nobody stirs.";
                return CommandResult.Ok(roll.Text + ". You are in. " + entered.Message + seen);
            }

            return PlaceCommandHandlers.EnterAt(context, w, place, entrance);
        }

        /// <summary>Phase 14: homes, shops and workshops are locked from ten at night to six in the morning; taverns, chapels and guardhouses are not.</summary>
        public static bool LockedNow(WorldData w, Realmkeeper.Domain.Settlements.BuildingFunction f)
        {
            if (f == Realmkeeper.Domain.Settlements.BuildingFunction.Tavern || f == Realmkeeper.Domain.Settlements.BuildingFunction.Religious || f == Realmkeeper.Domain.Settlements.BuildingFunction.GuardPost) return false;
            return w.MinuteOfDay >= 22 * 60 || w.MinuteOfDay < 6 * 60;
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
