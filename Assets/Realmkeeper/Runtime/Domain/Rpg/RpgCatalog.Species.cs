using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    public static partial class RpgCatalog
    {
        private static RuleEffect E(EffectKind kind, string key = null, int value = 0, string text = null) => new RuleEffect(kind, key, value, text);

        private static FeatureDefinition F(string id, string name, string description, params RuleEffect[] effects) => new FeatureDefinition(id, name, description, effects);

        private static Dictionary<Ability, int> Bonus(params (Ability a, int v)[] bonuses)
        {
            var d = new Dictionary<Ability, int>();
            foreach (var (a, v) in bonuses) d[a] = v;
            return d;
        }

        private static FeatureDefinition Darkvision(int feet) =>
            F("darkvision", "Darkvision", $"You see in dim light within {feet} feet as if it were bright, and in darkness as if it were dim light (shades of grey only).",
                E(EffectKind.Darkvision, value: feet));

        // Descriptions and lore are Realmkeeper's own. Mechanics follow the source named on each species;
        // ability increases follow the SRD 5.1 configuration (species grant them) and are Realmkeeper's own
        // where the source species has none.
        private static IReadOnlyList<SpeciesDefinition> BuildSpecies() => new[]
        {
            new SpeciesDefinition
            {
                Id = "human", Name = "Human", Source = ContentSource.Srd521,
                Description = "Short-lived, adaptable and everywhere: the most numerous people of the realm, found in every trade and on every road.",
                Lore = "Humans built most of the realm's towns and roads, and most of its quarrels. They have no single homeland or creed; what they share is a restlessness that turns villages into towns within a few generations.",
                Speed = 30, Languages = new[] { "common" },
                AbilityBonuses = Bonus((Ability.Strength, 1), (Ability.Dexterity, 1), (Ability.Constitution, 1), (Ability.Intelligence, 1), (Ability.Wisdom, 1), (Ability.Charisma, 1)),
                Traits = new[] { F("skillful", "Skillful", "You are proficient in one skill of your choice.") },
                Choices = new[] { new ChoiceDefinition { Id = "species:skillful", Name = "Skillful", Prompt = "Choose one skill.", Kind = ChoiceKind.Skill, Count = 1 } },
                Appearance = new SpeciesAppearance { HasFinishedModel = true, HeightScale = 1f },
            },
            new SpeciesDefinition
            {
                Id = "high_elf", Name = "High Elf", Source = ContentSource.Srd521,
                Description = "Long-lived and studious, keepers of old learning who trust patience over haste.",
                Lore = "High elves count their lives in centuries and their debts in generations. Their libraries and observatories are older than most kingdoms, and a high elf may remember a town's founding that its people have long forgotten.",
                Speed = 30, Languages = new[] { "common", "elvish" },
                AbilityBonuses = Bonus((Ability.Dexterity, 2), (Ability.Intelligence, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("keen_senses", "Keen Senses", "You are proficient in Perception.", E(EffectKind.SkillProficiency, nameof(Skill.Perception))),
                    F("fey_ancestry", "Fey Ancestry", "You have advantage on saving throws to avoid or end being charmed.", E(EffectKind.SaveAdvantage, "charmed")),
                    F("trance", "Trance", "You do not need sleep; four hours of meditation give you the rest others take eight hours to gain.", E(EffectKind.CombatRule, "trance-rest", 4, "Long rest in 4 hours")),
                    F("elven_cantrip", "Elven Lineage", "You know one minor spell of arcane learning (chosen when magic arrives in a later phase).", E(EffectKind.InnateMagic, "arcane-cantrip")),
                },
                Appearance = new SpeciesAppearance { HeightScale = 1.0f, Features = new[] { "pointed ears", "fine features" } },
            },
            new SpeciesDefinition
            {
                Id = "wood_elf", Name = "Wood Elf", Source = ContentSource.Srd521,
                Description = "Swift and watchful elves of the deep woods, at home under the canopy.",
                Lore = "Wood elves keep to forests the realm's maps leave blank. They trade little and travel far, and the few who come to the towns are usually looking for someone, or running from something.",
                Speed = 30, Languages = new[] { "common", "elvish" },
                AbilityBonuses = Bonus((Ability.Dexterity, 2), (Ability.Wisdom, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("fleet_of_foot", "Fleet of Foot", "Your walking speed increases by 5 feet.", E(EffectKind.SpeedBonus, value: 5)),
                    F("keen_senses", "Keen Senses", "You are proficient in Perception.", E(EffectKind.SkillProficiency, nameof(Skill.Perception))),
                    F("fey_ancestry", "Fey Ancestry", "You have advantage on saving throws to avoid or end being charmed.", E(EffectKind.SaveAdvantage, "charmed")),
                    F("mask_of_the_wild", "Mask of the Wild", "You can try to hide when only lightly obscured by foliage, rain or mist.", E(EffectKind.CombatRule, "hide-in-natural-cover", 0, "Hide when lightly obscured by nature")),
                },
                Appearance = new SpeciesAppearance { HeightScale = 0.98f, Features = new[] { "pointed ears", "weathered skin tones" } },
            },
            new SpeciesDefinition
            {
                Id = "dwarf", Name = "Dwarf", Source = ContentSource.Srd51,
                Description = "Stocky, stubborn and hard to kill; miners, smiths and keepers of grudges.",
                Lore = "Dwarves dig where others build. Their halls run under the realm's hills, and their smiths set the standard every forge is measured against. A dwarf's word is binding, and so is a dwarf's resentment.",
                Speed = 25, Languages = new[] { "common", "dwarvish" },
                AbilityBonuses = Bonus((Ability.Constitution, 2), (Ability.Wisdom, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("dwarven_resilience", "Dwarven Resilience", "You resist poison damage and have advantage on saving throws against being poisoned.",
                        E(EffectKind.Resistance, "poison"), E(EffectKind.SaveAdvantage, "poisoned")),
                    F("dwarven_toughness", "Dwarven Toughness", "Your hit point maximum increases by 1, and by 1 again at every level.", E(EffectKind.MaxHpPerLevel, value: 1)),
                    F("dwarven_training", "Dwarven Training", "You are proficient with battleaxes, handaxes, light hammers, warhammers and smith's tools.",
                        E(EffectKind.WeaponProficiency, "battleaxe"), E(EffectKind.WeaponProficiency, "handaxe"), E(EffectKind.WeaponProficiency, "light hammer"),
                        E(EffectKind.WeaponProficiency, "warhammer"), E(EffectKind.ToolProficiency, "smiths_tools")),
                },
                Appearance = new SpeciesAppearance { HeightScale = 0.8f, Features = new[] { "broad stocky build", "heavy brow" } },
            },
            new SpeciesDefinition
            {
                Id = "halfling", Name = "Halfling", Size = CreatureSize.Small, Source = ContentSource.Srd51,
                Description = "Small, quick and unflappable; luck seems to follow them.",
                Lore = "Halflings live in the realm's quieter corners: millers, innkeepers, carters. They are rarely noticed and seldom caught off guard, and they have a talent for walking away from disasters that flatten everyone else.",
                Speed = 25, Languages = new[] { "common", "halfling" },
                AbilityBonuses = Bonus((Ability.Dexterity, 2), (Ability.Charisma, 1)),
                Traits = new[]
                {
                    F("lucky", "Lucky", "When you roll a 1 on a d20 test, you can reroll it and must use the new roll.", E(EffectKind.CombatRule, "reroll-natural-one", 1, "Reroll 1s on d20 tests")),
                    F("brave", "Brave", "You have advantage on saving throws against being frightened.", E(EffectKind.SaveAdvantage, "frightened")),
                    F("nimble", "Halfling Nimbleness", "You can move through the space of any creature larger than you.", E(EffectKind.CombatRule, "move-through-larger", 0, "Move through larger creatures")),
                },
                Appearance = new SpeciesAppearance { HeightScale = 0.62f, Features = new[] { "small frame", "large bare feet" } },
            },
            new SpeciesDefinition
            {
                Id = "gnome", Name = "Gnome", Size = CreatureSize.Small, Source = ContentSource.Srd51,
                Description = "Small, curious and inventive, with minds that resist meddling.",
                Lore = "Gnomes tinker. Clocks, locks, toys, pumps: give a gnome an afternoon and a broken thing and you get back two working ones and a question you had not thought to ask.",
                Speed = 25, Languages = new[] { "common", "gnomish" },
                AbilityBonuses = Bonus((Ability.Intelligence, 2), (Ability.Constitution, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("gnome_cunning", "Gnomish Cunning", "You have advantage on Intelligence, Wisdom and Charisma saving throws against magic.", E(EffectKind.SaveAdvantage, "magic-mental")),
                    F("gnomish_lineage", "Gnomish Lineage", "Your family's line decides a small gift: the forest gnomes' way with beasts and illusion, or the rock gnomes' knack for small mechanisms."),
                },
                Choices = new[]
                {
                    // Phase 20 (rules 9): the SRD 5.2.1 lineages. Saves from before choose "rock", which is what a gnome was then.
                    new ChoiceDefinition
                    {
                        Id = "species:gnome_lineage", Name = "Gnomish Lineage", Prompt = "Choose your gnomish line.", Kind = ChoiceKind.Option, Count = 1, IntroducedInRules = 9,
                        Options = new[]
                        {
                            new ChoiceOption("forest", "Forest gnome", "You know a minor illusion and can speak with small beasts (innate magic: usable when innate spells arrive).",
                                E(EffectKind.InnateMagic, "minor-illusion"), E(EffectKind.InnateMagic, "speak-with-small-beasts")) { Needs = IntegrationNeed.Magic },
                            new ChoiceOption("rock", "Rock gnome", "You are proficient with tinker's tools and know the small tricks of mending and making (the tricks are innate magic, usable when innate spells arrive).",
                                E(EffectKind.ToolProficiency, "tinkers_tools"), E(EffectKind.InnateMagic, "mending")) { Needs = IntegrationNeed.Magic },
                        },
                    },
                },
                Appearance = new SpeciesAppearance { HeightScale = 0.6f, Features = new[] { "small frame", "large nose and ears" } },
            },
            new SpeciesDefinition
            {
                Id = "orc", Name = "Orc", Source = ContentSource.Srd521,
                Description = "Strong, enduring and hard to put down, with eyes made for the dark.",
                Lore = "Orc clans hold the realm's high moors and border hills. Outsiders call them raiders; the clans call the lowlanders thieves of grazing. Many orcs now hire on as guards and drovers, valued for their stamina.",
                Speed = 30, Languages = new[] { "common", "orc" },
                AbilityBonuses = Bonus((Ability.Strength, 2), (Ability.Constitution, 1)),
                Traits = new[]
                {
                    Darkvision(120),
                    F("adrenaline_rush", "Adrenaline Rush", "As a bonus action you can dash and gain temporary hit points equal to your proficiency bonus, a number of times equal to your proficiency bonus per long rest.",
                        E(EffectKind.Resource, "adrenaline_rush")),
                    F("relentless_endurance", "Relentless Endurance", "When reduced to 0 hit points but not killed, you can drop to 1 instead, once per long rest.",
                        E(EffectKind.Resource, "relentless_endurance")),
                },
                Resources = new[]
                {
                    new ResourceDefinition { Id = "adrenaline_rush", Name = "Adrenaline Rush", Formula = ResourceFormula.ProficiencyBonus, Recovery = ResourceRecovery.LongRest },
                    new ResourceDefinition { Id = "relentless_endurance", Name = "Relentless Endurance", Formula = ResourceFormula.Fixed, Value = 1, Recovery = ResourceRecovery.LongRest },
                },
                Appearance = new SpeciesAppearance { HeightScale = 1.06f, Features = new[] { "tusks", "grey-green skin tones", "heavy frame" } },
            },
            new SpeciesDefinition
            {
                Id = "half_orc", Name = "Half-Orc", Source = ContentSource.Srd51,
                Description = "Of orc and human blood: imposing, tough and hard to break.",
                Lore = "Half-orcs belong fully to neither clan nor town, and many make their own way as soldiers, smiths or caravan guards. A half-orc's reputation is usually earned twice: once against suspicion and once for real.",
                Speed = 30, Languages = new[] { "common", "orc" },
                AbilityBonuses = Bonus((Ability.Strength, 2), (Ability.Constitution, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("menacing", "Menacing", "You are proficient in Intimidation.", E(EffectKind.SkillProficiency, nameof(Skill.Intimidation))),
                    F("relentless_endurance", "Relentless Endurance", "When reduced to 0 hit points but not killed, you can drop to 1 instead, once per long rest.",
                        E(EffectKind.Resource, "relentless_endurance")),
                    F("savage_attacks", "Savage Attacks", "When you score a critical hit with a melee weapon, roll one of its damage dice once more and add it.",
                        E(EffectKind.CombatRule, "critical-extra-die", 1, "One extra weapon die on melee critical hits")),
                },
                Resources = new[] { new ResourceDefinition { Id = "relentless_endurance", Name = "Relentless Endurance", Formula = ResourceFormula.Fixed, Value = 1, Recovery = ResourceRecovery.LongRest } },
                Appearance = new SpeciesAppearance { HeightScale = 1.03f, Features = new[] { "small tusks", "greyish skin tones" } },
            },
            new SpeciesDefinition
            {
                Id = "tiefling", Name = "Tiefling", Source = ContentSource.Srd51,
                Description = "Marked by a fiendish inheritance: horns, a tail, and a resistance that depends on the legacy in their blood.",
                Lore = "No one chooses to be born a tiefling. Somewhere in the bloodline a pact was made, and the marks show generations later. Tieflings are met with suspicion in most towns and learn early to rely on themselves.",
                Speed = 30, Languages = new[] { "common", "infernal" },
                AbilityBonuses = Bonus((Ability.Charisma, 2), (Ability.Intelligence, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("fiendish_legacy", "Fiendish Legacy", "The power in your bloodline decides the harm you shrug off and the minor magic you were born knowing."),
                },
                Choices = new[]
                {
                    // Phase 20 (rules 9): the SRD 5.2.1 legacies. Saves from before choose "infernal", which is what a tiefling was then.
                    new ChoiceDefinition
                    {
                        Id = "species:fiendish_legacy", Name = "Fiendish Legacy", Prompt = "Choose the legacy in your blood.", Kind = ChoiceKind.Option, Count = 1, IntroducedInRules = 9,
                        Options = new[]
                        {
                            new ChoiceOption("abyssal", "Abyssal", "You resist poison damage, and know a spray of poison (innate magic: usable when innate spells arrive).",
                                E(EffectKind.Resistance, "poison"), E(EffectKind.InnateMagic, "poison-spray")) { Needs = IntegrationNeed.Magic },
                            new ChoiceOption("chthonic", "Chthonic", "You resist necrotic damage, and know a chilling, deathly touch (innate magic: usable when innate spells arrive).",
                                E(EffectKind.Resistance, "necrotic"), E(EffectKind.InnateMagic, "chill-touch")) { Needs = IntegrationNeed.Magic },
                            new ChoiceOption("infernal", "Infernal", "You resist fire damage, and know a bolt of fire (innate magic: usable when innate spells arrive).",
                                E(EffectKind.Resistance, "fire"), E(EffectKind.InnateMagic, "fire-bolt")) { Needs = IntegrationNeed.Magic },
                        },
                    },
                },
                Appearance = new SpeciesAppearance { HeightScale = 1f, Features = new[] { "horns", "tail", "red to violet skin tones", "solid-colour eyes" } },
            },
            new SpeciesDefinition
            {
                Id = "dragonborn", Name = "Dragonborn", Source = ContentSource.Srd51,
                Description = "Proud, scaled people with the breath and the resilience of a draconic ancestor.",
                Lore = "Dragonborn trace their lines to dragons they name with reverence and never describe the same way twice. They live by codes of honour that outsiders find rigid and they find simply obvious.",
                Speed = 30, Languages = new[] { "common", "draconic" },
                AbilityBonuses = Bonus((Ability.Strength, 2), (Ability.Charisma, 1)),
                Traits = new[]
                {
                    F("draconic_ancestry", "Draconic Ancestry", "Your ancestor's kind decides your breath weapon's damage and the damage you resist."),
                    F("breath_weapon", "Breath Weapon", "You exhale destructive energy in a line or cone, once per short rest.",
                        E(EffectKind.Resource, "breath_weapon"), E(EffectKind.CombatRule, "breath-weapon", 2, "2d6 damage, Dexterity save halves")),
                },
                Choices = new[]
                {
                    new ChoiceDefinition
                    {
                        Id = "species:ancestry", Name = "Draconic Ancestry", Prompt = "Choose your ancestor's kind.", Kind = ChoiceKind.Option, Count = 1,
                        Options = new[]
                        {
                            new ChoiceOption("fire", "Red or gold (fire)", "Fire breath; fire resistance.", E(EffectKind.Resistance, "fire")),
                            new ChoiceOption("cold", "White or silver (cold)", "Cold breath; cold resistance.", E(EffectKind.Resistance, "cold")),
                            new ChoiceOption("lightning", "Blue or bronze (lightning)", "Lightning breath; lightning resistance.", E(EffectKind.Resistance, "lightning")),
                            new ChoiceOption("acid", "Black or copper (acid)", "Acid breath; acid resistance.", E(EffectKind.Resistance, "acid")),
                            new ChoiceOption("poison", "Green (poison)", "Poison breath; poison resistance.", E(EffectKind.Resistance, "poison")),
                        },
                    },
                },
                Resources = new[] { new ResourceDefinition { Id = "breath_weapon", Name = "Breath Weapon", Formula = ResourceFormula.Fixed, Value = 1, Recovery = ResourceRecovery.ShortRest } },
                Appearance = new SpeciesAppearance { HeightScale = 1.08f, AllowsBeard = false, Features = new[] { "scaled skin", "draconic head", "no hair" } },
            },
            new SpeciesDefinition
            {
                // Not in any SRD: Realmkeeper's own definition, text and mechanics (name flagged for IP review).
                Id = "aasimar", Name = "Aasimar", Source = ContentSource.RealmkeeperOriginal,
                Description = "Mortals touched by a celestial power: a faint inner light and an instinct to mend.",
                Lore = "In some families a child is born with eyes that catch the lamplight too well and a calm no one taught them. Shrines claim such children; most grow up to choose for themselves what that light is for.",
                Speed = 30, Languages = new[] { "common", "celestial" },
                AbilityBonuses = Bonus((Ability.Charisma, 2), (Ability.Wisdom, 1)),
                Traits = new[]
                {
                    Darkvision(60),
                    F("celestial_resistance", "Celestial Resistance", "You resist radiant and necrotic damage.", E(EffectKind.Resistance, "radiant"), E(EffectKind.Resistance, "necrotic")),
                    F("mending_touch", "Mending Touch", "Once per long rest you can touch a creature and restore hit points equal to your level plus your Charisma modifier (at least 1).",
                        E(EffectKind.Resource, "mending_touch"), E(EffectKind.CombatRule, "mending-touch", 1, "Heal level + Charisma")),
                    F("inner_light", "Inner Light", "You can make yourself shed a dim glow, enough to read by (a light source when magic arrives).", E(EffectKind.InnateMagic, "inner-light")),
                },
                Resources = new[] { new ResourceDefinition { Id = "mending_touch", Name = "Mending Touch", Formula = ResourceFormula.Fixed, Value = 1, Recovery = ResourceRecovery.LongRest } },
                Appearance = new SpeciesAppearance { HeightScale = 1.02f, Features = new[] { "luminous eyes", "faint skin glow" } },
            },
            new SpeciesDefinition
            {
                Id = "goliath", Name = "Goliath", Source = ContentSource.Srd521,
                Description = "Towering mountain folk with a giant's strength and a climber's endurance.",
                Lore = "Goliaths come down from the high passes to trade hides and hire out their strength. They keep score of everything (loads carried, peaks climbed, debts paid) and respect anyone who keeps score honestly.",
                Speed = 35, Languages = new[] { "common", "giant" },
                AbilityBonuses = Bonus((Ability.Strength, 2), (Ability.Constitution, 1)),
                Traits = new[]
                {
                    F("powerful_build", "Powerful Build", "You count as one size larger for what you can carry: you carry twice as much before it slows you.", E(EffectKind.CarryCapacityPercent, value: 200)),
                    F("giant_ancestry", "Giant Ancestry", "A giant's gift runs in your line; you can call on it a number of times equal to your proficiency bonus per long rest.",
                        E(EffectKind.Resource, "stones_endurance")),
                    F("mountain_born", "Mountain Born", "You are proficient in Athletics.", E(EffectKind.SkillProficiency, nameof(Skill.Athletics))),
                },
                Choices = new[]
                {
                    // Phase 20 (rules 9): the SRD 5.2.1 ancestries. Saves from before choose "stone", which is what a goliath was then.
                    // None of the six boons is played by the combat engine yet; each is flagged, and the sheet says so.
                    new ChoiceDefinition
                    {
                        Id = "species:giant_ancestry", Name = "Giant Ancestry", Prompt = "Choose the giant in your line.", Kind = ChoiceKind.Option, Count = 1, IntroducedInRules = 9,
                        Options = new[]
                        {
                            new ChoiceOption("cloud", "Cloud giant: Cloud's Jaunt", "As a bonus action, you vanish and reappear up to 30 feet away.",
                                E(EffectKind.CombatRule, "teleport", 30, "Bonus action: teleport 30 ft")) { Needs = IntegrationNeed.Combat },
                            new ChoiceOption("fire", "Fire giant: Fire's Burn", "When you hit with an attack, you can add 1d10 fire damage.",
                                E(EffectKind.CombatRule, "extra-fire-damage", 10, "On a hit: +1d10 fire")) { Needs = IntegrationNeed.Combat },
                            new ChoiceOption("frost", "Frost giant: Frost's Chill", "When you hit, you can add 1d6 cold damage and slow the target by 10 feet until your next turn.",
                                E(EffectKind.CombatRule, "extra-cold-slow", 6, "On a hit: +1d6 cold, speed −10 ft")) { Needs = IntegrationNeed.Combat },
                            new ChoiceOption("hill", "Hill giant: Hill's Tumble", "When you hit a Large or smaller creature, you can knock it prone.",
                                E(EffectKind.CombatRule, "knock-prone", 0, "On a hit: knock Large or smaller prone")) { Needs = IntegrationNeed.Combat },
                            new ChoiceOption("stone", "Stone giant: Stone's Endurance", "When you take damage, you can use your reaction to reduce it by 1d12 + your Constitution modifier.",
                                E(EffectKind.CombatRule, "reduce-damage", 12, "Reduce damage by 1d12 + Constitution")) { Needs = IntegrationNeed.Combat },
                            new ChoiceOption("storm", "Storm giant: Storm's Thunder", "When a creature within 60 feet damages you, you can use your reaction to deal it 1d8 thunder damage.",
                                E(EffectKind.CombatRule, "retaliate-thunder", 8, "Reaction: 1d8 thunder to an attacker within 60 ft")) { Needs = IntegrationNeed.Combat },
                        },
                    },
                },
                Resources = new[] { new ResourceDefinition { Id = "stones_endurance", Name = "Giant Ancestry", Formula = ResourceFormula.ProficiencyBonus, Recovery = ResourceRecovery.LongRest } },
                Appearance = new SpeciesAppearance { HeightScale = 1.22f, Features = new[] { "very tall frame", "stony skin patterning" } },
            },
        };
    }
}
