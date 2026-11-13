using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 5: shops and transactions. A shop belongs to a settlement location, is kept by an
    // existing named person, and owns its stock. Its money is its keeper's own savings
    // (NpcCondition.Wealth): there is no second purse.

    /// <summary>One line of a shop's stock (fungible units of one item).</summary>
    public sealed class ShopStockData
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }

        public ShopStockData Clone() => (ShopStockData)MemberwiseClone();
    }

    public sealed class ShopData
    {
        public ShopId ShopId { get; set; }
        public SettlementId SettlementId { get; set; }

        /// <summary>The location (a building or the market) where it trades.</summary>
        public LocationId LocationId { get; set; }

        public string Name { get; set; }
        public List<ShopCategory> Categories { get; set; } = new List<ShopCategory>();

        /// <summary>The named person who keeps it (an existing NPC; never a separate vendor).</summary>
        public NpcId ShopkeeperNpcId { get; set; }

        public List<ShopStockData> Stock { get; set; } = new List<ShopStockData>();

        /// <summary>Phase 11: particular objects the shop holds (a worn sword it bought, a fine piece): sold as themselves, not from the stock count.</summary>
        public List<ItemStackData> Goods { get; set; } = new List<ItemStackData>();

        /// <summary>Most particular items a shop keeps (the oldest are sold on elsewhere).</summary>
        public const int MaxGoods = 24;

        /// <summary>Opening hours, as minutes of the day.</summary>
        public int OpensAt { get; set; } = 8 * 60;
        public int ClosesAt { get; set; } = 18 * 60;

        public WorldDate LastRestockedOn { get; set; }

        /// <summary>Lifetime takings from the player and sales to the player (for history and goals).</summary>
        public int TotalSales { get; set; }
        public int TotalPurchases { get; set; }

        /// <summary>Phase 21 (ADR-0106): what kind of shop this is (<see cref="ShopKinds"/>); set on load for older shops.</summary>
        public ShopKind Kind { get; set; }

        /// <summary>Phase 21: where its stock came from, the most recent <see cref="MaxSupplyRecords"/> lines.</summary>
        public List<SupplyRecordData> Supply { get; set; } = new List<SupplyRecordData>();

        public const int MaxSupplyRecords = 40;

        /// <summary>Records that so many of an item came in (merging with today's record of the same item and origin).</summary>
        public void RecordSupply(long day, string itemId, int quantity, SupplyOrigin origin, string detail)
        {
            if (quantity <= 0) return;
            foreach (var r in Supply)
                if (r.Day == day && r.ItemId == itemId && r.Origin == origin && r.Detail == detail) { r.Quantity += quantity; return; }
            Supply.Add(new SupplyRecordData { Day = day, ItemId = itemId, Quantity = quantity, Origin = origin, Detail = detail });
            while (Supply.Count > MaxSupplyRecords) Supply.RemoveAt(0);
        }

        public int QuantityOf(string itemId)
        {
            foreach (var s in Stock) if (s.ItemId == itemId) return s.Quantity;
            return 0;
        }

        public ShopData Clone()
        {
            var c = (ShopData)MemberwiseClone();
            c.Categories = new List<ShopCategory>(Categories);
            c.Stock = new List<ShopStockData>(Stock.Count);
            foreach (var s in Stock) c.Stock.Add(s.Clone());
            c.Goods = Goods.ConvertAll(x => x.Clone());
            c.Supply = (Supply ?? new List<SupplyRecordData>()).ConvertAll(x => x.Clone());
            return c;
        }
    }

    public enum TransactionKind
    {
        /// <summary>The player bought from a shop.</summary>
        Purchase = 0,

        /// <summary>The player sold to a shop.</summary>
        Sale = 1,
    }

    /// <summary>A completed trade with the player (bounded history; also in the chronicle).</summary>
    public sealed class TransactionData
    {
        public const int MaxHistory = 500;

        public TransactionId TransactionId { get; set; }
        public WorldDate Date { get; set; }
        public int MinuteOfDay { get; set; }
        public TransactionKind Kind { get; set; }
        public ShopId ShopId { get; set; }
        public NpcId ShopkeeperNpcId { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int Total { get; set; }

        /// <summary>"player" (the only trader in this phase).</summary>
        public string Source { get; set; } = "player";

        public TransactionData Clone() => (TransactionData)MemberwiseClone();
    }
}
