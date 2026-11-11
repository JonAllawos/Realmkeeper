using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Npcs
{
    /// <summary>NPC commands through the session (atomic, sourced, chronicled), dialogue, and persistence.</summary>
    public sealed class NpcCommandTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private string Json() => World(new JsonWorldSerializer().Serialize(_s.CurrentWorldSnapshotForTests()));

        /// <summary>The world section of a save (the envelope carries the save time).</summary>
        private static string World(string json) => JObject.Parse(json)["world"].ToString();

        private NpcListItem Someone(Func<NpcListItem, bool> where = null) => _s.GetNpcs().First(n => where == null || where(n));

        private HistoricalEventType LastEvent() => _s.GetRecentEvents(1)[0].EventType;

        private void AssertRejectedAndUnchanged<TCommand>(TCommand command, CommandStatus status) where TCommand : ICommand
        {
            var before = Json();
            var result = _s.Dispatch(command);
            Assert.That(result.Status, Is.EqualTo(status), result.ToString());
            Assert.That(Json(), Is.EqualTo(before), "A rejected command changes nothing.");
        }

        [Test]
        public void Queries_ListFilterAndInspectWithAllTenSections()
        {
            var all = _s.GetNpcs();
            Assert.That(all.Count, Is.EqualTo(15 + 4 + 5), "15 by role, (Phase 13) the watch fort's garrison of five, and (Phase 10) the people the places call for: a captain, a guard and a priest in the town, a warden in the hamlet.");
            var town = _s.GetSettlements().First(x => x.SettlementType == SettlementType.Town);
            Assert.That(_s.GetNpcs(new NpcFilter { Settlement = town.SettlementId }).Count, Is.EqualTo(6 + 3));
            Assert.That(_s.GetNpcs(new NpcFilter { Role = NpcRole.Elder }).All(n => n.Role == NpcRole.Elder), Is.True);
            Assert.That(_s.GetNpcs(new NpcFilter { Tier = NpcTier.Notable }).All(n => n.Tier == NpcTier.Notable), Is.True);
            Assert.That(_s.GetNpcs(new NpcFilter { NameContains = all[0].Name.Substring(0, 3) }), Has.Some.Matches<NpcListItem>(n => n.NpcId == all[0].NpcId));

            var d = _s.GetNpcDetail(all[0].NpcId);
            foreach (var section in new[] { d.Identity, d.Personality, d.Skills, d.Condition, d.Goals, d.Whereabouts, d.Relationships, d.Memories, d.Knowledge, d.History })
                Assert.That(section, Is.Not.Null);
            Assert.That(d.Personality.Count, Is.EqualTo(8));
            Assert.That(d.Skills.Count, Is.EqualTo(8));
            Assert.That(d.Whereabouts.Count, Is.EqualTo(3 + 7), "Today, home, work and seven weekdays.");
            Assert.That(d.Knowledge, Is.Not.Empty);
            Assert.That(NpcDetail.Explanations.Count, Is.EqualTo(10));
        }

        [Test]
        public void DmCommands_AreAtomicSourcedAndChronicled()
        {
            var n = Someone(x => x.Tier == NpcTier.Resident);
            var other = Someone(x => x.NpcId != n.NpcId && x.SettlementId == n.SettlementId);

            Assert.That(_s.Dispatch(new EditNpcIdentityCommand(n.NpcId, "Edda Fairweather", NpcSex.Female)).IsSuccess, Is.True);
            Assert.That(_s.FindNpc(n.NpcId).Name, Is.EqualTo("Edda Fairweather"));
            Assert.That(LastEvent(), Is.EqualTo(HistoricalEventType.NpcIdentityChanged));
            Assert.That(_s.GetRecentEvents(1)[0].IsManual, Is.True);
            Assert.That(_s.GetRecentEvents(1)[0].Summary, Does.StartWith("By the DM:"));

            Assert.That(_s.Dispatch(new SetNpcGoalCommand(n.NpcId, NpcGoalType.GainStanding, 80)).IsSuccess, Is.True);
            var goal = _s.GetNpcDetail(n.NpcId).Goals.First();
            Assert.That(goal.Label, Is.EqualTo(NpcText.Goal("GainStanding")));
            Assert.That(goal.Why, Does.Contain("Set by the DM"));

            Assert.That(_s.Dispatch(new SetNpcRelationshipCommand(n.NpcId, other.NpcId, affinity: -60, rivalry: 70)).IsSuccess, Is.True);
            Assert.That(_s.GetNpcDetail(n.NpcId).Relationships.Any(r => r.Label == other.Name && r.Value.StartsWith("Rival")), Is.True);

            var shortage = _s.GetRecentEvents(50).First().EventId;
            Assert.That(_s.Dispatch(new AddNpcMemoryCommand(n.NpcId, MemoryCategory.Personal, "The night the barn burned", 70, people: new[] { other.NpcId })).IsSuccess, Is.True);
            Assert.That(_s.GetNpcDetail(n.NpcId).Memories.Any(m => m.Value.Contains("barn burned")), Is.True);

            var far = _s.GetSettlements().First(x => x.SettlementId != n.SettlementId);
            Assert.That(_s.Dispatch(new AddNpcKnowledgeCommand(n.NpcId, KnowledgeTopic.SettlementFood, far.SettlementId.ToString(), "starving", KnowledgeStatus.Rumor, 40)).IsSuccess, Is.True);
            Assert.That(_s.GetNpcDetail(n.NpcId).Knowledge.Any(k => k.Value.Contains("starving") && k.Why.StartsWith("rumour")), Is.True);

            var sameTown = _s.GetLocations(n.SettlementId);
            var tavern = sameTown.First(l => l.Kind == LocationKind.Tavern);
            Assert.That(_s.Dispatch(new MoveNpcCommand(n.NpcId, tavern.LocationId)).IsSuccess, Is.True);
            Assert.That(_s.GetNpcDetail(n.NpcId).Whereabouts[0].Value, Does.StartWith("Tavern"));

            var week = Enumerable.Repeat(tavern.LocationId, 7).ToList();
            Assert.That(_s.Dispatch(new SetNpcScheduleCommand(n.NpcId, week)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new SetNpcOccupationCommand(n.NpcId, NpcRole.Innkeeper)).IsSuccess, Is.True);
            Assert.That(_s.FindNpc(n.NpcId).Role, Is.EqualTo(NpcRole.Innkeeper));

            var faction = _s.GetFactions().First(f => f.FactionId != n.FactionId && f.Status != FactionStatus.Dissolved);
            Assert.That(_s.Dispatch(new ChangeNpcAffiliationCommand(n.NpcId, faction.FactionId)).IsSuccess, Is.True);
            Assert.That(_s.GetNpcs().First(x => x.NpcId == n.NpcId).FactionId, Is.EqualTo(faction.FactionId));

            var dmEvents = _s.GetRecentEvents(30).Where(e => e.Data.TryGetValue("source", out var src) && src == "dm").ToList();
            Assert.That(dmEvents.Count, Is.GreaterThanOrEqualTo(9), "Every DM edit is in the chronicle.");
            Assert.That(dmEvents.All(e => e.Data["npc"] == n.NpcId.ToString() || e.EventType == HistoricalEventType.NpcRelationshipFormed), Is.True);
        }

        [Test]
        public void InvalidCommands_AreRejected_AndLeaveTheWorldUntouched()
        {
            var n = Someone();
            var leader = _s.GetFactions().First(f => f.HasLeader).LeaderNpcId;
            AssertRejectedAndUnchanged(new EditNpcIdentityCommand(n.NpcId, "  "), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new EditNpcIdentityCommand(n.NpcId, birthDate: new WorldDate(9999, 1, 1)), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetNpcGoalCommand(n.NpcId, NpcGoalType.EarnLiving, 101), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetNpcRelationshipCommand(n.NpcId, n.NpcId, affinity: 10), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetNpcRelationshipCommand(n.NpcId, Someone(x => x.NpcId != n.NpcId).NpcId, affinity: 500), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetNpcScheduleCommand(n.NpcId, new[] { LocationId.NewRandom() }), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new MoveNpcCommand(n.NpcId, LocationId.NewRandom()), CommandStatus.NotFound);
            AssertRejectedAndUnchanged(new AddNpcMemoryCommand(n.NpcId, MemoryCategory.Personal, "", 50), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new ChangeNpcAffiliationCommand(leader, default), CommandStatus.InvalidSessionState);
            AssertRejectedAndUnchanged(new EditNpcIdentityCommand(NpcId.NewRandom(), "Nobody"), CommandStatus.NotFound);
            AssertRejectedAndUnchanged(new EditNpcIdentityCommand(n.NpcId, "X", source: ChangeSource.Simulation), CommandStatus.ValidationFailed);
        }

        [Test]
        public void MovingSomeoneToAnotherSettlement_MovesOnePersonOfThePopulation_NoOneCountedTwice()
        {
            var n = Someone(x => x.Tier == NpcTier.Resident);
            var from = _s.GetSettlements().First(x => x.SettlementId == n.SettlementId);
            var to = _s.GetSettlements().First(x => x.SettlementId != n.SettlementId);
            var total = _s.GetSettlements().Sum(x => x.Population);
            var market = _s.GetLocations(to.SettlementId).First(l => l.Kind == LocationKind.Market);
            var result = _s.Dispatch(new MoveNpcCommand(n.NpcId, market.LocationId));
            Assert.That(result.IsSuccess, Is.True, result.ToString());
            Assert.That(_s.GetSettlements().Sum(x => x.Population), Is.EqualTo(total), "People are conserved.");
            Assert.That(_s.GetSettlements().First(x => x.SettlementId == from.SettlementId).Population, Is.EqualTo(from.Population - 1));
            Assert.That(_s.GetSettlements().First(x => x.SettlementId == to.SettlementId).Population, Is.EqualTo(to.Population + 1));
            Assert.That(_s.GetResidents(to.SettlementId).Any(r => r.NpcId == n.NpcId), Is.True);
            Assert.That(_s.GetPeoplePresent(to.SettlementId).Single(p => p.NpcId == n.NpcId).LocationKey, Is.EqualTo(SettlementSites.Plaza));
            var detail = _s.GetNpcDetail(n.NpcId);
            Assert.That(detail.Whereabouts[1].Value, Does.EndWith(to.Name), "A new home there.");
        }

        // ----- The player: questions answered from knowledge, never from the truth -----------------------

        [Test]
        public void Answers_ComeFromWhatThePersonKnows_NotFromTheWorld()
        {
            var town = _s.GetSettlements().First(x => x.SettlementType == SettlementType.Town);
            ExplorationModule.Register(_s);
            Assert.That(_s.Dispatch(new EnterSettlementCommand(town.SettlementId)).IsSuccess, Is.True);
            var here = _s.GetPeoplePresent(town.SettlementId).First();

            // The world says the granary is fine; they are told (and believe) otherwise.
            Assert.That(_s.Dispatch(new AddNpcKnowledgeCommand(here.NpcId, KnowledgeTopic.SettlementFood, town.SettlementId.ToString(), "short", KnowledgeStatus.Known, 100)).IsSuccess, Is.True);
            var answer = _s.Dispatch(new AskNpcCommand(here.NpcId, ConversationTopic.Settlement));
            Assert.That(answer.IsSuccess, Is.True);
            Assert.That(answer.Message, Does.Contain("running low"), "They answer from what they hold, not from the granary.");

            var stranger = _s.GetNpcs().First(x => x.SettlementId != town.SettlementId);
            var aboutStranger = _s.Dispatch(new AskNpcCommand(here.NpcId, ConversationTopic.Person, stranger.NpcId));
            Assert.That(aboutStranger.Message, Does.StartWith("Never met").Or.Contains("couldn't say"), "No omniscience about people they never saw.");

            var detail = _s.GetNpcDetail(here.NpcId);
            Assert.That(detail.Memories.Any(m => m.Value.Contains("Talked with")), Is.True, "They remember the conversation.");
            Assert.That(_s.FindNpc(here.NpcId).HasMetPlayer, Is.True);
        }

        [Test]
        public void YouCanOnlyTalkToPeopleWhoAreHereToday()
        {
            var town = _s.GetSettlements().First(x => x.SettlementType == SettlementType.Town);
            ExplorationModule.Register(_s);
            Assert.That(_s.Dispatch(new EnterSettlementCommand(town.SettlementId)).IsSuccess, Is.True);
            var n = _s.GetPeoplePresent(town.SettlementId).First(p => p.Tier == NpcTier.Resident);
            var elsewhere = _s.GetLocations(_s.GetSettlements().First(x => x.SettlementId != town.SettlementId).SettlementId).First();
            Assert.That(_s.Dispatch(new MoveNpcCommand(n.NpcId, elsewhere.LocationId)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new AskNpcCommand(n.NpcId, ConversationTopic.Greeting)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(_s.Dispatch(new TalkToResidentCommand(n.NpcId)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(ExplorationModule.LayoutFor(_s, town).Residents.Any(r => r.NpcId == n.NpcId), Is.False, "Exploration no longer shows them here.");
        }

        // ----- Persistence ---------------------------------------------------------------------------------

        [Test]
        public void SaveAndLoad_KeepsEveryPersonExactly()
        {
            _s.Execute(new AdvanceDaysCommand(20));
            var n = Someone();
            _s.Dispatch(new AddNpcMemoryCommand(n.NpcId, MemoryCategory.Personal, "A thing to keep", 90));
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(_s.CurrentWorldSnapshotForTests());
            var back = serializer.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(World(serializer.Serialize(back.World)), Is.EqualTo(World(json)), "Round trip is exact (v6).");
            Assert.That(back.World.FindNpc(n.NpcId).Memories.Any(m => m.Subject == "A thing to keep"), Is.True);
        }

        [Test]
        public void Version5Save_GainsTheEngineOnce_WithoutInventedHistory()
        {
            var fresh = Phase2Worlds.GeneratedBeforeLivingWorld(555); // Phase 10: the v11 world (its living-world additions arrive when a session loads a save; see the Phase 10 tests).
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(Phase2Worlds.GeneratedBeforeCommerce(555)));
            Phase2Worlds.StripToV5(doc);
            var eventsBefore = ((JArray)doc["world"]["historicalEvents"]).Count;

            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var w = loaded.World;
            Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(w.WorldSettings.EnabledSimulationSystems, Has.Member(NpcSystem.Id));
            Assert.That(w.HistoricalEvents.Count, Is.EqualTo(eventsBefore), "No events are invented.");
            Assert.That(w.Npcs.All(n => n.Memories.Count == 0), Is.True, "No memories are invented.");
            Assert.That(w.NpcRelations.All(r => r.Origin != NpcRelationCause.Conversation && r.Origin != NpcRelationCause.Quarrel), Is.True,
                "Only ties present facts imply.");
            Phase2Worlds.ExpectLegacyThenTreatAsFresh(w);
            Assert.That(World(serializer.Serialize(w)), Is.EqualTo(World(serializer.Serialize(fresh))), "Same people as a fresh world of that seed on that day.");

            // Once only: a v6 save is never re-initialised.
            var npc = w.Npcs[0];
            npc.Personality[NpcTrait.Courage] = 3;
            var again = serializer.Deserialize(serializer.Serialize(w));
            Assert.That(again.World.Npcs[0].Personality[NpcTrait.Courage], Is.EqualTo(3));
            Assert.That(NpcGenesis.EnsurePeople(again.World), Is.Zero, "Running the initializer again changes no one.");
        }

        [Test]
        public void ACorruptSchedule_IsRejectedOnLoad()
        {
            var doc = JObject.Parse(new JsonWorldSerializer().Serialize(Phase2Worlds.Generated(1)));
            ((JArray)doc["world"]["npcs"][0]["schedule"]).RemoveAt(0);
            var result = new JsonWorldSerializer().Deserialize(doc.ToString());
            Assert.That(result.IsSuccess, Is.False);
        }
    }
}
