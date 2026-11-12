using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15 (ADR-0073): the visible body of a party member in a settlement, keyed by their persistent
    /// <see cref="NpcId"/>. It holds no state of the person (the party and the sheet are the world's); it only walks.
    /// Following: it keeps a formation slot behind the player (its order and follow distance), runs to catch up, steps
    /// aside from the others, and moves with a <see cref="CharacterController"/> so walls and ground are respected.
    /// Waiting: it stands where it was told to. It is never teleported except by an explicit, counted recovery when it
    /// is stuck or far behind, to a free spot on the ground behind the player. Talking to it opens a conversation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CompanionFollower : MonoBehaviour, IInteractable
    {
        public const float WalkSpeed = 1.9f;
        public const float JogSpeed = 3.8f;
        public const float RunSpeed = 6.4f;

        /// <summary>Beyond this distance from its slot a follower is recovered (it cannot have kept up).</summary>
        public const float RecoverBeyond = 35f;

        /// <summary>Seconds without progress while away from its slot before it is recovered.</summary>
        public const float StuckSeconds = 3f;

        private CharacterController _controller;
        private float _fall;
        private float _stuck;
        private float _lastDistance = float.MaxValue;
        private GameObject _body;

        public NpcId NpcId { get; private set; }
        public string DisplayName { get; private set; }
        public CharacterAnimationDriver Animation { get; private set; }
        public FollowMode Mode { get; set; }

        /// <summary>Formation order (0 first behind the player) and how far behind they keep (metres).</summary>
        public int Order { get; set; }

        public float FollowDistance { get; set; } = 2.5f;

        /// <summary>How many times this body had to be recovered (explicit and counted, never silent).</summary>
        public int Recoveries { get; private set; }

        /// <summary>The ground point this follower is heading for (for tests and captures).</summary>
        public Vector3 Slot { get; private set; }

        public string Prompt => $"Talk to {DisplayName} (companion · {(Mode == FollowMode.Wait ? "waiting" : "following")})";
        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => transform.position;

        /// <summary>Not used: the controller opens the conversation itself (a companion is talked to in full dialogue).</summary>
        public InteractionResponse Interact(WorldSession session, Transform player) => null;

        public static CompanionFollower Create(Transform parent, NpcSummary npc, Vector3 at, float yaw, ArtCatalog catalog, ExplorationMaterials materials)
        {
            var go = new GameObject("Companion " + npc.NpcId);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            var f = go.AddComponent<CompanionFollower>();
            f._controller = cc;
            f.NpcId = npc.NpcId;
            f.DisplayName = npc.Name;
            f._body = new GameObject("Body");
            f._body.transform.SetParent(go.transform, false);
            var look = NpcAppearanceResolver.Resolve(npc.NpcId, npc.Role, npc.Name,
                npc.Sex == NpcSex.Female ? BodyFrame.Female : npc.Sex == NpcSex.Male ? BodyFrame.Male : (BodyFrame?)null);
            f.Animation = catalog != null ? HumanoidCharacterBuilder.Build(f._body.transform, look, catalog) : null;
            if (f.Animation == null && materials != null)
            {
                var body = SettlementEnvironmentBuilder.Primitive(f._body.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.85f, 0.6f),
                    materials.Get(new Color(0.30f, 0.45f, 0.30f)));
                SettlementEnvironmentBuilder.RemoveCollider(body);
            }

            return f;
        }

        /// <summary>Shows or hides the body (a fight draws its own figures; the follower waits out of sight meanwhile).</summary>
        public void SetShown(bool shown)
        {
            if (_body != null && _body.activeSelf != shown) _body.SetActive(shown);
            if (_controller != null) _controller.enabled = shown;
        }

        /// <summary>
        /// One step of following. <paramref name="others"/> are the other followers (for spacing). Returns true when this
        /// step was a recovery.
        /// </summary>
        public bool Step(Transform player, float playerSpeed, System.Collections.Generic.IReadOnlyList<CompanionFollower> others, float dt)
        {
            if (player == null || _controller == null || !_controller.enabled || dt <= 0f) return false;
            Slot = SlotFor(player, Order, FollowDistance);
            var pos = transform.position;
            var toSlot = Slot - pos;
            toSlot.y = 0f;
            var distance = toSlot.magnitude;

            if (Mode == FollowMode.Wait)
            {
                Fall(dt, Vector3.zero);
                Face(player.position - pos, dt * 0.5f);
                return false;
            }

            // Far behind, or stuck (trying to move but held by a wall) while away from the slot: an explicit, counted
            // recovery to the ground behind the player. Being slower than the player for a moment is not being stuck.
            _lastDistance = distance;
            if (distance > RecoverBeyond || (_stuck > StuckSeconds && distance > 3f))
            {
                Recover(player);
                return true;
            }

            var speed = distance > 6f ? Mathf.Max(RunSpeed, playerSpeed + 0.4f) : distance > 2f ? Mathf.Max(JogSpeed, playerSpeed) : distance > 0.35f ? WalkSpeed : 0f;
            var move = distance > 0.01f ? toSlot / distance * Mathf.Min(speed * dt, distance) : Vector3.zero;

            // Step aside from the others and from the player (no stacking in one spot).
            var push = Vector3.zero;
            foreach (var o in others)
            {
                if (o == null || o == this) continue;
                var away = pos - o.transform.position;
                away.y = 0f;
                var d = away.magnitude;
                if (d < 1.0f && d > 0.001f) push += away / d * (1.0f - d);
            }

            var fromPlayer = pos - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.magnitude < 1.1f && fromPlayer.magnitude > 0.001f) push += fromPlayer.normalized * (1.1f - fromPlayer.magnitude);
            move += push * (2.5f * dt);
            var before = transform.position;
            Fall(dt, move);
            var intended = new Vector2(move.x, move.z).magnitude;
            var moved = new Vector2(transform.position.x - before.x, transform.position.z - before.z).magnitude;
            if (intended > 0.4f * dt && moved < intended * 0.25f) _stuck += dt; // wanted to walk, could not
            else _stuck = Mathf.Max(0f, _stuck - 2f * dt);
            if (move.sqrMagnitude > 0.000001f) Face(move, dt);
            else Face(player.position - pos, dt * 0.5f);
            return false;
        }

        /// <summary>Where a follower of this order stands: behind the player, alternating sides, further back by order.</summary>
        public static Vector3 SlotFor(Transform player, int order, float followDistance)
        {
            var side = order % 2 == 0 ? -1f : 1f;
            var lateral = order == 0 ? 0.6f : side * 0.9f * ((order + 1) / 2);
            return player.position - player.forward * followDistance + player.right * lateral;
        }

        /// <summary>The recovery: onto the ground behind the player, clear of walls (checked with the body's own capsule).</summary>
        public void Recover(Transform player)
        {
            var want = SlotFor(player, Order, Mathf.Min(FollowDistance, 2.5f));
            var at = Ground(want, player.position);
            _controller.enabled = false;
            transform.position = at;
            _controller.enabled = true;
            _stuck = 0f;
            _lastDistance = float.MaxValue;
            _fall = 0f;
            Recoveries++;
            Debug.Log($"Realmkeeper: {DisplayName} could not keep up and rejoined the player (recovery {Recoveries}).");
        }

        private static Vector3 Ground(Vector3 want, Vector3 fallback)
        {
            foreach (var candidate in new[] { want, Vector3.Lerp(want, fallback, 0.5f), fallback })
            {
                var from = candidate + Vector3.up * 3f;
                if (Physics.Raycast(from, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore) &&
                    !Physics.CheckCapsule(hit.point + Vector3.up * 0.4f, hit.point + Vector3.up * 1.5f, 0.28f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point;
            }

            return fallback;
        }

        private void Fall(float dt, Vector3 move)
        {
            _fall = _controller.isGrounded ? -1f : _fall - 9.81f * dt;
            _controller.Move(move + Vector3.up * _fall * dt);
        }

        private void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Mathf.Clamp01(dt * 8f));
        }
    }
}
