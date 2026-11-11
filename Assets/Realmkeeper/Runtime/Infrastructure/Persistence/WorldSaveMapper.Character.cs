using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v8 (Phase 6) and v9 (Phase 7): the character sheet. Only choices, state and history are written.</summary>
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
                RulesVersion = s.RulesVersion,
                ExperienceLog = new List<ExperienceAwardDto>(),
                Advancements = new List<AdvancementRecordDto>(),
            };
            foreach (var a in s.ExperienceLog)
                d.ExperienceLog.Add(new ExperienceAwardDto { Key = a.Key, Source = a.Source.ToString(), SourceId = a.SourceId, Amount = a.Amount, Date = a.Date.IsValidDate ? a.Date.ToIsoString() : null, Reason = a.Reason });
            foreach (var r in s.Advancements)
                d.Advancements.Add(new AdvancementRecordDto { Level = r.Level, Date = r.Date.IsValidDate ? r.Date.ToIsoString() : null, HitPointsGained = r.HitPointsGained, Summary = r.Summary });
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
                RulesVersion = d.RulesVersion,
            };
            if (d.BaseScores != null)
            {
                if (d.BaseScores.Count != 6) throw new SaveDataException("A character needs six base scores.");
                s.BaseScores = d.BaseScores.ToArray();
            }

            foreach (var pair in d.Choices ?? new Dictionary<string, List<string>>()) s.Choices[pair.Key] = new List<string>(pair.Value ?? new List<string>());
            foreach (var pair in d.Resources ?? new Dictionary<string, int>()) s.Resources[pair.Key] = pair.Value;
            foreach (var a in d.ExperienceLog ?? new List<ExperienceAwardDto>())
            {
                if (a == null) throw new SaveDataException("An experience award is empty.");
                s.ExperienceLog.Add(new ExperienceAward
                {
                    Key = a.Key, Source = ParseEnum<ExperienceSource>(a.Source, "experience source"), SourceId = a.SourceId, Amount = a.Amount,
                    Date = OptionalDate(a.Date, "experience date"), Reason = a.Reason ?? string.Empty,
                });
            }

            foreach (var r in d.Advancements ?? new List<AdvancementRecordDto>())
            {
                if (r == null) throw new SaveDataException("An advancement record is empty.");
                s.Advancements.Add(new AdvancementRecord { Level = r.Level, Date = OptionalDate(r.Date, "advancement date"), HitPointsGained = r.HitPointsGained, Summary = r.Summary ?? string.Empty });
            }

            return s;
        }
    }
}
