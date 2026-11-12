using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Quests;

namespace Realmkeeper.Domain.Campaigns
{
    // Phase 17 (ADR-0082): a campaign is authored narrative laid over the one world. Its definition is data (versioned and
    // validated); an instance lives in the world and points at the world's own people, settlements, factions, roads and
    // quest instances by ID. Nothing here copies an entity. Arc objectives are linked quests, recorded decisions or
    // world-state conditions read by the existing evaluator: there is no second objective engine.

    /// <summary>A campaign instance's state. Transitions are checked (<see cref="CampaignStates.CanMove"/>).</summary>
    public enum CampaignState
    {
        /// <summary>Its definition can be started in this world (not stored: an instance exists only once started).</summary>
        Available = 0,
        Active = 1,

        /// <summary>Set aside by the player: nothing advances, nothing is lost. The world goes on.</summary>
        Suspended = 2,
        Completed = 3,
        Failed = 4,
        Abandoned = 5,
    }

    public enum ArcState
    {
        /// <summary>Its prerequisites have not been met.</summary>
        Locked = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,

        /// <summary>The player refused or gave it up.</summary>
        Abandoned = 4,
    }

    public enum ChapterState
    {
        Locked = 0,
        Active = 1,
        Completed = 2,
    }

    /// <summary>How a campaign role is filled from the world (ADR-0082). Every role names something that already exists.</summary>
    public enum CampaignRoleKind
    {
        /// <summary>A person, bound once at activation (they stay that person whatever becomes of them).</summary>
        Npc = 0,
        Settlement = 1,
        Faction = 2,
        Route = 3,
        Region = 4,

        /// <summary>Whoever leads a role's faction at the time of asking (leaders change through the political simulation).</summary>
        FactionLeader = 5,

        /// <summary>The first of several roles whose person can still be met (ADR-0084: another person carries the story on).</summary>
        FirstAvailable = 6,
    }

    /// <summary>What a role is chosen from. Selection is deterministic (world seed, IDs in order).</summary>
    public enum CampaignRoleRule
    {
        /// <summary>The opening town of the home region.</summary>
        HomeTown = 0,

        /// <summary>A road out of the home region into a neighbouring one (preferring one whose far town answers to another faction).</summary>
        HomeCrossing = 1,

        /// <summary>The settlement at the far end of a route role (Of).</summary>
        FarEnd = 2,

        /// <summary>A person living in a settlement role (Of), by preferred trades (NpcRoles), not already cast.</summary>
        PersonIn = 3,

        /// <summary>The faction a settlement role (Of) answers to.</summary>
        FactionOf = 4,

        /// <summary>Someone in a person role's household (Of), if they have one.</summary>
        HouseholdOf = 5,

        /// <summary>The region a settlement role (Of) lies in.</summary>
        RegionOf = 6,
    }

    public sealed class CampaignRoleSpec
    {
        public string Name { get; set; }
        public CampaignRoleKind Kind { get; set; }
        public CampaignRoleRule Rule { get; set; }
        public string Of { get; set; }
        public IReadOnlyList<string> NpcRoles { get; set; } = Array.Empty<string>();

        /// <summary>FirstAvailable: the roles tried in order.</summary>
        public IReadOnlyList<string> Candidates { get; set; } = Array.Empty<string>();

        /// <summary>The campaign can start without it (an optional person: a story that can do without them).</summary>
        public bool Optional { get; set; }

        /// <summary>How the journal names the part they play ("the warden at the crossing").</summary>
        public string Part { get; set; }
    }

    /// <summary>A quest the campaign uses: a quest definition with its roles filled from campaign roles.</summary>
    public sealed class CampaignQuestRef
    {
        public string Key { get; set; }
        public string QuestId { get; set; }

        /// <summary>Quest role → campaign role.</summary>
        public IReadOnlyDictionary<string, string> Roles { get; set; } = new Dictionary<string, string>();
    }

    public enum CampaignEffectKind
    {
        /// <summary>A person's regard for the player (Role; Key: dimension; Amount).</summary>
        Regard = 0,

        /// <summary>The player's standing with a faction (Role: a faction or a person's faction; Amount).</summary>
        FactionStanding = 1,
        FactionInfluence = 2,

        /// <summary>How two factions regard each other (Role and Key: faction roles; Amount: opinion).</summary>
        FactionOpinion = 3,
        SettlementStability = 4,
        SettlementProsperity = 5,

        /// <summary>A settlement's stock (Role; Key: resource; Amount, negative to take).</summary>
        SettlementResource = 6,

