using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>
    /// Phase 13 (ADR-0064): fights with a place's inhabitants, through the one combat engine. The grid is the place's own
    /// cells (one square = one cell), so walls are blocked squares and corridors are chokepoints. Creatures keep lasting
    /// records (a wounded spider that survived is still wounded); people are themselves; the aftermath is the generated
    /// encounters' own (experience keyed once, loot filled once), plus the place's: groups defeated stay defeated.
    /// </summary>
    public static class PlaceEncounters
    {
        public const string FoeSide = "foes";
        public const int Margin = 12;

        public static CommandResult Start(CommandContext context, WorldData w, PlaceData place, List<PlaceGroup> groups, PlaceRoom room, string prefix)
        {
            if (w.ActiveEncounter != null) return CommandResult.InvalidState("A fight is already going on.");
            foreach (var g in groups) QuestRuntime.Confirm(w, "group:" + g.Name + "@" + place.PlaceId); // Phase 18: seen with their own eyes (ADR-0088)
            var p = w.Player;
            if (p?.Rpg == null || p.Rpg.State != Domain.Rpg.CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
            var (px, pz) = PlaceRules.CellOf(p.Pose);
            if (!room.Rect.Contains(px, pz)) (px, pz) = Nearest(room, px, pz);
            var taken = new HashSet<(int, int)> { (px, pz) };
            var setup = new EncounterSetup
            {
                Kind = groups.Exists(g => g.Behaviour == GroupBehaviour.Ambush) ? EncounterKind.Ambush : EncounterKind.Skirmish, Lethality = Lethality.Lethal,
                SettlementId = place.SettlementId,
            };
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(px, pz), Control = CombatControl.Player });

            // The player's people: followers, and anyone held in this room (they fight for their lives beside the player).
            NpcId protectedNpc = default;
            foreach (var e in w.Escorts)
            {
                var here = (e.State == EscortState.Following && (e.PlaceId == null || e.Room == room.Id)) || ((e.State == EscortState.Captive || e.State == EscortState.Waiting) && e.PlaceId == place.PlaceId && e.Room == room.Id); // Phase 14: only those in the room
                if (!here || w.FindNpc(e.NpcId) is not NpcData npc || npc.IsDeceased) continue;
                var at = Free(place, room, taken, px, pz);
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = npc.NpcId, Side = "player", Position = new GridPos(at.Item1, at.Item2) });
                if (protectedNpc.IsEmpty) protectedNpc = npc.NpcId;
            }

            // Phase 15: the party members with the player, in free squares of this room.
            Party.PartyCombat.Join(w, setup, _ => { var at = Free(place, room, taken, px, pz); taken.Add((at.Item1, at.Item2)); return new GridPos(at.Item1, at.Item2); });

            // The inhabitants, each a lasting individual.
            var names = new List<string>();
            var groupIds = new List<string>();
            foreach (var g in groups)
            {
                groupIds.Add(g.Id);
                names.Add(g.Name);
                var state = place.GroupOf(g.Id);
                for (var i = 0; i < g.Members.Count; i++)
                {
                    var m = g.Members[i];
                    var cell = taken.Contains((m.X, m.Z)) ? Free(place, place.Layout.Room(g.Room) ?? room, taken, m.X, m.Z) : Tuple.Create(m.X, m.Z);
                    taken.Add((cell.Item1, cell.Item2));
                    if (!m.NpcId.IsEmpty)
                    {
                        var npc = w.FindNpc(m.NpcId);
                        if (npc == null || npc.IsDeceased) continue;
                        setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = npc.NpcId, Side = FoeSide, Position = new GridPos(cell.Item1, cell.Item2) });
                        continue;
                    }

                    var def = CreatureCatalog.Find(m.CreatureId);
                    if (def == null) continue;
                    var id = InstanceId(place, g, i);
                    var instance = w.Creatures.Find(x => x.InstanceId == id);
                    if (instance != null && instance.State == CreatureInstanceState.Dead) continue; // the dead stay dead
                    if (instance == null)
                    {
                        instance = new CreatureInstanceData
                        {
                            InstanceId = id, CreatureId = def.Id, OriginId = "place:" + place.PlaceId + ":" + g.Id, Name = def.Name + " " + (i + 1).ToString(CultureInfo.InvariantCulture),
                            State = CreatureInstanceState.Alive, CurrentHp = def.MaxHp, FirstSeenOn = w.CurrentSimulationDate,
                        };
                        w.Creatures.Add(instance);
                    }
                    else instance.State = CreatureInstanceState.Alive; // a creature that fled came back to its den

                    setup.Participants.Add(new ParticipantSetup
                    {
                        Kind = CombatantKind.Creature, CreatureId = def.Id, Name = instance.Name, Side = FoeSide, Position = new GridPos(cell.Item1, cell.Item2), CreatureInstanceId = instance.InstanceId,
                    });
                }

                state.State = GroupState.Fighting;
            }

            if (!setup.Participants.Exists(x => x.Side == FoeSide))
            {
                foreach (var g in groups) place.GroupOf(g.Id).State = GroupState.Defeated;
                return CommandResult.Ok("Nothing stirs: they are gone.");
            }

            Geometry(setup, place, room);

            var title = PlaceRules.Capital(string.Join(" and ", names));
            setup.Name = title + " — " + place.Name;
            setup.Origin = new EncounterOriginData
            {
                Type = groups.Exists(g => g.Behaviour == GroupBehaviour.Guard) ? EncounterType.Patrol : EncounterType.HostileCreatures,
                Objective = protectedNpc.IsEmpty ? EncounterObjective.Defeat : EncounterObjective.Protect, ProtectedNpcId = protectedNpc, NearSettlementId = place.SettlementId,
                Context = room.Name + " of " + place.Name, Terrain = place.ArchetypeId, Difficulty = "inhabitants", PlaceId = place.PlaceId, GroupId = string.Join(",", groupIds), Room = room.Id,
                PlanSeed = SeedMixer(place, room),
            };
            foreach (var g in groups) foreach (var m in g.Members) if (m.CreatureId != null) setup.Origin.SourceIds.Add(m.CreatureId);
            var result = CombatCommandHandlers.StartGenerated(context, setup, (prefix ?? string.Empty) + title + " in the " + room.Name.ToLowerInvariant() + ".");
            if (result.IsSuccess && w.ActiveEncounter != null)
                foreach (var g in groups) place.GroupOf(g.Id).EncounterId = w.ActiveEncounter.EncounterId;
            return result;
        }

        /// <summary>
        /// Phase 14 (ADR-0069): the player attacks a person who was no threat — an assault, recorded before the first blow
        /// with whoever sees it (the victim always knows). If they die, the fight's end records the murder.
        /// </summary>
        public static CommandResult StartAssault(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, NpcData victim, int vx, int vz)
        {
            if (w.ActiveEncounter != null) return CommandResult.InvalidState("A fight is already going on.");
            var (px, pz) = PlaceRules.CellOf(w.Player.Pose);
            if (!room.Rect.Contains(px, pz)) (px, pz) = Nearest(room, px, pz);
            var witnesses = CrimeRules.Witnesses(w, place, room, px, pz, true);
            if (!witnesses.Exists(x => x.Npc.NpcId == victim.NpcId))
            {
                var known = victim.HasMetPlayer || (w.Player.Social?.Notoriety ?? 0) >= 30;
                witnesses.Insert(0, new Witness { Npc = victim, Recognized = known, Description = known ? w.Player.Name : CrimeRules.Describe(w), How = "attacked" });
            }

            var crime = CrimeRules.Record(context, w, CrimeKind.Assault, place, room, victim.NpcId, default, "attacked " + victim.Name, witnesses);
            var setup = new EncounterSetup { Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal, SettlementId = place.SettlementId, Name = victim.Name + " — " + place.Name };
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(px, pz), Control = CombatControl.Player });
            var taken = new HashSet<(int, int)> { (px, pz) };
            var at = taken.Contains((vx, vz)) ? Free(place, room, taken, px, pz) : Tuple.Create(vx, vz);
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = victim.NpcId, Side = FoeSide, Position = new GridPos(at.Item1, at.Item2) });
            Geometry(setup, place, room);
            setup.Origin = new EncounterOriginData
            {
                Type = EncounterType.Patrol, Objective = EncounterObjective.Defeat, NearSettlementId = place.SettlementId, Context = crime.CrimeId, Terrain = place.ArchetypeId,
                Difficulty = "assault", PlaceId = place.PlaceId, GroupId = string.Empty, Room = room.Id, PlanSeed = SeedMixer(place, room),
            };
            return CombatCommandHandlers.StartGenerated(context, setup, $"You attack {victim.Name}.");
        }

        /// <summary>The fight's ground in a place: walls and rock round the room's floor, doors as doors, fallen bridges as gaps, furniture as low cover.</summary>
        public static void Geometry(EncounterSetup setup, PlaceData place, PlaceRoom room)
        {
            // The room's walls: every cell around the fight that is not floor.
            var floor = Floor(place, room.Floor);
            for (var x = room.Rect.X - Margin; x < room.Rect.MaxX + Margin; x++)
                for (var z = room.Rect.Z - Margin; z < room.Rect.MaxZ + Margin; z++)
                    if (!floor.Contains((x, z))) setup.Blocked.Add(new GridPos(x, z));
            // Phase 14 (ADR-0067): doors are doors — closed ones block movement, sight and spells until someone opens them;
            // a fallen bridge is a gap nobody crosses; furniture gives half cover.
            foreach (var c in place.Layout.Connections)
            {
                if (c.IsVertical || c.Kind == ConnectionKind.Entrance || c.Kind == ConnectionKind.Opening) continue;
                if (place.Layout.Room(c.A)?.Floor != room.Floor) continue;
                var squares = new List<GridPos>();
                for (var x = c.Rect.X; x < c.Rect.MaxX; x++)
                    for (var z = c.Rect.Z; z < c.Rect.MaxZ; z++)
                        if (!InAnyRoom(place, room.Floor, x, z)) squares.Add(new GridPos(x, z));
                if (squares.Count == 0) continue;
                if (c.Kind == ConnectionKind.Bridge)
                {
                    if (place.BridgeOf(c) == BridgeState.Destroyed) setup.Blocked.AddRange(squares);
                    continue;
                }

                if (c.Kind == ConnectionKind.Secret && !place.Found(c)) { setup.Blocked.AddRange(squares); continue; }
                var s = place.DoorOf(c);
                setup.Doors.Add(new EncounterDoorData
                {
                    Id = c.Id, Name = c.Kind == ConnectionKind.Secret ? "hidden door" : "door", Squares = squares,
                    Open = s == DoorState.Open || s == DoorState.Broken, Locked = s == DoorState.Locked || s == DoorState.Barred,
                });
            }

            foreach (var f in place.Layout.Features)
            {
                var fr = place.Layout.Room(f.Room);
                if (fr == null || fr.Floor != room.Floor || !GivesCover(f)) continue;
                setup.LowCover.Add(new LowCoverData { X = f.X, Y = f.Z, Name = "the " + PlaceRules.Name(f).ToLowerInvariant() });
            }

        }

        private static ulong SeedMixer(PlaceData place, PlaceRoom room) => Domain.Randomness.SeedMixer.Combine(place.Seed, "fight:" + room.Id + ":" + place.Visits.ToString(CultureInfo.InvariantCulture));

        public static string InstanceId(PlaceData place, PlaceGroup g, int i) => place.PlaceId + ":" + g.Id + "#" + (i + 1).ToString(CultureInfo.InvariantCulture);

        /// <summary>The floor cells of one floor: its rooms and the passages between them.</summary>
        public static HashSet<(int, int)> Floor(PlaceData place, int floor)
        {
            var cells = new HashSet<(int, int)>();
            foreach (var r in place.Layout.Rooms)
            {
                if (r.Floor != floor || (r.Secret && !SecretOpen(place, r))) continue;
                for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                    for (var z = r.Rect.Z; z < r.Rect.MaxZ; z++)
                        cells.Add((x, z));
            }

            foreach (var c in place.Layout.Connections)
            {
                if (c.IsVertical || c.Kind == ConnectionKind.Entrance || place.Layout.Room(c.A)?.Floor != floor) continue;
                if (c.Kind == ConnectionKind.Secret && !place.Found(c)) continue;
                for (var x = c.Rect.X; x < c.Rect.MaxX; x++)
                    for (var z = c.Rect.Z; z < c.Rect.MaxZ; z++)
                        cells.Add((x, z));
            }

            return cells;
        }

        /// <summary>Phase 14: furniture, workstations, altars and big containers stand between a creature and a shot (half cover).</summary>
        public static bool GivesCover(PlaceFeature f) =>
            f.Kind == FeatureKind.Furniture || f.Kind == FeatureKind.Workstation || f.Kind == FeatureKind.Altar ||
            (f.Kind == FeatureKind.Container && (f.DefinitionId == "sarcophagus" || f.DefinitionId == "chest" || f.DefinitionId == "ore_crate" || f.DefinitionId == "crate" || f.DefinitionId == "strongbox" || f.DefinitionId == "sealed_vault" || f.DefinitionId == "grain_bin"));

        private static bool SecretOpen(PlaceData place, PlaceRoom r)
        {
            foreach (var c in place.Layout.Connections) if (c.Kind == ConnectionKind.Secret && c.Joins(r.Id) && place.Found(c)) return true;
            return false;
        }

        private static bool InAnyRoom(PlaceData place, int floor, int x, int z) => place.Layout.RoomAt(floor, x, z) != null;

        private static (int, int) Nearest(PlaceRoom room, int x, int z) =>
            (Math.Max(room.Rect.X, Math.Min(room.Rect.MaxX - 1, x)), Math.Max(room.Rect.Z, Math.Min(room.Rect.MaxZ - 1, z)));

        private static Tuple<int, int> Free(PlaceData place, PlaceRoom room, HashSet<(int, int)> taken, int nearX, int nearZ)
        {
            for (var r = 1; r < 8; r++)
                for (var dx = -r; dx <= r; dx++)
                    for (var dz = -r; dz <= r; dz++)
                    {
                        var x = nearX + dx;
                        var z = nearZ + dz;
                        if (!room.Rect.Contains(x, z) || taken.Contains((x, z))) continue;
                        taken.Add((x, z));
                        return Tuple.Create(x, z);
                    }

            return Tuple.Create(nearX, nearZ);
        }

        /// <summary>
        /// The place's part of a fight's aftermath (called once, from <see cref="WorldEncounters.Resolve"/>): won, the groups
        /// are defeated for good and what they guarded opens; withdrawn or lost, the survivors stay, wounded. The loot left by
        /// the fallen lies in the room; a person held or following who survived was protected; one who died is lost.
        /// </summary>
        public static void Resolve(CommandContext context, WorldData w, EncounterData e)
        {
            var o = e.Origin;
            var place = w.FindPlace(o?.PlaceId);
            if (place == null) return;
            var won = e.WinningSide == "player";

            // Phase 14: doors opened or shut in the fight stay that way.
            foreach (var d in e.Doors)
            {
                var c = place.Layout.Connection(d.Id);
                if (c == null || d.Locked) continue;
                var before = place.DoorOf(c);
                if (d.Open && before == DoorState.Closed) place.Doors[c.Id] = DoorState.Open;
                else if (!d.Open && before == DoorState.Open) place.Doors[c.Id] = DoorState.Closed;
            }

            foreach (var id in (o.GroupId ?? string.Empty).Split(','))
            {
                if (id.Length == 0) continue;
                var g = place.Layout.Group(id);
                var s = place.GroupOf(id);
                var anyoneLeft = false;
                if (g != null)
                    foreach (var c in e.Combatants)
                        if (c.Side == FoeSide && c.State != VitalState.Dead && c.State != VitalState.Fled) anyoneLeft = true;
                s.State = won || !anyoneLeft ? GroupState.Defeated : GroupState.Survived;
                s.Alerted = true;
                if (s.State == GroupState.Defeated)
                {
                    place.Note(PlaceRules.Capital(g?.Name ?? id) + " defeated.");
                    PlaceContents.FreeGuarded(w, place, id);
                }
            }

            // Phase 14 (ADR-0069): an assault that killed is murder, known to whoever saw the attack and still lives.
            if (o.Difficulty == "assault" && w.FindCrime(o.Context) is CrimeRecordData assault)
                foreach (var c in e.Combatants)
                {
                    if (c.Kind != CombatantKind.Npc || c.Side != FoeSide || c.State != VitalState.Dead || !Guid.TryParse(c.SourceId, out var dead)) continue;
                    var survivors = new List<Witness>();
                    foreach (var x in assault.Witnesses)
                        if (x.NpcId != new NpcId(dead) && w.FindNpc(x.NpcId) is NpcData seen && !seen.IsDeceased)
                            survivors.Add(new Witness { Npc = seen, Recognized = x.Recognized, Description = x.Description, How = x.How });
                    CrimeRules.Record(context, w, CrimeKind.Murder, place, place.Layout.Room(o.Room), new NpcId(dead), default, "killed " + c.Name, survivors);
                }

            // Phase 14: outlaws all gone, by blade or by word — the road is free.
            if (place.DefinitionId == Phase14Places.Hideout && !place.Variables.ContainsKey("road"))
            {
                var anyone = false;
                foreach (var g in place.Layout.Groups)
                {
                    var gs = place.GroupStateOf(g.Id);
                    if (g.WakesOn == null && gs != GroupState.Defeated && gs != GroupState.Departed) anyone = true;
                }

                if (!anyone)
                {
                    place.Variables["road"] = "cleared";
                    context.Quest(new QuestEvent { Kind = QuestEventKind.Decision, Subject = "crowfoot_dealt" });
                }
            }

            // The fallen's things lie where they fell.
            var pile = w.FindContainer(WorldEncounters.LootKey(e.EncounterId));
            if (pile != null)
            {
                pile.PlaceId = place.PlaceId;
                pile.FeatureId = "loot:" + o.Room;
                pile.SettlementId = default;
            }

            // The people the player brought or found here.
            foreach (var c in e.Combatants)
            {
                if (c.Kind != CombatantKind.Npc || c.Side != "player" || !Guid.TryParse(c.SourceId, out var g)) continue;
                var npc = w.FindNpc(new NpcId(g));
                var escort = w.FindEscort(new NpcId(g));
                if (npc == null) continue;
                if (npc.IsDeceased || c.State == VitalState.Dead)
                {
                    if (escort != null) escort.State = EscortState.Lost;
                    continue;
                }

                if (won) context.Quest(new QuestEvent { Kind = QuestEventKind.Protected, Subject = npc.NpcId.ToString(), Settlement = place.SettlementId });
            }

            PlaceRules.UpdateDiscovery(context, w, place);
        }
    }
}
