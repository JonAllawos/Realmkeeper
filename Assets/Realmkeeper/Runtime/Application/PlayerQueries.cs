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
    // Phase 5 read side: the clock, the Player Hub, shops with explained prices, the trade ledger and
    // where people are this hour. Snapshots only; nothing here changes the world.

    public sealed class WorldTimeSummary
    {
        internal WorldTimeSummary(WorldDate date, int minute)
        {
            Date = date;
            MinuteOfDay = minute;
            DayPart = NpcActivityPlan.DefaultParts.PartOf(minute);
        }

        public WorldDate Date { get; }
        public int MinuteOfDay { get; }
        public DayPart DayPart { get; }
        public int Hour => MinuteOfDay / 60;
        public string Clock => WorldTime.Clock(MinuteOfDay);
        public string Text => Clock + " · " + DayPart + " · " + Date;
    }

    /// <summary>One item as the hub and shop show it.</summary>
    public sealed class ItemView
    {
        internal ItemView(ItemDefinition d, ItemStackId stack, int quantity, EquipmentSlot equippedIn)
        {
            StackId = stack;
            ItemId = d.Id;
            Name = d.Name;
            Description = d.Description;
            Category = d.Category;
            Quality = d.Quality;
            Quantity = quantity;
            WeightGrams = d.WeightGrams;
            BaseValue = d.BaseValue;
            Slot = d.Slot;
            TwoHanded = d.TwoHanded;
            Visual = d.Visual;
            Protection = d.Protection;
            Damage = d.Damage;
            WeaponClass = d.WeaponClass;
            Nourishment = d.Nourishment;
            Stackable = d.Stackable;
            MaxStack = d.Stackable ? d.MaxStack : 1;
            EquippedIn = equippedIn;
        }

        public ItemStackId StackId { get; }
        public string ItemId { get; }
        public string Name { get; }
        public string Description { get; }
        public ItemCategory Category { get; }
        public ItemQuality Quality { get; }
        public int Quantity { get; }
        public int WeightGrams { get; }
        public int BaseValue { get; }
        public EquipmentSlot Slot { get; }
        public bool TwoHanded { get; }
        public string Visual { get; }
        public int Protection { get; }
        public int Damage { get; }
        public WeaponClass WeaponClass { get; }
        public int Nourishment { get; }
        public bool Stackable { get; }
        public int MaxStack { get; }
        public EquipmentSlot EquippedIn { get; }
        public bool IsEquipped => EquippedIn != EquipmentSlot.None;
        public bool CanEquip => Slot != EquipmentSlot.None && !IsEquipped;
        public bool CanUse => Nourishment > 0 && !IsEquipped;
        public string Weight => (WeightGrams * Quantity / 1000f).ToString("0.0#", CultureInfo.InvariantCulture) + " kg";
    }

    public sealed class RegardView
    {
        internal RegardView(NpcData n)
        {
            NpcId = n.NpcId;
            Name = n.Name;
            Role = n.Role;
            Familiarity = n.PlayerRegard.Familiarity;
            Affinity = n.PlayerRegard.Affinity;
            Trades = n.PlayerRegard.Trades;
            Conversations = n.PlayerRegard.Conversations;
        }

        public NpcId NpcId { get; }
        public string Name { get; }
        public NpcRole Role { get; }
        public int Familiarity { get; }
        public int Affinity { get; }
        public int Trades { get; }
        public int Conversations { get; }

        /// <summary>Why they know the player, in words.</summary>
        public string Why => $"{Conversations} conversation(s), {Trades} trade(s)";
    }

    /// <summary>Everything the Player Hub shows.</summary>
    public sealed class PlayerHub
    {
        public string Name { get; internal set; }
        public string Species { get; internal set; }

        /// <summary>Phase 6: the species ID once the character is created (null before).</summary>
        public string SpeciesId { get; internal set; }
        public int Age { get; internal set; }
        public string Background { get; internal set; }
        public string Description { get; internal set; }
        public PlayerAppearanceData Appearance { get; internal set; }
        public string Location { get; internal set; }
        public int Purse { get; internal set; }
        public int Fed { get; internal set; }
        public string FedText { get; internal set; }
        public int CarriedGrams { get; internal set; }
        /// <summary>Carrying capacity (Phase 11: the SRD rule); over it Speed is 5 ft.</summary>
        public int EncumbranceGrams { get; internal set; }

        /// <summary>The most the player may carry at all (twice the capacity).</summary>
        public int PackLimitGrams { get; internal set; }
        public float SpeedFactor { get; internal set; }
        public string SpeedReason { get; internal set; }
        public int Protection { get; internal set; }
        public int Damage { get; internal set; }
        public string Weapon { get; internal set; }
        public int InventoryStacks { get; internal set; }
        public int MaxInventoryStacks => PlayerCommandHandlers.MaxInventoryStacks;
        public int PossessionsValue { get; internal set; }
        public List<ItemView> Inventory { get; } = new List<ItemView>();
        public Dictionary<EquipmentSlot, ItemView> Equipment { get; } = new Dictionary<EquipmentSlot, ItemView>();
        public List<RegardView> KnownBy { get; } = new List<RegardView>();
        public List<TransactionView> RecentTrades { get; } = new List<TransactionView>();
    }

    /// <summary>
    /// How fast the player walks, and why. Phase 11 (ADR-0051) replaces Phase 5's separate 25 kg pace curve with the
    /// one rule the character sheet uses: carrying capacity (Strength × 15 lb, more with Powerful Build). Within it the
    /// load does not slow the player; over it their Speed is 5 ft (SRD 5.2.1); past twice it nothing more can be picked
    /// up (<see cref="ItemCommandHandlers.PackLimitGrams"/>). Hunger still slows the walk.
    /// </summary>
    public static class PlayerMovement
    {
        public const int HungryBelow = 25;

        public static float SpeedFactor(PlayerCharacterData p, out string reason)
        {
            var grams = PlayerCommandHandlers.CarriedGrams(p);
            var factor = 1f;
            var reasons = new List<string>();

            // A created character walks at their speed (30 ft is the norm): the species' pace, class features whose
            // conditions the worn equipment meets, and — over carrying capacity — 5 ft.
            var rules = p.Rpg != null && p.Rpg.State == Domain.Rpg.CreationState.Complete ? new Domain.Rpg.CharacterRules(p.Rpg, Domain.Rpg.CharacterRules.LoadoutOf(p)) : null;
            if (rules != null)
            {
                if (rules.Speed != 30)
                {
                    factor *= rules.Speed / 30f;
                    if (rules.Encumbered) reasons.Add($"over carrying capacity ({Carrying.Pounds(grams)} of {Carrying.Pounds(rules.CarryingCapacityGrams)} lb): speed 5 ft");
                    else
                    {
                        var speed = rules.SpeedBreakdown();
                        reasons.Add((speed.Parts.Count > 1 ? "speed" : rules.Species.Name.ToLowerInvariant() + " pace") + " (" + rules.Speed + " ft)");
                    }
                }
            }
            else if (grams > UnencumberedFor(p))
            {
                factor *= 5f / 30f;
                reasons.Add($"over carrying capacity ({Carrying.Pounds(grams)} lb)");
            }

            if (p.Fed < HungryBelow)
            {
                factor *= 0.85f;
                reasons.Add("hungry");
            }

            reason = reasons.Count == 0 ? "Unburdened" : (factor < 1f ? "Slowed: " : "Pace: ") + string.Join(", ", reasons);
            return factor;
        }

        /// <summary>Carrying capacity: how much the player carries before it slows them (SRD 5.2.1; Powerful Build doubles it).</summary>
        public static int UnencumberedFor(PlayerCharacterData p)
        {
            if (p?.Rpg != null && p.Rpg.State == Domain.Rpg.CreationState.Complete) return new Domain.Rpg.CharacterRules(p.Rpg, Domain.Rpg.CharacterRules.LoadoutOf(p)).CarryingCapacityGrams;
            return Carrying.CapacityGrams(10, 100);
        }

        public static string FedText(int fed) => fed >= 75 ? "Well fed" : fed >= 50 ? "Fed" : fed >= HungryBelow ? "Peckish" : fed > 0 ? "Hungry" : "Starving";
    }

    public sealed class TransactionView
    {
        internal TransactionView(TransactionData t, string shop, string keeper)
        {
            TransactionId = t.TransactionId;
            Date = t.Date;
            Clock = WorldTime.Clock(t.MinuteOfDay);
            Kind = t.Kind;
            ShopId = t.ShopId;
            Shop = shop;
            Keeper = keeper;
            ItemId = t.ItemId;
            ItemName = ItemCatalog.Find(t.ItemId)?.Name ?? t.ItemId;
            Quantity = t.Quantity;
            UnitPrice = t.UnitPrice;
            Total = t.Total;
        }

        public TransactionId TransactionId { get; }
        public WorldDate Date { get; }
        public string Clock { get; }
        public TransactionKind Kind { get; }
        public ShopId ShopId { get; }
        public string Shop { get; }
        public string Keeper { get; }
        public string ItemId { get; }
        public string ItemName { get; }
        public int Quantity { get; }
        public int UnitPrice { get; }
        public int Total { get; }
        public string Text => $"{Date} {Clock}: {(Kind == TransactionKind.Purchase ? "bought" : "sold")} {Quantity} × {ItemName} {(Kind == TransactionKind.Purchase ? "from" : "to")} {Shop} for {Total} coin";
    }

    public sealed class ShopSummary
    {
        public ShopId ShopId { get; internal set; }
        public string Name { get; internal set; }
        public SettlementId SettlementId { get; internal set; }
        public LocationId LocationId { get; internal set; }
        public string LocationName { get; internal set; }
        public string LayoutKey { get; internal set; }
        public NpcId KeeperId { get; internal set; }
        public string KeeperName { get; internal set; }
        public IReadOnlyList<ShopCategory> Categories { get; internal set; }
        public string Hours { get; internal set; }
        public bool IsOpen { get; internal set; }
        public string Status { get; internal set; }
        public int StockUnits { get; internal set; }
    }

    /// <summary>A price and the reasons for it.</summary>
    public sealed class PriceLine
    {
        public string ItemId { get; internal set; }
        public string Name { get; internal set; }
        public ItemStackId StackId { get; internal set; }
        public int Quantity { get; internal set; }
        public int BaseValue { get; internal set; }
        public int UnitPrice { get; internal set; }
        public string Refusal { get; internal set; }
        public bool Accepted => Refusal == null;
        public IReadOnlyList<string> Factors { get; internal set; }
        public string Explanation => Accepted ? "Base " + BaseValue + (Factors.Count == 0 ? string.Empty : ": " + string.Join(", ", Factors)) : Refusal;
        public ItemView Item { get; internal set; }
    }

    public sealed class ShopDetail
    {
        public ShopSummary Summary { get; internal set; }
        public List<PriceLine> ForSale { get; } = new List<PriceLine>();
        public List<PriceLine> Offers { get; } = new List<PriceLine>();

        /// <summary>Phase 11: particular items the shop holds (worn, fine, part-charged), with prices.</summary>
        public List<(InventoryRow Row, int Price)> Goods { get; } = new List<(InventoryRow, int)>();

        /// <summary>Phase 11: what mending each worn thing the player carries costs here (an armourer only).</summary>
        public List<(InventoryRow Row, int Price)> Repairs { get; } = new List<(InventoryRow, int)>();
        public int KeeperWealth { get; internal set; }
        public int PlayerPurse { get; internal set; }
        public int Familiarity { get; internal set; }
        public int Affinity { get; internal set; }
        public string KeeperActivity { get; internal set; }
        public bool PlayerIsHere { get; internal set; }
        public List<TransactionView> Ledger { get; } = new List<TransactionView>();
    }

    /// <summary>Where a person is this hour and what they are doing (for exploration and the inspector).</summary>
    public sealed class NpcPresence
    {
        public NpcId NpcId { get; internal set; }
        public string Name { get; internal set; }
        public NpcRole Role { get; internal set; }
        public LocationId LocationId { get; internal set; }
        public LocationKind LocationKind { get; internal set; }
        public string LocationName { get; internal set; }
        public string LayoutKey { get; internal set; }
        public ActivityKind Activity { get; internal set; }
        public string Reason { get; internal set; }
        public string Until { get; internal set; }
        public bool IsShopkeeper { get; internal set; }
    }

    public sealed partial class WorldSession
    {
        public WorldTimeSummary GetWorldTime() => HasWorld ? new WorldTimeSummary(_world.CurrentSimulationDate, _world.MinuteOfDay) : null;

        public PlayerHub GetPlayerHub()
        {
            var p = _world?.Player;
            if (p == null) return null;
            var hub = new PlayerHub
            {
                Name = p.Name,
                Species = p.Species,
                SpeciesId = p.Rpg?.State == Domain.Rpg.CreationState.Complete ? p.Rpg.SpeciesId : null,
                Age = p.Age,
                Background = p.Background,
                Description = p.Description,
                // Phase 20A: the whole record. The field-by-field copy here dropped Phase 20's height and build, so the body in
                // the world was always of average height and build while the creator's preview showed the chosen ones.
                Appearance = p.Appearance.Clone(),
                Location = p.IsInSettlement ? _world.FindSettlement(p.SettlementId)?.Name ?? "Unknown" : "On the road",
                Purse = p.Purse,
                Fed = p.Fed,
                FedText = PlayerMovement.FedText(p.Fed),
                CarriedGrams = PlayerCommandHandlers.CarriedGrams(p),
                EncumbranceGrams = PlayerMovement.UnencumberedFor(p),
                PackLimitGrams = ItemCommandHandlers.PackLimitGrams(p),
                InventoryStacks = p.Inventory.Count,
            };
            hub.SpeedFactor = PlayerMovement.SpeedFactor(p, out var why);
            hub.SpeedReason = why;
            var value = 0;
            foreach (var s in p.Inventory)
            {
                var d = ItemCatalog.Find(s.ItemId);
                if (d == null) continue;
                hub.Inventory.Add(new ItemView(d, s.StackId, s.Quantity, EquipmentSlot.None));
                value += d.BaseValue * s.Quantity;
            }

            foreach (var pair in p.Equipment)
            {
                var d = ItemCatalog.Find(pair.Value.ItemId);
                if (d == null) continue;
                hub.Equipment[pair.Key] = new ItemView(d, pair.Value.StackId, 1, pair.Key);
                value += d.BaseValue;
                hub.Protection += d.Protection;
                if (pair.Key == EquipmentSlot.MainHand)
                {
                    hub.Damage = d.Damage;
                    hub.Weapon = d.Name;
                }
            }

            hub.Inventory.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : string.CompareOrdinal(a.Name, b.Name));
            hub.PossessionsValue = value;
            if (hub.Weapon == null)
            {
                hub.Weapon = "Bare hands";
                hub.Damage = 1;
            }

            foreach (var n in _world.Npcs)
                if (!n.IsDeceased && (n.PlayerRegard.Familiarity > 0 || n.PlayerRegard.Trades > 0)) hub.KnownBy.Add(new RegardView(n));
            hub.KnownBy.Sort((a, b) => b.Familiarity.CompareTo(a.Familiarity));
            hub.RecentTrades.AddRange(GetTransactions(10));
            return hub;
        }

        public IReadOnlyList<ShopSummary> GetShops(SettlementId settlement = default)
        {
            var list = new List<ShopSummary>();
            if (!HasWorld) return list;
            foreach (var s in _world.Shops)
                if (settlement.IsEmpty || s.SettlementId == settlement) list.Add(Summarize(s));
            return list;
        }

        private ShopSummary Summarize(ShopData s)
        {
            var keeper = _world.FindNpc(s.ShopkeeperNpcId);
            var place = _world.FindLocation(s.LocationId);
            var open = CommerceMath.Availability(_world, s);
            var units = 0;
            foreach (var line in s.Stock) units += line.Quantity;
            return new ShopSummary
            {
                ShopId = s.ShopId,
                Name = s.Name,
                SettlementId = s.SettlementId,
                LocationId = s.LocationId,
                LocationName = place?.Name,
                LayoutKey = place?.LayoutKey,
                KeeperId = s.ShopkeeperNpcId,
                KeeperName = keeper?.Name ?? "nobody",
                Categories = s.Categories.ToArray(),
                Hours = WorldTime.Clock(s.OpensAt) + "–" + WorldTime.Clock(s.ClosesAt),
                IsOpen = open.IsOpen,
                Status = open.Reason,
                StockUnits = units,
            };
        }

        /// <summary>The shop the person keeps, if any.</summary>
        public ShopSummary GetShopKeptBy(NpcId npc)
        {
            if (!HasWorld) return null;
            foreach (var s in _world.Shops) if (s.ShopkeeperNpcId == npc) return Summarize(s);
            return null;
        }

        public ShopDetail GetShopDetail(ShopId id)
        {
            var shop = _world?.FindShop(id);
            if (shop == null) return null;
            var keeper = _world.FindNpc(shop.ShopkeeperNpcId);
            var p = _world.Player;
            var detail = new ShopDetail
            {
                Summary = Summarize(shop),
                KeeperWealth = keeper?.Condition.Wealth ?? 0,
                PlayerPurse = p?.Purse ?? 0,
                Familiarity = keeper?.PlayerRegard.Familiarity ?? 0,
                Affinity = keeper?.PlayerRegard.Affinity ?? 0,
                PlayerIsHere = p != null && p.IsInSettlement && p.SettlementId == shop.SettlementId,
            };
            if (keeper != null && !keeper.IsDeceased)
            {
                var now = NpcActivityPlan.Now(_world, keeper);
                detail.KeeperActivity = now.Activity + ": " + now.Reason;
            }

            foreach (var line in shop.Stock)
            {
                var d = ItemCatalog.Find(line.ItemId);
                if (d == null) continue;
                var q = CommerceMath.SellingPrice(_world, shop, d);
                detail.ForSale.Add(Line(d, q, line.Quantity, default));
            }

            if (p != null)
            {
                foreach (var s in p.Inventory)
                {
                    var d = ItemCatalog.Find(s.ItemId);
                    if (d == null) continue;
                    var q = PlayerCommandHandlers.OfferFor(_world, shop, s); // Phase 11: condition and quality count
                    var line = Line(d, q, s.Quantity, s.StackId);
                    if (d.Wears && s.Condition != ItemCondition.Good) line.Name += " (" + ItemConditions.Name(s.Condition) + ")";
                    if (s.Quality == ItemQuality.Fine) line.Name += " (fine)";
                    detail.Offers.Add(line);
                }
            }

            detail.Goods.AddRange(GetShopGoods(shop.ShopId)); // Phase 11
            detail.Repairs.AddRange(GetRepairQuotes(shop.ShopId));

            for (var i = _world.Transactions.Count - 1; i >= 0 && detail.Ledger.Count < 8; i--)
                if (_world.Transactions[i].ShopId == shop.ShopId) detail.Ledger.Add(View(_world.Transactions[i]));
            return detail;
        }

        private static PriceLine Line(ItemDefinition d, Quote q, int quantity, ItemStackId stack)
        {
            var factors = new List<string>();
            foreach (var f in q.Factors) factors.Add(f.ToString());
            return new PriceLine
            {
                ItemId = d.Id,
                Name = d.Name,
                StackId = stack,
                Quantity = quantity,
                BaseValue = d.BaseValue,
                UnitPrice = q.Accepted ? q.UnitPrice : 0,
                Refusal = q.Refusal,
                Factors = factors,
                Item = new ItemView(d, stack, quantity, EquipmentSlot.None),
            };
        }

        private TransactionView View(TransactionData t) =>
            new TransactionView(t, _world.FindShop(t.ShopId)?.Name ?? "a shop", _world.FindNpc(t.ShopkeeperNpcId)?.Name ?? "a merchant");

        /// <summary>The most recent trades, newest first.</summary>
        public IReadOnlyList<TransactionView> GetTransactions(int max = 50)
        {
            var list = new List<TransactionView>();
            if (!HasWorld) return list;
            for (var i = _world.Transactions.Count - 1; i >= 0 && list.Count < max; i--) list.Add(View(_world.Transactions[i]));
            return list;
        }

        public NpcPresence GetNpcPresence(NpcId id)
        {
            var n = _world?.FindNpc(id);
            return n == null || n.IsDeceased ? null : Presence(n, NpcActivityPlan.Now(_world, n));
        }

        /// <summary>Everyone whose place this hour is in the settlement (asleep at home included).</summary>
        public IReadOnlyList<NpcPresence> GetPresenceIn(SettlementId settlement)
        {
            var list = new List<NpcPresence>();
            if (!HasWorld) return list;
            foreach (var n in _world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned) continue;
                var now = NpcActivityPlan.Now(_world, n);
                if (!now.IsKnown) continue;
                var place = _world.FindLocation(now.Location);
                if (place == null || place.SettlementId != settlement) continue;
                list.Add(Presence(n, now));
            }

            return list;
        }

        private NpcPresence Presence(NpcData n, NpcWhereabouts now)
        {
            var place = now.IsKnown ? _world.FindLocation(now.Location) : null;
            var keeps = false;
            foreach (var s in _world.Shops) if (s.ShopkeeperNpcId == n.NpcId) keeps = true;
            return new NpcPresence
            {
                NpcId = n.NpcId,
                Name = n.Name,
                Role = n.Role,
                LocationId = now.Location,
                LocationKind = place?.Kind ?? LocationKind.Residence,
                LocationName = place?.Name,
                LayoutKey = place?.LayoutKey,
                Activity = now.Activity,
                Reason = now.Reason,
                Until = WorldTime.Clock(WorldTime.Normalize(now.Until)),
                IsShopkeeper = keeps,
            };
        }
    }
}
