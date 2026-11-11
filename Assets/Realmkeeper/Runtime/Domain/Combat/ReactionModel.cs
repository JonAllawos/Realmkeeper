using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>What can be done when an opportunity arises (Phase 10, ADR-0045). Decisions are not reactions (no reaction is spent).</summary>
    public enum ReactionKind
    {
        OpportunityAttack = 0,
        Shield = 1,
        Counterspell = 2,
        CuttingWords = 3,
        DeflectAttacks = 4,
        UncannyDodge = 5,
        Parry = 6,
        HellishRebuke = 7,

        /// <summary>A decision, not a reaction: reroll a failed save with a bonus of the fighter level.</summary>
        Indomitable = 8,

        /// <summary>A decision, not a reaction: add a held Bardic Inspiration die to a failed roll.</summary>
        BardicInspiration = 9,
    }

    /// <summary>One chance to react (or decide), offered by the engine at the moment its trigger happens.</summary>
    public sealed class ReactionOpportunity
    {
        public ReactionKind Kind { get; set; }

        /// <summary>Who would react.</summary>
        public CombatantId ReactorId { get; set; }

        /// <summary>Who caused it (the mover, the attacker, the caster).</summary>
        public CombatantId TriggerId { get; set; }

        /// <summary>Who it is about (the target of the attack, the mover).</summary>
        public CombatantId TargetId { get; set; }

        public string Name { get; set; }

        /// <summary>The triggering event, in words ("Bandit hits you: 15 against AC 13").</summary>
        public string Trigger { get; set; }

        /// <summary>What it costs ("your reaction and a level 1 spell slot").</summary>
        public string Cost { get; set; }

        /// <summary>What it would do, where calculable ("+5 AC: 15 would miss").</summary>
        public string Consequence { get; set; }

        public bool IsDecision => Kind == ReactionKind.Indomitable || Kind == ReactionKind.BardicInspiration;

        public ReactionOpportunity Clone() => (ReactionOpportunity)MemberwiseClone();
    }

    /// <summary>
    /// The action waiting on a player's answer (persisted with the encounter). The action is replayed from the state
    /// before it, with the answers given so far, so its outcome is exactly what it would have been had the player
    /// answered at once (ADR-0045).
    /// </summary>
    public sealed class PendingReactionData
    {
        public int Id { get; set; }
        public ReactionOpportunity Opportunity { get; set; }
        public CombatantId ActorId { get; set; }
        public ActionRecord Action { get; set; }
        public List<bool> Answers { get; set; } = new List<bool>();

        /// <summary>The encounter's action sequence when it was asked (a stale answer is refused).</summary>
        public int Sequence { get; set; }

        public PendingReactionData Clone()
        {
            var c = (PendingReactionData)MemberwiseClone();
            c.Opportunity = Opportunity?.Clone();
            c.Action = Action?.Clone();
            c.Answers = new List<bool>(Answers);
            return c;
        }
    }

    /// <summary>A combat action in a form that can be saved and replayed.</summary>
    public sealed class ActionRecord
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public string Option { get; set; }
        public CombatantId Target { get; set; }
        public List<CombatantId> Targets { get; set; } = new List<CombatantId>();
        public List<GridPos> Path { get; set; } = new List<GridPos>();
        public bool HasPoint { get; set; }
        public GridPos Point { get; set; }
        public int Level { get; set; }
        public int Amount { get; set; }
        public List<string> Metamagic { get; set; } = new List<string>();
        public string FreeWith { get; set; }

        public ActionRecord Clone()
        {
            var c = (ActionRecord)MemberwiseClone();
            c.Targets = new List<CombatantId>(Targets);
            c.Path = new List<GridPos>(Path);
            c.Metamagic = new List<string>(Metamagic);
            return c;
        }
    }

    /// <summary>Turns actions into records and back (every action the engine accepts).</summary>
    public static class ActionCodec
    {
        public static ActionRecord ToRecord(CombatAction a)
        {
            switch (a)
            {
                case MoveAction m: return new ActionRecord { Kind = "move", Path = new List<GridPos>(m.Path) };
                case AttackAction x: return new ActionRecord { Kind = "attack", Target = x.Target, Option = x.OptionId };
                case DashAction _: return new ActionRecord { Kind = "dash" };
                case DisengageAction _: return new ActionRecord { Kind = "disengage" };
                case DodgeAction _: return new ActionRecord { Kind = "dodge" };
                case StandAction _: return new ActionRecord { Kind = "stand" };
                case EscapeAction _: return new ActionRecord { Kind = "escape" };
                case FleeAction _: return new ActionRecord { Kind = "flee" };
                case EndTurnAction _: return new ActionRecord { Kind = "end" };
                case StabilizeAction s: return new ActionRecord { Kind = "stabilize", Target = s.Target };
                case BonusAbilityAction b: return new ActionRecord { Kind = "bonus", Id = b.AbilityId };
                case UseFeatureAction f: return new ActionRecord { Kind = "feature", Id = f.FeatureId, Target = f.Target, Option = f.Option, Amount = f.Amount };
                case CastSpellAction c:
                    return new ActionRecord
                    {
                        Kind = "spell", Id = c.SpellId, Level = c.SlotLevel, Targets = new List<CombatantId>(c.Targets), HasPoint = c.Point.HasValue,
                        Point = c.Point ?? default, Metamagic = new List<string>(c.Metamagic), FreeWith = c.FreeWith,
                    };
                default: throw new ArgumentException("This action cannot be recorded: " + a?.GetType().Name);
            }
        }

        public static CombatAction FromRecord(ActionRecord r)
        {
            switch (r?.Kind)
            {
                case "move": return new MoveAction(r.Path.ToArray());
                case "attack": return new AttackAction(r.Target, r.Option);
                case "dash": return new DashAction();
                case "disengage": return new DisengageAction();
                case "dodge": return new DodgeAction();
                case "stand": return new StandAction();
                case "escape": return new EscapeAction();
                case "flee": return new FleeAction();
                case "end": return new EndTurnAction();
                case "stabilize": return new StabilizeAction(r.Target);
                case "bonus": return new BonusAbilityAction(r.Id);
                case "feature": return new UseFeatureAction(r.Id, r.Target, r.Option, r.Amount);
                case "spell": return new CastSpellAction(r.Id, r.Level, r.Targets, r.HasPoint ? r.Point : (GridPos?)null, r.Metamagic, r.FreeWith);
                default: return null;
            }
        }
    }
}
