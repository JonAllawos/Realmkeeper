using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    public enum SettlementType
    {
        Hamlet = 0,
        Village = 1,
        Town = 2,
        City = 3,
        Fortress = 4,
    }

    /// <summary>
    /// Authoritative settlement state. Plain mutable data owned by <see cref="WorldData"/>;
    /// only the simulation scheduler (on a working copy) and world generation mutate it.
    /// All numeric state is integral so simulation stays bit-for-bit deterministic.
    /// </summary>
    public sealed class SettlementData
    {
        public const int MinRating = 0;
        public const int MaxRating = 100;
        public const int MaxNameLength = 64;

        public SettlementId SettlementId { get; set; }
        public string Name { get; set; }
        public SettlementType SettlementType { get; set; }

        /// <summary>Region the settlement belongs to. Must reference a region in <see cref="WorldData.Regions"/>.</summary>
        public RegionId RegionId { get; set; }

        public MapCoord Position { get; set; }
        public int Population { get; set; }

        /// <summary>0..100. Economic health.</summary>
        public int Prosperity { get; set; }

        /// <summary>0..100. Social order.</summary>
        public int Stability { get; set; }

        public ResourceInventory ResourceInventory { get; set; } = new ResourceInventory();

        /// <summary>Owning faction; <see cref="FactionId.Empty"/> when independent.</summary>
        public FactionId FactionId { get; set; }

        /// <summary>Simulation condition: food reserves have fallen below the shortage threshold.</summary>
        public bool IsInFoodShortage { get; set; }

        /// <summary>Production, storage, last-day ledger and shortage tracking.</summary>
        public SettlementEconomy Economy { get; set; } = new SettlementEconomy();

        public int FoodReserves => ResourceInventory?.Get(ResourceTypes.Food) ?? 0;

        public SettlementData Clone()
        {
            return new SettlementData
            {
                SettlementId = SettlementId,
                Name = Name,
                SettlementType = SettlementType,
                RegionId = RegionId,
                Position = Position,
                Population = Population,
                Prosperity = Prosperity,
                Stability = Stability,
                ResourceInventory = ResourceInventory?.Clone(),
                FactionId = FactionId,
                IsInFoodShortage = IsInFoodShortage,
                Economy = Economy?.Clone(),
            };
        }
    }
}
