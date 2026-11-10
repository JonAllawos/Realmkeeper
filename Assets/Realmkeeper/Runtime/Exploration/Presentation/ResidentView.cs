using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Simulation.Npcs;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The visible body of one named resident, keyed by the resident's persistent
    /// <see cref="NpcId"/>. It holds no copy of the resident's state: its name and role are read
    /// from the session, and talking dispatches <see cref="TalkToResidentCommand"/>, so the meeting
    /// is recorded in world data and survives saving. Phase 5: where it stands and what it does come
    /// from the person's whereabouts this hour (<see cref="WorldSession.GetPresenceIn"/>), given to it
    /// by the controller as a plan; it walks a street route to the new spot, holds the pose of its
    /// activity there, and goes indoors (out of sight, and out of reach) at home or asleep.
    /// </summary>
    public sealed class ResidentView : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        public const float WalkSpeed = 1.8f;
        private const float FacePlayerDistance = 3.2f;

        private Vector3 _anchor;
        private float _wanderRadius;
        private Vector3 _target;
        private float _wait;
        private System.Random _random;
        private WorldSession _session;
        private Transform _player;
        private readonly List<Vector3> _route = new List<Vector3>();
        private int _routeIndex;
        private bool _hideOnArrival;
        private float? _facing;
        private GameObject _body;
        private Collider _collider;

        public NpcId NpcId { get; private set; }
        public NpcRole Role { get; private set; }
        public string DisplayName { get; private set; }

        /// <summary>How the resident looks, resolved from its NPC ID, role and name (never stored).</summary>
        public NpcAppearance Appearance { get; private set; }

        /// <summary>The humanoid body's animation driver, or null for the primitive fallback.</summary>
        public CharacterAnimationDriver Animation { get; private set; }

        /// <summary>What the world says the person is doing this hour (shown while they are there).</summary>
        public ActivityKind Activity { get; private set; } = ActivityKind.Standing;

        /// <summary>Why (e.g. "Working hours."), from the world.</summary>
        public string ActivityReason { get; private set; } = string.Empty;

        /// <summary>The place the world says they are this hour.</summary>
        public LocationId PlaceId { get; private set; }

        /// <summary>Indoors at home or asleep: not drawn and not approachable.</summary>
        public bool IsHidden { get; private set; }

        public bool IsWalking => _route.Count > 0;

        /// <summary>The spot the current plan ends at (settlement coordinates).</summary>
        public Vector3 Destination => _anchor;

        public bool IsAvailable => !IsHidden;

        public string Prompt => $"Talk to {DisplayName} ({RoleName(Role)} · {(IsWalking ? "walking" : ActivityText(Activity))})";
        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => transform.position;

        public static ResidentView Create(Transform parent, LayoutResidentSpot spot, NpcSummary npc, WorldSession session, Transform player,
            ExplorationMaterials materials, ArtCatalog catalog = null)
        {
            var go = new GameObject("Resident " + npc.NpcId);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(spot.Position.X, 0f, spot.Position.Z);
            var view = go.AddComponent<ResidentView>();
            view.NpcId = npc.NpcId;
            view.Role = npc.Role;
            view.DisplayName = npc.Name;
            view._anchor = go.transform.localPosition;
            view._wanderRadius = spot.WanderRadius;
            view._target = view._anchor;
            view._session = session;
            view._player = player;
            view._random = new System.Random(npc.NpcId.GetHashCode());
            view._wait = (float)view._random.NextDouble() * 3f;

            view._body = new GameObject("Body");
            view._body.transform.SetParent(go.transform, false);
            view.Appearance = NpcAppearanceResolver.Resolve(npc.NpcId, npc.Role, npc.Name,
                npc.Sex == NpcSex.Female ? BodyFrame.Female : npc.Sex == NpcSex.Male ? BodyFrame.Male : (BodyFrame?)null);
            view.Animation = HumanoidCharacterBuilder.Build(view._body.transform, view.Appearance, catalog);
            if (view.Animation == null)
            {
                var body = SettlementEnvironmentBuilder.Primitive(view._body.transform, PrimitiveType.Capsule, "Body",
                    new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.85f, 0.6f), materials.Get(RoleColour(npc.Role)));
                SettlementEnvironmentBuilder.RemoveCollider(body);
                var head = SettlementEnvironmentBuilder.Primitive(view._body.transform, PrimitiveType.Sphere, "Head",
                    new Vector3(0f, 1.62f, 0f), new Vector3(0.34f, 0.34f, 0.34f), materials.Get(new Color(0.80f, 0.64f, 0.52f)));
                SettlementEnvironmentBuilder.RemoveCollider(head);
            }

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.3f;
            view._collider = collider;
            return view;
        }

        /// <summary>
        /// Gives the resident where to be and what to do (from the world's whereabouts this hour). With
        /// <paramref name="snap"/> they are placed there at once (arrival in the settlement, a jump in
        /// time); otherwise they walk <paramref name="route"/> (settlement coordinates) to the spot.
        /// </summary>
        public void Plan(LocationId place, ActivityKind activity, string reason, Vector3 spot, float wander, float? facing,
            IReadOnlyList<Vector3> route, bool hideOnArrival, bool snap)
        {
            PlaceId = place;
            Activity = activity;
            ActivityReason = reason ?? string.Empty;
            _anchor = spot;
            _target = spot;
            _wanderRadius = wander;
            _facing = facing;
            _hideOnArrival = hideOnArrival;
            _route.Clear();
            _routeIndex = 0;
            if (snap || route == null || route.Count < 2)
            {
                transform.localPosition = spot;
                if (facing.HasValue) transform.localRotation = Quaternion.Euler(0f, facing.Value, 0f);
                SetHidden(hideOnArrival);
                UpdatePose();
                return;
            }

            if (IsHidden)
            {
                transform.localPosition = route[0]; // comes out of their door
                SetHidden(false);
            }

            _route.AddRange(route);
            _routeIndex = 1;
            UpdatePose();
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            var result = session.Dispatch(new TalkToResidentCommand(NpcId));
            if (player != null) FaceTowards(player.position);
            if (result.IsSuccess) Animation?.Talk(4f);
            return new InteractionResponse(DisplayName, result.IsSuccess ? result.Message : Describe(result), Kind, result);
        }

        private void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            if (_body != null) _body.SetActive(!hidden);
            if (_collider != null) _collider.enabled = !hidden;
        }

        private void UpdatePose()
        {
            if (Animation == null) return;
            Animation.Pose = IsWalking ? 0 : PoseFor(Activity);
        }

        /// <summary>The held pose for an activity: kneeling at work, kneeling at prayer, chatting; otherwise standing.</summary>
        public static int PoseFor(ActivityKind activity)
        {
            switch (activity)
            {
                case ActivityKind.Working: return 1;
                case ActivityKind.Worshipping: return 2;
                case ActivityKind.Socialising: return 3;
                default: return 0;
            }
        }

        public static string ActivityText(ActivityKind a)
        {
            switch (a)
            {
                case ActivityKind.Socialising: return "socialising";
                case ActivityKind.Worshipping: return "at prayer";
                case ActivityKind.None: return "here";
                default: return a.ToString().ToLowerInvariant();
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (IsHidden) return;
            if (_route.Count > 0)
            {
                Walk(dt);
                return;
            }

            if (_player != null && (_player.position - transform.position).sqrMagnitude < FacePlayerDistance * FacePlayerDistance)
            {
                FaceTowards(_player.position);
                return; // stand still while the player is close
            }

            if (_wanderRadius < 0.6f || Activity == ActivityKind.Working || Activity == ActivityKind.Worshipping || Activity == ActivityKind.Trading)
            {
                // At their post: hold the pose, facing their work.
                if (_facing.HasValue) transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.Euler(0f, _facing.Value, 0f), 240f * dt);
                return;
            }

            if (_wait > 0f)
            {
                _wait -= dt;
                return;
            }

            var position = transform.localPosition;
            var to = _target - position;
            to.y = 0f;
            if (to.magnitude < 0.1f)
            {
                var angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                var radius = (float)_random.NextDouble() * _wanderRadius;
                _target = _anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _wait = 2f + (float)_random.NextDouble() * 4f;
                return;
            }

            var step = to.normalized * Mathf.Min(1.2f * dt, to.magnitude);
            transform.localPosition = position + step;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(to.normalized, Vector3.up), 360f * dt);
        }

        private void Walk(float dt)
        {
            var budget = WalkSpeed * dt;
            while (budget > 0f && _routeIndex < _route.Count)
            {
                var position = transform.localPosition;
                var to = _route[_routeIndex] - position;
                to.y = 0f;
                var distance = to.magnitude;
                if (distance < 1e-3f)
                {
                    _routeIndex++;
                    continue;
                }

                var step = Mathf.Min(budget, distance);
                transform.localPosition = position + to / distance * step;
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(to / distance, Vector3.up), 540f * dt);
                budget -= step;
                if (step >= distance) _routeIndex++;
            }

            if (_routeIndex < _route.Count) return;
            _route.Clear();
            _routeIndex = 0;
            _target = _anchor;
            if (_hideOnArrival) SetHidden(true);
            UpdatePose();
        }

        private void FaceTowards(Vector3 point)
        {
            var to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 360f * Time.deltaTime + 1f);
        }

        internal static string Describe(CommandResult result)
        {
            var text = result.Message;
            if (result.Errors.Count > 0) text += " " + string.Join(" ", result.Errors);
            return text;
        }

        public static string RoleName(NpcRole role) => role.ToString();

        private static Color RoleColour(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Elder: return new Color(0.42f, 0.30f, 0.50f);
                case NpcRole.Merchant: return new Color(0.62f, 0.44f, 0.18f);
                case NpcRole.Guard: return new Color(0.42f, 0.44f, 0.50f);
                case NpcRole.Farmer: return new Color(0.46f, 0.52f, 0.28f);
                case NpcRole.Smith: return new Color(0.30f, 0.26f, 0.24f);
                case NpcRole.Innkeeper: return new Color(0.56f, 0.28f, 0.22f);
                case NpcRole.Labourer: return new Color(0.50f, 0.42f, 0.32f);
                default: return new Color(0.40f, 0.46f, 0.56f);
            }
        }
    }
}
