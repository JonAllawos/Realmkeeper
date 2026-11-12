using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>How a lasting condition can end, besides its time running out (Phase 14, ADR-0068).</summary>
    [Flags]
    public enum ConditionRemedy
    {
        None = 0,

        /// <summary>Lesser Restoration (SRD 5.2.1) ends it: Blinded, Deafened, Paralyzed or Poisoned.</summary>
        Restoration = 1,

        /// <summary>A long rest removes one level (Exhaustion, SRD 5.2.1).</summary>
        LongRestLevel = 2,

        /// <summary>A check against <see cref="LastingConditionData.EscapeDc"/> (Athletics or Acrobatics) ends it (a net, a snare).</summary>
        Escape = 4,
    }

    /// <summary>
    /// A condition that outlives a fight or comes from outside one (a trap's poison, cold water's exhaustion, a net). It
    /// lives on the character sheet. The combat engine reads it when a fight starts (it becomes a combat condition with the
    /// same key) and writes back what the fight changed when it ends.
    /// </summary>
    public sealed class LastingConditionData
    {
        public ConditionType Type { get; set; }

        /// <summary>Exhaustion level (1–6); 0 otherwise.</summary>
        public int Level { get; set; }

        /// <summary>What caused it, as a stable key ("trap:poison_needle", "hazard:deep_water") — re-applying the same source refreshes, never duplicates.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>In words: "the poisoned needle in the Fordkeepers' Crypt".</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Absolute world minute it began.</summary>
        public long AppliedAt { get; set; }

        /// <summary>Absolute world minute it ends by itself; −1 for never (it needs a remedy).</summary>
        public long ExpiresAt { get; set; } = -1;

        public ConditionRemedy Remedies { get; set; }

        /// <summary>For <see cref="ConditionRemedy.Escape"/>: the DC.</summary>
        public int EscapeDc { get; set; }

        /// <summary>Failed escape attempts so far (after <see cref="LastingConditions.EscapeTries"/>, the holder works free with time).</summary>
        public int EscapeTries { get; set; }

        public LastingConditionData Clone() => (LastingConditionData)MemberwiseClone();
    }

    /// <summary>
    /// The lasting-condition rules: apply (one instance per condition type; the longer time wins; exhaustion levels add),
    /// expire with world time, the remedies, and what each condition means outside a fight. The meanings are the SRD 5.2.1
    /// definitions already used by the combat engine; nothing here adds a mechanic the rules do not have.
    /// </summary>
    public static class LastingConditions
    {
        public const int MaxConditions = 16;

        /// <summary>After this many failed escapes the holder works free anyway, given time (no soft lock).</summary>
        public const int EscapeTries = 3;

        /// <summary>Minutes one escape attempt takes.</summary>
        public const int EscapeMinutes = 1;

        /// <summary>Minutes working free takes once the tries are spent.</summary>
        public const int WorkFreeMinutes = 10;

        /// <summary>Conditions that only make sense in a fight (a grappler's hold, lying prone): never kept outside one.</summary>
        public static bool FightOnly(ConditionType t) =>
            t == ConditionType.Grappled || t == ConditionType.Prone || t == ConditionType.Unconscious || t == ConditionType.Incapacitated ||
            t == ConditionType.Invisible || t == ConditionType.Charmed || t == ConditionType.Stunned || t == ConditionType.Paralyzed || t == ConditionType.Petrified;

        /// <summary>The conditions in force at <paramref name="now"/> (expired ones are not).</summary>
        public static List<LastingConditionData> Active(CharacterSheetData s, long now)
        {
            var list = new List<LastingConditionData>();
            if (s?.Conditions == null) return list;
            foreach (var c in s.Conditions) if (c.ExpiresAt < 0 || c.ExpiresAt > now) list.Add(c);
            return list;
        }

        public static LastingConditionData Find(CharacterSheetData s, ConditionType t, long now)
        {
            foreach (var c in Active(s, now)) if (c.Type == t) return c;
            return null;
        }

        public static bool Has(CharacterSheetData s, ConditionType t, long now) => Find(s, t, now) != null;

        public static int ExhaustionLevel(CharacterSheetData s, long now) => Find(s, ConditionType.Exhaustion, now)?.Level ?? 0;

        /// <summary>
        /// Applies a lasting condition. Fight-only conditions are refused. The same condition does not stack: it keeps one
        /// instance, the later end wins and remedies combine; exhaustion levels add (6 kills: the caller handles death).
        /// Returns what happened in words, or null when nothing changed.
        /// </summary>
        public static string Apply(CharacterSheetData s, ConditionType t, string source, string reason, long now, int minutes, ConditionRemedy remedies, int escapeDc = 0, int level = 1)
        {
            if (s == null || s.IsDead || FightOnly(t)) return null;
            Expire(s, now);
            var existing = Find(s, t, now);
            var ends = minutes > 0 ? now + minutes : -1;
            if (existing != null)
            {
                if (t == ConditionType.Exhaustion)
                {
                    existing.Level = Math.Min(6, existing.Level + Math.Max(1, level));
                    existing.Reason = reason ?? existing.Reason;
                    return $"Your exhaustion rises to level {existing.Level}.";
                }

                if (existing.ExpiresAt >= 0 && (ends < 0 || ends > existing.ExpiresAt)) existing.ExpiresAt = ends;
                existing.Remedies |= remedies;
                existing.EscapeDc = Math.Max(existing.EscapeDc, escapeDc);
                return null; // already so: refreshed, not doubled
            }

            if (s.Conditions.Count >= MaxConditions) return null;
            s.Conditions.Add(new LastingConditionData
            {
                Type = t, Level = t == ConditionType.Exhaustion ? Math.Max(1, level) : 0, Source = source ?? string.Empty, Reason = reason ?? string.Empty,
                AppliedAt = now, ExpiresAt = ends, Remedies = remedies, EscapeDc = escapeDc,
            });
            return t == ConditionType.Exhaustion ? $"You are exhausted (level {Math.Max(1, level)})." : $"You are {Name(t).ToLowerInvariant()}.";
        }

        /// <summary>Removes conditions whose time has run out; returns their names.</summary>
        public static List<string> Expire(CharacterSheetData s, long now)
        {
            var gone = new List<string>();
            if (s?.Conditions == null) return gone;
            for (var i = s.Conditions.Count - 1; i >= 0; i--)
            {
                var c = s.Conditions[i];
                if (c.ExpiresAt >= 0 && c.ExpiresAt <= now)
                {
                    s.Conditions.RemoveAt(i);
                    gone.Add(Name(c.Type));
                }
            }

            return gone;
        }

        public static bool Remove(CharacterSheetData s, ConditionType t)
        {
            if (s?.Conditions == null) return false;
            return s.Conditions.RemoveAll(c => c.Type == t) > 0;
        }

        /// <summary>A long rest: exhaustion falls by one level (SRD 5.2.1); nothing else ends because of the rest itself.</summary>
        public static string LongRest(CharacterSheetData s)
        {
            var ex = s?.Conditions?.Find(c => c.Type == ConditionType.Exhaustion);
            if (ex == null) return null;
            ex.Level--;
            if (ex.Level <= 0)
            {
                s.Conditions.Remove(ex);
                return "Exhaustion: gone.";
            }

            return $"Exhaustion: level {ex.Level} (one level less).";
        }

        /// <summary>Lesser Restoration: ends one of Blinded, Deafened, Paralyzed or Poisoned (the first it finds, in that order).</summary>
        public static string Restore(CharacterSheetData s, long now)
        {
            foreach (var t in new[] { ConditionType.Poisoned, ConditionType.Blinded, ConditionType.Deafened, ConditionType.Paralyzed })
            {
                var c = Find(s, t, now);
                if (c == null || (c.Remedies & ConditionRemedy.Restoration) == 0) continue;
                s.Conditions.Remove(c);
                return $"No longer {Name(t).ToLowerInvariant()}.";
            }

            return null;
        }

        public static string Name(ConditionType t) => t == ConditionType.Exhaustion ? "Exhausted" : t.ToString();

        /// <summary>What the condition does, as the rules say (the combat engine applies the same in fights).</summary>
        public static string Effect(ConditionType t, int level)
        {
            switch (t)
            {
                case ConditionType.Poisoned: return "Disadvantage on attack rolls and ability checks.";
                case ConditionType.Blinded: return "You can't see: checks that need sight fail; attacks against you have advantage, yours have disadvantage.";
                case ConditionType.Deafened: return "You can't hear: checks that need hearing fail.";
                case ConditionType.Frightened: return "Disadvantage on ability checks and attack rolls while the source of fear is in sight.";
                case ConditionType.Restrained: return "Speed 0: you can't move from room to room; attacks against you have advantage; disadvantage on Dexterity saves.";
                case ConditionType.Exhaustion: return $"Level {level}: −{2 * level} to every d20 test and −{5 * level} ft speed. Death at level 6.";
                default: return "See the rules.";
            }
        }

        /// <summary>How it can end, in words, for the HUD.</summary>
        public static string HowToEnd(LastingConditionData c, long now)
        {
            var parts = new List<string>();
            if (c.ExpiresAt >= 0)
            {
                var left = Math.Max(0, c.ExpiresAt - now);
                parts.Add(left >= 60 ? $"wears off in {left / 60} h {left % 60} min" : $"wears off in {left} min");
            }

            if ((c.Remedies & ConditionRemedy.Restoration) != 0) parts.Add("Lesser Restoration ends it");
            if ((c.Remedies & ConditionRemedy.LongRestLevel) != 0) parts.Add("each long rest removes a level");
            if ((c.Remedies & ConditionRemedy.Escape) != 0) parts.Add($"break free (Athletics or Acrobatics, DC {c.EscapeDc})");
            return parts.Count == 0 ? "lasts until removed" : string.Join("; ", parts);
        }

        public static string Minutes(long m) => m.ToString(CultureInfo.InvariantCulture);
    }
}
