using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Muted, stylized-realistic colours (specification §8: no saturated, toy-like tones).
    /// Values are authored in sRGB and converted to linear, because the project renders in
    /// linear colour space and vertex colours are not converted by Unity.
    /// </summary>
    public static class Palette
    {
        // Terrain
        public static readonly Color Grass = Lin(0.40f, 0.48f, 0.27f);
        public static readonly Color DryGrass = Lin(0.58f, 0.55f, 0.35f);
        public static readonly Color ForestFloor = Lin(0.29f, 0.35f, 0.21f);
        public static readonly Color HillGrass = Lin(0.47f, 0.49f, 0.31f);
        public static readonly Color Rock = Lin(0.50f, 0.48f, 0.44f);
        public static readonly Color DarkRock = Lin(0.38f, 0.36f, 0.34f);
        public static readonly Color Snow = Lin(0.90f, 0.91f, 0.92f);
        public static readonly Color Sand = Lin(0.72f, 0.66f, 0.51f);
        public static readonly Color ShallowBed = Lin(0.47f, 0.44f, 0.35f);
        public static readonly Color DeepBed = Lin(0.28f, 0.30f, 0.28f);
        public static readonly Color Earth = Lin(0.31f, 0.26f, 0.21f);

        // Roads and bridges
        public static readonly Color PackedEarth = Lin(0.53f, 0.45f, 0.33f);
        public static readonly Color BridgeTimber = Lin(0.40f, 0.31f, 0.22f);

        // Vegetation
        public static readonly Color ConiferA = Lin(0.18f, 0.27f, 0.18f);
        public static readonly Color ConiferB = Lin(0.22f, 0.31f, 0.20f);
        public static readonly Color BroadleafA = Lin(0.31f, 0.40f, 0.20f);
        public static readonly Color BroadleafB = Lin(0.38f, 0.43f, 0.22f);
        public static readonly Color Bark = Lin(0.30f, 0.23f, 0.17f);

        // Buildings
        public static readonly Color Limestone = Lin(0.76f, 0.72f, 0.64f);
        public static readonly Color Plaster = Lin(0.82f, 0.77f, 0.66f);
        public static readonly Color Timber = Lin(0.44f, 0.33f, 0.23f);
        public static readonly Color Slate = Lin(0.33f, 0.35f, 0.39f);
        public static readonly Color Thatch = Lin(0.62f, 0.53f, 0.34f);
        public static readonly Color RoofTile = Lin(0.52f, 0.33f, 0.25f);
        public static readonly Color Stone = Lin(0.58f, 0.56f, 0.52f);

        // Fields
        public static readonly Color Wheat = Lin(0.70f, 0.62f, 0.37f);
        public static readonly Color Crop = Lin(0.45f, 0.52f, 0.29f);
        public static readonly Color Ploughed = Lin(0.43f, 0.35f, 0.25f);

        // Atmosphere (these feed Unity lighting, which takes sRGB colours, so no conversion)
        public static readonly Color Sun = new Color(1.00f, 0.90f, 0.76f);
        public static readonly Color AmbientSky = new Color(0.62f, 0.67f, 0.74f);
        public static readonly Color AmbientEquator = new Color(0.55f, 0.55f, 0.52f);
        public static readonly Color AmbientGround = new Color(0.33f, 0.30f, 0.26f);
        public static readonly Color Haze = new Color(0.72f, 0.74f, 0.75f);

        public static Color Lin(float r, float g, float b) => new Color(r, g, b).linear;

        public static Color Scale(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }
}
