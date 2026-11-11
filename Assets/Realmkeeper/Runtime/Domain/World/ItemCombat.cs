using System;
using System.Collections.Generic;
using System.Globalization;

namespace Realmkeeper.Domain.World
{
    // Phase 8: the combat data of items (catalog data, never saved). Armour and weapon numbers follow the
    // SRD 5.2.1 equipment tables (Wizards of the Coast LLC, CC BY 4.0), verified for Phase 8; item IDs, names,
    // descriptions and visuals are Realmkeeper's own and unchanged.

    public enum ArmorCategory
    {
        /// <summary>Not armour: clothing, or an accessory piece (bracers, leggings, pauldrons, cap) that gives no AC.</summary>
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3,
        Shield = 4,
    }

    public enum DamageType
    {
        Acid = 0,
        Bludgeoning = 1,
        Cold = 2,
        Fire = 3,
        Force = 4,
        Lightning = 5,
        Necrotic = 6,
        Piercing = 7,
        Poison = 8,
        Psychic = 9,
        Radiant = 10,
        Slashing = 11,
        Thunder = 12,
    }

    [Flags]
    public enum WeaponProperty
    {
        None = 0,
        Finesse = 1,
        Light = 2,
        Heavy = 4,
        Thrown = 8,
        TwoHanded = 16,
        Versatile = 32,
        Reach = 64,
        Ammunition = 128,
        Loading = 256,
    }

    /// <summary>Dice: count × d sides + bonus ("2d6+1").</summary>
    public readonly struct Dice : IEquatable<Dice>
    {
        public Dice(int count, int sides, int bonus = 0)
        {
            Count = count;
            Sides = sides;
            Bonus = bonus;
        }

        public int Count { get; }
        public int Sides { get; }
        public int Bonus { get; }
        public bool IsEmpty => Count <= 0 || Sides <= 0;

        public override string ToString() =>
            (IsEmpty ? string.Empty : Count.ToString(CultureInfo.InvariantCulture) + "d" + Sides.ToString(CultureInfo.InvariantCulture)) +
            (Bonus == 0 ? string.Empty : (Bonus > 0 && !IsEmpty ? "+" : string.Empty) + Bonus.ToString(CultureInfo.InvariantCulture));

        public bool Equals(Dice other) => Count == other.Count && Sides == other.Sides && Bonus == other.Bonus;
        public override bool Equals(object obj) => obj is Dice d && Equals(d);
        public override int GetHashCode() => (Count * 397) ^ (Sides * 31) ^ Bonus;
    }

    /// <summary>What a weapon does in combat (SRD 5.2.1 weapon table).</summary>
    public sealed class WeaponProfile
    {
        /// <summary>The weapon kind used for proficiency ("longsword", "dagger" …).</summary>
        public string Kind { get; set; }

        /// <summary>Simple (true) or martial.</summary>
        public bool Simple { get; set; }

        /// <summary>A ranged weapon (bows, crossbows); thrown melee weapons are melee with a range.</summary>
        public bool Ranged { get; set; }

        public Dice Damage { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>The die used two-handed (Versatile), or empty.</summary>
        public Dice Versatile { get; set; }

        public WeaponProperty Properties { get; set; }

        /// <summary>Normal and long range in feet (ranged and thrown weapons), or 0.</summary>
        public int Range { get; set; }
        public int LongRange { get; set; }

        /// <summary>The SRD 5.2.1 mastery property name (used when weapon mastery arrives).</summary>
        public string Mastery { get; set; }

        public bool Has(WeaponProperty p) => (Properties & p) != 0;

        /// <summary>Melee reach in feet (5, or 10 with Reach).</summary>
        public int Reach => Has(WeaponProperty.Reach) ? 10 : 5;
    }

    /// <summary>Armour and shields (SRD 5.2.1 armour table).</summary>
    public sealed class ArmorProfile
    {
        public ArmorCategory Category { get; set; }

        /// <summary>Base AC for armour; the bonus for a shield.</summary>
        public int BaseAc { get; set; }

        /// <summary>The most Dexterity adds (−1 = no cap; 0 = none, as for heavy armour).</summary>
        public int DexCap { get; set; } = -1;

        /// <summary>The Strength score below which the armour slows its wearer by 10 ft (0 = none).</summary>
        public int StrengthRequirement { get; set; }

        public bool StealthDisadvantage { get; set; }

        /// <summary>The armour training needed ("light", "medium", "heavy", "shields").</summary>
        public string Training => Category == ArmorCategory.Shield ? "shields" : Category.ToString().ToLowerInvariant();
    }

    /// <summary>Where an item's combat numbers come from.</summary>
    public static class ItemCombat
    {
        public const string Srd = "SRD 5.2.1 (CC BY 4.0)";

