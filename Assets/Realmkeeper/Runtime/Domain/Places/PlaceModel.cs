using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Places
{
    /// <summary>
    /// Phase 13 (ADR-0062): what kind of explorable place something is. One architecture serves them all; the category
    /// chooses the archetype's rules (rooms, inhabitants, traps, lighting), not a separate scene type.
    /// </summary>
    public enum PlaceCategory
    {
        Mine = 0,
        Cave = 1,
        Ruin = 2,
        Tower = 3,
        Keep = 4,
        Crypt = 5,
        Catacomb = 6,
        Temple = 7,
        Shrine = 8,
        BanditHideout = 9,
        Camp = 10,
        Underground = 11,
        AbandonedSettlement = 12,

        /// <summary>The inside of a settlement building (a house, the granary, a workshop, the guardhouse).</summary>
        Interior = 13,
    }

    /// <summary>What the player knows of a place (ADR-0065). Only Discovered and beyond put it on the map.</summary>
    public enum PlaceDiscovery
    {
        Unknown = 0,

        /// <summary>Heard of: a name and a rough direction, no marker.</summary>
        Rumored = 1,

        /// <summary>Its whereabouts are known: a marker on the map and an entrance to go to.</summary>
        Discovered = 2,

        /// <summary>The player has been inside.</summary>
        Visited = 3,

        /// <summary>Every room that is not secret has been seen.</summary>
        Explored = 4,

        /// <summary>No hostile inhabitants remain.</summary>
        Cleared = 5,
    }

    /// <summary>What a place hangs from (ADR-0062): the region (wilderness), a settlement, one of its buildings, or another place.</summary>
    public enum PlaceParentKind
    {
        Region = 0,
        Settlement = 1,
        Building = 2,
        Place = 3,
    }

    public enum RoomKind
    {
        Entrance = 0,
        Chamber = 1,
        Corridor = 2,
        Hall = 3,
        Storage = 4,
        Gallery = 5,
        Shaft = 6,
        Tomb = 7,
        Shrine = 8,
        Vault = 9,
        Barracks = 10,
        Armoury = 11,
        Cell = 12,
        Quarters = 13,
        Kitchen = 14,
        TapRoom = 15,
        Workshop = 16,
        Bedroom = 17,
        Cellar = 18,
        Office = 19,
        Gatehouse = 20,
        Yard = 21,
        Collapse = 22,
        Den = 23,
    }

    public enum ConnectionKind
    {
        /// <summary>An open doorway or the mouth of a passage.</summary>
        Opening = 0,
        Door = 1,
        Stairs = 2,
        Ladder = 3,
        Bridge = 4,

        /// <summary>A passage hidden until found (a loose panel, a gap behind rubble).</summary>
        Secret = 5,

        /// <summary>The way in from outside (A is the room, B is empty).</summary>
        Entrance = 6,
    }

    public enum DoorState
    {
        Open = 0,
        Closed = 1,
        Locked = 2,
        Broken = 3,

        /// <summary>Shut from the far side; opened only from there (or forced).</summary>
        Barred = 4,
    }

    /// <summary>A rectangle of cells on one floor (1 cell = one combat square, 1.5 m).</summary>
    public struct CellRect : IEquatable<CellRect>
    {
        public CellRect(int x, int z, int w, int d)
        {
            X = x;
            Z = z;
            W = w;
            D = d;
        }

        public int X { get; set; }
        public int Z { get; set; }
        public int W { get; set; }
        public int D { get; set; }

        public int MaxX => X + W;
        public int MaxZ => Z + D;
        public int CenterX => X + W / 2;
        public int CenterZ => Z + D / 2;
        public int Area => W * D;

        public bool Contains(int x, int z) => x >= X && x < MaxX && z >= Z && z < MaxZ;

        /// <summary>True when the rectangles share at least one cell.</summary>
        public bool Overlaps(CellRect o) => X < o.MaxX && o.X < MaxX && Z < o.MaxZ && o.Z < MaxZ;

        /// <summary>True when they share a cell or touch along an edge (corners alone do not count).</summary>
        public bool Touches(CellRect o)
        {
            var xOverlap = X < o.MaxX && o.X < MaxX;
            var zOverlap = Z < o.MaxZ && o.Z < MaxZ;
            return (xOverlap && (Z == o.MaxZ || MaxZ == o.Z || zOverlap)) || (zOverlap && (X == o.MaxX || MaxX == o.X));
        }

        public bool Equals(CellRect o) => X == o.X && Z == o.Z && W == o.W && D == o.D;
        public override bool Equals(object obj) => obj is CellRect o && Equals(o);
        public override int GetHashCode() => unchecked(((X * 397 ^ Z) * 397 ^ W) * 397 ^ D);
        public override string ToString() => $"[{X},{Z} {W}x{D}]";
    }

    public sealed class PlaceFloor
    {
        public int Index { get; set; }
        public string Name { get; set; }

        /// <summary>Height of the floor in metres relative to the entrance floor (negative below ground).</summary>
        public float Elevation { get; set; }
    }

    public sealed class PlaceRoom
    {
        public string Id { get; set; }
        public int Floor { get; set; }
        public CellRect Rect { get; set; }
        public RoomKind Kind { get; set; }
        public string Name { get; set; }

        /// <summary>Ceiling height in metres.</summary>
        public float Ceiling { get; set; } = 3.5f;

        /// <summary>Reached only through a secret connection (never holds anything the place requires).</summary>
        public bool Secret { get; set; }

        /// <summary>Entering without leave is trespass (a garrison's armoury, an owner's bedroom).</summary>
        public bool Restricted { get; set; }

        /// <summary>A light burns here (torches, a hearth, daylight); unlit rooms are dark.</summary>
        public bool Lit { get; set; }

        /// <summary>A room-wide hazard id from <see cref="HazardCatalog"/> ("toxic_air"), or null.</summary>
        public string Hazard { get; set; }

        /// <summary>Free tags the archetype and the builder read ("rails", "coffins", "hearth", "bunks").</summary>
        public List<string> Tags { get; set; } = new List<string>();

        public PlaceRoom Clone()
        {
            var c = (PlaceRoom)MemberwiseClone();
            c.Tags = new List<string>(Tags);
            return c;
        }
    }

    public sealed class PlaceConnection
    {
        public string Id { get; set; }
        public ConnectionKind Kind { get; set; }
        public string A { get; set; }

        /// <summary>The other room (empty for an entrance).</summary>
        public string B { get; set; }

        /// <summary>The cells the passage occupies (a corridor's run, a doorway's single cell, a stair's foot on A's floor).</summary>
        public CellRect Rect { get; set; }

        /// <summary>For stairs and ladders: where the other end lands (a cell inside B, on B's floor).</summary>
        public int ToX { get; set; }
        public int ToZ { get; set; }

        /// <summary>A door's state when the place was made.</summary>
        public DoorState Door { get; set; }

        /// <summary>The key that opens it (an item id, e.g. "iron_key"), or null.</summary>
        public string KeyItem { get; set; }

        /// <summary>DC to pick the lock with thieves' tools (Sleight of Hand) or to force it (Athletics); 0 when it cannot be.</summary>
        public int LockDc { get; set; }

        /// <summary>DC to find a secret connection (Investigation when searching, passive Perception on entering).</summary>
        /// <summary>An inhabitant group that holds this passage (a gate's watch): passing needs their leave, or their defeat.</summary>
        public string GuardedBy { get; set; }

        public int FindDc { get; set; }

        public PlaceConnection Clone() => (PlaceConnection)MemberwiseClone();

        public string Other(string room) => room == A ? B : room == B ? A : null;
        public bool Joins(string room) => A == room || B == room;
        public bool IsVertical => Kind == ConnectionKind.Stairs || Kind == ConnectionKind.Ladder;
    }

    public enum FeatureKind
    {
        /// <summary>Something to look at closely: a clue, a mark, a body, remains.</summary>
        Clue = 0,

        /// <summary>Something to read: a letter, a ledger, an inscription.</summary>
        Document = 1,

        /// <summary>Holds things (a chest, a crate, a sarcophagus): a Phase 11 container.</summary>
        Container = 2,

        Trap = 3,

        /// <summary>A lever, a winch, a seal: changes something else in the place.</summary>
        Mechanism = 4,

        /// <summary>A person held here (a rescue).</summary>
        Captive = 5,

        /// <summary>A crafting station (a forge, a workbench) in an interior.</summary>
        Workstation = 6,

        /// <summary>A hazard with a place (a gas pocket, a sagging beam).</summary>
        Hazard = 7,

        /// <summary>A shrine, an altar: may be prayed at or examined.</summary>
        Altar = 8,

        /// <summary>Furniture with a use (a bed to rest in).</summary>
        Furniture = 9,
    }

    public sealed class PlaceFeature
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public FeatureKind Kind { get; set; }

        /// <summary>Its definition in <see cref="FeatureCatalog"/>.</summary>
        public string DefinitionId { get; set; }

        public int X { get; set; }
        public int Z { get; set; }
        public float Yaw { get; set; }

        /// <summary>Not seen until found (a hidden compartment, a concealed trap).</summary>
        public bool Hidden { get; set; }

        /// <summary>DC to find it when hidden.</summary>
        public int FindDc { get; set; }

        /// <summary>Per-feature data: "container" key, "npc" id, "reveals" connection id, "text", "loot" table, "trap" id.</summary>
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The place requires it (a quest's object); it must be reachable without secret passages.</summary>
        public bool Required { get; set; }

        public string Get(string key) => Data != null && Data.TryGetValue(key, out var v) ? v : null;

        public PlaceFeature Clone()
        {
            var c = (PlaceFeature)MemberwiseClone();
            c.Data = new Dictionary<string, string>(Data, StringComparer.Ordinal);
            return c;
        }
    }

    public enum GroupBehaviour
    {
        /// <summary>Stays in its room and fights intruders (a den, a lair).</summary>
        Lair = 0,

        /// <summary>Holds a post; lets those with leave pass (a garrison).</summary>
        Guard = 1,

        /// <summary>Waits to fall on whoever comes (an ambush; starts the fight with surprise).</summary>
        Ambush = 2,
    }

    /// <summary>One member of an inhabitant group: a creature of the bestiary, or a persistent person.</summary>
    public sealed class GroupMember
    {
        public string CreatureId { get; set; }
        public NpcId NpcId { get; set; }
        public int X { get; set; }
        public int Z { get; set; }

        public GroupMember Clone() => (GroupMember)MemberwiseClone();
    }

    /// <summary>Inhabitants who act together: a spider brood, a crypt's dead, a fort's watch.</summary>
    public sealed class PlaceGroup
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public string Name { get; set; }
        public GroupBehaviour Behaviour { get; set; }
        public List<GroupMember> Members { get; set; } = new List<GroupMember>();

        /// <summary>A faction they serve (guards), or empty.</summary>
        public FactionId FactionId { get; set; }

        /// <summary>Only wakes when a feature is used (a sarcophagus opened, a captive freed): that feature's id.</summary>
        public string WakesOn { get; set; }

        public PlaceGroup Clone()
        {
            var c = (PlaceGroup)MemberwiseClone();
            c.Members = new List<GroupMember>();
            foreach (var m in Members) c.Members.Add(m.Clone());
            return c;
        }
    }

    /// <summary>
    /// The structure of a place (ADR-0063): floors, rooms, connections, features and inhabitants, in cells. It is
    /// generated once (or authored) and then saved, so a later generator can never change a place that exists.
    /// </summary>
    public sealed class PlaceLayout
    {
        public List<PlaceFloor> Floors { get; set; } = new List<PlaceFloor>();
        public List<PlaceRoom> Rooms { get; set; } = new List<PlaceRoom>();
        public List<PlaceConnection> Connections { get; set; } = new List<PlaceConnection>();
        public List<PlaceFeature> Features { get; set; } = new List<PlaceFeature>();
        public List<PlaceGroup> Groups { get; set; } = new List<PlaceGroup>();

        public PlaceRoom Room(string id)
        {
            foreach (var r in Rooms) if (r.Id == id) return r;
            return null;
        }

        public PlaceConnection Connection(string id)
        {
            foreach (var c in Connections) if (c.Id == id) return c;
            return null;
        }

        public PlaceFeature Feature(string id)
        {
            foreach (var f in Features) if (f.Id == id) return f;
            return null;
        }

        public PlaceGroup Group(string id)
        {
            foreach (var g in Groups) if (g.Id == id) return g;
            return null;
        }

        public PlaceConnection Entrance
        {
            get
            {
                foreach (var c in Connections) if (c.Kind == ConnectionKind.Entrance) return c;
                return null;
            }
        }

        /// <summary>The room containing a cell of a floor (rooms first, then a corridor's own cells belong to its connection).</summary>
        public PlaceRoom RoomAt(int floor, int x, int z)
        {
            foreach (var r in Rooms) if (r.Floor == floor && r.Rect.Contains(x, z)) return r;
            return null;
        }

        public PlaceLayout Clone()
        {
            var c = new PlaceLayout();
            foreach (var f in Floors) c.Floors.Add(new PlaceFloor { Index = f.Index, Name = f.Name, Elevation = f.Elevation });
            foreach (var r in Rooms) c.Rooms.Add(r.Clone());
            foreach (var x in Connections) c.Connections.Add(x.Clone());
            foreach (var f in Features) c.Features.Add(f.Clone());
            foreach (var g in Groups) c.Groups.Add(g.Clone());
            return c;
        }
    }

    /// <summary>Flags a feature has gathered (persisted; only what changed is stored).</summary>
    [Flags]
    public enum FeatureFlags
    {
        None = 0,
        Found = 1,
        Examined = 2,
        Opened = 4,
        Disarmed = 8,
        Triggered = 16,
        Read = 32,
        Used = 64,
        Broken = 128,
        Freed = 256,
        Looted = 512,
    }

    public sealed class FeatureStateData
    {
        public FeatureFlags Flags { get; set; }

        /// <summary>Failed attempts (a lock tried, a search made) — some checks may not be retried until something changes.</summary>
        public int Attempts { get; set; }

        /// <summary>Absolute world minute of the last change, or -1.</summary>
        public long ChangedAt { get; set; } = -1;

        public bool Has(FeatureFlags f) => (Flags & f) == f;
        public FeatureStateData Clone() => (FeatureStateData)MemberwiseClone();
    }

    public enum GroupState
    {
        /// <summary>There, and not yet met.</summary>
        Present = 0,

        /// <summary>A fight with them is going on.</summary>
        Fighting = 1,

        /// <summary>Slain or routed: they stay gone (no respawn).</summary>
        Defeated = 2,

        /// <summary>Met and left in peace (guards who let the player pass).</summary>
        Peaceful = 3,

        /// <summary>Some survived a fight the player left: they remain, with their wounds.</summary>
        Survived = 4,
    }

    public sealed class GroupStateData
    {
        public GroupState State { get; set; }
        public EncounterId EncounterId { get; set; }

        /// <summary>Alerted (an alarm, an earlier fight): they lose surprise and gain it.</summary>
        public bool Alerted { get; set; }

        public GroupStateData Clone() => (GroupStateData)MemberwiseClone();
    }

    /// <summary>
    /// A persistent explorable place in the world (ADR-0062): its identity, where it hangs, its saved layout and every
    /// change the player or the world made to it. One per place; entering it never copies the world.
    /// </summary>
    public sealed class PlaceData
    {
        public const int MaxNotes = 30;

        /// <summary>Stable id ("place:mine:{region}:{n}", "interior:{settlement}:{layout key}").</summary>
        public string PlaceId { get; set; }

        /// <summary>The definition it was made from (<see cref="PlaceCatalog"/>).</summary>
        public string DefinitionId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public PlaceCategory Category { get; set; }
        public string ArchetypeId { get; set; }

        public PlaceParentKind ParentKind { get; set; }

        /// <summary>The parent's id: a region, a settlement, a Phase 4 location (for a building) or another place.</summary>
        public string ParentId { get; set; }

        public RegionId RegionId { get; set; }

        /// <summary>The settlement it belongs to or lies nearest (for rumours, encounters and the way back).</summary>
        public SettlementId SettlementId { get; set; }

        /// <summary>For an interior: the building's Phase 4 location (NPC schedules point at it) and layout key.</summary>
        public LocationId LocationId { get; set; }
        public string LayoutKey { get; set; }

        /// <summary>For an interior: where the character stands in the settlement on leaving (outside the door).</summary>
        public LocalPose ExitPose { get; set; }

        /// <summary>Where it is on the region map (wilderness places).</summary>
        public MapCoord Cell { get; set; }

        public ulong Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public PlaceLayout Layout { get; set; } = new PlaceLayout();

        public PlaceDiscovery Discovery { get; set; }
        public long DiscoveredAt { get; set; } = -1;
        public long LastVisitedAt { get; set; } = -1;
        public int Visits { get; set; }

        /// <summary>Who holds it (a fort's faction), or empty.</summary>
        public FactionId FactionId { get; set; }

        /// <summary>How dangerous it is said to be, 0–100 (from its inhabitants; falls as they are defeated).</summary>
        public int Threat { get; set; }

        public List<string> ExploredRooms { get; set; } = new List<string>();
        public Dictionary<string, DoorState> Doors { get; set; } = new Dictionary<string, DoorState>(StringComparer.Ordinal);

        /// <summary>Secret connections the player has found.</summary>
        public List<string> FoundConnections { get; set; } = new List<string>();

        public Dictionary<string, FeatureStateData> Features { get; set; } = new Dictionary<string, FeatureStateData>(StringComparer.Ordinal);
        public Dictionary<string, GroupStateData> Groups { get; set; } = new Dictionary<string, GroupStateData>(StringComparer.Ordinal);

        /// <summary>Small named facts ("access" = "granted", "collapse" = "cleared").</summary>
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>What happened here, newest last (bounded).</summary>
        public List<string> Notes { get; set; } = new List<string>();

        public DoorState DoorOf(PlaceConnection c) => Doors.TryGetValue(c.Id, out var s) ? s : c.Door;

        public FeatureStateData StateOf(string featureId)
        {
            if (!Features.TryGetValue(featureId, out var s)) Features[featureId] = s = new FeatureStateData();
            return s;
        }

        public bool Has(string featureId, FeatureFlags f) => Features.TryGetValue(featureId, out var s) && s.Has(f);

        public GroupStateData GroupOf(string groupId)
        {
            if (!Groups.TryGetValue(groupId, out var s)) Groups[groupId] = s = new GroupStateData();
            return s;
        }

        public GroupState GroupStateOf(string groupId) => Groups.TryGetValue(groupId, out var s) ? s.State : GroupState.Present;

        public bool Explored(string room) => ExploredRooms.Contains(room);

        public bool Found(PlaceConnection c) => c.Kind != ConnectionKind.Secret || FoundConnections.Contains(c.Id);

        public void Note(string text)
        {
            Notes.Add(text);
            while (Notes.Count > MaxNotes) Notes.RemoveAt(0);
        }

        public PlaceData Clone()
        {
            var c = (PlaceData)MemberwiseClone();
            c.Layout = Layout?.Clone();
            c.ExploredRooms = new List<string>(ExploredRooms);
            c.Doors = new Dictionary<string, DoorState>(Doors, StringComparer.Ordinal);
            c.FoundConnections = new List<string>(FoundConnections);
            c.Features = new Dictionary<string, FeatureStateData>(StringComparer.Ordinal);
            foreach (var p in Features) c.Features[p.Key] = p.Value.Clone();
            c.Groups = new Dictionary<string, GroupStateData>(StringComparer.Ordinal);
            foreach (var p in Groups) c.Groups[p.Key] = p.Value.Clone();
            c.Variables = new Dictionary<string, string>(Variables, StringComparer.Ordinal);
            c.Notes = new List<string>(Notes);
            return c;
        }
    }

    /// <summary>
    /// Where a person the player is responsible for stands (ADR-0066): held in a place, following the player, or home.
    /// The person is always the same <see cref="NpcData"/>; this only says where they are instead of their schedule.
    /// </summary>
    public enum EscortState
    {
        /// <summary>Held captive in a place.</summary>
        Captive = 0,

        /// <summary>Freed and following the player through every transition.</summary>
        Following = 1,

        /// <summary>Brought home: back to their schedule.</summary>
        Arrived = 2,

        /// <summary>Died while held or escorted.</summary>
        Lost = 3,

        /// <summary>Left behind when the player went elsewhere without them; waits where they were.</summary>
        Waiting = 4,
    }

    public sealed class EscortData
    {
        public NpcId NpcId { get; set; }
        public EscortState State { get; set; }

        /// <summary>Where they are while Captive or Waiting.</summary>
        public string PlaceId { get; set; }
        public string Room { get; set; }

        /// <summary>Where they should be brought.</summary>
        public SettlementId Destination { get; set; }

        /// <summary>The quest instance it belongs to, or null.</summary>
        public string QuestInstanceId { get; set; }

        public long ChangedAt { get; set; } = -1;

        public bool OverridesSchedule => State == EscortState.Captive || State == EscortState.Following || State == EscortState.Waiting;

        public EscortData Clone() => (EscortData)MemberwiseClone();
    }
}
