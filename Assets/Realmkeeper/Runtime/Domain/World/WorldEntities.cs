using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// A political group (Phase 3). One shared model for every <see cref="FactionType"/>.
    /// Identity is <see cref="FactionId"/>, never the name. A dissolved faction stays in
    /// <see cref="WorldData.Factions"/> so chronicle references remain valid.
    /// </summary>
    public sealed class FactionData
    {
        public const int MaxNameLength = 64;

        public FactionId FactionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public FactionType Type { get; set; }
        public WorldDate FoundedOn { get; set; }
        public FactionStatus Status { get; set; }

        /// <summary>Date the faction dissolved; default while it exists.</summary>
        public WorldDate DissolvedOn { get; set; }

        /// <summary>Leader (an existing named resident). Empty means the leadership is vacant.</summary>
        public NpcId LeaderNpcId { get; set; }

        /// <summary>0..100. Moves gradually toward <see cref="InfluenceTarget"/> each political tick.</summary>
        public int Influence { get; set; }

        /// <summary>Target from the influence formula at the last political tick.</summary>
        public int InfluenceTarget { get; set; }

        /// <summary>The faction's own coin: dues paid by settlements, minus upkeep. Never negative.</summary>
        public int Treasury { get; set; }

        /// <summary>Consecutive political ticks spent dormant (dissolution follows a sustained period).</summary>
        public int DormantTicks { get; set; }

        /// <summary>Influence at the last chronicle report (milestones every 10 points).</summary>
        public int LastReportedInfluence { get; set; }

        public List<FactionPresenceData> Presence { get; set; } = new List<FactionPresenceData>();
        public List<FactionGoalData> Goals { get; set; } = new List<FactionGoalData>();

        public bool IsDissolved => Status == FactionStatus.Dissolved;

        public FactionPresenceData FindPresence(SettlementId id)
        {
            foreach (var p in Presence)
            {
                if (p.SettlementId == id) return p;
            }

            return null;
        }

        public FactionGoalData FindGoal(GoalId id)
        {
            foreach (var g in Goals)
            {
                if (g.GoalId == id) return g;
            }

            return null;
        }

        public int TotalMembers
        {
            get
            {
                var n = 0;
                foreach (var p in Presence) n += p.Members;
                return n;
            }
        }

        public FactionData Clone()
        {
            var copy = (FactionData)MemberwiseClone();
            copy.Presence = new List<FactionPresenceData>(Presence.Count);
            foreach (var p in Presence) copy.Presence.Add(p.Clone());
            copy.Goals = new List<FactionGoalData>(Goals.Count);
            foreach (var g in Goals) copy.Goals.Add(g.Clone());
            return copy;
        }
    }

    /// <summary>What a named resident does in their settlement. Persisted by name.</summary>
    public enum NpcRole
    {
        Resident = 0,
        Elder = 1,
        Merchant = 2,
        Farmer = 3,
        Guard = 4,
        Smith = 5,
        Innkeeper = 6,
        Labourer = 7,
    }

    /// <summary>
    /// A named person. Named NPCs are a tracked subset of their home settlement's population:
    /// they are counted in <see cref="SettlementData.Population"/>, never added to it
    /// (ADR-0009 point 5, first step). Exploration shows these records; it never creates its own.
    /// </summary>
    public sealed class NpcData
    {
        public const int MaxNameLength = 64;

        public NpcId NpcId { get; set; }
        public string Name { get; set; }
        public SettlementId HomeSettlementId { get; set; }
        public FactionId FactionId { get; set; }
        public NpcRole Role { get; set; }

        /// <summary>Date the player first spoke with this person; default (invalid) when they have not met.</summary>
        public WorldDate MetPlayerOn { get; set; }

        /// <summary>Conversations with the player so far.</summary>
        public int ConversationCount { get; set; }

        public bool HasMetPlayer => MetPlayerOn.IsValidDate;

        // ----- Phase 4: identity, inner life and whereabouts -----------------------------------

        public NpcSex Sex { get; set; }

        /// <summary>Date of birth (invalid when unknown).</summary>
        public WorldDate BirthDate { get; set; }

        public NpcTier Tier { get; set; }
        public NpcPersonality Personality { get; set; } = new NpcPersonality();
        public NpcSkills Skills { get; set; } = new NpcSkills();
        public NpcCondition Condition { get; set; } = new NpcCondition();

        /// <summary>Where they live, work and are today (locations in <see cref="WorldData.Locations"/>).</summary>
        public LocationId ResidenceId { get; set; }
        public LocationId WorkplaceId { get; set; }
        public LocationId CurrentLocationId { get; set; }
        public NpcScheduleData Schedule { get; set; } = new NpcScheduleData();

        public List<NpcGoalData> Goals { get; set; } = new List<NpcGoalData>();
        public List<NpcMemoryData> Memories { get; set; } = new List<NpcMemoryData>();
        public List<NpcKnowledgeData> Knowledge { get; set; } = new List<NpcKnowledgeData>();

        public bool IsDeceased { get; set; }
        public WorldDate DiedOn { get; set; }

        /// <summary>Last day the simulation updated this person (tier 2 skips days).</summary>
        public WorldDate LastUpdatedOn { get; set; }

        /// <summary>Phase 5: how this person regards the player, with its causes.</summary>
        public PlayerRegardData PlayerRegard { get; set; } = new PlayerRegardData();

        /// <summary>Phase 10: the person's archetype, gear and (for sheet-holders) character sheet; null until generated.</summary>
        public NpcCharacterData Character { get; set; }

        public bool IsAlive => !IsDeceased;

        /// <summary>Whole years of age on a date (0 when the birth date is unknown).</summary>
        public int AgeOn(WorldDate date) => BirthDate.IsValidDate && date.IsValidDate ? Math.Max(0, (int)((date.ToDayNumber() - BirthDate.ToDayNumber()) / WorldDate.DaysPerYear)) : 0;

        public NpcData Clone()
        {
            var c = new NpcData
            {
                NpcId = NpcId,
                Name = Name,
                HomeSettlementId = HomeSettlementId,
                FactionId = FactionId,
                Role = Role,
                MetPlayerOn = MetPlayerOn,
                ConversationCount = ConversationCount,
                Sex = Sex,
                BirthDate = BirthDate,
                Tier = Tier,
                Personality = Personality?.Clone() ?? new NpcPersonality(),
                Skills = Skills?.Clone() ?? new NpcSkills(),
                Condition = Condition?.Clone() ?? new NpcCondition(),
                ResidenceId = ResidenceId,
                WorkplaceId = WorkplaceId,
                CurrentLocationId = CurrentLocationId,
                Schedule = Schedule?.Clone() ?? new NpcScheduleData(),
                Character = Character?.Clone(),
                Goals = new List<NpcGoalData>(Goals.Count),
                Memories = new List<NpcMemoryData>(Memories.Count),
                Knowledge = new List<NpcKnowledgeData>(Knowledge.Count),
                IsDeceased = IsDeceased,
                DiedOn = DiedOn,
                LastUpdatedOn = LastUpdatedOn,
                PlayerRegard = PlayerRegard?.Clone() ?? new PlayerRegardData(),
            };
            foreach (var g in Goals) c.Goals.Add(g.Clone());
            foreach (var m in Memories) c.Memories.Add(m.Clone());
            foreach (var k in Knowledge) c.Knowledge.Add(k.Clone());
            return c;
        }
    }

    /// <summary>Event kinds. Persisted by name; append new values, never reorder.</summary>
    public enum HistoricalEventType
    {
        WorldFounded = 0,
        FoodShortageBegan = 1,
        FoodShortageEnded = 2,
        FoodReservesExhausted = 3,
        StarvationBegan = 4,
        ResourceShortageBegan = 5,
        ResourceShortageEnded = 6,
        TradeShipmentDelivered = 7,
        TradeShipmentReturned = 8,
        ProsperityChanged = 9,
        StabilityChanged = 10,
        SettlementGrew = 11,
        SettlementDeclined = 12,
        ProductionMilestone = 13,
        EconomicMilestone = 14,
        PlayerMetResident = 15,
        LabourNoticePosted = 16,
        TradeRouteSuspended = 17,
        TradeRouteResumed = 18,

        // Phase 3: politics. Facts in Data; the factions involved in FactionIds.
        FactionFounded = 19,
        FactionDissolved = 20,
        FactionStatusChanged = 21,
        FactionInfluenceChanged = 22,
        GovernanceChanged = 23,
        TerritorialClaimFiled = 24,
        TerritorialClaimContested = 25,
        TerritorialClaimResolved = 26,
        RelationshipChanged = 27,
        AgreementEstablished = 28,
        AgreementBroken = 29,
        FactionGoalCompleted = 30,
        FactionGoalAbandoned = 31,
        AffiliationShifted = 32,
        FactionPresenceEstablished = 33,
        FactionPresenceWithdrawn = 34,
        FactionGoalSet = 35,

        // Phase 4: named people.
        NpcDied = 36,
        NpcGoalSet = 37,
        NpcGoalAchieved = 38,
        NpcRelationshipFormed = 39,
        NpcRelationshipChanged = 40,
        NpcMoved = 41,
        NpcIdentityChanged = 42,
        NpcOccupationChanged = 43,
        NpcScheduleChanged = 44,
        NpcMemoryAdded = 45,
        NpcKnowledgeAdded = 46,
        NpcAffiliationChanged = 47,
        NpcConversation = 48,
        NpcGoalAbandoned = 49,

        // Phase 5: the player's trade with shops (Minor; structured facts: player, item, quantity, total, shop, keeper).
        PlayerPurchase = 50,
        PlayerSale = 51,

        // Phase 6: the player created their character (Minor; facts: player, species, background, class, level).
        PlayerCharacterCreated = 52,

        /// <summary>Phase 7: the player gained one or more levels (facts: player, class, from, to, subclass).</summary>
        PlayerLevelAdvanced = 53,

        /// <summary>Phase 8: an encounter began (facts: name, kind, sides, participants).</summary>
        EncounterStarted = 54,

        /// <summary>Phase 8: an encounter ended (facts: name, outcome, winner, rounds).</summary>
        EncounterEnded = 55,

        /// <summary>Phase 8: the player or a named resident fell unconscious in a fight (facts: name, by).</summary>
        CombatantDowned = 56,

        /// <summary>Phase 8: the player was killed in a fight (residents use NpcDied, with cause "combat").</summary>
        CombatantKilled = 57,

        /// <summary>Phase 10: a named person was given a full character sheet (facts: name, class, level, occupation).</summary>
        NpcPromoted = 58,

        /// <summary>Phase 10: a named person rose a level through the shared advancement rules (facts: name, class, to).</summary>
        NpcLevelAdvanced = 59,

        /// <summary>Phase 10: a world encounter's consequences were applied, once (facts: name, outcome, xp, loot, deaths).</summary>
        EncounterResolved = 60,

        // Phase 12: quests, decisions, reputation, discoveries (facts: quest, instance, giver, settlement, outcome …).
        QuestDiscovered = 61,
        QuestOffered = 62,
        QuestAccepted = 63,
        QuestDeclined = 64,
        QuestAbandoned = 65,
        QuestCompleted = 66,
        QuestFailed = 67,
        DialogueDecision = 68,
        ReputationChanged = 69,
        SettlementProblemResolved = 70,
        PlayerDiscovery = 71,
        QuestItemDelivered = 72,
        QuestConsequence = 73,

        // Phase 13: places and exploration (ADR-0062 … ADR-0066).
        PlaceDiscovered = 74,
        PlaceEntered = 75,
        PlaceCleared = 76,
        TrapSprung = 77,
        CaptiveFreed = 78,
        EscortArrived = 79,
        Trespass = 80,
        PlaceSecretFound = 81,
    }

    /// <summary>Tone of an event (drives colour). Persisted by name.</summary>
    public enum EventSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2,
        Positive = 3,
    }

    /// <summary>How much an event matters to understanding the world. Persisted by name.</summary>
    public enum EventImportance
    {
        Minor = 0,
        Notable = 1,
        Major = 2,
    }

    /// <summary>
    /// Immutable chronicle entry produced by generation or simulation. New events carry
    /// structured facts (<see cref="SettlementIds"/>, <see cref="Data"/>) and no prose; the
    /// application layer formats them. <see cref="Description"/> remains for events from
    /// older saves that only have text.
    /// </summary>
    public sealed class HistoricalEvent
    {
        private static readonly IReadOnlyDictionary<string, string> NoData = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public HistoricalEvent(
            EventId eventId,
            WorldDate date,
            HistoricalEventType eventType,
            EventSeverity severity,
            SettlementId settlementId,
            string description)
            : this(eventId, date, eventType, severity, EventImportance.Notable,
                settlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { settlementId }, null, description)
        {
        }

        public HistoricalEvent(
            EventId eventId,
            WorldDate date,
            HistoricalEventType eventType,
            EventSeverity severity,
            EventImportance importance,
            IReadOnlyList<SettlementId> settlementIds,
            IEnumerable<KeyValuePair<string, string>> data,
            string description = null,
            IReadOnlyList<FactionId> factionIds = null,
            IReadOnlyList<NpcId> npcIds = null)
        {
            NpcIds = npcIds != null ? new List<NpcId>(npcIds) : (IReadOnlyList<NpcId>)Array.Empty<NpcId>();
            FactionIds = factionIds != null ? new List<FactionId>(factionIds) : (IReadOnlyList<FactionId>)Array.Empty<FactionId>();
            EventId = eventId;
            Date = date;
            EventType = eventType;
            Severity = severity;
            Importance = importance;
            SettlementIds = settlementIds != null ? new List<SettlementId>(settlementIds) : new List<SettlementId>();
            if (data != null)
            {
                var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in data) copy[pair.Key] = pair.Value ?? string.Empty;
                Data = copy;
            }
            else
            {
                Data = NoData;
            }

            Description = description ?? string.Empty;
        }

        public EventId EventId { get; }
        public WorldDate Date { get; }
        public HistoricalEventType EventType { get; }
        public EventSeverity Severity { get; }
        public EventImportance Importance { get; }

        /// <summary>Settlements involved, primary first. Empty for world-wide events.</summary>
        public IReadOnlyList<SettlementId> SettlementIds { get; }

        /// <summary>Factions involved (Phase 3), primary first. Empty for events without factions. Dissolved factions stay valid references.</summary>
        public IReadOnlyList<FactionId> FactionIds { get; }

        /// <summary>Named people involved (Phase 4), primary first. Empty for events without people.</summary>
        public IReadOnlyList<NpcId> NpcIds { get; }

        /// <summary>Primary settlement; <see cref="SettlementId.Empty"/> for world-wide events.</summary>
        public SettlementId SettlementId => SettlementIds.Count > 0 ? SettlementIds[0] : SettlementId.Empty;

        /// <summary>Structured facts (ordinal-sorted keys; values are invariant-culture strings).</summary>
        public IReadOnlyDictionary<string, string> Data { get; }

        /// <summary>Legacy prose for events that predate structured data; empty otherwise.</summary>
        public string Description { get; }

        public string Get(string key) => Data.TryGetValue(key, out var v) ? v : null;

        public int GetInt(string key, int fallback = 0) =>
            Data.TryGetValue(key, out var v) && int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }
}
