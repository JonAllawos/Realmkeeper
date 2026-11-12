using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15: creature models a developer installed locally under a licence that forbids redistributing them (the
    /// Quaternius Bestiary). The asset lives inside the git-ignored install folder, in a Resources folder there, so the
    /// repository never references those files: on a clone without them <see cref="Load"/> returns null and every body
    /// falls back to what the repository has. Built by <c>Realmkeeper.Editor.CharacterAnimationBuilder</c>.
    /// </summary>
    public sealed class LocalCreatureModels : ScriptableObject
    {
        public const string ResourceName = "RealmkeeperLocalCreatureModels";

        [Serializable]
        public sealed class Entry
        {
            /// <summary>The creature ID this model shows ("goblin").</summary>
            public string CreatureId;
            public GameObject Prefab;
            public Material Material;

            /// <summary>Scale so the model stands at the intended height.</summary>
            public float Scale = 1f;
        }

        public List<Entry> Entries = new List<Entry>();

        private static LocalCreatureModels _loaded;
        private static bool _searched;

        /// <summary>Tests: behave as if nothing were installed locally (the fallback a clone sees).</summary>
        public static bool ForceMissing { get; set; }

        public static LocalCreatureModels Load()
        {
            if (ForceMissing) return null;
            if (!_searched)
            {
                _searched = true;
                _loaded = Resources.Load<LocalCreatureModels>(ResourceName);
            }

            return _loaded;
        }

        public Entry For(string creatureId)
        {
            if (string.IsNullOrEmpty(creatureId)) return null;
            foreach (var e in Entries) if (e != null && e.Prefab != null && e.CreatureId == creatureId) return e;
            return null;
        }
    }
}
