using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Economy
{
    /// <summary>
    /// Stable key for a resource definition (e.g. "food"). This is a content key, not a
    /// display name: localized names are resolved by the presentation layer.
    /// Allowed form: lowercase letter followed by lowercase letters, digits, '_' or '.'.
    /// </summary>
    public readonly struct ResourceTypeId : IEquatable<ResourceTypeId>, IComparable<ResourceTypeId>
    {
        public const int MaxLength = 64;

        private readonly string _value;

        public ResourceTypeId(string value)
        {
            if (!IsValidKey(value))
            {
                throw new ArgumentException($"Invalid resource type key '{value}'.", nameof(value));
            }

            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public static bool IsValidKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxLength) return false;
            if (value[0] < 'a' || value[0] > 'z') return false;
            for (var i = 1; i < value.Length; i++)
            {
                var c = value[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.';
                if (!ok) return false;
            }

            return true;
        }

        public bool Equals(ResourceTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ResourceTypeId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public int CompareTo(ResourceTypeId other) => string.CompareOrdinal(Value, other.Value);

        public override string ToString() => Value;

        public static bool operator ==(ResourceTypeId a, ResourceTypeId b) => a.Equals(b);
        public static bool operator !=(ResourceTypeId a, ResourceTypeId b) => !a.Equals(b);
    }

    /// <summary>The resources simulated in Phase 2. Keys are persisted; never rename one without a save migration.</summary>
    public static class ResourceTypes
    {
        public static readonly ResourceTypeId Food = new ResourceTypeId("food");
        public static readonly ResourceTypeId Timber = new ResourceTypeId("timber");
        public static readonly ResourceTypeId Stone = new ResourceTypeId("stone");
        public static readonly ResourceTypeId Iron = new ResourceTypeId("iron");
        public static readonly ResourceTypeId Coin = new ResourceTypeId("coin");

        /// <summary>Every simulated resource, in display order.</summary>
        public static IReadOnlyList<ResourceTypeId> All { get; } = new[] { Food, Timber, Stone, Iron, Coin };

        /// <summary>Physical goods that can be shipped along trade routes (coin is payment, not cargo).</summary>
        public static IReadOnlyList<ResourceTypeId> Goods { get; } = new[] { Food, Timber, Stone, Iron };

        public static bool IsKnown(ResourceTypeId id)
        {
            foreach (var r in All)
            {
                if (r == id) return true;
            }

            return false;
        }
    }
}
