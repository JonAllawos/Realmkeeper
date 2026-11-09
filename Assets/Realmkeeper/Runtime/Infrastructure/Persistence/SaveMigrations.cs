using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Phase 1 (v1) → Phase 2 (v2):
    /// - the "ore" resource becomes "iron" (quantities merged if both exist);
    /// - settlement.foodProductionPerDay is dropped (food now comes from farms, fishing, hunting);
    /// - the Phase 1 system list (food, wellbeing) becomes the Phase 2 list;
    /// - economy, trade routes and shipments are left absent: on load the session derives them
    ///   from the regenerated terrain (see IWorldGenerator.InitializeEconomy).
    /// Events keep their text descriptions and gain no structured data.
    /// </summary>
    public sealed class MigrationV1ToV2 : ISaveMigration
    {
        public int SourceVersion => 1;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;

            if (world["settlements"] is JArray settlements)
            {
                foreach (var token in settlements)
                {
                    if (!(token is JObject settlement)) continue;
                    settlement.Remove("foodProductionPerDay");
                    if (settlement["resources"] is JArray resources) RenameOre(resources);
                }
            }

            if (world["settings"] is JObject settings && settings["enabledSimulationSystems"] is JArray systems)
            {
                var legacy = new HashSet<string>(StandardSimulationSystems.LegacyIds);
                var usesLegacy = false;
                foreach (var id in systems)
                {
                    if (id.Type == JTokenType.String && legacy.Contains((string)id)) usesLegacy = true;
                }

                if (usesLegacy) settings["enabledSimulationSystems"] = new JArray(StandardSimulationSystems.AllIds);
            }
        }

        private static void RenameOre(JArray resources)
        {
            JObject ore = null;
            JObject iron = null;
            foreach (var token in resources)
            {
                if (!(token is JObject stack)) continue;
                var key = (string)stack["resourceTypeId"];
                if (key == "ore") ore = stack;
                else if (key == "iron") iron = stack;
            }

            if (ore == null) return;
            if (iron == null)
            {
                ore["resourceTypeId"] = "iron";
                return;
            }

            iron["quantity"] = (int)iron["quantity"] + (int)ore["quantity"];
            ore.Remove();
        }
    }
}
