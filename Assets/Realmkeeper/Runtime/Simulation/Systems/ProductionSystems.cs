using System;
using Realmkeeper.Domain.Economy;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// Start of the economic day: assigns today's workforce (a share of the population) to
    /// production sources by their allocation weights, capped by what the land can employ.
    /// </summary>
    public sealed class LabourSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.labour";

        private readonly EconomyRules _rules;

        public LabourSystem(EconomyRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        public string SystemId => Id;
        public int ExecutionOrder => 50;

        public void Execute(SimulationDayContext context)
        {
            foreach (var s in context.World.Settlements)
            {
                if (!s.Economy.IsInitialized) continue;
                EconomyMath.AllocateLabour(s, _rules, context.Date);
            }
        }
    }

    /// <summary>
    /// Each assigned source produces its resource. Output that does not fit in storage is
    /// recorded as wasted rather than stored, so stock never exceeds capacity.
    /// </summary>
    public sealed class ProductionSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.production";

        private readonly EconomyRules _rules;

        public ProductionSystem(EconomyRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        public string SystemId => Id;
        public int ExecutionOrder => 100;

        public void Execute(SimulationDayContext context)
        {
            var day = context.Date.ToDayNumber();
            foreach (var s in context.World.Settlements)
            {
                var e = s.Economy;
                if (!e.IsInitialized) continue;
                foreach (var p in e.Production)
                {
                    var amount = EconomyMath.SourceOutput(p, _rules, context.Date);
                    var resource = p.Output;
                    var flow = EconomyMath.Flow(e, resource, day);
                    e.LastDaySources.Add(new SourceOutput { SourceType = p.SourceType, Workers = p.Workers, Amount = amount });
                    if (amount <= 0) continue;

                    var stock = s.ResourceInventory.Get(resource);
                    var free = (long)e.CapacityOf(resource) - stock;
                    var stored = (int)Math.Max(0, Math.Min(amount, free));
                    if (stored > 0) s.ResourceInventory.Add(resource, stored);
                    flow.Produced += stored;
                    flow.Wasted += amount - stored;
                    e.LifetimeProduced.Add(resource, stored);
                }
            }
        }
    }
}
