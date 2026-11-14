using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Schema v27 (Phase 25, ADR-0116): the creature kinds the player has met in fights. An absent list (a v26 save or
    /// older) is seeded once on load from the fights the save still holds; an empty list stays empty.
    /// </summary>
    public static partial class WorldSaveMapper
    {
        private static List<KnownCreatureDto> KnownCreaturesOut(PlayerCharacterData p)
        {
            var list = new List<KnownCreatureDto>();
            if (p?.KnownCreatures == null) return list;
            foreach (var k in p.KnownCreatures)
                if (k != null && !string.IsNullOrEmpty(k.CreatureId))
                    list.Add(new KnownCreatureDto { CreatureId = k.CreatureId, FirstMetAt = k.FirstMetAt, Met = k.Met, Defeated = k.Defeated });
            return list;
        }

        private static void KnownCreaturesIn(PlayerDto pd, PlayerCharacterData p)
        {
            if (pd?.KnownCreatures == null || p == null) return;
            foreach (var k in pd.KnownCreatures)
            {
                if (k == null || string.IsNullOrEmpty(k.CreatureId) || CreatureKnowledge.Find(p, k.CreatureId) != null) continue;
                p.KnownCreatures.Add(new KnownCreatureData { CreatureId = k.CreatureId, FirstMetAt = k.FirstMetAt, Met = System.Math.Max(0, k.Met), Defeated = System.Math.Max(0, k.Defeated) });
            }
        }
    }
}
