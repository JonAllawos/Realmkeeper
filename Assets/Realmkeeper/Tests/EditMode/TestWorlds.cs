using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Tests.EditMode
{
    /// <summary>Small hand-built worlds for focused tests (independent of the generator).</summary>
    internal static class TestWorlds
    {
        public static readonly WorldDate Start = new WorldDate(412, 3, 1);

        public static WorldData SingleSettlement(int population, int food, int production, params string[] enabledSystems)
        {
            var settings = new WorldSettings { StartingDate = Start };
            settings.EnabledSimulationSystems.AddRange(enabledSystems);
            var world = WorldData.CreateNew(
                new WorldId(new System.Guid("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a0001")), "Test Realm", 1234, 1, settings);
            world.Settlements.Add(MakeSettlement("Ashford", "8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1001", population, food, production));
            return world;
        }

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
                FoodProductionPerDay = production,
            };
            settlement.ResourceInventory.Set(ResourceTypes.Food, food);
            return settlement;
        }
    }
}
