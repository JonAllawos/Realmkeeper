using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Derives each settlement's economy from the land around it, and creates trade routes
    /// along the roads. Pure function of (world, map, route-id stream), so a freshly generated
    /// world and an upgraded Phase 1 save of the same seed get identical economies.
    ///
    /// Land → capacity (workers the land can employ):
    ///   Farming   = 2.5 × plains/shore cells within 9, excluding the built-up core
    ///   Fishing   = water cells within 9, only if there are at least 10 (a real fishery)
    ///   Hunting   = forest cells within 12 ÷ 4
    ///   Logging   = forest cells within 16 ÷ 2
    ///   Quarrying = hills + mountain cells within 16 ÷ 2
    ///   Mining    = (mountain + hills ÷ 4) within 20 ÷ 2, only if that ore count is at least 5
    ///   Trade     = by settlement size, +10 per road
    /// The specialization with the highest output value is primary (+25% efficiency, double
    /// labour weight); a runner-up worth at least half as much is secondary (+10%, 1.5×).
    /// </summary>
    public sealed class EconomyInitializer
    {
        public const int CellsPerTravelDay = 25;
        public const int RouteFrequencyDays = 2;

        // Catchment radii in cells (one cell is 2 world units).
        public const int FarmRadius = 9;
        public const int FishingRadius = 9;
        public const int HuntingRadius = 12;
        public const int LoggingRadius = 16;
        public const int QuarryRadius = 16;
        public const int MiningRadius = 20;

        /// <summary>Cells around the centre covered by buildings and streets (no fields there).</summary>
        public static int BuiltUpRadius(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City: return 8;
                case SettlementType.Town: return 6;
                case SettlementType.Fortress:
                case SettlementType.Village: return 5;
                default: return 4;
            }
        }

        private readonly EconomyRules _rules;

        public EconomyInitializer(EconomyRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public void Initialize(WorldData world, RegionMap map, IRandomSource routeIds)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (map == null) throw new ArgumentNullException(nameof(map));

            foreach (var s in world.Settlements)
            {
                if (s.Economy == null) s.Economy = new SettlementEconomy();
                if (!s.Economy.IsInitialized) InitializeSettlement(world, map, s);
            }

            if (world.TradeRoutes.Count == 0) BuildRoutes(world, map, routeIds);
        }

        // ----- Settlements ------------------------------------------------------------------

        private void InitializeSettlement(WorldData world, RegionMap map, SettlementData s)
        {
            var e = s.Economy;
            var builtUp = BuiltUpRadius(s.SettlementType);
            var fertile = Count(map, s.Position, FarmRadius, TerrainType.Plains, TerrainType.Shore)
                          - Count(map, s.Position, builtUp, TerrainType.Plains, TerrainType.Shore);
            var water = Count(map, s.Position, FishingRadius, TerrainType.ShallowWater, TerrainType.DeepWater, TerrainType.River);
            var nearForest = Count(map, s.Position, HuntingRadius, TerrainType.Forest);
            var forest = Count(map, s.Position, LoggingRadius, TerrainType.Forest);
            var rock = Count(map, s.Position, QuarryRadius, TerrainType.Hills, TerrainType.Mountain);
            var mountain = Count(map, s.Position, MiningRadius, TerrainType.Mountain);
            var hills = Count(map, s.Position, MiningRadius, TerrainType.Hills);
            var roads = 0;
            foreach (var road in map.Roads)
            {
                if (road.From == s.SettlementId || road.To == s.SettlementId) roads++;
            }

            var ore = mountain + hills / 4;
            var capacity = new Dictionary<ProductionSourceType, int>
            {
                [ProductionSourceType.Farming] = Math.Max(0, fertile) * 5 / 2,
                [ProductionSourceType.Fishing] = water >= 10 ? water : 0,
                [ProductionSourceType.Hunting] = nearForest / 4,
                [ProductionSourceType.Logging] = forest,
                [ProductionSourceType.Quarrying] = rock,
                [ProductionSourceType.Mining] = ore >= 5 ? ore : 0,
                [ProductionSourceType.Trade] = BaseTradeCapacity(s.SettlementType) + 10 * roads,
            };

            ChooseSpecialization(capacity, _rules, out var primary, out var secondary);
            e.Specialization = primary;
            e.SecondarySpecialization = secondary;

            e.Production.Clear();
            foreach (var source in ProductionSources.All)
            {
                var cap = capacity[source];
                if (cap <= 0) continue;
                var spec = ProductionSources.SpecializationOf(source);
                var weight = BaseWeight(source);
                var efficiency = 100;
                if (spec == primary) { weight *= 2; efficiency += 25; }
                else if (spec == secondary) { weight = weight * 3 / 2; efficiency += 10; }
                e.Production.Add(new ProductionAssignment
                {
                    SourceType = source,
                    AllocationWeight = weight,
                    WorkerCapacity = cap,
                    EfficiencyPercent = efficiency,
                });
            }

            // Storage, then make starting stock fit it and cover basic needs.
            foreach (var resource in ResourceTypes.All) e.StorageCapacity.Set(resource, StorageCapacity(s.SettlementType, resource));
            EconomyMath.AllocateLabour(s, _rules, world.CurrentSimulationDate);
            var workers = e.TotalWorkers;
            foreach (var resource in ResourceTypes.All)
            {
                var demand = _rules.DailyDemand(resource, s.Population, workers);
                var minimum = resource == ResourceTypes.Food ? 0 : demand * 20;
                var stock = Math.Max(s.ResourceInventory.Get(resource), minimum);
                s.ResourceInventory.Set(resource, Math.Min(stock, e.CapacityOf(resource)));
            }

            Project(world, s);
            var factors = EconomyMath.Factors(s, _rules);
            e.ProsperityTarget = EconomyMath.ProsperityTarget(factors);
            e.StabilityTarget = EconomyMath.StabilityTarget(factors, s.Prosperity);
            ChronicleSystem.Baseline(s);
        }

        /// <summary>
        /// Founding-day ledger: what each source will produce and the population needs, without
        /// changing stock, so the inspector shows real figures before the first day is simulated.
        /// </summary>
        private void Project(WorldData world, SettlementData s)
        {
            var e = s.Economy;
            var day = world.CurrentSimulationDate.ToDayNumber();
            e.LedgerDay = -1;
            foreach (var p in e.Production)
            {
                var amount = EconomyMath.SourceOutput(p, _rules, world.CurrentSimulationDate);
                EconomyMath.Flow(e, p.Output, day).Produced += amount;
                e.LastDaySources.Add(new SourceOutput { SourceType = p.SourceType, Workers = p.Workers, Amount = amount });
            }

            var workers = e.TotalWorkers;
            foreach (var resource in ResourceTypes.All)
            {
                var demand = _rules.DailyDemand(resource, s.Population, workers);
                var flow = EconomyMath.Flow(e, resource, day);
                flow.Demand = demand;
                flow.Consumed = Math.Min(demand, s.ResourceInventory.Get(resource));
                flow.Shortfall = demand - flow.Consumed;
            }
        }

        private static void ChooseSpecialization(Dictionary<ProductionSourceType, int> capacity, EconomyRules rules,
            out SettlementSpecialization primary, out SettlementSpecialization secondary)
        {
            // Output value potential of each specialization: Σ capacity × rate × resource value.
            long Potential(ProductionSourceType source) =>
                (long)capacity[source] * rules.OutputPer100Workers(source) * rules.ValueOf(ProductionSources.OutputOf(source));

            var scores = new[]
            {
                (SettlementSpecialization.Agricultural, Potential(ProductionSourceType.Farming) + Potential(ProductionSourceType.Fishing) + Potential(ProductionSourceType.Hunting)),
                (SettlementSpecialization.Forestry, Potential(ProductionSourceType.Logging)),
                (SettlementSpecialization.Quarrying, Potential(ProductionSourceType.Quarrying)),
                (SettlementSpecialization.Mining, Potential(ProductionSourceType.Mining)),
                (SettlementSpecialization.Trade, Potential(ProductionSourceType.Trade)),
            };

            primary = SettlementSpecialization.None;
            secondary = SettlementSpecialization.None;
            long best = 0, second = 0;
            foreach (var (spec, score) in scores)
            {
                if (score > best)
                {
                    second = best;
                    secondary = primary;
                    best = score;
                    primary = spec;
                }
                else if (score > second)
                {
                    second = score;
                    secondary = spec;
                }
            }

            if (second * 3 < best) secondary = SettlementSpecialization.None;
        }

        private static int BaseWeight(ProductionSourceType source)
        {
            switch (source)
            {
                case ProductionSourceType.Farming: return 50;
                case ProductionSourceType.Fishing: return 20;
                case ProductionSourceType.Hunting: return 6;
                case ProductionSourceType.Logging: return 14;
                case ProductionSourceType.Quarrying: return 8;
                case ProductionSourceType.Mining: return 6;
                default: return 10;
            }
        }

        private static int BaseTradeCapacity(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City: return 500;
                case SettlementType.Town: return 300;
                case SettlementType.Village: return 60;
                default: return 15;
            }
        }

        public static int StorageCapacity(SettlementType type, ResourceTypeId resource)
        {
            int scale;
            switch (type)
            {
                case SettlementType.City: scale = 250; break;
                case SettlementType.Town: scale = 100; break;
                case SettlementType.Fortress:
                case SettlementType.Village: scale = 40; break;
                default: scale = 15; break;
            }

            if (resource == ResourceTypes.Food) return 200 * scale;
            if (resource == ResourceTypes.Timber) return 40 * scale;
            if (resource == ResourceTypes.Stone) return 30 * scale;
            if (resource == ResourceTypes.Iron) return 15 * scale;
            return 10000 * scale; // coin
        }

        private static int Count(RegionMap map, MapCoord center, int radius, params TerrainType[] types)
        {
            var count = 0;
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dz * dz > radius * radius) continue;
                    var x = center.X + dx;
                    var z = center.Z + dz;
                    if (!map.InBounds(x, z)) continue;
                    var t = map.GetTerrain(x, z);
                    foreach (var type in types)
                    {
                        if (t == type) { count++; break; }
                    }
                }
            }

            return count;
        }

        // ----- Trade routes -----------------------------------------------------------------

        private static void BuildRoutes(WorldData world, RegionMap map, IRandomSource ids)
        {
            var start = world.CurrentSimulationDate.ToDayNumber();
            var index = 0;
            foreach (var road in map.Roads)
            {
                var a = world.FindSettlement(road.From);
                var b = world.FindSettlement(road.To);
                if (a == null || b == null) continue;

                var travel = Math.Max(1, (road.Cells.Count + CellsPerTravelDay - 1) / CellsPerTravelDay);
                var capacity = Math.Min(RouteCapacity(a.SettlementType), RouteCapacity(b.SettlementType));
                var efficiency = Math.Max(85, 100 - 2 * travel);
                foreach (var (from, to) in new[] { (a, b), (b, a) })
                {
                    world.TradeRoutes.Add(new TradeRouteData
                    {
                        TradeRouteId = new TradeRouteId(DeterministicGuid.Next(ids)),
                        OriginId = from.SettlementId,
                        DestinationId = to.SettlementId,
                        CapacityPerShipment = capacity,
                        TravelDays = travel,
                        FrequencyDays = RouteFrequencyDays,
                        EfficiencyPercent = efficiency,
                        Status = TradeRouteStatus.Active,
                        NextDepartureDay = start + 1 + index % RouteFrequencyDays,
                    });
                    index++;
                }
            }
        }

        private static int RouteCapacity(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City: return 400;
                case SettlementType.Town: return 240;
                case SettlementType.Village: return 150;
                default: return 80;
            }
        }
    }
}
