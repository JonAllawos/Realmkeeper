using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v8 (Phase 6): the player's character sheet. Only choices and state are written.</summary>
    public static partial class WorldSaveMapper
    {
        private static CharacterSheetDto SheetDto(CharacterSheetData s)
        {
            if (s == null) return null;
            var d = new CharacterSheetDto
            {
                State = s.State.ToString(),
                Title = s.Title,
                History = s.History,
                SpeciesId = s.SpeciesId,
                BackgroundId = s.BackgroundId,
                ClassId = s.ClassId,
                SubclassId = s.SubclassId,
                Level = s.Level,
                Experience = s.Experience,
                Method = s.Method.ToString(),
                BaseScores = new List<int>(s.BaseScores ?? new int[6]),
                Choices = new Dictionary<string, List<string>>(),
                CurrentHp = s.CurrentHp,
                TemporaryHp = s.TemporaryHp,
                Resources = new Dictionary<string, int>(s.Resources),
                StartingEquipmentGranted = s.StartingEquipmentGranted,
                CreatedOn = s.CreatedOn.IsValidDate ? s.CreatedOn.ToIsoString() : null,
            };
            var keys = new List<string>(s.Choices.Keys);
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys) d.Choices[k] = new List<string>(s.Choices[k]);
            return d;
        }

        private static CharacterSheetData Sheet(CharacterSheetDto d)
        {
            if (d == null) throw new SaveDataException("The player has no character record (schema v8 requires one).");
            var s = new CharacterSheetData
            {
                State = ParseEnum<CreationState>(d.State, "character state"),
                Title = d.Title ?? string.Empty,
                History = d.History ?? string.Empty,
                SpeciesId = d.SpeciesId,
                BackgroundId = d.BackgroundId,
                ClassId = d.ClassId,
                SubclassId = d.SubclassId,
                Level = d.Level,
                Experience = d.Experience,
                Method = string.IsNullOrEmpty(d.Method) ? AbilityMethod.None : ParseEnum<AbilityMethod>(d.Method, "ability method"),
                CurrentHp = d.CurrentHp,
                TemporaryHp = d.TemporaryHp,
                StartingEquipmentGranted = d.StartingEquipmentGranted,
                CreatedOn = OptionalDate(d.CreatedOn, "character createdOn"),
            };
            if (d.BaseScores != null)
            {
                if (d.BaseScores.Count != 6) throw new SaveDataException("A character needs six base scores.");
                s.BaseScores = d.BaseScores.ToArray();
            }

            foreach (var pair in d.Choices ?? new Dictionary<string, List<string>>()) s.Choices[pair.Key] = new List<string>(pair.Value ?? new List<string>());
            foreach (var pair in d.Resources ?? new Dictionary<string, int>()) s.Resources[pair.Key] = pair.Value;
            return s;
        }
    }
}
