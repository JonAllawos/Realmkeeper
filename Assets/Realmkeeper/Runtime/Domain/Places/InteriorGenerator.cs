using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Settlements;

namespace Realmkeeper.Domain.Places
{
    /// <summary>What an interior is made from: the building's function, footprint, height and the settlement's wealth.</summary>
    public sealed class InteriorRequest
    {
        public BuildingFunction Function { get; set; }
        public float WidthMetres { get; set; }
        public float DepthMetres { get; set; }
        public int Storeys { get; set; } = 1;
        public int Wealth { get; set; } = 50;
    }

    /// <summary>
    /// Phase 13 (ADR-0063): the inside of a settlement building, planned from its function and footprint. Rooms share
    /// walls (no corridors) and are joined by doorways in those walls; the plan reflects the building's purpose and the
    /// settlement's wealth (a cellar for the well-off, an upper floor where the building has one). People are not part of
    /// the plan: they are shown inside from their own schedules (ADR-0066).
    /// </summary>
    public static class InteriorGenerator
    {
        public const int MinCells = 5;
        public const int MaxCells = 14;

        /// <summary>The functions that have an interior to enter (the tavern hall and shops stay open to the street).</summary>
        public static bool HasInterior(BuildingFunction f)
        {
            switch (f)
            {
                case BuildingFunction.Tavern:
                case BuildingFunction.Shop:
                case BuildingFunction.MarketHall:
                case BuildingFunction.Shed:
                case BuildingFunction.Stable:
                    return false;
                default:
                    return true;
            }
        }

        public static string Plan(BuildingFunction f)
        {
            switch (f)
            {
                case BuildingFunction.Smithy:
                case BuildingFunction.Workshop:
                case BuildingFunction.Mill:
                    return "workshop";
                case BuildingFunction.Granary:
                case BuildingFunction.Warehouse:
                case BuildingFunction.Barn:
                    return "store";
                case BuildingFunction.GuardPost:
                case BuildingFunction.Gatehouse:
                    return "guard";
                case BuildingFunction.Religious:
                    return "chapel";
                case BuildingFunction.Civic:
                    return "civic";
                case BuildingFunction.Keep:
                case BuildingFunction.Watchtower:
                    return "tower";
                default:
                    return "home";
            }
        }

