using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode
{
    /// <summary>Small hand-built worlds for focused tests (independent of the generator).</summary>
    internal static class TestWorlds
    {
        public static readonly WorldDate Start = new WorldDate(412, 3, 1);

        /// <summary>A settlement amply stocked with non-food goods, so food tests are not disturbed by other shortages.</summary>
        public const int AmpleGoods = 1000000;

        /// <summary>
        /// Rules where everyone works and one farmer grows one food a day, so a test can ask for
        /// an exact daily food production. Everything else is the production default.
        /// </summary>
        public static EconomyRules ExactRules() => new EconomyRules { LabourParticipationPercent = 100, FarmingRate = 100 };

        public static SimulationScheduler Scheduler(EconomyRules rules = null) =>
            StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory(), rules ?? ExactRules());

        public static string[] AllSystems => new[]
        {
            LabourSystem.Id, ProductionSystem.Id, TradeSystem.Id, ConsumptionSystem.Id,
            SettlementWellbeingSystem.Id, PopulationSystem.Id, ChronicleSystem.Id,
        };

        /// <summary>Labour, production and consumption only: the food balance without ratings or growth.</summary>
        public static string[] FoodSystems => new[] { LabourSystem.Id, ProductionSystem.Id, ConsumptionSystem.Id };

        public static WorldData SingleSettlement(int population, int food, int production, params string[] enabledSystems)
        {
            var settings = new WorldSettings { StartingDate = Start };
            settings.EnabledSimulationSystems.AddRange(enabledSystems);
            var world = WorldData.CreateNew(
                new WorldId(new System.Guid("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a0001")), "Test Realm", 1234, 1, settings);
            world.Settlements.Add(MakeSettlement("Ashford", "8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1001", population, food, production));
            return world;
        }

        /// <summary>
        /// A village whose farms grow exactly <paramref name="production"/> food a day under
        /// <see cref="ExactRules"/> (requires population ≥ production), with no storage limits.
        /// </summary>
        public static SettlementData MakeSettlement(string name, string id, int population, int food, int production)
        {
            var settlement = new SettlementData
            {
                SettlementId = new SettlementId(new System.Guid(id)),
                Name = name,
                SettlementType = SettlementType.Village,
                Position = new MapCoord(10, 10),
                Population = population,
                Prosperity = 50,
                Stability = 60,
            };
            settlement.ResourceInventory.Set(ResourceTypes.Food, food);
            foreach (var r in new[] { ResourceTypes.Timber, ResourceTypes.Stone, ResourceTypes.Iron, ResourceTypes.Coin })
                settlement.ResourceInventory.Set(r, AmpleGoods);

            settlement.Economy.Specialization = SettlementSpecialization.Agricultural;
            settlement.Economy.Production.Add(new ProductionAssignment
            {
                SourceType = ProductionSourceType.Farming,
                AllocationWeight = 1,
                WorkerCapacity = production,
                EfficiencyPercent = 100,
            });
            ChronicleSystem.Baseline(settlement);
            return settlement;
        }

        /// <summary>Two settlements joined by a one-way route A→B (and the reverse), for trade tests.</summary>
        public static WorldData TwoSettlements(out SettlementData a, out SettlementData b, out TradeRouteData aToB, params string[] enabledSystems)
        {
            var world = SingleSettlement(100, 0, 0, enabledSystems);
            a = world.Settlements[0];
            b = MakeSettlement("Brookmere", "8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1002", 100, 0, 0);
            world.Settlements.Add(b);
            aToB = new TradeRouteData
            {
                TradeRouteId = new TradeRouteId(new System.Guid("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a2001")),
                OriginId = a.SettlementId,
                DestinationId = b.SettlementId,
                CapacityPerShipment = 100,
                TravelDays = 2,
                FrequencyDays = 1,
                EfficiencyPercent = 90,
                NextDepartureDay = Start.ToDayNumber() + 1,
            };
            world.TradeRoutes.Add(aToB);
            return world;
        }
    }
}
