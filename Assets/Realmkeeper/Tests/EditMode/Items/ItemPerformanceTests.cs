using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Items;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Items
{
    /// <summary>
    /// Phase 11 costs (ADR-0055): a large pack (150 instances) queried, filtered, sorted and compared; transfers through
    /// the session (each command validates the whole world); loot tables rolled in bulk; the item genesis; and saving
    /// and loading a world with full containers and work. Ceilings are generous; the numbers are printed for the report.
    /// </summary>
    public sealed class ItemPerformanceTests
    {
        [Test]
        public void Items_AreCheap()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var session = Phase2Worlds.Session(dir);
                ExplorationModule.Register(session);
                CombatTests.Create(session, "fighter");
                CombatTests.EnterTown(session);

                // A large pack: 150 instances of light things (weight is the limit, so they must be light).
                var w = session.CurrentWorldSnapshotForTests();
                var light = ItemCatalog.Items.Where(d => d.WeightGrams <= 300 && !d.Stackable).Select(d => d.Id).ToList();
                light.AddRange(ItemCatalog.Items.Where(d => d.Stackable && d.WeightGrams <= 100).Select(d => d.Id));
                for (var i = 0; i < 150; i++)
                {
                    var b = new byte[16];
                    BitConverter.GetBytes(i + 1).CopyTo(b, 0);
                    b[15] = 0x91;
                    w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(new Guid(b)), ItemCatalog.Find(light[i % light.Count]), 1, "perf"));
                }

                var chest = w.FindContainer(ItemGenesis.ChestKey(w.Player.SettlementId));
                Assert.That(new JsonWorldStore(dir).Save(w, "perf").IsSuccess, Is.True);
                Assert.That(session.Execute(new LoadWorldCommand("perf")).IsSuccess, Is.True);

                const int Queries = 100;
                var query = Stopwatch.StartNew();
                for (var i = 0; i < Queries; i++)
                {
                    var page = session.GetInventory(i % 2 == 0 ? null : "Consumables", i % 3 == 0 ? "a" : null, (InventorySort)(i % 5), i % 2 == 1);
                    Assert.That(page.TotalRows, Is.GreaterThanOrEqualTo(150));
                }

                query.Stop();

                var weapon = session.GetInventory().Rows.FirstOrDefault(r => r.CanEquip)?.StackId ?? default;
                var compare = Stopwatch.StartNew();
                for (var i = 0; i < 50; i++) session.CompareItem(weapon);
                compare.Stop();

                const int Transfers = 20;
                var transfer = Stopwatch.StartNew();
                for (var i = 0; i < Transfers; i++)
                {
                    var stack = session.GetInventory().Rows.First(r => !r.Attuned && !r.Bound);
                    Assert.That(session.Dispatch(new PutInContainerCommand(chest.Key, stack.StackId, stack.Quantity)).IsSuccess, Is.True);
                }

                transfer.Stop();

                var loot = Stopwatch.StartNew();
                var items = 0;
                for (ulong seed = 0; seed < 10000; seed++) items += LootTables.Roll("old_cache", new LootContext(), seed).Items.Count;
                loot.Stop();

                var world = session.CurrentWorldSnapshotForTests();
                var genesis = Stopwatch.StartNew();
                var fresh = world.DeepClone();
                fresh.Containers.Clear();
                fresh.GatheringSites.Clear();
                ItemGenesis.Ensure(fresh);
                genesis.Stop();

                var serializer = new JsonWorldSerializer();
                var json = serializer.Serialize(world);
                var save = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) json = serializer.Serialize(world);
                save.Stop();
                var load = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) Assert.That(serializer.Deserialize(json).IsSuccess, Is.True);
                load.Stop();
                var itemJson = Newtonsoft.Json.Linq.JObject.Parse(json)["world"];
                var itemBytes = itemJson["containers"].ToString().Length + itemJson["gatheringSites"].ToString().Length + itemJson["player"]["inventory"].ToString().Length;

                TestContext.Out.WriteLine($"Inventory query (150 rows, filter/search/sort): {query.Elapsed.TotalMilliseconds / Queries:0.00} ms each");
                TestContext.Out.WriteLine($"Comparison through the rules engine: {compare.Elapsed.TotalMilliseconds / 50:0.00} ms each");
                TestContext.Out.WriteLine($"Transfer to the chest (validated command): {transfer.Elapsed.TotalMilliseconds / Transfers:0.0} ms each");
                TestContext.Out.WriteLine($"Loot: 10000 rolls of old_cache in {loot.Elapsed.TotalMilliseconds:0} ms ({items} items)");
                TestContext.Out.WriteLine($"Item genesis for {fresh.Settlements.Count} settlements: {genesis.Elapsed.TotalMilliseconds:0.0} ms ({fresh.Containers.Count} containers, {fresh.GatheringSites.Count} sites)");
                TestContext.Out.WriteLine($"Save {json.Length / 1024} KB: {save.Elapsed.TotalMilliseconds / 5:0} ms; load {load.Elapsed.TotalMilliseconds / 5:0} ms; item sections {itemBytes / 1024} KB");

                Assert.That(query.Elapsed.TotalMilliseconds / Queries, Is.LessThan(20), "An inventory page in well under a frame.");
                Assert.That(compare.Elapsed.TotalMilliseconds / 50, Is.LessThan(20));
                Assert.That(transfer.Elapsed.TotalMilliseconds / Transfers, Is.LessThan(500), "A transfer validates the whole world; still interactive.");
                Assert.That(loot.Elapsed.TotalMilliseconds, Is.LessThan(2000));
                Assert.That(genesis.Elapsed.TotalMilliseconds, Is.LessThan(500));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
