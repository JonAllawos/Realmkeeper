using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The visible body of one named resident, keyed by the resident's persistent
    /// <see cref="NpcId"/>. It holds no copy of the resident's state: its name and role are read
    /// from the session, and talking dispatches <see cref="TalkToResidentCommand"/>, so the meeting
    /// is recorded in world data and survives saving. Idle behaviour (wandering near its spot,
    /// turning to face a nearby player) is presentation only.
    /// </summary>
    public sealed class ResidentView : MonoBehaviour, IInteractable
    {
        private const float WalkSpeed = 1.3f;
        private const float FacePlayerDistance = 3.2f;

        private Vector3 _anchor;
        private float _wanderRadius;
        private Vector3 _target;
        private float _wait;
        private System.Random _random;
        private WorldSession _session;
        private Transform _player;

        public NpcId NpcId { get; private set; }
        public NpcRole Role { get; private set; }
        public string DisplayName { get; private set; }

        /// <summary>How the resident looks, resolved from its NPC ID, role and name (never stored).</summary>
        public NpcAppearance Appearance { get; private set; }

        /// <summary>The humanoid body's animation driver, or null for the primitive fallback.</summary>
        public CharacterAnimationDriver Animation { get; private set; }

        public string Prompt => $"Talk to {DisplayName} ({RoleName(Role)})";
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

            view.Appearance = NpcAppearanceResolver.Resolve(npc.NpcId, npc.Role, npc.Name);
            view.Animation = HumanoidCharacterBuilder.Build(go.transform, view.Appearance, catalog);
            if (view.Animation == null)
            {
                var body = SettlementEnvironmentBuilder.Primitive(go.transform, PrimitiveType.Capsule, "Body",
                    new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.85f, 0.6f), materials.Get(RoleColour(npc.Role)));
                SettlementEnvironmentBuilder.RemoveCollider(body);
                var head = SettlementEnvironmentBuilder.Primitive(go.transform, PrimitiveType.Sphere, "Head",
                    new Vector3(0f, 1.62f, 0f), new Vector3(0.34f, 0.34f, 0.34f), materials.Get(new Color(0.80f, 0.64f, 0.52f)));
                SettlementEnvironmentBuilder.RemoveCollider(head);
            }

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.3f;
            return view;
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            var result = session.Dispatch(new TalkToResidentCommand(NpcId));
            if (player != null) FaceTowards(player.position);
            if (result.IsSuccess) Animation?.Talk(4f);
            return new InteractionResponse(DisplayName, result.IsSuccess ? result.Message : Describe(result), Kind, result);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_player != null && (_player.position - transform.position).sqrMagnitude < FacePlayerDistance * FacePlayerDistance)
            {
                FaceTowards(_player.position);
                return; // stand still while the player is close
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

            var step = to.normalized * Mathf.Min(WalkSpeed * dt, to.magnitude);
            transform.localPosition = position + step;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(to.normalized, Vector3.up), 360f * dt);
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
