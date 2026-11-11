using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v14 (Phase 12): quests, the conversation, the player's social state, content settings.</summary>
    public static partial class WorldSaveMapper
    {
        private static void WriteQuestWorld(WorldData w, WorldDto dto)
        {
            dto.Quests = new List<QuestInstanceDto>();
            foreach (var q in w.Quests)
            {
                dto.Quests.Add(new QuestInstanceDto
                {
                    InstanceId = q.InstanceId, DefinitionId = q.DefinitionId, DefinitionVersion = q.DefinitionVersion, State = q.State.ToString(),
                    Bindings = new Dictionary<string, string>(q.Bindings), SourceKey = q.SourceKey, CreatedAt = q.CreatedAt, DiscoveredAt = q.DiscoveredAt, OfferedAt = q.OfferedAt,
                    AcceptedAt = q.AcceptedAt, ClosedAt = q.ClosedAt, ExpiresAt = q.ExpiresAt,
                    Objectives = q.Objectives.ConvertAll(o => new ObjectiveProgressDto { Id = o.Id, Count = o.Count, Done = o.Done, Failed = o.Failed, Revealed = o.Revealed, Unavailable = o.Unavailable, DoneAt = o.DoneAt }),
                    Decisions = new List<string>(q.Decisions), Variables = new Dictionary<string, string>(q.Variables), Outcome = q.Outcome, RewardsClaimed = q.RewardsClaimed,
                    AppliedConsequences = new List<string>(q.AppliedConsequences), FailureReason = q.FailureReason,
                    History = q.History.ConvertAll(h => new QuestHistoryDto { At = h.At, Text = h.Text }), Tracked = q.Tracked,
                });
            }

            var c = w.Conversation;
            dto.Conversation = c == null ? null : new ConversationDto
            {
                Key = c.Key, NpcId = c.NpcId.ToString(), DialogueId = c.DialogueId, NodeId = c.NodeId, QuestInstanceId = c.QuestInstanceId, StartedAt = c.StartedAt, Turn = c.Turn,
                Variables = new Dictionary<string, string>(c.Variables), Transcript = c.Transcript.ConvertAll(t => new TranscriptLineDto { Speaker = t.Speaker, Text = t.Text }),
            };

            if (dto.Settings != null)
            {
                dto.Settings.Content = new Dictionary<string, string>();
                foreach (var pair in w.WorldSettings.Content.Rules) dto.Settings.Content[pair.Key.ToString()] = pair.Value.ToString();
            }

            if (dto.Player != null && w.Player != null)
            {
                var s = w.Player.Social;
                dto.Player.Social = new PlayerSocialDto
                {
                    FactionStandings = s.FactionStandings.ConvertAll(f => new FactionStandingDto { FactionId = f.FactionId.ToString(), Standing = f.Standing }),
                    SettlementStandings = s.SettlementStandings.ConvertAll(r => new SettlementStandingDto { SettlementId = r.SettlementId.ToString(), Reputation = r.Reputation }),
                    Notoriety = s.Notoriety,
                    Ledger = s.Ledger.ConvertAll(e => new ReputationChangeDto { Key = e.Key, At = e.At, Target = e.Target, Dimension = e.Dimension, Amount = e.Amount, Reason = e.Reason }),
                    KnownPlaces = new List<string>(s.KnownPlaces), VisitedSettlements = s.VisitedSettlements.ConvertAll(v => v.ToString()),
                    Discoveries = s.Discoveries.ConvertAll(d => new DiscoveryDto { Key = d.Key, Text = d.Text, At = d.At, Source = d.Source }),
                    Rumors = s.Rumors.ConvertAll(r => new RumorDto { Key = r.Key, Text = r.Text, At = r.At, SourceNpcId = r.SourceNpcId.IsEmpty ? null : r.SourceNpcId.ToString(), Confidence = r.Confidence, Verified = r.Verified }),
                    Decisions = s.Decisions.ConvertAll(d => new DecisionDto { Key = d.Key, Text = d.Text, At = d.At, QuestInstanceId = d.QuestInstanceId }),
                    AppliedKeys = new List<string>(s.AppliedKeys),
                };
            }
        }

        private static void ReadQuestWorld(WorldDto dto, WorldData world)
        {
            foreach (var d in dto.Quests ?? new List<QuestInstanceDto>())
            {
                if (d == null || string.IsNullOrEmpty(d.InstanceId)) throw new SaveDataException("A quest has no ID.");
                var q = new QuestInstanceData
                {
                    InstanceId = d.InstanceId, DefinitionId = d.DefinitionId, DefinitionVersion = d.DefinitionVersion, State = ParseEnum<QuestState>(d.State, "quest state"),
                    Bindings = new Dictionary<string, string>(d.Bindings ?? new Dictionary<string, string>(), StringComparer.Ordinal), SourceKey = d.SourceKey ?? "authored",
                    CreatedAt = d.CreatedAt, DiscoveredAt = d.DiscoveredAt, OfferedAt = d.OfferedAt, AcceptedAt = d.AcceptedAt, ClosedAt = d.ClosedAt, ExpiresAt = d.ExpiresAt,
                    Decisions = new List<string>(d.Decisions ?? new List<string>()), Variables = new Dictionary<string, string>(d.Variables ?? new Dictionary<string, string>(), StringComparer.Ordinal),
                    Outcome = d.Outcome, RewardsClaimed = d.RewardsClaimed, AppliedConsequences = new List<string>(d.AppliedConsequences ?? new List<string>()), FailureReason = d.FailureReason,
                    Tracked = d.Tracked,
                };
                foreach (var o in d.Objectives ?? new List<ObjectiveProgressDto>())
                {
                    if (o == null) throw new SaveDataException("Null quest objective.");
                    q.Objectives.Add(new ObjectiveProgressData { Id = o.Id, Count = o.Count, Done = o.Done, Failed = o.Failed, Revealed = o.Revealed, Unavailable = o.Unavailable, DoneAt = o.DoneAt });
                }

                foreach (var h in d.History ?? new List<QuestHistoryDto>()) if (h != null) q.History.Add(new QuestHistoryEntry { At = h.At, Text = h.Text });
                world.Quests.Add(q);
            }

            if (dto.Conversation != null)
            {
                var c = dto.Conversation;
                var conv = new ConversationData
                {
                    Key = c.Key, NpcId = new NpcId(RequiredGuid(c.NpcId, "conversation npcId")), DialogueId = c.DialogueId, NodeId = c.NodeId, QuestInstanceId = c.QuestInstanceId,
                    StartedAt = c.StartedAt, Turn = c.Turn, Variables = new Dictionary<string, string>(c.Variables ?? new Dictionary<string, string>(), StringComparer.Ordinal),
                };
                foreach (var t in c.Transcript ?? new List<TranscriptLineDto>()) if (t != null) conv.Transcript.Add(new TranscriptLine { Speaker = t.Speaker, Text = t.Text });
                world.Conversation = conv;
            }

            if (dto.Settings?.Content != null)
                foreach (var pair in dto.Settings.Content)
                    world.WorldSettings.Content.Rules[ParseEnum<ContentTags>(pair.Key, "content tag")] = ParseEnum<ContentPresentation>(pair.Value, "content presentation");

            if (dto.Player?.Social != null && world.Player != null)
            {
                var d = dto.Player.Social;
                var s = world.Player.Social;
                foreach (var f in d.FactionStandings ?? new List<FactionStandingDto>()) s.FactionStandings.Add(new FactionStandingData { FactionId = new FactionId(RequiredGuid(f.FactionId, "standing factionId")), Standing = f.Standing });
                foreach (var r in d.SettlementStandings ?? new List<SettlementStandingDto>()) s.SettlementStandings.Add(new SettlementStandingData { SettlementId = new SettlementId(RequiredGuid(r.SettlementId, "standing settlementId")), Reputation = r.Reputation });
                s.Notoriety = d.Notoriety;
                foreach (var e in d.Ledger ?? new List<ReputationChangeDto>()) if (e != null) s.Ledger.Add(new ReputationChangeData { Key = e.Key, At = e.At, Target = e.Target, Dimension = e.Dimension, Amount = e.Amount, Reason = e.Reason });
                s.KnownPlaces.AddRange(d.KnownPlaces ?? new List<string>());
                foreach (var v in d.VisitedSettlements ?? new List<string>()) s.VisitedSettlements.Add(new SettlementId(RequiredGuid(v, "visited settlement")));
                foreach (var x in d.Discoveries ?? new List<DiscoveryDto>()) if (x != null) s.Discoveries.Add(new DiscoveryData { Key = x.Key, Text = x.Text, At = x.At, Source = x.Source });
                foreach (var r in d.Rumors ?? new List<RumorDto>())
                    if (r != null) s.Rumors.Add(new RumorData { Key = r.Key, Text = r.Text, At = r.At, SourceNpcId = string.IsNullOrEmpty(r.SourceNpcId) ? default : new NpcId(RequiredGuid(r.SourceNpcId, "rumour source")), Confidence = r.Confidence, Verified = r.Verified });
                foreach (var x in d.Decisions ?? new List<DecisionDto>()) if (x != null) s.Decisions.Add(new DecisionData { Key = x.Key, Text = x.Text, At = x.At, QuestInstanceId = x.QuestInstanceId });
                s.AppliedKeys.AddRange(d.AppliedKeys ?? new List<string>());
            }
        }
    }
}
