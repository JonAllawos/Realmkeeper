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

        public IReadOnlyList<string> Chosen(string choiceId) => Choices.TryGetValue(choiceId, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();

        public CharacterSheetData Clone()
        {
            var c = (CharacterSheetData)MemberwiseClone();
            c.BaseScores = (int[])BaseScores.Clone();
            c.Choices = new Dictionary<string, List<string>>();
            foreach (var pair in Choices) c.Choices[pair.Key] = new List<string>(pair.Value);
            c.Resources = new Dictionary<string, int>(Resources);
            c.ExperienceLog = new List<ExperienceAward>();
            foreach (var a in ExperienceLog) c.ExperienceLog.Add(a.Clone());
            c.Advancements = new List<AdvancementRecord>();
            foreach (var a in Advancements) c.Advancements.Add(a.Clone());
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
        }

        public bool Armor { get; }
        public bool Shield { get; }

        /// <summary>The item catalog has no heavy armour yet, so this is false for every player today.</summary>
        public bool HeavyArmor { get; }

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
