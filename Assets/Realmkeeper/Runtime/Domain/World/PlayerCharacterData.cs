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

        public PlayerCharacterData Clone() => new PlayerCharacterData
        {
            CharacterId = CharacterId,
            Name = Name,
            LocationKind = LocationKind,
            SettlementId = SettlementId,
            RegionId = RegionId,
            Pose = Pose,
            Perspective = Perspective,
            LastSettlementId = LastSettlementId,
        };
    }
}
