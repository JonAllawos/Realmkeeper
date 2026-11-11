using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Politics
{
    /// <summary>Validation, generation, commands, persistence, integration and performance of the political engine.</summary>
    public sealed class PoliticalWorldTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Phase2Worlds.TempDirectory();

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>Everything political a player can see, as text: equal snapshots mean equal political state.</summary>
        private static string Snapshot(WorldSession session)
        {
            var sb = new StringBuilder();
            foreach (var f in session.GetFactions())
            {
                sb.Append($"F {f.FactionId} {f.Name} {f.Type} {f.Status} inf={f.Influence}/{f.InfluenceTarget} coin={f.Treasury} members={f.TotalMembers} leader={f.LeaderNpcId}").AppendLine();
                foreach (var p in f.Presence) sb.Append($"  P {p.SettlementId} {p.Strength} {p.Members} {p.Status}").AppendLine();
                foreach (var g in f.Goals) sb.Append($"  G {g.GoalId} {g.Type} {g.TargetValue} {g.Progress} {g.Priority} {g.Status} {g.CloseReason} {g.IsManual}").AppendLine();
            }

            foreach (var r in session.GetRelations()) sb.Append($"R {r.FactionA} {r.FactionB} {r.Opinion} {r.Trust} {r.Rivalry} {r.Agreement} {r.State}").AppendLine();
            foreach (var c in session.GetClaims()) sb.Append($"C {c.ClaimId} {c.ClaimantId} {c.SettlementId} {c.Status} {c.IsManual}").AppendLine();
            foreach (var s in session.GetSettlements())
            {
                var p = session.GetSettlementPolitics(s.SettlementId);
                sb.Append($"S {s.SettlementId} gov={p.GoverningFactionId} admin={p.AdministratorFactionId} pop={s.Population} coin={s.Find(ResourceTypes.Coin).Stock}").AppendLine();
            }

            sb.Append("events=").Append(session.GetOverview().EventCount);
            return sb.ToString();
        }

        private static FactionSummary Of(WorldSession session, FactionType type, bool governing = true) =>
            session.GetFactions().First(f => f.Type == type && (f.GovernedSettlementIds.Count > 0) == governing);

        // ----- Validation -------------------------------------------------------------------------

        private static IEnumerable<TestCaseData> BrokenWorlds()
        {
            TestCaseData Case(string name, Action<PoliticalFixture> breakIt, string expected) => new TestCaseData(breakIt, expected).SetName("Validator rejects: " + name);
            yield return Case("unknown faction type", fx => fx.Guild.Type = (FactionType)99, "unknown faction type");
            yield return Case("unknown status", fx => fx.Guild.Status = (FactionStatus)42, "unknown status");
            yield return Case("no founding date", fx => fx.Guild.FoundedOn = default, "founding date");
            yield return Case("influence above 100", fx => fx.Guild.Influence = 101, "outside 0..100");
            yield return Case("negative treasury", fx => fx.Guild.Treasury = -1, "negative treasury");
            yield return Case("leader who does not exist", fx => fx.Crown.LeaderNpcId = new NpcId(Guid.NewGuid()), "leader who does not exist");
            yield return Case("guild governing", fx => fx.Ashford.FactionId = fx.Guild.FactionId, "cannot govern");
            yield return Case("unknown governor", fx => fx.Ashford.FactionId = new FactionId(Guid.NewGuid()), "unknown faction");
            yield return Case("administrator without governor", fx => { fx.Ashford.FactionId = default; fx.Ashford.AdministratorFactionId = fx.House.FactionId; }, "no governing authority");
            yield return Case("governor as its own administrator", fx => fx.Ashford.AdministratorFactionId = fx.Crown.FactionId, "administered by its own governor");
            yield return Case("guild administering", fx => fx.Ashford.AdministratorFactionId = fx.Guild.FactionId, "cannot administer");
            yield return Case("presence in an unknown settlement", fx => fx.Guild.Presence[0].SettlementId = new SettlementId(Guid.NewGuid()), "unknown settlement");
            yield return Case("two presences in one settlement", fx => PoliticalFixture.Present(fx.Guild, fx.Ashford, 10, 0), "two presence records");
            yield return Case("strength of zero", fx => fx.Guild.Presence[0].Strength = 0, "outside 1..100");
            yield return Case("more members than people", fx => fx.Guild.Presence[0].Members = 1001, "members in a settlement of 1000");
            yield return Case("members of all factions exceed the population", fx => fx.Guild.Presence[0].Members = 700, "exceed its population");
            yield return Case("relation with itself", fx => fx.World.Relations.Add(new FactionRelationData { FactionA = fx.Crown.FactionId, FactionB = fx.Crown.FactionId }), "to itself");
            yield return Case("relation stored backwards", fx => { var r = fx.World.Relations[0]; (r.FactionA, r.FactionB) = (r.FactionB, r.FactionA); }, "wrong order");
            yield return Case("relation recorded twice", fx => fx.World.Relations.Add(fx.World.Relations[0].Clone()), "recorded twice");
            yield return Case("relation with an unknown faction", fx => fx.World.Relations[0].FactionB = new FactionId(Guid.NewGuid()), "unknown faction");
            yield return Case("opinion above 100", fx => fx.World.Relations[0].Opinion = 101, "outside -100..100");
            yield return Case("opinion below -100", fx => fx.World.Relations[0].Opinion = -101, "outside -100..100");
            yield return Case("rivalry above 100", fx => fx.World.Relations[0].Rivalry = 101, "rivalry");
            yield return Case("agreement without a date", fx => fx.World.Relations[0].Agreement = AgreementType.TradeAgreement, "no start date");
            yield return Case("claim by a guild", fx => fx.Claim(1, fx.Guild, fx.Brookmere, ClaimStatus.Unresolved), "cannot claim territory");
            yield return Case("two open claims on one settlement", fx => { fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Unresolved); fx.Claim(2, fx.House, fx.Ashford, ClaimStatus.Contested); }, "two open claims");
            yield return Case("claim on an unknown settlement", fx => fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Unresolved).SettlementId = new SettlementId(Guid.NewGuid()), "unknown settlement");
            yield return Case("duplicate claim ID", fx => { fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Relinquished); fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Relinquished); }, "Duplicate ClaimId");
            yield return Case("goal priority 0", fx => PoliticalFixture.Goal(fx.Guild, 1, GoalType.GainInfluence, 50).Priority = 0, "outside 1..5");
            yield return Case("presence goal without a settlement", fx => PoliticalFixture.Goal(fx.Guild, 1, GoalType.EstablishPresence, 30), "without a target settlement");
            yield return Case("relationship goal aimed at itself", fx => PoliticalFixture.Goal(fx.Guild, 1, GoalType.ImproveRelationship, 30, default, fx.Guild.FactionId), "another faction");
            yield return Case("goal aimed at an unknown faction", fx => PoliticalFixture.Goal(fx.Guild, 1, GoalType.ImproveRelationship, 30, default, new FactionId(Guid.NewGuid())), "unknown faction");
            yield return Case("closed goal without a reason", fx => PoliticalFixture.Goal(fx.Guild, 1, GoalType.GainInfluence, 50).Status = GoalStatus.Completed, "without a reason");
            yield return Case("duplicate goal ID", fx => { PoliticalFixture.Goal(fx.Guild, 1, GoalType.GainInfluence, 50); PoliticalFixture.Goal(fx.House, 1, GoalType.GainInfluence, 50); }, "Duplicate GoalId");
            yield return Case("dissolved faction with a presence", fx => { fx.Guild.Status = FactionStatus.Dissolved; fx.Guild.DissolvedOn = TestWorlds.Start; }, "still has a presence");
            yield return Case("dissolved faction governing", fx => { fx.Crown.Status = FactionStatus.Dissolved; fx.Crown.DissolvedOn = TestWorlds.Start; fx.Crown.Presence.Clear(); }, "governed by dissolved");
            yield return Case("event about an unknown faction", fx => fx.World.HistoricalEvents.Add(new HistoricalEvent(
                new EventId(Guid.NewGuid()), TestWorlds.Start, HistoricalEventType.FactionFounded, EventSeverity.Info, EventImportance.Notable,
                Array.Empty<SettlementId>(), null, null, new[] { new FactionId(Guid.NewGuid()) })), "references unknown faction");
        }

        [TestCaseSource(nameof(BrokenWorlds))]
        public void Validator_Rejects(Action<PoliticalFixture> breakIt, string expected)
        {
            var fx = PoliticalFixture.Create();
            breakIt(fx);
            Assert.That(WorldValidator.Validate(fx.World).Errors, Has.Some.Contains(expected));
        }

        [Test]
        public void FactionIdentity_IsItsId_NotItsNameOrPosition()
        {
            var fx = PoliticalFixture.Create();
            var id = fx.Crown.FactionId;
            fx.Crown.Name = "The Republic";
            fx.World.Factions.Reverse();

            Assert.That(WorldValidator.Validate(fx.World).Errors, Is.Empty);
            Assert.That(fx.World.FindFaction(fx.Ashford.FactionId).Name, Is.EqualTo("The Republic"));
            Assert.That(fx.World.FindRelation(fx.House.FactionId, id), Is.Not.Null, "Relations follow the ID in either order.");
            Assert.That(fx.World.FindRelation(id, fx.House.FactionId), Is.SameAs(fx.World.FindRelation(fx.House.FactionId, id)));
        }

        // ----- Generation -------------------------------------------------------------------------

        [Test]
        public void GeneratedWorld_HasACoherentPoliticalStructure()
        {
            var world = Phase2Worlds.Generated(123456789);
            Assert.That(WorldValidator.Validate(world).Errors, Is.Empty);
            Assert.That(world.Factions.Select(f => f.Type).OrderBy(t => t), Is.EqualTo(new[]
            {
                FactionType.Kingdom, FactionType.NobleHouse, FactionType.NobleHouse, FactionType.Guild, FactionType.ReligiousOrder,
            }));
            Assert.That(world.Factions.Select(f => f.FactionId).Distinct().Count(), Is.EqualTo(5));
            Assert.That(world.Factions.Select(f => f.Name).Distinct().Count(), Is.EqualTo(5));
            Assert.That(world.Factions.All(f => f.FoundedOn == world.WorldSettings.StartingDate && f.Status == FactionStatus.Active), Is.True);

            var kingdom = world.Factions.Single(f => f.Type == FactionType.Kingdom);
            var town = world.Settlements.Single(s => s.SettlementType == SettlementType.Town);
            var village = world.Settlements.Single(s => s.SettlementType == SettlementType.Village);
            var hamlet = world.Settlements.Single(s => s.SettlementType == SettlementType.Hamlet);
            Assert.That(town.FactionId, Is.EqualTo(kingdom.FactionId));
            Assert.That(village.FactionId, Is.EqualTo(kingdom.FactionId));
            Assert.That(hamlet.FactionId, Is.Not.EqualTo(kingdom.FactionId), "The hamlet keeps its own governing house.");
            var administrator = world.FindFaction(village.AdministratorFactionId);
            Assert.That(administrator.Type, Is.EqualTo(FactionType.NobleHouse), "A noble house administers the village for the crown.");
            Assert.That(town.AdministratorFactionId.IsEmpty, Is.True);

            // Leaders are existing residents; one leadership is explicitly vacant.
            var leaders = world.Factions.Where(f => !f.LeaderNpcId.IsEmpty).Select(f => f.LeaderNpcId).ToList();
            Assert.That(leaders.Count, Is.EqualTo(4));
            Assert.That(leaders.Distinct().Count(), Is.EqualTo(4), "Nobody leads two factions.");
            Assert.That(leaders.All(id => world.FindNpc(id) != null), Is.True);
            Assert.That(world.Factions.Single(f => f.Type == FactionType.ReligiousOrder).LeaderNpcId.IsEmpty, Is.True);
            Assert.That(world.Npcs.Count, Is.EqualTo(15 + 4 + 5), "No resident was created for politics (Phase 13: five are the watch fort's garrison) (the four Phase 10 people fill posts: guards, a priest, a warden).");

            // Every pair has one relationship; the starting agreements are in place.
            Assert.That(world.Relations.Count, Is.EqualTo(10));
            var guild = world.Factions.Single(f => f.Type == FactionType.Guild);
            Assert.That(world.FindRelation(kingdom.FactionId, guild.FactionId).Agreement, Is.EqualTo(AgreementType.TradeAgreement));
            Assert.That(world.FindRelation(kingdom.FactionId, administrator.FactionId).Agreement, Is.EqualTo(AgreementType.MutualSupport));
            Assert.That(PoliticalMath.StateOf(world.FindRelation(kingdom.FactionId, administrator.FactionId)), Is.EqualTo(RelationState.Allied));

            // Membership is aggregate and fits the population.
            foreach (var s in world.Settlements)
            {
                var members = world.Factions.Sum(f => f.FindPresence(s.SettlementId)?.Members ?? 0);
                Assert.That(members, Is.InRange(1, s.Population), s.Name);
            }

            Assert.That(world.Factions.All(f => f.Goals.Count >= 1 && f.Goals.All(g => g.IsOpen)), Is.True, "Every faction starts with a purpose.");
            Assert.That(world.Factions.All(f => f.Influence == f.InfluenceTarget && f.Influence > 0), Is.True, "Starting values sit at their own targets.");
            Assert.That(world.WorldSettings.EnabledSimulationSystems, Does.Contain(PoliticalSystem.Id));
        }

        [Test]
        public void SameSeed_SamePolitics_DifferentSeed_DifferentButValid()
        {
            var a = Phase2Worlds.Generated(424242);
            var b = Phase2Worlds.Generated(424242);
            Assert.That(WorldFingerprint.Of(b), Is.EqualTo(WorldFingerprint.Of(a)));

            var seen = new HashSet<FactionId>(a.Factions.Select(f => f.FactionId));
            foreach (var seed in new long[] { 1, 77, 9001, 31337 })
            {
                var other = Phase2Worlds.Generated(seed);
                Assert.That(WorldValidator.Validate(other).Errors, Is.Empty, $"Seed {seed}");
                Assert.That(other.Factions.Count, Is.EqualTo(5), $"Seed {seed}");
                Assert.That(other.Factions.Any(f => seen.Contains(f.FactionId)), Is.False, "Different seeds give different identities.");
            }
        }

        [Test]
        public void InitializingTwice_OrLoadingRepeatedly_NeverDuplicatesFactions()
        {
            var world = Phase2Worlds.Generated(555);
            var before = WorldFingerprint.Of(world);
            PoliticalInitializer.Ensure(world, new Xoshiro256RandomFactory());
            PoliticalInitializer.Ensure(world, new Xoshiro256RandomFactory());
            Assert.That(WorldFingerprint.Of(world), Is.EqualTo(before));

            var serializer = new JsonWorldSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(world)).World;
            loaded = serializer.Deserialize(serializer.Serialize(loaded)).World;
            Assert.That(WorldFingerprint.Of(loaded), Is.EqualTo(before), "Save and load are exact for political state.");
        }

        // ----- Persistence ------------------------------------------------------------------------

        [Test]
        public void Version4Save_GainsItsPolitics_AndKeepsEverythingElse()
        {
            var fresh = Phase2Worlds.GeneratedBeforeLivingWorld(777); // Phase 10: the v11 world (its living-world additions arrive when a session loads a save; see the Phase 10 tests).
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(Phase2Worlds.GeneratedBeforeCommerce(777)));
            Phase2Worlds.StripToV5(doc);

            // Rebuild the v4 layout: two bare factions, no relations, claims, administrators or political system.
            doc["schemaVersion"] = 4;
            var w = (JObject)doc["world"];
            w.Remove("relations");
            w.Remove("claims");
            var factions = (JArray)w["factions"];
            var kept = new JArray();
            foreach (JObject f in factions.Take(2)) kept.Add(new JObject { ["factionId"] = f["factionId"], ["name"] = f["name"] });
            w["factions"] = kept;
            foreach (JObject s in (JArray)w["settlements"]) s.Remove("administratorFactionId");
            var keptIds = new HashSet<string>(kept.Select(f => (string)f["factionId"]));
            foreach (JObject n in (JArray)w["npcs"])
            {
                // In v4 a resident's faction was simply their settlement's governor.
                var home = ((JArray)w["settlements"]).First(s => (string)s["settlementId"] == (string)n["homeSettlementId"]);
                n["factionId"] = home["factionId"];
            }

            var systems = (JArray)w["settings"]["enabledSimulationSystems"];
            systems.First(t => (string)t == PoliticalSystem.Id).Remove();
            Assert.That(systems.Count, Is.EqualTo(7));

            var result = serializer.Deserialize(doc.ToString());

            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors));
            var upgraded = result.World;
            Assert.That(upgraded.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(upgraded.WorldSettings.EnabledSimulationSystems, Does.Contain(PoliticalSystem.Id));
            Assert.That(WorldFingerprint.Of(upgraded), Is.EqualTo(WorldFingerprint.Of(fresh)),
                "Same factions, leaders, relations and goals as a fresh world of that seed; settlements, stock, routes and history untouched.");
            Assert.That(upgraded.Factions.Take(2).Select(f => f.FactionId.ToString()), Is.EqualTo(keptIds), "The existing factions keep their IDs.");
        }

        [Test]
        public void Version4Save_WithNoFactions_GetsNoneInvented()
        {
            var world = TestWorlds.SingleSettlement(100, 1000, 0, TestWorlds.FoodSystems);
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(world));
            doc["schemaVersion"] = 4;
            ((JObject)doc["world"]).Remove("relations");
            ((JObject)doc["world"]).Remove("claims");

            var result = serializer.Deserialize(doc.ToString());

            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.World.Factions, Is.Empty);
            Assert.That(result.World.WorldSettings.EnabledSimulationSystems, Does.Not.Contain(PoliticalSystem.Id), "Only worlds running the standard systems gain politics.");
        }

        [Test]
        public void SaveAndReload_MidPolitics_IsExact_AndTheSimulationContinuesIdentically()
        {
            var a = Phase2Worlds.Session(_dir);
            var house = Of(a, FactionType.NobleHouse, governing: false);
            var hamlet = a.GetSettlements().Single(s => s.SettlementType == SettlementType.Hamlet);
            var guild = a.GetFactions().Single(f => f.Type == FactionType.Guild);
            a.Execute(new AdvanceDaysCommand(40));
            Assert.That(a.Dispatch(new FileTerritorialClaimCommand(house.FactionId, hamlet.SettlementId)).IsSuccess, Is.True);
            Assert.That(a.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.GainInfluence, 60, 5)).IsSuccess, Is.True);
            Assert.That(a.Dispatch(new SetFactionRelationCommand(house.FactionId, guild.FactionId, -40)).IsSuccess, Is.True);
            a.Execute(new AdvanceDaysCommand(30));
            Assert.That(a.Dispatch(new SaveWorldCommand("politics")).IsSuccess, Is.True);
            var saved = Snapshot(a);

            var b = Phase2Worlds.Session(Phase2Worlds.TempDirectory(), 999); // a different world, then the save
            File.Copy(Path.Combine(_dir, "politics" + JsonWorldStore.Extension), Path.Combine(_dir, "copy" + JsonWorldStore.Extension));
            var c = Phase2Worlds.Session(_dir, 999);
            Assert.That(c.Execute(new LoadWorldCommand("copy")).IsSuccess, Is.True);
            Assert.That(Snapshot(c), Is.EqualTo(saved), "Relations, claims, goals, presence and influence survive exactly.");
            Assert.That(c.GetFactions().Count, Is.EqualTo(5), "Loading regenerates nothing.");
            var goal = c.FindFaction(guild.FactionId).Goals.Single(g => g.IsManual);
            Assert.That(goal.Progress, Is.EqualTo(a.FindFaction(guild.FactionId).Goals.Single(g => g.IsManual).Progress).And.GreaterThan(0));

            a.Execute(new AdvanceDaysCommand(60));
            c.Execute(new AdvanceDaysCommand(60));
            Assert.That(Snapshot(c), Is.EqualTo(Snapshot(a)), "A reloaded world continues exactly as the original does.");
            GC.KeepAlive(b);
        }

        [Test]
        public void DissolvedFaction_AndItsHistory_SurviveSaveAndLoad()
        {
            var world = Phase2Worlds.Generated(123456789);
            var guild = world.Factions.Single(f => f.Type == FactionType.Guild);
            guild.Presence.Clear();
            guild.Treasury = 0;
            guild.Influence = 3;
            foreach (var r in world.Relations.Where(r => r.Involves(guild.FactionId)))
            {
                r.Agreement = AgreementType.None;
                r.AgreementSince = default;
                r.Opinion = -100;
            }

            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory(), null, new PoliticalRules { DissolveAfterDormantTicks = 3 });
            var run = scheduler.AdvanceDays(world, 35);
            Assert.That(run.Completed, Is.True, string.Join("; ", run.Errors));
            var after = run.World;
            Assert.That(after.FindFaction(guild.FactionId).Status, Is.EqualTo(FactionStatus.Dissolved), "Fixture: the guild dissolved.");

            var serializer = new JsonWorldSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(after));

            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(WorldFingerprint.Of(loaded.World), Is.EqualTo(WorldFingerprint.Of(after)));
            var kept = loaded.World.FindFaction(guild.FactionId);
            Assert.That(kept.Status, Is.EqualTo(FactionStatus.Dissolved));
            Assert.That(kept.Goals.All(g => g.CloseReason == GoalCloseReason.FactionDissolved), Is.True);
            var history = loaded.World.HistoricalEvents.Where(e => e.FactionIds.Contains(guild.FactionId)).ToList();
            Assert.That(history.Any(e => e.EventType == HistoricalEventType.FactionDissolved), Is.True);
            Assert.That(scheduler.AdvanceDays(loaded.World, 30).Completed, Is.True, "The world keeps running with a dissolved faction in it.");
        }

        // ----- Commands -----------------------------------------------------------------------------

        [Test]
        public void InvalidPoliticalCommands_FailWithTheRightStatus_AndChangeNothing()
        {
            var s = Phase2Worlds.Session(_dir);
            var kingdom = Of(s, FactionType.Kingdom);
            var guild = s.GetFactions().Single(f => f.Type == FactionType.Guild);
            var order = s.GetFactions().Single(f => f.Type == FactionType.ReligiousOrder);
            var house = Of(s, FactionType.NobleHouse, governing: false);
            var town = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Town);
            var hamlet = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Hamlet);
            var ghostFaction = new FactionId(Guid.NewGuid());
            var ghostTown = new SettlementId(Guid.NewGuid());
            var before = Snapshot(s);

            void Refused(CommandResult result, CommandStatus expected, string what)
            {
                Assert.That(result.Status, Is.EqualTo(expected), what + ": " + result);
                Assert.That(result.Message, Is.Not.Empty, what);
                Assert.That(Snapshot(s), Is.EqualTo(before), what + " changed the world.");
            }

            Refused(s.Dispatch(new SetFactionRelationCommand(kingdom.FactionId, ghostFaction, 10)), CommandStatus.NotFound, "relation with unknown faction");
            Refused(s.Dispatch(new SetFactionRelationCommand(kingdom.FactionId, kingdom.FactionId, 10)), CommandStatus.ValidationFailed, "relation with itself");
            Refused(s.Dispatch(new SetFactionRelationCommand(kingdom.FactionId, guild.FactionId, 101)), CommandStatus.ValidationFailed, "opinion out of range");
            Refused(s.Dispatch(new SetFactionRelationCommand(kingdom.FactionId, guild.FactionId)), CommandStatus.ValidationFailed, "nothing to change");
            Refused(s.Dispatch(new SetFactionRelationCommand(kingdom.FactionId, guild.FactionId, null, AgreementType.TradeAgreement)), CommandStatus.ValidationFailed, "agreement already in place");
            Refused(s.Dispatch(new SetFactionRelationCommand(default, guild.FactionId, 5)), CommandStatus.ValidationFailed, "empty faction ID");

            Refused(s.Dispatch(new FileTerritorialClaimCommand(guild.FactionId, town.SettlementId)), CommandStatus.ValidationFailed, "guild claiming territory");
            Refused(s.Dispatch(new FileTerritorialClaimCommand(kingdom.FactionId, town.SettlementId)), CommandStatus.ValidationFailed, "claiming what it governs");
            Refused(s.Dispatch(new FileTerritorialClaimCommand(kingdom.FactionId, ghostTown)), CommandStatus.NotFound, "claim on unknown settlement");
            Refused(s.Dispatch(new ResolveTerritorialClaimCommand(new ClaimId(Guid.NewGuid()), ClaimStatus.Recognized)), CommandStatus.NotFound, "unknown claim");
            Refused(s.Dispatch(new ResolveTerritorialClaimCommand(default, ClaimStatus.Recognized)), CommandStatus.ValidationFailed, "empty claim ID");

            Refused(s.Dispatch(new EstablishFactionPresenceCommand(guild.FactionId, town.SettlementId)), CommandStatus.ValidationFailed, "presence that already exists");
            Refused(s.Dispatch(new EstablishFactionPresenceCommand(guild.FactionId, hamlet.SettlementId, 0)), CommandStatus.ValidationFailed, "strength 0");
            Refused(s.Dispatch(new EstablishFactionPresenceCommand(ghostFaction, hamlet.SettlementId)), CommandStatus.NotFound, "presence of unknown faction");
            Refused(s.Dispatch(new RemoveFactionPresenceCommand(guild.FactionId, hamlet.SettlementId)), CommandStatus.ValidationFailed, "removing a presence that is not there");
            Refused(s.Dispatch(new RemoveFactionPresenceCommand(kingdom.FactionId, town.SettlementId)), CommandStatus.InvalidSessionState, "removing the governor's presence");

            Refused(s.Dispatch(new SetSettlementGovernanceCommand(town.SettlementId, guild.FactionId)), CommandStatus.ValidationFailed, "guild as governor");
            Refused(s.Dispatch(new SetSettlementGovernanceCommand(town.SettlementId, kingdom.FactionId, order.FactionId)), CommandStatus.ValidationFailed, "religious order as administrator");
            Refused(s.Dispatch(new SetSettlementGovernanceCommand(town.SettlementId, default, house.FactionId)), CommandStatus.ValidationFailed, "administrator of an independent settlement");
            Refused(s.Dispatch(new SetSettlementGovernanceCommand(town.SettlementId, kingdom.FactionId, kingdom.FactionId)), CommandStatus.ValidationFailed, "governor administering for itself");
            Refused(s.Dispatch(new SetSettlementGovernanceCommand(town.SettlementId, kingdom.FactionId)), CommandStatus.ValidationFailed, "governance already as requested");
            Refused(s.Dispatch(new SetSettlementGovernanceCommand(ghostTown, kingdom.FactionId)), CommandStatus.NotFound, "governance of unknown settlement");

            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.GainInfluence, 0)), CommandStatus.ValidationFailed, "influence goal of 0");
            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.GainInfluence, 50, 9)), CommandStatus.ValidationFailed, "priority 9");
            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.SecureClaim, 0, 3, town.SettlementId)), CommandStatus.ValidationFailed, "guild securing a claim");
            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.MaintainControl)), CommandStatus.ValidationFailed, "control goal for a faction that governs nothing");
            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.ImproveRelationship, 50, 3, default, guild.FactionId)), CommandStatus.ValidationFailed, "relationship goal with itself");
            Refused(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.IncreaseMembership, guild.Goals[0].TargetValue)), CommandStatus.ValidationFailed, "goal it already has");
            Refused(s.Dispatch(new AbandonFactionGoalCommand(guild.FactionId, new GoalId(Guid.NewGuid()))), CommandStatus.NotFound, "abandoning an unknown goal");
            Refused(s.Dispatch(new AbandonFactionGoalCommand(kingdom.FactionId, guild.Goals[0].GoalId)), CommandStatus.NotFound, "abandoning another faction's goal");
            Refused(s.Dispatch(new SetFactionGoalPriorityCommand(guild.FactionId, guild.Goals[0].GoalId, guild.Goals[0].Priority)), CommandStatus.ValidationFailed, "same priority");
        }

        [Test]
        public void RelationCommand_SetsOpinion_MakesAndBreaksAgreements_AndIsMarkedManual()
        {
            var s = Phase2Worlds.Session(_dir);
            var guild = s.GetFactions().Single(f => f.Type == FactionType.Guild);
            var order = s.GetFactions().Single(f => f.Type == FactionType.ReligiousOrder);

            Assert.That(s.Dispatch(new SetFactionRelationCommand(guild.FactionId, order.FactionId, 30, AgreementType.MutualSupport)).IsSuccess, Is.True);
            var r = s.GetRelations(guild.FactionId).Single(x => x.Other(guild.FactionId) == order.FactionId);
            Assert.That(r.Opinion, Is.EqualTo(30));
            Assert.That(r.Agreement, Is.EqualTo(AgreementType.MutualSupport));
            Assert.That(r.AgreementSince, Is.EqualTo(s.GetOverview().Date));
            Assert.That(r.State, Is.EqualTo(RelationState.Friendly), "Opinion 30 is below the 40 an alliance needs.");
            var events = s.GetRecentEvents(2);
            Assert.That(events.Select(e => e.EventType), Is.EquivalentTo(new[] { HistoricalEventType.AgreementEstablished, HistoricalEventType.RelationshipChanged }));
            Assert.That(events.All(e => e.IsManual), Is.True);
            Assert.That(events.All(e => e.FactionIds.Contains(guild.FactionId) && e.FactionIds.Contains(order.FactionId)), Is.True);
            StringAssert.StartsWith("By your hand: ", events[0].Summary);

            // Breaking the pact costs 25 opinion and 30 trust.
            var trust = r.Trust;
            Assert.That(s.Dispatch(new SetFactionRelationCommand(order.FactionId, guild.FactionId, null, AgreementType.None)).IsSuccess, Is.True, "Either order of the pair works.");
            r = s.GetRelations(guild.FactionId).Single(x => x.Other(guild.FactionId) == order.FactionId);
            Assert.That(r.Opinion, Is.EqualTo(5));
            Assert.That(r.Trust, Is.EqualTo(trust - 30));
            Assert.That(r.Agreement, Is.EqualTo(AgreementType.None));
            Assert.That(r.LastCause, Is.EqualTo(RelationCause.BrokenAgreement));
            Assert.That(s.GetRecentEvents(3).Any(e => e.EventType == HistoricalEventType.AgreementBroken && e.IsManual), Is.True);
            Assert.That(s.GetRelations().Count, Is.EqualTo(10), "No second record for the same pair.");
        }

        [Test]
        public void ClaimCommands_FileRecognizeContestWithdraw_AndNeverTransferGovernance()
        {
            var s = Phase2Worlds.Session(_dir);
            var house = Of(s, FactionType.NobleHouse, governing: false);
            var town = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Town);
            var governor = s.GetSettlementPolitics(town.SettlementId).GoverningFactionId;

            Assert.That(s.Dispatch(new FileTerritorialClaimCommand(house.FactionId, town.SettlementId)).IsSuccess, Is.True);
            Assert.That(s.Dispatch(new FileTerritorialClaimCommand(house.FactionId, town.SettlementId)).Status, Is.EqualTo(CommandStatus.ValidationFailed), "No duplicate claim.");
            var claim = s.GetClaims(town.SettlementId, openOnly: true).Single();
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Unresolved));
            Assert.That(claim.IsManual, Is.True);
            Assert.That(s.GetRecentEvents(1)[0].EventType, Is.EqualTo(HistoricalEventType.TerritorialClaimFiled));
            Assert.That(s.GetRecentEvents(1)[0].IsManual, Is.True);

            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Contested)).IsSuccess, Is.True);
            Assert.That(s.GetSettlementPolitics(town.SettlementId).IsContested, Is.True);
            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Contested)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Fulfilled)).Status, Is.EqualTo(CommandStatus.ValidationFailed), "Only governing fulfils a claim.");
            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Recognized)).IsSuccess, Is.True);
            Assert.That(s.GetSettlementPolitics(town.SettlementId).GoverningFactionId, Is.EqualTo(governor), "Recognized, and the town is governed exactly as before.");

            s.Execute(new AdvanceDaysCommand(60));
            Assert.That(s.GetSettlementPolitics(town.SettlementId).GoverningFactionId, Is.EqualTo(governor));
            Assert.That(s.GetClaims(town.SettlementId).Single().Status, Is.EqualTo(ClaimStatus.Recognized), "The simulation respects the player's decision.");

            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Relinquished)).IsSuccess, Is.True);
            Assert.That(s.GetClaims(town.SettlementId, openOnly: true), Is.Empty);
            Assert.That(s.GetClaims(town.SettlementId).Count, Is.EqualTo(1), "A closed claim stays as history.");
            Assert.That(s.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Recognized)).Status, Is.EqualTo(CommandStatus.InvalidSessionState));
        }

        [Test]
        public void GovernanceCommand_ChangesAuthority_CreatesPresence_FulfilsClaims_AndIsChronicledAsMajor()
        {
            var s = Phase2Worlds.Session(_dir);
            var kingdom = Of(s, FactionType.Kingdom);
            var house = Of(s, FactionType.NobleHouse, governing: false);
            var hamlet = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Hamlet);
            var oldGovernor = s.GetSettlementPolitics(hamlet.SettlementId).GoverningFactionId;
            var economyBefore = string.Join(",", hamlet.Economy.Select(r => r.Stock));
            Assert.That(s.Dispatch(new FileTerritorialClaimCommand(kingdom.FactionId, hamlet.SettlementId)).IsSuccess, Is.True);
            Assert.That(s.FindFaction(house.FactionId).Presence.Any(p => p.SettlementId == hamlet.SettlementId), Is.False);

            var result = s.Dispatch(new SetSettlementGovernanceCommand(hamlet.SettlementId, kingdom.FactionId, house.FactionId));

            Assert.That(result.IsSuccess, Is.True, result.ToString());
            var politics = s.GetSettlementPolitics(hamlet.SettlementId);
            Assert.That(politics.GoverningFactionId, Is.EqualTo(kingdom.FactionId));
            Assert.That(politics.AdministratorFactionId, Is.EqualTo(house.FactionId));
            Assert.That(politics.Presence.Single(p => p.FactionId == house.FactionId).IsAdministrator, Is.True, "The administrator gained an organisation on the ground.");
            Assert.That(politics.Presence.Single(p => p.FactionId == kingdom.FactionId).IsGovernor, Is.True);
            Assert.That(s.GetClaims(hamlet.SettlementId).Single().Status, Is.EqualTo(ClaimStatus.Fulfilled));
            Assert.That(s.FindSettlement(hamlet.SettlementId).FactionName, Is.EqualTo(kingdom.Name), "The settlement inspector's faction follows.");
            Assert.That(string.Join(",", s.FindSettlement(hamlet.SettlementId).Economy.Select(r => r.Stock)), Is.EqualTo(economyBefore), "The economy is untouched.");
            var e = s.GetRecentEvents(1)[0];
            Assert.That(e.EventType, Is.EqualTo(HistoricalEventType.GovernanceChanged));
            Assert.That(e.Importance, Is.EqualTo(EventImportance.Major));
            Assert.That(e.IsManual, Is.True);
            Assert.That(e.FactionIds, Has.Member(oldGovernor).And.Member(kingdom.FactionId));

            // Independence: no governor, so no administrator either.
            Assert.That(s.Dispatch(new SetSettlementGovernanceCommand(hamlet.SettlementId, default)).IsSuccess, Is.True);
            Assert.That(s.GetSettlementPolitics(hamlet.SettlementId).IsIndependent, Is.True);
            Assert.That(s.FindSettlement(hamlet.SettlementId).FactionName, Is.Null);
            Assert.That(s.Execute(new AdvanceDaysCommand(30)).DaysAdvanced, Is.EqualTo(30), "The world stays valid and running.");
        }

        [Test]
        public void PresenceCommands_EstablishOnce_AndWithdraw()
        {
            var s = Phase2Worlds.Session(_dir);
            var guild = s.GetFactions().Single(f => f.Type == FactionType.Guild);
            var hamlet = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Hamlet);

            Assert.That(s.Dispatch(new EstablishFactionPresenceCommand(guild.FactionId, hamlet.SettlementId, 35)).IsSuccess, Is.True);
            var presence = s.GetSettlementPolitics(hamlet.SettlementId).Presence.Single(p => p.FactionId == guild.FactionId);
            Assert.That(presence.Strength, Is.EqualTo(35));
            Assert.That(presence.Members, Is.EqualTo(0), "Members come with time, not with the decree.");
            Assert.That(presence.Status, Is.EqualTo(PresenceStatus.Established));
            Assert.That(s.Dispatch(new EstablishFactionPresenceCommand(guild.FactionId, hamlet.SettlementId, 35)).Status, Is.EqualTo(CommandStatus.ValidationFailed));

            s.Execute(new AdvanceDaysCommand(28));
            var grown = s.GetSettlementPolitics(hamlet.SettlementId);
            Assert.That(grown.Presence.Single(p => p.FactionId == guild.FactionId).Members, Is.GreaterThan(0), "The branch recruits.");
            Assert.That(grown.AffiliatedPopulation, Is.LessThanOrEqualTo(grown.Population));

            Assert.That(s.Dispatch(new RemoveFactionPresenceCommand(guild.FactionId, hamlet.SettlementId)).IsSuccess, Is.True);
            Assert.That(s.GetSettlementPolitics(hamlet.SettlementId).Presence.Any(p => p.FactionId == guild.FactionId), Is.False);
            Assert.That(s.GetRecentEvents(1)[0].EventType, Is.EqualTo(HistoricalEventType.FactionPresenceWithdrawn));
        }

        [Test]
        public void GoalCommands_Create_Reprioritize_Abandon_WithTheReasonRecorded()
        {
            var s = Phase2Worlds.Session(_dir);
            var guild = s.GetFactions().Single(f => f.Type == FactionType.Guild);

            Assert.That(s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.GainInfluence, guild.Influence, 5)).IsSuccess, Is.True);
            var goal = s.FindFaction(guild.FactionId).Goals.Single(g => g.IsManual);
            Assert.That(goal.Status, Is.EqualTo(GoalStatus.Active), "Already met, but completion is decided by the simulation, on a tick.");
            Assert.That(goal.Progress, Is.EqualTo(99));
            Assert.That(s.FindFaction(guild.FactionId).PrimaryGoal.GoalId, Is.EqualTo(goal.GoalId), "Priority 5 makes it the primary goal.");
            Assert.That(goal.Description, Is.EqualTo($"Reach influence {guild.Influence}"));

            Assert.That(s.Dispatch(new SetFactionGoalPriorityCommand(guild.FactionId, goal.GoalId, 1)).IsSuccess, Is.True);
            Assert.That(s.FindFaction(guild.FactionId).PrimaryGoal.GoalId, Is.Not.EqualTo(goal.GoalId));

            var second = s.Dispatch(new CreateFactionGoalCommand(guild.FactionId, GoalType.ImproveRelationship, 80, 2, default, Of(s, FactionType.Kingdom).FactionId));
            Assert.That(second.IsSuccess, Is.True, second.ToString());
            var toAbandon = s.FindFaction(guild.FactionId).Goals.Single(g => g.Type == GoalType.ImproveRelationship);
            Assert.That(s.Dispatch(new AbandonFactionGoalCommand(guild.FactionId, toAbandon.GoalId)).IsSuccess, Is.True);
            var abandoned = s.FindFaction(guild.FactionId).Goals.Single(g => g.GoalId == toAbandon.GoalId);
            Assert.That(abandoned.Status, Is.EqualTo(GoalStatus.Abandoned));
            Assert.That(abandoned.CloseReason, Is.EqualTo(GoalCloseReason.AbandonedByPlayer));
            Assert.That(abandoned.ClosedOn, Is.EqualTo(s.GetOverview().Date));
            Assert.That(s.Dispatch(new AbandonFactionGoalCommand(guild.FactionId, toAbandon.GoalId)).Status, Is.EqualTo(CommandStatus.InvalidSessionState), "Not twice.");
            Assert.That(s.GetRecentEvents(1)[0].IsManual, Is.True);

            s.Execute(new AdvanceDaysCommand(7));
            var done = s.FindFaction(guild.FactionId).Goals.Single(g => g.GoalId == goal.GoalId);
            Assert.That(done.Status, Is.EqualTo(GoalStatus.Completed), "The first tick confirms the goal that was already met.");
            Assert.That(s.GetRecentEvents(20).Single(e => e.EventType == HistoricalEventType.FactionGoalCompleted).IsManual, Is.False, "The completion is the simulation's event.");
        }

        // ----- Queries and integration ----------------------------------------------------------------

        [Test]
        public void FactionSummary_ExplainsInfluence_WithTermsThatAddUp()
        {
            var s = Phase2Worlds.Session(_dir);
            s.Execute(new AdvanceDaysCommand(45));
            foreach (var f in s.GetFactions())
            {
                Assert.That(f.InfluenceContributions.Sum(p => p.Weight), Is.EqualTo(100), f.Name);
                Assert.That(f.InfluenceContributions.Sum(p => p.Hundredths) / 100, Is.EqualTo(f.InfluenceTarget), f.Name + ": the explanation is the calculation.");
                Assert.That(f.Influence, Is.InRange(0, 100));
                Assert.That(Math.Abs(f.Influence - f.InfluenceTarget), Is.LessThanOrEqualTo(12), f.Name + ": influence tracks its target.");
                Assert.That(f.TotalMembers, Is.EqualTo(f.Presence.Sum(p => p.Members)));
                Assert.That(f.HasLeader, Is.EqualTo(f.LeaderName != null));
                foreach (var r in f.Relations) Assert.That(r.Terms.Sum(t => t.Value), Is.EqualTo(r.OpinionTarget).Or.Not.InRange(-100, 100));
            }

            var kingdom = Of(s, FactionType.Kingdom);
            Assert.That(kingdom.HoldingNames.Count, Is.EqualTo(2));
            Assert.That(kingdom.PrimaryGoal.Type, Is.EqualTo(GoalType.MaintainControl));
            Assert.That(Of(s, FactionType.NobleHouse, governing: false).HoldingNames.Single(), Does.EndWith("(administers)"));
        }

        [Test]
        public void Hardship_ErodesSupportForTheGoverningFaction()
        {
            // Two copies of one world; in one the town's granary is emptied (through its save file).
            var calm = Phase2Worlds.Session(_dir);
            calm.Execute(new AdvanceDaysCommand(2));
            Assert.That(calm.Dispatch(new SaveWorldCommand("calm")).IsSuccess, Is.True);
            var path = Path.Combine(_dir, "calm" + JsonWorldStore.Extension);
            var doc = JObject.Parse(File.ReadAllText(path));
            var townJson = (JObject)((JArray)doc["world"]["settlements"]).First(t => (string)t["settlementType"] == "Town");
            foreach (JObject stack in (JArray)townJson["resources"])
            {
                if ((string)stack["resourceTypeId"] == "food") stack["quantity"] = 0;
            }

            foreach (JObject p in (JArray)townJson["economy"]["production"])
            {
                if ((string)p["sourceType"] != "Farming" && (string)p["sourceType"] != "Fishing" && (string)p["sourceType"] != "Hunting") continue;
                p["workerCapacity"] = 0; // and its food sources fail
                p["workers"] = 0;
            }

            File.WriteAllText(Path.Combine(_dir, "famine" + JsonWorldStore.Extension), doc.ToString());
            var famine = Phase2Worlds.Session(_dir, 4);
            var loadedFamine = famine.Execute(new LoadWorldCommand("famine"));
            Assert.That(loadedFamine.IsSuccess, Is.True, loadedFamine.ToString());

            calm.Execute(new AdvanceDaysCommand(120));
            famine.Execute(new AdvanceDaysCommand(120));

            var town = calm.GetSettlements().Single(x => x.SettlementType == SettlementType.Town).SettlementId;
            var kingdom = Of(calm, FactionType.Kingdom).FactionId;
            Assert.That(famine.FindSettlement(town).Stability, Is.LessThan(calm.FindSettlement(town).Stability), "Fixture: the famine hurt stability.");
            int Share(WorldSession s) => s.GetSettlementPolitics(town).Presence.Single(p => p.FactionId == kingdom).SharePerMille;
            Assert.That(Share(famine), Is.LessThan(Share(calm)), "Support for the crown follows the stability of the town it governs.");
            Assert.That(famine.FindFaction(kingdom).InfluenceContributions.Single(p => p.Factor == InfluenceFactor.Authority).Score,
                Is.LessThan(calm.FindFaction(kingdom).InfluenceContributions.Single(p => p.Factor == InfluenceFactor.Authority).Score));
            Assert.That(famine.FindFaction(kingdom).InfluenceTarget, Is.LessThan(calm.FindFaction(kingdom).InfluenceTarget));
        }

        [Test]
        public void EnteringAndLeavingASettlement_ChangesNothingPolitical()
        {
            var s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(s);
            s.Execute(new AdvanceDaysCommand(20));
            var town = s.GetSettlements().Single(x => x.SettlementType == SettlementType.Town);
            var before = Snapshot(s);

            Assert.That(s.Dispatch(new EnterSettlementCommand(town.SettlementId)).IsSuccess, Is.True);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.GetSettlementPolitics(town.SettlementId).GoverningFactionName, Is.EqualTo(town.FactionName), "Exploration reads the same political data.");
            Assert.That(s.Dispatch(new ExitSettlementCommand()).IsSuccess, Is.True);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.GetFactions().Count, Is.EqualTo(5));
        }

        [Test]
        public void PausedPlayback_DoesNotAdvancePolitics()
        {
            var s = Phase2Worlds.Session(_dir);
            var playback = new SimulationPlayback(s);
            var before = Snapshot(s);
            playback.Tick(10000);
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [Test]
        public void AYearOfPolitics_IsValid_Meaningful_AndNotSpam()
        {
            var s = Phase2Worlds.Session(_dir);
            Assert.That(s.Execute(new AdvanceDaysCommand(360)).DaysAdvanced, Is.EqualTo(360));
            Assert.That(s.Dispatch(new SaveWorldCommand("year")).IsSuccess, Is.True);
            var world = new JsonWorldSerializer().Deserialize(File.ReadAllText(Path.Combine(_dir, "year" + JsonWorldStore.Extension))).World;
            Assert.That(WorldValidator.Validate(world).Errors, Is.Empty);

            var political = world.HistoricalEvents.Where(e => e.FactionIds.Count > 0).ToList();
            var factionIds = new HashSet<FactionId>(world.Factions.Select(f => f.FactionId));
            var settlementIds = new HashSet<SettlementId>(world.Settlements.Select(x => x.SettlementId));
            foreach (var e in political)
            {
                Assert.That(e.FactionIds.All(factionIds.Contains), Is.True, e.EventType.ToString());
                Assert.That(e.SettlementIds.All(settlementIds.Contains), Is.True, e.EventType.ToString());
                Assert.That(e.Get("source"), Is.EqualTo(PoliticalSystem.SimulationSource), "Nothing here was done by a player.");
                Assert.That(e.Date.ToDayNumber() % 7, Is.EqualTo(0), "Political events happen on tick days.");
                Assert.That(EventFormatter.Format(e, id => world.FindSettlement(id)?.Name, id => world.FindFaction(id)?.Name), Does.Not.Contain("a faction").And.Not.Contain("a settlement"));
            }

            foreach (var line in political.GroupBy(e => e.EventType).Select(g => $"{g.Key}: {g.Count()}")) TestContext.WriteLine(line);
            TestContext.WriteLine("Factions after a year: " + string.Join("; ", world.Factions.Select(f => $"{f.Name} [{f.Type}, {f.Status}] influence {f.Influence}, members {f.TotalMembers}, coin {f.Treasury}")));
            TestContext.WriteLine("Claims: " + string.Join("; ", world.Claims.Select(c => $"{world.FindFaction(c.ClaimantId).Name} on {world.FindSettlement(c.SettlementId).Name}: {c.Status}")));

            Assert.That(political.Count, Is.GreaterThan(0), "Something political happened in a year.");
            Assert.That(political.Count, Is.LessThanOrEqualTo(51 * 2), "Far fewer events than two per tick: milestones, not a ticker.");
            foreach (var group in political.GroupBy(e => (e.EventType, e.FactionIds[0], e.SettlementId)))
                Assert.That(group.Count(), Is.LessThanOrEqualTo(8), $"{group.Key.EventType} repeats too often for one faction.");
            foreach (var x in world.Settlements)
                Assert.That(world.Factions.Sum(f => f.FindPresence(x.SettlementId)?.Members ?? 0), Is.LessThanOrEqualTo(x.Population), x.Name);
            Assert.That(world.Claims.Any(), Is.True, "The crown pursued its claim on the hamlet.");
            Assert.That(world.Settlements.Count(x => x.FactionId == world.Factions.Single(f => f.Type == FactionType.Kingdom).FactionId), Is.EqualTo(2), "And that claim moved no border.");
        }

        // ----- Performance ----------------------------------------------------------------------------

        [Test]
        public void Performance_IsMeasured_ForTheDefaultWorldAndALargerSyntheticOne()
        {
            // Default world: a 30-day run and a single tick day.
            var world = Phase2Worlds.Generated(123456789);
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            world = scheduler.AdvanceDays(world, 14).World; // warm up
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            world = scheduler.AdvanceDays(world, 30).World;
            timer.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            TestContext.WriteLine($"Default world (3 settlements, 5 factions, 10 relations): 30 days in {timer.Elapsed.TotalMilliseconds:0.0} ms, {allocated / 30 / 1024} KB allocated per day.");

            var system = new PoliticalSystem(new PoliticalRules());
            var tickOnly = TimeTick(world, system, 200);
            TestContext.WriteLine($"Default world: one political tick takes {tickOnly * 1000:0.0} µs (mean of 200).");

            // Synthetic: 30 settlements, 120 factions, every pair related (7,140 relations), 3 presences each.
            var big = Synthetic(30, 120);
            Assert.That(WorldValidator.Validate(big).Errors, Is.Empty);
            var bigTick = TimeTick(big, system, 5);
            var bigScheduler = PoliticalFixture.Scheduler();
            timer.Restart();
            var bigRun = bigScheduler.AdvanceDays(big, 30);
            timer.Stop();
            Assert.That(bigRun.Completed, Is.True, string.Join("; ", bigRun.Errors.Take(3)));
            TestContext.WriteLine($"Synthetic world (30 settlements, 120 factions, {big.Relations.Count} relations): one political tick takes {bigTick:0.0} ms; " +
                                  $"30 days (politics only, with the daily copy and validation) in {timer.Elapsed.TotalMilliseconds:0} ms.");

            Assert.That(tickOnly, Is.LessThan(50), "Generous bound for a default-world tick, in milliseconds.");
            Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(120), "Generous bound: catches a pathological regression only.");
        }

        /// <summary>Mean milliseconds of one political tick (the system alone, on a copy positioned on a tick day).</summary>
        private static double TimeTick(WorldData world, PoliticalSystem system, int repeats)
        {
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(system);
            var copy = world.DeepClone();
            copy.WorldSettings.EnabledSimulationSystems.Clear();
            copy.WorldSettings.EnabledSimulationSystems.Add(PoliticalSystem.Id);
            while ((copy.CurrentSimulationDate.ToDayNumber() + 1) % 7 != 0) copy.CurrentSimulationDate = copy.CurrentSimulationDate.AddDays(1);

            // Baseline: the same day with no systems (copy + validation), subtracted from the tick day.
            var empty = new SimulationScheduler(new Xoshiro256RandomFactory());
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < repeats; i++) empty.AdvanceDay(copy);
            var baseline = timer.Elapsed.TotalMilliseconds / repeats;
            timer.Restart();
            for (var i = 0; i < repeats; i++)
            {
                var step = scheduler.AdvanceDay(copy);
                if (!step.Committed) Assert.Fail(string.Join("; ", step.Errors.Take(3)));
            }

            return Math.Max(0, timer.Elapsed.TotalMilliseconds / repeats - baseline);
        }

        private static WorldData Synthetic(int settlements, int factions)
        {
            var world = TestWorlds.SingleSettlement(2000, 100000, 0, PoliticalSystem.Id);
            world.Settlements[0].Population = 2000;
            for (var i = 1; i < settlements; i++)
            {
                var s = TestWorlds.MakeSettlement("Town " + i, $"8d4f8f8e-6a43-4c1b-9d55-0d1f6f5a{i:0000}", 2000, 100000, 0);
                s.Position = new MapCoord(i % 60, i / 60 + 1);
                world.Settlements.Add(s);
            }

            for (var i = 0; i < factions; i++)
            {
                var type = FactionRules.AllTypes[i % FactionRules.AllTypes.Count];
                var f = new FactionData
                {
                    FactionId = new FactionId(new Guid($"8d4f8f8e-6a43-4c1b-9d55-0d1f6f6a{i:0000}")), Name = "Faction " + i, Type = type,
                    FoundedOn = TestWorlds.Start, Influence = 30 + i % 40, InfluenceTarget = 30, LastReportedInfluence = 30 + i % 40, Treasury = 100 * (i % 9),
                };
                for (var k = 0; k < 3; k++)
                {
                    var s = world.Settlements[(i * 7 + k * 11) % settlements];
                    if (f.FindPresence(s.SettlementId) == null) PoliticalFixture.Present(f, s, 20 + (i + k) % 60, 5 + i % 12);
                }

                world.Factions.Add(f);
            }

            for (var i = 0; i < settlements && i < factions; i++)
            {
                var governor = world.Factions.First(f => FactionRules.CanGovern(f.Type) && f.FindPresence(world.Settlements[i].SettlementId) != null || f.Type == FactionType.Kingdom);
                world.Settlements[i].FactionId = governor.FactionId;
            }

            for (var i = 0; i < factions; i++)
            {
                for (var j = i + 1; j < factions; j++)
                {
                    var (a, b) = PoliticalMath.OrderPair(world.Factions[i].FactionId, world.Factions[j].FactionId);
                    world.Relations.Add(new FactionRelationData { FactionA = a, FactionB = b, Trust = 50, Opinion = (i * 13 + j * 7) % 121 - 60 });
                }
            }

            foreach (var r in world.Relations) r.LastReportedState = PoliticalMath.StateOf(r);
            return world;
        }
    }
}
