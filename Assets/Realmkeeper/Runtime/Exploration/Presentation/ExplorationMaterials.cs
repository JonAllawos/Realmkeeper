using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Shared URP Lit materials for the local environment, one per palette colour (ADR-0009:
    /// standard URP Lit/PBR for exploration art, no pipeline change). Temporary vertical-slice art:
    /// flat colours on primitive geometry at correct scale.
    /// </summary>
    public sealed class ExplorationMaterials : IDisposable
    {
        public static readonly Color Grass = new Color(0.36f, 0.43f, 0.24f);
        public static readonly Color Cobble = new Color(0.52f, 0.50f, 0.46f);
        public static readonly Color Dirt = new Color(0.47f, 0.40f, 0.30f);
        public static readonly Color Plaster = new Color(0.82f, 0.77f, 0.66f);
        public static readonly Color Limestone = new Color(0.72f, 0.69f, 0.62f);
        public static readonly Color Timber = new Color(0.40f, 0.30f, 0.21f);
        public static readonly Color DarkTimber = new Color(0.26f, 0.19f, 0.14f);
        public static readonly Color Thatch = new Color(0.60f, 0.51f, 0.33f);
        public static readonly Color Slate = new Color(0.32f, 0.34f, 0.38f);
        public static readonly Color RoofTile = new Color(0.50f, 0.31f, 0.23f);
        public static readonly Color Stone = new Color(0.55f, 0.53f, 0.49f);
        public static readonly Color Canvas = new Color(0.66f, 0.56f, 0.40f);
        public static readonly Color Leaves = new Color(0.27f, 0.38f, 0.20f);
        public static readonly Color Parchment = new Color(0.86f, 0.80f, 0.64f);
        public static readonly Color Ember = new Color(0.85f, 0.42f, 0.18f);

        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly Shader _shader;

        public ExplorationMaterials()
        {
            _shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (_shader == null) throw new InvalidOperationException("The URP Lit shader was not found.");
        }

        public Material Get(Color color, float smoothness = 0.15f)
        {
            if (_materials.TryGetValue(color, out var m)) return m;
            m = new Material(_shader) { name = "Exploration " + ColorUtility.ToHtmlStringRGB(color), enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            _materials[color] = m;
            return m;
        }

        public void Dispose()
        {
            foreach (var m in _materials.Values)
            {
                if (m == null) continue;
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(m);
                else UnityEngine.Object.DestroyImmediate(m);
            }

            _materials.Clear();
        }
    }
}