        public static PlaceLayout Generate(InteriorRequest req, ulong seed)
        {
            var rng = new PlaceRng(seed);
            var w = Clamp((int)(req.WidthMetres / 1.5f));
            var d = Clamp((int)(req.DepthMetres / 1.5f));
            var layout = new PlaceLayout();
            layout.Floors.Add(new PlaceFloor { Index = 0, Name = "Ground floor", Elevation = 0f });
            var plan = Plan(req.Function);
            var front = Math.Max(3, (int)Math.Round(d * 0.55));
            var back = d - front;
            var rooms = layout.Rooms;
            var c = 0;
            string NextC() => "c" + (c++);

            PlaceRoom Room(string id, int floor, int x, int z, int rw, int rd, RoomKind kind, string name, bool restricted = false) =>
                Add(rooms, new PlaceRoom { Id = id, Floor = floor, Rect = new CellRect(x, z, rw, rd), Kind = kind, Name = name, Lit = true, Ceiling = 2.8f, Restricted = restricted });

            PlaceRoom main;
            switch (plan)
            {
                case "workshop":
                    main = Room("work", 0, 0, 0, w, front, RoomKind.Workshop, req.Function == BuildingFunction.Smithy ? "Forge" : "Workshop");
                    Room("store", 0, 0, front, w / 2, back, RoomKind.Storage, "Store room");
                    Room("quarters", 0, w / 2, front, w - w / 2, back, RoomKind.Quarters, "Quarters", restricted: true);
                    Door(layout, NextC(), "work", "store", ConnectionKind.Opening);
                    Door(layout, NextC(), "work", "quarters", ConnectionKind.Door);
                    Feature(layout, rng, "work", req.Function == BuildingFunction.Smithy ? "forge" : "workbench");
                    Feature(layout, rng, "quarters", "bed");
                    Box(layout, rng, "store", "crate", owned: true);
                    break;
                case "store":
                    main = Room("hall", 0, 0, 0, w, d - Math.Min(3, back), RoomKind.Storage, req.Function == BuildingFunction.Granary ? "Store floor" : "Store hall");
                    var officeD = d - main.Rect.D;
                    Room("office", 0, 0, main.Rect.D, Math.Max(3, w / 2), officeD, RoomKind.Office, "Keeper's office");
                    Door(layout, NextC(), "hall", "office", ConnectionKind.Door);
                    Feature(layout, rng, "office", req.Function == BuildingFunction.Granary ? "granary_ledger_in" : "desk");
                    Box(layout, rng, "hall", "crate", owned: true);
                    Box(layout, rng, "hall", "crate", owned: true);
                    break;
                case "guard":
                    main = Room("guardroom", 0, 0, 0, w, front, RoomKind.Barracks, "Guard room");
                    Room("cells", 0, 0, front, w / 2, back, RoomKind.Cell, "Cells", restricted: true);
                    Room("armoury", 0, w / 2, front, w - w / 2, back, RoomKind.Armoury, "Armoury", restricted: true);
                    Door(layout, NextC(), "guardroom", "cells", ConnectionKind.Door);
                    var lockC = Door(layout, NextC(), "guardroom", "armoury", ConnectionKind.Door);
                    lockC.Door = DoorState.Locked;
                    lockC.LockDc = 15;
                    Feature(layout, rng, "guardroom", "duty_roster");
                    Box(layout, rng, "armoury", "chest", owned: true, loot: "garrison_stores");
                    break;
                case "chapel":
                    main = Room("nave", 0, 0, 0, w, d - Math.Min(3, back), RoomKind.Shrine, "Nave");
                    Room("vestry", 0, 0, main.Rect.D, Math.Max(3, w / 2), d - main.Rect.D, RoomKind.Office, "Vestry", restricted: true);
                    Door(layout, NextC(), "nave", "vestry", ConnectionKind.Door);
                    Feature(layout, rng, "nave", "ward_altar");
                    Feature(layout, rng, "vestry", "desk");
                    break;
                case "civic":
                    main = Room("hall", 0, 0, 0, w, front, RoomKind.Hall, "Hall");
                    Room("office", 0, 0, front, w, back, RoomKind.Office, "Clerk's office", restricted: true);
                    Door(layout, NextC(), "hall", "office", ConnectionKind.Door);
                    Feature(layout, rng, "office", "desk");
                    Box(layout, rng, "office", "chest", owned: true);
                    break;
                case "tower":
                    main = Room("base", 0, 0, 0, w, d, RoomKind.Hall, "Tower hall");
                    Feature(layout, rng, "base", "faction_banner");
                    break;
                default:
                    main = Room("main", 0, 0, 0, w, front, RoomKind.Chamber, "Main room");
                    if (w >= 7)
                    {
                        Room("bedroom", 0, 0, front, w / 2, back, RoomKind.Bedroom, "Bedroom", restricted: true);
                        Room("larder", 0, w / 2, front, w - w / 2, back, RoomKind.Storage, "Larder");
                        Door(layout, NextC(), "main", "larder", ConnectionKind.Opening);
                    }
                    else Room("bedroom", 0, 0, front, w, back, RoomKind.Bedroom, "Bedroom", restricted: true);

                    Door(layout, NextC(), "main", "bedroom", ConnectionKind.Door);
                    Feature(layout, rng, "main", "hearth");
                    Feature(layout, rng, "bedroom", "bed");
                    Box(layout, rng, "bedroom", "chest", owned: true);
                    if (req.Wealth >= 55) Feature(layout, rng, "main", "desk");
                    break;
            }

            // Upper floors (houses with a second storey, towers): reached by stairs from the main room.
            var storeys = Math.Max(1, Math.Min(plan == "tower" ? 3 : 2, req.Storeys));
            for (var f = 1; f < storeys; f++)
            {
                layout.Floors.Add(new PlaceFloor { Index = f, Name = f == 1 ? "Upper floor" : "Floor " + (f + 1), Elevation = f * 3.2f });
                var below = f == 1 ? main : layout.Room("up" + (f - 1));
                var up = Room("up" + f, f, 0, 0, w, d, plan == "tower" ? RoomKind.Gatehouse : RoomKind.Bedroom, plan == "tower" ? (f == storeys - 1 ? "Watch room" : "Guard floor") : "Upper room",
                    restricted: plan != "tower");
                var odd = f % 2 == 1;
                layout.Connections.Add(new PlaceConnection
                {
                    Id = NextC(), Kind = ConnectionKind.Stairs, A = below.Id, B = up.Id,
                    Rect = odd ? new CellRect(below.Rect.MaxX - 2, below.Rect.MaxZ - 2, 1, 1) : new CellRect(below.Rect.X + 1, below.Rect.Z + 1, 1, 1),
                    ToX = odd ? w - 2 : 1, ToZ = odd ? d - 3 : 2,
                });
                if (plan != "tower") Feature(layout, rng, up.Id, "bed");
            }

            // A cellar for the well-off (homes and stores).
            if (req.Wealth >= 60 && (plan == "home" || plan == "store"))
            {
                layout.Floors.Add(new PlaceFloor { Index = storeys, Name = "Cellar", Elevation = -3f });
                var cellar = Room("cellar", storeys, 0, 0, Math.Max(4, w - 2), Math.Max(4, d - 2), RoomKind.Cellar, "Cellar");
                cellar.Lit = false;
                cellar.Hazard = "darkness";
                layout.Connections.Add(new PlaceConnection { Id = NextC(), Kind = ConnectionKind.Stairs, A = main.Id, B = cellar.Id, Rect = new CellRect(main.Rect.X + 1, main.Rect.MaxZ - 2, 1, 1), ToX = 1, ToZ = cellar.Rect.MaxZ - 2 });
                Box(layout, rng, "cellar", "crate", owned: true);
            }

            // Nothing stands on a stair: furniture placed before the stairs were added moves aside.
            foreach (var f in layout.Features)
                foreach (var s in layout.Connections)
                    if (s.IsVertical && ((s.A == f.Room && s.Rect.X == f.X && s.Rect.Z == f.Z) || (s.B == f.Room && s.ToX == f.X && s.ToZ == f.Z)))
                    {
                        f.X = -1;
                        (f.X, f.Z) = Spot(layout, rng, layout.Room(f.Room));
                        if (f.X < 0) f.Room = null; // nowhere left: removed below
                    }
            layout.Features.RemoveAll(f => f.Room == null);

            layout.Connections.Insert(0, new PlaceConnection { Id = "entrance", Kind = ConnectionKind.Entrance, A = main.Id, Rect = new CellRect(main.Rect.CenterX - 1, 0, 2, 1), Door = DoorState.Closed });
            return layout;
        }

