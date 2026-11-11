using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>
    /// Phase 8 costs: a practice turn through the session (each command validates the whole world), the UI snapshot,
    /// a large AI-only encounter (40 combatants) played to its end, structure generation, and saving and loading a
    /// world with an active encounter. Ceilings are generous; the numbers are printed for the report.
    /// </summary>
    public sealed class CombatPerformanceTests
    {
        [Test]
        public void Combat_IsCheap()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var session = Phase2Worlds.Session(dir);
                ExplorationModule.Register(session);
                CombatTests.Create(session, "fighter");
                CombatTests.EnterTown(session);
                Assert.That(session.Dispatch(new StartPracticeEncounterCommand()).IsSuccess, Is.True);

                const int Snapshots = 200;
                var snapshot = Stopwatch.StartNew();
                for (var i = 0; i < Snapshots; i++) Assert.That(session.GetEncounter(), Is.Not.Null);
                snapshot.Stop();

                var turns = 0;
                var command = Stopwatch.StartNew();
                var snap = session.GetEncounter();
                for (var i = 0; i < 40 && snap.Status == EncounterStatus.Active; i++)
                {
                    var r = snap.IsPlayersTurn
                        ? session.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, new EndTurnAction(), snap.Sequence))
                        : session.Dispatch(new RunAiTurnCommand(snap.Id, snap.Sequence));
                    Assert.That(r.IsSuccess, Is.True);
                    turns++;
                    snap = session.GetEncounter(true);
                }

                command.Stop();

                var world = session.CurrentWorldSnapshotForTests();
                var serializer = new JsonWorldSerializer();
                var json = serializer.Serialize(world);
                var save = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) json = serializer.Serialize(world);
                save.Stop();
                var load = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) Assert.That(serializer.Deserialize(json).IsSuccess, Is.True);
                load.Stop();

                // A large encounter: 20 against 20, every turn chosen by the AI and resolved by the engine.
                var big = Phase2Worlds.Generated(8080);
                var setup = new EncounterSetup { Name = "Large", Kind = EncounterKind.Other };
                for (var i = 0; i < 10; i++)
                {
                    setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "guard", Side = "watch", Position = new GridPos(i, 0) });
                    setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "commoner", Side = "watch", Position = new GridPos(i, -1) });
                    setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "bandits", Position = new GridPos(i, 12) });
                    setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "wolf", Side = "bandits", Position = new GridPos(i, 13) });
                }

                var n = 0;
                var enc = CombatEngine.Create(big, setup, p => new Guid(n++, 0, 0, new byte[8]), out var errors);
                Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
                big.Encounters.Add(enc);
                var engine = new CombatEngine(big, enc);
                var large = Stopwatch.StartNew();
                engine.Begin();
                var actions = 0;
                for (; actions < 20000 && enc.IsActive; actions++)
                {
                    var actor = enc.Active;
                    Assert.That(engine.Act(actor.CombatantId, CombatAi.Next(engine, actor)).Ok, Is.True);
                }

                large.Stop();
                Assert.That(enc.IsActive, Is.False, "The large fight ends.");
                Assert.That(enc.Log.Count, Is.LessThanOrEqualTo(EncounterData.MaxLogEntries), "The log is bounded.");

                var structures = Stopwatch.StartNew();
                for (var i = 0; i < 20; i++)
                {
                    var copy = Phase2Worlds.Generated(8080);
                    copy.Structures.Clear();
                    StructureGenesis.Ensure(copy);
                }

                structures.Stop();

                TestContext.Out.WriteLine(
                    $"PERF Phase8: snapshot {snapshot.Elapsed.TotalMilliseconds / Snapshots:F2} ms; " +
                    $"turn command (validated commit) {command.Elapsed.TotalMilliseconds / Math.Max(1, turns):F1} ms over {turns}; " +
                    $"large encounter 40 combatants {large.Elapsed.TotalMilliseconds:F0} ms for {actions} actions, {enc.Round} rounds ({large.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, actions):F0} µs/action); " +
                    $"save {save.Elapsed.TotalMilliseconds / 5:F1} ms; load {load.Elapsed.TotalMilliseconds / 5:F1} ms; " +
                    $"structure genesis (world generation included) {structures.Elapsed.TotalMilliseconds / 20:F1} ms");

                Assert.That(snapshot.Elapsed.TotalMilliseconds / Snapshots, Is.LessThan(50));
                Assert.That(command.Elapsed.TotalMilliseconds / Math.Max(1, turns), Is.LessThan(500));
                Assert.That(large.Elapsed.TotalSeconds, Is.LessThan(20));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
