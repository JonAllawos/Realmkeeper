using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 10 (ADR-0044): one kind of creature living in one part of a region, simulated in aggregate. Animals are
    /// not individual objects at this distance: a population is a count with a habitat, a range and a carrying
    /// capacity. Individuals become <see cref="CreatureInstanceData"/> only when an encounter draws them out.
    /// </summary>
    public sealed class CreaturePopulationData
    {
        /// <summary>Stable key: "{creature}@{region}:{range}" (deterministic, never reused).</summary>
        public string PopulationId { get; set; }

        public RegionId RegionId { get; set; }
        public string CreatureId { get; set; }

        /// <summary>The habitat the range is mostly made of (the creature\'s own habitat list allows it).</summary>
        public string Habitat { get; set; }

        /// <summary>The middle of the range on the region map, and how far it reaches (cells).</summary>
        public MapCoord Center { get; set; }
        public int RangeCells { get; set; }

        public int Count { get; set; }

        /// <summary>How many the range can feed (from its suitable cells).</summary>
        public int Capacity { get; set; }

        /// <summary>Deaths recorded against the population (encounters, hunting, predators), for the record.</summary>
        public int Deaths { get; set; }

        /// <summary>Individuals drawn into encounters so far (names their instances).</summary>
        public int Drawn { get; set; }

        public WorldDate LastUpdatedOn { get; set; }

        public CreaturePopulationData Clone() => (CreaturePopulationData)MemberwiseClone();
    }

    public enum CreatureInstanceState
    {
        Alive = 0,
        Dead = 1,
        Fled = 2,
    }

    /// <summary>
    /// Phase 10: one particular creature, made when an encounter draws it from a population (or a herd). Its fate is
    /// kept: a wolf killed stays dead and is never drawn again; one that fled goes back to its population.
    /// </summary>
    public sealed class CreatureInstanceData
    {
        public string InstanceId { get; set; }
        public string CreatureId { get; set; }
        public string Name { get; set; }

        /// <summary>The population or herd it came from.</summary>
        public string OriginId { get; set; }

        public CreatureInstanceState State { get; set; }
        public int CurrentHp { get; set; }
        public WorldDate FirstSeenOn { get; set; }
        public WorldDate DiedOn { get; set; }

        /// <summary>The last encounter it took part in.</summary>
        public EncounterId LastEncounterId { get; set; }

        public CreatureInstanceData Clone() => (CreatureInstanceData)MemberwiseClone();
    }

    /// <summary>
    /// Phase 10: a settlement's kept animals of one kind (cattle, sheep, goats, horses, poultry, dogs), sized from its
    /// farming and held on its pastures. The count is the economy's side of the farms the layout shows.
    /// </summary>
    public sealed class LivestockHerdData
    {
        /// <summary>Stable key: "{settlement}:{creature}".</summary>
        public string HerdId { get; set; }

        public SettlementId SettlementId { get; set; }
        public string CreatureId { get; set; }
        public int Count { get; set; }

        /// <summary>The pasture or yard in the settlement layout that holds them (null until the layout has one).</summary>
        public string PastureKey { get; set; }

        /// <summary>The farmer who keeps them, when a named one does.</summary>
        public NpcId KeeperNpcId { get; set; }

        public WorldDate LastUpdatedOn { get; set; }

        public LivestockHerdData Clone() => (LivestockHerdData)MemberwiseClone();
    }

    public static class FaunaBounds
    {
        public const int MaxPopulation = 2000;
        public const int MaxHerd = 400;

        /// <summary>Creature instances kept: the dead are remembered up to this many (oldest dropped first).</summary>
        public const int MaxInstances = 400;
    }
}
