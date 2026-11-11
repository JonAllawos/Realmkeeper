using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    public static partial class RpgCatalog
    {
        private static EquipmentGroup G(string id, params EquipmentOption[] options) => new EquipmentGroup(id, options);

        private static EquipmentOption O(string id, string name, params KitItem[] items) => new EquipmentOption(id, name, items);

        private static ResourceDefinition R(string id, string name, ResourceFormula formula, int value = 0, Ability ability = Ability.Strength,
            ResourceRecovery recovery = ResourceRecovery.LongRest, string description = null) =>
            new ResourceDefinition { Id = id, Name = name, Formula = formula, Value = value, Ability = ability, Recovery = recovery, Description = description };

        private static FeatureDefinition Spellcasting(string name, string text) => F("spellcasting", name, text, E(EffectKind.Resource, "spell_slots_1"));

        private static ResourceDefinition Slots(int n, ResourceRecovery recovery = ResourceRecovery.LongRest) =>
            R("spell_slots_1", "1st-level spell slots", ResourceFormula.Fixed, n, recovery: recovery);

        private static readonly Skill[] AnySkill = (Skill[])System.Enum.GetValues(typeof(Skill));

        // Level-one mechanics follow SRD 5.2.1 (classes are in the SRD); text is Realmkeeper's own. Spells are not
        // listed: spellcasting records the ability, cantrips, slots and focus for the magic phase.
        private static IReadOnlyList<ClassDefinition> BuildClasses()
        {
            var list = BuildLevelOneClasses();
            foreach (var c in list) ApplyProgression(c);
            return list;
        }

        private static IReadOnlyList<ClassDefinition> BuildLevelOneClasses() => new[]
        {
            new ClassDefinition
            {
                Id = "barbarian", Name = "Barbarian", Source = ContentSource.Srd521, SubclassTitle = "Primal Path",
                Description = "A warrior who fights on fury and endurance rather than drill.",
                Identity = "Front-line toughness: the most hit points, damage resistance while raging, and armour of bare skin.",
                PrimaryAbilities = new[] { Ability.Strength }, HitDie = 12, SavingThrows = new[] { Ability.Strength, Ability.Constitution },
                ArmorProficiencies = new[] { "light", "medium", "shields" }, WeaponProficiencies = new[] { "simple", "martial" },
                SkillChoices = 2, SkillOptions = new[] { Skill.AnimalHandling, Skill.Athletics, Skill.Intimidation, Skill.Nature, Skill.Perception, Skill.Survival },
                Resources = new[] { R("rage", "Rage", ResourceFormula.Fixed, 2, description: "Enter a rage: bonus damage and resistance to physical damage for a minute.") },
                Features = new[]
                {
                    F("rage", "Rage", "Twice per long rest you can rage: +2 melee damage and resistance to bludgeoning, piercing and slashing damage.",
                        E(EffectKind.Resource, "rage"), E(EffectKind.CombatRule, "rage-damage", 2, "+2 melee damage while raging")),
                    F("unarmored_defense", "Unarmored Defence", "Without armour, your armour class is 10 + Dexterity + Constitution.", E(EffectKind.UnarmoredDefense, nameof(Ability.Constitution))),
                    F("weapon_mastery", "Weapon Mastery", "You know the special properties of two weapons (used when combat arrives).", E(EffectKind.CombatRule, "weapon-mastery", 2, "Two weapon masteries")),
                },
                Equipment = new[]
                {
                    G("weapon", O("greataxe", "A greataxe", K("greataxe", 1, true)), O("handaxes", "Two hand axes", K("axe_hand", 1, true), K("axe_hand"))),
                    G("javelins", O("javelins", "Four javelins", K("javelin", 4))),
                },
            },
            new ClassDefinition
            {
                Id = "bard", Name = "Bard", Source = ContentSource.Srd521, SubclassTitle = "Bard College",
                Description = "A performer whose words and music carry real power.",
                Identity = "The widest skills, inspiration for allies, and Charisma-based magic.",
                PrimaryAbilities = new[] { Ability.Charisma }, HitDie = 8, SavingThrows = new[] { Ability.Dexterity, Ability.Charisma },
                ArmorProficiencies = new[] { "light" }, WeaponProficiencies = new[] { "simple" }, Tools = new[] { "lute" },
                SkillChoices = 3, SkillOptions = AnySkill,
                Resources = new[] { R("bardic_inspiration", "Bardic Inspiration", ResourceFormula.AbilityModifierMin1, ability: Ability.Charisma), Slots(2) },
                Features = new[]
                {
                    F("bardic_inspiration", "Bardic Inspiration", "You can give an ally a d6 to add to one roll, a number of times equal to your Charisma modifier per long rest.",
                        E(EffectKind.Resource, "bardic_inspiration"), E(EffectKind.CombatRule, "inspiration-die", 6, "d6 inspiration die")),
                    Spellcasting("Spellcasting", "You cast bard spells using Charisma, with a musical instrument as your focus."),
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Charisma, Style = "Known spells", CantripsKnown = 2, FirstLevelSlots = 2, Focus = "Musical instrument" },
                Equipment = new[]
                {
                    G("weapon", O("rapier", "A rapier", K("rapier", 1, true)), O("dagger", "A dagger", K("dagger_iron", 1, true))),
                    G("armour", O("leather", "A leather jerkin", K("jerkin_leather", 1, true))),
                    G("instrument", O("lute", "A lute", K("lute"))),
                },
            },
            new ClassDefinition
            {
                Id = "cleric", Name = "Cleric", Source = ContentSource.Srd521, SubclassTitle = "Divine Domain",
                Description = "A servant of a divine power who heals, protects and smites in its name.",
                Identity = "Wisdom-based divine magic, medium armour and a shield, and healing.",
                PrimaryAbilities = new[] { Ability.Wisdom }, HitDie = 8, SavingThrows = new[] { Ability.Wisdom, Ability.Charisma },
                ArmorProficiencies = new[] { "light", "medium", "shields" }, WeaponProficiencies = new[] { "simple" },
                SkillChoices = 2, SkillOptions = new[] { Skill.History, Skill.Insight, Skill.Medicine, Skill.Persuasion, Skill.Religion },
                Resources = new[] { Slots(2) },
                Features = new[] { Spellcasting("Spellcasting", "You prepare and cast cleric spells using Wisdom, with a holy symbol as your focus.") },
                Choices = new[]
                {
                    new ChoiceDefinition
                    {
                        Id = "class:divine_order", Name = "Divine Order", Prompt = "Choose your calling.", Kind = ChoiceKind.Option,
                        Options = new[]
                        {
                            new ChoiceOption("protector", "Protector", "Trained for battle: heavy armour and martial weapons.", E(EffectKind.ArmorProficiency, "heavy"), E(EffectKind.WeaponProficiency, "martial")),
                            new ChoiceOption("thaumaturge", "Thaumaturge", "Steeped in lore: one extra cantrip and your Wisdom added to Arcana and Religion checks.",
                                E(EffectKind.InnateMagic, "extra-cantrip", 1, "One extra cantrip"),
                                new RuleEffect(EffectKind.SkillAbilityBonus, nameof(Skill.Arcana), 0, nameof(Ability.Wisdom), EffectCondition.Always, 1),
                                new RuleEffect(EffectKind.SkillAbilityBonus, nameof(Skill.Religion), 0, nameof(Ability.Wisdom), EffectCondition.Always, 1)),
                        },
                    },
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Wisdom, Style = "Prepared spells", CantripsKnown = 3, FirstLevelSlots = 2, Focus = "Holy symbol" },
                Equipment = new[]
                {
                    G("weapon", O("mace", "A flanged mace", K("mace_flanged", 1, true))),
                    G("armour", O("chain_shirt", "A chain shirt", K("chain_shirt", 1, true)), O("leather", "A leather jerkin", K("jerkin_leather", 1, true))),
                    G("shield", O("shield", "A round shield", K("shield_round", 1, true))),
                    G("focus", O("holy_symbol", "A holy symbol", K("holy_symbol"))),
                },
            },
            new ClassDefinition
            {
                Id = "druid", Name = "Druid", Source = ContentSource.Srd521, SubclassTitle = "Druid Circle",
                Description = "A guardian of the wild who draws magic from nature itself.",
                Identity = "Wisdom-based nature magic, the secret Druidic tongue, and herbal knowledge.",
                PrimaryAbilities = new[] { Ability.Wisdom }, HitDie = 8, SavingThrows = new[] { Ability.Intelligence, Ability.Wisdom },
                ArmorProficiencies = new[] { "light", "shields" }, WeaponProficiencies = new[] { "simple" }, Tools = new[] { "herbalism_kit" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Arcana, Skill.AnimalHandling, Skill.Insight, Skill.Medicine, Skill.Nature, Skill.Perception, Skill.Religion, Skill.Survival },
                Resources = new[] { Slots(2) },
                Features = new[]
                {
                    F("druidic", "Druidic", "You know Druidic, the secret language of druids.", E(EffectKind.Language, "druidic")),
                    Spellcasting("Spellcasting", "You prepare and cast druid spells using Wisdom, with a druidic focus."),
                },
                Choices = new[]
                {
                    // Phase 7 (rules v7): SRD 5.2.1 level-1 Primal Order. Druids created before it choose at their next advancement.
                    new ChoiceDefinition
                    {
                        Id = "class:primal_order", Name = "Primal Order", Prompt = "Choose your sacred role.", Kind = ChoiceKind.Option, IntroducedInRules = 7,
                        Options = new[]
                        {
                            new ChoiceOption("magician", "Magician", "One extra cantrip, and your Wisdom added to Arcana and Nature checks.",
                                E(EffectKind.InnateMagic, "extra-cantrip", 1, "One extra cantrip"),
                                new RuleEffect(EffectKind.SkillAbilityBonus, nameof(Skill.Arcana), 0, nameof(Ability.Wisdom), EffectCondition.Always, 1),
                                new RuleEffect(EffectKind.SkillAbilityBonus, nameof(Skill.Nature), 0, nameof(Ability.Wisdom), EffectCondition.Always, 1)),
                            new ChoiceOption("warden", "Warden", "Trained for battle: martial weapons and medium armour.", E(EffectKind.WeaponProficiency, "martial"), E(EffectKind.ArmorProficiency, "medium")),
                        },
                    },
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Wisdom, Style = "Prepared spells", CantripsKnown = 2, FirstLevelSlots = 2, Focus = "Druidic focus" },
                Equipment = new[]
                {
                    G("weapon", O("scimitar", "A scimitar", K("scimitar", 1, true)), O("staff", "A quarterstaff", K("staff_quarter", 1, true))),
                    G("armour", O("leather", "A leather jerkin and a wooden buckler", K("jerkin_leather", 1, true), K("shield_buckler", 1, true))),
                    G("focus", O("druidic_focus", "A druidic focus", K("druidic_focus"))),
                },
            },
            new ClassDefinition
            {
                Id = "fighter", Name = "Fighter", Source = ContentSource.Srd521, SubclassTitle = "Martial Archetype",
                Description = "A trained warrior, master of arms and armour.",
                Identity = "Every armour and weapon, a fighting style, and the stamina to get back up.",
                PrimaryAbilities = new[] { Ability.Strength, Ability.Dexterity }, HitDie = 10, SavingThrows = new[] { Ability.Strength, Ability.Constitution },
                ArmorProficiencies = new[] { "light", "medium", "heavy", "shields" }, WeaponProficiencies = new[] { "simple", "martial" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Acrobatics, Skill.AnimalHandling, Skill.Athletics, Skill.History, Skill.Insight, Skill.Intimidation, Skill.Perception, Skill.Survival },
                Resources = new[] { R("second_wind", "Second Wind", ResourceFormula.Fixed, 2, recovery: ResourceRecovery.ShortRest) },
                Features = new[]
                {
                    F("second_wind", "Second Wind", "As a bonus action you regain 1d10 + your level in hit points (two uses, one back on a short rest).",
                        E(EffectKind.Resource, "second_wind"), E(EffectKind.CombatRule, "second-wind", 10, "Heal 1d10 + level")),
                    F("weapon_mastery", "Weapon Mastery", "You know the special properties of three weapons (used when combat arrives).", E(EffectKind.CombatRule, "weapon-mastery", 3, "Three weapon masteries")),
                },
                Choices = new[]
                {
                    new ChoiceDefinition
                    {
                        Id = "class:fighting_style", Name = "Fighting Style", Prompt = "Choose the way you fight.", Kind = ChoiceKind.Option,
                        Options = new[]
                        {
                            new ChoiceOption("archery", "Archery", "+2 to attack rolls with ranged weapons.", E(EffectKind.CombatRule, "ranged-attack-bonus", 2), E(EffectKind.FightingStyle, "archery")),
                            new ChoiceOption("defense", "Defence", "+1 armour class while wearing armour.",
                                new RuleEffect(EffectKind.ArmorClassBonus, null, 1, null, EffectCondition.WearingArmor), E(EffectKind.FightingStyle, "defense")),
                            new ChoiceOption("dueling", "Duelling", "+2 damage with a one-handed weapon and no other weapon.", E(EffectKind.CombatRule, "dueling-damage", 2), E(EffectKind.FightingStyle, "dueling")),
                            new ChoiceOption("great_weapon", "Great Weapon Fighting", "Treat 1s and 2s as 3s on two-handed weapon damage dice.", E(EffectKind.CombatRule, "great-weapon-floor", 3), E(EffectKind.FightingStyle, "great_weapon")),
                            new ChoiceOption("protection", "Protection", "Impose disadvantage on an attack against an ally next to you, using your shield.", E(EffectKind.CombatRule, "protect-ally", 1), E(EffectKind.FightingStyle, "protection")),
                        },
                    },
                },
                Equipment = new[]
                {
                    G("armour", O("chain_shirt", "A chain shirt", K("chain_shirt", 1, true)), O("leather", "A leather jerkin and a hunting bow", K("jerkin_leather", 1, true), K("bow_hunting"))),
                    G("weapon", O("sword_shield", "An arming sword and a round shield", K("sword_arming", 1, true), K("shield_round", 1, true)),
                        O("greataxe", "A greataxe", K("greataxe", 1, true))),
                    G("ranged", O("crossbow", "A light crossbow", K("light_crossbow")), O("javelins", "Four javelins", K("javelin", 4))),
                },
            },
            new ClassDefinition
            {
                Id = "monk", Name = "Monk", Source = ContentSource.Srd521, SubclassTitle = "Monastic Tradition",
                Description = "A disciplined fighter whose body is the weapon.",
                Identity = "Unarmoured defence from Wisdom, martial arts, and speed.",
                PrimaryAbilities = new[] { Ability.Dexterity, Ability.Wisdom }, HitDie = 8, SavingThrows = new[] { Ability.Strength, Ability.Dexterity },
                WeaponProficiencies = new[] { "simple", "shortsword" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Acrobatics, Skill.Athletics, Skill.History, Skill.Insight, Skill.Religion, Skill.Stealth },
                Features = new[]
                {
                    F("unarmored_defense", "Unarmoured Defence", "Without armour or shield, your armour class is 10 + Dexterity + Wisdom.", E(EffectKind.UnarmoredDefense, nameof(Ability.Wisdom))),
                    F("martial_arts", "Martial Arts", "Your unarmed strikes and monk weapons use a d6 and Dexterity, and you can strike again as a bonus action.",
                        E(EffectKind.CombatRule, "martial-arts-die", 6, "d6 martial arts")),
                },
                Equipment = new[]
                {
                    G("weapon", O("shortsword", "A shortsword", K("shortsword", 1, true)), O("spear", "A spear", K("spear_ash", 1, true))),
                    G("provisions", O("rations", "Travel rations", K("travel_rations", 3))),
                },
            },
            new ClassDefinition
            {
                Id = "paladin", Name = "Paladin", Source = ContentSource.Srd521, SubclassTitle = "Sacred Oath",
                Description = "A sworn champion whose oath grants holy power.",
                Identity = "Heavy armour, a healing pool, and a sense for the uncanny.",
                PrimaryAbilities = new[] { Ability.Strength, Ability.Charisma }, HitDie = 10, SavingThrows = new[] { Ability.Wisdom, Ability.Charisma },
                ArmorProficiencies = new[] { "light", "medium", "heavy", "shields" }, WeaponProficiencies = new[] { "simple", "martial" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Athletics, Skill.Insight, Skill.Intimidation, Skill.Medicine, Skill.Persuasion, Skill.Religion },
                Resources = new[]
                {
                    R("lay_on_hands", "Lay on Hands", ResourceFormula.PerLevel, 5, description: "A pool of healing: restore hit points by touch."),
                    Slots(2),
                },
                Features = new[]
                {
                    F("lay_on_hands", "Lay on Hands", "You have a pool of healing equal to five times your level, spent by touch.", E(EffectKind.Resource, "lay_on_hands")),
                    Spellcasting("Spellcasting", "You prepare and cast paladin spells using Charisma, with a holy symbol as your focus."),
                    F("weapon_mastery", "Weapon Mastery", "You know the special properties of two weapons (used when combat arrives).", E(EffectKind.CombatRule, "weapon-mastery", 2, "Two weapon masteries")),
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Charisma, Style = "Prepared spells", CantripsKnown = 0, FirstLevelSlots = 2, Focus = "Holy symbol" },
                Equipment = new[]
                {
                    G("armour", O("chain_shirt", "A chain shirt", K("chain_shirt", 1, true))),
                    G("weapon", O("sword_shield", "An arming sword and a round shield", K("sword_arming", 1, true), K("shield_round", 1, true)),
                        O("javelins", "A flanged mace and four javelins", K("mace_flanged", 1, true), K("javelin", 4))),
                    G("focus", O("holy_symbol", "A holy symbol", K("holy_symbol"))),
                },
            },
            new ClassDefinition
            {
                Id = "ranger", Name = "Ranger", Source = ContentSource.Srd521, SubclassTitle = "Ranger Conclave",
                Description = "A hunter and scout of the wild borders.",
                Identity = "Three skills, nature magic from Wisdom, and a marked quarry.",
                PrimaryAbilities = new[] { Ability.Dexterity, Ability.Wisdom }, HitDie = 10, SavingThrows = new[] { Ability.Strength, Ability.Dexterity },
                ArmorProficiencies = new[] { "light", "medium", "shields" }, WeaponProficiencies = new[] { "simple", "martial" },
                SkillChoices = 3, SkillOptions = new[] { Skill.AnimalHandling, Skill.Athletics, Skill.Insight, Skill.Investigation, Skill.Nature, Skill.Perception, Skill.Stealth, Skill.Survival },
                Resources = new[] { R("favored_enemy", "Favoured Enemy", ResourceFormula.Fixed, 2, description: "Mark a quarry without spending a spell slot."), Slots(2) },
                Features = new[]
                {
                    F("favored_enemy", "Favoured Enemy", "Twice per long rest you can mark a quarry and deal an extra d6 damage to it.",
                        E(EffectKind.Resource, "favored_enemy"), E(EffectKind.CombatRule, "quarry-damage", 6, "+d6 against your quarry")),
                    Spellcasting("Spellcasting", "You prepare and cast ranger spells using Wisdom."),
                    F("weapon_mastery", "Weapon Mastery", "You know the special properties of two weapons (used when combat arrives).", E(EffectKind.CombatRule, "weapon-mastery", 2, "Two weapon masteries")),
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Wisdom, Style = "Prepared spells", CantripsKnown = 0, FirstLevelSlots = 2, Focus = "Druidic focus" },
                Equipment = new[]
                {
                    G("armour", O("leather", "A leather jerkin and bracers", K("jerkin_leather", 1, true), K("bracers_leather", 1, true))),
                    G("weapon", O("shortswords", "Two shortswords", K("shortsword", 1, true), K("shortsword")), O("sword", "An arming sword", K("sword_arming", 1, true))),
                    G("bow", O("bow", "A hunting bow", K("bow_hunting"))),
                },
            },
            new ClassDefinition
            {
                Id = "rogue", Name = "Rogue", Source = ContentSource.Srd521, SubclassTitle = "Roguish Archetype",
                Description = "A specialist in stealth, skill and precise strikes.",
                Identity = "The most skills, expertise, sneak attack and thieves' tools.",
                PrimaryAbilities = new[] { Ability.Dexterity }, HitDie = 8, SavingThrows = new[] { Ability.Dexterity, Ability.Intelligence },
                ArmorProficiencies = new[] { "light" }, WeaponProficiencies = new[] { "simple", "martial (finesse or light)" }, Tools = new[] { "thieves_tools" },
                SkillChoices = 4, SkillOptions = new[] { Skill.Acrobatics, Skill.Athletics, Skill.Deception, Skill.Insight, Skill.Intimidation, Skill.Investigation, Skill.Perception, Skill.Performance, Skill.Persuasion, Skill.SleightOfHand, Skill.Stealth },
                Features = new[]
                {
                    F("expertise", "Expertise", "Two of your skill proficiencies count double."),
                    F("sneak_attack", "Sneak Attack", "Once per turn, deal an extra 1d6 damage to a creature you have advantage against.", E(EffectKind.CombatRule, "sneak-attack-dice", 1, "1d6 sneak attack")),
                    F("thieves_cant", "Thieves' Cant", "You know the signs and slang of the underworld.", E(EffectKind.Language, "thieves_cant")),
                    F("weapon_mastery", "Weapon Mastery", "You know the special properties of two weapons (used when combat arrives).", E(EffectKind.CombatRule, "weapon-mastery", 2, "Two weapon masteries")),
                },
                Choices = new[] { new ChoiceDefinition { Id = "class:expertise", Name = "Expertise", Prompt = "Choose two skills you are proficient in.", Kind = ChoiceKind.Expertise, Count = 2 } },
                Equipment = new[]
                {
                    G("weapon", O("rapier", "A rapier", K("rapier", 1, true)), O("shortsword", "A shortsword", K("shortsword", 1, true))),
                    G("ranged", O("bow", "A hunting bow", K("bow_hunting")), O("daggers", "Two daggers", K("dagger_iron"), K("dagger_iron"))),
                    G("armour", O("leather", "A leather jerkin", K("jerkin_leather", 1, true))),
                    G("tools", O("thieves_tools", "Thieves' tools", K("thieves_tools"))),
                },
            },
            new ClassDefinition
            {
                Id = "sorcerer", Name = "Sorcerer", Source = ContentSource.Srd521, SubclassTitle = "Sorcerous Origin",
                Description = "A caster whose magic is in the blood.",
                Identity = "The most cantrips, Charisma magic, and bursts of innate sorcery.",
                PrimaryAbilities = new[] { Ability.Charisma }, HitDie = 6, SavingThrows = new[] { Ability.Constitution, Ability.Charisma },
                WeaponProficiencies = new[] { "simple" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Arcana, Skill.Deception, Skill.Insight, Skill.Intimidation, Skill.Persuasion, Skill.Religion },
                Resources = new[] { R("innate_sorcery", "Innate Sorcery", ResourceFormula.Fixed, 2, description: "Let raw magic surge for a minute."), Slots(2) },
                Features = new[]
                {
                    Spellcasting("Spellcasting", "You cast sorcerer spells using Charisma, with an arcane focus."),
                    F("innate_sorcery", "Innate Sorcery", "Twice per long rest your magic surges for a minute: your spells are harder to resist.",
                        E(EffectKind.Resource, "innate_sorcery"), E(EffectKind.CombatRule, "spell-dc-bonus", 1, "+1 spell save DC while surging")),
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Charisma, Style = "Known spells", CantripsKnown = 4, FirstLevelSlots = 2, Focus = "Arcane focus" },
                Equipment = new[]
                {
                    G("weapon", O("crossbow", "A light crossbow", K("light_crossbow")), O("staff", "A quarterstaff", K("staff_quarter", 1, true))),
                    G("focus", O("arcane_focus", "An arcane focus", K("arcane_focus"))),
                    G("dagger", O("dagger", "Two daggers", K("dagger_iron"), K("dagger_iron"))),
                },
            },
            new ClassDefinition
            {
                Id = "warlock", Name = "Warlock", Source = ContentSource.Srd521, SubclassTitle = "Otherworldly Patron",
                Description = "A caster who bargained with an otherworldly patron for power.",
                Identity = "Pact magic that returns on a short rest, and eldritch invocations.",
                PrimaryAbilities = new[] { Ability.Charisma }, HitDie = 8, SavingThrows = new[] { Ability.Wisdom, Ability.Charisma },
                ArmorProficiencies = new[] { "light" }, WeaponProficiencies = new[] { "simple" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Arcana, Skill.Deception, Skill.History, Skill.Intimidation, Skill.Investigation, Skill.Nature, Skill.Religion },
                Features = new[]
                {
                    F("spellcasting", "Pact Magic", "You cast warlock spells using Charisma; your pact slots return after a short rest.", E(EffectKind.Resource, "pact_slots")),
                    F("eldritch_invocation", "Eldritch Invocations", "You know eldritch invocations, fragments of forbidden lore: one at level 1, ten by level 18."),
                },
                // Phase 7 (rules v7): the level-1 invocation is chosen. Warlocks created before it choose at their next advancement.
                Choices = new[] { Invocations(1, 1) },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Charisma, Style = "Pact magic", CantripsKnown = 2, FirstLevelSlots = 1, Focus = "Arcane focus", SlotRecovery = ResourceRecovery.ShortRest },
                Equipment = new[]
                {
                    G("weapon", O("crossbow", "A light crossbow", K("light_crossbow")), O("staff", "A quarterstaff", K("staff_quarter", 1, true))),
                    G("armour", O("leather", "A leather jerkin", K("jerkin_leather", 1, true))),
                    G("focus", O("arcane_focus", "An arcane focus and a dagger", K("arcane_focus"), K("dagger_iron"))),
                },
            },
            new ClassDefinition
            {
                Id = "wizard", Name = "Wizard", Source = ContentSource.Srd521, SubclassTitle = "Arcane Tradition",
                Description = "A scholar of magic who learns spells from study and keeps them in a book.",
                Identity = "Intelligence magic from a spellbook, and recovering spent power by study.",
                PrimaryAbilities = new[] { Ability.Intelligence }, HitDie = 6, SavingThrows = new[] { Ability.Intelligence, Ability.Wisdom },
                WeaponProficiencies = new[] { "simple" },
                SkillChoices = 2, SkillOptions = new[] { Skill.Arcana, Skill.History, Skill.Insight, Skill.Investigation, Skill.Medicine, Skill.Religion },
                Resources = new[] { R("arcane_recovery", "Arcane Recovery", ResourceFormula.Fixed, 1, description: "Recover a spent slot after a short rest, once a day."), Slots(2) },
                Features = new[]
                {
                    Spellcasting("Spellcasting", "You prepare and cast wizard spells from your spellbook using Intelligence."),
                    Sc(F("arcane_recovery", "Arcane Recovery", "Once a day, after a short rest, you recover spent spell slots.", E(EffectKind.Resource, "arcane_recovery")), "arcane_recovery"),
                    new FeatureDefinition("ritual_adept", "Ritual Adept", "You can cast ritual spells from your book without preparing them.") { Needs = IntegrationNeed.Magic },
                },
                Spellcasting = new SpellcastingDefinition { Ability = Ability.Intelligence, Style = "Prepared from a spellbook", CantripsKnown = 3, FirstLevelSlots = 2, Focus = "Arcane focus" },
                Equipment = new[]
                {
                    G("weapon", O("staff", "A quarterstaff", K("staff_quarter", 1, true)), O("dagger", "A dagger", K("dagger_iron", 1, true))),
                    G("book", O("spellbook", "A spellbook and an arcane focus", K("spellbook"), K("arcane_focus"))),
                },
            },
        };
    }
}
