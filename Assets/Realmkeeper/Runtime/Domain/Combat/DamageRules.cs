using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// Phase 14 (ADR-0068): the one damage rule, shared by fights and by harm outside them (traps, hazards, fire, falls).
    /// SRD 5.2.1 order: immunity (none), then resistance (halved, rounded down), then vulnerability (doubled), each at most
    /// once; then temporary hit points absorb first. The combat engine adds what only a fight has (death saves, massive
    /// damage, concentration); outside a fight a character keeps their last hit point (ADR-0064's fairness rule, kept).
    /// </summary>
    public static class DamageRules
    {
        public static int Adjust(int amount, bool immune, bool resists, bool vulnerable, List<string> notes)
        {
            var final = Math.Max(0, amount);
            if (immune)
            {
                notes?.Add("immune");
                return 0;
            }

            if (resists) { final /= 2; notes?.Add("resistance (halved)"); }
            if (vulnerable) { final *= 2; notes?.Add("vulnerability (doubled)"); }
            return final;
        }

        public static bool Lists(IReadOnlyList<string> keys, DamageType t)
        {
            if (keys == null) return false;
            var key = t.ToString().ToLowerInvariant();
            foreach (var x in keys) if (string.Equals(x, key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Harm to a character with a sheet, outside a fight: the character's own resistances (species, features,
        /// effects on the sheet), temporary hit points first, and the last hit point kept (no dying outside a fight).
        /// </summary>
        public static DamageResult ApplyToSheet(CharacterSheetData s, int amount, DamageType type)
        {
            var r = new DamageResult { Rolled = amount, Type = type, HpBefore = s.CurrentHp };
            if (s.IsDead) return r;
            var rules = new CharacterRules(s);
            r.Final = Adjust(amount, false, Lists(rules.Resistances, type), false, r.Adjustments);
            var tempBefore = s.TemporaryHp;
            if (r.Final > 0)
            {
                var floor = Math.Min(1, s.CurrentHp);
                CharacterVitals.ChangeHitPoints(s, -r.Final);
                if (s.CurrentHp < floor)
                {
                    s.CurrentHp = floor;
                    r.Adjustments.Add("the last hit point is kept outside a fight");
                }
            }

            r.TemporaryAbsorbed = tempBefore - s.TemporaryHp;
            r.HpAfter = s.CurrentHp;
            r.HitPointsLost = r.HpBefore - r.HpAfter;
            return r;
        }

        /// <summary>"7 poison damage (resistance (halved), from 14); 3 absorbed by temporary hit points: 12 → 8 HP".</summary>
        public static string Describe(DamageResult r)
        {
            var name = r.Type.ToString().ToLowerInvariant();
            return $"{r.Final} {name} damage" + (r.Adjustments.Count > 0 ? " (" + string.Join(", ", r.Adjustments) + $"; rolled {r.Rolled})" : string.Empty) +
                   (r.TemporaryAbsorbed > 0 ? $", {r.TemporaryAbsorbed} absorbed by temporary hit points" : string.Empty) + $": {r.HpBefore} → {r.HpAfter} HP";
        }
    }
}
