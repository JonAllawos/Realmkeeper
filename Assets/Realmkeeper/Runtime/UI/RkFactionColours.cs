using UnityEngine;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Stable display colours for factions, by their position in the world's faction list. One table for the strategic map
    /// (its political overlay, through <c>FactionPalette</c>) and the towns (Phase 27: banners and the watch's clothes), so a
    /// faction wears the same colour everywhere.
    /// </summary>
    public static class RkFactionColours
    {
        public static readonly Color[] Colors =
        {
            new Color(0.78f, 0.25f, 0.22f), // crimson
            new Color(0.25f, 0.45f, 0.78f), // blue
            new Color(0.85f, 0.68f, 0.22f), // gold
            new Color(0.36f, 0.62f, 0.36f), // green
            new Color(0.62f, 0.38f, 0.72f), // violet
            new Color(0.22f, 0.66f, 0.68f), // teal
            new Color(0.88f, 0.50f, 0.22f), // orange
            new Color(0.80f, 0.80f, 0.84f), // silver
        };

        public static readonly Color Independent = new Color(0.55f, 0.53f, 0.50f);

        public static Color For(int index) => index < 0 ? Independent : Colors[index % Colors.Length];
    }
}
