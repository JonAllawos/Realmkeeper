using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 9: effects in force (spell and feature effects), read live by the rules: armour class, speed, attack and
    /// save modifiers, extra damage, resistances, reactions. Effects expire on their timer, with their concentration,
    /// or when the fight ends. Concentration is one use at a time: a new one ends the old; damage forces a
    /// Constitution save (DC 10 or half the damage, at most 30); incapacity ends it. Statistics are cached per command
    /// (<see cref="Stats"/>); these modifiers are not, so a buff applied mid-command counts at once.
    /// </summary>
    public sealed partial class CombatEngine
    {
        public IEnumerable<ActiveEffectData> EffectsOn(CombatantData c)
        {
            foreach (var x in Encounter.Effects) if (x.TargetId == c.CombatantId) yield return x;
        }

        public bool HasEffect(CombatantData c, CombatEffectKind kind)
        {
            foreach (var x in Encounter.Effects) if (x.TargetId == c.CombatantId && x.Kind == kind) return true;
            return false;
        }

        private ActiveEffectData FirstEffect(CombatantData c, CombatEffectKind kind)
        {
            foreach (var x in Encounter.Effects) if (x.TargetId == c.CombatantId && x.Kind == kind) return x;
            return null;
        }

        private int NewInstance() => Encounter.NextInstanceId++;

        private ActiveEffectData AddEffect(int instance, string origin, string name, CombatantData source, CombatantData target, CombatEffectKind kind, int value = 0,
            Dice dice = default, DamageType type = default, int spellLevel = 0, bool concentration = false, EffectExpiry expiry = EffectExpiry.None, CombatantData expiresOn = null,
            int expiresRound = 0, bool singleUse = false, CombatantData related = null)
        {
            var e = new ActiveEffectData
            {
                Id = Encounter.Effects.Count == 0 ? 1 : Encounter.Effects[Encounter.Effects.Count - 1].Id + 1, InstanceId = instance, Origin = origin, Name = name,
                SourceId = source?.CombatantId ?? default, TargetId = target.CombatantId, RelatedId = related?.CombatantId ?? default, Kind = kind, Value = value,
                DiceCount = dice.Count, DiceSides = dice.Sides, DamageType = type, SpellLevel = spellLevel, Concentration = concentration, Expiry = expiry,
                ExpiresOn = expiresOn?.CombatantId ?? default, ExpiresRound = expiresRound, SingleUse = singleUse,
            };
            Encounter.Effects.Add(e);
            return e;
        }

        private void RemoveEffect(ActiveEffectData e, string reason)
        {
            if (!Encounter.Effects.Remove(e)) return;
            var target = Encounter.Find(e.TargetId);
            if (e.Kind == CombatEffectKind.HitPointMaximumBonus && target != null && target.Kind != CombatantKind.Player)
            {
                target.MaxHp = Math.Max(1, target.MaxHp - e.Value);
                target.CurrentHp = Math.Min(target.CurrentHp, target.MaxHp);
            }

            if (e.Kind == CombatEffectKind.WildShapeForm && target != null) target.FormCreatureId = null;
            if (target != null && !e.SingleUse) Log(CombatLogKind.Effect, e.SourceId, e.TargetId, $"{e.Name} on {target.Name} ends ({reason}).");
        }

        /// <summary>Ends every effect and condition a spell or feature use applied.</summary>
        private void EndInstance(int instance, string reason)
        {
            if (instance <= 0) return;
            for (var i = Encounter.Effects.Count - 1; i >= 0; i--)
                if (i < Encounter.Effects.Count && Encounter.Effects[i].InstanceId == instance) RemoveEffect(Encounter.Effects[i], reason);
            foreach (var c in Encounter.Combatants)
                for (var i = c.Conditions.Count - 1; i >= 0; i--)
                    if (c.Conditions[i].InstanceId == instance) RemoveCondition(c, c.Conditions[i].Type, reason);
        }

        // ===== Concentration ===============================================================================

        private void StartConcentration(CombatantData c, int instance, string spellId, string name)
        {
            if (c.Concentration != null) EndConcentration(c, $"{c.Name} began concentrating on {name}");
            c.Concentration = new ConcentrationData { InstanceId = instance, SpellId = spellId, Name = name, StartedRound = Encounter.Round };
            Log(CombatLogKind.Effect, c.CombatantId, default, $"{c.Name} concentrates on {name}.");
        }

        public void EndConcentration(CombatantData c, string reason)
        {
            var conc = c.Concentration;
            if (conc == null) return;
            c.Concentration = null;
            Log(CombatLogKind.Effect, c.CombatantId, default, $"{c.Name} stops concentrating on {conc.Name} ({reason}).");
            EndInstance(conc.InstanceId, "concentration ended");
        }

        /// <summary>After damage: a Constitution save to keep concentrating (DC 10 or half the damage, up to 30).</summary>
        private void ConcentrationCheck(CombatantData c, int damage)
        {
            if (c.Concentration == null || damage <= 0) return;
            if (c.State != VitalState.Conscious || Incapacitated(c)) { EndConcentration(c, "incapacitated"); return; }
            var dc = Math.Min(30, Math.Max(10, damage / 2));
            var save = SavingThrow(c, Ability.Constitution, dc, "losing concentration on " + c.Concentration.Name, false, HasInvocation(c, "eldritch_mind") ? "Eldritch Mind" : null);
            if (!save.Success) EndConcentration(c, $"failed a DC {dc} Constitution save after {damage} damage");
        }

        // ===== Expiry ======================================================================================

        private void ExpireEffects(EffectExpiry when, CombatantData owner)
        {
            for (var i = Encounter.Effects.Count - 1; i >= 0; i--)
            {
                if (i >= Encounter.Effects.Count) continue;
                var e = Encounter.Effects[i];
                if (e.Expiry == when && e.ExpiresOn == owner.CombatantId && Encounter.Round >= e.ExpiresRound) RemoveEffect(e, "its time ran out");
            }

            // A timed spell's whole duration: its concentration ends with it.
            if (when == EffectExpiry.StartOfTurn && owner.Concentration != null)
            {
                var anyLeft = false;
                foreach (var e in Encounter.Effects) if (e.InstanceId == owner.Concentration.InstanceId) anyLeft = true;
                foreach (var c in Encounter.Combatants) foreach (var k in c.Conditions) if (k.InstanceId == owner.Concentration.InstanceId) anyLeft = true;
                if (!anyLeft) EndConcentration(owner, "nothing left to sustain");
            }
        }

        /// <summary>End of a creature's turn: repeated saves against spell conditions (Hold Person).</summary>
        private void RepeatSaves(CombatantData c)
        {
            for (var i = c.Conditions.Count - 1; i >= 0; i--)
            {
                if (i >= c.Conditions.Count) continue;
                var k = c.Conditions[i];
                if (!k.RepeatSave || k.SaveDc <= 0) continue;
                var save = SavingThrow(c, k.SaveAbility, k.SaveDc, "repeating the save against " + (string.IsNullOrEmpty(k.Reason) ? Describe(k.Type) : k.Reason));
                if (save.Success) RemoveCondition(c, k.Type, "saved at the end of its turn");
            }
        }

        // ===== Modifiers read by the rules =================================================================

        /// <summary>Armour class now: the statistics plus spell and feature effects (Shield of Faith, Haste, Mage Armour).</summary>
        public Breakdown ArmorClass(CombatantData c)
        {
            var b0 = Stats(c).ArmorClass;
            var b = new Breakdown();
            foreach (var (label, value) in b0.Parts) b.Add(label, value);
            foreach (var n in b0.Notes) b.Note(n);
            foreach (var e in EffectsOn(c))
            {
                if (e.Kind == CombatEffectKind.ArmorClassBonus) b.Add(e.Name, e.Value);
                if (e.Kind == CombatEffectKind.ArmorClassBase)
                {
                    var unarmoured = c.Kind != CombatantKind.Player || _world.Player.Equipped(EquipmentSlot.Chest) == null || ItemCatalog.Find(_world.Player.Equipped(EquipmentSlot.Chest).ItemId)?.Armor == null;
                    var with = e.Value + Stats(c).Modifier(Ability.Dexterity);
                    if (unarmoured && with > b0.Total) b.Add(e.Name + $" (base {e.Value} + Dex instead of {b0.Total})", with - b0.Total);
                }
            }

            return b;
        }

        /// <summary>Dice from Bless (+) or Bane (−) on an attack roll or saving throw; logged in the roll's explanation.</summary>
        private int RollDieEffects(CombatantData c, List<string> notes)
        {
            var total = 0;
            foreach (var e in new List<ActiveEffectData>(EffectsOn(c)))
            {
                if (e.Kind != CombatEffectKind.AttackAndSaveDie) continue;
                var r = 0;
                for (var i = 0; i < Math.Max(1, e.DiceCount); i++) r += Die(e.DiceSides);
                var signed = e.Value < 0 ? -r : r;
                total += signed;
                notes.Add($"{e.Name} {(signed >= 0 ? "+" : string.Empty)}{signed}");
            }

            return total;
        }

        /// <summary>Uses a held Bardic Inspiration die when it turns a failure into a success; returns what it added.</summary>
        private int SpendInspiration(CombatantData c, int total, int needed, List<string> notes)
        {
            if (total >= needed) return 0;
            var e = FirstEffect(c, CombatEffectKind.InspirationDie);
            if (e == null || total + e.DiceSides < needed) return 0;
            var r = Die(e.DiceSides);
            Encounter.Effects.Remove(e);
            notes.Add($"Bardic Inspiration +{r}");
            return r;
        }

        private bool ConsumeNextAttackAdvantage(CombatantData attacker, CombatantData target)
        {
            foreach (var e in new List<ActiveEffectData>(EffectsOn(attacker)))
                if (e.Kind == CombatEffectKind.NextAttackAdvantage && (e.RelatedId.IsEmpty || e.RelatedId == target.CombatantId))
                {
                    Encounter.Effects.Remove(e);
                    return true;
                }

            return false;
        }

        private bool ConsumeEffectOn(CombatantData c, CombatEffectKind kind)
        {
            var e = FirstEffect(c, kind);
            if (e == null) return false;
            Encounter.Effects.Remove(e);
            return true;
        }

        /// <summary>Ends effects that end when their holder acts against someone (Invisibility).</summary>
        private void HostileActBy(CombatantData c)
        {
            var ending = new List<int>();
            foreach (var e in EffectsOn(c)) if (e.Kind == CombatEffectKind.EndsOnHostileAct) ending.Add(e.InstanceId);
            foreach (var instance in ending)
            {
                for (var i = Encounter.Effects.Count - 1; i >= 0; i--)
                    if (Encounter.Effects[i].InstanceId == instance && Encounter.Effects[i].TargetId == c.CombatantId) RemoveEffect(Encounter.Effects[i], "it acted against someone");
                for (var i = c.Conditions.Count - 1; i >= 0; i--)
                    if (c.Conditions[i].InstanceId == instance) RemoveCondition(c, c.Conditions[i].Type, "it attacked or cast a spell");
            }
        }

        private int ResourceLeft(CombatantData c, string id)
        {
            var map = c.Kind == CombatantKind.Player ? _world.Player.Rpg.Resources : c.Resources;
            return map.TryGetValue(id, out var n) ? n : 0;
        }

        private void SpendResource(CombatantData c, string id, int amount)
        {
            var map = c.Kind == CombatantKind.Player ? _world.Player.Rpg.Resources : c.Resources;
            map[id] = Math.Max(0, (map.TryGetValue(id, out var n) ? n : 0) - amount);
        }
    }
}

