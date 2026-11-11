using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Character;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>Phase 8 through the session: commands, the practice encounter, time, the chronicle, residents' deaths, saving and loading, migration and validation.</summary>
    public sealed class CombatIntegrationTests
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

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private EncounterSnapshot StartPractice(bool lethal = true)
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            var r = _s.Dispatch(new StartPracticeEncounterCommand(lethal));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
            return _s.GetEncounter();
        }

        /// <summary>Plays the encounter to its end: the player attacks whoever is in reach (else ends the turn); the AI takes its turns.</summary>
        private EncounterSnapshot PlayOut(int maxSteps = 600)
        {
            var snap = _s.GetEncounter();
            for (var step = 0; step < maxSteps && snap != null && snap.Status == EncounterStatus.Active; step++)
            {
                CommandResult r;
                if (snap.IsPlayersTurn)
                {
                    var attack = snap.PlayerOptions.FirstOrDefault(o => o.Action is AttackAction && o.Unavailable == null && o.Label.StartsWith(snap.Player.Weapon, StringComparison.Ordinal));
                    var action = attack?.Action ?? new EndTurnAction();
                    r = _s.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, action, snap.Sequence));
                }
                else r = _s.Dispatch(new RunAiTurnCommand(snap.Id, snap.Sequence));

                Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
                snap = _s.GetEncounter(true);
            }

            return snap;
        }

        [Test]
        public void PracticeEncounter_StartsInTheSettlement_WithTheLocalGuardAsAnAlly()
        {
            var snap = StartPractice();
            Assert.That(snap, Is.Not.Null);
            Assert.That(snap.Kind, Is.EqualTo(EncounterKind.Practice));
            Assert.That(snap.Name, Does.StartWith("Practice"));
            Assert.That(snap.Order.Count(c => c.Kind == CombatantKind.Creature && c.HostileToPlayer), Is.EqualTo(2));
            var w = W;
            // Phase 10: the guard on duty (rank and file before the captain), then the lowest ID.
            var guard = w.Npcs.Where(n => !n.IsDeceased && n.Role == NpcRole.Guard && n.HomeSettlementId == w.Player.SettlementId)
                .OrderBy(n => n.Tier == NpcTier.Notable ? 1 : 0).ThenBy(n => n.NpcId).FirstOrDefault();
            if (guard != null)
            {
                var ally = snap.Order.Single(c => c.Kind == CombatantKind.Npc);
                Assert.That(ally.Name, Is.EqualTo(guard.Name), "The guard is the resident, not a copy.");
                Assert.That(ally.HostileToPlayer, Is.False);
            }

            Assert.That(w.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.EncounterStarted), Is.EqualTo(1));
            Assert.That(_s.Dispatch(new StartPracticeEncounterCommand()).IsSuccess, Is.False, "One fight at a time.");
        }

        [Test]
        public void TimeStandsStill_DuringAFight()
        {
            StartPractice();
            var date = W.CurrentSimulationDate;
            var minute = W.MinuteOfDay;
            var r = _s.Execute(new AdvanceDayCommand());
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Message, Is.EqualTo(WorldSession.EncounterBlocksTime));
            Assert.That(_s.Execute(new AdvanceTimeCommand(30)).IsSuccess, Is.False);
            Assert.That(W.CurrentSimulationDate, Is.EqualTo(date));
            Assert.That(W.MinuteOfDay, Is.EqualTo(minute));
        }

        [Test]
        public void StaleOrDuplicateActions_AreRefused()
        {
            var snap = StartPractice();
            for (var i = 0; i < 10 && !snap.IsPlayersTurn && snap.Status == EncounterStatus.Active; i++)
            {
                Assert.That(_s.Dispatch(new RunAiTurnCommand(snap.Id, snap.Sequence)).IsSuccess, Is.True);
                snap = _s.GetEncounter(true);
            }

            Assume.That(snap.IsPlayersTurn, Is.True);
            var dash = new CombatActionCommand(snap.Id, snap.Player.Id, new DashAction(), snap.Sequence);
            Assert.That(_s.Dispatch(dash).IsSuccess, Is.True);
            var again = _s.Dispatch(dash);
            Assert.That(again.IsSuccess, Is.False, "The same click twice is processed once.");
            Assert.That(again.Message, Does.Contain("already processed"));
            Assert.That(_s.Dispatch(new RunAiTurnCommand(snap.Id)).IsSuccess, Is.False, "The AI cannot act on the player's turn.");
            var dashOption = _s.GetEncounter().PlayerOptions.Single(o => o.Label == "Dash");
            Assert.That(dashOption.Unavailable, Is.EqualTo("The action this turn is used."), "The UI is told why.");
        }

        [Test]
        public void AFight_PlaysToItsEnd_WithAFewChronicleEntries_NotOnePerSwing()
        {
            StartPractice();
            var eventsBefore = W.HistoricalEvents.Count;
            var end = PlayOut();
            Assert.That(end.Status, Is.EqualTo(EncounterStatus.Ended));
            Assert.That(end.Outcome, Is.Not.EqualTo(EncounterOutcome.Undecided));
            var w = W;
            var combatEvents = w.HistoricalEvents.Where(e => e.Get("source") == "combat").ToList();
            Assert.That(combatEvents.Count(e => e.EventType == HistoricalEventType.EncounterStarted), Is.EqualTo(1));
            Assert.That(combatEvents.Count(e => e.EventType == HistoricalEventType.EncounterEnded), Is.EqualTo(1));
            Assert.That(combatEvents.All(e => e.EventType == HistoricalEventType.EncounterStarted || e.EventType == HistoricalEventType.EncounterEnded ||
                                             e.EventType == HistoricalEventType.CombatantDowned || e.EventType == HistoricalEventType.CombatantKilled || e.EventType == HistoricalEventType.NpcDied), Is.True);
            Assert.That(combatEvents.Count, Is.LessThan(end.Log.Count), "The combat log holds the detail; the chronicle only the moments that matter.");
            Assert.That(combatEvents.Count, Is.LessThanOrEqualTo(2 + 2 * end.Order.Count));
            Assert.That(w.HistoricalEvents.Count - eventsBefore, Is.EqualTo(combatEvents.Count - 1));
            Assert.That(_s.Execute(new AdvanceDayCommand()).IsSuccess, Is.True, "Time moves again.");
            Assert.That(CharacterValidation.Check(w.Player), Is.Empty);
        }

        [Test]
        public void AResidentKilledInAFight_DiesThroughTheSharedDeathRoutine()
        {
            var snap = StartPractice();
            var ally = snap.Order.FirstOrDefault(c => c.Kind == CombatantKind.Npc);
            Assume.That(ally, Is.Not.Null, "This world has a guard.");
            var w = W;
            var npc = w.Npcs.Single(n => n.Name == ally.Name && n.Role == NpcRole.Guard);
            Assert.That(_s.Dispatch(new ApplyConditionCommand(snap.Id, ally.Id, ConditionType.Exhaustion, "test", 0, 6)).IsSuccess, Is.True);
            w = W;
            var dead = w.FindNpc(npc.NpcId);
            Assert.That(dead.IsDeceased, Is.True);
            Assert.That(dead.DiedOn, Is.EqualTo(w.CurrentSimulationDate));
            var died = w.HistoricalEvents.Single(e => e.EventType == HistoricalEventType.NpcDied && e.NpcIds.Contains(npc.NpcId));
            Assert.That(died.Get("cause"), Is.EqualTo("combat"));
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(w).Errors));
        }

        [Test]
        public void ADeadPlayer_StaysDead_AndCannotFightAgain()
        {
            var snap = StartPractice();
            Assert.That(_s.Dispatch(new ApplyConditionCommand(snap.Id, snap.Player.Id, ConditionType.Exhaustion, "test", 0, 6)).IsSuccess, Is.True);
            var w = W;
            Assert.That(w.Player.Rpg.IsDead, Is.True);
            Assert.That(w.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.CombatantKilled), Is.True);
            var after = _s.GetEncounter(true);
            if (after.Status == EncounterStatus.Active) Assert.That(_s.Dispatch(new EndEncounterCommand(after.Id, false)).IsSuccess, Is.True);
            var again = _s.Dispatch(new StartPracticeEncounterCommand());
            Assert.That(again.IsSuccess, Is.False);
            Assert.That(string.Join(" ", again.Errors), Does.Contain("dead"));
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(5)).IsSuccess, Is.False, "No resurrection.");

            Assert.That(_s.Execute(new SaveWorldCommand("fallen")).IsSuccess, Is.True);
            var fresh = RealmkeeperComposition.CreateSession(_dir);
            Assert.That(fresh.Execute(new LoadWorldCommand("fallen")).IsSuccess, Is.True);
            Assert.That(fresh.CurrentWorldSnapshotForTests().Player.Rpg.IsDead, Is.True);
        }

        [Test]
        public void Withdrawal_NeedsDistance_AndNonLethalFights_LeaveEveryoneAlive()
        {
            var snap = StartPractice(lethal: false);
            Assert.That(snap.Lethality, Is.EqualTo(Lethality.NonLethal));
            var end = PlayOut();
            Assert.That(end.Status, Is.EqualTo(EncounterStatus.Ended));
            Assert.That(end.Order.All(c => c.State != VitalState.Dead), Is.True, "Nobody dies in a non-lethal fight.");
            Assert.That(W.Player.Rpg.IsDead, Is.False);
        }

        [Test]
        public void AnActiveFight_SurvivesSaveAndLoad_AndContinuesIdentically()
        {
            var snap = StartPractice();
            for (var i = 0; i < 3 && snap.Status == EncounterStatus.Active; i++)
            {
                var r = snap.IsPlayersTurn
                    ? _s.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, new EndTurnAction(), snap.Sequence))
                    : _s.Dispatch(new RunAiTurnCommand(snap.Id, snap.Sequence));
                Assert.That(r.IsSuccess, Is.True);
                snap = _s.GetEncounter(true);
            }

            Assume.That(snap.Status, Is.EqualTo(EncounterStatus.Active));
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);
            var loaded = serializer.Deserialize(json);
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(WorldFingerprint.Of(loaded.World), Is.EqualTo(WorldFingerprint.Of(W)));
            Assert.That(JObject.Parse(serializer.Serialize(loaded.World))["world"].ToString(), Is.EqualTo(JObject.Parse(json)["world"].ToString()), "Identical world section on a round trip.");
            var doc = JObject.Parse(json);
            Assert.That(doc["world"]["encounters"][0]["combatants"][0]["armorClass"], Is.Null, "Derived values are not saved.");

            // The saved and the live encounter play on the same way.
            Assert.That(_s.Execute(new SaveWorldCommand("mid-fight")).IsSuccess, Is.True);
            var fresh = RealmkeeperComposition.CreateSession(_dir);
            Assert.That(fresh.Execute(new LoadWorldCommand("mid-fight")).IsSuccess, Is.True);
            for (var i = 0; i < 6; i++)
            {
                var a = _s.GetEncounter(true);
                var b = fresh.GetEncounter(true);
                if (a.Status != EncounterStatus.Active) break;
                CommandResult ra, rb;
                if (a.IsPlayersTurn)
                {
                    ra = _s.Dispatch(new CombatActionCommand(a.Id, a.Player.Id, new EndTurnAction(), a.Sequence));
                    rb = fresh.Dispatch(new CombatActionCommand(b.Id, b.Player.Id, new EndTurnAction(), b.Sequence));
                }
                else
                {
                    ra = _s.Dispatch(new RunAiTurnCommand(a.Id, a.Sequence));
                    rb = fresh.Dispatch(new RunAiTurnCommand(b.Id, b.Sequence));
                }

                Assert.That(rb.Message, Is.EqualTo(ra.Message));
            }

            Assert.That(string.Join("\n", fresh.GetEncounter(true).Log.Select(l => l.Text)), Is.EqualTo(string.Join("\n", _s.GetEncounter(true).Log.Select(l => l.Text))));
        }

        [Test]
        public void FinishedEncounters_AreBounded()
        {
            CombatTests.Create(_s, "fighter");
            CombatTests.EnterTown(_s);
            for (var i = 0; i < WorldData.MaxEndedEncounters + 3; i++)
            {
                Assert.That(_s.Dispatch(new StartPracticeEncounterCommand(false)).IsSuccess, Is.True);
                var snap = _s.GetEncounter();
                Assert.That(_s.Dispatch(new EndEncounterCommand(snap.Id, false)).IsSuccess, Is.True);
            }

            Assert.That(W.Encounters.Count, Is.EqualTo(WorldData.MaxEndedEncounters));
        }

        // ===== Migration =====================================================================================

        [Test]
        public void PhaseSevenSave_MigratesToV10_WithStructuresAndNoFight()
        {
            CombatTests.Create(_s, "rogue");
            Assert.That(_s.Dispatch(new ChangeHitPointsCommand(-2)).IsSuccess, Is.True);
            var original = W;
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(original));
            Phase2Worlds.StripToV9(doc);
            Assert.That(doc["world"]["structures"], Is.Null);
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var w = loaded.World;
            Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion)); // v10 when written (Phase 8)
            Assert.That(w.Encounters, Is.Empty);
            Assert.That(w.Player.Rpg.DeathSaveSuccesses + w.Player.Rpg.DeathSaveFailures, Is.Zero);
            Assert.That(w.Player.Rpg.IsStable || w.Player.Rpg.IsDead, Is.False);
            Assert.That(w.Player.Rpg.CurrentHp, Is.EqualTo(original.Player.Rpg.CurrentHp), "Hit points are kept.");
            Assert.That(w.Player.Inventory.Select(i => i.StackId), Is.EqualTo(original.Player.Inventory.Select(i => i.StackId)));
            Assert.That(w.Structures.Select(s => s.StructureId), Is.EquivalentTo(original.Structures.Select(s => s.StructureId)), "Structures are built from the save's own places, as a new world would have them.");
            Assert.That(w.HistoricalEvents.Count, Is.EqualTo(original.HistoricalEvents.Count), "The chronicle is untouched.");
        }

        [Test]
        public void PhaseSixAndFiveSaves_StillLoad_AndGainStructures()
        {
            CombatTests.Create(_s, "fighter");
            var serializer = new JsonWorldSerializer();
            var v8 = JObject.Parse(serializer.Serialize(W));
            ProgressionTests.StripToV8(v8);
            var loaded8 = serializer.Deserialize(v8.ToString());
            Assert.That(loaded8.IsSuccess, Is.True, string.Join("; ", loaded8.Errors));
            Assert.That(loaded8.World.Structures, Is.Not.Empty);

            var v7 = JObject.Parse(serializer.Serialize(Phase2Worlds.Generated(97531)));
            Phase2Worlds.StripToV7(v7);
            var loaded7 = serializer.Deserialize(v7.ToString());
            Assert.That(loaded7.IsSuccess, Is.True, string.Join("; ", loaded7.Errors));
            Assert.That(loaded7.World.Structures.Count, Is.EqualTo(Phase2Worlds.Generated(97531).Structures.Count));
        }

        // ===== Validation ====================================================================================

        [Test]
        public void TamperedSaves_AreRefused()
        {
            var snap = StartPractice();
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);

            bool Loads(Action<JObject> change)
            {
                var doc = JObject.Parse(json);
                change(doc);
                return serializer.Deserialize(doc.ToString()).IsSuccess;
            }

            Assert.That(Loads(_ => { }), Is.True);
            Assert.That(Loads(d =>
            {
                var copy = (JObject)d["world"]["encounters"][0].DeepClone();
                copy["encounterId"] = Guid.NewGuid().ToString();
                ((JArray)d["world"]["encounters"]).Add(copy);
            }), Is.False, "Two active encounters.");
            Assert.That(Loads(d => d["world"]["encounters"][0]["combatants"][1]["currentHp"] = 999), Is.False, "Hit points above the maximum.");
            Assert.That(Loads(d => d["world"]["encounters"][0]["combatants"][1]["creatureId"] = "dragon"), Is.False, "Unknown creature.");
            Assert.That(Loads(d => ((JArray)d["world"]["encounters"][0]["order"]).RemoveAt(0)), Is.False, "Order missing a combatant.");
            Assert.That(Loads(d => d["world"]["encounters"][0]["status"] = "Paused"), Is.False, "Unknown status.");
            Assert.That(Loads(d => d["world"]["player"]["rpg"]["deathSaveFailures"] = 4), Is.False, "Four failures.");
            Assert.That(Loads(d => d["world"]["player"]["rpg"]["isDead"] = true), Is.False, "Dead with hit points.");
            Assert.That(Loads(d => d["world"]["structures"][0]["definitionId"] = "palace"), Is.False, "Unknown structure.");
            Assert.That(Loads(d => d["world"]["structures"][1]["locationId"] = d["world"]["structures"][0]["locationId"]), Is.False, "Two structures on one place.");
            if (snap.Order.Any(c => c.Kind == CombatantKind.Npc))
                Assert.That(Loads(d =>
                {
                    var combatants = (JArray)d["world"]["encounters"][0]["combatants"];
                    var npc = (JObject)combatants.First(c => (string)c["kind"] == "Npc").DeepClone();
                    npc["combatantId"] = Guid.NewGuid().ToString();
                    npc["position"]["x"] = 40;
                    combatants.Add(npc);
                    ((JArray)d["world"]["encounters"][0]["order"]).Add(npc["combatantId"]);
                }), Is.False, "The same resident twice.");
        }
    }
}
