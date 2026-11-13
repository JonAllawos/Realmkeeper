using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// A character's RPG record (Phase 6), held by <c>PlayerCharacterData.Rpg</c> for the player and usable for
    /// any other character (Phase 7). Only choices and state are stored: species, background, class and
    /// subclass IDs, level and experience, base scores, choices (creation and level choices), hit points,
    /// resource uses, the experience ledger and the advancement history. Everything derivable (final scores,
    /// modifiers, proficiencies, skills, saves, maximum hit points, armour class, speed, features, resource
    /// maximums) is computed by <see cref="CharacterRules"/> and never saved.
    /// </summary>
    public sealed class CharacterSheetData
    {
        public const int MaxTitleLength = 40;
        public const int MaxHistoryLength = 1200;

        public CreationState State { get; set; } = CreationState.Pending;

        /// <summary>Optional epithet ("the Grey", "of Ashford").</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional personal history in the player's words.</summary>
        public string History { get; set; } = string.Empty;

        public const int MaxPronounsLength = 24;
        public const int MaxConceptLength = 80;

        /// <summary>Phase 20: how the character is referred to ("she/her", "they/them", or anything the player writes). Optional.</summary>
        public string Pronouns { get; set; } = string.Empty;

        /// <summary>Phase 20: the character in a line, in the player's words ("a disgraced notary who keeps other people's secrets"). Optional.</summary>
        public string Concept { get; set; } = string.Empty;

        public string SpeciesId { get; set; }
        public string BackgroundId { get; set; }
        public string ClassId { get; set; }

        /// <summary>Chosen at the class's subclass level (Phase 7).</summary>
        public string SubclassId { get; set; }

        public int Level { get; set; } = 1;

        /// <summary>Total experience; always the sum of <see cref="ExperienceLog"/>.</summary>
        public int Experience { get; set; }

        public AbilityMethod Method { get; set; }

        /// <summary>The six scores before species increases, in <see cref="Ability"/> order.</summary>
        public int[] BaseScores { get; set; } = new int[6];

        /// <summary>
        /// Choices by choice ID. Creation: "class:skills", "species:skillful", "species:ancestry",
        /// "class:fighting_style", "class:expertise", "class:divine_order", "equipment:{group}". Levels (Phase 7):
        /// "lv{level}:{name}", with a feat's own choices under "lv{level}:{name}:{local}".
        /// </summary>
        public Dictionary<string, List<string>> Choices { get; set; } = new Dictionary<string, List<string>>();

        public int CurrentHp { get; set; }
        public int TemporaryHp { get; set; }

        // ----- Phase 8: dying and death (SRD 5.2.1 death saving throws) -----

        /// <summary>Death saving throw successes (0–3) while at 0 hit points.</summary>
        public int DeathSaveSuccesses { get; set; }

        /// <summary>Death saving throw failures (0–3) while at 0 hit points.</summary>
        public int DeathSaveFailures { get; set; }

        /// <summary>At 0 hit points but no longer making death saves.</summary>
        public bool IsStable { get; set; }

        /// <summary>Dead (three failed death saves, or massive damage). Resurrection is not part of Phase 8.</summary>
        public bool IsDead { get; set; }

        // ----- Phase 9: spellcasting and rests -----

        /// <summary>Prepared spells, the preparation window and rest history (spell picks themselves are choices).</summary>
        public SpellcastingData Spellcasting { get; set; } = new SpellcastingData();

        /// <summary>Uses left of each resource (rage, spell slots, hit dice …), by resource ID.</summary>
        public Dictionary<string, int> Resources { get; set; } = new Dictionary<string, int>();

        /// <summary>The class and background kit has been given (exactly once, at creation).</summary>
        public bool StartingEquipmentGranted { get; set; }

        public WorldDate CreatedOn { get; set; }

        // ----- Phase 7 -----

        /// <summary>
        /// The rules version the sheet's choices were made under. Choices introduced by a later version may be
        /// missing ("outstanding") until the next advancement, which requires them and raises this to current.
        /// </summary>
        public int RulesVersion { get; set; } = ProgressionRules.CurrentRulesVersion;

        /// <summary>Every experience award, in order. Keys are unique, so a keyed award cannot be given twice.</summary>
        public List<ExperienceAward> ExperienceLog { get; set; } = new List<ExperienceAward>();

        /// <summary>One record per level gained after the first, in order.</summary>
        public List<AdvancementRecord> Advancements { get; set; } = new List<AdvancementRecord>();

        /// <summary>Phase 14 (ADR-0068): conditions that outlive a fight or come from outside one (a trap's poison, exhaustion).</summary>
        public List<LastingConditionData> Conditions { get; set; } = new List<LastingConditionData>();

        // ----- Phase 22 (ADR-0109) -----

        /// <summary>
        /// The next level-up while the player is still choosing: the picks made so far for level <c>Level + 1</c> (null when
        /// none). Nothing in it applies until the level-up is confirmed; it only keeps the player's progress across closing
        /// the window and saving.
        /// </summary>
        public LevelChoices PendingAdvancement { get; set; }

        /// <summary>The highest level the player has been told they can take (0: none yet), so the level-up notice is shown once per level.</summary>
        public int LevelUpNoticed { get; set; }

        public IReadOnlyList<string> Chosen(string choiceId) => Choices.TryGetValue(choiceId, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();

        public CharacterSheetData Clone()
        {
            var c = (CharacterSheetData)MemberwiseClone();
            c.Conditions = new List<LastingConditionData>();
            foreach (var k in Conditions ?? new List<LastingConditionData>()) c.Conditions.Add(k.Clone());
            c.BaseScores = (int[])BaseScores.Clone();
            c.Choices = new Dictionary<string, List<string>>();
            foreach (var pair in Choices) c.Choices[pair.Key] = new List<string>(pair.Value);
            c.Resources = new Dictionary<string, int>(Resources);
            c.ExperienceLog = new List<ExperienceAward>();
            foreach (var a in ExperienceLog) c.ExperienceLog.Add(a.Clone());
            c.Advancements = new List<AdvancementRecord>();
            foreach (var a in Advancements) c.Advancements.Add(a.Clone());
            c.Spellcasting = (Spellcasting ?? new SpellcastingData()).Clone();
            c.PendingAdvancement = PendingAdvancement?.Clone();
            return c;
        }
    }

    /// <summary>One award of experience (Phase 7).</summary>
    public sealed class ExperienceAward
    {
        /// <summary>Unique within the character; awards from the same source instance share a key, so repeats are refused.</summary>
        public string Key { get; set; }

        public ExperienceSource Source { get; set; }

        /// <summary>The quest, encounter, place or other thing the award is for (optional).</summary>
        public string SourceId { get; set; }

        public int Amount { get; set; }
        public WorldDate Date { get; set; }
        public string Reason { get; set; } = string.Empty;

        public ExperienceAward Clone() => (ExperienceAward)MemberwiseClone();
    }

    /// <summary>What happened at one level-up (Phase 7): the history behind the choices recorded under that level.</summary>
    public sealed class AdvancementRecord
    {
        public int Level { get; set; }
        public WorldDate Date { get; set; }

        /// <summary>The increase in maximum hit points at that moment.</summary>
        public int HitPointsGained { get; set; }

        /// <summary>A readable account: features, choices, resources.</summary>
        public string Summary { get; set; } = string.Empty;

        public AdvancementRecord Clone() => (AdvancementRecord)MemberwiseClone();
    }

    /// <summary>
    /// What a character wears, as far as the rules care (Phase 7): body armour, a shield, heavy armour. Built by
    /// the owner of the equipment (the player's from their equipment slots); a character with no known
    /// equipment (an NPC sheet) is unarmoured.
    /// </summary>
    public readonly struct Loadout
    {
        public Loadout(bool armor, bool shield, bool heavyArmor)
        {
            Armor = armor;
            Shield = shield;
            HeavyArmor = heavyArmor;
            BodyArmorId = null;
            ShieldId = null;
            BodyCondition = Realmkeeper.Domain.World.ItemCondition.Good;
            ShieldCondition = Realmkeeper.Domain.World.ItemCondition.Good;
            CarriedGrams = 0;
            ItemEffects = null;
        }

        /// <summary>Phase 8: the loadout of a worn suit of armour and shield, by item ID (either may be null).</summary>
        public Loadout(string bodyArmorId, string shieldId)
        {
            var body = Realmkeeper.Domain.World.ItemCatalog.Find(bodyArmorId)?.Armor;
            BodyArmorId = body != null && body.Category != Realmkeeper.Domain.World.ArmorCategory.Shield ? bodyArmorId : null;
            ShieldId = Realmkeeper.Domain.World.ItemCatalog.Find(shieldId)?.Armor?.Category == Realmkeeper.Domain.World.ArmorCategory.Shield ? shieldId : null;
            Armor = BodyArmorId != null;
            Shield = ShieldId != null;
            HeavyArmor = BodyArmorId != null && body.Category == Realmkeeper.Domain.World.ArmorCategory.Heavy;
            BodyCondition = Realmkeeper.Domain.World.ItemCondition.Good;
            ShieldCondition = Realmkeeper.Domain.World.ItemCondition.Good;
            CarriedGrams = 0;
            ItemEffects = null;
        }

        public bool Armor { get; }
        public bool Shield { get; }

        /// <summary>A suit of heavy armour is worn (Phase 8: chain mail).</summary>
        public bool HeavyArmor { get; }

        /// <summary>Phase 8: the worn suit of armour's item ID, or null.</summary>
        public string BodyArmorId { get; }

        /// <summary>Phase 8: the shield's item ID, or null.</summary>
        public string ShieldId { get; }

        /// <summary>
        /// Phase 11 (ADR-0052): the loadout of actual worn objects — their condition, the weight carried, and the rule
        /// effects of worn magic items (already filtered: attuned where attunement is required, not broken).
        /// </summary>
        public Loadout(string bodyArmorId, string shieldId, Realmkeeper.Domain.World.ItemCondition bodyCondition, Realmkeeper.Domain.World.ItemCondition shieldCondition,
            int carriedGrams, IReadOnlyList<(string Source, RuleEffect Effect)> itemEffects) : this(bodyArmorId, shieldId)
        {
            BodyCondition = BodyArmorId != null ? bodyCondition : Realmkeeper.Domain.World.ItemCondition.Good;
            ShieldCondition = ShieldId != null ? shieldCondition : Realmkeeper.Domain.World.ItemCondition.Good;
            CarriedGrams = carriedGrams;
            ItemEffects = itemEffects;
        }

        /// <summary>Phase 11: the worn armour's and shield's condition (Good when unknown or absent).</summary>
        public Realmkeeper.Domain.World.ItemCondition BodyCondition { get; }
        public Realmkeeper.Domain.World.ItemCondition ShieldCondition { get; }

        /// <summary>Phase 11: grams carried and worn (0 when unknown: an NPC sheet or a test).</summary>
        public int CarriedGrams { get; }

        /// <summary>Phase 11: rule effects from worn magic items, each with the item's name as its source.</summary>
        public IReadOnlyList<(string Source, RuleEffect Effect)> ItemEffects { get; }

        public Realmkeeper.Domain.World.ArmorProfile BodyArmor => Realmkeeper.Domain.World.ItemCatalog.Find(BodyArmorId)?.Armor;
        public Realmkeeper.Domain.World.ArmorProfile ShieldArmor => Realmkeeper.Domain.World.ItemCatalog.Find(ShieldId)?.Armor;

        public static readonly Loadout Unarmored = new Loadout(false, false, false);

        public bool Allows(EffectCondition condition)
        {
            switch (condition)
            {
                case EffectCondition.NoArmor: return !Armor;
                case EffectCondition.NoArmorNoShield: return !Armor && !Shield;
                case EffectCondition.NoHeavyArmor: return !HeavyArmor;
                case EffectCondition.WearingArmor: return Armor;
                default: return true;
            }
        }
    }
}

