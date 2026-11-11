using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    // Phase 8: what a combatant can do on its turn (the action economy). The engine validates every action before
    // it changes anything; the AI and the player's UI both propose these, and the engine alone resolves them.

    public abstract class CombatAction
    {
        public abstract string Describe { get; }
    }

    /// <summary>Move square by square (each step to an adjacent square); the last square is the destination.</summary>
    public sealed class MoveAction : CombatAction
    {
        public MoveAction(params GridPos[] path) { Path = path; }
        public IReadOnlyList<GridPos> Path { get; }
        public override string Describe => "Move";
    }

    /// <summary>The Attack action (or the next attack of it, with Extra Attack): weapon, unarmed strike, grapple or shove.</summary>
    public sealed class AttackAction : CombatAction
    {
        public AttackAction(CombatantId target, string optionId)
        {
            Target = target;
            OptionId = optionId;
        }

        public CombatantId Target { get; }
        public string OptionId { get; }
        public override string Describe => "Attack";
    }

    public sealed class DashAction : CombatAction { public override string Describe => "Dash"; }
    public sealed class DisengageAction : CombatAction { public override string Describe => "Disengage"; }
    public sealed class DodgeAction : CombatAction { public override string Describe => "Dodge"; }

    /// <summary>Stand up from Prone (costs half the speed).</summary>
    public sealed class StandAction : CombatAction { public override string Describe => "Stand up"; }

    /// <summary>Try to escape a grapple (Strength (Athletics) or Dexterity (Acrobatics) against the escape DC).</summary>
    public sealed class EscapeAction : CombatAction { public override string Describe => "Escape grapple"; }

    /// <summary>The Help action used on a dying creature: Wisdom (Medicine) DC 10 to stabilize it.</summary>
    public sealed class StabilizeAction : CombatAction
    {
        public StabilizeAction(CombatantId target) { Target = target; }
        public CombatantId Target { get; }
        public override string Describe => "Stabilize";
    }

    /// <summary>A bonus action a feature grants ("second_wind", "cunning_dash", "cunning_disengage").</summary>
    public sealed class BonusAbilityAction : CombatAction
    {
        public BonusAbilityAction(string abilityId) { AbilityId = abilityId; }
        public string AbilityId { get; }
        public override string Describe => "Bonus action";
    }

    public sealed class EndTurnAction : CombatAction { public override string Describe => "End turn"; }

    /// <summary>How a d20 roll went: both dice when rolled with advantage or disadvantage, and why.</summary>
    public sealed class D20Roll
    {
        public int First { get; set; }
        public int Second { get; set; }
        public int Natural { get; set; }
        public bool Advantage { get; set; }
        public bool Disadvantage { get; set; }
        public List<string> AdvantageSources { get; } = new List<string>();
        public List<string> DisadvantageSources { get; } = new List<string>();

        public string ModeText => Advantage ? "advantage (" + string.Join(", ", AdvantageSources) + ")" :
            Disadvantage ? "disadvantage (" + string.Join(", ", DisadvantageSources) + ")" :
            AdvantageSources.Count > 0 && DisadvantageSources.Count > 0 ? "normal (advantage and disadvantage cancel)" : "normal";
    }

    /// <summary>Damage after defences, and what it did.</summary>
    public sealed class DamageResult
    {
        public int Rolled { get; set; }
        public DamageType Type { get; set; }
        public List<string> Adjustments { get; } = new List<string>();
        public int Final { get; set; }
        public int TemporaryAbsorbed { get; set; }
        public int HitPointsLost { get; set; }
        public int HpBefore { get; set; }
        public int HpAfter { get; set; }
        public bool Downed { get; set; }
        public bool Killed { get; set; }
        public int DeathSaveFailuresAdded { get; set; }
    }

    /// <summary>The full account of one attack: rolls, modifiers, AC, hit or miss, critical, damage and its effect.</summary>
    public sealed class AttackResult
    {
        public CombatantId AttackerId { get; set; }
        public CombatantId TargetId { get; set; }
        public string AttackName { get; set; }
        public AttackKind Kind { get; set; }
        public bool Opportunity { get; set; }
        public D20Roll Roll { get; set; }
        public Breakdown AttackBonus { get; set; }
        public int Total { get; set; }
        public int TargetAc { get; set; }
        public Breakdown TargetAcBreakdown { get; set; }
        public bool Hit { get; set; }
        public bool Critical { get; set; }
        public string MissReason { get; set; }
        public List<int> DamageDice { get; } = new List<int>();
        public Breakdown DamageBreakdown { get; set; }
        public DamageResult Damage { get; set; }

        /// <summary>For Grapple and Shove: the target's save.</summary>
        public SaveResult Save { get; set; }

        public ConditionType? ConditionApplied { get; set; }
    }

    /// <summary>A saving throw (or ability check) against a DC, explained.</summary>
    public sealed class SaveResult
    {
        public Ability Ability { get; set; }
        public string Skill { get; set; }
        public D20Roll Roll { get; set; }
        public Breakdown Bonus { get; set; }
        public int Total { get; set; }
        public int Dc { get; set; }
        public bool Success { get; set; }
        public string AutomaticFailure { get; set; }
    }

    /// <summary>What an action produced: whether it was allowed, why not, and the log entries it wrote.</summary>
    public sealed class ActionOutcome
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public List<CombatLogEntry> Entries { get; } = new List<CombatLogEntry>();
        public List<AttackResult> Attacks { get; } = new List<AttackResult>();

        public static ActionOutcome Refused(string why) => new ActionOutcome { Ok = false, Error = why };
    }

    /// <summary>Who joins an encounter, on which side, where.</summary>
    public sealed class ParticipantSetup
    {
        public CombatantKind Kind { get; set; }

        /// <summary>For residents.</summary>
        public NpcId NpcId { get; set; }

        /// <summary>For creatures (and to override a resident's profile).</summary>
        public string CreatureId { get; set; }

        /// <summary>A creature's display name (defaults to its catalog name).</summary>
        public string Name { get; set; }

        public string Side { get; set; }
        public GridPos Position { get; set; }
        public CombatControl Control { get; set; } = CombatControl.Ai;
    }

    public sealed class EncounterSetup
    {
        public string Name { get; set; } = "Encounter";
        public EncounterKind Kind { get; set; }
        public Lethality Lethality { get; set; }
        public SettlementId SettlementId { get; set; }
        public List<ParticipantSetup> Participants { get; set; } = new List<ParticipantSetup>();
        public List<GridPos> DifficultTerrain { get; set; } = new List<GridPos>();
        public string QuestRef { get; set; }
        public string WorldEventRef { get; set; }
    }

    /// <summary>A chronicle-worthy moment the command reports (start, end, someone downed or killed).</summary>
    public sealed class CombatChronicleNote
    {
        public HistoricalEventType Type { get; set; }
        public Dictionary<string, string> Facts { get; set; } = new Dictionary<string, string>();
        public NpcId NpcId { get; set; }
    }
}
