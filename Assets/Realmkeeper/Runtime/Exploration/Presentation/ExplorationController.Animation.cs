using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15: animated bodies in fights. Each combatant body built by <see cref="CombatantBodies"/> has a
    /// <see cref="CombatantAnimator"/>; the player's body switches to the shared combat controller for the fight and
    /// back afterwards. Motions are chosen from what the combat log says happened (the weapon a line names, whether it
    /// struck from range), shown once per entry by the log's sequence. Nothing here changes a result.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private readonly Dictionary<string, CombatantAnimator> _markerAnims = new Dictionary<string, CombatantAnimator>();
        private readonly Dictionary<string, string> _markerSources = new Dictionary<string, string>();
        private CombatantAnimator _playerAnim;
        private RuntimeAnimatorController _playerExploreController;

        /// <summary>The animation interface of a combatant's body (null: a placeholder shape, or none).</summary>
        public CombatantAnimator CombatAnimatorOf(CombatantId id)
        {
            var c = Combat?.Snapshot?.Order.FirstOrDefault(x => x.Id == id);
            if (c != null && c.Kind == CombatantKind.Player) return _playerAnim;
            return _markerAnims.TryGetValue(id.ToString(), out var a) && a != null ? a : null;
        }

        /// <summary>What a combatant's body was made from ("KayKit Skeleton_Warrior with Skeleton_Blade"; null: the placeholder).</summary>
        public string BodySourceOf(CombatantId id) => _markerSources.TryGetValue(id.ToString(), out var s) ? s : null;

        private CombatantAnimator AnimOf(CombatantId id) => id.IsEmpty ? null : CombatAnimatorOf(id);

        private bool HasShield(CombatantId id)
        {
            var c = Combat?.Snapshot?.Order.FirstOrDefault(x => x.Id == id);
            return c?.ArmorClassText != null && c.ArmorClassText.IndexOf("shield", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The attack motion a log line describes: by the weapon it names, and whether it struck from range.</summary>
        public static CombatMotion MotionFor(string text, bool ranged)
        {
            var t = (text ?? string.Empty).ToLowerInvariant();
            if (t.Contains("crossbow")) return CombatMotion.AttackRanged;
            if (t.Contains("bow")) return CombatMotion.AttackBow;
            if (ranged || t.Contains("javelin") || t.Contains("dart") || t.Contains("sling") || t.Contains("thrown")) return CombatMotion.AttackRanged;
            foreach (var w in new[] { "unarmed", "punch", "slam", "bite", "claw", "fist", "kick", "tusk", "gore", "ram", "hooves", "peck" })
                if (t.Contains(w)) return CombatMotion.AttackUnarmed;
            foreach (var w in new[] { "great", "maul", "glaive", "halberd", "pike", "two-handed", "quarterstaff" })
                if (t.Contains(w)) return CombatMotion.Attack2H;
            return CombatMotion.Attack1H;
        }

        /// <summary>Whether an attack or spell struck from range: the combatants' squares more than 5 ft apart (reach weapons stay melee).</summary>
        private bool IsRanged(CombatLogEntry e)
        {
            var order = Combat?.Snapshot?.Order;
            var a = order?.FirstOrDefault(x => x.Id == e.ActorId);
            var t = order?.FirstOrDefault(x => x.Id == e.TargetId);
            if (a == null || t == null) return false;
            var feet = a.Position.DistanceFeet(t.Position);
            if (feet <= 5) return false;
            var text = (e.Text ?? string.Empty).ToLowerInvariant();
            return !(feet <= 10 && (text.Contains("glaive") || text.Contains("halberd") || text.Contains("pike") || text.Contains("whip") || text.Contains("lance")));
        }

        private void Face(Transform who, Vector3 at)
        {
            if (who == null || (Player != null && who == Player.transform)) return; // the player's facing follows the camera
            var figure = who.Find("Figure") ?? who;
            var dir = at - figure.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) figure.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>
        /// The player's body uses the shared combat controller during a fight (the exploration parameters plus the combat
        /// motions) and its own controller again afterwards. Null when the body is not a humanoid.
        /// </summary>
        private CombatantAnimator EnsurePlayerCombatAnimator()
        {
            if (_playerAnim != null) return _playerAnim;
            var driver = Player != null ? Player.GetComponentInChildren<CharacterAnimationDriver>(true) : null;
            if (driver == null || driver.Animator == null || Catalog == null || Catalog.Combatant == null) return null;
            _playerExploreController = driver.Animator.runtimeAnimatorController;
            driver.Animator.runtimeAnimatorController = Catalog.PersonCombatant != null ? Catalog.PersonCombatant : Catalog.Combatant; // the clips made for the Quaternius body
            _playerAnim = driver.GetComponent<CombatantAnimator>();
            if (_playerAnim == null) _playerAnim = driver.gameObject.AddComponent<CombatantAnimator>();
            _playerAnim.Bind(driver.Animator, drivesSpeed: false); // the driver measures the body's walking
            return _playerAnim;
        }

        private void RestorePlayerAnimator()
        {
            if (_playerAnim == null) return;
            if (_playerAnim.Animator != null && _playerExploreController != null)
            {
                _playerAnim.SetDead(false);
                _playerAnim.Animator.runtimeAnimatorController = _playerExploreController;
            }

            Object.Destroy(_playerAnim);
            _playerAnim = null;
        }
    }
}
