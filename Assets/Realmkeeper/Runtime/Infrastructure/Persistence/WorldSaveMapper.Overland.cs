using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Schema v26 (Phase 24, ADR-0114): what changed in the country. Landmarks themselves are regenerated from the seed; only
    /// their states are saved (found, searched, rested on). Overland encounters keep their ids (their deterministic buckets),
    /// so a load never rolls one again; the encounter clock keeps the bucket last checked and the last encounter's time.
    /// </summary>
    public static partial class WorldSaveMapper
    {
        private static void WriteOverland(WorldData w, WorldDto dto)
        {
            var o = w.Overland ?? new OverlandStateData();
            var d = new OverlandDto { LastCheckBucket = o.LastCheckBucket, LastEncounterAt = o.LastEncounterAt, Landmarks = new List<LandmarkStateDto>(), Encounters = new List<OverlandEncounterDto>() };
            foreach (var l in o.Landmarks)
                d.Landmarks.Add(new LandmarkStateDto { LandmarkId = l.LandmarkId, Found = l.Found, FoundAt = l.FoundAt, Searched = l.Searched, RestedDay = l.RestedDay });
            foreach (var e in o.Encounters)
                d.Encounters.Add(new OverlandEncounterDto
                {
                    Id = e.Id, Kind = e.Kind.ToString(), State = e.State.ToString(), RegionId = e.RegionId.ToString(), X = e.X, Z = e.Z, At = e.At, PopulationId = e.PopulationId,
                    GroupId = e.GroupId, Text = e.Text, Outcome = e.Outcome, EncounterId = e.EncounterId,
                });
            dto.Overland = d;
        }

        private static void ReadOverland(WorldDto dto, WorldData world)
        {
            var o = new OverlandStateData();
            var d = dto.Overland;
            if (d != null)
            {
                o.LastCheckBucket = d.LastCheckBucket;
                o.LastEncounterAt = d.LastEncounterAt;
                foreach (var l in d.Landmarks ?? new List<LandmarkStateDto>())
                {
                    if (l == null || string.IsNullOrEmpty(l.LandmarkId)) throw new SaveDataException("A landmark state has no id.");
                    o.Landmarks.Add(new LandmarkStateData { LandmarkId = l.LandmarkId, Found = l.Found, FoundAt = l.FoundAt, Searched = l.Searched, RestedDay = l.RestedDay });
                }

                foreach (var e in d.Encounters ?? new List<OverlandEncounterDto>())
                {
                    if (e == null || string.IsNullOrEmpty(e.Id)) throw new SaveDataException("An overland encounter has no id.");
                    o.Encounters.Add(new OverlandEncounterData
                    {
                        Id = e.Id, Kind = ParseEnum<OverlandEncounterKind>(e.Kind, "overland encounter kind"), State = ParseEnum<OverlandEncounterState>(e.State, "overland encounter state"),
                        RegionId = string.IsNullOrEmpty(e.RegionId) ? default : new RegionId(RequiredGuid(e.RegionId, "overland encounter regionId")), X = e.X, Z = e.Z, At = e.At, PopulationId = e.PopulationId, GroupId = e.GroupId,
                        Text = e.Text, Outcome = e.Outcome, EncounterId = e.EncounterId,
                    });
                }
            }

            world.Overland = o;
        }
    }

    public sealed class OverlandDto
    {
        public long LastCheckBucket { get; set; } = -1;
        public long LastEncounterAt { get; set; } = -1;
        public List<LandmarkStateDto> Landmarks { get; set; }
        public List<OverlandEncounterDto> Encounters { get; set; }
    }

    public sealed class LandmarkStateDto
    {
        public string LandmarkId { get; set; }
        public bool Found { get; set; }
        public long FoundAt { get; set; }
        public bool Searched { get; set; }
        public long RestedDay { get; set; } = -1;
    }

    public sealed class OverlandEncounterDto
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string State { get; set; }
        public string RegionId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public long At { get; set; }
        public string PopulationId { get; set; }
        public string GroupId { get; set; }
        public string Text { get; set; }
        public string Outcome { get; set; }
        public string EncounterId { get; set; }
    }
}
