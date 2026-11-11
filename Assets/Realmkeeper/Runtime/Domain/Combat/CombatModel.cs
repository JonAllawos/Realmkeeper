using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Combat
{
    // Phase 8: the persistent state of encounters. Pure data, saved with the world (schema v10); the rules that
    // change it live in CombatEngine and the commands in the Application layer.

    public enum EncounterStatus
    {
        Active = 0,
        Ended = 1,
    }

    public enum EncounterKind
    {
        /// <summary>A labelled practice fight started from the settlement HUD (no encounter generator exists yet).</summary>
        Practice = 0,
        Ambush = 1,
        Brawl = 2,
        Skirmish = 3,
        Other = 4,
    }

    /// <summary>Whether reaching 0 hit points can kill (SRD knock-out rule: non-lethal fights only knock out).</summary>
    public enum Lethality
    {
        Lethal = 0,
        NonLethal = 1,
    }

    public enum EncounterOutcome
    {
        Undecided = 0,

        /// <summary>One side is the only one still able to fight.</summary>
        Victory = 1,

        /// <summary>Nobody on any side is able to fight.</summary>
        Stalemate = 2,

        /// <summary>The player's side withdrew.</summary>
        Withdrawn = 3,

        /// <summary>Ended by a command (scripted or administrative).</summary>
        Ended = 4,
    }

    public enum CombatantKind
    {
        /// <summary>The player character (stats and hit points from the character sheet).</summary>
        Player = 0,

        /// <summary>A persistent named resident (identity from the world's NPC; combat statistics from a creature profile).</summary>
        Npc = 1,

        /// <summary>An encounter-only creature from the creature catalog.</summary>
        Creature = 2,
    }

    public enum CombatControl
    {
        Player = 0,
        Ai = 1,
    }

    /// <summary>Alive and conscious; unconscious and dying; unconscious but stable; dead; gone from the fight.</summary>
    public enum VitalState
    {
        Conscious = 0,
        Dying = 1,
        Stable = 2,
        Dead = 3,
        Fled = 4,
    }

    /// <summary>A position on the tactical grid: 5-ft squares, elevation in squares (0 = ground).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public const int Feet = 5;

        public GridPos(int x, int y, int z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        /// <summary>
        /// Distance in feet. Convention (SRD 5.2.1 grid option, documented in Combat.md): every square moved, diagonals
        /// included, is 5 ft (Chebyshev distance); elevation counts the same way.
        /// </summary>
        public int DistanceFeet(GridPos o) => Math.Max(Math.Abs(X - o.X), Math.Max(Math.Abs(Y - o.Y), Math.Abs(Z - o.Z))) * Feet;

        public bool IsAdjacent(GridPos o) => !Equals(o) && DistanceFeet(o) <= Feet;

        public GridPos Step(int dx, int dy) => new GridPos(X + Math.Sign(dx), Y + Math.Sign(dy), Z);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPos p && Equals(p);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
        public override string ToString() => $"({X},{Y}" + (Z != 0 ? $",{Z}" : string.Empty) + ")";
    }

    public enum ConditionType
    {
        Blinded = 0,
        Charmed = 1,
        Deafened = 2,
        Exhaustion = 3,
        Frightened = 4,
        Grappled = 5,
        Incapacitated = 6,
        Invisible = 7,
        Paralyzed = 8,
        Petrified = 9,
        Poisoned = 10,
        Prone = 11,
        Restrained = 12,
        Stunned = 13,
        Unconscious = 14,
    }

    /// <summary>When a condition ends on its own.</summary>
    public enum ConditionDuration
    {
        /// <summary>Until something removes it (a command, an escape, healing, standing up).</summary>
        UntilRemoved = 0,

        /// <summary>At the start of a given combatant's turn in a given round or later.</summary>
        StartOfTurn = 1,

        /// <summary>At the end of a given combatant's turn in a given round or later.</summary>
        EndOfTurn = 2,
    }

    /// <summary>One condition on a combatant: what, from whom, why, until when.</summary>
    public sealed class ConditionInstance
    {
        public ConditionType Type { get; set; }

        /// <summary>The combatant that caused it (empty for none or the environment).</summary>
        public CombatantId SourceId { get; set; }

        public string Reason { get; set; } = string.Empty;
        public int AppliedRound { get; set; }
        public ConditionDuration Duration { get; set; }

        /// <summary>For timed conditions: whose turn ends it, and from which round.</summary>
        public CombatantId ExpiresOn { get; set; }
        public int ExpiresRound { get; set; }

        /// <summary>Exhaustion level (1–6); 0 otherwise.</summary>
        public int Level { get; set; }

        /// <summary>For Grappled: the DC to escape.</summary>
        public int EscapeDc { get; set; }

        public ConditionInstance Clone() => (ConditionInstance)MemberwiseClone();
    }

    /// <summary>What a combatant has used this turn (action economy). The reaction resets at the start of its own turn.</summary>
    public sealed class TurnResources
    {
        public bool ActionUsed { get; set; }
        public bool BonusActionUsed { get; set; }
        public bool ReactionUsed { get; set; }
        public bool InteractionUsed { get; set; }

        /// <summary>Feet of movement spent this turn.</summary>
        public int MovementUsed { get; set; }

        /// <summary>Extra feet gained this turn (Dash).</summary>
        public int MovementGranted { get; set; }

        /// <summary>The Attack action was taken this turn with a Light weapon (enables the off-hand bonus attack).</summary>
        public string LightAttackWeapon { get; set; }

        /// <summary>Attacks left in the current Attack action (Extra Attack gives more than one).</summary>
        public int AttacksLeft { get; set; }

        public bool Disengaged { get; set; }

        /// <summary>Dodging until the start of its next turn.</summary>
        public bool Dodging { get; set; }

        public TurnResources Clone() => (TurnResources)MemberwiseClone();
    }

    /// <summary>
    /// One participant. Identity is stable: the player's <see cref="SourceId"/> is their character ID, a resident's is
    /// their NPC ID (never a copy of the NPC), a creature's is its catalog ID. The player's hit points, temporary hit
    /// points and death saves stay on the character sheet (one source of truth); everyone else's live here.
    /// </summary>
    public sealed class CombatantData
    {
        public CombatantId CombatantId { get; set; }
        public CombatantKind Kind { get; set; }
        public string SourceId { get; set; }
        public string Name { get; set; }

        /// <summary>The side it fights for ("player", "bandits" …). Same side = allies; "neutral" is hostile to nobody.</summary>
        public string Side { get; set; }

        public FactionId FactionId { get; set; }
        public CombatControl Control { get; set; }

        /// <summary>The creature profile giving its statistics (NPCs and creatures).</summary>
        public string CreatureId { get; set; }

        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        public int TemporaryHp { get; set; }
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures { get; set; }
        public VitalState State { get; set; }

        /// <summary>True for characters who make death saves at 0 HP (the player, named residents); creatures simply die.</summary>
        public bool UsesDeathSaves { get; set; }

        public GridPos Position { get; set; }
        public List<ConditionInstance> Conditions { get; set; } = new List<ConditionInstance>();
        public TurnResources Turn { get; set; } = new TurnResources();

        public int InitiativeRoll { get; set; }
        public int Initiative { get; set; }

        /// <summary>A seeded value used only to break initiative ties deterministically.</summary>
        public int TieBreak { get; set; }

        /// <summary>The round it joined (reinforcements join mid-fight).</summary>
        public int JoinedRound { get; set; }

        public CombatantData Clone()
        {
            var c = (CombatantData)MemberwiseClone();
            c.Conditions = new List<ConditionInstance>();
            foreach (var x in Conditions) c.Conditions.Add(x.Clone());
            c.Turn = Turn.Clone();
            return c;
        }
    }

    public enum CombatLogKind
    {
        Started = 0,
        Joined = 1,
        Initiative = 2,
        TurnStarted = 3,
        Moved = 4,
        Attack = 5,
        Damage = 6,
        Healing = 7,
        Save = 8,
        ConditionApplied = 9,
        ConditionRemoved = 10,
        Downed = 11,
        DeathSave = 12,
        Stabilized = 13,
        Died = 14,
        Action = 15,
        Ended = 16,
    }

    /// <summary>One line of the encounter's log: structured enough to explain and replay, readable as text.</summary>
    public sealed class CombatLogEntry
    {
        public int Sequence { get; set; }
        public int Round { get; set; }
        public CombatLogKind Kind { get; set; }
        public CombatantId ActorId { get; set; }
        public CombatantId TargetId { get; set; }
        public string Text { get; set; } = string.Empty;

        /// <summary>The dice rolled for this entry, in order.</summary>
        public List<int> Rolls { get; set; } = new List<int>();

        /// <summary>The headline number (attack total, damage, healing, save total …).</summary>
        public int Value { get; set; }

        public CombatLogEntry Clone()
        {
            var c = (CombatLogEntry)MemberwiseClone();
            c.Rolls = new List<int>(Rolls);
            return c;
        }
    }

    /// <summary>
    /// An encounter: who, where, the initiative order and whose turn it is, the round, the log, and how it ended. All
    /// dice come from <see cref="Seed"/> and <see cref="RollCount"/>, so the same inputs replay the same fight, before
    /// and after a save. <see cref="ActionSequence"/> counts processed actions; a command naming a stale sequence is
    /// refused, so the same action cannot be processed twice.
    /// </summary>
    public sealed class EncounterData
    {
        public const int MaxLogEntries = 500;

        public EncounterId EncounterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public EncounterKind Kind { get; set; }
        public Lethality Lethality { get; set; }
        public SettlementId SettlementId { get; set; }
        public EncounterStatus Status { get; set; }
        public EncounterOutcome Outcome { get; set; }
        public string WinningSide { get; set; }
        public int Round { get; set; }

        /// <summary>Index into <see cref="Order"/> of the active combatant.</summary>
        public int TurnIndex { get; set; }

        public List<CombatantId> Order { get; set; } = new List<CombatantId>();
        public List<CombatantData> Combatants { get; set; } = new List<CombatantData>();

        /// <summary>Squares that cost double movement.</summary>
        public List<GridPos> DifficultTerrain { get; set; } = new List<GridPos>();

        public WorldDate StartedOn { get; set; }
        public int StartedAtMinute { get; set; }
        public WorldDate EndedOn { get; set; }
        public ulong Seed { get; set; }
        public int RollCount { get; set; }
        public int ActionSequence { get; set; }
        public List<CombatLogEntry> Log { get; set; } = new List<CombatLogEntry>();

        /// <summary>Optional links for future quest and world-event integration.</summary>
        public string QuestRef { get; set; }
        public string WorldEventRef { get; set; }

        public bool IsActive => Status == EncounterStatus.Active;

        public CombatantData Find(CombatantId id)
        {
            foreach (var c in Combatants) if (c.CombatantId == id) return c;
            return null;
        }

        public CombatantData Active => IsActive && TurnIndex >= 0 && TurnIndex < Order.Count ? Find(Order[TurnIndex]) : null;

        public EncounterData Clone()
        {
            var e = (EncounterData)MemberwiseClone();
            e.Order = new List<CombatantId>(Order);
            e.Combatants = new List<CombatantData>();
            foreach (var c in Combatants) e.Combatants.Add(c.Clone());
            e.DifficultTerrain = new List<GridPos>(DifficultTerrain);
            e.Log = new List<CombatLogEntry>();
            foreach (var l in Log) e.Log.Add(l.Clone());
            return e;
        }
    }
}
