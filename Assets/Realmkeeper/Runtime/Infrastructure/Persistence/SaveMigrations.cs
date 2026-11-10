using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// v4 → v5 (Phase 3: factions and politics). Structural only: adds empty <c>relations</c> and
    /// <c>claims</c> arrays and enables the political system for worlds that run the standard
    /// Phase 2 systems. Faction types, presence, leaders, relationships and goals cannot be
    /// derived from JSON alone, so after mapping the serializer runs
    /// <see cref="Generation.PoliticalInitializer"/>: the two existing factions keep their IDs
    /// and names, and the new ones get the IDs a fresh world of that seed gets. No settlement,
    /// resource, route or event is changed.
    /// </summary>
    public sealed class MigrationV4ToV5 : ISaveMigration
    {
        public int SourceVersion => 4;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["relations"] == null) world["relations"] = new JArray();
            if (world["claims"] == null) world["claims"] = new JArray();

            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken chronicle = null;
            var hasPolitics = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == ChronicleSystem.Id) chronicle = id;
                if ((string)id == PoliticalSystem.Id) hasPolitics = true;
            }

            // Listed where a fresh world lists it (the scheduler orders systems itself; this only keeps saves alike).
            if (chronicle != null && !hasPolitics) chronicle.AddBeforeSelf(PoliticalSystem.Id);
        }
    }

    /// <summary>
    /// v3 → v4 (Phase 2 completion): trade routes gain "isSuspended". Every route in an older
    /// save was trading, so the documented default is false (not suspended). Nothing else
    /// changes; resources, routes, shipments and history are kept exactly as saved.
    /// </summary>
    public sealed class MigrationV3ToV4 : ISaveMigration
    {
        public int SourceVersion => 3;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world) || !(world["tradeRoutes"] is JArray routes)) return;
            foreach (var token in routes)
            {
                if (token is JObject route && route["isSuspended"] == null) route["isSuspended"] = false;
            }
        }
    }

    /// <summary>
    /// v2 → v3 (ADR-0009 prerequisites). The JSON layout gains regions, settlement.regionId, the
    /// player and resident fields, but none of them can be derived from JSON alone: they come from
    /// seeded streams. So this step is structural only. After mapping, the serializer runs
    /// <see cref="Generation.WorldPopulationInitializer"/>, which adds the region (assigning each
    /// settlement to the region containing it), the player character on the world map, and named
    /// residents if there are none, with the same IDs a fresh world of that seed gets. Existing
    /// IDs and data are never changed or regenerated.
    /// </summary>
    public sealed class MigrationV2ToV3 : ISaveMigration
    {
        public int SourceVersion => 2;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["regions"] == null) world["regions"] = new JArray();
        }
    }

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
