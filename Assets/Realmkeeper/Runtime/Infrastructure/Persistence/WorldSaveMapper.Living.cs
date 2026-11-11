using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v12 (Phase 10): creature populations, creatures drawn into encounters, settlement herds and layouts.</summary>
    public static partial class WorldSaveMapper
    {
        private static void WriteLivingWorld(WorldData w, WorldDto dto)
        {
            dto.CreaturePopulations = new List<CreaturePopulationDto>();
            foreach (var p in w.CreaturePopulations)
                dto.CreaturePopulations.Add(new CreaturePopulationDto
                {
                    PopulationId = p.PopulationId, RegionId = p.RegionId.ToString(), CreatureId = p.CreatureId, Habitat = p.Habitat, CenterX = p.Center.X, CenterZ = p.Center.Z,
                    RangeCells = p.RangeCells, Count = p.Count, Capacity = p.Capacity, Deaths = p.Deaths, Drawn = p.Drawn, LastUpdatedOn = p.LastUpdatedOn.IsValidDate ? Date(p.LastUpdatedOn) : null,
                });

            dto.Creatures = new List<CreatureInstanceDto>();
            foreach (var c in w.Creatures)
                dto.Creatures.Add(new CreatureInstanceDto
                {
                    InstanceId = c.InstanceId, CreatureId = c.CreatureId, Name = c.Name, OriginId = c.OriginId, State = c.State.ToString(), CurrentHp = c.CurrentHp,
                    FirstSeenOn = c.FirstSeenOn.IsValidDate ? Date(c.FirstSeenOn) : null, DiedOn = c.DiedOn.IsValidDate ? Date(c.DiedOn) : null,
                    LastEncounterId = c.LastEncounterId.IsEmpty ? null : c.LastEncounterId.ToString(),
                });

            dto.Livestock = new List<LivestockHerdDto>();
            foreach (var h in w.Livestock)
                dto.Livestock.Add(new LivestockHerdDto
                {
                    HerdId = h.HerdId, SettlementId = h.SettlementId.ToString(), CreatureId = h.CreatureId, Count = h.Count, PastureKey = h.PastureKey,
                    KeeperNpcId = h.KeeperNpcId.IsEmpty ? null : h.KeeperNpcId.ToString(), LastUpdatedOn = h.LastUpdatedOn.IsValidDate ? Date(h.LastUpdatedOn) : null,
                });

            WriteSettlementLayouts(w, dto);
        }

        private static void ReadLivingWorld(WorldDto dto, WorldData world)
        {
            foreach (var d in dto.CreaturePopulations ?? new List<CreaturePopulationDto>())
            {
                if (d == null || string.IsNullOrWhiteSpace(d.PopulationId)) throw new SaveDataException("A creature population has no ID.");
                world.CreaturePopulations.Add(new CreaturePopulationData
                {
                    PopulationId = d.PopulationId, RegionId = new RegionId(RequiredGuid(d.RegionId, "population regionId")), CreatureId = d.CreatureId, Habitat = d.Habitat,
                    Center = new MapCoord(d.CenterX, d.CenterZ), RangeCells = d.RangeCells, Count = d.Count, Capacity = d.Capacity, Deaths = d.Deaths, Drawn = d.Drawn,
                    LastUpdatedOn = OptionalDate(d.LastUpdatedOn, "population lastUpdatedOn"),
                });
            }

            foreach (var d in dto.Creatures ?? new List<CreatureInstanceDto>())
            {
                if (d == null || string.IsNullOrWhiteSpace(d.InstanceId)) throw new SaveDataException("A creature has no ID.");
                world.Creatures.Add(new CreatureInstanceData
                {
                    InstanceId = d.InstanceId, CreatureId = d.CreatureId, Name = d.Name ?? string.Empty, OriginId = d.OriginId,
                    State = ParseEnum<CreatureInstanceState>(d.State, "creature state"), CurrentHp = d.CurrentHp,
                    FirstSeenOn = OptionalDate(d.FirstSeenOn, "creature firstSeenOn"), DiedOn = OptionalDate(d.DiedOn, "creature diedOn"),
                    LastEncounterId = new EncounterId(OptionalGuid(d.LastEncounterId, "creature lastEncounterId")),
                });
            }

            foreach (var d in dto.Livestock ?? new List<LivestockHerdDto>())
            {
                if (d == null || string.IsNullOrWhiteSpace(d.HerdId)) throw new SaveDataException("A herd has no ID.");
                world.Livestock.Add(new LivestockHerdData
                {
                    HerdId = d.HerdId, SettlementId = new SettlementId(RequiredGuid(d.SettlementId, "herd settlementId")), CreatureId = d.CreatureId, Count = d.Count,
                    PastureKey = d.PastureKey, KeeperNpcId = new NpcId(OptionalGuid(d.KeeperNpcId, "herd keeperNpcId")), LastUpdatedOn = OptionalDate(d.LastUpdatedOn, "herd lastUpdatedOn"),
                });
            }

            ReadSettlementLayouts(dto, world);
        }

        // ----- Reactions waiting on the player (schema v12) ----------------------------------------------------

        private static PendingReactionDto PendingDto(Domain.Combat.PendingReactionData p)
        {
            if (p == null) return null;
            var o = p.Opportunity;
            var a = p.Action;
            var d = new PendingReactionDto
            {
                Id = p.Id, Kind = o.Kind.ToString(), Name = o.Name, ReactorId = IdText(o.ReactorId), TriggerId = IdText(o.TriggerId), TargetId = IdText(o.TargetId),
                Trigger = o.Trigger, Cost = o.Cost, Consequence = o.Consequence, ActorId = IdText(p.ActorId), Sequence = p.Sequence, Answers = new List<bool>(p.Answers),
                Action = new ActionRecordDto
                {
                    Kind = a.Kind, Id = a.Id, Option = a.Option, Target = IdText(a.Target), Targets = new List<string>(), Path = new List<GridPosDto>(),
                    Point = a.HasPoint ? PosDto(a.Point) : null, Level = a.Level, Amount = a.Amount, Metamagic = new List<string>(a.Metamagic), FreeWith = a.FreeWith,
                },
            };
            foreach (var t in a.Targets) d.Action.Targets.Add(IdText(t));
            foreach (var s in a.Path) d.Action.Path.Add(PosDto(s));
            return d;
        }

        private static Domain.Combat.PendingReactionData Pending(PendingReactionDto d)
        {
            if (d == null) return null;
            if (d.Action == null || string.IsNullOrEmpty(d.Action.Kind)) throw new SaveDataException("A waiting reaction has no action.");
            var a = new Domain.Combat.ActionRecord
            {
                Kind = d.Action.Kind, Id = d.Action.Id, Option = d.Action.Option, Target = CombatantRef(d.Action.Target, "reaction action target"),
                HasPoint = d.Action.Point != null, Point = Pos(d.Action.Point), Level = d.Action.Level, Amount = d.Action.Amount,
                Metamagic = new List<string>(d.Action.Metamagic ?? new List<string>()), FreeWith = d.Action.FreeWith,
            };
            foreach (var t in d.Action.Targets ?? new List<string>()) a.Targets.Add(CombatantRef(t, "reaction action targets"));
            foreach (var s in d.Action.Path ?? new List<GridPosDto>()) a.Path.Add(Pos(s));
            if (Domain.Combat.ActionCodec.FromRecord(a) == null) throw new SaveDataException("A waiting reaction has an unknown action '" + a.Kind + "'.");
            return new Domain.Combat.PendingReactionData
            {
                Id = d.Id, ActorId = CombatantRef(d.ActorId, "reaction actorId"), Sequence = d.Sequence, Answers = new List<bool>(d.Answers ?? new List<bool>()), Action = a,
                Opportunity = new Domain.Combat.ReactionOpportunity
                {
                    Kind = ParseEnum<Domain.Combat.ReactionKind>(d.Kind, "reaction kind"), Name = d.Name ?? string.Empty, ReactorId = CombatantRef(d.ReactorId, "reaction reactorId"),
                    TriggerId = CombatantRef(d.TriggerId, "reaction triggerId"), TargetId = CombatantRef(d.TargetId, "reaction targetId"),
                    Trigger = d.Trigger ?? string.Empty, Cost = d.Cost ?? string.Empty, Consequence = d.Consequence ?? string.Empty,
                },
            };
        }

        private static EncounterOriginDto OriginDto(Domain.Combat.EncounterOriginData o) => o == null ? null : new EncounterOriginDto
        {
            Type = o.Type.ToString(), Objective = o.Objective.ToString(), ProtectedNpcId = o.ProtectedNpcId.IsEmpty ? null : o.ProtectedNpcId.ToString(),
            NearSettlementId = o.NearSettlementId.IsEmpty ? null : o.NearSettlementId.ToString(), Context = o.Context, Terrain = o.Terrain, SourceIds = new List<string>(o.SourceIds),
            XpBudget = o.XpBudget, XpTotal = o.XpTotal, Difficulty = o.Difficulty, PlanSeed = o.PlanSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Resolved = o.Resolved, Result = o.Result, ObjectiveMet = o.ObjectiveMet,
        };

        private static Domain.Combat.EncounterOriginData Origin(EncounterOriginDto d) => d == null ? null : new Domain.Combat.EncounterOriginData
        {
            Type = ParseEnum<Domain.Combat.EncounterType>(d.Type, "encounter type"), Objective = ParseEnum<Domain.Combat.EncounterObjective>(d.Objective, "encounter objective"),
            ProtectedNpcId = new NpcId(OptionalGuid(d.ProtectedNpcId, "origin protectedNpcId")), NearSettlementId = new SettlementId(OptionalGuid(d.NearSettlementId, "origin nearSettlementId")),
            Context = d.Context ?? string.Empty, Terrain = d.Terrain ?? string.Empty, SourceIds = new List<string>(d.SourceIds ?? new List<string>()),
            XpBudget = d.XpBudget, XpTotal = d.XpTotal, Difficulty = d.Difficulty ?? string.Empty,
            PlanSeed = string.IsNullOrEmpty(d.PlanSeed) ? 0UL : ulong.Parse(d.PlanSeed, System.Globalization.CultureInfo.InvariantCulture),
            Resolved = d.Resolved, Result = d.Result, ObjectiveMet = d.ObjectiveMet,
        };

        // Settlement layouts (Workstream E) are mapped in WorldSaveMapper.Layouts.cs.
        static partial void WriteSettlementLayouts(WorldData w, WorldDto dto);

        static partial void ReadSettlementLayouts(WorldDto dto, WorldData world);
    }
}
