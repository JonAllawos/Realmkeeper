using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;

namespace Realmkeeper.Application
{
    // Phase 11 (ADR-0051, ADR-0052, ADR-0054): moving items between holders (the pack, containers, shops), attunement
    // and curses, mending, and the player's work (crafting, gathering, repairing, attuning). Like every command, each
    // validates first and edits a working copy: a transfer that cannot complete moves nothing, so an item is never
    // duplicated or lost between two holders.

    /// <summary>Takes items from a container into the pack.</summary>
    public sealed class TakeFromContainerCommand : ICommand
    {
        public TakeFromContainerCommand(string containerKey, ItemStackId stackId, int quantity)
        {
            ContainerKey = containerKey;
            StackId = stackId;
            Quantity = quantity;
        }

        public string ContainerKey { get; }
        public ItemStackId StackId { get; }
        public int Quantity { get; }
    }

    /// <summary>Puts items from the pack into a container that accepts deposits.</summary>
    public sealed class PutInContainerCommand : ICommand
    {
        public PutInContainerCommand(string containerKey, ItemStackId stackId, int quantity)
        {
            ContainerKey = containerKey;
            StackId = stackId;
            Quantity = quantity;
        }

        public string ContainerKey { get; }
        public ItemStackId StackId { get; }
        public int Quantity { get; }
    }

    /// <summary>Takes everything (items and coin) from a container, or nothing if it will not all fit.</summary>
    public sealed class TakeAllFromContainerCommand : ICommand
    {
        public TakeAllFromContainerCommand(string containerKey) { ContainerKey = containerKey; }
        public string ContainerKey { get; }
    }

    /// <summary>Moves coin between the purse and a container (positive: take; negative: put in).</summary>
    public sealed class MoveContainerCoinCommand : ICommand
    {
        public MoveContainerCoinCommand(string containerKey, int amount)
        {
            ContainerKey = containerKey;
            Amount = amount;
        }

        public string ContainerKey { get; }
        public int Amount { get; }
    }

    /// <summary>Marks a container as looked into (it is then listed as opened).</summary>
    public sealed class OpenContainerCommand : ICommand
    {
        public OpenContainerCommand(string containerKey) { ContainerKey = containerKey; }
        public string ContainerKey { get; }
    }

    /// <summary>Ends attunement to an item (refused while a curse binds it).</summary>
    public sealed class EndAttunementCommand : ICommand
    {
        public EndAttunementCommand(ItemStackId stackId) { StackId = stackId; }
        public ItemStackId StackId { get; }
    }

    /// <summary>A shrine's priest lifts a curse: the item is no longer bound and attunement ends (a priced service).</summary>
    public sealed class LiftCurseCommand : ICommand
    {
        public LiftCurseCommand(ItemStackId stackId, int expectedPrice)
        {
            StackId = stackId;
            ExpectedPrice = expectedPrice;
        }

        public ItemStackId StackId { get; }
        public int ExpectedPrice { get; }
    }

    /// <summary>A smith mends an item (pack or worn) to good condition, at the price the player was shown.</summary>
    public sealed class RepairAtShopCommand : ICommand
    {
        public RepairAtShopCommand(ShopId shopId, ItemStackId stackId, int expectedPrice)
        {
            ShopId = shopId;
            StackId = stackId;
            ExpectedPrice = expectedPrice;
        }

        public ShopId ShopId { get; }
        public ItemStackId StackId { get; }
        public int ExpectedPrice { get; }
    }

    /// <summary>Buys one particular item a shop holds (its <c>Goods</c>: worn, fine or otherwise distinct things).</summary>
    public sealed class BuyGoodsCommand : ICommand
    {
        public BuyGoodsCommand(ShopId shopId, ItemStackId stackId, int expectedPrice)
        {
            ShopId = shopId;
            StackId = stackId;
            ExpectedPrice = expectedPrice;
        }

        public ShopId ShopId { get; }
        public ItemStackId StackId { get; }
        public int ExpectedPrice { get; }
    }

    /// <summary>Starts work: crafting a recipe, gathering at a site, repairing an item, or attuning to one.</summary>
    public sealed class StartWorkCommand : ICommand
    {
        private StartWorkCommand(WorkKind kind, string recipeId, string siteKey, ItemStackId stackId, ShopId at = default)
        {
            Kind = kind;
            RecipeId = recipeId;
            SiteKey = siteKey;
            StackId = stackId;
            At = at;
        }

        /// <summary>Crafts a recipe; <paramref name="at"/> names the shop whose station the player works at (Phase 21; empty: any open one here).</summary>
        public static StartWorkCommand Craft(string recipeId, ShopId at = default) => new StartWorkCommand(WorkKind.Craft, recipeId, null, default, at);
        public static StartWorkCommand Gather(string siteKey) => new StartWorkCommand(WorkKind.Gather, null, siteKey, default);
        public static StartWorkCommand Repair(ItemStackId stackId) => new StartWorkCommand(WorkKind.Repair, null, null, stackId);
        public static StartWorkCommand Attune(ItemStackId stackId) => new StartWorkCommand(WorkKind.Attune, null, null, stackId);

        public WorkKind Kind { get; }
        public string RecipeId { get; }
        public string SiteKey { get; }
        public ItemStackId StackId { get; }

        /// <summary>Phase 21: the shop whose station is worked at (empty: any open one in the settlement).</summary>
        public ShopId At { get; }
    }

    /// <summary>Collects finished work (its time has passed): the result goes to the pack.</summary>
    public sealed class CollectWorkCommand : ICommand
    {
        public CollectWorkCommand(string jobKey) { JobKey = jobKey; }
        public string JobKey { get; }
    }

    /// <summary>Abandons unfinished work: what it held comes back unchanged (a site gets its material back).</summary>
    public sealed class CancelWorkCommand : ICommand
    {
        public CancelWorkCommand(string jobKey) { JobKey = jobKey; }
        public string JobKey { get; }
    }

    public static class ItemCommandHandlers
    {
        /// <summary>A shrine's fee for lifting a curse.</summary>
        public const int LiftCursePrice = 60;

        /// <summary>Minutes per step of condition when the player mends something themselves.</summary>
        public const int RepairMinutesPerStep = 60;

