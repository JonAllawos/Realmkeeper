using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v17 (Phase 15, ADR-0073): the player's party.</summary>
    public sealed class PartyDto
    {
        public List<PartyMemberDto> Members { get; set; }
        public string Leader { get; set; }
    }

    public sealed class PartyMemberDto
    {
        public string NpcId { get; set; }
        public string Role { get; set; }
        public string Mode { get; set; }
        public int Order { get; set; }
        public float FollowDistance { get; set; } = 2.5f;
        public long JoinedAt { get; set; }
        public long Until { get; set; } = -1;
        public string WaitSettlement { get; set; }
        public string WaitPlaceId { get; set; }
        public string WaitRoom { get; set; }
        public float[] WaitPose { get; set; } // v18: x, y, z, yaw
    }

    /// <summary>Schema v17: a person's history as a companion (kept after they leave).</summary>
    public sealed class CompanionRecordDto
    {
        public string NpcId { get; set; }
        public string Standing { get; set; }
        public int Loyalty { get; set; } = 50;
        public List<ApprovalDto> Approvals { get; set; }
        public int TimesRecruited { get; set; }
        public long LeftAt { get; set; } = -1;
        public string LeftReason { get; set; }
        public bool ItemsGranted { get; set; }
        public string PersonalQuestId { get; set; }
    }

    public sealed class ApprovalDto
    {
        public string Key { get; set; }
        public int Delta { get; set; }
        public string Reason { get; set; }
        public long At { get; set; }
    }

    public static partial class WorldSaveMapper
    {
        private static void WriteParty(WorldData w, WorldDto dto)
        {
            dto.Party = new PartyDto { Members = new List<PartyMemberDto>(), Leader = w.Party.Leader.IsEmpty ? null : w.Party.Leader.ToString() };
            foreach (var m in w.Party.Members)
                dto.Party.Members.Add(new PartyMemberDto
                {
                    NpcId = m.NpcId.ToString(), Role = m.Role.ToString(), Mode = m.Mode.ToString(), Order = m.Order, FollowDistance = m.FollowDistance, JoinedAt = m.JoinedAt,
                    Until = m.Until, WaitSettlement = m.WaitSettlement.IsEmpty ? null : m.WaitSettlement.ToString(), WaitPlaceId = m.WaitPlaceId, WaitRoom = m.WaitRoom, WaitPose = m.WaitPose is LocalPose wp ? new[] { wp.X, wp.Y, wp.Z, wp.Yaw } : null,
                });
            dto.Companions = new List<CompanionRecordDto>();
            foreach (var c in w.Companions)
            {
                var d = new CompanionRecordDto
                {
                    NpcId = c.NpcId.ToString(), Standing = c.Standing.ToString(), Loyalty = c.Loyalty, TimesRecruited = c.TimesRecruited, LeftAt = c.LeftAt, LeftReason = c.LeftReason,
                    ItemsGranted = c.ItemsGranted, PersonalQuestId = c.PersonalQuestId, Approvals = new List<ApprovalDto>(),
                };
                foreach (var a in c.Approvals) d.Approvals.Add(new ApprovalDto { Key = a.Key, Delta = a.Delta, Reason = a.Reason, At = a.At });
                dto.Companions.Add(d);
            }
        }

        private static void ReadParty(WorldDto dto, WorldData world)
        {
            world.Party = new PartyData { Leader = new NpcId(OptionalGuid(dto.Party?.Leader, "party leader")) };
            foreach (var d in dto.Party?.Members ?? new List<PartyMemberDto>())
            {
                if (d == null) throw new SaveDataException("A party member is empty.");
                world.Party.Members.Add(new PartyMemberData
                {
                    NpcId = new NpcId(RequiredGuid(d.NpcId, "party member npcId")), Role = ParseEnum<PartyRole>(d.Role, "party role"), Mode = ParseEnum<FollowMode>(d.Mode, "follow mode"),
                    Order = d.Order, FollowDistance = d.FollowDistance, JoinedAt = d.JoinedAt, Until = d.Until,
                    WaitSettlement = new SettlementId(OptionalGuid(d.WaitSettlement, "party waitSettlement")), WaitPlaceId = d.WaitPlaceId, WaitRoom = d.WaitRoom, WaitPose = d.WaitPose != null && d.WaitPose.Length == 4 ? new LocalPose(d.WaitPose[0], d.WaitPose[1], d.WaitPose[2], d.WaitPose[3]) : (LocalPose?)null,
                });
            }

            world.Companions = new List<CompanionRecordData>();
            foreach (var d in dto.Companions ?? new List<CompanionRecordDto>())
            {
                if (d == null) throw new SaveDataException("A companion record is empty.");
                var c = new CompanionRecordData
                {
                    NpcId = new NpcId(RequiredGuid(d.NpcId, "companion npcId")), Standing = ParseEnum<CompanionStanding>(d.Standing, "companion standing"), Loyalty = d.Loyalty,
                    TimesRecruited = d.TimesRecruited, LeftAt = d.LeftAt, LeftReason = d.LeftReason, ItemsGranted = d.ItemsGranted, PersonalQuestId = d.PersonalQuestId,
                };
                foreach (var a in d.Approvals ?? new List<ApprovalDto>())
                    if (a != null) c.Approvals.Add(new ApprovalData { Key = a.Key, Delta = a.Delta, Reason = a.Reason, At = a.At });
                world.Companions.Add(c);
            }
        }
    }
}