        /// <summary>A person remembers it (Role; Text; Amount: salience).</summary>
        NpcMemory = 7,

        /// <summary>Two people's bond (Role, Key: person roles; Value: dimension; Amount).</summary>
        NpcRelation = 8,

        /// <summary>A companion's loyalty (Role, or "party" for every companion present; Amount; Text: the reason).</summary>
        CompanionLoyalty = 9,

        /// <summary>A campaign quest becomes available (Key: its campaign quest key).</summary>
        StartQuest = 10,

        /// <summary>The player learns a fact (Key; Text).</summary>
        Discovery = 11,

        /// <summary>The people of a settlement role learn of it (Role; Text): it then travels as talk does.</summary>
        Spread = 12,

        /// <summary>A world event begins (Key: its type; Role: a route or settlement role; Amount: severity; Text: its cause).</summary>
        RaiseWorldEvent = 13,

        /// <summary>A road's state (Role: route role; Value: a <c>RouteCondition</c> name).</summary>
        RouteCondition = 14,

        /// <summary>A campaign flag (Key; Value).</summary>
        Flag = 15,

        /// <summary>Closes or opens an arc (Key: arc; Value: Completed, Failed, Abandoned or Active; Text: outcome).</summary>
        ArcState = 16,
        Experience = 17,
        Coin = 18,

        /// <summary>A Chronicle entry (Text).</summary>
        Chronicle = 19,

        /// <summary>A person's morale (Role; Amount).</summary>
        NpcMorale = 20,

        /// <summary>The player's deed, known to a person (Role; Text: the deed). It then travels as a rumour.</summary>
        PlayerDeed = 21,
    }

    /// <summary>One typed consequence (ADR-0083). Applied once: its key is the decision, the choice and its place in the list.</summary>
    public sealed class CampaignEffect
    {
        public CampaignEffectKind Kind { get; set; }
        public string Role { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public int Amount { get; set; }
        public string Text { get; set; }

        public static CampaignEffect Of(CampaignEffectKind kind, string role = null, string key = null, int amount = 0, string value = null, string text = null) =>
            new CampaignEffect { Kind = kind, Role = role, Key = key, Amount = amount, Value = value, Text = text };
    }

    /// <summary>A consequence that comes later: after some days and, if given, once its conditions hold (ADR-0083).</summary>
    public sealed class DelayedConsequence
    {
        public string Id { get; set; }
        public int AfterDays { get; set; }

        /// <summary>Further conditions (all must hold when it is due); it waits until they do or the campaign ends.</summary>
        public IReadOnlyList<QuestCondition> When { get; set; } = Array.Empty<QuestCondition>();

        public IReadOnlyList<CampaignEffect> Effects { get; set; } = Array.Empty<CampaignEffect>();

        /// <summary>What the journal says once it happens.</summary>
        public string Text { get; set; }

        /// <summary>What the journal says while it waits (null: nothing; a consequence need not announce itself).</summary>
        public string Hint { get; set; }
    }

    /// <summary>A personality the choice appeals to (or offends): companions with the trait high approve (Sign +1) or disapprove (−1).</summary>
    public sealed class ChoiceLeaning
    {
        public string Trait { get; set; }
        public int Sign { get; set; }

        /// <summary>How a companion who approves puts it (tokens: {companion}, {player}).</summary>
        public string Approve { get; set; }

        public string Disapprove { get; set; }
    }

    public sealed class ChoiceDefinition
    {
        public string Id { get; set; }

        /// <summary>The words of the choice.</summary>
        public string Text { get; set; }

        /// <summary>What the journal records the player as having done.</summary>
        public string Summary { get; set; }

        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>Shown when the conditions do not hold (null: the choice is not shown at all).</summary>
        public string UnavailableText { get; set; }

        public IReadOnlyList<CampaignEffect> Effects { get; set; } = Array.Empty<CampaignEffect>();
        public IReadOnlyList<DelayedConsequence> Delayed { get; set; } = Array.Empty<DelayedConsequence>();
        public IReadOnlyList<ChoiceLeaning> Leanings { get; set; } = Array.Empty<ChoiceLeaning>();

        /// <summary>The ending this choice gives its arc (null: none).</summary>
        public string Outcome { get; set; }
    }

    public sealed class DecisionDefinition
    {
        public string Id { get; set; }
        public string ArcId { get; set; }
        public string Title { get; set; }

        /// <summary>The question as the journal puts it.</summary>
        public string Prompt { get; set; }

        /// <summary>Who puts it to the player (a role); null: the player decides alone, from the journal.</summary>
        public string SpeakerRole { get; set; }

        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();
        public IReadOnlyList<ChoiceDefinition> Choices { get; set; } = Array.Empty<ChoiceDefinition>();

