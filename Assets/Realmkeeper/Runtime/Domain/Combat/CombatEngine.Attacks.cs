using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Attacks (Phase 8, extended in Phase 9). One attack roll serves weapons, unarmed strikes and spell attacks alike:
    /// roll modes from features, conditions, effects, range and allies; Bless and Bane dice; Bardic Inspiration; critical
    /// ranges (Champion); a total against the target's live armour class. Weapon damage then adds its riders through the
    /// same path: rage damage, Sneak Attack, Frenzy, Hunter's Mark, weapon masteries, Uncanny Dodge.
    /// </summary>
    public sealed partial class CombatEngine
    {
        private void DoAttack(CombatantData actor, AttackAction a)
        {
            var t = actor.Turn;
            var stats = Stats(actor);
            if (!t.ActionUsed)
            {
                t.ActionUsed = true;
                t.SurgeActionPending = false;
                t.AttacksLeft = stats.AttacksPerAction;
                t.AttackActionTaken = true;
            }

            t.AttacksLeft--;
            var o = stats.Find(a.OptionId);
            if (o.Light && o.Kind == AttackKind.Weapon) t.LightAttackWeapon = o.ItemId;
            ResolveAttack(actor, Encounter.Find(a.Target), o, false);
        }

        /// <summary>Resolves one attack (weapon, unarmed strike, grapple or shove) fully, including its damage and effects.</summary>
        public AttackResult ResolveAttack(CombatantData attacker, CombatantData target, AttackOption o, bool opportunity)
        {
            if (o.Kind == AttackKind.Grapple || o.Kind == AttackKind.Shove)
            {
                var g = new AttackResult { AttackerId = attacker.CombatantId, TargetId = target.CombatantId, AttackName = o.Name, Kind = o.Kind, Opportunity = opportunity };
                _current?.Attacks.Add(g);
                HostileActBy(attacker);
                attacker.Turn.AttackedOrForcedSave = true;
                // Unarmed Strike options: the target saves with Strength or Dexterity (its choice: the better bonus).
                var ts = Stats(target);
                var ability = ts.SaveBonus(Ability.Strength).Total >= ts.SaveBonus(Ability.Dexterity).Total ? Ability.Strength : Ability.Dexterity;
                g.Save = SavingThrow(target, ability, o.SaveDc, $"{attacker.Name}'s {o.Name.ToLowerInvariant()}");
                g.Hit = !g.Save.Success;
                if (g.Hit)
                {
                    g.ConditionApplied = o.Kind == AttackKind.Grapple ? ConditionType.Grappled : ConditionType.Prone;
                    ApplyCondition(target, g.ConditionApplied.Value, attacker.CombatantId, attacker.Name + "'s " + o.Name.ToLowerInvariant(), ConditionDuration.UntilRemoved, default, 0, 1, o.SaveDc);
                }

                Log(CombatLogKind.Attack, attacker.CombatantId, target.CombatantId,
                    $"{attacker.Name} tries to {(o.Kind == AttackKind.Grapple ? "grapple" : "shove")} {target.Name}: {target.Name} {(g.Hit ? "fails" : "succeeds on")} a DC {o.SaveDc} {ability} save ({g.Save.Total}).", g.Save.Total, g.Save.Roll);
                return g;
            }

            var r = AttackRoll(attacker, target, o, opportunity);
            var weapon = o.ItemId != null ? ItemCatalog.Find(o.ItemId)?.Weapon : null;
            if (!r.Hit)
            {
                // Graze (mastery): a miss still deals the ability modifier's damage.
                if (Mastery(attacker, o, weapon) == "Graze")
                {
                    var graze = Stats(attacker).Modifier(o.Ability);
                    if (graze > 0)
                    {
                        Log(CombatLogKind.Action, attacker.CombatantId, target.CombatantId, $"Graze: the miss still deals {graze} damage.");
                        r.Damage = ApplyDamage(target, graze, o.DamageType, false, attacker.Name);
                    }
                }

                return r;
            }

            // Damage: the dice (twice on a critical) plus the bonus; Great Weapon Fighting lifts low dice.
            var dice = r.Critical ? o.Damage.Count * 2 : o.Damage.Count;
            var total = 0;
            for (var i = 0; i < dice; i++)
            {
                var d = Die(o.Damage.Sides);
                if (o.DieFloor > 0 && d < o.DieFloor) d = o.DieFloor;
                r.DamageDice.Add(d);
                total += d;
            }

            r.DamageBreakdown = new Breakdown();
            if (dice > 0) r.DamageBreakdown.Add($"{dice}d{o.Damage.Sides}{(r.Critical ? " (critical)" : string.Empty)} [{string.Join(", ", r.DamageDice)}]", total);
            if (o.Damage.Bonus != 0) r.DamageBreakdown.Add("bonus", o.Damage.Bonus);
            if (o.DamageBonus != null) foreach (var (label, value) in o.DamageBonus.Parts) if (value != 0) r.DamageBreakdown.Add(label, value);
            AddDamageRiders(attacker, target, o, weapon, r);
            var amount = Math.Max(0, r.DamageBreakdown.Total);

            // Uncanny Dodge (automatic reaction): half the damage of an attack the rogue can see.
            if (TargetFeature(target, "uncanny_dodge") && CanReact(target) && !Has(target, ConditionType.Blinded) && amount > 0)
            {
                target.Turn.ReactionUsed = true;
                amount /= 2;
                r.DamageBreakdown.Note("Uncanny Dodge halves it");
                Log(CombatLogKind.Action, target.CombatantId, attacker.CombatantId, $"{target.Name} uses Uncanny Dodge: the damage is halved to {amount}.");
            }

            var hpBefore = Hp(target);
            r.Damage = ApplyDamage(target, amount, o.DamageType, r.Critical, attacker.Name);
            ExtraDamageFromMarks(attacker, target, r);
            if (o.OnHit.HasValue && target.State == VitalState.Conscious)
            {
                r.ConditionApplied = o.OnHit;
                ApplyCondition(target, o.OnHit.Value, attacker.CombatantId, attacker.Name + "'s " + o.Name.ToLowerInvariant(), ConditionDuration.UntilRemoved, default, 0);
            }

            var t = attacker.Turn;
            t.LastHitTargetId = target.CombatantId;
            t.LastHitMelee = o.Melee && (o.Kind == AttackKind.Weapon || o.Kind == AttackKind.Unarmed);
            t.LastHitCritical = r.Critical;
            t.LastHitMonkWeapon = o.Kind == AttackKind.Unarmed || weapon != null && o.Melee && (weapon.Simple || weapon.Has(WeaponProperty.Light)) && !weapon.Has(WeaponProperty.Heavy) && !weapon.Has(WeaponProperty.TwoHanded);
            ApplyMastery(attacker, target, o, weapon);
            DarkOnesBlessing(attacker, target, hpBefore);
            return r;
        }

        /// <summary>The d20 part of any attack (weapon, unarmed, spell): modes, dice, bonuses, critical range, the target's armour class, explained.</summary>
        public AttackResult AttackRoll(CombatantData attacker, CombatantData target, AttackOption o, bool opportunity)
        {
            var r = new AttackResult { AttackerId = attacker.CombatantId, TargetId = target.CombatantId, AttackName = o.Name, Kind = o.Kind, Opportunity = opportunity };
            _current?.Attacks.Add(r);
            HostileActBy(attacker);
            attacker.Turn.AttackedOrForcedSave = true;
            var roll = new D20Roll();
            var modes = Stats(attacker).RollModes("attack:" + o.Ability);
            roll.AdvantageSources.AddRange(modes.Advantage);
            roll.DisadvantageSources.AddRange(modes.Disadvantage);
            AttackModes(attacker, target, o, roll);
            Roll(roll);
            var exhaustion = ExhaustionLevel(attacker);
            var notes = new List<string>();
            var dieBonus = RollDieEffects(attacker, notes);
            var flat = 0;
            if (o.Kind == AttackKind.Weapon)
                foreach (var e in EffectsOn(attacker))
                    if (e.Kind == CombatEffectKind.WeaponAttackBonus) { flat += e.Value; notes.Add($"{e.Name} +{e.Value}"); }
            r.Roll = roll;
            r.AttackBonus = o.AttackBonus;
            r.Total = roll.Natural + o.AttackBonus.Total - 2 * exhaustion + dieBonus + flat;
            r.TargetAcBreakdown = ArmorClass(target);
            r.TargetAc = r.TargetAcBreakdown.Total;
            var critFrom = 20;
            if (o.Kind == AttackKind.Weapon || o.Kind == AttackKind.Unarmed)
            {
                if (AttackerFeature(attacker, "superior_critical")) critFrom = 18;
                else if (AttackerFeature(attacker, "improved_critical")) critFrom = 19;
            }

            if (roll.Natural == 1) { r.Hit = false; r.MissReason = "a natural 1 always misses"; }
            else if (roll.Natural >= critFrom) { r.Hit = true; r.Critical = true; }
            else
            {
                r.Total += SpendInspiration(attacker, r.Total, r.TargetAc, notes);
                r.Hit = r.Total >= r.TargetAc;
                if (!r.Hit) r.MissReason = $"{r.Total} is below AC {r.TargetAc}";
            }

            var within5 = attacker.Position.DistanceFeet(target.Position) <= 5;
            if (r.Hit && !r.Critical && within5 && (Has(target, ConditionType.Paralyzed) || Has(target, ConditionType.Unconscious))) r.Critical = true;

            var verb = opportunity ? "Opportunity attack" : o.Name;
            Log(CombatLogKind.Attack, attacker.CombatantId, target.CombatantId,
                $"{attacker.Name} → {target.Name}: {verb}, d20 {roll.Natural}{(roll.Advantage || roll.Disadvantage ? $" ({roll.First}/{roll.Second}, {roll.ModeText})" : string.Empty)} " +
                $"{Breakdown.Signed(o.AttackBonus.Total)}{(exhaustion > 0 ? $" −{2 * exhaustion} exhaustion" : string.Empty)}{(notes.Count > 0 ? " " + string.Join(" ", notes) : string.Empty)} = {r.Total} vs AC {r.TargetAc}: " +
                (r.Critical ? "critical hit!" : r.Hit ? "hit." : "miss (" + r.MissReason + ")."), r.Total, roll);
            return r;
        }

        /// <summary>Advantage and disadvantage from conditions, effects, dodging, range, nearby enemies, pack tactics and features.</summary>
        private void AttackModes(CombatantData attacker, CombatantData target, AttackOption o, D20Roll roll)
        {
            var d = attacker.Position.DistanceFeet(target.Position);
            void Adv(string s) { if (!roll.AdvantageSources.Contains(s)) roll.AdvantageSources.Add(s); }
            void Dis(string s) { if (!roll.DisadvantageSources.Contains(s)) roll.DisadvantageSources.Add(s); }

            if (Has(attacker, ConditionType.Blinded)) Dis("blinded");
            if (Has(attacker, ConditionType.Frightened)) Dis("frightened");
            if (Has(attacker, ConditionType.Poisoned)) Dis("poisoned");
            if (Has(attacker, ConditionType.Prone)) Dis("prone");
            if (Has(attacker, ConditionType.Restrained)) Dis("restrained");
            foreach (var k in attacker.Conditions)
                if (k.Type == ConditionType.Grappled && k.SourceId != target.CombatantId) Dis("grappled (attacking someone else)");
            if (Has(attacker, ConditionType.Invisible)) Adv("invisible");

            if (Has(target, ConditionType.Blinded)) Adv("target blinded");
            if (Has(target, ConditionType.Paralyzed)) Adv("target paralyzed");
            if (Has(target, ConditionType.Petrified)) Adv("target petrified");
            if (Has(target, ConditionType.Restrained)) Adv("target restrained");
            if (Has(target, ConditionType.Stunned)) Adv("target stunned");
            if (Has(target, ConditionType.Unconscious)) Adv("target unconscious");
            if (Has(target, ConditionType.Invisible)) Dis("target invisible");
            if (Has(target, ConditionType.Prone)) { if (d <= 5) Adv("target prone, within 5 ft"); else Dis("target prone, at range"); }
            if (target.Turn.Dodging && !Incapacitated(target) && Speed(target) > 0 && !Has(attacker, ConditionType.Invisible)) Dis("target dodging");

            if (!o.Melee)
            {
                if (d > o.Range) Dis("long range");
                foreach (var x in Encounter.Combatants)
                    if (x != attacker && Hostile(x, attacker) && x.State == VitalState.Conscious && !Incapacitated(x) && x.Position.IsAdjacent(attacker.Position))
                    {
                        Dis("an enemy within 5 ft");
                        break;
                    }
            }

            if (Stats(attacker).PackTactics)
                foreach (var ally in Encounter.Combatants)
                    if (ally != attacker && ally.Side == attacker.Side && ally.State == VitalState.Conscious && !Incapacitated(ally) && ally.Position.IsAdjacent(target.Position))
                    {
                        Adv("pack tactics");
                        break;
                    }

            // Phase 9: spell and feature effects.
            if (HasEffect(target, CombatEffectKind.AttackersHaveAdvantage)) Adv("target outlined (Faerie Fire)");
            if (HasEffect(target, CombatEffectKind.RecklessDefender)) Adv("target attacking recklessly");
            if (ConsumeEffectOn(target, CombatEffectKind.NextAttackAgainstHasAdvantage)) Adv("target marked (next attack has advantage)");
            if (ConsumeEffectOn(attacker, CombatEffectKind.NextAttackHasDisadvantage)) Dis("shaken (next attack has disadvantage)");
            if (ConsumeNextAttackAdvantage(attacker, target)) Adv("advantage on the next attack");
            if (attacker.Turn.Reckless && o.Melee && o.Ability == Ability.Strength && (o.Kind == AttackKind.Weapon || o.Kind == AttackKind.Unarmed)) Adv("Reckless Attack");
            if (o.Kind == AttackKind.Spell && HasEffect(attacker, CombatEffectKind.InnateSorcery)) Adv("Innate Sorcery");
        }

        // ===== Riders ======================================================================================

        private bool AttackerFeature(CombatantData c, string id) => c.Kind == CombatantKind.Player && Stats(c).Rules != null && Stats(c).Rules.HasFeature(id);

        private bool TargetFeature(CombatantData c, string id) => AttackerFeature(c, id);

        private void AddDamageRiders(CombatantData attacker, CombatantData target, AttackOption o, WeaponProfile weapon, AttackResult r)
        {
            var b = r.DamageBreakdown;
            var crit = r.Critical;
            var physical = o.Kind == AttackKind.Weapon || o.Kind == AttackKind.Unarmed;
            if (!physical) return;
            var t = attacker.Turn;

            // Rage: Strength-based weapon attacks and unarmed strikes.
            if (HasEffect(attacker, CombatEffectKind.Raging) && o.Ability == Ability.Strength)
                b.Add("Rage", Stats(attacker).Rules?.Scaling("rage_damage") ?? 2);

            // Sneak Attack: once per turn, finesse or ranged weapon, with advantage — or an ally next to the target and no disadvantage.
            if (AttackerFeature(attacker, "sneak_attack") && !t.SneakAttackUsed && o.Kind == AttackKind.Weapon && weapon != null && (weapon.Has(WeaponProperty.Finesse) || !o.Melee))
            {
                var allyNear = false;
                foreach (var x in Encounter.Combatants)
                    if (x != attacker && x.Side == attacker.Side && x.State == VitalState.Conscious && !Incapacitated(x) && x.Position.IsAdjacent(target.Position)) allyNear = true;
                if (r.Roll.Advantage || allyNear && !r.Roll.Disadvantage)
                {
                    t.SneakAttackUsed = true;
                    var n = Stats(attacker).Rules.Scaling("sneak_attack");
                    b.Add($"Sneak Attack {(crit ? 2 * n : n)}d6 [{RollDice(crit ? 2 * n : n, 6, out var sum)}]", sum);
                }
            }

            // Frenzy (Berserker): raging and reckless, the first target hit each turn takes rage-damage d6s more.
            if (AttackerFeature(attacker, "frenzy") && HasEffect(attacker, CombatEffectKind.Raging) && t.Reckless && !t.FrenzyUsed && o.Ability == Ability.Strength)
            {
                t.FrenzyUsed = true;
                var n = Stats(attacker).Rules.Scaling("rage_damage");
                b.Add($"Frenzy {(crit ? 2 * n : n)}d6 [{RollDice(crit ? 2 * n : n, 6, out var sum)}]", sum);
            }
        }

        /// <summary>Extra damage from the attacker's marks on the target (Hunter's Mark), applied as its own damage type.</summary>
        private void ExtraDamageFromMarks(CombatantData attacker, CombatantData target, AttackResult r)
        {
            if (target.State == VitalState.Dead) return;
            foreach (var e in new List<ActiveEffectData>(EffectsOn(target)))
            {
                if (e.Kind != CombatEffectKind.ExtraDamageWhenCasterHits || e.SourceId != attacker.CombatantId) continue;
                var n = r.Critical ? 2 * e.DiceCount : e.DiceCount;
                var dice = RollDice(n, e.DiceSides, out var sum);
                Log(CombatLogKind.Damage, attacker.CombatantId, target.CombatantId, $"{e.Name}: {n}d{e.DiceSides} [{dice}] = {sum} {e.DamageType.ToString().ToLowerInvariant()} damage.", sum);
                ApplyDamage(target, sum, e.DamageType, r.Critical, attacker.Name);
            }
        }

        private string RollDice(int count, int sides, out int sum)
        {
            sum = 0;
            var list = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var d = Die(sides);
                list.Add(d);
                sum += d;
            }

            return string.Join(", ", list);
        }

        // ===== Weapon mastery (SRD 5.2.1) ===================================================================

        /// <summary>The weapon's mastery property when the attacker can use it (Weapon Mastery and proficiency), or null.</summary>
        private string Mastery(CombatantData attacker, AttackOption o, WeaponProfile weapon)
        {
            if (weapon == null || o.Kind != AttackKind.Weapon || !o.Proficient || !AttackerFeature(attacker, "weapon_mastery")) return null;
            return weapon.Mastery;
        }

        private void ApplyMastery(CombatantData attacker, CombatantData target, AttackOption o, WeaponProfile weapon)
        {
            var m = Mastery(attacker, o, weapon);
            if (m == null || target.State != VitalState.Conscious) return;
            var instance = NewInstance();
            switch (m)
            {
                case "Vex":
                    AddEffect(instance, "mastery:vex", "Vex", attacker, attacker, CombatEffectKind.NextAttackAdvantage, related: target, singleUse: true,
                        expiry: EffectExpiry.EndOfTurn, expiresOn: attacker, expiresRound: Encounter.Round + 1);
                    Log(CombatLogKind.Action, attacker.CombatantId, target.CombatantId, $"Vex: {attacker.Name}'s next attack against {target.Name} has advantage.");
                    break;
                case "Sap":
                    AddEffect(instance, "mastery:sap", "Sap", attacker, target, CombatEffectKind.NextAttackHasDisadvantage, singleUse: true,
                        expiry: EffectExpiry.StartOfTurn, expiresOn: attacker, expiresRound: Encounter.Round + 1);
                    Log(CombatLogKind.Action, attacker.CombatantId, target.CombatantId, $"Sap: {target.Name}'s next attack has disadvantage.");
                    break;
                case "Slow":
                    AddEffect(instance, "mastery:slow", "Slow", attacker, target, CombatEffectKind.SpeedChange, -10, expiry: EffectExpiry.StartOfTurn, expiresOn: attacker, expiresRound: Encounter.Round + 1);
                    Log(CombatLogKind.Action, attacker.CombatantId, target.CombatantId, $"Slow: {target.Name}'s speed drops by 10 feet.");
                    break;
                case "Topple":
                {
                    var dc = 8 + Stats(attacker).Modifier(o.Ability) + Stats(attacker).ProficiencyBonus;
                    var save = SavingThrow(target, Ability.Constitution, dc, "Topple");
                    if (!save.Success) ApplyCondition(target, ConditionType.Prone, attacker.CombatantId, "toppled", ConditionDuration.UntilRemoved, default, 0);
                    break;
                }

                case "Push":
                    PushAway(attacker.Position, target, 10);
                    break;
            }
        }

        private void PushAway(GridPos from, CombatantData target, int feet)
        {
            var at = target.Position;
            var dx = Math.Sign(at.X - from.X);
            var dy = Math.Sign(at.Y - from.Y);
            if (dx == 0 && dy == 0) return;
            var start = at;
            for (var i = 0; i < feet / GridPos.Feet; i++)
            {
                var next = new GridPos(at.X + dx, at.Y + dy, at.Z);
                if (At(next) != null) break;
                at = next;
            }

            if (at.Equals(start)) return;
            target.Position = at;
            Log(CombatLogKind.Moved, target.CombatantId, default, $"{target.Name} is pushed {start} → {at}.");
            ReleaseBrokenGrapples();
        }

        /// <summary>Dark One's Blessing (Fiend warlock): dropping a hostile creature to 0 grants temporary hit points.</summary>
        private void DarkOnesBlessing(CombatantData attacker, CombatantData target, int hpBefore)
        {
            if (!AttackerFeature(attacker, "dark_ones_blessing") || hpBefore <= 0 || Hp(target) > 0 || !Hostile(attacker, target)) return;
            var rules = Stats(attacker).Rules;
            var amount = Math.Max(1, rules.Level + AbilityRules.Modifier(rules.Score(Ability.Charisma)));
            CharacterVitals.GrantTemporaryHitPoints(_world.Player.Rpg, amount);
            Log(CombatLogKind.Healing, attacker.CombatantId, default, $"Dark One's Blessing: {attacker.Name} gains {amount} temporary hit points.", amount);
        }
    }
}

