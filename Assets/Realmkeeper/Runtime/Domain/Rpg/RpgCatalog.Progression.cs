using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    // Phase 7: class progression for levels 1–20. Numbers (feature levels, uses, dice, slots, cantrips,
    // prepared spells) follow the SRD 5.2.1 class tables (Wizards of the Coast LLC, CC BY 4.0); the wording
    // is Realmkeeper's own. Features that need combat, magic, rests, dialogue or exploration systems are
    // acquired, validated and shown now, and say what they wait for (IntegrationNeed) instead of faking it.
    public static partial class RpgCatalog
    {
        private const IntegrationNeed Combat = IntegrationNeed.Combat;
        private const IntegrationNeed Magic = IntegrationNeed.Magic;
        private const IntegrationNeed Rest = IntegrationNeed.Rest;
        private const IntegrationNeed Explore = IntegrationNeed.Exploration;
        private const IntegrationNeed Talk = IntegrationNeed.Dialogue;
        private const IntegrationNeed None = IntegrationNeed.None;

        private static FeatureDefinition LF(string id, string name, string text, FeatureKind kind, IntegrationNeed needs, params RuleEffect[] effects) =>
            new FeatureDefinition(id, name, text, effects) { Kind = kind, Needs = needs };

        private static RuleEffect EC(EffectKind kind, string key, int value, EffectCondition condition, string text = null, int limit = 0) =>
            new RuleEffect(kind, key, value, text, condition, limit);

        private static FeatureDefinition Rep(FeatureDefinition f, string replaces)
        {
            f.Replaces = replaces;
            return f;
        }

        private static FeatureDefinition Sc(FeatureDefinition f, string scaling)
        {
            f.ScalingId = scaling;
            return f;
        }

        private static RuleEffect Inc(Ability a, int amount, int limit) => new RuleEffect(EffectKind.AbilityIncrease, a.ToString(), amount, null, EffectCondition.Always, limit);

        private static IReadOnlyDictionary<int, IReadOnlyList<FeatureDefinition>> Levels(params (int level, FeatureDefinition feature)[] items)
        {
            var map = new Dictionary<int, List<FeatureDefinition>>();
            foreach (var (level, f) in items)
            {
                if (!map.TryGetValue(level, out var list)) map[level] = list = new List<FeatureDefinition>();
                list.Add(f);
            }

            var result = new Dictionary<int, IReadOnlyList<FeatureDefinition>>();
            foreach (var pair in map) result[pair.Key] = pair.Value;
            return result;
        }

        private static ResourceDefinition RT(string id, string name, ResourceRecovery recovery, string description, params int[] table) =>
            new ResourceDefinition { Id = id, Name = name, Formula = ResourceFormula.Table, Table = table, Recovery = recovery, Description = description };

        private static ChoiceDefinition Expertise(int level, int count, string id = "expertise", params Skill[] from) => new ChoiceDefinition
        {
            Id = $"lv{level}:{id}", Level = level, Name = "Expertise", Prompt = $"Choose {count} skill{(count == 1 ? string.Empty : "s")} you are proficient in to double your proficiency bonus.",
            Kind = ChoiceKind.Expertise, Count = count, Skills = from,
        };

        private static ChoiceDefinition FightingStyleFeat(int level, string id = "fighting_style") => new ChoiceDefinition
        {
            Id = $"lv{level}:{id}", Level = level, Name = "Fighting Style", Prompt = "Choose a Fighting Style feat.", Kind = ChoiceKind.Feat, FeatCategories = FeatCategory.FightingStyle,
        };

        private static ChoiceDefinition Invocations(int level, int count) => new ChoiceDefinition
        {
            Id = level == 1 ? "class:invocations" : $"lv{level}:invocations", Level = level, Name = "Eldritch Invocations",
            Prompt = $"Choose {count} invocation{(count == 1 ? string.Empty : "s")}.", Kind = ChoiceKind.Option, Count = count, Pool = "invocation",
            Options = level == 1 ? FirstInvocations : InvocationOptions, IntroducedInRules = level == 1 ? 7 : 6,
        };

        private static ChoiceDefinition Metamagic(int level) => new ChoiceDefinition
        {
            Id = $"lv{level}:metamagic", Level = level, Name = "Metamagic", Prompt = "Choose two Metamagic options.", Kind = ChoiceKind.Option, Count = 2, Pool = "metamagic", Options = MetamagicOptions,
        };

        // ----- Spell slot tables (SRD 5.2.1) ----------------------------------------------------------

        private static readonly int[][] FullCasterSlots =
        {
            new[] { 2 }, new[] { 3 }, new[] { 4, 2 }, new[] { 4, 3 }, new[] { 4, 3, 2 }, new[] { 4, 3, 3 }, new[] { 4, 3, 3, 1 }, new[] { 4, 3, 3, 2 },
            new[] { 4, 3, 3, 3, 1 }, new[] { 4, 3, 3, 3, 2 }, new[] { 4, 3, 3, 3, 2, 1 }, new[] { 4, 3, 3, 3, 2, 1 }, new[] { 4, 3, 3, 3, 2, 1, 1 },
            new[] { 4, 3, 3, 3, 2, 1, 1 }, new[] { 4, 3, 3, 3, 2, 1, 1, 1 }, new[] { 4, 3, 3, 3, 2, 1, 1, 1 }, new[] { 4, 3, 3, 3, 2, 1, 1, 1, 1 },
            new[] { 4, 3, 3, 3, 3, 1, 1, 1, 1 }, new[] { 4, 3, 3, 3, 3, 2, 1, 1, 1 }, new[] { 4, 3, 3, 3, 3, 2, 2, 1, 1 },
        };

        /// <summary>Spell slots of one slot level at a class level (the half-caster table is the full table at half level, rounded up).</summary>
        public static int SlotsAt(CasterProgression progression, int classLevel, int slotLevel)
        {
            if (progression != CasterProgression.Full && progression != CasterProgression.Half) return 0;
            var row = progression == CasterProgression.Full ? classLevel : (classLevel + 1) / 2;
            var slots = FullCasterSlots[Math.Max(1, Math.Min(20, row)) - 1];
            return slotLevel >= 1 && slotLevel <= slots.Length ? slots[slotLevel - 1] : 0;
        }

        private static readonly int[] PactSlots = { 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 };
        private static readonly int[] PactSlotLevel = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };

        private static readonly int[] PreparedFull = { 4, 5, 6, 7, 9, 10, 11, 12, 14, 15, 16, 16, 17, 17, 18, 18, 19, 20, 21, 22 };
        private static readonly int[] PreparedHalf = { 2, 3, 4, 5, 6, 6, 7, 7, 9, 9, 10, 10, 11, 11, 12, 12, 14, 14, 15, 15 };

        private static int[] Steps(int first, params (int fromLevel, int value)[] steps)
        {
            var t = new int[20];
            for (var level = 1; level <= 20; level++)
            {
                var v = first;
                foreach (var (from, value) in steps) if (level >= from) v = value;
                t[level - 1] = v;
            }

            return t;
        }

        private static int[] FromLevelEqualsLevel(int from)
        {
            var t = new int[20];
            for (var level = from; level <= 20; level++) t[level - 1] = level;
            return t;
        }

        /// <summary>Spell slot resources for a caster (one resource per slot level, or the warlock's pact slots).</summary>
        private static IEnumerable<ResourceDefinition> SlotResources(SpellcastingDefinition sc)
        {
            if (sc.Progression == CasterProgression.Pact)
            {
                yield return RT("pact_slots", "Pact slots", ResourceRecovery.ShortRest, "Warlock spell slots, all of the pact slot level, back after a short rest.", PactSlots);
                yield break;
            }

            var top = sc.Progression == CasterProgression.Full ? 9 : 5;
            for (var slot = 1; slot <= top; slot++)
            {
                var table = new int[20];
                for (var level = 1; level <= 20; level++) table[level - 1] = SlotsAt(sc.Progression, level, slot);
                yield return RT("spell_slots_" + slot, Ordinal(slot) + "-level spell slots", ResourceRecovery.LongRest, "Spent to cast spells of this level or lower (spells arrive with the magic phase).", table);
            }
        }

        private static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        // ----- Progression per class ------------------------------------------------------------------

        private static void ApplyProgression(ClassDefinition c)
        {
            var resources = new List<ResourceDefinition>();
            foreach (var r in c.Resources) if (!r.Id.StartsWith("spell_slots_", StringComparison.Ordinal)) resources.Add(r);
            var choices = new List<ChoiceDefinition>();

            switch (c.Id)
            {
                case "barbarian":
                    Replace(resources, RT("rage", "Rage", ResourceRecovery.LongRest, "Enter a rage: bonus damage and resistance to physical damage for a minute. One use returns on a short rest.",
                        2, 2, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6));
                    c.Scalings = new[]
                    {
                        new ScalingDefinition("rage_damage", "Rage damage", "+{0}", Steps(2, (9, 3), (16, 4))),
                        new ScalingDefinition("weapon_mastery", "Weapon masteries", "{0}", Steps(2, (4, 3), (10, 4))),
                    };
                    c.SubclassFeatureLevels = new[] { 3, 6, 10, 14 };
                    c.LaterFeatures = Levels(
                        (2, LF("danger_sense", "Danger Sense", "You sense trouble coming: advantage on Dexterity saving throws unless you are incapacitated.", FeatureKind.Passive, None,
                            new RuleEffect(EffectKind.Advantage, "save:Dexterity", 0, "unless incapacitated"))),
                        (2, LF("reckless_attack", "Reckless Attack", "Throw caution aside: advantage on your Strength attacks this turn, and attacks against you have advantage too.", FeatureKind.Active, Combat)),
                        (3, LF("primal_knowledge", "Primal Knowledge", "You gain another Barbarian skill, and while raging you can use Strength for some checks.", FeatureKind.Choice, Combat)),
                        (5, LF("extra_attack", "Extra Attack", "You attack twice when you take the Attack action.", FeatureKind.Passive, Combat)),
                        (5, LF("fast_movement", "Fast Movement", "Your speed rises by 10 feet while you are not wearing heavy armour.", FeatureKind.Passive, None,
                            EC(EffectKind.SpeedBonus, null, 10, EffectCondition.NoHeavyArmor))),
                        (7, LF("feral_instinct", "Feral Instinct", "Your instincts are sharp: advantage on initiative rolls.", FeatureKind.Passive, None, new RuleEffect(EffectKind.Advantage, "initiative"))),
                        (7, LF("instinctive_pounce", "Instinctive Pounce", "When you rage you can move half your speed.", FeatureKind.Passive, Combat)),
                        (9, Sc(LF("brutal_strike", "Brutal Strike", "Give up advantage on a reckless attack to add a die of damage and a brutal effect.", FeatureKind.Active, Combat), "rage_damage")),
                        (11, LF("relentless_rage", "Relentless Rage", "Your rage keeps you standing when you would drop to 0 hit points.", FeatureKind.Passive, Combat)),
                        (13, LF("improved_brutal_strike", "Improved Brutal Strike", "New brutal effects: staggering and sundering blows.", FeatureKind.Passive, Combat)),
                        (15, LF("persistent_rage", "Persistent Rage", "Your rage lasts its full span unless you end it, and rolling initiative can restore your rages.", FeatureKind.Passive, Combat | Rest)),
                        (17, Rep(LF("improved_brutal_strike_17", "Improved Brutal Strike (greater)", "Brutal Strike deals two extra dice and applies two effects.", FeatureKind.Passive, Combat), "improved_brutal_strike")),
                        (18, LF("indomitable_might", "Indomitable Might", "A Strength check or save that totals less than your Strength score uses the score instead.", FeatureKind.Passive, Combat | Explore)),
                        (20, LF("primal_champion", "Primal Champion", "Your Strength and Constitution rise by 4, to a maximum of 25.", FeatureKind.Passive, None,
                            Inc(Ability.Strength, 4, 25), Inc(Ability.Constitution, 4, 25))));
                    choices.Add(new ChoiceDefinition
                    {
                        Id = "lv3:primal_knowledge", Level = 3, Name = "Primal Knowledge", Prompt = "Choose another Barbarian skill.", Kind = ChoiceKind.Skill, Count = 1, Skills = c.SkillOptions,
                    });
                    break;

                case "bard":
                    Replace(resources, new ResourceDefinition
                    {
                        Id = "bardic_inspiration", Name = "Bardic Inspiration", Formula = ResourceFormula.AbilityModifierMin1, Ability = Ability.Charisma,
                        Recovery = ResourceRecovery.LongRest, ShortRestFromLevel = 5, Description = "Give an ally an inspiration die.",
                    });
                    c.Scalings = new[] { new ScalingDefinition("bardic_die", "Bardic Inspiration die", "d{0}", Steps(6, (5, 8), (10, 10), (15, 12))) };
                    c.SubclassFeatureLevels = new[] { 3, 6, 14 };
                    c.LaterFeatures = Levels(
                        (2, LF("expertise_2", "Expertise", "Two of your skill proficiencies count double.", FeatureKind.Choice, None)),
                        (2, LF("jack_of_all_trades", "Jack of All Trades", "Half your proficiency bonus (rounded down) applies to ability checks you are not proficient in.", FeatureKind.Passive, None,
                            new RuleEffect(EffectKind.HalfProficiencyUntrained))),
                        (5, LF("font_of_inspiration", "Font of Inspiration", "Your Bardic Inspiration returns on a short rest as well, and you can trade a spell slot for a use.", FeatureKind.Passive, Rest | Magic)),
                        (7, LF("countercharm", "Countercharm", "Your performance helps allies shake off charm and fear.", FeatureKind.Active, Combat)),
                        (9, LF("expertise_9", "Expertise", "Two more of your skill proficiencies count double.", FeatureKind.Choice, None)),
                        (10, LF("magical_secrets", "Magical Secrets", "You may prepare spells from the cleric, druid and wizard lists too.", FeatureKind.Passive, Magic)),
                        (18, LF("superior_inspiration", "Superior Inspiration", "Rolling initiative restores your Bardic Inspiration to at least two uses.", FeatureKind.Passive, Combat)),
                        (20, LF("words_of_creation", "Words of Creation", "Your words of life and death can touch two creatures at once.", FeatureKind.Passive, Magic)));
                    choices.Add(Expertise(2, 2));
                    choices.Add(Expertise(9, 2));
                    break;

                case "cleric":
                    resources.Add(RT("channel_divinity", "Channel Divinity", ResourceRecovery.LongRest, "Channel divine power (Divine Spark, Turn Undead). One use returns on a short rest.",
                        Steps(0, (2, 2), (6, 3), (18, 4))));
                    c.SubclassFeatureLevels = new[] { 3, 6, 17 };
                    c.LaterFeatures = Levels(
                        (2, LF("channel_divinity", "Channel Divinity", "Channel divine power to heal or harm (Divine Spark) or to turn the undead.", FeatureKind.LimitedUse, Magic | Combat)),
                        (5, LF("sear_undead", "Sear Undead", "Turning the undead also burns them with radiance.", FeatureKind.Passive, Combat)),
                        (7, LF("blessed_strikes", "Blessed Strikes", "Divine power empowers your weapon strikes or your cantrips (your choice).", FeatureKind.Choice, Combat | Magic)),
                        (10, LF("divine_intervention", "Divine Intervention", "Call on your deity to cast a spell without a slot, once per long rest.", FeatureKind.LimitedUse, Magic)),
                        (14, LF("improved_blessed_strikes", "Improved Blessed Strikes", "Your Blessed Strikes option grows stronger.", FeatureKind.Passive, Combat | Magic)),
                        (20, Rep(LF("greater_divine_intervention", "Greater Divine Intervention", "Divine Intervention can now call down the mightiest of wishes.", FeatureKind.LimitedUse, Magic), "divine_intervention")));
                    choices.Add(new ChoiceDefinition
                    {
                        Id = "lv7:blessed_strikes", Level = 7, Name = "Blessed Strikes", Prompt = "Choose how divine power flows through you.", Kind = ChoiceKind.Option,
                        Options = new[]
                        {
                            new ChoiceOption("divine_strike", "Divine Strike", "Once per turn your weapon hits deal an extra 1d8 radiant or necrotic damage.", E(EffectKind.CombatRule, "divine-strike", 8)) { Needs = Combat },
                            new ChoiceOption("potent_spellcasting", "Potent Spellcasting", "Add your Wisdom modifier to cleric cantrip damage.", E(EffectKind.CombatRule, "potent-spellcasting", 1)) { Needs = Magic },
                        },
                    });
                    break;

                case "druid":
                    resources.Add(RT("wild_shape", "Wild Shape", ResourceRecovery.LongRest, "Take the shape of a beast. One use returns on a short rest.", Steps(0, (2, 2), (6, 3), (17, 4))));
                    c.SubclassFeatureLevels = new[] { 3, 6, 10, 14 };
                    c.LaterFeatures = Levels(
                        (2, LF("wild_shape", "Wild Shape", "Magically take the form of a beast you have seen.", FeatureKind.LimitedUse, Combat | Explore)),
                        (2, LF("wild_companion", "Wild Companion", "Spend a Wild Shape use or a spell slot to summon a spirit familiar.", FeatureKind.Active, Magic)),
                        (5, LF("wild_resurgence", "Wild Resurgence", "Trade spell slots and Wild Shape uses for one another.", FeatureKind.Active, Magic)),
                        (7, LF("elemental_fury", "Elemental Fury", "Elemental power fills your strikes or your cantrips (your choice).", FeatureKind.Choice, Combat | Magic)),
                        (15, LF("improved_elemental_fury", "Improved Elemental Fury", "Your Elemental Fury option grows stronger.", FeatureKind.Passive, Combat | Magic)),
                        (18, LF("beast_spells", "Beast Spells", "You can cast spells while in beast form.", FeatureKind.Passive, Magic)),
                        (20, LF("archdruid", "Archdruid", "Your Wild Shape and spell slots renew as you call on nature, and you age slowly.", FeatureKind.Passive, Magic | Rest)));
                    choices.Add(new ChoiceDefinition
                    {
                        Id = "lv7:elemental_fury", Level = 7, Name = "Elemental Fury", Prompt = "Choose how elemental power fills you.", Kind = ChoiceKind.Option,
                        Options = new[]
                        {
                            new ChoiceOption("potent_spellcasting", "Potent Spellcasting", "Add your Wisdom modifier to druid cantrip damage.", E(EffectKind.CombatRule, "potent-spellcasting", 1)) { Needs = Magic },
                            new ChoiceOption("primal_strike", "Primal Strike", "Once per turn your attacks deal an extra 1d8 elemental damage.", E(EffectKind.CombatRule, "primal-strike", 8)) { Needs = Combat },
                        },
                    });
                    break;

                case "fighter":
                    Replace(resources, RT("second_wind", "Second Wind", ResourceRecovery.ShortRest, "Catch your breath and regain 1d10 + fighter level hit points.", Steps(2, (4, 3), (10, 4))));
                    resources.Add(RT("action_surge", "Action Surge", ResourceRecovery.ShortRest, "Take one additional action.", Steps(0, (2, 1), (17, 2))));
                    resources.Add(RT("indomitable", "Indomitable", ResourceRecovery.LongRest, "Reroll a failed saving throw with a bonus of your fighter level.", Steps(0, (9, 1), (13, 2), (17, 3))));
                    c.Scalings = new[] { new ScalingDefinition("weapon_mastery", "Weapon masteries", "{0}", Steps(3, (4, 4), (10, 5), (16, 6))) };
                    c.FeatLevels = new[] { 4, 6, 8, 12, 14, 16 };
                    c.FightingStyleLevel = 1;
                    c.SubclassFeatureLevels = new[] { 3, 7, 10, 15, 18 };
                    c.LaterFeatures = Levels(
                        (2, LF("action_surge", "Action Surge", "Push yourself for one more action (twice per rest from level 17).", FeatureKind.LimitedUse, Combat)),
                        (2, LF("tactical_mind", "Tactical Mind", "Spend a Second Wind use to add 1d10 to a failed ability check.", FeatureKind.Active, Explore | Talk)),
                        (5, LF("extra_attack", "Extra Attack", "You attack twice when you take the Attack action.", FeatureKind.Passive, Combat)),
                        (5, LF("tactical_shift", "Tactical Shift", "Second Wind lets you move half your speed without provoking attacks.", FeatureKind.Passive, Combat)),
                        (9, LF("indomitable", "Indomitable", "Reroll a failed saving throw, adding your fighter level.", FeatureKind.LimitedUse, Combat)),
                        (9, LF("tactical_master", "Tactical Master", "Swap a weapon's mastery property for Push, Sap or Slow when you attack.", FeatureKind.Passive, Combat)),
                        (11, Rep(LF("two_extra_attacks", "Two Extra Attacks", "You attack three times when you take the Attack action.", FeatureKind.Passive, Combat), "extra_attack")),
                        (13, LF("studied_attacks", "Studied Attacks", "After a miss, your next attack against that creature has advantage.", FeatureKind.Passive, Combat)),
                        (20, Rep(LF("three_extra_attacks", "Three Extra Attacks", "You attack four times when you take the Attack action.", FeatureKind.Passive, Combat), "two_extra_attacks")));
                    break;

                case "monk":
                    resources.Add(RT("focus_points", "Focus Points", ResourceRecovery.ShortRest, "Fuel Flurry of Blows, Patient Defence and Step of the Wind.", FromLevelEqualsLevel(2)));
                    resources.Add(new ResourceDefinition { Id = "uncanny_metabolism", Name = "Uncanny Metabolism", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 2, Recovery = ResourceRecovery.LongRest, Description = "Regain focus and some hit points as you roll initiative." });
                    c.Scalings = new[]
                    {
                        new ScalingDefinition("martial_arts", "Martial Arts die", "d{0}", Steps(6, (5, 8), (11, 10), (17, 12))),
                        new ScalingDefinition("unarmored_movement", "Unarmoured Movement", "+{0} ft", Steps(0, (2, 10), (6, 15), (10, 20), (14, 25), (18, 30))),
                    };
                    c.SubclassFeatureLevels = new[] { 3, 6, 11, 17 };
                    c.LaterFeatures = Levels(
                        (2, LF("monks_focus", "Monk's Focus", "Spend focus points on Flurry of Blows, Patient Defence and Step of the Wind.", FeatureKind.LimitedUse, Combat)),
                        (2, Sc(LF("unarmored_movement", "Unarmoured Movement", "Your speed rises while you wear no armour and carry no shield.", FeatureKind.Scaling, None,
                            EC(EffectKind.SpeedBonus, "scale:unarmored_movement", 0, EffectCondition.NoArmorNoShield)), "unarmored_movement")),
                        (2, LF("uncanny_metabolism", "Uncanny Metabolism", "Once per long rest, rolling initiative restores your focus and heals you.", FeatureKind.LimitedUse, Combat | Rest)),
                        (3, LF("deflect_attacks", "Deflect Attacks", "Turn aside a weapon attack and perhaps redirect it.", FeatureKind.Active, Combat)),
                        (4, LF("slow_fall", "Slow Fall", "Reduce falling damage by five times your monk level.", FeatureKind.Active, Explore)),
                        (5, LF("extra_attack", "Extra Attack", "You attack twice when you take the Attack action.", FeatureKind.Passive, Combat)),
                        (5, LF("stunning_strike", "Stunning Strike", "Spend focus to try to stun a creature you hit.", FeatureKind.Active, Combat)),
                        (6, LF("empowered_strikes", "Empowered Strikes", "Your unarmed strikes can deal force damage.", FeatureKind.Passive, Combat)),
                        (7, LF("evasion", "Evasion", "Area effects that allow a Dexterity save harm you less, or not at all.", FeatureKind.Passive, Combat)),
                        (9, LF("acrobatic_movement", "Acrobatic Movement", "Without armour or shield you run along walls and across liquids.", FeatureKind.Passive, Explore)),
                        (10, LF("heightened_focus", "Heightened Focus", "Flurry of Blows, Patient Defence and Step of the Wind improve.", FeatureKind.Passive, Combat)),
                        (10, LF("self_restoration", "Self-Restoration", "End charm, fear or poison on yourself, and need no food or drink.", FeatureKind.Passive, Combat)),
                        (13, LF("deflect_energy", "Deflect Energy", "Deflect Attacks works against any damage type.", FeatureKind.Passive, Combat)),
                        (14, LF("disciplined_survivor", "Disciplined Survivor", "You are proficient in every saving throw, and can spend focus to reroll one.", FeatureKind.Passive, None,
                            Save(Ability.Strength), Save(Ability.Dexterity), Save(Ability.Constitution), Save(Ability.Intelligence), Save(Ability.Wisdom), Save(Ability.Charisma))),
                        (15, LF("perfect_focus", "Perfect Focus", "Rolling initiative with little focus left restores some.", FeatureKind.Passive, Combat)),
                        (18, LF("superior_defense", "Superior Defence", "Spend focus to resist all damage but force for a minute.", FeatureKind.Active, Combat)),
                        (20, LF("body_and_mind", "Body and Mind", "Your Dexterity and Wisdom rise by 4, to a maximum of 25.", FeatureKind.Passive, None,
                            Inc(Ability.Dexterity, 4, 25), Inc(Ability.Wisdom, 4, 25))));
                    break;

                case "paladin":
                    resources.Add(RT("channel_divinity", "Channel Divinity", ResourceRecovery.LongRest, "Channel divine power (Divine Sense and your oath's options). One use returns on a short rest.",
                        Steps(0, (3, 2), (11, 3))));
                    c.FightingStyleLevel = 2;
                    c.SubclassFeatureLevels = new[] { 3, 7, 15, 20 };
                    c.LaterFeatures = Levels(
                        (2, LF("fighting_style", "Fighting Style", "You learn a Fighting Style feat.", FeatureKind.Choice, None)),
                        (2, LF("paladins_smite", "Paladin's Smite", "You always have Divine Smite prepared and can cast it once without a slot.", FeatureKind.LimitedUse, Magic | Combat)),
                        (3, LF("channel_divinity", "Channel Divinity", "Channel divine power: Divine Sense, and your oath's options.", FeatureKind.LimitedUse, Magic | Explore)),
                        (5, LF("extra_attack", "Extra Attack", "You attack twice when you take the Attack action.", FeatureKind.Passive, Combat)),
                        (5, LF("faithful_steed", "Faithful Steed", "You can call a spirit mount (Find Steed) once without a slot.", FeatureKind.LimitedUse, Magic)),
                        (6, LF("aura_of_protection", "Aura of Protection", "You and allies near you add your Charisma modifier (at least +1) to saving throws.", FeatureKind.Passive, Combat,
                            new RuleEffect(EffectKind.SaveAbilityBonus, "all", 0, nameof(Ability.Charisma), EffectCondition.Always, 1))),
                        (9, LF("abjure_foes", "Abjure Foes", "Channel Divinity to frighten foes.", FeatureKind.Active, Combat)),
                        (10, LF("aura_of_courage", "Aura of Courage", "You and allies in your aura cannot be frightened.", FeatureKind.Passive, Combat)),
                        (11, LF("radiant_strikes", "Radiant Strikes", "Your weapon hits deal an extra 1d8 radiant damage.", FeatureKind.Passive, Combat)),
                        (14, LF("restoring_touch", "Restoring Touch", "Lay on Hands can also end conditions.", FeatureKind.Passive, Combat)),
                        (18, LF("aura_expansion", "Aura Expansion", "Your auras reach 30 feet.", FeatureKind.Passive, Combat)));
                    choices.Add(FightingStyleFeat(2));
                    break;

                case "ranger":
                    Replace(resources, RT("favored_enemy", "Favoured Enemy", ResourceRecovery.LongRest, "Mark a quarry (Hunter's Mark) without spending a spell slot.", Steps(2, (5, 3), (9, 4), (13, 5), (17, 6))));
                    resources.Add(new ResourceDefinition { Id = "tireless", Name = "Tireless", Formula = ResourceFormula.AbilityModifierMin1, Ability = Ability.Wisdom, FromLevel = 10, Recovery = ResourceRecovery.LongRest, Description = "Gain temporary hit points." });
                    resources.Add(new ResourceDefinition { Id = "natures_veil", Name = "Nature's Veil", Formula = ResourceFormula.AbilityModifierMin1, Ability = Ability.Wisdom, FromLevel = 14, Recovery = ResourceRecovery.LongRest, Description = "Become invisible until your next turn ends." });
                    c.FightingStyleLevel = 2;
                    c.SubclassFeatureLevels = new[] { 3, 7, 11, 15 };
                    c.LaterFeatures = Levels(
                        (2, LF("deft_explorer", "Deft Explorer", "Expertise in one skill and two more languages.", FeatureKind.Choice, None)),
                        (2, LF("fighting_style", "Fighting Style", "You learn a Fighting Style feat.", FeatureKind.Choice, None)),
                        (5, LF("extra_attack", "Extra Attack", "You attack twice when you take the Attack action.", FeatureKind.Passive, Combat)),
                        (6, LF("roving", "Roving", "Your speed rises by 10 feet out of heavy armour, and you can climb and swim at your walking speed.", FeatureKind.Passive, None,
                            EC(EffectKind.SpeedBonus, null, 10, EffectCondition.NoHeavyArmor), EC(EffectKind.MovementMode, "climb", 0, EffectCondition.NoHeavyArmor), EC(EffectKind.MovementMode, "swim", 0, EffectCondition.NoHeavyArmor))),
                        (9, LF("expertise_9", "Expertise", "Two of your skill proficiencies count double.", FeatureKind.Choice, None)),
                        (10, LF("tireless", "Tireless", "Gain temporary hit points as an action, and short rests ease exhaustion.", FeatureKind.LimitedUse, Combat | Rest)),
                        (13, LF("relentless_hunter", "Relentless Hunter", "Damage cannot break your concentration on Hunter's Mark.", FeatureKind.Passive, Magic)),
                        (14, LF("natures_veil", "Nature's Veil", "Become invisible for a moment.", FeatureKind.LimitedUse, Combat)),
                        (17, LF("precise_hunter", "Precise Hunter", "Advantage on attacks against the creature you have marked.", FeatureKind.Passive, Combat)),
                        (18, LF("feral_senses", "Feral Senses", "You have blindsight to 30 feet.", FeatureKind.Passive, None, new RuleEffect(EffectKind.Sense, "blindsight", 30))),
                        (20, LF("foe_slayer", "Foe Slayer", "Hunter's Mark deals a d10 instead of a d6.", FeatureKind.Passive, Combat)));
                    choices.Add(Expertise(2, 1, "deft_expertise"));
                    choices.Add(new ChoiceDefinition { Id = "lv2:deft_languages", Level = 2, Name = "Deft Explorer: languages", Prompt = "Choose two languages.", Kind = ChoiceKind.Language, Count = 2 });
                    choices.Add(FightingStyleFeat(2));
                    choices.Add(Expertise(9, 2));
                    break;

                case "rogue":
                    c.Scalings = new[] { new ScalingDefinition("sneak_attack", "Sneak Attack", "{0}d6", Steps(1, (3, 2), (5, 3), (7, 4), (9, 5), (11, 6), (13, 7), (15, 8), (17, 9), (19, 10))) };
                    resources.Add(new ResourceDefinition { Id = "stroke_of_luck", Name = "Stroke of Luck", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 20, Recovery = ResourceRecovery.ShortRest, Description = "Turn a failed d20 test into a 20." });
                    c.FeatLevels = new[] { 4, 8, 10, 12, 16 };
                    c.SubclassFeatureLevels = new[] { 3, 9, 13, 17 };
                    c.LaterFeatures = Levels(
                        (2, LF("cunning_action", "Cunning Action", "Dash, Disengage or Hide as a bonus action.", FeatureKind.Active, Combat)),
                        (3, LF("steady_aim", "Steady Aim", "Give up moving to gain advantage on your next attack.", FeatureKind.Active, Combat)),
                        (5, Sc(LF("cunning_strike", "Cunning Strike", "Trade sneak attack dice for poison, trips or withdrawal.", FeatureKind.Active, Combat), "sneak_attack")),
                        (5, LF("uncanny_dodge", "Uncanny Dodge", "Halve the damage of an attack you can see.", FeatureKind.Active, Combat)),
                        (6, LF("expertise_6", "Expertise", "Two more of your skill proficiencies count double.", FeatureKind.Choice, None)),
                        (7, LF("evasion", "Evasion", "Area effects that allow a Dexterity save harm you less, or not at all.", FeatureKind.Passive, Combat)),
                        (7, LF("reliable_talent", "Reliable Talent", "A d20 below 10 counts as 10 on ability checks with skills or tools you are proficient in.", FeatureKind.Passive, None,
                            new RuleEffect(EffectKind.MinimumRoll, "proficient_checks", 10))),
                        (11, LF("improved_cunning_strike", "Improved Cunning Strike", "Use two Cunning Strike effects at once.", FeatureKind.Passive, Combat)),
                        (14, LF("devious_strikes", "Devious Strikes", "New Cunning Strike effects: daze, knock out, obscure.", FeatureKind.Passive, Combat)),
                        (15, LF("slippery_mind", "Slippery Mind", "You are proficient in Wisdom and Charisma saving throws.", FeatureKind.Passive, None, Save(Ability.Wisdom), Save(Ability.Charisma))),
                        (18, LF("elusive", "Elusive", "No attack against you has advantage while you are not incapacitated.", FeatureKind.Passive, Combat)),
                        (20, LF("stroke_of_luck", "Stroke of Luck", "Turn a failed d20 test into a 20, once per short rest.", FeatureKind.LimitedUse, Combat)));
                    choices.Add(Expertise(6, 2));
                    break;

                case "sorcerer":
                    resources.Add(RT("sorcery_points", "Sorcery Points", ResourceRecovery.LongRest, "Fuel Metamagic and convert to and from spell slots.", FromLevelEqualsLevel(2)));
                    c.SubclassFeatureLevels = new[] { 3, 6, 14, 18 };
                    c.LaterFeatures = Levels(
                        (2, LF("font_of_magic", "Font of Magic", "Sorcery points equal to your level; convert them to and from spell slots.", FeatureKind.LimitedUse, Magic)),
                        (2, LF("metamagic", "Metamagic", "Learn two ways to twist your spells.", FeatureKind.Choice, Magic)),
                        (5, LF("sorcerous_restoration", "Sorcerous Restoration", "Regain some sorcery points on a short rest.", FeatureKind.LimitedUse, Rest)),
                        (7, LF("sorcery_incarnate", "Sorcery Incarnate", "Spend sorcery points to use Innate Sorcery, and use two Metamagic options at once while it lasts.", FeatureKind.Passive, Magic)),
                        (10, LF("metamagic_10", "Metamagic", "Learn two more Metamagic options.", FeatureKind.Choice, Magic)),
                        (17, LF("metamagic_17", "Metamagic", "Learn two more Metamagic options.", FeatureKind.Choice, Magic)),
                        (20, LF("arcane_apotheosis", "Arcane Apotheosis", "While Innate Sorcery is active, one Metamagic option per turn is free.", FeatureKind.Passive, Magic)));
                    choices.Add(Metamagic(2));
                    choices.Add(Metamagic(10));
                    choices.Add(Metamagic(17));
                    break;

                case "warlock":
                    resources.Add(new ResourceDefinition { Id = "magical_cunning", Name = "Magical Cunning", Formula = ResourceFormula.Fixed, Value = 1, FromLevel = 2, Recovery = ResourceRecovery.LongRest, Description = "Recover half your pact slots with a brief rite." });
                    c.Scalings = new[] { new ScalingDefinition("pact_slot_level", "Pact slot level", "level {0}", PactSlotLevel) };
                    c.SubclassFeatureLevels = new[] { 3, 6, 10, 14 };
                    c.LaterFeatures = Levels(
                        (2, LF("magical_cunning", "Magical Cunning", "Once per long rest, a minute's rite restores half your pact slots.", FeatureKind.LimitedUse, Magic | Rest)),
                        (9, LF("contact_patron", "Contact Patron", "You can commune with your patron once per long rest.", FeatureKind.LimitedUse, Magic | Talk)),
                        (11, LF("mystic_arcanum_6", "Mystic Arcanum (6th level)", "One 6th-level spell, once per long rest without a slot.", FeatureKind.LimitedUse, Magic)),
                        (13, LF("mystic_arcanum_7", "Mystic Arcanum (7th level)", "One 7th-level spell, once per long rest without a slot.", FeatureKind.LimitedUse, Magic)),
                        (15, LF("mystic_arcanum_8", "Mystic Arcanum (8th level)", "One 8th-level spell, once per long rest without a slot.", FeatureKind.LimitedUse, Magic)),
                        (17, LF("mystic_arcanum_9", "Mystic Arcanum (9th level)", "One 9th-level spell, once per long rest without a slot.", FeatureKind.LimitedUse, Magic)),
                        (20, LF("eldritch_master", "Eldritch Master", "Magical Cunning restores all your pact slots.", FeatureKind.Passive, Magic)));
                    foreach (var (level, count) in new[] { (2, 2), (5, 2), (7, 1), (9, 1), (12, 1), (15, 1), (18, 1) }) choices.Add(Invocations(level, count));
                    break;

                case "wizard":
                    c.Scalings = new[] { new ScalingDefinition("arcane_recovery", "Arcane Recovery (slot levels)", "{0}", Steps(1, (3, 2), (5, 3), (7, 4), (9, 5), (11, 6), (13, 7), (15, 8), (17, 9), (19, 10))) };
                    c.SubclassFeatureLevels = new[] { 3, 6, 10, 14 };
                    c.LaterFeatures = Levels(
                        (2, LF("scholar", "Scholar", "Expertise in one academic skill you are proficient in.", FeatureKind.Choice, None)),
                        (5, LF("memorize_spell", "Memorize Spell", "Swap a prepared spell for another from your book after a short rest.", FeatureKind.Active, Magic | Rest)),
                        (18, LF("spell_mastery", "Spell Mastery", "Cast a chosen 1st- and 2nd-level spell at will.", FeatureKind.Passive, Magic)),
                        (20, LF("signature_spells", "Signature Spells", "Two 3rd-level spells, each once per short rest without a slot.", FeatureKind.LimitedUse, Magic)));
                    choices.Add(Expertise(2, 1, "scholar", Skill.Arcana, Skill.History, Skill.Investigation, Skill.Medicine, Skill.Nature, Skill.Religion));
                    break;
            }

            if (c.Spellcasting != null)
            {
                var sc = c.Spellcasting;
                switch (c.Id)
                {
                    case "bard": sc.Progression = CasterProgression.Full; sc.Cantrips = Steps(2, (4, 3), (10, 4)); sc.Prepared = PreparedFull; break;
                    case "cleric": sc.Progression = CasterProgression.Full; sc.Cantrips = Steps(3, (4, 4), (10, 5)); sc.Prepared = PreparedFull; break;
                    case "druid": sc.Progression = CasterProgression.Full; sc.Cantrips = Steps(2, (4, 3), (10, 4)); sc.Prepared = PreparedFull; break;
                    case "sorcerer":
                        sc.Progression = CasterProgression.Full; sc.Cantrips = Steps(4, (4, 5), (10, 6));
                        sc.Prepared = new[] { 2, 4, 6, 7, 9, 10, 11, 12, 14, 15, 16, 16, 17, 17, 18, 18, 19, 20, 21, 22 };
                        break;
                    case "wizard":
                        sc.Progression = CasterProgression.Full; sc.Cantrips = Steps(3, (4, 4), (10, 5));
                        sc.Prepared = new[] { 4, 5, 6, 7, 9, 10, 11, 12, 14, 15, 16, 16, 17, 18, 19, 21, 22, 23, 24, 25 };
                        break;
                    case "paladin":
                    case "ranger": sc.Progression = CasterProgression.Half; sc.Prepared = PreparedHalf; break;
                    case "warlock":
                        sc.Progression = CasterProgression.Pact; sc.Cantrips = Steps(2, (4, 3), (10, 4));
                        sc.Prepared = new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15, 15 };
                        break;
                }

                resources.AddRange(SlotResources(sc));
            }

            // Feat levels and the epic boon: shown as features, chosen through feat choices.
            var later = new Dictionary<int, List<FeatureDefinition>>();
            foreach (var pair in c.LaterFeatures) later[pair.Key] = new List<FeatureDefinition>(pair.Value);
            foreach (var level in c.FeatLevels)
            {
                Add(later, level, LF("feat_" + level, "Ability Score Improvement", "Take the Ability Score Improvement feat or another feat you qualify for.", FeatureKind.Choice, None));
                choices.Add(new ChoiceDefinition
                {
                    Id = $"lv{level}:feat", Level = level, Name = "Ability Score Improvement or feat", Prompt = "Choose a feat (Ability Score Improvement raises one ability by 2 or two by 1).",
                    Kind = ChoiceKind.Feat, FeatCategories = FeatCategory.General | FeatCategory.Origin | (c.FightingStyleLevel > 0 ? FeatCategory.FightingStyle : FeatCategory.None),
                });
            }

            Add(later, c.EpicBoonLevel, LF("epic_boon", "Epic Boon", "Take an Epic Boon feat (or another feat you qualify for).", FeatureKind.Choice, None));
            choices.Add(new ChoiceDefinition
            {
                Id = $"lv{c.EpicBoonLevel}:boon", Level = c.EpicBoonLevel, Name = "Epic Boon", Prompt = "Choose an Epic Boon or another feat you qualify for.", Kind = ChoiceKind.Feat,
                FeatCategories = FeatCategory.EpicBoon | FeatCategory.General | FeatCategory.Origin | (c.FightingStyleLevel > 0 ? FeatCategory.FightingStyle : FeatCategory.None),
            });
            Add(later, c.SubclassLevel, LF("subclass", c.SubclassTitle, $"Choose your {c.SubclassTitle}; it grants features at levels {string.Join(", ", c.SubclassFeatureLevels)}.", FeatureKind.Choice, None));

            var final = new Dictionary<int, IReadOnlyList<FeatureDefinition>>();
            foreach (var pair in later) final[pair.Key] = pair.Value;
            c.LaterFeatures = final;
            c.LevelChoices = choices;
            c.Resources = resources;
        }

        private static RuleEffect Save(Ability a) => new RuleEffect(EffectKind.SaveProficiency, a.ToString());

        private static void Add(Dictionary<int, List<FeatureDefinition>> map, int level, FeatureDefinition f)
        {
            if (!map.TryGetValue(level, out var list)) map[level] = list = new List<FeatureDefinition>();
            list.Add(f);
        }

        private static void Replace(List<ResourceDefinition> list, ResourceDefinition r)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i].Id == r.Id)
                {
                    list[i] = r;
                    return;
                }

            list.Add(r);
        }

        /// <summary>Hit dice: one per level, half of them back on a long rest (all classes; Phase 7).</summary>
        public static readonly ResourceDefinition HitDice = new ResourceDefinition
        {
            Id = "hit_dice", Name = "Hit dice", Formula = ResourceFormula.PerLevel, Value = 1, Recovery = ResourceRecovery.LongRest, Amount = RecoveryAmount.All, // Phase 9: SRD 5.2.1 — a long rest restores all spent hit point dice
            Description = "Spent on a short rest to recover hit points (rest phase). Half return on a long rest.", Origin = "Level",
        };
    }
}