        /// <summary>Attuning takes a short rest (SRD 5.2.1).</summary>
        public const int AttuneMinutes = CharacterRest.ShortRestMinutes;

        public const string NotInFights = "Not during a fight.";

        public static void Register(CommandRegistry commands)
        {
            commands.Register<TakeFromContainerCommand>(new TakeHandler());
            commands.Register<PutInContainerCommand>(new PutHandler());
            commands.Register<TakeAllFromContainerCommand>(new TakeAllHandler());
            commands.Register<MoveContainerCoinCommand>(new CoinHandler());
            commands.Register<OpenContainerCommand>(new OpenHandler());
            commands.Register<EndAttunementCommand>(new EndAttunementHandler());
            commands.Register<LiftCurseCommand>(new LiftCurseHandler());
            commands.Register<RepairAtShopCommand>(new RepairAtShopHandler());
            commands.Register<BuyGoodsCommand>(new BuyGoodsHandler());
            commands.Register<StartWorkCommand>(new StartWorkHandler());
            commands.Register<CollectWorkCommand>(new CollectWorkHandler());
            commands.Register<CancelWorkCommand>(new CancelWorkHandler());
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static PlayerCharacterData Player(CommandContext context, out CommandResult failure)
        {
            failure = null;
            if (!context.HasWorld) { failure = CommandResult.InvalidState("There is no active world."); return null; }
            if (context.Committed.Player == null) { failure = CommandResult.NotFound("This world has no player character."); return null; }
            if (context.Committed.ActiveEncounter != null) { failure = CommandResult.InvalidState(NotInFights); return null; }
            return context.Committed.Player;
        }

        // ----- Holding: the pack's weight limit and adding instances ----------------------------------

        /// <summary>The most the pack (with worn gear) may weigh: twice the carrying capacity (SRD 5.2.1; past it the player cannot move).</summary>
        public static int PackLimitGrams(PlayerCharacterData p)
        {
            if (p?.Rpg != null && p.Rpg.State == CreationState.Complete) return 2 * new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).CarryingCapacityGrams;
            return 2 * Carrying.CapacityGrams(10, 100);
        }

        /// <summary>Why <paramref name="grams"/> more cannot be carried, or null when it can.</summary>
        public static string WeightProblem(PlayerCharacterData p, int grams)
        {
            if (grams <= 0) return null;
            var after = ItemWeights.CarriedGrams(p) + grams;
            var limit = PackLimitGrams(p);
            if (after <= limit) return null;
            return $"Too heavy: that would be {I(Carrying.Pounds(after))} lb, and you can carry at most {I(Carrying.Pounds(limit))} lb.";
        }

        /// <summary>
        /// Adds an instance to an editable pack, merging it into a matching stack when the item stacks and the two are
        /// alike. The caller has checked the weight. The added stack keeps its ID when it is not merged.
        /// </summary>
        internal static void AddStack(PlayerCharacterData p, ItemStackData stack)
        {
            var def = ItemCatalog.Find(stack.ItemId);
            if (def != null && def.Stackable)
            {
                foreach (var s in p.Inventory)
                {
                    if (stack.Quantity == 0) break;
                    if (!s.SameAs(stack) || s.Quantity >= def.MaxStack) continue;
                    var add = Math.Min(stack.Quantity, def.MaxStack - s.Quantity);
                    s.Quantity += add;
                    stack.Quantity -= add;
                }

                while (stack.Quantity > def.MaxStack)
                {
                    var part = stack.Clone();
                    part.Quantity = def.MaxStack;
                    part.StackId = new ItemStackId(DerivedGuid(stack.StackId, p.Inventory.Count));
                    stack.Quantity -= def.MaxStack;
                    p.Inventory.Add(part);
                }
            }

            if (stack.Quantity > 0) p.Inventory.Add(stack);
        }

        /// <summary>
        /// A new item instance ID (ADR-0050). <see cref="CommandContext.NextGuid"/> is deterministic per day and event
        /// count, so two commands on a quiet day could draw the same value; an ID any holder already uses is skipped
        /// (the next draw differs), so every instance keeps exactly one identity.
        /// </summary>
        internal static ItemStackId NewStackId(CommandContext context)
        {
            var w = context.Edit();
            for (var i = 0; ; i++)
            {
                var id = new ItemStackId(context.NextGuid("item-stack"));
                if (!StackIdInUse(w, id) || i > 64) return id;
            }
        }

        internal static bool StackIdInUse(WorldData w, ItemStackId id)
        {
            if (w.Player != null)
            {
                foreach (var s in w.Player.Inventory) if (s.StackId == id) return true;
                foreach (var s in w.Player.Equipment.Values) if (s.StackId == id) return true;
            }

            foreach (var c in w.Containers) foreach (var s in c.Items) if (s.StackId == id) return true;
            foreach (var shop in w.Shops) foreach (var s in shop.Goods) if (s.StackId == id) return true;
            foreach (var j in w.WorkJobs) foreach (var s in j.Held) if (s.StackId == id) return true;
            return false;
        }

        /// <summary>A new work key, never one already in the world's work list.</summary>
        internal static string NewJobKey(CommandContext context)
        {
            var w = context.Edit();
            for (;;)
            {
                var key = "work:" + context.NextGuid("work-job").ToString("N");
                if (FindJob(w, key) == null) return key;
            }
        }

        /// <summary>A deterministic ID derived from another (for the second part of a split that no context is at hand for).</summary>
        internal static Guid DerivedGuid(ItemStackId from, int n)
        {
            var b = from.Value.ToByteArray();
            b[15] ^= (byte)(n + 1);
            b[14] ^= 0x5A;
            return new Guid(b);
        }

        /// <summary>Takes <paramref name="quantity"/> off a stack in a list, returning the part taken (the same instance when it is all of it).</summary>
        internal static ItemStackData TakeFrom(List<ItemStackData> list, ItemStackData stack, int quantity, CommandContext context)
        {
            if (quantity >= stack.Quantity)
            {
                list.Remove(stack);
                return stack;
            }

            stack.Quantity -= quantity;
            var part = stack.Clone();
            part.Quantity = quantity;
            part.StackId = NewStackId(context);
            return part;
        }

