using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Infrastructure.Randomness;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 21: the townspeople named to keep the specialist shops, recognised by their own deterministic ID stream
    /// (<c>WorldPopulationInitializer.EnsureTradespeople</c>), so older tests can still count a town's other people exactly.
    /// </summary>
    public static class Phase21Keepers
    {
        public static int Count(WorldSession session, SettlementId settlement)
        {
            var random = new Xoshiro256RandomFactory().Create(SeedMixer.Combine(SeedMixer.FromWorldSeed(session.GetOverview().Seed), "npcs-v23-trades"));
            var ids = new HashSet<NpcId>();
            for (var i = 0; i < 128; i++) ids.Add(new NpcId(DeterministicGuid.Next(random)));
            return session.GetNpcs(new NpcFilter { Settlement = settlement }).Count(n => ids.Contains(n.NpcId));
        }
    }
}