        private static int Clamp(int cells) => Math.Max(MinCells, Math.Min(MaxCells, cells));

        private static PlaceRoom Add(List<PlaceRoom> rooms, PlaceRoom r)
        {
            rooms.Add(r);
            return r;
        }

        /// <summary>A doorway in the wall A and B share: two cells inside A along the shared edge, centred on the overlap.</summary>
        private static PlaceConnection Door(PlaceLayout layout, string id, string a, string b, ConnectionKind kind)
        {
            var ra = layout.Room(a).Rect;
            var rb = layout.Room(b).Rect;
            CellRect rect;
            if (ra.MaxZ == rb.Z || rb.MaxZ == ra.Z)
            {
                var lo = Math.Max(ra.X, rb.X);
                var hi = Math.Min(ra.MaxX, rb.MaxX);
                var x = Math.Max(lo, (lo + hi) / 2 - 1);
                var width = Math.Min(2, hi - x);
                rect = new CellRect(x, ra.MaxZ == rb.Z ? ra.MaxZ - 1 : ra.Z, width, 1);
            }
            else
            {
                var lo = Math.Max(ra.Z, rb.Z);
                var hi = Math.Min(ra.MaxZ, rb.MaxZ);
                var z = Math.Max(lo, (lo + hi) / 2 - 1);
                rect = new CellRect(ra.MaxX == rb.X ? ra.MaxX - 1 : ra.X, z, 1, Math.Min(2, hi - z));
            }

            var c = new PlaceConnection { Id = id, Kind = kind, A = a, B = b, Rect = rect, Door = kind == ConnectionKind.Door ? DoorState.Closed : DoorState.Open };
            layout.Connections.Add(c);
            return c;
        }

