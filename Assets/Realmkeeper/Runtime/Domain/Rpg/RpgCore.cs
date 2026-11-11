using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    // Phase 6: the core vocabulary of Realmkeeper's character rules (5e-compatible; see
    // Documentation/Systems/SkillsAndAbilities.md and the licence notes in Characters.md).

    public enum Ability
    {
        Strength = 0,
        Dexterity = 1,
        Constitution = 2,
        Intelligence = 3,
        Wisdom = 4,
        Charisma = 5,
    }

    public enum Skill
    {
        Acrobatics = 0,
        AnimalHandling = 1,
        Arcana = 2,
        Athletics = 3,
        Deception = 4,
        History = 5,
        Insight = 6,
        Intimidation = 7,
        Investigation = 8,
        Medicine = 9,
        Nature = 10,
        Perception = 11,
        Performance = 12,
        Persuasion = 13,
        Religion = 14,
        SleightOfHand = 15,
        Stealth = 16,
        Survival = 17,
    }

    /// <summary>How well a character knows a skill: proficiency adds the bonus once, expertise twice.</summary>
    public enum ProficiencyRank
    {
        None = 0,
        Proficient = 1,
        Expertise = 2,
    }

    public enum CreatureSize
    {
        Small = 0,
        Medium = 1,

        // Phase 10 (bestiary); appended so saved values keep their meaning.
        Tiny = 2,
        Large = 3,
        Huge = 4,
    }

    /// <summary>Where a rules definition comes from (licence provenance; see Characters.md).</summary>
    public enum ContentSource
    {
        /// <summary>System Reference Document 5.2.1, Wizards of the Coast LLC, CC BY 4.0.</summary>
        Srd521 = 0,

        /// <summary>System Reference Document 5.1, Wizards of the Coast LLC, CC BY 4.0.</summary>
        Srd51 = 1,

        /// <summary>Written for Realmkeeper.</summary>
        RealmkeeperOriginal = 2,
    }

    /// <summary>How the six base scores were set at creation.</summary>
    public enum AbilityMethod
    {
        None = 0,
        StandardArray = 1,
        PointBuy = 2,
    }

    /// <summary>Where a character is in creation.</summary>
    public enum CreationState
    {
        /// <summary>A player from a save made before Phase 6: nothing assigned or invented; setup is offered.</summary>
        Legacy = 0,

        /// <summary>A new world's player who has not yet created their character.</summary>
        Pending = 1,

        /// <summary>Created: species, background, class, scores and choices are set and the kit was given.</summary>
        Complete = 2,
    }

    /// <summary>When a class resource comes back.</summary>
    public enum ResourceRecovery
    {
        LongRest = 0,
        ShortRest = 1,
    }

    /// <summary>How a resource's maximum is worked out at a level.</summary>
    public enum ResourceFormula
    {
        /// <summary>A fixed number.</summary>
        Fixed = 0,

        /// <summary>An ability modifier, at least 1.</summary>
        AbilityModifierMin1 = 1,

        /// <summary>1 + an ability modifier, at least 1.</summary>
        OnePlusAbilityModifier = 2,

        /// <summary>A number times the character's level (e.g. a healing pool).</summary>
        PerLevel = 3,

        /// <summary>The proficiency bonus.</summary>
        ProficiencyBonus = 4,

        /// <summary>Phase 7: a value per class level from <see cref="ResourceDefinition.Table"/> (0 = not available yet).</summary>
        Table = 5,
    }

    /// <summary>How much of a resource a recovery brings back.</summary>
    public enum RecoveryAmount
    {
        All = 0,

        /// <summary>Half the maximum, rounded down, at least 1 (hit dice on a long rest).</summary>
        Half = 1,
    }

    /// <summary>When an effect applies (Phase 7). Evaluated against the character's <see cref="Loadout"/>.</summary>
    public enum EffectCondition
    {
        Always = 0,

        /// <summary>No body armour is worn (shields allowed).</summary>
        NoArmor = 1,

        /// <summary>No body armour and no shield.</summary>
        NoArmorNoShield = 2,

        /// <summary>No heavy armour is worn.</summary>
        NoHeavyArmor = 3,

        /// <summary>Body armour is worn.</summary>
        WearingArmor = 4,
    }

    /// <summary>What a feature is, for display and for later systems (Phase 7).</summary>
    public enum FeatureKind
    {
        Passive = 0,
        Active = 1,
        LimitedUse = 2,
        Scaling = 3,
        Choice = 4,
    }

    /// <summary>
    /// A system that must exist before a feature fully works (Phase 7). Features are acquired, validated and
    /// shown now; the parts that need these systems are recorded as integration requirements, not faked.
    /// </summary>
    [Flags]
    public enum IntegrationNeed
    {
        None = 0,
        Combat = 1,
        Magic = 2,
        Rest = 4,
        Dialogue = 8,
        Exploration = 16,
    }

    /// <summary>Kinds of feat (SRD 5.2.1).</summary>
    [Flags]
    public enum FeatCategory
    {
        None = 0,
        Origin = 1,
        General = 2,
        FightingStyle = 4,
        EpicBoon = 8,
    }

    /// <summary>Where experience came from (Phase 7).</summary>
    public enum ExperienceSource
    {
        Quest = 0,
        Encounter = 1,
        Discovery = 2,
        Milestone = 3,
        Story = 4,
        Other = 5,
    }

    /// <summary>The kinds of typed rule effect. Each has one meaning and one handler (<see cref="CharacterRules"/>).</summary>
    public enum EffectKind
    {
        /// <summary>Proficiency in a skill (Key = skill name).</summary>
        SkillProficiency = 0,

        /// <summary>Proficiency in a saving throw (Key = ability name).</summary>
        SaveProficiency = 1,

        /// <summary>Proficiency with a tool (Key = tool ID).</summary>
        ToolProficiency = 2,

        /// <summary>Proficiency with weapons (Key = group or weapon, e.g. "simple", "martial", "rapier").</summary>
        WeaponProficiency = 3,

        /// <summary>Proficiency with armour (Key = "light", "medium", "heavy", "shields").</summary>
        ArmorProficiency = 4,

        /// <summary>A language (Key = language ID).</summary>
        Language = 5,

        /// <summary>Sight in darkness (Value = range in feet).</summary>
        Darkvision = 6,

        /// <summary>Resistance to a damage type (Key = damage type).</summary>
        Resistance = 7,

        /// <summary>Walking speed change (Value = feet).</summary>
        SpeedBonus = 8,

        /// <summary>Extra hit points per level (Value).</summary>
        MaxHpPerLevel = 9,

        /// <summary>Armour class without armour: 10 + Dexterity + another ability (Key = ability name).</summary>
        UnarmoredDefense = 10,

        /// <summary>A class resource (Key = resource ID, defined on the class or species).</summary>
        Resource = 11,

        /// <summary>Advantage on saving throws against something (Key = condition or tag).</summary>
        SaveAdvantage = 12,

        /// <summary>Carrying capacity in percent of normal (Value; e.g. 200 for powerful builds).</summary>
        CarryCapacityPercent = 13,

        /// <summary>A spell or magical effect known innately (Key = an effect ID kept for the magic phase).</summary>
        InnateMagic = 14,

        /// <summary>A rule for tactical play kept as data until combat exists (Key = rule ID, Value = magnitude, Text = summary).</summary>
        CombatRule = 15,

        /// <summary>A narrative or social privilege used by world systems (Key = feature ID).</summary>
        SocialFeature = 16,

        /// <summary>A change to shop prices in percent (Value; negative is cheaper for the player when buying).</summary>
        ShopPricePercent = 17,

        // ----- Phase 7 -----

        /// <summary>An ability score increase (Key = ability, Value = amount, Limit = the score it cannot pass).</summary>
        AbilityIncrease = 18,

        /// <summary>Advantage on a roll (Key = target: "initiative", "skill:Athletics", "save:Dexterity", "check:Strength"; Text = condition, if any).</summary>
        Advantage = 19,

        /// <summary>Disadvantage on a roll (Key and Text as for <see cref="Advantage"/>).</summary>
        Disadvantage = 20,

        /// <summary>A bonus to initiative (Key "proficiency" = the proficiency bonus, otherwise Value).</summary>
        InitiativeBonus = 21,

        /// <summary>Half the proficiency bonus (rounded down) on ability checks without proficiency (Jack of All Trades).</summary>
        HalfProficiencyUntrained = 22,

        /// <summary>An ability modifier added to a skill (Key = skill, Text = ability, Limit = minimum bonus).</summary>
        SkillAbilityBonus = 23,

        /// <summary>An ability modifier added to saving throws (Key = "all" or an ability, Text = ability, Limit = minimum bonus).</summary>
        SaveAbilityBonus = 24,

        /// <summary>A special sense (Key = "blindsight", "truesight", "devils_sight"; Value = range in feet).</summary>
        Sense = 25,

        /// <summary>A movement mode (Key = "climb", "swim"; Value 0 = equal to walking speed).</summary>
        MovementMode = 26,

        /// <summary>A bonus to armour class (Value; usually with a condition such as <see cref="EffectCondition.WearingArmor"/>).</summary>
        ArmorClassBonus = 27,

        /// <summary>The lowest a d20 can count as (Key = "proficient_checks"; Value; Reliable Talent).</summary>
        MinimumRoll = 28,

        /// <summary>A fighting style (Key = style ID: archery, defense, dueling, great_weapon, protection, two_weapon); styles do not repeat.</summary>
        FightingStyle = 29,
    }

    /// <summary>One typed rule effect. Key, Value and Text are interpreted by <see cref="EffectKind"/> only.</summary>
    public sealed class RuleEffect
    {
        public RuleEffect(EffectKind kind, string key = null, int value = 0, string text = null, EffectCondition condition = EffectCondition.Always, int limit = 0)
        {
            Kind = kind;
            Key = key;
            Value = value;
            Text = text;
            Condition = condition;
            Limit = limit;
        }

        public EffectKind Kind { get; }
        public string Key { get; }
        public int Value { get; }
        public string Text { get; }

        /// <summary>Phase 7: when the effect applies.</summary>
        public EffectCondition Condition { get; }

        /// <summary>Phase 7: a cap (ability increases) or minimum (ability-modifier bonuses), by kind.</summary>
        public int Limit { get; }

        public override string ToString() => Kind + (Key != null ? ":" + Key : string.Empty) + (Value != 0 ? "=" + Value : string.Empty);
    }

    /// <summary>
    /// Experience and levels (Phase 7). The thresholds are the SRD 5.2.1 Character Advancement table
    /// (CC BY 4.0), used as Realmkeeper's rules configuration; the Bible keeps the final XP curve open.
    /// </summary>
    public static class ProgressionRules
    {
        /// <summary>Experience needed for levels 1–20.</summary>
        public static readonly IReadOnlyList<int> Thresholds = new[]
        {
            0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000,
            85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000,
        };

        /// <summary>The largest single award (a guard against typing errors and overflow).</summary>
        public const int MaxAward = 1000000;

        /// <summary>The most experience a character may hold.</summary>
        public const int MaxExperience = 10000000;

        /// <summary>The rules version new characters are built under (6 = Phase 6 data; 7 adds level-1 choices).</summary>
        public const int CurrentRulesVersion = 8;

        /// <summary>The experience a level needs.</summary>
        public static int ExperienceFor(int level) => Thresholds[Math.Max(1, Math.Min(AbilityRules.MaxLevel, level)) - 1];

        /// <summary>The highest level the experience allows.</summary>
        public static int LevelFor(int experience)
        {
            var level = 1;
            for (var i = 1; i < Thresholds.Count; i++)
                if (experience >= Thresholds[i]) level = i + 1;
            return level;
        }

        /// <summary>The experience for the next level, or null at level 20.</summary>
        public static int? NextThreshold(int level) => level >= AbilityRules.MaxLevel ? (int?)null : Thresholds[level];
    }

    /// <summary>
    /// Realmkeeper's core numbers (5e-compatible, from SRD 5.2.1): the ability modifier, the proficiency
    /// bonus, the standard array and point buy.
    /// </summary>
    public static class AbilityRules
    {
        public const int MinScore = 1;
        public const int MaxScore = 20;

        /// <summary>The most a level-1 character's final score may be (15 bought + 2 from species).</summary>
        public const int MaxCreationScore = 17;

        public const int PointBuyBudget = 27;
        public const int PointBuyMin = 8;
        public const int PointBuyMax = 15;
        public const int MaxLevel = 20;

        public static readonly IReadOnlyList<int> StandardArray = new[] { 15, 14, 13, 12, 10, 8 };

        public static readonly IReadOnlyList<Ability> All = (Ability[])Enum.GetValues(typeof(Ability));

        /// <summary>floor((score − 10) / 2).</summary>
        public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

        /// <summary>+2 at levels 1–4, +3 at 5–8 … +6 at 17–20.</summary>
        public static int ProficiencyBonus(int level) => 2 + (Math.Max(1, Math.Min(MaxLevel, level)) - 1) / 4;

        /// <summary>Point-buy cost of one score (8 costs 0, 13 costs 5, 14 costs 7, 15 costs 9); −1 if outside 8–15.</summary>
        public static int PointCost(int score)
        {
            if (score < PointBuyMin || score > PointBuyMax) return -1;
            if (score <= 13) return score - 8;
            return score == 14 ? 7 : 9;
        }

        /// <summary>Null if the six base scores are valid for the method, otherwise why not.</summary>
        public static string Check(AbilityMethod method, IReadOnlyList<int> scores)
        {
            if (scores == null || scores.Count != 6) return "Six ability scores are required.";
            switch (method)
            {
                case AbilityMethod.StandardArray:
                    var sorted = new List<int>(scores);
                    sorted.Sort((a, b) => b.CompareTo(a));
                    for (var i = 0; i < 6; i++)
                        if (sorted[i] != StandardArray[i]) return "The standard array assigns 15, 14, 13, 12, 10 and 8, each exactly once.";
                    return null;
                case AbilityMethod.PointBuy:
                    var total = 0;
                    foreach (var s in scores)
                    {
                        var cost = PointCost(s);
                        if (cost < 0) return $"Point buy scores must be {PointBuyMin}–{PointBuyMax} (got {s}).";
                        total += cost;
                    }

                    return total > PointBuyBudget ? $"Point buy spends {total} of {PointBuyBudget} points." : null;
                default:
                    return "Choose the standard array or point buy.";
            }
        }

        /// <summary>Points spent on six scores (invalid scores count as 0).</summary>
        public static int PointsSpent(IReadOnlyList<int> scores)
        {
            var total = 0;
            if (scores == null) return 0;
            foreach (var s in scores) total += Math.Max(0, PointCost(s));
            return total;
        }
    }

    public static class SkillRules
    {
        public static readonly IReadOnlyList<Skill> All = (Skill[])Enum.GetValues(typeof(Skill));

        /// <summary>The ability each skill uses.</summary>
        public static Ability AbilityOf(Skill s)
        {
            switch (s)
            {
                case Skill.Athletics: return Ability.Strength;
                case Skill.Acrobatics:
                case Skill.SleightOfHand:
                case Skill.Stealth: return Ability.Dexterity;
                case Skill.Arcana:
                case Skill.History:
                case Skill.Investigation:
                case Skill.Nature:
                case Skill.Religion: return Ability.Intelligence;
                case Skill.AnimalHandling:
                case Skill.Insight:
                case Skill.Medicine:
                case Skill.Perception:
                case Skill.Survival: return Ability.Wisdom;
                default: return Ability.Charisma; // Deception, Intimidation, Performance, Persuasion
            }
        }

        public static string Name(Skill s)
        {
            switch (s)
            {
                case Skill.AnimalHandling: return "Animal Handling";
                case Skill.SleightOfHand: return "Sleight of Hand";
                default: return s.ToString();
            }
        }

        public static bool TryParse(string key, out Skill skill) => Enum.TryParse(key, false, out skill) && Enum.IsDefined(typeof(Skill), skill);
    }
}
