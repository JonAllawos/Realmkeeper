using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>How a class feature is used in a fight (Phase 9).</summary>
    public enum FeatureActivation
    {
        Action = 0,
        BonusAction = 1,

        /// <summary>Costs no action (Action Surge, Reckless Attack, Font of Magic's conversion).</summary>
        Free = 2,

        /// <summary>Right after a qualifying hit this turn (Stunning Strike).</summary>
        AfterHit = 3,

        /// <summary>Applied by the rules whenever it applies (Sneak Attack, Uncanny Dodge, Evasion, auras, critical ranges …).</summary>
        Automatic = 4,
    }

    public enum FeatureTargeting
    {
        Self = 0,

        /// <summary>A willing creature (the user's side) within range.</summary>
        Ally = 1,

        /// <summary>A hostile creature within range.</summary>
        Enemy = 2,

        /// <summary>The creature last hit this turn.</summary>
        LastHit = 3,

        /// <summary>An ally or a foe, depending on the option chosen (Divine Spark).</summary>
        Either = 4,

        /// <summary>Phase 20A: an unoccupied square within range (Cloud's Jaunt).</summary>
        Point = 5,
    }

    /// <summary>A class feature as the combat engine uses it: cost, resource, target, range, status (data; resolved by the engine).</summary>
    public sealed class CombatFeatureDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>The class feature (in the character's rules) that grants it.</summary>
        public string RequiresFeature { get; set; }

        public string ClassId { get; set; }
        public FeatureActivation Activation { get; set; }
        public string ResourceId { get; set; }
        public int ResourceCost { get; set; }
        public FeatureTargeting Targeting { get; set; }
        public int Range { get; set; }

        /// <summary>Choices the feature offers (id, label), e.g. Divine Spark's heal or harm; empty for none.</summary>
        public IReadOnlyList<(string id, string label)> Options { get; set; } = Array.Empty<(string, string)>();

        /// <summary>Asks for an amount (Lay on Hands).</summary>
        public bool NeedsAmount { get; set; }

        public ImplementationStatus Status { get; set; }
        public string StatusNote { get; set; }

        public bool IsActive => Activation != FeatureActivation.Automatic;
    }

    /// <summary>
    /// The combat side of the class features (Phase 9, ADR-0039): what each active feature costs and targets, and the
    /// automatic ones the rules apply on their own. Features are matched to a character by the feature ID in its rules,
    /// so a feature works for anyone who has it (the player today; companions later). Features not listed here are
    /// accounted for in Documentation/Systems/ClassFeatures.md with their status.
    /// </summary>
    public static class CombatFeatureCatalog
    {
        private static readonly Lazy<IReadOnlyList<CombatFeatureDefinition>> List = new Lazy<IReadOnlyList<CombatFeatureDefinition>>(Build);

        public static IReadOnlyList<CombatFeatureDefinition> All => List.Value;

        public static CombatFeatureDefinition Find(string id)
        {
            if (id == null) return null;
            foreach (var f in All) if (f.Id == id) return f;
            return null;
        }

        private static CombatFeatureDefinition A(string id, string name, string cls, string requires, FeatureActivation act, FeatureTargeting target, string text,
            string resource = null, int cost = 0, int range = 0, ImplementationStatus status = ImplementationStatus.Functional, string note = null) => new CombatFeatureDefinition
        {
            Id = id, Name = name, ClassId = cls, RequiresFeature = requires, Activation = act, Targeting = target, Description = text, ResourceId = resource, ResourceCost = cost,
            Range = range, Status = status, StatusNote = note,
        };

        private static CombatFeatureDefinition Auto(string id, string name, string cls, string text, ImplementationStatus status = ImplementationStatus.Functional, string note = null) =>
            A(id, name, cls, id, FeatureActivation.Automatic, FeatureTargeting.Self, text, status: status, note: note);

        private static IReadOnlyList<CombatFeatureDefinition> Build() => new[]
        {
            // ----- From spells -----
            A("spell_dash", "Dash (Expeditious Retreat)", "", null, FeatureActivation.BonusAction, FeatureTargeting.Self, "Dash as a bonus action while Expeditious Retreat lasts."),

            // ----- Barbarian -----
            A("rage", "Rage", "barbarian", "rage", FeatureActivation.BonusAction, FeatureTargeting.Self,
                "Rage: bonus damage on Strength attacks, resistance to bludgeoning, piercing and slashing, advantage on Strength saves. No spells or concentration.", "rage", 1,
                status: ImplementationStatus.Partial, note: "Rage lasts the fight while kept up each turn; the 10-minute limit outside fights and the Strength check advantage are not modelled."),
            A("rage_extend", "Keep raging", "barbarian", "rage", FeatureActivation.BonusAction, FeatureTargeting.Self, "Spend the bonus action to keep your rage going this turn."),
            A("reckless_attack", "Reckless Attack", "barbarian", "reckless_attack", FeatureActivation.Free, FeatureTargeting.Self,
                "Before your first attack: advantage on Strength melee attacks this turn; attacks against you have advantage until your next turn."),
            Auto("danger_sense", "Danger Sense", "barbarian", "Advantage on Dexterity saving throws (through the rules engine's roll modes)."),
            Auto("frenzy", "Frenzy", "barbarian", "Raging and reckless: the first creature you hit each turn takes extra d6s equal to your rage damage."),

            // ----- Bard -----
            A("bardic_inspiration", "Bardic Inspiration", "bard", "bardic_inspiration", FeatureActivation.BonusAction, FeatureTargeting.Ally,
                "Give an ally within 60 feet an inspiration die. It is added on its own to the ally's next attack roll or saving throw that would fail and that the die can save.", "bardic_inspiration", 1, 60,
                ImplementationStatus.Partial, "The die is used automatically when it turns a failure into a success (the player does not choose); ability checks do not use it."),

            // ----- Cleric -----
            A("divine_spark", "Channel Divinity: Divine Spark", "cleric", "channel_divinity", FeatureActivation.Action, FeatureTargeting.Either,
                "Point your holy symbol at a creature within 30 feet: heal an ally (d8s + Wisdom) or sear a foe (Constitution save, half on a success).", "channel_divinity", 1, 30),
            A("preserve_life", "Channel Divinity: Preserve Life", "cleric", "preserve_life", FeatureActivation.Action, FeatureTargeting.Self,
                "Share out five times your cleric level in healing among bloodied allies within 30 feet (no one above half their maximum).", "channel_divinity", 1, 30),
            Auto("disciple_of_life", "Disciple of Life", "cleric", "Healing spells of level 1 or higher restore 2 + the spell's level more."),

            // ----- Druid -----
            A("wild_shape", "Wild Shape", "druid", "wild_shape", FeatureActivation.BonusAction, FeatureTargeting.Self,
                "Take a beast's form: its armour class, speed, Strength, Dexterity, Constitution and attacks; temporary hit points equal to your druid level.", "wild_shape", 1,
                status: ImplementationStatus.Partial, note: "Foundation: one known form (wolf). Other forms, form limits by level and the bestiary come later."),
            A("leave_wild_shape", "Leave Wild Shape", "druid", "wild_shape", FeatureActivation.BonusAction, FeatureTargeting.Self, "Return to your own form."),

            // ----- Fighter -----
            A("second_wind", "Second Wind", "fighter", "second_wind", FeatureActivation.BonusAction, FeatureTargeting.Self, "Regain 1d10 + your fighter level in hit points.", "second_wind", 1),
            A("action_surge", "Action Surge", "fighter", "action_surge", FeatureActivation.Free, FeatureTargeting.Self,
                "Once per turn, take one more action (not a Magic action) after using yours.", "action_surge", 1),
            Auto("indomitable", "Indomitable", "fighter", "A failed saving throw is rerolled with your fighter level added (used automatically).",
                ImplementationStatus.Partial, "Used automatically on the first failed save while uses remain."),
            Auto("improved_critical", "Improved Critical", "fighter", "Weapon attacks and unarmed strikes score a critical hit on 19 or 20 (18 with Superior Critical)."),
            Auto("weapon_mastery", "Weapon Mastery", "", "Vex, Sap, Slow, Topple, Push and Graze apply on a hit (or a miss, for Graze) with weapons you are proficient with.",
                ImplementationStatus.Partial, "Applies to every proficient weapon (the chosen-weapons limit is not tracked); Nick and Cleave are not modelled."),

            // ----- Monk -----
            A("martial_arts_strike", "Martial Arts: Unarmed Strike", "monk", "martial_arts", FeatureActivation.BonusAction, FeatureTargeting.Enemy,
                "After the Attack action, one unarmed strike as a bonus action.", range: 5),
            A("flurry_of_blows", "Flurry of Blows", "monk", "monks_focus", FeatureActivation.BonusAction, FeatureTargeting.Enemy, "Spend 1 focus point: two unarmed strikes.", "focus_points", 1, 5),
            A("patient_defense", "Patient Defence", "monk", "monks_focus", FeatureActivation.BonusAction, FeatureTargeting.Self, "Disengage; or spend 1 focus point to Disengage and Dodge."),
            A("step_of_the_wind", "Step of the Wind", "monk", "monks_focus", FeatureActivation.BonusAction, FeatureTargeting.Self, "Dash; or spend 1 focus point to Dash and Disengage."),
            A("stunning_strike", "Stunning Strike", "monk", "stunning_strike", FeatureActivation.AfterHit, FeatureTargeting.LastHit,
                "Once per turn after hitting with an unarmed strike or monk weapon, spend 1 focus point: a Constitution save or stunned until your next turn (on a success, half speed and the next attack against it has advantage).",
                "focus_points", 1, 5),
            A("wholeness_of_body", "Wholeness of Body", "monk", "wholeness_of_body", FeatureActivation.BonusAction, FeatureTargeting.Self,
                "Regain a martial arts die + your Wisdom modifier in hit points.", "wholeness_of_body", 1),
            Auto("evasion", "Evasion", "monk", "Dexterity saves for half damage: no damage on a success, half on a failure (monks and rogues)."),

            // ----- Paladin -----
            A("lay_on_hands", "Lay on Hands", "paladin", "lay_on_hands", FeatureActivation.BonusAction, FeatureTargeting.Ally,
                "Touch a creature and restore hit points from your pool (5 × paladin level per long rest).", "lay_on_hands", 0, 5),
            A("sacred_weapon", "Channel Divinity: Sacred Weapon", "paladin", "sacred_weapon", FeatureActivation.Free, FeatureTargeting.Self,
                "Imbue your weapon: add your Charisma modifier to weapon attack rolls for the rest of the fight.", "channel_divinity", 1,
                status: ImplementationStatus.Partial, note: "Lasts the fight; the light it sheds is not modelled."),
            Auto("aura_of_protection", "Aura of Protection", "paladin", "You and allies within 10 feet (30 with Aura Expansion) add your Charisma modifier to saving throws."),

            // ----- Rogue -----
            A("cunning_dash", "Cunning Action: Dash", "rogue", "cunning_action", FeatureActivation.BonusAction, FeatureTargeting.Self, "Dash as a bonus action."),
            A("cunning_disengage", "Cunning Action: Disengage", "rogue", "cunning_action", FeatureActivation.BonusAction, FeatureTargeting.Self, "Disengage as a bonus action."),
            A("steady_aim", "Steady Aim", "rogue", "steady_aim", FeatureActivation.BonusAction, FeatureTargeting.Self, "If you have not moved: advantage on your next attack; your speed is 0 this turn."),
            Auto("sneak_attack", "Sneak Attack", "rogue", "Once per turn, extra d6s on a finesse or ranged weapon hit with advantage, or with an ally next to the target and no disadvantage."),
            Auto("uncanny_dodge", "Uncanny Dodge", "rogue", "Your reaction halves the damage of an attack that hits you (used automatically when you can react)."),

            // ----- Sorcerer -----
            A("innate_sorcery", "Innate Sorcery", "sorcerer", "innate_sorcery", FeatureActivation.BonusAction, FeatureTargeting.Self,
                "For a minute: +1 to your spell save DC and advantage on your spell attacks.", "innate_sorcery", 1),
            new CombatFeatureDefinition
            {
                Id = "font_create_slot", Name = "Font of Magic: create a slot", ClassId = "sorcerer", RequiresFeature = "font_of_magic", Activation = FeatureActivation.BonusAction,
                Targeting = FeatureTargeting.Self, Description = "Spend sorcery points for a spell slot: level 1 for 2, 2 for 3, 3 for 5, 4 for 6, 5 for 7.",
                Options = new[] { ("1", "Level 1 (2 points)"), ("2", "Level 2 (3 points)"), ("3", "Level 3 (5 points)"), ("4", "Level 4 (6 points)"), ("5", "Level 5 (7 points)") },
            },
            new CombatFeatureDefinition
            {
                Id = "font_convert_slot", Name = "Font of Magic: convert a slot", ClassId = "sorcerer", RequiresFeature = "font_of_magic", Activation = FeatureActivation.Free,
                Targeting = FeatureTargeting.Self, Description = "Turn a spell slot into sorcery points equal to its level.",
                Options = new[] { ("1", "Level 1 slot"), ("2", "Level 2 slot"), ("3", "Level 3 slot"), ("4", "Level 4 slot"), ("5", "Level 5 slot") },
            },
            Auto("metamagic", "Metamagic", "sorcerer", "Careful, Distant, Empowered, Heightened, Quickened and Twinned Spell shape a spell as you cast it.",
                ImplementationStatus.Partial, "Extended, Seeking, Subtle and Transmuted Spell are not simulated yet."),

            // ----- Species (Phase 20A): the goliath's giant ancestries, each use spending one of the ancestry's uses -----
            A("clouds_jaunt", "Cloud's Jaunt", "", "species:giant_ancestry:cloud", FeatureActivation.BonusAction, FeatureTargeting.Point,
                "Vanish and reappear in an unoccupied square you can see within 30 ft.", "stones_endurance", 1, 30),
            A("fires_burn", "Fire's Burn", "", "species:giant_ancestry:fire", FeatureActivation.AfterHit, FeatureTargeting.LastHit,
                "After hitting a creature with an attack roll: it takes 1d10 fire damage.", "stones_endurance", 1),
            A("frosts_chill", "Frost's Chill", "", "species:giant_ancestry:frost", FeatureActivation.AfterHit, FeatureTargeting.LastHit,
                "After hitting a creature with an attack roll: it takes 1d6 cold damage and its speed drops by 10 ft until the start of your next turn.", "stones_endurance", 1),
            A("hills_tumble", "Hill's Tumble", "", "species:giant_ancestry:hill", FeatureActivation.AfterHit, FeatureTargeting.LastHit,
                "After hitting a Large or smaller creature with an attack roll: it falls prone.", "stones_endurance", 1),
            A("stones_endurance_reaction", "Stone's Endurance", "", "species:giant_ancestry:stone", FeatureActivation.Automatic, FeatureTargeting.Self,
                "When you take damage in a fight, your reaction can reduce it by 1d12 + your Constitution modifier (one of the ancestry's uses).",
                "stones_endurance", 1, status: ImplementationStatus.Partial, note: "Offered against attacks, spells and saving-throw damage in a fight (Phase 22); hazards outside a fight (a trap, a fall) do not offer it, as no reaction is taken outside a fight."),
            A("storms_thunder", "Storm's Thunder", "", "species:giant_ancestry:storm", FeatureActivation.Automatic, FeatureTargeting.Self,
                "When a creature within 60 ft damages you, your reaction deals it 1d8 thunder damage (one of the ancestry's uses).",
                "stones_endurance", 1, 60, ImplementationStatus.Functional, "Offered after damage from a creature's attack or spell (Phase 22)."),

            // ----- Warlock -----
            Auto("eldritch_invocation", "Eldritch Invocations", "warlock", "Agonizing Blast adds Charisma to each Eldritch Blast beam; Armour of Shadows casts Mage Armour at will.",
                ImplementationStatus.Partial, "Other invocations are listed on the sheet with what they wait for."),
            Auto("dark_ones_blessing", "Dark One's Blessing", "warlock", "Dropping a hostile creature to 0 hit points grants temporary hit points (Charisma + warlock level)."),

            // ----- Wizard -----
            Auto("potent_cantrip", "Potent Cantrip", "wizard", "A damaging cantrip that misses or is saved against still deals half damage."),
            Auto("sculpt_spells", "Sculpt Spells", "wizard", "Allies in your evocation's area (up to 1 + its level) succeed on its save and take no damage."),
            Auto("empowered_evocation", "Empowered Evocation", "wizard", "Add your Intelligence modifier to one damage roll of each evocation spell."),
        };
    }
}
