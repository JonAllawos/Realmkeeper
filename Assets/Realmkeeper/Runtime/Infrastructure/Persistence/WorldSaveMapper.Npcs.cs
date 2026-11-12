using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v6 (Phase 4): named people's inner life, locations and relationships.</summary>
    public static partial class WorldSaveMapper
    {
        private static string Date(WorldDate d) => d.IsValidDate ? d.ToIsoString() : null;

        private static string Id(Guid g) => g == Guid.Empty ? null : g.ToString();

        private static List<string> NpcIdStrings(IReadOnlyList<NpcId> ids)
        {
            var list = new List<string>(ids.Count);
            foreach (var id in ids) list.Add(id.ToString());
            return list;
        }

        private static List<NpcId> ParseNpcIds(List<string> ids, string field)
        {
            var list = new List<NpcId>();
            if (ids == null) return list;
            foreach (var id in ids) list.Add(new NpcId(RequiredGuid(id, field)));
            return list;
        }

        // ----- Domain → DTO --------------------------------------------------------------------

        private static void WriteNpc(NpcData n, NpcDto d)
        {
            d.Sex = n.Sex.ToString();
            d.BirthDate = Date(n.BirthDate);
            d.Tier = n.Tier.ToString();
            d.Personality = new Dictionary<string, int>();
            for (var i = 0; i < NpcPersonality.Count; i++) d.Personality[((NpcTrait)i).ToString()] = n.Personality[(NpcTrait)i];
            d.Skills = new Dictionary<string, int>();
            for (var i = 0; i < NpcSkills.Count; i++) d.Skills[((NpcSkill)i).ToString()] = n.Skills[(NpcSkill)i];
            var c = n.Condition;
            d.Condition = new NpcConditionDto { Health = c.Health, Satiation = c.Satiation, Energy = c.Energy, Morale = c.Morale, Safety = c.Safety, Wealth = c.Wealth };
            d.ResidenceId = Id(n.ResidenceId.Value);
            d.WorkplaceId = Id(n.WorkplaceId.Value);
            d.CurrentLocationId = Id(n.CurrentLocationId.Value);
            d.Schedule = new List<string>(NpcScheduleData.Days);
            for (var i = 0; i < NpcScheduleData.Days; i++) d.Schedule.Add(Id(n.Schedule[i].Value));
            d.Goals = new List<NpcGoalDto>();
            foreach (var g in n.Goals)
            {
                d.Goals.Add(new NpcGoalDto
                {
                    GoalId = g.GoalId.ToString(),
                    Type = g.Type.ToString(),
                    Priority = g.Priority,
                    Progress = g.Progress,
                    Status = g.Status.ToString(),
                    CreatedOn = Date(g.CreatedOn),
                    ClosedOn = Date(g.ClosedOn),
                    Source = g.Source.ToString(),
                    TargetSkill = g.TargetSkill?.ToString(),
                    TargetNpcId = Id(g.TargetNpcId.Value),
                });
            }

            d.Memories = new List<NpcMemoryDto>();
            foreach (var m in n.Memories)
            {
                d.Memories.Add(new NpcMemoryDto
                {
                    MemoryId = m.MemoryId.ToString(),
                    EventId = Id(m.EventId.Value),
                    Date = Date(m.Date),
                    Category = m.Category.ToString(),
                    Subject = m.Subject,
                    SettlementId = Id(m.SettlementId.Value),
                    FactionId = Id(m.FactionId.Value),
                    NpcIds = m.NpcIds.Count > 0 ? NpcIdStrings(m.NpcIds) : null,
                    Salience = m.Salience,
                    Confidence = m.Confidence,
                    Source = m.Source.ToString(),
                });
            }

            d.Knowledge = new List<NpcKnowledgeDto>();
            foreach (var k in n.Knowledge)
            {
                d.Knowledge.Add(new NpcKnowledgeDto
                {
                    KnowledgeId = k.KnowledgeId.ToString(),
                    Topic = k.Topic.ToString(),
                    SubjectId = k.SubjectId,
                    Value = k.Value,
                    Status = k.Status.ToString(),
                    Confidence = k.Confidence,
                    LearnedOn = Date(k.LearnedOn),
                    SourceNpcId = Id(k.SourceNpcId.Value),
                    Hops = k.Hops,
                    EventId = Id(k.EventId.Value),
                });
            }

            d.IsDeceased = n.IsDeceased;
            d.DiedOn = Date(n.DiedOn);
            d.LastUpdatedOn = Date(n.LastUpdatedOn);
            d.PlayerRegard = RegardDto(n.PlayerRegard);
            d.Character = NpcCharacterDto(n.Character); // Phase 10
        }

        private static void WriteNpcWorld(WorldData w, WorldDto dto)
        {
            WriteCommerceWorld(w, dto);
            dto.Locations = new List<LocationDto>();
            foreach (var l in w.Locations)
            {
                dto.Locations.Add(new LocationDto
                {
                    LocationId = l.LocationId.ToString(), SettlementId = l.SettlementId.ToString(), Kind = l.Kind.ToString(), Name = l.Name, LayoutKey = l.LayoutKey,
                });
            }

            dto.NpcRelations = new List<NpcRelationDto>();
            foreach (var r in w.NpcRelations)
            {
                dto.NpcRelations.Add(new NpcRelationDto
                {
                    NpcA = r.NpcA.ToString(),
                    NpcB = r.NpcB.ToString(),
                    Kind = r.Kind.ToString(),
                    Affinity = r.Affinity,
                    Trust = r.Trust,
                    Respect = r.Respect,
                    Fear = r.Fear,
                    Rivalry = r.Rivalry,
                    Origin = r.Origin.ToString(),
                    LastCause = r.LastCause.ToString(),
                    Since = Date(r.Since),
                    LastChanged = Date(r.LastChanged),
                    Interactions = r.Interactions,
                });
            }
        }

        // ----- DTO → Domain --------------------------------------------------------------------

        private static void ReadNpc(NpcDto d, NpcData n)
        {
            n.Sex = string.IsNullOrEmpty(d.Sex) ? NpcSex.Unspecified : ParseEnum<NpcSex>(d.Sex, "npc sex");
            n.BirthDate = OptionalDate(d.BirthDate, "npc birthDate");
            n.Tier = string.IsNullOrEmpty(d.Tier) ? NpcTier.Unassigned : ParseEnum<NpcTier>(d.Tier, "npc tier");
            if (d.Personality != null)
                foreach (var pair in d.Personality) n.Personality[ParseEnum<NpcTrait>(pair.Key, "npc trait")] = pair.Value;
            if (d.Skills != null)
                foreach (var pair in d.Skills) n.Skills[ParseEnum<NpcSkill>(pair.Key, "npc skill")] = pair.Value;
            if (d.Condition != null)
            {
                n.Condition = new NpcCondition
                {
                    Health = d.Condition.Health, Satiation = d.Condition.Satiation, Energy = d.Condition.Energy,
                    Morale = d.Condition.Morale, Safety = d.Condition.Safety, Wealth = d.Condition.Wealth,
                };
            }

            n.ResidenceId = new LocationId(OptionalGuid(d.ResidenceId, "npc residenceId"));
            n.WorkplaceId = new LocationId(OptionalGuid(d.WorkplaceId, "npc workplaceId"));
            n.CurrentLocationId = new LocationId(OptionalGuid(d.CurrentLocationId, "npc currentLocationId"));
            if (d.Schedule != null)
            {
                if (d.Schedule.Count != NpcScheduleData.Days) throw new SaveDataException($"NPC {n.NpcId} has a schedule of {d.Schedule.Count} days, not {NpcScheduleData.Days}.");
                for (var i = 0; i < NpcScheduleData.Days; i++) n.Schedule[i] = new LocationId(OptionalGuid(d.Schedule[i], "npc schedule"));
            }

            foreach (var g in d.Goals ?? new List<NpcGoalDto>())
            {
                if (g == null) throw new SaveDataException("Null NPC goal entry.");
                n.Goals.Add(new NpcGoalData
                {
                    GoalId = new GoalId(RequiredGuid(g.GoalId, "npc goalId")),
                    Type = ParseEnum<NpcGoalType>(g.Type, "npc goal type"),
                    Priority = g.Priority,
                    Progress = g.Progress,
                    Status = ParseEnum<NpcGoalStatus>(g.Status, "npc goal status"),
                    CreatedOn = OptionalDate(g.CreatedOn, "npc goal createdOn"),
                    ClosedOn = OptionalDate(g.ClosedOn, "npc goal closedOn"),
                    Source = string.IsNullOrEmpty(g.Source) ? ChangeSource.Simulation : ParseEnum<ChangeSource>(g.Source, "npc goal source"),
                    TargetSkill = string.IsNullOrEmpty(g.TargetSkill) ? (NpcSkill?)null : ParseEnum<NpcSkill>(g.TargetSkill, "npc goal skill"),
                    TargetNpcId = new NpcId(OptionalGuid(g.TargetNpcId, "npc goal target")),
                });
            }

            foreach (var m in d.Memories ?? new List<NpcMemoryDto>())
            {
                if (m == null) throw new SaveDataException("Null NPC memory entry.");
                n.Memories.Add(new NpcMemoryData
                {
                    MemoryId = new MemoryId(RequiredGuid(m.MemoryId, "npc memoryId")),
                    EventId = new EventId(OptionalGuid(m.EventId, "npc memory eventId")),
                    Date = ParseDate(m.Date, "npc memory date"),
                    Category = ParseEnum<MemoryCategory>(m.Category, "npc memory category"),
                    Subject = m.Subject,
                    SettlementId = new SettlementId(OptionalGuid(m.SettlementId, "npc memory settlementId")),
                    FactionId = new FactionId(OptionalGuid(m.FactionId, "npc memory factionId")),
                    NpcIds = ParseNpcIds(m.NpcIds, "npc memory npcIds"),
                    Salience = m.Salience,
                    Confidence = m.Confidence,
                    Source = ParseEnum<MemorySource>(m.Source, "npc memory source"),
                });
            }

            foreach (var k in d.Knowledge ?? new List<NpcKnowledgeDto>())
            {
                if (k == null) throw new SaveDataException("Null NPC knowledge entry.");
                n.Knowledge.Add(new NpcKnowledgeData
                {
                    KnowledgeId = new KnowledgeId(RequiredGuid(k.KnowledgeId, "npc knowledgeId")),
                    Topic = ParseEnum<KnowledgeTopic>(k.Topic, "npc knowledge topic"),
                    SubjectId = k.SubjectId,
                    Value = k.Value,
                    Status = ParseEnum<KnowledgeStatus>(k.Status, "npc knowledge status"),
                    Confidence = k.Confidence,
                    LearnedOn = OptionalDate(k.LearnedOn, "npc knowledge learnedOn"),
                    SourceNpcId = new NpcId(OptionalGuid(k.SourceNpcId, "npc knowledge source")),
                    Hops = k.Hops,
                    EventId = new EventId(OptionalGuid(k.EventId, "npc knowledge eventId")),
                });
            }

            n.IsDeceased = d.IsDeceased;
            n.DiedOn = OptionalDate(d.DiedOn, "npc diedOn");
            n.LastUpdatedOn = OptionalDate(d.LastUpdatedOn, "npc lastUpdatedOn");
            n.PlayerRegard = Regard(d.PlayerRegard);
            n.Character = NpcCharacter(d.Character, n.Name); // Phase 10
        }

        private static void ReadNpcWorld(WorldDto dto, WorldData world)
        {
            ReadCommerceWorld(dto, world);
            foreach (var l in dto.Locations ?? new List<LocationDto>())
            {
                if (l == null) throw new SaveDataException("Null location entry.");
                world.Locations.Add(new LocationData
                {
                    LocationId = new LocationId(RequiredGuid(l.LocationId, "locationId")),
                    SettlementId = new SettlementId(RequiredGuid(l.SettlementId, "location settlementId")),
                    Kind = ParseEnum<LocationKind>(l.Kind, "location kind"),
                    Name = l.Name,
                    LayoutKey = l.LayoutKey,
                });
            }

            foreach (var r in dto.NpcRelations ?? new List<NpcRelationDto>())
            {
                if (r == null) throw new SaveDataException("Null NPC relationship entry.");
                world.NpcRelations.Add(new NpcRelationData
                {
                    NpcA = new NpcId(RequiredGuid(r.NpcA, "relation npcA")),
                    NpcB = new NpcId(RequiredGuid(r.NpcB, "relation npcB")),
                    Kind = ParseEnum<NpcRelationKind>(r.Kind, "relation kind"),
                    Affinity = r.Affinity,
                    Trust = r.Trust,
                    Respect = r.Respect,
                    Fear = r.Fear,
                    Rivalry = r.Rivalry,
                    Origin = ParseEnum<NpcRelationCause>(r.Origin, "relation origin"),
                    LastCause = ParseEnum<NpcRelationCause>(r.LastCause, "relation cause"),
                    Since = OptionalDate(r.Since, "relation since"),
                    LastChanged = OptionalDate(r.LastChanged, "relation lastChanged"),
                    Interactions = r.Interactions,
                });
            }
        }
    
        // ----- Phase 10: a named person's mechanical record -----------------------------------------------

        private static NpcCharacterDto NpcCharacterDto(NpcCharacterData c) => c == null ? null : new NpcCharacterDto
        {
            ArchetypeId = c.ArchetypeId,
            Occupation = c.Occupation,
            Sheet = c.Sheet != null ? SheetDto(c.Sheet) : null,
            Body = c.Gear?.Body,
            Shield = c.Gear?.Shield,
            MainHand = c.Gear?.MainHand,
            OffHand = c.Gear?.OffHand,
            Carried = c.Gear?.Carried != null ? new List<string>(c.Gear.Carried) : new List<string>(),
            Purse = c.Purse,
            SheetSince = c.SheetSince.IsValidDate ? Date(c.SheetSince) : null,
            OwnsItems = c.OwnsItems,
            Inventory = c.OwnsItems ? c.Inventory.ConvertAll(StackDto) : null,
            Equipment = c.OwnsItems ? EquipmentDto(c.Equipment) : null,
        };

        private static Dictionary<string, ItemStackDto> EquipmentDto(Dictionary<EquipmentSlot, ItemStackData> equipment)
        {
            var d = new Dictionary<string, ItemStackDto>();
            foreach (var pair in equipment) d[pair.Key.ToString()] = StackDto(pair.Value);
            return d;
        }

        private static NpcCharacterData NpcCharacter(NpcCharacterDto d, string who)
        {
            if (d == null) return null;
            if (string.IsNullOrWhiteSpace(d.ArchetypeId)) throw new SaveDataException($"{who}'s character record has no archetype.");
            if (d.Purse < 0) throw new SaveDataException($"{who}'s purse is negative.");
            var made = new NpcCharacterData
            {
                ArchetypeId = d.ArchetypeId,
                Occupation = d.Occupation ?? string.Empty,
                Sheet = d.Sheet != null ? Sheet(d.Sheet) : null,
                Gear = new NpcGearData { Body = d.Body, Shield = d.Shield, MainHand = d.MainHand, OffHand = d.OffHand, Carried = d.Carried != null ? new List<string>(d.Carried) : new List<string>() },
                Purse = d.Purse,
                SheetSince = OptionalDate(d.SheetSince, "npc sheetSince"),
            };
            made.OwnsItems = d.OwnsItems;
            foreach (var s in d.Inventory ?? new List<ItemStackDto>()) made.Inventory.Add(Stack(s, who + " inventory"));
            foreach (var pair in d.Equipment ?? new Dictionary<string, ItemStackDto>())
                made.Equipment[ParseEnum<EquipmentSlot>(pair.Key, "equipment slot")] = Stack(pair.Value, who + " equipment");
            return made;
        }
    }
}
