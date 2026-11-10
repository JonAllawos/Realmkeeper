using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Faction categories. One shared model for all of them; behaviour differences are data
    /// (<c>PoliticalRules.Profile</c> in Simulation). Persisted by name; append, never reorder.
    /// </summary>
    public enum FactionType
    {
        Kingdom = 0,
        NobleHouse = 1,
        Guild = 2,
        ReligiousOrder = 3,
        MilitaryOrder = 4,
        MercenaryCompany = 5,
        CriminalOrganization = 6,
    }

    /// <summary>Faction lifecycle. Dissolved factions stay in the world so history keeps its references.</summary>
    public enum FactionStatus
    {
        Active = 0,

        /// <summary>Influence has fallen below the declining threshold.</summary>
        Declining = 1,

        /// <summary>Almost no influence and no presence anywhere.</summary>
        Dormant = 2,

        /// <summary>Ended. Holds nothing and acts no more, but is kept for the chronicle.</summary>
        Dissolved = 3,
    }

    /// <summary>A faction's standing in one settlement.</summary>
    public enum PresenceStatus
    {
        /// <summary>Strength at or above the established threshold.</summary>
        Established = 0,

        /// <summary>Present but weak: strength below the established threshold.</summary>
        Waning = 1,
    }

    /// <summary>State of a territorial claim. A claim never transfers governance by itself.</summary>
    public enum ClaimStatus
    {
        /// <summary>Filed, not yet answered by the governing authority.</summary>
        Unresolved = 0,

        /// <summary>The governing authority (or the claimant's sovereign) accepts the claim as legitimate.</summary>
        Recognized = 1,

        /// <summary>Disputed by the governing authority or by a competing claim.</summary>
        Contested = 2,

        /// <summary>Withdrawn by the claimant. Closed.</summary>
        Relinquished = 3,

        /// <summary>The claimant now governs the settlement. Closed.</summary>
        Fulfilled = 4,
    }

    /// <summary>A formal arrangement between two factions.</summary>
    public enum AgreementType
    {
        None = 0,
        TradeAgreement = 1,
        MutualSupport = 2,
    }

    /// <summary>Readable state derived from a relationship (thresholds in <c>PoliticalMath.StateOf</c>). Persisted by name.</summary>
    public enum RelationState
    {
        Hostile = 0,
        Rival = 1,
        Wary = 2,
        Neutral = 3,
        Friendly = 4,
        Allied = 5,
    }

    /// <summary>What last moved a relationship most (for explanation and the chronicle).</summary>
    public enum RelationCause
    {
        None = 0,
        Agreement = 1,
        SharedTrade = 2,
        Competition = 3,
        TerritorialDispute = 4,
        PoliticalSupport = 5,
        BrokenAgreement = 6,
        ManualIntervention = 7,
        Drift = 8,
    }

    public enum GoalType
    {
        /// <summary>Keep governing every currently held settlement with stability ≥ the rules' minimum. Continuous: never completes.</summary>
        MaintainControl = 0,

        /// <summary>Reach the target influence.</summary>
        GainInfluence = 1,

        /// <summary>Reach the target total members.</summary>
        IncreaseMembership = 2,

        /// <summary>Reach the target presence strength in the target settlement.</summary>
        EstablishPresence = 3,

        /// <summary>Reach the target opinion with the target faction.</summary>
        ImproveRelationship = 4,

        /// <summary>Have a recognized claim on, or govern, the target settlement.</summary>
        SecureClaim = 5,
    }

    public enum GoalStatus
    {
        Active = 0,
        Completed = 1,
        Abandoned = 2,
    }

    public enum GoalCloseReason
    {
        None = 0,
        Achieved = 1,
        AbandonedByPlayer = 2,
        TargetLost = 3,
        FactionDissolved = 4,
    }

    /// <summary>A faction's presence in one settlement: strength and aggregate local membership.</summary>
    public sealed class FactionPresenceData
    {
        public SettlementId SettlementId { get; set; }

        /// <summary>0..100. How strongly the faction is organized locally.</summary>
        public int Strength { get; set; }

        /// <summary>People in the settlement affiliated with the faction (aggregate; no NPC objects).</summary>
        public int Members { get; set; }

        public PresenceStatus Status { get; set; }

        /// <summary>Date the presence was established.</summary>
        public WorldDate EstablishedOn { get; set; }

        /// <summary>Membership share (per mille of population) when last reported to the chronicle.</summary>
        public int LastReportedSharePerMille { get; set; }

        public FactionPresenceData Clone() => (FactionPresenceData)MemberwiseClone();
    }

    /// <summary>A structured goal. Progress comes only from world conditions, never from chance.</summary>
    public sealed class FactionGoalData
    {
        public GoalId GoalId { get; set; }
        public GoalType Type { get; set; }
        public SettlementId TargetSettlementId { get; set; }
        public FactionId TargetFactionId { get; set; }

        /// <summary>Goal-specific threshold (influence, members, strength or opinion). Unused by SecureClaim and MaintainControl.</summary>
        public int TargetValue { get; set; }

        /// <summary>0..100, re-evaluated each political tick.</summary>
        public int Progress { get; set; }

        /// <summary>1 (low) … 5 (high).</summary>
        public int Priority { get; set; }

        public GoalStatus Status { get; set; }
        public WorldDate CreatedOn { get; set; }

        /// <summary>Date it completed or was abandoned; default while active.</summary>
        public WorldDate ClosedOn { get; set; }

        public GoalCloseReason CloseReason { get; set; }

        /// <summary>True when the player created it.</summary>
        public bool IsManual { get; set; }

        public bool IsOpen => Status == GoalStatus.Active;

        public FactionGoalData Clone() => (FactionGoalData)MemberwiseClone();
    }

    /// <summary>
    /// The relationship between two factions, stored once per pair with <see cref="FactionA"/> &lt;
    /// <see cref="FactionB"/>. Several dimensions, so a rivalry between friends is expressible.
    /// </summary>
    public sealed class FactionRelationData
    {
        public const int MinOpinion = -100;
        public const int MaxOpinion = 100;
        public const int MaxScore = 100;

        public FactionId FactionA { get; set; }
        public FactionId FactionB { get; set; }

        /// <summary>-100 (hatred) … +100 (devotion).</summary>
        public int Opinion { get; set; }

        /// <summary>0..100. Grows while agreements are kept; broken agreements cut it.</summary>
        public int Trust { get; set; }

        /// <summary>0..100. Competition for the same settlements, members or territory.</summary>
        public int Rivalry { get; set; }

        public AgreementType Agreement { get; set; }

        /// <summary>Date the current agreement began; default without one.</summary>
        public WorldDate AgreementSince { get; set; }

        /// <summary>The term that moved this relationship most at the last evaluation (or the last manual action).</summary>
        public RelationCause LastCause { get; set; }

        /// <summary>Derived state at the last chronicle report.</summary>
        public RelationState LastReportedState { get; set; } = RelationState.Neutral;

        public bool Involves(FactionId id) => FactionA == id || FactionB == id;

        public FactionId Other(FactionId id) => FactionA == id ? FactionB : FactionA;

        public FactionRelationData Clone() => (FactionRelationData)MemberwiseClone();
    }

    /// <summary>A faction's claim to a settlement. Distinct from governance: a claim never changes who governs.</summary>
    public sealed class TerritorialClaimData
    {
        public ClaimId ClaimId { get; set; }
        public FactionId ClaimantId { get; set; }
        public SettlementId SettlementId { get; set; }
        public ClaimStatus Status { get; set; }
        public WorldDate FiledOn { get; set; }

        /// <summary>Date of the last status change.</summary>
        public WorldDate UpdatedOn { get; set; }

        /// <summary>True when filed by the player.</summary>
        public bool IsManual { get; set; }

        public bool IsOpen => Status == ClaimStatus.Unresolved || Status == ClaimStatus.Recognized || Status == ClaimStatus.Contested;

        public TerritorialClaimData Clone() => (TerritorialClaimData)MemberwiseClone();
    }

    /// <summary>Static helpers shared by validation, simulation and commands.</summary>
    public static class FactionRules
    {
        public const int MinInfluence = 0;
        public const int MaxInfluence = 100;
        public const int MinPriority = 1;
        public const int MaxPriority = 5;
        public const int MaxDescriptionLength = 200;

        /// <summary>Only these types may be the governing authority of a settlement.</summary>
        public static bool CanGovern(FactionType type) => type == FactionType.Kingdom || type == FactionType.NobleHouse;

        /// <summary>Only noble houses administer a settlement on behalf of its governor.</summary>
        public static bool CanAdminister(FactionType type) => type == FactionType.NobleHouse;

        /// <summary>Which types may hold or claim territory.</summary>
        public static bool CanClaimTerritory(FactionType type) =>
            type == FactionType.Kingdom || type == FactionType.NobleHouse || type == FactionType.MilitaryOrder;

        public static IReadOnlyList<FactionType> AllTypes { get; } = new[]
        {
            FactionType.Kingdom, FactionType.NobleHouse, FactionType.Guild, FactionType.ReligiousOrder,
            FactionType.MilitaryOrder, FactionType.MercenaryCompany, FactionType.CriminalOrganization,
        };
    }
}
