using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>Deterministic place-name generator from original English-style toponym parts.</summary>
    internal sealed class NameGenerator
    {
        private static readonly string[] Prefixes =
        {
            "Ash", "Bram", "Cold", "Elder", "Fen", "Gray", "Hollow", "Iron", "Kings", "Mill",
            "Oak", "Raven", "Stone", "Thorn", "Wester", "Wil", "Marsh", "Red", "Hart", "Brack",
            "Alder", "Barrow", "Copper", "Dun", "Hazel", "Lark", "Moss", "Rush", "Sorrel", "Wyn",
        };

        private static readonly string[] Suffixes =
        {
            "ford", "holm", "wick", "stead", "bury", "mere", "dale", "brook", "haven", "moor",
            "ley", "field", "gate", "thorpe", "combe", "well",
        };

        private readonly IRandomSource _random;
        private readonly HashSet<string> _used = new HashSet<string>();

        public NameGenerator(IRandomSource random)
        {
            _random = random;
        }

        public string NextPlaceName()
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var name = _random.Pick(Prefixes) + _random.Pick(Suffixes);
                if (_used.Add(name)) return name;
            }

            var fallback = "Settlement " + (_used.Count + 1);
            _used.Add(fallback);
            return fallback;
        }
    }
}
