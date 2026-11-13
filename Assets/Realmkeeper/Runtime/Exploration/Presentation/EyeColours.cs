using System.Collections.Generic;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 20A (ADR-0104): eye colour without tinting the whites. The CC0 eye texture paints iris, whites and the skin
    /// around the eye in one map, so a material tint would colour all of it. <c>T_Eye_IrisMask</c> (derived from that
    /// texture by <c>Tools/Art/make_iris_mask.py</c>) keeps the texture's colours with the iris weight in alpha; each eye
    /// colour is made once from it, by keeping the iris's own shading and replacing its hue, and shared by every body that
    /// uses it. Colour 0 (brown) is the texture as drawn and is never replaced.
    /// </summary>
    public static class EyeColours
    {
        private const string MaskPath = "RealmkeeperEyes/T_Eye_IrisMask";
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly Dictionary<int, Texture2D> Made = new Dictionary<int, Texture2D>();
        private static Texture2D _mask;
        private static bool _missing;

        /// <summary>The average luminance of the drawn iris (measured by the mask tool), so recoloured eyes keep its depth.</summary>
        private const float IrisLuminance = 41.3f / 255f;

        /// <summary>The texture for an eye colour, or null for the drawn brown (or when the mask is missing).</summary>
        public static Texture2D For(int colour)
        {
            if (colour <= 0 || colour >= PlayerAppearanceOptions.EyePalette.Count) return null;
            if (Made.TryGetValue(colour, out var t) && t != null) return t;
            var mask = Mask();
            if (mask == null) return null;
            var (name, r, g, b) = PlayerAppearanceOptions.EyePalette[colour];
            var tint = new Color(r, g, b);
            var src = mask.GetPixels32();
            var dst = new Color32[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                var p = src[i];
                var weight = p.a / 255f;
                var c = new Color(p.r / 255f, p.g / 255f, p.b / 255f);
                if (weight > 0f)
                {
                    var shade = Mathf.Clamp((0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / IrisLuminance, 0f, 2.2f) * 0.5f;
                    var iris = new Color(Mathf.Clamp01(tint.r * shade), Mathf.Clamp01(tint.g * shade), Mathf.Clamp01(tint.b * shade));
                    c = Color.Lerp(c, iris, weight);
                }

                dst[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }

            t = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, true) { name = "Eye " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(dst);
            t.Apply(true, true); // mipmaps, then no CPU copy
            Made[colour] = t;
            return t;
        }

        /// <summary>Gives the eye materials of these renderers the colour's texture (a property block: the shared material is untouched).</summary>
        public static void Apply(IEnumerable<SkinnedMeshRenderer> renderers, int colour)
        {
            var tex = For(colour);
            if (tex == null) return;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith("M_Eyes", System.StringComparison.Ordinal)) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    block.SetTexture(BaseMap, tex);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        private static Texture2D Mask()
        {
            if (_mask != null || _missing) return _mask;
            _mask = Resources.Load<Texture2D>(MaskPath);
            if (_mask == null || !_mask.isReadable)
            {
                _missing = true;
                Debug.LogWarning("[Realmkeeper] The iris mask is missing or not readable; eyes stay their drawn brown.");
                _mask = null;
            }

            return _mask;
        }
    }
}
