using System;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 11 (ADR-0052): the player's actual objects in a fight — ammunition taken from the pack and half of it
    /// recovered afterwards, wear at its rule points (a natural 1 wears the weapon; a critical hit taken wears the
    /// armour, or the shield when no armour is worn), potions drunk as a bonus action, and wands spending charges to
    /// cast their spell through the spell engine. Only the player holds item instances; others' gear does not wear.
    /// </summary>
    public sealed partial class CombatEngine
    {
        private const string AmmoSpent = "ammo:";

        private PlayerCharacterData PlayerOf(CombatantData c) => c != null && c.Kind == CombatantKind.Player ? _world.Player : null;

        /// <summary>The pack stack this attack would shoot from, or null when there is none (or none is needed).</summary>
        internal ItemStackData AmmunitionLeft(CombatantData c, AttackOption o)
        {
            var p = PlayerOf(c);
            if (p == null || o.Ammunition == null) return null;
            foreach (var s in p.Inventory)
                if (s.Quantity > 0 && Holds(o.Ammunition, s.ItemId)) return s;
            return null;
        }

        private static bool Holds(System.Collections.Generic.IReadOnlyList<string> list, string id)
        {
            foreach (var x in list) if (x == id) return true;
            return false;
        }

        private void SpendAmmunition(CombatantData c, AttackOption o)
        {
            var stack = AmmunitionLeft(c, o);
            if (stack == null) return;
            var p = PlayerOf(c);
            stack.Quantity--;
            if (stack.Quantity <= 0) p.Inventory.Remove(stack);
            var key = AmmoSpent + stack.ItemId;
            c.Resources[key] = (c.Resources.TryGetValue(key, out var n) ? n : 0) + 1;
        }

        /// <summary>When the fight ends, half the ammunition shot (rounded down) is found again (SRD 5.2.1 Ammunition).</summary>
        private void ReturnAmmunition()
        {
            foreach (var c in Encounter.Combatants)
            {
                var p = PlayerOf(c);
                if (p == null) continue;
                foreach (var key in new System.Collections.Generic.List<string>(c.Resources.Keys))
                {
                    if (!key.StartsWith(AmmoSpent, StringComparison.Ordinal)) continue;
                    var itemId = key.Substring(AmmoSpent.Length);
                    var back = c.Resources[key] / 2;
                    c.Resources.Remove(key);
                    if (back <= 0 || ItemCatalog.Find(itemId) == null) continue;
                    ItemStackData into = null;
                    foreach (var s in p.Inventory) if (s.ItemId == itemId && s.Quantity < ItemCatalog.Find(itemId).MaxStack) into = s;
                    if (into != null) into.Quantity += back;
                    else
                    {
                        var seed = SeedMixer.Combine(SeedMixer.HashString("ammo-back"), Encounter.EncounterId + ":" + itemId);
                        var bytes = new byte[16];
                        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
                        BitConverter.GetBytes(SeedMixer.Combine(seed, "b")).CopyTo(bytes, 8);
                        p.Inventory.Add(new ItemStackData { StackId = new ItemStackId(new Guid(bytes)), ItemId = itemId, Quantity = back, Provenance = "Recovered after a fight" });
                    }

                    Log(CombatLogKind.Action, c.CombatantId, default, $"{c.Name} gathers {back} {ItemCatalog.Find(itemId).Name.ToLowerInvariant()} back after the fight.");
                }
            }
        }

        private void WearOnAttack(CombatantData attacker, CombatantData target, AttackOption o, AttackResult r)
        {
            if (r == null) return;
            var p = PlayerOf(attacker);
            if (p != null && o.Kind == AttackKind.Weapon && o.ItemId != null && r.Roll != null && r.Roll.Natural == 1)
            {
                var stack = p.Equipped(o.OffHand ? EquipmentSlot.OffHand : EquipmentSlot.MainHand);
                if (stack != null && stack.ItemId == o.ItemId) Wear(attacker, stack, "a natural 1");
            }

            var q = PlayerOf(target);
            if (q != null && r.Hit && r.Critical)
            {
                var armour = q.Equipped(EquipmentSlot.Chest);
                var shield = q.Equipped(EquipmentSlot.OffHand);
                if (armour != null && ItemCatalog.Find(armour.ItemId)?.Armor != null) Wear(target, armour, "a critical hit");
                else if (shield != null && ItemCatalog.Find(shield.ItemId)?.Armor?.Category == ArmorCategory.Shield) Wear(target, shield, "a critical hit");
            }
        }

        private void Wear(CombatantData c, ItemStackData stack, string cause)
        {
            var def = ItemCatalog.Find(stack.ItemId);
            if (def == null || !def.Wears || stack.Condition == ItemCondition.Broken) return;
            var before = stack.Condition;
            stack.Condition = ItemConditions.Worse(before);
            ForgetStats(c);
            Log(CombatLogKind.Effect, c.CombatantId, default,
                $"{c.Name}'s {def.Name.ToLowerInvariant()} wears from {cause}: {ItemConditions.Name(before)} → {ItemConditions.Name(stack.Condition)} ({ItemConditions.Effect(stack.Condition, def.Weapon != null)}).");
        }

        // ----- Using items --------------------------------------------------------------------------------

        public string UseItemProblem(CombatantData actor, UseItemAction a)
        {
            var p = PlayerOf(actor);
            if (p == null) return "Only the player uses items from a pack in a fight.";
            var stack = p.Inventory.Find(x => x.StackId == a.StackId);
            if (stack == null) return "That item is not in your pack.";
            var def = ItemCatalog.Find(stack.ItemId);
            if (def?.Consumable == null || def.Consumable.InCombat == null) return $"{def?.Name ?? "That"} cannot be used in a fight.";
            if (actor.Turn.BonusActionUsed) return "The bonus action this turn is used.";
            if (actor.State == VitalState.Dead) return $"{actor.Name} is dead.";
            return null;
        }

        private void DoUseItem(CombatantData actor, UseItemAction a)
        {
            var p = PlayerOf(actor);
            var stack = p.Inventory.Find(x => x.StackId == a.StackId);
            var def = ItemCatalog.Find(stack.ItemId);
            actor.Turn.BonusActionUsed = true;
            stack.Quantity--;
            if (stack.Quantity <= 0) p.Inventory.Remove(stack);
            var heal = def.Consumable.Healing;
            if (!heal.IsEmpty)
            {
                var total = heal.Bonus;
                var dice = new System.Collections.Generic.List<int>();
                for (var i = 0; i < heal.Count; i++)
                {
                    var d = Die(heal.Sides);
                    dice.Add(d);
                    total += d;
                }

                Log(CombatLogKind.Action, actor.CombatantId, default, $"{actor.Name} drinks a {def.Name}: {heal} [{string.Join(", ", dice)}] = {total} hit points.", total);
                Heal(actor, total, def.Name);
            }
        }

        // ----- Wands: casting from charges ----------------------------------------------------------------

        public const string ItemCast = "item:";

        /// <summary>The stack a free cast "item:{stack}" names, if the actor holds it (pack or equipped).</summary>
        private ItemStackData CastingItem(CombatantData actor, string freeWith)
        {
            var p = PlayerOf(actor);
            if (p == null || freeWith == null || !freeWith.StartsWith(ItemCast, StringComparison.Ordinal)) return null;
            if (!Guid.TryParse(freeWith.Substring(ItemCast.Length), out var g)) return null;
            var id = new ItemStackId(g);
            var stack = p.Inventory.Find(x => x.StackId == id);
            if (stack != null) return stack;
            foreach (var pair in p.Equipment) if (pair.Value.StackId == id) return pair.Value;
            return null;
        }

        /// <summary>Why a spell cannot be cast from this item now (null when it can): charges, one per spell level.</summary>
        private string ItemCastProblem(CombatantData actor, CastSpellAction a, SpellDefinition s)
        {
            var stack = CastingItem(actor, a.FreeWith);
            if (stack == null) return "You are not holding that item.";
            var m = ItemCatalog.Find(stack.ItemId)?.Magic;
            if (m?.SpellId != s.Id) return $"That item does not cast {s.Name}.";
            if (m.RequiresAttunement && !stack.Attuned) return "You must be attuned to it to use its magic.";
            var level = Math.Max(s.Level, a.SlotLevel);
            var charges = level - s.Level + 1;
            if (stack.Charges < charges) return $"It has {Math.Max(0, stack.Charges)} charge(s); level {level} needs {charges}.";
            return null;
        }

        private string SpendItemCharges(CombatantData actor, CastSpellAction a, SpellDefinition s)
        {
            var stack = CastingItem(actor, a.FreeWith);
            var level = Math.Max(s.Level, a.SlotLevel);
            var charges = level - s.Level + 1;
            stack.Charges = Math.Max(0, stack.Charges - charges);
            return $"{charges} charge{(charges == 1 ? string.Empty : "s")} of the {ItemCatalog.Find(stack.ItemId)?.Name} ({stack.Charges} left)";
        }
    }
}
