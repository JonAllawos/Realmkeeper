using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v19 (Phase 17, ADR-0082): a campaign in this world. Its definition is not saved, only its version.</summary>
    public sealed class CampaignInstanceDto
    {
        public string InstanceId { get; set; }
        public string CampaignId { get; set; }
        public int Version { get; set; }
        public string WorldId { get; set; }
        public string State { get; set; }
        public Dictionary<string, string> Bindings { get; set; }
        public long StartedAt { get; set; }
        public long ClosedAt { get; set; } = -1;
        public long SuspendedAt { get; set; } = -1;
        public string Ending { get; set; }
        public List<ChapterProgressDto> Chapters { get; set; }
        public List<ArcProgressDto> Arcs { get; set; }
        public List<DecisionRecordDto> Decisions { get; set; }
        public List<PendingConsequenceDto> Pending { get; set; }
        public List<string> Applied { get; set; }
        public Dictionary<string, string> Flags { get; set; }
        public Dictionary<string, string> Quests { get; set; }
        public List<CampaignHistoryDto> History { get; set; }
    }

    public sealed class ChapterProgressDto
    {
        public string Id { get; set; }
        public string State { get; set; }
        public long StartedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;
    }

    public sealed class ArcProgressDto
    {
        public string Id { get; set; }
        public string State { get; set; }
        public long StartedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public List<ArcObjectiveProgressDto> Objectives { get; set; }
    }

    public sealed class ArcObjectiveProgressDto
    {
        public string Id { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public bool Revealed { get; set; }
        public long DoneAt { get; set; } = -1;
    }

    public sealed class DecisionRecordDto
    {
        public string DecisionId { get; set; }
        public string ChoiceId { get; set; }
        public long At { get; set; }
        public string Source { get; set; }
    }

    public sealed class PendingConsequenceDto
    {
        public string Key { get; set; }
        public string DecisionId { get; set; }
        public string ChoiceId { get; set; }
        public string DelayedId { get; set; }
        public long DueAt { get; set; }
        public bool Fired { get; set; }
        public long FiredAt { get; set; } = -1;
        public bool Lapsed { get; set; }
    }

    public sealed class CampaignHistoryDto
    {
        public long At { get; set; }
        public string Text { get; set; }
    }

    /// <summary>Schema v19 (Phase 17, ADR-0087): something a companion will say when asked.</summary>
    public sealed class CompanionRemarkDto
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public bool Heard { get; set; }
    }

    public static partial class WorldSaveMapper
    {
        private static void WriteCampaigns(WorldData w, WorldDto dto)
        {
            dto.Campaigns = new List<CampaignInstanceDto>();
            foreach (var c in w.Campaigns)
            {
                var d = new CampaignInstanceDto
                {
                    InstanceId = c.InstanceId, CampaignId = c.CampaignId, Version = c.Version, WorldId = c.WorldId, State = c.State.ToString(),
                    Bindings = new Dictionary<string, string>(c.Bindings, StringComparer.Ordinal), StartedAt = c.StartedAt, ClosedAt = c.ClosedAt, SuspendedAt = c.SuspendedAt, Ending = c.Ending,
                    Chapters = new List<ChapterProgressDto>(), Arcs = new List<ArcProgressDto>(), Decisions = new List<DecisionRecordDto>(), Pending = new List<PendingConsequenceDto>(),
                    Applied = new List<string>(c.Applied), Flags = new Dictionary<string, string>(c.Flags, StringComparer.Ordinal), Quests = new Dictionary<string, string>(c.Quests, StringComparer.Ordinal),
                    History = new List<CampaignHistoryDto>(),
                };
                foreach (var ch in c.Chapters) d.Chapters.Add(new ChapterProgressDto { Id = ch.Id, State = ch.State.ToString(), StartedAt = ch.StartedAt, ClosedAt = ch.ClosedAt });
                foreach (var a in c.Arcs)
                {
                    var ad = new ArcProgressDto { Id = a.Id, State = a.State.ToString(), StartedAt = a.StartedAt, ClosedAt = a.ClosedAt, Outcome = a.Outcome, Reason = a.Reason, Objectives = new List<ArcObjectiveProgressDto>() };
                    foreach (var o in a.Objectives) ad.Objectives.Add(new ArcObjectiveProgressDto { Id = o.Id, Done = o.Done, Failed = o.Failed, Revealed = o.Revealed, DoneAt = o.DoneAt });
                    d.Arcs.Add(ad);
                }

                foreach (var x in c.Decisions) d.Decisions.Add(new DecisionRecordDto { DecisionId = x.DecisionId, ChoiceId = x.ChoiceId, At = x.At, Source = x.Source });
                foreach (var p in c.Pending)
                    d.Pending.Add(new PendingConsequenceDto { Key = p.Key, DecisionId = p.DecisionId, ChoiceId = p.ChoiceId, DelayedId = p.DelayedId, DueAt = p.DueAt, Fired = p.Fired, FiredAt = p.FiredAt, Lapsed = p.Lapsed });
                foreach (var h in c.History) d.History.Add(new CampaignHistoryDto { At = h.At, Text = h.Text });
                dto.Campaigns.Add(d);
            }
        }

        private static void ReadCampaigns(WorldDto dto, WorldData world)
        {
            world.Campaigns = new List<CampaignInstanceData>();
            foreach (var d in dto.Campaigns ?? new List<CampaignInstanceDto>())
            {
                if (d == null) throw new SaveDataException("A campaign record is empty.");
                if (string.IsNullOrWhiteSpace(d.InstanceId) || string.IsNullOrWhiteSpace(d.CampaignId)) throw new SaveDataException("A campaign record has no ID.");
                var c = new CampaignInstanceData
                {
                    InstanceId = d.InstanceId, CampaignId = d.CampaignId, Version = d.Version, WorldId = d.WorldId, State = ParseEnum<CampaignState>(d.State, "campaign state"),
                    StartedAt = d.StartedAt, ClosedAt = d.ClosedAt, SuspendedAt = d.SuspendedAt, Ending = d.Ending,
                };
                foreach (var pair in d.Bindings ?? new Dictionary<string, string>()) c.Bindings[pair.Key] = pair.Value;
                foreach (var ch in d.Chapters ?? new List<ChapterProgressDto>())
                    if (ch != null) c.Chapters.Add(new ChapterProgressData { Id = ch.Id, State = ParseEnum<ChapterState>(ch.State, "chapter state"), StartedAt = ch.StartedAt, ClosedAt = ch.ClosedAt });
                foreach (var a in d.Arcs ?? new List<ArcProgressDto>())
                {
                    if (a == null) continue;
                    var ap = new ArcProgressData { Id = a.Id, State = ParseEnum<ArcState>(a.State, "arc state"), StartedAt = a.StartedAt, ClosedAt = a.ClosedAt, Outcome = a.Outcome, Reason = a.Reason };
                    foreach (var o in a.Objectives ?? new List<ArcObjectiveProgressDto>())
                        if (o != null) ap.Objectives.Add(new ArcObjectiveProgressData { Id = o.Id, Done = o.Done, Failed = o.Failed, Revealed = o.Revealed, DoneAt = o.DoneAt });
                    c.Arcs.Add(ap);
                }

                foreach (var x in d.Decisions ?? new List<DecisionRecordDto>())
                    if (x != null) c.Decisions.Add(new DecisionRecordData { DecisionId = x.DecisionId, ChoiceId = x.ChoiceId, At = x.At, Source = x.Source });
                foreach (var p in d.Pending ?? new List<PendingConsequenceDto>())
                    if (p != null) c.Pending.Add(new PendingConsequenceData { Key = p.Key, DecisionId = p.DecisionId, ChoiceId = p.ChoiceId, DelayedId = p.DelayedId, DueAt = p.DueAt, Fired = p.Fired, FiredAt = p.FiredAt, Lapsed = p.Lapsed });
                c.Applied.AddRange(d.Applied ?? new List<string>());
                foreach (var pair in d.Flags ?? new Dictionary<string, string>()) c.Flags[pair.Key] = pair.Value;
                foreach (var pair in d.Quests ?? new Dictionary<string, string>()) c.Quests[pair.Key] = pair.Value ?? string.Empty;
                foreach (var h in d.History ?? new List<CampaignHistoryDto>()) if (h != null) c.History.Add(new CampaignHistoryEntry { At = h.At, Text = h.Text });
                world.Campaigns.Add(c);
            }
        }
    }
}
