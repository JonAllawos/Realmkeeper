using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 10: the bestiary's typed traits, resolved by the shared engine (ADR-0044). Charge, Regeneration, Undead
    /// Fortitude, Relentless, Nimble Escape, saves and venom on a hit, recharging attacks and leaving the fight. Per-fight
    /// trait state lives in the combatant's resources map (saved with the encounter): "relentless", "regen_blocked",
    /// "recharge:{attack}".
    /// </summary>
    public sealed partial class CombatEngine
    {
        private const string RegenBlocked = "regen_blocked";
        private const string RelentlessUse = "relentless";

        private static string RechargeKey(string attackId) => "recharge:" + attackId;

        private CreatureDefinition CreatureOf(CombatantData c) => c.CreatureId != null && c.FormCreatureId == null ? CreatureCatalog.Find(c.CreatureId) : null;

        /// <summary>The trait state a creature starts a fight with (Relentless ready, recharging attacks charged).</summary>
        private static void PrepareTraits(CombatantData c, CreatureDefinition def)
        {
            if (def.Trait(CreatureTraitKind.Relentless) != null) c.Resources[RelentlessUse] = 1;
            foreach (var a in def.Attacks) if (a.Recharge > 0) c.Resources[RechargeKey(a.Id)] = 1;
        }

        /// <summary>Why an attack option cannot be used now (a recharging attack that has not come back), or null.</summary>
        public string AttackNotReady(CombatantData c, AttackOption o)
        {
            if (o?.Source == null || o.Source.Recharge <= 0) return null;
            return c.Resources.TryGetValue(RechargeKey(o.Source.Id), out var ready) && ready > 0 ? null : $"{o.Name} is recharging (it returns on a d6 roll of {o.Source.Recharge}+ at the start of {c.Name}'s turn).";
        }

        /// <summary>The start of a creature's turn: the square it starts on (for Charge), recharges and regeneration.</summary>
        private void StartOfTurnTraits(CombatantData c)
        {
            c.Turn.HasStart = true;
            c.Turn.StartX = c.Position.X;
            c.Turn.StartY = c.Position.Y;
            var def = CreatureOf(c);
            if (def == null || c.State == VitalState.Dead || c.State == VitalState.Fled) return;

            foreach (var a in def.Attacks)
            {
                if (a.Recharge <= 0 || !c.Resources.TryGetValue(RechargeKey(a.Id), out var ready) || ready > 0) continue;
                var d = Die(6);
                if (d >= a.Recharge) c.Resources[RechargeKey(a.Id)] = 1;
                Log(CombatLogKind.Action, c.CombatantId, default, $"{c.Name}'s {a.Name.ToLowerInvariant()} recharge: d6 {d} — {(d >= a.Recharge ? "ready again" : "not yet")}.", d);
            }

            var regen = def.Trait(CreatureTraitKind.Regeneration);
            if (regen == null) return;
            var blocked = c.Resources.TryGetValue(RegenBlocked, out var b) && b > 0;
            c.Resources[RegenBlocked] = 0;
            if (blocked)
            {
                Log(CombatLogKind.Effect, c.CombatantId, default, $"{c.Name} does not regenerate: fire or acid seared it since its last turn.");
                if (Hp(c) <= 0) Kill(c, "it could not regenerate", null);
                return;
            }

            if (Hp(c) < MaxHp(c)) Heal(c, regen.Value, "Regeneration");
        }

        /// <summary>Damage a regenerating creature takes of a type that stops it (fire, acid) blocks its next regeneration.</summary>
        private void NoteDamageType(CombatantData target, DamageType type, int final)
        {
            var regen = CreatureOf(target)?.Trait(CreatureTraitKind.Regeneration);
            if (regen == null || final <= 0) return;
            foreach (var t in regen.StoppedBy) if (t == type) target.Resources[RegenBlocked] = 1;
        }

        /// <summary>
        /// Before a creature's hit points reach 0: Undead Fortitude (a Constitution save, DC 5 + the damage, unless the
        /// damage is radiant or a critical hit) or Relentless (once, for damage up to its limit) leaves it at 1. The
        /// hit points it actually loses.
        /// </summary>
        private int ClingToLife(CombatantData target, int lost, DamageType type, bool critical)
        {
            if (target.CurrentHp - lost > 0) return lost;
            var def = CreatureOf(target);
            if (def == null || target.CurrentHp <= 0) return lost;
            if (def.Trait(CreatureTraitKind.UndeadFortitude) != null && type != DamageType.Radiant && !critical)
            {
                var save = SavingThrow(target, Rpg.Ability.Constitution, 5 + lost, "Undead Fortitude");
                Log(CombatLogKind.Effect, target.CombatantId, default, $"{target.Name}'s Undead Fortitude: Constitution save {save.Total} vs DC {5 + lost} — {(save.Success ? "it stays up at 1 hit point" : "it falls")}.", save.Total, save.Roll);
                if (save.Success) return target.CurrentHp - 1;
            }

            var relentless = def.Trait(CreatureTraitKind.Relentless);
            if (relentless != null && lost <= relentless.Value && target.Resources.TryGetValue(RelentlessUse, out var left) && left > 0)
            {
                target.Resources[RelentlessUse] = 0;
                Log(CombatLogKind.Effect, target.CombatantId, default, $"{target.Name} is Relentless: {lost} damage leaves it at 1 hit point (once per fight).");
                return target.CurrentHp - 1;
            }

            return lost;
        }

        /// <summary>A creature at 0 that can still regenerate collapses instead of dying (it dies at the start of its turn only if it cannot regenerate).</summary>
        private bool CollapsesInsteadOfDying(CombatantData target)
        {
            if (CreatureOf(target)?.Trait(CreatureTraitKind.Regeneration) == null) return false;
            SetState(target, VitalState.Stable);
            MarkUnconscious(target, "dropped to 0 hit points");
            Log(CombatLogKind.Downed, target.CombatantId, default, $"{target.Name} collapses — but its wounds are already knitting.");
            return true;
        }

        /// <summary>Charge: moved 20 ft or more this turn and ended closer to the target by at least that much; the extra dice go into the damage.</summary>
        private CreatureTrait ChargeBonus(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r)
        {
            var charge = CreatureOf(attacker)?.Trait(CreatureTraitKind.Charge);
            if (charge == null || !o.Melee || !attacker.Turn.HasStart || attacker.Turn.MovementUsed < 20) return null;
            var start = new GridPos(attacker.Turn.StartX, attacker.Turn.StartY);
            if (start.DistanceFeet(target.Position) - attacker.Position.DistanceFeet(target.Position) < 20) return null;
            var extra = 0;
            var rolls = new List<int>();
            for (var i = 0; i < charge.Value * (r.Critical ? 2 : 1); i++)
            {
                var d = Die(charge.DiceSides);
                rolls.Add(d);
                extra += d;
            }

            r.DamageBreakdown.Add($"Charge {charge.Value}d{charge.DiceSides} [{string.Join(", ", rolls)}]", extra);
            return charge;
        }

        /// <summary>What a creature's hit does besides its damage: a charge's knock-down, a held condition (with its save), venom (with its save).</summary>
        private void CreatureHitEffects(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r, CreatureTrait charge)
        {
            if (target.State != VitalState.Conscious) return;
            if (charge != null && charge.SaveDc > 0)
            {
                var s = SavingThrow(target, Rpg.Ability.Strength, charge.SaveDc, attacker.Name + "'s charge");
                Log(CombatLogKind.Effect, target.CombatantId, attacker.CombatantId, $"{target.Name} braces against the charge: Strength save {s.Total} vs DC {charge.SaveDc} — {(s.Success ? "keeps their feet" : "knocked prone")}.", s.Total, s.Roll);
                if (!s.Success) ApplyCondition(target, ConditionType.Prone, attacker.CombatantId, attacker.Name + "'s charge", ConditionDuration.UntilRemoved, default, 0);
            }

            var src = o.Source;
            if (src != null && src.SaveDamage.Count > 0 && target.State == VitalState.Conscious)
            {
                var s = SavingThrow(target, src.SaveDamageAbility, src.SaveDamageDc, attacker.Name + "'s " + o.Name.ToLowerInvariant());
                var total = 0;
                for (var i = 0; i < src.SaveDamage.Count; i++) total += Die(src.SaveDamage.Sides);
                var amount = s.Success ? total / 2 : total;
                Log(CombatLogKind.Effect, target.CombatantId, attacker.CombatantId,
                    $"{target.Name}'s {src.SaveDamageAbility} save against the {src.SaveDamageType.ToString().ToLowerInvariant()}: {s.Total} vs DC {src.SaveDamageDc} — {(s.Success ? "half" : "full")} ({amount} of {total}).", s.Total, s.Roll);
                if (amount > 0) ApplyDamage(target, amount, src.SaveDamageType, false, attacker.Name);
            }
        }

        /// <summary>Applies a hit's condition: after a save when the stat block gives one; a restraint can be escaped with a Strength check.</summary>
        private void ApplyHitCondition(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r)
        {
            var src = o.Source;
            if (src?.OnHitSave != null)
            {
                var s = SavingThrow(target, src.OnHitSave.Value, src.OnHitDc, attacker.Name + "'s " + o.Name.ToLowerInvariant());
                Log(CombatLogKind.Effect, target.CombatantId, attacker.CombatantId,
                    $"{target.Name}'s {src.OnHitSave.Value} save: {s.Total} vs DC {src.OnHitDc} — {(s.Success ? "resists" : o.OnHit.Value.ToString().ToLowerInvariant())}.", s.Total, s.Roll);
                if (s.Success) return;
            }

            r.ConditionApplied = o.OnHit;
            ApplyCondition(target, o.OnHit.Value, attacker.CombatantId, attacker.Name + "'s " + o.Name.ToLowerInvariant(), ConditionDuration.UntilRemoved, default, 0, 1, src?.EscapeDc ?? 0);
            if (src != null && src.EscapeDc > 0)
                foreach (var k in target.Conditions)
                    if (k.Type == o.OnHit.Value && k.SourceId == attacker.CombatantId) k.EscapeByCheck = true;
        }

        /// <summary>Spends a recharging attack when it is used.</summary>
        private void SpendRecharge(CombatantData attacker, AttackOption o)
        {
            if (o?.Source == null || o.Source.Recharge <= 0) return;
            attacker.Resources[RechargeKey(o.Source.Id)] = 0;
        }

        /// <summary>Nimble Escape: a creature with the trait may Disengage with its bonus action.</summary>
        public bool NimbleEscape(CombatantData c) => CreatureOf(c)?.Trait(CreatureTraitKind.NimbleEscape) != null;

        /// <summary>
        /// Why a combatant cannot leave the fight now (Phase 10, a Realmkeeper rule): only those the computer controls; it
        /// costs the action; and no conscious enemy may be within 30 ft (anyone closer would cut it off).
        /// </summary>
        public string FleeProblem(CombatantData c)
        {
            if (c.Control == CombatControl.Player || c.Kind == CombatantKind.Player) return "You leave a fight with Withdraw.";
            if (c.Turn.ActionUsed) return "The action this turn is used.";
            foreach (var o in Encounter.Combatants)
                if (Hostile(c, o) && o.State == VitalState.Conscious && c.Position.DistanceFeet(o.Position) <= 30) return $"{o.Name} is within 30 ft.";
            return null;
        }

        private void DoFlee(CombatantData c)
        {
            UseAction(c);
            c.State = VitalState.Fled;
            foreach (var e in new List<ActiveEffectData>(EffectsOn(c))) RemoveEffect(e, "it left the fight");
            EndConcentration(c, "left the fight");
            Log(CombatLogKind.Moved, c.CombatantId, default, $"{c.Name} flees the fight.");
            AdvanceTurn(); // gone: its turn ends with it
        }

        /// <summary>At the end of a fight, a regenerating creature that lies at 0 is finished off by the side that stands (Realmkeeper rule: they burn it).</summary>
        private void FinishOffRegenerators()
        {
            foreach (var c in Encounter.Combatants)
                if (c.State == VitalState.Stable && CreatureOf(c)?.Trait(CreatureTraitKind.Regeneration) != null) Kill(c, "finished off before it could rise", null);
        }
    }
}
