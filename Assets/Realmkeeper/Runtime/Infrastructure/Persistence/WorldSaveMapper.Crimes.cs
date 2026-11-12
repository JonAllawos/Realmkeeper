using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v16 (Phase 14, ADR-0069): a crime record.</summary>
    public sealed class CrimeDto
    {
        public string CrimeId { get; set; }
        public string Kind { get; set; }
        public string Status { get; set; }
        public long At { get; set; }
        public string SettlementId { get; set; }
        public string PlaceId { get; set; }
        public string Room { get; set; }
        public string VictimNpcId { get; set; }
        public string VictimFactionId { get; set; }
        public string Detail { get; set; }
        public Dictionary<string, int> Taken { get; set; }
        public int Coin { get; set; }
        public int Bounty { get; set; }
        public List<CrimeWitnessDto> Witnesses { get; set; }
        public long DiscoveredAt { get; set; } = -1;
    }

    public sealed class CrimeWitnessDto
    {
        public string NpcId { get; set; }
        public bool Recognized { get; set; }
        public string Description { get; set; }
        public string How { get; set; }
    }

    public static partial class WorldSaveMapper
    {
        private static void WriteCrimes(WorldData w, WorldDto dto)
        {
            dto.Crimes = new List<CrimeDto>();
            dto.NextCrimeNumber = w.NextCrimeNumber;
            foreach (var c in w.Crimes)
            {
                var d = new CrimeDto
                {
                    CrimeId = c.CrimeId, Kind = c.Kind.ToString(), Status = c.Status.ToString(), At = c.At, SettlementId = c.SettlementId.IsEmpty ? null : c.SettlementId.ToString(),
                    PlaceId = c.PlaceId, Room = c.Room, VictimNpcId = c.VictimNpcId.IsEmpty ? null : c.VictimNpcId.ToString(),
                    VictimFactionId = c.VictimFactionId.IsEmpty ? null : c.VictimFactionId.ToString(), Detail = c.Detail, Taken = new Dictionary<string, int>(c.Taken), Coin = c.Coin,
                    Bounty = c.Bounty, DiscoveredAt = c.DiscoveredAt, Witnesses = new List<CrimeWitnessDto>(),
                };
                foreach (var x in c.Witnesses) d.Witnesses.Add(new CrimeWitnessDto { NpcId = x.NpcId.ToString(), Recognized = x.Recognized, Description = x.Description, How = x.How });
                dto.Crimes.Add(d);
            }
        }

        private static void ReadCrimes(WorldDto dto, WorldData world)
        {
            world.NextCrimeNumber = Math.Max(1, dto.NextCrimeNumber);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in dto.Crimes ?? new List<CrimeDto>())
            {
                if (d == null) throw new SaveDataException("Null crime entry.");
                if (string.IsNullOrEmpty(d.CrimeId) || !seen.Add(d.CrimeId)) throw new SaveDataException($"A crime has no id, or a repeated one ('{d.CrimeId}').");
                var c = new CrimeRecordData
                {
                    CrimeId = d.CrimeId, Kind = ParseEnum<CrimeKind>(d.Kind, "crime kind"), Status = ParseEnum<CrimeStatus>(d.Status, "crime status"), At = d.At,
                    SettlementId = new SettlementId(OptionalGuid(d.SettlementId, "crime settlementId")), PlaceId = d.PlaceId, Room = d.Room,
                    VictimNpcId = new NpcId(OptionalGuid(d.VictimNpcId, "crime victim")), VictimFactionId = new FactionId(OptionalGuid(d.VictimFactionId, "crime faction")),
                    Detail = d.Detail ?? string.Empty, Taken = new Dictionary<string, int>(d.Taken ?? new Dictionary<string, int>(), StringComparer.Ordinal), Coin = d.Coin,
                    Bounty = d.Bounty, DiscoveredAt = d.DiscoveredAt,
                };
                foreach (var x in d.Witnesses ?? new List<CrimeWitnessDto>())
                {
                    if (x == null) throw new SaveDataException("Null crime witness.");
                    c.Witnesses.Add(new CrimeWitnessData { NpcId = new NpcId(RequiredGuid(x.NpcId, "crime witness")), Recognized = x.Recognized, Description = x.Description ?? string.Empty, How = x.How ?? string.Empty });
                }

                world.Crimes.Add(c);
            }
        }
    }
}
