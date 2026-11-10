using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// The political simulation. It runs inside the daily transaction, after the economy and
    /// population and before the chronicle milestones, so a failed day never leaves politics
    /// half-applied. Every day it only keeps faction membership within the (possibly shrunken)
    /// population. The political evaluation itself runs on tick days: day numbers divisible by
    /// <see cref="PoliticalRules.TickIntervalDays"/> (weekly). No randomness is used except the
    /// deterministic stream for new IDs.
    ///
    /// Tick order: goals → dues, upkeep and influence → presence → membership → relationships →
    /// goal-driven actions → claims → status. Events are reported as each step finds a
    /// meaningful change.
    /// </summary>
    public sealed class PoliticalSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.politics";

        /// <summary>Value of the "source" fact on events the simulation produced (commands write "player").</summary>
        public const string SimulationSource = "simulation";

        private readonly PoliticalRules _rules;

        public PoliticalSystem(PoliticalRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public string SystemId => Id;
        public int ExecutionOrder => 450;

        public void Execute(SimulationDayContext context)
        {
            var world = context.World;
            if (world.Factions.Count == 0) return;
            PoliticalMath.ClampMembers(world);
            if (!PoliticalMath.IsTickDay(context.Date, _rules)) return;

            EvaluateGoals(context);
            CollectDuesAndUpdateInfluence(context);
            UpdatePresence(world);
            UpdateMembership(context);
            UpdateRelations(context);
            ActOnGoals(context);
            EvaluateClaims(context);
            UpdateStatus(context);
        }

        private static EventData Facts() => new EventData().Set("source", SimulationSource);

        // ----- 1. Goals -----------------------------------------------------------------------

        private void EvaluateGoals(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                foreach (var g in f.Goals)
                {
                    if (!g.IsOpen) continue;
                    var targetFaction = g.TargetFactionId.IsEmpty ? null : world.FindFaction(g.TargetFactionId);
                    var lost = (targetFaction != null && targetFaction.IsDissolved)
                               || (g.Type == GoalType.MaintainControl && !PoliticalMath.GovernsAnything(world, f));
                    if (lost)
                    {
                        Close(g, GoalStatus.Abandoned, GoalCloseReason.TargetLost, context);
                        ReportGoal(context, HistoricalEventType.FactionGoalAbandoned, EventSeverity.Info, EventImportance.Minor, f, g);
                        continue;
                    }

                    g.Progress = PoliticalMath.GoalProgress(world, f, g, _rules);
                    if (g.Progress < 100 || g.Type == GoalType.MaintainControl) continue;
                    Close(g, GoalStatus.Completed, GoalCloseReason.Achieved, context);
                    ReportGoal(context, HistoricalEventType.FactionGoalCompleted, EventSeverity.Positive, EventImportance.Notable, f, g);
                }
            }
        }

        private static void Close(FactionGoalData g, GoalStatus status, GoalCloseReason reason, SimulationDayContext context)
        {
            g.Status = status;
            g.CloseReason = reason;
            g.ClosedOn = context.Date;
        }

        private static void ReportGoal(SimulationDayContext context, HistoricalEventType type, EventSeverity severity, EventImportance importance,
            FactionData f, FactionGoalData g)
        {
            var factions = new List<FactionId> { f.FactionId };
            if (!g.TargetFactionId.IsEmpty) factions.Add(g.TargetFactionId);
            context.ReportEvent(type, severity, importance,
                g.TargetSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { g.TargetSettlementId }, factions,
                Facts().Set("goal", g.GoalId.ToString()).Set("goalType", g.Type.ToString()).Set("reason", g.CloseReason.ToString()).Set("target", g.TargetValue));
        }

        // ----- 2. Dues, upkeep and influence ----------------------------------------------------

        private void CollectDuesAndUpdateInfluence(SimulationDayContext context)
        {
            var world = context.World;
            var day = context.Date.ToDayNumber();
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                var profile = _rules.Profile(f.Type);

                // Dues are real coin moved out of the settlements' treasuries (never created), and
                // only from what a settlement holds beyond its own reserve.
                foreach (var p in f.Presence)
                {
                    var s = world.FindSettlement(p.SettlementId);
                    if (s == null) continue;
                    var dues = (int)((long)p.Members * profile.DuesPerThousandMembers / 1000);
                    var reserve = (long)_rules.DuesReserveDays * (s.Population / 100);
                    var available = Math.Max(0, s.ResourceInventory.Get(ResourceTypes.Coin) - reserve);
                    var paid = (int)Math.Min(dues, available);
                    if (paid <= 0) continue;
                    if (!s.ResourceInventory.TryRemove(ResourceTypes.Coin, paid)) throw new InvalidOperationException("Dues exceeded a settlement's coin.");
                    EconomyMath.Flow(s.Economy, ResourceTypes.Coin, day).Exported += paid;
                    f.Treasury = checked(f.Treasury + paid);
                }

                f.Treasury -= (int)((long)f.Treasury * _rules.UpkeepPercent / 100);

                var parts = PoliticalMath.InfluenceContributions(world, f, _rules);
                f.InfluenceTarget = PoliticalMath.InfluenceTarget(parts);
                f.Influence = PoliticalMath.StepToward(f.Influence, f.InfluenceTarget, _rules.MaxInfluenceStep);

                if (Math.Abs(f.Influence - f.LastReportedInfluence) < _rules.InfluenceReportStep) continue;
                var rose = f.Influence > f.LastReportedInfluence;
                context.ReportEvent(HistoricalEventType.FactionInfluenceChanged, rose ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Notable,
                    Array.Empty<SettlementId>(), new[] { f.FactionId },
                    Facts().Set("from", f.LastReportedInfluence).Set("to", f.Influence).Set("factor", PoliticalMath.LargestFactor(parts).ToString()));
                f.LastReportedInfluence = f.Influence;
            }
        }

        // ----- 3. Presence ----------------------------------------------------------------------

        private void UpdatePresence(WorldData world)
        {
            // Targets first, from one snapshot, so the result does not depend on faction order.
            var targets = new List<(FactionPresenceData presence, int target)>();
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                foreach (var p in f.Presence)
                {
                    var s = world.FindSettlement(p.SettlementId);
                    if (s != null) targets.Add((p, PoliticalMath.PresenceTarget(world, f, p, s, _rules)));
                }
            }

            foreach (var (presence, target) in targets)
            {
                presence.Strength = PoliticalMath.StepToward(presence.Strength, target, 1);
                presence.Status = presence.Strength >= _rules.EstablishedStrength ? PresenceStatus.Established : PresenceStatus.Waning;
            }
        }

        // ----- 4. Membership ---------------------------------------------------------------------

        private void UpdateMembership(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                foreach (var p in f.Presence)
                {
                    var s = world.FindSettlement(p.SettlementId);
                    if (s == null) continue;
                    var step = Math.Max(1, s.Population / _rules.MembershipStepDivisor);
                    p.Members = PoliticalMath.StepToward(p.Members, PoliticalMath.MembershipTarget(f, p, s, _rules), step);
                }
            }

            PoliticalMath.ClampMembers(world);

            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                foreach (var p in f.Presence)
                {
                    var s = world.FindSettlement(p.SettlementId);
                    if (s == null) continue;
                    var share = PoliticalMath.SharePerMille(p.Members, s.Population);
                    if (Math.Abs(share - p.LastReportedSharePerMille) < _rules.AffiliationReportPerMille) continue;
                    var rose = share > p.LastReportedSharePerMille;
                    context.ReportEvent(HistoricalEventType.AffiliationShifted, EventSeverity.Info, EventImportance.Notable,
                        new[] { s.SettlementId }, new[] { f.FactionId },
                        Facts().Set("fromPerMille", p.LastReportedSharePerMille).Set("toPerMille", share).Set("members", p.Members)
                            .Set("direction", rose ? "rose" : "fell"));
                    p.LastReportedSharePerMille = share;
                }
            }
        }

        // ----- 5. Relationships --------------------------------------------------------------------

        private void UpdateRelations(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var r in world.Relations)
            {
                var a = world.FindFaction(r.FactionA);
                var b = world.FindFaction(r.FactionB);
                if (a == null || b == null || a.IsDissolved || b.IsDissolved) continue;

                var evaluation = PoliticalMath.EvaluateRelation(world, a, b, r, _rules);
                r.Opinion = PoliticalMath.StepToward(r.Opinion, evaluation.OpinionTarget, _rules.MaxOpinionStep);
                r.Rivalry = PoliticalMath.StepToward(r.Rivalry, evaluation.RivalryTarget, _rules.MaxRivalryStep);
                r.Trust = r.Agreement != AgreementType.None ? Math.Min(FactionRelationData.MaxScore, r.Trust + 1) : PoliticalMath.StepToward(r.Trust, 50, 1);
                r.LastCause = evaluation.MainCause;

                var state = PoliticalMath.StateOf(r);
                if (state == r.LastReportedState) continue;
                var improved = state > r.LastReportedState;
                context.ReportEvent(HistoricalEventType.RelationshipChanged, improved ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Notable,
                    Array.Empty<SettlementId>(), new[] { a.FactionId, b.FactionId },
                    Facts().Set("from", r.LastReportedState.ToString()).Set("to", state.ToString()).Set("cause", r.LastCause.ToString()).Set("opinion", r.Opinion));
                r.LastReportedState = state;
            }
        }

        // ----- 6. Goal-driven actions ------------------------------------------------------------

        private void ActOnGoals(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                foreach (var g in f.Goals)
                {
                    if (!g.IsOpen) continue;
                    switch (g.Type)
                    {
                        case GoalType.EstablishPresence:
                            PursuePresence(context, f, g);
                            break;
                        case GoalType.SecureClaim:
                            PursueClaim(context, f, g);
                            break;
                        case GoalType.ImproveRelationship:
                        {
                            var r = world.FindRelation(f.FactionId, g.TargetFactionId);
                            if (r != null) r.Opinion = Math.Min(FactionRelationData.MaxOpinion, r.Opinion + 1);
                            break;
                        }
                    }
                }
            }
        }

        private void PursuePresence(SimulationDayContext context, FactionData f, FactionGoalData g)
        {
            var s = context.World.FindSettlement(g.TargetSettlementId);
            if (s == null) return;
            var presence = f.FindPresence(s.SettlementId);
            if (presence != null)
            {
                presence.Strength = Math.Min(100, presence.Strength + 1);
                return;
            }

            if (f.Influence < _rules.MinInfluenceToExpand) return;
            f.Presence.Add(new FactionPresenceData
            {
                SettlementId = s.SettlementId,
                Strength = _rules.NewPresenceStrength,
                Status = _rules.NewPresenceStrength >= _rules.EstablishedStrength ? PresenceStatus.Established : PresenceStatus.Waning,
                EstablishedOn = context.Date,
            });
            context.ReportEvent(HistoricalEventType.FactionPresenceEstablished, EventSeverity.Info, EventImportance.Notable,
                new[] { s.SettlementId }, new[] { f.FactionId }, Facts().Set("strength", _rules.NewPresenceStrength));
        }

        private void PursueClaim(SimulationDayContext context, FactionData f, FactionGoalData g)
        {
            var world = context.World;
            var s = world.FindSettlement(g.TargetSettlementId);
            if (s == null || s.FactionId == f.FactionId || !FactionRules.CanClaimTerritory(f.Type)) return;
            var presence = f.FindPresence(s.SettlementId);
            if (presence == null || presence.Strength < _rules.MinStrengthToClaim) return;
            foreach (var c in world.Claims)
            {
                if (c.IsOpen && c.ClaimantId == f.FactionId && c.SettlementId == s.SettlementId) return;
            }

            var claim = new TerritorialClaimData
            {
                ClaimId = new ClaimId(DeterministicGuid.Next(context.Random)),
                ClaimantId = f.FactionId,
                SettlementId = s.SettlementId,
                Status = ClaimStatus.Unresolved,
                FiledOn = context.Date,
                UpdatedOn = context.Date,
            };
            world.Claims.Add(claim);
            context.ReportEvent(HistoricalEventType.TerritorialClaimFiled, EventSeverity.Info, EventImportance.Notable,
                new[] { s.SettlementId }, ClaimFactions(f.FactionId, s), Facts().Set("claim", claim.ClaimId.ToString()));
        }

        // ----- 7. Claims ---------------------------------------------------------------------------

        private void EvaluateClaims(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var c in world.Claims)
            {
                if (!c.IsOpen) continue;
                var s = world.FindSettlement(c.SettlementId);
                var claimant = world.FindFaction(c.ClaimantId);
                if (s == null || claimant == null) continue;

                if (s.FactionId == c.ClaimantId)
                {
                    SetClaim(context, c, s, ClaimStatus.Fulfilled, "governs");
                    continue;
                }

                // The simulation only answers claims nobody has answered; decisions already made
                // (by the simulation or the player) stand until someone changes them.
                if (c.Status != ClaimStatus.Unresolved) continue;

                var competing = false;
                foreach (var other in world.Claims)
                {
                    if (other != c && other.IsOpen && other.SettlementId == c.SettlementId && other.ClaimantId != c.ClaimantId) competing = true;
                }

                if (competing)
                {
                    SetClaim(context, c, s, ClaimStatus.Contested, "competingClaim");
                    continue;
                }

                if (s.FactionId.IsEmpty)
                {
                    var presence = claimant.FindPresence(s.SettlementId);
                    if (presence != null && presence.Strength >= _rules.UncontestedClaimStrength) SetClaim(context, c, s, ClaimStatus.Recognized, "noAuthority");
                    continue;
                }

                if (s.AdministratorFactionId == c.ClaimantId)
                {
                    SetClaim(context, c, s, ClaimStatus.Recognized, "administrator");
                    continue;
                }

                var relation = world.FindRelation(c.ClaimantId, s.FactionId);
                if (relation == null) continue;
                var state = PoliticalMath.StateOf(relation);
                if (state == RelationState.Allied || state == RelationState.Friendly) SetClaim(context, c, s, ClaimStatus.Recognized, "goodRelations");
                else if (state == RelationState.Rival || state == RelationState.Hostile) SetClaim(context, c, s, ClaimStatus.Contested, "poorRelations");
            }
        }

        private static void SetClaim(SimulationDayContext context, TerritorialClaimData c, SettlementData s, ClaimStatus status, string cause)
        {
            c.Status = status;
            c.UpdatedOn = context.Date;
            var contested = status == ClaimStatus.Contested;
            context.ReportEvent(contested ? HistoricalEventType.TerritorialClaimContested : HistoricalEventType.TerritorialClaimResolved,
                contested ? EventSeverity.Warning : EventSeverity.Info, EventImportance.Notable,
                new[] { s.SettlementId }, ClaimFactions(c.ClaimantId, s),
                Facts().Set("claim", c.ClaimId.ToString()).Set("outcome", status.ToString()).Set("cause", cause));
        }

        /// <summary>Claimant first, then the settlement's governing authority when there is one.</summary>
        public static IReadOnlyList<FactionId> ClaimFactions(FactionId claimant, SettlementData s) =>
            s.FactionId.IsEmpty || s.FactionId == claimant ? new[] { claimant } : new[] { claimant, s.FactionId };

        // ----- 8. Status ---------------------------------------------------------------------------

        private void UpdateStatus(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var f in world.Factions)
            {
                if (f.IsDissolved) continue;
                var before = f.Status;
                if (f.Influence <= _rules.DormantInfluence && !PoliticalMath.GovernsAnything(world, f))
                {
                    f.Status = FactionStatus.Dormant;
                    f.DormantTicks++;
                    if (f.DormantTicks >= _rules.DissolveAfterDormantTicks)
                    {
                        PoliticalMath.Dissolve(world, f, context.Date);
                        context.ReportEvent(HistoricalEventType.FactionDissolved, EventSeverity.Warning, EventImportance.Major,
                            Array.Empty<SettlementId>(), new[] { f.FactionId }, Facts().Set("cause", "dormancy").Set("ticks", f.DormantTicks));
                        continue;
                    }
                }
                else
                {
                    f.DormantTicks = 0;
                    if (f.Influence < _rules.DecliningInfluence) f.Status = FactionStatus.Declining;
                    else if (f.Influence >= _rules.RecoveryInfluence || before != FactionStatus.Declining) f.Status = FactionStatus.Active;
                }

                if (f.Status == before) continue;
                context.ReportEvent(HistoricalEventType.FactionStatusChanged,
                    f.Status == FactionStatus.Active ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Notable,
                    Array.Empty<SettlementId>(), new[] { f.FactionId },
                    Facts().Set("from", before.ToString()).Set("to", f.Status.ToString()).Set("influence", f.Influence));
            }
        }
    }
}