        // ----- Containers ---------------------------------------------------------------------------

        /// <summary>The container, open to the player here, or the reason why not.</summary>
        internal static ContainerData Reachable(WorldData world, PlayerCharacterData p, string key, out CommandResult failure)
        {
            failure = null;
            var c = world.FindContainer(key);
            if (c == null) { failure = CommandResult.NotFound("There is no such container."); return null; }
            if (!c.PlayerAccess) { failure = CommandResult.InvalidState($"{c.Name} is not yours to take from."); return null; }
            if (c.PlaceId != null)
            {
                // Phase 13: a container in a place is reached from inside that place.
                if (!p.IsInPlace || p.PlaceId != c.PlaceId) { failure = CommandResult.InvalidState($"{c.Name} is in {world.FindPlace(c.PlaceId)?.Name ?? "another place"}."); return null; }
                return c;
            }

            if (!c.SettlementId.IsEmpty && (!p.IsInSettlement || p.SettlementId != c.SettlementId))
            {
                failure = CommandResult.InvalidState($"{c.Name} is in {world.FindSettlement(c.SettlementId)?.Name ?? "another settlement"}.");
                return null;
            }

            return c;
        }

        private sealed class OpenHandler : ICommandHandler<OpenContainerCommand>
        {
            public CommandResult Handle(OpenContainerCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var box = Reachable(context.Committed, p, c.ContainerKey, out failure);
                if (box == null) return failure;
                if (box.Opened) return CommandResult.Ok($"{box.Name}.");
                context.Edit().FindContainer(c.ContainerKey).Opened = true;
                return CommandResult.Ok($"Opened {box.Name}.");
            }
        }

        private sealed class TakeHandler : ICommandHandler<TakeFromContainerCommand>
        {
            public CommandResult Handle(TakeFromContainerCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var box = Reachable(context.Committed, p, c.ContainerKey, out failure);
                if (box == null) return failure;
                var stack = box.Items.Find(x => x.StackId == c.StackId);
                if (stack == null) return CommandResult.NotFound("That is not in there.");
                if (c.Quantity < 1 || c.Quantity > stack.Quantity) return CommandResult.Invalid($"Take between 1 and {I(stack.Quantity)}.");
                var def = ItemCatalog.Find(stack.ItemId);
                var heavy = WeightProblem(p, (def?.WeightGrams ?? 0) * c.Quantity);
                if (heavy != null) return CommandResult.InvalidState(heavy);

                var edit = context.Edit();
                var ebox = edit.FindContainer(c.ContainerKey);
                var part = TakeFrom(ebox.Items, ebox.Items.Find(x => x.StackId == c.StackId), c.Quantity, context);
                if (part.AcquiredAt < 0) part.AcquiredAt = SpellCommandHandlers.Now(edit);
                ebox.Opened = true;
                AddStack(edit.Player, part);
                return CommandResult.Ok($"Took {(c.Quantity > 1 ? I(c.Quantity) + " × " : string.Empty)}{def?.Name ?? stack.ItemId} from {box.Name}.");
            }
        }

        private sealed class PutHandler : ICommandHandler<PutInContainerCommand>
        {
            public CommandResult Handle(PutInContainerCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var box = Reachable(context.Committed, p, c.ContainerKey, out failure);
                if (box == null) return failure;
                if (!box.AcceptsDeposits) return CommandResult.InvalidState($"You cannot leave things in {box.Name}.");
                var i = PlayerCommandHandlers.IndexOf(p, c.StackId);
                if (i < 0) return CommandResult.NotFound("That item is not in your pack.");
                var stack = p.Inventory[i];
                if (c.Quantity < 1 || c.Quantity > stack.Quantity) return CommandResult.Invalid($"Put in between 1 and {I(stack.Quantity)}.");
                if (stack.Bound) return CommandResult.InvalidState("A curse binds it to you.");
                if (stack.Attuned) return CommandResult.InvalidState("End your attunement to it first.");
                var def = ItemCatalog.Find(stack.ItemId);
                var grams = (def?.WeightGrams ?? 0) * c.Quantity;
                if (box.CapacityGrams > 0 && ItemWeights.Grams(box.Items) + grams > box.CapacityGrams)
                    return CommandResult.InvalidState($"{box.Name} is too full: it holds {I(Carrying.Pounds(box.CapacityGrams))} lb.");

                var edit = context.Edit();
                var ebox = edit.FindContainer(c.ContainerKey);
                var player = edit.Player;
                var part = TakeFrom(player.Inventory, player.Inventory[PlayerCommandHandlers.IndexOf(player, c.StackId)], c.Quantity, context);
                Merge(ebox.Items, part);
                return CommandResult.Ok($"Put {(c.Quantity > 1 ? I(c.Quantity) + " × " : string.Empty)}{def?.Name ?? stack.ItemId} in {box.Name}.");
            }
        }

        /// <summary>Adds an instance to a container's list, merging alike stacks of stackable items.</summary>
        internal static void Merge(List<ItemStackData> into, ItemStackData stack)
        {
            var def = ItemCatalog.Find(stack.ItemId);
            if (def != null && def.Stackable)
                foreach (var s in into)
                {
                    if (stack.Quantity == 0) return;
                    if (!s.SameAs(stack) || s.Quantity >= def.MaxStack) continue;
                    var add = Math.Min(stack.Quantity, def.MaxStack - s.Quantity);
                    s.Quantity += add;
                    stack.Quantity -= add;
                }

            if (stack.Quantity > 0) into.Add(stack);
        }

