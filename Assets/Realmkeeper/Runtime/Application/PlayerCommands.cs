using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    // Phase 5 commands: the player's belongings, looks and trade. Every command validates first and
    // edits a working copy that the session validates whole before committing (atomic: a failed
    // purchase changes nothing — no coin taken, no goods moved, no stock lost). Every item stack has
    // exactly one owner: the player's inventory, one equipment slot, or (as a count) a shop's stock.

    /// <summary>Advances the clock by minutes; each midnight crossed runs one simulated day.</summary>
    public sealed class AdvanceTimeCommand
    {
        public const int MaxMinutes = 7 * WorldTime.MinutesPerDay;

        public AdvanceTimeCommand(int minutes)
        {
            Minutes = minutes;
        }

        public int Minutes { get; }
    }

    public sealed class EquipItemCommand : ICommand
    {
        public EquipItemCommand(ItemStackId stackId) { StackId = stackId; }
        public ItemStackId StackId { get; }
    }

    public sealed class UnequipItemCommand : ICommand
    {
        public UnequipItemCommand(EquipmentSlot slot) { Slot = slot; }
        public EquipmentSlot Slot { get; }
    }

    /// <summary>Takes <see cref="Quantity"/> off a stack into a new stack.</summary>
    public sealed class SplitStackCommand : ICommand
    {
        public SplitStackCommand(ItemStackId stackId, int quantity)
        {
            StackId = stackId;
            Quantity = quantity;
        }

        public ItemStackId StackId { get; }
        public int Quantity { get; }
    }

    /// <summary>Moves as much of one stack into another of the same item as fits.</summary>
    public sealed class CombineStacksCommand : ICommand
    {
        public CombineStacksCommand(ItemStackId from, ItemStackId into)
        {
            From = from;
            Into = into;
        }

        public ItemStackId From { get; }
        public ItemStackId Into { get; }
    }

    /// <summary>Drops items from the inventory (they are gone; the world keeps no loose items yet).</summary>
    public sealed class DiscardItemCommand : ICommand
    {
        public DiscardItemCommand(ItemStackId stackId, int quantity)
        {
            StackId = stackId;
            Quantity = quantity;
        }

        public ItemStackId StackId { get; }
        public int Quantity { get; }
    }

    /// <summary>Uses one item: food is eaten.</summary>
    public sealed class UseItemCommand : ICommand
    {
        public UseItemCommand(ItemStackId stackId) { StackId = stackId; }
        public ItemStackId StackId { get; }
    }

    public sealed class SetPlayerAppearanceCommand : ICommand
    {
        public SetPlayerAppearanceCommand(PlayerAppearanceData appearance) { Appearance = appearance; }
        public PlayerAppearanceData Appearance { get; }
    }

    /// <summary>Edits the player's identity; null fields are kept.</summary>
    public sealed class SetPlayerProfileCommand : ICommand
    {
        public SetPlayerProfileCommand(string name = null, int? age = null, string background = null, string description = null)
        {
            Name = name;
            Age = age;
            Background = background;
            Description = description;
        }

        public string Name { get; }
        public int? Age { get; }
        public string Background { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Buys from a shop. <see cref="ExpectedUnitPrice"/> is the price the player was shown; if the
    /// price has changed since, nothing happens and the new price is reported.
    /// </summary>
    public sealed class BuyItemCommand : ICommand
    {
        public BuyItemCommand(ShopId shopId, string itemId, int quantity, int expectedUnitPrice)
        {
            ShopId = shopId;
            ItemId = itemId;
            Quantity = quantity;
            ExpectedUnitPrice = expectedUnitPrice;
        }

        public ShopId ShopId { get; }
        public string ItemId { get; }
        public int Quantity { get; }
        public int ExpectedUnitPrice { get; }
    }

    /// <summary>Sells items from an inventory stack to a shop at the price the player was shown.</summary>
    public sealed class SellItemCommand : ICommand
    {
        public SellItemCommand(ShopId shopId, ItemStackId stackId, int quantity, int expectedUnitPrice)
        {
            ShopId = shopId;
            StackId = stackId;
            Quantity = quantity;
            ExpectedUnitPrice = expectedUnitPrice;
        }

        public ShopId ShopId { get; }
        public ItemStackId StackId { get; }
        public int Quantity { get; }
        public int ExpectedUnitPrice { get; }
    }

    public static class PlayerCommandHandlers
    {
        /// <summary>Most separate stacks the player can carry.</summary>
        public const int MaxInventoryStacks = 40;

        public const int MaxBackgroundLength = 40;
        public const int MaxDescriptionLength = 400;
        public const int MinAge = 16;
        public const int MaxAge = 90;

        /// <summary>Familiarity and affinity gained from trading with a keeper (once per day per keeper).</summary>
        public const int TradeFamiliarity = 3;
        public const int TradeAffinity = 1;

        /// <summary>Familiarity gained from talking (once per day per person).</summary>
        public const int TalkFamiliarity = 2;

        public static void Register(CommandRegistry commands)
        {
            commands.Register<EquipItemCommand>(new EquipHandler());
            commands.Register<UnequipItemCommand>(new UnequipHandler());
            commands.Register<SplitStackCommand>(new SplitHandler());
            commands.Register<CombineStacksCommand>(new CombineHandler());
            commands.Register<DiscardItemCommand>(new DiscardHandler());
            commands.Register<UseItemCommand>(new UseHandler());
            commands.Register<SetPlayerAppearanceCommand>(new AppearanceHandler());
            commands.Register<SetPlayerProfileCommand>(new ProfileHandler());
            commands.Register<BuyItemCommand>(new BuyHandler());
            commands.Register<SellItemCommand>(new SellHandler());
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static PlayerCharacterData Player(CommandContext context, out CommandResult failure)
        {
            failure = null;
            if (!context.HasWorld) { failure = CommandResult.InvalidState("There is no active world."); return null; }
            if (context.Committed.Player == null) { failure = CommandResult.NotFound("This world has no player character."); return null; }
            return context.Committed.Player;
        }

        internal static int IndexOf(PlayerCharacterData p, ItemStackId id)
        {
            for (var i = 0; i < p.Inventory.Count; i++) if (p.Inventory[i].StackId == id) return i;
            return -1;
        }

        private static ItemStackData InInventory(PlayerCharacterData p, ItemStackId id, out CommandResult failure)
        {
            failure = null;
            if (id.IsEmpty) { failure = CommandResult.Invalid("An item is required."); return null; }
            var i = IndexOf(p, id);
            if (i >= 0) return p.Inventory[i];
            foreach (var pair in p.Equipment)
                if (pair.Value.StackId == id) { failure = CommandResult.InvalidState($"That item is equipped ({Slot(pair.Key)}); unequip it first."); return null; }
            failure = CommandResult.NotFound("That item is not in your pack.");
            return null;
        }

        public static string Slot(EquipmentSlot s)
        {
            switch (s)
            {
                case EquipmentSlot.MainHand: return "main hand";
                case EquipmentSlot.OffHand: return "off hand";
                default: return s.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Total weight carried and worn, in grams.</summary>
        public static int CarriedGrams(PlayerCharacterData p)
        {
            var grams = 0;
            foreach (var s in p.Inventory) grams += (ItemCatalog.Find(s.ItemId)?.WeightGrams ?? 0) * s.Quantity;
            foreach (var s in p.Equipment.Values) grams += ItemCatalog.Find(s.ItemId)?.WeightGrams ?? 0;
            return grams;
        }

        /// <summary>
        /// Adds items to the player's pack, topping up existing stacks first. Returns false (and adds
        /// nothing) if they would need more stacks than the pack holds.
        /// </summary>
        internal static bool AddToPack(CommandContext context, PlayerCharacterData p, ItemDefinition def, int quantity)
        {
            var max = def.Stackable ? def.MaxStack : 1;
            var room = 0;
            foreach (var s in p.Inventory) if (s.ItemId == def.Id) room += max - s.Quantity;
            var newStacks = Math.Max(0, (quantity - room + max - 1) / max);
            if (p.Inventory.Count + newStacks > MaxInventoryStacks) return false;
            var left = quantity;
            foreach (var s in p.Inventory)
            {
                if (left == 0) break;
                if (s.ItemId != def.Id || s.Quantity >= max) continue;
                var add = Math.Min(left, max - s.Quantity);
                s.Quantity += add;
                left -= add;
            }

            while (left > 0)
            {
                var add = Math.Min(left, max);
                p.Inventory.Add(new ItemStackData { StackId = new ItemStackId(context.NextGuid("item-stack")), ItemId = def.Id, Quantity = add });
                left -= add;
            }

            return true;
        }

        /// <summary>Trades at or above this many coin are memorable to the keeper (as is the first with the player).</summary>
        public const int MemorableTrade = 25;

        /// <summary>
        /// A keeper remembers their first trade with the player and any large one (Phase 5): a private memory,
        /// like a conversation, that their talk can draw on. Small repeat trades leave no memory.
        /// </summary>
        internal static void RememberTrade(CommandContext context, NpcData keeper, PlayerCharacterData player, ItemDefinition def, int quantity, int total, bool keeperSold, SettlementId where)
        {
            var first = keeper.PlayerRegard.Trades == 0;
            if (!first && total < MemorableTrade) return;
            var what = (quantity > 1 ? I(quantity) + " × " : string.Empty) + def.Name.ToLowerInvariant();
            NpcMind.Remember(keeper, new NpcMemoryData
            {
                MemoryId = new MemoryId(context.NextGuid("npc-memory")),
                Date = context.Edit().CurrentSimulationDate,
                Category = MemoryCategory.Work,
                Subject = (keeperSold ? "Sold " + what + " to " : "Bought " + what + " from ") + player.Name + " for " + I(total) + " coin",
                SettlementId = where,
                Salience = Math.Min(60, (first ? 35 : 20) + total / 5),
                Source = MemorySource.Experienced,
            });
        }

        /// <summary>Once per day per person: familiarity (and for trade, a little warmth) grows. Repetition gains nothing more.</summary>
        internal static void Acquaint(NpcData npc, WorldDate today, bool trade)
        {
            var r = npc.PlayerRegard;
            var day = today.ToDayNumber();
            if (trade)
            {
                r.Trades++;
                if (r.LastTradeDay == day) return;
                r.LastTradeDay = (int)day;
                r.Familiarity += TradeFamiliarity;
                r.Affinity += TradeAffinity;
            }
            else
            {
                r.Conversations++;
                if (r.LastTalkDay == day) return;
                r.LastTalkDay = (int)day;
                r.Familiarity += TalkFamiliarity;
            }
        }

        // ----- Equipment ----------------------------------------------------------------------------

        private sealed class EquipHandler : ICommandHandler<EquipItemCommand>
        {
            public CommandResult Handle(EquipItemCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var stack = InInventory(p, c.StackId, out failure);
                if (stack == null) return failure;
                var def = ItemCatalog.Find(stack.ItemId);
                if (def == null) return CommandResult.Invalid($"Unknown item '{stack.ItemId}'.");
                if (!def.IsEquippable) return CommandResult.Invalid($"{def.Name} cannot be worn or wielded.");

                var edit = context.Edit().Player;
                var freed = EquipFromPack(context, edit, c.StackId, def);
                if (freed == null) return CommandResult.InvalidState("Your pack is too full to take off what you are wearing there.");
                return CommandResult.Ok($"Equipped {def.Name} ({Slot(def.Slot)})." + (freed.Count > 0 ? " Put away: " + string.Join(", ", freed) + "." : string.Empty));
            }
        }

        /// <summary>
        /// Equips one unit of a pack stack on an editable player (shared by equipping and character creation):
        /// the slot's item, and anything the two-handed rules displace, go back to the pack. Returns the names
        /// put away, or null when the pack would overflow (the caller then fails; the working copy is discarded).
        /// </summary>
        internal static List<string> EquipFromPack(CommandContext context, PlayerCharacterData edit, ItemStackId stackId, ItemDefinition def)
        {
            {
                var item = edit.Inventory[IndexOf(edit, stackId)];
                var freed = new List<string>();

                // The equipped item comes off a stack of one; the rest stays in the pack.
                ItemStackData wearing;
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                    wearing = new ItemStackData { StackId = new ItemStackId(context.NextGuid("item-stack")), ItemId = item.ItemId, Quantity = 1 };
                }
                else
                {
                    edit.Inventory.Remove(item);
                    wearing = item;
                }

                void TakeOff(EquipmentSlot slot)
                {
                    var off = edit.Equipped(slot);
                    if (off == null) return;
                    edit.Equipment.Remove(slot);
                    edit.Inventory.Add(off);
                    freed.Add(ItemCatalog.Find(off.ItemId)?.Name ?? off.ItemId);
                }

                TakeOff(def.Slot);
                if (def.Slot == EquipmentSlot.MainHand && def.TwoHanded) TakeOff(EquipmentSlot.OffHand);
                if (def.Slot == EquipmentSlot.OffHand)
                {
                    var main = edit.Equipped(EquipmentSlot.MainHand);
                    if (main != null && (ItemCatalog.Find(main.ItemId)?.TwoHanded ?? false)) TakeOff(EquipmentSlot.MainHand);
                }

                if (edit.Inventory.Count > MaxInventoryStacks) return null;
                edit.Equipment[def.Slot] = wearing;
                return freed;
            }
        }

        private sealed class UnequipHandler : ICommandHandler<UnequipItemCommand>
        {
            public CommandResult Handle(UnequipItemCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                if (c.Slot == EquipmentSlot.None || !Enum.IsDefined(typeof(EquipmentSlot), c.Slot)) return CommandResult.Invalid("Unknown equipment slot.");
                var worn = p.Equipped(c.Slot);
                if (worn == null) return CommandResult.InvalidState($"Nothing is equipped in the {Slot(c.Slot)} slot.");
                if (p.Inventory.Count >= MaxInventoryStacks) return CommandResult.InvalidState("Your pack is full.");
                var edit = context.Edit().Player;
                var off = edit.Equipped(c.Slot);
                edit.Equipment.Remove(c.Slot);
                edit.Inventory.Add(off);
                return CommandResult.Ok($"Unequipped {ItemCatalog.Find(off.ItemId)?.Name ?? off.ItemId}.");
            }
        }

        // ----- Inventory ----------------------------------------------------------------------------

        private sealed class SplitHandler : ICommandHandler<SplitStackCommand>
        {
            public CommandResult Handle(SplitStackCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var stack = InInventory(p, c.StackId, out failure);
                if (stack == null) return failure;
                if (c.Quantity < 1 || c.Quantity >= stack.Quantity) return CommandResult.Invalid($"Split off between 1 and {stack.Quantity - 1}.");
                if (p.Inventory.Count >= MaxInventoryStacks) return CommandResult.InvalidState("Your pack is full.");
                var edit = context.Edit().Player;
                var s = edit.Inventory[IndexOf(edit, c.StackId)];
                s.Quantity -= c.Quantity;
                edit.Inventory.Add(new ItemStackData { StackId = new ItemStackId(context.NextGuid("item-stack")), ItemId = s.ItemId, Quantity = c.Quantity });
                return CommandResult.Ok($"Split {I(c.Quantity)} off.");
            }
        }

        private sealed class CombineHandler : ICommandHandler<CombineStacksCommand>
        {
            public CommandResult Handle(CombineStacksCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var from = InInventory(p, c.From, out failure);
                if (from == null) return failure;
                var into = InInventory(p, c.Into, out failure);
                if (into == null) return failure;
                if (c.From == c.Into) return CommandResult.Invalid("Choose two different stacks.");
                if (from.ItemId != into.ItemId) return CommandResult.Invalid("Only stacks of the same item combine.");
                var def = ItemCatalog.Find(from.ItemId);
                if (def == null || !def.Stackable) return CommandResult.Invalid($"{def?.Name ?? from.ItemId} does not stack.");
                var room = def.MaxStack - into.Quantity;
                if (room <= 0) return CommandResult.InvalidState("That stack is already full.");
                var edit = context.Edit().Player;
                var f = edit.Inventory[IndexOf(edit, c.From)];
                var t = edit.Inventory[IndexOf(edit, c.Into)];
                var moved = Math.Min(room, f.Quantity);
                t.Quantity += moved;
                f.Quantity -= moved;
                if (f.Quantity == 0) edit.Inventory.Remove(f);
                return CommandResult.Ok($"Combined {I(moved)} {def.Name}.");
            }
        }

        private sealed class DiscardHandler : ICommandHandler<DiscardItemCommand>
        {
            public CommandResult Handle(DiscardItemCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var stack = InInventory(p, c.StackId, out failure);
                if (stack == null) return failure;
                if (c.Quantity < 1 || c.Quantity > stack.Quantity) return CommandResult.Invalid($"Discard between 1 and {stack.Quantity}.");
                var edit = context.Edit().Player;
                var s = edit.Inventory[IndexOf(edit, c.StackId)];
                s.Quantity -= c.Quantity;
                if (s.Quantity == 0) edit.Inventory.Remove(s);
                return CommandResult.Ok($"Discarded {I(c.Quantity)} {ItemCatalog.Find(s.ItemId)?.Name ?? s.ItemId}.");
            }
        }

        private sealed class UseHandler : ICommandHandler<UseItemCommand>
        {
            public CommandResult Handle(UseItemCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var stack = InInventory(p, c.StackId, out failure);
                if (stack == null) return failure;
                var def = ItemCatalog.Find(stack.ItemId);
                if (def == null || !def.IsConsumable) return CommandResult.Invalid($"{def?.Name ?? stack.ItemId} cannot be used like that.");
                if (p.Fed >= 100) return CommandResult.InvalidState("You are not hungry.");
                var edit = context.Edit().Player;
                var s = edit.Inventory[IndexOf(edit, c.StackId)];
                s.Quantity--;
                if (s.Quantity == 0) edit.Inventory.Remove(s);
                var before = edit.Fed;
                edit.Fed = Math.Min(100, edit.Fed + def.Nourishment);
                return CommandResult.Ok($"Ate {def.Name}. Fed {I(before)} → {I(edit.Fed)}.");
            }
        }

        // ----- Identity -----------------------------------------------------------------------------

        private sealed class AppearanceHandler : ICommandHandler<SetPlayerAppearanceCommand>
        {
            public CommandResult Handle(SetPlayerAppearanceCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var problem = PlayerAppearanceOptions.Check(c.Appearance);
                if (problem != null) return CommandResult.Invalid(problem);
                var a = c.Appearance;
                context.Edit().Player.Appearance = new PlayerAppearanceData
                {
                    Frame = a.Frame, HairId = a.HairId, BeardId = a.BeardId, SkinTone = a.SkinTone, HairColour = a.HairColour, ClothTint = a.ClothTint,
                };
                return CommandResult.Ok("Appearance updated.");
            }
        }

        private sealed class ProfileHandler : ICommandHandler<SetPlayerProfileCommand>
        {
            public CommandResult Handle(SetPlayerProfileCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                if (c.Name == null && !c.Age.HasValue && c.Background == null && c.Description == null) return CommandResult.Invalid("Nothing to change.");
                if (c.Name != null && (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > PlayerCharacterData.MaxNameLength))
                    return CommandResult.Invalid($"A name must be 1–{PlayerCharacterData.MaxNameLength} characters.");
                if (c.Age.HasValue && (c.Age < MinAge || c.Age > MaxAge)) return CommandResult.Invalid($"Age must be {MinAge}–{MaxAge}.");
                if (c.Background != null && (string.IsNullOrWhiteSpace(c.Background) || c.Background.Trim().Length > MaxBackgroundLength))
                    return CommandResult.Invalid($"A background must be 1–{MaxBackgroundLength} characters.");
                if (c.Background != null && p.Rpg != null && p.Rpg.State == Domain.Rpg.CreationState.Complete)
                    return CommandResult.Invalid("Your background was set when you created your character.");
                if (c.Description != null && c.Description.Length > MaxDescriptionLength)
                    return CommandResult.Invalid($"A description must be at most {MaxDescriptionLength} characters.");
                var edit = context.Edit().Player;
                if (c.Name != null) edit.Name = c.Name.Trim();
                if (c.Age.HasValue) edit.Age = c.Age.Value;
                if (c.Background != null) edit.Background = c.Background.Trim();
                if (c.Description != null) edit.Description = c.Description.Trim();
                return CommandResult.Ok("Character updated.");
            }
        }

        // ----- Trade --------------------------------------------------------------------------------

        /// <summary>The shop, open and reachable by the player, or the reason why not.</summary>
        private static ShopData OpenShop(WorldData world, PlayerCharacterData p, ShopId id, out CommandResult failure)
        {
            failure = null;
            if (id.IsEmpty) { failure = CommandResult.Invalid("A shop is required."); return null; }
            var shop = world.FindShop(id);
            if (shop == null) { failure = CommandResult.NotFound($"No shop has ID {id}."); return null; }
            if (!p.IsInSettlement || p.SettlementId != shop.SettlementId)
            {
                failure = CommandResult.InvalidState($"You are not in {world.FindSettlement(shop.SettlementId)?.Name ?? "that settlement"}.");
                return null;
            }

            var open = CommerceMath.Availability(world, shop);
            if (!open.IsOpen) { failure = CommandResult.InvalidState(open.Reason); return null; }
            return shop;
        }

        private static Dictionary<string, string> TradeFacts(PlayerCharacterData p, ShopData shop, NpcData keeper, ItemDefinition def, int quantity, int unit, int total) =>
            new Dictionary<string, string>
            {
                ["source"] = "player",
                ["player"] = p.Name,
                ["shop"] = shop.Name,
                ["shopId"] = shop.ShopId.ToString(),
                ["keeper"] = keeper.Name,
                ["item"] = def.Id,
                ["itemName"] = def.Name,
                ["quantity"] = I(quantity),
                ["unitPrice"] = I(unit),
                ["total"] = I(total),
            };

        private static void Record(CommandContext context, WorldData world, TransactionKind kind, ShopData shop, string item, int quantity, int unit)
        {
            world.Transactions.Add(new TransactionData
            {
                TransactionId = new TransactionId(context.NextGuid("transaction")),
                Date = world.CurrentSimulationDate,
                MinuteOfDay = world.MinuteOfDay,
                Kind = kind,
                ShopId = shop.ShopId,
                ShopkeeperNpcId = shop.ShopkeeperNpcId,
                ItemId = item,
                Quantity = quantity,
                UnitPrice = unit,
                Total = quantity * unit,
                Source = "player",
            });
            while (world.Transactions.Count > TransactionData.MaxHistory) world.Transactions.RemoveAt(0);
        }

        private sealed class BuyHandler : ICommandHandler<BuyItemCommand>
        {
            public CommandResult Handle(BuyItemCommand c, CommandContext context)
            {
                // 1. The shop is open and its keeper present.
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var world = context.Committed;
                var shop = OpenShop(world, p, c.ShopId, out failure);
                if (shop == null) return failure;
                var def = ItemCatalog.Find(c.ItemId);
                if (def == null) return CommandResult.NotFound($"Unknown item '{c.ItemId}'.");
                if (c.Quantity < 1) return CommandResult.Invalid("Buy at least one.");

                // 2. Stock.
                var have = shop.QuantityOf(def.Id);
                if (have < c.Quantity) return CommandResult.InvalidState(have == 0 ? $"{shop.Name} has no {def.Name} left." : $"{shop.Name} has only {I(have)} {def.Name}.");

                // 3. Price: the one the player was shown.
                var quote = CommerceMath.SellingPrice(world, shop, def);
                if (quote.UnitPrice != c.ExpectedUnitPrice)
                    return CommandResult.InvalidState($"The price has changed: {def.Name} now costs {I(quote.UnitPrice)} coin.");
                var total = quote.UnitPrice * c.Quantity;

                // 4. Funds.
                if (p.Purse < total) return CommandResult.InvalidState($"You need {I(total)} coin and have {I(p.Purse)}.");

                var edit = context.Edit();
                var player = edit.Player;
                var eshop = edit.FindShop(shop.ShopId);
                var keeper = edit.FindNpc(eshop.ShopkeeperNpcId);

                // 5. Pay. 6. Take the goods (or nothing happens: the working copy is discarded).
                player.Purse -= total;
                if (!AddToPack(context, player, def, c.Quantity)) return CommandResult.InvalidState("Your pack is full.");

                // 7. The shop's stock falls. 8. The keeper is paid.
                if (!CommerceMath.RemoveStock(eshop, def.Id, c.Quantity)) return CommandResult.Fail("The stock could not be taken; nothing was changed.");
                eshop.TotalSales += total;
                keeper.Condition.Wealth += total;
                RememberTrade(context, keeper, player, def, c.Quantity, total, true, eshop.SettlementId);
                Acquaint(keeper, edit.CurrentSimulationDate, true);

                // 9. Recorded: the shop's ledger and the chronicle.
                Record(context, edit, TransactionKind.Purchase, eshop, def.Id, c.Quantity, quote.UnitPrice);
                context.ReportEvent(HistoricalEventType.PlayerPurchase, EventSeverity.Info, EventImportance.Minor, new[] { eshop.SettlementId },
                    TradeFacts(player, eshop, keeper, def, c.Quantity, quote.UnitPrice, total), null, new[] { keeper.NpcId });
                return CommandResult.Ok($"Bought {(c.Quantity > 1 ? I(c.Quantity) + " × " : string.Empty)}{def.Name} for {I(total)} coin.");
            }
        }

        private sealed class SellHandler : ICommandHandler<SellItemCommand>
        {
            public CommandResult Handle(SellItemCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var world = context.Committed;
                var shop = OpenShop(world, p, c.ShopId, out failure);
                if (shop == null) return failure;
                var stack = InInventory(p, c.StackId, out failure);
                if (stack == null) return failure;
                if (c.Quantity < 1 || c.Quantity > stack.Quantity) return CommandResult.Invalid($"Sell between 1 and {I(stack.Quantity)}.");
                var def = ItemCatalog.Find(stack.ItemId);
                if (def == null) return CommandResult.Invalid($"Unknown item '{stack.ItemId}'.");
                var quote = CommerceMath.BuyingPrice(world, shop, def);
                if (!quote.Accepted) return CommandResult.InvalidState(quote.Refusal);
                if (quote.UnitPrice != c.ExpectedUnitPrice)
                    return CommandResult.InvalidState($"The offer has changed: they now pay {I(quote.UnitPrice)} coin for {def.Name}.");
                var total = quote.UnitPrice * c.Quantity;
                var keeperNow = world.FindNpc(shop.ShopkeeperNpcId);
                if (keeperNow.Condition.Wealth < total) return CommandResult.InvalidState($"{keeperNow.Name} cannot afford {I(total)} coin just now.");

                var edit = context.Edit();
                var player = edit.Player;
                var eshop = edit.FindShop(shop.ShopId);
                var keeper = edit.FindNpc(eshop.ShopkeeperNpcId);
                var s = player.Inventory[IndexOf(player, c.StackId)];
                s.Quantity -= c.Quantity;
                if (s.Quantity == 0) player.Inventory.Remove(s);
                keeper.Condition.Wealth -= total;
                player.Purse += total;
                CommerceMath.AddStock(eshop, def.Id, c.Quantity);
                eshop.TotalPurchases += total;
                RememberTrade(context, keeper, player, def, c.Quantity, total, false, eshop.SettlementId);
                Acquaint(keeper, edit.CurrentSimulationDate, true);
                Record(context, edit, TransactionKind.Sale, eshop, def.Id, c.Quantity, quote.UnitPrice);
                context.ReportEvent(HistoricalEventType.PlayerSale, EventSeverity.Info, EventImportance.Minor, new[] { eshop.SettlementId },
                    TradeFacts(player, eshop, keeper, def, c.Quantity, quote.UnitPrice, total), null, new[] { keeper.NpcId });
                return CommandResult.Ok($"Sold {(c.Quantity > 1 ? I(c.Quantity) + " × " : string.Empty)}{def.Name} for {I(total)} coin.");
            }
        }
    }
}
