using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 13 costs (.NET, Release; the numbers are printed and bounded loosely — they are not production figures):
    /// generating layouts, validating them, the world with many interiors, the scene and map queries, moving between rooms,
    /// a fight's walled grid and its AI routes, and saving and loading with every place.
    /// </summary>
    public sealed class PlacePerformanceTests
    {
        [Test]
        public void Places_AreCheap()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                // Generation and validation.
                var sw = Stopwatch.StartNew();
                var n = 0;
                foreach (var a in ArchetypeCatalog.All)
                    for (var seed = 1UL; seed <= 50; seed++) { PlaceGenerator.Generate(a, seed * 31UL); n++; }
                var generate = sw.Elapsed.TotalMilliseconds;
                var big = PlaceGenerator.Generate(ArchetypeCatalog.Find("catacomb"), 77UL);
                sw.Restart();
                for (var i = 0; i < 100; i++) PlaceValidator.Validate(big);
                var validate = sw.Elapsed.TotalMilliseconds / 100;

                // A world with its places and 40 interiors entered.
                var s = Phase2Worlds.Session(dir);
                ExplorationModule.Register(s);
                CombatTests.Create(s, "fighter", PlaceSystemTests.Strong);
                var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(s.CurrentWorldSnapshotForTests());
                Assert.That(s.Dispatch(new EnterSettlementCommand(town.SettlementId)).IsSuccess, Is.True);
                var layout = ExplorationModule.LayoutFor(s.CurrentWorldSnapshotForTests(), s.CurrentWorldSnapshotForTests().FindSettlement(town.SettlementId));
                var doors = layout.Buildings.Where(b => b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None && b.Function != null && InteriorGenerator.HasInterior(b.Function.Value)).Take(40).ToList();
                sw.Restart();
                foreach (var b in doors)
                {
                    var r = s.Dispatch(new EnterBuildingCommand(b.Key));
                    Assert.That(r.IsSuccess, Is.True, r.Message);
                    var w = s.CurrentWorldSnapshotForTests();
                    var place = w.FindPlace(w.Player.PlaceId);
                    var exitRoom = place.Layout.Entrance.A;
                    if (w.Player.PlaceRoom != exitRoom) continue;
                    Assert.That(s.Dispatch(new ExitPlaceCommand()).IsSuccess, Is.True);
                }

                var interiors = sw.Elapsed.TotalMilliseconds / Math.Max(1, doors.Count);
                var world = s.CurrentWorldSnapshotForTests();
                if (world.Player.IsInPlace) Assert.That(s.Dispatch(new ExitPlaceCommand()).IsSuccess, Is.True);

                // The mine: scene and map queries, and a move between rooms.
                var mine = world.Places.Single(p => p.DefinitionId == PlaceCatalog.Mine);
                var copy = s.CurrentWorldSnapshotForTests();
                copy.FindPlace(mine.PlaceId).Discovery = PlaceDiscovery.Discovered;
                Assert.That(new JsonWorldStore(dir).Save(copy, "perf").IsSuccess, Is.True);
                Assert.That(s.Execute(new LoadWorldCommand("perf")).IsSuccess, Is.True);
                Assert.That(s.Dispatch(new ExitSettlementCommand()).IsSuccess, Is.True);
                Assert.That(s.Dispatch(new EnterPlaceCommand(mine.PlaceId)).IsSuccess, Is.True);
                sw.Restart();
                for (var i = 0; i < 100; i++) s.GetPlaceScene();
                var scene = sw.Elapsed.TotalMilliseconds / 100;
                sw.Restart();
                for (var i = 0; i < 100; i++) s.GetPlaceMap();
                var map = sw.Elapsed.TotalMilliseconds / 100;
                sw.Restart();
                for (var i = 0; i < 100; i++) s.GetKnownPlaces();
                var known = sw.Elapsed.TotalMilliseconds / 100;
                var here = s.CurrentWorldSnapshotForTests().Player.PlaceRoom;
                sw.Restart();
                for (var i = 0; i < 20; i++)
                {
                    var room = mine.Layout.Room(here);
                    s.Dispatch(new MoveInPlaceCommand(here, PlaceRules.PoseAt(mine, room.Floor, room.Rect.CenterX, room.Rect.CenterZ, 0f)));
                }

                var move = sw.Elapsed.TotalMilliseconds / 20;

                // A fight's walled grid: the AI's route around walls.
                var setup = new EncounterSetup { Name = "Walls", Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal };
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "giant_rat", Name = "Rat", Side = "foes", Position = new GridPos(10, 0) });
                for (var y = -10; y <= 10; y++) setup.Blocked.Add(new GridPos(5, y));
                var cw = s.CurrentWorldSnapshotForTests();
                var enc = CombatEngine.Create(cw, setup, _ => Guid.NewGuid(), out _);
                cw.Encounters.Add(enc);
                var engine = new CombatEngine(cw, enc);
                var rat = enc.Combatants.Single(c => c.Kind == CombatantKind.Creature);
                var player = enc.Combatants.Single(c => c.Kind == CombatantKind.Player);
                sw.Restart();
                for (var i = 0; i < 100; i++) CombatAi.PathAround(engine, rat, player, 5);
                var route = sw.Elapsed.TotalMilliseconds / 100;

                // Saving and loading with every place.
                var serializer = new JsonWorldSerializer();
                var all = s.CurrentWorldSnapshotForTests();
                sw.Restart();
                var json = serializer.Serialize(all);
                var save = sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                var loaded = serializer.Deserialize(json);
                var load = sw.Elapsed.TotalMilliseconds;
                Assert.That(loaded.IsSuccess, Is.True);
                var placesJson = Newtonsoft.Json.Linq.JObject.Parse(json)["world"]["places"].ToString().Length;

                TestContext.WriteLine($"Generation: {n} layouts in {generate:0} ms ({generate / n:0.00} ms each); validating a {big.Rooms.Count}-room catacomb: {validate:0.000} ms");
                TestContext.WriteLine($"Interiors: {doors.Count} entered (planned, filled, entered, left) at {interiors:0.00} ms each; places in the world: {all.Places.Count}");
                TestContext.WriteLine($"Mine queries: scene {scene:0.000} ms, map {map:0.000} ms, known places {known:0.000} ms; a move into a room (command, validation) {move:0.00} ms");
                TestContext.WriteLine($"AI route round a 21-square wall: {route:0.000} ms");
                TestContext.WriteLine($"Save {json.Length / 1024} KB in {save:0} ms, load {load:0} ms; places section {placesJson / 1024} KB");
                Assert.That(generate / n, Is.LessThan(50), "Generating a layout");
                Assert.That(scene, Is.LessThan(20), "The scene query");
                Assert.That(move, Is.LessThan(200), "A move between rooms");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
