using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Generation
{
    public sealed class RegionGeneratorTests
    {
        private static readonly WorldId FixedId = new WorldId(new Guid("0b5cf6a2-1d8e-4f3e-9a5b-6c7d8e9f0a1b"));
        private static readonly long[] SampleSeeds = { 1, 2, 3, 42, 1337, 2024, 99991, -7, 123456789, 5000000000L, 8, 77 };

        private static GeneratedWorld Generate(long seed)
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            return new RegionGenerator(new Xoshiro256RandomFactory())
                .Generate(new WorldGenerationRequest("Testmark", seed, settings, FixedId));
        }

        internal static string MapFingerprint(RegionMap map)
        {
            var sb = new StringBuilder();
            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    sb.Append(BitConverter.SingleToInt32Bits(map.GetElevation(x, z)).ToString("x8"))
                      .Append((int)map.GetTerrain(x, z)).Append(map.IsRoad(x, z) ? 'r' : '.');
                }
            }

            foreach (var road in map.Roads) sb.Append(road.From).Append(road.To).Append(string.Join(";", road.Cells));
            foreach (var river in map.Rivers) sb.Append(string.Join(";", river));
            return sb.ToString();
        }

        [Test]
        public void SameSeed_ReproducesIdenticalRegionAndWorld()
        {
            var a = Generate(424242);
            var b = Generate(424242);

            Assert.That(MapFingerprint(a.Map), Is.EqualTo(MapFingerprint(b.Map)));
            Assert.That(WorldFingerprint.Of(a.World), Is.EqualTo(WorldFingerprint.Of(b.World)));
            Assert.That(a.World.GeneratorVersion, Is.EqualTo(RegionGenerator.Version));
        }

        [Test]
        public void DifferentSeeds_ProduceVisiblyDifferentRegions()
        {
            var a = Generate(1);
            var b = Generate(2);

            var differing = 0;
            for (var z = 0; z < a.Map.Depth; z++)
                for (var x = 0; x < a.Map.Width; x++)
                    if (a.Map.GetTerrain(x, z) != b.Map.GetTerrain(x, z)) differing++;

            Assert.That(differing, Is.GreaterThan(a.Map.Width * a.Map.Depth / 5), "At least 20% of cells should differ.");
            Assert.That(a.World.Settlements.Select(s => s.Position), Is.Not.EqualTo(b.World.Settlements.Select(s => s.Position)));
        }

        [Test]
        public void Settlements_AreValidlyPlacedAcrossManySeeds()
        {
            foreach (var seed in SampleSeeds)
            {
                var g = Generate(seed);
                var s = g.World.Settlements;
                Assert.That(s.Count, Is.EqualTo(3), $"seed {seed}");
                Assert.That(s.Select(x => x.SettlementType), Is.EquivalentTo(new[] { SettlementType.Town, SettlementType.Village, SettlementType.Hamlet }), $"seed {seed}");
                Assert.That(s.Select(x => x.SettlementId), Is.Unique, $"seed {seed}");
                Assert.That(s.Select(x => x.Name), Is.Unique, $"seed {seed}");

                foreach (var settlement in s)
                {
                    var p = settlement.Position;
                    Assert.That(g.Map.GetTerrain(p.X, p.Z), Is.EqualTo(TerrainType.Plains), $"seed {seed} {settlement.Name}");
                    Assert.That(p.X, Is.InRange(12, g.Map.Width - 13), $"seed {seed}");
                    Assert.That(p.Z, Is.InRange(12, g.Map.Depth - 13), $"seed {seed}");
                    foreach (var other in s.Where(o => o != settlement))
                        Assert.That(p.DistanceSquared(other.Position), Is.GreaterThanOrEqualTo(12 * 12), $"seed {seed}");
                }

                Assert.That(WorldValidator.Validate(g.World).IsValid, Is.True, $"seed {seed}");
            }
        }

        [Test]
        public void Terrain_IsValidAndContainsEveryLandscapeFeatureAcrossSeeds()
        {
            foreach (var seed in SampleSeeds)
            {
                var g = Generate(seed);
                Assert.That(RegionMapValidator.Validate(g.Map, g.World.Settlements).IsValid, Is.True, $"seed {seed}");

                var counts = new Dictionary<TerrainType, int>();
                for (var z = 0; z < g.Map.Depth; z++)
                    for (var x = 0; x < g.Map.Width; x++)
                    {
                        var t = g.Map.GetTerrain(x, z);
                        counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
                    }

                int Count(TerrainType t) => counts.TryGetValue(t, out var c) ? c : 0;
                var total = g.Map.Width * g.Map.Depth;
                Assert.That(Count(TerrainType.Plains), Is.GreaterThan(total / 10), $"seed {seed} plains");
                Assert.That(Count(TerrainType.Forest), Is.GreaterThan(total / 50), $"seed {seed} forest");
                Assert.That(Count(TerrainType.Hills), Is.GreaterThan(total / 100), $"seed {seed} hills");
                Assert.That(Count(TerrainType.Mountain), Is.GreaterThan(total / 100), $"seed {seed} mountains");
                Assert.That(Count(TerrainType.DeepWater) + Count(TerrainType.ShallowWater), Is.GreaterThan(150), $"seed {seed} lake");
                Assert.That(g.Map.Rivers.Count, Is.EqualTo(1), $"seed {seed} river");
                Assert.That(g.Map.Rivers[0].Count, Is.GreaterThan(20), $"seed {seed} river length");
            }
        }

        [Test]
        public void Roads_ConnectEverySettlementPairContiguously()
        {
            foreach (var seed in SampleSeeds.Take(5))
            {
                var g = Generate(seed);
                Assert.That(g.Map.Roads.Count, Is.EqualTo(3), $"seed {seed}");
                foreach (var road in g.Map.Roads)
                {
                    var from = g.World.FindSettlement(road.From);
                    var to = g.World.FindSettlement(road.To);
                    Assert.That(from, Is.Not.Null);
                    Assert.That(to, Is.Not.Null);
                    Assert.That(road.Cells.First(), Is.EqualTo(from.Position));
                    Assert.That(road.Cells.Last(), Is.EqualTo(to.Position));
                    Assert.That(road.Cells.All(c => g.Map.IsRoad(c.X, c.Z)), Is.True);
                }
            }
        }

        [Test]
        public void Economy_IsDerivedFromTerrain_AndTradeRoutesFollowTheRoads()
        {
            var rules = new EconomyRules();
            foreach (var seed in SampleSeeds.Take(6))
            {
                var g = Generate(seed);
                foreach (var s in g.World.Settlements)
                {
                    var e = s.Economy;
                    Assert.That(e.IsInitialized, Is.True, $"seed {seed}");
                    Assert.That(e.Specialization, Is.Not.EqualTo(Realmkeeper.Domain.Economy.SettlementSpecialization.None));
                    Assert.That(e.FindProduction(Realmkeeper.Domain.Economy.ProductionSourceType.Farming), Is.Not.Null, "Every site is on plains, so every settlement can farm.");
                    Assert.That(e.TotalWorkers, Is.LessThanOrEqualTo(EconomyMath.Workforce(s.Population, rules)));

                    // Fishing exists only where there is enough water nearby.
                    var fishing = e.FindProduction(Realmkeeper.Domain.Economy.ProductionSourceType.Fishing);
                    if (fishing != null) Assert.That(fishing.WorkerCapacity, Is.GreaterThanOrEqualTo(10));

                    // Founding-day ledger is the real projection: demand from population, production from staffed sources.
                    var food = e.FindFlow(Realmkeeper.Domain.Economy.ResourceTypes.Food);
                    Assert.That(food.Demand, Is.EqualTo(rules.Food.DailyConsumption(s.Population)));
                    Assert.That(food.Produced, Is.GreaterThan(0));
                    foreach (var stack in s.ResourceInventory.Stacks) Assert.That(stack.Quantity, Is.LessThanOrEqualTo(e.CapacityOf(stack.ResourceTypeId)));
                }

                // One route each way per road, between existing settlements.
                Assert.That(g.World.TradeRoutes.Count, Is.EqualTo(g.Map.Roads.Count * 2));
                Assert.That(g.World.TradeRoutes.Select(r => r.TradeRouteId).Distinct().Count(), Is.EqualTo(g.World.TradeRoutes.Count));
                foreach (var road in g.Map.Roads)
                {
                    Assert.That(g.World.TradeRoutes.Any(r => r.OriginId == road.From && r.DestinationId == road.To), Is.True);
                    Assert.That(g.World.TradeRoutes.Any(r => r.OriginId == road.To && r.DestinationId == road.From), Is.True);
                }
            }
        }

        [Test]
        public void RegenerateRegion_RebuildsIdenticalGeography_AndRejectsUnknownVersions()
        {
            var generator = new RegionGenerator(new Xoshiro256RandomFactory());
            var g = Generate(31337);

            var rebuilt = generator.RegenerateRegion(g.World);
            Assert.That(MapFingerprint(rebuilt), Is.EqualTo(MapFingerprint(g.Map)));

            g.World.GeneratorVersion = 999;
            Assert.Throws<NotSupportedException>(() => generator.RegenerateRegion(g.World));
        }

        [Test]
        public void RegionEditor_SculptsRecordsEditsAndReportsChangedBounds()
        {
            var map = Generate(5).Map;
            var editor = new RegionMapEditor(map);
            var center = new MapCoord(64, 64);
            var before = map.GetElevation(64, 64);
            RegionBounds? changed = null;
            editor.RegionChanged += b => changed = b;

            editor.Sculpt(center, 3, 0.05f);

            Assert.That(map.GetElevation(64, 64), Is.EqualTo(Math.Min(1f, before + 0.05f)).Within(1e-6f));
            Assert.That(editor.Edits.Count, Is.GreaterThan(20));
            Assert.That(changed.HasValue, Is.True);
            Assert.That(changed.Value.MinX, Is.EqualTo(61));
            Assert.That(changed.Value.MaxZ, Is.EqualTo(67));
            Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetTerrain(new MapCoord(-1, 0), TerrainType.Forest));
        }
    }
}
