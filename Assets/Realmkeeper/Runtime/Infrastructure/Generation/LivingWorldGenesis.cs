using System;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Phase 10's additions to a world, in dependency order (ADR-0048): the people a settlement's places call for;
    /// every named person's archetype, gear and sheet; each settlement's identity and persisted layout; regional
    /// wildlife and settlement livestock. Runs when a world is generated and when an older save is loaded (the region
    /// map is regenerated first). Every step only fills what is missing and is deterministic, so an upgraded save and a
    /// fresh world of the same seed get the same additions, and nothing established is ever regenerated.
    /// </summary>
    public static class LivingWorldGenesis
    {
        /// <summary>Fills in whatever Phase 10 data the world lacks. True when anything was added.</summary>
        public static bool Ensure(WorldData world, RegionMap map, IRandomSourceFactory random)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (map == null) throw new ArgumentNullException(nameof(map));
            var changed = false;

            var added = WorldPopulationInitializer.EnsurePhase10People(world, random);
            if (added.Count > 0)
            {
                Simulation.Npcs.NpcGenesis.EnsurePeople(world);
                WorldPopulationInitializer.SettlePhase10People(world, added);
                changed = true;
            }

            if (NpcCharacterGenesis.Ensure(world) > 0) changed = true;
            if (Simulation.Fauna.FaunaGenesis.Ensure(world, map)) changed = true; // wildlife, road bands, vermin, herds
            if (EnsureLayouts(world, map)) changed = true;
            if (Simulation.Items.ItemGenesis.Ensure(world)) changed = true; // Phase 11: chests, caches, gathering sites
            if (Simulation.Quests.QuestGenesis.Ensure(world)) changed = true; // Phase 12: the authored quests and today's emergent ones
            if (PlaceGenesis.Ensure(world, map, random)) changed = true; // Phase 13: wilderness places, their contents, captive and garrison
            return changed;
        }

        /// <summary>
        /// Each settlement's identity and plan, made once (ADR-0047) and kept; then each herd is given its pasture (a
        /// pasture field for grazing beasts, a yard for poultry) when it has none.
        /// </summary>
        public static bool EnsureLayouts(WorldData world, RegionMap map)
        {
            var changed = false;
            foreach (var s in world.Settlements)
            {
                if (s == null || world.FindLayout(s.SettlementId) != null) continue;
                world.SettlementLayouts.Add(Domain.Settlements.SettlementLayoutGenerator.Generate(world, map, s));
                changed = true;
            }

            foreach (var herd in world.Livestock)
            {
                if (herd == null || !string.IsNullOrEmpty(herd.PastureKey)) continue;
                var plan = world.FindLayout(herd.SettlementId);
                if (plan == null) continue;
                var poultry = herd.CreatureId == "chicken";
                Domain.Settlements.FieldData best = null;
                foreach (var f in plan.Fields)
                {
                    var fits = poultry ? f.Kind == Domain.Settlements.FieldKind.Yard : f.Kind == Domain.Settlements.FieldKind.Pasture;
                    if (!fits) continue;
                    if (best == null || (f.Crop == herd.CreatureId && best.Crop != herd.CreatureId)) best = f;
                }

                if (best == null && !poultry) foreach (var f in plan.Fields) if (f.Kind == Domain.Settlements.FieldKind.Yard) { best = f; break; }
                if (best == null) continue;
                herd.PastureKey = best.Key;
                changed = true;
            }

            return changed;
        }
    }
}
