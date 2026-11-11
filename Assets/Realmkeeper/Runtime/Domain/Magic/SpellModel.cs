using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Magic
{
    // Phase 9: spells as immutable data. A definition says what a spell is and does; the combat engine resolves it
    // through the same attack, saving throw, damage, healing and condition routines every other action uses
    // (ADR-0037). Characters store only their selections (choices and prepared lists), never copies of definitions.

    public enum SpellSchool
    {
        Abjuration = 0,
        Conjuration = 1,
        Divination = 2,
        Enchantment = 3,
        Evocation = 4,
        Illusion = 5,
        Necromancy = 6,
        Transmutation = 7,
    }

    public enum CastingTime
    {
        Action = 0,
        BonusAction = 1,
        Reaction = 2,

        /// <summary>A minute or more: never in combat.</summary>
        Longer = 3,
    }

    /// <summary>What the caster aims the spell at.</summary>
    public enum SpellTargeting
    {
        /// <summary>The caster only.</summary>
        Self = 0,

        /// <summary>Chosen creatures (up to <see cref="SpellDefinition.MaxTargets"/>), each within range.</summary>
        Creatures = 1,

        /// <summary>An area around a point within range (sphere, cube, cylinder).</summary>
        Point = 2,

        /// <summary>An area that starts at the caster (cone, line, cube), aimed toward a point.</summary>
        FromSelf = 3,
    }

    /// <summary>Who may be chosen or is affected.</summary>
    public enum SpellAffects
    {
        Any = 0,
        Hostile = 1,

        /// <summary>The caster's side (willing creatures).</summary>
        Ally = 2,
    }

    public enum AreaShape
    {
        None = 0,
        Sphere = 1,
        Cube = 2,
        Cone = 3,
        Line = 4,
        Cylinder = 5,
    }

    public enum SpellResolution
    {
        /// <summary>No roll: the effects simply apply (healing, buffs, Magic Missile's darts).</summary>
        Automatic = 0,

        /// <summary>A spell attack roll against each target's AC.</summary>
        Attack = 1,

        /// <summary>Each target makes a saving throw against the spell save DC.</summary>
        Save = 2,
    }

    public enum SpellDurationKind
    {
        Instantaneous = 0,

        /// <summary>A number of encounter rounds (minutes and hours count as the rest of the fight: see the status note).</summary>
        Timed = 1,

        /// <summary>Until the start of the caster's next turn.</summary>
        UntilCasterNextTurn = 2,
    }

    /// <summary>How complete a catalog entry is. Only Functional and Partial spells can be cast.</summary>
    public enum ImplementationStatus
    {
        /// <summary>Every mechanical effect is resolved.</summary>
        Functional = 0,

        /// <summary>The core effect is resolved; what is not is named in the status note.</summary>
        Partial = 1,

        /// <summary>Listed and selectable, with no mechanical resolution (utility spells outside combat).</summary>
        DataOnly = 2,

        /// <summary>Needs a system that does not exist yet (reactions, summoning, resurrection); not castable.</summary>
        Deferred = 3,
    }

    public enum CombatEffectKind
    {
        /// <summary>Damage (half on a successful save when <see cref="SpellEffect.HalfOnSave"/>).</summary>
        Damage = 0,
        Healing = 1,
        TemporaryHitPoints = 2,

        /// <summary>A condition (on a failed save, or always when the spell has no save).</summary>
        Condition = 3,

        /// <summary>Ends one of the listed conditions on the target.</summary>
        RemoveCondition = 4,

        /// <summary>+Value to AC while the effect lasts.</summary>
        ArmorClassBonus = 5,

        /// <summary>Base AC becomes Value + Dex when no armour is worn (Mage Armour).</summary>
        ArmorClassBase = 6,

        /// <summary>Speed changes by Value feet (negative slows).</summary>
        SpeedChange = 7,

        /// <summary>Speed is multiplied by Value.</summary>
        SpeedMultiplier = 8,

        /// <summary>Adds the dice (or subtracts them when Value is −1) to attack rolls and saving throws (Bless, Bane).</summary>
        AttackAndSaveDie = 9,

        /// <summary>Attack rolls against the target have advantage while it lasts (Faerie Fire).</summary>
        AttackersHaveAdvantage = 10,

        /// <summary>The next attack roll against the target has advantage (Guiding Bolt); consumed.</summary>
        NextAttackAgainstHasAdvantage = 11,

        /// <summary>The target's next attack roll has disadvantage (Vicious Mockery); consumed.</summary>
        NextAttackHasDisadvantage = 12,

        /// <summary>The caster's attacks that hit the target deal the dice as extra damage (Hunter's Mark).</summary>
        ExtraDamageWhenCasterHits = 13,

        /// <summary>The target cannot take reactions (Shocking Grasp).</summary>
        NoReactions = 14,

        /// <summary>The caster moves to an unoccupied point within range without provoking (Misty Step).</summary>
        Teleport = 15,

        /// <summary>A failed save pushes the target Value feet away from the caster.</summary>
        Push = 16,

        /// <summary>Advantage on Dexterity saving throws (Haste).</summary>
        AdvantageOnDexteritySaves = 17,

        /// <summary>Raises the hit point maximum and current hit points by Value (Aid).</summary>
        HitPointMaximumBonus = 18,

        /// <summary>Dash as a bonus action while it lasts (Expeditious Retreat).</summary>
        BonusDash = 19,

        /// <summary>Ends spell effects of the given level or lower on the target (Dispel Magic).</summary>
        EndSpells = 20,

        /// <summary>The effect ends early when the target attacks, deals damage or casts a spell (Invisibility).</summary>
        EndsOnHostileAct = 21,

        // ----- Phase 9: class-feature effects (same records, same engine) -----

        /// <summary>Rage: bonus damage on Strength attacks, resistance to bludgeoning, piercing and slashing, advantage on Strength saves; no spells or concentration.</summary>
        Raging = 40,

        /// <summary>Reckless Attack: attack rolls against this creature have advantage until its next turn starts.</summary>
        RecklessDefender = 41,

        /// <summary>A Bardic Inspiration die, added automatically to the holder's next failed attack roll or saving throw it can turn into a success.</summary>
        InspirationDie = 42,

        /// <summary>The holder's next attack roll (against <c>RelatedId</c> when set) has advantage (Steady Aim, Vex); consumed.</summary>
        NextAttackAdvantage = 43,

        /// <summary>Wild Shape: the holder uses a beast form's statistics.</summary>
        WildShapeForm = 44,

        /// <summary>Innate Sorcery: +1 spell save DC and advantage on sorcerer spell attacks.</summary>
        InnateSorcery = 45,

        /// <summary>+Value to weapon attack rolls (Sacred Weapon).</summary>
        WeaponAttackBonus = 46,
    }

    /// <summary>One part of what a spell does to each target (data, resolved by the engine).</summary>
    public sealed class SpellEffect
    {
        public CombatEffectKind Kind { get; set; }
        public Dice Dice { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>Damage: half on a successful save (otherwise none).</summary>
        public bool HalfOnSave { get; set; }

        /// <summary>Healing: adds the spellcasting ability modifier.</summary>
        public bool AddAbilityModifier { get; set; }

        public ConditionType? Condition { get; set; }
        public IReadOnlyList<ConditionType> Conditions { get; set; } = Array.Empty<ConditionType>();
        public int Value { get; set; }

        /// <summary>Extra dice per slot level above the spell's level.</summary>
        public Dice PerSlotAbove { get; set; }

        /// <summary>The target repeats the save at the end of each of its turns, ending the condition on a success (Hold Person).</summary>
        public bool RepeatSaveEachTurn { get; set; }

        /// <summary>The target may spend its action on a Strength (Athletics) check against the DC to end it (Entangle).</summary>
        public bool EscapeWithStrengthCheck { get; set; }

        /// <summary>Only applies on a failed save (for spells with a save; damage uses <see cref="HalfOnSave"/>).</summary>
        public bool OnFailedSave { get; set; } = true;

        /// <summary>Lasts only until the end of the target's next turn instead of the spell's duration.</summary>
        public bool UntilTargetsNextTurnEnds { get; set; }

        /// <summary>Lasts only until the start of the caster's next turn.</summary>
        public bool UntilCastersNextTurnStarts { get; set; }

        /// <summary>Lasts only until the end of the caster's next turn.</summary>
        public bool UntilCastersNextTurnEnds { get; set; }
    }

    public sealed class SpellDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>Realmkeeper's own wording (mechanics from the source; text not copied).</summary>
        public string Description { get; set; }

        /// <summary>0 for cantrips, 1–9.</summary>
        public int Level { get; set; }

        public SpellSchool School { get; set; }
        public CastingTime CastingTime { get; set; }

        /// <summary>Range in feet; 0 = self; 5 with <see cref="Touch"/>.</summary>
        public int Range { get; set; }

        public bool Touch { get; set; }
        public SpellTargeting Targeting { get; set; }
        public SpellAffects Affects { get; set; }

        /// <summary>For <see cref="SpellTargeting.Creatures"/>: how many creatures at the base level.</summary>
        public int MaxTargets { get; set; } = 1;

        /// <summary>More creatures per slot level above the spell's level.</summary>
        public int TargetsPerSlotAbove { get; set; }

        /// <summary>Only humanoids can be targeted (Hold Person).</summary>
        public bool HumanoidsOnly { get; set; }

        public AreaShape Area { get; set; }

        /// <summary>Radius (sphere, cylinder), side (cube) or length (cone, line), in feet.</summary>
        public int AreaSize { get; set; }

        public bool Verbal { get; set; } = true;
        public bool Somatic { get; set; } = true;
        public string Material { get; set; }

        public SpellDurationKind Duration { get; set; }

        /// <summary>For timed spells: rounds (10 for a minute; longer spells last the rest of the fight).</summary>
        public int DurationRounds { get; set; }

        /// <summary>The duration as the rules state it ("1 minute", "1 hour", "8 hours").</summary>
        public string DurationText { get; set; } = "Instantaneous";

        public bool Concentration { get; set; }
        public bool Ritual { get; set; }

        /// <summary>Class IDs whose spell list includes it.</summary>
        public IReadOnlyList<string> Classes { get; set; } = Array.Empty<string>();

        public SpellResolution Resolution { get; set; }
        public Ability SaveAbility { get; set; }

        /// <summary>Separate attacks or darts (Scorching Ray, Magic Missile, Eldritch Blast); 1 for most spells.</summary>
        public int Instances { get; set; } = 1;

        public int InstancesPerSlotAbove { get; set; }

        /// <summary>Cantrips: damage dice (or beams for Eldritch Blast) grow at character levels 5, 11 and 17.</summary>
        public bool CantripScaling { get; set; }

        /// <summary>For Eldritch Blast: scaling adds beams instead of dice.</summary>
        public bool ScalesByInstances { get; set; }

        /// <summary>Cast as a rider right after the caster hits with a melee weapon or unarmed strike (Divine Smite).</summary>
        public bool AfterMeleeHit { get; set; }

        /// <summary>Extra damage dice against fiends and undead (Divine Smite).</summary>
        public int ExtraDiceAgainstFiendsAndUndead { get; set; }

        public IReadOnlyList<SpellEffect> Effects { get; set; } = Array.Empty<SpellEffect>();

        /// <summary>How it scales when cast with a higher slot, in words.</summary>
        public string Upcast { get; set; }

        public ContentSource Source { get; set; } = ContentSource.Srd521;

        /// <summary>The rules configuration the entry was written for.</summary>
        public int RulesVersion { get; set; } = SpellCatalog.RulesVersion;

        public ImplementationStatus Status { get; set; }

        /// <summary>What is simplified or missing (always set unless Functional).</summary>
        public string StatusNote { get; set; }

        public bool IsCantrip => Level == 0;
        public bool Castable => Status == ImplementationStatus.Functional || Status == ImplementationStatus.Partial;
        public bool IsAttack => Resolution == SpellResolution.Attack;
        public bool HasArea => Area != AreaShape.None;

        public bool DealsDamage
        {
            get
            {
                foreach (var e in Effects) if (e.Kind == CombatEffectKind.Damage) return true;
                return false;
            }
        }

        public bool Heals
        {
            get
            {
                foreach (var e in Effects) if (e.Kind == CombatEffectKind.Healing) return true;
                return false;
            }
        }

        public string LevelText => Level == 0 ? $"{School} cantrip" : $"Level {Level} {School.ToString().ToLowerInvariant()}";

        public string ComponentsText => (Verbal ? "V" : string.Empty) + (Somatic ? (Verbal ? ", S" : "S") : string.Empty) + (Material != null ? ", M (" + Material + ")" : string.Empty);

        public string RangeText => Targeting == SpellTargeting.Self && Area == AreaShape.None ? "Self" :
            Targeting == SpellTargeting.FromSelf ? $"Self ({AreaSize}-ft {Area.ToString().ToLowerInvariant()})" :
            Touch ? "Touch" : Range + " ft" + (Area != AreaShape.None ? $" ({AreaSize}-ft {(Area == AreaShape.Sphere || Area == AreaShape.Cylinder ? "radius " : string.Empty)}{Area.ToString().ToLowerInvariant()})" : string.Empty);
    }
}
