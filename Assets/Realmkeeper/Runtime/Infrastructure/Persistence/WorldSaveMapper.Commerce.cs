using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v7 (Phase 5): world time, the player's belongings and looks, shops, trades, regard.</summary>
    public static partial class WorldSaveMapper
    {
        private static ItemStackDto StackDto(ItemStackData s) => new ItemStackDto
        {
            StackId = s.StackId.ToString(), ItemId = s.ItemId, Quantity = s.Quantity,
            Condition = s.Condition == ItemCondition.Good ? null : s.Condition.ToString(),
            Quality = s.Quality == ItemQuality.Common ? null : s.Quality.ToString(),
            Charges = s.Charges < 0 ? (int?)null : s.Charges,
            Attuned = s.Attuned, Bound = s.Bound, Provenance = s.Provenance,
            AcquiredAt = s.AcquiredAt < 0 ? (long?)null : s.AcquiredAt,
        };

        private static ItemStackData Stack(ItemStackDto d, string field)
        {
            if (d == null) throw new SaveDataException($"Null {field} entry.");
            return new ItemStackData
            {
                StackId = new ItemStackId(RequiredGuid(d.StackId, field + " stackId")), ItemId = d.ItemId, Quantity = d.Quantity,
                Condition = string.IsNullOrEmpty(d.Condition) ? ItemCondition.Good : ParseEnum<ItemCondition>(d.Condition, field + " condition"),
                Quality = string.IsNullOrEmpty(d.Quality) ? ItemQuality.Common : ParseEnum<ItemQuality>(d.Quality, field + " quality"),
                Charges = d.Charges ?? -1, Attuned = d.Attuned, Bound = d.Bound, Provenance = d.Provenance, AcquiredAt = d.AcquiredAt ?? -1,
            };
        }

        private static void WriteCommerceWorld(WorldData w, WorldDto dto)
        {
            dto.MinuteOfDay = w.MinuteOfDay;
            if (dto.Player != null && w.Player != null)
            {
                var p = w.Player;
                var d = dto.Player;
                d.Species = p.Species;
                d.Age = p.Age;
                d.Background = p.Background;
                d.Description = p.Description;
                d.Purse = p.Purse;
                d.Fed = p.Fed;
                d.IsOutfitted = p.IsOutfitted;
                d.Rpg = SheetDto(p.Rpg);
                var a = p.Appearance;
                d.Appearance = new PlayerAppearanceDto
                {
                    Frame = a.Frame.ToString(), HairId = a.HairId, BeardId = a.BeardId, SkinTone = a.SkinTone, HairColour = a.HairColour, ClothTint = a.ClothTint,
                };
                d.Inventory = new List<ItemStackDto>();
                foreach (var s in p.Inventory) d.Inventory.Add(StackDto(s));
                d.Equipment = new Dictionary<string, ItemStackDto>();
                foreach (var pair in p.Equipment) d.Equipment[pair.Key.ToString()] = StackDto(pair.Value);
            }

            dto.Shops = new List<ShopDto>();
            foreach (var s in w.Shops)
            {
                var categories = new List<string>();
                foreach (var c in s.Categories) categories.Add(c.ToString());
                var stock = new Dictionary<string, int>();
                foreach (var line in s.Stock) stock[line.ItemId] = line.Quantity;
                dto.Shops.Add(new ShopDto
                {
                    ShopId = s.ShopId.ToString(),
                    SettlementId = s.SettlementId.ToString(),
                    LocationId = s.LocationId.ToString(),
                    Name = s.Name,
                    Categories = categories,
                    ShopkeeperNpcId = s.ShopkeeperNpcId.ToString(),
                    Stock = stock,
                    OpensAt = s.OpensAt,
                    ClosesAt = s.ClosesAt,
                    LastRestockedOn = s.LastRestockedOn.IsValidDate ? s.LastRestockedOn.ToIsoString() : null,
                    TotalSales = s.TotalSales,
                    TotalPurchases = s.TotalPurchases,
                    Goods = s.Goods.ConvertAll(StackDto),
                });
            }

            dto.Transactions = new List<TransactionDto>();
            foreach (var t in w.Transactions)
            {
                dto.Transactions.Add(new TransactionDto
                {
                    TransactionId = t.TransactionId.ToString(),
                    Date = t.Date.ToIsoString(),
                    MinuteOfDay = t.MinuteOfDay,
                    Kind = t.Kind.ToString(),
                    ShopId = t.ShopId.ToString(),
                    ShopkeeperNpcId = t.ShopkeeperNpcId.ToString(),
                    ItemId = t.ItemId,
                    Quantity = t.Quantity,
                    UnitPrice = t.UnitPrice,
                    Total = t.Total,
                    Source = t.Source,
                });
            }
        }

        private static void ReadCommerceWorld(WorldDto dto, WorldData world)
        {
            var minute = dto.MinuteOfDay ?? WorldTime.DefaultStartMinute;
            if (minute < 0 || minute >= WorldTime.MinutesPerDay) throw new SaveDataException($"Minute of day {minute} is out of range.");
            world.MinuteOfDay = minute;

            if (dto.Player != null && world.Player != null)
            {
                var d = dto.Player;
                var p = world.Player;
                if (d.Species != null) p.Species = d.Species;
                if (d.Age > 0) p.Age = d.Age;
                if (d.Background != null) p.Background = d.Background;
                if (d.Description != null) p.Description = d.Description;
                p.Purse = d.Purse;
                p.Fed = d.IsOutfitted ? d.Fed : p.Fed;
                p.IsOutfitted = d.IsOutfitted;
                p.Rpg = Sheet(d.Rpg);
                if (d.Appearance != null)
                {
                    p.Appearance = new PlayerAppearanceData
                    {
                        Frame = string.IsNullOrEmpty(d.Appearance.Frame) ? NpcSex.Male : ParseEnum<NpcSex>(d.Appearance.Frame, "player frame"),
                        HairId = d.Appearance.HairId,
                        BeardId = d.Appearance.BeardId,
                        SkinTone = d.Appearance.SkinTone,
                        HairColour = d.Appearance.HairColour,
                        ClothTint = d.Appearance.ClothTint,
                    };
                }

                foreach (var s in d.Inventory ?? new List<ItemStackDto>()) p.Inventory.Add(Stack(s, "inventory"));
                foreach (var pair in d.Equipment ?? new Dictionary<string, ItemStackDto>())
                    p.Equipment[ParseEnum<EquipmentSlot>(pair.Key, "equipment slot")] = Stack(pair.Value, "equipment");
            }

            foreach (var sd in dto.Shops ?? new List<ShopDto>())
            {
                if (sd == null) throw new SaveDataException("Null shop entry.");
                var shop = new ShopData
                {
                    ShopId = new ShopId(RequiredGuid(sd.ShopId, "shopId")),
                    SettlementId = new SettlementId(RequiredGuid(sd.SettlementId, "shop settlementId")),
                    LocationId = new LocationId(RequiredGuid(sd.LocationId, "shop locationId")),
                    Name = sd.Name,
                    ShopkeeperNpcId = new NpcId(RequiredGuid(sd.ShopkeeperNpcId, "shopkeeperNpcId")),
                    OpensAt = sd.OpensAt,
                    ClosesAt = sd.ClosesAt,
                    LastRestockedOn = OptionalDate(sd.LastRestockedOn, "shop lastRestockedOn"),
                    TotalSales = sd.TotalSales,
                    TotalPurchases = sd.TotalPurchases,
                };
                foreach (var c in sd.Categories ?? new List<string>()) shop.Categories.Add(ParseEnum<ShopCategory>(c, "shop category"));
                var items = new List<string>(sd.Stock?.Keys ?? (IEnumerable<string>)new string[0]);
                items.Sort(string.CompareOrdinal);
                foreach (var item in items) shop.Stock.Add(new ShopStockData { ItemId = item, Quantity = sd.Stock[item] });
                foreach (var g in sd.Goods ?? new List<ItemStackDto>()) shop.Goods.Add(Stack(g, "shop goods"));
                world.Shops.Add(shop);
            }

            foreach (var td in dto.Transactions ?? new List<TransactionDto>())
            {
                if (td == null) throw new SaveDataException("Null transaction entry.");
                world.Transactions.Add(new TransactionData
                {
                    TransactionId = new TransactionId(RequiredGuid(td.TransactionId, "transactionId")),
                    Date = ParseDate(td.Date, "transaction date"),
                    MinuteOfDay = td.MinuteOfDay,
                    Kind = ParseEnum<TransactionKind>(td.Kind, "transaction kind"),
                    ShopId = new ShopId(RequiredGuid(td.ShopId, "transaction shopId")),
                    ShopkeeperNpcId = new NpcId(RequiredGuid(td.ShopkeeperNpcId, "transaction shopkeeper")),
                    ItemId = td.ItemId,
                    Quantity = td.Quantity,
                    UnitPrice = td.UnitPrice,
                    Total = td.Total,
                    Source = td.Source ?? "player",
                });
            }
        }

        private static PlayerRegardDto RegardDto(PlayerRegardData r) => new PlayerRegardDto
        {
            Familiarity = r.Familiarity, Affinity = r.Affinity, Trades = r.Trades, Conversations = r.Conversations, LastTradeDay = r.LastTradeDay, LastTalkDay = r.LastTalkDay,
        };

        private static PlayerRegardData Regard(PlayerRegardDto d) => d == null
            ? new PlayerRegardData()
            : new PlayerRegardData
            {
                Familiarity = d.Familiarity, Affinity = d.Affinity, Trades = d.Trades, Conversations = d.Conversations, LastTradeDay = d.LastTradeDay, LastTalkDay = d.LastTalkDay,
            };
    }
}
