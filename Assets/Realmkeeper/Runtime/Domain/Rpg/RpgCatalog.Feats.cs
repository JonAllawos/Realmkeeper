using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    // Phase 7: subclasses, feats, eldritch invocations and Metamagic. All are SRD 5.2.1 content (Wizards of the
    // Coast LLC, CC BY 4.0), one subclass per class as the SRD provides; the wording is Realmkeeper's own.
    // Further subclasses, and invocations that need damage cantrips or nested feats, are deferred (see
    // Documentation/Systems/CharacterProgression.md).
    public static partial class RpgCatalog
    {
        private static readonly Lazy<IReadOnlyList<SubclassDefinition>> SubclassList = new Lazy<IReadOnlyList<SubclassDefinition>>(BuildSubclasses);
        private static readonly Lazy<IReadOnlyList<FeatDefinition>> FeatList = new Lazy<IReadOnlyList<FeatDefinition>>(BuildFeats);

        public static IReadOnlyList<SubclassDefinition> Subclasses => SubclassList.Value;
        public static IReadOnlyList<FeatDefinition> Feats => FeatList.Value;

        public static SubclassDefinition FindSubclass(string id) => Find(Subclasses, id, s => s.Id);
        public static FeatDefinition FindFeat(string id) => Find(Feats, id, f => f.Id);

        public static IReadOnlyList<SubclassDefinition> SubclassesOf(string classId)
        {
            var list = new List<SubclassDefinition>();
            foreach (var s in Subclasses) if (s.ClassId == classId) list.Add(s);
            return list;
        }

        private static ChoiceDefinition SubOption(int level, string id, string name, string prompt, params ChoiceOption[] options) => new ChoiceDefinition
        {
            Id = $"lv{level}:{id}", Level = level, Name = name, Prompt = prompt, Kind = ChoiceKind.Option, Options = options,
        };

        private static ChoiceOption Opt(string id, string name, string text, IntegrationNeed needs, params RuleEffect[] effects) => new ChoiceOption(id, name, text, effects) { Needs = needs };

        private static IReadOnlyList<SubclassDefinition> BuildSubclasses() => new[]
        {
            new SubclassDefinition
            {
                Id = "berserker", ClassId = "barbarian", Name = "Path of the Berserker", Source = ContentSource.Srd521,
                Description = "Rage as raw, violent abandon.",
                Features = Levels(
                    (3, LF("frenzy", "Frenzy", "While raging, your reckless first strike each turn deals extra damage.", FeatureKind.Passive, Combat)),
                    (6, LF("mindless_rage", "Mindless Rage", "While raging you cannot be charmed or frightened.", FeatureKind.Passive, Combat)),
                    (10, LF("retaliation", "Retaliation", "Strike back at a creature that damages you.", FeatureKind.Active, Combat)),
                    (14, LF("intimidating_presence", "Intimidating Presence", "Frighten everyone you choose around you, once per long rest.", FeatureKind.LimitedUse, Combat))),
                Resources = new[] { new ResourceDefinition { Id = "intimidating_presence", Name = "Intimidating Presence", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 14, Recovery = ResourceRecovery.LongRest, Description = "Frighten nearby foes." } },
            },
            new SubclassDefinition
            {
                Id = "lore", ClassId = "bard", Name = "College of Lore", Source = ContentSource.Srd521,
                Description = "Collectors of knowledge and cutting remarks.",
                Features = Levels(
                    (3, LF("bonus_proficiencies", "Bonus Proficiencies", "You gain proficiency in three skills of your choice.", FeatureKind.Choice, None)),
                    (3, LF("cutting_words", "Cutting Words", "Spend Bardic Inspiration to subtract from a foe's roll.", FeatureKind.Active, Combat)),
                    (6, LF("magical_discoveries", "Magical Discoveries", "Learn two spells from any class list.", FeatureKind.Passive, Magic)),
                    (14, LF("peerless_skill", "Peerless Skill", "Add your Bardic Inspiration die to a failed ability check.", FeatureKind.Active, Explore | Talk))),
                Choices = new[] { new ChoiceDefinition { Id = "lv3:lore_skills", Level = 3, Name = "Bonus Proficiencies", Prompt = "Choose three skills.", Kind = ChoiceKind.Skill, Count = 3 } },
            },
            new SubclassDefinition
            {
                Id = "life", ClassId = "cleric", Name = "Life Domain", Source = ContentSource.Srd521,
                Description = "Healers devoted to vitality.",
                Features = Levels(
                    (3, LF("disciple_of_life", "Disciple of Life", "Your healing spells restore extra hit points.", FeatureKind.Passive, Magic)),
                    (3, LF("life_domain_spells", "Life Domain Spells", "Healing spells are always prepared.", FeatureKind.Passive, Magic)),
                    (3, LF("preserve_life", "Preserve Life", "Channel Divinity to heal many wounded allies.", FeatureKind.Active, Magic)),
                    (6, LF("blessed_healer", "Blessed Healer", "Healing others heals you too.", FeatureKind.Passive, Magic)),
                    (17, LF("supreme_healing", "Supreme Healing", "Your healing dice always roll their maximum.", FeatureKind.Passive, Magic))),
            },
            new SubclassDefinition
            {
                Id = "land", ClassId = "druid", Name = "Circle of the Land", Source = ContentSource.Srd521,
                Description = "Druids bound to a kind of land and its magic.",
                Features = Levels(
                    (3, LF("circle_of_the_land_spells", "Circle of the Land Spells", "Spells of your chosen land are always prepared.", FeatureKind.Choice, Magic)),
                    (3, LF("lands_aid", "Land's Aid", "Spend Wild Shape to harm foes and heal friends with thorny growth.", FeatureKind.Active, Combat)),
                    (6, LF("natural_recovery", "Natural Recovery", "Recover spell slots, or cast a land spell free, once per long rest.", FeatureKind.LimitedUse, Magic | Rest)),
                    (10, LF("natures_ward", "Nature's Ward", "You cannot be poisoned, and you resist your land's damage type.", FeatureKind.Passive, Combat)),
                    (14, LF("natures_sanctuary", "Nature's Sanctuary", "Spend Wild Shape to raise a sheltering grove.", FeatureKind.Active, Combat))),
                Choices = new[]
                {
                    SubOption(3, "land_type", "Land", "Choose the land your circle is bound to.",
                        Opt("arid", "Arid land", "Desert and wasteland magic.", Magic), Opt("polar", "Polar land", "Ice and tundra magic.", Magic),
                        Opt("temperate", "Temperate land", "Forest and field magic.", Magic), Opt("tropical", "Tropical land", "Jungle and swamp magic.", Magic)),
                },
                Resources = new[] { new ResourceDefinition { Id = "natural_recovery", Name = "Natural Recovery", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 6, Recovery = ResourceRecovery.LongRest, Description = "Recover spell slots." } },
            },
            new SubclassDefinition
            {
                Id = "champion", ClassId = "fighter", Name = "Champion", Source = ContentSource.Srd521,
                Description = "Raw physical excellence honed to deadly effect.",
                Features = Levels(
                    (3, LF("improved_critical", "Improved Critical", "Your weapon attacks score a critical hit on a 19 or 20.", FeatureKind.Passive, Combat)),
                    (3, LF("remarkable_athlete", "Remarkable Athlete", "Advantage on initiative and on Strength (Athletics) checks; move after a critical hit.", FeatureKind.Passive, None,
                        new RuleEffect(EffectKind.Advantage, "initiative"), new RuleEffect(EffectKind.Advantage, "skill:Athletics"))),
                    (7, LF("additional_fighting_style", "Additional Fighting Style", "You learn another Fighting Style feat.", FeatureKind.Choice, None)),
                    (10, LF("heroic_warrior", "Heroic Warrior", "Inspiration renews itself at the start of each of your turns in combat.", FeatureKind.Passive, Combat)),
                    (15, Rep(LF("superior_critical", "Superior Critical", "Your weapon attacks score a critical hit on an 18–20.", FeatureKind.Passive, Combat), "improved_critical")),
                    (18, LF("survivor", "Survivor", "Advantage on death saves, and you regain hit points each turn when bloodied.", FeatureKind.Passive, Combat))),
                Choices = new[] { FightingStyleFeat(7, "champion_style") },
            },
            new SubclassDefinition
            {
                Id = "open_hand", ClassId = "monk", Name = "Warrior of the Open Hand", Source = ContentSource.Srd521,
                Description = "Masters of unarmed technique.",
                Features = Levels(
                    (3, LF("open_hand_technique", "Open Hand Technique", "Flurry of Blows can push, topple or deny reactions.", FeatureKind.Passive, Combat)),
                    (6, LF("wholeness_of_body", "Wholeness of Body", "Heal yourself with a martial arts die plus Wisdom, Wisdom-modifier times per long rest.", FeatureKind.LimitedUse, Combat)),
                    (11, LF("fleet_step", "Fleet Step", "Step of the Wind follows your other bonus actions.", FeatureKind.Passive, Combat)),
                    (17, LF("quivering_palm", "Quivering Palm", "Set deadly vibrations in a creature's body.", FeatureKind.Active, Combat))),
                Resources = new[] { new ResourceDefinition { Id = "wholeness_of_body", Name = "Wholeness of Body", Formula = ResourceFormula.AbilityModifierMin1, Ability = Ability.Wisdom, FromLevel = 6, Recovery = ResourceRecovery.LongRest, Description = "Heal yourself." } },
            },
            new SubclassDefinition
            {
                Id = "devotion", ClassId = "paladin", Name = "Oath of Devotion", Source = ContentSource.Srd521,
                Description = "The ideals of justice, virtue and order.",
                Features = Levels(
                    (3, LF("oath_of_devotion_spells", "Oath of Devotion Spells", "Oath spells are always prepared.", FeatureKind.Passive, Magic)),
                    (3, LF("sacred_weapon", "Sacred Weapon", "Channel Divinity to make your weapon blaze with holy light.", FeatureKind.Active, Combat)),
                    (7, LF("aura_of_devotion", "Aura of Devotion", "You and allies in your aura cannot be charmed.", FeatureKind.Passive, Combat)),
                    (15, LF("smite_of_protection", "Smite of Protection", "Your smites shelter allies in your aura.", FeatureKind.Passive, Combat)),
                    (20, LF("holy_nimbus", "Holy Nimbus", "Become a radiant source of holy light, once per long rest.", FeatureKind.LimitedUse, Combat))),
                Resources = new[] { new ResourceDefinition { Id = "holy_nimbus", Name = "Holy Nimbus", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 20, Recovery = ResourceRecovery.LongRest, Description = "Radiate holy light." } },
            },
            new SubclassDefinition
            {
                Id = "hunter", ClassId = "ranger", Name = "Hunter", Source = ContentSource.Srd521,
                Description = "Protectors who hunt the threats at civilisation's edge.",
                Features = Levels(
                    (3, LF("hunters_lore", "Hunter's Lore", "You know the immunities and weaknesses of the creature you mark.", FeatureKind.Passive, Combat)),
                    (3, LF("hunters_prey", "Hunter's Prey", "Choose how you bring down prey.", FeatureKind.Choice, Combat)),
                    (7, LF("defensive_tactics", "Defensive Tactics", "Choose how you survive the hunt.", FeatureKind.Choice, Combat)),
                    (11, LF("superior_hunters_prey", "Superior Hunter's Prey", "Your mark's extra damage spreads to a second foe.", FeatureKind.Passive, Combat)),
                    (15, LF("superior_hunters_defense", "Superior Hunter's Defence", "Resist the damage of the attack that hits you.", FeatureKind.Active, Combat))),
                Choices = new[]
                {
                    SubOption(3, "hunters_prey", "Hunter's Prey", "Choose how you bring down prey.",
                        Opt("colossus_slayer", "Colossus Slayer", "Extra damage against wounded foes.", Combat), Opt("horde_breaker", "Horde Breaker", "An extra attack against a foe near your target.", Combat)),
                    SubOption(7, "defensive_tactics", "Defensive Tactics", "Choose how you survive the hunt.",
                        Opt("escape_the_horde", "Escape the Horde", "Opportunity attacks against you have disadvantage.", Combat), Opt("multiattack_defense", "Multiattack Defence", "A foe that hits you has disadvantage on its other attacks.", Combat)),
                },
            },
            new SubclassDefinition
            {
                Id = "thief", ClassId = "rogue", Name = "Thief", Source = ContentSource.Srd521,
                Description = "Burglars, treasure hunters and explorers.",
                Features = Levels(
                    (3, LF("fast_hands", "Fast Hands", "Use objects and tools as a bonus action.", FeatureKind.Passive, Combat)),
                    (3, LF("second_story_work", "Second-Story Work", "You climb at your walking speed and jump using Dexterity.", FeatureKind.Passive, Explore, new RuleEffect(EffectKind.MovementMode, "climb"))),
                    (9, LF("supreme_sneak", "Supreme Sneak", "A Cunning Strike option keeps you hidden.", FeatureKind.Passive, Combat | Explore)),
                    (13, LF("use_magic_device", "Use Magic Device", "Attune to more magic items and use any scroll.", FeatureKind.Passive, Magic)),
                    (17, LF("thiefs_reflexes", "Thief's Reflexes", "Take two turns in the first round of combat.", FeatureKind.Passive, Combat))),
            },
            new SubclassDefinition
            {
                Id = "draconic", ClassId = "sorcerer", Name = "Draconic Sorcery", Source = ContentSource.Srd521,
                Description = "Magic from a draconic ancestor.",
                Features = Levels(
                    (3, LF("draconic_resilience", "Draconic Resilience", "Your hit point maximum rises by 1 per sorcerer level, and without armour your AC is 10 + Dexterity + Charisma.", FeatureKind.Passive, None,
                        new RuleEffect(EffectKind.MaxHpPerLevel, null, 1), new RuleEffect(EffectKind.UnarmoredDefense, nameof(Ability.Charisma)))),
                    (3, LF("draconic_spells", "Draconic Spells", "Dragon-themed spells are always prepared.", FeatureKind.Passive, Magic)),
                    (6, LF("elemental_affinity", "Elemental Affinity", "Resist your chosen element and add Charisma to spell damage of that type.", FeatureKind.Choice, Magic)),
                    (14, LF("dragon_wings", "Dragon Wings", "Sprout wings and fly for an hour, once per long rest.", FeatureKind.LimitedUse, Explore)),
                    (18, LF("dragon_companion", "Dragon Companion", "Summon a dragon without material components.", FeatureKind.Passive, Magic))),
                Choices = new[]
                {
                    SubOption(6, "elemental_affinity", "Elemental Affinity", "Choose your draconic element.",
                        Opt("acid", "Acid", "Resistance to acid.", None, E(EffectKind.Resistance, "acid")), Opt("cold", "Cold", "Resistance to cold.", None, E(EffectKind.Resistance, "cold")),
                        Opt("fire", "Fire", "Resistance to fire.", None, E(EffectKind.Resistance, "fire")), Opt("lightning", "Lightning", "Resistance to lightning.", None, E(EffectKind.Resistance, "lightning")),
                        Opt("poison", "Poison", "Resistance to poison.", None, E(EffectKind.Resistance, "poison"))),
                },
                Resources = new[] { new ResourceDefinition { Id = "dragon_wings", Name = "Dragon Wings", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 14, Recovery = ResourceRecovery.LongRest, Description = "Fly for an hour." } },
            },
            new SubclassDefinition
            {
                Id = "fiend", ClassId = "warlock", Name = "Fiend Patron", Source = ContentSource.Srd521,
                Description = "A pact with a power of the lower planes.",
                Features = Levels(
                    (3, LF("dark_ones_blessing", "Dark One's Blessing", "Defeating foes grants you temporary hit points.", FeatureKind.Passive, Combat)),
                    (3, LF("fiend_spells", "Fiend Spells", "Fiendish spells are always prepared.", FeatureKind.Passive, Magic)),
                    (6, LF("dark_ones_own_luck", "Dark One's Own Luck", "Add a d10 to an ability check or save, Charisma-modifier times per long rest.", FeatureKind.LimitedUse, Combat | Explore)),
                    (10, LF("fiendish_resilience", "Fiendish Resilience", "After each rest, choose a damage type to resist.", FeatureKind.Passive, Rest | Combat)),
                    (14, LF("hurl_through_hell", "Hurl Through Hell", "Send a creature you hit through the lower planes, once per long rest.", FeatureKind.LimitedUse, Combat))),
                Resources = new[]
                {
                    new ResourceDefinition { Id = "dark_ones_own_luck", Name = "Dark One's Own Luck", Formula = ResourceFormula.AbilityModifierMin1, Ability = Ability.Charisma, FromLevel = 6, Recovery = ResourceRecovery.LongRest, Description = "Add a d10 to a check or save." },
                    new ResourceDefinition { Id = "hurl_through_hell", Name = "Hurl Through Hell", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 14, Recovery = ResourceRecovery.LongRest, Description = "Banish a foe briefly." },
                },
            },
            new SubclassDefinition
            {
                Id = "evoker", ClassId = "wizard", Name = "Evoker", Source = ContentSource.Srd521,
                Description = "Wizards of raw elemental force.",
                Features = Levels(
                    (3, LF("evocation_savant", "Evocation Savant", "Evocation spells come easily into your spellbook.", FeatureKind.Passive, Magic)),
                    (3, LF("potent_cantrip", "Potent Cantrip", "Your damaging cantrips harm even when they miss.", FeatureKind.Passive, Magic)),
                    (6, LF("sculpt_spells", "Sculpt Spells", "Shield allies from your own evocations.", FeatureKind.Passive, Magic)),
                    (10, LF("empowered_evocation", "Empowered Evocation", "Add Intelligence to evocation damage.", FeatureKind.Passive, Magic)),
                    (14, LF("overchannel", "Overchannel", "Push a spell to its maximum damage, at a cost.", FeatureKind.Active, Magic))),
            },
        };

        // ----- Feats (SRD 5.2.1) -----------------------------------------------------------------------

        private static ChoiceDefinition Increase(int count, bool repeat, int limit, params Ability[] from) => new ChoiceDefinition
        {
            Id = "abilities", Name = "Ability increase", Prompt = from.Length == 0 ? $"Raise abilities by {count} in total." : "Choose the ability to raise.",
            Kind = ChoiceKind.AbilityIncrease, Count = count, AllowRepeat = repeat, Limit = limit, Abilities = from,
        };

        private static FeatDefinition Boon(string id, string name, string text, IntegrationNeed needs, Ability[] from, params RuleEffect[] effects) => new FeatDefinition
        {
            Id = id, Name = name, Category = FeatCategory.EpicBoon, Description = text, Needs = needs, Effects = effects,
            Prerequisites = new[] { Prerequisite.Level(19) }, Choices = new[] { Increase(1, false, 30, from) },
        };

        private static readonly Ability[] AnyAbility = (Ability[])Enum.GetValues(typeof(Ability));

        private static IReadOnlyList<FeatDefinition> BuildFeats() => new[]
        {
            new FeatDefinition
            {
                Id = "ability_score_improvement", Name = "Ability Score Improvement", Category = FeatCategory.General, Repeatable = true,
                Description = "Raise one ability by 2, or two abilities by 1 (to a maximum of 20).",
                Prerequisites = new[] { Prerequisite.Level(4) }, Choices = new[] { Increase(2, true, 20) },
            },
            new FeatDefinition
            {
                Id = "grappler", Name = "Grappler", Category = FeatCategory.General, Needs = Combat,
                Description = "Raise Strength or Dexterity by 1; you grapple with ease and strike grappled foes with advantage.",
                Prerequisites = new[] { Prerequisite.Level(4), Prerequisite.Ability(13, Ability.Strength, Ability.Dexterity) },
                Choices = new[] { Increase(1, false, 20, Ability.Strength, Ability.Dexterity) },
                Effects = new[] { E(EffectKind.CombatRule, "grappler", 1, "Advantage against creatures you grapple") },
            },
            new FeatDefinition
            {
                Id = "alert", Name = "Alert", Category = FeatCategory.Origin,
                Description = "Add your proficiency bonus to initiative, and you can swap initiative with an ally.",
                Effects = new[] { new RuleEffect(EffectKind.InitiativeBonus, "proficiency"), E(EffectKind.CombatRule, "initiative-swap", 1) },
            },
            new FeatDefinition
            {
                Id = "magic_initiate", Name = "Magic Initiate", Category = FeatCategory.Origin, Repeatable = true, RepeatDistinct = "list", Needs = Magic,
                Description = "Learn two cantrips and a 1st-level spell from one class list (spells arrive with the magic phase).",
                Choices = new[]
                {
                    new ChoiceDefinition
                    {
                        Id = "list", Name = "Spell list", Prompt = "Choose the spell list.", Kind = ChoiceKind.Option,
                        Options = new[]
                        {
                            Opt("cleric", "Cleric", "Divine magic.", Magic, E(EffectKind.InnateMagic, "magic-initiate:cleric")),
                            Opt("druid", "Druid", "Nature magic.", Magic, E(EffectKind.InnateMagic, "magic-initiate:druid")),
                            Opt("wizard", "Wizard", "Arcane magic.", Magic, E(EffectKind.InnateMagic, "magic-initiate:wizard")),
                        },
                    },
                },
            },
            new FeatDefinition
            {
                Id = "savage_attacker", Name = "Savage Attacker", Category = FeatCategory.Origin, Needs = Combat,
                Description = "Once per turn, roll a weapon's damage dice twice and use either roll.",
                Effects = new[] { E(EffectKind.CombatRule, "savage-attacker", 1) },
            },
            new FeatDefinition
            {
                Id = "skilled", Name = "Skilled", Category = FeatCategory.Origin, Repeatable = true,
                Description = "Gain proficiency in three skills (Realmkeeper offers skills only; tools are deferred).",
                Choices = new[] { new ChoiceDefinition { Id = "skills", Name = "Skills", Prompt = "Choose three skills.", Kind = ChoiceKind.Skill, Count = 3 } },
            },
            StyleFeat("archery", "Archery", "+2 to attack rolls with ranged weapons.", Combat, E(EffectKind.CombatRule, "ranged-attack-bonus", 2)),
            StyleFeat("defense", "Defense", "+1 armour class while you wear armour.", None, new RuleEffect(EffectKind.ArmorClassBonus, null, 1, null, EffectCondition.WearingArmor)),
            StyleFeat("great_weapon", "Great Weapon Fighting", "Treat 1s and 2s as 3s on two-handed weapon damage dice.", Combat, E(EffectKind.CombatRule, "great-weapon-floor", 3)),
            StyleFeat("two_weapon", "Two-Weapon Fighting", "Add your ability modifier to the damage of your off-hand attack.", Combat, E(EffectKind.CombatRule, "two-weapon-damage", 1)),
            Boon("boon_combat_prowess", "Boon of Combat Prowess", "Once per turn, turn a miss into a hit.", Combat, AnyAbility),
            Boon("boon_dimensional_travel", "Boon of Dimensional Travel", "Teleport a short distance after you attack or cast.", Combat | Explore, AnyAbility),
            Boon("boon_fate", "Boon of Fate", "Nudge a nearby creature's d20 test by 2d4.", Combat, AnyAbility),
            Boon("boon_irresistible_offense", "Boon of Irresistible Offense", "Your weapon damage ignores resistance.", Combat, new[] { Ability.Strength, Ability.Dexterity }),
            new FeatDefinition
            {
                Id = "boon_spell_recall", Name = "Boon of Spell Recall", Category = FeatCategory.EpicBoon, Needs = Magic,
                Description = "Cast one prepared low-level spell without a slot, once per long rest.",
                Prerequisites = new[] { Prerequisite.Level(19), Prerequisite.Caster() },
                Choices = new[] { Increase(1, false, 30, Ability.Intelligence, Ability.Wisdom, Ability.Charisma) },
            },
            Boon("boon_night_spirit", "Boon of the Night Spirit", "Merge with shadows: become invisible in dim light or darkness.", Explore | Combat, AnyAbility),
            Boon("boon_truesight", "Boon of Truesight", "You have truesight to 60 feet.", None, AnyAbility, new RuleEffect(EffectKind.Sense, "truesight", 60)),
        };

        private static FeatDefinition StyleFeat(string style, string name, string text, IntegrationNeed needs, params RuleEffect[] effects)
        {
            var all = new List<RuleEffect>(effects) { new RuleEffect(EffectKind.FightingStyle, style) };
            return new FeatDefinition
            {
                Id = "fs_" + style, Name = name, Category = FeatCategory.FightingStyle, Description = text, Needs = needs, Effects = all,
                Prerequisites = new[] { Prerequisite.StyleFeature(), Prerequisite.NewStyle(style) },
            };
        }

        // ----- Eldritch invocations (SRD 5.2.1) ---------------------------------------------------------

        private static ChoiceOption Inv(string id, string name, string text, IntegrationNeed needs, int level, string requires = null, params RuleEffect[] effects)
        {
            var p = new List<Prerequisite>();
            if (level > 1) p.Add(Prerequisite.Level(level));
            if (requires != null) p.Add(Prerequisite.Pick(requires));
            return new ChoiceOption(id, name, text, effects) { Needs = needs, Prerequisites = p };
        }

        private static readonly Lazy<IReadOnlyList<ChoiceOption>> InvocationList = new Lazy<IReadOnlyList<ChoiceOption>>(() => new[]
        {
            Inv("armor_of_shadows", "Armour of Shadows", "Cast Mage Armour on yourself at will.", Magic, 1),
            Inv("eldritch_mind", "Eldritch Mind", "Advantage on saves to keep concentrating.", Magic, 1),
            Inv("agonizing_blast", "Agonizing Blast", "Add your Charisma modifier to the damage of each Eldritch Blast beam (Phase 9).", Combat | Magic, 2),
            Inv("pact_of_the_blade", "Pact of the Blade", "Conjure a pact weapon and fight with Charisma.", Combat, 1),
            Inv("pact_of_the_chain", "Pact of the Chain", "Call an unusual familiar.", Magic, 1),
            Inv("pact_of_the_tome", "Pact of the Tome", "A Book of Shadows holds extra cantrips and rituals.", Magic, 1),
            Inv("devils_sight", "Devil's Sight", "See normally in darkness, magical or not, to 120 feet.", None, 2, null, new RuleEffect(EffectKind.Sense, "devils_sight", 120)),
            Inv("fiendish_vigor", "Fiendish Vigour", "Grant yourself False Life at will.", Magic, 2),
            Inv("mask_of_many_faces", "Mask of Many Faces", "Disguise yourself at will.", Magic | Talk, 2),
            Inv("misty_visions", "Misty Visions", "Cast Silent Image at will.", Magic, 2),
            Inv("otherworldly_leap", "Otherworldly Leap", "Cast Jump on yourself at will.", Magic | Explore, 2),
            Inv("ascendant_step", "Ascendant Step", "Levitate on yourself at will.", Magic | Explore, 5),
            Inv("gaze_of_two_minds", "Gaze of Two Minds", "See through a willing creature's senses.", Magic, 5),
            Inv("gift_of_the_depths", "Gift of the Depths", "Breathe water and swim at your walking speed.", Explore, 5, null, new RuleEffect(EffectKind.MovementMode, "swim")),
            Inv("master_of_myriad_forms", "Master of Myriad Forms", "Alter Self at will.", Magic, 5),
            Inv("one_with_shadows", "One with Shadows", "Turn invisible in dim light or darkness.", Magic | Explore, 5),
            Inv("thirsting_blade", "Thirsting Blade", "Your pact weapon attacks twice.", Combat, 5, "pact_of_the_blade"),
            Inv("eldritch_smite", "Eldritch Smite", "Spend a pact slot to blast a foe your pact weapon hits.", Combat, 5, "pact_of_the_blade"),
            Inv("investment_of_the_chain_master", "Investment of the Chain Master", "Your familiar grows stronger.", Magic, 5, "pact_of_the_chain"),
            Inv("whispers_of_the_grave", "Whispers of the Grave", "Speak with the dead at will.", Magic | Talk, 7),
            Inv("lifedrinker", "Lifedrinker", "Your pact weapon drains life.", Combat, 9, "pact_of_the_blade"),
            Inv("gift_of_the_protectors", "Gift of the Protectors", "Names in your book keep their bearers from death.", Magic, 9, "pact_of_the_tome"),
            Inv("visions_of_distant_realms", "Visions of Distant Realms", "Cast Arcane Eye at will.", Magic, 9),
            Inv("devouring_blade", "Devouring Blade", "Your pact weapon attacks three times.", Combat, 12, "thirsting_blade"),
            Inv("witch_sight", "Witch Sight", "You have truesight to 30 feet.", None, 15, null, new RuleEffect(EffectKind.Sense, "truesight", 30)),
        });

        private static IReadOnlyList<ChoiceOption> InvocationOptions => InvocationList.Value;

        private static readonly Lazy<IReadOnlyList<ChoiceOption>> MetamagicList = new Lazy<IReadOnlyList<ChoiceOption>>(() => new[]
        {
            Opt("careful", "Careful Spell", "Protect chosen creatures from your spell's effects.", Magic),
            Opt("distant", "Distant Spell", "Double a spell's range.", Magic),
            Opt("empowered", "Empowered Spell", "Reroll some damage dice.", Magic),
            Opt("extended", "Extended Spell", "Double a spell's duration.", Magic),
            Opt("heightened", "Heightened Spell", "Give one target disadvantage on its save.", Magic),
            Opt("quickened", "Quickened Spell", "Cast a spell as a bonus action.", Magic),
            Opt("seeking", "Seeking Spell", "Reroll a missed spell attack.", Magic),
            Opt("subtle", "Subtle Spell", "Cast without words or gestures.", Magic | Talk),
            Opt("transmuted", "Transmuted Spell", "Change a spell's damage type.", Magic),
            Opt("twinned", "Twinned Spell", "Target one more creature with a single-target spell.", Magic),
        });

        private static IReadOnlyList<ChoiceOption> MetamagicOptions => MetamagicList.Value;

        /// <summary>The level-1 invocation: only invocations with no prerequisites (rules v7).</summary>
        private static IReadOnlyList<ChoiceOption> FirstInvocations
        {
            get
            {
                var list = new List<ChoiceOption>();
                foreach (var o in InvocationOptions) if (o.Prerequisites.Count == 0) list.Add(o);
                return list;
            }
        }
    }
}
