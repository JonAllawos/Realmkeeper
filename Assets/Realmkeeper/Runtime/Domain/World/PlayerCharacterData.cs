using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Where the player character is in the world. Persisted by name.</summary>
    public enum CharacterLocationKind
    {
        /// <summary>Not inside any settlement: the user is on the strategic map.</summary>
        WorldMap = 0,

        /// <summary>Inside a settlement's local environment.</summary>
        Settlement = 1,

        /// <summary>Phase 13: inside an explorable place (a mine, a crypt, a building's interior).</summary>
        Place = 2,

        /// <summary>Phase 23 (ADR-0110): out in the walkable country of a region (<see cref="PlayerCharacterData.Pose"/> in region metres).</summary>
        Overland = 3,
    }

    /// <summary>Exploration camera perspective. Persisted by name.</summary>
    public enum CameraPerspective
    {
        ThirdPerson = 0,
        FirstPerson = 1,
    }

    /// <summary>Position (metres, local-environment space) and facing (degrees) of the character.</summary>
    public readonly struct LocalPose : System.IEquatable<LocalPose>
    {
        public LocalPose(float x, float y, float z, float yaw)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Yaw { get; }

        public bool IsFinite =>
            !float.IsNaN(X) && !float.IsInfinity(X) && !float.IsNaN(Y) && !float.IsInfinity(Y) &&
            !float.IsNaN(Z) && !float.IsInfinity(Z) && !float.IsNaN(Yaw) && !float.IsInfinity(Yaw);

        public bool Equals(LocalPose other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && Yaw.Equals(other.Yaw);
        public override bool Equals(object obj) => obj is LocalPose other && Equals(other);
        public override int GetHashCode() => unchecked(((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode()) * 397 ^ Yaw.GetHashCode());
        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##}) yaw {Yaw:0.#}";
    }

    /// <summary>
    /// The player's character: authoritative world state, saved with the world (ADR-0009
    /// clarification 1). Both the strategic map and exploration read it; only commands change it.
    /// The character is one of the world's people, never an extra copy of a settlement or NPC.
    /// </summary>
    public sealed class PlayerCharacterData
    {
        public const int MaxNameLength = 64;

        public CharacterId CharacterId { get; set; }
        public string Name { get; set; }
        public CharacterLocationKind LocationKind { get; set; }

        /// <summary>Settlement the character is in (empty on the world map).</summary>
        public SettlementId SettlementId { get; set; }

        /// <summary>Region of that settlement (empty on the world map).</summary>
        public RegionId RegionId { get; set; }

        /// <summary>Position inside the settlement's local environment.</summary>
        public LocalPose Pose { get; set; }

        public CameraPerspective Perspective { get; set; }

        /// <summary>The settlement most recently visited, so the map can focus on it when the character leaves.</summary>
        public SettlementId LastSettlementId { get; set; }

        public bool IsInSettlement => LocationKind == CharacterLocationKind.Settlement;

        /// <summary>Phase 13 (ADR-0062): the place the character is in (with <see cref="CharacterLocationKind.Place"/>), and the floor; <see cref="Pose"/> is then in the place's own space.</summary>
        public string PlaceId { get; set; }
        public int PlaceFloor { get; set; }

        /// <summary>The room the character was last recorded in (rooms are entered by command, so the world always knows).</summary>
        public string PlaceRoom { get; set; }

        public bool IsInPlace => LocationKind == CharacterLocationKind.Place;

        /// <summary>Phase 23 (ADR-0110): walking the country of <see cref="RegionId"/> (in <see cref="RealmId"/>).</summary>
        public bool IsOverland => LocationKind == CharacterLocationKind.Overland;

        /// <summary>Phase 23 (ADR-0111): the realm the character is in (empty: the surface).</summary>
        public string RealmId { get; set; } = string.Empty;

        /// <summary>Phase 23: the settlement or place was entered from the country, so leaving it returns there (not to the map).</summary>
        public bool ReturnToOverland { get; set; }

        /// <summary>Phase 24 (ADR-0113): the destination the compass points to ("settlement:{id}", "place:{id}", "landmark:{id}", "gateway:{id}", "journey"), or null.</summary>
        public string Waypoint { get; set; }

        // ----- Phase 5: the player as a person with looks, belongings and standing -----------------

        public string Species { get; set; } = "Human";
        public int Age { get; set; } = 28;
        public string Background { get; set; } = "Wanderer";
        public string Description { get; set; } = string.Empty;
        public PlayerAppearanceData Appearance { get; set; } = new PlayerAppearanceData();

        /// <summary>Coin carried (never negative).</summary>
        public int Purse { get; set; }

        /// <summary>0–100: how well fed the player is. Falls with time; eating restores it. Below 25 the player is slowed.</summary>
        public int Fed { get; set; } = 80;

        /// <summary>Carried items (not worn).</summary>
        public List<ItemStackData> Inventory { get; set; } = new List<ItemStackData>();

        /// <summary>Worn and held items, one per slot.</summary>
        public Dictionary<EquipmentSlot, ItemStackData> Equipment { get; set; } = new Dictionary<EquipmentSlot, ItemStackData>();

        /// <summary>True once the Phase 5 belongings were set up (new worlds, or once on migration).</summary>
        public bool IsOutfitted { get; set; }

        /// <summary>The RPG character (Phase 6): species, background, class, scores, choices, hit points, resources.</summary>
        public Realmkeeper.Domain.Rpg.CharacterSheetData Rpg { get; set; } = new Realmkeeper.Domain.Rpg.CharacterSheetData();

        /// <summary>Phase 12: faction standing, settlement reputation, notoriety, the reputation ledger, discoveries, rumours and decisions.</summary>
        public Realmkeeper.Domain.Quests.PlayerSocialData Social { get; set; } = new Realmkeeper.Domain.Quests.PlayerSocialData();

        /// <summary>Phase 25 (ADR-0116): the creature kinds the player has met in fights, for the bestiary.</summary>
        public List<KnownCreatureData> KnownCreatures { get; set; } = new List<KnownCreatureData>();

        public ItemStackData Equipped(EquipmentSlot slot) => Equipment.TryGetValue(slot, out var s) ? s : null;

        public PlayerCharacterData Clone()
        {
            var c = new PlayerCharacterData
            {
            CharacterId = CharacterId,
            Name = Name,
            LocationKind = LocationKind,
            SettlementId = SettlementId,
            RegionId = RegionId,
            Pose = Pose,
            Perspective = Perspective,
            LastSettlementId = LastSettlementId,
            PlaceId = PlaceId,
            PlaceFloor = PlaceFloor,
            PlaceRoom = PlaceRoom,
            RealmId = RealmId, // Phase 23
            ReturnToOverland = ReturnToOverland,
            Waypoint = Waypoint, // Phase 24
                Species = Species,
                Age = Age,
                Background = Background,
                Description = Description,
                Appearance = Appearance?.Clone() ?? new PlayerAppearanceData(),
                Purse = Purse,
                Fed = Fed,
                IsOutfitted = IsOutfitted,
                Rpg = Rpg?.Clone() ?? new Realmkeeper.Domain.Rpg.CharacterSheetData(),
                Social = Social?.Clone() ?? new Realmkeeper.Domain.Quests.PlayerSocialData(),
            };
            foreach (var s in Inventory) c.Inventory.Add(s.Clone());
            foreach (var pair in Equipment) c.Equipment[pair.Key] = pair.Value.Clone();
            if (KnownCreatures != null) foreach (var k in KnownCreatures) c.KnownCreatures.Add(k.Clone()); // Phase 25
            return c;
        }
    }
}
