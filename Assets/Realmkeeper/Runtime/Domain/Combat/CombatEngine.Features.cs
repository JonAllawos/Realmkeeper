using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Class features in combat (Phase 9, ADR-0039): one validated activation path for every active feature (cost,
    /// resource, target, range, preconditions), resolved through the same action economy, attacks, saves, healing,
    /// conditions and effects as everything else. Passive features are applied inside those routines.
    /// </summary>
    public sealed partial class CombatEngine
    {
        private static readonly int[] FontSlotCost = { 0, 2, 3, 5, 6, 7 };

        private void UseAction(CombatantData actor)
        {
            actor.Turn.ActionUsed = true;
            actor.Turn.SurgeActionPending = false;
        }

        private void ForgetStats(CombatantData c) => _stats.Remove(c.CombatantId);

        public bool RageResists(CombatantData c, DamageType t) =>
            HasEffect(c, CombatEffectKind.Raging) && (t == DamageType.Bludgeoning || t == DamageType.Piercing || t == DamageType.Slashing);

        private void EndOwnEffects(CombatantData c, CombatEffectKind kind, string reason)
        {
            var instances = new List<int>();
            foreach (var e in EffectsOn(c)) if (e.Kind == kind && !instances.Contains(e.InstanceId)) instances.Add(e.InstanceId);
            foreach (var i in instances) EndInstance(i, reason);
            if (kind == CombatEffectKind.WildShapeForm && instances.Count > 0) ForgetStats(c);
        }

        private bool HasClassFeature(CombatantData c, string id) => Stats(c).Rules != null && Stats(c).Rules.HasFeature(id);

        /// <summary>The active features a combatant has now, each with why it is unavailable (null when it can be used).</summary>
        public List<(CombatFeatureDefinition feature, string unavailable)> FeaturesOf(CombatantData c)
        {
            var list = new List<(CombatFeatureDefinition, string)>();
            if (Stats(c).Rules == null && !HasBonusDash(c)) return list;
            foreach (var f in CombatFeatureCatalog.All)
            {
                if (!f.IsActive || (f.RequiresFeature == null ? !HasBonusDash(c) : !HasClassFeature(c, f.RequiresFeature))) continue;
                if (f.Id == "rage_extend" && !HasEffect(c, CombatEffectKind.Raging)) continue;
                if (f.Id == "leave_wild_shape" && c.FormCreatureId == null) continue;
                list.Add((f, FeatureProblem(c, new UseFeatureAction(f.Id, default, f.Options.Count > 0 ? f.Options[0].id : null))));
            }

            return list;
        }

        /// <summary>Phase 8 compatibility: the bonus-action features (Second Wind, Cunning Action …) as (id, name, unavailable).</summary>
        public IReadOnlyList<(string id, string name, string unavailable)> BonusAbilities(CombatantData actor)
        {
            var list = new List<(string, string, string)>();
            foreach (var (f, why) in FeaturesOf(actor))
                if (f.Activation == FeatureActivation.BonusAction && f.Targeting == FeatureTargeting.Self && f.Options.Count == 0)
                {
                    var name = f.Name;
                    if (f.Id == "second_wind") name = $"Second Wind (heal 1d10 + {Stats(actor).Rules.Level}; {ResourceLeft(actor, "second_wind")} left)";
                    list.Add((f.Id, name, why));
                }

            return list;
        }

        /// <summary>Why a feature cannot be used now (null when it can).</summary>
        public string FeatureProblem(CombatantData actor, UseFeatureAction a)
        {
            var f = CombatFeatureCatalog.Find(a.FeatureId);
            if (f == null || !f.IsActive) return "That feature is not something you use.";
            if (f.RequiresFeature == null ? !HasBonusDash(actor) : !HasClassFeature(actor, f.RequiresFeature)) return $"{f.Name} is not available to {actor.Name}.";
            if (Incapacitated(actor)) return $"{actor.Name} is incapacitated.";
            var t = actor.Turn;
            if (f.Activation == FeatureActivation.Action && t.ActionUsed) return "The action this turn is used.";
            if (f.Activation == FeatureActivation.BonusAction && t.BonusActionUsed) return "The bonus action this turn is used.";
            if (f.ResourceId != null && f.ResourceCost > 0 && ResourceLeft(actor, f.ResourceId) < f.ResourceCost)
                return $"No {Stats(actor).Rules.FindResource(f.ResourceId)?.Name ?? f.ResourceId} left.";

            CombatantData target = null;
            if (f.Targeting == FeatureTargeting.Ally || f.Targeting == FeatureTargeting.Enemy || f.Targeting == FeatureTargeting.Either)
            {
                target = a.Target.IsEmpty ? (f.Targeting == FeatureTargeting.Ally ? actor : null) : Encounter.Find(a.Target);
                if (target == null) return "Choose a target.";
                if (target.State == VitalState.Dead || target.State == VitalState.Fled) return $"{target.Name} is out of the fight.";
                var enemyWanted = f.Targeting == FeatureTargeting.Enemy || f.Targeting == FeatureTargeting.Either && a.Option == "harm";
                if (enemyWanted && !Hostile(actor, target)) return $"{target.Name} is not hostile.";
                if (!enemyWanted && target != actor && target.Side != actor.Side) return $"{target.Name} is not an ally.";
                var range = f.Range > 0 ? f.Range : Stats(actor).MeleeReach;
                if (actor.Position.DistanceFeet(target.Position) > range) return $"{target.Name} is {actor.Position.DistanceFeet(target.Position)} ft away; {f.Name} reaches {range} ft.";
            }

            switch (f.Id)
            {
                case "rage":
                    if (HasEffect(actor, CombatEffectKind.Raging)) return "Already raging.";
                    if (Stats(actor).Rules.HeavyArmorWorn) return "No rage in heavy armour.";
                    return null;
                case "reckless_attack":
                    if (t.Reckless) return "Already attacking recklessly.";
                    return t.AttackActionTaken ? "Decide before your first attack this turn." : null;
                case "bardic_inspiration":
                    return target == actor ? "Inspire someone else." : HasEffect(target, CombatEffectKind.InspirationDie) ? $"{target.Name} already holds an inspiration die." : null;
                case "divine_spark":
                    return a.Option == "heal" || a.Option == "harm" ? null : "Choose to heal or to harm.";
                case "wild_shape":
                    return actor.FormCreatureId != null ? "Already in a beast form." : null;
                case "action_surge":
                    if (t.ActionSurgeUsed) return "Action Surge is once per turn.";
                    return !t.ActionUsed ? "Use your action first; Action Surge gives you another." : null;
                case "martial_arts_strike":
                    return !t.AttackActionTaken ? "Take the Attack action first." : null;
                case "patient_defense":
                case "step_of_the_wind":
                    return a.Option == "focus" && ResourceLeft(actor, "focus_points") < 1 ? "No focus points left." : null;
                case "stunning_strike":
                {
                    if (t.StunningStrikeUsed) return "Stunning Strike is once per turn.";
                    var hit = Encounter.Find(t.LastHitTargetId);
                    if (hit == null || !t.LastHitMonkWeapon) return "Hit a creature with an unarmed strike or monk weapon this turn first.";
                    return hit.State != VitalState.Conscious ? $"{hit.Name} is already down." : null;
                }

                case "lay_on_hands":
                {
                    var pool = ResourceLeft(actor, "lay_on_hands");
                    if (pool <= 0) return "Your Lay on Hands pool is empty.";
                    var missing = MaxHp(target) - Hp(target);
                    if (a.Amount == 0 && missing <= 0) return $"{target.Name} is not hurt.";
                    if (a.Amount < 0 || a.Amount > pool) return $"Spend 1–{pool} hit points from the pool.";
                    return null;
                }

                case "sacred_weapon":
                    return HasEffect(actor, CombatEffectKind.WeaponAttackBonus) ? "Your weapon is already blessed." : null;
                case "steady_aim":
                    return t.MovementUsed > 0 ? "Steady Aim needs you not to have moved this turn." : null;
                case "innate_sorcery":
                    return HasEffect(actor, CombatEffectKind.InnateSorcery) ? "Innate Sorcery is already surging." : null;
                case "font_create_slot":
                case "font_convert_slot":
                {
                    if (!int.TryParse(a.Option, out var level) || level < 1 || level > 5) return "Choose a slot level from 1 to 5.";
                    var access = Access(actor);
                    var id = "spell_slots_" + level;
                    var max = Stats(actor).Rules.ResourceMax(Stats(actor).Rules.FindResource(id) ?? new ResourceDefinition { Id = id });
                    var spMax = Stats(actor).Rules.ResourceMax(Stats(actor).Rules.FindResource("sorcery_points"));
                    if (f.Id == "font_create_slot")
                    {
                        if (ResourceLeft(actor, "sorcery_points") < FontSlotCost[level]) return $"A level {level} slot costs {FontSlotCost[level]} sorcery points.";
                        if (access == null || access.SlotsLeft(level) >= max) return $"Your level {level} slots are full (a created slot refills a spent one).";
                        return null;
                    }

                    if (access == null || access.SlotsLeft(level) < 1) return $"No level {level} slot to convert.";
                    return ResourceLeft(actor, "sorcery_points") + level > spMax ? $"That would take your sorcery points above {spMax}." : null;
                }
            }

            return null;
        }

        private void DoFeature(CombatantData actor, UseFeatureAction a)
        {
            var f = CombatFeatureCatalog.Find(a.FeatureId);
            var t = actor.Turn;
            var rules = Stats(actor).Rules;
            if (f.Activation == FeatureActivation.Action) UseAction(actor);
            else if (f.Activation == FeatureActivation.BonusAction) t.BonusActionUsed = true;
            if (f.ResourceId != null && f.ResourceCost > 0) SpendResource(actor, f.ResourceId, f.ResourceCost);
            var target = a.Target.IsEmpty ? actor : Encounter.Find(a.Target);
            var instance = NewInstance();
            var origin = "feature:" + f.Id;
            void Say(string text) => Log(CombatLogKind.Feature, actor.CombatantId, target?.CombatantId ?? default, text);

            switch (f.Id)
            {
                case "second_wind":
                {
                    var d = Die(10);
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} uses Second Wind: d10 {d} + level {rules.Level}.", d + rules.Level, new D20Roll { First = d, Natural = d });
                    Heal(actor, d + rules.Level, "Second Wind");
                    break;
                }

                case "spell_dash":
                case "cunning_dash":
                    t.MovementGranted += Speed(actor);
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} dashes as a bonus action ({f.Name}).");
                    break;
                case "cunning_disengage":
                    t.Disengaged = true;
                    Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} disengages as a bonus action (Cunning Action).");
                    break;

                case "rage":
                    EndConcentration(actor, "raging");
                    AddEffect(instance, origin, "Rage", actor, actor, CombatEffectKind.Raging, rules.Scaling("rage_damage"));
                    t.AttackedOrForcedSave = true;
                    Say($"{actor.Name} rages: +{rules.Scaling("rage_damage")} damage on Strength attacks, resistance to bludgeoning, piercing and slashing, advantage on Strength saves.");
                    break;
                case "rage_extend":
                    t.AttackedOrForcedSave = true;
                    Say($"{actor.Name} keeps the rage burning.");
                    break;
                case "reckless_attack":
                    t.Reckless = true;
                    AddEffect(instance, origin, "Reckless Attack", actor, actor, CombatEffectKind.RecklessDefender, expiry: EffectExpiry.StartOfTurn, expiresOn: actor, expiresRound: Encounter.Round + 1);
                    Say($"{actor.Name} attacks recklessly: advantage on Strength melee attacks this turn; attacks against them have advantage until their next turn.");
                    break;

                case "bardic_inspiration":
                {
                    var die = Math.Max(6, rules.Scaling("bardic_die"));
                    AddEffect(instance, origin, "Bardic Inspiration", actor, target, CombatEffectKind.InspirationDie, dice: new Dice(1, die), singleUse: true);
                    Say($"{actor.Name} inspires {target.Name}: a d{die} for a roll that would fail.");
                    break;
                }

                case "divine_spark":
                {
                    var n = 1 + (rules.Level >= 7 ? 1 : 0) + (rules.Level >= 13 ? 1 : 0) + (rules.Level >= 18 ? 1 : 0);
                    var dice = RollDice(n, 8, out var sum);
                    if (a.Option == "heal")
                    {
                        var amount = Math.Max(1, sum + Stats(actor).Modifier(Ability.Wisdom));
                        Say($"Divine Spark heals {target.Name}: {n}d8 [{dice}] + Wisdom = {amount}.");
                        Heal(target, amount, "Divine Spark");
                    }
                    else
                    {
                        actor.Turn.AttackedOrForcedSave = true;
                        var save = SavingThrow(target, Ability.Constitution, SpellSaveDc(actor), "Divine Spark");
                        var amount = save.Success ? sum / 2 : sum;
                        Say($"Divine Spark sears {target.Name}: {n}d8 [{dice}]{(save.Success ? ", halved on a save" : string.Empty)} = {amount} radiant.");
                        ApplyDamage(target, amount, DamageType.Radiant, false, actor.Name);
                    }

                    break;
                }

                case "preserve_life":
                {
                    var pool = 5 * rules.Level;
                    var given = new List<string>();
                    var allies = new List<CombatantData>();
                    foreach (var c in Encounter.Combatants)
                        if (c.Side == actor.Side && c.State != VitalState.Dead && c.State != VitalState.Fled && actor.Position.DistanceFeet(c.Position) <= 30 && Hp(c) <= MaxHp(c) / 2) allies.Add(c);
                    allies.Sort((x, y) => (Hp(x) * 1000 / Math.Max(1, MaxHp(x))).CompareTo(Hp(y) * 1000 / Math.Max(1, MaxHp(y))));
                    foreach (var c in allies)
                    {
                        if (pool <= 0) break;
                        var room = MaxHp(c) / 2 - Hp(c);
                        var amount = Math.Min(room, pool);
                        if (amount <= 0) continue;
                        pool -= amount;
                        Heal(c, amount, "Preserve Life");
                        given.Add($"{c.Name} +{amount}");
                    }

                    Say(given.Count == 0 ? "Preserve Life: no ally within 30 feet is bloodied." : "Preserve Life: " + string.Join(", ", given) + ".");
                    break;
                }

                case "wild_shape":
                {
                    actor.FormCreatureId = "wolf";
                    ForgetStats(actor);
                    AddEffect(instance, origin, "Wild Shape (wolf)", actor, actor, CombatEffectKind.WildShapeForm);
                    CharacterVitals.GrantTemporaryHitPoints(SheetOf(actor), rules.Level);
                    Say($"{actor.Name} takes the shape of a wolf and gains {rules.Level} temporary hit points.");
                    break;
                }

                case "leave_wild_shape":
                    EndOwnEffects(actor, CombatEffectKind.WildShapeForm, "returned to their own form");
                    Say($"{actor.Name} returns to their own form.");
                    break;

                case "action_surge":
                    t.ActionUsed = false;
                    t.AttacksLeft = 0;
                    t.ActionSurgeUsed = true;
                    t.SurgeActionPending = true;
                    Say($"{actor.Name} surges: one more action this turn.");
                    break;

                case "martial_arts_strike":
                    ResolveAttack(actor, target, Stats(actor).Find("unarmed"), false);
                    break;
                case "flurry_of_blows":
                    Say($"{actor.Name} unleashes a Flurry of Blows.");
                    for (var i = 0; i < 2 && target.State == VitalState.Conscious; i++) ResolveAttack(actor, target, Stats(actor).Find("unarmed"), false);
                    break;
                case "patient_defense":
                    t.Disengaged = true;
                    if (a.Option == "focus")
                    {
                        SpendResource(actor, "focus_points", 1);
                        t.Dodging = true;
                    }

                    Say($"{actor.Name} {(a.Option == "focus" ? "disengages and dodges (1 focus)" : "disengages")} with Patient Defence.");
                    break;
                case "step_of_the_wind":
                    t.MovementGranted += Speed(actor);
                    if (a.Option == "focus")
                    {
                        SpendResource(actor, "focus_points", 1);
                        t.Disengaged = true;
                    }

                    Say($"{actor.Name} {(a.Option == "focus" ? "dashes and disengages (1 focus)" : "dashes")} with Step of the Wind.");
                    break;
                case "stunning_strike":
                {
                    t.StunningStrikeUsed = true;
                    target = Encounter.Find(t.LastHitTargetId);
                    var dc = 8 + Stats(actor).Modifier(Ability.Wisdom) + Stats(actor).ProficiencyBonus;
                    t.AttackedOrForcedSave = true;
                    var save = SavingThrow(target, Ability.Constitution, dc, "Stunning Strike");
                    if (!save.Success)
                    {
                        ApplyCondition(target, ConditionType.Stunned, actor.CombatantId, "Stunning Strike", ConditionDuration.StartOfTurn, actor.CombatantId, Encounter.Round + 1);
                        foreach (var k in target.Conditions) if (k.Type == ConditionType.Stunned) k.InstanceId = instance;
                    }
                    else
                    {
                        AddEffect(instance, origin, "Stunning Strike (resisted)", actor, target, CombatEffectKind.SpeedChange, -Speed(target) / 2, expiry: EffectExpiry.StartOfTurn, expiresOn: actor, expiresRound: Encounter.Round + 1);
                        AddEffect(instance, origin, "Stunning Strike (resisted)", actor, target, CombatEffectKind.NextAttackAgainstHasAdvantage, singleUse: true, expiry: EffectExpiry.StartOfTurn, expiresOn: actor, expiresRound: Encounter.Round + 1);
                        Say($"{target.Name} shrugs off the stun: half speed, and the next attack against it has advantage.");
                    }

                    break;
                }

                case "wholeness_of_body":
                {
                    var die = Math.Max(6, rules.Scaling("martial_arts"));
                    var d = Die(die);
                    var amount = Math.Max(1, d + Stats(actor).Modifier(Ability.Wisdom));
                    Say($"Wholeness of Body: d{die} [{d}] + Wisdom = {amount}.");
                    Heal(actor, amount, "Wholeness of Body");
                    break;
                }

                case "lay_on_hands":
                {
                    var pool = ResourceLeft(actor, "lay_on_hands");
                    var amount = a.Amount > 0 ? a.Amount : Math.Min(pool, MaxHp(target) - Hp(target));
                    SpendResource(actor, "lay_on_hands", amount);
                    Say($"{actor.Name} lays hands on {target.Name}: {amount} from the pool ({pool - amount} left).");
                    Heal(target, amount, "Lay on Hands");
                    break;
                }

                case "sacred_weapon":
                {
                    var bonus = Math.Max(1, Stats(actor).Modifier(Ability.Charisma));
                    AddEffect(instance, origin, "Sacred Weapon", actor, actor, CombatEffectKind.WeaponAttackBonus, bonus);
                    t.SacredWeaponUsed = true;
                    Say($"{actor.Name}'s weapon blazes with holy light: +{bonus} to weapon attack rolls.");
                    break;
                }

                case "steady_aim":
                    AddEffect(instance, origin, "Steady Aim", actor, actor, CombatEffectKind.NextAttackAdvantage, singleUse: true, expiry: EffectExpiry.EndOfTurn, expiresOn: actor, expiresRound: Encounter.Round);
                    t.MovementUsed = Speed(actor) + t.MovementGranted;
                    Say($"{actor.Name} takes steady aim: advantage on the next attack; no more movement this turn.");
                    break;

                case "innate_sorcery":
                    AddEffect(instance, origin, "Innate Sorcery", actor, actor, CombatEffectKind.InnateSorcery, expiry: EffectExpiry.StartOfTurn, expiresOn: actor, expiresRound: Encounter.Round + 10);
                    Say($"{actor.Name}'s magic surges: +1 spell save DC and advantage on spell attacks for a minute.");
                    break;

                case "font_create_slot":
                {
                    var level = int.Parse(a.Option);
                    SpendResource(actor, "sorcery_points", FontSlotCost[level]);
                    var id = Access(actor).SlotResource(level);
                    SheetOf(actor).Resources[id] = ResourceLeft(actor, id) + 1;
                    Say($"{actor.Name} shapes {FontSlotCost[level]} sorcery points into a level {level} spell slot.");
                    break;
                }

                case "font_convert_slot":
                {
                    var level = int.Parse(a.Option);
                    SpendResource(actor, Access(actor).SlotResource(level), 1);
                    SheetOf(actor).Resources["sorcery_points"] = ResourceLeft(actor, "sorcery_points") + level;
                    Say($"{actor.Name} unravels a level {level} slot into {level} sorcery points.");
                    break;
                }
            }
        }

        /// <summary>Bonus Dash from Expeditious Retreat (an effect, offered with the other bonus actions).</summary>
        public bool HasBonusDash(CombatantData c) => HasEffect(c, CombatEffectKind.BonusDash);
    }
}
