using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Places;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Application
{
    public sealed class PlaceSummaryView
    {
        public string PlaceId { get; set; }
        public string Name { get; set; }
        public PlaceCategory Category { get; set; }
        public string Kind { get; set; }
        public PlaceDiscovery Discovery { get; set; }
        public string Description { get; set; }

        /// <summary>Rough words for where it lies (a rumour says no more than this).</summary>
        public string Direction { get; set; }

        /// <summary>The map cell, only once its whereabouts are known (Discovered or beyond).</summary>
        public MapCoord? Cell { get; set; }

        public string Threat { get; set; }
        public int Visits { get; set; }
        public string Settlement { get; set; }
        public string Faction { get; set; }
        public List<(string Id, string Label)> Entrances { get; } = new List<(string, string)>();
        public bool HasQuest { get; set; }

        /// <summary>Phase 14: how the place stands now, as far as the player has seen it ("a bridge has fallen", "the outlaws have gone").</summary>
        public List<string> State { get; } = new List<string>();
    }

    public sealed class RoomView
    {
        public string Id { get; set; }
        public int Floor { get; set; }
        public CellRect Rect { get; set; }
        public RoomKind Kind { get; set; }
        public string Name { get; set; }
        public float Ceiling { get; set; }
        public bool Lit { get; set; }
        public bool Secret { get; set; }
        public bool Restricted { get; set; }
        public string Hazard { get; set; }
        public bool Explored { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class PassageView
    {
        public string Id { get; set; }
        public ConnectionKind Kind { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public CellRect Rect { get; set; }
        public int ToX { get; set; }
        public int ToZ { get; set; }
        public DoorState Door { get; set; }
        public bool Guarded { get; set; }
        public string Label { get; set; }
    }

    public sealed class FeatureView
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public FeatureKind Kind { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Z { get; set; }

        /// <summary>Not yet found: the scene must not draw it (a hidden trap still goes off if stepped on).</summary>
        public bool Hidden { get; set; }

        public FeatureFlags Flags { get; set; }
        public string ContainerKey { get; set; }
        public string TrapId { get; set; }
        public bool Armed { get; set; }
    }

    public sealed class FigureView
    {
        public string Name { get; set; }
        public string CreatureId { get; set; }
        public NpcId NpcId { get; set; }
        public string Room { get; set; }
        public int X { get; set; }
        public int Z { get; set; }

        /// <summary>"foe", "guard", "captive", "follower", "resident".</summary>
        public string Role { get; set; }

        public string GroupId { get; set; }
        public string Activity { get; set; }

        // ----- Phase 14 (ADR-0070) -----

        /// <summary>Where they were a few minutes ago, and the passages they took (the scene walks them through the doorways).</summary>
        public string FromRoom { get; set; }

        public List<string> Route { get; set; } = new List<string>();

        /// <summary>On their way out of the building.</summary>
        public bool Leaving { get; set; }

        /// <summary>For a waiting follower: why they cannot reach the player.</summary>
        public string Waiting { get; set; }
    }

    public sealed class PlaceSceneView
    {
        public string PlaceId { get; set; }
        public string Name { get; set; }
        public PlaceCategory Category { get; set; }
        public string Material { get; set; }
        public PlaceDiscovery Discovery { get; set; }
        public List<PlaceFloor> Floors { get; } = new List<PlaceFloor>();
        public List<RoomView> Rooms { get; } = new List<RoomView>();
        public List<PassageView> Passages { get; } = new List<PassageView>();
        public List<FeatureView> Features { get; } = new List<FeatureView>();
        public List<FigureView> Figures { get; } = new List<FigureView>();
        public List<(string Key, string Room, int X, int Z, string Name)> LootPiles { get; } = new List<(string, string, int, int, string)>();
        public string PlayerRoom { get; set; }
        public int PlayerFloor { get; set; }
        public LocalPose PlayerPose { get; set; }
        public string FloorName { get; set; }
        public bool PlayerHasLight { get; set; }
        public string Status { get; set; }

        // ----- Phase 14 -----

        /// <summary>Burning rooms ("burning") and burnt-out ones ("burnt out"), by room.</summary>
        public Dictionary<string, string> Fires { get; } = new Dictionary<string, string>();

        /// <summary>Bridges and their state, by connection.</summary>
        public Dictionary<string, BridgeState> Bridges { get; } = new Dictionary<string, BridgeState>();

        /// <summary>Rooms whose water is gone (the culverts drained).</summary>
        public bool Drained { get; set; }

        public RoomView Room(string id) => Rooms.Find(r => r.Id == id);
    }

    public sealed class PlaceMapView
    {
        public string Name { get; set; }
        public int CurrentFloor { get; set; }
        public string PlayerRoom { get; set; }
        public List<(int Index, string Name, List<RoomView> Rooms, List<PassageView> Passages)> Floors { get; } = new List<(int, string, List<RoomView>, List<PassageView>)>();
    }

    /// <summary>Phase 13 (ADR-0062, ADR-0065): views of places, limited to what the player knows or can see.</summary>
    public sealed partial class WorldSession
    {
        /// <summary>Places the player has heard of or found (wilderness), for the map and the journal. Interiors are not listed.</summary>
        public IReadOnlyList<PlaceSummaryView> GetKnownPlaces()
        {
            var list = new List<PlaceSummaryView>();
            if (!HasWorld) return list;
            foreach (var p in _world.Places)
                if (p.Category != PlaceCategory.Interior && p.Discovery >= PlaceDiscovery.Rumored) list.Add(Summary(p));
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        public PlaceSummaryView GetPlaceSummary(string placeId)
        {
            var p = HasWorld ? _world.FindPlace(placeId) : null;
            if (p == null || (p.Category != PlaceCategory.Interior && p.Discovery < PlaceDiscovery.Rumored)) return null;
            return Summary(p);
        }

        private PlaceSummaryView Summary(PlaceData p)
        {
            var v = new PlaceSummaryView
            {
                PlaceId = p.PlaceId, Name = p.Name, Category = p.Category, Kind = ArchetypeCatalog.Find(p.ArchetypeId)?.Name ?? p.Category.ToString(), Discovery = p.Discovery,
                Description = p.Discovery >= PlaceDiscovery.Discovered ? p.Description : "Only a name and a direction, heard in passing.",
                Direction = PlaceKnowledge.Direction(_world, p), Cell = p.Discovery >= PlaceDiscovery.Discovered ? p.Cell : (MapCoord?)null, Visits = p.Visits,
                Threat = p.Discovery < PlaceDiscovery.Visited ? "unknown" : p.Threat == 0 ? "quiet" : p.Threat < 15 ? "some danger" : p.Threat < 40 ? "dangerous" : "deadly",
                Settlement = _world.FindSettlement(p.SettlementId)?.Name, Faction = p.FactionId.IsEmpty ? null : FindFaction(p.FactionId)?.Name,
            };
            if (p.Discovery >= PlaceDiscovery.Discovered)
                foreach (var c in p.Layout.Connections)
                {
                    if (c.Kind != ConnectionKind.Entrance) continue;
                    if (c.FindDc > 0 && !(p.Variables.TryGetValue("entrance:" + c.Id, out var k) && k == "known") && !(_world.Player?.Social?.Knows("fort_side_door") ?? false)) continue;
                    var state = p.DoorOf(c);
                    v.Entrances.Add((c.Id, c.Id == "entrance" ? "Main entrance" : PlaceRules.Capital(c.Id.Replace('_', ' ')) + (state == DoorState.Locked ? " (locked)" : string.Empty)));
                }

            foreach (var q in _world.Quests)
                if (q.IsOpen && q.Bindings.ContainsValue(p.PlaceId)) v.HasQuest = true;
            if (p.Discovery >= PlaceDiscovery.Visited) // Phase 14: the place as the player last saw it
            {
                if (p.Layout.Connections.Any(c => c.Kind == ConnectionKind.Bridge && p.BridgeOf(c) == BridgeState.Destroyed)) v.State.Add("a bridge has fallen");
                else if (p.Layout.Connections.Any(c => c.Kind == ConnectionKind.Bridge && p.BridgeOf(c) == BridgeState.Damaged)) v.State.Add("a damaged bridge");
                if (p.Variables.ContainsKey("drained")) v.State.Add("drained");
                foreach (var r in p.Layout.Rooms.Where(r => p.ExploredRooms.Contains(r.Id))) // only what the player has seen
                {
                    var fire = PlaceRules.FireState(_world, p, r);
                    if (fire == "burning") { v.State.Add("on fire"); break; }
                    if (fire == "burnt out") { v.State.Add("burnt out in places"); break; }
                }

                if (p.Groups.Values.Any(g => g.Woken && g.State == GroupState.Present)) v.State.Add("its sleepers are awake");
                if (p.Groups.Values.Any(g => g.State == GroupState.Departed)) v.State.Add("some have left");
            }

            return v;
        }

        /// <summary>The place the player is in, as the scene should show it (secrets only once found).</summary>
        public PlaceSceneView GetPlaceScene()
        {
            if (!HasWorld || _world.Player == null || !_world.Player.IsInPlace) return null;
            var p = _world.FindPlace(_world.Player.PlaceId);
            if (p == null) return null;
            var w = _world;
            var v = new PlaceSceneView
            {
                PlaceId = p.PlaceId, Name = p.Name, Category = p.Category, Discovery = p.Discovery, PlayerRoom = w.Player.PlaceRoom, PlayerFloor = w.Player.PlaceFloor,
                PlayerPose = w.Player.Pose, Material = ArchetypeCatalog.Find(p.ArchetypeId)?.Material ?? (p.Category == PlaceCategory.Interior ? "interior" : "rock"),
            };
            v.Floors.AddRange(p.Layout.Floors);
            v.FloorName = p.Layout.Floors.Find(f => f.Index == v.PlayerFloor)?.Name;
            v.PlayerHasLight = PlaceRules.CarriesLight(w.Player);
            var visibleSecretRooms = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in p.Layout.Connections)
                if (c.Kind == ConnectionKind.Secret && p.Found(c)) { visibleSecretRooms.Add(c.A); visibleSecretRooms.Add(c.B); }
            foreach (var r in p.Layout.Rooms)
            {
                if (r.Secret && !visibleSecretRooms.Contains(r.Id)) continue;
                v.Rooms.Add(new RoomView
                {
                    Id = r.Id, Floor = r.Floor, Rect = r.Rect, Kind = r.Kind, Name = r.Name, Ceiling = r.Ceiling, Lit = r.Lit, Secret = r.Secret, Restricted = r.Restricted, Hazard = r.Hazard,
                    Explored = p.Explored(r.Id), Tags = new List<string>(r.Tags),
                });
            }

            foreach (var c in p.Layout.Connections)
            {
                if (c.Kind == ConnectionKind.Secret && !p.Found(c)) continue;
                v.Passages.Add(new PassageView
                {
                    Id = c.Id, Kind = c.Kind, A = c.A, B = c.B, Rect = c.Rect, ToX = c.ToX, ToZ = c.ToZ, Door = p.DoorOf(c),
                    Guarded = c.GuardedBy != null && !PlaceRules.HasPassage(p, c.GuardedBy) && p.GroupStateOf(c.GuardedBy) != GroupState.Peaceful,
                    Label = c.Kind == ConnectionKind.Stairs ? "Stairs" : c.Kind == ConnectionKind.Ladder ? "Ladder" : c.Kind == ConnectionKind.Entrance ? "Way out" : c.Kind == ConnectionKind.Secret ? "Hidden way" : "Door",
                });
            }

            foreach (var f in p.Layout.Features)
            {
                if (!v.Rooms.Exists(r => r.Id == f.Room)) continue;
                var state = p.Features.TryGetValue(f.Id, out var s) ? s.Flags : FeatureFlags.None;
                var trap = f.Kind == FeatureKind.Trap ? TrapCatalog.Find(f.Get("trap")) : null;
                var armed = trap != null && (state & FeatureFlags.Disarmed) == 0 &&
                            ((state & FeatureFlags.Triggered) == 0 || (trap.Reset == TrapReset.Rearms && !(p.Variables.TryGetValue("sprung:" + f.Id, out var last) && last == p.Visits.ToString())));
                v.Features.Add(new FeatureView
                {
                    Id = f.Id, Room = f.Room, Kind = f.Kind, DefinitionId = f.DefinitionId, Name = PlaceRules.Name(f), X = f.X, Z = f.Z, Flags = state,
                    Hidden = f.Hidden && (state & FeatureFlags.Found) == 0, ContainerKey = f.Kind == FeatureKind.Container ? PlaceContents.Key(p.PlaceId, f.Id) : null,
                    TrapId = trap?.Id, Armed = armed,
                });
            }

            // Inhabitants still there.
            foreach (var g in p.Layout.Groups)
            {
                var s = p.GroupStateOf(g.Id);
                if (s == GroupState.Defeated) continue;
                for (var i = 0; i < g.Members.Count; i++)
                {
                    var m = g.Members[i];
                    string name;
                    if (!m.NpcId.IsEmpty)
                    {
                        var npc = w.FindNpc(m.NpcId);
                        if (npc == null || npc.IsDeceased) continue;
                        name = npc.Name;
                    }
                    else
                    {
                        var instance = w.Creatures.Find(x => x.InstanceId == PlaceEncounters.InstanceId(p, g, i));
                        if (instance != null && instance.State == CreatureInstanceState.Dead) continue;
                        name = instance?.Name ?? CreatureCatalog.Find(m.CreatureId)?.Name ?? m.CreatureId;
                    }

                    v.Figures.Add(new FigureView
                    {
                        Name = name, CreatureId = m.CreatureId, NpcId = m.NpcId, Room = g.Room, X = m.X, Z = m.Z, GroupId = g.Id,
                        Role = g.Behaviour == GroupBehaviour.Guard && s != GroupState.Fighting ? "guard" : "foe",
                    });
                }
            }

            // People held here; people following; people at home inside.
            foreach (var e in w.Escorts)
            {
                var npc = w.FindNpc(e.NpcId);
                if (npc == null || npc.IsDeceased) continue;
                var captiveFeature = p.Layout.Features.Find(x => x.Kind == FeatureKind.Captive && x.Get("npc") == e.NpcId.ToString());
                var held = captiveFeature != null && !p.Has(captiveFeature.Id, FeatureFlags.Freed);
                if (e.State == EscortState.Captive && e.PlaceId == p.PlaceId)
                    v.Figures.Add(new FigureView { Name = npc.Name, NpcId = npc.NpcId, Room = e.Room, X = captiveFeature?.X ?? 0, Z = captiveFeature?.Z ?? 0, Role = "captive" });
                else if ((e.State == EscortState.Waiting || e.State == EscortState.Following) && e.PlaceId == p.PlaceId && !held)
                {
                    // Phase 14 (ADR-0070): a follower stands in their own room — beside the player, or waiting where the way was shut.
                    var room = p.Layout.Room(e.Room) ?? p.Layout.Room(w.Player.PlaceRoom);
                    if (room == null) continue;
                    var waiting = e.State == EscortState.Waiting;
                    v.Figures.Add(new FigureView
                    {
                        Name = npc.Name, NpcId = npc.NpcId, Room = room.Id, X = room.Rect.CenterX, Z = room.Rect.CenterZ, Role = "follower",
                        Waiting = waiting ? PlaceRoutes.Find(p, room.Id, w.Player.PlaceRoom, Mover.Follower) == null ? PlaceRoutes.Blocker(p, room.Id, w.Player.PlaceRoom) : "waiting" : null,
                    });
                }
                else if (e.State == EscortState.Waiting && e.PlaceId == p.PlaceId)
                    v.Figures.Add(new FigureView { Name = npc.Name, NpcId = npc.NpcId, Room = e.Room, X = captiveFeature?.X ?? 0, Z = captiveFeature?.Z ?? 0, Role = "captive" });
            }

            // Phase 14 (ADR-0070): people inside move between rooms on their own rhythm, along real passages.
            if (!p.LocationId.IsEmpty)
                foreach (var presence in IndoorSchedule.Everyone(w, p, Simulation.Quests.QuestClock.Now(w)))
                {
                    var fig = new FigureView
                    {
                        Name = presence.Npc.Name, NpcId = presence.Npc.NpcId, Room = presence.Room, X = presence.X, Z = presence.Z, Role = "resident",
                        Activity = presence.Activity.ToString(), FromRoom = presence.FromRoom, Leaving = presence.Leaving,
                    };
                    foreach (var step in presence.Route) fig.Route.Add(step.ConnectionId);
                    v.Figures.Add(fig);
                }

            foreach (var r in p.Layout.Rooms)
            {
                var fire = PlaceRules.FireState(w, p, r);
                if (fire != null) v.Fires[r.Id] = fire;
            }

            foreach (var c in p.Layout.Connections)
                if (c.Kind == ConnectionKind.Bridge) v.Bridges[c.Id] = p.BridgeOf(c);
            v.Drained = p.Variables.ContainsKey("drained");

            foreach (var c in w.Containers)
                if (c.PlaceId == p.PlaceId && c.FeatureId != null && c.FeatureId.StartsWith("loot:", StringComparison.Ordinal) && (c.Items.Count > 0 || c.Coin > 0))
                {
                    var room = p.Layout.Room(c.FeatureId.Substring(5));
                    if (room != null) v.LootPiles.Add((c.Key, room.Id, room.Rect.CenterX, room.Rect.CenterZ, c.Name));
                }

            var here = p.Layout.Room(w.Player.PlaceRoom);
            v.Status = Status(p, here);
            return v;
        }

        private string Status(PlaceData p, PlaceRoom room)
        {
            if (room == null) return null;
            var parts = new List<string>();
            if (room.Restricted) parts.Add("restricted");
            var h = PlaceRules.HazardOf(p, room);
            if (h != null) parts.Add(h.Name.ToLowerInvariant() + (PlaceRules.Dark(_world, room) ? " — you have no light" : string.Empty));
            if (!room.Lit && h == null) parts.Add("dim");
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>Where someone at home inside is, by what they are doing: asleep in a bedroom, at work in the workroom, otherwise the main room.</summary>
        public static PlaceRoom OccupantRoom(PlaceData p, ActivityKind activity)
        {
            RoomKind[] prefer;
            switch (activity)
            {
                case ActivityKind.Sleeping:
                case ActivityKind.Resting: prefer = new[] { RoomKind.Bedroom, RoomKind.Quarters, RoomKind.Barracks }; break;
                case ActivityKind.Working:
                case ActivityKind.Trading: prefer = new[] { RoomKind.Workshop, RoomKind.Office, RoomKind.Storage, RoomKind.Barracks }; break;
                case ActivityKind.Worshipping: prefer = new[] { RoomKind.Shrine }; break;
                case ActivityKind.Guarding: prefer = new[] { RoomKind.Barracks, RoomKind.Gatehouse }; break;
                default: prefer = new[] { RoomKind.Chamber, RoomKind.Hall, RoomKind.Barracks, RoomKind.Workshop }; break;
            }

            foreach (var k in prefer)
                foreach (var r in p.Layout.Rooms)
                    if (r.Kind == k && r.Floor == 0) return r;
            foreach (var k in prefer)
                foreach (var r in p.Layout.Rooms)
                    if (r.Kind == k) return r;
            return p.Layout.Room(p.Layout.Entrance?.A);
        }

        public static (int, int) OccupantCell(PlaceData p, PlaceRoom room, NpcId npc)
        {
            var h = SeedMixer.Mix(SeedMixer.HashString(npc.ToString()));
            for (var i = 0; i < room.Rect.Area; i++)
            {
                var k = (int)((h + (ulong)i) % (ulong)room.Rect.Area);
                var x = room.Rect.X + k % room.Rect.W;
                var z = room.Rect.Z + k / room.Rect.W;
                if (z == room.Rect.Z) continue; // keep the doorway row clear
                var taken = false;
                foreach (var f in p.Layout.Features) if (f.Room == room.Id && f.X == x && f.Z == z) taken = true;
                foreach (var c in p.Layout.Connections) if (c.IsVertical && ((c.A == room.Id && c.Rect.X == x && c.Rect.Z == z) || (c.B == room.Id && c.ToX == x && c.ToZ == z))) taken = true;
                if (!taken) return (x, z);
            }

            return (room.Rect.CenterX, room.Rect.CenterZ);
        }

        /// <summary>The map of the place as the player has seen it: explored rooms, and passages between them, floor by floor.</summary>
        public PlaceMapView GetPlaceMap()
        {
            var scene = GetPlaceScene();
            if (scene == null) return null;
            var v = new PlaceMapView { Name = scene.Name, CurrentFloor = scene.PlayerFloor, PlayerRoom = scene.PlayerRoom };
            foreach (var f in scene.Floors)
            {
                var rooms = scene.Rooms.FindAll(r => r.Floor == f.Index && r.Explored);
                if (rooms.Count == 0) continue;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in rooms) ids.Add(r.Id);
                var passages = scene.Passages.FindAll(x => ids.Contains(x.A) && (x.B == null || ids.Contains(x.B) || x.Kind == ConnectionKind.Stairs || x.Kind == ConnectionKind.Ladder));
                v.Floors.Add((f.Index, f.Name, rooms, passages));
            }

            return v;
        }

        /// <summary>The name of a settlement's place at a layout key (a building), or null.</summary>
        public string GetLocationName(SettlementId settlement, string layoutKey)
        {
            if (!HasWorld) return null;
            foreach (var l in _world.Locations) if (l.SettlementId == settlement && l.LayoutKey == layoutKey) return l.Name;
            return null;
        }

        /// <summary>What can be done to a target now (feature, passage, room or group), for the interaction prompt.</summary>
        public IReadOnlyList<ActionOption> GetPlaceActions(string targetId)
        {
            if (!HasWorld) return new List<ActionOption>();
            var p = PlaceRules.Current(_world);
            return PlaceActions.Options(_world, p, PlaceRules.CurrentRoom(_world), targetId);
        }
    }
}
