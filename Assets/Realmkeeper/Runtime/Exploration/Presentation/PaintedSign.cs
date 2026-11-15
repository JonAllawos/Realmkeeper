using System.Collections.Generic;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 27 (owner request: "signs should not be a 3D white lettered name floating in front of a shop"): a painted
    /// signboard. A wooden board hangs from an iron bracket on the wall beside the door, at right angles to the wall so it
    /// is seen along the street, with the trade's emblem painted on both faces (the game-icons.net artwork the interface
    /// already uses, CC BY 3.0, credited). No lettering: the building's name is in its prompt, the journal and the map.
    /// </summary>
    public static class PaintedSign
    {
        private static readonly Dictionary<string, Material> Paint = new Dictionary<string, Material>();
        private static Mesh _quad;

        /// <summary>The emblem for a shop's trade (a game-icons file name), or null for none.</summary>
        public static string EmblemFor(ShopFit fit)
        {
            switch (fit)
            {
                case ShopFit.Smithy: return "anvil-impact";
                case ShopFit.General: return "two-coins";
                case ShopFit.Leatherworks: return "boots";
                case ShopFit.Tailor: return "cloak";
                case ShopFit.Bowyer: return "bow-arrow";
                case ShopFit.Apothecary: return "potion-ball";
                case ShopFit.Provisions: return "bread";
                case ShopFit.Carpentry: return "hammer-nails";
                default: return null;
            }
        }

        /// <summary>The emblem for a public building by what it is for.</summary>
        public static string EmblemFor(StructureCategory category)
        {
            switch (category)
            {
                case StructureCategory.Trade: return "trade";
                case StructureCategory.Food: return "bread";
                case StructureCategory.Hospitality: return "cooking-pot";
                case StructureCategory.Civic: return "scales";
                case StructureCategory.Military: return "checked-shield";
                case StructureCategory.Worship: return "holy-symbol";
                case StructureCategory.Storage: return "wheat";
                case StructureCategory.Agriculture: return "wheat";
                case StructureCategory.Transport: return "horse-head";
                case StructureCategory.Utility: return "gears";
                default: return null;
            }
        }

        /// <summary>
        /// Hangs a sign under <paramref name="parent"/> at <paramref name="local"/> (the bracket's root on the wall). The board
        /// stands out along local −Z (the parent's front), its faces turned along the street. Null when the emblem is missing.
        /// </summary>
        public static GameObject Hang(Transform parent, Vector3 local, string emblem, Material wood, Material iron)
        {
            var texture = string.IsNullOrEmpty(emblem) ? null : Resources.Load<Texture2D>(Realmkeeper.UI.RkIcons.ResourceFolder + emblem);
            if (texture == null) return null;
            var root = new GameObject("Painted sign " + emblem).transform;
            root.SetParent(parent, false);
            root.localPosition = local;
            Part(root, "Bracket", PrimitiveType.Cube, new Vector3(0f, 0.32f, -0.4f), new Vector3(0.05f, 0.05f, 0.85f), iron);
            Part(root, "Board", PrimitiveType.Cube, new Vector3(0f, 0f, -0.48f), new Vector3(0.05f, 0.6f, 0.62f), wood);
            foreach (var side in new[] { -1f, 1f })
            {
                var face = new GameObject("Emblem");
                face.transform.SetParent(root, false);
                face.transform.localPosition = new Vector3(side * 0.03f, 0f, -0.48f);
                face.transform.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f); // facing out of each side of the board
                face.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
                face.AddComponent<MeshFilter>().sharedMesh = Quad();
                var r = face.AddComponent<MeshRenderer>();
                r.sharedMaterial = PaintFor(texture);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            return root.gameObject;
        }

        private static void Part(Transform root, string name, PrimitiveType type, Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (m != null) go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>The emblem in dark paint, cut out where the artwork is clear (one material per emblem, shared).</summary>
        private static Material PaintFor(Texture2D texture)
        {
            if (Paint.TryGetValue(texture.name, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            m = new Material(shader) { name = "Sign paint " + texture.name };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", new Color(0.93f, 0.84f, 0.6f)); // pale ochre on dark planks: readable from the street
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Smoothness", 0.1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            Paint[texture.name] = m;
            return m;
        }

        private static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Sign emblem quad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateNormals();
            return _quad;
        }
    }
}
