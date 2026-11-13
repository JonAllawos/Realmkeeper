using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.World
{
    // Phase 21 (ADR-0106): what kind of shop a shop is, and where its stock came from. One catalog of data: what each kind
    // deals in, who keeps it, its hours and how big a place must be to have one. Shops stay the Phase 5 shops (one stock,
    // one till, a resident keeper); the kind only says what they trade.

    public enum ShopKind
    {
        Unspecified = 0,
        GeneralStore = 1,
        Outfitter = 2,
        Smithy = 3,
        TavernKitchen = 4,
        GranaryYard = 5,
        FarmStall = 6,
        Provisioner = 7,
        Leatherworker = 8,
        Tailor = 9,
        Bowyer = 10,
        Apothecary = 11,
        Carpenter = 12,
    }

    /// <summary>Where a unit of a shop's stock came from.</summary>
    public enum SupplyOrigin
    {
        /// <summary>The shop's opening stock (or stock it held before records were kept).</summary>
        Opening = 0,

        /// <summary>Made from the settlement's own stores (iron, timber, stone, food), paid for into the treasury.</summary>
        MadeLocally = 1,

        /// <summary>Bought in from elsewhere along an open trade road.</summary>
        Imported = 2,

        /// <summary>Made by the keeper at their workbench or forge from the shop's own materials (Phase 21 production).</summary>
        Produced = 3,

        /// <summary>Bought from the player.</summary>
        FromPlayer = 4,
    }

    /// <summary>One line of a shop's supply record: so many of an item came in on a day, from somewhere.</summary>
    public sealed class SupplyRecordData
    {
        public long Day { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public SupplyOrigin Origin { get; set; }

        /// <summary>From where or from what, in words ("iron from the town's stores", "via the road to Ashford").</summary>
        public string Detail { get; set; }

        public SupplyRecordData Clone() => (SupplyRecordData)MemberwiseClone();
    }

    public sealed class ShopKindDefinition
    {
        public ShopKind Kind { get; set; }

        /// <summary>The shop's name after the keeper's ("Harrow's Leatherworks").</summary>
        public string Name { get; set; }

        /// <summary>The trade in a word ("leatherworker").</summary>
        public string Trade { get; set; }

        public string Description { get; set; }

        /// <summary>The categories it deals in when it has no item list (the Phase 5 kinds).</summary>
        public ShopCategory[] Categories { get; set; } = Array.Empty<ShopCategory>();

        /// <summary>The items it deals in (buys and sells). When set, only these.</summary>
        public string[] Items { get; set; } = Array.Empty<string>();

        /// <summary>The roles a keeper is chosen from, in order of preference.</summary>
        public NpcRole[] Keepers { get; set; } = Array.Empty<NpcRole>();

        public int Opens { get; set; } = 8 * 60;
        public int Closes { get; set; } = 18 * 60;

        /// <summary>The smallest settlement that has one (Phase 21 kinds; the Phase 5 kinds keep their own plan).</summary>
        public SettlementType MinimumSize { get; set; } = SettlementType.Town;

        /// <summary>Phase 21 (ADR-0107): the work stations the shop keeps (the player may work at them while it is open).</summary>
        public StationKind[] Stations { get; set; } = Array.Empty<StationKind>();

        /// <summary>The keeper makes goods there (a workshop), rather than only selling.</summary>
        public bool Workshop { get; set; }

        public bool Deals(ItemDefinition item)
        {
            if (item == null) return false;
            if (Items.Length > 0) return Array.IndexOf(Items, item.Id) >= 0;
            foreach (var c in item.SoldBy) if (Array.IndexOf(Categories, c) >= 0) return true;
            return false;
        }
    }

    public static class ShopKinds
    {
        private static readonly ShopKindDefinition[] Catalog =
        {
            new ShopKindDefinition
            {
                Kind = ShopKind.GeneralStore, Name = "General Store", Trade = "shopkeeper", MinimumSize = SettlementType.Village,
                Description = "Everyday goods, simple clothes and food for the road.",
                Categories = new[] { ShopCategory.General, ShopCategory.Clothing }, Keepers = new[] { NpcRole.Merchant },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Outfitter, Name = "Outfitters", Trade = "outfitter", MinimumSize = SettlementType.Town,
                Description = "Gear for travellers: packs, tools, lanterns, clothes and provisions.",
                Categories = new[] { ShopCategory.General, ShopCategory.Clothing }, Keepers = new[] { NpcRole.Merchant },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Smithy, Name = "Smithy", Trade = "smith", MinimumSize = SettlementType.Town, Workshop = true, Stations = new[] { StationKind.Forge }, Opens = 8 * 60, Closes = 17 * 60,
                Description = "Iron worked at the forge: blades, axes, mail, tools and bar iron.",
                Keepers = new[] { NpcRole.Smith },
                Items = new[]
                {
                    "dagger_iron", "sword_arming", "axe_hand", "mace_flanged", "spear_ash", "shield_buckler", "shield_round", "cap_iron", "greataxe", "rapier", "shortsword", "scimitar",
                    "chain_shirt", "chain_mail", "light_hammer", "sickle", "battleaxe", "flail", "morningstar", "warhammer", "greatsword", "maul", "glaive", "halberd", "pike",
                    "scale_mail", "ring_mail", "breastplate", "splint_armour", "iron_bar", "timber_plank",
                },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.TavernKitchen, Name = "Tavern Kitchen", Trade = "innkeeper", MinimumSize = SettlementType.Village, Stations = new[] { StationKind.Hearth }, Opens = 10 * 60, Closes = 22 * 60,
                Description = "Hot food and drink.",
                Categories = new[] { ShopCategory.Provisions }, Keepers = new[] { NpcRole.Innkeeper },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.GranaryYard, Name = "Granary Yard", Trade = "yard keeper", MinimumSize = SettlementType.Town, Opens = 8 * 60, Closes = 17 * 60,
                Description = "Grain, timber, stone and iron by the load.",
                Categories = new[] { ShopCategory.Materials }, Keepers = new[] { NpcRole.Labourer },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.FarmStall, Name = "Farm Stall", Trade = "farmer", MinimumSize = SettlementType.Hamlet, Opens = 7 * 60, Closes = 16 * 60,
                Description = "What the farm grows and makes.",
                Categories = new[] { ShopCategory.Provisions }, Keepers = new[] { NpcRole.Farmer },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Provisioner, Name = "Bakery & Provisions", Trade = "baker", MinimumSize = SettlementType.Town, Stations = new[] { StationKind.Oven }, Opens = 6 * 60 + 30, Closes = 15 * 60, Workshop = true,
                Description = "Bread from the oven, cheese, smoked fish, flour and food for the road.",
                Keepers = new[] { NpcRole.Resident, NpcRole.Farmer, NpcRole.Labourer },
                Items = new[] { "bread_loaf", "cheese_wheel", "travel_rations", "smoked_fish", "grain_sack", "ale_jug", "fishing_tackle", "cooks_utensils" },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Leatherworker, Name = "Leatherworks", Trade = "leatherworker", MinimumSize = SettlementType.Village, Stations = new[] { StationKind.LeatherBench }, Workshop = true,
                Description = "Hides cured and cut into jerkins, boots, bracers, packs and slings. Buys raw hides and pelts.",
                Keepers = new[] { NpcRole.Resident, NpcRole.Labourer, NpcRole.Farmer },
                Items = new[]
                {
                    "jerkin_leather", "jerkin_forester", "bracers_leather", "leggings_leather", "pauldrons_leather", "hood_leather", "boots_riding", "hide_armour", "backpack",
                    "sling", "cured_leather", "raw_hide", "wolf_pelt", "leatherworkers_tools",
                },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Tailor, Name = "Tailor", Trade = "tailor", MinimumSize = SettlementType.Town, Stations = new[] { StationKind.SewingTable }, Workshop = true,
                Description = "Linen and wool cut and sewn: tunics, trousers, cloaks, hats and padded coats.",
                Keepers = new[] { NpcRole.Resident, NpcRole.Merchant },
                Items = new[] { "tunic_linen", "tunic_work", "sleeves_linen", "trousers_wool", "hat_felt", "travellers_cloak", "shoes_simple", "padded_armour", "linen_cloth", "merchants_cloth", "weavers_tools" },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Bowyer, Name = "Bowyer & Fletcher", Trade = "bowyer", MinimumSize = SettlementType.Town, Stations = new[] { StationKind.Workbench }, Workshop = true,
                Description = "Bows and crossbows, arrows and bolts, javelins and staves.",
                Keepers = new[] { NpcRole.Resident, NpcRole.Labourer },
                Items = new[] { "bow_hunting", "longbow", "light_crossbow", "heavy_crossbow", "arrows", "crossbow_bolts", "sling_bullets", "javelin", "staff_quarter", "timber_plank" },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Apothecary, Name = "Apothecary", Trade = "apothecary", MinimumSize = SettlementType.Town, Stations = new[] { StationKind.Still }, Workshop = true,
                Description = "Herbs, remedies and healing draughts, vials and herbalist's kits. Buys silverleaf.",
                Keepers = new[] { NpcRole.Resident, NpcRole.Elder },
                Items = new[] { "potion_healing", "silverleaf", "glass_vial", "herbalism_kit", "alchemists_supplies" },
            },
            new ShopKindDefinition
            {
                Kind = ShopKind.Carpenter, Name = "Carpentry", Trade = "carpenter", MinimumSize = SettlementType.Town, Stations = new[] { StationKind.Workbench }, Workshop = true,
                Description = "Planks, staves, clubs, round shields, lutes and carpenter's tools.",
                Keepers = new[] { NpcRole.Labourer, NpcRole.Resident },
                Items = new[] { "timber_plank", "staff_quarter", "club", "shield_round", "lute", "druidic_focus", "carpenters_tools" },
            },
        };

        public static IReadOnlyList<ShopKindDefinition> All => Catalog;

        public static ShopKindDefinition Find(ShopKind kind)
        {
            foreach (var d in Catalog) if (d.Kind == kind) return d;
            return null;
        }

        /// <summary>The kinds a settlement of this size gains in its unused shop buildings (Phase 21), in order.</summary>
        public static IEnumerable<ShopKind> AddedIn(SettlementType type)
        {
            if (type >= SettlementType.Town)
            {
                yield return ShopKind.Leatherworker;
                yield return ShopKind.Provisioner;
                yield return ShopKind.Tailor;
                yield return ShopKind.Apothecary;
                yield return ShopKind.Bowyer;
                yield return ShopKind.Carpenter;
            }
            else if (type == SettlementType.Village)
            {
                yield return ShopKind.Leatherworker;
            }
        }

        /// <summary>The kind of a Phase 5 shop, read from what it was made as (its name and categories).</summary>
        public static ShopKind Infer(ShopData shop)
        {
            var name = shop.Name ?? string.Empty;
            if (name.EndsWith("Smithy", StringComparison.Ordinal)) return ShopKind.Smithy;
            if (name.EndsWith("Tavern Kitchen", StringComparison.Ordinal)) return ShopKind.TavernKitchen;
            if (name.EndsWith("Granary Yard", StringComparison.Ordinal)) return ShopKind.GranaryYard;
            if (name.EndsWith("Farm Stall", StringComparison.Ordinal)) return ShopKind.FarmStall;
            if (name.EndsWith("General Store", StringComparison.Ordinal)) return ShopKind.GeneralStore;
            if (name.EndsWith("Outfitters", StringComparison.Ordinal)) return ShopKind.Outfitter;
            if (shop.Categories.Contains(ShopCategory.WeaponsAndArmour)) return ShopKind.Smithy;
            if (shop.Categories.Contains(ShopCategory.Materials)) return ShopKind.GranaryYard;
            if (shop.Categories.Contains(ShopCategory.Provisions)) return ShopKind.TavernKitchen;
            return ShopKind.GeneralStore;
        }
    }
}