        private sealed class TakeAllHandler : ICommandHandler<TakeAllFromContainerCommand>
        {
            public CommandResult Handle(TakeAllFromContainerCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var box = Reachable(context.Committed, p, c.ContainerKey, out failure);
                if (box == null) return failure;
                if (box.IsEmpty) return CommandResult.InvalidState($"{box.Name} is empty.");
                var heavy = WeightProblem(p, ItemWeights.Grams(box.Items));
                if (heavy != null) return CommandResult.InvalidState(heavy + " Take some of it instead.");

                var edit = context.Edit();
                var ebox = edit.FindContainer(c.ContainerKey);
                var now = SpellCommandHandlers.Now(edit);
                var names = new List<string>();
                foreach (var s in ebox.Items)
                {
                    if (s.AcquiredAt < 0) s.AcquiredAt = now;
                    names.Add((s.Quantity > 1 ? I(s.Quantity) + " × " : string.Empty) + (ItemCatalog.Find(s.ItemId)?.Name ?? s.ItemId));
                    AddStack(edit.Player, s);
                }

                ebox.Items.Clear();
                if (ebox.Coin > 0)
                {
                    names.Add(I(ebox.Coin) + " coin");
                    edit.Player.Purse += ebox.Coin;
                    ebox.Coin = 0;
                }

                ebox.Opened = true;
                return CommandResult.Ok($"Took everything from {box.Name}: {string.Join(", ", names)}.");
            }
        }

        private sealed class CoinHandler : ICommandHandler<MoveContainerCoinCommand>
        {
            public CommandResult Handle(MoveContainerCoinCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var box = Reachable(context.Committed, p, c.ContainerKey, out failure);
                if (box == null) return failure;
                if (c.Amount == 0) return CommandResult.Invalid("Move at least one coin.");
                if (c.Amount > 0 && c.Amount > box.Coin) return CommandResult.InvalidState($"{box.Name} holds {I(box.Coin)} coin.");
                if (c.Amount < 0 && !box.AcceptsDeposits) return CommandResult.InvalidState($"You cannot leave coin in {box.Name}.");
                if (c.Amount < 0 && -c.Amount > p.Purse) return CommandResult.InvalidState($"You have {I(p.Purse)} coin.");
                var edit = context.Edit();
                var ebox = edit.FindContainer(c.ContainerKey);
                ebox.Coin -= c.Amount;
                edit.Player.Purse += c.Amount;
                return CommandResult.Ok(c.Amount > 0 ? $"Took {I(c.Amount)} coin." : $"Left {I(-c.Amount)} coin in {box.Name}.");
            }
        }

        // ----- Attunement and curses ----------------------------------------------------------------

        /// <summary>The player's instance with this ID, in the pack or worn (and where), or null.</summary>
        internal static ItemStackData Held(PlayerCharacterData p, ItemStackId id, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.None;
            foreach (var s in p.Inventory) if (s.StackId == id) return s;
            foreach (var pair in p.Equipment)
                if (pair.Value.StackId == id)
                {
                    slot = pair.Key;
                    return pair.Value;
                }

            return null;
        }

        public static int AttunedCount(PlayerCharacterData p)
        {
            var n = 0;
            foreach (var s in p.Inventory) if (s.Attuned) n++;
            foreach (var s in p.Equipment.Values) if (s.Attuned) n++;
            return n;
        }

        /// <summary>Why the player cannot attune to this item (null when they can).</summary>
        public static string AttuneProblem(PlayerCharacterData p, ItemStackData s)
        {
            if (s == null) return "You are not carrying that.";
            var def = ItemCatalog.Find(s.ItemId);
            if (def?.Magic == null || !def.Magic.RequiresAttunement) return $"{def?.Name ?? "That"} does not need attunement.";
            if (s.Attuned) return "You are already attuned to it.";
            if (AttunedCount(p) >= Carrying.MaxAttuned) return $"You are attuned to {I(Carrying.MaxAttuned)} items already; end one attunement first.";
            return null;
        }

        private sealed class EndAttunementHandler : ICommandHandler<EndAttunementCommand>
        {
            public CommandResult Handle(EndAttunementCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var s = Held(p, c.StackId, out _);
                if (s == null) return CommandResult.NotFound("You are not carrying that.");
                if (!s.Attuned) return CommandResult.InvalidState("You are not attuned to it.");
                if (s.Bound) return CommandResult.InvalidState("A curse binds you to it; a shrine's priest can lift it.");
                Held(context.Edit().Player, c.StackId, out _).Attuned = false;
                return CommandResult.Ok($"Ended your attunement to the {ItemCatalog.Find(s.ItemId)?.Name}.");
            }
        }

        /// <summary>A shrine in the player's settlement (a place of worship), or null.</summary>
        internal static LocationData Shrine(WorldData world, PlayerCharacterData p)
        {
            if (!p.IsInSettlement) return null;
            foreach (var l in world.Locations) if (l.SettlementId == p.SettlementId && l.Kind == LocationKind.Worship) return l;
            return null;
        }

        private sealed class LiftCurseHandler : ICommandHandler<LiftCurseCommand>
        {
            public CommandResult Handle(LiftCurseCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var shrine = Shrine(context.Committed, p);
                if (shrine == null) return CommandResult.InvalidState("There is no shrine here to lift a curse.");
                var s = Held(p, c.StackId, out _);
                if (s == null) return CommandResult.NotFound("You are not carrying that.");
                if (!s.Bound) return CommandResult.InvalidState("No curse binds it to you.");
                if (c.ExpectedPrice != LiftCursePrice) return CommandResult.InvalidState($"The offering is {I(LiftCursePrice)} coin.");
                if (p.Purse < LiftCursePrice) return CommandResult.InvalidState($"The offering is {I(LiftCursePrice)} coin; you have {I(p.Purse)}.");
                var edit = context.Edit();
                edit.Player.Purse -= LiftCursePrice;
                var es = Held(edit.Player, c.StackId, out _);
                es.Bound = false;
                es.Attuned = false;
                return CommandResult.Ok($"The priest at {shrine.Name} lifts the curse on the {ItemCatalog.Find(s.ItemId)?.Name} ({I(LiftCursePrice)} coin). It no longer binds you, and your attunement to it has ended.");
            }
        }

        // ----- Shops: repair and particular goods ---------------------------------------------------

        private static ShopData OpenShop(WorldData world, PlayerCharacterData p, ShopId id, out CommandResult failure)
        {
            failure = null;
            var shop = world.FindShop(id);
            if (shop == null) { failure = CommandResult.NotFound("There is no such shop."); return null; }
            if (!p.IsInSettlement || p.SettlementId != shop.SettlementId) { failure = CommandResult.InvalidState("You are not where that shop is."); return null; }
            var open = CommerceMath.Availability(world, shop);
            if (!open.IsOpen) { failure = CommandResult.InvalidState(open.Reason); return null; }
            return shop;
        }

