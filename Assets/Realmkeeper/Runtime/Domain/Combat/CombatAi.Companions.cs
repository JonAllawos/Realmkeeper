using System;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 15 (ADR-0075): the companion layer of the one combat AI. A party member thinks first about the people on
    /// their side: a healing spell for someone dying or badly hurt; hands-on first aid for a dying friend (going to them
    /// if needed); their own Second Wind; Rage when the fight starts; the foe threatening the player. Otherwise they act
    /// on the baseline (<see cref="Next"/>). Every choice carries a reason in words, which the encounter log records.
    /// Deterministic, and proposals only: the engine still validates every action.
    /// </summary>
    public static partial class CombatAi
    {
        /// <summary>Whether this combatant is a member of the player's party (the companion layer applies).</summary>
        public static bool IsCompanion(CombatEngine engine, CombatantData actor)
        {
            if (actor == null || actor.Kind != CombatantKind.Npc || actor.CreatureId != null || !Guid.TryParse(actor.SourceId, out var g)) return false;
            return engine.World?.Party?.Contains(new NpcId(g)) == true;
        }

        public static CombatAction ForCompanion(CombatEngine engine, CombatantData actor, out string reason)
        {
            reason = null;
            var e = engine.Encounter;
            if (actor.State != VitalState.Conscious) return new EndTurnAction();
            var t = actor.Turn;
            CombatAction Ok(CombatAction a) => a != null && engine.Unavailable(actor, a) == null ? a : null;
            var foes = new System.Collections.Generic.List<CombatantData>();
            foreach (var c in e.Combatants) if (CombatEngine.Hostile(actor, c) && c.State == VitalState.Conscious) foes.Add(c);

            // 1. A healing spell for a friend who is dying or badly hurt (the baseline's own healing rule picks spell and slot).
            if (!engine.Has(actor, ConditionType.Prone) && foes.Count > 0)
            {
                var spell = SpellChoice(engine, actor, foes, CombatAiProfile.Default);
                if (spell != null && engine.SpellsOf(actor).Find(s => s.Id == spell.SpellId) is SpellDefinition def && def.Heals && spell.Targets.Count > 0)
                {
                    var who = e.Find(spell.Targets[0]);
                    reason = $"heals {Who(actor, who)} ({(who.State == VitalState.Dying ? "dying" : engine.Hp(who) + "/" + engine.MaxHp(who) + " HP")})";
                    return spell;
                }
            }

            // 2. First aid for a dying friend: stabilise if next to them, otherwise go to them.
            CombatantData dying = null;
            foreach (var c in e.Combatants)
                if (c.Side == actor.Side && c != actor && c.State == VitalState.Dying && (dying == null || actor.Position.DistanceFeet(c.Position) < actor.Position.DistanceFeet(dying.Position))) dying = c;
            if (dying != null && !t.ActionUsed)
            {
                if (dying.Position.IsAdjacent(actor.Position) && Ok(new StabilizeAction(dying.CombatantId)) is CombatAction aid)
                {
                    reason = $"{Who(actor, dying)} is dying: first aid";
                    return aid;
                }

                if (engine.MovementLeft(actor) > 0 && !t.Disengaged)
                {
                    var path = PathToward(engine, actor, dying, 5);
                    if (path.Count > 0 && Ok(new MoveAction(path.ToArray())) is CombatAction go)
                    {
                        reason = $"runs to {Who(actor, dying)}, who is dying";
                        return go;
                    }
                }
            }

            if (foes.Count == 0) return new EndTurnAction();

            // Phase 20A: a goliath's giant ancestry answers the hit just landed (one boon per hit, while its uses last).
            foreach (var boon in new[] { "fires_burn", "frosts_chill", "hills_tumble" })
                if (Ok(new UseFeatureAction(boon)) is CombatAction answer)
                {
                    reason = $"answers the hit with {CombatFeatureCatalog.Find(boon).Name}";
                    return answer;
                }

            // 3. Their own Second Wind when at half their hit points or less.
            if (engine.Hp(actor) * 2 <= engine.MaxHp(actor) && Ok(new UseFeatureAction("second_wind")) is CombatAction wind)
            {
                reason = $"catches their second wind ({engine.Hp(actor)}/{engine.MaxHp(actor)} HP)";
                return wind;
            }

            // 4. Rage as soon as there is a fight to rage in (not when already raging: the engine refuses that).
            if (!engine.HasEffect(actor, CombatEffectKind.Raging) && Ok(new UseFeatureAction("rage")) is CombatAction rage)
            {
                reason = "rages: the fight is on";
                return rage;
            }

            // 5. Defend the player: the foe next to them, if this companion can reach it.
            var player = e.Combatants.Find(c => c.Kind == CombatantKind.Player && c.Side == actor.Side);
            var melee = engine.Stats(actor).BestMelee();
            if (player != null && melee != null && (!t.ActionUsed || t.AttacksLeft > 0))
            {
                CombatantData threat = null;
                foreach (var f in foes)
                    if (f.Position.IsAdjacent(player.Position) && actor.Position.DistanceFeet(f.Position) <= melee.Reach && engine.CoverOf(actor, f).HasLineOfEffect &&
                        (threat == null || engine.Hp(f) < engine.Hp(threat))) threat = f;
                if (threat != null && Ok(new AttackAction(threat.CombatantId, melee.Id)) is CombatAction defend)
                {
                    reason = $"defends {player.Name} from {threat.Name}";
                    return defend;
                }
            }

            // 6. The baseline, explained by what it chose.
            var next = Next(engine, actor);
            reason = Explain(e, next);
            return next;
        }

        private static string Who(CombatantData actor, CombatantData c) => c == actor ? "themself" : c.Name;

        private static string Explain(EncounterData e, CombatAction a)
        {
            switch (a)
            {
                case AttackAction x: return "attacks " + (e.Find(x.Target)?.Name ?? "a foe") + " (the weakest in reach)";
                case CastSpellAction s: return "casts " + s.SpellId.Replace('_', ' ');
                case MoveAction _: return "closes in";
                case DashAction _: return "dashes toward the fight";
                case DisengageAction _: return "badly hurt: pulls back";
                case DodgeAction _: return "badly hurt: takes cover";
                case StandAction _: return "gets up";
                default: return null;
            }
        }
    }
}
