using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 5: items. Definitions are data (ItemCatalog); stacks are instances owned by exactly one
    // container (the player's inventory or equipment, or a shop's stock). Enums persist by name.

    public enum ItemCategory
    {
        Weapon = 0,
        Shield = 1,
        Clothing = 2,
        Armour = 3,
        Headwear = 4,
        Food = 5,
        TradeGood = 6,
        Accessory = 7,

        /// <summary>Phase 6: the tools of a trade (proficiency tools: thieves' tools, smith's tools, instruments …).</summary>
        Tool = 8,

        /// <summary>Phase 6: a spellcasting focus or book (used when magic arrives).</summary>
        Focus = 9,
    }

    public enum EquipmentSlot
    {
        None = 0,
        MainHand = 1,
        OffHand = 2,
        Head = 3,
        Chest = 4,
        Hands = 5,
        Legs = 6,
        Feet = 7,
        Back = 8,
    }

    public enum WeaponClass
    {
        None = 0,
        Dagger = 1,
        Sword = 2,
        Axe = 3,
        Mace = 4,
        Spear = 5,
        Staff = 6,
        Bow = 7,
        Crossbow = 8,
    }

    public enum ItemQuality
    {
        Common = 0,
        Fine = 1,
    }

    /// <summary>What a shop sells (a shop may stock several).</summary>
    public enum ShopCategory
    {
        General = 0,
        WeaponsAndArmour = 1,
        Clothing = 2,
        Provisions = 3,
        Materials = 4,
    }

    /// <summary>
    /// What an item is (data, never saved): identity, description, category, value, weight,
    /// stacking, slot, the visual the presentation shows when it is equipped, functional
    /// properties, and the aggregate materials one unit is made from (the bridge to the settlement
    /// economy used when shops restock).
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemCategory Category { get; set; }
        public ItemQuality Quality { get; set; }

        /// <summary>Base value in coin.</summary>
        public int BaseValue { get; set; }

        /// <summary>Weight in grams.</summary>
        public int WeightGrams { get; set; }

        public bool Stackable { get; set; }
        public int MaxStack { get; set; } = 1;
        public EquipmentSlot Slot { get; set; }

        /// <summary>Occupies both hands (main hand; the off hand must be empty).</summary>
        public bool TwoHanded { get; set; }

        /// <summary>Visual reference the presentation resolves (e.g. "outfit:Ranger:Body:Ranger3", "prop:sword").</summary>
        public string Visual { get; set; }

        public int Protection { get; set; }
        public WeaponClass WeaponClass { get; set; }

        /// <summary>Damage potential, for a future combat system. Not used yet.</summary>
        public int Damage { get; set; }

        /// <summary>For food: how much eating one restores of the player's "fed" condition.</summary>
        public int Nourishment { get; set; }

        /// <summary>Aggregate settlement resources one unit is made from (consumed when a shop restocks).</summary>
        public IReadOnlyList<ResourceStack> Materials { get; set; } = Array.Empty<ResourceStack>();

        /// <summary>Shops that stock it.</summary>
        public IReadOnlyList<ShopCategory> SoldBy { get; set; } = Array.Empty<ShopCategory>();

        public bool IsEquippable => Slot != EquipmentSlot.None;
        public bool IsConsumable => Nourishment > 0;
    }

    /// <summary>
    /// An item instance: a quantity of one definition, owned by the container that holds it.
    /// Stackable items share a stack; others are one per stack (quantity 1).
    /// </summary>
    public sealed class ItemStackData
    {
        public ItemStackId StackId { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }

        public ItemStackData Clone() => (ItemStackData)MemberwiseClone();
    }

    /// <summary>The player's looks, limited to what the character assets can render.</summary>
    public sealed class PlayerAppearanceData
    {
        public NpcSex Frame { get; set; } = NpcSex.Male;

        /// <summary>Hair part ID, or null for none.</summary>
        public string HairId { get; set; } = "Hair_SimpleParted";

        /// <summary>Beard part ID, or null (male frame only).</summary>
        public string BeardId { get; set; } = "Hair_Beard";

        public int SkinTone { get; set; } = 1;
        public int HairColour { get; set; } = 1;

        /// <summary>Tint applied to clothing (index into the outfit tints).</summary>
        public int ClothTint { get; set; }

        public PlayerAppearanceData Clone() => (PlayerAppearanceData)MemberwiseClone();
    }

    /// <summary>How one named person regards the player (Phase 5), with its causes.</summary>
    public sealed class PlayerRegardData
    {
        private int _familiarity, _affinity;

        /// <summary>0–100: how well they know the player (conversations, trades).</summary>
        public int Familiarity { get => _familiarity; set => _familiarity = NpcBounds.Clamp(value, 0, 100); }

        /// <summary>−100…100: how they feel about the player.</summary>
        public int Affinity { get => _affinity; set => _affinity = NpcBounds.Clamp(value, -100, 100); }

        public int Trades { get; set; }
        public int Conversations { get; set; }

        /// <summary>Day number of the last trade and the last conversation that counted (one of each per day counts).</summary>
        public long LastTradeDay { get; set; } = -1;
        public long LastTalkDay { get; set; } = -1;

        public PlayerRegardData Clone() => (PlayerRegardData)MemberwiseClone();
    }
}

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// What the player's look may be set to: only parts and colours the character art can render
    /// (the same hair and beard parts and palettes the residents use). Counts match the presentation
    /// palettes (skin 5, hair 6, cloth 4).
    /// </summary>
    public static class PlayerAppearanceOptions
    {
        public static readonly System.Collections.Generic.IReadOnlyList<string> MaleHair = new[] { "Hair_SimpleParted", "Hair_Buzzed", "Hair_Long", null };
        public static readonly System.Collections.Generic.IReadOnlyList<string> FemaleHair = new[] { "Hair_Buns", "Hair_Long", "Hair_BuzzedFemale", null };
        public static readonly System.Collections.Generic.IReadOnlyList<string> Beards = new[] { "Hair_Beard", null };
        public const int SkinTones = 5;
        public const int HairColours = 6;
        public const int ClothTints = 4;

        public static System.Collections.Generic.IReadOnlyList<string> HairFor(NpcSex frame) => frame == NpcSex.Female ? FemaleHair : MaleHair;

        /// <summary>Null when the appearance can be rendered, otherwise why not.</summary>
        public static string Check(PlayerAppearanceData a)
        {
            if (a == null) return "An appearance is required.";
            if (a.Frame != NpcSex.Male && a.Frame != NpcSex.Female) return "Unknown body frame.";
            var hair = HairFor(a.Frame);
            var found = false;
            foreach (var h in hair) if (h == a.HairId) found = true;
            if (!found) return $"Hair '{a.HairId ?? "none"}' is not available for that frame.";
            if (a.BeardId != null && (a.Frame != NpcSex.Male || a.BeardId != "Hair_Beard")) return "That beard is not available.";
            if (a.SkinTone < 0 || a.SkinTone >= SkinTones) return $"Skin tone must be 0–{SkinTones - 1}.";
            if (a.HairColour < 0 || a.HairColour >= HairColours) return $"Hair colour must be 0–{HairColours - 1}.";
            if (a.ClothTint < 0 || a.ClothTint >= ClothTints) return $"Cloth tint must be 0–{ClothTints - 1}.";
            return null;
        }
    }
}