        /// <summary>True when a shop mends weapons, armour and tools (one that deals in them).</summary>
        public static bool Repairs(ShopData shop) => shop.Categories.Contains(ShopCategory.WeaponsAndArmour);

        private sealed class RepairAtShopHandler : ICommandHandler<RepairAtShopCommand>
        {
            public CommandResult Handle(RepairAtShopCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var shop = OpenShop(context.Committed, p, c.ShopId, out failure);
                if (shop == null) return failure;
                if (!Repairs(shop)) return CommandResult.InvalidState($"{shop.Name} does not mend arms or armour.");
                var s = Held(p, c.StackId, out _);
                if (s == null) return CommandResult.NotFound("You are not carrying that.");
                var def = ItemCatalog.Find(s.ItemId);
                if (def == null || !def.Wears) return CommandResult.Invalid($"{def?.Name ?? "That"} does not need mending.");
                var price = ItemStacks.RepairPrice(def, s);
                if (price == 0) return CommandResult.InvalidState($"Your {def.Name.ToLowerInvariant()} is in {ItemConditions.Name(s.Condition)} condition already.");
                if (price != c.ExpectedPrice) return CommandResult.InvalidState($"The price has changed: mending it now costs {I(price)} coin.");
                if (p.Purse < price) return CommandResult.InvalidState($"Mending it costs {I(price)} coin; you have {I(p.Purse)}.");
                var edit = context.Edit();
                edit.Player.Purse -= price;
                var before = s.Condition;
                Held(edit.Player, c.StackId, out _).Condition = ItemCondition.Good;
                var eshop = edit.FindShop(shop.ShopId);
                eshop.TotalSales += price;
                var keeper = edit.FindNpc(eshop.ShopkeeperNpcId);
                if (keeper != null) keeper.Condition.Wealth += price;
                return CommandResult.Ok($"{shop.Name} mends your {def.Name.ToLowerInvariant()}: {ItemConditions.Name(before)} → good, for {I(price)} coin.");
            }
        }

        /// <summary>What the player pays for one particular instance a shop holds.</summary>
        public static int GoodsPrice(WorldData world, ShopData shop, ItemStackData s)
        {
            var def = ItemCatalog.Find(s.ItemId);
            if (def == null) return 0;
            var unit = CommerceMath.SellingPrice(world, shop, def).UnitPrice;
            return Math.Max(1, unit * ItemStacks.ValuePercent(s) / 100);
        }

        private sealed class BuyGoodsHandler : ICommandHandler<BuyGoodsCommand>
        {
            public CommandResult Handle(BuyGoodsCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var world = context.Committed;
                var shop = OpenShop(world, p, c.ShopId, out failure);
                if (shop == null) return failure;
                var s = shop.Goods.Find(x => x.StackId == c.StackId);
                if (s == null) return CommandResult.NotFound($"{shop.Name} no longer has that.");
                var def = ItemCatalog.Find(s.ItemId);
                var price = GoodsPrice(world, shop, s);
                if (price != c.ExpectedPrice) return CommandResult.InvalidState($"The price has changed: it now costs {I(price)} coin.");
                if (p.Purse < price) return CommandResult.InvalidState($"You need {I(price)} coin and have {I(p.Purse)}.");
                var heavy = WeightProblem(p, ItemWeights.StackGrams(s));
                if (heavy != null) return CommandResult.InvalidState(heavy);

                var edit = context.Edit();
                var eshop = edit.FindShop(shop.ShopId);
                var es = eshop.Goods.Find(x => x.StackId == c.StackId);
                eshop.Goods.Remove(es);
                edit.Player.Purse -= price;
                es.AcquiredAt = SpellCommandHandlers.Now(edit);
                AddStack(edit.Player, es);
                eshop.TotalSales += price;
                var keeper = edit.FindNpc(eshop.ShopkeeperNpcId);
                if (keeper != null) keeper.Condition.Wealth += price;
                PlayerCommandHandlers.RecordTrade(context, edit, TransactionKind.Purchase, eshop, s.ItemId, s.Quantity, price / Math.Max(1, s.Quantity));
                return CommandResult.Ok($"Bought the {ItemConditions.Name(s.Condition)} {def?.Name.ToLowerInvariant()} for {I(price)} coin.");
            }
        }

        // ----- Work: crafting, gathering, repairing, attuning ---------------------------------------

        public static WorkJobData ActiveJob(WorldData world)
        {
            foreach (var j in world.WorkJobs) if (!j.Done) return j;
            return null;
        }

        /// <summary>True when the player's settlement has a place of this kind (a smithy is a workshop).</summary>
        public static bool HasStation(WorldData world, PlayerCharacterData p, LocationKind? station)
        {
            if (!p.IsInSettlement) return false;
            if (station == null) return true;
            foreach (var l in world.Locations) if (l.SettlementId == p.SettlementId && l.Kind == station.Value) return true;
            return false;
        }

        /// <summary>A usable (not broken) tool the player carries, by item ID or by discipline.</summary>
        public static ItemStackData Tool(PlayerCharacterData p, string toolId, string discipline)
        {
            bool Fits(ItemStackData s)
            {
                if (s.Condition == ItemCondition.Broken) return false;
                if (toolId != null) return s.ItemId == toolId;
                return ItemCatalog.Find(s.ItemId)?.ToolDiscipline == discipline;
            }

            foreach (var s in p.Inventory) if (Fits(s)) return s;
            foreach (var s in p.Equipment.Values) if (Fits(s)) return s;
            return null;
        }

        public static int CountInPack(PlayerCharacterData p, string itemId)
        {
            var n = 0;
            foreach (var s in p.Inventory) if (s.ItemId == itemId && !s.Bound && !s.Attuned) n += s.Quantity;
            return n;
        }

