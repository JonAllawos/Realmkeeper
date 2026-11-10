using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>One term of a faction's influence target.</summary>
    public enum InfluenceFactor
    {
        Membership = 0,
        Wealth = 1,
        Holdings = 2,
        Presence = 3,
        Economy = 4,
        Authority = 5,
        Reputation = 6,
    }

    /// <summary>
    /// What makes one faction type different from another: data, not code. The influence weights
    /// add up to 100, so an influence target is always 0..100.
    /// </summary>
    public sealed class FactionProfile
    {
        public int MembershipWeight { get; set; }
        public int WealthWeight { get; set; }
        public int HoldingsWeight { get; set; }
        public int PresenceWeight { get; set; }
        public int EconomyWeight { get; set; }
        public int AuthorityWeight { get; set; }
        public int ReputationWeight { get; set; }

        /// <summary>Members, per mille of the world's population, at which the membership score is 100.</summary>
        public int FullMembershipPerMille { get; set; }

        /// <summary>Largest share of a settlement's population (per mille) the faction can enrol at strength 100.</summary>
        public int MaxSharePerMille { get; set; }

        /// <summary>Coin collected per political tick per 1,000 local members.</summary>
        public int DuesPerThousandMembers { get; set; }

        /// <summary>True: the economy score follows local trade activity. False: local prosperity.</summary>
        public bool EconomyFollowsTrade { get; set; }

        public int WeightOf(InfluenceFactor factor)
        {
            switch (factor)
            {
                case InfluenceFactor.Membership: return MembershipWeight;
                case InfluenceFactor.Wealth: return WealthWeight;
                case InfluenceFactor.Holdings: return HoldingsWeight;
                case InfluenceFactor.Presence: return PresenceWeight;
                case InfluenceFactor.Economy: return EconomyWeight;
                case InfluenceFactor.Authority: return AuthorityWeight;
                default: return ReputationWeight;
            }
        }

        public int TotalWeight => MembershipWeight + WealthWeight + HoldingsWeight + PresenceWeight + EconomyWeight + AuthorityWeight + ReputationWeight;
    }

    /// <summary>
    /// Every political constant in one place. Integers only, so results are exact and identical
    /// on every runtime. Documented in Documentation/Systems/Politics.md.
    /// </summary>
    public sealed class PoliticalRules
    {
        private readonly Dictionary<FactionType, FactionProfile> _profiles = new Dictionary<FactionType, FactionProfile>
        {
            [FactionType.Kingdom] = new FactionProfile
            {
                MembershipWeight = 15, WealthWeight = 10, HoldingsWeight = 30, PresenceWeight = 10, EconomyWeight = 5, AuthorityWeight = 20, ReputationWeight = 10,
                FullMembershipPerMille = 500, MaxSharePerMille = 600, DuesPerThousandMembers = 20,
            },
            [FactionType.NobleHouse] = new FactionProfile
            {
                MembershipWeight = 10, WealthWeight = 20, HoldingsWeight = 25, PresenceWeight = 15, EconomyWeight = 10, AuthorityWeight = 10, ReputationWeight = 10,
                FullMembershipPerMille = 80, MaxSharePerMille = 100, DuesPerThousandMembers = 60,
            },
            [FactionType.Guild] = new FactionProfile
            {
                MembershipWeight = 30, WealthWeight = 20, HoldingsWeight = 0, PresenceWeight = 15, EconomyWeight = 25, AuthorityWeight = 0, ReputationWeight = 10,
                FullMembershipPerMille = 100, MaxSharePerMille = 120, DuesPerThousandMembers = 80, EconomyFollowsTrade = true,
            },
            [FactionType.ReligiousOrder] = new FactionProfile
            {
                MembershipWeight = 40, WealthWeight = 5, HoldingsWeight = 0, PresenceWeight = 30, EconomyWeight = 5, AuthorityWeight = 0, ReputationWeight = 20,
                FullMembershipPerMille = 300, MaxSharePerMille = 350, DuesPerThousandMembers = 15,
            },
            [FactionType.MilitaryOrder] = new FactionProfile
            {
                MembershipWeight = 25, WealthWeight = 15, HoldingsWeight = 10, PresenceWeight = 20, EconomyWeight = 0, AuthorityWeight = 10, ReputationWeight = 20,
                FullMembershipPerMille = 30, MaxSharePerMille = 40, DuesPerThousandMembers = 50,
            },
            [FactionType.MercenaryCompany] = new FactionProfile
            {
                MembershipWeight = 25, WealthWeight = 35, HoldingsWeight = 0, PresenceWeight = 15, EconomyWeight = 15, AuthorityWeight = 0, ReputationWeight = 10,
                FullMembershipPerMille = 20, MaxSharePerMille = 30, DuesPerThousandMembers = 100, EconomyFollowsTrade = true,
            },
            [FactionType.CriminalOrganization] = new FactionProfile
            {
                MembershipWeight = 20, WealthWeight = 30, HoldingsWeight = 0, PresenceWeight = 25, EconomyWeight = 25, AuthorityWeight = 0, ReputationWeight = 0,
                FullMembershipPerMille = 30, MaxSharePerMille = 40, DuesPerThousandMembers = 100, EconomyFollowsTrade = true,
            },
        };

        /// <summary>Days between political ticks. A tick runs on days whose day number divides evenly by this.</summary>
        public int TickIntervalDays { get; set; } = 7;

        /// <summary>Most a faction's influence moves toward its target per tick.</summary>
        public int MaxInfluenceStep { get; set; } = 2;

        /// <summary>Treasury at which the wealth score is 100.</summary>
        public int WealthForFullScore { get; set; } = 1500;

        /// <summary>Settlement trade activity at which a trade-following economy score is 100.</summary>
        public int TradeActivityForFullScore { get; set; } = 600;

        /// <summary>Percent of a faction's treasury spent on upkeep each tick.</summary>
        public int UpkeepPercent { get; set; } = 3;

        /// <summary>A settlement keeps this many days of its own coin upkeep before paying any dues.</summary>
        public int DuesReserveDays { get; set; } = 30;

        /// <summary>Presence strength at or above which a presence is Established (below: Waning).</summary>
        public int EstablishedStrength { get; set; } = 25;

        /// <summary>Strength lost per stronger rival or hostile faction in the same settlement.</summary>
        public int RivalPressure { get; set; } = 5;

        /// <summary>Most a faction's local membership changes per tick, as a divisor of the population (and at least 1).</summary>
        public int MembershipStepDivisor { get; set; } = 100;

        /// <summary>Change in membership share (per mille of population) that is worth a chronicle entry.</summary>
        public int AffiliationReportPerMille { get; set; } = 50;

        /// <summary>Influence change that is worth a chronicle entry.</summary>
        public int InfluenceReportStep { get; set; } = 10;

        public int MaxOpinionStep { get; set; } = 2;
        public int MaxRivalryStep { get; set; } = 3;
        public int TradeAgreementOpinion { get; set; } = 20;
        public int MutualSupportOpinion { get; set; } = 35;
        public int SharedProsperityOpinion { get; set; } = 10;
        public int SharedProsperityCap { get; set; } = 20;
        public int SharedProsperityThreshold { get; set; } = 55;
        public int AdministrationOpinion { get; set; } = 15;
        public int RecognizedClaimOpinion { get; set; } = 10;
        public int ContestedClaimOpinion { get; set; } = 30;
        public int RivalryPerSharedSettlement { get; set; } = 20;
        public int RivalryPerContestedClaim { get; set; } = 40;
        public int RivalryPerUnresolvedClaim { get; set; } = 15;

        /// <summary>Opinion and trust lost when an agreement is broken.</summary>
        public int BrokenAgreementOpinion { get; set; } = 25;
        public int BrokenAgreementTrust { get; set; } = 30;

        /// <summary>Below this influence a faction is Declining; it is Active again at <see cref="RecoveryInfluence"/>.</summary>
        public int DecliningInfluence { get; set; } = 20;
        public int RecoveryInfluence { get; set; } = 25;

        /// <summary>At or below this influence a faction that governs nothing is Dormant.</summary>
        public int DormantInfluence { get; set; } = 5;

        /// <summary>Consecutive dormant ticks after which a faction dissolves.</summary>
        public int DissolveAfterDormantTicks { get; set; } = 26;

        /// <summary>Influence a faction needs before a goal makes it open a presence in a new settlement.</summary>
        public int MinInfluenceToExpand { get; set; } = 25;

        /// <summary>Strength of a presence opened by the simulation.</summary>
        public int NewPresenceStrength { get; set; } = 5;

        /// <summary>Presence strength a faction needs before a goal makes it file a claim.</summary>
        public int MinStrengthToClaim { get; set; } = 30;

        /// <summary>Presence strength at which a claim on an ungoverned settlement is recognized.</summary>
        public int UncontestedClaimStrength { get; set; } = 50;

        /// <summary>Stability a governed settlement needs to count as "under control".</summary>
        public int ControlStability { get; set; } = 40;

        public FactionProfile Profile(FactionType type) =>
            _profiles.TryGetValue(type, out var p) ? p : throw new InvalidOperationException($"No profile for faction type {type}.");

        public void Validate()
        {
            foreach (var type in FactionRules.AllTypes)
            {
                var p = Profile(type);
                if (p.TotalWeight != 100) throw new InvalidOperationException($"Influence weights of {type} add up to {p.TotalWeight}, not 100.");
                if (p.FullMembershipPerMille < 1 || p.MaxSharePerMille < 1 || p.MaxSharePerMille > 1000 || p.DuesPerThousandMembers < 0)
                    throw new InvalidOperationException($"Profile of {type} has an invalid membership or dues value.");
            }

            if (TickIntervalDays < 1 || MaxInfluenceStep < 1 || WealthForFullScore < 1 || TradeActivityForFullScore < 1 || MembershipStepDivisor < 1)
                throw new InvalidOperationException("Political cadence and scale values must be at least 1.");
            if (UpkeepPercent < 0 || UpkeepPercent > 100) throw new InvalidOperationException("UpkeepPercent must be 0..100.");
            if (RecoveryInfluence < DecliningInfluence) throw new InvalidOperationException("RecoveryInfluence must be >= DecliningInfluence.");
            if (DissolveAfterDormantTicks < 1) throw new InvalidOperationException("DissolveAfterDormantTicks must be >= 1.");
        }
    }
}
