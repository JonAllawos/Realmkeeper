using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 4: the NPC Intelligence Engine's data. All of it extends NpcData (one record per
    // named person) or WorldData (relationships, locations); there is no second NPC store.
    // Enums are persisted by name: append new values, never reorder or rename.

    public enum NpcSex
    {
        Unspecified = 0,
        Male = 1,
        Female = 2,
    }

    /// <summary>
    /// How closely a named person is simulated. Tier 3 is not an entity: it is the rest of the
    /// settlement's population (population minus its living named residents), simulated in aggregate.
    /// </summary>
    public enum NpcTier
    {
        /// <summary>Not yet initialised (a pre-Phase-4 record before migration).</summary>
        Unassigned = 0,

        /// <summary>Notable people (faction leaders, elders, key role holders): updated every day.</summary>
        Notable = 1,

        /// <summary>Other named residents: updated once a week, on a day fixed by their ID.</summary>
        Resident = 2,
    }

    public enum NpcTrait
    {
        Sociability = 0,
        Courage = 1,
        Ambition = 2,
        Compassion = 3,
        Discipline = 4,
        Curiosity = 5,
        Trustfulness = 6,
        Greed = 7,
    }

    public enum NpcSkill
    {
        Farming = 0,
        Crafting = 1,
        Trading = 2,
        Combat = 3,
        Leadership = 4,
        Lore = 5,
        Hospitality = 6,
        Labour = 7,
    }

    /// <summary>Who caused a change: the simulation, the player, the DM, or the v6 migration.</summary>
    public enum ChangeSource
    {
        Simulation = 0,
        Player = 1,
        DungeonMaster = 2,
        Migration = 3,
    }

    /// <summary>Eight bounded traits (0–100, 50 is average). Fixed at creation; changed only by the DM.</summary>
    public sealed class NpcPersonality
    {
        public const int Count = 8;
        private readonly int[] _values = new int[Count];

        public NpcPersonality()
        {
            for (var i = 0; i < Count; i++) _values[i] = 50;
        }

        public int this[NpcTrait trait]
        {
            get => _values[(int)trait];
            set => _values[(int)trait] = NpcBounds.Clamp(value, 0, 100);
        }

        public NpcPersonality Clone()
        {
            var c = new NpcPersonality();
            Array.Copy(_values, c._values, Count);
            return c;
        }
    }

    /// <summary>Eight bounded skills (0–100).</summary>
    public sealed class NpcSkills
    {
        public const int Count = 8;
        private readonly int[] _values = new int[Count];

        public int this[NpcSkill skill]
        {
            get => _values[(int)skill];
            set => _values[(int)skill] = NpcBounds.Clamp(value, 0, 100);
        }

        public NpcSkills Clone()
        {
            var c = new NpcSkills();
            Array.Copy(_values, c._values, Count);
            return c;
        }
    }

    /// <summary>Needs and condition: 0–100 each (100 is best), plus personal savings in coin.</summary>
    public sealed class NpcCondition
    {
        private int _health = 100, _satiation = 80, _energy = 80, _morale = 60, _safety = 70;
        private int _wealth;

        public int Health { get => _health; set => _health = NpcBounds.Clamp(value, 0, 100); }
        public int Satiation { get => _satiation; set => _satiation = NpcBounds.Clamp(value, 0, 100); }
        public int Energy { get => _energy; set => _energy = NpcBounds.Clamp(value, 0, 100); }
        public int Morale { get => _morale; set => _morale = NpcBounds.Clamp(value, 0, 100); }
        public int Safety { get => _safety; set => _safety = NpcBounds.Clamp(value, 0, 100); }

        /// <summary>Personal savings in coin (never negative).</summary>
        public int Wealth { get => _wealth; set => _wealth = Math.Max(0, value); }

        public NpcCondition Clone() => new NpcCondition
        {
            _health = _health, _satiation = _satiation, _energy = _energy, _morale = _morale, _safety = _safety, _wealth = _wealth,
        };
    }

    public enum NpcGoalType
    {
        EarnLiving = 0,
        SecureFood = 1,
        Recover = 2,
        GainStanding = 3,
        Socialise = 4,
        ServeFaction = 5,
        AcquireWealth = 6,
        LearnSkill = 7,
        SeekSafety = 8,
    }

    public enum NpcGoalStatus
    {
        Active = 0,
        Achieved = 1,
        Abandoned = 2,
    }

    /// <summary>A motivation turned into a goal: why the person does what they do.</summary>
    public sealed class NpcGoalData
    {
        public GoalId GoalId { get; set; }
        public NpcGoalType Type { get; set; }

        /// <summary>0–100: how much it matters now (the highest active goal drives the day).</summary>
        public int Priority { get; set; }

        /// <summary>0–100.</summary>
        public int Progress { get; set; }

        public NpcGoalStatus Status { get; set; }
        public WorldDate CreatedOn { get; set; }
        public WorldDate ClosedOn { get; set; }
        public ChangeSource Source { get; set; }

        /// <summary>Optional target (a skill for LearnSkill, a person for Socialise).</summary>
        public NpcSkill? TargetSkill { get; set; }
        public NpcId TargetNpcId { get; set; }

        public bool IsActive => Status == NpcGoalStatus.Active;

        public NpcGoalData Clone() => (NpcGoalData)MemberwiseClone();
    }

    public enum NpcRelationKind
    {
        Acquaintance = 0,
        Household = 1,
        Colleague = 2,
        Friend = 3,
        Rival = 4,
        Superior = 5,
    }

    /// <summary>Why a relationship exists or last changed. Relationships are only ever created by one of these.</summary>
    public enum NpcRelationCause
    {
        SharedHousehold = 0,
        SharedWorkplace = 1,
        Conversation = 2,
        SharedHardship = 3,
        FactionHierarchy = 4,
        ManualIntervention = 5,
        Quarrel = 6,
        Kindness = 7,
    }

    /// <summary>
    /// How two named people regard each other. One record per related pair (IDs ordered), never
    /// all pairs: a record exists only because something connected them (see <see cref="Cause"/>).
    /// Values are mutual in this phase.
    /// </summary>
    public sealed class NpcRelationData
    {
        public NpcId NpcA { get; set; }
        public NpcId NpcB { get; set; }
        public NpcRelationKind Kind { get; set; }

        private int _affinity, _trust = 40, _respect = 40, _fear, _rivalry;

        /// <summary>−100 (hatred) … 100 (devotion).</summary>
        public int Affinity { get => _affinity; set => _affinity = NpcBounds.Clamp(value, -100, 100); }
        public int Trust { get => _trust; set => _trust = NpcBounds.Clamp(value, 0, 100); }
        public int Respect { get => _respect; set => _respect = NpcBounds.Clamp(value, 0, 100); }
        public int Fear { get => _fear; set => _fear = NpcBounds.Clamp(value, 0, 100); }
        public int Rivalry { get => _rivalry; set => _rivalry = NpcBounds.Clamp(value, 0, 100); }

        /// <summary>What created the tie, and what changed it last.</summary>
        public NpcRelationCause Origin { get; set; }
        public NpcRelationCause LastCause { get; set; }
        public WorldDate Since { get; set; }
        public WorldDate LastChanged { get; set; }
        public int Interactions { get; set; }

        public bool Involves(NpcId id) => NpcA == id || NpcB == id;

        public NpcId Other(NpcId id) => NpcA == id ? NpcB : NpcA;

        public NpcRelationData Clone() => (NpcRelationData)MemberwiseClone();
    }

    public enum MemoryCategory
    {
        Conversation = 0,
        Hardship = 1,
        Prosperity = 2,
        Politics = 3,
        Social = 4,
        Work = 5,
        Personal = 6,
        Death = 7,
    }

    /// <summary>How the person came to remember it.</summary>
    public enum MemorySource
    {
        /// <summary>Saw it happen.</summary>
        Witnessed = 0,

        /// <summary>It happened to them.</summary>
        Experienced = 1,

        /// <summary>Heard about it from someone.</summary>
        Learned = 2,
    }

    /// <summary>
    /// One remembered thing, linked to the chronicle event it came from when there is one.
    /// Bounded per person; salience decays weekly and the least salient memories are forgotten.
    /// </summary>
    public sealed class NpcMemoryData
    {
        public MemoryId MemoryId { get; set; }

        /// <summary>The chronicle event, or empty for private memories (a conversation).</summary>
        public EventId EventId { get; set; }

        public WorldDate Date { get; set; }
        public MemoryCategory Category { get; set; }

        /// <summary>Short structured key of what happened (an event type name or a conversation topic).</summary>
        public string Subject { get; set; }

        public SettlementId SettlementId { get; set; }
        public FactionId FactionId { get; set; }
        public List<NpcId> NpcIds { get; set; } = new List<NpcId>();

        private int _salience, _confidence = 100;

        /// <summary>0–100: how much it still matters.</summary>
        public int Salience { get => _salience; set => _salience = NpcBounds.Clamp(value, 0, 100); }

        /// <summary>0–100: how sure they are it happened as remembered.</summary>
        public int Confidence { get => _confidence; set => _confidence = NpcBounds.Clamp(value, 0, 100); }

        public MemorySource Source { get; set; }

        public NpcMemoryData Clone()
        {
            var c = (NpcMemoryData)MemberwiseClone();
            c.NpcIds = new List<NpcId>(NpcIds);
            return c;
        }
    }

    public enum KnowledgeTopic
    {
        /// <summary>Whether a settlement has enough food (value: "plenty", "short", "starving").</summary>
        SettlementFood = 0,

        /// <summary>How a settlement is doing (value: "thriving", "steady", "struggling").</summary>
        SettlementProsperity = 1,

        /// <summary>Who governs a settlement (value: faction ID).</summary>
        SettlementGovernor = 2,

        /// <summary>Who leads a faction (value: NPC ID).</summary>
        FactionLeader = 3,

        /// <summary>Where a person can be found (value: location ID).</summary>
        Whereabouts = 4,

        /// <summary>A notable recent event (value: event type name; subject: event ID).</summary>
        RecentEvent = 5,

        /// <summary>Phase 12: something the player did (subject: a deed key; value: the deed, "helped:{settlement}", "threatened:{npc}").</summary>
        PlayerDeed = 6,
    }

    /// <summary>
    /// The separation the engine keeps: the world's truth is WorldData; what a person holds is
    /// Known (observed, current), Believed (observed but possibly out of date), or Rumor (heard
    /// second-hand, unverified).
    /// </summary>
    public enum KnowledgeStatus
    {
        Known = 0,
        Believed = 1,
        Rumor = 2,
    }

    /// <summary>One thing a person holds to be true, with where it came from and how sure they are.</summary>
    public sealed class NpcKnowledgeData
    {
        public KnowledgeId KnowledgeId { get; set; }
        public KnowledgeTopic Topic { get; set; }

        /// <summary>What it is about (a settlement, faction, person or event ID, as text).</summary>
        public string SubjectId { get; set; }

        /// <summary>What they hold (as of <see cref="LearnedOn"/>); may differ from the truth.</summary>
        public string Value { get; set; }

        public KnowledgeStatus Status { get; set; }

        private int _confidence = 100;

        /// <summary>0–100.</summary>
        public int Confidence { get => _confidence; set => _confidence = NpcBounds.Clamp(value, 0, 100); }

        public WorldDate LearnedOn { get; set; }

        /// <summary>Who told them (empty: they observed it).</summary>
        public NpcId SourceNpcId { get; set; }

        /// <summary>How many tellings away from an observation (0: observed).</summary>
        public int Hops { get; set; }

        public EventId EventId { get; set; }

        public NpcKnowledgeData Clone() => (NpcKnowledgeData)MemberwiseClone();
    }

    public enum LocationKind
    {
        Residence = 0,
        Shop = 1,
        Workshop = 2,
        Tavern = 3,
        Market = 4,
        Administration = 5,
        Worship = 6,
        GuardPost = 7,
        Square = 8,
    }

    /// <summary>
    /// A persistent place in a settlement where people live, work or gather. It refers to a
    /// building or area of the settlement's local environment by its stable layout key
    /// (e.g. "hall", "house-3", "plaza"), so exploration shows people where the world says they are.
    /// </summary>
    public sealed class LocationData
    {
        public LocationId LocationId { get; set; }
        public SettlementId SettlementId { get; set; }
        public LocationKind Kind { get; set; }
        public string Name { get; set; }
        public string LayoutKey { get; set; }

        public LocationData Clone() => (LocationData)MemberwiseClone();
    }

    /// <summary>Where a person spends each day of the week (index = day number mod 7).</summary>
    public sealed class NpcScheduleData
    {
        public const int Days = WorldDate.DaysPerWeek;
        private readonly LocationId[] _days = new LocationId[Days];

        public LocationId this[int weekday]
        {
            get => _days[((weekday % Days) + Days) % Days];
            set => _days[((weekday % Days) + Days) % Days] = value;
        }

        public static int WeekdayOf(WorldDate date) => (int)(date.ToDayNumber() % Days);

        public LocationId For(WorldDate date) => this[WeekdayOf(date)];

        public NpcScheduleData Clone()
        {
            var c = new NpcScheduleData();
            Array.Copy(_days, c._days, Days);
            return c;
        }
    }

    public static class NpcBounds
    {
        public const int MaxMemories = 24;
        public const int MaxKnowledge = 40;
        public const int MaxActiveGoals = 4;
        public const int MaxRelationsPerNpc = 16;

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