        /// <summary>Why a recipe cannot be started now (null when it can): place, station, tool and materials.</summary>
        public static string CraftProblem(WorldData world, PlayerCharacterData p, RecipeDefinition r, ShopId at = default)
        {
            if (r == null) return "Unknown recipe.";
            if (!p.IsInSettlement) return "Crafting needs a settlement's shelter and fire.";
            if (r.StationKind != StationKind.None)
            {
                // Phase 21 (ADR-0107): a physical station in an open shop here; the older place kind (a shrine's still, a
                // tavern's kitchen, a village workshop) still serves where the recipe names one.
                var station = Simulation.Commerce.CraftingStations.Problem(world, p.SettlementId, r, at, out _);
                if (station != null && !(at.IsEmpty && r.Station != null && HasStation(world, p, r.Station))) return station;
            }
            else if (!HasStation(world, p, r.Station)) return $"{r.Name} needs a {StationName(r.Station)}, and there is none here.";
            if (r.ToolId != null && Tool(p, r.ToolId, null) == null) return $"You need {ItemCatalog.Find(r.ToolId)?.Name.ToLowerInvariant() ?? r.ToolId} (not broken).";
            foreach (var (item, qty) in r.Inputs)
                if (CountInPack(p, item) < qty) return $"You need {I(qty)} {ItemCatalog.Find(item)?.Name.ToLowerInvariant() ?? item} and have {I(CountInPack(p, item))}.";
            return null;
        }

        public static string StationName(LocationKind? k)
        {
            switch (k)
            {
                case LocationKind.Workshop: return "smithy or workshop";
                case LocationKind.Tavern: return "tavern kitchen";
                case LocationKind.Worship: return "shrine's stillroom";
                case null: return "settlement";
                default: return k.Value.ToString().ToLowerInvariant();
            }
        }

        /// <summary>The check bonus for work with a tool: the ability modifier, plus proficiency when proficient with the tool.</summary>
        public static int CheckBonus(PlayerCharacterData p, Ability ability, string toolId, out bool proficient)
        {
            var rules = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
            proficient = false;
            if (toolId != null) foreach (var t in rules.ToolProficiencies) if (t == toolId) proficient = true;
            return AbilityRules.Modifier(rules.Score(ability)) + (proficient ? rules.ProficiencyBonus : 0);
        }

        /// <summary>Removes <paramref name="quantity"/> of an item from an editable pack (smallest stacks first), returning what was taken.</summary>
        internal static List<ItemStackData> Consume(PlayerCharacterData p, string itemId, int quantity, CommandContext context)
        {
            var taken = new List<ItemStackData>();
            var stacks = p.Inventory.FindAll(s => s.ItemId == itemId && !s.Bound && !s.Attuned);
            stacks.Sort((a, b) => a.Quantity != b.Quantity ? a.Quantity.CompareTo(b.Quantity) : a.StackId.Value.CompareTo(b.StackId.Value));
            var left = quantity;
            foreach (var s in stacks)
            {
                if (left == 0) break;
                var n = Math.Min(left, s.Quantity);
                taken.Add(TakeFrom(p.Inventory, s, n, context));
                left -= n;
            }

            return taken;
        }

