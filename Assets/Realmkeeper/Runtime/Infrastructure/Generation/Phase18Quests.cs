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
    /// Phase 18 (ADR-0091): the first quest of each chain, bound when its place is put into a region — real people of the
    /// place's settlement as giver and suspect, the faction that holds the watchtower against one that claims it, and (not
    /// tied to a place) a miller's dispute in one of the region's market settlements. Each is made once; a chain's second
    /// quest follows its first. People already in another quest are not taken.
    /// </summary>
    public static class Phase18Quests
    {
        public static void Bind(WorldData world, List<PlaceData> made, RegionId region, QuestReporter report)
        {
            foreach (var p in made)
            {
                switch (p.DefinitionId)
                {
                    case Phase18Places.Hollin:
                    {
                        var giver = Pick(world, p.SettlementId, p.Seed, "giver", null, NpcRole.Elder, NpcRole.Guard, NpcRole.Resident);
                        var suspect = giver == null ? null : Pick(world, p.SettlementId, p.Seed, "suspect", giver, NpcRole.Merchant, NpcRole.Elder, NpcRole.Smith);
                        if (giver != null && suspect != null)
                            Create(world, QuestCatalog.RecordHouse, report, ("giver", giver.NpcId.ToString()), ("suspect", suspect.NpcId.ToString()), ("place", p.PlaceId),
                                ("settlement", p.SettlementId.ToString()));
                        break;
                    }

                    case Phase18Places.Drift:
                    {
                        var giver = Pick(world, p.SettlementId, p.Seed, "giver", null, NpcRole.Labourer, NpcRole.Smith, NpcRole.Farmer, NpcRole.Resident);
                        if (giver != null) Create(world, QuestCatalog.DrownedDrift, report, ("giver", giver.NpcId.ToString()), ("place", p.PlaceId), ("settlement", p.SettlementId.ToString()));
                        break;
                    }

                    case Phase18Places.Cairn:
                    {
                        if (p.FactionId.IsEmpty) break;
                        // The claimant: a settlement of another faction, nearest the tower's own (then by ID).
                        SettlementData claim = null;
                        var holderSettlement = world.FindSettlement(p.SettlementId);
                        var best = int.MaxValue;
                        foreach (var s in world.Settlements)
                        {
                            if (s == null || s.FactionId.IsEmpty || s.FactionId == p.FactionId || s.RegionId != region) continue;
                            var d = holderSettlement == null ? 0 : Math.Abs(s.Position.X - holderSettlement.Position.X) + Math.Abs(s.Position.Z - holderSettlement.Position.Z);
                            if (d < best || (d == best && claim != null && s.SettlementId.CompareTo(claim.SettlementId) < 0)) { claim = s; best = d; }
                        }

                        if (claim == null) break;
                        var giver = Pick(world, claim.SettlementId, p.Seed, "claimant", null, NpcRole.Elder, NpcRole.Guard);
                        if (giver == null || giver.FactionId.IsEmpty || giver.FactionId == p.FactionId) break;
                        Create(world, QuestCatalog.CairnQuestion, report, ("giver", giver.NpcId.ToString()), ("faction", giver.FactionId.ToString()), ("holder", p.FactionId.ToString()),
                            ("place", p.PlaceId), ("settlement", claim.SettlementId.ToString()));
                        break;
                    }
                }
            }

            // The miller's dispute: the region's first market settlement (by ID) with a farmer and a miller to quarrel.
            var market = new List<SettlementData>();
            foreach (var s in world.Settlements)
                if (s != null && s.RegionId == region && (s.SettlementType == SettlementType.Village || s.SettlementType == SettlementType.Town || s.SettlementType == SettlementType.City)) market.Add(s);
            market.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            foreach (var s in market)
            {
                var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "mill:" + s.SettlementId);
                var giver = Pick(world, s.SettlementId, seed, "giver", null, NpcRole.Farmer, NpcRole.Labourer);
                var miller = giver == null ? null : Pick(world, s.SettlementId, seed, "miller", giver, NpcRole.Merchant, NpcRole.Smith, NpcRole.Innkeeper);
                if (giver == null || miller == null || giver.Role != NpcRole.Farmer && giver.Role != NpcRole.Labourer) continue;
                if (miller.Role != NpcRole.Merchant && miller.Role != NpcRole.Smith && miller.Role != NpcRole.Innkeeper) continue;
                Create(world, QuestCatalog.MillersMeasure, report, ("giver", giver.NpcId.ToString()), ("miller", miller.NpcId.ToString()), ("settlement", s.SettlementId.ToString()));
                break;
            }
        }

        private static void Create(WorldData world, string questId, QuestReporter report, params (string Role, string Value)[] roles)
        {
            var def = QuestCatalog.Find(questId);
            if (def == null) return;
            var given = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (role, value) in roles) given[role] = value;
            QuestLifecycle.Create(world, def, given, "authored", null, report, out _);
        }

        /// <summary>A living, free person of a settlement, preferring the roles in order; among equals, chosen by the seed.</summary>
        private static NpcData Pick(WorldData world, SettlementId settlement, ulong seed, string salt, NpcData not, params NpcRole[] preferred)
        {
            var busy = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in world.Quests) foreach (var v in q.Bindings.Values) busy.Add(v);
            var candidates = new List<NpcData>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId != settlement || n == not || busy.Contains(n.NpcId.ToString())) continue;
                if (world.FindEscort(n.NpcId) != null || Array.IndexOf(preferred, n.Role) < 0) continue;
                candidates.Add(n);
            }

            if (candidates.Count == 0) return null;
            int Rank(NpcData x) => Array.IndexOf(preferred, x.Role);
            candidates.Sort((a, b) => Rank(a) != Rank(b) ? Rank(a).CompareTo(Rank(b)) : a.NpcId.CompareTo(b.NpcId));
            var best = candidates.FindAll(x => Rank(x) == Rank(candidates[0]));
            return best[(int)(SeedMixer.Mix(SeedMixer.Combine(seed, salt)) % (ulong)best.Count)];
        }
    }
}
