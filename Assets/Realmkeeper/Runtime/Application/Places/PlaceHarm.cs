using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>What a trap, hazard, fire or fall does (Phase 14, ADR-0068): one description for every source of harm outside a fight.</summary>
    public sealed class HarmSpec
    {
        /// <summary>Stable key of the source ("trap:poison_needle", "hazard:fire") — lasting conditions refresh by it.</summary>
        public string Source { get; set; }

        /// <summary>In words, for the record and the condition ("the poisoned needle").</summary>
        public string Name { get; set; }

        public Dice Damage { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>The save, or null for none.</summary>
        public Ability? Save { get; set; }

        public int SaveDc { get; set; }

        /// <summary>A successful save halves the damage (true) or avoids it (false).</summary>
        public bool HalfOnSave { get; set; } = true;

        /// <summary>The condition a failed save (or no save) leaves, or null.</summary>
        public ConditionType? Condition { get; set; }

        /// <summary>How long it lasts by itself (0: until a remedy).</summary>
        public int ConditionMinutes { get; set; }

        public ConditionRemedy Remedies { get; set; }
        public int EscapeDc { get; set; }

        /// <summary>Exhaustion levels added (for exhaustion).</summary>
        public int Levels { get; set; } = 1;
    }

    /// <summary>
    /// Harm outside a fight, through the shared rules: the save with the same modes a fight uses (exhaustion, restrained
    /// on Dexterity), the damage through <see cref="DamageRules.ApplyToSheet"/>, the lasting condition through
    /// <see cref="LastingConditions"/>, and the explanation. The dice come from the world's seed and the key, so the same
    /// world and the same action always give the same result.
    /// </summary>
    public static class PlaceHarm
    {
        public static string Apply(WorldData w, HarmSpec h, string key, out DamageResult damage)
        {
            damage = null;
            var sheet = w.Player?.Rpg;
            if (sheet == null || sheet.State != CreationState.Complete || sheet.IsDead) return string.Empty;
            var parts = new List<string>();
            var now = QuestClock.Now(w);
            var saved = false;
            if (h.Save.HasValue)
            {
                var save = PlaceRules.Save(w, h.Save.Value, h.SaveDc, key);
                saved = save.Success;
                parts.Add(save.Text);
            }

            if (!h.Damage.IsEmpty && !(saved && !h.HalfOnSave))
            {
                var die = SpellCommandHandlers.Dice(w, "place-harm:" + key);
                var rolled = h.Damage.Bonus;
                for (var i = 0; i < h.Damage.Count; i++) rolled += die(h.Damage.Sides);
                if (saved) rolled /= 2;
                damage = DamageRules.ApplyToSheet(sheet, Math.Max(0, rolled), h.DamageType);
                parts.Add(DamageRules.Describe(damage));
            }

            if (h.Condition.HasValue && !saved)
            {
                var said = LastingConditions.Apply(sheet, h.Condition.Value, h.Source, h.Name, now, h.ConditionMinutes, h.Remedies, h.EscapeDc, h.Levels);
                if (said != null) parts.Add(said + " (" + LastingConditions.HowToEnd(LastingConditions.Find(sheet, h.Condition.Value, now), now) + ")");
                if (LastingConditions.ExhaustionLevel(sheet, now) >= 6)
                {
                    sheet.CurrentHp = 0;
                    sheet.IsDead = true;
                    parts.Add("Exhaustion takes the last of your strength.");
                }
            }

            return string.Join(". ", parts) + (parts.Count > 0 ? "." : string.Empty);
        }

        public static string Key(PlaceData place, string what) => place.PlaceId + ":" + what + ":" + place.Visits.ToString(CultureInfo.InvariantCulture);
    }
}