        private sealed class StartWorkHandler : ICommandHandler<StartWorkCommand>
        {
            public CommandResult Handle(StartWorkCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                if (p.Rpg == null || p.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
                var world = context.Committed;
                var busy = ActiveJob(world);
                if (busy != null) return CommandResult.InvalidState($"You are already busy: {Describe(busy)}. Finish or abandon it first.");
                switch (c.Kind)
                {
                    case WorkKind.Craft: return StartCraft(c, context, p);
                    case WorkKind.Gather: return StartGather(c, context, p);
                    case WorkKind.Repair: return StartRepair(c, context, p);
                    case WorkKind.Attune: return StartAttune(c, context, p);
                    default: return CommandResult.Invalid("Unknown kind of work.");
                }
            }

            private static WorkJobData NewJob(CommandContext context, WorldData edit, WorkKind kind, int minutes)
            {
                var now = SpellCommandHandlers.Now(edit);
                var job = new WorkJobData
                {
                    Key = NewJobKey(context),
                    Kind = kind,
                    SettlementId = edit.Player.SettlementId,
                    StartedAt = now,
                    ReadyAt = now + minutes,
                };
                edit.WorkJobs.Add(job);
                while (edit.WorkJobs.Count > 40 && edit.WorkJobs[0].Done) edit.WorkJobs.RemoveAt(0);
                return job;
            }

            private static void Roll(WorldData edit, WorkJobData job, int bonus, int dc)
            {
                var die = SpellCommandHandlers.Dice(edit, job.Key);
                job.Roll = die(20);
                job.Total = job.Roll + bonus;
                job.Dc = dc;
                job.Succeeded = dc <= 0 || job.Total >= dc;
            }

            private static CommandResult StartCraft(StartWorkCommand c, CommandContext context, PlayerCharacterData p)
            {
                var r = RecipeCatalog.Find(c.RecipeId);
                if (r == null) return CommandResult.NotFound($"Unknown recipe '{c.RecipeId}'.");
                var problem = CraftProblem(context.Committed, p, r, c.At);
                if (problem != null) return CommandResult.InvalidState(problem);
                var bonus = CheckBonus(p, r.CheckAbility, r.ToolId, out var proficient);

                var edit = context.Edit();
                var job = NewJob(context, edit, WorkKind.Craft, r.Minutes);
                job.RecipeId = r.Id;
                foreach (var (item, qty) in r.Inputs) job.Held.AddRange(Consume(edit.Player, item, qty, context));
                Roll(edit, job, bonus, r.Dc);
                job.OutputItemId = r.OutputItemId;
                job.OutputQuantity = job.Succeeded ? r.OutputQuantity : 0;
                job.OutputQuality = job.Succeeded && r.CanBeFine && r.Dc > 0 && job.Total >= r.Dc + 5 ? ItemQuality.Fine : ItemQuality.Common;
                var check = r.Dc > 0 ? $" ({r.CheckAbility} check{(proficient ? " with proficiency" : string.Empty)}: d20 {I(job.Roll)} {Signed(bonus)} = {I(job.Total)} vs DC {I(r.Dc)})" : string.Empty;
                job.Outcome = job.Succeeded
                    ? $"{r.Name}: success{check}{(job.OutputQuality == ItemQuality.Fine ? " — fine work" : string.Empty)}."
                    : $"{r.Name}: failed{check}; {I(r.FailureLossPercent)}% of the materials are spoiled.";
                return CommandResult.Ok($"Started: {r.Name}. Ready in {Duration(r.Minutes)}.");
            }

            private static CommandResult StartGather(StartWorkCommand c, CommandContext context, PlayerCharacterData p)
            {
                var world = context.Committed;
                GatheringSiteData site = null;
                foreach (var s in world.GatheringSites) if (s.Key == c.SiteKey) site = s;
                if (site == null) return CommandResult.NotFound("There is no such place to gather.");
                if (!p.IsInSettlement || p.SettlementId != site.SettlementId) return CommandResult.InvalidState($"{site.Name} is in another settlement's land.");
                if (site.Available <= 0) return CommandResult.InvalidState($"{site.Name} is worked out for now; it recovers week by week.");
                if (site.ToolDiscipline != null && Tool(p, null, site.ToolDiscipline) == null) return CommandResult.InvalidState($"{site.Name} needs a {site.ToolDiscipline} tool (not broken).");
                var def = ItemCatalog.Find(site.ItemId);
                var take = Math.Min(site.Yield, site.Available);
                var heavy = WeightProblem(p, (def?.WeightGrams ?? 0) * take);
                if (heavy != null) return CommandResult.InvalidState(heavy);

                var edit = context.Edit();
                foreach (var s in edit.GatheringSites) if (s.Key == site.Key) s.Available -= take;
                var job = NewJob(context, edit, WorkKind.Gather, site.Minutes);
                job.SiteKey = site.Key;
                job.OutputItemId = site.ItemId;
                job.OutputQuantity = take;
                job.Succeeded = true;
                job.Outcome = $"{site.Name}: gathered {I(take)} {def?.Name.ToLowerInvariant()}.";
                return CommandResult.Ok($"Started gathering at {site.Name}. Ready in {Duration(site.Minutes)}.");
            }

            private static CommandResult StartRepair(StartWorkCommand c, CommandContext context, PlayerCharacterData p)
            {
                var i = PlayerCommandHandlers.IndexOf(p, c.StackId);
                if (i < 0) return CommandResult.NotFound("Mend things from your pack (take it off first).");
                var s = p.Inventory[i];
                var def = ItemCatalog.Find(s.ItemId);
                var steps = ItemStacks.RepairSteps(s);
                if (def == null || !def.Wears) return CommandResult.Invalid($"{def?.Name ?? "That"} does not need mending.");
                if (steps == 0) return CommandResult.InvalidState($"It is in {ItemConditions.Name(s.Condition)} condition already.");
                if (s.Quantity != 1) return CommandResult.Invalid("Mend one thing at a time.");
                var discipline = ItemStacks.RepairDiscipline(def);
                var tool = Tool(p, null, discipline);
                if (tool == null || tool.StackId == s.StackId) return CommandResult.InvalidState($"Mending it needs {discipline} tools (not broken).");
                var ability = discipline == "blacksmithing" ? Ability.Strength : Ability.Dexterity;
                var bonus = CheckBonus(p, ability, tool.ItemId, out var proficient);

                var edit = context.Edit();
                var job = NewJob(context, edit, WorkKind.Repair, RepairMinutesPerStep * steps);
                job.StackId = s.StackId;
                job.Held.Add(TakeFrom(edit.Player.Inventory, edit.Player.Inventory[PlayerCommandHandlers.IndexOf(edit.Player, s.StackId)], 1, context));
                Roll(edit, job, bonus, 10 + 2 * steps);
                job.OutputItemId = s.ItemId;
                job.OutputQuantity = 1;
                job.Outcome = $"Mending the {def.Name.ToLowerInvariant()} ({ability} check{(proficient ? " with proficiency" : string.Empty)}: d20 {I(job.Roll)} {Signed(bonus)} = {I(job.Total)} vs DC {I(job.Dc)}): "
                    + (job.Succeeded ? "it is in good condition again." : "the work did not hold; it is as it was.");
                return CommandResult.Ok($"Started mending the {def.Name.ToLowerInvariant()}. Ready in {Duration(job.ReadyAt - job.StartedAt)}.");
            }

            private static CommandResult StartAttune(StartWorkCommand c, CommandContext context, PlayerCharacterData p)
            {
                var s = Held(p, c.StackId, out _);
                var problem = AttuneProblem(p, s);
                if (problem != null) return CommandResult.InvalidState(problem);
                var def = ItemCatalog.Find(s.ItemId);
                var edit = context.Edit();
                var job = NewJob(context, edit, WorkKind.Attune, AttuneMinutes);
                job.StackId = s.StackId;
                job.OutputItemId = s.ItemId;
                job.Succeeded = true;
                job.Outcome = $"Attuned to the {def.Name}." + (def.Magic.Curse != null ? " " + def.Magic.Curse : string.Empty);
                return CommandResult.Ok($"You settle down to attune to the {def.Name}: a short rest ({Duration(AttuneMinutes)}).");
            }
        }

        private static string Signed(int v) => v >= 0 ? "+ " + I(v) : "− " + I(-v);

        public static string Duration(long minutes) => minutes >= 60 ? $"{minutes / 60} h{(minutes % 60 != 0 ? $" {minutes % 60} min" : string.Empty)}" : $"{minutes} min";

        public static string Describe(WorkJobData j)
        {
            switch (j.Kind)
            {
                case WorkKind.Craft: return RecipeCatalog.Find(j.RecipeId)?.Name ?? "crafting";
                case WorkKind.Gather: return "gathering " + (ItemCatalog.Find(j.OutputItemId)?.Name.ToLowerInvariant() ?? string.Empty);
                case WorkKind.Repair: return "mending the " + (ItemCatalog.Find(j.OutputItemId)?.Name.ToLowerInvariant() ?? "item");
                default: return "attuning to the " + (ItemCatalog.Find(j.OutputItemId)?.Name ?? "item");
            }
        }

        internal static WorkJobData FindJob(WorldData world, string key)
        {
            foreach (var j in world.WorkJobs) if (j.Key == key) return j;
            return null;
        }

