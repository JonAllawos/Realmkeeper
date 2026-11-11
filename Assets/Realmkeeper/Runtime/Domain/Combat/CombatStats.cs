using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    public enum AttackKind
    {
        Weapon = 0,
        Unarmed = 1,

        /// <summary>Unarmed Strike option: the target saves (Strength or Dexterity) or is Grappled.</summary>
        Grapple = 2,

        /// <summary>Unarmed Strike option: the target saves or is knocked Prone.</summary>
        Shove = 3,
    }

    /// <summary>One way a combatant can attack, with everything needed to resolve and explain it.</summary>
    public sealed class AttackOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public AttackKind Kind { get; set; }
        public bool Melee { get; set; }

        /// <summary>Melee reach in feet.</summary>
        public int Reach { get; set; }

        /// <summary>Normal and long range in feet (ranged and thrown attacks).</summary>
        public int Range { get; set; }
        public int LongRange { get; set; }

        public Breakdown AttackBonus { get; set; }
        public Dice Damage { get; set; }
        public Breakdown DamageBonus { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>Damage dice below this count as this (Great Weapon Fighting); 0 = none.</summary>
        public int DieFloor { get; set; }

        /// <summary>The ability the roll uses (for "attack:{ability}" roll modes).</summary>
        public Ability Ability { get; set; }

        public bool Proficient { get; set; }
        public string ItemId { get; set; }

        /// <summary>For Grapple and Shove: the save DC (8 + Strength + proficiency).</summary>
        public int SaveDc { get; set; }

        public ConditionType? OnHit { get; set; }

        public bool Light { get; set; }
    }

    /// <summary>
    /// The combat view of one combatant: armour class, ability modifiers, saves, initiative, speed, attacks,
    /// defences and roll modes. For the player it is read from <see cref="CharacterRules"/> with their worn
    /// equipment (so the sheet and the fight always agree); for residents and creatures from their creature
    /// profile. Nothing else in combat or in the UI calculates these numbers.
    /// </summary>
    public sealed class CombatStats
    {
        private readonly List<AttackOption> _attacks = new List<AttackOption>();

        private CombatStats(CombatantData c)
        {
            Combatant = c;
        }

        public CombatantData Combatant { get; }
        public CharacterRules Rules { get; private set; }
        public PlayerCharacterData Player { get; private set; }
        public CreatureDefinition Creature { get; private set; }
        public Breakdown ArmorClass { get; private set; }
        public int BaseSpeed { get; private set; }
        public int ProficiencyBonus { get; private set; }
        public IReadOnlyList<AttackOption> Attacks => _attacks;

        /// <summary>Attacks one Attack action allows (Extra Attack and its improvements).</summary>
        public int AttacksPerAction { get; private set; } = 1;

        public bool PackTactics => Creature?.PackTactics ?? false;
        public string WeaponName { get; private set; }

        public static CombatStats For(WorldData world, CombatantData c)
        {
            var s = new CombatStats(c);
            if (c.Kind == CombatantKind.Player)
            {
                var p = world.Player;
                s.Player = p;
                s.Rules = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
                s.ArmorClass = s.Rules.ArmorClass(p);
                s.BaseSpeed = s.Rules.Speed;
                s.ProficiencyBonus = s.Rules.ProficiencyBonus;
                s.AttacksPerAction = s.Rules.HasFeature("three_extra_attacks") ? 4 : s.Rules.HasFeature("two_extra_attacks") ? 3 : s.Rules.HasFeature("extra_attack") ? 2 : 1;
                s.BuildPlayerAttacks();
            }
            else
            {
                var def = CreatureCatalog.Find(c.CreatureId) ?? CreatureCatalog.Find("commoner");
                s.Creature = def;
                s.ArmorClass = new Breakdown().Add(def.Name + " (" + def.ArmorNote + ")", def.ArmorClass);
                s.BaseSpeed = def.Speed;
                s.ProficiencyBonus = def.ProficiencyBonus;
                foreach (var a in def.Attacks) s.BuildCreatureAttacks(a);
                s.WeaponName = def.Attacks.Count > 0 ? def.Attacks[0].Name : "Unarmed";
            }

            return s;
        }

        // ----- Abilities and defences -----------------------------------------------------------------------

        public int Score(Ability a) => Rules != null ? Rules.Score(a) : Creature.Scores[(int)a];
        public int Modifier(Ability a) => AbilityRules.Modifier(Score(a));

        /// <summary>A saving throw's bonus and how it is made up.</summary>
        public Breakdown SaveBonus(Ability a)
        {
            if (Rules != null) return Rules.SaveBonus(a);
            var b = new Breakdown().Add(a.ToString(), Modifier(a));
            foreach (var p in Creature.SaveProficiencies) if (p == a) b.Add("proficiency", ProficiencyBonus);
            return b;
        }

        /// <summary>Advantage and disadvantage from features and armour on a roll target (the player); none for creatures.</summary>
        public RollMode RollModes(params string[] targets) => Rules != null ? Rules.RollFor(targets) : new RollMode();

        public Breakdown InitiativeBonus() => Rules != null ? Rules.Initiative() : new Breakdown().Add("Dexterity", Modifier(Ability.Dexterity));

        public bool Resists(DamageType t) => Rules != null ? Contains(Rules.Resistances, t) : Contains(Creature.Resistances, t);
        public bool Vulnerable(DamageType t) => Rules == null && Contains(Creature.Vulnerabilities, t);
        public bool Immune(DamageType t) => Rules == null && Contains(Creature.Immunities, t);
        public bool ImmuneTo(ConditionType t) => Creature != null && Contains(Creature.ConditionImmunities, t);

        public int MaxHp => Rules != null ? Rules.MaxHp : Combatant.MaxHp;

        /// <summary>The longest melee reach it has.</summary>
        public int MeleeReach
        {
            get
            {
                var r = 0;
                foreach (var a in _attacks) if (a.Melee && (a.Kind == AttackKind.Weapon || a.Kind == AttackKind.Unarmed)) r = Math.Max(r, a.Reach);
                return Math.Max(5, r);
            }
        }

        public AttackOption Find(string id)
        {
            foreach (var a in _attacks) if (a.Id == id) return a;
            return null;
        }

        /// <summary>The best melee attack (opportunity attacks and AI): the highest average damage.</summary>
        public AttackOption BestMelee()
        {
            AttackOption best = null;
            foreach (var a in _attacks)
                if (a.Melee && (a.Kind == AttackKind.Weapon || a.Kind == AttackKind.Unarmed) && (best == null || Average(a) > Average(best))) best = a;
            return best;
        }

        public AttackOption BestRanged()
        {
            AttackOption best = null;
            foreach (var a in _attacks)
                if (!a.Melee && a.Kind == AttackKind.Weapon && (best == null || Average(a) > Average(best))) best = a;
            return best;
        }

        public static double Average(AttackOption a) => a.Damage.Count * (a.Damage.Sides + 1) / 2.0 + a.Damage.Bonus + (a.DamageBonus?.Total ?? 0);

        private static bool Contains(IReadOnlyList<string> list, DamageType t)
        {
            var key = t.ToString().ToLowerInvariant();
            foreach (var x in list) if (string.Equals(x, key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool Contains<T>(IReadOnlyList<T> list, T x)
        {
            foreach (var y in list) if (EqualityComparer<T>.Default.Equals(x, y)) return true;
            return false;
        }

        // ----- Attacks ---------------------------------------------------------------------------------------

        /// <summary>Whether the character is proficient with a weapon (class lists: "simple", "martial", a kind, or the rogue's finesse-or-light).</summary>
        public static bool Proficient(CharacterRules r, WeaponProfile w)
        {
            if (w.Kind == "unarmed") return true;
            foreach (var p in r.WeaponProficiencies)
            {
                if (p == (w.Simple ? "simple" : "martial") || p == w.Kind) return true;
                if (!w.Simple && p == "martial (finesse or light)" && (w.Has(WeaponProperty.Finesse) || w.Has(WeaponProperty.Light))) return true;
            }

            return false;
        }

        private int CombatRuleValue(string key)
        {
            var v = 0;
            foreach (var se in Rules.Effects)
                if (se.Effect.Kind == EffectKind.CombatRule && se.Effect.Key == key) v = Math.Max(v, se.Effect.Value);
            return v;
        }

        private void BuildPlayerAttacks()
        {
            var p = Player;
            var main = ItemCatalog.Find(p.Equipped(EquipmentSlot.MainHand)?.ItemId);
            var off = ItemCatalog.Find(p.Equipped(EquipmentSlot.OffHand)?.ItemId);
            var str = Modifier(Ability.Strength);
            var pb = ProficiencyBonus;
            if (main?.Weapon != null)
            {
                var w = main.Weapon;
                WeaponName = main.Name;
                var ability = w.Ranged ? Ability.Dexterity : w.Has(WeaponProperty.Finesse) && Modifier(Ability.Dexterity) > str ? Ability.Dexterity : Ability.Strength;
                var proficient = Proficient(Rules, w);
                var twoHanded = w.Has(WeaponProperty.TwoHanded) || (main.TwoHanded && w.Has(WeaponProperty.Versatile)) || (w.Has(WeaponProperty.Versatile) && off == null);
                var dice = w.Has(WeaponProperty.Versatile) && twoHanded && !w.Versatile.IsEmpty ? w.Versatile : w.Damage;

                AttackOption Make(string id, string name, bool melee, int range, int longRange)
                {
                    var bonus = new Breakdown().Add(ability.ToString(), Modifier(ability));
                    if (proficient) bonus.Add("proficiency", pb);
                    else bonus.Note($"not proficient with {w.Kind}s: no proficiency bonus");
                    var archery = CombatRuleValue("ranged-attack-bonus");
                    if (!melee && archery > 0) bonus.Add("Archery", archery);
                    var damage = new Breakdown().Add(ability.ToString(), Modifier(ability));
                    var dueling = CombatRuleValue("dueling-damage");
                    if (melee && dueling > 0 && !twoHanded && (off == null || off.Weapon == null)) damage.Add("Duelling", dueling);
                    return new AttackOption
                    {
                        Id = id, Name = name, Kind = AttackKind.Weapon, Melee = melee, Reach = melee ? w.Reach : 0, Range = range, LongRange = longRange,
                        AttackBonus = bonus, Damage = melee ? dice : w.Damage, DamageBonus = damage, DamageType = w.DamageType,
                        DieFloor = melee && twoHanded ? CombatRuleValue("great-weapon-floor") : 0, Ability = ability, Proficient = proficient, ItemId = main.Id,
                        Light = w.Has(WeaponProperty.Light),
                    };
                }

                if (w.Ranged) _attacks.Add(Make("weapon", main.Name, false, w.Range, w.LongRange));
                else
                {
                    _attacks.Add(Make("weapon", main.Name + (twoHanded && !w.Versatile.IsEmpty ? " (two-handed)" : string.Empty), true, 0, 0));
                    if (w.Has(WeaponProperty.Thrown)) _attacks.Add(Make("thrown", main.Name + " (thrown)", false, w.Range, w.LongRange));
                }
            }
            else WeaponName = "Unarmed";

            var unarmed = new Breakdown().Add("Strength", str).Add("proficiency", pb);
            _attacks.Add(new AttackOption
            {
                Id = "unarmed", Name = "Unarmed strike", Kind = AttackKind.Unarmed, Melee = true, Reach = 5, AttackBonus = unarmed, Damage = new Dice(0, 0, 1),
                DamageBonus = new Breakdown().Add("Strength", str), DamageType = DamageType.Bludgeoning, Ability = Ability.Strength, Proficient = true,
            });
            var dc = 8 + str + pb;
            var freeHand = main == null || off == null && !(main.TwoHanded && main.Weapon != null && main.Weapon.Has(WeaponProperty.TwoHanded));
            if (freeHand)
                _attacks.Add(new AttackOption { Id = "grapple", Name = "Grapple", Kind = AttackKind.Grapple, Melee = true, Reach = 5, SaveDc = dc, Ability = Ability.Strength, AttackBonus = new Breakdown().Add("DC 8", 8).Add("Strength", str).Add("proficiency", pb) });
            _attacks.Add(new AttackOption { Id = "shove", Name = "Shove (prone)", Kind = AttackKind.Shove, Melee = true, Reach = 5, SaveDc = dc, Ability = Ability.Strength, AttackBonus = new Breakdown().Add("DC 8", 8).Add("Strength", str).Add("proficiency", pb) });
        }

        private void BuildCreatureAttacks(CreatureAttack a)
        {
            var bonus = new Breakdown().Add(a.Name, a.AttackBonus);
            var damage = new Breakdown();
            if (a.Reach > 0)
                _attacks.Add(new AttackOption
                {
                    Id = a.Id, Name = a.Name, Kind = AttackKind.Weapon, Melee = true, Reach = a.Reach, AttackBonus = bonus, Damage = a.Damage, DamageBonus = damage,
                    DamageType = a.DamageType, Ability = Ability.Strength, Proficient = true, OnHit = a.OnHit,
                });
            if (a.Range > 0)
                _attacks.Add(new AttackOption
                {
                    Id = a.Reach > 0 ? a.Id + ":thrown" : a.Id, Name = a.Reach > 0 ? a.Name + " (thrown)" : a.Name, Kind = AttackKind.Weapon, Melee = false,
                    Range = a.Range, LongRange = a.LongRange, AttackBonus = bonus, Damage = a.Damage, DamageBonus = damage, DamageType = a.DamageType,
                    Ability = Ability.Dexterity, Proficient = true,
                });
        }
    }
}
