using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>What a combatant visibly does (Phase 15). Chosen from what the combat log says happened.</summary>
    public enum CombatMotion
    {
        Attack1H,
        Attack2H,
        AttackUnarmed,
        AttackRanged,
        AttackBow,
        Cast,
        Block,
        Hit,
        Interact,
        Jump,
    }

    /// <summary>
    /// Phase 15: the one animation interface for every animated body — the player, people, skeletons, local creature
    /// models. It feeds the shared controllers (<see cref="ArtCatalog.Combatant"/> and its overrides, and the extended
    /// locomotion controller) through parameter hashes computed once. Presentation only: it is told what happened after
    /// the engine decided it, and nothing here changes a result. Death holds: once dead, no other motion plays until the
    /// body is alive again. Each motion is recorded (for tests and captures).
    /// </summary>
    public sealed class CombatantAnimator : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int DeadId = Animator.StringToHash("Dead");

        private static readonly Dictionary<CombatMotion, int> Triggers = new Dictionary<CombatMotion, int>
        {
            [CombatMotion.Attack1H] = Animator.StringToHash("Attack1H"),
            [CombatMotion.Attack2H] = Animator.StringToHash("Attack2H"),
            [CombatMotion.AttackUnarmed] = Animator.StringToHash("AttackUnarmed"),
            [CombatMotion.AttackRanged] = Animator.StringToHash("AttackRanged"),
            [CombatMotion.AttackBow] = Animator.StringToHash("AttackBow"),
            [CombatMotion.Cast] = Animator.StringToHash("Cast"),
            [CombatMotion.Block] = Animator.StringToHash("Block"),
            [CombatMotion.Hit] = Animator.StringToHash("Hit"),
            [CombatMotion.Interact] = Animator.StringToHash("Interact"),
            [CombatMotion.Jump] = Animator.StringToHash("Jump"),
        };

        private Animator _animator;
        private readonly HashSet<int> _parameters = new HashSet<int>();
        private bool _drivesSpeed;

        public Animator Animator => _animator;

        public bool Dead { get; private set; }

        /// <summary>Every motion asked for, in order (tests and captures read this; it never replays).</summary>
        public List<CombatMotion> History { get; } = new List<CombatMotion>();

        /// <summary>
        /// Binds to an animator. <paramref name="drivesSpeed"/>: this component sets Speed (bodies it moves itself);
        /// false where <see cref="CharacterAnimationDriver"/> already measures it (the player and people).
        /// </summary>
        public void Bind(Animator animator, bool drivesSpeed)
        {
            _animator = animator;
            _drivesSpeed = drivesSpeed;
            Refresh();
        }

        /// <summary>Re-reads the controller's parameters (after the controller is swapped).</summary>
        public void Refresh()
        {
            _parameters.Clear();
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters) _parameters.Add(p.nameHash);
        }

        public bool Supports(CombatMotion m) => _animator != null && Triggers.TryGetValue(m, out var id) && _parameters.Contains(id);

        /// <summary>Plays a one-shot motion (not while dead). Returns whether the controller has it.</summary>
        public bool Play(CombatMotion motion)
        {
            if (Dead || !Supports(motion)) return false;
            if (motion != CombatMotion.Hit) foreach (var t in Triggers.Values) if (_parameters.Contains(t)) _animator.ResetTrigger(t);
            _animator.SetTrigger(Triggers[motion]);
            History.Add(motion);
            return true;
        }

        private Quaternion _standing = Quaternion.identity;

        /// <summary>Down or dead: the death motion, which nothing else replaces; alive again: back to standing.</summary>
        public void SetDead(bool dead)
        {
            if (Dead == dead) return;
            Dead = dead;
            if (_animator == null)
            {
                // Phase 18: a body without a rig (a static model) tips onto its side when it falls, and stands when it rises.
                if (dead) _standing = transform.localRotation; // the model's own import turn, kept for when it rises
                transform.localRotation = dead ? Quaternion.Euler(0f, 0f, 85f) * _standing : _standing;
                return;
            }

            if (!_parameters.Contains(DeadId)) return;
            if (dead) foreach (var t in Triggers.Values) if (_parameters.Contains(t)) _animator.ResetTrigger(t);
            _animator.SetBool(DeadId, dead);
        }

        /// <summary>Walking speed in m/s for bodies this component moves (markers walking their squares).</summary>
        public void SetSpeed(float metresPerSecond)
        {
            if (_drivesSpeed && _animator != null && _parameters.Contains(SpeedId)) _animator.SetFloat(SpeedId, metresPerSecond);
        }

        /// <summary>True when the animator is in (or entering) a state with this tag: what a test can check really plays.</summary>
        public bool InState(string tag)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return false;
            return _animator.GetCurrentAnimatorStateInfo(0).IsTag(tag) || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsTag(tag));
        }
    }
}
