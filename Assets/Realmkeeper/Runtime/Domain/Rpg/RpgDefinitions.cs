using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>A named rules feature (species trait, background feature, class feature) made of typed effects.</summary>
    public sealed class FeatureDefinition
    {
        public FeatureDefinition(string id, string name, string description, params RuleEffect[] effects)
        {
            Id = id;
            Name = name;
            Description = description;
            Effects = effects ?? Array.Empty<RuleEffect>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<RuleEffect> Effects { get; }

        // ----- Phase 7 -----

        public FeatureKind Kind { get; set; } = FeatureKind.Passive;

        /// <summary>Systems this feature waits for (shown on the sheet; nothing is faked meanwhile).</summary>
        public IntegrationNeed Needs { get; set; }

        /// <summary>
        /// Phase 22: for a species trait, the character level it arrives at (1: from creation). Class features use the
        /// level they are listed under instead. A trait above the character's level is locked: shown, never applied.
        /// </summary>
        public int MinLevel { get; set; } = 1;

        /// <summary>The feature this one supersedes (e.g. Two Extra Attacks replaces Extra Attack); the replaced one stops applying.</summary>
        public string Replaces { get; set; }

        /// <summary>A named scaling column this feature reads (e.g. "sneak_attack"), shown with its current value.</summary>
        public string ScalingId { get; set; }
    }

    public enum ChoiceKind
    {
        /// <summary>Pick from fixed options, each with its own effects.</summary>
        Option = 0,

        /// <summary>Pick skills to be proficient in, from <see cref="ChoiceDefinition.Skills"/> (empty = any).</summary>
        Skill = 1,

        /// <summary>Pick skills you are already proficient in to gain expertise.</summary>
        Expertise = 2,

        // ----- Phase 7 -----

        /// <summary>Pick a feat from <see cref="ChoiceDefinition.FeatCategories"/>; the feat's own choices follow under "{id}:{local}".</summary>
        Feat = 3,

        /// <summary>Pick languages you do not know yet.</summary>
        Language = 4,

        /// <summary>Pick abilities to raise by 1 each (<see cref="ChoiceDefinition.AllowRepeat"/> lets one ability take several).</summary>
        AbilityIncrease = 5,

        /// <summary>Pick the class's subclass (recorded in <c>CharacterSheetData.SubclassId</c>, not in the choices map).</summary>
        Subclass = 6,
    }

    /// <summary>What must be true before a feat, invocation or option can be taken (Phase 7).</summary>
    public enum PrerequisiteKind
    {
        /// <summary>Character level at least Value.</summary>
        MinLevel = 0,

        /// <summary>Any of <see cref="Prerequisite.Abilities"/> at Value or more.</summary>
        AbilityAtLeast = 1,

        /// <summary>A feature with ID Key is active.</summary>
        HasFeature = 2,

        /// <summary>Option Key is already taken in the same pool (e.g. Pact of the Blade before Thirsting Blade).</summary>
        PoolPick = 3,

        /// <summary>The character can cast spells.</summary>
        Spellcasting = 4,

        /// <summary>The character has a Fighting Style feature.</summary>
        FightingStyleFeature = 5,

        /// <summary>The character does not already have fighting style Key.</summary>
        NotFightingStyle = 6,
    }

    public sealed class Prerequisite
    {
        public PrerequisiteKind Kind { get; set; }
        public string Key { get; set; }
        public int Value { get; set; }
        public IReadOnlyList<Ability> Abilities { get; set; } = Array.Empty<Ability>();

        public static Prerequisite Level(int level) => new Prerequisite { Kind = PrerequisiteKind.MinLevel, Value = level };
        public static Prerequisite Ability(int score, params Ability[] any) => new Prerequisite { Kind = PrerequisiteKind.AbilityAtLeast, Value = score, Abilities = any };
        public static Prerequisite Feature(string id) => new Prerequisite { Kind = PrerequisiteKind.HasFeature, Key = id };
        public static Prerequisite Pick(string optionId) => new Prerequisite { Kind = PrerequisiteKind.PoolPick, Key = optionId };
        public static Prerequisite Caster() => new Prerequisite { Kind = PrerequisiteKind.Spellcasting };
        public static Prerequisite StyleFeature() => new Prerequisite { Kind = PrerequisiteKind.FightingStyleFeature };
        public static Prerequisite NewStyle(string style) => new Prerequisite { Kind = PrerequisiteKind.NotFightingStyle, Key = style };
    }

    public sealed class ChoiceOption
    {
        public ChoiceOption(string id, string name, string description, params RuleEffect[] effects)
        {
            Id = id;
            Name = name;
            Description = description;
            Effects = effects ?? Array.Empty<RuleEffect>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<RuleEffect> Effects { get; }

        /// <summary>Phase 7: what must be true to take this option.</summary>
        public IReadOnlyList<Prerequisite> Prerequisites { get; set; } = Array.Empty<Prerequisite>();

        /// <summary>Phase 7: systems the option waits for.</summary>
        public IntegrationNeed Needs { get; set; }
    }

    /// <summary>A required choice: at creation (skills, a fighting style, an ancestry) or, from Phase 7, at a later level.</summary>
    public sealed class ChoiceDefinition
    {
        /// <summary>The key the choice is stored under (level choices are "lv{level}:{name}").</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public string Prompt { get; set; }
        public ChoiceKind Kind { get; set; }
        public int Count { get; set; } = 1;

        /// <summary>Phase 9: may be made with fewer picks than <see cref="Count"/> (spell picks: the catalog may hold fewer spells than the class table allows; open picks can be made later).</summary>
        public bool UpTo { get; set; }

        /// <summary>Whether this many picks complete the choice.</summary>
        public bool CountAllowed(int picked) => UpTo ? picked <= Count : picked == Count;
        public IReadOnlyList<ChoiceOption> Options { get; set; } = Array.Empty<ChoiceOption>();
        public IReadOnlyList<Skill> Skills { get; set; } = Array.Empty<Skill>();

        // ----- Phase 7 -----

        /// <summary>The character level the choice is made at.</summary>
        public int Level { get; set; } = 1;

        /// <summary>Choices sharing a pool never repeat a value across levels (invocations, metamagic).</summary>
        public string Pool { get; set; }

        /// <summary>The rules version that introduced the choice. Older sheets may lack it until their next advancement.</summary>
        public int IntroducedInRules { get; set; } = 6;

        /// <summary>For <see cref="ChoiceKind.Feat"/>: the feat categories offered.</summary>
        public FeatCategory FeatCategories { get; set; }

        /// <summary>For <see cref="ChoiceKind.AbilityIncrease"/>: the abilities allowed (empty = any).</summary>
        public IReadOnlyList<Ability> Abilities { get; set; } = Array.Empty<Ability>();

        /// <summary>For <see cref="ChoiceKind.AbilityIncrease"/>: one ability may take several points.</summary>
        public bool AllowRepeat { get; set; }

        /// <summary>For <see cref="ChoiceKind.AbilityIncrease"/>: the score an increase may not pass.</summary>
        public int Limit { get; set; } = AbilityRules.MaxScore;
    }

    /// <summary>A pool a class or species spends (rage, spell slots, a healing pool).</summary>
    public sealed class ResourceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ResourceFormula Formula { get; set; }
        public int Value { get; set; }
        public Ability Ability { get; set; }
        public ResourceRecovery Recovery { get; set; }

        // ----- Phase 7 -----

        /// <summary>For <see cref="ResourceFormula.Table"/>: the maximum at each class level 1–20.</summary>
        public IReadOnlyList<int> Table { get; set; }

        /// <summary>The first level the resource exists at (a table starts where its first value is non-zero).</summary>
        public int FromLevel { get; set; } = 1;

        /// <summary>From this level the resource also returns on a short rest (0 = never; Font of Inspiration).</summary>
        public int ShortRestFromLevel { get; set; }

        /// <summary>Phase 9: uses regained on a short rest by a resource that otherwise returns on a long rest (Rage, Channel Divinity, Wild Shape: one).</summary>
        public int ShortRestRegain { get; set; }

        /// <summary>How much a recovery restores.</summary>
        public RecoveryAmount Amount { get; set; }

        /// <summary>Where the resource comes from (for the sheet), set by the catalog.</summary>
        public string Origin { get; set; }

        public int FirstLevel
        {
            get
            {
                if (Formula != ResourceFormula.Table || Table == null) return FromLevel;
                for (var i = 0; i < Table.Count; i++) if (Table[i] > 0) return Math.Max(FromLevel, i + 1);
                return int.MaxValue;
            }
        }

        public ResourceRecovery RecoveryAt(int level) => ShortRestFromLevel > 0 && level >= ShortRestFromLevel ? ResourceRecovery.ShortRest : Recovery;
    }

    /// <summary>How a caster's slots grow (SRD 5.2.1 class tables).</summary>
    public enum CasterProgression
    {
        None = 0,

        /// <summary>Bard, cleric, druid, sorcerer, wizard: slots of levels 1–9.</summary>
        Full = 1,

        /// <summary>Paladin, ranger: the full table at half level, rounded up (slots of levels 1–5).</summary>
        Half = 2,

        /// <summary>Warlock: a few slots, all of one level, back on a short rest.</summary>
        Pact = 3,
    }

    /// <summary>What a class can do with magic (spells themselves belong to the magic phase).</summary>
    public sealed class SpellcastingDefinition
    {
        public Ability Ability { get; set; }
        public string Style { get; set; }

        /// <summary>Cantrips at level 1 (kept from Phase 6; <see cref="CantripsAt"/> for any level).</summary>
        public int CantripsKnown { get; set; }

        /// <summary>Spell slots at level 1 (kept from Phase 6).</summary>
        public int FirstLevelSlots { get; set; }

        public string Focus { get; set; }
        public ResourceRecovery SlotRecovery { get; set; }

        // ----- Phase 7 -----

        public CasterProgression Progression { get; set; }

        /// <summary>Cantrips known at levels 1–20 (null = none).</summary>
        public IReadOnlyList<int> Cantrips { get; set; }

        /// <summary>Prepared spells at levels 1–20.</summary>
        public IReadOnlyList<int> Prepared { get; set; }

        public int CantripsAt(int level) => Cantrips == null ? 0 : Cantrips[Math.Max(1, Math.Min(20, level)) - 1];
        public int PreparedAt(int level) => Prepared == null ? 0 : Prepared[Math.Max(1, Math.Min(20, level)) - 1];
    }

    /// <summary>A named column of a class table (rage damage, sneak attack dice, martial arts die …), per level 1–20.</summary>
    public sealed class ScalingDefinition
    {
        public ScalingDefinition(string id, string name, string format, params int[] values)
        {
            Id = id;
            Name = name;
            Format = format;
            Values = values;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>A composite format with {0} for the value ("+{0}", "{0}d6", "d{0}", "+{0} ft").</summary>
        public string Format { get; }

        public IReadOnlyList<int> Values { get; }

        public int At(int level) => Values[Math.Max(1, Math.Min(20, level)) - 1];
        public string Text(int level) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Format, At(level));
    }

    /// <summary>A subclass (Phase 7): its features and choices by level, and its resources.</summary>
    public sealed class SubclassDefinition
    {
        public string Id { get; set; }
        public string ClassId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ContentSource Source { get; set; }
        public IReadOnlyDictionary<int, IReadOnlyList<FeatureDefinition>> Features { get; set; } = new Dictionary<int, IReadOnlyList<FeatureDefinition>>();
        public IReadOnlyList<ChoiceDefinition> Choices { get; set; } = Array.Empty<ChoiceDefinition>();
        public IReadOnlyList<ResourceDefinition> Resources { get; set; } = Array.Empty<ResourceDefinition>();
    }

    /// <summary>A feat (Phase 7, SRD 5.2.1): category, prerequisites, effects, and its own choices.</summary>
    public sealed class FeatDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public FeatCategory Category { get; set; }
        public IReadOnlyList<Prerequisite> Prerequisites { get; set; } = Array.Empty<Prerequisite>();
        public bool Repeatable { get; set; }

        /// <summary>When repeated, this sub-choice must differ each time (Magic Initiate's spell list).</summary>
        public string RepeatDistinct { get; set; }

        public IReadOnlyList<RuleEffect> Effects { get; set; } = Array.Empty<RuleEffect>();

        /// <summary>The feat's own choices; their IDs are local ("abilities", "skills") and stored under "{feat choice}:{local}".</summary>
        public IReadOnlyList<ChoiceDefinition> Choices { get; set; } = Array.Empty<ChoiceDefinition>();

        public IntegrationNeed Needs { get; set; }
        public ContentSource Source { get; set; } = ContentSource.Srd521;
    }

    /// <summary>Items granted together (item ID, quantity, equip on grant).</summary>
    public sealed class KitItem
    {
        public KitItem(string itemId, int quantity = 1, bool equip = false)
        {
            ItemId = itemId;
            Quantity = quantity;
            Equip = equip;
        }

        public string ItemId { get; }
        public int Quantity { get; }
        public bool Equip { get; }
    }

    public sealed class EquipmentOption
    {
        public EquipmentOption(string id, string name, params KitItem[] items)
        {
            Id = id;
            Name = name;
            Items = items ?? Array.Empty<KitItem>();
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<KitItem> Items { get; }
    }

    /// <summary>One starting-equipment decision: exactly one option is taken (a group with one option is simply given).</summary>
    public sealed class EquipmentGroup
    {
        public EquipmentGroup(string id, params EquipmentOption[] options)
        {
            Id = id;
            Options = options ?? Array.Empty<EquipmentOption>();
        }

        public string Id { get; }
        public IReadOnlyList<EquipmentOption> Options { get; }
    }

    /// <summary>How a species looks in the game today, and the contract for its finished model.</summary>
    public sealed class SpeciesAppearance
    {
        /// <summary>True when the species has its own finished body and head art.</summary>
        public bool HasFinishedModel { get; set; }

        /// <summary>The body set the builder uses ("Human" for every species until its model exists).</summary>
        public string BodySet { get; set; } = "Human";

        /// <summary>The head set the builder uses.</summary>
        public string HeadSet { get; set; } = "Human";

        /// <summary>Height relative to a human (the placeholder body is scaled by it).</summary>
        public float HeightScale { get; set; } = 1f;

        /// <summary>Visual features the finished model needs (horns, tusks, scales, pointed ears …), for the art phase.</summary>
        public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();

        /// <summary>Hair options allowed (null = the frame's normal options).</summary>
        public bool AllowsBeard { get; set; } = true;

        public string PlaceholderNote => HasFinishedModel ? null : "Placeholder body: the human model at this species' height. The final model is pending.";
    }

    public sealed class SpeciesDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Lore { get; set; }
        public CreatureSize Size { get; set; } = CreatureSize.Medium;
        public int Speed { get; set; } = 30;
        public string CreatureType { get; set; } = "Humanoid";
        public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();

        /// <summary>Ability score increases (the rules configuration gives these to species).</summary>
        public IReadOnlyDictionary<Ability, int> AbilityBonuses { get; set; } = new Dictionary<Ability, int>();

        public IReadOnlyList<FeatureDefinition> Traits { get; set; } = Array.Empty<FeatureDefinition>();
        public IReadOnlyList<ChoiceDefinition> Choices { get; set; } = Array.Empty<ChoiceDefinition>();
        public IReadOnlyList<ResourceDefinition> Resources { get; set; } = Array.Empty<ResourceDefinition>();
        public SpeciesAppearance Appearance { get; set; } = new SpeciesAppearance();
        public ContentSource Source { get; set; }
    }

    public sealed class BackgroundDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Lore { get; set; }
        public IReadOnlyList<Skill> Skills { get; set; } = Array.Empty<Skill>();
        public IReadOnlyList<string> Tools { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();
        public IReadOnlyList<KitItem> Equipment { get; set; } = Array.Empty<KitItem>();
        public int Coin { get; set; }
        public FeatureDefinition Feature { get; set; }

        /// <summary>Optional roleplaying prompts (flavour only).</summary>
        public IReadOnlyList<string> Suggestions { get; set; } = Array.Empty<string>();

        public ContentSource Source { get; set; }
    }

    public sealed class ClassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Identity { get; set; }
        public IReadOnlyList<Ability> PrimaryAbilities { get; set; } = Array.Empty<Ability>();
        public int HitDie { get; set; }
        public IReadOnlyList<Ability> SavingThrows { get; set; } = Array.Empty<Ability>();
        public IReadOnlyList<string> ArmorProficiencies { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> WeaponProficiencies { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Tools { get; set; } = Array.Empty<string>();
        public int SkillChoices { get; set; }
        public IReadOnlyList<Skill> SkillOptions { get; set; } = Array.Empty<Skill>();
        public IReadOnlyList<EquipmentGroup> Equipment { get; set; } = Array.Empty<EquipmentGroup>();
        public IReadOnlyList<ResourceDefinition> Resources { get; set; } = Array.Empty<ResourceDefinition>();

        /// <summary>Level-one features.</summary>
        public IReadOnlyList<FeatureDefinition> Features { get; set; } = Array.Empty<FeatureDefinition>();

        /// <summary>Choices beyond skills (fighting style, expertise).</summary>
        public IReadOnlyList<ChoiceDefinition> Choices { get; set; } = Array.Empty<ChoiceDefinition>();

        public SpellcastingDefinition Spellcasting { get; set; }

        /// <summary>The level at which a subclass is chosen (progression phase).</summary>
        public int SubclassLevel { get; set; } = 3;

        /// <summary>What the class's subclasses are called (e.g. "Primal Path").</summary>
        public string SubclassTitle { get; set; }

        /// <summary>Features at levels 2–20 (Phase 7): level → features.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<FeatureDefinition>> LaterFeatures { get; set; } = new Dictionary<int, IReadOnlyList<FeatureDefinition>>();

        // ----- Phase 7 -----

        /// <summary>Choices made on reaching levels 2–20 (IDs "lv{level}:{name}").</summary>
        public IReadOnlyList<ChoiceDefinition> LevelChoices { get; set; } = Array.Empty<ChoiceDefinition>();

        /// <summary>Levels that grant a feat (Ability Score Improvement or another general or origin feat).</summary>
        public IReadOnlyList<int> FeatLevels { get; set; } = new[] { 4, 8, 12, 16 };

        /// <summary>The level of the Epic Boon feat.</summary>
        public int EpicBoonLevel { get; set; } = 19;

        /// <summary>Levels at which the subclass grants features (from the class table).</summary>
        public IReadOnlyList<int> SubclassFeatureLevels { get; set; } = Array.Empty<int>();

        /// <summary>Named columns of the class table.</summary>
        public IReadOnlyList<ScalingDefinition> Scalings { get; set; } = Array.Empty<ScalingDefinition>();

        /// <summary>The fighting style feature, if the class has one (the level it arrives at; 0 = none).</summary>
        public int FightingStyleLevel { get; set; }

        public ScalingDefinition FindScaling(string id)
        {
            foreach (var s in Scalings) if (s.Id == id) return s;
            return null;
        }

        public ContentSource Source { get; set; }

        /// <summary>The choice ID under which class skills are recorded.</summary>
        public const string SkillChoiceId = "class:skills";
    }

    /// <summary>A tool proficiency and the item that embodies it (if any).</summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(string id, string name, string itemId)
        {
            Id = id;
            Name = name;
            ItemId = itemId;
        }

        public string Id { get; }
        public string Name { get; }
        public string ItemId { get; }
    }

    public sealed class LanguageDefinition
    {
        public LanguageDefinition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
    }
}
