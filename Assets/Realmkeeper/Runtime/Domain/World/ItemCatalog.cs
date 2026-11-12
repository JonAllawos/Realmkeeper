using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// The item definitions (data). A small, coherent starting catalog that can grow: every
    /// equippable item maps to a visual the presentation can render. Clothing maps to parts of the
    /// CC0 outfits (Peasant and Ranger sets, with variant textures). Weapons, shields and hard
    /// headwear map to procedural models. Materials link each item to the settlement economy.
    /// Item IDs are stable and saved; never rename one.
    /// </summary>
    public static partial class ItemCatalog
    {
        private static readonly Dictionary<string, ItemDefinition> ById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private static readonly List<ItemDefinition> All = new List<ItemDefinition>();

        public static IReadOnlyList<ItemDefinition> Items => All;

        public static ItemDefinition Find(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;

        public static bool Exists(string id) => Find(id) != null;

        private static ResourceStack[] M(params (ResourceTypeId r, int n)[] parts)
        {
            var list = new ResourceStack[parts.Length];
            for (var i = 0; i < parts.Length; i++) list[i] = new ResourceStack(parts[i].r, parts[i].n);
            return list;
        }

        private static void Add(ItemDefinition d)
        {
            ById.Add(d.Id, d);
            All.Add(d);
        }

        static ItemCatalog()
        {
            var iron = ResourceTypes.Iron;
            var timber = ResourceTypes.Timber;
            var food = ResourceTypes.Food;
            var stone = ResourceTypes.Stone;
            var arms = new[] { ShopCategory.WeaponsAndArmour };
            var cloth = new[] { ShopCategory.Clothing, ShopCategory.General };
            var outfitter = new[] { ShopCategory.Clothing, ShopCategory.WeaponsAndArmour };
            var provisions = new[] { ShopCategory.Provisions, ShopCategory.General };
            var materials = new[] { ShopCategory.Materials };

            // ----- Weapons (main hand) -----
            Add(new ItemDefinition { Id = "dagger_iron", Name = "Iron dagger", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Dagger,
                BaseValue = 12, WeightGrams = 450, Damage = 3, Visual = "prop:dagger", Materials = M((iron, 1)), SoldBy = arms,
                Description = "A plain iron blade, a hand long. Every traveller carries one." });
            Add(new ItemDefinition { Id = "sword_arming", Name = "Arming sword", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Sword,
                BaseValue = 45, WeightGrams = 1300, Damage = 6, Visual = "prop:sword", Materials = M((iron, 3), (timber, 1)), SoldBy = arms,
                Description = "A one-handed sword with a cross guard, the town guard's pattern." });
            Add(new ItemDefinition { Id = "axe_hand", Name = "Hand axe", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Axe,
                BaseValue = 20, WeightGrams = 1100, Damage = 5, Visual = "prop:axe", Materials = M((iron, 2), (timber, 1)), SoldBy = arms,
                Description = "A woodsman's axe that serves as well against wolves as against kindling." });
            Add(new ItemDefinition { Id = "mace_flanged", Name = "Flanged mace", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Mace,
                BaseValue = 32, WeightGrams = 1700, Damage = 6, Visual = "prop:mace", Materials = M((iron, 3), (timber, 1)), SoldBy = arms,
                Description = "An iron head with six flanges on an ash haft." });
            Add(new ItemDefinition { Id = "spear_ash", Name = "Ash spear", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Spear, TwoHanded = true,
                BaseValue = 18, WeightGrams = 1900, Damage = 5, Visual = "prop:spear", Materials = M((iron, 1), (timber, 2)), SoldBy = arms,
                Description = "A long ash shaft with a leaf-shaped iron head. Needs both hands." });
            Add(new ItemDefinition { Id = "staff_quarter", Name = "Quarterstaff", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Staff, TwoHanded = true,
                BaseValue = 6, WeightGrams = 1600, Damage = 3, Visual = "prop:staff", Materials = M((timber, 2)), SoldBy = new[] { ShopCategory.WeaponsAndArmour, ShopCategory.General },
                Description = "A walking staff of seasoned oak. Needs both hands." });
            Add(new ItemDefinition { Id = "bow_hunting", Name = "Hunting bow", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Bow, TwoHanded = true,
                BaseValue = 28, WeightGrams = 900, Damage = 4, Visual = "prop:bow", Materials = M((timber, 2)), SoldBy = arms,
                Description = "A yew self-bow for game. Needs both hands." });

            // ----- Shields (off hand) -----
            Add(new ItemDefinition { Id = "shield_round", Name = "Round shield", Category = ItemCategory.Shield, Slot = EquipmentSlot.OffHand,
                BaseValue = 22, WeightGrams = 3500, Protection = 3, Visual = "prop:shield_round", Materials = M((timber, 3), (iron, 1)), SoldBy = arms,
                Description = "Planks under painted hide, with an iron boss and rim." });
            Add(new ItemDefinition { Id = "shield_buckler", Name = "Buckler", Category = ItemCategory.Shield, Slot = EquipmentSlot.OffHand,
                BaseValue = 15, WeightGrams = 1400, Protection = 1, Visual = "prop:buckler", Materials = M((iron, 2)), SoldBy = arms,
                Description = "A small iron fist-shield." });

            // ----- Headwear -----
            Add(new ItemDefinition { Id = "hat_felt", Name = "Felt hat", Category = ItemCategory.Headwear, Slot = EquipmentSlot.Head,
                BaseValue = 6, WeightGrams = 150, Visual = "prop:felt_hat", SoldBy = cloth,
                Description = "A broad-brimmed hat against sun and rain." });
            Add(new ItemDefinition { Id = "hood_leather", Name = "Leather hood", Category = ItemCategory.Headwear, Slot = EquipmentSlot.Head, Quality = ItemQuality.Fine,
                BaseValue = 14, WeightGrams = 350, Protection = 1, Visual = "outfit:Ranger:Hood:Ranger", SoldBy = outfitter,
                Description = "A ranger's hood of oiled leather." });
            Add(new ItemDefinition { Id = "cap_iron", Name = "Iron cap", Category = ItemCategory.Headwear, Slot = EquipmentSlot.Head,
                BaseValue = 26, WeightGrams = 1300, Protection = 2, Visual = "prop:iron_cap", Materials = M((iron, 2)), SoldBy = arms,
                Description = "A riveted iron skullcap." });

            // ----- Body (chest) -----
            Add(new ItemDefinition { Id = "tunic_linen", Name = "Linen tunic", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Chest,
                BaseValue = 8, WeightGrams = 500, Visual = "outfit:Peasant:Body:Peasant", SoldBy = cloth,
                Description = "A belted linen tunic over a shirt." });
            Add(new ItemDefinition { Id = "tunic_work", Name = "Work tunic", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Chest,
                BaseValue = 6, WeightGrams = 600, Visual = "outfit:Peasant:Body:Peasant2", SoldBy = cloth,
                Description = "A hard-wearing tunic in drab wool." });
            Add(new ItemDefinition { Id = "jerkin_leather", Name = "Leather jerkin", Category = ItemCategory.Armour, Slot = EquipmentSlot.Chest,
                BaseValue = 34, WeightGrams = 2800, Protection = 3, Visual = "outfit:Ranger:Body:Ranger", SoldBy = outfitter,
                Description = "Boiled leather over a padded coat: light armour for the road." });
            Add(new ItemDefinition { Id = "jerkin_forester", Name = "Forester's jerkin", Category = ItemCategory.Armour, Slot = EquipmentSlot.Chest, Quality = ItemQuality.Fine,
                BaseValue = 40, WeightGrams = 2700, Protection = 3, Visual = "outfit:Ranger:Body:Ranger3", SoldBy = outfitter,
                Description = "A green-dyed leather jerkin, cut for the woods." });

            // ----- Hands, legs, feet -----
            Add(new ItemDefinition { Id = "sleeves_linen", Name = "Linen sleeves", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Hands,
                BaseValue = 3, WeightGrams = 150, Visual = "outfit:Peasant:Arms:Peasant", SoldBy = cloth,
                Description = "Loose linen sleeves with cuffs." });
            Add(new ItemDefinition { Id = "bracers_leather", Name = "Leather bracers", Category = ItemCategory.Armour, Slot = EquipmentSlot.Hands,
                BaseValue = 12, WeightGrams = 400, Protection = 1, Visual = "outfit:Ranger:Arms:Ranger", SoldBy = outfitter,
                Description = "Laced bracers and gloves." });
            Add(new ItemDefinition { Id = "trousers_wool", Name = "Wool trousers", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Legs,
                BaseValue = 6, WeightGrams = 500, Visual = "outfit:Peasant:Legs:Peasant", SoldBy = cloth,
                Description = "Warm, loose trousers." });
            Add(new ItemDefinition { Id = "leggings_leather", Name = "Leather leggings", Category = ItemCategory.Armour, Slot = EquipmentSlot.Legs,
                BaseValue = 18, WeightGrams = 1200, Protection = 1, Visual = "outfit:Ranger:Legs:Ranger", SoldBy = outfitter,
                Description = "Close-fitting leather riding leggings." });
            Add(new ItemDefinition { Id = "shoes_simple", Name = "Simple shoes", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Feet,
                BaseValue = 5, WeightGrams = 400, Visual = "outfit:Peasant:Feet:Peasant", SoldBy = cloth,
                Description = "Turnshoes wrapped at the ankle." });
            Add(new ItemDefinition { Id = "boots_riding", Name = "Riding boots", Category = ItemCategory.Clothing, Slot = EquipmentSlot.Feet,
                BaseValue = 16, WeightGrams = 900, Visual = "outfit:Ranger:Feet:Ranger", SoldBy = outfitter,
                Description = "Tall leather boots." });
            Add(new ItemDefinition { Id = "pauldrons_leather", Name = "Leather pauldrons", Category = ItemCategory.Armour, Slot = EquipmentSlot.Back,
                BaseValue = 20, WeightGrams = 1000, Protection = 1, Visual = "outfit:Ranger:Pauldrons:Ranger", SoldBy = arms,
                Description = "Shoulder guards strapped over the coat." });

            // ----- Food -----
            Add(new ItemDefinition { Id = "bread_loaf", Name = "Loaf of bread", Category = ItemCategory.Food, Stackable = true, MaxStack = 20,
                BaseValue = 2, WeightGrams = 400, Nourishment = 30, Materials = M((food, 1)), SoldBy = provisions,
                Description = "A round of brown bread." });
            Add(new ItemDefinition { Id = "cheese_wheel", Name = "Small cheese", Category = ItemCategory.Food, Stackable = true, MaxStack = 10,
                BaseValue = 5, WeightGrams = 800, Nourishment = 45, Materials = M((food, 2)), SoldBy = provisions,
                Description = "A hard farmhouse cheese." });
            Add(new ItemDefinition { Id = "travel_rations", Name = "Travel rations", Category = ItemCategory.Food, Stackable = true, MaxStack = 10,
                BaseValue = 6, WeightGrams = 700, Nourishment = 60, Materials = M((food, 3)), SoldBy = provisions,
                Description = "Hard bread, dried meat and apples, wrapped for the road." });

            // ----- Trade goods (the materials themselves, item-sized) -----
            Add(new ItemDefinition { Id = "iron_bar", Name = "Iron bar", Category = ItemCategory.TradeGood, Stackable = true, MaxStack = 20,
                BaseValue = 4, WeightGrams = 2000, Materials = M((iron, 1)), SoldBy = materials,
                Description = "Smelted iron, ready for the forge. One unit of the settlement's iron." });
            Add(new ItemDefinition { Id = "timber_plank", Name = "Timber plank", Category = ItemCategory.TradeGood, Stackable = true, MaxStack = 20,
                BaseValue = 2, WeightGrams = 3000, Materials = M((timber, 1)), SoldBy = materials,
                Description = "Sawn and seasoned. One unit of the settlement's timber." });
            Add(new ItemDefinition { Id = "dressed_stone", Name = "Dressed stone", Category = ItemCategory.TradeGood, Stackable = true, MaxStack = 10,
                BaseValue = 2, WeightGrams = 6000, Materials = M((stone, 1)), SoldBy = materials,
                Description = "A squared building block. One unit of the settlement's stone." });
            Add(new ItemDefinition { Id = "grain_sack", Name = "Sack of grain", Category = ItemCategory.TradeGood, Stackable = true, MaxStack = 10,
                BaseValue = 3, WeightGrams = 5000, Materials = M((food, 2)), SoldBy = materials,
                Description = "Threshed grain. Two units of the settlement's food." });

            // ----- Phase 6: arms, armour, tools and focuses for class and background starting kits -----
            var general = new[] { ShopCategory.General };
            Add(new ItemDefinition { Id = "greataxe", Name = "Greataxe", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Axe, TwoHanded = true,
                BaseValue = 30, WeightGrams = 3200, Damage = 7, Visual = "prop:greataxe", Materials = M((iron, 3), (timber, 1)), SoldBy = arms,
                Description = "A broad, heavy axe swung with both hands." });
            Add(new ItemDefinition { Id = "rapier", Name = "Rapier", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Sword,
                BaseValue = 32, WeightGrams = 1000, Damage = 5, Visual = "prop:rapier", Materials = M((iron, 2)), SoldBy = arms, Quality = ItemQuality.Fine,
                Description = "A slender thrusting blade with a swept guard." });
            Add(new ItemDefinition { Id = "shortsword", Name = "Shortsword", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Sword,
                BaseValue = 18, WeightGrams = 900, Damage = 4, Visual = "prop:shortsword", Materials = M((iron, 2)), SoldBy = arms,
                Description = "A short, light blade for close work." });
            Add(new ItemDefinition { Id = "scimitar", Name = "Scimitar", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Sword,
                BaseValue = 24, WeightGrams = 1300, Damage = 4, Visual = "prop:scimitar", Materials = M((iron, 2)), SoldBy = arms,
                Description = "A curved slashing blade." });
            Add(new ItemDefinition { Id = "light_crossbow", Name = "Light crossbow", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Crossbow, TwoHanded = true,
                BaseValue = 28, WeightGrams = 2300, Damage = 5, Visual = "prop:crossbow", Materials = M((timber, 2), (iron, 1)), SoldBy = arms,
                Description = "A stocked bow with a trigger; slow to load, easy to aim." });
            Add(new ItemDefinition { Id = "javelin", Name = "Javelin", Category = ItemCategory.Weapon, Slot = EquipmentSlot.MainHand, WeaponClass = WeaponClass.Spear, Stackable = true, MaxStack = 8,
                BaseValue = 3, WeightGrams = 900, Damage = 3, Visual = "prop:javelin", Materials = M((timber, 1)), SoldBy = arms,
                Description = "A light throwing spear." });
            Add(new ItemDefinition { Id = "chain_shirt", Name = "Chain shirt", Category = ItemCategory.Armour, Slot = EquipmentSlot.Chest,
                BaseValue = 50, WeightGrams = 9000, Protection = 4, Visual = "outfit:Ranger:Body:Ranger3", Materials = M((iron, 5)), SoldBy = arms,
                Description = "Interlocking rings worn under a surcoat. (Shown with the forester's jerkin model until armour art exists.)" });
            Add(new ItemDefinition { Id = "holy_symbol", Name = "Holy symbol", Category = ItemCategory.Focus, BaseValue = 5, WeightGrams = 100, SoldBy = general,
                Visual = "prop:holy_symbol", Materials = M((iron, 1)), Description = "A worn emblem of faith; a focus for divine magic." });
            Add(new ItemDefinition { Id = "arcane_focus", Name = "Arcane focus", Category = ItemCategory.Focus, BaseValue = 10, WeightGrams = 500, SoldBy = general,
                Visual = "prop:orb", Description = "A polished crystal on a cord; a focus for arcane magic." });
            Add(new ItemDefinition { Id = "druidic_focus", Name = "Druidic focus", Category = ItemCategory.Focus, BaseValue = 4, WeightGrams = 400, SoldBy = general,
                Visual = "prop:totem", Materials = M((timber, 1)), Description = "A carved sprig of yew bound with feathers; a focus for nature magic." });
            Add(new ItemDefinition { Id = "spellbook", Name = "Spellbook", Category = ItemCategory.Focus, BaseValue = 25, WeightGrams = 1500, SoldBy = general,
                Visual = "prop:book", Description = "A leather-bound book of arcane notation, mostly blank." });
            Add(new ItemDefinition { Id = "thieves_tools", Name = "Thieves' tools", Category = ItemCategory.Tool, BaseValue = 12, WeightGrams = 500, SoldBy = general,
                Visual = "prop:kit", Materials = M((iron, 1)), Description = "Picks, a file and a small mirror in a roll of leather." });
            Add(new ItemDefinition { Id = "smiths_tools", Name = "Smith's tools", Category = ItemCategory.Tool, BaseValue = 10, WeightGrams = 4000, SoldBy = general,
                Visual = "prop:kit", Materials = M((iron, 2)), Description = "Hammers, tongs and a small anvil." });
            Add(new ItemDefinition { Id = "carpenters_tools", Name = "Carpenter's tools", Category = ItemCategory.Tool, BaseValue = 8, WeightGrams = 3000, SoldBy = general,
                Visual = "prop:kit", Materials = M((iron, 1), (timber, 1)), Description = "Saw, chisels, mallet and plane." });
            Add(new ItemDefinition { Id = "herbalism_kit", Name = "Herbalism kit", Category = ItemCategory.Tool, BaseValue = 5, WeightGrams = 1500, SoldBy = general,
                Visual = "prop:kit", Description = "Pouches, shears and a mortar for gathering and preparing herbs." });
            Add(new ItemDefinition { Id = "navigators_tools", Name = "Navigator's tools", Category = ItemCategory.Tool, BaseValue = 15, WeightGrams = 1000, SoldBy = general,
                Visual = "prop:kit", Materials = M((iron, 1)), Description = "Sounding line, compass and charts." });
            Add(new ItemDefinition { Id = "calligraphers_supplies", Name = "Calligrapher's supplies", Category = ItemCategory.Tool, BaseValue = 6, WeightGrams = 500, SoldBy = general,
                Visual = "prop:kit", Description = "Quills, inks and good vellum." });
            Add(new ItemDefinition { Id = "lute", Name = "Lute", Category = ItemCategory.Tool, BaseValue = 20, WeightGrams = 1500, SoldBy = general,
                Visual = "prop:lute", Materials = M((timber, 1)), Description = "A round-backed stringed instrument." });
            Add(new ItemDefinition { Id = "dice_set", Name = "Dice set", Category = ItemCategory.Tool, BaseValue = 1, WeightGrams = 50, SoldBy = general,
                Visual = "prop:dice", Description = "Bone dice in a little bag." });

            // ----- Phase 8: heavy armour (data only: not stocked by shops yet, so shop generation is unchanged) -----
            Add(new ItemDefinition { Id = "chain_mail", Name = "Chain mail", Category = ItemCategory.Armour, Slot = EquipmentSlot.Chest,
                BaseValue = 75, WeightGrams = 25000, Protection = 6, Visual = "outfit:Ranger:Body:Ranger3", Materials = M((iron, 8)),
                Description = "A full coat of riveted rings over padding. Heavy and loud. (Shown with the forester's jerkin model until armour art exists.)" });

            // ----- Phase 8: combat data (SRD 5.2.1) on the existing items, by ID -----
            foreach (var d in All)
            {
                if (ItemCombat.Weapons.TryGetValue(d.Id, out var w)) { d.Weapon = w; d.RulesSource = ItemCombat.Srd; }
                if (ItemCombat.Armor.TryGetValue(d.Id, out var a)) { d.Armor = a; d.RulesSource = ItemCombat.Srd; }
            }

            AddPhase11(); // Phase 11: the rest of the catalog
            AddPhase13(); // Phase 13: exploration items
            AddPhase14(); // Phase 14: keys and relics of the new places
            AddPhase18(); // Phase 18: the new places' evidence and distinctive tools
            CompletePhase11();
        }
    }
}
