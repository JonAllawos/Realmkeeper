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
    }

    public enum ChoiceKind
    {
        /// <summary>Pick from fixed options, each with its own effects.</summary>
        Option = 0,

        /// <summary>Pick skills to be proficient in, from <see cref="ChoiceDefinition.Skills"/> (empty = any).</summary>
        Skill = 1,

        /// <summary>Pick skills you are already proficient in to gain expertise.</summary>
        Expertise = 2,
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
    }

    /// <summary>A required choice a species or class asks for at creation (e.g. skills, a fighting style, an ancestry).</summary>
    public sealed class ChoiceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Prompt { get; set; }
        public ChoiceKind Kind { get; set; }
        public int Count { get; set; } = 1;
        public IReadOnlyList<ChoiceOption> Options { get; set; } = Array.Empty<ChoiceOption>();
        public IReadOnlyList<Skill> Skills { get; set; } = Array.Empty<Skill>();
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
    }

    /// <summary>What a class can do with magic at level 1 (spells themselves belong to the magic phase).</summary>
    public sealed class SpellcastingDefinition
    {
        public Ability Ability { get; set; }
        public string Style { get; set; }
        public int CantripsKnown { get; set; }
        public int FirstLevelSlots { get; set; }
        public string Focus { get; set; }
        public ResourceRecovery SlotRecovery { get; set; }
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

        /// <summary>Features at later levels (empty in Phase 6; the progression phase fills it: level → features).</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<FeatureDefinition>> LaterFeatures { get; set; } = new Dictionary<int, IReadOnlyList<FeatureDefinition>>();

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
