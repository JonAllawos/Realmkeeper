using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    public enum BuildingKind
    {
        House = 0,

        /// <summary>The enterable hall (inn or meeting hall) on the plaza, with an interior.</summary>
        Hall = 1,

        Granary = 2,

        /// <summary>The tall landmark (keep tower, chapel tower or watchtower).</summary>
        Landmark = 3,
    }

    public enum PropKind
    {
        Well = 0,
        MarketStall = 1,
        Barrel = 2,
        Crate = 3,
        LampPost = 4,
        Tree = 5,
        Table = 6,
        Bench = 7,
        Hearth = 8,
    }

    /// <summary>What an environmental interaction point does. Persisted nowhere: derived from the layout.</summary>
    public enum InteractableKind
    {
        /// <summary>Shows the settlement's food and granary state. Informational: changes nothing.</summary>
        GranaryLedger = 0,

        /// <summary>Posts a labour notice. State-changing: dispatches PostLabourNoticeCommand.</summary>
        LabourNoticeBoard = 1,
    }

    /// <summary>A position in the local environment: metres, origin at the plaza centre, +Z north.</summary>
    public readonly struct LocalPoint
    {
        public LocalPoint(float x, float z) { X = x; Z = z; }
        public float X { get; }
        public float Z { get; }
    }

    public sealed class LayoutBuilding
    {
        public string Key { get; set; }
        public BuildingKind Kind { get; set; }
        public LocalPoint Center { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public float Height { get; set; }

        /// <summary>
        /// Facing in degrees: the door faces direction (sin yaw, −cos yaw). 0 = south (−Z),
        /// 90 = east (+X), 180 = north (+Z), 270 = west (−X). Doors face the street or plaza.
        /// </summary>
        public float Yaw { get; set; }

        /// <summary>Palette index for walls and roof (presentation decides the colours).</summary>
        public int Style { get; set; }

        public bool IsEnterable => Kind == BuildingKind.Hall;
    }

    public sealed class LayoutProp
    {
        public PropKind Kind { get; set; }
        public LocalPoint Position { get; set; }
        public float Yaw { get; set; }
    }

    public sealed class LayoutStreet
    {
        public LocalPoint From { get; set; }
        public LocalPoint To { get; set; }
        public float Width { get; set; }
    }

    public sealed class LayoutInteractable
    {
        public InteractableKind Kind { get; set; }
        public LocalPoint Position { get; set; }
        public float Yaw { get; set; }
    }

    /// <summary>Where a named resident stands. Keyed by the resident's persistent <see cref="NpcId"/>.</summary>
    public sealed class LayoutResidentSpot
    {
        public NpcId NpcId { get; set; }
        public NpcRole Role { get; set; }
        public LocalPoint Position { get; set; }

        /// <summary>Radius of the resident's idle wander, in metres.</summary>
        public float WanderRadius { get; set; }
    }

    /// <summary>
    /// The engine-free description of one settlement's explorable environment. It is derived from
    /// world data (seed, settlement ID, type, residents) and never saved. It refers to the
    /// authoritative settlement and region by ID (ADR-0009 point 3; A2.4) and holds no copy of
    /// their state.
    /// </summary>
    public sealed class LocalEnvironmentLayout
    {
        public SettlementId SettlementId { get; set; }
        public RegionId RegionId { get; set; }

        /// <summary>Half the side length of the walkable square, in metres.</summary>
        /// <summary>The settlement's type, which selects its visual profile (building styles and sizes).</summary>
        public SettlementType Type { get; set; }

        public float HalfExtent { get; set; }

        public float PlazaRadius { get; set; }
        public bool HasWalls { get; set; }

        /// <summary>Where the character arrives through the south gate, facing north.</summary>
        public LocalPose ArrivalPose { get; set; }

        public List<LayoutStreet> Streets { get; } = new List<LayoutStreet>();
        public List<LayoutBuilding> Buildings { get; } = new List<LayoutBuilding>();
        public List<LayoutProp> Props { get; } = new List<LayoutProp>();
        public List<LayoutInteractable> Interactables { get; } = new List<LayoutInteractable>();
        public List<LayoutResidentSpot> Residents { get; } = new List<LayoutResidentSpot>();

        /// <summary>Stable key of the environment (one per settlement).</summary>
        public string EnvironmentKey => "settlement:" + SettlementId;

        /// <summary>True when a point is inside the walkable area (with a small margin for walls).</summary>
        public bool Contains(float x, float z) => Math.Abs(x) <= HalfExtent && Math.Abs(z) <= HalfExtent;

        public LayoutInteractable FindInteractable(InteractableKind kind)
        {
            foreach (var i in Interactables)
            {
                if (i.Kind == kind) return i;
            }

            return null;
        }
    }
}
