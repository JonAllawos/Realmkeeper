using System;
using NUnit.Framework;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Tests.EditMode.Domain
{
    public sealed class WorldModelTests
    {
        [Test]
        public void CreateNew_InitializesIdentityDateAndEmptyCollections()
        {
            var settings = new WorldSettings { StartingDate = new WorldDate(300, 6, 12) };
            var id = WorldId.NewRandom();
            var world = WorldData.CreateNew(id, "  Vale of Ash ", 77, 1, settings);

            Assert.That(world.WorldId, Is.EqualTo(id));
            Assert.That(world.WorldName, Is.EqualTo("Vale of Ash"));
            Assert.That(world.Seed, Is.EqualTo(77));
            Assert.That(world.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(world.CurrentSimulationDate, Is.EqualTo(settings.StartingDate));
            Assert.That(world.Settlements, Is.Empty);
            Assert.That(world.HistoricalEvents, Is.Empty);
            Assert.That(world.WorldSettings, Is.Not.SameAs(settings), "Settings must be copied, not shared.");
            Assert.That(WorldValidator.Validate(world).IsValid, Is.True);
        }

        [Test]
        public void CreateNew_RejectsMissingIdentity()
        {
            Assert.Throws<ArgumentException>(() => WorldData.CreateNew(WorldId.Empty, "x", 1, 1, new WorldSettings()));
            Assert.Throws<ArgumentException>(() => WorldData.CreateNew(WorldId.NewRandom(), " ", 1, 1, new WorldSettings()));
        }

        [Test]
        public void DeepClone_IsIndependentOfOriginal()
        {
            var world = TestWorlds.SingleSettlement(population: 500, food: 1000, production: 50);
            var clone = world.DeepClone();
            clone.Settlements[0].Population = 1;
            clone.Settlements[0].ResourceInventory.Set(ResourceTypes.Food, 3);
            clone.WorldSettings.EnabledSimulationSystems.Add("other");

            Assert.That(world.Settlements[0].Population, Is.EqualTo(500));
            Assert.That(world.Settlements[0].FoodReserves, Is.EqualTo(1000));
            Assert.That(world.WorldSettings.EnabledSimulationSystems, Does.Not.Contain("other"));
            Assert.That(clone.Settlements[0].SettlementId, Is.EqualTo(world.Settlements[0].SettlementId));
        }

        [Test]
        public void SettlementValidation_ReportsEveryBrokenInvariant()
        {
            var settlement = new SettlementData
            {
                SettlementId = SettlementId.Empty,
                Name = "",
                Population = -1,
                Prosperity = 101,
                Stability = -5,
            };
            settlement.Economy.Production.Add(new ProductionAssignment
            {
                SourceType = ProductionSourceType.Farming,
                WorkerCapacity = 5,
                Workers = 9,
            });

            var result = WorldValidator.ValidateSettlement(settlement);

            // Empty ID, no name, negative population, prosperity and stability out of range,
            // more workers than capacity. ("More workers than people" is not double-reported
            // when the population itself is already invalid.)
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Count, Is.EqualTo(6), result.ToString());

            settlement.Population = 4;
            Assert.That(WorldValidator.ValidateSettlement(settlement).Errors, Has.Some.Contains("more workers than it has people"));
        }

        [Test]
        public void WorldValidation_DetectsDuplicateIdsAndDanglingFactionReferences()
        {
            var world = TestWorlds.SingleSettlement(100, 100, 10);
            var duplicate = world.Settlements[0].Clone();
            duplicate.Name = "Twin";
            world.Settlements.Add(duplicate);
            world.Settlements[0].FactionId = FactionId.NewRandom();

            var result = WorldValidator.Validate(world);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ToString(), Does.Contain("Duplicate SettlementId"));
            Assert.That(result.ToString(), Does.Contain("unknown faction"));
        }

        [Test]
        public void Inventory_RejectsNegativeQuantitiesAndDuplicateStacks()
        {
            var inventory = new ResourceInventory();
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Set(ResourceTypes.Food, -1));
            Assert.Throws<ArgumentException>(() => ResourceInventory.FromStacks(new[]
            {
                new ResourceStack(ResourceTypes.Food, 1),
                new ResourceStack(ResourceTypes.Food, 2),
            }));
            Assert.Throws<ArgumentException>(() => new ResourceTypeId("Food Stores"));
            Assert.Throws<ArgumentException>(() => inventory.Set(default(ResourceTypeId), 1));
        }

        [Test]
        public void Inventory_TryRemoveIsAllOrNothing_AndOverflowIsDetected()
        {
            var inventory = new ResourceInventory();
            inventory.Add(ResourceTypes.Timber, 10);

            Assert.That(inventory.TryRemove(ResourceTypes.Timber, 11), Is.False);
            Assert.That(inventory.Get(ResourceTypes.Timber), Is.EqualTo(10));
            Assert.That(inventory.TryRemove(ResourceTypes.Timber, 10), Is.True);
            Assert.That(inventory.Count, Is.EqualTo(0), "Zero stacks are removed.");

            inventory.Set(ResourceTypes.Coin, int.MaxValue);
            Assert.Throws<OverflowException>(() => inventory.Add(ResourceTypes.Coin, 1));
        }

        [Test]
        public void Inventory_EnumeratesInStableKeyOrder()
        {
            var inventory = new ResourceInventory();
            inventory.Set(ResourceTypes.Timber, 1);
            inventory.Set(ResourceTypes.Coin, 2);
            inventory.Set(ResourceTypes.Food, 3);

            var keys = string.Join(",", System.Linq.Enumerable.Select(inventory.Stacks, s => s.ResourceTypeId.Value));
            Assert.That(keys, Is.EqualTo("coin,food,timber"));
        }

        [TestCase(1, 1, 1, 0L)]
        [TestCase(1, 12, 30, 359L)]
        [TestCase(2, 1, 1, 360L)]
        [TestCase(412, 3, 15, 411L * 360 + 2 * 30 + 14)]
        public void WorldDate_DayNumberRoundTrips(int year, int month, int day, long expected)
        {
            var date = new WorldDate(year, month, day);
            Assert.That(date.ToDayNumber(), Is.EqualTo(expected));
            Assert.That(WorldDate.FromDayNumber(expected), Is.EqualTo(date));
        }

        [Test]
        public void WorldDate_RollsOverMonthsAndYears_AndRejectsInvalidParts()
        {
            Assert.That(new WorldDate(10, 1, 30).AddDays(1), Is.EqualTo(new WorldDate(10, 2, 1)));
            Assert.That(new WorldDate(10, 12, 30).AddDays(1), Is.EqualTo(new WorldDate(11, 1, 1)));
            Assert.That(new WorldDate(10, 1, 1).AddDays(WorldDate.DaysPerYear), Is.EqualTo(new WorldDate(11, 1, 1)));
            Assert.That(new WorldDate(10, 3, 1).AddDays(-1), Is.EqualTo(new WorldDate(10, 2, 30)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldDate(10, 13, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldDate(10, 2, 31));
            Assert.That(default(WorldDate).IsValidDate, Is.False);
        }
    }
}
