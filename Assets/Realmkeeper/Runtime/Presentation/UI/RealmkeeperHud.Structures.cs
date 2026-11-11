using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>Phase 8: the settlement inspector's structures, each marked Functional, Informational or Decorative.</summary>
    public sealed partial class RealmkeeperHud
    {
        public VisualElement SettlementStructures => Root?.Q<VisualElement>("insp-structures");

        private void RefreshSettlementStructures(SettlementSummary s)
        {
            var box = SettlementStructures;
            if (box == null || _session == null) return;
            box.Clear();
            var list = _session.GetStructures(s.SettlementId);
            if (list.Count == 0)
            {
                box.Add(Cell("No structures recorded.", "rk-trade-line"));
                return;
            }

            int functional = 0, informational = 0, decorative = 0;
            foreach (var x in list)
            {
                if (x.Role == StructureRole.Functional) functional++;
                else if (x.Role == StructureRole.Informational) informational++;
                else decorative++;
            }

            box.Add(Cell($"{list.Count} structures: {functional} functional (homes, shops, workplaces, gathering places), {informational} informational, {decorative} decorative.", "rk-npc-explain"));
            foreach (var x in list)
            {
                var row = Cell($"{x.Name} — {x.Kind} · {x.RoleText}{(x.Owner != null ? " · " + x.Owner : string.Empty)}", "rk-trade-line", "rk-structure--" + x.Role.ToString().ToLowerInvariant());
                row.name = "structure-" + x.Id;
                row.tooltip = $"{x.Description}\nTakes part in: {x.Participates}" + (x.Shop != null ? $"\nShop: {x.Shop}" : string.Empty) + $"\nCondition: {x.Condition}";
                box.Add(row);
            }
        }
    }
}