        public ChoiceDefinition Choice(string id)
        {
            foreach (var c in Choices) if (c.Id == id) return c;
            return null;
        }
    }

    public enum ArcObjectiveKind
    {
        /// <summary>A campaign quest reaches Completed (with a given outcome, if named).</summary>
        Quest = 0,

        /// <summary>A decision is made (a given choice, if named).</summary>
        Decision = 1,

        /// <summary>The world is in a state (all conditions hold).</summary>
        State = 2,
    }

    public sealed class ArcObjectiveDefinition
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public ArcObjectiveKind Kind { get; set; }
        public string QuestKey { get; set; }
        public string QuestOutcome { get; set; }
        public string DecisionId { get; set; }
        public string ChoiceId { get; set; }
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>When these hold, it can no longer be done (a person it needs is gone, say).</summary>
        public IReadOnlyList<QuestCondition> UnavailableWhen { get; set; } = Array.Empty<QuestCondition>();
        public bool Optional { get; set; }
        public bool Hidden { get; set; }

        /// <summary>Sequential stages, as for quests: a later stage opens when the earlier ones are done.</summary>
        public int Stage { get; set; }

        /// <summary>Alternatives: objectives sharing a group are satisfied when any one is.</summary>
        public string Group { get; set; }

        /// <summary>The arc's outcome when this is the objective that finished it.</summary>
        public string Outcome { get; set; }
    }

    /// <summary>
    /// Another way on (ADR-0084): when its conditions hold while the arc is under way, a further campaign quest is offered
    /// (a different person who knows, say). Once; the arc's objectives decide whether that way counts.
    /// </summary>
    public sealed class ArcFallback
    {
        public IReadOnlyList<QuestCondition> When { get; set; } = Array.Empty<QuestCondition>();
        public string Quest { get; set; }

        /// <summary>What the journal says when it opens.</summary>
        public string Note { get; set; }
    }

    public sealed class ArcDefinition
    {
        public string Id { get; set; }
        public string ChapterId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public bool Optional { get; set; }

        /// <summary>Not shown in the journal until it begins.</summary>
        public bool Hidden { get; set; }

        /// <summary>Arcs that must be closed (completed or failed) first; any order.</summary>
        public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();

        /// <summary>Further conditions to begin.</summary>
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>Campaign quests made available when it begins.</summary>
        public IReadOnlyList<string> StartQuests { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ArcObjectiveDefinition> Objectives { get; set; } = Array.Empty<ArcObjectiveDefinition>();

        /// <summary>Days after it begins that the world stops waiting (0: none).</summary>
        public int TimeLimitDays { get; set; }

        /// <summary>Why it fails when the time runs out (the world settled it without the player).</summary>
        public string ExpiryText { get; set; }

        /// <summary>When these hold it fails.</summary>
        public IReadOnlyList<QuestCondition> FailWhen { get; set; } = Array.Empty<QuestCondition>();
        public string FailText { get; set; }

        /// <summary>The player may refuse it (optional arcs always may).</summary>
        public bool MayRefuse { get; set; }

        public IReadOnlyList<ArcFallback> Fallbacks { get; set; } = Array.Empty<ArcFallback>();
        public IReadOnlyList<CampaignEffect> OnComplete { get; set; } = Array.Empty<CampaignEffect>();
        public IReadOnlyList<CampaignEffect> OnFail { get; set; } = Array.Empty<CampaignEffect>();
    }

    public sealed class ChapterDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();
    }

    public sealed class EndingDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();

