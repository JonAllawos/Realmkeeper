using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// The resource and hit-point engine (Phase 7), for any character sheet. Each operation validates first and
    /// changes nothing when it refuses (returning why); maximums always come from <see cref="CharacterRules"/>.
    /// Rest gameplay (time passing, interruptions, spending hit dice to heal) is a later phase: <see cref="Recover"/>
    /// is the hook it will call.
    /// </summary>
    public static class CharacterVitals
    {
        public const int MaxChange = 100000;

        /// <summary>Spends uses of a resource. Refused when the resource is unknown or too few uses are left.</summary>
        public static string Spend(CharacterSheetData s, string resourceId, int amount)
        {
            var rules = new CharacterRules(s);
            var r = rules.FindResource(resourceId);
            if (r == null) return $"'{resourceId}' is not one of this character's resources.";
            if (amount < 1 || amount > MaxChange) return "Spend at least 1.";
            var left = s.Resources.TryGetValue(resourceId, out var v) ? v : 0;
            if (amount > left) return $"{r.Name}: only {left} left (needs {amount}).";
            s.Resources[resourceId] = left - amount;
            return null;
        }

        /// <summary>Restores uses of a resource, up to its maximum (null amount = all).</summary>
        public static string Restore(CharacterSheetData s, string resourceId, int? amount)
        {
            var rules = new CharacterRules(s);
            var r = rules.FindResource(resourceId);
            if (r == null) return $"'{resourceId}' is not one of this character's resources.";
            if (amount.HasValue && (amount.Value < 1 || amount.Value > MaxChange)) return "Restore at least 1.";
            var max = rules.ResourceMax(r);
            var left = s.Resources.TryGetValue(resourceId, out var v) ? v : 0;
            s.Resources[resourceId] = amount.HasValue ? Math.Min(max, left + amount.Value) : max;
            return null;
        }

        /// <summary>
        /// Restores what a rest restores: on a short rest, every resource that returns on one; on a long rest, every
        /// resource (all hit dice, SRD 5.2.1; one use of Rage, Channel Divinity and Wild Shape on a short rest). Returns what came back ("Rage 1→3").
        /// </summary>
        public static List<string> Recover(CharacterSheetData s, ResourceRecovery rest)
        {
            var rules = new CharacterRules(s);
            var changes = new List<string>();
            foreach (var r in rules.ResourceDefinitions)
            {
                var partial = rest == ResourceRecovery.ShortRest && rules.RecoveryOf(r) != ResourceRecovery.ShortRest;
                if (partial && r.ShortRestRegain <= 0) continue;
                var max = rules.ResourceMax(r);
                var left = s.Resources.TryGetValue(r.Id, out var v) ? v : 0;
                var back = partial ? r.ShortRestRegain : r.Amount == RecoveryAmount.Half ? Math.Max(1, max / 2) : max;
                var now = Math.Min(max, left + back);
                if (now == left) continue;
                s.Resources[r.Id] = now;
                changes.Add($"{r.Name} {left}→{now}");
            }

            return changes;
        }

        /// <summary>
        /// Damage (negative) or healing (positive). Damage takes temporary hit points first, and hit points stop at 0;
        /// healing stops at the maximum and never touches temporary hit points.
        /// </summary>
        public static string ChangeHitPoints(CharacterSheetData s, int change)
        {
            if (change == 0 || Math.Abs(change) > MaxChange) return "Change hit points by at least 1.";
            if (s.IsDead) return "The dead cannot be healed or harmed (there is no resurrection yet).";
            var max = new CharacterRules(s).MaxHp;
            if (change > 0)
            {
                s.CurrentHp = Math.Min(max, s.CurrentHp + change);
                // Phase 8: any healing brings a dying or stable character back; death saves reset.
                s.IsStable = false;
                s.DeathSaveSuccesses = s.DeathSaveFailures = 0;
                return null;
            }

            var damage = -change;
            var absorbed = Math.Min(s.TemporaryHp, damage);
            s.TemporaryHp -= absorbed;
            s.CurrentHp = Math.Max(0, s.CurrentHp - (damage - absorbed));
            return null;
        }

        /// <summary>Temporary hit points do not stack: the higher of the current and the new amount is kept.</summary>
        public static string GrantTemporaryHitPoints(CharacterSheetData s, int amount)
        {
            if (amount < 1 || amount > MaxChange) return "Grant at least 1 temporary hit point.";
            s.TemporaryHp = Math.Max(s.TemporaryHp, amount);
            return null;
        }
    }
}
