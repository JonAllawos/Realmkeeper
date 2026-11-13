using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 5 invariants: the clock, the player's belongings (one owner per item), shops and trades.</summary>
    public static partial class WorldValidator
    {
        private static void ValidateCommerce(WorldData world, HashSet<SettlementId> settlementIds, HashSet<NpcId> npcIds, ValidationResult result)
        {
            if (world.MinuteOfDay < 0 || world.MinuteOfDay >= WorldTime.MinutesPerDay)
                result.AddError($"Minute of day {world.MinuteOfDay} is outside 0–{WorldTime.MinutesPerDay - 1}.");

            var stackIds = new HashSet<ItemStackId>();
            if (world.Player != null) ValidateBelongings(world.Player, stackIds, result);

            var shopIds = new HashSet<ShopId>();
            var keepers = new HashSet<NpcId>();
            foreach (var shop in world.Shops)
            {
                var label = $"Shop '{shop.Name}'";
                if (shop.ShopId.IsEmpty || !shopIds.Add(shop.ShopId)) result.AddError($"{label} has a missing or duplicate id.");
                if (!settlementIds.Contains(shop.SettlementId)) result.AddError($"{label} references unknown settlement {shop.SettlementId}.");
                var place = world.FindLocation(shop.LocationId);
                if (place == null) result.AddError($"{label} references unknown location {shop.LocationId}.");
                else if (place.SettlementId != shop.SettlementId) result.AddError($"{label} stands in a location of another settlement.");
                if (!npcIds.Contains(shop.ShopkeeperNpcId)) result.AddError($"{label} has no existing shopkeeper ({shop.ShopkeeperNpcId}).");
                else if (!keepers.Add(shop.ShopkeeperNpcId)) result.AddError($"{label}: its shopkeeper already keeps another shop.");
                if (shop.Categories.Count == 0) result.AddError($"{label} deals in nothing.");
                if (shop.OpensAt < 0 || shop.ClosesAt > WorldTime.MinutesPerDay || shop.OpensAt >= shop.ClosesAt)
                    result.AddError($"{label} has invalid hours {shop.OpensAt}–{shop.ClosesAt}.");
                if (shop.TotalSales < 0 || shop.TotalPurchases < 0) result.AddError($"{label} has negative trade totals.");
                var items = new HashSet<string>();
                foreach (var line in shop.Stock)
                {
                    if (!ItemCatalog.Exists(line.ItemId)) result.AddError($"{label} stocks unknown item '{line.ItemId}'.");
                    if (line.Quantity <= 0) result.AddError($"{label} holds {line.Quantity} of '{line.ItemId}'.");
                    if (!items.Add(line.ItemId)) result.AddError($"{label} lists '{line.ItemId}' twice.");
                }
            }

            if (world.Transactions.Count > TransactionData.MaxHistory)
                result.AddError($"Transaction history holds {world.Transactions.Count} entries (limit {TransactionData.MaxHistory}).");
            ValidateItems(world, settlementIds, stackIds, result); // Phase 11
            ValidateParty(world, npcIds, stackIds, result); // Phase 15

            var transactionIds = new HashSet<TransactionId>();
            foreach (var t in world.Transactions)
            {
                if (t.TransactionId.IsEmpty || !transactionIds.Add(t.TransactionId)) result.AddError("A transaction has a missing or duplicate id.");
                if (!ItemCatalog.Exists(t.ItemId)) result.AddError($"Transaction {t.TransactionId} names unknown item '{t.ItemId}'.");
                if (t.Quantity <= 0 || t.UnitPrice <= 0) result.AddError($"Transaction {t.TransactionId} has quantity {t.Quantity} at {t.UnitPrice}.");
                if ((long)t.Quantity * t.UnitPrice != t.Total) result.AddError($"Transaction {t.TransactionId}: total {t.Total} is not {t.Quantity} × {t.UnitPrice}.");
                if (t.MinuteOfDay < 0 || t.MinuteOfDay >= WorldTime.MinutesPerDay) result.AddError($"Transaction {t.TransactionId} has an invalid time.");
            }
        }

        private static void ValidateBelongings(PlayerCharacterData p, HashSet<ItemStackId> stackIds, ValidationResult result)
        {
            if (p.Purse < 0) result.AddError($"The player's purse is negative ({p.Purse}).");
            if (p.Fed < 0 || p.Fed > 100) result.AddError($"The player's fed level {p.Fed} is outside 0–100.");
            var look = PlayerAppearanceOptions.Check(p.Appearance, p.Rpg?.State == Realmkeeper.Domain.Rpg.CreationState.Complete ? p.Rpg.SpeciesId : null);
            if (look != null) result.AddError("Player appearance: " + look);

            void Check(ItemStackData s, string where)
            {
                if (s == null)
                {
                    result.AddError($"Null item in the player's {where}.");
                    return;
                }

                if (s.StackId.IsEmpty || !stackIds.Add(s.StackId)) result.AddError($"Item stack {s.StackId} ({s.ItemId}) is missing an id or has more than one owner.");
                var def = ItemCatalog.Find(s.ItemId);
                if (def == null)
                {
                    result.AddError($"The player's {where} holds unknown item '{s.ItemId}'.");
                    return;
                }

                if (s.Quantity <= 0) result.AddError($"The player's {where} holds {s.Quantity} of '{s.ItemId}'.");
                var max = def.Stackable ? def.MaxStack : 1;
                if (s.Quantity > max) result.AddError($"The player's {where} holds {s.Quantity} of '{s.ItemId}' in one stack (at most {max}).");
            }

            foreach (var s in p.Inventory) Check(s, "inventory");
            foreach (var pair in p.Equipment)
            {
                Check(pair.Value, pair.Key + " slot");
                var def = pair.Value == null ? null : ItemCatalog.Find(pair.Value.ItemId);
                if (def == null) continue;
                if (pair.Key == EquipmentSlot.None || !SlotFits(def, pair.Key))
                    result.AddError($"'{def.Name}' cannot be worn in the {pair.Key} slot.");
                if (pair.Value.Quantity != 1) result.AddError($"The {pair.Key} slot holds {pair.Value.Quantity} items.");
            }

            var main = p.Equipped(EquipmentSlot.MainHand);
            var mainDef = main == null ? null : ItemCatalog.Find(main.ItemId);
            if (mainDef != null && mainDef.TwoHanded && p.Equipped(EquipmentSlot.OffHand) != null)
                result.AddError($"'{mainDef.Name}' needs both hands, but the off hand is also in use.");
        }
    }
}
