using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Quests;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 17, Phase 16 closeout (ADR-0086): a fight met on the road is the one engine's encounter, staged on the ground
    /// at the road's nearer end, and the party stays on the road until the player leaves the fight and chooses to travel
    /// on; groups beyond the player's sight show only where a report puts them, as of the report.
    /// </summary>
    public sealed class Phase17AtlasTests : Phase16Fixture
    {
        private SettlementData Start => In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementType == SettlementType.Town);

        private void Hero()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            Edit(w => { w.MinuteOfDay = 7 * 60; w.Player.Purse = 2000; });
            Ok(S.Dispatch(new EnterSettlementCommand(Start.SettlementId)));
        }

        private void Arm(RouteSegmentData seg, int count)
        {
            Edit(w =>
            {
                var a = w.FindSettlement(seg.A);
                var b = w.FindSettlement(seg.B);
                var p = w.CreaturePopulations.FirstOrDefault(x => x.PopulationId == "bandit@road:p17");
                if (p == null)
                {
                    p = new CreaturePopulationData
                    {
                        PopulationId = "bandit@road:p17", RegionId = seg.RegionA, CreatureId = "bandit", Habitat = "Road", Center = new MapCoord((a.Position.X + b.Position.X) / 2, (a.Position.Z + b.Position.Z) / 2),
                        RangeCells = 30, Capacity = 12, Count = count, LastUpdatedOn = w.CurrentSimulationDate,
                    };
                    w.CreaturePopulations.Add(p);
                }

                var g = new TravelingGroupData { GroupId = "bandits:" + p.PopulationId, Kind = TravelGroupKind.Bandits, Name = "Outlaws", PopulationId = p.PopulationId, StationRouteId = seg.RouteId, State = TravelGroupState.Stationed, Members = count, Strength = count };
                g.Segments.Add(seg.RouteId);
                g.Nodes.Add(seg.A);
                g.Nodes.Add(seg.B);
                w.Groups.Add(g);
            });
        }

        /// <summary>Sets out on a short road with six outlaws on it, until they bar the way.</summary>
        private void MeetOutlaws()
        {
            Hero();
            var to = In(Home).Where(s => s.SettlementId != Start.SettlementId).OrderBy(s => s.SettlementId).First(s => W.FindRoute(Start.SettlementId, s.SettlementId) != null);
            Arm(W.FindRoute(Start.SettlementId, to.SettlementId), 6);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            for (var i = 0; i < 30 && W.Journey.Status == JourneyStatus.Travelling; i++) S.Travel(1, true);
            Assume.That(W.Journey.EncounterId, Does.StartWith("threat:"), "The outlaws did not bar the road this time.");
        }

        // 16, 17 ---------------------------------------------------------------------------------------------

        [Test]
        public void AFightOnTheRoad_IsStagedFromTheRoad_AndThePartyStaysOnTheRoadUntilThePlayerLeavesIt()
        {
            MeetOutlaws();
            Assert.That(S.GetRoadFight(), Is.Null, "No fight yet: the outlaws only bar the road.");
            Ok(S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.Fight)));
            var fight = S.GetRoadFight();
            Assert.That(fight, Is.Not.Null, "The fight is one to stage (16).");
            Assert.That(fight.Active, Is.True);
            Assert.That(fight.EncounterId, Is.EqualTo(W.ActiveEncounter.EncounterId.ToString()), "The engine's own encounter, with its own participants.");
            var seg = W.FindRoute(W.Journey.Segments[W.Journey.SegmentIndex]);
            Assert.That(seg.Joins(fight.Ground), Is.True, "Staged at an end of the road the party is on.");
            Assert.That(W.Player.IsInSettlement, Is.False, "The party is on the road, not in a town.");
            Assert.That(W.Player.LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));
            var metres = W.Journey.MetresIntoSegment;
            Assert.That(S.Dispatch(new LeaveRoadFightCommand()).IsSuccess, Is.False, "A fight going on cannot be left (withdraw, or finish it).");

            // Save and load mid-fight: still the same fight on the same road.
            Ok(S.Execute(new SaveWorldCommand("mid-fight")));
            Ok(S.Execute(new LoadWorldCommand("mid-fight")));
            Assert.That(S.GetRoadFight()?.EncounterId, Is.EqualTo(fight.EncounterId));

            EncounterGenerationTests.FightToTheEnd(S);
            if (W.Player.Rpg.IsDead) Assert.Inconclusive("The hero fell.");
            var after = S.GetRoadFight();
            Assert.That(after, Is.Not.Null, "The finished fight stays staged until the player leaves it (loot, the fallen).");
            Assert.That(after.Active, Is.False);
            Ok(S.Dispatch(new LeaveRoadFightCommand()));
            Assert.That(S.GetRoadFight(), Is.Null);
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Halted), "Back on the road, the journey halted (17).");
            Assert.That(W.Journey.MetresIntoSegment, Is.EqualTo(metres), "Where it was.");
            Assert.That(W.Player.IsInSettlement, Is.False);
            Ok(S.Dispatch(new ResumeJourneyCommand()));
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling), "Travel goes on only when the player resumes it.");
            Valid();
        }

        [Test]
        public void WithdrawingFromARoadFight_EndsIt_AndThePartyCanLeaveTheGround()
        {
            MeetOutlaws();
            Ok(S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.Fight)));
            var id = W.ActiveEncounter.EncounterId;
            Ok(S.Dispatch(new EndEncounterCommand(id, true)));
            Assert.That(S.GetRoadFight().Active, Is.False);
            Ok(S.Dispatch(new LeaveRoadFightCommand()));
            Assert.That(W.Journey.Reason, Does.Contain("fight"));
        }

        // 14 -------------------------------------------------------------------------------------------------

        [Test]
        public void GroupsBeyondSight_ShowOnlyWhereAReportPutsThem_AsOfTheReport()
        {
            Hero();
            var here = AtlasQueries.View(W).PlayerRegion;
            var away = W.Routes.Where(r => r.RegionA != here && r.RegionB != here).OrderBy(r => r.RouteId).First();
            Arm(away, 4);
            var band = W.FindGroup("bandits:bandit@road:p17");
            Assert.That(AtlasQueries.View(W).Groups.Any(g => g.GroupId == band.GroupId), Is.False, "Unknown, far away: not shown.");

            // A report reaches the player.
            Edit(w =>
            {
                var e = WorldEvents.Raise(w, WorldEventType.BanditActivity, "bandits:" + band.PopulationId, QuestClock.Now(w), 3, "outlaws on the road", x =>
                {
                    x.RouteId = away.RouteId;
                    x.RegionId = away.RegionA;
                    x.GroupId = band.GroupId;
                }, out _);
                WorldEvents.PlayerLearns(e, EventKnowledge.Rumored, QuestClock.Now(w), "a carter");
            });
            var reported = AtlasQueries.View(W).Groups.Single(g => g.GroupId == band.GroupId);
            Assert.That(reported.Seen, Is.False, "Reported, not seen.");
            Assert.That(reported.ReportedFrom, Is.EqualTo("a carter"));
            Assert.That(reported.Strength, Is.EqualTo(-1), "Its numbers are not known from here.");
            Assert.That(reported.History, Is.Empty, "Nor what it has done since.");
            var (mx, mz) = AtlasQueries.Along(W, away, away.A, 0.5);
            Assert.That(reported.X, Is.EqualTo(mx).Within(0.01), "Marked where the report placed it.");
            Assert.That(reported.Z, Is.EqualTo(mz).Within(0.01));

            // In the player's own region, a group is seen where it is.
            var near = W.Groups.FirstOrDefault(g => g.IsActive && AtlasQueries.GroupAt(W, g, QuestClock.Now(W)).region == here);
            if (near != null) Assert.That(AtlasQueries.View(W).Groups.Single(g => g.GroupId == near.GroupId).Seen, Is.True);
        }
    }
}
