using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Npcs
{
    /// <summary>
    /// Cost of the NPC engine at three sizes: the default world (3 settlements, 15 people), about 100
    /// people in 3 settlements, and about 300 people in 10 settlements. Measures whole simulated days
    /// (every system, in the transaction) and save/load. Bounds are generous ceilings that catch a
    /// change in complexity class, not tuning targets; the measured numbers are printed for the report.
    /// </summary>
    public sealed class NpcPerformanceTests
    {
        private const int Days = 60;

        [Test]
        public void DefaultWorld_15People() => Measure(Phase2Worlds.Generated(2026), "3 settlements / 15 people", 15.0);

        [Test]
        public void ThreeSettlements_About100People() => Measure(WithPeople(Phase2Worlds.Generated(2026), 33), "3 settlements / ~100 people", 40.0);

        [Test]
        public void TenSettlements_About300People() => Measure(WithPeople(WithSettlements(Phase2Worlds.Generated(2026), 10), 30), "10 settlements / ~300 people", 120.0);

        private static void Measure(WorldData world, string label, double maxMsPerDay)
        {
            Assert.That(WorldValidator.Validate(world).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(world).Errors.Take(5)));
            var people = world.Npcs.Count;
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            scheduler.AdvanceDays(world, 2); // warm-up (JIT)

            var watch = Stopwatch.StartNew();
            var run = scheduler.AdvanceDays(world, Days);
            watch.Stop();
            Assert.That(run.Completed, Is.True, string.Join("; ", run.Errors));
            var perDay = watch.Elapsed.TotalMilliseconds / Days;

            var serializer = new JsonWorldSerializer();
            var save = Stopwatch.StartNew();
            var json = serializer.Serialize(run.World);
            save.Stop();
            var load = Stopwatch.StartNew();
            var back = serializer.Deserialize(json);
            load.Stop();
            Assert.That(back.IsSuccess, Is.True);

            // The same world without the NPC engine, to isolate its share.
            var bare = world.DeepClone();
            bare.WorldSettings.EnabledSimulationSystems.Remove(NpcSystem.Id);
            var bareWatch = Stopwatch.StartNew();
            scheduler.AdvanceDays(bare, Days);
            bareWatch.Stop();
            var barePerDay = bareWatch.Elapsed.TotalMilliseconds / Days;

            var living = run.World.Npcs.Count(n => n.IsAlive);
            TestContext.Out.WriteLine(
                $"PERF {label}: {people} people ({living} alive after {Days} days), {run.World.NpcRelations.Count} relationships, " +
                $"{run.World.Npcs.Sum(n => n.Memories.Count)} memories, {run.World.Npcs.Sum(n => n.Knowledge.Count)} knowledge | " +
                $"day {perDay:F2} ms (without NPC engine {barePerDay:F2} ms) | save {save.Elapsed.TotalMilliseconds:F1} ms, load {load.Elapsed.TotalMilliseconds:F1} ms, " +
                $"{json.Length / 1024} KB");
            Assert.That(perDay, Is.LessThan(maxMsPerDay), $"{label}: {perDay:F2} ms per simulated day.");
        }

        /// <summary>Adds named people to every settlement (raising its population so they are members of it).</summary>
        private static WorldData WithPeople(WorldData world, int perSettlement)
        {
            var roles = (NpcRole[])Enum.GetValues(typeof(NpcRole));
            var given = new[] { "Aldric", "Edda", "Godric", "Maud", "Osric", "Petra", "Ulric", "Wenna", "Brand", "Sabine" };
            var k = 0;
            foreach (var s in world.Settlements)
            {
                var existing = world.Npcs.Count(n => n.HomeSettlementId == s.SettlementId);
                for (var i = existing; i < perSettlement; i++, k++)
                {
                    world.Npcs.Add(new NpcData
                    {
                        NpcId = new NpcId(new Guid(k + 100, 7, 7, 1, 2, 3, 4, 5, 6, 7, 8)),
                        Name = given[k % given.Length] + " " + (k + 1),
                        HomeSettlementId = s.SettlementId,
                        FactionId = s.FactionId,
                        Role = roles[k % roles.Length],
                    });
                }

                s.Population = Math.Max(s.Population, perSettlement * 4);
            }

            PoliticalMath.ClampMembers(world);
            NpcGenesis.EnsurePeople(world);
            return world;
        }

        /// <summary>Copies settlements (economy, governor and faction presence included) until there are <paramref name="count"/>.</summary>
        private static WorldData WithSettlements(WorldData world, int count)
        {
            var originals = world.Settlements.ToList();
            var n = 0;
            while (world.Settlements.Count < count)
            {
                var source = originals[n % originals.Count];
                var copy = source.Clone();
                copy.SettlementId = new SettlementId(new Guid(n + 1, 9, 9, 1, 2, 3, 4, 5, 6, 7, 8));
                copy.Name = source.Name + " " + (n + 2);
                world.Settlements.Add(copy);
                foreach (var f in world.Factions)
                {
                    var presence = f.Presence.FirstOrDefault(p => p.SettlementId == source.SettlementId);
                    if (presence == null) continue;
                    var c = presence.Clone();
                    c.SettlementId = copy.SettlementId;
                    f.Presence.Add(c);
                }

                n++;
            }

            NpcGenesis.EnsureLocations(world);
            return world;
        }
    }
}
