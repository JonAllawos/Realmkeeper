using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Quests
{
    // Phase 12 (ADR-0056, ADR-0057): quest definitions are templates with roles; a quest instance binds the roles to real
    // world entities (a person, a settlement, a faction, an item) and keeps everything the player did with it. Authored
    // and emergent quests are the same kind of thing; the categories describe them, they are not separate engines.

    public enum QuestCategory
    {
        MainCampaign = 0,
        Side = 1,
        Faction = 2,
        Settlement = 3,
        Personal = 4,
        Exploration = 5,
        Investigation = 6,
        Hunting = 7,
        CraftingGathering = 8,
        DeliveryRetrieval = 9,
        BountyContract = 10,
        Emergent = 11,
    }

    /// <summary>A quest instance's state. Transitions are checked (<see cref="QuestStates.CanMove"/>).</summary>
    public enum QuestState
    {
        /// <summary>Its conditions do not hold (yet); the player does not see it.</summary>
        Unavailable = 0,

        /// <summary>Its conditions hold, but the player has not come across it.</summary>
        Available = 1,

        /// <summary>The player has heard of it (a notice, a rumour) but not been asked.</summary>
        Discovered = 2,

        /// <summary>Someone has asked the player; it waits for an answer.</summary>
        Offered = 3,

        /// <summary>The player said yes (a brief moment: the quest becomes Active in the same command).</summary>
        Accepted = 4,

        Active = 5,
        Completed = 6,
        Failed = 7,
        Abandoned = 8,
    }

    /// <summary>The content of quests and dialogue (ADR-0061): tags a presentation rule can show, soften or hide.</summary>
    [Flags]
    public enum ContentTags
    {
        None = 0,
        Profanity = 1,
        Violence = 2,
        Threats = 4,
        BloodAndInjury = 8,
        MatureThemes = 16,
        AdultRelationships = 32,
        SensitiveSubjects = 64,
    }

    public enum ObjectiveKind
    {
        TalkTo = 0,
        DiscoverLocation = 1,
        VisitLocation = 2,
        Investigate = 3,
        Collect = 4,
        Deliver = 5,
        Acquire = 6,
        Craft = 7,
        DefeatCreature = 8,
        DefeatNamed = 9,
        Protect = 10,
        Escort = 11,
        Rescue = 12,
        DiscoverInfo = 13,
        Persuade = 14,
        FactionApproval = 15,
        CraftingTask = 16,
        ReachSettlement = 17,
        ResolveSettlementProblem = 18,
        CompleteQuest = 19,
        SurviveEncounter = 20,
        Decision = 21,
    }

    /// <summary>
    /// One condition, shared by quests (availability, prerequisites, failure, conditional objectives) and dialogue
    /// (lines and responses). The application's evaluator reads the world; the data only says what to ask.
    /// </summary>
    public sealed class QuestCondition
    {
        public ConditionKind Kind { get; set; }

        /// <summary>What it is about: a role ("giver", "suspect"), an item ID, a decision key, a trait, a skill …</summary>
        public string Key { get; set; }

        /// <summary>An expected value (a state name, a variable's value, an activity).</summary>
        public string Value { get; set; }

        public int Min { get; set; } = int.MinValue;
        public int Max { get; set; } = int.MaxValue;
        public bool Negate { get; set; }

        public static QuestCondition Of(ConditionKind kind, string key = null, string value = null, int min = int.MinValue, int max = int.MaxValue, bool negate = false) =>
            new QuestCondition { Kind = kind, Key = key, Value = value, Min = min, Max = max, Negate = negate };

        public override string ToString() => (Negate ? "not " : string.Empty) + Kind + (Key != null ? ":" + Key : string.Empty) + (Value != null ? "=" + Value : string.Empty);
    }

    public enum ConditionKind
    {
        Always = 0,

        /// <summary>The state of a quest (Key: a definition ID or "this"; Value: a state name).</summary>
        QuestState = 1,

        /// <summary>A decision recorded on this quest (Key: decision key).</summary>
        Decision = 2,

        /// <summary>A dimension of the person's regard for the player (Key: dimension; Min/Max).</summary>
        Regard = 3,

        /// <summary>The player's standing with the person's faction, or a role's faction (Key: role or "npc"; Min/Max).</summary>
        FactionStanding = 4,

        /// <summary>The player carries an item (Key: item ID; Min: quantity).</summary>
        HasItem = 5,

        /// <summary>The hour of day lies within Min..Max (minutes).</summary>
        TimeOfDay = 6,

        /// <summary>The person's current activity (Value: an activity name).</summary>
        NpcActivity = 7,

        /// <summary>A personality trait of the person (Key: trait; Min/Max).</summary>
        NpcTrait = 8,

        /// <summary>The person holds knowledge (Key: topic; Value: subject, or a role prefixed "role:").</summary>
        NpcKnows = 9,

        /// <summary>The person remembers something whose subject contains Value.</summary>
        NpcRemembers = 10,

        /// <summary>The player is proficient in a skill (Key: skill name).</summary>
        PlayerSkill = 11,

        /// <summary>A conversation or quest variable equals Value (Key: variable).</summary>
        Variable = 12,

        /// <summary>The settlement (Key: role or "here") is short of food.</summary>
        SettlementFoodShort = 13,

        /// <summary>An objective of this quest is done (Key: objective ID).</summary>
        ObjectiveDone = 14,

        /// <summary>A role's person is dead (Key: role).</summary>
        NpcDead = 15,

        /// <summary>The player's local reputation in the settlement (Key: role or "here"; Min/Max).</summary>
        SettlementReputation = 16,

        /// <summary>Days since the quest was accepted (Min/Max).</summary>
        DaysSinceAccepted = 17,

        /// <summary>The player's level (Min/Max).</summary>
        PlayerLevel = 18,

        /// <summary>The person's morale (Min/Max).</summary>
        NpcMorale = 19,
    }

    public sealed class ObjectiveDefinition
    {
        public string Id { get; set; }

        /// <summary>What the journal says (tokens: {giver}, {suspect}, {settlement}, {item} …, from the instance's roles).</summary>
        public string Text { get; set; }

        public ObjectiveKind Kind { get; set; }

        /// <summary>
        /// What it counts: "role:{name}" (a bound person, place or settlement), "item:{id}", "creature:{id}",
        /// "object:{key}", "info:{key}", "decision:{key}", "quest:{definition}", or "faction:{role}" with a Min.
        /// </summary>
        public string Target { get; set; }

        /// <summary>Deliveries and talks: to whom (a role).</summary>
        public string To { get; set; }

        public int Quantity { get; set; } = 1;
        public bool Optional { get; set; }

        /// <summary>Not shown until it is revealed (by an earlier stage finishing, an event or a consequence).</summary>
        public bool Hidden { get; set; }

        /// <summary>Stage: a later stage opens when every required group of the earlier stages is done (sequential); one stage holds parallel objectives.</summary>
        public int Stage { get; set; }

        /// <summary>Alternatives: objectives sharing a group are satisfied when any one of them is (OR); empty: its own group.</summary>
        public string Group { get; set; }

        /// <summary>Minutes after acceptance it must be done by (0: no limit); missing it fails the objective.</summary>
        public int TimeLimitMinutes { get; set; }

        /// <summary>Conditions that must hold for it to be offered at all (conditional objectives).</summary>
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>When these hold, it can no longer be done (it becomes unavailable).</summary>
        public IReadOnlyList<QuestCondition> UnavailableWhen { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>A failed required objective fails the quest.</summary>
        public bool FailsQuest { get; set; }

        /// <summary>The ending this objective leads to, when it is done (alternative paths name their outcome).</summary>
        public string Outcome { get; set; }
    }

    public enum RewardKind
    {
        Experience = 0,
        Coin = 1,
        Item = 2,
        Regard = 3,
        FactionStanding = 4,
        SettlementReputation = 5,
        Information = 6,
        Location = 7,
        FollowUpQuest = 8,
        MerchantAccess = 9,
    }

    public sealed class RewardDefinition
    {
        public RewardKind Kind { get; set; }
        public int Amount { get; set; }

        /// <summary>Item ID, regard dimension, information key, follow-up definition, or a role.</summary>
        public string Key { get; set; }

        /// <summary>Whose regard, which faction or settlement (a role).</summary>
        public string Role { get; set; }

        /// <summary>Only for this outcome (null: every successful outcome).</summary>
        public string Outcome { get; set; }

        /// <summary>What the journal says about it before it is given (null: not shown in advance).</summary>
        public string Shown { get; set; }
    }

    public enum ConsequenceKind
    {
        /// <summary>Add (or remove, negative) a settlement resource (Key: resource ID; Role: settlement).</summary>
        SettlementResource = 0,
        SettlementStability = 1,
        SettlementProsperity = 2,

        /// <summary>A faction's influence (Role: whose faction).</summary>
        FactionInfluence = 3,

        /// <summary>A person remembers it (Role; Key: the memory's subject; Amount: salience).</summary>
        NpcMemory = 4,

        /// <summary>A person's regard for the player changes (Role; Key: dimension; Amount).</summary>
        NpcRegard = 5,

        /// <summary>A person learns of the player's deed (Role; Key: deed). It then travels as a rumour.</summary>
        PlayerDeed = 6,

        /// <summary>A Chronicle entry (Key: what happened, as a short subject).</summary>
        ChronicleNote = 7,

        /// <summary>A person's morale (Role; Amount).</summary>
        NpcMorale = 8,

        /// <summary>The player's notoriety (Amount).</summary>
        Notoriety = 9,
    }

    public enum ConsequenceTiming
    {
        /// <summary>When the instance comes into being and is available (the problem it is about happens then).</summary>
        OnAvailable = 4,

        OnAccept = 0,
        OnComplete = 1,
        OnFail = 2,
        OnAbandon = 3,
    }

    public sealed class ConsequenceDefinition
    {
        public ConsequenceKind Kind { get; set; }
        public ConsequenceTiming When { get; set; } = ConsequenceTiming.OnComplete;
        public string Role { get; set; }
        public string Key { get; set; }
        public int Amount { get; set; }

        /// <summary>An amount read from a quest variable instead ("var:missing"), or null.</summary>
        public string Value { get; set; }

        /// <summary>Only for this outcome (null: any).</summary>
        public string Outcome { get; set; }
    }

    public enum RoleKind
    {
        Npc = 0,
        Settlement = 1,
        Faction = 2,
        WorldObject = 3,
        Item = 4,
        Creature = 5,
    }

    /// <summary>
    /// How a role is filled from the world (ADR-0056): a person by their role in a settlement, the settlement itself, a
    /// faction someone belongs to, an object of a place, an item or a creature. The binder is generic: it reads this, never
    /// a quest-specific routine.
    /// </summary>
    public sealed class RoleSpec
    {
        public string Name { get; set; }
        public RoleKind Kind { get; set; }

        /// <summary>People: the roles preferred, in order (Elder, Guard …); empty: anyone.</summary>
        public IReadOnlyList<string> NpcRoles { get; set; } = Array.Empty<string>();

        /// <summary>People: "same" (the giver's settlement), "other" (another settlement, else the same), or "any".</summary>
        public string Where { get; set; } = "same";

        /// <summary>People: must belong to a faction.</summary>
        public bool NeedsFaction { get; set; }

        /// <summary>Factions and settlements: of which role ("suspect" → the suspect's faction; "giver" → the giver's settlement).</summary>
        public string Of { get; set; }

        /// <summary>Objects, items and creatures: the value, with {settlement} filled ("ledger:{settlement}", "sealed_letter", "wolf").</summary>
        public string Value { get; set; }
    }

    public enum FailureKind
    {
        /// <summary>A role's person died (Key: role).</summary>
        NpcDied = 0,

        /// <summary>Days since acceptance passed (Amount).</summary>
        TimeLimit = 1,

        /// <summary>A decision was made (Key).</summary>
        Decision = 2,

        /// <summary>The cause of an emergent quest went away before it was done (the problem resolved itself).</summary>
        CauseEnded = 3,
    }

    public sealed class FailureDefinition
    {
        public FailureKind Kind { get; set; }
        public string Key { get; set; }
        public int Amount { get; set; }

        /// <summary>The reason as the journal says it.</summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// A quest template (ADR-0056). Roles are filled when an instance is made ("giver" always; others as the quest needs:
    /// "settlement", "suspect", "recipient", "faction" …). Text uses {role} tokens, replaced by the bound names.
    /// </summary>
    public sealed class QuestDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>How the giver puts it, the first time (shown in the offer).</summary>
        public string Introduction { get; set; }

        public QuestCategory Category { get; set; }

        /// <summary>Who gives it: the role played by an NPC (an occupation or role word the binder looks for).</summary>
        public string GiverRole { get; set; } = "giver";

        /// <summary>The roles and how each is filled; the first is the giver.</summary>
        public IReadOnlyList<RoleSpec> Roles { get; set; } = Array.Empty<RoleSpec>();

        public IReadOnlyList<QuestCondition> Prerequisites { get; set; } = Array.Empty<QuestCondition>();
        public IReadOnlyList<QuestCondition> Availability { get; set; } = Array.Empty<QuestCondition>();
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; set; } = Array.Empty<ObjectiveDefinition>();
        public IReadOnlyList<FailureDefinition> Failures { get; set; } = Array.Empty<FailureDefinition>();
        public IReadOnlyList<RewardDefinition> Rewards { get; set; } = Array.Empty<RewardDefinition>();
        public IReadOnlyList<ConsequenceDefinition> Consequences { get; set; } = Array.Empty<ConsequenceDefinition>();
        public IReadOnlyList<string> FollowUps { get; set; } = Array.Empty<string>();

        /// <summary>Days an offer or a discovered quest stays open before it lapses (0: no limit).</summary>
        public int ExpiresAfterDays { get; set; }

        /// <summary>The dialogue the giver uses for it (offer, progress and turn-in nodes).</summary>
        public string DialogueId { get; set; }

        /// <summary>May be taken again after it ends (a recurring contract); default no.</summary>
        public bool Repeatable { get; set; }

        public ContentTags Tags { get; set; }
        public string Source { get; set; } = "Realmkeeper original";
        public int Version { get; set; } = 1;
    }

    // ----- Instances ---------------------------------------------------------------------------------------

    public sealed class ObjectiveProgressData
    {
        public string Id { get; set; }
        public int Count { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public bool Revealed { get; set; }

        /// <summary>No longer possible (its unavailability condition held).</summary>
        public bool Unavailable { get; set; }

        public long DoneAt { get; set; } = -1;

        public ObjectiveProgressData Clone() => (ObjectiveProgressData)MemberwiseClone();
    }

    public sealed class QuestHistoryEntry
    {
        /// <summary>Absolute world minute.</summary>
        public long At { get; set; }
        public string Text { get; set; }

        public QuestHistoryEntry Clone() => (QuestHistoryEntry)MemberwiseClone();
    }

    /// <summary>The player's quest (ADR-0056): a definition bound to real entities, with everything that happened to it.</summary>
    public sealed class QuestInstanceData
    {
        public const int MaxHistory = 40;

        /// <summary>Stable and deterministic: the definition and its binding ("quest:granary_shortfall:{settlement}").</summary>
        public string InstanceId { get; set; }

        public string DefinitionId { get; set; }
        public int DefinitionVersion { get; set; }
        public QuestState State { get; set; }

        /// <summary>Role → bound entity ID (as text); "giver" is always present.</summary>
        public Dictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Why it exists: "authored", or an emergent cause ("shortage:food:{settlement}").</summary>
        public string SourceKey { get; set; } = "authored";

        public long CreatedAt { get; set; } = -1;
        public long DiscoveredAt { get; set; } = -1;
        public long OfferedAt { get; set; } = -1;
        public long AcceptedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;

        /// <summary>When an open (not accepted) offer lapses, or -1.</summary>
        public long ExpiresAt { get; set; } = -1;

        public List<ObjectiveProgressData> Objectives { get; set; } = new List<ObjectiveProgressData>();

        /// <summary>Decision keys, in the order made.</summary>
        public List<string> Decisions { get; set; } = new List<string>();

        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The ending reached (null until one is).</summary>
        public string Outcome { get; set; }

        public bool RewardsClaimed { get; set; }

        /// <summary>Keys of consequences already applied (idempotency).</summary>
        public List<string> AppliedConsequences { get; set; } = new List<string>();

        public string FailureReason { get; set; }
        public List<QuestHistoryEntry> History { get; set; } = new List<QuestHistoryEntry>();
        public bool Tracked { get; set; }

        public bool IsOpen => State == QuestState.Active || State == QuestState.Accepted;
        public bool IsClosed => State == QuestState.Completed || State == QuestState.Failed || State == QuestState.Abandoned;

        public string Bound(string role) => role != null && Bindings.TryGetValue(role, out var v) ? v : null;

        public ObjectiveProgressData Progress(string objectiveId)
        {
            foreach (var o in Objectives) if (o.Id == objectiveId) return o;
            return null;
        }

        public void Note(long at, string text)
        {
            History.Add(new QuestHistoryEntry { At = at, Text = text });
            while (History.Count > MaxHistory) History.RemoveAt(0);
        }

        public QuestInstanceData Clone()
        {
            var c = (QuestInstanceData)MemberwiseClone();
            c.Bindings = new Dictionary<string, string>(Bindings, StringComparer.Ordinal);
            c.Objectives = Objectives.ConvertAll(o => o.Clone());
            c.Decisions = new List<string>(Decisions);
            c.Variables = new Dictionary<string, string>(Variables, StringComparer.Ordinal);
            c.AppliedConsequences = new List<string>(AppliedConsequences);
            c.History = History.ConvertAll(h => h.Clone());
            return c;
        }
    }

    /// <summary>The checked state machine (ADR-0056).</summary>
    public static class QuestStates
    {
        public static bool CanMove(QuestState from, QuestState to, bool repeatable = false)
        {
            switch (from)
            {
                case QuestState.Unavailable: return to == QuestState.Available;
                case QuestState.Available: return to == QuestState.Discovered || to == QuestState.Offered || to == QuestState.Unavailable;
                case QuestState.Discovered: return to == QuestState.Offered || to == QuestState.Unavailable;
                case QuestState.Offered: return to == QuestState.Accepted || to == QuestState.Unavailable || to == QuestState.Discovered; // declined: back to known
                case QuestState.Accepted: return to == QuestState.Active;
                case QuestState.Active: return to == QuestState.Completed || to == QuestState.Failed || to == QuestState.Abandoned;
                case QuestState.Completed:
                case QuestState.Failed:
                case QuestState.Abandoned:
                    return repeatable && to == QuestState.Available; // never reactivated silently; only a repeatable contract reopens
                default: return false;
            }
        }

        public static string Name(QuestState s) => s.ToString().ToLowerInvariant();
    }
}
