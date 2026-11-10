using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    // Immutable political snapshots for presentation. Like every query type, they copy values
    // out of the world; the UI never holds a domain object.

    /// <summary>One faction's standing in one settlement.</summary>
    public sealed class FactionPresenceSummary
    {
        internal FactionPresenceSummary(FactionData f, FactionPresenceData p, SettlementData s)
        {
            FactionId = f.FactionId;
            FactionName = f.Name;
            FactionType = f.Type;
            SettlementId = s.SettlementId;
            SettlementName = s.Name;
            Strength = p.Strength;
            Members = p.Members;
            SharePerMille = PoliticalMath.SharePerMille(p.Members, s.Population);
            Status = p.Status;
            IsGovernor = s.FactionId == f.FactionId;
            IsAdministrator = s.AdministratorFactionId == f.FactionId;
        }

        public FactionId FactionId { get; }
        public string FactionName { get; }
        public FactionType FactionType { get; }
        public SettlementId SettlementId { get; }
        public string SettlementName { get; }

        /// <summary>0..100 local organisation.</summary>
        public int Strength { get; }

        /// <summary>People of the settlement affiliated with the faction.</summary>
        public int Members { get; }

        /// <summary>Members as a share of the settlement's population, per mille.</summary>
        public int SharePerMille { get; }

        public PresenceStatus Status { get; }
        public bool IsGovernor { get; }
        public bool IsAdministrator { get; }
    }

    public sealed class FactionGoalSummary
    {
        internal FactionGoalSummary(FactionGoalData g, string settlementName, string factionName)
        {
            GoalId = g.GoalId;
            Type = g.Type;
            TargetSettlementId = g.TargetSettlementId;
            TargetSettlementName = settlementName;
            TargetFactionId = g.TargetFactionId;
            TargetFactionName = factionName;
            TargetValue = g.TargetValue;
            Progress = g.Progress;
            Priority = g.Priority;
            Status = g.Status;
            CreatedOn = g.CreatedOn;
            ClosedOn = g.ClosedOn.IsValidDate ? g.ClosedOn : (WorldDate?)null;
            CloseReason = g.CloseReason;
            IsManual = g.IsManual;
            Description = PoliticalText.Goal(g.Type, g.TargetValue, settlementName, factionName);
        }

        public GoalId GoalId { get; }
        public GoalType Type { get; }
        public SettlementId TargetSettlementId { get; }
        public string TargetSettlementName { get; }
        public FactionId TargetFactionId { get; }
        public string TargetFactionName { get; }
        public int TargetValue { get; }
        public int Progress { get; }
        public int Priority { get; }
        public GoalStatus Status { get; }
        public WorldDate CreatedOn { get; }
        public WorldDate? ClosedOn { get; }
        public GoalCloseReason CloseReason { get; }

        /// <summary>True when the player created the goal.</summary>
        public bool IsManual { get; }

        /// <summary>The goal in words, built from its structured fields.</summary>
        public string Description { get; }
    }

    public sealed class FactionRelationSummary
    {
        internal FactionRelationSummary(FactionRelationData r, FactionData a, FactionData b, RelationEvaluation evaluation)
        {
            FactionA = r.FactionA;
            FactionB = r.FactionB;
            NameA = a.Name;
            NameB = b.Name;
            Opinion = r.Opinion;
            Trust = r.Trust;
            Rivalry = r.Rivalry;
            Agreement = r.Agreement;
            AgreementSince = r.AgreementSince.IsValidDate ? r.AgreementSince : (WorldDate?)null;
            State = PoliticalMath.StateOf(r);
            LastCause = r.LastCause;
            OpinionTarget = evaluation?.OpinionTarget ?? r.Opinion;
            RivalryTarget = evaluation?.RivalryTarget ?? r.Rivalry;
            Terms = evaluation != null ? new List<OpinionTerm>(evaluation.Terms) : new List<OpinionTerm>();
            InvolvesDissolved = a.IsDissolved || b.IsDissolved;
        }

        public FactionId FactionA { get; }
        public FactionId FactionB { get; }
        public string NameA { get; }
        public string NameB { get; }

        /// <summary>-100..100.</summary>
        public int Opinion { get; }

        /// <summary>0..100.</summary>
        public int Trust { get; }

        /// <summary>0..100.</summary>
        public int Rivalry { get; }

        public AgreementType Agreement { get; }
        public WorldDate? AgreementSince { get; }

        /// <summary>Readable state derived from opinion, rivalry and agreement.</summary>
        public RelationState State { get; }

        public RelationCause LastCause { get; }

        /// <summary>Where opinion is heading, and the terms that add up to it.</summary>
        public int OpinionTarget { get; }

        public int RivalryTarget { get; }
        public IReadOnlyList<OpinionTerm> Terms { get; }
        public bool InvolvesDissolved { get; }

        public FactionId Other(FactionId self) => FactionA == self ? FactionB : FactionA;
        public string OtherName(FactionId self) => FactionA == self ? NameB : NameA;
    }

    public sealed class TerritorialClaimSummary
    {
        internal TerritorialClaimSummary(TerritorialClaimData c, string claimantName, string settlementName)
        {
            ClaimId = c.ClaimId;
            ClaimantId = c.ClaimantId;
            ClaimantName = claimantName;
            SettlementId = c.SettlementId;
            SettlementName = settlementName;
            Status = c.Status;
            FiledOn = c.FiledOn;
            UpdatedOn = c.UpdatedOn;
            IsManual = c.IsManual;
            IsOpen = c.IsOpen;
        }

        public ClaimId ClaimId { get; }
        public FactionId ClaimantId { get; }
        public string ClaimantName { get; }
        public SettlementId SettlementId { get; }
        public string SettlementName { get; }
        public ClaimStatus Status { get; }
        public WorldDate FiledOn { get; }
        public WorldDate UpdatedOn { get; }
        public bool IsManual { get; }
        public bool IsOpen { get; }
    }

    public sealed class FactionSummary
    {
        internal FactionSummary(WorldData world, FactionData f, PoliticalRules rules)
        {
            FactionId = f.FactionId;
            Name = f.Name;
            Description = f.Description ?? string.Empty;
            Type = f.Type;
            Status = f.Status;
            FoundedOn = f.FoundedOn;
            DissolvedOn = f.DissolvedOn.IsValidDate ? f.DissolvedOn : (WorldDate?)null;
            LeaderNpcId = f.LeaderNpcId;
            LeaderName = f.LeaderNpcId.IsEmpty ? null : world.FindNpc(f.LeaderNpcId)?.Name;
            Influence = f.Influence;
            Treasury = f.Treasury;
            TotalMembers = f.TotalMembers;

            var parts = PoliticalMath.InfluenceContributions(world, f, rules);
            InfluenceContributions = parts;
            InfluenceTarget = f.IsDissolved ? f.Influence : PoliticalMath.InfluenceTarget(parts);
            InfluenceTrend = InfluenceTarget > Influence ? RatingTrend.Rising : InfluenceTarget < Influence ? RatingTrend.Falling : RatingTrend.Steady;

            var presence = new List<FactionPresenceSummary>();
            foreach (var p in f.Presence)
            {
                var s = world.FindSettlement(p.SettlementId);
                if (s != null) presence.Add(new FactionPresenceSummary(f, p, s));
            }

            Presence = presence;

            var governed = new List<SettlementId>();
            var administered = new List<SettlementId>();
            var held = new List<string>();
            foreach (var s in world.Settlements)
            {
                if (s.FactionId == f.FactionId)
                {
                    governed.Add(s.SettlementId);
                    held.Add(s.Name);
                }
                else if (s.AdministratorFactionId == f.FactionId)
                {
                    administered.Add(s.SettlementId);
                    held.Add(s.Name + " (administers)");
                }
            }

            GovernedSettlementIds = governed;
            AdministeredSettlementIds = administered;
            HoldingNames = held;

            var goals = new List<FactionGoalSummary>();
            FactionGoalSummary primary = null;
            foreach (var g in f.Goals)
            {
                var summary = new FactionGoalSummary(g, world.FindSettlement(g.TargetSettlementId)?.Name, world.FindFaction(g.TargetFactionId)?.Name);
                goals.Add(summary);
                if (g.IsOpen && (primary == null || g.Priority > primary.Priority)) primary = summary;
            }

            Goals = goals;
            PrimaryGoal = primary;

            var claims = new List<TerritorialClaimSummary>();
            foreach (var c in world.Claims)
            {
                if (c.ClaimantId == f.FactionId) claims.Add(new TerritorialClaimSummary(c, f.Name, world.FindSettlement(c.SettlementId)?.Name));
            }

            Claims = claims;

            var relations = new List<FactionRelationSummary>();
            foreach (var r in world.Relations)
            {
                if (!r.Involves(f.FactionId)) continue;
                var a = world.FindFaction(r.FactionA);
                var b = world.FindFaction(r.FactionB);
                if (a == null || b == null) continue;
                var live = !a.IsDissolved && !b.IsDissolved;
                relations.Add(new FactionRelationSummary(r, a, b, live ? PoliticalMath.EvaluateRelation(world, a, b, r, rules) : null));
            }

            Relations = relations;
        }

        public FactionId FactionId { get; }
        public string Name { get; }
        public string Description { get; }
        public FactionType Type { get; }
        public FactionStatus Status { get; }
        public bool IsDissolved => Status == FactionStatus.Dissolved;
        public WorldDate FoundedOn { get; }
        public WorldDate? DissolvedOn { get; }

        /// <summary>Empty when the leadership is vacant.</summary>
        public NpcId LeaderNpcId { get; }

        /// <summary>Null when the leadership is vacant.</summary>
        public string LeaderName { get; }

        public bool HasLeader => !LeaderNpcId.IsEmpty;

        /// <summary>0..100.</summary>
        public int Influence { get; }

        /// <summary>Where influence is heading: the sum of <see cref="InfluenceContributions"/>.</summary>
        public int InfluenceTarget { get; }

        public RatingTrend InfluenceTrend { get; }

        /// <summary>The terms of the influence target (weight × score for each factor).</summary>
        public IReadOnlyList<InfluenceContribution> InfluenceContributions { get; }

        public int Treasury { get; }

        /// <summary>Affiliated people across all settlements (aggregate).</summary>
        public int TotalMembers { get; }

        public IReadOnlyList<FactionPresenceSummary> Presence { get; }
        public IReadOnlyList<SettlementId> GovernedSettlementIds { get; }
        public IReadOnlyList<SettlementId> AdministeredSettlementIds { get; }

        /// <summary>Names of the settlements it governs or administers, for display.</summary>
        public IReadOnlyList<string> HoldingNames { get; }

        public IReadOnlyList<FactionGoalSummary> Goals { get; }

        /// <summary>The open goal with the highest priority, or null.</summary>
        public FactionGoalSummary PrimaryGoal { get; }

        /// <summary>Claims this faction has made (open and closed).</summary>
        public IReadOnlyList<TerritorialClaimSummary> Claims { get; }

        public IReadOnlyList<FactionRelationSummary> Relations { get; }
    }

    /// <summary>The political picture of one settlement.</summary>
    public sealed class SettlementPoliticsSummary
    {
        internal SettlementPoliticsSummary(WorldData world, SettlementData s)
        {
            SettlementId = s.SettlementId;
            Population = s.Population;
            Stability = s.Stability;
            GoverningFactionId = s.FactionId;
            GoverningFactionName = world.FindFaction(s.FactionId)?.Name;
            AdministratorFactionId = s.AdministratorFactionId;
            AdministratorFactionName = world.FindFaction(s.AdministratorFactionId)?.Name;

            var presence = new List<FactionPresenceSummary>();
            var affiliated = 0;
            foreach (var f in world.Factions)
            {
                var p = f.FindPresence(s.SettlementId);
                if (p == null) continue;
                presence.Add(new FactionPresenceSummary(f, p, s));
                affiliated += p.Members;
            }

            presence.Sort((a, b) => b.Strength != a.Strength ? b.Strength.CompareTo(a.Strength) : a.FactionId.CompareTo(b.FactionId));
            Presence = presence;
            AffiliatedPopulation = affiliated;

            var claims = new List<TerritorialClaimSummary>();
            var contested = false;
            foreach (var c in world.Claims)
            {
                if (c.SettlementId != s.SettlementId || !c.IsOpen) continue;
                claims.Add(new TerritorialClaimSummary(c, world.FindFaction(c.ClaimantId)?.Name, s.Name));
                if (c.Status == ClaimStatus.Contested) contested = true;
            }

            OpenClaims = claims;
            IsContested = contested;
        }

        public SettlementId SettlementId { get; }
        public int Population { get; }
        public int Stability { get; }

        /// <summary>The governing authority; empty when the settlement is independent.</summary>
        public FactionId GoverningFactionId { get; }

        public string GoverningFactionName { get; }
        public bool IsIndependent => GoverningFactionId.IsEmpty;

        /// <summary>Noble house administering for the governor; empty when the governor rules directly.</summary>
        public FactionId AdministratorFactionId { get; }

        public string AdministratorFactionName { get; }

        /// <summary>Every faction present, strongest first (the governor is one of them when it has a presence).</summary>
        public IReadOnlyList<FactionPresenceSummary> Presence { get; }

        /// <summary>Sum of all factions' members here. Never more than <see cref="Population"/>.</summary>
        public int AffiliatedPopulation { get; }

        /// <summary>Open claims on this settlement (none of them changes who governs).</summary>
        public IReadOnlyList<TerritorialClaimSummary> OpenClaims { get; }

        /// <summary>True when an open claim on the settlement is contested.</summary>
        public bool IsContested { get; }
    }

    /// <summary>Words for political data. The simulation stores facts; wording lives here.</summary>
    public static class PoliticalText
    {
        public static string TypeName(FactionType type)
        {
            switch (type)
            {
                case FactionType.NobleHouse: return "Noble house";
                case FactionType.ReligiousOrder: return "Religious organization";
                case FactionType.MilitaryOrder: return "Military order";
                case FactionType.MercenaryCompany: return "Mercenary company";
                case FactionType.CriminalOrganization: return "Criminal organization";
                default: return type.ToString();
            }
        }

        public static string Goal(GoalType type, int value, string settlement, string faction)
        {
            var n = value.ToString("N0", CultureInfo.InvariantCulture);
            switch (type)
            {
                case GoalType.MaintainControl: return "Keep its settlements under control";
                case GoalType.GainInfluence: return $"Reach influence {n}";
                case GoalType.IncreaseMembership: return $"Reach {n} members";
                case GoalType.EstablishPresence: return $"Build a presence of {n} in {settlement ?? "a settlement"}";
                case GoalType.ImproveRelationship: return $"Reach opinion {Signed(value)} with {faction ?? "another faction"}";
                default: return $"Secure a recognized claim on {settlement ?? "a settlement"}";
            }
        }

        public static string Cause(RelationCause cause)
        {
            switch (cause)
            {
                case RelationCause.Agreement: return "their agreement";
                case RelationCause.SharedTrade: return "shared prosperity";
                case RelationCause.Competition: return "competition";
                case RelationCause.TerritorialDispute: return "a territorial dispute";
                case RelationCause.PoliticalSupport: return "political support";
                case RelationCause.BrokenAgreement: return "a broken agreement";
                case RelationCause.ManualIntervention: return "your intervention";
                default: return "time";
            }
        }

        public static string Agreement(AgreementType type) =>
            type == AgreementType.TradeAgreement ? "trade agreement" : type == AgreementType.MutualSupport ? "pact of mutual support" : "no agreement";

        public static string Signed(int value) => (value > 0 ? "+" : value < 0 ? "−" : string.Empty) + Math.Abs(value).ToString(CultureInfo.InvariantCulture);
    }
}