namespace Realmkeeper.Domain.Combat
{
    using System;
    using System.Collections.Generic;
    using Realmkeeper.Domain.Magic;
    using Realmkeeper.Domain.Rpg;

    public sealed partial class CombatEngine
    {
        /// <summary>
        /// A saving throw against a DC: the save bonus (proficiency included), roll modes from features, conditions and
        /// effects (Haste, rage, Heightened Spell), exhaustion (−2 per level), Bless and Bane dice, the Aura of Protection,
        /// Bardic Inspiration, Indomitable, and automatic failures (Strength and Dexterity saves while paralyzed,
        /// petrified, stunned or unconscious).
        /// </summary>
        public SaveResult SavingThrow(CombatantData c, Ability ability, int dc, string against, bool disadvantage = false, string advantage = null)
        {
            var s = Stats(c);
            var r = new SaveResult { Ability = ability, Dc = dc, Bonus = s.SaveBonus(ability) };
            if ((ability == Ability.Strength || ability == Ability.Dexterity) &&
                (Has(c, ConditionType.Paralyzed) || Has(c, ConditionType.Petrified) || Has(c, ConditionType.Stunned) || Has(c, ConditionType.Unconscious)))
            {
                r.AutomaticFailure = "fails Strength and Dexterity saves automatically while " + (Has(c, ConditionType.Unconscious) ? "unconscious" : Has(c, ConditionType.Paralyzed) ? "paralyzed" : Has(c, ConditionType.Petrified) ? "petrified" : "stunned");
                r.Roll = new D20Roll();
                r.Success = false;
                Log(CombatLogKind.Save, c.CombatantId, default, $"{c.Name} {r.AutomaticFailure} (DC {dc} {ability}, against {against}).");
                return r;
            }

            var roll = new D20Roll();
            var modes = s.RollModes("save:" + ability);
            roll.AdvantageSources.AddRange(modes.Advantage);
            roll.DisadvantageSources.AddRange(modes.Disadvantage);
            if (ability == Ability.Dexterity && Has(c, ConditionType.Restrained)) roll.DisadvantageSources.Add("restrained");
            if (ability == Ability.Dexterity && c.Turn.Dodging && !Incapacitated(c)) roll.AdvantageSources.Add("dodging");
            if (ability == Ability.Dexterity && HasEffect(c, CombatEffectKind.AdvantageOnDexteritySaves)) roll.AdvantageSources.Add("Haste");
            if (ability == Ability.Strength && HasEffect(c, CombatEffectKind.Raging)) roll.AdvantageSources.Add("Rage");
            if (disadvantage) roll.DisadvantageSources.Add("Heightened Spell");
            if (advantage != null) roll.AdvantageSources.Add(advantage);
            Roll(roll);
            r.Roll = roll;
            var notes = new List<string>();
            var aura = AuraOfProtection(c);
            if (aura > 0) notes.Add($"Aura of Protection +{aura}");
            r.Total = roll.Natural + r.Bonus.Total - 2 * ExhaustionLevel(c) + RollDieEffects(c, notes) + aura;
            r.Success = r.Total >= dc;
            if (!r.Success)
            {
                r.Total += SpendInspiration(c, r.Total, dc, notes);
                r.Success = r.Total >= dc;
            }

            if (!r.Success && TargetFeature(c, "indomitable") && ResourceLeft(c, "indomitable") > 0)
            {
                // Indomitable (taken automatically): reroll the failed save with a bonus equal to the fighter level.
                SpendResource(c, "indomitable", 1);
                var again = Die(20);
                var level = s.Rules.Level;
                r.Total = again + r.Bonus.Total - 2 * ExhaustionLevel(c) + level + aura;
                r.Success = r.Total >= dc;
                notes.Add($"Indomitable: rerolled {again} +{level}");
            }

            Log(CombatLogKind.Save, c.CombatantId, default,
                $"{c.Name}'s {ability} save against {against}: d20 {roll.Natural}{(roll.Advantage || roll.Disadvantage ? $" ({roll.First}/{roll.Second}, {roll.ModeText})" : string.Empty)} {Breakdown.Signed(r.Bonus.Total)}{(notes.Count > 0 ? " " + string.Join(" ", notes) : string.Empty)} = {r.Total} vs DC {dc}: {(r.Success ? "success" : "failure")}.", r.Total, roll);
            return r;
        }

        /// <summary>The Aura of Protection that reaches a creature: a conscious allied paladin within 10 feet adds their Charisma modifier (at least 1).</summary>
        private int AuraOfProtection(CombatantData c)
        {
            var best = 0;
            foreach (var p in Encounter.Combatants)
            {
                if (p.State != VitalState.Conscious || p.Side != c.Side || Incapacitated(p) || !TargetFeature(p, "aura_of_protection")) continue;
                var reach = TargetFeature(p, "aura_expansion") ? 30 : 10;
                if (p.Position.DistanceFeet(c.Position) > reach) continue;
                best = Math.Max(best, Math.Max(1, Stats(p).Modifier(Ability.Charisma)));
            }

            return best;
        }
    }
}
