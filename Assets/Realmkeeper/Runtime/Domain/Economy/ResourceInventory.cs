using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Economy
{
    /// <summary>A quantity of one resource type.</summary>
    public readonly struct ResourceStack : IEquatable<ResourceStack>
    {
        public ResourceTypeId ResourceTypeId { get; }
        public int Quantity { get; }

        public ResourceStack(ResourceTypeId resourceTypeId, int quantity)
        {
            ResourceTypeId = resourceTypeId;
            Quantity = quantity;
        }

        public bool Equals(ResourceStack other) => ResourceTypeId == other.ResourceTypeId && Quantity == other.Quantity;
        public override bool Equals(object obj) => obj is ResourceStack other && Equals(other);
        public override int GetHashCode() => ResourceTypeId.GetHashCode() * 31 + Quantity;
        public override string ToString() => $"{ResourceTypeId} x{Quantity}";
    }

    /// <summary>
    /// Settlement resource store. Invariants: no empty keys, no negative quantities,
    /// no int overflow. Zero quantities are removed. Enumeration is ordinally sorted by
    /// key, which keeps simulation iteration and serialization deterministic.
    /// </summary>
    public sealed class ResourceInventory
    {
        private readonly SortedDictionary<ResourceTypeId, int> _stacks = new SortedDictionary<ResourceTypeId, int>();

        public int Count => _stacks.Count;

        public IEnumerable<ResourceStack> Stacks
        {
            get
            {
                foreach (var pair in _stacks)
                {
                    yield return new ResourceStack(pair.Key, pair.Value);
                }
            }
        }

        public int Get(ResourceTypeId type) => _stacks.TryGetValue(type, out var q) ? q : 0;

        public void Set(ResourceTypeId type, int quantity)
        {
            if (type.IsEmpty) throw new ArgumentException("Resource type is required.", nameof(type));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Resource quantity cannot be negative.");
            if (quantity == 0) _stacks.Remove(type);
            else _stacks[type] = quantity;
        }

        public void Add(ResourceTypeId type, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TryRemove to decrease quantities.");
            Set(type, checked(Get(type) + amount));
        }

        /// <summary>Removes the amount if available. Returns false and changes nothing otherwise.</summary>
        public bool TryRemove(ResourceTypeId type, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var current = Get(type);
            if (current < amount) return false;
            Set(type, current - amount);
            return true;
        }

        /// <summary>Builds an inventory, rejecting duplicate keys and negative quantities.</summary>
        public static ResourceInventory FromStacks(IEnumerable<ResourceStack> stacks)
        {
            if (stacks == null) throw new ArgumentNullException(nameof(stacks));
            var inventory = new ResourceInventory();
            foreach (var stack in stacks)
            {
                if (inventory._stacks.ContainsKey(stack.ResourceTypeId))
                {
                    throw new ArgumentException($"Duplicate resource type '{stack.ResourceTypeId}'.", nameof(stacks));
                }

                inventory.Set(stack.ResourceTypeId, stack.Quantity);
            }

            return inventory;
        }

        public ResourceInventory Clone()
        {
            var copy = new ResourceInventory();
            foreach (var pair in _stacks)
            {
                copy._stacks[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
