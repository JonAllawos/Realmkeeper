using System.Text;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Tests.EditMode
{
    /// <summary>Canonical text dump of a world, used to assert exact state equality.</summary>
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
                  .Append(" st=").Append(s.Stability).Append(" fac=").Append(s.FactionId).Append(" prod=").Append(s.FoodProductionPerDay)
                  .Append(" short=").Append(s.IsInFoodShortage);
                foreach (var r in s.ResourceInventory.Stacks) sb.Append(' ').Append(r.ResourceTypeId).Append('=').Append(r.Quantity);
                sb.AppendLine();
            }

            foreach (var n in w.Npcs) sb.Append("N ").Append(n.NpcId).Append(' ').Append(n.Name).AppendLine();
            foreach (var e in w.HistoricalEvents)
            {
                sb.Append("E ").Append(e.EventId).Append(' ').Append(e.Date.ToIsoString()).Append(' ').Append(e.EventType)
                  .Append(' ').Append(e.Severity).Append(' ').Append(e.SettlementId).Append(' ').Append(e.Description).AppendLine();
            }

            return sb.ToString();
        }
    }
}
