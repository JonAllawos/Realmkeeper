using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>A factor's 0–100 score and its contribution (weight × score) in hundredths of an influence point.</summary>
    public readonly struct InfluenceContribution
    {
        public InfluenceContribution(InfluenceFactor factor, int weight, int score)
        {
            Factor = factor;
            Weight = weight;
            Score = score;
        }

        public InfluenceFactor Factor { get; }
        public int Weight { get; }
        public int Score { get; }
        public int Hundredths => Weight * Score;

        /// <summary>Contribution rounded to whole influence points, for display.</summary>
        public int Points => (Hundredths + 50) / 100;
    }

    /// <summary>One signed term of a relationship's opinion target, with its cause.</summary>
    public readonly struct OpinionTerm
    {
        public OpinionTerm(RelationCause cause, int value)
        {
            Cause = cause;
            Value = value;
        }

        public RelationCause Cause { get; }
        public int Value { get; }
    }

    /// <summary>Opinion and rivalry targets for one pair, with the terms that produce them.</summary>
    public sealed class RelationEvaluation
    {
        public int OpinionTarget { get; set; }
        public int RivalryTarget { get; set; }
        public List<OpinionTerm> Terms { get; } = new List<OpinionTerm>();

        /// <summary>The term with the largest effect, or Drift when nothing pulls either way.</summary>
        public RelationCause MainCause
        {
            get
            {
                var best = RelationCause.Drift;
                var size = 0;
                foreach (var t in Terms)
                {
                    if (Math.Abs(t.Value) > size)
                    {
                        size = Math.Abs(t.Value);
                        best = t.Cause;
                    }
                }

                return best;
            }
        }
    }

    /// <summary>
    /// Pure political calculations shared by the political system, world generation, queries and
    /// tests. Integer arithmetic only; no state, no randomness.
    /// </summary>
    public static class PoliticalMath
    {
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static int StepToward(int current, int target, int maxStep) =>
            target > current ? Math.Min(target, current + maxStep) : Math.Max(target, current - maxStep);

        public static bool IsTickDay(WorldDate date, PoliticalRules rules) => date.ToDayNumber() % rules.TickIntervalDays == 0;

        public static bool Governs(FactionData f, SettlementData s) => s.FactionId == f.FactionId;

        public static bool Administers(FactionData f, SettlementData s) => s.AdministratorFactionId == f.FactionId;

        public static bool GovernsAnything(WorldData world, FactionData f)
        {
            foreach (var s in world.Settlements)
            {
                if (s.FactionId == f.FactionId) return true;
            }

            return false;
        }

        // ----- Influence ----------------------------------------------------------------------

        /// <summary>
        /// The terms of a faction's influence target. Each score is 0..100 and is built from a
        /// different underlying quantity (members, own coin, governed population, local
        /// organisation, the settlements' economy, their stability, others' opinion), so nothing
        /// is counted twice. Σ weight × score / 100 is the target.
        /// </summary>
        public static IReadOnlyList<InfluenceContribution> InfluenceContributions(WorldData world, FactionData f, PoliticalRules rules)
        {
            var profile = rules.Profile(f.Type);
            long worldPopulation = 0;
            long governed = 0;
            long administered = 0;
            long stabilitySum = 0;
            var held = 0;
            foreach (var s in world.Settlements)
            {
                worldPopulation += s.Population;
                if (s.FactionId == f.FactionId)
                {
                    governed += s.Population;
                    stabilitySum += s.Stability;
                    held++;
                }
                else if (s.AdministratorFactionId == f.FactionId)
                {
                    administered += s.Population;
                    stabilitySum += s.Stability;
                    held++;
                }
            }

            var membership = worldPopulation <= 0 ? 0 : (int)Math.Min(100, (long)f.TotalMembers * 1000 * 100 / (worldPopulation * profile.FullMembershipPerMille));
            var wealth = (int)Math.Min(100, (long)f.Treasury * 100 / rules.WealthForFullScore);
            var holdings = worldPopulation <= 0 ? 0 : (int)Math.Min(100, (governed + administered / 2) * 100 / worldPopulation);

            long strengthSum = 0;
            long economySum = 0;
            foreach (var p in f.Presence)
            {
                strengthSum += p.Strength;
                var s = world.FindSettlement(p.SettlementId);
                if (s == null) continue;
                economySum += profile.EconomyFollowsTrade
                    ? Math.Min(100, (long)s.Economy.TradeActivity * 100 / rules.TradeActivityForFullScore)
                    : s.Prosperity;
            }

            var presence = world.Settlements.Count == 0 ? 0 : (int)Math.Min(100, strengthSum / world.Settlements.Count);
            var economy = f.Presence.Count == 0 ? 0 : (int)(economySum / f.Presence.Count);
            var authority = held == 0 ? 0 : (int)(stabilitySum / held);

            long opinionSum = 0;
            var relations = 0;
            foreach (var r in world.Relations)
            {
                if (!r.Involves(f.FactionId)) continue;
                var other = world.FindFaction(r.Other(f.FactionId));
                if (other == null || other.IsDissolved) continue;
                opinionSum += (r.Opinion + 100) / 2;
                relations++;
            }

            var reputation = relations == 0 ? 50 : (int)(opinionSum / relations);

            return new[]
            {
                new InfluenceContribution(InfluenceFactor.Membership, profile.MembershipWeight, membership),
                new InfluenceContribution(InfluenceFactor.Wealth, profile.WealthWeight, wealth),
                new InfluenceContribution(InfluenceFactor.Holdings, profile.HoldingsWeight, holdings),
                new InfluenceContribution(InfluenceFactor.Presence, profile.PresenceWeight, presence),
                new InfluenceContribution(InfluenceFactor.Economy, profile.EconomyWeight, economy),
                new InfluenceContribution(InfluenceFactor.Authority, profile.AuthorityWeight, authority),
                new InfluenceContribution(InfluenceFactor.Reputation, profile.ReputationWeight, reputation),
            };
        }

        public static int InfluenceTarget(IReadOnlyList<InfluenceContribution> parts)
        {
            long sum = 0;
            foreach (var p in parts) sum += p.Hundredths;
            return Clamp((int)(sum / 100), FactionRules.MinInfluence, FactionRules.MaxInfluence);
        }

        public static InfluenceFactor LargestFactor(IReadOnlyList<InfluenceContribution> parts)
        {
            var best = parts[0];
            foreach (var p in parts)
            {
                if (p.Hundredths > best.Hundredths) best = p;
            }

            return best.Factor;
        }

        // ----- Presence and membership ---------------------------------------------------------

        /// <summary>
        /// Where a faction's local strength is heading: 30 + influence / 2, +25 where it governs,
        /// +15 where it administers, −<see cref="PoliticalRules.RivalPressure"/> per rival or hostile
        /// faction that is at least as strong there. Always 1..100.
        /// </summary>
        public static int PresenceTarget(WorldData world, FactionData f, FactionPresenceData presence, SettlementData s, PoliticalRules rules)
        {
            var target = 30 + f.Influence / 2;
            if (Governs(f, s)) target += 25;
            else if (Administers(f, s)) target += 15;

            foreach (var other in world.Factions)
            {
                if (other.FactionId == f.FactionId || other.IsDissolved) continue;
                var theirs = other.FindPresence(s.SettlementId);
                if (theirs == null || theirs.Strength < presence.Strength) continue;
                var relation = world.FindRelation(f.FactionId, other.FactionId);
                if (relation == null) continue;
                var state = StateOf(relation);
                if (state == RelationState.Rival || state == RelationState.Hostile) target -= rules.RivalPressure;
            }

            return Clamp(target, 1, 100);
        }

        /// <summary>
        /// The membership a presence is heading for: population × max share × strength% × support%.
        /// Support is the settlement's stability where the faction governs or administers (hardship
        /// erodes support for the authorities), the mean of prosperity and (50 + influence / 2) for
        /// trade-following factions, and 50 + influence / 2 otherwise.
        /// </summary>
        public static int MembershipTarget(FactionData f, FactionPresenceData presence, SettlementData s, PoliticalRules rules)
        {
            var profile = rules.Profile(f.Type);
            var standing = 50 + f.Influence / 2;
            var support = Governs(f, s) || Administers(f, s) ? s.Stability
                : profile.EconomyFollowsTrade ? (s.Prosperity + standing) / 2
                : standing;
            var perMille = (long)profile.MaxSharePerMille * presence.Strength * support / (100 * 100);
            return (int)Math.Min(s.Population, (long)s.Population * perMille / 1000);
        }

        public static int SharePerMille(int members, int population) => population <= 0 ? 0 : (int)((long)members * 1000 / population);

        /// <summary>Keeps every faction's members within the population, and their total within it too (proportionally).</summary>
        public static void ClampMembers(WorldData world)
        {
            foreach (var s in world.Settlements)
            {
                long total = 0;
                foreach (var f in world.Factions)
                {
                    var p = f.FindPresence(s.SettlementId);
                    if (p == null) continue;
                    if (p.Members > s.Population) p.Members = Math.Max(0, s.Population);
                    total += p.Members;
                }

                if (total <= s.Population) continue;
                foreach (var f in world.Factions)
                {
                    var p = f.FindPresence(s.SettlementId);
                    if (p != null) p.Members = (int)((long)p.Members * Math.Max(0, s.Population) / total);
                }
            }
        }

        // ----- Relationships --------------------------------------------------------------------

        /// <summary>
        /// Readable state of a relationship. Checked in this order, so states never contradict:
        /// Hostile (opinion ≤ −50); Allied (mutual-support agreement and opinion ≥ 40);
        /// Rival (opinion ≤ −25, or rivalry ≥ 60 with opinion &lt; 25); Friendly (opinion ≥ 25);
        /// Wary (opinion ≤ −10 or rivalry ≥ 30); otherwise Neutral.
        /// </summary>
        public static RelationState StateOf(int opinion, int rivalry, AgreementType agreement)
        {
            if (opinion <= -50) return RelationState.Hostile;
            if (agreement == AgreementType.MutualSupport && opinion >= 40) return RelationState.Allied;
            if (opinion <= -25 || (rivalry >= 60 && opinion < 25)) return RelationState.Rival;
            if (opinion >= 25) return RelationState.Friendly;
            if (opinion <= -10 || rivalry >= 30) return RelationState.Wary;
            return RelationState.Neutral;
        }

        public static RelationState StateOf(FactionRelationData r) => StateOf(r.Opinion, r.Rivalry, r.Agreement);

        /// <summary>Two factions compete when both can govern, or when they are of the same type.</summary>
        public static bool AreCompetitors(FactionData a, FactionData b) =>
            (FactionRules.CanGovern(a.Type) && FactionRules.CanGovern(b.Type)) || a.Type == b.Type;

        /// <summary>
        /// Where a pair's opinion and rivalry are heading, and why. Rivalry target: 20 per
        /// settlement shared by competitors (never a governor and its own administrator), 40 per contested and 15 per unresolved claim by one
        /// on a settlement the other governs or administers (capped at 100). Opinion target: the
        /// sum of the listed terms, clamped to −100..100.
        /// </summary>
        public static RelationEvaluation EvaluateRelation(WorldData world, FactionData a, FactionData b, FactionRelationData relation, PoliticalRules rules)
        {
            var result = new RelationEvaluation();
            var administration = false;
            foreach (var s in world.Settlements)
            {
                if ((s.FactionId == a.FactionId && s.AdministratorFactionId == b.FactionId) ||
                    (s.FactionId == b.FactionId && s.AdministratorFactionId == a.FactionId)) administration = true;
            }

            // A house that administers for an authority works with it, not against it.
            var competitors = AreCompetitors(a, b) && !administration;
            var shared = 0;
            var prosperous = 0;
            foreach (var pa in a.Presence)
            {
                if (b.FindPresence(pa.SettlementId) == null) continue;
                shared++;
                var s = world.FindSettlement(pa.SettlementId);
                if (s != null && s.Prosperity >= rules.SharedProsperityThreshold) prosperous++;
            }

            var contested = 0;
            var unresolved = 0;
            var recognized = 0;
            foreach (var c in world.Claims)
            {
                if (!c.IsOpen) continue;
                var s = world.FindSettlement(c.SettlementId);
                if (s == null) continue;
                var aOnB = c.ClaimantId == a.FactionId && (s.FactionId == b.FactionId || s.AdministratorFactionId == b.FactionId);
                var bOnA = c.ClaimantId == b.FactionId && (s.FactionId == a.FactionId || s.AdministratorFactionId == a.FactionId);
                if (!aOnB && !bOnA) continue;
                if (c.Status == ClaimStatus.Contested) contested++;
                else if (c.Status == ClaimStatus.Unresolved) unresolved++;
                else recognized++;
            }

            result.RivalryTarget = Math.Min(100, (competitors ? rules.RivalryPerSharedSettlement * shared : 0)
                                                 + rules.RivalryPerContestedClaim * contested + rules.RivalryPerUnresolvedClaim * unresolved);

            if (relation.Agreement == AgreementType.TradeAgreement) result.Terms.Add(new OpinionTerm(RelationCause.Agreement, rules.TradeAgreementOpinion));
            if (relation.Agreement == AgreementType.MutualSupport) result.Terms.Add(new OpinionTerm(RelationCause.Agreement, rules.MutualSupportOpinion));
            if (!competitors && prosperous > 0)
                result.Terms.Add(new OpinionTerm(RelationCause.SharedTrade, Math.Min(rules.SharedProsperityCap, rules.SharedProsperityOpinion * prosperous)));
            var support = (administration ? rules.AdministrationOpinion : 0) + rules.RecognizedClaimOpinion * recognized;
            if (support > 0) result.Terms.Add(new OpinionTerm(RelationCause.PoliticalSupport, support));
            if (result.RivalryTarget > 0) result.Terms.Add(new OpinionTerm(RelationCause.Competition, -(result.RivalryTarget / 2)));
            if (contested > 0) result.Terms.Add(new OpinionTerm(RelationCause.TerritorialDispute, -rules.ContestedClaimOpinion));

            var sum = 0;
            foreach (var t in result.Terms) sum += t.Value;
            result.OpinionTarget = Clamp(sum, FactionRelationData.MinOpinion, FactionRelationData.MaxOpinion);
            return result;
        }

        /// <summary>Orders a pair the way relations are stored (FactionA &lt; FactionB).</summary>
        public static (FactionId a, FactionId b) OrderPair(FactionId x, FactionId y) => x.CompareTo(y) <= 0 ? (x, y) : (y, x);

        // ----- Goals ----------------------------------------------------------------------------

        /// <summary>
        /// Progress (0..100) of a goal, from world conditions only:
        /// GainInfluence = influence / target; IncreaseMembership = members / target;
        /// EstablishPresence = local strength / target; ImproveRelationship = (opinion + 100) / (target + 100);
        /// SecureClaim = 100 when governing or holding a recognized claim, 50 contested, 25 unresolved;
        /// MaintainControl = share of governed settlements with stability ≥ <see cref="PoliticalRules.ControlStability"/>.
        /// </summary>
        public static int GoalProgress(WorldData world, FactionData f, FactionGoalData g, PoliticalRules rules)
        {
            switch (g.Type)
            {
                case GoalType.GainInfluence:
                    return Ratio(f.Influence, g.TargetValue);
                case GoalType.IncreaseMembership:
                    return Ratio(f.TotalMembers, g.TargetValue);
                case GoalType.EstablishPresence:
                    return Ratio(f.FindPresence(g.TargetSettlementId)?.Strength ?? 0, g.TargetValue);
                case GoalType.ImproveRelationship:
                {
                    var r = world.FindRelation(f.FactionId, g.TargetFactionId);
                    return Ratio((r?.Opinion ?? 0) + 100, g.TargetValue + 100);
                }
                case GoalType.SecureClaim:
                {
                    var s = world.FindSettlement(g.TargetSettlementId);
                    if (s != null && s.FactionId == f.FactionId) return 100;
                    var best = 0;
                    foreach (var c in world.Claims)
                    {
                        if (c.ClaimantId != f.FactionId || c.SettlementId != g.TargetSettlementId) continue;
                        var value = c.Status == ClaimStatus.Recognized || c.Status == ClaimStatus.Fulfilled ? 100
                            : c.Status == ClaimStatus.Contested ? 50
                            : c.Status == ClaimStatus.Unresolved ? 25 : 0;
                        if (value > best) best = value;
                    }

                    return best;
                }
                default:
                {
                    var held = 0;
                    var steady = 0;
                    foreach (var s in world.Settlements)
                    {
                        if (s.FactionId != f.FactionId) continue;
                        held++;
                        if (s.Stability >= rules.ControlStability) steady++;
                    }

                    return held == 0 ? 0 : steady * 100 / held;
                }
            }
        }

        private static int Ratio(long value, long target) => target <= 0 ? 100 : (int)Clamp((int)Math.Min(100, Math.Max(0, value) * 100 / target), 0, 100);

        // ----- Lifecycle ------------------------------------------------------------------------

        /// <summary>
        /// Ends a faction without deleting it: it gives up everything it holds, but stays in the
        /// world so the chronicle's references to it remain valid.
        /// </summary>
        public static void Dissolve(WorldData world, FactionData f, WorldDate date)
        {
            f.Status = FactionStatus.Dissolved;
            f.DissolvedOn = date;
            f.Presence.Clear();
            foreach (var g in f.Goals)
            {
                if (!g.IsOpen) continue;
                g.Status = GoalStatus.Abandoned;
                g.CloseReason = GoalCloseReason.FactionDissolved;
                g.ClosedOn = date;
            }

            foreach (var c in world.Claims)
            {
                if (c.ClaimantId != f.FactionId || !c.IsOpen) continue;
                c.Status = ClaimStatus.Relinquished;
                c.UpdatedOn = date;
            }

            foreach (var r in world.Relations)
            {
                if (!r.Involves(f.FactionId) || r.Agreement == AgreementType.None) continue;
                r.Agreement = AgreementType.None;
                r.AgreementSince = default;
            }

            foreach (var s in world.Settlements)
            {
                if (s.FactionId == f.FactionId)
                {
                    s.FactionId = FactionId.Empty;
                    s.AdministratorFactionId = FactionId.Empty; // no governor to administer for
                }

                if (s.AdministratorFactionId == f.FactionId) s.AdministratorFactionId = FactionId.Empty;
            }

            foreach (var n in world.Npcs)
            {
                if (n.FactionId == f.FactionId) n.FactionId = FactionId.Empty;
            }
        }
    }
}
