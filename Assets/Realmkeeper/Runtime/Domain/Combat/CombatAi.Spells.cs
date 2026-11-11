using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// The baseline caster (Phase 9): scores the spells it has ready, deterministically, and proposes the best one when
    /// it beats its weapon — the engine still validates everything. In order: heal a dying or badly hurt ally (a bonus
    /// action spell first); bless allies when not concentrating; then damage or control, counting each foe an area would
    /// catch and each ally it would hurt; a slot is spent only when it beats the best cantrip or attack by half again,
    /// never on a foe a cantrip would finish, and never to replace a concentration spell already running.
    /// </summary>
    public static partial class CombatAi
    {
        private static double Average(Dice d, int count) => count * (d.Sides + 1) / 2.0 + d.Bonus;

        private static double ExpectedDamage(CombatEngine engine, CombatantData actor, SpellDefinition s, int level)
        {
            var total = 0.0;
            var tier = 1 + (engine.Stats(actor).CasterLevel >= 5 ? 1 : 0) + (engine.Stats(actor).CasterLevel >= 11 ? 1 : 0) + (engine.Stats(actor).CasterLevel >= 17 ? 1 : 0);
            foreach (var e in s.Effects)
            {
                if (e.Kind != CombatEffectKind.Damage) continue;
                var n = e.Dice.Count;
                if (s.IsCantrip && s.CantripScaling && !s.ScalesByInstances) n *= tier;
                if (!s.IsCantrip && level > s.Level) n += e.PerSlotAbove.Count * (level - s.Level);
                total += Average(e.Dice, n);
            }

            var instances = s.ScalesByInstances ? tier : s.Instances + (s.IsCantrip ? 0 : Math.Max(0, level - s.Level) * s.InstancesPerSlotAbove);
            var hit = s.Resolution == SpellResolution.Attack ? 0.6 : s.Resolution == SpellResolution.Save ? 0.65 : 1.0;
            return total * Math.Max(1, instances) * hit;
        }

        private static CastSpellAction SpellChoice(CombatEngine engine, CombatantData actor, List<CombatantData> foes, CombatAiProfile profile)
        {
            var spells = engine.SpellsOf(actor);
            if (spells.Count == 0) return null;
            var concentrating = actor.Concentration != null;

            int LowestLevel(SpellDefinition s)
            {
                if (s.IsCantrip) return 0;
                var levels = engine.CastLevels(actor, s);
                return levels.Count > 0 ? levels[0] : -1;
            }

            CastSpellAction Valid(CastSpellAction a) => engine.SpellProblem(actor, a) == null ? a : null;

            // 1. Healing: a dying ally, or one at a quarter of its hit points or less.
            CombatantData hurt = null;
            foreach (var c in engine.Encounter.Combatants)
            {
                if (c.Side != actor.Side || c.State == VitalState.Dead || c.State == VitalState.Fled) continue;
                var low = c.State == VitalState.Dying || engine.Hp(c) * 4 <= engine.MaxHp(c);
                if (low && (hurt == null || engine.Hp(c) < engine.Hp(hurt) || engine.Hp(c) == engine.Hp(hurt) && c.CombatantId.CompareTo(hurt.CombatantId) < 0)) hurt = c;
            }

            if (hurt != null)
            {
                var heals = spells.FindAll(s => s.Heals && s.Castable);
                heals.Sort((a, b) => a.CastingTime != b.CastingTime ? (a.CastingTime == CastingTime.BonusAction ? -1 : 1) : a.Level.CompareTo(b.Level));
                foreach (var s in heals)
                {
                    var level = LowestLevel(s);
                    if (level < 0) continue;
                    var a = Valid(new CastSpellAction(s.Id, level, new[] { hurt.CombatantId }));
                    if (a != null) return a;
                }
            }

            // 2. A blessing on allies, when not already concentrating.
            if (!concentrating)
                foreach (var s in spells)
                {
                    if (!s.Castable || !s.Concentration || s.Affects != SpellAffects.Ally || s.Targeting != SpellTargeting.Creatures || s.MaxTargets < 2) continue;
                    var level = LowestLevel(s);
                    if (level < 0) continue;
                    var allies = new List<Realmkeeper.Domain.Identifiers.CombatantId>();
                    foreach (var c in engine.Encounter.Combatants)
                        if (c.Side == actor.Side && c.State == VitalState.Conscious && actor.Position.DistanceFeet(c.Position) <= s.Range && allies.Count < engine.MaxTargets(s, level, false)) allies.Add(c.CombatantId);
                    if (allies.Count >= 2 && foes.Count > 0)
                    {
                        var a = Valid(new CastSpellAction(s.Id, level, allies));
                        if (a != null) return a;
                    }
                }

            // 3. Damage or control against the foes.
            var weapon = Math.Max(engine.Stats(actor).BestMelee() is AttackOption m ? CombatStats.Average(m) : 0, engine.Stats(actor).BestRanged() is AttackOption r ? CombatStats.Average(r) : 0)
                         * engine.Stats(actor).AttacksPerAction * 0.6;
            var cantripBest = 0.0;
            foreach (var s in spells) if (s.IsCantrip && s.DealsDamage) cantripBest = Math.Max(cantripBest, ExpectedDamage(engine, actor, s, 0));
            var baseline = Math.Max(weapon, cantripBest);

            CastSpellAction best = null;
            var bestValue = 0.0;
            foreach (var s in spells)
            {
                if (!s.Castable || s.Affects == SpellAffects.Ally || s.AfterMeleeHit) continue;
                if (s.Concentration && concentrating) continue;
                var level = LowestLevel(s);
                if (level < 0) continue;
                var damage = ExpectedDamage(engine, actor, s, level);
                var control = 0.0;
                foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.Condition) control = 12;
                foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.AttackAndSaveDie) control = 6;
                foreach (var foe in foes)
                {
                    CastSpellAction a;
                    double value;
                    if (s.HasArea)
                    {
                        a = new CastSpellAction(s.Id, level, null, foe.Position);
                        var caught = engine.AreaTargets(actor, s, foe.Position);
                        var enemies = 0;
                        var friends = 0;
                        foreach (var c in caught) if (CombatEngine.Hostile(actor, c)) enemies++; else if (c.Side == actor.Side) friends++;
                        if (enemies == 0) continue;
                        value = (damage + control) * enemies - (damage + control) * friends * 1.5;
                    }
                    else
                    {
                        a = new CastSpellAction(s.Id, level, new[] { foe.CombatantId });
                        value = damage + control;
                        if (!s.IsCantrip && engine.Hp(foe) <= cantripBest) value = 0; // never a slot on a foe a cantrip would finish
                    }

                    if (!s.IsCantrip && value < baseline * 1.5) continue;
                    if (value <= bestValue || Valid(a) == null) continue;
                    best = a;
                    bestValue = value;
                }
            }

            return bestValue > 0 && (bestValue >= weapon || best != null && SpellCatalog.Find(best.SpellId).IsCantrip && bestValue >= weapon) ? best : null;
        }
    }
}
