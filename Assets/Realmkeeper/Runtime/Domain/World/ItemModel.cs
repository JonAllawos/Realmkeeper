using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.World
{
    // Phase 11 (ADR-0050): the parts of the item model that are not the definition or the instance themselves —
    // rarity, condition, the category table, and the typed profiles a definition may carry.

    /// <summary>How rare an item is (SRD 5.2.1's rarities). Rarity is about discovery and value, never balance by itself.</summary>
    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        VeryRare = 3,
        Legendary = 4,
        Artifact = 5,
    }

    /// <summary>
    /// The physical state of an object that wears (weapons, armour, shields, tools). Only Damaged and Broken change the
    /// rules (<see cref="ItemConditions"/>); every state changes the price.
    /// </summary>
    public enum ItemCondition
    {
        Pristine = 0,
        Good = 1,
        Worn = 2,
        Damaged = 3,
        Broken = 4,
    }

    /// <summary>What each condition means: the rules effect, the value, and what causes a step down.</summary>
    public static class ItemConditions
    {
        /// <summary>Price multiplier in percent.</summary>
        public static int ValuePercent(ItemCondition c)
        {
            switch (c)
            {
                case ItemCondition.Pristine: return 110;
                case ItemCondition.Good: return 100;
                case ItemCondition.Worn: return 75;
                case ItemCondition.Damaged: return 50;
                default: return 25;
            }
        }

        public static int QualityValuePercent(ItemQuality q) => q == ItemQuality.Fine ? 150 : q == ItemQuality.Poor ? 50 : 100;

        /// <summary>A damaged weapon's attack penalty, or a damaged armour or shield's AC penalty (Damaged −1).</summary>
        public static int Penalty(ItemCondition c) => c == ItemCondition.Damaged ? 1 : 0;

        /// <summary>A broken weapon cannot attack; broken armour or a broken shield gives no AC.</summary>
        public static bool Unusable(ItemCondition c) => c == ItemCondition.Broken;

        /// <summary>One step worse (Broken stays Broken).</summary>
        public static ItemCondition Worse(ItemCondition c) => c >= ItemCondition.Broken ? ItemCondition.Broken : c + 1;

        public static string Name(ItemCondition c) => c.ToString().ToLowerInvariant();

        /// <summary>What a condition does, in words.</summary>
        public static string Effect(ItemCondition c, bool weapon)
        {
            switch (c)
            {
                case ItemCondition.Damaged: return weapon ? "−1 to attack rolls until repaired" : "−1 to AC until repaired";
                case ItemCondition.Broken: return weapon ? "cannot be used to attack until repaired" : "gives no AC until repaired";
                case ItemCondition.Worn: return "no effect in use; worth less";
                default: return "no effect";
            }
        }

        /// <summary>The causes, as the rules apply them (ADR-0052).</summary>
        public const string Causes = "A weapon wears one step when its wielder rolls a natural 1 to attack with it; worn armour and a shield wear one step when their wearer takes a critical hit.";
    }

    /// <summary>One row of the category table: what the UI calls it and how it groups it. New categories only need a row.</summary>
    public sealed class ItemCategoryInfo
    {
        public ItemCategory Category { get; set; }
        public string Name { get; set; }

        /// <summary>The filter group in the inventory ("Weapons & armour", "Consumables", "Materials & goods", "Other").</summary>
        public string Group { get; set; }

        public int Order { get; set; }
    }

    public static class ItemCategories
    {
        public static readonly IReadOnlyList<ItemCategoryInfo> All = new[]
        {
            new ItemCategoryInfo { Category = ItemCategory.Weapon, Name = "Weapons", Group = "Weapons & armour", Order = 0 },
            new ItemCategoryInfo { Category = ItemCategory.Shield, Name = "Shields", Group = "Weapons & armour", Order = 1 },
            new ItemCategoryInfo { Category = ItemCategory.Armour, Name = "Armour", Group = "Weapons & armour", Order = 2 },
            new ItemCategoryInfo { Category = ItemCategory.Headwear, Name = "Headwear", Group = "Clothing", Order = 3 },
            new ItemCategoryInfo { Category = ItemCategory.Clothing, Name = "Clothing", Group = "Clothing", Order = 4 },
            new ItemCategoryInfo { Category = ItemCategory.Jewelry, Name = "Jewellery", Group = "Clothing", Order = 5 },
            new ItemCategoryInfo { Category = ItemCategory.Accessory, Name = "Accessories", Group = "Clothing", Order = 6 },
            new ItemCategoryInfo { Category = ItemCategory.Wondrous, Name = "Wondrous items", Group = "Magic", Order = 7 },
            new ItemCategoryInfo { Category = ItemCategory.Ammunition, Name = "Ammunition", Group = "Weapons & armour", Order = 8 },
            new ItemCategoryInfo { Category = ItemCategory.Potion, Name = "Potions", Group = "Consumables", Order = 9 },
            new ItemCategoryInfo { Category = ItemCategory.Food, Name = "Food & drink", Group = "Consumables", Order = 10 },
            new ItemCategoryInfo { Category = ItemCategory.Tool, Name = "Tools", Group = "Tools & foci", Order = 11 },
            new ItemCategoryInfo { Category = ItemCategory.Focus, Name = "Spellcasting foci", Group = "Tools & foci", Order = 12 },
            new ItemCategoryInfo { Category = ItemCategory.Material, Name = "Crafting materials", Group = "Materials & goods", Order = 13 },
            new ItemCategoryInfo { Category = ItemCategory.TradeGood, Name = "Trade goods", Group = "Materials & goods", Order = 14 },
            new ItemCategoryInfo { Category = ItemCategory.Book, Name = "Books & documents", Group = "Other", Order = 15 },
            new ItemCategoryInfo { Category = ItemCategory.Key, Name = "Keys", Group = "Other", Order = 16 },
            new ItemCategoryInfo { Category = ItemCategory.Container, Name = "Containers", Group = "Other", Order = 17 },
            new ItemCategoryInfo { Category = ItemCategory.Quest, Name = "Quest items", Group = "Other", Order = 18 },
            new ItemCategoryInfo { Category = ItemCategory.Misc, Name = "Miscellaneous", Group = "Other", Order = 19 },
        };

        public static ItemCategoryInfo Of(ItemCategory c)
        {
            foreach (var i in All) if (i.Category == c) return i;
            return new ItemCategoryInfo { Category = c, Name = c.ToString(), Group = "Other", Order = 99 };
        }

        public static IReadOnlyList<string> Groups => new[] { "Weapons & armour", "Clothing", "Magic", "Consumables", "Tools & foci", "Materials & goods", "Other" };
    }

    /// <summary>What using a consumable does (food, drink, potions).</summary>
    public sealed class ConsumableProfile
    {
        /// <summary>Hit points regained (a potion of healing's 2d4+2), or empty.</summary>
        public Dice Healing { get; set; }

        /// <summary>How much it feeds ("fed" points), as food did in Phase 5.</summary>
        public int Nourishment { get; set; }

        /// <summary>The action it takes in a fight (drinking a potion is a bonus action in SRD 5.2.1); null: not in a fight.</summary>
        public string InCombat { get; set; }

        /// <summary>The verb shown on the button ("Drink", "Eat").</summary>
        public string Verb { get; set; } = "Use";
    }

    /// <summary>
    /// A magic item's rules (ADR-0052): typed bonuses and effects the shared rules engine applies — never a separate
    /// calculation. Bonuses apply only while the item is worn or wielded (and attuned when attunement is required).
    /// </summary>
    public sealed class MagicProfile
    {
        /// <summary>Bonus to attack and damage rolls with this weapon (+1 …).</summary>
        public int WeaponBonus { get; set; }

        /// <summary>Bonus to AC while worn (magic armour, a shield, a ring or cloak of protection).</summary>
        public int AcBonus { get; set; }

        /// <summary>Bonus to every saving throw while worn (ring or cloak of protection).</summary>
        public int SaveBonus { get; set; }

        /// <summary>Rule effects while worn (an ability minimum, darkvision, advantage on a skill …).</summary>
        public IReadOnlyList<RuleEffect> Effects { get; set; } = Array.Empty<RuleEffect>();

        public bool RequiresAttunement { get; set; }

        /// <summary>Maximum charges (0: none).</summary>
        public int MaxCharges { get; set; }

        /// <summary>Charges regained at dawn (rolled), or empty.</summary>
        public Dice Recharge { get; set; }

        /// <summary>The spell its charges cast (a spell catalog ID), or null; one charge per spell level.</summary>
        public string SpellId { get; set; }

        /// <summary>A curse, in words, or null. A cursed item binds itself when attuned: it cannot be unequipped or unattuned.</summary>
        public string Curse { get; set; }

        /// <summary>Gaps in the item's rules, in words (Partial items say what is not modelled).</summary>
        public string StatusNote { get; set; }
    }

    /// <summary>Carrying (SRD 5.2.1): capacity Strength × 15 lb; over it, Speed 5 ft; nothing more past twice it.</summary>
    public static class Carrying
    {
        public const double GramsPerPound = 453.59237;

        public static int Pounds(int grams) => (int)Math.Round(grams / GramsPerPound);

        public static int CapacityGrams(int strength, int percent) => (int)Math.Round(strength * 15 * GramsPerPound * percent / 100.0);

        /// <summary>The attunement limit (SRD 5.2.1): three items at once.</summary>
        public const int MaxAttuned = 3;
    }
}

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 11: what the player carries, by weight (pack and worn), for carrying capacity and pace.</summary>
    public static class ItemWeights
    {
        public static int StackGrams(ItemStackData s) => (ItemCatalog.Find(s?.ItemId)?.WeightGrams ?? 0) * (s?.Quantity ?? 0);

        public static int CarriedGrams(PlayerCharacterData p)
        {
            if (p == null) return 0;
            var grams = 0;
            foreach (var s in p.Inventory) grams += StackGrams(s);
            foreach (var s in p.Equipment.Values) grams += StackGrams(s);
            return grams;
        }

        public static int Grams(IEnumerable<ItemStackData> stacks)
        {
            var grams = 0;
            foreach (var s in stacks) grams += StackGrams(s);
            return grams;
        }
    }
}

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 11 (ADR-0050): making and comparing item instances. Every new stack is made here, so a wand starts with
    /// its charges and nothing is created half-formed.
    /// </summary>
    public static class ItemStacks
    {
        public static ItemStackData Create(Identifiers.ItemStackId id, ItemDefinition def, int quantity, string provenance = null, long acquiredAt = -1) => new ItemStackData
        {
            StackId = id,
            ItemId = def.Id,
            Quantity = quantity,
            Charges = def.Magic != null && def.Magic.MaxCharges > 0 ? def.Magic.MaxCharges : -1,
            Provenance = provenance,
            AcquiredAt = acquiredAt,
        };

        /// <summary>Price multiplier (percent) of this instance's condition and quality.</summary>
        public static int ValuePercent(ItemStackData s) => ItemConditions.ValuePercent(s.Condition) * ItemConditions.QualityValuePercent(s.Quality) / 100;

        /// <summary>
        /// True when the instance is like any fresh one (good condition, common make, full charges, not attuned or bound):
        /// a shop can take it into its counted stock. Anything else keeps its identity (a shop's <c>Goods</c>).
        /// </summary>
        public static bool IsPlain(ItemStackData s, ItemDefinition def) =>
            s.Condition == ItemCondition.Good && s.Quality == ItemQuality.Common && !s.Attuned && !s.Bound
            && (s.Charges < 0 || def?.Magic == null || s.Charges == def.Magic.MaxCharges);

        /// <summary>Steps below Good (0 when nothing needs mending).</summary>
        public static int RepairSteps(ItemStackData s) => s.Condition > ItemCondition.Good ? (int)s.Condition - (int)ItemCondition.Good : 0;

        /// <summary>
        /// The craft that mends an item (ADR-0054): leather and padded armour by leatherworking; wooden weapons and bows by
        /// woodworking; other weapons, armour and shields by blacksmithing; null when it does not wear.
        /// </summary>
        public static string RepairDiscipline(ItemDefinition def)
        {
            if (def == null || !def.Wears) return null;
            if (def.ToolDiscipline != null) return "blacksmithing";
            if (def.Armor != null && def.Armor.Category == ArmorCategory.Light) return "leatherworking";
            if (def.Weapon != null)
            {
                var k = def.Weapon.Kind ?? string.Empty;
                if (k == "club" || k == "quarterstaff" || k == "greatclub" || k.Contains("bow") || k == "sling" || k == "javelin") return "woodworking";
            }

            return "blacksmithing";
        }

        /// <summary>A smith's price to mend an instance to good condition: 15% of its base value per step (at least 1).</summary>
        public static int RepairPrice(ItemDefinition def, ItemStackData s)
        {
            var steps = RepairSteps(s);
            return steps == 0 ? 0 : Math.Max(1, def.BaseValue * steps * 15 / 100);
        }
    }
}
