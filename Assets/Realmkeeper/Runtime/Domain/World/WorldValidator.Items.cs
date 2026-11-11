using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 11 invariants (ADR-0050, ADR-0051): every item instance has exactly one holder — the pack, a slot, a
    /// container, a shop's particular goods, or work in progress — and its state is possible (charges within the
    /// maximum, a curse only on what is attuned, no more than three attunements).
    /// </summary>
    public static partial class WorldValidator
    {
        /// <summary>True when an item may be worn in a slot: its own; either ring finger; a Light weapon in the off hand.</summary>
        public static bool SlotFits(ItemDefinition def, EquipmentSlot slot)
        {
            if (def.Slot == slot) return true;
            var ring = def.Slot == EquipmentSlot.RingLeft || def.Slot == EquipmentSlot.RingRight;
            if (ring) return slot == EquipmentSlot.RingLeft || slot == EquipmentSlot.RingRight;
            return slot == EquipmentSlot.OffHand && def.Slot == EquipmentSlot.MainHand && def.Weapon != null && !def.TwoHanded && def.Weapon.Has(WeaponProperty.Light);
        }

        private static void CheckInstance(ItemStackData s, string where, HashSet<ItemStackId> stackIds, ValidationResult result)
        {
            if (s == null)
            {
                result.AddError($"Null item in {where}.");
                return;
            }

            if (s.StackId.IsEmpty || !stackIds.Add(s.StackId)) result.AddError($"Item stack {s.StackId} ({s.ItemId}) in {where} is missing an id or has more than one holder.");
            var def = ItemCatalog.Find(s.ItemId);
            if (def == null)
            {
                result.AddError($"{where} holds unknown item '{s.ItemId}'.");
                return;
            }

            if (s.Quantity <= 0) result.AddError($"{where} holds {s.Quantity} of '{s.ItemId}'.");
            if (s.Quantity > (def.Stackable ? def.MaxStack : 1)) result.AddError($"{where} holds {s.Quantity} of '{s.ItemId}' in one stack.");
            CheckState(s, def, where, result);
        }

        private static void CheckState(ItemStackData s, ItemDefinition def, string where, ValidationResult result)
        {
            if (!System.Enum.IsDefined(typeof(ItemCondition), s.Condition)) result.AddError($"'{s.ItemId}' in {where} has an unknown condition.");
            if (!System.Enum.IsDefined(typeof(ItemQuality), s.Quality)) result.AddError($"'{s.ItemId}' in {where} has an unknown quality.");
            var max = def.Magic?.MaxCharges ?? 0;
            if (s.Charges > max || s.Charges < -1) result.AddError($"'{s.ItemId}' in {where} has {s.Charges} charges (at most {max}).");
            if (s.Attuned && def.Magic?.RequiresAttunement != true) result.AddError($"'{s.ItemId}' in {where} is attuned but needs no attunement.");
            if (s.Bound && !s.Attuned) result.AddError($"'{s.ItemId}' in {where} is bound without being attuned.");
        }

        private static void ValidateItems(WorldData world, HashSet<SettlementId> settlementIds, HashSet<ItemStackId> stackIds, ValidationResult result)
        {
            if (world.Player != null)
            {
                var attuned = 0;
                foreach (var s in world.Player.Inventory)
                    if (s != null && ItemCatalog.Find(s.ItemId) is ItemDefinition d)
                    {
                        CheckState(s, d, "the player's inventory", result);
                        if (s.Attuned) attuned++;
                    }

                foreach (var pair in world.Player.Equipment)
                    if (pair.Value != null && ItemCatalog.Find(pair.Value.ItemId) is ItemDefinition d)
                    {
                        CheckState(pair.Value, d, "the " + pair.Key + " slot", result);
                        if (pair.Value.Attuned) attuned++;
                    }

                if (attuned > Carrying.MaxAttuned) result.AddError($"The player is attuned to {attuned} items (at most {Carrying.MaxAttuned}).");
            }

            var keys = new HashSet<string>();
            foreach (var c in world.Containers)
            {
                var label = $"Container '{c?.Key}'";
                if (c == null || string.IsNullOrEmpty(c.Key) || !keys.Add(c.Key)) { result.AddError($"{label} is missing or duplicated."); continue; }
                if (!c.SettlementId.IsEmpty && !settlementIds.Contains(c.SettlementId)) result.AddError($"{label} references unknown settlement {c.SettlementId}.");
                if (c.Coin < 0) result.AddError($"{label} holds negative coin.");
                if (c.CapacityGrams < 0) result.AddError($"{label} has a negative capacity.");
                if (c.AcceptsDeposits && !c.PlayerAccess) result.AddError($"{label} accepts deposits but the player cannot reach it.");
                foreach (var s in c.Items) CheckInstance(s, label, stackIds, result);
            }

            foreach (var shop in world.Shops)
            {
                if (shop.Goods.Count > ShopData.MaxGoods) result.AddError($"Shop '{shop.Name}' keeps {shop.Goods.Count} particular goods (at most {ShopData.MaxGoods}).");
                foreach (var s in shop.Goods) CheckInstance(s, $"Shop '{shop.Name}' goods", stackIds, result);
            }

            var jobKeys = new HashSet<string>();
            var active = 0;
            foreach (var j in world.WorkJobs)
            {
                var label = $"Work '{j?.Key}'";
                if (j == null || string.IsNullOrEmpty(j.Key) || !jobKeys.Add(j.Key)) { result.AddError($"{label} is missing or duplicated."); continue; }
                if (!j.Done) active++;
                if (j.ReadyAt < j.StartedAt) result.AddError($"{label} is ready before it starts.");
                if (j.Kind == WorkKind.Craft && RecipeCatalog.Find(j.RecipeId) == null) result.AddError($"{label} names unknown recipe '{j.RecipeId}'.");
                if (j.Done && j.Held.Count > 0) result.AddError($"{label} is finished but still holds items.");
                foreach (var s in j.Held) CheckInstance(s, label, stackIds, result);
            }

            if (active > 1) result.AddError($"The player has {active} pieces of work in progress (at most one).");

            var siteKeys = new HashSet<string>();
            foreach (var s in world.GatheringSites)
            {
                var label = $"Gathering site '{s?.Key}'";
                if (s == null || string.IsNullOrEmpty(s.Key) || !siteKeys.Add(s.Key)) { result.AddError($"{label} is missing or duplicated."); continue; }
                if (!settlementIds.Contains(s.SettlementId)) result.AddError($"{label} references unknown settlement {s.SettlementId}.");
                if (!ItemCatalog.Exists(s.ItemId)) result.AddError($"{label} yields unknown item '{s.ItemId}'.");
                if (s.Available < 0 || s.Available > s.Capacity || s.Yield < 1 || s.Minutes < 1 || s.PerWeek < 0) result.AddError($"{label} has impossible amounts.");
            }

            if (world.ItemCatalogVersion > ItemCatalog.Version)
                result.AddError($"The world's item catalog version {world.ItemCatalogVersion} is newer than this build's ({ItemCatalog.Version}).");
        }
    }
}
