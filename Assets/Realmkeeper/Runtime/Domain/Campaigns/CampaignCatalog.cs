using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0082): the campaigns this build knows. A definition enters the catalog only if it validates; a
    /// definition that does not is listed in <see cref="Rejected"/> with its errors, and can never be started. The
    /// catalog holds no setting canon: the one campaign here is a provisional demonstration of the engine.
    /// </summary>
    public static partial class CampaignCatalog
    {
        private static readonly Dictionary<string, CampaignDefinition> ById = new Dictionary<string, CampaignDefinition>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> RejectedById = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static bool _loaded;

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            AddWaystone();
        }

        public static IEnumerable<CampaignDefinition> All { get { Load(); return ById.Values; } }

        /// <summary>Definitions that failed validation, with their errors (never startable).</summary>
        public static IReadOnlyDictionary<string, List<string>> Rejected { get { Load(); return RejectedById; } }

        public static CampaignDefinition Find(string id)
        {
            Load();
            return id != null && ById.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>Adds a definition if it validates (tests and future content use this; the errors are kept otherwise).</summary>
        public static List<string> Register(CampaignDefinition def)
        {
            Load();
            var errors = CampaignValidator.Validate(def);
            if (def?.Id == null) return errors;
            if (errors.Count == 0)
            {
                ById[def.Id] = def;
                RejectedById.Remove(def.Id);
            }
            else RejectedById[def.Id] = errors;
            return errors;
        }

        /// <summary>Removes a definition (tests; a world holding an instance of it then keeps the instance, suspended).</summary>
        public static void Unregister(string id)
        {
            Load();
            if (id != null) ById.Remove(id);
        }
    }
}