        /// <summary>The unarmed strike (SRD 5.2.1): 1 + Strength bludgeoning; proficiency applies.</summary>
        public static readonly WeaponProfile Unarmed = new WeaponProfile
        {
            Kind = "unarmed", Simple = true, Damage = new Dice(0, 0, 1), DamageType = DamageType.Bludgeoning,
        };

        private static WeaponProfile W(string kind, bool simple, int count, int sides, DamageType type, WeaponProperty p, string mastery,
            int range = 0, int longRange = 0, int versatileSides = 0, bool ranged = false) => new WeaponProfile
        {
            Kind = kind, Simple = simple, Ranged = ranged, Damage = new Dice(count, sides), DamageType = type, Properties = p, Mastery = mastery,
            Range = range, LongRange = longRange, Versatile = versatileSides > 0 ? new Dice(1, versatileSides) : default,
        };

        /// <summary>Weapon data by item ID. The Realmkeeper item each maps to is named on the right.</summary>
        internal static readonly IReadOnlyDictionary<string, WeaponProfile> Weapons = new Dictionary<string, WeaponProfile>(StringComparer.Ordinal)
        {
            ["dagger_iron"] = W("dagger", true, 1, 4, DamageType.Piercing, WeaponProperty.Finesse | WeaponProperty.Light | WeaponProperty.Thrown, "Nick", 20, 60),
            ["sword_arming"] = W("longsword", false, 1, 8, DamageType.Slashing, WeaponProperty.Versatile, "Sap", versatileSides: 10), // an arming sword is a longsword
            ["axe_hand"] = W("handaxe", true, 1, 6, DamageType.Slashing, WeaponProperty.Light | WeaponProperty.Thrown, "Vex", 20, 60),
            ["mace_flanged"] = W("mace", true, 1, 6, DamageType.Bludgeoning, WeaponProperty.None, "Sap"),
            ["spear_ash"] = W("spear", true, 1, 6, DamageType.Piercing, WeaponProperty.Thrown | WeaponProperty.Versatile, "Sap", 20, 60, 8),
            ["staff_quarter"] = W("quarterstaff", true, 1, 6, DamageType.Bludgeoning, WeaponProperty.Versatile, "Topple", versatileSides: 8),
            ["bow_hunting"] = W("shortbow", true, 1, 6, DamageType.Piercing, WeaponProperty.Ammunition | WeaponProperty.TwoHanded, "Vex", 80, 320, ranged: true), // a hunting bow is a shortbow
            ["greataxe"] = W("greataxe", false, 1, 12, DamageType.Slashing, WeaponProperty.Heavy | WeaponProperty.TwoHanded, "Cleave"),
            ["rapier"] = W("rapier", false, 1, 8, DamageType.Piercing, WeaponProperty.Finesse, "Vex"),
            ["shortsword"] = W("shortsword", false, 1, 6, DamageType.Piercing, WeaponProperty.Finesse | WeaponProperty.Light, "Vex"),
            ["scimitar"] = W("scimitar", false, 1, 6, DamageType.Slashing, WeaponProperty.Finesse | WeaponProperty.Light, "Nick"),
            ["light_crossbow"] = W("light crossbow", true, 1, 8, DamageType.Piercing, WeaponProperty.Ammunition | WeaponProperty.Loading | WeaponProperty.TwoHanded, "Slow", 80, 320, ranged: true),
            ["javelin"] = W("javelin", true, 1, 6, DamageType.Piercing, WeaponProperty.Thrown, "Slow", 30, 120),
        };

        /// <summary>Armour data by item ID (only one suit of armour and one shield count; other pieces give no AC).</summary>
        internal static readonly IReadOnlyDictionary<string, ArmorProfile> Armor = new Dictionary<string, ArmorProfile>(StringComparer.Ordinal)
        {
            ["jerkin_leather"] = new ArmorProfile { Category = ArmorCategory.Light, BaseAc = 11 },            // leather armour
            ["jerkin_forester"] = new ArmorProfile { Category = ArmorCategory.Light, BaseAc = 12 },           // studded leather armour
            ["chain_shirt"] = new ArmorProfile { Category = ArmorCategory.Medium, BaseAc = 13, DexCap = 2 },
            ["chain_mail"] = new ArmorProfile { Category = ArmorCategory.Heavy, BaseAc = 16, DexCap = 0, StrengthRequirement = 13, StealthDisadvantage = true },
            ["shield_round"] = new ArmorProfile { Category = ArmorCategory.Shield, BaseAc = 2 },
            ["shield_buckler"] = new ArmorProfile { Category = ArmorCategory.Shield, BaseAc = 2 },            // the SRD has one shield: +2
        };
    }
}
