using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Phase 14 (ADR-0071): binds the new places' quests when the places are put into the world — the givers are real
    /// residents of the place's settlement who play no part in another quest, chosen by role and then by the place's seed.
    /// The hamlet has no quest: its story is found, not asked for.
    /// </summary>
    public static class Phase14Quests
    {
        public static void Bind(WorldData world, List<PlaceData> made, QuestReporter report)
        {
            foreach (var p in made)
            {
                switch (p.DefinitionId)
                {
                    case Phase14Places.Catacomb: Give(world, p, QuestCatalog.FoundersRegister, report, null, NpcRole.Elder, NpcRole.Resident); break;
                    case Phase14Places.Temple:
                    {
                        var faction = p.Variables.TryGetValue("built_by", out var f) && Guid.TryParse(f, out var g) ? new FactionId(g) : default;
                        Give(world, p, QuestCatalog.FaneLantern, report, faction, NpcRole.Elder, NpcRole.Guard, NpcRole.Resident);
                        break;
                    }

                    case Phase14Places.Hideout: Give(world, p, QuestCatalog.RoadTolls, report, null, NpcRole.Merchant, NpcRole.Innkeeper, NpcRole.Elder); break;
                    case Phase14Places.Works: Give(world, p, QuestCatalog.Culverts, report, null, NpcRole.Elder, NpcRole.Guard, NpcRole.Merchant); break;
                }
            }
        }

        private static void Give(WorldData world, PlaceData place, string questId, QuestReporter report, FactionId? faction, params NpcRole[] preferred)
        {
            var def = QuestCatalog.Find(questId);
            if (def == null) return;
            var busy = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in world.Quests) foreach (var v in q.Bindings.Values) busy.Add(v);
            var candidates = new List<NpcData>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId != place.SettlementId || busy.Contains(n.NpcId.ToString())) continue;
                if (faction.HasValue && !faction.Value.IsEmpty && n.FactionId != faction.Value) continue;
                if (world.FindEscort(n.NpcId) != null) continue;
                candidates.Add(n);
            }

            if (candidates.Count == 0) return;
            candidates.Sort((a, b) =>
            {
                int Rank(NpcData x) { var i = Array.IndexOf(preferred, x.Role); return i < 0 ? preferred.Length : i; }
                var r = Rank(a).CompareTo(Rank(b));
                return r != 0 ? r : a.NpcId.CompareTo(b.NpcId);
            });
            var best = candidates.FindAll(x => Array.IndexOf(preferred, x.Role) == Array.IndexOf(preferred, candidates[0].Role));
            var giver = best[(int)(SeedMixer.Mix(SeedMixer.Combine(place.Seed, "giver")) % (ulong)best.Count)];
            var given = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["giver"] = giver.NpcId.ToString(), ["place"] = place.PlaceId, ["settlement"] = place.SettlementId.ToString(),
            };
            if (questId == QuestCatalog.FaneLantern && !giver.FactionId.IsEmpty) given["faction"] = giver.FactionId.ToString();
            QuestLifecycle.Create(world, def, given, "authored", null, report, out _);
        }
    }
}
