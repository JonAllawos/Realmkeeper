using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Party;

namespace Realmkeeper.Application.Party
{
    /// <summary>
    /// Phase 15: the party rests together. When the player's rest is applied, each member with the player applies the
    /// same rest to their own sheet once (the rest's key is recorded in their sheet's log, as for the player), through the
    /// same <see cref="CharacterRest"/> rules: no separate rest system. On a short rest a companion spends hit dice only
    /// as far as they need (deterministic dice from the rest key and their ID).
    /// </summary>
    public static class PartyRest
    {
        public static List<string> Apply(WorldData world, RestKind kind, string key, long now, Func<string, Func<int, int>> dice)
        {
            var lines = new List<string>();
            foreach (var npc in CompanionLoyalty.Present(world))
            {
                var s = npc.Character?.Sheet;
                if (s == null || s.IsDead) continue;
                var own = key + ":" + npc.NpcId;
                if (s.Spellcasting != null && s.Spellcasting.RestLog.Exists(r => r.Key == own)) continue;
                List<string> got;
                if (kind == RestKind.Short)
                {
                    var spend = HitDiceNeeded(s);
                    if (CharacterRest.ShortRestProblem(s, spend) is string no) { lines.Add($"{npc.Name}: {no}"); continue; }
                    got = CharacterRest.ShortRest(s, spend, dice(own), now, own);
                }
                else
                {
                    if (CharacterRest.LongRestProblem(s, now - CharacterRest.LongRestMinutes) is string no) { lines.Add($"{npc.Name}: {no}"); continue; }
                    got = CharacterRest.LongRest(s, now, own);
                    if (LastingConditions.LongRest(s) is string ex) got.Add(ex);
                }

                got.RemoveAll(l => l.StartsWith("You can ", StringComparison.Ordinal));
                if (got.Count == 0) got.Add("rested; nothing needed recovering.");
                lines.Add($"{npc.Name}: {string.Join(" ", got)}");
            }

            return lines;
        }

        /// <summary>Hit dice to spend: enough to close the gap at the die's average, never more than they have.</summary>
        public static int HitDiceNeeded(CharacterSheetData s)
        {
            var rules = new CharacterRules(s);
            var gap = rules.MaxHp - s.CurrentHp;
            if (gap <= 0) return 0;
            var per = Math.Max(1, rules.HitDie / 2 + 1 + AbilityRules.Modifier(rules.Score(Ability.Constitution)));
            var left = s.Resources.TryGetValue("hit_dice", out var n) ? n : 0;
            return Math.Min(left, (gap + per - 1) / per);
        }
    }
}
