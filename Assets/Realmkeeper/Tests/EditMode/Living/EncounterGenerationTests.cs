using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Magic;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 10 (ADR-0046): encounters arise from the world — deterministic for a world state, made of individuals drawn
    /// from their populations, checked before they start, and leaving consequences applied exactly once.
    /// </summary>
    public sealed class EncounterGenerationTests
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

        private void Ready(WorldSession s, int level = 5)
        {
            SpellTests.Caster(s, "fighter", level);
            CombatTests.EnterTown(s);
        }

        internal static string Signature(EncounterPlan p) =>
            p.Description + "|" + string.Join(";", p.Setup.Participants.Select(x => $"{x.Kind}:{x.CreatureId}:{x.NpcId}:{x.Side}:{x.Position}")) + "|" + string.Join(",", p.Draws.Select(d => d.Key + "=" + d.Value));

        [Test]
        public void ThePlan_IsDeterministic_ForAWorldState_AndDependsOnWhereYouGo()
        {
            Ready(_s);
            var a = _s.PreviewWorldEncounter(EncounterPurpose.Outskirts, out var problem);
            Assert.That(a, Is.Not.Null, problem);
            Assert.That(a.IsValid, Is.True, string.Join("; ", a.Problems));
            var b = _s.PreviewWorldEncounter(EncounterPurpose.Outskirts, out _);
            Assert.That(Signature(b), Is.EqualTo(Signature(a)), "Same world state, same plan.");

            var other = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
            ExplorationModule.Register(other);
            Ready(other);
            Assert.That(Signature(other.PreviewWorldEncounter(EncounterPurpose.Outskirts, out _)), Is.EqualTo(Signature(a)), "Same seed and state in another session.");

            var road = _s.PreviewWorldEncounter(EncounterPurpose.Road, out _);
            Assert.That(road.IsValid, Is.True, string.Join("; ", road.Problems));
            Assert.That(road.Origin.Type == EncounterType.BanditAmbush || road.Origin.Type == EncounterType.Patrol, Is.True,
                "The road out of a town is where bandits work: " + road.Description);
            Assert.That(road.Origin.Context, Does.Contain("road"));
        }

        [Test]
        public void ImpossiblePlans_AreRefused()
        {
            Ready(_s);
            var p = _s.PreviewWorldEncounter(EncounterPurpose.Road, out _);
            var foe = p.Setup.Participants.First(x => x.Kind == CombatantKind.Creature);
            foe.Position = p.Setup.Participants[0].Position;
            p.Problems.Clear();
            EncounterGenerator.Check(_s.CurrentWorldSnapshotForTests(), p);
            Assert.That(p.Problems, Has.Some.Contains("Two participants stand"));

            foe.Position = new GridPos(40, 0);
            p.Problems.Clear();
            EncounterGenerator.Check(_s.CurrentWorldSnapshotForTests(), p);
            Assert.That(p.Problems, Has.Some.Contains("outside the field"));

            foe.Position = new GridPos(5, 5);
            var key = p.Draws.Keys.First();
            var world = _s.CurrentWorldSnapshotForTests();
            var pop = world.CreaturePopulations.First(x => x.PopulationId == p.Draws[key]);
            for (var i = 0; i < pop.Count + 2; i++) p.Draws[100 + i] = pop.PopulationId;
            p.Problems.Clear();
            EncounterGenerator.Check(world, p);
            Assert.That(p.Problems, Has.Some.Contains("drawn from"), "More drawn than live there.");
        }

        [Test]
        public void AnOuting_DrawsPersistentIndividuals_AndTheAftermath_IsAppliedOnce()
        {
            Ready(_s, 6);
            var plan = _s.PreviewWorldEncounter(EncounterPurpose.Road, out _);
            var popId = plan.Draws.Values.First();
            var before = _s.CurrentWorldSnapshotForTests().CreaturePopulations.First(x => x.PopulationId == popId).Count;
            var started = _s.Dispatch(new StartWorldEncounterCommand(EncounterPurpose.Road));
            Assert.That(started.IsSuccess, Is.True, started.Message + string.Join("; ", started.Errors));
            var w = _s.CurrentWorldSnapshotForTests();
            var enc = w.ActiveEncounter;
            Assert.That(enc.Origin, Is.Not.Null);
            var drawn = enc.Combatants.Where(c => c.CreatureInstanceId != null).ToList();
            Assert.That(drawn.Count, Is.EqualTo(plan.Draws.Count));
            foreach (var c in drawn)
                Assert.That(w.Creatures.Single(x => x.InstanceId == c.CreatureInstanceId).State, Is.EqualTo(CreatureInstanceState.Alive), "Each foe is a lasting individual.");

            // Save mid-fight, load, and fight it out from the loaded state.
            var serializer = new JsonWorldSerializer();
            var reloaded = serializer.Deserialize(serializer.Serialize(w));
            Assert.That(reloaded.IsSuccess, Is.True, string.Join("; ", reloaded.Errors));
            Assert.That(reloaded.World.ActiveEncounter.Origin.SourceIds, Is.EqualTo(enc.Origin.SourceIds));

            FightToTheEnd(_s);
            w = _s.CurrentWorldSnapshotForTests();
            var ended = w.Encounters.Last(x => x.Origin != null);
            Assert.That(ended.Origin.Resolved, Is.True);
            Assert.That(ended.Origin.Result, Is.Not.Empty);
            var dead = ended.Combatants.Count(c => c.CreatureInstanceId != null && c.State == VitalState.Dead);
            var popAfter = w.CreaturePopulations.First(x => x.PopulationId == popId).Count;
            var deadFromPop = ended.Combatants.Count(c => c.CreatureInstanceId != null && c.State == VitalState.Dead && w.Creatures.Single(x => x.InstanceId == c.CreatureInstanceId).OriginId == popId);
            Assert.That(popAfter, Is.EqualTo(before - deadFromPop), "The slain leave their population; the rest stay.");
            foreach (var c in ended.Combatants.Where(c => c.CreatureInstanceId != null && c.State == VitalState.Dead))
                Assert.That(w.Creatures.Single(x => x.InstanceId == c.CreatureInstanceId).State, Is.EqualTo(CreatureInstanceState.Dead), "A slain foe stays dead.");
            var key = "encounter:" + ended.EncounterId;
            var awards = w.Player.Rpg.ExperienceLog.Count(a => a.Key == key);
            Assert.That(awards, Is.LessThanOrEqualTo(1));
            if (ended.WinningSide == "player" && dead > 0) Assert.That(awards, Is.EqualTo(1), "Experience for the slain, once.");
            Assert.That(w.HistoricalEvents.Count(x => x.EventType == HistoricalEventType.EncounterResolved), Is.EqualTo(1));

            // Replaying the resolution changes nothing.
            var purse = w.Player.Purse;
            var xp = w.Player.Rpg.Experience;
            var items = w.Player.Inventory.Sum(x => x.Quantity);
            var json = serializer.Serialize(w);
            var again = serializer.Deserialize(json).World;
            var ctxWorld = again;
            Assert.That(ctxWorld.Encounters.Last(x => x.Origin != null).Origin.Resolved, Is.True, "Saved as resolved: a reload never re-applies it.");
            Assert.That(_s.Dispatch(new EndEncounterCommand(ended.EncounterId, false)).IsSuccess, Is.False, "An ended fight cannot be ended again.");
            w = _s.CurrentWorldSnapshotForTests();
            Assert.That(w.Player.Purse, Is.EqualTo(purse));
            Assert.That(w.Player.Rpg.Experience, Is.EqualTo(xp));
            Assert.That(w.Player.Inventory.Sum(x => x.Quantity), Is.EqualTo(items));

            // Resolving the same ended encounter again (as a second settlement would) changes nothing.
            var copy = _s.CurrentWorldSnapshotForTests();
            var endedCopy = copy.Encounters.Last(x => x.Origin != null);
            var deaths = copy.CreaturePopulations.Sum(p => p.Deaths);
            var counts = copy.CreaturePopulations.Sum(p => p.Count);
            var memories = copy.Npcs.Sum(n => n.Memories.Count);
            WorldEncounters.Resolve(new CommandContext(_s, copy, new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory()), copy, endedCopy);
            Assert.That(copy.CreaturePopulations.Sum(p => p.Deaths), Is.EqualTo(deaths), "No death is counted twice.");
            Assert.That(copy.CreaturePopulations.Sum(p => p.Count), Is.EqualTo(counts));
            Assert.That(copy.Npcs.Sum(n => n.Memories.Count), Is.EqualTo(memories));
            Assert.That(copy.Player.Rpg.Experience, Is.EqualTo(xp));
            Assert.That(copy.Player.Purse, Is.EqualTo(purse));
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(w).Errors));
        }

        /// <summary>Plays a fight through the commands: the player attacks the nearest foe it can (or ends the turn), answers every question yes, and lets the computer act.</summary>
        internal static void FightToTheEnd(WorldSession s)
        {
            for (var i = 0; i < 600; i++)
            {
                var snap = s.GetEncounter();
                if (snap == null || snap.Status != EncounterStatus.Active) return;
                if (snap.Prompt != null)
                {
                    Assert.That(s.Dispatch(new RespondToReactionCommand(snap.Id, snap.Prompt.Id, true)).IsSuccess, Is.True);
                    continue;
                }

                if (snap.IsPlayersTurn)
                {
                    // The player plays like a sensible fighter: the domain AI proposes from a copy of the state.
                    var copy = s.CurrentWorldSnapshotForTests();
                    var engine = new CombatEngine(copy, copy.ActiveEncounter);
                    var act = CombatAi.Next(engine, engine.Encounter.Find(snap.Player.Id));
                    var r = s.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, act));
                    if (!r.IsSuccess) s.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, new EndTurnAction()));
                    continue;
                }

                s.Dispatch(new RunAiTurnCommand(snap.Id));
            }

            var last = s.GetEncounter();
            Assert.Fail("The fight did not end. Last log: " + string.Join(" | ", last.Log.Skip(System.Math.Max(0, last.Log.Count - 14)).Select(l => l.Text)));
        }
    }
}
