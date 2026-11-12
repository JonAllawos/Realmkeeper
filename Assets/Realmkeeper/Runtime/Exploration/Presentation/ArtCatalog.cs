using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The project's art lookup: kit modules, materials, character parts and the locomotion
    /// controller, by stable string ID. Built by the editor setup (<c>Realmkeeper.Editor.ArtSetup</c>)
    /// from the licence-clear third-party packs in <c>Assets/Realmkeeper/Art/ThirdParty</c> and loaded
    /// from Resources. Presentation only: nothing in world data refers to it. When it is missing
    /// (for example in a project without the art imported) the builders fall back to primitives.
    /// </summary>
    [CreateAssetMenu(menuName = "Realmkeeper/Art Catalog", fileName = ResourceName)]
    public sealed class ArtCatalog : ScriptableObject
    {
        public const string ResourceName = "RealmkeeperArtCatalog";

        [Serializable]
        public sealed class ModuleEntry
        {
            public string Id;
            public GameObject Prefab;

            /// <summary>Local bounds of the module as imported (metres, Unity axes).</summary>
            public Bounds Bounds;
        }

        [Serializable]
        public sealed class MaterialEntry
        {
            public string Id;
            public Material Material;
        }

        [Serializable]
        public sealed class PrefabEntry
        {
            public string Id;
            public GameObject Prefab;
        }

        public List<ModuleEntry> Modules = new List<ModuleEntry>();
        public List<MaterialEntry> Materials = new List<MaterialEntry>();

        /// <summary>Complete clothed bodies on the shared humanoid skeleton (no head), e.g. "Male_Peasant".</summary>
        public List<PrefabEntry> Bodies = new List<PrefabEntry>();

        /// <summary>Skinned parts bound to the same skeleton by bone name: heads, eyes, brows, hair, beards.</summary>
        public List<PrefabEntry> Parts = new List<PrefabEntry>();

        public RuntimeAnimatorController Locomotion;

        /// <summary>Phase 15: creature bodies by ID (the KayKit skeletons: "Skeleton_Warrior", "Skeleton_Minion", "Skeleton_Rogue", "Skeleton_Mage").</summary>
        public List<PrefabEntry> Creatures = new List<PrefabEntry>();

        /// <summary>Phase 15: props held in a hand slot, by ID ("Skeleton_Blade", "Skeleton_Quiver" …).</summary>
        public List<PrefabEntry> Props = new List<PrefabEntry>();

        /// <summary>Phase 15: the shared combat controller for humanoid combatants (locomotion, attacks, cast, block, hit, death, interact, jump).</summary>
        public RuntimeAnimatorController Combatant;

        /// <summary>Phase 15: the same controller with the skeletons' own idle, walk and death (an override controller).</summary>
        public RuntimeAnimatorController SkeletonCombatant;

        /// <summary>Phase 15: the same controller with the Universal Animation Library clips made for the Quaternius bodies (the player and people).</summary>
        public RuntimeAnimatorController PersonCombatant;

        private Dictionary<string, GameObject> _creatures;
        private Dictionary<string, GameObject> _props;

        public GameObject Creature(string id) => Lookup(ref _creatures, Creatures, id);

        public GameObject Prop(string id) => Lookup(ref _props, Props, id);

        [Serializable]
        public sealed class ControllerEntry
        {
            public string Id;
            public RuntimeAnimatorController Controller;
        }

        /// <summary>Phase 18: animals and monsters (the Quaternius creatures, the farm sheep, the Poly by Google animals), by model name ("Wolf").</summary>
        public List<PrefabEntry> Animals = new List<PrefabEntry>();

        /// <summary>Phase 18: each animated animal's own combat controller (the shared one with its clips), by model name.</summary>
        public List<ControllerEntry> AnimalControllers = new List<ControllerEntry>();

        /// <summary>Phase 18: static scenery by model name — trees, rocks, wells, props, lanterns, dungeon pieces (CC0 kits).</summary>
        public List<PrefabEntry> Scenery = new List<PrefabEntry>();

        private Dictionary<string, GameObject> _scenery;

        public GameObject SceneryModel(string id) => Lookup(ref _scenery, Scenery, id);

        private Dictionary<string, GameObject> _animals;

        public GameObject Animal(string id) => Lookup(ref _animals, Animals, id);

        public RuntimeAnimatorController AnimalController(string id)
        {
            foreach (var e in AnimalControllers) if (e != null && e.Id == id) return e.Controller;
            return null;
        }

        private Dictionary<string, ModuleEntry> _modules;
        private Dictionary<string, Material> _materials;
        private Dictionary<string, GameObject> _bodies;
        private Dictionary<string, GameObject> _parts;

        private static ArtCatalog _loaded;
        private static bool _searched;

        /// <summary>The catalog from Resources, or null when the art has not been set up.</summary>
        public static ArtCatalog Load()
        {
            if (_searched) return _loaded;
            _searched = true;
            _loaded = Resources.Load<ArtCatalog>(ResourceName);
            return _loaded;
        }

        /// <summary>Test hook: forget the cached lookup (or force a catalog, or none).</summary>
        public static void Override(ArtCatalog catalog)
        {
            _loaded = catalog;
            _searched = true;
        }

        public static void ResetOverride()
        {
            _loaded = null;
            _searched = false;
        }

        public ModuleEntry Module(string id)
        {
            if (_modules == null)
            {
                _modules = new Dictionary<string, ModuleEntry>(StringComparer.Ordinal);
                foreach (var m in Modules) if (m != null && m.Prefab != null && !string.IsNullOrEmpty(m.Id)) _modules[m.Id] = m;
            }

            return id != null && _modules.TryGetValue(id, out var e) ? e : null;
        }

        public Material Material(string id)
        {
            if (_materials == null)
            {
                _materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var m in Materials) if (m != null && m.Material != null && !string.IsNullOrEmpty(m.Id)) _materials[m.Id] = m.Material;
            }

            return id != null && _materials.TryGetValue(id, out var mat) ? mat : null;
        }

        public GameObject Body(string id) => Lookup(ref _bodies, Bodies, id);

        public GameObject Part(string id) => Lookup(ref _parts, Parts, id);

        private static GameObject Lookup(ref Dictionary<string, GameObject> cache, List<PrefabEntry> list, string id)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var e in list) if (e != null && e.Prefab != null && !string.IsNullOrEmpty(e.Id)) cache[e.Id] = e.Prefab;
            }

            return id != null && cache.TryGetValue(id, out var go) ? go : null;
        }

        /// <summary>True when the catalog has everything humanoid residents need.</summary>
        public bool HasCharacters => Bodies.Count > 0 && Locomotion != null;
    }
}
