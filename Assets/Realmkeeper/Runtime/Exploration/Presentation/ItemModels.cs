using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Procedural models of the item catalog (Phase 5): the CC0 packs have no weapons, shields, hats or
    /// wares, so they are built from metre meshes with the kit's materials. One model per item
    /// definition, the same in a shop display and in the player's hands. Item frame: the grip (or the
    /// centre) at the origin, a held item's length along +Y, a shield's face toward +Z.
    /// </summary>
    public static class ItemModels
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>Builds the model of <paramref name="itemId"/> under <paramref name="parent"/>; null if the item has no model.</summary>
        public static GameObject Build(Transform parent, string itemId, ArtCatalog catalog, GeneratedAssets assets)
        {
            var def = ItemCatalog.Find(itemId);
            if (def == null || catalog == null) return null;
            var root = new GameObject("Item " + def.Id);
            root.transform.SetParent(parent, false);
            var k = new Kit(root.transform, catalog, assets);
            // Phase 11: an item drawn like another (a greatsword like the arming sword) until it has its own model.
            var model = def.Visual != null && def.Visual.StartsWith("like:", System.StringComparison.Ordinal) ? def.Visual.Substring(5) : def.Id;
            // Phase 20A assets: an art model where the catalog has one (KayKit Fantasy Weapons Bits, the Fantasy Props kit);
            // the item's own entry first, then the item it is drawn like. The built shape below remains the fallback.
            if (TryArtModel(root.transform, def.Id, catalog) || (model != def.Id && TryArtModel(root.transform, model, catalog))) return root;
            switch (model)
            {
                case "dagger_iron":
                    k.Box("Grip", new Vector3(0.03f, 0.11f, 0.03f), "Wood", new Vector3(0f, 0f, 0f));
                    k.Box("Guard", new Vector3(0.11f, 0.02f, 0.03f), "Metal", new Vector3(0f, 0.065f, 0f));
                    k.Box("Blade", new Vector3(0.034f, 0.22f, 0.007f), "Steel", new Vector3(0f, 0.185f, 0f));
                    break;
                case "sword_arming":
                    k.Box("Grip", new Vector3(0.034f, 0.16f, 0.034f), "Leather", Vector3.zero);
                    k.Lathe("Pommel", new[] { new Vector2(0.001f, -0.03f), new Vector2(0.03f, 0f), new Vector2(0.001f, 0.03f) }, "Metal", new Vector3(0f, -0.1f, 0f));
                    k.Box("Guard", new Vector3(0.2f, 0.028f, 0.04f), "Metal", new Vector3(0f, 0.093f, 0f));
                    k.Box("Blade", new Vector3(0.045f, 0.72f, 0.009f), "Steel", new Vector3(0f, 0.467f, 0f));
                    break;
                case "axe_hand":
                    k.Box("Haft", new Vector3(0.034f, 0.52f, 0.034f), "Wood", new Vector3(0f, 0.16f, 0f));
                    k.Box("Head", new Vector3(0.16f, 0.09f, 0.022f), "Steel", new Vector3(0.07f, 0.37f, 0f));
                    k.Box("Edge", new Vector3(0.02f, 0.15f, 0.018f), "Steel", new Vector3(0.15f, 0.37f, 0f));
                    break;
                case "mace_flanged":
                    k.Box("Haft", new Vector3(0.034f, 0.48f, 0.034f), "Wood", new Vector3(0f, 0.12f, 0f));
                    k.Lathe("Head", new[] { new Vector2(0.025f, -0.06f), new Vector2(0.055f, -0.02f), new Vector2(0.055f, 0.04f), new Vector2(0.02f, 0.07f) }, "Metal", new Vector3(0f, 0.4f, 0f));
                    for (var i = 0; i < 4; i++)
                    {
                        var f = k.Box("Flange", new Vector3(0.012f, 0.1f, 0.06f), "Metal", new Vector3(0f, 0.4f, 0f));
                        f.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                        f.transform.localPosition += f.transform.localRotation * new Vector3(0f, 0f, 0.05f);
                    }

                    break;
                case "spear_ash":
                    k.Lathe("Shaft", new[] { new Vector2(0.018f, -0.55f), new Vector2(0.018f, 1.3f) }, "Wood", Vector3.zero);
                    k.Box("Head", new Vector3(0.045f, 0.24f, 0.01f), "Steel", new Vector3(0f, 1.42f, 0f));
                    k.Lathe("Socket", new[] { new Vector2(0.022f, 0f), new Vector2(0.016f, 0.08f) }, "Metal", new Vector3(0f, 1.26f, 0f));
                    break;
                case "staff_quarter":
                    k.Lathe("Staff", new[] { new Vector2(0.022f, -0.7f), new Vector2(0.024f, 0f), new Vector2(0.02f, 1.1f) }, "Wood", Vector3.zero);
                    break;
                case "bow_hunting":
                    foreach (var s in new[] { -1f, 1f })
                    {
                        var limb = k.Box("Limb", new Vector3(0.025f, 0.62f, 0.03f), "Wood", new Vector3(0f, s * 0.3f, 0.06f));
                        limb.transform.localRotation = Quaternion.Euler(-s * 12f, 0f, 0f);
                    }

                    k.Box("String", new Vector3(0.004f, 1.18f, 0.004f), "Canvas", new Vector3(0f, 0f, -0.02f));
                    k.Box("Grip", new Vector3(0.035f, 0.12f, 0.04f), "Leather", new Vector3(0f, 0f, 0.0f));
                    break;
                case "shield_round":
                    k.Lathe("Board", new[] { new Vector2(0.001f, 0f), new Vector2(0.38f, 0f), new Vector2(0.38f, 0.03f), new Vector2(0.001f, 0.03f) }, "Planks", Vector3.zero, 24)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    k.Lathe("Rim", new[] { new Vector2(0.385f, -0.005f), new Vector2(0.385f, 0.035f) }, "Metal", Vector3.zero, 24, false)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    k.Lathe("Boss", new[] { new Vector2(0.08f, 0f), new Vector2(0.07f, 0.03f), new Vector2(0.03f, 0.06f), new Vector2(0.001f, 0.065f) }, "Metal", new Vector3(0f, 0f, -0.03f), 12)
                        .transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    break;
                case "shield_buckler":
                    k.Lathe("Buckler", new[] { new Vector2(0.001f, 0f), new Vector2(0.2f, 0f), new Vector2(0.17f, 0.04f), new Vector2(0.06f, 0.06f), new Vector2(0.001f, 0.08f) }, "Metal", Vector3.zero, 20)
                        .transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    break;
                case "hat_felt":
                    k.Lathe("Hat", new[] { new Vector2(0.17f, 0f), new Vector2(0.17f, 0.012f), new Vector2(0.105f, 0.02f), new Vector2(0.1f, 0.1f), new Vector2(0.07f, 0.13f), new Vector2(0.001f, 0.135f) },
                        "Felt", Vector3.zero, 18);
                    break;
                case "cap_iron":
                    k.Lathe("Cap", new[] { new Vector2(0.118f, 0f), new Vector2(0.118f, 0.03f), new Vector2(0.11f, 0.08f), new Vector2(0.075f, 0.135f), new Vector2(0.001f, 0.16f) },
                        "Steel", Vector3.zero, 18);
                    break;
                case "bread_loaf":
                    k.Lathe("Loaf", new[] { new Vector2(0.07f, 0f), new Vector2(0.09f, 0.03f), new Vector2(0.075f, 0.075f), new Vector2(0.001f, 0.09f) }, "Bread", Vector3.zero, 12)
                        .transform.localScale = new Vector3(1.4f, 1f, 0.9f);
                    break;
                case "cheese_wheel":
                    k.Lathe("Wheel", new[] { new Vector2(0.15f, 0f), new Vector2(0.16f, 0.04f), new Vector2(0.15f, 0.09f), new Vector2(0.001f, 0.09f) }, "Cheese", Vector3.zero, 16);
                    break;
                case "travel_rations":
                    k.Box("Bundle", new Vector3(0.22f, 0.08f, 0.14f), "Canvas", new Vector3(0f, 0.04f, 0f));
                    k.Box("Tie", new Vector3(0.23f, 0.085f, 0.015f), "Leather", new Vector3(0f, 0.04f, 0f));
                    break;
                case "iron_bar":
                    k.Box("Bar", new Vector3(0.24f, 0.05f, 0.07f), "Steel", new Vector3(0f, 0.025f, 0f));
                    break;
                case "timber_plank":
                    k.Box("Plank", new Vector3(1.2f, 0.04f, 0.18f), "Wood", new Vector3(0f, 0.02f, 0f));
                    break;
                case "dressed_stone":
                    k.Box("Stone", new Vector3(0.36f, 0.2f, 0.22f), "RockTrim", new Vector3(0f, 0.1f, 0f));
                    break;
                case "grain_sack":
                    k.Lathe("Sack", new[] { new Vector2(0.12f, 0f), new Vector2(0.17f, 0.12f), new Vector2(0.15f, 0.3f), new Vector2(0.05f, 0.38f), new Vector2(0.001f, 0.4f) }, "Canvas", Vector3.zero, 12);
                    break;
                default:
                    // Clothing and armour are shown folded on a shelf in their cloth's colour.
                    var colour = ClothColour(def.Visual);
                    k.Box("Folded", new Vector3(0.34f, 0.1f, 0.27f), k.Tinted("Canvas", colour), new Vector3(0f, 0.05f, 0f));
                    k.Box("Fold", new Vector3(0.32f, 0.03f, 0.12f), k.Tinted("Canvas", colour * 0.82f), new Vector3(0f, 0.112f, -0.06f));
                    k.Box("Collar", new Vector3(0.12f, 0.02f, 0.06f), k.Tinted("Canvas", colour * 0.7f), new Vector3(0f, 0.105f, 0.09f));
                    break;
            }

            return root;
        }

        /// <summary>
        /// Phase 20A assets: which catalog model draws an item, its scale to the item's real size and its turn into the item
        /// frame (grip at the origin, length along +Y, a shield's face toward +Z). KayKit's weapons are modelled about twice
        /// life size for its chunky figures; the Fantasy Props are in metres.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string model, float scale, Vector3 euler)> ArtModels = new Dictionary<string, (string, float, Vector3)>(StringComparer.Ordinal)
        {
            ["dagger_iron"] = ("weapon_dagger_A", 0.28f, Vector3.zero),
            ["sword_arming"] = ("weapon_sword_B", 0.54f, Vector3.zero),
            ["shortsword"] = ("weapon_sword_B", 0.42f, Vector3.zero),
            ["scimitar"] = ("weapon_sword_C", 0.42f, Vector3.zero),
            ["rapier"] = ("weapon_sword_D", 0.49f, Vector3.zero),
            ["axe_hand"] = ("weapon_axe_A", 0.46f, Vector3.zero),
            ["greataxe"] = ("weapon_axe_B", 0.6f, Vector3.zero),
            ["mace_flanged"] = ("weapon_hammer_B", 0.41f, Vector3.zero),
            ["spear_ash"] = ("weapon_spear_A", 0.6f, Vector3.zero),
            ["javelin"] = ("weapon_spear_A", 0.42f, Vector3.zero),
            ["staff_quarter"] = ("weapon_staff_A", 0.84f, Vector3.zero),
            ["bow_hunting"] = ("weapon_bow_A_withString", 0.66f, new Vector3(0f, 0f, 90f)),
            ["shield_round"] = ("weapon_shield_A", 0.72f, new Vector3(0f, 180f, 0f)),
            ["shield_buckler"] = ("weapon_shield_A", 0.5f, new Vector3(0f, 180f, 0f)),
            ["arcane_focus"] = ("weapon_wand_A", 0.36f, Vector3.zero),
            ["old_key"] = ("Key_Metal", 2.2f, Vector3.zero),
            ["fort_key"] = ("Key_Metal", 2.2f, Vector3.zero),
            ["ashgrave_vault_key"] = ("Key_Metal", 2.2f, Vector3.zero),
            ["crowfoot_strongbox_key"] = ("Key_Gold", 2.2f, Vector3.zero),
            ["potion_healing"] = ("Potion_1", 1f, Vector3.zero),
            ["potion_greater_healing"] = ("Potion_2", 1f, Vector3.zero),
            ["sealed_letter"] = ("Scroll_1", 1f, Vector3.zero),
            ["old_charter"] = ("Scroll_2", 1f, Vector3.zero),
            ["almanac"] = ("Book_5", 1f, Vector3.zero),
            ["true_ledger"] = ("Book_7", 1f, Vector3.zero),
            ["spellbook"] = ("Book_7", 1f, Vector3.zero),
            ["backpack"] = ("Bag", 0.55f, Vector3.zero),
        };

        private static bool TryArtModel(Transform root, string itemId, ArtCatalog catalog)
        {
            if (!ArtModels.TryGetValue(itemId, out var art)) return false;
            var prefab = catalog.SceneryModel(art.model);
            if (prefab == null) return false;
            var go = Object.Instantiate(prefab, root, false);
            go.name = "Art " + art.model;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(art.euler) * go.transform.localRotation; // the model keeps its import turn
            go.transform.localScale = go.transform.localScale * art.scale; // and its import scale
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return true;
        }

        /// <summary>The colour a cloth item reads as (its outfit material).</summary>
        public static Color ClothColour(string visual)
        {
            var material = visual != null && visual.StartsWith("outfit:", StringComparison.Ordinal) ? visual.Substring(visual.LastIndexOf(':') + 1) : string.Empty;
            switch (material)
            {
                case "Peasant": return new Color(0.74f, 0.66f, 0.52f);
                case "Peasant2": return new Color(0.46f, 0.52f, 0.64f);
                case "Ranger": return new Color(0.42f, 0.31f, 0.2f);
                case "Ranger3": return new Color(0.33f, 0.4f, 0.25f);
                default: return new Color(0.6f, 0.55f, 0.45f);
            }
        }

        /// <summary>Builds meshes and coloured materials once per environment (shared by every copy).</summary>
        private sealed class Kit
        {
            private static readonly Dictionary<GeneratedAssets, Dictionary<string, Object>> Cache = new Dictionary<GeneratedAssets, Dictionary<string, Object>>();
            private readonly Transform _root;
            private readonly ArtCatalog _catalog;
            private readonly GeneratedAssets _assets;
            private readonly Dictionary<string, Object> _cache;

            public Kit(Transform root, ArtCatalog catalog, GeneratedAssets assets)
            {
                _root = root;
                _catalog = catalog;
                _assets = assets;
                if (assets == null)
                {
                    _cache = new Dictionary<string, Object>();
                }
                else if (!Cache.TryGetValue(assets, out _cache))
                {
                    var stale = new List<GeneratedAssets>();
                    foreach (var key in Cache.Keys) if (key == null) stale.Add(key);
                    foreach (var key in stale) Cache.Remove(key);
                    _cache = new Dictionary<string, Object>(StringComparer.Ordinal);
                    Cache[assets] = _cache;
                }
            }

            public Material Material(string id)
            {
                switch (id)
                {
                    case "Steel": return Tinted("Metal", new Color(0.62f, 0.64f, 0.66f));
                    case "Leather": return Tinted("Canvas", new Color(0.36f, 0.24f, 0.15f));
                    case "Felt": return Tinted("Canvas", new Color(0.3f, 0.26f, 0.22f));
                    case "Bread": return Tinted("Canvas", new Color(0.72f, 0.47f, 0.24f));
                    case "Cheese": return Tinted("Canvas", new Color(0.92f, 0.76f, 0.32f));
                    default: return _catalog.Material(id) ?? _catalog.Material("Canvas");
                }
            }

            public Material Tinted(string baseId, Color colour)
            {
                var key = "mat:" + baseId + ":" + ColorUtility.ToHtmlStringRGB(colour);
                if (_cache.TryGetValue(key, out var found) && found != null) return (Material)found;
                var source = _catalog.Material(baseId) ?? _catalog.Material("Canvas");
                var m = new Material(source) { name = "Item " + baseId + " " + ColorUtility.ToHtmlStringRGB(colour) };
                m.SetColor(BaseColor, colour);
                _assets?.Track(m);
                _cache[key] = m;
                return m;
            }

            public GameObject Box(string name, Vector3 size, string material, Vector3 at) => Box(name, size, Material(material), at);

            public GameObject Box(string name, Vector3 size, Material material, Vector3 at)
            {
                var key = "box:" + size.x.ToString("F3") + ":" + size.y.ToString("F3") + ":" + size.z.ToString("F3");
                if (!_cache.TryGetValue(key, out var mesh) || mesh == null) _cache[key] = mesh = MetreMeshes.Box(size, _assets);
                return Part(name, (Mesh)mesh, material, at);
            }

            public GameObject Lathe(string name, Vector2[] profile, string material, Vector3 at, int segments = 10, bool cap = true)
            {
                var key = "lathe:" + segments + ":" + cap + ":" + string.Join(";", Array.ConvertAll(profile, p => p.x.ToString("F3") + "," + p.y.ToString("F3")));
                if (!_cache.TryGetValue(key, out var mesh) || mesh == null) _cache[key] = mesh = MetreMeshes.Lathe(profile, segments, _assets, cap);
                return Part(name, (Mesh)mesh, Material(material), at);
            }

            private GameObject Part(string name, Mesh mesh, Material material, Vector3 at)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = at;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                return go;
            }
        }
    }
}
