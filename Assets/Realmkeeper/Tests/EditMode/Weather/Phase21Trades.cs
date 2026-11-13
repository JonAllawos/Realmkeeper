using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>
    /// Phase 21: the townspeople named to keep the specialist shops, recognised by their own deterministic ID stream
    /// (<c>WorldPopulationInitializer.EnsureTradespeople</c>), so older tests can still count everyone else exactly.
    /// </summary>
    public static class Phase21Trades
    {
        public static HashSet<NpcId> Ids(WorldData world, int max = 128)
        {
            var random = new Xoshiro256RandomFactory().Create(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "npcs-v23-trades"));
            var ids = new HashSet<NpcId>();
            for (var i = 0; i < max; i++) ids.Add(new NpcId(DeterministicGuid.Next(random)));
            return ids;
        }

        /// <summary>How many of the world's people were named in Phase 21 to keep a specialist shop.</summary>
        public static int Count(WorldData world)
        {
            var ids = Ids(world);
            return world.Npcs.Count(n => ids.Contains(n.NpcId));
        }

        /// <summary>How many of a settlement's people were.</summary>
        public static int Count(WorldData world, SettlementId settlement)
        {
            var ids = Ids(world);
            return world.Npcs.Count(n => n.HomeSettlementId == settlement && ids.Contains(n.NpcId));
        }
    }
}