namespace Realmkeeper.Domain.Rpg
{
    using System.Collections.Generic;

    /// <summary>
    /// Phase 9: a character's spellcasting state that is not a choice — the prepared list (cleric, druid, paladin,
    /// ranger; the wizard from the spellbook), how much of it may change now, and the rest history. Definitions are never
    /// copied here: spells are referenced by catalog ID.
    /// </summary>
    public sealed class SpellcastingData
    {
        public const int MaxRestLog = 20;

        /// <summary>Prepared spell IDs (prepared casters and the wizard); empty for known casters.</summary>
        public List<string> Prepared { get; set; } = new List<string>();

        /// <summary>
        /// Prepared spells that may be replaced now: −1 = the whole list (after a long rest, for the cleric, druid and
        /// wizard), 1 = one (after a long rest, for the paladin and ranger), 0 = none until the next long rest. Adding
        /// spells while the list is below its limit is always allowed outside fights.
        /// </summary>
        public int PreparationChanges { get; set; } = -1;

        /// <summary>World minute (absolute: day × 1440 + minute) a long rest last finished, or −1 when never.</summary>
        public long LastLongRestEnded { get; set; } = -1;

        /// <summary>Rests taken, newest last (bounded by <see cref="MaxRestLog"/>).</summary>
        public List<RestRecord> RestLog { get; set; } = new List<RestRecord>();

        public SpellcastingData Clone()
        {
            var c = (SpellcastingData)MemberwiseClone();
            c.Prepared = new List<string>(Prepared);
            c.RestLog = new List<RestRecord>();
            foreach (var r in RestLog) c.RestLog.Add(r.Clone());
            return c;
        }
    }

    public enum RestKind
    {
        Short = 0,
        Long = 1,
    }

    /// <summary>One finished rest: when, what kind, and what it gave back.</summary>
    public sealed class RestRecord
    {
        /// <summary>Unique within the character (the absolute world minute the rest finished, with its kind); repeats are refused.</summary>
        public string Key { get; set; }

        public RestKind Kind { get; set; }
        public long EndedAt { get; set; }
        public string Summary { get; set; } = string.Empty;

        public RestRecord Clone() => (RestRecord)MemberwiseClone();
    }
}
