using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 11 (ADR-0050): the item catalog's second part — the rest of the SRD 5.2.1 weapons and armour tables,
    /// ammunition, tools and gathering gear, crafting materials, food and potions, jewellery, books and keys, and the
    /// first magic items. Numbers follow the SRD 5.2.1 (Wizards of the Coast LLC, CC BY 4.0); every name not taken
    /// from the SRD, every description and every lore line is Realmkeeper's own. Prices are in the game's coin
    /// (about three to a gold piece). IDs are stable and saved; never rename one.
    /// </summary>
    public static partial class ItemCatalog
    {
        /// <summary>The catalog's version, saved with the world (ADR-0050). Phase 11 is version 2.</summary>
        public const int Version = 2;

        /// <summary>
        /// Item IDs renamed or retired by each catalog version, old → new (ADR-0050). Version 2 keeps every version 1 ID,
        /// so it is empty; a later version that renames an item adds the pair here and old saves follow it on load.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Brings a loaded world's items up to this catalog: renamed IDs are followed in every holder (pack, slots,
        /// containers, shop stock and goods, work) and the world records the current version. Returns the stacks changed.
        /// </summary>
        public static int Upgrade(WorldData world)
        {
            if (world.ItemCatalogVersion >= Version) return 0;
            var changed = 0;
            void Follow(ItemStackData s)
            {
                if (s != null && s.ItemId != null && Renamed.TryGetValue(s.ItemId, out var to)) { s.ItemId = to; changed++; }
            }

            if (world.Player != null)
            {
                foreach (var s in world.Player.Inventory) Follow(s);
                foreach (var s in world.Player.Equipment.Values) Follow(s);
            }

            foreach (var c in world.Containers) foreach (var s in c.Items) Follow(s);
            foreach (var j in world.WorkJobs) foreach (var s in j.Held) Follow(s);
            foreach (var shop in world.Shops)
            {
                foreach (var s in shop.Goods) Follow(s);
                foreach (var line in shop.Stock)
                    if (Renamed.TryGetValue(line.ItemId, out var to)) { line.ItemId = to; changed++; }
            }

            world.ItemCatalogVersion = Version;
            return changed;
        }

        private static WeaponProfile Wp(string kind, bool simple, int count, int sides, DamageType type, WeaponProperty p, string mastery,
            int range = 0, int longRange = 0, int versatileSides = 0, bool ranged = false) => new WeaponProfile
        {
            Kind = kind, Simple = simple, Ranged = ranged, Damage = new Dice(count, sides), DamageType = type, Properties = p, Mastery = mastery,
            Range = range, LongRange = longRange, Versatile = versatileSides > 0 ? new Dice(1, versatileSides) : default,
        };

        private static void AddPhase11()
        {
            var iron = ResourceTypes.Iron;
            var timber = ResourceTypes.Timber;
            var food = ResourceTypes.Food;
            var arms = new[] { ShopCategory.WeaponsAndArmour };
            var general = new[] { ShopCategory.General };
            var provisions = new[] { ShopCategory.Provisions, ShopCategory.General };
            var materials = new[] { ShopCategory.Materials };
            var srd = ContentSource.Srd521;

            ItemDefinition Weapon(string id, string name, WeaponClass cls, WeaponProfile w, int value, int grams, string like, string desc, ResourceStack[] mats, bool sold = true)
            {
                var twoHanded = w.Has(WeaponProperty.TwoHanded);
                return new ItemDefinition
                {
                    Id = id, Name = name, Category = ItemCategory.Weapon, Subcategory = w.Kind, Slot = EquipmentSlot.MainHand, WeaponClass = cls, TwoHanded = twoHanded,
                    BaseValue = value, WeightGrams = grams, Damage = w.Damage.Count * w.Damage.Sides, Visual = "like:" + like, Materials = mats, SoldBy = sold ? arms : Array.Empty<ShopCategory>(),
                    Description = desc, Weapon = w, RulesSource = ItemCombat.Srd, Source = srd, Wears = true, Tags = new[] { "metal", "weapon" },
                };
            }

            // ----- Weapons: the rest of the SRD 5.2.1 table used by the game's fighting styles -----
            Add(Weapon("club", "Club", WeaponClass.Mace, Wp("club", true, 1, 4, DamageType.Bludgeoning, WeaponProperty.Light, "Slow"), 2, 900, "staff_quarter",
                "A knotted length of hardwood, weighted at one end.", M((timber, 1))));
            Add(Weapon("light_hammer", "Light hammer", WeaponClass.Mace, Wp("light hammer", true, 1, 4, DamageType.Bludgeoning, WeaponProperty.Light | WeaponProperty.Thrown, "Nick", 20, 60), 6, 900, "mace_flanged",
                "A short-hafted hammer balanced for throwing.", M((iron, 1), (timber, 1))));
            Add(Weapon("sickle", "Sickle", WeaponClass.Dagger, Wp("sickle", true, 1, 4, DamageType.Slashing, WeaponProperty.Light, "Nick"), 3, 900, "dagger_iron",
                "A harvest blade on a short handle; farmhands know which end bites.", M((iron, 1))));
            Add(Weapon("sling", "Sling", WeaponClass.Bow, Wp("sling", true, 1, 4, DamageType.Bludgeoning, WeaponProperty.Ammunition, "Slow", 30, 120, ranged: true), 1, 100, "bow_hunting",
                "A leather cradle on two cords, for stones or lead bullets.", Array.Empty<ResourceStack>()));
            Add(Weapon("battleaxe", "Battleaxe", WeaponClass.Axe, Wp("battleaxe", false, 1, 8, DamageType.Slashing, WeaponProperty.Versatile, "Topple", versatileSides: 10), 30, 1800, "axe_hand",
                "A bearded war axe; one hand for the shield wall, two for the breach.", M((iron, 2), (timber, 1))));
            Add(Weapon("flail", "Flail", WeaponClass.Mace, Wp("flail", false, 1, 8, DamageType.Bludgeoning, WeaponProperty.None, "Sap"), 30, 900, "mace_flanged",
                "A spiked ball on a short chain, hard to parry.", M((iron, 2), (timber, 1))));
            Add(Weapon("morningstar", "Morningstar", WeaponClass.Mace, Wp("morningstar", false, 1, 8, DamageType.Piercing, WeaponProperty.None, "Sap"), 45, 1800, "mace_flanged",
                "A spiked iron head on a stout haft.", M((iron, 3), (timber, 1))));
            Add(Weapon("warhammer", "Warhammer", WeaponClass.Mace, Wp("warhammer", false, 1, 8, DamageType.Bludgeoning, WeaponProperty.Versatile, "Push", versatileSides: 10), 45, 2300, "mace_flanged",
                "A square hammerhead with a back spike, made to dent plate.", M((iron, 3), (timber, 1))));
            Add(Weapon("greatsword", "Greatsword", WeaponClass.Sword, Wp("greatsword", false, 2, 6, DamageType.Slashing, WeaponProperty.Heavy | WeaponProperty.TwoHanded, "Graze"), 150, 2700, "sword_arming",
                "A long blade with a grip for two hands; it ends arguments.", M((iron, 5), (timber, 1))));
            Add(Weapon("maul", "Maul", WeaponClass.Mace, Wp("maul", false, 2, 6, DamageType.Bludgeoning, WeaponProperty.Heavy | WeaponProperty.TwoHanded, "Topple"), 30, 4500, "mace_flanged",
                "A great iron-bound mallet.", M((iron, 4), (timber, 2))));
            Add(Weapon("glaive", "Glaive", WeaponClass.Spear, Wp("glaive", false, 1, 10, DamageType.Slashing, WeaponProperty.Heavy | WeaponProperty.Reach | WeaponProperty.TwoHanded, "Graze"), 60, 2700, "spear_ash",
                "A single-edged blade on a long pole.", M((iron, 2), (timber, 2))));
            Add(Weapon("halberd", "Halberd", WeaponClass.Spear, Wp("halberd", false, 1, 10, DamageType.Slashing, WeaponProperty.Heavy | WeaponProperty.Reach | WeaponProperty.TwoHanded, "Cleave"), 60, 2700, "spear_ash",
                "An axe blade, a spike and a hook on a pole: the town watch's answer to horsemen.", M((iron, 3), (timber, 2))));
            Add(Weapon("pike", "Pike", WeaponClass.Spear, Wp("pike", false, 1, 10, DamageType.Piercing, WeaponProperty.Heavy | WeaponProperty.Reach | WeaponProperty.TwoHanded, "Push"), 15, 8200, "spear_ash",
                "A very long spear for a line of shoulders.", M((iron, 1), (timber, 3))));
            Add(Weapon("longbow", "Longbow", WeaponClass.Bow, Wp("longbow", false, 1, 8, DamageType.Piercing, WeaponProperty.Ammunition | WeaponProperty.Heavy | WeaponProperty.TwoHanded, "Slow", 150, 600, ranged: true), 150, 900, "bow_hunting",
                "A yew stave as tall as its archer.", M((timber, 2))));
            Add(Weapon("heavy_crossbow", "Heavy crossbow", WeaponClass.Crossbow, Wp("heavy crossbow", false, 1, 10, DamageType.Piercing, WeaponProperty.Ammunition | WeaponProperty.Heavy | WeaponProperty.Loading | WeaponProperty.TwoHanded, "Push", 100, 400, ranged: true), 150, 8200, "light_crossbow",
                "A windlass crossbow that punches through shields.", M((iron, 2), (timber, 2))));

            // ----- Armour: the rest of the SRD 5.2.1 table (one suit is worn; the chest slot) -----
            ItemDefinition Suit(string id, string name, ArmorProfile a, int value, int grams, string visual, string desc, ResourceStack[] mats) => new ItemDefinition
            {
                Id = id, Name = name, Category = ItemCategory.Armour, Subcategory = a.Category.ToString().ToLowerInvariant() + " armour", Slot = EquipmentSlot.Chest,
                BaseValue = value, WeightGrams = grams, Protection = a.BaseAc - 10, Visual = visual, Materials = mats, SoldBy = arms, Description = desc, Armor = a,
                RulesSource = ItemCombat.Srd, Source = srd, Wears = true, Tags = new[] { "armour" },
            };

            Add(Suit("padded_armour", "Padded armour", new ArmorProfile { Category = ArmorCategory.Light, BaseAc = 11, StealthDisadvantage = true }, 15, 3600, "outfit:Peasant:Body:Peasant2",
                "Quilted layers of cloth and wadding. Warm, cheap and noisy.", Array.Empty<ResourceStack>()));
            Add(Suit("hide_armour", "Hide armour", new ArmorProfile { Category = ArmorCategory.Medium, BaseAc = 12, DexCap = 2 }, 30, 5400, "outfit:Ranger:Body:Ranger",
                "Thick furs and hides, laced and layered.", Array.Empty<ResourceStack>()));
            Add(Suit("scale_mail", "Scale mail", new ArmorProfile { Category = ArmorCategory.Medium, BaseAc = 14, DexCap = 2, StealthDisadvantage = true }, 150, 20400, "outfit:Ranger:Body:Ranger3",
                "Overlapping iron scales on a leather coat.", M((iron, 6))));
            Add(Suit("breastplate", "Breastplate", new ArmorProfile { Category = ArmorCategory.Medium, BaseAc = 14, DexCap = 2 }, 1200, 9100, "outfit:Ranger:Body:Ranger3",
                "A fitted steel chest piece over a padded coat; light for what it stops.", M((iron, 6))));
            Add(Suit("ring_mail", "Ring mail", new ArmorProfile { Category = ArmorCategory.Heavy, BaseAc = 14, DexCap = 0, StealthDisadvantage = true }, 90, 18100, "outfit:Ranger:Body:Ranger3",
                "Iron rings sewn onto heavy leather.", M((iron, 6))));
            Add(Suit("splint_armour", "Splint armour", new ArmorProfile { Category = ArmorCategory.Heavy, BaseAc = 17, DexCap = 0, StrengthRequirement = 15, StealthDisadvantage = true }, 600, 27200, "outfit:Ranger:Body:Ranger3",
                "Vertical strips of metal riveted to a backing, with mail at the joints.", M((iron, 10))));

            // ----- Ammunition (the SRD's: twenty to a bundle) -----
            ItemDefinition Ammo(string id, string name, string[] forKinds, int value, int grams, string desc) => new ItemDefinition
            {
                Id = id, Name = name, Category = ItemCategory.Ammunition, Subcategory = "ammunition", BaseValue = value, WeightGrams = grams, Stackable = true, MaxStack = 60,
                AmmunitionFor = forKinds, SoldBy = arms, Description = desc, Source = srd, Visual = "like:timber_plank", Tags = new[] { "ammunition" },
            };

            Add(Ammo("arrows", "Arrows", new[] { "shortbow", "longbow" }, 1, 45, "Goose-fletched ash arrows with iron heads."));
            Add(Ammo("crossbow_bolts", "Crossbow bolts", new[] { "light crossbow", "heavy crossbow" }, 1, 70, "Short, heavy bolts with square heads."));
            Add(Ammo("sling_bullets", "Sling bullets", new[] { "sling" }, 1, 35, "Cast lead bullets the size of an acorn."));

            // ----- Tools: crafting disciplines and gathering -----
            ItemDefinition Tool(string id, string name, string discipline, int value, int grams, string desc, ShopCategory[] sold) => new ItemDefinition
            {
                Id = id, Name = name, Category = ItemCategory.Tool, Subcategory = "artisan's tools", BaseValue = value, WeightGrams = grams, ToolDiscipline = discipline, SoldBy = sold,
                Description = desc, Source = srd, Wears = true, Visual = "prop:kit", Tags = new[] { "tool" },
            };

            Add(Tool("leatherworkers_tools", "Leatherworker's tools", "leatherworking", 15, 2300, "Awls, knives, thread and a stitching pony.", general));
            Add(Tool("cooks_utensils", "Cook's utensils", "cooking", 3, 3600, "A pot, a pan, a ladle and a good knife.", provisions));
            Add(Tool("alchemists_supplies", "Alchemist's supplies", "alchemy", 150, 3600, "Glass, a small still, tongs and stoppered jars.", general));
            Add(Tool("weavers_tools", "Weaver's tools", "tailoring", 3, 2300, "Shuttles, needles and a hand loom.", general));
            Add(Tool("miners_pick", "Miner's pick", "mining", 6, 4500, "A pointed iron pick for loosening ore.", general));
            Add(Tool("fishing_tackle", "Fishing tackle", "fishing", 3, 1800, "Line, hooks, floats and a rod of split ash.", provisions));
            // The existing tools gain their disciplines (their IDs and prices are unchanged).
            foreach (var (id, discipline) in new[] { ("smiths_tools", "blacksmithing"), ("carpenters_tools", "woodworking"), ("herbalism_kit", "foraging") })
            {
                var t = Find(id);
                t.ToolDiscipline = discipline;
                t.Wears = true;
                t.Source = srd;
            }

            // ----- Crafting materials -----
            ItemDefinition Mat(string id, string name, string[] disciplines, int value, int grams, string desc, ResourceStack[] mats, ShopCategory[] sold, params string[] tags) => new ItemDefinition
            {
                Id = id, Name = name, Category = ItemCategory.Material, Subcategory = "material", BaseValue = value, WeightGrams = grams, Stackable = true, MaxStack = 50, MaterialFor = disciplines,
                Materials = mats, SoldBy = sold, Description = desc, Visual = "like:iron_bar", Tags = tags,
            };

            Add(Mat("iron_ore", "Iron ore", new[] { "blacksmithing" }, 2, 2000, "Rust-red lumps of ore from the hills.", M((iron, 1)), Array.Empty<ShopCategory>(), "metal", "ore"));
            Add(Mat("charcoal", "Charcoal", new[] { "blacksmithing", "cooking" }, 1, 500, "Black, light and dusty: the smith's fuel.", M((timber, 1)), materials, "fuel"));
            Add(Mat("raw_hide", "Raw hide", new[] { "leatherworking" }, 3, 3000, "A fresh hide, scraped but not yet cured.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>(), "hide", "game"));
            Add(Mat("wolf_pelt", "Wolf pelt", new[] { "leatherworking", "tailoring" }, 8, 2500, "A thick grey pelt, prized for winter cloaks.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>(), "hide", "game"));
            Add(Mat("cured_leather", "Cured leather", new[] { "leatherworking" }, 6, 1500, "Tanned leather, supple and strong.", Array.Empty<ResourceStack>(), materials, "leather"));
            Add(Mat("antler", "Antler", new[] { "woodworking" }, 2, 800, "A shed tine of deer antler, good for handles.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>(), "bone", "game"));
            Add(Mat("silverleaf", "Silverleaf", new[] { "alchemy" }, 2, 30, "A low herb with silver-backed leaves that stops bleeding.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>(), "herb"));
            Add(Mat("linen_cloth", "Linen cloth", new[] { "tailoring" }, 3, 300, "A bolt-end of plain linen.", Array.Empty<ResourceStack>(), materials, "cloth"));
            Add(Mat("glass_vial", "Glass vial", new[] { "alchemy" }, 1, 60, "A small stoppered vial of green glass.", Array.Empty<ResourceStack>(), materials, "glass"));
            foreach (var (id, discipline) in new[] { ("iron_bar", "blacksmithing"), ("timber_plank", "woodworking") })
            {
                var m = Find(id);
                m.MaterialFor = new[] { discipline };
            }

            // ----- Food and drink -----
            ItemDefinition Fare(string id, string name, int nourishment, int value, int grams, string desc, ResourceStack[] mats, ShopCategory[] sold, string verb = "Eat") => new ItemDefinition
            {
                Id = id, Name = name, Category = ItemCategory.Food, Subcategory = "food", BaseValue = value, WeightGrams = grams, Stackable = true, MaxStack = 10, Nourishment = nourishment,
                Consumable = new ConsumableProfile { Nourishment = nourishment, Verb = verb }, Materials = mats, SoldBy = sold, Description = desc, Visual = "like:bread_loaf",
            };

            Add(Fare("venison", "Venison", 10, 4, 1000, "A joint of raw deer meat. Better cooked.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>()));
            Add(Fare("fresh_fish", "Fresh fish", 8, 2, 500, "A trout from the stream, still cold.", Array.Empty<ResourceStack>(), Array.Empty<ShopCategory>()));
            Add(Fare("hunters_stew", "Hunter's stew", 45, 6, 700, "Venison, roots and barley stewed slow. A full belly for a day's walk.", M((food, 1)), Array.Empty<ShopCategory>()));
            Add(Fare("smoked_fish", "Smoked fish", 25, 4, 300, "Fish smoked over alder; keeps for weeks.", M((food, 1)), provisions));
            Add(Fare("ale_jug", "Jug of ale", 5, 2, 1200, "Brown ale from the tavern's own barrels.", M((food, 1)), provisions, "Drink"));

            // ----- Potions (SRD 5.2.1 magic items: drinking one is a bonus action) -----
            Add(new ItemDefinition
            {
                Id = "potion_healing", Name = "Potion of Healing", Category = ItemCategory.Potion, Subcategory = "potion of healing", Rarity = ItemRarity.Common, BaseValue = 150, WeightGrams = 250,
                Stackable = true, MaxStack = 10, Consumable = new ConsumableProfile { Healing = new Dice(2, 4, 2), InCombat = "bonus action", Verb = "Drink" }, Source = srd,
                SoldBy = general, Visual = "like:bread_loaf", Description = "A red draught that knits flesh as it goes down: 2d4 + 2 hit points.",
                Lore = "Every shrine in the vale keeps a few; the priests do not say what is in them.", Tags = new[] { "potion", "magic" },
            });
            Add(new ItemDefinition
            {
                Id = "potion_greater_healing", Name = "Potion of Greater Healing", Category = ItemCategory.Potion, Subcategory = "potion of healing", Rarity = ItemRarity.Uncommon, BaseValue = 450, WeightGrams = 250,
                Stackable = true, MaxStack = 10, Consumable = new ConsumableProfile { Healing = new Dice(4, 4, 4), InCombat = "bonus action", Verb = "Drink" }, Source = srd,
                Visual = "like:bread_loaf", Description = "A darker red, thick as syrup: 4d4 + 4 hit points.", Tags = new[] { "potion", "magic" },
            });

            // ----- Jewellery, clothing pieces, books, keys, quest items, containers -----
            Add(new ItemDefinition { Id = "gold_ring", Name = "Gold ring", Category = ItemCategory.Jewelry, Subcategory = "ring", Slot = EquipmentSlot.RingLeft, BaseValue = 75, WeightGrams = 10,
                Visual = "like:dice_set", Description = "A plain band of red gold.", Tags = new[] { "jewellery", "valuable" } });
            Add(new ItemDefinition { Id = "silver_amulet", Name = "Silver amulet", Category = ItemCategory.Jewelry, Subcategory = "amulet", Slot = EquipmentSlot.Neck, BaseValue = 40, WeightGrams = 40,
                Visual = "like:dice_set", Description = "A silver disc on a chain, stamped with a stag.", Tags = new[] { "jewellery", "valuable" } });
            Add(new ItemDefinition { Id = "travellers_cloak", Name = "Traveller's cloak", Category = ItemCategory.Clothing, Subcategory = "cloak", Slot = EquipmentSlot.Back, BaseValue = 6, WeightGrams = 1400,
                Visual = "like:tunic_work", SoldBy = new[] { ShopCategory.Clothing }, Description = "Heavy oiled wool with a deep hood." });
            Add(new ItemDefinition { Id = "almanac", Name = "Farmer's almanac", Category = ItemCategory.Book, Subcategory = "book", BaseValue = 8, WeightGrams = 600, SoldBy = general,
                Visual = "like:spellbook", Description = "Moons, sowing days and remedies, much annotated.", Lore = "The margins say more about the vale's weather than the print does." });
            Add(new ItemDefinition { Id = "old_key", Name = "Old iron key", Category = ItemCategory.Key, Subcategory = "key", BaseValue = 1, WeightGrams = 60,
                Visual = "like:dagger_iron", Description = "A heavy key, its bow worn smooth. Nobody remembers its lock." });
            Add(new ItemDefinition { Id = "sealed_letter", Name = "Sealed letter", Category = ItemCategory.Quest, Subcategory = "letter", BaseValue = 0, WeightGrams = 20,
                Visual = "like:spellbook", Description = "A letter under an unbroken seal. Not yours to open.", Tags = new[] { "quest" } });
            Add(new ItemDefinition { Id = "backpack", Name = "Backpack", Category = ItemCategory.Container, Subcategory = "pack", BaseValue = 6, WeightGrams = 2300, ContainerCapacityGrams = 13600,
                SoldBy = general, Visual = "like:grain_sack", Source = srd, Description = "Canvas and leather with two straps. (Carried goods count against your strength either way.)" });

            // ----- Magic items (SRD 5.2.1 names and rules; descriptions are Realmkeeper's own) -----
            ItemDefinition Magic(ItemDefinition baseItem, string id, string name, ItemRarity rarity, int value, MagicProfile m, string desc, string lore = null)
            {
                var d = new ItemDefinition
                {
                    Id = id, Name = name, Category = baseItem.Category, Subcategory = baseItem.Subcategory ?? baseItem.Weapon?.Kind, Slot = baseItem.Slot, WeaponClass = baseItem.WeaponClass,
                    TwoHanded = baseItem.TwoHanded, BaseValue = value, WeightGrams = baseItem.WeightGrams, Damage = baseItem.Damage, Protection = baseItem.Protection, Visual = "like:" + (baseItem.Visual != null && baseItem.Visual.StartsWith("like:", StringComparison.Ordinal) ? baseItem.Visual.Substring(5) : baseItem.Id),
                    Weapon = baseItem.Weapon, Armor = baseItem.Armor, RulesSource = baseItem.RulesSource, Rarity = rarity, Magic = m, Source = srd, Wears = baseItem.Wears || baseItem.Weapon != null || baseItem.Armor != null,
                    Description = desc, Lore = lore, Tags = new[] { "magic" },
                };
                if (baseItem.Category == ItemCategory.Armour && baseItem.Visual != null && baseItem.Visual.StartsWith("outfit:", StringComparison.Ordinal)) d.Visual = baseItem.Visual;
                return d;
            }

            ItemDefinition Wondrous(string id, string name, ItemCategory category, EquipmentSlot slot, ItemRarity rarity, int value, int grams, MagicProfile m, string desc, string like) => new ItemDefinition
            {
                Id = id, Name = name, Category = category, Subcategory = "wondrous item", Slot = slot, Rarity = rarity, BaseValue = value, WeightGrams = grams, Magic = m, Source = srd,
                Visual = "like:" + like, Description = desc, Tags = new[] { "magic" },
            };

            Add(Magic(Find("sword_arming"), "longsword_plus1", "+1 Longsword", ItemRarity.Uncommon, 1500, new MagicProfile { WeaponBonus = 1 },
                "An arming sword whose edge never seems to dull: +1 to attack and damage rolls.", "Smiths in Bramhaven say it was quenched in the mill race on a frost night."));
            Add(Magic(Find("dagger_iron"), "dagger_plus1", "+1 Dagger", ItemRarity.Uncommon, 900, new MagicProfile { WeaponBonus = 1 },
                "A dagger that sits in the hand as if it chose it: +1 to attack and damage rolls."));
            Add(Magic(Find("battleaxe"), "berserker_axe", "Berserker Axe", ItemRarity.Rare, 2400, new MagicProfile
                {
                    WeaponBonus = 1, RequiresAttunement = true, Effects = new[] { new RuleEffect(EffectKind.MaxHpPerLevel, value: 1) },
                    Curse = "Once attuned, you cannot willingly part with the axe: it cannot be unequipped or unattuned.",
                    StatusNote = "Partial: the bonus, the extra hit points and the binding are applied; the rage it provokes when you are damaged is not modelled.",
                },
                "A heavy axe with a red-stained haft: +1 to attack and damage rolls, and 1 more hit point per level while attuned. It is cursed."));
            Add(Magic(Find("chain_shirt"), "chain_shirt_plus1", "+1 Chain Shirt", ItemRarity.Rare, 2100, new MagicProfile { AcBonus = 1 },
                "Rings so fine they ripple like water: +1 to AC while worn."));
            Add(Magic(Find("shield_round"), "shield_plus1", "+1 Shield", ItemRarity.Uncommon, 1200, new MagicProfile { AcBonus = 1 },
                "A round shield whose boss is always cold: +1 to AC beyond the shield's own."));
            Add(Wondrous("ring_of_protection", "Ring of Protection", ItemCategory.Jewelry, EquipmentSlot.RingLeft, ItemRarity.Rare, 3500, 10,
                new MagicProfile { AcBonus = 1, SaveBonus = 1, RequiresAttunement = true }, "A steel band set with a grey stone: +1 to AC and saving throws while attuned.", "gold_ring"));
            Add(Wondrous("cloak_of_protection", "Cloak of Protection", ItemCategory.Wondrous, EquipmentSlot.Back, ItemRarity.Uncommon, 1500, 1400,
                new MagicProfile { AcBonus = 1, SaveBonus = 1, RequiresAttunement = true }, "A plain brown cloak that turns aside what it should not: +1 to AC and saving throws while attuned.", "tunic_work"));
            Add(Wondrous("gauntlets_of_ogre_power", "Gauntlets of Ogre Power", ItemCategory.Wondrous, EquipmentSlot.Hands, ItemRarity.Uncommon, 2000, 900,
                new MagicProfile { RequiresAttunement = true, Effects = new[] { new RuleEffect(EffectKind.AbilityMinimum, "Strength", 19) } },
                "Studded leather gauntlets: your Strength is 19 while you wear them and are attuned (no effect if it is already 19 or more).", "bracers_leather"));
            Add(Wondrous("goggles_of_night", "Goggles of Night", ItemCategory.Wondrous, EquipmentSlot.Head, ItemRarity.Uncommon, 1500, 100,
                new MagicProfile { Effects = new[] { new RuleEffect(EffectKind.Darkvision, value: 60) } }, "Smoked lenses in brass rims: darkvision 60 ft while worn.", "cap_iron"));
            Add(Wondrous("boots_of_elvenkind", "Boots of Elvenkind", ItemCategory.Wondrous, EquipmentSlot.Feet, ItemRarity.Uncommon, 1500, 700,
                new MagicProfile { Effects = new[] { new RuleEffect(EffectKind.Advantage, "Stealth", text: "Boots of Elvenkind") } }, "Soft boots that make no sound: advantage on Stealth checks while worn.", "boots_riding"));
            Add(Wondrous("wand_of_magic_missiles", "Wand of Magic Missiles", ItemCategory.Wondrous, EquipmentSlot.None, ItemRarity.Uncommon, 2400, 450,
                new MagicProfile { MaxCharges = 7, Recharge = new Dice(1, 6, 1), SpellId = "magic_missile" },
                "A slender wand of black wood: 7 charges; spend 1 to cast Magic Missile at level 1 (more charges for more darts). It regains 1d6 + 1 charges at dawn.", "staff_quarter"));
        }

        /// <summary>The Phase 11 defaults for every item: foods become consumables, combat items wear and carry their SRD source.</summary>
        private static void CompletePhase11()
        {
            foreach (var d in All)
            {
                if (d.Nourishment > 0 && d.Consumable == null) d.Consumable = new ConsumableProfile { Nourishment = d.Nourishment, Verb = "Eat" };
                if (d.Weapon != null || d.Armor != null) { d.Wears = !d.Stackable; if (d.Source == ContentSource.RealmkeeperOriginal) d.Source = ContentSource.Srd521; }

                // The rules data decides how many hands a weapon needs: Phase 5 marked the spear and quarterstaff two-handed,
                // but in SRD 5.2.1 both are Versatile (one hand, or two for the larger die), so a shield may go with them.
                if (d.Weapon != null) d.TwoHanded = d.Weapon.Has(WeaponProperty.TwoHanded);
                if (d.Subcategory == null) d.Subcategory = d.Weapon?.Kind ?? (d.Armor != null ? d.Armor.Category.ToString().ToLowerInvariant() : d.Category.ToString().ToLowerInvariant());
            }
        }

        /// <summary>
        /// The catalog's own checks (Phase 11): unique IDs; values and weights not negative; stack sizes sensible;
        /// equipment slots that suit the category; combat data where combat needs it; magic within its rules; every
        /// tool, material and ammunition reference known.
        /// </summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in All)
            {
                if (string.IsNullOrWhiteSpace(d.Id) || !seen.Add(d.Id)) errors.Add($"Item '{d.Id}' is missing or duplicated.");
                if (string.IsNullOrWhiteSpace(d.Name)) errors.Add($"Item '{d.Id}' has no name.");
                if (string.IsNullOrWhiteSpace(d.Description)) errors.Add($"Item '{d.Id}' has no description.");
                if (d.BaseValue < 0 || d.WeightGrams < 0) errors.Add($"Item '{d.Id}' has a negative value or weight.");
                if (d.Stackable && d.MaxStack < 2) errors.Add($"Item '{d.Id}' stacks but its stack holds one.");
                if (!d.Stackable && d.MaxStack != 1) errors.Add($"Item '{d.Id}' does not stack but has a stack size.");
                if (d.Stackable && d.Wears) errors.Add($"Item '{d.Id}' stacks but wears (condition belongs to single objects).");
                if (d.Category == ItemCategory.Weapon && d.Weapon == null) errors.Add($"Weapon '{d.Id}' has no combat data.");
                if ((d.Category == ItemCategory.Shield || d.Armor?.Category == ArmorCategory.Shield) && d.Slot != EquipmentSlot.OffHand) errors.Add($"Shield '{d.Id}' is not held in the off hand.");
                if (d.Armor != null && d.Armor.Category != ArmorCategory.Shield && d.Slot != EquipmentSlot.Chest) errors.Add($"Armour '{d.Id}' is not worn on the chest.");
                if (d.Weapon != null && d.Weapon.Has(WeaponProperty.TwoHanded) != d.TwoHanded) errors.Add($"Weapon '{d.Id}' disagrees with itself about two hands.");
                if (d.Consumable != null && d.Consumable.Healing.IsEmpty && d.Consumable.Nourishment <= 0) errors.Add($"Consumable '{d.Id}' does nothing.");
                if (d.Magic != null)
                {
                    if (d.Rarity == ItemRarity.Common && d.Category != ItemCategory.Potion) errors.Add($"Magic item '{d.Id}' is common.");
                    if (d.Magic.WeaponBonus != 0 && d.Weapon == null) errors.Add($"'{d.Id}' has a weapon bonus but is no weapon.");
                    if (d.Magic.MaxCharges < 0 || d.Magic.MaxCharges == 0 && d.Magic.SpellId != null) errors.Add($"'{d.Id}' casts a spell without charges.");
                    if (d.Magic.Curse != null && !d.Magic.RequiresAttunement) errors.Add($"Cursed '{d.Id}' must require attunement (the curse binds on attuning).");
                    if (d.Magic.SpellId != null && Realmkeeper.Domain.Magic.SpellCatalog.Find(d.Magic.SpellId) == null) errors.Add($"'{d.Id}' casts an unknown spell.");
                }

                if (d.Rarity > ItemRarity.Common && d.Magic == null && d.Category != ItemCategory.Potion) errors.Add($"'{d.Id}' is rare without being magic.");
                foreach (var kind in d.AmmunitionFor)
                {
                    var any = false;
                    foreach (var w in All) if (w.Weapon != null && w.Weapon.Kind == kind) any = true;
                    if (!any) errors.Add($"Ammunition '{d.Id}' serves an unknown weapon kind '{kind}'.");
                }
            }

            return errors;
        }
    }
}
