using System;
using System.Collections.Generic;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Assembles a humanoid body from an <see cref="NpcAppearance"/>: the clothed body (outfit) with
    /// its humanoid rig, the cut-out head with eyes and brows, hair and beard, all skinned to the
    /// body's one skeleton by bone name, tinted per appearance, and animated by the shared
    /// locomotion controller. The body carries no identity: whoever owns it (a resident view keyed by
    /// NPC ID, or the player character) drives it.
    /// </summary>
    public static class HumanoidCharacterBuilder
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>Builds the body under <paramref name="parent"/>, or returns null if the catalog lacks the parts.</summary>
        public static CharacterAnimationDriver Build(Transform parent, NpcAppearance appearance, ArtCatalog catalog)
        {
            if (catalog == null || !catalog.HasCharacters || appearance == null) return null;
            var bodyPrefab = catalog.Body(appearance.BodyId) ?? catalog.Body("Male_Peasant");
            if (bodyPrefab == null) return null;

            var body = Object.Instantiate(bodyPrefab, parent, false);
            body.name = "Body " + appearance.BodyId;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * appearance.HeightScale;

            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            var skin = Tint(NpcAppearanceResolver.SkinTones[appearance.SkinTone]);
            var hair = Tint(NpcAppearanceResolver.HairColours[appearance.HairColour]);
            var cloth = Tint(NpcAppearanceResolver.OutfitTints[appearance.OutfitTint]);
            var outfit = catalog.Material(appearance.OutfitMaterialId);

            // Outfit: swap in the variant texture and tint the cloth and the skin parts.
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var isSkin = mats[i].name.StartsWith("M_Body", StringComparison.Ordinal);
                    if (!isSkin && outfit != null) mats[i] = outfit;
                }

                r.sharedMaterials = mats;
                for (var i = 0; i < mats.Length; i++)
                {
                    var isSkin = mats[i] != null && mats[i].name.StartsWith("M_Body", StringComparison.Ordinal);
                    SetColour(r, i, isSkin ? skin : cloth);
                }

                Tune(r);
            }

            Attach(catalog.Part(appearance.HeadId), body.transform, bones, (r, i, m) => m != null && m.name.StartsWith("M_Hair", StringComparison.Ordinal) ? hair : m != null && m.name.StartsWith("M_Head", StringComparison.Ordinal) ? skin : Color.white);
            if (appearance.HairId != null) Attach(catalog.Part(appearance.HairId), body.transform, bones, (r, i, m) => hair);
            if (appearance.BeardId != null) Attach(catalog.Part(appearance.BeardId), body.transform, bones, (r, i, m) => hair);

            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) animator.avatar = FallbackAvatar(catalog, appearance.Frame);
            animator.runtimeAnimatorController = catalog.Locomotion;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // few bodies; never shows a bind pose when first seen
            var driver = body.AddComponent<CharacterAnimationDriver>();
            driver.Initialize(animator);
            return driver;
        }

        /// <summary>
        /// A valid humanoid avatar from another body of the same frame (all bodies share one skeleton).
        /// Unity's auto-mapping rejects one of the CC0 bodies (Female_Peasant); it borrows its sibling's.
        /// </summary>
        public static Avatar FallbackAvatar(ArtCatalog catalog, BodyFrame frame)
        {
            var prefix = frame == BodyFrame.Female ? "Female_" : "Male_";
            foreach (var entry in catalog.Bodies)
            {
                if (entry?.Prefab == null || !entry.Id.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var a = entry.Prefab.GetComponent<Animator>();
                if (a != null && a.avatar != null && a.avatar.isValid && a.avatar.isHuman) return a.avatar;
            }

            return null;
        }

        /// <summary>Copies each skinned renderer of a part onto the body's skeleton (bones matched by name).</summary>
        private static void Attach(GameObject part, Transform body, Dictionary<string, Transform> bones, Func<SkinnedMeshRenderer, int, Material, Color> colour) =>
            Attach(part, body, bones, colour, null, null);

        /// <summary>
        /// Copies the skinned renderers of <paramref name="part"/> accepted by <paramref name="include"/> onto the body's
        /// skeleton, optionally with every material replaced by <paramref name="material"/>. Returns the copies.
        /// </summary>
        internal static List<SkinnedMeshRenderer> Attach(GameObject part, Transform body, Dictionary<string, Transform> bones,
            Func<SkinnedMeshRenderer, int, Material, Color> colour, Func<SkinnedMeshRenderer, bool> include, Material material)
        {
            var made = new List<SkinnedMeshRenderer>();
            if (part == null) return made;
            foreach (var src in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (src.sharedMesh == null || include != null && !include(src)) continue;
                var go = new GameObject(part.name + " " + src.name);
                go.transform.SetParent(body, false);
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = src.sharedMesh;
                var materials = src.sharedMaterials;
                if (material != null) for (var m = 0; m < materials.Length; m++) if (materials[m] == null || !materials[m].name.StartsWith("M_Body", StringComparison.Ordinal)) materials[m] = material;
                r.sharedMaterials = materials;
                var mapped = new Transform[src.bones.Length];
                for (var i = 0; i < mapped.Length; i++)
                {
                    var b = src.bones[i];
                    mapped[i] = b != null && bones.TryGetValue(b.name, out var t) ? t : body;
                }

                r.bones = mapped;
                r.rootBone = src.rootBone != null && bones.TryGetValue(src.rootBone.name, out var root) ? root : mapped.Length > 0 ? mapped[0] : body;
                r.localBounds = src.localBounds;
                for (var i = 0; i < r.sharedMaterials.Length; i++) SetColour(r, i, colour(r, i, r.sharedMaterials[i]));
                Tune(r);
                made.Add(r);
            }

            return made;
        }

        internal static void Colour(Renderer r, int index, Color colour) => SetColour(r, index, colour);

        internal static Color TintOf((float r, float g, float b) c) => Tint(c);

        private static void Tune(SkinnedMeshRenderer r)
        {
            r.updateWhenOffscreen = false;
            r.skinnedMotionVectors = false;
            r.quality = SkinQuality.Bone2;
        }

        private static void SetColour(Renderer r, int index, Color colour)
        {
            if (colour == Color.white) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block, index);
            block.SetColor(BaseColor, colour);
            r.SetPropertyBlock(block, index);
        }

        private static Color Tint((float r, float g, float b) c) => new Color(c.r, c.g, c.b, 1f);
    }

    /// <summary>
    /// Drives a humanoid body's animator from what its owner actually does: planar speed measured
    /// from the owner's movement (idle, walk, jog, sprint), turning in place (a slow step), and
    /// talking. Presentation only; it never moves anything.
    /// </summary>
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int TalkingId = Animator.StringToHash("Talking");
        private static readonly int ActivityId = Animator.StringToHash("Activity");

        private Animator _animator;
        private Transform _owner;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _talkTimer;
        private bool _initialised;
        private bool _hasActivity;

        public Animator Animator => _animator;

        /// <summary>Smoothed planar speed in m/s, as fed to the animator.</summary>
        public float Speed { get; private set; }

        public bool Talking => _talkTimer > 0f;

        /// <summary>
        /// The pose held while standing still (Phase 5): 0 idle, 1 working (kneeling at a task), 2 kneeling in
        /// prayer, 3 chatting. Set by the owner from what the person is doing; walking always wins.
        /// </summary>
        public int Pose { get; set; }

        public void Initialize(Animator animator)
        {
            _animator = animator;
            _owner = transform.parent != null ? transform.parent : transform;
            foreach (var p in animator.parameters) if (p.nameHash == ActivityId) _hasActivity = true;
            _lastPosition = _owner.position;
            _lastYaw = _owner.eulerAngles.y;
            _initialised = true;
        }

        /// <summary>Plays the talking animation for a while (the owner calls this when a conversation happens).</summary>
        public void Talk(float seconds) => _talkTimer = Mathf.Max(_talkTimer, seconds);

        private void LateUpdate()
        {
            if (!_initialised || _animator == null) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            var position = _owner.position;
            var delta = position - _lastPosition;
            delta.y = 0f;
            var measured = delta.magnitude / dt;
            if (measured > 25f) measured = 0f; // a teleport (placement, perspective change), not movement
            var yaw = _owner.eulerAngles.y;
            var turnRate = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / dt;
            _lastPosition = position;
            _lastYaw = yaw;

            // Turning on the spot reads as small steps (the CC0 library has no dedicated turn clip).
            var target = measured < 0.2f && turnRate > 60f ? 0.9f : measured;
            Speed = Mathf.Lerp(Speed, target, 1f - Mathf.Exp(-12f * dt));
            if (Speed < 0.02f) Speed = 0f;
            _animator.SetFloat(SpeedId, Speed);
            if (_talkTimer > 0f) _talkTimer -= dt;
            _animator.SetBool(TalkingId, _talkTimer > 0f && Speed < 0.3f);
            if (_hasActivity) _animator.SetInteger(ActivityId, Speed < 0.3f && _talkTimer <= 0f ? Pose : 0);
        }
    }
}