        /// <summary>An ending that is a failure of the campaign (the default ends it Completed).</summary>
        public bool Failure { get; set; }
    }

    /// <summary>A campaign definition (ADR-0082): data, versioned, validated before it may enter a world.</summary>
    public sealed class CampaignDefinition
    {
        public string Id { get; set; }
        public int Version { get; set; } = 1;
        public string Title { get; set; }
        public string Summary { get; set; }

        /// <summary>The situation as the player first meets it.</summary>
        public string Premise { get; set; }
        public string Source { get; set; } = "Realmkeeper original";

        /// <summary>Demonstration content, not setting canon (ADR-0082): the journal says so.</summary>
        public bool Provisional { get; set; }

        /// <summary>Plain words on what the world needs for it (shown when it cannot start).</summary>
        public string Requires { get; set; }

        public IReadOnlyList<CampaignRoleSpec> Roles { get; set; } = Array.Empty<CampaignRoleSpec>();
        public IReadOnlyList<CampaignQuestRef> Quests { get; set; } = Array.Empty<CampaignQuestRef>();
        public IReadOnlyList<ChapterDefinition> Chapters { get; set; } = Array.Empty<ChapterDefinition>();
        public IReadOnlyList<ArcDefinition> Arcs { get; set; } = Array.Empty<ArcDefinition>();
        public IReadOnlyList<DecisionDefinition> Decisions { get; set; } = Array.Empty<DecisionDefinition>();
        public IReadOnlyList<EndingDefinition> Endings { get; set; } = Array.Empty<EndingDefinition>();

        /// <summary>The arc whose closing ends the campaign (its ending then chosen from <see cref="Endings"/>).</summary>
        public string FinalArc { get; set; }

        /// <summary>Further world conditions to begin.</summary>
        public IReadOnlyList<QuestCondition> StartConditions { get; set; } = Array.Empty<QuestCondition>();

        public CampaignRoleSpec Role(string name)
        {
            foreach (var r in Roles) if (r.Name == name) return r;
            return null;
        }

        public ArcDefinition Arc(string id)
        {
            foreach (var a in Arcs) if (a.Id == id) return a;
            return null;
        }

        public ChapterDefinition Chapter(string id)
        {
            foreach (var c in Chapters) if (c.Id == id) return c;
            return null;
        }

        public DecisionDefinition Decision(string id)
        {
            foreach (var d in Decisions) if (d.Id == id) return d;
            return null;
        }

        public CampaignQuestRef Quest(string key)
        {
            foreach (var q in Quests) if (q.Key == key) return q;
            return null;
        }
    }

    // ----- Instances ---------------------------------------------------------------------------------------

    public sealed class ArcObjectiveProgressData
    {
        public string Id { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public bool Revealed { get; set; }
        public long DoneAt { get; set; } = -1;

        public ArcObjectiveProgressData Clone() => (ArcObjectiveProgressData)MemberwiseClone();
    }

    public sealed class ArcProgressData
    {
        public string Id { get; set; }
        public ArcState State { get; set; }
        public long StartedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public List<ArcObjectiveProgressData> Objectives { get; set; } = new List<ArcObjectiveProgressData>();

        public bool IsClosed => State == ArcState.Completed || State == ArcState.Failed || State == ArcState.Abandoned;

        public ArcObjectiveProgressData Objective(string id)
        {
            foreach (var o in Objectives) if (o.Id == id) return o;
            return null;
        }

        public ArcProgressData Clone()
        {
            var c = (ArcProgressData)MemberwiseClone();
            c.Objectives = Objectives.ConvertAll(o => o.Clone());
            return c;
        }
    }

    public sealed class ChapterProgressData
    {
        public string Id { get; set; }
        public ChapterState State { get; set; }
        public long StartedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;

        public ChapterProgressData Clone() => (ChapterProgressData)MemberwiseClone();
    }

    /// <summary>A decision as made (ADR-0083): once per decision, never undone.</summary>
    public sealed class DecisionRecordData
    {
        public string DecisionId { get; set; }
        public string ChoiceId { get; set; }
        public long At { get; set; }

        /// <summary>Who it was put by (a name), or "the journal".</summary>
        public string Source { get; set; }

        public DecisionRecordData Clone() => (DecisionRecordData)MemberwiseClone();
    }

    /// <summary>A delayed consequence waiting for its time (ADR-0083). Fired once; kept as history afterwards.</summary>
    public sealed class PendingConsequenceData
    {
        /// <summary>"{decision}:{choice}:{delayed}" — unique within the instance.</summary>
        public string Key { get; set; }
        public string DecisionId { get; set; }
        public string ChoiceId { get; set; }
        public string DelayedId { get; set; }
        public long DueAt { get; set; }
        public bool Fired { get; set; }
        public long FiredAt { get; set; } = -1;

        /// <summary>Cancelled: the campaign ended before it came due.</summary>
        public bool Lapsed { get; set; }

        public PendingConsequenceData Clone() => (PendingConsequenceData)MemberwiseClone();
    }

    public sealed class CampaignHistoryEntry
    {
        public long At { get; set; }
        public string Text { get; set; }

        public CampaignHistoryEntry Clone() => (CampaignHistoryEntry)MemberwiseClone();
    }

    /// <summary>One campaign in one world (ADR-0082): its roles point at real entities; its quests are real quest instances.</summary>
    public sealed class CampaignInstanceData
    {
        public const int MaxHistory = 80;

        /// <summary>"campaign:{campaignId}" — one instance per campaign per world.</summary>
        public string InstanceId { get; set; }
        public string CampaignId { get; set; }
        public int Version { get; set; }
        public string WorldId { get; set; }
        public CampaignState State { get; set; }

        /// <summary>Role → entity ID (as text).</summary>
        public Dictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public long StartedAt { get; set; }
        public long ClosedAt { get; set; } = -1;
        public long SuspendedAt { get; set; } = -1;
        public string Ending { get; set; }

        public List<ChapterProgressData> Chapters { get; set; } = new List<ChapterProgressData>();
        public List<ArcProgressData> Arcs { get; set; } = new List<ArcProgressData>();
        public List<DecisionRecordData> Decisions { get; set; } = new List<DecisionRecordData>();
        public List<PendingConsequenceData> Pending { get; set; } = new List<PendingConsequenceData>();

        /// <summary>Keys of effects already applied (idempotency).</summary>
        public List<string> Applied { get; set; } = new List<string>();

        public Dictionary<string, string> Flags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Campaign quest key → quest instance ID.</summary>
        public Dictionary<string, string> Quests { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<CampaignHistoryEntry> History { get; set; } = new List<CampaignHistoryEntry>();

        public bool IsOpen => State == CampaignState.Active || State == CampaignState.Suspended;
        public bool IsClosed => State == CampaignState.Completed || State == CampaignState.Failed || State == CampaignState.Abandoned;

        public string Bound(string role) => role != null && Bindings.TryGetValue(role, out var v) ? v : null;

        public ArcProgressData Arc(string id)
        {
            foreach (var a in Arcs) if (a.Id == id) return a;
            return null;
        }

        public ChapterProgressData Chapter(string id)
        {
            foreach (var c in Chapters) if (c.Id == id) return c;
            return null;
        }

        public DecisionRecordData Decision(string id)
        {
            foreach (var d in Decisions) if (d.DecisionId == id) return d;
            return null;
        }

        public bool Done(string key) => Applied.Contains(key);

        public void Note(long at, string text)
        {
            History.Add(new CampaignHistoryEntry { At = at, Text = text });
            while (History.Count > MaxHistory) History.RemoveAt(0);
        }

        public CampaignInstanceData Clone()
        {
            var c = (CampaignInstanceData)MemberwiseClone();
            c.Bindings = new Dictionary<string, string>(Bindings, StringComparer.Ordinal);
            c.Chapters = Chapters.ConvertAll(x => x.Clone());
            c.Arcs = Arcs.ConvertAll(x => x.Clone());
            c.Decisions = Decisions.ConvertAll(x => x.Clone());
            c.Pending = Pending.ConvertAll(x => x.Clone());
            c.Applied = new List<string>(Applied);
            c.Flags = new Dictionary<string, string>(Flags, StringComparer.Ordinal);
            c.Quests = new Dictionary<string, string>(Quests, StringComparer.Ordinal);
            c.History = History.ConvertAll(x => x.Clone());
            return c;
        }
    }

    public static class CampaignStates
    {
        public static bool CanMove(CampaignState from, CampaignState to)
        {
            switch (from)
            {
                case CampaignState.Available: return to == CampaignState.Active;
                case CampaignState.Active: return to == CampaignState.Suspended || to == CampaignState.Completed || to == CampaignState.Failed || to == CampaignState.Abandoned;
                case CampaignState.Suspended: return to == CampaignState.Active || to == CampaignState.Abandoned;
                default: return false; // closed is final: an old objective re-read never reopens a finished campaign
            }
        }

        public static bool CanMove(ArcState from, ArcState to)
        {
            switch (from)
            {
                case ArcState.Locked: return to == ArcState.Active || to == ArcState.Failed || to == ArcState.Abandoned;
                case ArcState.Active: return to == ArcState.Completed || to == ArcState.Failed || to == ArcState.Abandoned;
                default: return false;
            }
        }
    }

    /// <summary>
    /// What has become of a person, read from the systems that decide it (ADR-0084): death, captivity, absence, hostility,
    /// a companion's leaving. A campaign never stores its own copy and never brings anyone back.
    /// </summary>
    public enum CharacterStatus
    {
        Available = 0,

        /// <summary>Alive, but away from where they are found (on the road with the player, posted elsewhere, held in a place).</summary>
        Unavailable = 1,

        /// <summary>Their whereabouts are unknown to the world (no home, no place).</summary>
        Missing = 2,
        Captured = 3,
        Injured = 4,
        Dead = 5,

        /// <summary>A companion who left the player over a grievance.</summary>
        Departed = 6,
        Hostile = 7,

        /// <summary>Travelling with the player.</summary>
        Allied = 8,
    }
}
