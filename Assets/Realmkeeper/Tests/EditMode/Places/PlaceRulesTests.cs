using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;
using Realmkeeper.Tests.EditMode.Phase2;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 13 (ADR-0064, ADR-0066): doors, locks and keys; traps and hazards; the fort's gate, side door, key and
    /// garrison; building interiors with their people; walls in fights; migration; and fault injection — every refused
    /// or failed action leaves the world as it was.
    /// </summary>
    public sealed class PlaceRulesTests
    {
        private string _dir;
        private WorldSession _s;
        private PlaceSystemTests _h;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
            _h = new PlaceSystemTests();
            typeof(PlaceSystemTests).GetField("_dir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_h, _dir);
            typeof(PlaceSystemTests).GetField("_s", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_h, _s);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();
        private static void Ok(CommandResult r) => PlaceSystemTests.Ok(r);

        private string WorldJson() => JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"].ToString();

        private void Known(string def) => _h.Edit(w => w.Places.Single(p => p.DefinitionId == def).Discovery = PlaceDiscovery.Discovered);

        private void Refused<T>(T c, string why = null) where T : ICommand
        {
            var before = WorldJson();
            var r = _s.Dispatch(c);
            Assert.That(r.IsSuccess, Is.False, "Should be refused: " + c.GetType().Name);
            if (why != null) Assert.That(r.Message, Does.Contain(why));
            Assert.That(WorldJson(), Is.EqualTo(before), "A refused action changes nothing.");
        }

        // ----- The fort: gate, words, side door, key, trespass ------------------------------------

        [Test]
        public void TheFortGate_IsHeld_UntilTheWatchLetsThePlayerPass()
        {
            _h.Hero();
            Known(PlaceCatalog.Fort);
            var fort = _h.Place(PlaceCatalog.Fort);
            Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId)));
            Assert.That(W.Player.PlaceRoom, Is.EqualTo("gate"), "The gatehouse is public ground: no fight.");
            Assert.That(_s.GetEncounter() == null || _s.GetEncounter().Status != EncounterStatus.Active, Is.True);
            var yard = fort.Layout.Room("yard");
            Refused(new MoveInPlaceCommand("yard", PlaceRules.PoseAt(fort, 0, yard.Rect.CenterX, yard.Rect.CenterZ, 0f)), "bar the way");
            var open = _s.GetPlaceActions("c_gate").Single(o => o.Action == PlaceAction.Open);
            Assert.That(open.Enabled, Is.False, "The watch is at the door.");
            Refused(new PlaceActionCommand("c_gate", PlaceAction.Open), "watching");

            // Words, then coin.
            var guard = fort.Layout.Group("watch").Members[0].NpcId;
            _h.Edit(w => w.Player.Purse = 50);
            Ok(_s.Dispatch(new StartConversationCommand(guard)));
            Assert.That(_s.GetConversation().Responses.Select(r => r.Id), Has.Some.StartsWith("gate:"));
            Ok(_s.Dispatch(new ChooseResponseCommand(_s.GetConversation().Responses.First(r => r.Id.StartsWith("gate:")).Id)));
            var purse = W.Player.Purse;
            Ok(_s.Dispatch(new ChooseResponseCommand("bribe")));
            Assert.That(W.Player.Purse, Is.EqualTo(purse - 10));
            if (_s.GetConversation() != null) Ok(_s.Dispatch(new EndConversationCommand()));
            Assert.That(PlaceRules.HasPassage(W.FindPlace(fort.PlaceId), "watch"), Is.True);
            Ok(_s.Dispatch(new PlaceActionCommand("c_gate", PlaceAction.Open)));
            Ok(_s.Dispatch(new MoveInPlaceCommand("yard", PlaceRules.PoseAt(fort, 0, yard.Rect.CenterX, yard.Rect.CenterZ, 0f))));
            Assert.That(_s.GetEncounter() == null || _s.GetEncounter().Status != EncounterStatus.Active, Is.True, "With leave, the courtyard is open to the player.");
            Assert.That(W.Player.Social.StandingWith(fort.FactionId), Is.GreaterThanOrEqualTo(0));
            _h.Valid();
        }

        [Test]
        public void TheSideDoor_IsLearned_ThenForced_TheArmouryHoldsTheKey_AndTheCourtyardIsTrespass()
        {
            _h.Hero();
            Known(PlaceCatalog.Fort);
            var fort = _h.Place(PlaceCatalog.Fort);
            Assert.That(_s.GetKnownPlaces().Single(p => p.PlaceId == fort.PlaceId).Entrances.Select(e => e.Id), Is.EquivalentTo(new[] { "entrance" }), "The side door is not known.");
            Refused(new EnterPlaceCommand(fort.PlaceId, "side_door", PlaceAction.Force), "no other way");

            // The duty roster in the gatehouse tells of it.
            Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId)));
            Ok(_s.Dispatch(new PlaceActionCommand("roster", PlaceAction.Read)));
            Assert.That(W.Player.Social.Knows("fort_side_door"), Is.True);
            Ok(_s.Dispatch(new ExitPlaceCommand()));
            Assert.That(_s.GetKnownPlaces().Single(p => p.PlaceId == fort.PlaceId).Entrances.Select(e => e.Id), Has.Member("side_door"));

            // Forced (each failure only counts the try).
            for (var i = 0; i < 3 && !W.Player.IsInPlace; i++) Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId, "side_door", PlaceAction.Force)));
            if (!W.Player.IsInPlace)
            {
                Refused(new EnterPlaceCommand(fort.PlaceId, "side_door", PlaceAction.Force), "another time");
                _h.Edit(w => w.FindPlace(fort.PlaceId).Doors["side_door"] = DoorState.Broken);
                Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId, "side_door")));
            }

            Assert.That(W.Player.PlaceRoom, Is.EqualTo("store"));
            _h.WalkTo("armoury");
            Assert.That(_s.GetEncounter() == null || _s.GetEncounter().Status != EncounterStatus.Active, Is.True, "Nobody watches the armoury.");
            var chest = W.FindPlace(fort.PlaceId).Layout.Feature("armoury_chest");
            Ok(_s.Dispatch(new PlaceActionCommand(chest.Id, PlaceAction.Open)));
            var key = PlaceContents.Key(fort.PlaceId, chest.Id);
            var stack = W.FindContainer(key).Items.Single(x => x.ItemId == "fort_key");
            Ok(_s.Dispatch(new TakeFromContainerCommand(key, stack.StackId, 1)));
            Assert.That(W.Player.Inventory.Any(x => x.ItemId == "fort_key"), Is.True, "The key comes out of a real container.");
            Assert.That(W.FindContainer(key).Items.Any(x => x.ItemId == "fort_key"), Is.False, "One key, one holder.");

            // Into the courtyard without leave: the barracks sees.
            var standing = W.Player.Social.StandingWith(fort.FactionId);
            Ok(_s.Dispatch(new PlaceActionCommand("c_armoury", PlaceAction.Open)));
            var yard = fort.Layout.Room("yard");
            var moved = _s.Dispatch(new MoveInPlaceCommand("yard", PlaceRules.PoseAt(fort, 0, yard.Rect.CenterX, yard.Rect.CenterZ, 0f)));
            Ok(moved);
            Assert.That(moved.Message, Does.Contain("stranger inside the walls"));
            Assert.That(_s.GetEncounter().Status, Is.EqualTo(EncounterStatus.Active), "Caught: a fight with the garrison.");
            Assert.That(W.Player.Social.StandingWith(fort.FactionId), Is.EqualTo(standing - 10), "Trespass costs standing, once.");
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.Trespass), Is.EqualTo(1));
            _h.Valid();
        }

        [Test]
        public void Locks_KeysAndForce_AreReal_AndAFailedAttemptChangesOnlyItsCount()
        {
            _h.Hero();
            Known(PlaceCatalog.Fort);
            var fort = _h.Place(PlaceCatalog.Fort);
            _h.Edit(w =>
            {
                var p = w.Places.Single(x => x.PlaceId == fort.PlaceId);
                p.Variables["passage:watch"] = "granted";
                p.GroupOf("watch").State = GroupState.Peaceful;
                p.GroupOf("barracks").State = GroupState.Peaceful;
            });
            Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId)));
            _h.WalkTo("watch");
            Assert.That(W.FindPlace(fort.PlaceId).DoorOf(fort.Layout.Connection("c_quarters")), Is.EqualTo(DoorState.Locked));
            Refused(new PlaceActionCommand("c_quarters", PlaceAction.Unlock), "do not have its key");
            Refused(new PlaceActionCommand("c_quarters", PlaceAction.Pick), "thieves' tools");
            var quarters = fort.Layout.Room("quarters");
            Refused(new MoveInPlaceCommand("quarters", PlaceRules.PoseAt(fort, 1, quarters.Rect.CenterX, quarters.Rect.CenterZ, 0f)), "locked");

            // With the key: unlocked, and the key is kept.
            _h.Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find("fort_key"), 1, "test")));
            var keys = W.Player.Inventory.Count(x => x.ItemId == "fort_key");
            Ok(_s.Dispatch(new PlaceActionCommand("c_quarters", PlaceAction.Unlock)));
            Assert.That(W.FindPlace(fort.PlaceId).DoorOf(fort.Layout.Connection("c_quarters")), Is.EqualTo(DoorState.Closed));
            Assert.That(W.Player.Inventory.Count(x => x.ItemId == "fort_key"), Is.EqualTo(keys), "A key is not used up.");
            Ok(_s.Dispatch(new PlaceActionCommand("c_quarters", PlaceAction.Lock)));
            Assert.That(W.FindPlace(fort.PlaceId).DoorOf(fort.Layout.Connection("c_quarters")), Is.EqualTo(DoorState.Locked));

            // Picking: each failure is counted (and nothing else changes); three a visit.
            _h.Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find("thieves_tools"), 1, "test")));
            var tries = 0;
            while (W.FindPlace(fort.PlaceId).DoorOf(fort.Layout.Connection("c_quarters")) == DoorState.Locked && tries < 3)
            {
                var doorsBefore = W.FindPlace(fort.PlaceId).Doors.Count(d => d.Value == DoorState.Locked);
                var r = _s.Dispatch(new PlaceActionCommand("c_quarters", PlaceAction.Pick));
                Ok(r);
                tries++;
                if (r.Message.Contains("holds")) Assert.That(W.FindPlace(fort.PlaceId).Doors.Count(d => d.Value == DoorState.Locked), Is.EqualTo(doorsBefore));
            }

            if (W.FindPlace(fort.PlaceId).DoorOf(fort.Layout.Connection("c_quarters")) == DoorState.Locked)
                Refused(new PlaceActionCommand("c_quarters", PlaceAction.Pick), "another time");
            _h.Valid();
        }

        // ----- Traps and hazards ----------------------------------------------------------------

        [Test]
        public void ATrap_HurtsButNeverKillsOutright_IsSpentOrResets_AndCanBeDisarmed()
        {
            _h.Hero();
            var crypt = W.Places.First(p => p.Layout.Features.Any(f => f.Kind == FeatureKind.Trap));
            _h.Edit(w => w.FindPlace(crypt.PlaceId).Discovery = PlaceDiscovery.Discovered);
            var trap = crypt.Layout.Features.First(f => f.Kind == FeatureKind.Trap);
            var def = TrapCatalog.Find(trap.Get("trap"));
            Ok(_s.Dispatch(new EnterPlaceCommand(crypt.PlaceId)));
            _h.WalkTo(trap.Room);
            _h.Edit(w => w.Player.Rpg.CurrentHp = 1);
            var sprung = _s.Dispatch(new SpringTrapCommand(trap.Id));
            Ok(sprung);
            Assert.That(sprung.Message, Does.Contain(def.Sprung.Split(' ')[0]));
            Assert.That(W.Player.Rpg.CurrentHp, Is.EqualTo(1), "Outside a fight a trap never takes the last hit point.");
            Assert.That(W.FindPlace(crypt.PlaceId).Has(trap.Id, FeatureFlags.Triggered | FeatureFlags.Found), Is.True, "Sprung, and now plain to see.");
            var again = _s.Dispatch(new SpringTrapCommand(trap.Id));
            Ok(again);
            Assert.That(again.Message, Does.Contain("spent"), "Not twice in one visit.");

            // Disarming: the tool is needed; success makes it safe for good.
            _h.Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find("thieves_tools"), 1, "test")));
            for (var i = 0; i < 3 && !W.FindPlace(crypt.PlaceId).Has(trap.Id, FeatureFlags.Disarmed); i++)
            {
                _h.Edit(w => w.Player.Rpg.CurrentHp = Math.Max(w.Player.Rpg.CurrentHp, 10));
                Ok(_s.Dispatch(new PlaceActionCommand(trap.Id, PlaceAction.Disarm)));
            }

            if (W.FindPlace(crypt.PlaceId).Has(trap.Id, FeatureFlags.Disarmed))
            {
                Ok(_s.Dispatch(new SpringTrapCommand(trap.Id)));
                Assert.That(W.Player.Rpg.CurrentHp, Is.GreaterThanOrEqualTo(1));
                Assert.That(W.Encounters.Count, Is.GreaterThanOrEqualTo(0));
                Assert.That(_s.GetPlaceScene().Features.Single(f => f.Id == trap.Id).Armed, Is.False, "A disarmed trap is never armed again.");
            }
            else Refused(new PlaceActionCommand(trap.Id, PlaceAction.Disarm), "another time");
            _h.Valid();
        }

        [Test]
        public void AHazard_IsMetOnceAVisit_ThroughTheRulesSaves()
        {
            _h.Hero();
            var mine = _h.Place(PlaceCatalog.Mine);
            var next = mine.Layout.Connections.First(c => c.Kind != ConnectionKind.Entrance && !c.IsVertical && c.Joins(mine.Layout.Entrance.A));
            var room = next.Other(mine.Layout.Entrance.A);
            _h.Edit(w =>
            {
                var p = w.FindPlace(mine.PlaceId);
                p.Discovery = PlaceDiscovery.Discovered;
                p.Layout.Room(room).Hazard = "toxic_air";
                if (next.Kind == ConnectionKind.Door) p.Doors[next.Id] = DoorState.Open;
                foreach (var g in p.Layout.Groups.Where(g => g.Room == room)) p.GroupOf(g.Id).State = GroupState.Defeated;
            });
            Ok(_s.Dispatch(new EnterPlaceCommand(mine.PlaceId)));
            var r = W.FindPlace(mine.PlaceId).Layout.Room(room);
            var first = _s.Dispatch(new MoveInPlaceCommand(room, PlaceRules.PoseAt(mine, r.Floor, r.Rect.CenterX, r.Rect.CenterZ, 0f)));
            Ok(first);
            Assert.That(first.Message, Does.Contain("Constitution save"));
            var hp = W.Player.Rpg.CurrentHp;
            var back = W.FindPlace(mine.PlaceId).Layout.Room(mine.Layout.Entrance.A);
            Ok(_s.Dispatch(new MoveInPlaceCommand(back.Id, PlaceRules.PoseAt(mine, back.Floor, back.Rect.CenterX, back.Rect.CenterZ, 0f))));
            var second = _s.Dispatch(new MoveInPlaceCommand(room, PlaceRules.PoseAt(mine, r.Floor, r.Rect.CenterX, r.Rect.CenterZ, 0f)));
            Ok(second);
            Assert.That(second.Message, Does.Not.Contain("save"), "Once a visit.");
            Assert.That(W.Player.Rpg.CurrentHp, Is.EqualTo(hp));
            Assert.That(_s.GetPlaceScene().Status, Does.Contain("foul air"));
        }

        // ----- Interiors --------------------------------------------------------------------------

        [Test]
        public void ABuilding_IsEnteredThroughItsDoor_HoldsItsPeople_AndIsTheSameOnReturn()
        {
            _h.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(_s.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            // A home with someone in it now: the night, when people are at home.
            _h.Edit(w => w.MinuteOfDay = 23 * 60 + 30);
            var w0 = W;
            var home = layout.Buildings.First(b => b.Function != null && Realmkeeper.Domain.Places.InteriorGenerator.Plan(b.Function.Value) == "home" &&
                                                   w0.Locations.Any(l => l.LayoutKey == b.Key && l.SettlementId == town.SettlementId && w0.Npcs.Any(n => n.ResidenceId == l.LocationId && !n.IsDeceased && n.Tier != NpcTier.Unassigned)));
            var entered = _s.Dispatch(new EnterBuildingCommand(home.Key));
            Ok(entered);
            var id = "interior:" + town.SettlementId + ":" + home.Key;
            Assert.That(W.Player.IsInPlace, Is.True);
            Assert.That(W.Player.PlaceId, Is.EqualTo(id));
            var scene = _s.GetPlaceScene();
            Assert.That(scene.Category, Is.EqualTo(PlaceCategory.Interior));
            Assert.That(scene.Figures.Any(f => f.Role == "resident"), Is.True, "The people who live here are here, asleep.");
            var resident = scene.Figures.First(f => f.Role == "resident");
            Assert.That(scene.Rooms.Single(r => r.Id == resident.Room).Kind, Is.EqualTo(RoomKind.Bedroom).Or.EqualTo(RoomKind.Chamber));
            Assert.That(_s.GetPresenceIn(town.SettlementId).Count(p => p.NpcId == resident.NpcId), Is.LessThanOrEqualTo(1), "One identity, never two.");

            var box = scene.Features.FirstOrDefault(f => f.Kind == FeatureKind.Container);
            if (box != null)
            {
                _h.WalkTo(box.Room, fight: false);
                var opt = _s.GetPlaceActions(box.Id).Single(o => o.Action == PlaceAction.Open);
                Assert.That(opt.Enabled, Is.False, "A household's chest is not the player's.");
            }

            _h.WalkTo(W.FindPlace(id).Layout.Entrance.A, fight: false);
            Ok(_s.Dispatch(new ExitPlaceCommand()));
            Assert.That(W.Player.IsInSettlement, Is.True);
            Assert.That(W.Player.Pose, Is.EqualTo(W.FindPlace(id).ExitPose), "Back outside the same door.");
            var places = W.Places.Count;
            Ok(_s.Dispatch(new EnterBuildingCommand(home.Key)));
            Assert.That(W.Places.Count, Is.EqualTo(places), "The same interior, not a new one.");
            Assert.That(W.FindPlace(id).Visits, Is.EqualTo(2));
            _h.Valid();
        }

        // ----- Walls in a fight ----------------------------------------------------------------------

        [Test]
        public void Walls_BlockMovement_AndTheAiGoesAroundThem()
        {
            _h.Hero();
            var setup = new EncounterSetup { Name = "Wall test", Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal };
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "giant_rat", Name = "Rat", Side = "foes", Position = new GridPos(4, 0) });
            for (var y = -3; y <= 3; y++) setup.Blocked.Add(new GridPos(2, y)); // a wall between them, open past y = 3
            Ok(_s.Dispatch(new StartEncounterCommand(setup)));
            var w = W;
            var engine = new CombatEngine(w, w.ActiveEncounter);
            var player = engine.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);
            var rat = engine.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Creature);
            Assert.That(engine.Unavailable(player, new MoveAction(new GridPos(1, 0), new GridPos(2, 0))), Does.Contain("wall"));
            var path = CombatAi.PathAround(engine, rat, player, 5);
            Assert.That(path, Is.Not.Empty, "A route round the wall exists.");
            Assert.That(path.Any(p => engine.IsBlocked(p)), Is.False, "It never steps into the wall.");
            Assert.That(new JsonWorldSerializer().Deserialize(new JsonWorldSerializer().Serialize(w)).World.ActiveEncounter.Blocked.Count, Is.EqualTo(7), "Walls are saved with the fight.");
        }

        [Test]
        public void ACorneredCreature_AmongWalls_FightsInsteadOfDashingInPlaceForEver()
        {
            // Found in the scene (a stalemate of 749 rounds): a frightened rat in a dead end could neither flee nor come on.
            _h.Hero();
            var setup = new EncounterSetup { Name = "Dead end", Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal };
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "giant_rat", Name = "Rat", Side = "foes", Position = new GridPos(3, 0) });
            for (var x = -6; x <= 6; x++) { setup.Blocked.Add(new GridPos(x, 1)); setup.Blocked.Add(new GridPos(x, -1)); }
            setup.Blocked.Add(new GridPos(4, 0)); // the corridor ends behind the rat
            setup.Blocked.Add(new GridPos(-1, 0)); // and behind the player
            Ok(_s.Dispatch(new StartEncounterCommand(setup)));
            var w = W;
            var engine = new CombatEngine(w, w.ActiveEncounter);
            var rat = engine.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Creature);
            rat.CurrentHp = 1; // badly hurt: it wants to run
            var act = CombatAi.Next(engine, rat);
            Assert.That(act, Is.InstanceOf<MoveAction>().Or.InstanceOf<AttackAction>(), "Cornered, it comes on: " + act?.GetType().Name);
            Assert.That(act, Is.Not.InstanceOf<DashAction>());
        }

        [Test]
        public void Attempts_AreLimitedPerVisit_AndANewVisitAllowsMore()
        {
            _h.Hero();
            Known(PlaceCatalog.Fort);
            var fort = _h.Place(PlaceCatalog.Fort);
            Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId)));
            for (var i = 0; i < PlaceRules.AttemptsPerVisit; i++) Ok(_s.Dispatch(new PlaceActionCommand("c_gate", PlaceAction.Listen)));
            Refused(new PlaceActionCommand("c_gate", PlaceAction.Listen), "another time");
            Ok(_s.Dispatch(new ExitPlaceCommand()));
            Ok(_s.Dispatch(new EnterPlaceCommand(fort.PlaceId)));
            Ok(_s.Dispatch(new PlaceActionCommand("c_gate", PlaceAction.Listen)));
        }

        [Test]
        public void APlacesContainer_IsReachedOnlyFromInsideThatPlace()
        {
            _h.Hero();
            var box = W.Containers.First(c => c.PlaceId != null && c.PlayerAccess && c.Items.Count > 0);
            Refused(new TakeFromContainerCommand(box.Key, box.Items[0].StackId, 1), "is in");
        }

        // ----- Saves made before Phase 13 ---------------------------------------------------------

        [Test]
        public void AVersion14Save_GainsItsPlacesOnce_AndKeepsEverythingElse()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            Phase2Worlds.StripToV14(doc);
            Assert.That(doc["world"]["places"], Is.Null);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "v14" + JsonWorldStore.Extension), doc.ToString());
            var load = _s.Execute(new LoadWorldCommand("v14"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
            var w = W;
            Assert.That(w.Places.Count(p => p.ParentKind == PlaceParentKind.Region), Is.EqualTo(5), "The places come with the living world on load.");
            Assert.That(w.Quests.Count(q => q.DefinitionId == QuestCatalog.LostBelow), Is.EqualTo(1));
            Assert.That(w.Escorts.Count, Is.EqualTo(1));
            var again = JObject.Parse(serializer.Serialize(w));
            Assert.That(((JArray)again["world"]["places"]).Count, Is.EqualTo(w.Places.Count));
            _h.Valid();
        }

        // ----- Faults ---------------------------------------------------------------------------------

        [Test]
        public void RefusedActions_LeaveTheWorldUnchanged()
        {
            _h.Hero();
            var mine = _h.Place(PlaceCatalog.Mine);
            Refused(new EnterPlaceCommand(mine.PlaceId), "know of no such place");
            Refused(new EnterPlaceCommand("place:nowhere"), "no such place");
            Refused(new ExitPlaceCommand(), "not inside");
            Known(PlaceCatalog.Mine);
            Ok(_s.Dispatch(new EnterPlaceCommand(mine.PlaceId)));
            Refused(new EnterPlaceCommand(mine.PlaceId), "must leave");
            var far = mine.Layout.Rooms.First(r => r.Floor == 0 && r.Id != mine.Layout.Entrance.A && !mine.Layout.Connections.Any(c => c.Joins(r.Id) && c.Joins(mine.Layout.Entrance.A)));
            Refused(new MoveInPlaceCommand(far.Id, PlaceRules.PoseAt(mine, far.Floor, far.Rect.CenterX, far.Rect.CenterZ, 0f)), "can't get there");
            var ladder = mine.Layout.Connections.First(c => c.IsVertical);
            if (!ladder.Joins(mine.Layout.Entrance.A)) Refused(new TraversePlaceCommand(ladder.Id), "not at");
            Refused(new PlaceActionCommand("nothing", PlaceAction.Open), "cannot be done");
            var captive = mine.Layout.Features.Single(f => f.Kind == FeatureKind.Captive);
            Refused(new PlaceActionCommand(captive.Id, PlaceAction.Free), "cannot be done");
            Refused(new SpringTrapCommand("nothing"), "no trap");
            _h.WalkTo(mine.Layout.Rooms.First(r => r.Id != mine.Layout.Entrance.A && mine.Layout.Connections.Any(c => c.Kind != ConnectionKind.Entrance && c.Joins(r.Id) && c.Joins(mine.Layout.Entrance.A))).Id);
            if (_s.GetEncounter() == null || _s.GetEncounter().Status != EncounterStatus.Active) Refused(new ExitPlaceCommand(), "no way out from here");
        }

        [Test]
        public void TheValidator_CatchesImpossiblePlaceStates()
        {
            _h.Hero();
            string Errors(Action<WorldData> breakIt)
            {
                var w = W;
                breakIt(w);
                return string.Join("; ", WorldValidator.Validate(w).Errors);
            }

            var mine = _h.Place(PlaceCatalog.Mine);
            Assert.That(Errors(w => { w.Player.LocationKind = CharacterLocationKind.Place; w.Player.PlaceId = "place:nowhere"; }), Does.Contain("unknown place"));
            Assert.That(Errors(w => w.FindPlace(mine.PlaceId).ExploredRooms.Add("no-such-room")), Does.Contain("unknown room"));
            Assert.That(Errors(w => w.FindPlace(mine.PlaceId).Doors[mine.Layout.Connections.First(c => c.Kind == ConnectionKind.Opening).Id] = DoorState.Locked), Does.Contain("no door"));
            Assert.That(Errors(w => w.Escorts.Single().State = EscortState.Lost), Does.Contain("lost but alive"));
            Assert.That(Errors(w => w.Escorts.Add(w.Escorts.Single().Clone())), Does.Contain("escorted twice"));
            Assert.That(Errors(w =>
            {
                var fort = w.Places.Single(p => p.DefinitionId == PlaceCatalog.Fort);
                var npc = fort.Layout.Groups[0].Members[0].NpcId;
                fort.Layout.Groups[1].Members.Add(new GroupMember { NpcId = npc, X = 3, Z = 15 });
            }), Does.Contain("posted in two places"));
            Assert.That(Errors(w => w.Containers.First(c => c.PlaceId != null).PlaceId = "place:nowhere"), Does.Contain("unknown place"));
            Assert.That(Errors(w => w.FindPlace(mine.PlaceId).Layout.Rooms[1].Rect = w.FindPlace(mine.PlaceId).Layout.Rooms[0].Rect), Does.Contain("overlap"));
        }
    }
}
