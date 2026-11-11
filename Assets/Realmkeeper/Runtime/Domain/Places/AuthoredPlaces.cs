namespace Realmkeeper.Domain.Places
{
    /// <summary>
    /// Phase 13 (ADR-0062): hand-built layouts, in the same model as generated ones. The watch fort is authored because
    /// its story depends on exact geometry: a guarded gate, a courtyard the barracks overlooks, a barred armoury, a side
    /// door few know of, and the captain's quarters upstairs behind a locked door whose key is kept in the armoury.
    /// Its garrison are persistent people, bound when the fort is placed in the world.
    /// </summary>
    public static class AuthoredPlaces
    {
        public static PlaceLayout WatchFort()
        {
            var l = new PlaceLayout();
            l.Floors.Add(new PlaceFloor { Index = 0, Name = "Courtyard level", Elevation = 0f });
            l.Floors.Add(new PlaceFloor { Index = 1, Name = "Wall walk", Elevation = 5f });

            PlaceRoom R(string id, int floor, int x, int z, int w, int d, RoomKind kind, string name, float ceiling = 3.5f, bool lit = true, bool restricted = false, string hazard = null)
            {
                var r = new PlaceRoom { Id = id, Floor = floor, Rect = new CellRect(x, z, w, d), Kind = kind, Name = name, Ceiling = ceiling, Lit = lit, Restricted = restricted, Hazard = hazard };
                l.Rooms.Add(r);
                return r;
            }

            R("gate", 0, 10, 0, 8, 6, RoomKind.Gatehouse, "Gatehouse");
            R("yard", 0, 8, 8, 12, 10, RoomKind.Yard, "Courtyard", ceiling: 6f).Tags.Add("open_sky");
            R("barracks", 0, 0, 9, 6, 8, RoomKind.Barracks, "Barracks").Tags.Add("bunks");
            R("armoury", 0, 22, 9, 6, 6, RoomKind.Armoury, "Armoury", restricted: true).Tags.Add("racks");
            R("store", 0, 22, 0, 6, 6, RoomKind.Storage, "Side store", lit: false, hazard: "darkness");
            R("watch", 1, 16, 8, 6, 7, RoomKind.Gatehouse, "Watch room");
            R("quarters", 1, 8, 8, 7, 7, RoomKind.Quarters, "Captain's quarters", restricted: true);

            l.Connections.Add(new PlaceConnection { Id = "entrance", Kind = ConnectionKind.Entrance, A = "gate", Rect = new CellRect(13, 0, 2, 1) });
            l.Connections.Add(new PlaceConnection
            {
                Id = "side_door", Kind = ConnectionKind.Entrance, A = "store", Rect = new CellRect(24, 0, 2, 1), Door = DoorState.Locked, LockDc = 14, FindDc = 14,
            });
            l.Connections.Add(new PlaceConnection { Id = "c_gate", Kind = ConnectionKind.Door, A = "gate", B = "yard", Rect = new CellRect(13, 6, 2, 2), Door = DoorState.Closed, GuardedBy = "watch" });
            l.Connections.Add(new PlaceConnection { Id = "c_barracks", Kind = ConnectionKind.Opening, A = "yard", B = "barracks", Rect = new CellRect(6, 12, 2, 2) });
            l.Connections.Add(new PlaceConnection { Id = "c_armoury", Kind = ConnectionKind.Door, A = "armoury", B = "yard", Rect = new CellRect(20, 11, 2, 2), Door = DoorState.Barred, LockDc = 16 });
            l.Connections.Add(new PlaceConnection { Id = "c_store", Kind = ConnectionKind.Opening, A = "store", B = "armoury", Rect = new CellRect(24, 6, 2, 3) });
            l.Connections.Add(new PlaceConnection { Id = "c_stairs", Kind = ConnectionKind.Stairs, A = "yard", B = "watch", Rect = new CellRect(18, 16, 1, 1), ToX = 17, ToZ = 9 });
            l.Connections.Add(new PlaceConnection
            {
                Id = "c_quarters", Kind = ConnectionKind.Door, A = "watch", B = "quarters", Rect = new CellRect(15, 10, 1, 2), Door = DoorState.Locked, KeyItem = "fort_key", LockDc = 15,
            });

            PlaceFeature F(string id, string room, FeatureKind kind, string def, int x, int z, params (string, string)[] data)
            {
                var f = new PlaceFeature { Id = id, Room = room, Kind = kind, DefinitionId = def, X = x, Z = z };
                foreach (var (k, v) in data) f.Data[k] = v;
                l.Features.Add(f);
                return f;
            }

            F("roster", "gate", FeatureKind.Document, "duty_roster", 11, 3);
            F("banner", "yard", FeatureKind.Clue, "faction_banner", 10, 16);
            F("bunks", "barracks", FeatureKind.Furniture, "bed", 4, 10);
            F("barracks_chest", "barracks", FeatureKind.Container, "chest", 1, 15, ("loot", "garrison_stores"), ("owner_group", "barracks"));
            F("armoury_chest", "armoury", FeatureKind.Container, "chest", 26, 13, ("loot", "garrison_stores"), ("key", "fort_key"));
            F("orders", "quarters", FeatureKind.Document, "garrison_orders", 9, 13);
            F("captains_box", "quarters", FeatureKind.Container, "chest", 13, 9, ("loot", "vault_treasure"), ("owner_group", "captain"));
            F("captains_bed", "quarters", FeatureKind.Furniture, "bed", 9, 9);

            PlaceGroup G(string id, string room, string name, params (string Creature, int X, int Z)[] members)
            {
                var g = new PlaceGroup { Id = id, Room = room, Name = name, Behaviour = GroupBehaviour.Guard };
                foreach (var (c, x, z) in members) g.Members.Add(new GroupMember { CreatureId = c, X = x, Z = z });
                l.Groups.Add(g);
                return g;
            }

            G("watch", "gate", "the gate watch", ("guard", 11, 1), ("guard", 16, 2));
            G("barracks", "barracks", "off-duty guards", ("guard", 2, 11), ("guard", 3, 13));
            G("captain", "quarters", "the captain", ("veteran", 12, 11));
            return l;
        }
    }
}
