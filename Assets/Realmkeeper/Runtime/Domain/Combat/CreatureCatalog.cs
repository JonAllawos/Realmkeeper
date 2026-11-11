using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>A creature's natural or carried attack (from its stat block).</summary>
    public sealed class CreatureAttack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int AttackBonus { get; set; }
        public Dice Damage { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>Melee reach in feet (0 for a ranged-only attack).</summary>
        public int Reach { get; set; }

        /// <summary>Range and long range in feet (0 for melee-only).</summary>
        public int Range { get; set; }
        public int LongRange { get; set; }

        /// <summary>A condition a hit inflicts (the wolf's bite knocks a Medium or smaller target prone).</summary>
        public ConditionType? OnHit { get; set; }

        // ----- Phase 10 -----

        /// <summary>The save that avoids <see cref="OnHit"/> (null: no save; the condition always applies).</summary>
        public Ability? OnHitSave { get; set; }
        public int OnHitDc { get; set; }

        /// <summary>Escape DC for a restraining hit (a web): the target can break free with a Strength check.</summary>
        public int EscapeDc { get; set; }

        /// <summary>Damage added on a hit that the target may save against (a venomous bite): dice, type, save, half on a success.</summary>
        public Dice SaveDamage { get; set; }
        public DamageType SaveDamageType { get; set; }
        public Ability SaveDamageAbility { get; set; } = Ability.Constitution;
        public int SaveDamageDc { get; set; }

        /// <summary>Recharge on this d6 roll or higher at the start of its turn (0: always available).</summary>
        public int Recharge { get; set; }
    }

    /// <summary>
    /// The combat statistics of a creature that has no character sheet (SRD 5.2.1 stat blocks, verified for Phase 8;
    /// descriptions are Realmkeeper's own). Named residents fight with the profile their role suggests: guards as
    /// Guards, everyone else as Commoners. Their identity stays the resident's.
    /// </summary>
    public sealed class CreatureDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public CreatureSize Size { get; set; } = CreatureSize.Medium;
        public string Type { get; set; } = "Humanoid";
        public int ArmorClass { get; set; }
        public string ArmorNote { get; set; }
        public Dice HitDice { get; set; }
        public int MaxHp { get; set; }
        public int Speed { get; set; } = 30;

        /// <summary>Ability scores in <see cref="Ability"/> order.</summary>
        public int[] Scores { get; set; } = { 10, 10, 10, 10, 10, 10 };

        public IReadOnlyList<Ability> SaveProficiencies { get; set; } = Array.Empty<Ability>();
        public int ProficiencyBonus { get; set; } = 2;
        public IReadOnlyList<CreatureAttack> Attacks { get; set; } = Array.Empty<CreatureAttack>();

        /// <summary>Advantage on attacks against a creature that one of its allies stands next to (wolves).</summary>
        public bool PackTactics { get; set; }

        public int Darkvision { get; set; }
        public IReadOnlyList<DamageType> Resistances { get; set; } = Array.Empty<DamageType>();
        public IReadOnlyList<DamageType> Vulnerabilities { get; set; } = Array.Empty<DamageType>();
        public IReadOnlyList<DamageType> Immunities { get; set; } = Array.Empty<DamageType>();
        public IReadOnlyList<ConditionType> ConditionImmunities { get; set; } = Array.Empty<ConditionType>();
        /// <summary>Attacks one Attack action makes (Multiattack).</summary>
        public int Multiattack { get; set; } = 1;

        /// <summary>Phase 9: spellcasting from the stat block (null for non-casters).</summary>
        public CreatureSpellcasting Spellcasting { get; set; }

        public ContentSource Source { get; set; } = ContentSource.Srd521;

        // ----- Phase 10: the bestiary (ADR-0044) -----

        public CreatureCategory Category { get; set; } = CreatureCategory.Humanoid;

        /// <summary>Challenge rating as written ("1/4", "2").</summary>
        public string ChallengeRating { get; set; } = "0";

        public int Xp => ChallengeRatings.Xp(ChallengeRating);

        public int ClimbSpeed { get; set; }
        public int SwimSpeed { get; set; }
        public string Senses { get; set; } = string.Empty;
        public string Languages { get; set; } = string.Empty;
        public string Skills { get; set; } = string.Empty;
        public IReadOnlyList<CreatureTrait> Traits { get; set; } = Array.Empty<CreatureTrait>();
        public IReadOnlyList<CreatureReaction> Reactions { get; set; } = Array.Empty<CreatureReaction>();
        public BehaviorProfile Behavior { get; set; } = new BehaviorProfile { Kind = CreatureBehavior.Intelligent, Summary = "Closes in and picks off the weakest; falls back when badly hurt." };
        public IReadOnlyList<Habitat> Habitats { get; set; } = Array.Empty<Habitat>();
        public Diet Diet { get; set; } = Diet.Omnivore;
        public SocialForm Social { get; set; } = SocialForm.Solitary;
        public string Temperament { get; set; } = string.Empty;
        public LootProfile Loot { get; set; } = LootProfile.None;

        /// <summary>Where the statistics come from, in words (stat block and licence, or Realmkeeper's own).</summary>
        public string Provenance { get; set; }

        public CreatureTrait Trait(CreatureTraitKind kind)
        {
            foreach (var t in Traits) if (t.Kind == kind) return t;
            return null;
        }

        public CreatureReaction Reaction(CreatureReactionKind kind)
        {
            foreach (var r in Reactions) if (r.Kind == kind) return r;
            return null;
        }

        public int Modifier(Ability a) => AbilityRules.Modifier(Scores[(int)a]);
    }

    public static partial class CreatureCatalog
    {
        private static readonly Lazy<IReadOnlyList<CreatureDefinition>> List = new Lazy<IReadOnlyList<CreatureDefinition>>(Build);

        public static IReadOnlyList<CreatureDefinition> All => List.Value;

        public static CreatureDefinition Find(string id)
        {
            if (id == null) return null;
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        /// <summary>The profile a resident of this role fights with.</summary>
        public static string ProfileFor(NpcRole role) => role == NpcRole.Guard ? "guard" : "commoner";

        private static IReadOnlyList<CreatureDefinition> Build()
        {
            var list = new List<CreatureDefinition>(Core());
            Annotate(list); // Phase 10: bestiary data for the Phase 8 and 9 creatures
            list.AddRange(Bestiary()); // Phase 10: the bestiary
            return list;
        }

        private static IReadOnlyList<CreatureDefinition> Core() => new[]
        {
            new CreatureDefinition
            {
                Id = "commoner", Name = "Commoner", Description = "An ordinary person with no training in arms.",
                ArmorClass = 10, ArmorNote = "no armour", HitDice = new Dice(1, 8), MaxHp = 4, Scores = new[] { 10, 10, 10, 10, 10, 10 },
                Attacks = new[] { new CreatureAttack { Id = "club", Name = "Club", AttackBonus = 2, Damage = new Dice(1, 4), DamageType = DamageType.Bludgeoning, Reach = 5 } },
            },
            new CreatureDefinition
            {
                Id = "bandit", Name = "Bandit", Description = "A robber who works the roads in a loose band.",
                ArmorClass = 12, ArmorNote = "leather armour", HitDice = new Dice(2, 8, 2), MaxHp = 11, Scores = new[] { 11, 12, 12, 10, 10, 10 },
                Attacks = new[]
                {
                    new CreatureAttack { Id = "scimitar", Name = "Scimitar", AttackBonus = 3, Damage = new Dice(1, 6, 1), DamageType = DamageType.Slashing, Reach = 5 },
                    new CreatureAttack { Id = "light_crossbow", Name = "Light crossbow", AttackBonus = 3, Damage = new Dice(1, 8, 1), DamageType = DamageType.Piercing, Range = 80, LongRange = 320 },
                },
            },
            new CreatureDefinition
            {
                Id = "guard", Name = "Guard", Description = "A watch keeper in a chain shirt, with spear and shield.",
                ArmorClass = 16, ArmorNote = "chain shirt, shield", HitDice = new Dice(2, 8, 2), MaxHp = 11, Scores = new[] { 13, 12, 12, 10, 11, 10 },
                Attacks = new[] { new CreatureAttack { Id = "spear", Name = "Spear", AttackBonus = 3, Damage = new Dice(1, 6, 1), DamageType = DamageType.Piercing, Reach = 5, Range = 20, LongRange = 60 } },
            },
            new CreatureDefinition
            {
                Id = "wolf", Name = "Wolf", Description = "A lean grey hunter that runs in packs.", Type = "Beast",
                ArmorClass = 12, ArmorNote = "natural", HitDice = new Dice(2, 8, 2), MaxHp = 11, Speed = 40, Scores = new[] { 14, 15, 12, 3, 12, 6 },
                PackTactics = true, Darkvision = 60,
                Attacks = new[] { new CreatureAttack { Id = "bite", Name = "Bite", AttackBonus = 4, Damage = new Dice(1, 6, 2), DamageType = DamageType.Piercing, Reach = 5, OnHit = ConditionType.Prone } },
            },

            // ----- Phase 9: casters (SRD 5.1 stat blocks; spells not in the catalog are left out of their lists) -----
            new CreatureDefinition
            {
                Id = "acolyte", Name = "Acolyte", Description = "A temple servant who tends the sick and keeps the rites.", Source = ContentSource.Srd51,
                ArmorClass = 10, ArmorNote = "no armour", HitDice = new Dice(2, 8), MaxHp = 9, Scores = new[] { 10, 10, 10, 10, 14, 11 },
                Attacks = new[] { new CreatureAttack { Id = "club", Name = "Club", AttackBonus = 2, Damage = new Dice(1, 4), DamageType = DamageType.Bludgeoning, Reach = 5 } },
                Spellcasting = new CreatureSpellcasting
                {
                    Ability = Ability.Wisdom, SaveDc = 12, AttackBonus = 4, CasterLevel = 1, Cantrips = new[] { "light", "sacred_flame" },
                    Spells = new[] { "bless", "cure_wounds" }, Slots = new[] { 3 },
                    Omitted = "Thaumaturgy and Sanctuary (not in the catalog)",
                },
            },
            new CreatureDefinition
            {
                Id = "cult_fanatic", Name = "Cult Fanatic", Description = "A zealot whose devotion is backed by real power.", Source = ContentSource.Srd51,
                ArmorClass = 13, ArmorNote = "leather armour", HitDice = new Dice(6, 8, 6), MaxHp = 33, Scores = new[] { 11, 14, 12, 10, 13, 14 }, Multiattack = 2, ProficiencyBonus = 2,
                Attacks = new[] { new CreatureAttack { Id = "dagger", Name = "Dagger", AttackBonus = 4, Damage = new Dice(1, 4, 2), DamageType = DamageType.Piercing, Reach = 5, Range = 20, LongRange = 60 } },
                Spellcasting = new CreatureSpellcasting
                {
                    Ability = Ability.Wisdom, SaveDc = 11, AttackBonus = 3, CasterLevel = 4, Cantrips = new[] { "light", "sacred_flame" },
                    Spells = new[] { "inflict_wounds", "shield_of_faith", "hold_person" }, Slots = new[] { 4, 3 },
                    Omitted = "Thaumaturgy, Command and Spiritual Weapon (not in the catalog or not castable); Dark Devotion",
                },
            },
        };
    }
}

namespace Realmkeeper.Domain.Combat
{
    using System;
    using System.Collections.Generic;
    using Realmkeeper.Domain.Rpg;

    /// <summary>A creature's spellcasting (Phase 9): the same spells and engine the player uses, with the stat block's numbers.</summary>
    public sealed class CreatureSpellcasting
    {
        public Ability Ability { get; set; }
        public int SaveDc { get; set; }
        public int AttackBonus { get; set; }

        /// <summary>Caster level for cantrip scaling.</summary>
        public int CasterLevel { get; set; } = 1;

        public IReadOnlyList<string> Cantrips { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Spells { get; set; } = Array.Empty<string>();

        /// <summary>Slots per spell level (index 0 = level 1).</summary>
        public IReadOnlyList<int> Slots { get; set; } = Array.Empty<int>();

        /// <summary>What of the stat block's spellcasting is not modelled.</summary>
        public string Omitted { get; set; }
    }
}