        private sealed class CollectWorkHandler : ICommandHandler<CollectWorkCommand>
        {
            public CommandResult Handle(CollectWorkCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var world = context.Committed;
                var job = FindJob(world, c.JobKey);
                if (job == null) return CommandResult.NotFound("There is no such work.");
                if (job.Done) return CommandResult.InvalidState("That work is already finished and collected.");
                var now = SpellCommandHandlers.Now(world);
                if (now < job.ReadyAt) return CommandResult.InvalidState($"Not finished: {Duration(job.ReadyAt - now)} to go.");
                if (job.Kind != WorkKind.Attune && job.Kind != WorkKind.Gather && (!p.IsInSettlement || p.SettlementId != job.SettlementId))
                    return CommandResult.InvalidState($"Your work is in {world.FindSettlement(job.SettlementId)?.Name ?? "another settlement"}.");

                // What comes back, and its weight.
                var outDef = ItemCatalog.Find(job.OutputItemId);
                var grams = 0;
                switch (job.Kind)
                {
                    case WorkKind.Craft:
                        grams = (outDef?.WeightGrams ?? 0) * job.OutputQuantity;
                        if (!job.Succeeded) grams += Returned(job, context: null, count: true);
                        break;
                    case WorkKind.Gather: grams = (outDef?.WeightGrams ?? 0) * job.OutputQuantity; break;
                    case WorkKind.Repair: grams = ItemWeights.Grams(job.Held); break;
                }

                var heavy = WeightProblem(p, grams);
                if (heavy != null) return CommandResult.InvalidState(heavy + " Make room first.");

                var edit = context.Edit();
                var ejob = FindJob(edit, c.JobKey);
                var player = edit.Player;
                ejob.Done = true;
                switch (ejob.Kind)
                {
                    case WorkKind.Craft:
                        if (ejob.Succeeded)
                        {
                            var made = ItemStacks.Create(NewStackId(context), outDef, ejob.OutputQuantity, "Made by you", now);
                            made.Quality = ejob.OutputQuality;
                            AddStack(player, made);
                            context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = Realmkeeper.Simulation.Quests.QuestEventKind.Crafted, Subject = made.ItemId, Quantity = made.Quantity }); // Phase 12
                        }
                        else Returned(ejob, context, count: false, into: player);

                        ejob.Held.Clear();
                        break;
                    case WorkKind.Gather:
                        AddStack(player, ItemStacks.Create(NewStackId(context), outDef, ejob.OutputQuantity, "Gathered by you", now));
                        break;
                    case WorkKind.Repair:
                        foreach (var h in ejob.Held)
                        {
                            if (ejob.Succeeded) h.Condition = ItemCondition.Good;
                            AddStack(player, h);
                        }

                        ejob.Held.Clear();
                        break;
                    case WorkKind.Attune:
                        var s = Held(player, ejob.StackId, out _);
                        var problem = AttuneProblem(player, s);
                        if (problem != null) return CommandResult.InvalidState("The attunement did not take: " + problem);
                        s.Attuned = true;
                        if (ItemCatalog.Find(s.ItemId)?.Magic?.Curse != null) s.Bound = true;
                        break;
                }

                return CommandResult.Ok(ejob.Outcome);
            }

            /// <summary>The materials a failed craft gives back (the recipe's loss taken from each held stack, rounded against the player).</summary>
            private static int Returned(WorkJobData job, CommandContext context, bool count, PlayerCharacterData into = null)
            {
                var r = RecipeCatalog.Find(job.RecipeId);
                var keep = 100 - (r?.FailureLossPercent ?? 100);
                var grams = 0;
                foreach (var h in job.Held)
                {
                    var back = h.Quantity * keep / 100;
                    if (back <= 0) continue;
                    if (count) grams += (ItemCatalog.Find(h.ItemId)?.WeightGrams ?? 0) * back;
                    else
                    {
                        var part = h.Clone();
                        part.Quantity = back;
                        AddStack(into, part);
                    }
                }

                return grams;
            }
        }

        private sealed class CancelWorkHandler : ICommandHandler<CancelWorkCommand>
        {
            public CommandResult Handle(CancelWorkCommand c, CommandContext context)
            {
                var p = Player(context, out var failure);
                if (p == null) return failure;
                var job = FindJob(context.Committed, c.JobKey);
                if (job == null) return CommandResult.NotFound("There is no such work.");
                if (job.Done) return CommandResult.InvalidState("That work is already finished.");
                var heavy = WeightProblem(p, ItemWeights.Grams(job.Held));
                if (heavy != null) return CommandResult.InvalidState(heavy + " Make room first.");
                var edit = context.Edit();
                var ejob = FindJob(edit, c.JobKey);
                foreach (var h in ejob.Held) AddStack(edit.Player, h);
                ejob.Held.Clear();
                if (ejob.Kind == WorkKind.Gather)
                    foreach (var s in edit.GatheringSites)
                        if (s.Key == ejob.SiteKey) s.Available = Math.Min(s.Capacity, s.Available + ejob.OutputQuantity);
                edit.WorkJobs.Remove(ejob);
                return CommandResult.Ok($"Abandoned {Describe(job)}; everything it held is back in your pack.");
            }
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>
        /// Waits out the player's current work (the world lives through the time) and collects it. If the time cannot
        /// pass (a fight, a failure), nothing is collected.
        /// </summary>
        public CommandResult FinishWork(string jobKey)
        {
            if (!HasWorld) return CommandResult.InvalidState("There is no active world.");
            var job = ItemCommandHandlers.FindJob(_world, jobKey);
            if (job == null || job.Done) return CommandResult.NotFound("There is no such work in progress.");
            var left = job.ReadyAt - SpellCommandHandlers.Now(_world);
            if (left > 0)
            {
                var time = Execute(new AdvanceTimeCommand((int)Math.Min(left, AdvanceTimeCommand.MaxMinutes)));
                if (!time.IsSuccess) return CommandResult.InvalidState("The work was interrupted: " + time.Message);
            }

            return Dispatch(new CollectWorkCommand(jobKey));
        }
    }
}
