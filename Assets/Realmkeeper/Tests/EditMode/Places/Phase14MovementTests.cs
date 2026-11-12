using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Places;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 (ADR-0070): people move through rooms along real passages; followers keep a room of their own, wait when
    /// the way is shut, and catch up when it opens.
    /// </summary>
    public sealed class Phase14MovementTests : Phase14Fixture
    {
        [Test]
        public void Residents_MoveThroughTheirHouse_AlongRealPassages_EachOnTheirOwnRhythm()
        {
            H.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            var checkedHouses = 0;
            var moves = 0;
            var timetables = new Dictionary<string, string>();
            foreach (var b in layout.Buildings.Where(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None &&
                                                          InteriorGenerator.HasInterior(b.Function.Value)).Take(8))
            {
                if (W.Player.IsInPlace) Ok(S.Dispatch(new ExitPlaceCommand()));
                if (!S.Dispatch(new EnterBuildingCommand(b.Key)).IsSuccess) continue;
                var home = W.FindPlace(W.Player.PlaceId);
                if (home.Layout.Rooms.Count < 2) continue;
                checkedHouses++;
                var day = W.CurrentSimulationDate.ToDayNumber() * 1440L;
                for (var m = 0; m < 1440; m += 10)
                {
                    var all = IndoorSchedule.Everyone(W, home, day + m);
                    Assert.That(all.Select(p => p.Npc.NpcId).Distinct().Count(), Is.EqualTo(all.Count), "Nobody twice.");
                    Assert.That(all.Select(p => (p.X, p.Z)).Distinct().Count(), Is.EqualTo(all.Count), "Nobody on another's cell.");
                    foreach (var p in all)
                    {
                        Assert.That(home.Layout.Room(p.Room), Is.Not.Null);
                        if (p.FromRoom != p.Room)
                        {
                            moves++;
                            Assert.That(p.Route, Is.Not.Empty, "A move between rooms has a route.");
                            var at = p.FromRoom;
                            foreach (var step in p.Route)
                            {
                                var c = home.Layout.Connection(step.ConnectionId);
                                Assert.That(c.Joins(at), Is.True, "Each passage leaves the room they are in: no walking through walls.");
                                Assert.That(PlaceRoutes.Passable(home, c, at, Mover.Resident), Is.True);
                                at = step.RoomId;
                            }

                            Assert.That(at, Is.EqualTo(p.Room));
                        }

                        var key = p.Npc.NpcId.ToString();
                        timetables[key] = (timetables.TryGetValue(key, out var t) ? t : string.Empty) + p.Room + "|";
                    }

                    // Deterministic: asking again gives the same.
                    if (m % 120 == 0) Assert.That(IndoorSchedule.Everyone(W, home, day + m).Select(p => p.Room + p.X + p.Z), Is.EqualTo(all.Select(p => p.Room + p.X + p.Z)));
                }
            }

            Assert.That(checkedHouses, Is.GreaterThan(0));
            Assert.That(moves, Is.GreaterThan(0), "People do move between rooms over a day.");
            var distinct = timetables.Values.Distinct().Count();
            Assert.That(distinct, Is.GreaterThan(1), "Not everyone keeps the same timetable.");
            TestContext.WriteLine($"{checkedHouses} buildings, {timetables.Count} people, {moves} moves seen, {distinct} distinct timetables.");
        }

        [Test]
        public void ALockedDoor_IsNoWayForAResidentWhoHasNoKey_NorForAFollower_ButAResidentsOwnLockIs()
        {
            var place = new PlaceData { Category = PlaceCategory.Interior, Layout = new PlaceLayout() };
            place.Layout.Rooms.Add(new PlaceRoom { Id = "a", Rect = new CellRect(0, 0, 4, 4) });
            place.Layout.Rooms.Add(new PlaceRoom { Id = "b", Rect = new CellRect(5, 0, 4, 4) });
            place.Layout.Connections.Add(new PlaceConnection { Id = "d", Kind = ConnectionKind.Door, A = "a", B = "b", Door = DoorState.Locked, Rect = new CellRect(4, 1, 1, 2) });
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Resident), Is.Not.Null, "Their own house, their own key.");
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Follower), Is.Null, "A follower does not pass a locked door.");
            place.Category = PlaceCategory.Crypt;
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Resident), Is.Null, "Not their lock.");
            place.Doors["d"] = DoorState.Open;
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Follower).Single().RoomId, Is.EqualTo("b"));
        }

        [Test]
        public void AFollower_FollowsThroughOpenWays_WaitsAtALockedDoor_AndCatchesUpWhenItOpens()
        {
            H.Hero();
            var mine = Enter(PlaceCatalog.Mine);
            var captive = mine.Layout.Features.Single(f => f.Kind == FeatureKind.Captive);
            H.WalkTo(captive.Room);
            Ok(S.Dispatch(new PlaceActionCommand(captive.Id, PlaceAction.Free)));
            for (var i = 0; i < 2 && W.Escorts.Single().State != EscortState.Following; i++) Ok(S.Dispatch(new PlaceActionCommand(captive.Id, PlaceAction.Free)));
            Assume.That(W.Escorts.Single().State, Is.EqualTo(EscortState.Following));
            var e = W.Escorts.Single();
            Assert.That(e.PlaceId, Is.EqualTo(mine.PlaceId));
            Assert.That(e.Room, Is.EqualTo(captive.Room), "They stand where they were freed.");

            // Walk back towards the way out: they come along, room by room.
            var path = PlaceSystemTests.Path(Place(PlaceCatalog.Mine), W.Player.PlaceRoom, mine.Layout.Entrance.A);
            var first = path.First(p => !Place(PlaceCatalog.Mine).Layout.Connection(p.Via).IsVertical);
            H.WalkTo(path[0].Room);
            Assert.That(W.Escorts.Single().Room, Is.EqualTo(W.Player.PlaceRoom), "Beside the player.");

            // The test shuts and locks a door between them (fault injection), and the player walks on.
            var here = W.Player.PlaceRoom;
            var on = Place(PlaceCatalog.Mine).Layout.Connections.First(c => !c.IsVertical && c.Kind != ConnectionKind.Entrance && c.Kind != ConnectionKind.Secret && c.Joins(here));
            var beyond = on.Other(here);
            H.Edit(w =>
            {
                var p = w.FindPlace(mine.PlaceId);
                w.Player.PlaceRoom = beyond;
                var r = p.Layout.Room(beyond);
                w.Player.PlaceFloor = r.Floor;
                w.Player.Pose = PlaceRules.PoseAt(p, r.Floor, r.Rect.CenterX, r.Rect.CenterZ, 0f);
                // Every way out of the follower's room is a locked door now (openings the test fits with doors), so no way round either.
                foreach (var c in p.Layout.Connections)
                {
                    if (c.IsVertical || c.Kind == ConnectionKind.Entrance || c.Kind == ConnectionKind.Secret || !c.Joins(here)) continue;
                    if (c.Kind != ConnectionKind.Door) { c.Kind = ConnectionKind.Door; c.Door = DoorState.Closed; c.LockDc = 12; }
                    p.Doors[c.Id] = DoorState.Locked;
                }
            });
            Assume.That(Place(PlaceCatalog.Mine).Layout.Connections.Any(c => c.IsVertical && c.Joins(here)), Is.False, "No ladder out of that room.");
            // The next move tells: they cannot follow and wait, with the reason.
            var next = Place(PlaceCatalog.Mine).Layout.Connections.FirstOrDefault(c => !c.IsVertical && c.Kind != ConnectionKind.Entrance && c.Kind != ConnectionKind.Secret && c.Id != on.Id && c.Joins(beyond)
                                                                                            && PlaceRules.Blocks(Place(PlaceCatalog.Mine), c, beyond) == null);
            var stepTo = next?.Other(beyond) ?? beyond;
            var r2 = Place(PlaceCatalog.Mine).Layout.Room(stepTo);
            var moved = S.Dispatch(new MoveInPlaceCommand(stepTo, PlaceRules.PoseAt(Place(PlaceCatalog.Mine), r2.Floor, r2.Rect.CenterX, r2.Rect.CenterZ, 0f)));
            Ok(moved);
            e = W.Escorts.Single();
            Assert.That(e.State, Is.EqualTo(EscortState.Waiting), "The way is locked: they wait.");
            Assert.That(e.Room, Is.EqualTo(here), "Where they were — never through the locked door.");
            Assert.That(S.GetFollowers().Single().State, Does.Contain("locked door"));
            Valid();

            // The doors open: on the next move they catch up.
            H.Edit(w => { foreach (var c in w.FindPlace(mine.PlaceId).Layout.Connections) if (c.Joins(here) && c.Kind == ConnectionKind.Door) w.FindPlace(mine.PlaceId).Doors[c.Id] = DoorState.Open; });
            var back = Place(PlaceCatalog.Mine).Layout.Room(beyond);
            Ok(S.Dispatch(new MoveInPlaceCommand(beyond, PlaceRules.PoseAt(Place(PlaceCatalog.Mine), back.Floor, back.Rect.CenterX, back.Rect.CenterZ, 0f))));
            e = W.Escorts.Single();
            Assert.That(e.State, Is.EqualTo(EscortState.Following));
            Assert.That(e.Room, Is.EqualTo(W.Player.PlaceRoom));
            Assert.That(W.Npcs.Count(n => n.NpcId == e.NpcId), Is.EqualTo(1), "The same person throughout.");
            Valid();
            Assert.That(first.Via, Is.Not.Null);
        }

        [Test]
        public void LeavingAPlace_TakesOnlyThoseWithThePlayer_TheOthersStayWhereTheyWait()
        {
            H.Hero();
            var mine = Enter(PlaceCatalog.Mine);
            var captive = mine.Layout.Features.Single(f => f.Kind == FeatureKind.Captive);
            H.WalkTo(captive.Room);
            for (var i = 0; i < 3 && W.Escorts.Single().State != EscortState.Following; i++) Ok(S.Dispatch(new PlaceActionCommand(captive.Id, PlaceAction.Free)));
            Assume.That(W.Escorts.Single().State, Is.EqualTo(EscortState.Following));
            // The follower is left behind in the den (the test moves the player to the way out alone).
            H.Edit(w =>
            {
                var p = w.FindPlace(mine.PlaceId);
                var r = p.Layout.Room(p.Layout.Entrance.A);
                w.Player.PlaceRoom = r.Id;
                w.Player.PlaceFloor = r.Floor;
                w.Player.Pose = PlaceRules.PoseAt(p, r.Floor, r.Rect.CenterX, r.Rect.CenterZ, 0f);
            });
            var left = S.Dispatch(new ExitPlaceCommand());
            Ok(left);
            Assert.That(left.Message, Does.Contain("behind"));
            var e = W.Escorts.Single();
            Assert.That(e.State, Is.EqualTo(EscortState.Waiting));
            Assert.That(e.PlaceId, Is.EqualTo(mine.PlaceId));
            Assert.That(e.Room, Is.EqualTo(captive.Room));
            Assert.That(S.GetFollowers().Single().State, Does.Contain("left behind"));
            Valid();

            // Back in, and to them: they are still there, and come along again.
            Ok(S.Dispatch(new EnterPlaceCommand(mine.PlaceId)));
            H.WalkTo(captive.Room);
            e = W.Escorts.Single();
            Assert.That(e.State, Is.EqualTo(EscortState.Following));
            Valid();
        }
    }
}
