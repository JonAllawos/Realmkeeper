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

        /// <summary>
        /// Phase 18: which creature model draws which creature (models are already at real-world size; the scale adjusts
        /// for the larger kin of a beast), and a tint where one model stands for two creatures. Null: no model.
        /// </summary>
        public static (string Model, float Scale, Color? Tint) AnimalFor(string creatureId)
        {
            switch (creatureId)
            {
                case "wolf": return ("Wolf", 1f, null);
                case "dire_wolf": return ("Wolf", 1.45f, new Color(0.55f, 0.52f, 0.5f));
                case "mastiff": return ("Husky", 1f, new Color(0.62f, 0.48f, 0.34f));
                case "giant_spider": return ("Spider", 1f, null);
                case "giant_rat": return ("Rat", 1f, null);
                case "deer": return ("Deer", 1f, null);
                case "fox": return ("Fox", 1f, null);
                case "goblin": return ("Goblin", 1f, null);
                case "ogre": return ("Giant", 1f, null);
                case "troll": return ("Giant", 0.95f, new Color(0.48f, 0.6f, 0.45f));
                case "cattle": return ("Cow", 1f, null);
                case "riding_horse": return ("Horse", 1f, null);
                case "draft_horse": return ("Horse", 1.12f, new Color(0.45f, 0.36f, 0.28f));
                case "sheep": return ("Sheep", 1f, null);
                case "chicken": return ("Chicken", 1f, null);
                case "black_bear": return ("BlackBear", 1f, null);
                case "brown_bear": return ("BlackBear", 1.25f, new Color(0.5f, 0.36f, 0.24f));
                case "boar": return ("Boar", 1f, null);
                case "goat": return ("Goat", 1f, null);
                default: return (null, 1f, null);
            }
        }

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

            // Phase 18: beasts and monsters — the repository's creature models (Quaternius CC0; Poly by Google CC BY 3.0), each
            // with its own clips where it has them; a static model tips over when it falls.
            var beast = AnimalFor(creature);
            if (beast.Model != null && catalog.Animal(beast.Model) != null)
            {
                var controller = catalog.AnimalController(beast.Model);
                var body = Object.Instantiate(catalog.Animal(beast.Model), parent, false);
                body.name = "Body " + beast.Model;
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = catalog.Animal(beast.Model).transform.localRotation; // the import's own root turn (the Poly by Google models stand on it)
                body.transform.localScale = Vector3.one * beast.Scale;
                var anim = body.AddComponent<CombatantAnimator>();
                if (controller != null)
                {
                    var a = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
                    a.runtimeAnimatorController = controller;
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anim.Bind(a, drivesSpeed: true);
                }
                else anim.Bind(null, drivesSpeed: false);

                if (beast.Tint.HasValue) Tint(body, beast.Tint.Value);
                return new Body { Root = body, Animator = anim, Source = (controller != null ? "animated " : "static ") + beast.Model };
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
