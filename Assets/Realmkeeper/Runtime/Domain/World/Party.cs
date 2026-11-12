using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Whether a party member is in the active line-up (fights, follows) or held in reserve (Phase 15).</summary>
    public enum PartyRole
    {
        Active = 0,
        Reserve = 1,
    }

    /// <summary>What a party member does when the player moves on (Phase 15).</summary>
    public enum FollowMode
    {
        Follow = 0,
        Wait = 1,
    }

    /// <summary>Where a person stands with the player as a companion, in or out of the party (Phase 15).</summary>
    public enum CompanionStanding
    {
        /// <summary>Travelling with the player.</summary>
        InParty = 0,

        /// <summary>Sent home by the player; may be asked again.</summary>
        Dismissed = 1,

        /// <summary>Left of their own accord over a grievance; will come back only when it is mended.</summary>
        Departed = 2,

        /// <summary>A temporary arrangement ran out; they went back to their life.</summary>
        TermEnded = 3,
    }

    /// <summary>
    /// One member of the player's party (Phase 15, ADR-0073). The person is the world's own <see cref="NpcData"/>: this only
    /// records their place in the party. Their schedule, home, work and relationships are untouched; while they travel
    /// with the player, <c>PlacePresence.IsAway</c> keeps the daily schedule from moving them.
    /// </summary>
    public sealed class PartyMemberData
    {
        public NpcId NpcId { get; set; }
        public PartyRole Role { get; set; }
        public FollowMode Mode { get; set; }

        /// <summary>Formation order (0 first behind the player).</summary>
        public int Order { get; set; }

        /// <summary>How far behind the player they keep, in metres (exploration).</summary>
        public float FollowDistance { get; set; } = 2.5f;

        /// <summary>Absolute world minute they joined.</summary>
        public long JoinedAt { get; set; }

        /// <summary>Absolute world minute a temporary arrangement ends (−1: permanent).</summary>
        public long Until { get; set; } = -1;

        /// <summary>Where they wait when told to (settlement or place, and room inside a place); empty when following.</summary>
        public SettlementId WaitSettlement { get; set; }
        public string WaitPlaceId { get; set; }
        public string WaitRoom { get; set; }

        /// <summary>Phase 16: where in the settlement they stand while waiting (their body's place when told to wait); null when unknown.</summary>
        public LocalPose? WaitPose { get; set; }

        public bool Temporary => Until >= 0;

        public PartyMemberData Clone() => (PartyMemberData)MemberwiseClone();
    }

    /// <summary>The player's party (Phase 15). The player always leads unless a member is named leader (for presentation only).</summary>
    public sealed class PartyData
    {
        public List<PartyMemberData> Members { get; set; } = new List<PartyMemberData>();

        /// <summary>Who leads the formation (empty: the player).</summary>
        public NpcId Leader { get; set; }

        public PartyMemberData Find(NpcId id)
        {
            foreach (var m in Members) if (m.NpcId == id) return m;
            return null;
        }

        public bool Contains(NpcId id) => Find(id) != null;

        public int Count(PartyRole role)
        {
            var n = 0;
            foreach (var m in Members) if (m.Role == role) n++;
            return n;
        }

        public PartyData Clone()
        {
            var p = new PartyData { Leader = Leader };
            foreach (var m in Members) p.Members.Add(m.Clone());
            return p;
        }
    }

    /// <summary>One remembered reason a companion's loyalty moved (Phase 15): applied once per key, explained in words.</summary>
    public sealed class ApprovalData
    {
        /// <summary>Stable key of the cause ("crime:4", "quest:…:done", "abandoned:enc-…"): the same cause never counts twice.</summary>
        public string Key { get; set; }
        public int Delta { get; set; }
        public string Reason { get; set; }
        public long At { get; set; }

        public ApprovalData Clone() => (ApprovalData)MemberwiseClone();
    }

    /// <summary>
    /// A person's history as the player's companion (Phase 15): loyalty, what moved it, whether they are with the player
    /// now or left, and why. Kept after dismissal or departure so asking again remembers the past.
    /// </summary>
    public sealed class CompanionRecordData
    {
        public const int MaxApprovals = 40;

        public NpcId NpcId { get; set; }
        public CompanionStanding Standing { get; set; }
        private int _loyalty = 50;

        /// <summary>0–100. Below <see cref="PartyRules.LeaveBelow"/> they leave; they return only at <see cref="PartyRules.ReturnAt"/> or above.</summary>
        public int Loyalty { get => _loyalty; set => _loyalty = Math.Max(0, Math.Min(100, value)); }

        public List<ApprovalData> Approvals { get; set; } = new List<ApprovalData>();
        public int TimesRecruited { get; set; }
        public long LeftAt { get; set; } = -1;
        public string LeftReason { get; set; }

        /// <summary>Their gear strings have been turned into item instances (once; never again).</summary>
        public bool ItemsGranted { get; set; }

        /// <summary>The personal quest offered to the player, if any (a quest instance ID).</summary>
        public string PersonalQuestId { get; set; }

        public bool Counted(string key)
        {
            foreach (var a in Approvals) if (a.Key == key) return true;
            return false;
        }

        public CompanionRecordData Clone()
        {
            var c = (CompanionRecordData)MemberwiseClone();
            c.Approvals = new List<ApprovalData>();
            foreach (var a in Approvals) c.Approvals.Add(a.Clone());
            return c;
        }
    }

    /// <summary>Party rules (Phase 15): capacities and loyalty thresholds, data rather than hard-coded limits in the architecture.</summary>
    public static class PartyRules
    {
        /// <summary>Members who travel and fight with the player.</summary>
        public static int ActiveCapacity { get; set; } = 3;

        /// <summary>Members held in reserve (they wait at an inn or at home, still in the party).</summary>
        public static int ReserveCapacity { get; set; } = 3;

        /// <summary>Loyalty below which a companion leaves.</summary>
        public const int LeaveBelow = 15;

        /// <summary>Loyalty a departed companion needs before they will come back.</summary>
        public const int ReturnAt = 35;

        /// <summary>A change this large (either way) is announced to the player; smaller ones are only recorded.</summary>
        public const int AnnounceAt = 5;

        /// <summary>Default length of a temporary arrangement, in days.</summary>
        public const int TemporaryDays = 3;
    }
}
