using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;

namespace Realmkeeper.Application
{
    /// <summary>One settlement structure as the inspectors show it (Phase 8): what it is, who owns it, and what it does in the simulation.</summary>
    public sealed class StructureSummary
    {
        public StructureId Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Description { get; set; }
        public StructureCategory Category { get; set; }
        public StructureRole Role { get; set; }

        /// <summary>What it takes part in, honestly ("nothing yet: informational …").</summary>
        public string Participates { get; set; }

        public string Owner { get; set; }
        public string Shop { get; set; }
        public string LayoutKey { get; set; }
        public string Sign { get; set; }
        public StructureCondition Condition { get; set; }

        public string RoleText => Role == StructureRole.Functional ? "Functional" : Role == StructureRole.Informational ? "Informational" : "Decorative";
    }

    public sealed partial class WorldSession
    {
        /// <summary>A settlement's structures, functional first; empty when there are none.</summary>
        public IReadOnlyList<StructureSummary> GetStructures(SettlementId settlement)
        {
            var list = new List<StructureSummary>();
            if (_world == null) return list;
            foreach (var s in StructureGenesis.Of(_world, settlement))
            {
                var def = StructureCatalog.Find(s.DefinitionId);
                if (def == null) continue;
                var owner = s.OwnerNpcId.IsEmpty ? null : _world.FindNpc(s.OwnerNpcId);
                var shop = s.ShopId.IsEmpty ? null : _world.FindShop(s.ShopId);
                list.Add(new StructureSummary
                {
                    Id = s.StructureId, Name = s.Name, Kind = def.Name, Description = def.Description, Category = def.Category, Role = def.Role,
                    Participates = def.Participates, Owner = owner == null ? null : owner.Name + (owner.IsDeceased ? " (died)" : string.Empty),
                    Shop = shop?.Name, LayoutKey = s.LayoutKey, Sign = def.Sign, Condition = s.Condition,
                });
            }

            return list;
        }
    }
}
