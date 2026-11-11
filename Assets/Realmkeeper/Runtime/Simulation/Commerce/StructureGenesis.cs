using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Simulation.Commerce
{
    /// <summary>
    /// Creates each settlement's structures (Phase 8): one functional structure per existing location (linked to it and,
    /// where there is one, to its shop: never a duplicate), plus the informational and decorative structures its size
    /// calls for. Deterministic (IDs hashed from the world seed, settlement and source) and once per settlement.
    /// </summary>
    public static class StructureGenesis
    {
        public static int Ensure(WorldData world)
        {
            if (world.Locations.Count == 0) return 0;
            var added = 0;
            foreach (var s in world.Settlements)
            {
                var has = false;
                foreach (var st in world.Structures) if (st.SettlementId == s.SettlementId) { has = true; break; }
                if (has) continue;

                foreach (var l in world.Locations)
                {
                    if (l.SettlementId != s.SettlementId) continue;
                    var def = StructureCatalog.Find(StructureCatalog.ForLocation(l));
                    ShopData shop = null;
                    foreach (var x in world.Shops) if (x.LocationId == l.LocationId) shop = x;
                    var owner = shop != null ? shop.ShopkeeperNpcId : OwnerOf(world, l, def);
                    world.Structures.Add(new StructureData
                    {
                        StructureId = new StructureId(NpcHash.Guid(NpcHash.Of(world.Seed, "structure", s.SettlementId + ":" + l.LocationId))),
                        DefinitionId = def.Id,
                        Name = shop != null ? shop.Name : l.Name,
                        SettlementId = s.SettlementId,
                        LocationId = l.LocationId,
                        ShopId = shop?.ShopId ?? ShopId.Empty,
                        OwnerNpcId = owner,
                        FactionId = def.Category == StructureCategory.Civic || def.Category == StructureCategory.Military ? s.FactionId : FactionId.Empty,
                        LayoutKey = l.LayoutKey ?? string.Empty,
                        EstablishedOn = world.CurrentSimulationDate,
                    });
                    added++;
                }

                foreach (var def in StructureCatalog.All)
                {
                    if (!StructureCatalog.Suits(def, s.SettlementType)) continue;
                    world.Structures.Add(new StructureData
                    {
                        StructureId = new StructureId(NpcHash.Guid(NpcHash.Of(world.Seed, "structure", s.SettlementId + ":" + def.Id))),
                        DefinitionId = def.Id,
                        Name = def.Name,
                        SettlementId = s.SettlementId,
                        EstablishedOn = world.CurrentSimulationDate,
                    });
                    added++;
                }
            }

            return added;
        }

        /// <summary>The settlement's structures in a stable order: functional first (by name), then the rest.</summary>
        public static List<StructureData> Of(WorldData world, SettlementId settlement)
        {
            var list = new List<StructureData>();
            foreach (var s in world.Structures) if (s.SettlementId == settlement) list.Add(s);
            list.Sort((a, b) =>
            {
                var da = StructureCatalog.Find(a.DefinitionId);
                var db = StructureCatalog.Find(b.DefinitionId);
                var c = (da?.Role ?? StructureRole.Decorative).CompareTo(db?.Role ?? StructureRole.Decorative);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
            return list;
        }

        // A home belongs to its first living resident (by ID); a workplace to the first living person working there.
        private static NpcId OwnerOf(WorldData world, LocationData l, StructureDefinition def)
        {
            if (def.Category == StructureCategory.Civic && l.Kind != LocationKind.Administration) return NpcId.Empty;
            NpcData best = null;
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased) continue;
                var here = l.Kind == LocationKind.Residence ? n.ResidenceId == l.LocationId : n.WorkplaceId == l.LocationId;
                if (here && (best == null || n.NpcId.CompareTo(best.NpcId) < 0)) best = n;
            }

            return best?.NpcId ?? NpcId.Empty;
        }
    }
}
