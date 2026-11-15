using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 27: the authoritative figures a settlement's street life is drawn from (ADR-0119): its population, kind,
    /// prosperity, the workers of each trade, and its owner's place in the world's faction list (the same index the
    /// strategic map colours factions by, so banners match the map). Read-only.
    /// </summary>
    public sealed class StreetLifeView
    {
        public SettlementId SettlementId { get; internal set; }
        public int Population { get; internal set; }
        public SettlementType Type { get; internal set; }
        public int Prosperity { get; internal set; }
        public List<(ProductionSourceType source, int workers)> Labour { get; } = new List<(ProductionSourceType, int)>();

        /// <summary>The owning faction's index in the world's faction list, or −1 when independent or unknown.</summary>
        public int OwnerIndex { get; internal set; } = -1;

        public string OwnerName { get; internal set; }
    }

    public sealed partial class WorldSession
    {
        public StreetLifeView GetStreetLife(SettlementId id)
        {
            var s = _world?.FindSettlement(id);
            if (s == null) return null;
            var v = new StreetLifeView { SettlementId = id, Population = s.Population, Type = s.SettlementType, Prosperity = s.Prosperity };
            foreach (var p in s.Economy?.Production ?? new List<ProductionAssignment>()) v.Labour.Add((p.SourceType, p.Workers));
            var owner = s.FactionId;
            for (var i = 0; i < _world.Factions.Count; i++)
                if (_world.Factions[i].FactionId == owner && !owner.IsEmpty)
                {
                    v.OwnerIndex = i;
                    v.OwnerName = _world.Factions[i].Name;
                }

            return v;
        }
    }
}
