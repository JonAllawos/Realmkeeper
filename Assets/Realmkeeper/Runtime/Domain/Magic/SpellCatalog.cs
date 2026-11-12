using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Magic
{
    /// <summary>
    /// The spell catalog (Phase 9, rules version 8). Mechanics follow the SRD 5.2.1 (CC BY 4.0); every description is
    /// Realmkeeper's own wording. Entries are chosen for the mechanics they exercise, not for count, and each one says
    /// honestly how complete it is. Spells whose current rules the project could not confirm are left out rather
    /// than guessed. Effects end when the fight ends: durations longer than a fight are noted on the spell.
    /// </summary>
    public static class SpellCatalog
    {
        public const int RulesVersion = 8;

        private static readonly Lazy<IReadOnlyList<SpellDefinition>> List = new Lazy<IReadOnlyList<SpellDefinition>>(Build);
        private static readonly Lazy<Dictionary<string, SpellDefinition>> Index = new Lazy<Dictionary<string, SpellDefinition>>(() =>
        {
            var d = new Dictionary<string, SpellDefinition>(StringComparer.Ordinal);
            foreach (var s in All) d[s.Id] = s;
            return d;
        });

        public static IReadOnlyList<SpellDefinition> All => List.Value;

        public static SpellDefinition Find(string id) => id != null && Index.Value.TryGetValue(id, out var s) ? s : null;

        /// <summary>A class's spell list at a spell level (0 = cantrips), in catalog order.</summary>
        public static List<SpellDefinition> ForClass(string classId, int level)
        {
            var list = new List<SpellDefinition>();
            foreach (var s in All)
                if (s.Level == level && Contains(s.Classes, classId)) list.Add(s);
            return list;
        }

        public static bool OnList(SpellDefinition s, string classId) => s != null && Contains(s.Classes, classId);

        private static bool Contains(IReadOnlyList<string> list, string x)
        {
            foreach (var y in list) if (y == x) return true;
            return false;
        }

        // ----- Helpers -------------------------------------------------------------------------------------

        private const string B = "bard", C = "cleric", D = "druid", P = "paladin", R = "ranger", S = "sorcerer", K = "warlock", W = "wizard";

        private static string[] Cls(params string[] c) => c;

        private static SpellEffect Dmg(int n, int sides, DamageType t, bool half = false, int perSlot = 0) =>
            new SpellEffect { Kind = CombatEffectKind.Damage, Dice = new Dice(n, sides), DamageType = t, HalfOnSave = half, PerSlotAbove = new Dice(perSlot, sides) };

        private static SpellEffect Heal(int n, int sides, int perSlotDice = 0) =>
            new SpellEffect { Kind = CombatEffectKind.Healing, Dice = new Dice(n, sides), AddAbilityModifier = true, PerSlotAbove = new Dice(perSlotDice, sides) };

        private static SpellEffect Cond(ConditionType c) => new SpellEffect { Kind = CombatEffectKind.Condition, Condition = c };

        private static SpellEffect Fx(CombatEffectKind k, int value = 0) => new SpellEffect { Kind = k, Value = value };

        private static IReadOnlyList<SpellDefinition> Build() => new[]
        {
            // ===== Cantrips ====================================================================================
            new SpellDefinition
            {
                Id = "fire_bolt", Name = "Fire Bolt", Level = 0, School = SpellSchool.Evocation, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(S, W), Resolution = SpellResolution.Attack, CantripScaling = true, Effects = new[] { Dmg(1, 10, DamageType.Fire) },
                Description = "A spark of flame hurled at a foe.", Upcast = "One more d10 at character levels 5, 11 and 17.",
            },
            new SpellDefinition
            {
                Id = "ray_of_frost", Name = "Ray of Frost", Level = 0, School = SpellSchool.Evocation, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(S, W), Resolution = SpellResolution.Attack, CantripScaling = true,
                Effects = new[] { Dmg(1, 8, DamageType.Cold), new SpellEffect { Kind = CombatEffectKind.SpeedChange, Value = -10, UntilCastersNextTurnStarts = true } },
                Description = "A pale beam that numbs and slows what it strikes.", Upcast = "One more d8 at character levels 5, 11 and 17.",
            },
            new SpellDefinition
            {
                Id = "shocking_grasp", Name = "Shocking Grasp", Level = 0, School = SpellSchool.Evocation, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(S, W), Resolution = SpellResolution.Attack, CantripScaling = true,
                Effects = new[] { Dmg(1, 8, DamageType.Lightning), new SpellEffect { Kind = CombatEffectKind.NoReactions, UntilTargetsNextTurnEnds = true } },
                Description = "Lightning leaps from your hand; the jolted creature cannot make opportunity attacks for a moment.", Upcast = "One more d8 at character levels 5, 11 and 17.",
                Status = ImplementationStatus.Partial, StatusNote = "The target cannot take any reaction (the rule names only opportunity attacks) until its next turn ends.",
            },
            new SpellDefinition
            {
                Id = "sacred_flame", Name = "Sacred Flame", Level = 0, IgnoresCover = true, School = SpellSchool.Evocation, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(C), Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, CantripScaling = true, Effects = new[] { Dmg(1, 8, DamageType.Radiant) },
                Description = "Radiance falls on a creature you can see; it must dodge or burn.", Upcast = "One more d8 at character levels 5, 11 and 17.",
            },
            new SpellDefinition
            {
                Id = "eldritch_blast", Name = "Eldritch Blast", Level = 0, School = SpellSchool.Evocation, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile, MaxTargets = 4,
                Classes = Cls(K), Resolution = SpellResolution.Attack, CantripScaling = true, ScalesByInstances = true, Effects = new[] { Dmg(1, 10, DamageType.Force) },
                Description = "Beams of crackling force; each beam is its own attack.", Upcast = "Two beams at character level 5, three at 11, four at 17.",
            },
            new SpellDefinition
            {
                Id = "vicious_mockery", Name = "Vicious Mockery", Level = 0, School = SpellSchool.Enchantment, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(B), Resolution = SpellResolution.Save, SaveAbility = Ability.Wisdom, CantripScaling = true, Somatic = false,
                Effects = new[] { Dmg(1, 6, DamageType.Psychic), new SpellEffect { Kind = CombatEffectKind.NextAttackHasDisadvantage, UntilTargetsNextTurnEnds = true } },
                Description = "A cutting insult laced with magic; the stung creature fumbles its next attack.", Upcast = "One more d6 at character levels 5, 11 and 17.",
            },
            new SpellDefinition
            {
                Id = "poison_spray", Name = "Poison Spray", Level = 0, School = SpellSchool.Necromancy, Range = 30, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(D, S, K, W), Resolution = SpellResolution.Attack, CantripScaling = true, Effects = new[] { Dmg(1, 12, DamageType.Poison) },
                Description = "A puff of toxic mist flung at a creature.", Upcast = "One more d12 at character levels 5, 11 and 17.",
            },
            new SpellDefinition
            {
                Id = "light", Name = "Light", Level = 0, School = SpellSchool.Evocation, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Any,
                Classes = Cls(B, C, S, W), Material = "a firefly or phosphorescent moss", Duration = SpellDurationKind.Timed, DurationText = "1 hour",
                Description = "An object you touch sheds bright light.", Status = ImplementationStatus.DataOnly, StatusNote = "Lighting is not simulated; the spell is listed for the spellbook only.",
            },
            new SpellDefinition
            {
                Id = "mage_hand", Name = "Mage Hand", Level = 0, School = SpellSchool.Conjuration, Range = 30, Targeting = SpellTargeting.Point, Affects = SpellAffects.Any,
                Classes = Cls(B, S, K, W), Duration = SpellDurationKind.Timed, DurationText = "1 minute",
                Description = "A floating spectral hand that lifts and carries small things.", Status = ImplementationStatus.DataOnly, StatusNote = "Object interaction is not simulated.",
            },
            new SpellDefinition
            {
                Id = "guidance", Name = "Guidance", Level = 0, School = SpellSchool.Divination, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(C, D), Duration = SpellDurationKind.Timed, DurationText = "Concentration, up to 1 minute", Concentration = true,
                Description = "A whispered blessing that steadies a companion's next attempt at a skill.", Status = ImplementationStatus.DataOnly, StatusNote = "Skill checks outside combat do not take spell bonuses yet.",
            },

            // ===== Level 1 =====================================================================================
            new SpellDefinition
            {
                Id = "magic_missile", Name = "Magic Missile", Level = 1, School = SpellSchool.Evocation, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile, MaxTargets = 3, TargetsPerSlotAbove = 1,
                Classes = Cls(S, W), Resolution = SpellResolution.Automatic, Instances = 3, InstancesPerSlotAbove = 1,
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.Damage, Dice = new Dice(1, 4, 1), DamageType = DamageType.Force } },
                Description = "Three glowing darts that never miss, split among targets as you choose.", Upcast = "One more dart per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "cure_wounds", Name = "Cure Wounds", Level = 1, School = SpellSchool.Abjuration, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(B, C, D, P, R), Resolution = SpellResolution.Automatic, Effects = new[] { Heal(2, 8, 2) },
                Description = "Your touch closes wounds.", Upcast = "2d8 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "healing_word", Name = "Healing Word", Level = 1, School = SpellSchool.Abjuration, CastingTime = CastingTime.BonusAction, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(B, C, D), Resolution = SpellResolution.Automatic, Somatic = false, Effects = new[] { Heal(2, 4, 2) },
                Description = "A spoken word that mends a creature you can see.", Upcast = "2d4 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "guiding_bolt", Name = "Guiding Bolt", Level = 1, School = SpellSchool.Evocation, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(C), Resolution = SpellResolution.Attack, Duration = SpellDurationKind.Timed, DurationRounds = 1, DurationText = "1 round",
                Effects = new[] { Dmg(4, 6, DamageType.Radiant, perSlot: 1), new SpellEffect { Kind = CombatEffectKind.NextAttackAgainstHasAdvantage, UntilCastersNextTurnEnds = true } },
                Description = "A flash of light that marks its target; the next attack against it has advantage.", Upcast = "1d6 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "burning_hands", Name = "Burning Hands", Level = 1, School = SpellSchool.Evocation, Targeting = SpellTargeting.FromSelf, Affects = SpellAffects.Any, Area = AreaShape.Cone, AreaSize = 15,
                Classes = Cls(S, W), Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, Effects = new[] { Dmg(3, 6, DamageType.Fire, true, 1) },
                Description = "A sheet of flame fans out from your hands.", Upcast = "1d6 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "thunderwave", Name = "Thunderwave", Level = 1, School = SpellSchool.Evocation, Targeting = SpellTargeting.FromSelf, Affects = SpellAffects.Any, Area = AreaShape.Cube, AreaSize = 15,
                Classes = Cls(B, D, S, W), Resolution = SpellResolution.Save, SaveAbility = Ability.Constitution,
                Effects = new[] { Dmg(2, 8, DamageType.Thunder, true, 1), new SpellEffect { Kind = CombatEffectKind.Push, Value = 10 } },
                Description = "A wave of thunder bursts from you, throwing creatures back.", Upcast = "1d8 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "shield_of_faith", Name = "Shield of Faith", Level = 1, School = SpellSchool.Abjuration, CastingTime = CastingTime.BonusAction, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(C, P), Material = "a prayer scroll", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 100, DurationText = "Concentration, up to 10 minutes",
                Effects = new[] { Fx(CombatEffectKind.ArmorClassBonus, 2) },
                Description = "A shimmering ward settles around a creature: +2 armour class.",
            },
            new SpellDefinition
            {
                Id = "bless", Name = "Bless", Level = 1, School = SpellSchool.Enchantment, Range = 30, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally, MaxTargets = 3, TargetsPerSlotAbove = 1,
                Classes = Cls(C, P), Material = "a holy symbol", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.AttackAndSaveDie, Dice = new Dice(1, 4), Value = 1 } },
                Description = "Up to three creatures add a d4 to their attack rolls and saving throws.", Upcast = "One more creature per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "bane", Name = "Bane", Level = 1, School = SpellSchool.Enchantment, Range = 30, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile, MaxTargets = 3, TargetsPerSlotAbove = 1,
                Classes = Cls(B, C), Material = "a drop of blood", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Resolution = SpellResolution.Save, SaveAbility = Ability.Charisma,
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.AttackAndSaveDie, Dice = new Dice(1, 4), Value = -1 } },
                Description = "Up to three creatures that fail a Charisma save subtract a d4 from attack rolls and saving throws.", Upcast = "One more creature per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "mage_armor", Name = "Mage Armour", Level = 1, School = SpellSchool.Abjuration, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(S, W), Material = "a piece of cured leather", Duration = SpellDurationKind.Timed, DurationRounds = 4800, DurationText = "8 hours",
                Effects = new[] { Fx(CombatEffectKind.ArmorClassBase, 13) },
                Description = "A willing creature not wearing armour has a base armour class of 13 + its Dexterity modifier.",
                Status = ImplementationStatus.Partial, StatusNote = "Lasts until the fight ends (out-of-combat durations are not tracked yet).",
            },
            new SpellDefinition
            {
                Id = "faerie_fire", Name = "Faerie Fire", Level = 1, School = SpellSchool.Evocation, Range = 60, Targeting = SpellTargeting.Point, Affects = SpellAffects.Any, Area = AreaShape.Cube, AreaSize = 20,
                Classes = Cls(B, D), Somatic = false, Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, Effects = new[] { Fx(CombatEffectKind.AttackersHaveAdvantage) },
                Description = "Creatures in the cube that fail a Dexterity save are outlined in light; attacks against them have advantage.",
                Status = ImplementationStatus.Partial, StatusNote = "Outlined creatures are not revealed from invisibility (no stealth or sight model).",
            },
            new SpellDefinition
            {
                Id = "inflict_wounds", Name = "Inflict Wounds", Level = 1, School = SpellSchool.Necromancy, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(C), Resolution = SpellResolution.Save, SaveAbility = Ability.Constitution, Effects = new[] { Dmg(2, 10, DamageType.Necrotic, true, 1) },
                Description = "A touch that withers flesh; a Constitution save halves it.", Upcast = "1d10 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "hunters_mark", Name = "Hunter's Mark", Level = 1, School = SpellSchool.Divination, CastingTime = CastingTime.BonusAction, Range = 90, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(R), Somatic = false, Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 600, DurationText = "Concentration, up to 1 hour",
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.ExtraDamageWhenCasterHits, Dice = new Dice(1, 6), DamageType = DamageType.Force } },
                Description = "You mark a quarry; your attacks that hit it deal an extra 1d6 force damage.",
                Status = ImplementationStatus.Partial, StatusNote = "Moving the mark to a new target when the first falls, and the tracking advantage, are not modelled.",
            },
            new SpellDefinition
            {
                Id = "divine_smite", Name = "Divine Smite", Level = 1, School = SpellSchool.Evocation, CastingTime = CastingTime.BonusAction, Range = 5, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(P), Somatic = false, AfterMeleeHit = true, ExtraDiceAgainstFiendsAndUndead = 1, Effects = new[] { Dmg(2, 8, DamageType.Radiant, perSlot: 1) },
                Description = "Right after you hit with a melee weapon or an unarmed strike, holy power adds radiant damage (more against fiends and undead).",
                Upcast = "1d8 more per slot level above 1.",
            },
            new SpellDefinition
            {
                Id = "expeditious_retreat", Name = "Expeditious Retreat", Level = 1, School = SpellSchool.Transmutation, CastingTime = CastingTime.BonusAction, Targeting = SpellTargeting.Self, Affects = SpellAffects.Ally,
                Classes = Cls(S, K, W), Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 100, DurationText = "Concentration, up to 10 minutes",
                Effects = new[] { Fx(CombatEffectKind.BonusDash) },
                Description = "You take the Dash action now, and can Dash as a bonus action on each later turn while the spell lasts.",
            },
            new SpellDefinition
            {
                Id = "entangle", Name = "Entangle", Level = 1, School = SpellSchool.Conjuration, Range = 90, Targeting = SpellTargeting.Point, Affects = SpellAffects.Any, Area = AreaShape.Cube, AreaSize = 20,
                Classes = Cls(D, R), Somatic = true, Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Resolution = SpellResolution.Save, SaveAbility = Ability.Strength,
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.Condition, Condition = ConditionType.Restrained, EscapeWithStrengthCheck = true } },
                Description = "Grasping weeds in a 20-foot square restrain creatures that fail a Strength save; a restrained creature can spend its action to tear free.",
                Status = ImplementationStatus.Partial, StatusNote = "The square does not become difficult terrain.",
            },
            new SpellDefinition
            {
                Id = "detect_magic", Name = "Detect Magic", Level = 1, School = SpellSchool.Divination, Targeting = SpellTargeting.Self, Ritual = true, Concentration = true,
                Classes = Cls(B, C, D, P, R, S, W), Duration = SpellDurationKind.Timed, DurationText = "Concentration, up to 10 minutes",
                Description = "You sense magic nearby.", Status = ImplementationStatus.DataOnly, StatusNote = "No magic items or auras exist in the world yet.",
            },
            new SpellDefinition
            {
                Id = "hellish_rebuke", Name = "Hellish Rebuke", Level = 1, School = SpellSchool.Evocation, CastingTime = CastingTime.Reaction, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile,
                Classes = Cls(K), Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, Effects = new[] { Dmg(2, 10, DamageType.Fire, true, 1) },
                Description = "When a creature harms you, flames engulf it.", Upcast = "1d10 more per slot level above 1.",
                Status = ImplementationStatus.Partial, StatusNote = "Offered as a reaction (Phase 10) after damage from a weapon or creature attack; damage from spells does not trigger it yet.",
            },
            new SpellDefinition
            {
                Id = "shield", Name = "Shield", Level = 1, School = SpellSchool.Abjuration, CastingTime = CastingTime.Reaction, Targeting = SpellTargeting.Self,
                Classes = Cls(S, W), Duration = SpellDurationKind.UntilCasterNextTurn, DurationText = "1 round", Effects = new[] { Fx(CombatEffectKind.ArmorClassBonus, 5) },
                Description = "An invisible barrier: +5 armour class until your next turn, cast when you are hit.",
                Status = ImplementationStatus.Partial, StatusNote = "Offered as a reaction (Phase 10) when a weapon or creature attack hits; spell attacks and Magic Missile do not trigger it yet.",
            },

            // ===== Level 2 =====================================================================================
            new SpellDefinition
            {
                Id = "scorching_ray", Name = "Scorching Ray", Level = 2, School = SpellSchool.Evocation, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile, MaxTargets = 3, TargetsPerSlotAbove = 1,
                Classes = Cls(S, W), Resolution = SpellResolution.Attack, Instances = 3, InstancesPerSlotAbove = 1, Effects = new[] { Dmg(2, 6, DamageType.Fire) },
                Description = "Three rays of fire, each its own attack, at one or several targets.", Upcast = "One more ray per slot level above 2.",
            },
            new SpellDefinition
            {
                Id = "shatter", Name = "Shatter", Level = 2, School = SpellSchool.Evocation, Range = 60, Targeting = SpellTargeting.Point, Affects = SpellAffects.Any, Area = AreaShape.Sphere, AreaSize = 10,
                Classes = Cls(B, S, K, W), Material = "a chip of mica", Resolution = SpellResolution.Save, SaveAbility = Ability.Constitution, Effects = new[] { Dmg(3, 8, DamageType.Thunder, true, 1) },
                Description = "A ringing burst of sound in a 10-foot sphere.", Upcast = "1d8 more per slot level above 2.",
            },
            new SpellDefinition
            {
                Id = "hold_person", Name = "Hold Person", Level = 2, School = SpellSchool.Enchantment, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Hostile, TargetsPerSlotAbove = 1, HumanoidsOnly = true,
                Classes = Cls(B, C, D, S, K, W), Material = "a straight piece of iron", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Resolution = SpellResolution.Save, SaveAbility = Ability.Wisdom,
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.Condition, Condition = ConditionType.Paralyzed, RepeatSaveEachTurn = true } },
                Description = "A humanoid that fails a Wisdom save is paralyzed; it saves again at the end of each of its turns.", Upcast = "One more humanoid per slot level above 2.",
            },
            new SpellDefinition
            {
                Id = "misty_step", Name = "Misty Step", Level = 2, School = SpellSchool.Conjuration, CastingTime = CastingTime.BonusAction, Range = 30, Targeting = SpellTargeting.Point, Affects = SpellAffects.Ally,
                Classes = Cls(S, K, W), Somatic = false, Effects = new[] { Fx(CombatEffectKind.Teleport, 30) },
                Description = "You vanish in silver mist and reappear in an unoccupied space you can see within 30 feet.",
            },
            new SpellDefinition
            {
                Id = "lesser_restoration", Name = "Lesser Restoration", Level = 2, School = SpellSchool.Abjuration, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(B, C, D, P, R),
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.RemoveCondition, Conditions = new[] { ConditionType.Paralyzed, ConditionType.Poisoned, ConditionType.Blinded, ConditionType.Deafened } } },
                Description = "Your touch ends one ailment: paralysis, poison, blindness or deafness (the first of these the creature has).",
                Status = ImplementationStatus.Partial, StatusNote = "Diseases are not simulated; the condition ended is the first in the listed order.",
            },
            new SpellDefinition
            {
                Id = "invisibility", Name = "Invisibility", Level = 2, School = SpellSchool.Illusion, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally, TargetsPerSlotAbove = 1,
                Classes = Cls(B, S, K, W), Material = "an eyelash in gum arabic", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 600, DurationText = "Concentration, up to 1 hour",
                Effects = new[] { Cond(ConditionType.Invisible), Fx(CombatEffectKind.EndsOnHostileAct) },
                Description = "A creature you touch turns invisible until it attacks, deals damage or casts a spell.", Upcast = "One more creature per slot level above 2.",
            },
            new SpellDefinition
            {
                Id = "aid", Name = "Aid", Level = 2, School = SpellSchool.Abjuration, Range = 30, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally, MaxTargets = 3,
                Classes = Cls(C, P), Material = "a strip of white cloth", Duration = SpellDurationKind.Timed, DurationRounds = 4800, DurationText = "8 hours",
                Effects = new[] { new SpellEffect { Kind = CombatEffectKind.HitPointMaximumBonus, Value = 5, PerSlotAbove = new Dice(0, 0, 5) } },
                Description = "Up to three creatures gain 5 to their hit point maximum and current hit points.", Upcast = "5 more per slot level above 2.",
                Status = ImplementationStatus.Partial, StatusNote = "Lasts until the fight ends (out-of-combat durations are not tracked yet); for the player, the current hit points gained stay.",
            },
            new SpellDefinition
            {
                Id = "spiritual_weapon", Name = "Spiritual Weapon", Level = 2, School = SpellSchool.Evocation, CastingTime = CastingTime.BonusAction, Range = 60, Targeting = SpellTargeting.Point,
                Classes = Cls(C), Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Description = "A floating spectral weapon that strikes at your command.", Status = ImplementationStatus.Deferred, StatusNote = "Summoned entities are a hook for a later phase.",
            },

            // ===== Level 3 =====================================================================================
            new SpellDefinition
            {
                Id = "fireball", Name = "Fireball", Level = 3, School = SpellSchool.Evocation, Range = 150, Targeting = SpellTargeting.Point, Affects = SpellAffects.Any, Area = AreaShape.Sphere, AreaSize = 20,
                Classes = Cls(S, W), Material = "a ball of bat guano and sulfur", Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, Effects = new[] { Dmg(8, 6, DamageType.Fire, true, 1) },
                Description = "A bead of fire that blossoms into a 20-foot sphere of flame.", Upcast = "1d6 more per slot level above 3.",
            },
            new SpellDefinition
            {
                Id = "lightning_bolt", Name = "Lightning Bolt", Level = 3, School = SpellSchool.Evocation, Targeting = SpellTargeting.FromSelf, Affects = SpellAffects.Any, Area = AreaShape.Line, AreaSize = 100,
                Classes = Cls(S, W), Material = "a bit of fur and a crystal rod", Resolution = SpellResolution.Save, SaveAbility = Ability.Dexterity, Effects = new[] { Dmg(8, 6, DamageType.Lightning, true, 1) },
                Description = "A 100-foot line of lightning.", Upcast = "1d6 more per slot level above 3.",
            },
            new SpellDefinition
            {
                Id = "haste", Name = "Haste", Level = 3, School = SpellSchool.Transmutation, Range = 30, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(S, W), Material = "a shaving of licorice root", Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Effects = new[] { Fx(CombatEffectKind.ArmorClassBonus, 2), Fx(CombatEffectKind.SpeedMultiplier, 2), Fx(CombatEffectKind.AdvantageOnDexteritySaves) },
                Description = "A willing creature moves at double speed, gains +2 armour class and has advantage on Dexterity saves.",
                Status = ImplementationStatus.Partial, StatusNote = "The extra limited action each turn and the lethargy when the spell ends are not modelled.",
            },
            new SpellDefinition
            {
                Id = "dispel_magic", Name = "Dispel Magic", Level = 3, School = SpellSchool.Abjuration, Range = 120, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Any,
                Classes = Cls(B, C, D, P, S, K, W), Effects = new[] { Fx(CombatEffectKind.EndSpells, 3) },
                Description = "Ends spells of 3rd level or lower affecting a creature.", Upcast = "Ends spells up to the slot level used.",
                Status = ImplementationStatus.Partial, StatusNote = "Spells above the slot level stay (the ability check to end them is not modelled).",
            },
            new SpellDefinition
            {
                Id = "revivify", Name = "Revivify", Level = 3, School = SpellSchool.Necromancy, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures,
                Classes = Cls(C, P), Material = "a diamond worth 300+ GP, consumed", Description = "Returns a creature dead less than a minute to life.",
                Status = ImplementationStatus.Deferred, StatusNote = "Resurrection is out of scope (ADR-0034).",
            },
            new SpellDefinition
            {
                Id = "spirit_guardians", Name = "Spirit Guardians", Level = 3, School = SpellSchool.Conjuration, Targeting = SpellTargeting.Self, Area = AreaShape.Sphere, AreaSize = 15,
                Classes = Cls(C), Concentration = true, DurationText = "Concentration, up to 10 minutes", Description = "Spirits circle you, harming foes who come near.",
                Status = ImplementationStatus.Deferred, StatusNote = "Moving auras that trigger on entering an area are not modelled yet.",
            },
            new SpellDefinition
            {
                Id = "counterspell", Name = "Counterspell", Level = 3, School = SpellSchool.Abjuration, CastingTime = CastingTime.Reaction, Range = 60, Targeting = SpellTargeting.Creatures,
                Classes = Cls(S, K, W), Description = "Interrupt a creature casting a spell.",
                Status = ImplementationStatus.Functional, StatusNote = "Offered as a reaction (Phase 10) when a hostile creature within 60 ft begins a spell: it saves (Constitution) or the spell fails, its slot unspent.",
            },

            // ===== Levels 4–5 ==================================================================================
            new SpellDefinition
            {
                Id = "greater_invisibility", Name = "Greater Invisibility", Level = 4, School = SpellSchool.Illusion, Range = 5, Touch = true, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally,
                Classes = Cls(B, S, W), Concentration = true, Duration = SpellDurationKind.Timed, DurationRounds = 10, DurationText = "Concentration, up to 1 minute",
                Effects = new[] { Cond(ConditionType.Invisible) }, Description = "A creature you touch stays invisible even while it attacks and casts.",
            },
            new SpellDefinition
            {
                Id = "cone_of_cold", Name = "Cone of Cold", Level = 5, School = SpellSchool.Evocation, Targeting = SpellTargeting.FromSelf, Affects = SpellAffects.Any, Area = AreaShape.Cone, AreaSize = 60,
                Classes = Cls(S, W), Material = "a small crystal or glass cone", Resolution = SpellResolution.Save, SaveAbility = Ability.Constitution, Effects = new[] { Dmg(8, 8, DamageType.Cold, true, 1) },
                Description = "A 60-foot cone of killing cold.", Upcast = "1d8 more per slot level above 5.",
            },
            new SpellDefinition
            {
                Id = "mass_cure_wounds", Name = "Mass Cure Wounds", Level = 5, School = SpellSchool.Abjuration, Range = 60, Targeting = SpellTargeting.Creatures, Affects = SpellAffects.Ally, MaxTargets = 6,
                Classes = Cls(B, C, D), Effects = new[] { Heal(5, 8, 1) },
                Description = "A wave of healing for up to six creatures within a 30-foot sphere.", Upcast = "1d8 more per slot level above 5.",
                Status = ImplementationStatus.Partial, StatusNote = "Targets are chosen creatures within range; the 30-foot grouping is not enforced.",
            },
        };

        /// <summary>Problems with the catalog itself; empty when it is valid (checked by the catalog validator and tests).</summary>
        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in All)
            {
                if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id)) errors.Add($"Spell '{s.Name}' has a missing or duplicate id.");
                if (string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Description)) errors.Add($"Spell {s.Id} needs a name and a description.");
                if (s.Level < 0 || s.Level > 9) errors.Add($"Spell {s.Id} has level {s.Level}.");
                if (s.Classes.Count == 0) errors.Add($"Spell {s.Id} is on no class list.");
                foreach (var c in s.Classes) if (RpgCatalog.FindClass(c)?.Spellcasting == null) errors.Add($"Spell {s.Id} names {c}, which does not cast spells.");
                if (s.Status != ImplementationStatus.Functional && string.IsNullOrWhiteSpace(s.StatusNote)) errors.Add($"Spell {s.Id} is {s.Status} without saying why.");
                if (s.Castable && s.Effects.Count == 0 && s.CastingTime != CastingTime.Reaction) errors.Add($"Spell {s.Id} is castable but does nothing.");
                if (s.Range < 0 || s.Touch && s.Range != 5) errors.Add($"Spell {s.Id} has an invalid range.");
                if (s.Targeting == SpellTargeting.Creatures && s.MaxTargets < 1) errors.Add($"Spell {s.Id} targets no creature.");
                if ((s.Targeting == SpellTargeting.Point || s.Targeting == SpellTargeting.FromSelf) && s.Area == AreaShape.None && !HasTeleport(s) && s.Castable)
                    errors.Add($"Spell {s.Id} aims at a point but has no area.");
                if (s.Area != AreaShape.None && s.AreaSize < 5) errors.Add($"Spell {s.Id} has an area smaller than a square.");
                if (s.Concentration && s.Duration == SpellDurationKind.Instantaneous && s.Castable) errors.Add($"Spell {s.Id} needs concentration but is instantaneous.");
                if (s.IsCantrip && (s.Upcast != null && !s.CantripScaling)) errors.Add($"Cantrip {s.Id} cannot be upcast.");
                if (s.AfterMeleeHit && s.CastingTime != CastingTime.BonusAction) errors.Add($"Spell {s.Id}: a smite is a bonus action.");
                foreach (var e in s.Effects)
                {
                    if ((e.Kind == CombatEffectKind.Damage || e.Kind == CombatEffectKind.Healing) && e.Dice.IsEmpty && e.Dice.Bonus == 0) errors.Add($"Spell {s.Id}: a {e.Kind} effect with no dice.");
                    if (e.Kind == CombatEffectKind.Condition && e.Condition == null) errors.Add($"Spell {s.Id}: a condition effect names no condition.");
                    if (e.Kind == CombatEffectKind.RemoveCondition && e.Conditions.Count == 0) errors.Add($"Spell {s.Id}: ends no condition.");
                    if (e.HalfOnSave && s.Resolution != SpellResolution.Save) errors.Add($"Spell {s.Id}: half damage needs a saving throw.");
                }
            }

            return errors;
        }

        private static bool HasTeleport(SpellDefinition s)
        {
            foreach (var e in s.Effects) if (e.Kind == CombatEffectKind.Teleport) return true;
            return false;
        }
    }
}