namespace Realmkeeper.Domain.Combat
{
    using Realmkeeper.Domain.Magic;

    public sealed partial class CombatEngine
    {
        /// <summary>The end of a creature's turn: repeated saves, timed conditions and effects, and a rage that was not kept up.</summary>
        private void EndTurn(CombatantData c)
        {
            if (c.State == VitalState.Conscious) RepeatSaves(c);
            var rage = FirstEffect(c, CombatEffectKind.Raging);
            if (rage != null && !c.Turn.AttackedOrForcedSave) EndInstance(rage.InstanceId, "no attack, forced save or extension this turn");
            Expire(ConditionDuration.EndOfTurn, c);
            ExpireEffects(EffectExpiry.EndOfTurn, c);
        }

        /// <summary>When the fight ends, every spell and feature effect ends with it (durations are encounter time only).</summary>
        private void EndAllEffects()
        {
            foreach (var c in Encounter.Combatants)
            {
                c.Concentration = null;
                c.FormCreatureId = null;
                for (var i = c.Conditions.Count - 1; i >= 0; i--) if (c.Conditions[i].InstanceId > 0) c.Conditions.RemoveAt(i);
            }

            foreach (var e in new System.Collections.Generic.List<ActiveEffectData>(Encounter.Effects))
                if (e.Kind == CombatEffectKind.HitPointMaximumBonus) RemoveEffect(e, "the fight ended");
            Encounter.Effects.Clear();
        }
    }
}
