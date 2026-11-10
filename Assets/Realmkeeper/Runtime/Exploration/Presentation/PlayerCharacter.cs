using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The player character's body in the local environment: one <see cref="CharacterController"/>
    /// shared by both camera perspectives, so switching perspective never moves the character.
    /// Movement is relative to the camera's heading. Handles collision, ground detection,
    /// gravity and jumping. Its position is presentation state; it reaches world data only through
    /// <c>SetPlayerPoseCommand</c> (on save, on perspective switch and on exit).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        public const float Height = 1.8f;
        public const float Radius = 0.35f;
        public const float WalkSpeed = 3.5f;
        public const float SprintSpeed = 6f;
        public const float JumpHeight = 1.1f;
        public const float Gravity = -20f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private GameObject _body;

        public IExplorationInput Input { get; set; }

        /// <summary>Yaw (degrees) of the camera; movement is relative to it.</summary>
        public float Heading { get; set; }

        public bool IsGrounded { get; private set; }

        public bool MovementEnabled { get; set; } = true;

        /// <summary>First person: the body always faces the view heading (strafing instead of turning).</summary>
        public bool AlignToHeading { get; set; }

        public CharacterController Controller => _controller;

        /// <summary>The visible body (hidden in first person so it does not block the view).</summary>
        public GameObject Body => _body;

        /// <summary>The humanoid body's animation driver, or null when the body is the primitive fallback.</summary>
        public CharacterAnimationDriver Animation { get; private set; }

        public static PlayerCharacter Create(Transform parent, Material bodyMaterial, Material headMaterial, ArtCatalog catalog = null)
        {
            var go = new GameObject("Player Character");
            go.transform.SetParent(parent, false);
            var cc = go.AddComponent<CharacterController>();
            cc.height = Height;
            cc.radius = Radius;
            cc.center = new Vector3(0f, Height / 2f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            // Unity ignores moves shorter than minMoveDistance (default 0.001 m). At high frame rates (a light
            // scene can render thousands of frames a second) every step is shorter than that and the
            // character would not move at all. Unity recommends 0.
            cc.minMoveDistance = 0f;
            var player = go.AddComponent<PlayerCharacter>();
            player._controller = cc;

            player._body = new GameObject("Body");
            player._body.transform.SetParent(go.transform, false);
            player.Animation = HumanoidCharacterBuilder.Build(player._body.transform, NpcAppearanceResolver.Player(), catalog);
            if (player.Animation != null) return player; // a humanoid body; the primitives below are the fallback

            var torso = SettlementEnvironmentBuilder.Primitive(player._body.transform, PrimitiveType.Capsule, "Torso",
                new Vector3(0f, 0.8f, 0f), new Vector3(0.62f, 0.8f, 0.62f), bodyMaterial);
            SettlementEnvironmentBuilder.RemoveCollider(torso);
            var head = SettlementEnvironmentBuilder.Primitive(player._body.transform, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 1.6f, 0f), new Vector3(0.36f, 0.36f, 0.36f), headMaterial);
            SettlementEnvironmentBuilder.RemoveCollider(head);
            var pack = SettlementEnvironmentBuilder.Primitive(player._body.transform, PrimitiveType.Cube, "Pack",
                new Vector3(0f, 1.05f, -0.28f), new Vector3(0.4f, 0.45f, 0.2f), headMaterial);
            SettlementEnvironmentBuilder.RemoveCollider(pack);
            return player;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>Places the character exactly (disables the controller so the move is not swept).</summary>
        public void Teleport(LocalPose pose)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(new Vector3(pose.X, pose.Y, pose.Z), Quaternion.Euler(0f, pose.Yaw, 0f));
            _controller.enabled = true;
            _verticalVelocity = 0f;
            Heading = pose.Yaw;
        }

        /// <summary>The character's pose as world data stores it.</summary>
        public LocalPose CurrentPose
        {
            get
            {
                var p = transform.position;
                return new LocalPose(p.x, p.y, p.z, Mathf.Repeat(transform.eulerAngles.y, 360f));
            }
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>One movement step (public so tests can drive it with a fixed time step).</summary>
        public void Step(float deltaTime)
        {
            if (_controller == null || !_controller.enabled || deltaTime <= 0f) return;
            var move = MovementEnabled && Input != null ? Input.Move : Vector2.zero;
            var sprint = MovementEnabled && Input != null && Input.Sprint;
            var jump = MovementEnabled && Input != null && Input.JumpPressed;

            var heading = Quaternion.Euler(0f, Heading, 0f);
            var planar = heading * new Vector3(move.x, 0f, move.y);
            var speed = sprint ? SprintSpeed : WalkSpeed;

            IsGrounded = _controller.isGrounded;
            if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // keep contact on slopes and steps
            if (IsGrounded && jump) _verticalVelocity = Mathf.Sqrt(2f * -Gravity * JumpHeight);
            _verticalVelocity += Gravity * deltaTime;

            var flags = _controller.Move((planar * speed + Vector3.up * _verticalVelocity) * deltaTime);
            IsGrounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;

            if (AlignToHeading)
            {
                FaceHeading();
            }
            else if (planar.sqrMagnitude > 1e-4f)
            {
                var target = Quaternion.LookRotation(planar.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * deltaTime);
            }
        }

        /// <summary>Turns the character to face the camera heading (first person keeps body and view aligned).</summary>
        public void FaceHeading()
        {
            transform.rotation = Quaternion.Euler(0f, Heading, 0f);
        }
    }
}
