using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>Phase 20A: how a trait or lineage benefit works in the game today, as the sheet and the creator say it.</summary>
    public enum BenefitState
    {
        /// <summary>Always in effect (a resistance, a proficiency, darkvision, speed, carrying capacity).</summary>
        Active = 0,

        /// <summary>Played, but only in its situation or when used (on a hit, as a reaction, as a bonus action, a cantrip, a save).</summary>
        Conditional = 1,

        /// <summary>Recorded on the character, but the system it needs does not exist yet.</summary>
        NotYetPlayed = 2,
    }

    /// <summary>
    /// Phase 20A (ADR-0103): classifies a trait or a lineage option from its effects and declared needs, with the words that
    /// explain it. One place decides it, so the creator, the sheet and the tests agree.
    /// </summary>
    public static class BenefitStatus
    {
        private static readonly Dictionary<string, string> CombatWhen = new Dictionary<string, string>
        {
            ["teleport"] = "in a fight, as a bonus action",
            ["extra-fire-damage"] = "in a fight, after you hit with an attack roll",
            ["extra-cold-slow"] = "in a fight, after you hit with an attack roll",
            ["knock-prone"] = "in a fight, after you hit a Large or smaller creature",
            ["reduce-damage"] = "in a fight, as a reaction when an attack hurts you",
            ["retaliate-thunder"] = "in a fight, as a reaction when a creature within 60 ft hurts you with an attack",
        };

        public static (BenefitState state, string text) Of(IReadOnlyList<RuleEffect> effects, IntegrationNeed declared)
        {
            var needs = declared | CharacterRules.NeedsOf(effects);
            if (needs != IntegrationNeed.None) return (BenefitState.NotYetPlayed, "Not yet played by the game: waits for " + NeedsWords(needs) + ".");
            var when = new List<string>();
            foreach (var e in effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.CombatRule:
                        if (CombatWhen.TryGetValue(e.Key, out var w) && !when.Contains(w)) when.Add(w);
                        break;
                    case EffectKind.InnateMagic:
                        if (e.Key == "extra-cantrip") break; // a wider cantrip list: always in effect
                        var cast = "cast in a fight as a cantrip, with the spellcasting ability chosen for it";
                        if (!when.Contains(cast)) when.Add(cast);
                        break;
                    case EffectKind.SaveAdvantage:
                        if (!when.Contains("on the saving throws it names")) when.Add("on the saving throws it names");
                        break;
                    case EffectKind.Resource:
                        if (!when.Contains("a limited number of uses, recovered by resting")) when.Add("a limited number of uses, recovered by resting");
                        break;
                }
            }

            return when.Count == 0 ? (BenefitState.Active, "Always in effect.") : (BenefitState.Conditional, "Played " + string.Join("; ", when) + ".");
        }

        public static (BenefitState state, string text) Of(FeatureDefinition f) => Of(f.Effects, f.Needs);

        public static (BenefitState state, string text) Of(ChoiceOption o) => Of(o.Effects, o.Needs);

        public static string Label(BenefitState s) => s == BenefitState.Active ? "active" : s == BenefitState.Conditional ? "conditional" : "not yet played";

        private static string NeedsWords(IntegrationNeed n)
        {
            var parts = new List<string>();
            if ((n & IntegrationNeed.Combat) != 0) parts.Add("combat rules");
            if ((n & IntegrationNeed.Magic) != 0) parts.Add("a magic system for it");
            if ((n & IntegrationNeed.Rest) != 0) parts.Add("rests");
            if ((n & IntegrationNeed.Dialogue) != 0) parts.Add("dialogue");
            if ((n & IntegrationNeed.Exploration) != 0) parts.Add("exploration");
            return string.Join(", ", parts);
        }
    }
}
