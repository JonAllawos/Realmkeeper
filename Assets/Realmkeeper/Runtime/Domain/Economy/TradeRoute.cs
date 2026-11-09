using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Economy
{
    public enum TradeRouteStatus
    {
        /// <summary>Ready to send the next shipment when one is due.</summary>
        Active = 0,

        /// <summary>A shipment is on the road.</summary>
        InTransit = 1,

        /// <summary>The last check found nothing worth shipping (no surplus, no need, or no payment).</summary>
        Idle = 2,

        /// <summary>A shipment is waiting because the destination's storage is full.</summary>
        Blocked = 3,
    }

    /// <summary>
    /// A one-way trade route along a road between two settlements. Routes come in pairs
    /// (A→B and B→A). At most one shipment per route is on the road at a time, which with the
    /// departure schedule guarantees no duplicate transfers.
    /// </summary>
    public sealed class TradeRouteData
    {
        public TradeRouteId TradeRouteId { get; set; }
        public SettlementId OriginId { get; set; }
        public SettlementId DestinationId { get; set; }

        /// <summary>Maximum units per shipment.</summary>
        public int CapacityPerShipment { get; set; }

        /// <summary>Days a shipment spends on the road (at least 1).</summary>
        public int TravelDays { get; set; }

        /// <summary>Days between departure checks (at least 1).</summary>
        public int FrequencyDays { get; set; }

        /// <summary>Percent of perishable goods (food) that survives the journey; other goods arrive whole.</summary>
        public int EfficiencyPercent { get; set; }

        public TradeRouteStatus Status { get; set; }

        /// <summary>Absolute day number (WorldDate.ToDayNumber) of the next departure check.</summary>
        public long NextDepartureDay { get; set; }

        public int ShipmentsDelivered { get; set; }

        /// <summary>Goods delivered over the route's lifetime, per resource.</summary>
        public ResourceInventory Delivered { get; set; } = new ResourceInventory();

        /// <summary>Day number of the last delivery recorded in the chronicle (rate-limits trade events).</summary>
        public long LastReportedDay { get; set; } = -1;

        /// <summary>Record of the most recent completed delivery (day -1 when none yet).</summary>
        public long LastDeliveryDay { get; set; } = -1;
        public ResourceTypeId LastDeliveryResource { get; set; }
        public int LastDeliveryQuantity { get; set; }
        public int LastDeliveryPayment { get; set; }

        public TradeRouteData Clone() => new TradeRouteData
        {
            TradeRouteId = TradeRouteId,
            OriginId = OriginId,
            DestinationId = DestinationId,
            CapacityPerShipment = CapacityPerShipment,
            TravelDays = TravelDays,
            FrequencyDays = FrequencyDays,
            EfficiencyPercent = EfficiencyPercent,
            Status = Status,
            NextDepartureDay = NextDepartureDay,
            ShipmentsDelivered = ShipmentsDelivered,
            Delivered = Delivered.Clone(),
            LastReportedDay = LastReportedDay,
            LastDeliveryDay = LastDeliveryDay,
            LastDeliveryResource = LastDeliveryResource,
            LastDeliveryQuantity = LastDeliveryQuantity,
            LastDeliveryPayment = LastDeliveryPayment,
        };
    }

    /// <summary>
    /// Goods on the road. While a shipment exists its goods belong to no settlement's
    /// inventory, so world totals are conserved: stock + in transit only changes by
    /// production, consumption, waste and recorded transit loss.
    /// </summary>
    public sealed class ShipmentData
    {
        public ShipmentId ShipmentId { get; set; }
        public TradeRouteId TradeRouteId { get; set; }
        public SettlementId OriginId { get; set; }

        /// <summary>Where the goods are currently heading (the origin again when returning).</summary>
        public SettlementId DestinationId { get; set; }

        public ResourceTypeId ResourceTypeId { get; set; }

        /// <summary>Units still on the road.</summary>
        public int Quantity { get; set; }

        /// <summary>Units already handed over at the destination (a full destination can take a shipment in parts).</summary>
        public int DeliveredQuantity { get; set; }

        /// <summary>Units lost in transit (perishables only), applied on first arrival.</summary>
        public int LostQuantity { get; set; }

        /// <summary>Coin received for the goods so far.</summary>
        public int Payment { get; set; }

        public long DepartureDay { get; set; }
        public long ArrivalDay { get; set; }

        /// <summary>Days spent waiting at a full destination.</summary>
        public int DaysWaiting { get; set; }

        /// <summary>True once transit loss has been applied (applied once, on first arrival).</summary>
        public bool LossApplied { get; set; }

        public bool IsReturning { get; set; }

        public ShipmentData Clone() => new ShipmentData
        {
            ShipmentId = ShipmentId,
            TradeRouteId = TradeRouteId,
            OriginId = OriginId,
            DestinationId = DestinationId,
            ResourceTypeId = ResourceTypeId,
            Quantity = Quantity,
            DeliveredQuantity = DeliveredQuantity,
            LostQuantity = LostQuantity,
            Payment = Payment,
            DepartureDay = DepartureDay,
            ArrivalDay = ArrivalDay,
            DaysWaiting = DaysWaiting,
            LossApplied = LossApplied,
            IsReturning = IsReturning,
        };
    }
}
