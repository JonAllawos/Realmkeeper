using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    /// <summary>The kinds of building the modular kit assembles (presentation only, derived from the layout).</summary>
    public enum BuildingArchetype
    {
        Cottage = 0,
        Townhouse = 1,
        MerchantHouse = 2,
        Workshop = 3,
        Tavern = 4,
        Granary = 5,
        Tower = 6,
        Gatehouse = 7,
    }

    /// <summary>The wall material family of a storey.</summary>
    public enum WallFamily
    {
        Plaster = 0,
        UnevenBrick = 1,
    }

    /// <summary>
    /// How a settlement type looks: which wall families its storeys use, how tall its houses get and
    /// which house archetypes it builds. Towns are brick below and plaster above, up to three storeys;
    /// villages are plaster with timber, up to two; hamlets are single-storey cottages and workshops.
    /// </summary>
    public sealed class SettlementVisualProfile
    {
        public string Name { get; private set; }
        public WallFamily GroundFloor { get; private set; }
        public WallFamily UpperFloors { get; private set; }
        public int MaxStoreys { get; private set; }
        public IReadOnlyList<BuildingArchetype> HouseArchetypes { get; private set; }

        /// <summary>Round-headed doors and windows (towns) or flat ones.</summary>
        public bool RoundOpenings { get; private set; }

        public static SettlementVisualProfile For(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.Hamlet:
                    return new SettlementVisualProfile
                    {
                        Name = "Hamlet", GroundFloor = WallFamily.Plaster, UpperFloors = WallFamily.Plaster, MaxStoreys = 1, RoundOpenings = false,
                        HouseArchetypes = new[] { BuildingArchetype.Cottage, BuildingArchetype.Cottage, BuildingArchetype.Workshop },
                    };
                case SettlementType.Village:
                    return new SettlementVisualProfile
                    {
                        Name = "Village", GroundFloor = WallFamily.Plaster, UpperFloors = WallFamily.Plaster, MaxStoreys = 2, RoundOpenings = false,
                        HouseArchetypes = new[] { BuildingArchetype.Cottage, BuildingArchetype.Townhouse, BuildingArchetype.Workshop, BuildingArchetype.MerchantHouse },
                    };
                default:
                    return new SettlementVisualProfile
                    {
                        Name = "Town", GroundFloor = WallFamily.UnevenBrick, UpperFloors = WallFamily.Plaster, MaxStoreys = 3, RoundOpenings = true,
                        HouseArchetypes = new[] { BuildingArchetype.Townhouse, BuildingArchetype.MerchantHouse, BuildingArchetype.Townhouse, BuildingArchetype.Workshop, BuildingArchetype.Cottage },
                    };
            }
        }
    }

    /// <summary>
    /// One piece of a building: a kit module (by catalog ID) or a procedural piece (ID starting with
    /// '@'), in the building's local frame (metres; door side is local −Z; yaw in degrees about +Y,
    /// where yaw 90 turns a module's −Z face toward −X).
    /// </summary>
    public readonly struct ModulePlacement : IEquatable<ModulePlacement>
    {
        public ModulePlacement(string moduleId, float x, float y, float z, float yaw, float scaleX = 1f, float scaleY = 1f, float scaleZ = 1f)
        {
            ModuleId = moduleId;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
        }

        public string ModuleId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Yaw { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
        public float ScaleZ { get; }

        public bool IsProcedural => ModuleId != null && ModuleId.StartsWith("@", StringComparison.Ordinal);

        public bool Equals(ModulePlacement other) =>
            ModuleId == other.ModuleId && X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && Yaw.Equals(other.Yaw) &&
            ScaleX.Equals(other.ScaleX) && ScaleY.Equals(other.ScaleY) && ScaleZ.Equals(other.ScaleZ);

        public override bool Equals(object obj) => obj is ModulePlacement p && Equals(p);

        public override int GetHashCode() => unchecked((ModuleId?.Length ?? 0) * 397 ^ X.GetHashCode() ^ Z.GetHashCode() * 31 ^ Y.GetHashCode() * 17);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} ({1:F2}, {2:F2}, {3:F2}) yaw {4:F0}", ModuleId, X, Y, Z, Yaw);
    }

    /// <summary>The assembled form of one layout building: archetype, snapped footprint and its pieces.</summary>
    public sealed class BuildingPlan
    {
        public string Key { get; set; }
        public BuildingArchetype Archetype { get; set; }

        /// <summary>Footprint in modules (2 m each) along local X and Z.</summary>
        public int ModulesX { get; set; }
        public int ModulesZ { get; set; }
        public int Storeys { get; set; }

        public float Width => ModulesX * BuildingKitPlanner.ModuleSize;
        public float Depth => ModulesZ * BuildingKitPlanner.ModuleSize;
        public float WallHeight => Storeys * BuildingKitPlanner.StoreyHeight;

        /// <summary>Height of the ridge above the ground (for colliders and silhouettes).</summary>
        public float RidgeHeight { get; set; }

        /// <summary>True when the roof ridge runs along local Z (gable ends face front and back).</summary>
        public bool RidgeAlongZ { get; set; }

        /// <summary>The walkable opening in the front wall, if the building is entered (the tavern); else 0.</summary>
        public float DoorwayWidth { get; set; }

        /// <summary>Centre of that opening along the front (local X; the tavern's is 0).</summary>
        public float DoorwayX { get; set; }

        /// <summary>Height of the interior ceiling (a shop has one storey under its roof).</summary>
        public float InteriorHeight { get; set; }

        public List<ModulePlacement> Pieces { get; } = new List<ModulePlacement>();
    }

    /// <summary>
    /// Turns engine-free layout buildings into modular-kit assembly plans. Deterministic: the plan
    /// depends only on the building's layout data, the settlement type and a stable variation key
    /// (settlement ID and building key), never on the engine, time or iteration order. It does not
    /// change the layout: footprints are snapped down onto the kit's 2 m grid, so a plan never
    /// extends past its layout footprint by more than a quarter metre.
    /// </summary>
    public static class BuildingKitPlanner
    {
        public const float ModuleSize = 2f;
        public const float StoreyHeight = 3f;

        /// <summary>Every kit module ID a plan can contain (the art catalog must provide each).</summary>
        public static readonly IReadOnlyList<string> KitModuleIds = new[]
        {
            "Wall_Plaster_Straight", "Wall_Plaster_Door_Flat", "Wall_Plaster_Door_Round", "Wall_Plaster_Window_Wide_Flat",
            "Wall_Plaster_Window_Wide_Round", "Wall_Plaster_Window_Thin_Round", "Wall_Plaster_WoodGrid",
            "Wall_UnevenBrick_Straight", "Wall_UnevenBrick_Door_Flat", "Wall_UnevenBrick_Door_Round", "Wall_UnevenBrick_Window_Wide_Flat",
            "Wall_UnevenBrick_Window_Wide_Round", "Wall_UnevenBrick_Window_Thin_Round", "Wall_Arch", "Wall_BottomCover",
            "Corner_Exterior_Wood", "Corner_Exterior_Brick",
            "Door_1_Flat", "Door_1_Round", "Door_2_Round",
            "Window_Wide_Flat1", "Window_Wide_Round1", "Window_Thin_Round1",
            "WindowShutters_Wide_Flat_Open", "WindowShutters_Wide_Round_Open", "WindowShutters_Thin_Round_Open", "WindowShutters_Wide_Flat_Closed",
            "Roof_RoundTiles_4x4", "Roof_RoundTiles_4x6", "Roof_RoundTiles_4x8", "Roof_RoundTiles_6x6", "Roof_RoundTiles_6x8", "Roof_RoundTiles_6x10",
            "Roof_RoundTiles_6x12", "Roof_RoundTiles_6x14", "Roof_RoundTiles_8x8", "Roof_RoundTiles_8x10", "Roof_RoundTiles_8x12", "Roof_RoundTiles_8x14",
            "Roof_Front_Brick4", "Roof_Front_Brick6", "Roof_Front_Brick8", "Roof_Tower_RoundTiles",
            "Balcony_Simple_Straight", "Floor_WoodDark", "Floor_WoodLight",
            "Prop_Chimney", "Prop_Chimney2", "Prop_Crate", "Prop_Vine1", "Prop_Vine2", "Prop_Vine4",
        };

        /// <summary>Procedural pieces the presentation builds itself (no kit model).</summary>
        public static readonly IReadOnlyList<string> ProceduralIds = new[] { "@Awning", "@FlowerBox", "@Barrel", "@Sign", "@Lantern", "@Counter", "@Gallery", "@Ceiling" };

        private static readonly int[] RoofWidths = { 4, 6, 8 };

        private static readonly Dictionary<int, int[]> RoofLengths = new Dictionary<int, int[]>
        {
            [4] = new[] { 4, 6, 8 },
            [6] = new[] { 6, 8, 10, 12, 14 },
            [8] = new[] { 8, 10, 12, 14 },
        };

        /// <summary>Ridge height above the wall top of each kit roof width (measured from the kit).</summary>
        public static float RoofRise(int kitWidth) => kitWidth <= 4 ? 3.73f : kitWidth <= 6 ? 4.89f : 6f;

        public static BuildingPlan Plan(LayoutBuilding building, SettlementType type, string variationKey)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var profile = SettlementVisualProfile.For(type);
            var seed = SeedMixer.Combine(SeedMixer.HashString(variationKey ?? ""), building.Key ?? "");
            var rng = new PlanRandom(seed);

            switch (building.Kind)
            {
                case BuildingKind.Hall: return Tavern(building, profile);
                case BuildingKind.House when building.Fit != ShopFit.None: return Shop(building, profile, rng);
                case BuildingKind.Granary: return Granary(building, profile, rng);
                case BuildingKind.Landmark: return Tower(building, rng);
                default: return House(building, profile, rng);
            }
        }

        /// <summary>The archetype a house gets: one of the profile's house archetypes, chosen by the variation key.</summary>
        public static BuildingArchetype HouseArchetype(SettlementVisualProfile profile, ulong seed) =>
            profile.HouseArchetypes[(int)(SeedMixer.Mix(seed) % (ulong)profile.HouseArchetypes.Count)];

        // ----- Archetypes -----------------------------------------------------------------------------

        private static BuildingPlan House(LayoutBuilding b, SettlementVisualProfile profile, PlanRandom rng)
        {
            var archetype = profile.HouseArchetypes[rng.Int(profile.HouseArchetypes.Count)];
            var plan = Footprint(b, archetype);
            var storeys = archetype == BuildingArchetype.Cottage ? 1
                : archetype == BuildingArchetype.Workshop ? Math.Min(2, profile.MaxStoreys)
                : Math.Max(1, Math.Min(profile.MaxStoreys, b.Height >= 5.5f ? 2 + (profile.MaxStoreys > 2 && rng.Int(3) == 0 ? 1 : 0) : 1 + rng.Int(2)));
            plan.Storeys = Math.Min(storeys, profile.MaxStoreys);

            var round = profile.RoundOpenings;
            var doorSlot = DoorSlot(plan.ModulesX, rng);
            var shopSlot = archetype == BuildingArchetype.MerchantHouse ? (doorSlot == 0 ? 1 : doorSlot - 1) : -1;
            var shutterChance = archetype == BuildingArchetype.Cottage ? 2 : 3;

            for (var s = 0; s < plan.Storeys; s++)
            {
                var family = s == 0 ? (archetype == BuildingArchetype.Workshop ? WallFamily.UnevenBrick : profile.GroundFloor) : profile.UpperFloors;
                Ring(plan, s, family, (side, slot) =>
                {
                    if (side == Side.Front && s == 0 && slot == doorSlot) return Opening.Door;
                    if (side == Side.Front && s == 0 && slot == shopSlot) return Opening.ShopFront;
                    if (side == Side.Front && s == 1 && archetype == BuildingArchetype.Townhouse && slot == doorSlot) return Opening.BalconyDoor;
                    if (side == Side.Back && s == 0 && slot == 0 && plan.ModulesX > 2) return Opening.Door;
                    if (side == Side.Front || side == Side.Back) return (slot + s) % 2 == 0 || side == Side.Front ? Opening.Window : Opening.Plain;
                    return slot == (plan.ModulesZ - 1) / 2 ? Opening.Window : (s > 0 && family == WallFamily.Plaster ? Opening.Timber : Opening.Plain);
                }, round, rng, shutterChance);
            }

            Roof(plan, rng);
            Chimney(plan, archetype == BuildingArchetype.Workshop ? "Prop_Chimney2" : "Prop_Chimney", rng);

            // Dressing that belongs to the building (placed against its own walls).
            var front = -plan.Depth / 2f;
            var doorX = SlotX(plan.ModulesX, doorSlot);
            plan.Pieces.Add(new ModulePlacement("@Lantern", doorX + 0.95f, 2.45f, front - 0.25f, 0f));
            if (archetype == BuildingArchetype.MerchantHouse)
            {
                var shopX = SlotX(plan.ModulesX, shopSlot);
                plan.Pieces.Add(new ModulePlacement("@Awning", shopX, 2.5f, front, 0f));
                plan.Pieces.Add(new ModulePlacement("@Sign", doorX - 1.1f, 2.7f, front - 0.3f, 0f));
                plan.Pieces.Add(new ModulePlacement("Prop_Crate", shopX - 0.5f, 0f, front - 0.75f, rng.Range(-10f, 10f), 0.6f, 0.6f, 0.6f));
            }
            else if (archetype == BuildingArchetype.Workshop)
            {
                plan.Pieces.Add(new ModulePlacement("@Barrel", plan.Width / 2f - 0.5f, 0f, front - 0.6f, rng.Range(0f, 90f)));
                plan.Pieces.Add(new ModulePlacement("Prop_Crate", -plan.Width / 2f + 0.6f, 0f, front - 0.7f, rng.Range(-15f, 15f), 0.7f, 0.7f, 0.7f));
                plan.Pieces.Add(new ModulePlacement("Prop_Crate", -plan.Width / 2f + 0.6f, 0.74f, front - 0.7f, rng.Range(-25f, 25f), 0.55f, 0.55f, 0.55f));
            }

            if (archetype != BuildingArchetype.Workshop)
            {
                // Flower boxes under some ground-floor front windows.
                for (var slot = 0; slot < plan.ModulesX; slot++)
                {
                    if (slot == doorSlot || slot == shopSlot || rng.Int(3) == 0) continue;
                    plan.Pieces.Add(new ModulePlacement("@FlowerBox", SlotX(plan.ModulesX, slot), 0.72f, front - 0.22f, 0f));
                }
            }

            if (rng.Int(3) == 0)
            {
                var vine = new[] { "Prop_Vine1", "Prop_Vine2", "Prop_Vine4" }[rng.Int(3)];
                plan.Pieces.Add(new ModulePlacement(vine, SlotX(plan.ModulesX, plan.ModulesX - 1) + 0.6f, plan.WallHeight - 0.4f, front - 0.12f, 0f));
            }

            return plan;
        }

        /// <summary>
        /// The tavern (the enterable hall): two storeys of open interior, a centred open archway on the
        /// front, and a front row shifted by half a module so the archway sits on the centre line.
        /// </summary>
        private static BuildingPlan Tavern(LayoutBuilding b, SettlementVisualProfile profile)
        {
            var plan = Footprint(b, BuildingArchetype.Tavern);
            plan.Storeys = 2;
            plan.DoorwayWidth = 1.6f;
            plan.InteriorHeight = 2 * StoreyHeight;
            var family = profile.GroundFloor;
            for (var s = 0; s < plan.Storeys; s++)
            {
                var wall = s == 0 ? family : profile.UpperFloors;
                Ring(plan, s, wall, (side, slot) =>
                {
                    if (side == Side.Front) return Opening.Skip; // built below on the shifted grid
                    return s == 0 && side == Side.Back ? Opening.Plain : Opening.Window;
                }, profile.RoundOpenings, null, 0);

                // Front: odd number of slots centred on x = 0, plus half-width fillers at both ends.
                var slots = plan.ModulesX % 2 == 1 ? plan.ModulesX : plan.ModulesX - 1;
                var y = s * StoreyHeight;
                var z = -plan.Depth / 2f;
                for (var i = 0; i < slots; i++)
                {
                    var x = (i - (slots - 1) / 2f) * ModuleSize;
                    if (s == 0 && Math.Abs(x) < 0.01f)
                    {
                        plan.Pieces.Add(new ModulePlacement("Wall_Arch", x, y, z, 0f));
                        continue;
                    }

                    AddOpening(plan, Opening.Window, wall, x, y, z, 0f, profile.RoundOpenings, null, 0);
                }

                if (slots < plan.ModulesX)
                {
                    var edge = slots * ModuleSize / 2f + ModuleSize / 4f;
                    plan.Pieces.Add(new ModulePlacement(WallId(wall, "Straight"), -edge, y, z, 0f, 0.5f));
                    plan.Pieces.Add(new ModulePlacement(WallId(wall, "Straight"), edge, y, z, 0f, 0.5f));
                }
            }

            // Plank floor over the whole interior.
            for (var ix = 0; ix < plan.ModulesX; ix++)
            for (var iz = 0; iz < plan.ModulesZ; iz++)
                plan.Pieces.Add(new ModulePlacement("Floor_WoodDark", SlotX(plan.ModulesX, ix), 0.02f, SlotX(plan.ModulesZ, iz), 0f));

            Roof(plan, null);
            Chimney(plan, "Prop_Chimney", null);
            plan.Pieces.Add(new ModulePlacement("@Sign", 1.7f, 3.4f, -plan.Depth / 2f - 0.3f, 0f));
            plan.Pieces.Add(new ModulePlacement("@Lantern", -1.25f, 2.6f, -plan.Depth / 2f - 0.25f, 0f));
            plan.Pieces.Add(new ModulePlacement("@Lantern", 1.25f, 2.6f, -plan.Depth / 2f - 0.25f, 0f));
            return plan;
        }

        /// <summary>Where a shop's open doorway sits along its front (its door slot, chosen without randomness).</summary>
        public static float ShopDoorX(LayoutBuilding b)
        {
            var modulesX = Snap(b.Width);
            return SlotX(modulesX, (modulesX - 1) / 2);
        }

        /// <summary>
        /// An enterable shop (Phase 5): one storey with an open archway at the door slot, windows on the
        /// front, a plank floor, a ceiling under the roof, a sign and lanterns. A smithy is brick with a
        /// workshop chimney; a merchant's has an awning. The counter, shelves and wares are not part of
        /// the plan: they show the shop's live stock and are built by the presentation.
        /// </summary>
        private static BuildingPlan Shop(LayoutBuilding b, SettlementVisualProfile profile, PlanRandom rng)
        {
            var smithy = b.Fit == ShopFit.Smithy;
            var plan = Footprint(b, smithy ? BuildingArchetype.Workshop : BuildingArchetype.MerchantHouse);
            plan.Storeys = 1;
            plan.DoorwayWidth = 1.6f;
            plan.InteriorHeight = StoreyHeight;
            var doorSlot = (plan.ModulesX - 1) / 2;
            plan.DoorwayX = SlotX(plan.ModulesX, doorSlot);
            var family = smithy ? WallFamily.UnevenBrick : profile.GroundFloor;
            var mid = (plan.ModulesZ - 1) / 2;
            Ring(plan, 0, family, (side, slot) =>
            {
                if (side == Side.Front) return slot == doorSlot ? Opening.Archway : Opening.Window;
                if (side == Side.Back) return Opening.Plain;
                return slot == mid ? Opening.Window : Opening.Plain;
            }, profile.RoundOpenings, null, 0);

            for (var ix = 0; ix < plan.ModulesX; ix++)
            for (var iz = 0; iz < plan.ModulesZ; iz++)
                plan.Pieces.Add(new ModulePlacement(smithy ? "Floor_WoodDark" : "Floor_WoodLight", SlotX(plan.ModulesX, ix), 0.02f, SlotX(plan.ModulesZ, iz), 0f));
            plan.Pieces.Add(new ModulePlacement("@Ceiling", 0f, plan.InteriorHeight - 0.06f, 0f, 0f, plan.Width - 0.3f, 0.12f, plan.Depth - 0.3f));

            Roof(plan, rng);
            Chimney(plan, smithy ? "Prop_Chimney2" : "Prop_Chimney", rng);
            var front = -plan.Depth / 2f;
            plan.Pieces.Add(new ModulePlacement("@Sign", plan.DoorwayX - 1.15f, 2.55f, front - 0.3f, 0f));
            plan.Pieces.Add(new ModulePlacement("@Lantern", plan.DoorwayX + 1.0f, 2.45f, front - 0.25f, 0f));
            if (!smithy && plan.ModulesX > 2)
            {
                var awningSlot = doorSlot == 0 ? 1 : doorSlot - 1;
                plan.Pieces.Add(new ModulePlacement("@Awning", SlotX(plan.ModulesX, awningSlot), 2.5f, front, 0f));
            }
            else
            {
                plan.Pieces.Add(new ModulePlacement("@Barrel", plan.Width / 2f - 0.5f, 0f, front - 0.6f, 0f));
            }

            return plan;
        }

        private static BuildingPlan Granary(LayoutBuilding b, SettlementVisualProfile profile, PlanRandom rng)
        {
            var plan = Footprint(b, BuildingArchetype.Granary);
            plan.Storeys = 1;
            var doorSlot = DoorSlot(plan.ModulesX, rng);
            Ring(plan, 0, WallFamily.UnevenBrick, (side, slot) =>
                side == Side.Front && slot == doorSlot ? Opening.BarnDoor : (side == Side.Left || side == Side.Right) && slot == 1 ? Opening.SmallWindow : Opening.Plain,
                true, rng, 0);
            Roof(plan, rng);
            return plan;
        }

        /// <summary>The landmark: a brick tower of 2×2 modules with narrow windows and a conical tiled roof.</summary>
        private static BuildingPlan Tower(LayoutBuilding b, PlanRandom rng)
        {
            var plan = new BuildingPlan { Key = b.Key, Archetype = BuildingArchetype.Tower, ModulesX = 2, ModulesZ = 2 };
            plan.Storeys = Math.Max(3, (int)Math.Round(b.Height / StoreyHeight, MidpointRounding.AwayFromZero));
            for (var s = 0; s < plan.Storeys; s++)
            {
                var level = s;
                Ring(plan, s, WallFamily.UnevenBrick, (side, slot) =>
                    side == Side.Front && level == 0 && slot == 0 ? Opening.Door
                    : level > 0 && slot == (level + (int)side) % 2 ? Opening.SmallWindow : Opening.Plain, true, rng, 0);
            }

            plan.Pieces.Add(new ModulePlacement("Roof_Tower_RoundTiles", 0f, plan.WallHeight, 0f, 0f));
            plan.RidgeHeight = plan.WallHeight + 6.8f;
            return plan;
        }

        /// <summary>
        /// The gate in a walled settlement's south wall: two brick towers flanking the opening, joined
        /// by a timber gallery. Positions are in the environment frame (plaza centre origin).
        /// </summary>
        public static BuildingPlan Gatehouse(float gateWidth, float wallLine)
        {
            var plan = new BuildingPlan { Key = "gatehouse", Archetype = BuildingArchetype.Gatehouse, ModulesX = 2, ModulesZ = 2, Storeys = 3 };
            foreach (var sign in new[] { -1f, 1f })
            {
                var cx = sign * (gateWidth / 2f + ModuleSize);
                for (var s = 0; s < plan.Storeys; s++)
                {
                    var y = s * StoreyHeight;
                    foreach (var side in new[] { Side.Front, Side.Back, Side.Left, Side.Right })
                    for (var slot = 0; slot < 2; slot++)
                    {
                        var (x, z, yaw) = WallTransform(side, slot, 2, 2);
                        var id = s == 2 && slot == 0 && side == Side.Front ? WallId(WallFamily.UnevenBrick, "Window_Thin_Round") : WallId(WallFamily.UnevenBrick, "Straight");
                        plan.Pieces.Add(new ModulePlacement(id, cx + x, y, wallLine + z, yaw));
                        if (id.Contains("Window")) plan.Pieces.Add(new ModulePlacement("Window_Thin_Round1", cx + x, y, wallLine + z, yaw));
                    }

                    foreach (var (x, z, yaw) in Corners(2, 2)) plan.Pieces.Add(new ModulePlacement("Corner_Exterior_Brick", cx + x, y, wallLine + z, yaw));
                }

                plan.Pieces.Add(new ModulePlacement("Roof_Tower_RoundTiles", cx, plan.WallHeight, wallLine, 0f));
            }

            plan.Pieces.Add(new ModulePlacement("@Gallery", 0f, 5.2f, wallLine, 0f, gateWidth + 0.2f));
            plan.RidgeHeight = plan.WallHeight + 6.8f;
            return plan;
        }

        // ----- Walls ------------------------------------------------------------------------------------

        private enum Side
        {
            Front = 0,
            Right = 1,
            Back = 2,
            Left = 3,
        }

        private enum Opening
        {
            Plain,
            Timber,
            Window,
            SmallWindow,
            Door,
            BarnDoor,
            BalconyDoor,
            ShopFront,
            Skip,
            Archway,
        }

        private static BuildingPlan Footprint(LayoutBuilding b, BuildingArchetype archetype) => new BuildingPlan
        {
            Key = b.Key,
            Archetype = archetype,
            ModulesX = Snap(b.Width),
            ModulesZ = Snap(b.Depth),
        };

        /// <summary>Modules along a side: the footprint rounded down to the 2 m grid (within a quarter metre), at least 2.</summary>
        public static int Snap(float metres) => Math.Max(2, (int)Math.Floor((metres + 0.5f) / ModuleSize));

        private static int DoorSlot(int modulesX, PlanRandom rng) => rng == null ? (modulesX - 1) / 2 : Math.Min(modulesX - 1, (modulesX - 1) / 2 + rng.Int(2));

        /// <summary>Centre of slot <paramref name="i"/> of <paramref name="count"/> along a side centred on 0.</summary>
        public static float SlotX(int count, int i) => (i - (count - 1) / 2f) * ModuleSize;

        private static (float x, float z, float yaw) WallTransform(Side side, int slot, int modulesX, int modulesZ)
        {
            var w = modulesX * ModuleSize / 2f;
            var d = modulesZ * ModuleSize / 2f;
            switch (side)
            {
                case Side.Front: return (SlotX(modulesX, slot), -d, 0f);
                case Side.Back: return (-SlotX(modulesX, slot), d, 180f);
                case Side.Left: return (-w, -SlotX(modulesZ, slot), 90f);
                default: return (w, SlotX(modulesZ, slot), 270f);
            }
        }

        private static IEnumerable<(float x, float z, float yaw)> Corners(int modulesX, int modulesZ)
        {
            var w = modulesX * ModuleSize / 2f;
            var d = modulesZ * ModuleSize / 2f;
            yield return (-w, -d, 0f);
            yield return (w, -d, 270f);
            yield return (w, d, 180f);
            yield return (-w, d, 90f);
        }

        private static void Ring(BuildingPlan plan, int storey, WallFamily family, Func<Side, int, Opening> choose, bool round, PlanRandom rng, int shutterChance)
        {
            var y = storey * StoreyHeight;
            foreach (var side in new[] { Side.Front, Side.Right, Side.Back, Side.Left })
            {
                var count = side == Side.Front || side == Side.Back ? plan.ModulesX : plan.ModulesZ;
                for (var slot = 0; slot < count; slot++)
                {
                    var opening = choose(side, slot);
                    if (opening == Opening.Skip) continue;
                    var (x, z, yaw) = WallTransform(side, slot, plan.ModulesX, plan.ModulesZ);
                    AddOpening(plan, opening, family, x, y, z, yaw, round, rng, shutterChance);
                    if (storey > 0) plan.Pieces.Add(new ModulePlacement("Wall_BottomCover", x, y, z, yaw));
                }
            }

            var corner = family == WallFamily.UnevenBrick ? "Corner_Exterior_Brick" : "Corner_Exterior_Wood";
            foreach (var (x, z, yaw) in Corners(plan.ModulesX, plan.ModulesZ)) plan.Pieces.Add(new ModulePlacement(corner, x, y, z, yaw));
        }

        private static void AddOpening(BuildingPlan plan, Opening opening, WallFamily family, float x, float y, float z, float yaw, bool round, PlanRandom rng, int shutterChance)
        {
            var shape = round ? "Round" : "Flat";
            switch (opening)
            {
                case Opening.Door:
                case Opening.BalconyDoor:
                    plan.Pieces.Add(new ModulePlacement(WallId(family, "Door_" + shape), x, y, z, yaw));
                    plan.Pieces.Add(Offset("Door_1_" + shape, x, y, z, yaw, -0.53f, 0f));
                    if (opening == Opening.BalconyDoor)
                    {
                        plan.Pieces.Add(Offset("Balcony_Simple_Straight", x, y, z, yaw, 0f, 0f));
                        plan.Pieces.Add(Offset("Floor_WoodLight", x, y + 0.02f, z, yaw, 0f, -0.55f, 1f, 1f, 0.55f));
                    }

                    break;
                case Opening.BarnDoor:
                    plan.Pieces.Add(new ModulePlacement(WallId(family, "Door_Round"), x, y, z, yaw));
                    plan.Pieces.Add(Offset("Door_2_Round", x, y, z, yaw, -0.53f, 0f));
                    break;
                case Opening.Archway:
                    plan.Pieces.Add(new ModulePlacement("Wall_Arch", x, y, z, yaw));
                    break;
                case Opening.ShopFront:
                    plan.Pieces.Add(new ModulePlacement("Wall_Arch", x, y, z, yaw));
                    plan.Pieces.Add(Offset("@Counter", x, y, z, yaw, 0f, -0.05f));
                    break;
                case Opening.Window:
                    plan.Pieces.Add(new ModulePlacement(WallId(family, "Window_Wide_" + shape), x, y, z, yaw));
                    plan.Pieces.Add(new ModulePlacement("Window_Wide_" + shape + "1", x, y, z, yaw));
                    if (shutterChance > 0 && rng != null && rng.Int(shutterChance) == 0)
                        plan.Pieces.Add(new ModulePlacement(rng.Int(4) == 0 && !round ? "WindowShutters_Wide_Flat_Closed" : "WindowShutters_Wide_" + shape + "_Open", x, y, z, yaw));
                    break;
                case Opening.SmallWindow:
                    plan.Pieces.Add(new ModulePlacement(WallId(family, "Window_Thin_Round"), x, y, z, yaw));
                    plan.Pieces.Add(new ModulePlacement("Window_Thin_Round1", x, y, z, yaw));
                    break;
                case Opening.Timber:
                    plan.Pieces.Add(new ModulePlacement(family == WallFamily.Plaster ? "Wall_Plaster_WoodGrid" : WallId(family, "Straight"), x, y, z, yaw));
                    break;
                default:
                    plan.Pieces.Add(new ModulePlacement(WallId(family, "Straight"), x, y, z, yaw));
                    break;
            }
        }

        /// <summary>A piece offset within a wall slot's own frame (along the wall, and outward along −Z).</summary>
        private static ModulePlacement Offset(string id, float x, float y, float z, float yaw, float along, float outward,
            float sx = 1f, float sy = 1f, float sz = 1f)
        {
            var r = yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            // Local (along, 0, outward) rotated by yaw: x' = a cos + o sin, z' = −a sin + o cos.
            return new ModulePlacement(id, x + along * cos + outward * sin, y, z - along * sin + outward * cos, yaw, sx, sy, sz);
        }

        private static string WallId(WallFamily family, string suffix) => (family == WallFamily.UnevenBrick ? "Wall_UnevenBrick_" : "Wall_Plaster_") + suffix;

        // ----- Roofs ------------------------------------------------------------------------------------

        /// <summary>
        /// A gabled tiled roof on the wall top, ridge along the longer side: the nearest kit roof,
        /// scaled to the exact footprint, with gable ends.
        /// </summary>
        private static void Roof(BuildingPlan plan, PlanRandom rng)
        {
            var ridgeAlongZ = plan.ModulesZ > plan.ModulesX || plan.ModulesZ == plan.ModulesX && (rng == null || rng.Int(2) == 0);
            var span = (int)((ridgeAlongZ ? plan.ModulesX : plan.ModulesZ) * ModuleSize);
            var length = (int)((ridgeAlongZ ? plan.ModulesZ : plan.ModulesX) * ModuleSize);
            var kitWidth = Nearest(RoofWidths, span);
            var kitLength = Nearest(RoofLengths[kitWidth], length);
            var sx = span / (float)kitWidth;
            var sz = length / (float)kitLength;
            plan.RidgeAlongZ = ridgeAlongZ;
            var yaw = ridgeAlongZ ? 0f : 90f;
            var top = plan.WallHeight;
            plan.Pieces.Add(new ModulePlacement($"Roof_RoundTiles_{kitWidth}x{kitLength}", 0f, top, 0f, yaw, sx, 1f, sz));
            var gable = $"Roof_Front_Brick{kitWidth}";
            var half = length / 2f;
            if (ridgeAlongZ)
            {
                plan.Pieces.Add(new ModulePlacement(gable, 0f, top, -half, 0f, sx));
                plan.Pieces.Add(new ModulePlacement(gable, 0f, top, half, 180f, sx));
            }
            else
            {
                plan.Pieces.Add(new ModulePlacement(gable, -half, top, 0f, 90f, sx));
                plan.Pieces.Add(new ModulePlacement(gable, half, top, 0f, 270f, sx));
            }

            plan.RidgeHeight = top + RoofRise(kitWidth);
        }

        private static void Chimney(BuildingPlan plan, string id, PlanRandom rng)
        {
            var ridgeAlongZ = plan.RidgeAlongZ;
            var along = (ridgeAlongZ ? plan.Depth : plan.Width) / 2f - 1.2f;
            var sign = rng == null || rng.Int(2) == 0 ? 1f : -1f;
            var rise = plan.RidgeHeight - plan.WallHeight;
            var x = ridgeAlongZ ? 0.4f : sign * along;
            var z = ridgeAlongZ ? sign * along : 0.4f;
            plan.Pieces.Add(new ModulePlacement(id, x, plan.WallHeight + rise * 0.55f, z, 0f));
        }

        private static int Nearest(int[] options, int value)
        {
            var best = options[0];
            foreach (var o in options) if (Math.Abs(o - value) < Math.Abs(best - value)) best = o;
            return best;
        }

        /// <summary>Small deterministic generator for plan variation (xorshift64*, never shared).</summary>
        private sealed class PlanRandom
        {
            private ulong _state;

            public PlanRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

            private ulong Next()
            {
                _state ^= _state >> 12;
                _state ^= _state << 25;
                _state ^= _state >> 27;
                return unchecked(_state * 2685821657736338717UL);
            }

            public int Int(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(Next() % (ulong)maxExclusive);

            public float Range(float min, float max) => min + (max - min) * (float)((Next() >> 11) * (1.0 / 9007199254740992.0));
        }
    }
}