        private static void Feature(PlaceLayout layout, PlaceRng rng, string room, string def)
        {
            var d = FeatureCatalog.Find(def);
            var r = layout.Room(room);
            if (d == null || r == null) return;
            var (x, z) = Spot(layout, rng, r);
            if (x < 0) return; // no room left for it
            layout.Features.Add(new PlaceFeature { Id = def + ":" + room, Room = room, Kind = d.Kind, DefinitionId = def, X = x, Z = z });
        }

        /// <summary>A container that belongs to the household (looked into, not taken from: theft is not part of Phase 13).</summary>
        private static void Box(PlaceLayout layout, PlaceRng rng, string room, string def, bool owned, string loot = null)
        {
            var r = layout.Room(room);
            if (r == null) return;
            var (x, z) = Spot(layout, rng, r);
            if (x < 0) return;
            var f = new PlaceFeature { Id = def + ":" + room + ":" + layout.Features.Count, Room = room, Kind = FeatureKind.Container, DefinitionId = def, X = x, Z = z };
            if (owned) f.Data["owned"] = "yes";
            if (loot != null) f.Data["loot"] = loot;
            layout.Features.Add(f);
        }

        /// <summary>A free cell along the room's back or side wall, clear of doorways and stairs.</summary>
        private static (int, int) Spot(PlaceLayout layout, PlaceRng rng, PlaceRoom r)
        {
            var options = new List<(int, int)>();
            for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                for (var z = r.Rect.Z; z < r.Rect.MaxZ; z++)
                {
                    var edge = x == r.Rect.X || x == r.Rect.MaxX - 1 || z == r.Rect.MaxZ - 1;
                    if (!edge || z == r.Rect.Z) continue;
                    if (Busy(layout, r, x, z)) continue;
                    options.Add((x, z));
                }

            if (options.Count == 0)
                for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                    for (var z = r.Rect.Z + 1; z < r.Rect.MaxZ; z++)
                        if (!Busy(layout, r, x, z)) options.Add((x, z));
            if (options.Count == 0)
                for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                    for (var z = r.Rect.Z; z < r.Rect.MaxZ; z++)
                        if (!Busy(layout, r, x, z)) options.Add((x, z));
            return options.Count == 0 ? (-1, -1) : options[rng.Next(options.Count)];
        }

        private static bool Busy(PlaceLayout layout, PlaceRoom r, int x, int z)
        {
            foreach (var f in layout.Features) if (f.Room == r.Id && f.X == x && f.Z == z) return true;
            foreach (var c in layout.Connections)
            {
                if (c.IsVertical && ((c.A == r.Id && c.Rect.X == x && c.Rect.Z == z) || (c.B == r.Id && c.ToX == x && c.ToZ == z))) return true;
                if (c.IsVertical || !c.Joins(r.Id)) continue;
                if (x >= c.Rect.X - 1 && x <= c.Rect.MaxX && z >= c.Rect.Z - 1 && z <= c.Rect.MaxZ) return true;
            }

            return false;
        }
    }
}
