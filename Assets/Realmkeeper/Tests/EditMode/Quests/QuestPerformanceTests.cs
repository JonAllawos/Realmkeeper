using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Quests;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Quests
{
    /// <summary>
    /// Phase 12 costs: a large journal (150 quest instances, 60 of them active), event processing against all of them,
    /// dialogue views with every hook, knowledge lookups, a deed spreading for 30 days, a long Chronicle read through the
    /// player-known filter, and saving and loading it all. Ceilings are generous; the numbers are printed for the report.
    /// </summary>
    public sealed class QuestPerformanceTests
    {
        [Test]
        public void Quests_AreCheap()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var session = Phase2Worlds.Session(dir);
                ExplorationModule.Register(session);
                CombatTests.Create(session, "fighter", CombatTests.Scores);
                var town = QuestGenesis.FirstTown(session.CurrentWorldSnapshotForTests());
                Assert.That(session.Dispatch(new EnterSettlementCommand(town.SettlementId)).IsSuccess, Is.True);

                // 150 instances: the hunt bound to the town's people, 60 accepted.
                var w = session.CurrentWorldSnapshotForTests();
                w.MinuteOfDay = 600;
                var people = w.Npcs.Where(n => n.HomeSettlementId == town.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned).ToList();
                var def = QuestCatalog.Find(QuestCatalog.WolvesAtTheFolds);
                for (var i = 0; i < 150; i++)
                {
                    var q = QuestLifecycle.Create(w, def, new Dictionary<string, string> { ["giver"] = people[i % people.Count].NpcId.ToString(), ["creature"] = "wolf" }, "perf:" + i,
                        new Dictionary<string, string> { ["count"] = "2", ["creature_plural"] = "wolves" }, null, out var problem);
                    Assert.That(q, Is.Not.Null, problem);
                    if (i < 60)
                    {
                        QuestLifecycle.Move(w, q, QuestState.Offered, null, null);
                        QuestLifecycle.Move(w, q, QuestState.Accepted, null, null);
                        QuestLifecycle.Move(w, q, QuestState.Active, null, null);
                    }
                    else QuestLifecycle.Move(w, q, QuestState.Discovered, null, null);
                }

                Assert.That(new JsonWorldStore(dir).Save(w, "perf").IsSuccess, Is.True);
                Assert.That(session.Execute(new LoadWorldCommand("perf")).IsSuccess, Is.True);

                var journal = Stopwatch.StartNew();
                for (var i = 0; i < 20; i++) Assert.That(session.GetJournal().TotalQuests, Is.GreaterThanOrEqualTo(150));
                journal.Stop();
                var detail = Stopwatch.StartNew();
                var ids = session.GetJournal().Active.Select(q => q.InstanceId).ToList();
                foreach (var id in ids) Assert.That(session.GetQuest(id), Is.Not.Null);
                detail.Stop();

                // Events against 60 active quests, through the real dispatch (each command validates the whole world).
                var events = Stopwatch.StartNew();
                const int Visits = 20;
                for (var i = 0; i < Visits; i++) Assert.That(session.Dispatch(new InspectObjectCommand("notice:" + town.SettlementId)).IsSuccess, Is.True);
                events.Stop();

                // Dialogue views with every hook (60 quests' givers in town).
                var talk = Stopwatch.StartNew();
                var talked = 0;
                foreach (var n in people.Take(10))
                {
                    var r = session.Dispatch(new StartConversationCommand(n.NpcId));
                    if (!r.IsSuccess) continue;
                    Assert.That(session.GetConversation(), Is.Not.Null);
                    talked++;
                }

                talk.Stop();

                // Knowledge lookups: every person, every topic.
                var copy = session.CurrentWorldSnapshotForTests();
                var lookups = 0;
                var know = Stopwatch.StartNew();
                foreach (var n in copy.Npcs)
                    foreach (KnowledgeTopic t in Enum.GetValues(typeof(KnowledgeTopic)))
                    {
                        Realmkeeper.Simulation.Npcs.NpcMind.Find(n, t, town.SettlementId.ToString());
                        lookups++;
                    }

                know.Stop();

                // A deed spreading for 30 days, with the whole simulation running.
                session.Dispatch(new EndConversationCommand());
                var x = session.CurrentWorldSnapshotForTests();
                Deeds.Witness(x, x.FindNpc(people[0].NpcId), "recovered_grain", "perf", "perf");
                Assert.That(new JsonWorldStore(dir).Save(x, "perf2").IsSuccess, Is.True);
                Assert.That(session.Execute(new LoadWorldCommand("perf2")).IsSuccess, Is.True);
                var days = Stopwatch.StartNew();
                session.Execute(new AdvanceDaysCommand(30));
                days.Stop();
                var holders = session.CurrentWorldSnapshotForTests().Npcs.Count(n => n.Knowledge.Any(k => k.Topic == KnowledgeTopic.PlayerDeed));

                var final = session.CurrentWorldSnapshotForTests();
                var serializer = new JsonWorldSerializer();
                var json = serializer.Serialize(final);
                var save = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) json = serializer.Serialize(final);
                save.Stop();
                var load = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) Assert.That(serializer.Deserialize(json).IsSuccess, Is.True);
                load.Stop();
                var questBytes = Newtonsoft.Json.Linq.JObject.Parse(json)["world"]["quests"].ToString().Length;

                TestContext.Out.WriteLine($"Journal with {final.Quests.Count} instances: {journal.Elapsed.TotalMilliseconds / 20:0.00} ms each; {ids.Count} quest details in {detail.Elapsed.TotalMilliseconds:0.0} ms");
                TestContext.Out.WriteLine($"Object inspected with 60 active quests (validated command + event processing): {events.Elapsed.TotalMilliseconds / Visits:0.0} ms each");
                TestContext.Out.WriteLine($"Conversation start + view with all hooks: {(talked == 0 ? 0 : talk.Elapsed.TotalMilliseconds / talked):0.0} ms each ({talked} people)");
                TestContext.Out.WriteLine($"Knowledge lookups: {lookups} in {know.Elapsed.TotalMilliseconds:0.00} ms");
                TestContext.Out.WriteLine($"30 days with a deed spreading: {days.Elapsed.TotalMilliseconds:0} ms; {holders} of {final.Npcs.Count} people heard of it");
                TestContext.Out.WriteLine($"Chronicle {final.HistoricalEvents.Count} events; save {json.Length / 1024} KB in {save.Elapsed.TotalMilliseconds / 5:0} ms, load {load.Elapsed.TotalMilliseconds / 5:0} ms; quest section {questBytes / 1024} KB");

                Assert.That(journal.Elapsed.TotalMilliseconds / 20, Is.LessThan(50));
                Assert.That(events.Elapsed.TotalMilliseconds / Visits, Is.LessThan(500));
                Assert.That(holders, Is.LessThan(final.Npcs.Count), "Bounded propagation: not everyone knows.");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
