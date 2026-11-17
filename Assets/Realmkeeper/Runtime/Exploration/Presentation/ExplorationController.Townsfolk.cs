using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 27 (ADR-0119): the settlement's unnamed population in its streets, in two tiers.
    /// <list type="bullet">
    /// <item><b>Abstract:</b> everyone <see cref="TownsfolkPlanner"/> plans out this hour (a share of the authoritative
    /// population, by hour and by the town's labour) has a position at every moment, from a pure function of time. Nothing
    /// is simulated frame by frame.</item>
    /// <item><b>Shown:</b> the nearest <see cref="TownsfolkShown"/> within <see cref="TownsfolkRange"/> metres get a full
    /// figure (the same humanoid bodies and appearance rules as named residents). Figures are kept per person and reused, so
    /// a person keeps their looks; beyond a small cache they are released.</item>
    /// </list>
    /// They have no records, no collider and no conversation (they are the counted, unnamed majority, ADR-0009); named
    /// residents are unchanged. The plan is renewed when the hour changes.
    /// </summary>
    public sealed partial class ExplorationController
    {
        /// <summary>Full figures shown at once (measured: see the Phase 27 performance report).</summary>
        public const int TownsfolkShown = 32;

        /// <summary>Metres within which a planned person is given a figure.</summary>
        public const float TownsfolkRange = 70f;

        private const int TownsfolkCache = 48;

        private readonly List<TownsfolkSpec> _townsfolk = new List<TownsfolkSpec>();
        private readonly Dictionary<string, Townsperson> _townsBodies = new Dictionary<string, Townsperson>();
        private readonly List<(float d2, int index)> _townsNear = new List<(float, int)>();
        private readonly HashSet<string> _townsWanted = new HashSet<string>();
        private readonly List<string> _townsDrop = new List<string>();
        private Transform _townsRoot;
        private GameObject _townsArea;
        private int _townsHour = -1;
        private float _townsAssignTimer;
        private float _townsClock;
        private readonly System.Diagnostics.Stopwatch _townsWatch = new System.Diagnostics.Stopwatch();

        /// <summary>The people planned out this hour (abstract tier).</summary>
        public IReadOnlyList<TownsfolkSpec> Townsfolk => _townsfolk;

        /// <summary>Figures currently shown (detailed tier).</summary>
        public int TownsfolkFigures { get; private set; }

        public double TownsfolkFrameMs { get; private set; }
        public double TownsfolkPlanMs { get; private set; }
        public int TownsfolkBuilt { get; private set; }

        /// <summary>The slowest single figure build so far (milliseconds).</summary>
        public double TownsfolkBuildMsMax { get; private set; }

        /// <summary>Whether the unnamed townsfolk are shown (for measuring the frame with and without them).</summary>
        public bool TownsfolkEnabled { get; set; } = true;

        private sealed class Townsperson
        {
            public GameObject Go;
            public CharacterAnimationDriver Animation;
            public TownsfolkSpec Spec;
            public bool Active;
            public float Yaw;
            public Renderer[] Renderers; // visual pass: found once, for the shadow switch
            public bool Shadows = true;
        }

        /// <summary>
        /// Visual pass (2026-11-16): townsfolk further than this from the camera cast no shadow. Each figure is several skinned
        /// parts, drawn again for each shadow cascade; a shadow this far off is a few pixels.
        /// </summary>
        public const float TownsfolkShadowMetres = 30f;

        private void UpdateTownsfolk()
        {
            var layout = _streamer.Current;
            var inTown = TownsfolkEnabled && !InOverland && !InPlace && layout != null && EnvironmentRoot != null && Player != null && _session != null;
            if (!inTown)
            {
                if (_townsArea != null) ClearTownsfolk();
                return;
            }

            _townsWatch.Restart();
            var time = _session.GetWorldTime();
            var hour = time.MinuteOfDay / 60;
            if (time == null) return;
            if (!ReferenceEquals(_townsArea, EnvironmentRoot) || hour != _townsHour) PlanTownsfolk(layout, time.MinuteOfDay, hour);

            _townsClock += Time.deltaTime;
            _townsAssignTimer -= Time.unscaledDeltaTime;
            if (_townsAssignTimer <= 0f)
            {
                _townsAssignTimer = 0.3f;
                AssignTownsfolk();
            }

            // Move the shown figures along their ways.
            var eye = CameraRig != null && CameraRig.Camera != null ? CameraRig.Camera.transform.position : Player.transform.position;
            var shadowReach2 = TownsfolkShadowMetres * TownsfolkShadowMetres;
            foreach (var t in _townsBodies.Values)
            {
                if (!t.Active) continue;
                var castShadow = (t.Go.transform.position - eye).sqrMagnitude < shadowReach2;
                if (castShadow != t.Shadows)
                {
                    t.Renderers ??= t.Go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in t.Renderers)
                        if (r != null) r.shadowCastingMode = castShadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    t.Shadows = castShadow;
                }

                var s = t.Spec;
                var p = TownsfolkPlanner.PositionAt(s, _townsClock, out var heading, out var walking);
                if (s.Activity == TownsfolkActivity.Playing)
                {
                    // Children at play: bursts of running round their spot, then a pause.
                    var k = (s.Key.GetHashCode() & 0xffff) * 0.0001f;
                    var running = Mathf.Sin(_townsClock * 0.45f + k * 7f) > -0.2f;
                    var angle = _townsClock * (running ? 1.1f : 0f) + k * 40f;
                    p = new LocalPoint(p.X + Mathf.Cos(angle) * s.Wander, p.Z + Mathf.Sin(angle) * s.Wander);
                    heading = Mathf.Atan2(-Mathf.Sin(angle), Mathf.Cos(angle)) * Mathf.Rad2Deg; // along the circle
                    walking = running;
                }
                else if (s.Route.Count == 1 && s.Wander > 0f)
                {
                    // Standing about: a slow sway round their spot. Someone at work holds their place (a kneeling worker must not slide).
                    var (dx, dz) = s.Activity == TownsfolkActivity.Working ? (0f, 0f) : StandingSway(s.Key, s.Wander, _townsClock);
                    p = new LocalPoint(p.X + dx, p.Z + dz);
                    if (s.Facing.HasValue) t.Yaw = s.Facing.Value; // Phase 28: a gate guard looks out of the gate
                }

                var at = new Vector3(p.X, layout.GroundAt(p.X, p.Z), p.Z);
                t.Go.transform.localPosition = at;
                if (walking) t.Yaw = Mathf.MoveTowardsAngle(t.Yaw, heading, 360f * Time.deltaTime);
                t.Go.transform.localRotation = Quaternion.Euler(0f, t.Yaw, 0f);
                if (t.Animation != null) t.Animation.Pose = walking ? 0 : s.Activity == TownsfolkActivity.Working ? 1 : s.Activity == TownsfolkActivity.Gathering ? 3 : 0;
            }

            TownsfolkFrameMs = _townsWatch.Elapsed.TotalMilliseconds;
        }

        private void PlanTownsfolk(LocalEnvironmentLayout layout, int minute, int hour)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ClearTownsfolk(); // a new hour's people (figures are rebuilt a few at a time as they are needed)
            _townsArea = EnvironmentRoot;
            _townsHour = hour;
            if (_townsRoot == null)
            {
                _townsRoot = new GameObject("Townsfolk").transform;
                _townsRoot.SetParent(EnvironmentRoot.transform, false);
            }

            var life = _session.GetStreetLife(layout.SettlementId);
            _townsfolk.Clear();
            if (life != null)
            {
                var namedOut = 0;
                foreach (var r in _residents.Values) if (r != null && !r.IsHidden) namedOut++;
                var seed = Realmkeeper.Domain.Randomness.SeedMixer.Combine(Realmkeeper.Domain.Randomness.SeedMixer.HashString("townsfolk:" + layout.SettlementId), (ulong)_session.GetWorldTime().Date.ToDayNumber());
                _townsfolk.AddRange(TownsfolkPlanner.Plan(layout, new TownsfolkInput
                {
                    Population = life.Population, Type = life.Type, Prosperity = life.Prosperity, Labour = life.Labour, NamedOut = namedOut, MinuteOfDay = minute, Seed = seed,
                }));
            }

            // People no longer planned leave (their figures are released).
            var keys = new HashSet<string>();
            foreach (var s in _townsfolk) keys.Add(s.Key);
            _townsDrop.Clear();
            foreach (var pair in _townsBodies) if (!keys.Contains(pair.Key)) _townsDrop.Add(pair.Key);
            foreach (var k in _townsDrop) { Object.Destroy(_townsBodies[k].Go); _townsBodies.Remove(k); }
            _townsAssignTimer = 0f;
            TownsfolkPlanMs = watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>The nearest planned people get figures; the rest stay abstract. Figures are reused per person.</summary>
        private void AssignTownsfolk()
        {
            var player = _townsRoot.InverseTransformPoint(Player.transform.position);
            _townsNear.Clear();
            for (var i = 0; i < _townsfolk.Count; i++)
            {
                var p = TownsfolkPlanner.PositionAt(_townsfolk[i], _townsClock, out _, out _);
                var dx = p.X - player.x;
                var dz = p.Z - player.z;
                var d2 = dx * dx + dz * dz;
                if (d2 < TownsfolkRange * TownsfolkRange) _townsNear.Add((d2, i));
            }

            _townsNear.Sort((a, b) => a.d2.CompareTo(b.d2));
            _townsWanted.Clear();
            for (var n = 0; n < _townsNear.Count && n < TownsfolkShown; n++) _townsWanted.Add(_townsfolk[_townsNear[n].index].Key);

            foreach (var t in _townsBodies.Values)
            {
                var want = _townsWanted.Contains(t.Spec.Key);
                if (t.Active != want) { t.Active = want; t.Go.SetActive(want); }
            }

            var built = 0;
            for (var n = 0; n < _townsNear.Count && n < TownsfolkShown; n++)
            {
                var spec = _townsfolk[_townsNear[n].index];
                if (_townsBodies.ContainsKey(spec.Key)) continue;
                if (built++ >= 2) break; // at most two new figures each 0.3 s (about 2 ms each), so a new hour or a street corner never stalls a frame
                var go = new GameObject("Townsperson " + spec.Key);
                go.transform.SetParent(_townsRoot, false);
                var body = new GameObject("Body");
                body.transform.SetParent(go.transform, false);
                var looks = NpcAppearanceResolver.ResolveUnnamed(_streamer.Current.SettlementId + "/" + spec.Key + "/" + _townsHour, spec.Look);
                var buildWatch = System.Diagnostics.Stopwatch.StartNew();
                var anim = HumanoidCharacterBuilder.Build(body.transform, looks, Catalog);
                if (spec.Child) MakeChild(go.transform, anim); // Phase 27: the same bodies at a child's size, the head a little larger
                if (spec.Look == Realmkeeper.Domain.World.NpcRole.Guard) // Phase 27: the watch dressed in their lord's colour (the strategic map's)
                    HumanoidCharacterBuilder.TintCloth(body, Color.Lerp(Color.white, Realmkeeper.UI.RkFactionColours.For(_streamer.Current.OwnerIndex), 0.75f));
                TownsfolkBuildMsMax = System.Math.Max(TownsfolkBuildMsMax, buildWatch.Elapsed.TotalMilliseconds);
                _townsBodies[spec.Key] = new Townsperson { Go = go, Animation = anim, Spec = spec, Active = true };
                TownsfolkBuilt++;
            }

            // Release the cache beyond its size: inactive figures first, farthest first.
            if (_townsBodies.Count > TownsfolkCache)
            {
                _townsDrop.Clear();
                foreach (var pair in _townsBodies) if (!pair.Value.Active) _townsDrop.Add(pair.Key);
                for (var i = 0; i < _townsDrop.Count && _townsBodies.Count > TownsfolkCache; i++)
                {
                    Object.Destroy(_townsBodies[_townsDrop[i]].Go);
                    _townsBodies.Remove(_townsDrop[i]);
                }
            }

            var shown = 0;
            foreach (var t in _townsBodies.Values) if (t.Active) shown++;
            TownsfolkFigures = shown;
        }

        /// <summary>
        /// The offset of someone standing about from their spot at a moment: a slow drift within half their wander radius.
        /// The per-person phase is kept in [0, 2π): it was once the key's hash × 0.001 (up to about two million), where a
        /// float's steps (about 0.1) made the figure stand frozen and then jump a few centimetres in one frame. Each jump read
        /// as a burst of speed, so kneeling workers stood up and knelt again and people standing about twitched (2026-11-16).
        /// </summary>
        public static (float dx, float dz) StandingSway(string key, float wander, float clock)
        {
            var phase = (key.GetHashCode() & 0xffff) / 65536f * (2f * Mathf.PI);
            return (Mathf.Sin(clock * 0.13f + phase) * wander * 0.5f, Mathf.Cos(clock * 0.11f + phase) * wander * 0.5f);
        }

        /// <summary>A child: the shared humanoid body at about 1.1 m, its head scaled up for a child's proportions.</summary>
        public const float ChildScale = 0.62f;

        private static void MakeChild(Transform figure, CharacterAnimationDriver anim)
        {
            figure.localScale = Vector3.one * ChildScale;
            var head = anim != null && anim.Animator != null && anim.Animator.isHuman ? anim.Animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null) head.localScale *= 1.25f;
        }

        private void ClearTownsfolk()
        {
            foreach (var t in _townsBodies.Values) if (t.Go != null) Object.Destroy(t.Go);
            _townsBodies.Clear();
            _townsfolk.Clear();
            if (_townsRoot != null) Object.Destroy(_townsRoot.gameObject);
            _townsRoot = null;
            _townsArea = null;
            _townsHour = -1;
            TownsfolkFigures = 0;
        }
    }
}
