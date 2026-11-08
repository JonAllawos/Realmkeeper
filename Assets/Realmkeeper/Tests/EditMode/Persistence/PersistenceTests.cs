using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Persistence
{
    public sealed class PersistenceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "realmkeeper-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>A generated world advanced long enough to carry events and changed state.</summary>
        private static WorldData PlayedWorld(int days = 300)
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            var g = new RegionGenerator(new Xoshiro256RandomFactory()).Generate(new WorldGenerationRequest("Saveland", 2718, settings));
            var run = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory()).AdvanceDays(g.World, days);
            Assert.That(run.Completed, Is.True);
            return run.World;
        }

        [Test]
        public void SaveLoad_RoundTripPreservesEntireWorldState()
        {
            var world = PlayedWorld();
            Assert.That(world.HistoricalEvents.Count, Is.GreaterThan(1), "Fixture should include simulation events.");
            var store = new JsonWorldStore(_dir);

            Assert.That(store.Save(world, "slot1").IsSuccess, Is.True);
            var load = store.Load("slot1");

            Assert.That(load.Status, Is.EqualTo(LoadStatus.Success), string.Join("; ", load.Errors));
            Assert.That(WorldFingerprint.Of(load.World), Is.EqualTo(WorldFingerprint.Of(world)));
        }

        [Test]
        public void SaveLoad_PreservesStableEntityIdentifiers()
        {
            var world = PlayedWorld(40);
            var serializer = new JsonWorldSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(world)).World;

            Assert.That(loaded.WorldId, Is.EqualTo(world.WorldId));
            Assert.That(loaded.Settlements.Select(s => s.SettlementId), Is.EqualTo(world.Settlements.Select(s => s.SettlementId)));
            Assert.That(loaded.Factions.Select(f => f.FactionId), Is.EqualTo(world.Factions.Select(f => f.FactionId)));
            Assert.That(loaded.HistoricalEvents.Select(e => e.EventId), Is.EqualTo(world.HistoricalEvents.Select(e => e.EventId)));
            Assert.That(loaded.Settlements.Select(s => s.FactionId), Is.EqualTo(world.Settlements.Select(s => s.FactionId)));
        }

        [Test]
        public void SaveLoad_PreservesExactDate_AndContinuedSimulationMatchesUninterruptedRun()
        {
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            var world = PlayedWorld(77);
            var serializer = new JsonWorldSerializer();

            var loaded = serializer.Deserialize(serializer.Serialize(world)).World;
            Assert.That(loaded.CurrentSimulationDate, Is.EqualTo(world.CurrentSimulationDate));
            Assert.That(loaded.WorldSettings.StartingDate, Is.EqualTo(world.WorldSettings.StartingDate));

            var uninterrupted = scheduler.AdvanceDays(world, 120).World;
            var resumed = scheduler.AdvanceDays(loaded, 120).World;
            Assert.That(WorldFingerprint.Of(resumed), Is.EqualTo(WorldFingerprint.Of(uninterrupted)));
        }

        [Test]
        public void Load_MissingFileReportsNotFound()
        {
            var result = new JsonWorldStore(_dir).Load("does-not-exist");
            Assert.That(result.Status, Is.EqualTo(LoadStatus.NotFound));
            Assert.That(result.World, Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{ this is not json")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"format\":\"something.else\",\"schemaVersion\":1}")]
        [TestCase("{\"format\":\"realmkeeper.world\",\"schemaVersion\":\"one\"}")]
        [TestCase("{\"format\":\"realmkeeper.world\",\"schemaVersion\":1,\"world\":{\"seed\":\"abc\"}}")]
        public void Load_GarbageOrForeignFilesAreRejectedAsCorrupt(string content)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "bad" + JsonWorldStore.Extension), content);

            var result = new JsonWorldStore(_dir).Load("bad");

            Assert.That(result.Status, Is.EqualTo(LoadStatus.Corrupt), string.Join("; ", result.Errors));
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void Load_FutureSchemaVersionIsUnsupported()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(PlayedWorld(5)));
            doc["schemaVersion"] = WorldData.CurrentSchemaVersion + 1;

            Assert.That(serializer.Deserialize(doc.ToString()).Status, Is.EqualTo(LoadStatus.UnsupportedVersion));
        }

        [Test]
        public void Load_SemanticallyInvalidDataIsRejectedWithReasons()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(PlayedWorld(5)));
            doc["world"]["settlements"][0]["population"] = -10;
            doc["world"]["settlements"][1]["settlementId"] = doc["world"]["settlements"][0]["settlementId"];

            var result = serializer.Deserialize(doc.ToString());

            Assert.That(result.Status, Is.EqualTo(LoadStatus.Invalid));
            Assert.That(string.Join("; ", result.Errors), Does.Contain("negative population").And.Contain("Duplicate SettlementId"));
        }

        [TestCase("settlementId", "not-a-guid")]
        [TestCase("settlementType", "Metropolis")]
        [TestCase("settlementType", "2")]
        public void Load_MalformedFieldsAreRejectedAsInvalid(string field, string value)
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(PlayedWorld(5)));
            doc["world"]["settlements"][0][field] = value;

            Assert.That(serializer.Deserialize(doc.ToString()).Status, Is.EqualTo(LoadStatus.Invalid));
        }

        [Test]
        public void Load_AppliesRegisteredMigrationsForOlderSchemas()
        {
            var current = new JsonWorldSerializer();
            var doc = JObject.Parse(current.Serialize(PlayedWorld(5)));

            // Simulate a hypothetical v0 layout that called the world name "name".
            doc["schemaVersion"] = 0;
            var world = (JObject)doc["world"];
            world["name"] = world["worldName"];
            world.Remove("worldName");

            Assert.That(current.Deserialize(doc.ToString()).Status, Is.EqualTo(LoadStatus.UnsupportedVersion), "No migration registered.");

            var migrating = new JsonWorldSerializer(new ISaveMigration[] { new RenameWorldNameMigration() });
            var result = migrating.Deserialize(doc.ToString());
            Assert.That(result.Status, Is.EqualTo(LoadStatus.Success), string.Join("; ", result.Errors));
            Assert.That(result.World.WorldName, Is.EqualTo("Saveland"));
        }

        [TestCase("../escape")]
        [TestCase("a/b")]
        [TestCase("")]
        [TestCase("C:")]
        public void Store_RejectsUnsafeSlotNames(string slot)
        {
            var store = new JsonWorldStore(_dir);
            Assert.That(store.Save(PlayedWorld(1), slot).IsSuccess, Is.False);
            Assert.That(store.Load(slot).IsSuccess, Is.False);
        }

        [Test]
        public void Store_OverwritesExistingSlotAtomically_AndRefusesInvalidWorlds()
        {
            var store = new JsonWorldStore(_dir);
            var early = PlayedWorld(1);
            var later = PlayedWorld(60);

            Assert.That(store.Save(early, "main").IsSuccess, Is.True);
            Assert.That(store.Save(later, "main").IsSuccess, Is.True);
            Assert.That(store.Load("main").World.CurrentSimulationDate, Is.EqualTo(later.CurrentSimulationDate));
            Assert.That(Directory.GetFiles(_dir).Length, Is.EqualTo(1), "No temporary files left behind.");

            later.Settlements[0].Stability = 999;
            Assert.That(store.Save(later, "main").IsSuccess, Is.False);
            Assert.That(store.Load("main").IsSuccess, Is.True, "Failed save must not damage the existing file.");
        }

        private sealed class RenameWorldNameMigration : ISaveMigration
        {
            public int SourceVersion => 0;

            public void Migrate(JObject document)
            {
                var world = (JObject)document["world"];
                world["worldName"] = world["name"];
                world.Remove("name");
            }
        }
    }
}
