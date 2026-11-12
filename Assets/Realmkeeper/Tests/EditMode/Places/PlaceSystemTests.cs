using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Phase 13 (ADR-0062 … ADR-0066): places in the world, played through the same commands the scene uses — the mine's
    /// rescue from rumour to home, doors, locks, traps, hazards, the fort's gate and garrison, building interiors with
    /// their people, saving anywhere, and what happens when things fail.
    /// </summary>
    public sealed class PlaceSystemTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        internal WorldData W => _s.CurrentWorldSnapshotForTests();

        internal static readonly int[] Strong = { 15, 14, 13, 8, 10, 12 };

        internal void Edit(Action<WorldData> change)
        {
            var w = W;
            change(w);
            var saved = new JsonWorldStore(_dir).Save(w, "edit");
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            var load = _s.Execute(new LoadWorldCommand("edit"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
        }

        internal static void Ok(CommandResult r) => Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));

        internal PlaceData Place(string def) => W.Places.Single(p => p.DefinitionId == def);

        internal void Hero(int level = 3)
        {
            CombatTests.Create(_s, "fighter", Strong);
            // A fighter of level 3, the level the mine is meant for (ADR-0064: a rescue for a band of one, not a beginner).
            var need = Realmkeeper.Domain.Rpg.ProgressionRules.ExperienceFor(level) - W.Player.Rpg.Experience;
            Ok(_s.Dispatch(new AwardExperienceCommand(need, Realmkeeper.Domain.Rpg.ExperienceSource.Milestone, null, "test")));
            while (W.Player.Rpg.Level < level) Ok(_s.Dispatch(new AdvanceCharacterCommand(Realmkeeper.Domain.Rpg.CharacterAdvancement.SuggestChoices(W.Player.Rpg, W.Player.Rpg.Level + 1))));
            Edit(w => w.MinuteOfDay = 10 * 60);
        }

        /// <summary>Walks from the room the player is in to a room, through the commands: doors opened, locks forced, ladders climbed.</summary>
        internal void WalkTo(string target, bool fight = true)
        {
            for (var guard = 0; guard < 40; guard++)
            {
                var w = W;
                var p = w.FindPlace(w.Player.PlaceId);
                var at = w.Player.PlaceRoom;
                if (at == target) return;
                var path = Path(p, at, target);
                Assert.That(path, Is.Not.Null, $"No way from {at} to {target}.");
                var (via, next) = path[0];
                var c = p.Layout.Connection(via);
                if (c.IsVertical) { Ok(_s.Dispatch(new TraversePlaceCommand(c.Id))); }
                else
                {
                    var state = p.DoorOf(c);
                    if (c.Kind != ConnectionKind.Opening && state != DoorState.Open && state != DoorState.Broken)
                    {
                        if (state == DoorState.Closed || (state == DoorState.Barred && c.A == at)) Ok(_s.Dispatch(new PlaceActionCommand(c.Id, PlaceAction.Open)));
                        else
                        {
                            for (var i = 0; i < 3 && W.FindPlace(p.PlaceId).DoorOf(c) == state; i++) Ok(_s.Dispatch(new PlaceActionCommand(c.Id, PlaceAction.Force)));
                            if (W.FindPlace(p.PlaceId).DoorOf(c) == state) Edit(x => x.FindPlace(p.PlaceId).Doors[c.Id] = DoorState.Broken); // the test's strong arm
                        }
                    }

                    var room = p.Layout.Room(next);
                    Ok(_s.Dispatch(new MoveInPlaceCommand(next, PlaceRules.PoseAt(p, room.Floor, room.Rect.CenterX, room.Rect.CenterZ, 0f))));
                }

                if (_s.GetEncounter() != null && _s.GetEncounter().Status == EncounterStatus.Active)
                {
                    Assert.That(fight, Is.True, "A fight started on the way.");
                    EncounterGenerationTests.FightToTheEnd(_s);
                }
            }

            Assert.Fail("Did not arrive.");
        }

        internal static List<(string Via, string Room)> Path(PlaceData p, string from, string to)
        {
            var prev = new Dictionary<string, (string Via, string Room)> { [from] = (null, null) };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                if (r == to) break;
                foreach (var c in p.Layout.Connections)
                {
                    if (c.Kind == ConnectionKind.Entrance || !c.Joins(r) || (c.Kind == ConnectionKind.Secret && !p.Found(c))) continue;
                    var o = c.Other(r);
                    if (o == null || prev.ContainsKey(o)) continue;
                    prev[o] = (c.Id, r);
                    queue.Enqueue(o);
                }
            }

            if (!prev.ContainsKey(to)) return null;
            var path = new List<(string, string)>();
            for (var at = to; at != from; at = prev[at].Room) path.Insert(0, (prev[at].Via, at));
            return path;
        }

        internal void Valid()
        {
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        // ----- The world's places ------------------------------------------------------------------

        [Test]
        public void TheWorld_HasItsPlaces_OnLand_OffTheRoads_WithTheirPeople_Once()
        {
            var w = W;
            var places = w.Places.Where(p => p.ParentKind == PlaceParentKind.Region).ToList();
            var phase13 = new[] { PlaceCatalog.Mine, PlaceCatalog.Crypt, PlaceCatalog.Fort, PlaceCatalog.Cave, PlaceCatalog.Ruin };
            Assert.That(places.Select(p => p.DefinitionId).Where(phase13.Contains), Is.EquivalentTo(phase13));
            var newer = places.Select(p => p.DefinitionId).Where(Phase14Places.All.Contains).ToList();
            Assert.That(newer.Count, Is.InRange(3, 5), "Phase 14 (ADR-0071): three to five of the new places, chosen by the world.");
            var phase18 = places.Select(p => p.DefinitionId).Where(Phase18Places.All.Contains).ToList();
            Assert.That(phase18.Count, Is.InRange(3, 4), "Phase 18 (ADR-0089): the region's own drift, vault and tower, and a contested watch where two factions exist.");
            Assert.That(phase18.Distinct().Count(), Is.EqualTo(phase18.Count));
            Assert.That(places.Count, Is.EqualTo(5 + newer.Count + phase18.Count), "No place twice, nothing else.");
            foreach (var p in places)
            {
                Assert.That(PlaceValidator.Validate(p.Layout), Is.Empty, p.Name);
                Assert.That(p.Discovery, Is.EqualTo(PlaceDiscovery.Unknown), p.Name + " starts unknown.");
                Assert.That(w.FindSettlement(p.SettlementId), Is.Not.Null, p.Name + " belongs near a settlement.");
                Assert.That(places.Count(o => Math.Max(Math.Abs(o.Cell.X - p.Cell.X), Math.Abs(o.Cell.Z - p.Cell.Z)) < Realmkeeper.Infrastructure.Generation.PlaceGenesis.MinDistance), Is.EqualTo(1), p.Name + " stands apart.");
                foreach (var f in p.Layout.Features.Where(f => f.Kind == FeatureKind.Container))
                    Assert.That(w.FindContainer(PlaceContents.Key(p.PlaceId, f.Id)), Is.Not.Null, $"{p.Name}: {f.Id} holds a real container.");
            }

            var mine = places.Single(p => p.DefinitionId == PlaceCatalog.Mine);
            var escort = w.Escorts.Single();
            Assert.That(escort.State, Is.EqualTo(EscortState.Captive));
            Assert.That(escort.PlaceId, Is.EqualTo(mine.PlaceId));
            var captive = w.FindNpc(escort.NpcId);
            Assert.That(_s.GetPresenceIn(captive.HomeSettlementId).Any(x => x.NpcId == captive.NpcId), Is.False, "The captive is not at home: one identity, one place.");
            var quest = w.Quests.Single(q => q.DefinitionId == QuestCatalog.LostBelow);
            Assert.That(quest.Bindings["captive"], Is.EqualTo(captive.NpcId.ToString()));
            Assert.That(quest.Bindings["place"], Is.EqualTo(mine.PlaceId));
            Assert.That(quest.State, Is.EqualTo(QuestState.Available));

            var fort = places.Single(p => p.DefinitionId == PlaceCatalog.Fort);
            var garrison = fort.Layout.Groups.SelectMany(g => g.Members).ToList();
            Assert.That(garrison.Count, Is.EqualTo(5));
            Assert.That(garrison.All(m => w.FindNpc(m.NpcId) is NpcData n && n.FactionId == fort.FactionId && !n.FactionId.IsEmpty), Is.True, "Persistent people of the fort's faction.");
            Assert.That(garrison.Select(m => m.NpcId).Distinct().Count(), Is.EqualTo(5), "Five people, not copies.");
            foreach (var m in garrison) Assert.That(PlacePresence.PostedAt(w, m.NpcId)?.PlaceId, Is.EqualTo(fort.PlaceId));

            var copy = W;
            var map = _s.Map;
            Assert.That(Realmkeeper.Infrastructure.Generation.PlaceGenesis.Ensure(copy, map, new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory()), Is.False, "Made once.");
            Assert.That(copy.Places.Count, Is.EqualTo(w.Places.Count));
            Valid();
        }

        [Test]
        public void ARumour_GivesANameAndADirection_ThenTheWay_AndOnlyAKnownPlaceCanBeEntered()
        {
            Hero();
            var mine = Place(PlaceCatalog.Mine);
            Ok(_s.Dispatch(new EnterSettlementCommand(mine.SettlementId)));
            Ok(_s.Dispatch(new ExitSettlementCommand()));
            var refused = _s.Dispatch(new EnterPlaceCommand(mine.PlaceId));
            Assert.That(refused.IsSuccess, Is.False, "Unknown: it cannot be found.");
            Assert.That(_s.GetKnownPlaces().Any(p => p.PlaceId == mine.PlaceId), Is.False, "Not on any list.");

            Ok(_s.Dispatch(new EnterSettlementCommand(mine.SettlementId)));
            var teller = W.Npcs.First(n => n.HomeSettlementId == mine.SettlementId && n.Tier != NpcTier.Unassigned && !n.IsDeceased && !PlacePresence.IsAway(W, n) &&
                                           _s.Dispatch(new StartConversationCommand(n.NpcId)).IsSuccess);
            var r = _s.Dispatch(new ChooseResponseCommand("ask"));
            Ok(r);
            Ok(_s.Dispatch(new ChooseResponseCommand("ask_rumours")));
            var line = _s.GetConversation()?.NpcLine ?? string.Empty;
            var places = _s.GetKnownPlaces();
            var heard = places.FirstOrDefault(p => p.Discovery == PlaceDiscovery.Rumored);
            Assert.That(heard, Is.Not.Null, "A rumour names a place: " + line);
            Assert.That(heard.Cell, Is.Null, "A rumour gives no map position.");
            Ok(_s.Dispatch(new ChooseResponseCommand("ask_rumours")));
            var known = _s.GetKnownPlaces().Single(p => p.PlaceId == heard.PlaceId);
            Assert.That(known.Discovery, Is.EqualTo(PlaceDiscovery.Discovered), "Asked again: the way there.");
            Assert.That(known.Cell, Is.Not.Null, "Now it is on the map.");
            Ok(_s.Dispatch(new EndConversationCommand()));
            Ok(_s.Dispatch(new ExitSettlementCommand()));
            Ok(_s.Dispatch(new EnterPlaceCommand(heard.PlaceId)));
            Assert.That(W.Player.IsInPlace, Is.True);
            Assert.That(W.FindPlace(heard.PlaceId).Discovery, Is.GreaterThanOrEqualTo(PlaceDiscovery.Visited));
            Valid();
        }

        // ----- The vertical slice: Lost Below ------------------------------------------------------

        internal QuestInstanceData Lost => W.Quests.Single(q => q.DefinitionId == QuestCatalog.LostBelow);

        internal void AcceptLostBelow()
        {
            var q = Lost;
            var giver = new NpcId(Guid.Parse(q.Bindings["giver"]));
            Ok(_s.Dispatch(new EnterSettlementCommand(Place(PlaceCatalog.Mine).SettlementId)));
            var start = _s.Dispatch(new StartConversationCommand(giver));
            if (!start.IsSuccess) { Edit(w => w.MinuteOfDay = 14 * 60); start = _s.Dispatch(new StartConversationCommand(giver)); }
            Ok(start);
            Ok(_s.Dispatch(new ChooseResponseCommand("hook:" + q.InstanceId + ":giver")));
            Ok(_s.Dispatch(new ChooseResponseCommand("accept")));
            if (_s.GetConversation() != null) Ok(_s.Dispatch(new EndConversationCommand()));
        }

        internal void GiveLight() =>
            Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find("hooded_lantern"), 1, "test")));

        [Test]
        public void LostBelow_FromTheAsking_ToTheReturn_ThroughTheRealCommands()
        {
            Hero();
            GiveLight();
            var mine = Place(PlaceCatalog.Mine);
            AcceptLostBelow();
            var q = Lost;
            Assert.That(q.State, Is.EqualTo(QuestState.Active));
            Assert.That(W.FindPlace(mine.PlaceId).Discovery, Is.EqualTo(PlaceDiscovery.Discovered), "The giver says where it is.");
            Assert.That(q.Progress("find").Done, Is.True);
            Assert.That(_s.GetKnownPlaces().Single(p => p.PlaceId == mine.PlaceId).Cell, Is.Not.Null, "On the map now.");

            // Travel there and go in.
            Ok(_s.Dispatch(new ExitSettlementCommand()));
            var entered = _s.Dispatch(new EnterPlaceCommand(mine.PlaceId));
            Ok(entered);
            Assert.That(W.Player.IsInPlace, Is.True);
            Assert.That(Lost.Progress("enter").Done, Is.True);
            var scene = _s.GetPlaceScene();
            Assert.That(scene.Rooms.Any(r => r.Secret), Is.False, "Nothing secret is shown before it is found.");

            // Why the mine was abandoned, from the foreman's own hand.
            var ledger = mine.Layout.Features.Single(f => f.DefinitionId == "foremans_ledger");
            WalkTo(ledger.Room);
            var read = _s.Dispatch(new PlaceActionCommand(ledger.Id, PlaceAction.Read));
            Ok(read);
            Assert.That(read.Message, Does.Contain("We close tomorrow"));
            Assert.That(W.Player.Social.Knows("mine_why_abandoned"), Is.True, "A discovery the journal keeps.");

            // Down to the captive: the brood is fought where it lives, in the den.
            var captiveFeature = mine.Layout.Features.Single(f => f.Kind == FeatureKind.Captive);
            WalkTo(captiveFeature.Room);
            Assert.That(W.Player.Rpg.IsDead, Is.False, "The hero survived the den.");
            var den = W.FindPlace(mine.PlaceId);
            var fought = W.Encounters.LastOrDefault(e => e.Origin?.PlaceId == mine.PlaceId);
            foreach (var g in mine.Layout.Groups.Where(g => g.Room == captiveFeature.Room)) Assert.That(den.GroupStateOf(g.Id), Is.EqualTo(GroupState.Defeated), fought == null ? "no fight" : fought.Outcome + " " + fought.WinningSide + " | " + string.Join(", ", fought.Combatants.Select(c => c.Name + ":" + c.State + ":" + c.CurrentHp)) + " | " + string.Join(" / ", fought.Log.Skip(Math.Max(0, fought.Log.Count - 8)).Select(l => l.Text)));
            Assert.That(Lost.Progress("lower").Done, Is.True, "The lower level was reached.");

            CommandResult freed = null;
            for (var i = 0; i < 3 && W.Escorts.Single().State == EscortState.Captive; i++) freed = _s.Dispatch(new PlaceActionCommand(captiveFeature.Id, PlaceAction.Free));
            Ok(freed);
            var escort = W.Escorts.Single();
            Assert.That(escort.State, Is.EqualTo(EscortState.Following), freed.Message);
            Assert.That(Lost.Progress("rescue").Done, Is.True);
            Assert.That(_s.GetPlaceScene().Figures.Any(f => f.Role == "follower" && f.NpcId == escort.NpcId), Is.True, "They follow.");

            // Out, and home.
            WalkTo(mine.Layout.Entrance.A);
            Ok(_s.Dispatch(new ExitPlaceCommand()));
            Assert.That(W.Player.LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));
            Assert.That(W.Escorts.Single().State, Is.EqualTo(EscortState.Following), "Still with the player on the road.");
            var home = _s.Dispatch(new EnterSettlementCommand(escort.Destination));
            Ok(home);
            Assert.That(W.Escorts.Single().State, Is.EqualTo(EscortState.Arrived), home.Message);
            Assert.That(Lost.Progress("home").Done, Is.True);
            var captive = W.FindNpc(escort.NpcId);
            Assert.That(PlacePresence.IsAway(W, captive), Is.False, "Back to their own life.");

            // The report: once, with its rewards once.
            var giver = new NpcId(Guid.Parse(q.Bindings["giver"]));
            var start = _s.Dispatch(new StartConversationCommand(giver));
            if (!start.IsSuccess) { Edit(w => w.MinuteOfDay = 14 * 60); start = _s.Dispatch(new StartConversationCommand(giver)); }
            Ok(start);
            Ok(_s.Dispatch(new ChooseResponseCommand("hook:" + q.InstanceId + ":giver")));
            var purse = W.Player.Purse;
            Ok(_s.Dispatch(new ChooseResponseCommand("report")));
            Assert.That(Lost.State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 30));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.CaptiveFreed), Is.EqualTo(1));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.EscortArrived), Is.EqualTo(1));
            if (_s.GetConversation() != null) Ok(_s.Dispatch(new EndConversationCommand()));

            // Back to the mine: what was done stays done.
            var before = W.FindPlace(mine.PlaceId);
            Ok(_s.Dispatch(new ExitSettlementCommand()));
            Ok(_s.Dispatch(new EnterPlaceCommand(mine.PlaceId)));
            Assert.That(_s.GetEncounter() == null || _s.GetEncounter().Status != EncounterStatus.Active, Is.True, "Nobody waits at the door.");
            WalkTo(captiveFeature.Room, fight: false);
            var after = W.FindPlace(mine.PlaceId);
            foreach (var g in mine.Layout.Groups.Where(g => before.GroupStateOf(g.Id) == GroupState.Defeated)) Assert.That(after.GroupStateOf(g.Id), Is.EqualTo(GroupState.Defeated), g.Id + " stays defeated.");
            Assert.That(after.Has(captiveFeature.Id, FeatureFlags.Freed), Is.True);
            Assert.That(_s.GetPlaceScene().Figures.Any(f => f.Role == "captive"), Is.False, "Nobody is held here any more.");
            Assert.That(after.Visits, Is.EqualTo(2));

            // Saved and loaded here, everything is as it was.
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);
            var loaded = serializer.Deserialize(json);
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(serializer.Serialize(loaded.World))["world"].ToString(), Is.EqualTo(Newtonsoft.Json.Linq.JObject.Parse(json)["world"].ToString()), "The place, its state and the player's position survive a round trip exactly.");
            Valid();
        }

    }
}
