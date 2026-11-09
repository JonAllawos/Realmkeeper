using System;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Identifies one save. A slot is a short name of 1–48 letters, digits, '-' or '_', so it
    /// can never escape the save folder. <see cref="Default"/> ("autosave") is the slot used
    /// when nothing else is chosen, so worlds saved before slots existed keep loading.
    /// </summary>
    public readonly struct SaveSlot : IEquatable<SaveSlot>
    {
        public const string DefaultName = "autosave";
        public const int MaxLength = 48;

        private readonly string _name;

        private SaveSlot(string name) { _name = name; }

        public static SaveSlot Default => new SaveSlot(DefaultName);

        /// <summary>The slot name (the default slot's name for an uninitialised value).</summary>
        public string Name => _name ?? DefaultName;

        public static bool IsValidName(string name) => Validate(name) == null;

        public static bool TryCreate(string name, out SaveSlot slot, out string error)
        {
            error = Validate(name);
            slot = error == null ? new SaveSlot(name) : Default;
            return error == null;
        }

        /// <summary>Creates a slot or throws <see cref="ArgumentException"/> with the reason.</summary>
        public static SaveSlot Create(string name)
        {
            if (!TryCreate(name, out var slot, out var error)) throw new ArgumentException(error, nameof(name));
            return slot;
        }

        /// <summary>Null when valid, otherwise a sentence explaining what is wrong.</summary>
        public static string Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "A save slot name is required.";
            if (name.Length > MaxLength) return $"Save slot names are at most {MaxLength} characters ('{name.Substring(0, 12)}…' has {name.Length}).";
            foreach (var c in name)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return $"Save slot '{name}' may only contain letters, digits, '-' and '_'.";
            }

            return null;
        }

        public bool Equals(SaveSlot other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSlot other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
        public static bool operator ==(SaveSlot a, SaveSlot b) => a.Equals(b);
        public static bool operator !=(SaveSlot a, SaveSlot b) => !a.Equals(b);
        public override string ToString() => Name;
    }
}
