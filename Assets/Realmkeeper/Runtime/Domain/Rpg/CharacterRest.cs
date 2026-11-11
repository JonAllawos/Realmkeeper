using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// Short and long rests (Phase 9, SRD 5.2.1; ADR-0040), on any character sheet. A short rest spends hit dice to heal
    /// (each die + Constitution), returns short-rest resources (and one use of Rage, Channel Divinity and Wild Shape),
    /// and lets a wizard use Arcane Recovery. A long rest restores hit points, every resource and every hit die, ends the
    /// dying state, and reopens spell preparation; one long rest per 24 hours, and it needs at least 1 hit point to start.
    /// The world clock is advanced by the caller (the application) before these apply; nothing here keeps time.
    /// </summary>
    public static class CharacterRest
    {
        public const int ShortRestMinutes = 60;
        public const int LongRestMinutes = 480;

        /// <summary>A long rest cannot start within 16 hours of the last one ending (one long rest per 24 hours).</summary>
        public const int LongRestGapMinutes = 16 * 60;

        public static string ShortRestProblem(CharacterSheetData s, int hitDice)
        {
            if (s == null || s.State != CreationState.Complete) return "Create your character first.";
            if (s.IsDead) return "The dead do not rest.";
            if (hitDice < 0) return "Spend zero or more hit dice.";
            var left = s.Resources.TryGetValue("hit_dice", out var n) ? n : 0;
            if (hitDice > left) return $"Only {left} hit dice are left.";
            return null;
        }

        public static string LongRestProblem(CharacterSheetData s, long startsAt)
        {
            if (s == null || s.State != CreationState.Complete) return "Create your character first.";
            if (s.IsDead) return "The dead do not rest.";
            if (s.CurrentHp < 1) return "A long rest needs at least 1 hit point to begin (heal or spend hit dice on a short rest first).";
            var last = s.Spellcasting?.LastLongRestEnded ?? -1;
            if (last >= 0 && startsAt - last < LongRestGapMinutes)
            {
                var wait = LongRestGapMinutes - (startsAt - last);
                return $"Only one long rest per 24 hours: {wait / 60} h {wait % 60} min until the next can begin.";
            }

            return null;
        }

        /// <summary>Applies a short rest that ended at <paramref name="endedAt"/>; returns what it gave back, line by line.</summary>
        public static List<string> ShortRest(CharacterSheetData s, int hitDice, Func<int, int> die, long endedAt, string key)
        {
            var lines = new List<string>();
            var rules = new CharacterRules(s);
            var con = AbilityRules.Modifier(rules.Score(Ability.Constitution));
            var healed = 0;
            var rolls = new List<int>();
            for (var i = 0; i < hitDice; i++)
            {
                var d = die(rules.HitDie);
                rolls.Add(d);
                var gain = Math.Max(0, d + con);
                s.Resources["hit_dice"] = s.Resources["hit_dice"] - 1;
                if (gain > 0 && s.CurrentHp < rules.MaxHp)
                {
                    var before = s.CurrentHp;
                    CharacterVitals.ChangeHitPoints(s, gain);
                    healed += s.CurrentHp - before;
                }
            }

            if (hitDice > 0) lines.Add($"Spent {hitDice} hit di{(hitDice == 1 ? "e" : "ce")} (d{rules.HitDie} [{string.Join(", ", rolls)}] {(con >= 0 ? "+" : string.Empty)}{con} each): +{healed} hit points.");
            var back = CharacterVitals.Recover(s, ResourceRecovery.ShortRest);
            if (back.Count > 0) lines.Add("Recovered: " + string.Join(", ", back) + ".");
            var arcane = ArcaneRecovery(s, rules);
            if (arcane != null) lines.Add(arcane);
            if (lines.Count == 0) lines.Add("Rested an hour; nothing needed recovering.");
            Record(s, RestKind.Short, endedAt, key, string.Join(" ", lines));
            return lines;
        }

        /// <summary>Applies a long rest that ended at <paramref name="endedAt"/>; returns what it gave back.</summary>
        public static List<string> LongRest(CharacterSheetData s, long endedAt, string key)
        {
            var lines = new List<string>();
            var rules = new CharacterRules(s);
            var before = s.CurrentHp;
            s.CurrentHp = rules.MaxHp;
            s.IsStable = false;
            s.DeathSaveSuccesses = s.DeathSaveFailures = 0;
            if (s.CurrentHp > before) lines.Add($"Hit points {before} → {s.CurrentHp}.");
            var back = CharacterVitals.Recover(s, ResourceRecovery.LongRest);
            if (back.Count > 0) lines.Add("Recovered: " + string.Join(", ", back) + ".");
            var keeping = RpgCatalog.KeepingOf(rules.Class);
            s.Spellcasting.PreparationChanges = keeping == SpellKeeping.PreparedOneSwap ? 1 : keeping == SpellKeeping.Known || keeping == SpellKeeping.None ? 0 : -1;
            if (keeping == SpellKeeping.Prepared || keeping == SpellKeeping.Spellbook) lines.Add("You can change your prepared spells.");
            else if (keeping == SpellKeeping.PreparedOneSwap) lines.Add("You can replace one prepared spell.");
            s.Spellcasting.LastLongRestEnded = endedAt;
            Record(s, RestKind.Long, endedAt, key, string.Join(" ", lines));
            return lines;
        }

        /// <summary>Arcane Recovery (wizard): after a short rest, spent slots of combined level up to half the wizard level (rounded up), none above 5; once per long rest.</summary>
        private static string ArcaneRecovery(CharacterSheetData s, CharacterRules rules)
        {
            if (!rules.HasFeature("arcane_recovery") || !s.Resources.TryGetValue("arcane_recovery", out var uses) || uses <= 0) return null;
            var budget = (rules.Level + 1) / 2;
            var got = new List<string>();
            for (var level = 5; level >= 1 && budget > 0; level--)
            {
                var id = "spell_slots_" + level;
                if (!s.Resources.TryGetValue(id, out var left)) continue;
                var res = rules.FindResource(id);
                var max = res == null ? left : rules.ResourceMax(res);
                while (left < max && level <= budget)
                {
                    left++;
                    budget -= level;
                    got.Add("level " + level);
                }

                s.Resources[id] = left;
            }

            if (got.Count == 0) return null;
            s.Resources["arcane_recovery"] = uses - 1;
            return "Arcane Recovery: " + string.Join(", ", got) + " slot" + (got.Count == 1 ? string.Empty : "s") + ".";
        }

        private static void Record(CharacterSheetData s, RestKind kind, long endedAt, string key, string summary)
        {
            s.Spellcasting.RestLog.Add(new RestRecord { Key = key, Kind = kind, EndedAt = endedAt, Summary = summary });
            while (s.Spellcasting.RestLog.Count > SpellcastingData.MaxRestLog) s.Spellcasting.RestLog.RemoveAt(0);
        }
    }
}
