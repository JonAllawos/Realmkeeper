using System;

namespace Realmkeeper.Domain.World
{
    /// <summary>Integer cell coordinate on the region grid (X = east, Z = north).</summary>
    public readonly struct MapCoord : IEquatable<MapCoord>
    {
        public int X { get; }
        public int Z { get; }

        public MapCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>Squared Euclidean distance in cells (exact, integer).</summary>
        public int DistanceSquared(MapCoord other)
        {
            var dx = X - other.X;
            var dz = Z - other.Z;
            return dx * dx + dz * dz;
        }

        public int ChebyshevDistance(MapCoord other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));

        public bool Equals(MapCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is MapCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X * 73856093 ^ Z * 19349663);
        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(MapCoord a, MapCoord b) => a.Equals(b);
        public static bool operator !=(MapCoord a, MapCoord b) => !a.Equals(b);
    }
}
