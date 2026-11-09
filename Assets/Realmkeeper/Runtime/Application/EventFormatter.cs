using System;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Turns the structured facts the simulation records into readable sentences. The
    /// simulation never writes prose; changing wording (or adding a language later) only
    /// touches this class. Events from older saves that carry only text are shown as written.
    /// </summary>
    public static class EventFormatter
    {
        public static string Format(HistoricalEvent e, Func<SettlementId, string> nameOf)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.Data.Count == 0 && !string.IsNullOrEmpty(e.Description)) return e.Description;

            string Name(int index)
            {
                if (index >= e.SettlementIds.Count) return "a settlement";
                return nameOf?.Invoke(e.SettlementIds[index]) ?? "a settlement";
            }

            var s = Name(0);
            switch (e.EventType)
            {
                case HistoricalEventType.WorldFounded:
                    return string.IsNullOrEmpty(e.Description) ? "The chronicle begins." : e.Description;
                case HistoricalEventType.FoodShortageBegan:
                    return $"Food is running short in {s}: about {N(e, "daysLeft")} days of reserves remain.";
                case HistoricalEventType.FoodShortageEnded:
                    return $"The granaries of {s} have recovered ({N(e, "reserves")} food in store).";
                case HistoricalEventType.FoodReservesExhausted:
                    return $"{s} has run out of food. People will begin to starve after {N(e, "graceDays")} days.";
                case HistoricalEventType.StarvationBegan:
                    return $"{s} is starving: {N(e, "dailyLoss")} people are lost each day.";
                case HistoricalEventType.ResourceShortageBegan:
                    return $"{s} is short of {Resource(e)}: {N(e, "stock")} left against {N(e, "demand")} needed a day.";
                case HistoricalEventType.ResourceShortageEnded:
                    return $"{s} has enough {Resource(e)} again.";
                case HistoricalEventType.TradeShipmentDelivered:
                {
                    var lost = e.GetInt("lost");
                    var spoilage = lost > 0 ? $", {N(e, "lost")} spoiled on the road" : string.Empty;
                    return $"{N(e, "quantity")} {Resource(e)} from {s} reached {Name(1)} for {N(e, "payment")} coin{spoilage}.";
                }
                case HistoricalEventType.TradeShipmentReturned:
                    return $"A shipment of {Resource(e)} from {s} came home: {Name(1)} had no room to store it.";
                case HistoricalEventType.ProsperityChanged:
                    return $"Prosperity in {s} {(e.GetInt("to") > e.GetInt("from") ? "rose" : "fell")} from {N(e, "from")} to {N(e, "to")}.";
                case HistoricalEventType.StabilityChanged:
                    return $"Stability in {s} {(e.GetInt("to") > e.GetInt("from") ? "rose" : "fell")} from {N(e, "from")} to {N(e, "to")}.";
                case HistoricalEventType.SettlementGrew:
                    return $"{s} has grown to {N(e, "to")} people (from {N(e, "from")}).";
                case HistoricalEventType.SettlementDeclined:
                    return $"{s} has shrunk to {N(e, "to")} people (from {N(e, "from")}).";
                case HistoricalEventType.ProductionMilestone:
                    return $"{s} has produced {N(e, "amount")} {Resource(e)} since its founding.";
                case HistoricalEventType.EconomicMilestone:
                    return $"The treasury of {s} has passed {N(e, "treasury")} coin.";
                default:
                    return string.IsNullOrEmpty(e.Description) ? e.EventType.ToString() : e.Description;
            }
        }

        /// <summary>Display name for a resource key ("iron" → "Iron").</summary>
        public static string ResourceName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "-";
            return char.ToUpperInvariant(key[0]) + key.Substring(1).Replace('_', ' ').Replace('.', ' ');
        }

        private static string Resource(HistoricalEvent e) => e.Get("resource") ?? "goods";

        private static string N(HistoricalEvent e, string key)
        {
            var raw = e.Get(key);
            if (raw == null) return "?";
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n.ToString("N0", CultureInfo.InvariantCulture)
                : raw;
        }
    }
}
