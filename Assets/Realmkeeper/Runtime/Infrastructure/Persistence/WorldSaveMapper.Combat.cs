using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v10 (Phase 8): encounters (active and recently finished) and settlement structures.</summary>
    public static partial class WorldSaveMapper
    {
        private static string DateText(WorldDate d) => d.IsValidDate ? d.ToIsoString() : null;
        private static GridPosDto PosDto(GridPos p) => new GridPosDto { X = p.X, Y = p.Y, Z = p.Z };
        private static GridPos Pos(GridPosDto d) => d == null ? default : new GridPos(d.X, d.Y, d.Z);
        private static string IdText(CombatantId id) => id.IsEmpty ? null : id.ToString();
        private static CombatantId CombatantRef(string text, string field) => new CombatantId(OptionalGuid(text, field));

        private static void WriteCombatWorld(WorldData w, WorldDto dto)
        {
            dto.Encounters = new List<EncounterDto>();
            foreach (var e in w.Encounters)
            {
                var d = new EncounterDto
                {
                    EncounterId = e.EncounterId.ToString(), Name = e.Name, Kind = e.Kind.ToString(), Lethality = e.Lethality.ToString(),
                    SettlementId = e.SettlementId.IsEmpty ? null : e.SettlementId.ToString(), Status = e.Status.ToString(), Outcome = e.Outcome.ToString(),
                    WinningSide = e.WinningSide, Round = e.Round, TurnIndex = e.TurnIndex, Order = new List<string>(), Combatants = new List<CombatantDto>(),
                    DifficultTerrain = new List<GridPosDto>(), StartedOn = DateText(e.StartedOn), StartedAtMinute = e.StartedAtMinute, EndedOn = DateText(e.EndedOn),
                    Seed = e.Seed.ToString(CultureInfo.InvariantCulture), RollCount = e.RollCount, ActionSequence = e.ActionSequence, Log = new List<CombatLogEntryDto>(),
                    QuestRef = e.QuestRef, WorldEventRef = e.WorldEventRef,
                };
                foreach (var id in e.Order) d.Order.Add(id.ToString());
                foreach (var p in e.DifficultTerrain) d.DifficultTerrain.Add(PosDto(p));
                foreach (var c in e.Combatants)
                {
                    var cd = new CombatantDto
                    {
                        CombatantId = c.CombatantId.ToString(), Kind = c.Kind.ToString(), SourceId = c.SourceId, Name = c.Name, Side = c.Side,
                        FactionId = c.FactionId.IsEmpty ? null : c.FactionId.ToString(), Control = c.Control.ToString(), CreatureId = c.CreatureId,
                        MaxHp = c.MaxHp, CurrentHp = c.CurrentHp, TemporaryHp = c.TemporaryHp, DeathSaveSuccesses = c.DeathSaveSuccesses, DeathSaveFailures = c.DeathSaveFailures,
                        State = c.State.ToString(), UsesDeathSaves = c.UsesDeathSaves, Position = PosDto(c.Position), Conditions = new List<ConditionDto>(),
                        InitiativeRoll = c.InitiativeRoll, Initiative = c.Initiative, TieBreak = c.TieBreak, JoinedRound = c.JoinedRound,
                        Turn = new TurnResourcesDto
                        {
                            ActionUsed = c.Turn.ActionUsed, BonusActionUsed = c.Turn.BonusActionUsed, ReactionUsed = c.Turn.ReactionUsed, InteractionUsed = c.Turn.InteractionUsed,
                            MovementUsed = c.Turn.MovementUsed, MovementGranted = c.Turn.MovementGranted, LightAttackWeapon = c.Turn.LightAttackWeapon,
                            AttacksLeft = c.Turn.AttacksLeft, Disengaged = c.Turn.Disengaged, Dodging = c.Turn.Dodging,
                        },
                    };
                    foreach (var k in c.Conditions)
                        cd.Conditions.Add(new ConditionDto
                        {
                            Type = k.Type.ToString(), SourceId = IdText(k.SourceId), Reason = k.Reason, AppliedRound = k.AppliedRound, Duration = k.Duration.ToString(),
                            ExpiresOn = IdText(k.ExpiresOn), ExpiresRound = k.ExpiresRound, Level = k.Level, EscapeDc = k.EscapeDc,
                        });
                    d.Combatants.Add(cd);
                }

                foreach (var l in e.Log)
                    d.Log.Add(new CombatLogEntryDto
                    {
                        Sequence = l.Sequence, Round = l.Round, Kind = l.Kind.ToString(), ActorId = IdText(l.ActorId), TargetId = IdText(l.TargetId),
                        Text = l.Text, Rolls = new List<int>(l.Rolls), Value = l.Value,
                    });
                dto.Encounters.Add(d);
            }

            dto.Structures = new List<StructureDto>();
            foreach (var s in w.Structures)
                dto.Structures.Add(new StructureDto
                {
                    StructureId = s.StructureId.ToString(), DefinitionId = s.DefinitionId, Name = s.Name, SettlementId = s.SettlementId.ToString(),
                    LocationId = s.LocationId.IsEmpty ? null : s.LocationId.ToString(), ShopId = s.ShopId.IsEmpty ? null : s.ShopId.ToString(),
                    OwnerNpcId = s.OwnerNpcId.IsEmpty ? null : s.OwnerNpcId.ToString(), FactionId = s.FactionId.IsEmpty ? null : s.FactionId.ToString(),
                    LayoutKey = s.LayoutKey, Condition = s.Condition.ToString(), EstablishedOn = DateText(s.EstablishedOn),
                });
        }

        private static void ReadCombatWorld(WorldDto dto, WorldData world)
        {
            foreach (var d in dto.Encounters ?? new List<EncounterDto>())
            {
                if (d == null) throw new SaveDataException("Null encounter entry.");
                if (!ulong.TryParse(d.Seed ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) throw new SaveDataException($"Invalid encounter seed '{d.Seed}'.");
                var e = new EncounterData
                {
                    EncounterId = new EncounterId(RequiredGuid(d.EncounterId, "encounterId")), Name = d.Name ?? string.Empty,
                    Kind = ParseEnum<EncounterKind>(d.Kind, "encounter kind"), Lethality = ParseEnum<Lethality>(d.Lethality, "encounter lethality"),
                    SettlementId = new SettlementId(OptionalGuid(d.SettlementId, "encounter settlementId")), Status = ParseEnum<EncounterStatus>(d.Status, "encounter status"),
                    Outcome = ParseEnum<EncounterOutcome>(d.Outcome, "encounter outcome"), WinningSide = d.WinningSide, Round = d.Round, TurnIndex = d.TurnIndex,
                    StartedOn = OptionalDate(d.StartedOn, "encounter startedOn"), StartedAtMinute = d.StartedAtMinute, EndedOn = OptionalDate(d.EndedOn, "encounter endedOn"),
                    Seed = seed, RollCount = d.RollCount, ActionSequence = d.ActionSequence, QuestRef = d.QuestRef, WorldEventRef = d.WorldEventRef,
                };
                foreach (var id in d.Order ?? new List<string>()) e.Order.Add(new CombatantId(RequiredGuid(id, "encounter order")));
                foreach (var p in d.DifficultTerrain ?? new List<GridPosDto>()) e.DifficultTerrain.Add(Pos(p));
                foreach (var cd in d.Combatants ?? new List<CombatantDto>())
                {
                    if (cd == null) throw new SaveDataException("Null combatant entry.");
                    var t = cd.Turn ?? new TurnResourcesDto();
                    var c = new CombatantData
                    {
                        CombatantId = new CombatantId(RequiredGuid(cd.CombatantId, "combatantId")), Kind = ParseEnum<CombatantKind>(cd.Kind, "combatant kind"),
                        SourceId = cd.SourceId, Name = cd.Name ?? string.Empty, Side = cd.Side ?? string.Empty,
                        FactionId = new FactionId(OptionalGuid(cd.FactionId, "combatant factionId")), Control = ParseEnum<CombatControl>(cd.Control, "combatant control"),
                        CreatureId = cd.CreatureId, MaxHp = cd.MaxHp, CurrentHp = cd.CurrentHp, TemporaryHp = cd.TemporaryHp,
                        DeathSaveSuccesses = cd.DeathSaveSuccesses, DeathSaveFailures = cd.DeathSaveFailures, State = ParseEnum<VitalState>(cd.State, "combatant state"),
                        UsesDeathSaves = cd.UsesDeathSaves, Position = Pos(cd.Position), InitiativeRoll = cd.InitiativeRoll, Initiative = cd.Initiative,
                        TieBreak = cd.TieBreak, JoinedRound = cd.JoinedRound,
                        Turn = new TurnResources
                        {
                            ActionUsed = t.ActionUsed, BonusActionUsed = t.BonusActionUsed, ReactionUsed = t.ReactionUsed, InteractionUsed = t.InteractionUsed,
                            MovementUsed = t.MovementUsed, MovementGranted = t.MovementGranted, LightAttackWeapon = t.LightAttackWeapon,
                            AttacksLeft = t.AttacksLeft, Disengaged = t.Disengaged, Dodging = t.Dodging,
                        },
                    };
                    foreach (var k in cd.Conditions ?? new List<ConditionDto>())
                    {
                        if (k == null) throw new SaveDataException("Null condition entry.");
                        c.Conditions.Add(new ConditionInstance
                        {
                            Type = ParseEnum<ConditionType>(k.Type, "condition type"), SourceId = CombatantRef(k.SourceId, "condition sourceId"), Reason = k.Reason ?? string.Empty,
                            AppliedRound = k.AppliedRound, Duration = ParseEnum<ConditionDuration>(k.Duration, "condition duration"),
                            ExpiresOn = CombatantRef(k.ExpiresOn, "condition expiresOn"), ExpiresRound = k.ExpiresRound, Level = k.Level, EscapeDc = k.EscapeDc,
                        });
                    }

                    e.Combatants.Add(c);
                }

                foreach (var l in d.Log ?? new List<CombatLogEntryDto>())
                {
                    if (l == null) throw new SaveDataException("Null combat log entry.");
                    e.Log.Add(new CombatLogEntry
                    {
                        Sequence = l.Sequence, Round = l.Round, Kind = ParseEnum<CombatLogKind>(l.Kind, "combat log kind"),
                        ActorId = CombatantRef(l.ActorId, "log actorId"), TargetId = CombatantRef(l.TargetId, "log targetId"),
                        Text = l.Text ?? string.Empty, Rolls = new List<int>(l.Rolls ?? new List<int>()), Value = l.Value,
                    });
                }

                world.Encounters.Add(e);
            }

            foreach (var d in dto.Structures ?? new List<StructureDto>())
            {
                if (d == null) throw new SaveDataException("Null structure entry.");
                world.Structures.Add(new StructureData
                {
                    StructureId = new StructureId(RequiredGuid(d.StructureId, "structureId")), DefinitionId = d.DefinitionId, Name = d.Name ?? string.Empty,
                    SettlementId = new SettlementId(RequiredGuid(d.SettlementId, "structure settlementId")),
                    LocationId = new LocationId(OptionalGuid(d.LocationId, "structure locationId")), ShopId = new ShopId(OptionalGuid(d.ShopId, "structure shopId")),
                    OwnerNpcId = new NpcId(OptionalGuid(d.OwnerNpcId, "structure ownerNpcId")), FactionId = new FactionId(OptionalGuid(d.FactionId, "structure factionId")),
                    LayoutKey = d.LayoutKey ?? string.Empty,
                    Condition = string.IsNullOrEmpty(d.Condition) ? StructureCondition.Good : ParseEnum<StructureCondition>(d.Condition, "structure condition"),
                    EstablishedOn = OptionalDate(d.EstablishedOn, "structure establishedOn"),
                });
            }
        }
    }
}
