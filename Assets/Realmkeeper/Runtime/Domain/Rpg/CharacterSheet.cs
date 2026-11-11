using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// The player's RPG record (Phase 6), held by <c>PlayerCharacterData.Rpg</c>. Only choices and state are
    /// stored: species, background and class IDs, base scores, creation choices, hit points and resource
    /// uses. Everything derivable (final scores, modifiers, proficiencies, skills, saves, maximum hit points,
    /// armour class, speed, languages) is computed by <see cref="CharacterRules"/> and never saved.
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

        /// <summary>Unset until the progression phase (subclasses are chosen at the class's subclass level).</summary>
        public string SubclassId { get; set; }

        public int Level { get; set; } = 1;
        public int Experience { get; set; }

        public AbilityMethod Method { get; set; }

        /// <summary>The six scores before species increases, in <see cref="Ability"/> order.</summary>
        public int[] BaseScores { get; set; } = new int[6];

        /// <summary>
        /// Creation choices by choice ID: "class:skills", "species:skillful", "species:ancestry",
        /// "class:fighting_style", "class:expertise", "class:divine_order", and "equipment:{group}" for kits.
        /// </summary>
        public Dictionary<string, List<string>> Choices { get; set; } = new Dictionary<string, List<string>>();

        public int CurrentHp { get; set; }
        public int TemporaryHp { get; set; }

        /// <summary>Uses left of each resource (rage, spell slots …), by resource ID.</summary>
        public Dictionary<string, int> Resources { get; set; } = new Dictionary<string, int>();

        /// <summary>The class and background kit has been given (exactly once, at creation).</summary>
        public bool StartingEquipmentGranted { get; set; }

        public WorldDate CreatedOn { get; set; }

        public IReadOnlyList<string> Chosen(string choiceId) => Choices.TryGetValue(choiceId, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();

        public CharacterSheetData Clone()
        {
            var c = (CharacterSheetData)MemberwiseClone();
            c.BaseScores = (int[])BaseScores.Clone();
            c.Choices = new Dictionary<string, List<string>>();
            foreach (var pair in Choices) c.Choices[pair.Key] = new List<string>(pair.Value);
            c.Resources = new Dictionary<string, int>(Resources);
            return c;
        }
    }
}
