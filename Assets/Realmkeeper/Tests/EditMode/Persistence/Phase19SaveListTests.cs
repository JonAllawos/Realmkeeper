using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;

namespace Realmkeeper.Tests.EditMode.Persistence
{
    /// <summary>
    /// Phase 19: the world-selection screen's data. Saves are listed newest first from their headers (name, seed, date,
    /// schema), damaged and future saves are flagged instead of hidden, and deleting removes exactly one slot.
    /// </summary>
    public sealed class Phase19SaveListTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "realmkeeper-p19-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static WorldData World(string name, long seed)
        {
            var settings = new WorldSettings();
            return new RegionGenerator(new Xoshiro256RandomFactory()).Generate(new WorldGenerationRequest(name, seed, settings)).World;
        }

        [Test]
        public void SavedWorlds_AreListedNewestFirst_WithTheirHeaders()
        {
            var store = new JsonWorldStore(_dir);
            Assert.That(store.ListSaves(), Is.Empty, "No folder yet: an empty list, not an error.");
            Assert.That(store.Save(World("Ashvale", 11), "ashvale").IsSuccess, Is.True);
            Assert.That(store.Save(World("Brindle Reach", 22), "brindle").IsSuccess, Is.True);
            File.SetLastWriteTimeUtc(store.PathFor("ashvale"), DateTime.UtcNow.AddHours(-2));

            var list = store.ListSaves();
            Assert.That(list.Select(s => s.Slot), Is.EqualTo(new[] { "brindle", "ashvale" }));
            var b = list[0];
            Assert.That(b.WorldName, Is.EqualTo("Brindle Reach"));
            Assert.That(b.Seed, Is.EqualTo("22"));
            Assert.That(b.Date, Is.EqualTo("0412-03-01"));
            Assert.That(b.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(b.Readable && b.Compatible, Is.True);
            Assert.That(b.Bytes, Is.GreaterThan(1000));
        }

        [Test]
        public void DamagedAndNewerSaves_AreFlagged_NotHidden()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "broken" + JsonWorldStore.Extension), "not json at all");
            File.WriteAllText(Path.Combine(_dir, "future" + JsonWorldStore.Extension),
                "{\n  \"format\": \"realmkeeper.world\",\n  \"schemaVersion\": 999,\n  \"world\": { \"worldName\": \"Tomorrow\", \"seed\": 1, \"currentSimulationDate\": \"0500-01-01\" }\n}");
            File.WriteAllText(Path.Combine(_dir, "not a slot" + JsonWorldStore.Extension), "{}");

            var list = new JsonWorldStore(_dir).ListSaves();
            Assert.That(list.Select(s => s.Slot), Is.EquivalentTo(new[] { "broken", "future" }), "Files whose names are not valid slots are ignored.");
            Assert.That(list.Single(s => s.Slot == "broken").Readable, Is.False);
            var future = list.Single(s => s.Slot == "future");
            Assert.That(future.Readable, Is.True);
            Assert.That(future.Compatible, Is.False);
        }

        [Test]
        public void Delete_RemovesOneSlot_AndRefusesBadNames()
        {
            var store = new JsonWorldStore(_dir);
            store.Save(World("One", 1), "one");
            store.Save(World("Two", 2), "two");

            Assert.That(store.Delete("one").IsSuccess, Is.True);
            Assert.That(store.Exists("one"), Is.False);
            Assert.That(store.Exists("two"), Is.True);
            Assert.That(store.Delete("one").IsSuccess, Is.False, "Already gone.");
            Assert.That(store.Delete("../two").IsSuccess, Is.False, "No path tricks.");
            Assert.That(store.Exists("two"), Is.True);
        }
    }
}
