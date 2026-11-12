using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15: the body a combatant shows in the world, chosen by what the creature is and what it carries — never by
    /// relabelling. Skeletons are the KayKit skeletons (a warrior or a rogue, each holding the blade their shortsword
    /// is), zombies the weaker skeleton minion in a sickly tint, goblins the locally installed Puglin when present, and
    /// people the project's humanoid bodies. Anything else, or anything missing, returns null and the caller keeps its
    /// placeholder shape. Statistics, behaviour and rules are untouched: this only decides what is drawn.
    /// </summary>
    public static class CombatantBodies
    {
        /// <summary>Skeleton models are small (about 1.05–1.34 m as made); scaled to stand near a person's height.</summary>
        public const float SkeletonScale = 1.3f;

        public sealed class Body
        {
            public GameObject Root;
            public CombatantAnimator Animator;

            /// <summary>What was used, in words ("KayKit Skeleton_Warrior with Skeleton_Blade"), for captions and the report.</summary>
            public string Source;
        }

        /// <summary>The model a creature would get from the repository's own art (null: none), ignoring local-only models.</summary>
        public static string ModelFor(string creatureId, CombatantId id) =>
            creatureId == "skeleton" ? (Variant(id) == 0 ? "Skeleton_Rogue" : "Skeleton_Warrior") :
            creatureId == "zombie" ? "Skeleton_Minion" : null;

        private static int Variant(CombatantId id) => (int)((uint)id.Value.GetHashCode() % 3u); // one in three skeletons is a rogue

        public static Body Create(CombatantView c, ArtCatalog catalog, Transform parent)
        {
            if (c == null || catalog == null) return null;
            var creature = c.CreatureId;

            // Undead: the KayKit skeletons (repository art, CC0).
            var model = ModelFor(creature, c.Id);
            if (model != null && catalog.Creature(model) != null && catalog.SkeletonCombatant != null)
            {
                var body = Instantiate(catalog.Creature(model), parent, SkeletonScale, catalog.SkeletonCombatant, out var animator);
                var source = "KayKit " + model;
                if (creature == "skeleton" && Attach(catalog.Prop("Skeleton_Blade"), body, "handslot.r")) source += " with Skeleton_Blade";
                if (creature == "zombie") Tint(body, new Color(0.62f, 0.72f, 0.55f));
                return new Body { Root = body, Animator = animator, Source = source };
            }

            // Goblins: the local-only Quaternius Puglin, when this machine has it (never required).
            var local = LocalCreatureModels.Load()?.For(creature);
            if (local != null && catalog.Combatant != null)
            {
                var body = Instantiate(local.Prefab, parent, local.Scale, catalog.Combatant, out var animator);
                if (local.Material != null) foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = local.Material;
                return new Body { Root = body, Animator = animator, Source = "local " + local.Prefab.name };
            }

            // People (bandits, cultists, guards, named residents): the humanoid bodies the towns use.
            if (IsPerson(c) && catalog.HasCharacters && catalog.Combatant != null)
            {
                var appearance = NpcAppearanceResolver.Resolve(new NpcId(c.Id.Value), RoleFor(creature), c.Name);
                var driver = HumanoidCharacterBuilder.Build(parent, appearance, catalog);
                if (driver == null) return null;
                driver.Animator.runtimeAnimatorController = catalog.PersonCombatant != null ? catalog.PersonCombatant : catalog.Combatant; // the clips made for these bodies
                var animator = driver.gameObject.AddComponent<CombatantAnimator>();
                animator.Bind(driver.Animator, drivesSpeed: false); // the driver measures walking from the body's movement
                return new Body { Root = driver.gameObject, Animator = animator, Source = "humanoid " + appearance.BodyId };
            }

            return null;
        }

        public static bool IsPerson(CombatantView c) =>
            c.Kind == CombatantKind.Npc ||
            c.CreatureId is "bandit" or "bandit_captain" or "highwayman" or "cultist" or "cult_fanatic" or "veteran" or "guard" or "acolyte" or "commoner";

        private static NpcRole RoleFor(string creature) => creature is "guard" or "veteran" or "bandit_captain" ? NpcRole.Guard : NpcRole.Labourer;

        private static GameObject Instantiate(GameObject prefab, Transform parent, float scale, RuntimeAnimatorController controller, out CombatantAnimator anim)
        {
            var body = Object.Instantiate(prefab, parent, false);
            body.name = "Body " + prefab.name;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * scale;
            var animator = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the engine places bodies; clips never move them
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim = body.AddComponent<CombatantAnimator>();
            anim.Bind(animator, drivesSpeed: true);
            return body;
        }

        /// <summary>Puts a prop in a named slot bone (the KayKit rig's handslot.r / handslot.l). False when either is missing.</summary>
        public static bool Attach(GameObject prop, GameObject body, string bone)
        {
            if (prop == null || body == null) return false;
            var slot = Find(body.transform, bone);
            if (slot == null) return false;
            var p = Object.Instantiate(prop, slot, false);
            p.name = "Held " + prop.name;
            p.transform.localPosition = Vector3.zero;
            p.transform.localRotation = Quaternion.identity;
            p.transform.localScale = Vector3.one;
            return true;
        }

        public static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static void Tint(GameObject body, Color colour)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                r.SetPropertyBlock(block);
            }
        }
    }
}
