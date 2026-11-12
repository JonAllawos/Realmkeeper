using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0078): travel as a world action, with the party. Within a region and across a border; alternatives
    /// compared; terrain and roads costed; the clock moving; outlaws barring the road (fought, slipped past or paid);
    /// redirection; saving mid-journey; bridges falling and mended; three companions along, reserves at home.
    /// </summary>
    public sealed class Phase16TravelTests : Phase16Fixture
    {
        private SettlementData Start => In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementType == SettlementType.Town);

        private void Hero()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            Edit(w => { w.MinuteOfDay = 7 * 60; w.Player.Purse = 2000; });
            Ok(S.Dispatch(new EnterSettlementCommand(Start.SettlementId)));
        }

        /// <summary>Travels until the journey ends or stops (through nights too), at most a fortnight.</summary>
        private void TravelOn(bool throughTheNight = true)
        {
            for (var i = 0; i < 40; i++)
            {
                var j = W.Journey;
                if (j == null || j.Status == JourneyStatus.Arrived) return;
                if (j.Status == JourneyStatus.Interrupted)
                {
                    if (j.EncounterId == null || !j.EncounterId.StartsWith("threat:")) return;
                    Ok(S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.PayToll))); // the road's own outlaws: pay and go on
                    continue;
                }
                if (j.Status == JourneyStatus.Halted) Ok(S.Dispatch(new ResumeJourneyCommand()));
                S.Travel(24, throughTheNight);
            }
        }

        private SettlementData Neighbour(int dx, int dz) => W.Settlements.First(s => W.FindRegion(s.RegionId) is RegionData r && r.AtlasX == Home.AtlasX + dx && r.AtlasZ == Home.AtlasZ + dz);

        // 8, 12 -------------------------------------------------------------------------------------------

        [Test]
        public void ATripWithinTheVale_TakesTheRoutesHours_OnTheWorldsClock_AndEndsInTheSettlement()
        {
            Hero();
            var to = In(Home).Where(s => s.SettlementId != Start.SettlementId).OrderBy(s => s.SettlementId).First();
            var options = S.GetTravelOptions(to.SettlementId);
            Assert.That(options, Is.Not.Empty);
            var before = Realmkeeper.Simulation.Quests.QuestClock.Now(W);
            var expected = options[0].Hours;
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            Assert.That(W.Player.IsInSettlement, Is.False, "On the road.");
            TravelOn();
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived));
            Assert.That(W.Player.SettlementId, Is.EqualTo(to.SettlementId), "Entered the settlement on arrival.");
            var elapsed = (Realmkeeper.Simulation.Quests.QuestClock.Now(W) - before) / 60.0;
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(System.Math.Floor(expected)), "The world's clock moved by the walking (at least).");
            Assert.That(W.Journey.HoursTravelled, Is.InRange((int)System.Math.Floor(expected) - 1, (int)System.Math.Ceiling(expected * 2) + 1), "Daylight hours at the estimate; night hours at half pace.");
            Valid();
        }

        // 9 ----------------------------------------------------------------------------------------------

        [Test]
        public void AcrossTheBorder_ThePartyEntersTheNextRegion_AndTheMapFollows()
        {
            Hero();
            var to = Neighbour(1, 0);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            TravelOn();
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived), W.Journey.Reason);
            Assert.That(W.Player.RegionId, Is.EqualTo(to.RegionId));
            Assert.That(W.FindRegion(to.RegionId).Discovery, Is.EqualTo(RegionDiscovery.Visited));
            Assert.That(S.Map.RegionId, Is.EqualTo(to.RegionId), "The map shown is now that region's.");
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.JourneyMilestone), Is.True);
            Valid();
        }

        [Test]
        public void AnotherRegionsSettlement_CannotBeEnteredWithoutTravellingThere()
        {
            Hero();
            Ok(S.Dispatch(new ExitSettlementCommand()));
            var r = S.Dispatch(new EnterSettlementCommand(Neighbour(1, 0).SettlementId));
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Message, Does.Contain("travel there first"));
        }

        // 10, 11 -------------------------------------------------------------------------------------------

        [Test]
        public void Alternatives_AreRealRoutes_CostedFromTheirTerrain_CheapestFirst()
        {
            Hero();
            var far = W.Settlements.First(s => W.FindRegion(s.RegionId) is RegionData r && r.AtlasX == 1 && r.AtlasZ == 1);
            var options = S.GetTravelOptions(far.SettlementId);
            Assert.That(options.Count, Is.GreaterThanOrEqualTo(2), "The grid of regions gives more than one way.");
            Assert.That(options.Select(o => string.Join(">", o.Segments)).Distinct().Count(), Is.EqualTo(options.Count));
            for (var i = 1; i < options.Count; i++) Assert.That(options[i].Hours, Is.GreaterThanOrEqualTo(options[i - 1].Hours));
            foreach (var o in options)
                Assert.That(o.Hours, Is.EqualTo(o.Segments.Sum(id => TravelRules.Hours(W.FindRoute(id)))).Within(0.01), "Estimates come from the route data.");

            var road = new RouteSegmentData { Cells = 20, TerrainPercent = 100 };
            var pass = new RouteSegmentData { Cells = 20, TerrainPercent = 200, Kind = RouteKind.Pass };
            var worn = new RouteSegmentData { Cells = 20, TerrainPercent = 100, Condition = RouteCondition.Worn };
            Assert.That(TravelRules.Hours(pass), Is.EqualTo(TravelRules.Hours(road) * 2).Within(0.001));
            Assert.That(TravelRules.Hours(worn), Is.GreaterThan(TravelRules.Hours(road)));
            Assert.That(TravelRules.Hours(new RouteSegmentData { Cells = 20, HasBridge = true, Bridge = BridgeState.Destroyed }), Is.EqualTo(double.PositiveInfinity));
        }

        // 13, 14, 22 -----------------------------------------------------------------------------------------

        [Test]
        public void OutlawsOnTheRoad_StopTheJourney_AndAreFoughtThroughTheOneEngine_ThenTheJourneyGoesOn()
        {
            Hero();
            var to = In(Home).Where(s => s.SettlementId != Start.SettlementId).OrderBy(s => s.SettlementId).First();
            var seg = W.FindRoute(Start.SettlementId, to.SettlementId);
            Assume.That(seg, Is.Not.Null);
            Arm(seg, 6);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            for (var i = 0; i < 30 && W.Journey.Status == JourneyStatus.Travelling; i++) S.Travel(1, true);
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Interrupted), "With six outlaws on a short road, they bar it within the day.");
            Assert.That(W.Journey.EncounterId, Does.StartWith("threat:"));
            Assert.That(S.Dispatch(new ResumeJourneyCommand()).IsSuccess, Is.False, "Nothing is skipped: the threat must be settled.");

            Ok(S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.Fight)));
            Assert.That(W.ActiveEncounter, Is.Not.Null, "A fight in the one engine.");
            Assert.That(W.ActiveEncounter.Combatants.Any(c => c.CreatureId == "bandit" || c.CreatureId == "highwayman"), Is.True);
            var clock = Realmkeeper.Simulation.Quests.QuestClock.Now(W);
            EncounterGenerationTests.FightToTheEnd(S);
            Assert.That(Realmkeeper.Simulation.Quests.QuestClock.Now(W), Is.EqualTo(clock), "Fights do not move the clock.");
            if (W.Player.Rpg.IsDead) Assert.Inconclusive("The hero fell.");
            Ok(S.Dispatch(new ResumeJourneyCommand()));
            TravelOn();
            Assert.That(W.Journey.Status == JourneyStatus.Arrived || W.Journey.Status == JourneyStatus.Interrupted, Is.True);
            Valid();
        }

        [Test]
        public void Outlaws_CanBePaid_OrSlippedPast_WithoutAFight()
        {
            Hero();
            var to = In(Home).Where(s => s.SettlementId != Start.SettlementId).OrderBy(s => s.SettlementId).First();
            var seg = W.FindRoute(Start.SettlementId, to.SettlementId);
            Arm(seg, 6);
            Edit(w => w.Player.Purse = 500);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            for (var i = 0; i < 30 && W.Journey.Status == JourneyStatus.Travelling; i++) S.Travel(1, true);
            Assume.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Interrupted));
            var purse = W.Player.Purse;
            var said = S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.PayToll));
            Ok(said);
            Assert.That(W.Player.Purse, Is.EqualTo(purse - 30), "Five coin an outlaw.");
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));
            Assert.That(W.ActiveEncounter, Is.Null);
        }

        // 15 -------------------------------------------------------------------------------------------------

        [Test]
        public void AJourney_CanBeTurnedFromTheRoad_OrGivenUp()
        {
            Hero();
            var to = Neighbour(1, 0);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            S.Travel(2, true);
            Assume.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));
            var back = Start;
            var options = S.GetTravelOptions(back.SettlementId);
            Assert.That(options, Is.Not.Empty, "Plans start from partway along the road.");
            Ok(S.Dispatch(new StartJourneyCommand(back.SettlementId)));
            Assert.That(W.Journey.DestinationId, Is.EqualTo(back.SettlementId));
            Assert.That(W.Journey.MetresIntoSegment, Is.GreaterThan(0), "Turned on the spot: no jump back to a settlement.");
            Ok(S.Dispatch(new AbandonJourneyCommand()));
            Assert.That(S.Dispatch(new EnterSettlementCommand(back.SettlementId)).IsSuccess, Is.False, "Out on the road: you must walk there.");
            Ok(S.Dispatch(new StartJourneyCommand(back.SettlementId)));
            TravelOn();
            Assert.That(W.Player.SettlementId, Is.EqualTo(back.SettlementId));
        }

        // 16, 24 ----------------------------------------------------------------------------------------------

        [Test]
        public void SavingMidJourney_KeepsEverything_AndTheJourneyContinues()
        {
            Hero();
            Recruit(3);
            Ok(S.Dispatch(new StartJourneyCommand(Neighbour(1, 0).SettlementId)));
            S.Travel(3, true);
            var before = WorldJson();
            Edit(_ => { });
            Assert.That(WorldJson(), Is.EqualTo(before), "Exact: journey, party and clock.");
            Assert.That(W.Party.Count(PartyRole.Active), Is.EqualTo(3));
            TravelOn();
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived), W.Journey.Reason);
        }

        // 17, 18 ----------------------------------------------------------------------------------------------

        [Test]
        public void ARoadThatClosesUnderTheTraveller_LeavesTheWayBack_AndNoWayThrough()
        {
            Hero();
            var seg = W.Routes.First(r => !r.IsCrossing && r.Joins(Start.SettlementId));
            var ahead = W.FindSettlement(seg.Other(Start.SettlementId));
            var plan = S.GetTravelOptions(ahead.SettlementId).First(o => o.Segments.Count == 1 && o.Segments[0] == seg.RouteId);
            Ok(S.Dispatch(new StartJourneyCommand(ahead.SettlementId, plan.Index)));
            S.Travel(2, true);
            Assume.That(W.Journey.Status == JourneyStatus.Travelling && W.Journey.SegmentIndex == 0 && W.Journey.MetresIntoSegment > 0, Is.True);
            Edit(w => { var r = w.FindRoute(seg.RouteId); r.HasBridge = true; r.Bridge = BridgeState.Destroyed; });

            Assert.That(S.GetTravelOptions(ahead.SettlementId).All(o => !double.IsInfinity(o.Hours)), Is.True, "Only ways that can be walked are offered.");
            Assert.That(S.GetTravelOptions(ahead.SettlementId).All(o => o.Segments.Count > 1 || o.Segments[0] != seg.RouteId), Is.True, "No way through the closure.");
            var home = S.GetTravelOptions(Start.SettlementId);
            Assert.That(home, Is.Not.Empty, "The way back is open: the road closed ahead of where they stood.");
            Assert.That(home[0].Notes, Does.Contain("back the way you came: the road ahead is closed"));
            Ok(S.Dispatch(new StartJourneyCommand(Start.SettlementId)));
            var mid = W.Journey.Clone();
            Edit(_ => { });
            Assert.That(W.Journey.Back, Is.True, "Turning back is saved with the journey.");
            Assert.That(W.Journey.MetresIntoSegment, Is.EqualTo(mid.MetresIntoSegment));
            TravelOn();
            Assert.That(W.Player.IsInSettlement && W.Player.SettlementId == Start.SettlementId, Is.True, "Walked back to where they set out from.");
            Valid();
        }

        [Test]
        public void ABrokenBridge_ClosesTheWay_PlansGoAround_AndAMendedOne_OpensIt()
        {
            Hero();
            var crossing = W.Routes.First(r => r.IsCrossing && (r.Joins(Start.SettlementId) || In(Home).Any(s => r.Joins(s.SettlementId))));
            var far = W.FindSettlement(W.FindSettlement(crossing.A).RegionId == Home.RegionId ? crossing.B : crossing.A);
            Assert.That(S.GetTravelOptions(far.SettlementId).Any(o => o.Segments.Contains(crossing.RouteId)), Is.True);
            Edit(w => { var r = w.FindRoute(crossing.RouteId); r.HasBridge = true; r.Bridge = BridgeState.Destroyed; });
            var around = S.GetTravelOptions(far.SettlementId);
            Assert.That(around.All(o => !o.Segments.Contains(crossing.RouteId)), Is.True, "The planner does not use a fallen bridge.");
            Assert.That(around, Is.Not.Empty, "Another way round exists.");

            // A journey on a way that closes ahead stops there.
            Edit(w => { var r = w.FindRoute(crossing.RouteId); r.Bridge = BridgeState.Intact; });
            var plan = S.GetTravelOptions(far.SettlementId).First(o => o.Segments.Contains(crossing.RouteId));
            Ok(S.Dispatch(new StartJourneyCommand(far.SettlementId, plan.Index)));
            Edit(w => { var r = w.FindRoute(crossing.RouteId); r.Bridge = BridgeState.Destroyed; });
            TravelOn();
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Interrupted));
            Assert.That(W.Journey.Reason, Does.Contain("bridge"));

            Edit(w => { var r = w.FindRoute(crossing.RouteId); r.Bridge = BridgeState.Intact; });
            Assert.That(S.GetTravelOptions(far.SettlementId).Any(o => o.Segments.Contains(crossing.RouteId)), Is.True, "Mended: the way is used again.");
            Ok(S.Dispatch(new ResumeJourneyCommand()));
            TravelOn();
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived));
        }

        // 19, 20, 21, 23, 25 --------------------------------------------------------------------------------

        [Test]
        public void ThePartyTravelsTogether_ReservesStayHome_NobodyIsDuplicated_AndTheMapShowsThem()
        {
            Hero();
            var ids = Recruit(4);
            Ok(S.Dispatch(new SetPartyRoleCommand(ids[3], PartyRole.Reserve)));
            var hp = ids.Take(3).ToDictionary(id => id, id => W.FindNpc(id).Character.Sheet.CurrentHp);
            var reserveHome = W.FindNpc(ids[3]).HomeSettlementId;
            var to = Neighbour(1, 0);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            S.Travel(2, true);
            var atlas = S.GetAtlas();
            Assert.That(atlas.Party.Count, Is.EqualTo(3), "The party is on the map with the player.");
            Assert.That(atlas.Journey, Is.Not.Null);
            Assert.That(atlas.Journey.PercentDone, Is.GreaterThan(0));
            TravelOn();
            Assume.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived));
            Assert.That(W.Party.Members.Count, Is.EqualTo(4));
            Assert.That(W.Party.Members.Select(m => m.NpcId).Distinct().Count(), Is.EqualTo(4), "Nobody twice.");
            foreach (var id in ids.Take(3))
            {
                Assert.That(Realmkeeper.Simulation.Party.CompanionLoyalty.Present(W).Any(n => n.NpcId == id), Is.True, "Still with the player.");
                Assert.That(W.FindNpc(id).Character.Sheet.CurrentHp, Is.EqualTo(hp[id]), "Walking does not heal or hurt.");
            }

            Assert.That(PlacePresence.IsAway(W, W.FindNpc(ids[3])), Is.False, "The reserve lives at home.");
            Assert.That(W.FindNpc(ids[3]).HomeSettlementId, Is.EqualTo(reserveHome));
            Valid();
        }


        [Test]
        public void AMemberToldToWait_KeepsTheSpotTheyStoodOn_ThroughSaving_AndRegroupClearsIt()
        {
            Hero();
            var id = Recruit(1)[0];
            var spot = new LocalPose(12.5f, 3f, -7.25f, 90f);
            Ok(S.Dispatch(new SetFollowModeCommand(id, FollowMode.Wait, spot)));
            Edit(_ => { });
            var waiting = S.GetParty().Members.Single(m => m.NpcId == id);
            Assert.That(waiting.Mode, Is.EqualTo(FollowMode.Wait));
            Assert.That(waiting.WaitPose, Is.EqualTo(spot), "Their spot is kept, and saved: found there on return (the Phase 15 issue).");
            Assert.That(W.Party.Find(id).WaitSettlement, Is.EqualTo(Start.SettlementId));

            Ok(S.Dispatch(new RegroupPartyCommand()));
            var back = S.GetParty().Members.Single(m => m.NpcId == id);
            Assert.That(back.Mode, Is.EqualTo(FollowMode.Follow));
            Assert.That(back.WaitPose, Is.Null, "Regrouped: no spot kept.");

            // Without a spot from the scene, the player's own pose is the spot (a command from anywhere still works).
            Ok(S.Dispatch(new SetFollowModeCommand(id, FollowMode.Wait)));
            Assert.That(S.GetParty().Members.Single(m => m.NpcId == id).WaitPose, Is.EqualTo(W.Player.Pose));
            Valid();
        }
        // ----- helpers ----------------------------------------------------------------------------------------

        /// <summary>Puts a strong road band on a road (the test's licence: the band's numbers).</summary>
        private void Arm(RouteSegmentData seg, int count)
        {
            var band = W.Groups.FirstOrDefault(g => g.Kind == TravelGroupKind.Bandits && g.StationRouteId == seg.RouteId);
            Edit(w =>
            {
                CreaturePopulationData p;
                if (band != null) p = w.CreaturePopulations.First(x => x.PopulationId == band.PopulationId);
                else
                {
                    var a = w.FindSettlement(seg.A);
                    var b = w.FindSettlement(seg.B);
                    p = new CreaturePopulationData
                    {
                        PopulationId = "bandit@road:test", RegionId = seg.RegionA, CreatureId = "bandit", Habitat = "Road", Center = new MapCoord((a.Position.X + b.Position.X) / 2, (a.Position.Z + b.Position.Z) / 2),
                        RangeCells = 30, Capacity = 12, Count = count, LastUpdatedOn = w.CurrentSimulationDate,
                    };
                    w.CreaturePopulations.Add(p);
                }

                p.Count = count;
                var g = w.FindGroup("bandits:" + p.PopulationId) ?? new TravelingGroupData { GroupId = "bandits:" + p.PopulationId, Kind = TravelGroupKind.Bandits, Name = "Outlaws", PopulationId = p.PopulationId };
                if (!w.Groups.Contains(g)) w.Groups.Add(g);
                g.StationRouteId = seg.RouteId;
                g.State = TravelGroupState.Stationed;
                g.Members = count;
                g.Strength = count;
                g.Segments.Clear();
                g.Segments.Add(seg.RouteId);
                g.Nodes.Clear();
                g.Nodes.Add(seg.A);
                g.Nodes.Add(seg.B);
            });
        }

        private List<NpcId> Recruit(int n)
        {
            var picks = W.Npcs.Where(x => !x.IsDeceased && x.HomeSettlementId == Start.SettlementId && (!x.BirthDate.IsValidDate || x.AgeOn(W.CurrentSimulationDate) >= 18) &&
                                          W.FindEscort(x.NpcId) == null && PlacePresence.PostedAt(W, x.NpcId) == null && !W.Groups.Any(g => g.IsActive && g.LeaderNpcId == x.NpcId))
                .OrderBy(x => RecruitRules.HoldsPost(x.Role) ? 1 : 0).ThenBy(x => x.NpcId).Take(n).Select(x => x.NpcId).ToList();
            Assume.That(picks.Count, Is.EqualTo(n));
            Edit(w =>
            {
                foreach (var id in picks)
                {
                    var x = w.FindNpc(id);
                    if (RecruitRules.HoldsPost(x.Role)) x.Role = NpcRole.Resident;
                    x.PlayerRegard.Familiarity = 40;
                    x.PlayerRegard.Trust = 40;
                    x.PlayerRegard.Affinity = 20;
                    x.Personality[NpcTrait.Courage] = 60;
                }
            });
            foreach (var id in picks) Ok(S.Dispatch(new RecruitCompanionCommand(id)));
            return picks;
        }
    }
}
