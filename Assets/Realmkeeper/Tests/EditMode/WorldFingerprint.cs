using System.Text;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Tests.EditMode
{
    /// <summary>
    /// Canonical text dump of a world, used to assert exact state equality. Covers every
    /// persisted field: settlements and their economies, trade routes, shipments and
    /// structured events.
    /// </summary>
    internal static class WorldFingerprint
    {
        public static string Of(WorldData w)
        {
            var sb = new StringBuilder();
            sb.Append(w.WorldId).Append('|').Append(w.WorldName).Append('|').Append(w.Seed).Append('|')
              .Append(w.GeneratorVersion).Append('|').Append(w.SchemaVersion).Append('|')
              .Append(w.CurrentSimulationDate.ToIsoString()).Append('|')
              .Append(w.WorldSettings.SimulationSpeed).Append('|').Append(w.WorldSettings.StartingDate.ToIsoString()).Append('|')
              .Append(string.Join(",", w.WorldSettings.EnabledSimulationSystems)).AppendLine();
            foreach (var f in w.Factions) sb.Append("F ").Append(f.FactionId).Append(' ').Append(f.Name).AppendLine();
            foreach (var s in w.Settlements)
            {
                sb.Append("S ").Append(s.SettlementId).Append(' ').Append(s.Name).Append(' ').Append(s.SettlementType)
                  .Append(' ').Append(s.Position).Append(" pop=").Append(s.Population).Append(" pr=").Append(s.Prosperity)
                  .Append(" st=").Append(s.Stability).Append(" fac=").Append(s.FactionId)
                  .Append(" short=").Append(s.IsInFoodShortage);
                foreach (var r in s.ResourceInventory.Stacks) sb.Append(' ').Append(r.ResourceTypeId).Append('=').Append(r.Quantity);
                sb.AppendLine();

                var e = s.Economy;
                sb.Append("  econ ").Append(e.Specialization).Append('/').Append(e.SecondarySpecialization)
                  .Append(" ledger=").Append(e.LedgerDay).Append(" dwf=").Append(e.DaysWithoutFood)
                  .Append(" prog=").Append(e.PopulationProgressMilli).Append(" pt=").Append(e.ProsperityTarget)
                  .Append(" stt=").Append(e.StabilityTarget).Append(" ta=").Append(e.TradeActivity)
                  .Append(" mk=").Append(e.Markers.ReportedProsperity).Append(',').Append(e.Markers.ReportedStability)
                  .Append(',').Append(e.Markers.ReportedPopulation).Append(',').Append(e.Markers.CoinMilestone)
                  .Append(',').Append(e.Markers.LastFoodExhaustedReportDay).Append(',').Append(e.Markers.LastStarvationReportDay);
                foreach (var pair in e.Markers.ProductionMilestoneIndex) sb.Append(' ').Append(pair.Key).Append('#').Append(pair.Value);
                sb.AppendLine();
                foreach (var p in e.Production)
                    sb.Append("  P ").Append(p.SourceType).Append(" w=").Append(p.AllocationWeight).Append(" cap=").Append(p.WorkerCapacity)
                      .Append(" eff=").Append(p.EfficiencyPercent).Append(" workers=").Append(p.Workers).AppendLine();
                foreach (var c in e.StorageCapacity.Stacks) sb.Append("  C ").Append(c.ResourceTypeId).Append('=').Append(c.Quantity).AppendLine();
                foreach (var l in e.LifetimeProduced.Stacks) sb.Append("  L ").Append(l.ResourceTypeId).Append('=').Append(l.Quantity).AppendLine();
                foreach (var f in e.LastDay)
                    sb.Append("  D ").Append(f.ResourceTypeId).Append(' ').Append(f.Produced).Append(' ').Append(f.Wasted).Append(' ')
                      .Append(f.Demand).Append(' ').Append(f.Consumed).Append(' ').Append(f.Shortfall).Append(' ')
                      .Append(f.Imported).Append(' ').Append(f.Exported).AppendLine();
                foreach (var so in e.LastDaySources) sb.Append("  O ").Append(so.SourceType).Append(' ').Append(so.Workers).Append(' ').Append(so.Amount).AppendLine();
                foreach (var st in e.ResourceStatuses) sb.Append("  R ").Append(st.ResourceTypeId).Append(' ').Append(st.InShortage).Append(' ').Append(st.DaysBelowThreshold).AppendLine();
            }

            foreach (var r in w.TradeRoutes)
            {
                sb.Append("T ").Append(r.TradeRouteId).Append(' ').Append(r.OriginId).Append("->").Append(r.DestinationId)
                  .Append(" cap=").Append(r.CapacityPerShipment).Append(" travel=").Append(r.TravelDays).Append(" freq=").Append(r.FrequencyDays)
                  .Append(" eff=").Append(r.EfficiencyPercent).Append(' ').Append(r.Status).Append(" next=").Append(r.NextDepartureDay)
                  .Append(" n=").Append(r.ShipmentsDelivered).Append(" rep=").Append(r.LastReportedDay)
                  .Append(" last=").Append(r.LastDeliveryDay).Append(',').Append(r.LastDeliveryResource).Append(',')
                  .Append(r.LastDeliveryQuantity).Append(',').Append(r.LastDeliveryPayment);
                foreach (var d in r.Delivered.Stacks) sb.Append(' ').Append(d.ResourceTypeId).Append('=').Append(d.Quantity);
                sb.AppendLine();
            }

            foreach (var s in w.Shipments)
            {
                sb.Append("H ").Append(s.ShipmentId).Append(' ').Append(s.TradeRouteId).Append(' ').Append(s.OriginId).Append("->")
                  .Append(s.DestinationId).Append(' ').Append(s.ResourceTypeId).Append(' ').Append(s.Quantity).Append(' ')
                  .Append(s.DeliveredQuantity).Append(' ').Append(s.LostQuantity).Append(' ').Append(s.Payment).Append(' ')
                  .Append(s.DepartureDay).Append(' ').Append(s.ArrivalDay).Append(' ').Append(s.DaysWaiting).Append(' ')
                  .Append(s.LossApplied).Append(' ').Append(s.IsReturning).AppendLine();
            }

            foreach (var n in w.Npcs) sb.Append("N ").Append(n.NpcId).Append(' ').Append(n.Name).AppendLine();
            foreach (var e in w.HistoricalEvents)
            {
                sb.Append("E ").Append(e.EventId).Append(' ').Append(e.Date.ToIsoString()).Append(' ').Append(e.EventType)
                  .Append(' ').Append(e.Severity).Append(' ').Append(e.Importance).Append(' ').Append(string.Join(",", e.SettlementIds))
                  .Append(' ').Append(e.Description);
                foreach (var pair in e.Data) sb.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
