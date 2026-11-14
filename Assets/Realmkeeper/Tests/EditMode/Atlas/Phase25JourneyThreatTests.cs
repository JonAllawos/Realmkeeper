using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 25 closes a Phase 24 verification gap. The Phase 24 PlayMode test only settles a road threat if one happens
    /// to appear, and its isolated re-run met none. This test makes one appear deterministically: a strong band stationed
    /// on a short road (the Phase 16 tests do the same). It then checks the journey step-off end to end:
    /// <list type="number">
    /// <item>outlaws bar the road, so stepping off into the country is refused with the reason;</item>
    /// <item>paying their toll settles them;</item>
    /// <item>stepping off then halts the journey on the road;</item>
    /// <item>rejoining it takes the journey up again.</item>
    /// </list>
    /// </summary>
    public sealed class Phase25JourneyThreatTests : Phase16Fixture
    {
        [Test]
        public void SteppingOff_IsRefusedWhileOutlawsBarTheRoad_AndAllowedOnceTheyAreSettled()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            var start = In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementType == SettlementType.Town);
            Edit(w => { w.MinuteOfDay = 7 * 60; w.Player.Purse = 2000; });
            Ok(S.Dispatch(new EnterSettlementCommand(start.SettlementId)));
            var to = In(Home).Where(s => s.SettlementId != start.SettlementId).OrderBy(s => s.SettlementId).First();
            var seg = W.FindRoute(start.SettlementId, to.SettlementId);
            Assume.That(seg, Is.Not.Null);
            Arm(seg, 6);
            Ok(S.Dispatch(new StartJourneyCommand(to.SettlementId)));
            for (var i = 0; i < 30 && W.Journey.Status == JourneyStatus.Travelling; i++) S.Travel(1, true);
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Interrupted), "Six outlaws on a short road bar it within the day.");
            Assert.That(W.Journey.EncounterId, Does.StartWith("threat:"));

            var refused = S.Dispatch(new EnterOverlandCommand());
            Assert.That(refused.IsSuccess, Is.False, "Stepping off does not get round outlaws on the road.");
            Assert.That(refused.Message, Does.Contain("Outlaws bar the road"));

            Ok(S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.PayToll)));
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));
            var off = S.Dispatch(new EnterOverlandCommand());
            Ok(off);
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Halted), "Stepping off halts the journey where it was.");
            Assert.That(W.Player.LocationKind, Is.EqualTo(CharacterLocationKind.Overland));
            Assert.That(W.Player.Waypoint, Is.EqualTo("journey"), "The compass marks the journey's point.");
            Ok(S.Dispatch(new RejoinJourneyCommand()));
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling), "Rejoined where it was left.");
            Valid();
        }

        /// <summary>A strong road band on a road, as Phase 16's tests station one.</summary>
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
                        PopulationId = "bandit@road:p25", RegionId = seg.RegionA, CreatureId = "bandit", Habitat = "Road", Center = new MapCoord((a.Position.X + b.Position.X) / 2, (a.Position.Z + b.Position.Z) / 2),
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
    }
}
