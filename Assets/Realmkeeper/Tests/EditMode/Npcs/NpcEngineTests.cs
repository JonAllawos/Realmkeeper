using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Npcs
{
    /// <summary>The NPC Intelligence Engine's domain and simulation rules, on generated worlds.</summary>
    public sealed class NpcEngineTests
    {
        private static SimulationScheduler Scheduler() => StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());

        private static WorldData Advance(WorldData world, int days)
        {
            var result = Scheduler().AdvanceDays(world, days);
            Assert.That(result.Completed, Is.True, string.Join("; ", result.Errors));
            return result.World;
        }

        private static string NpcState(WorldData w) =>
            string.Join("\n", Newtonsoft.Json.Linq.JObject.Parse(new JsonWorldSerializer().Serialize(w))["world"]
                .Where(t => true).Cast<Newtonsoft.Json.Linq.JProperty>()
                .Where(p => p.Name == "npcs" || p.Name == "npcRelations" || p.Name == "locations").Select(p => p.ToString()));

        // ----- Generation -----------------------------------------------------------------------------

        [Test]
        public void FreshWorld_InitialisesEveryNamedPerson_FromPresentFactsOnly()
        {
            var w = Phase2Worlds.Generated(4242);
            Assert.That(w.Npcs.Count, Is.EqualTo(15));
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(w).Errors));
            var leaders = new HashSet<NpcId>(w.Factions.Where(f => !f.LeaderNpcId.IsEmpty).Select(f => f.LeaderNpcId));
            foreach (var n in w.Npcs)
            {
                Assert.That(n.Tier, Is.Not.EqualTo(NpcTier.Unassigned), n.Name);
                if (leaders.Contains(n.NpcId)) Assert.That(n.Tier, Is.EqualTo(NpcTier.Notable), "Faction leaders are simulated every day.");
                for (var t = 0; t < NpcPersonality.Count; t++) Assert.That(n.Personality[(NpcTrait)t], Is.InRange(0, 100));
                Assert.That(n.Skills[NpcGenesis.PrimarySkill(n.Role)], Is.GreaterThanOrEqualTo(45), "Their work's skill is their strongest.");
                foreach (var id in new[] { n.ResidenceId, n.WorkplaceId, n.CurrentLocationId })
                    Assert.That(w.FindLocation(id)?.SettlementId, Is.EqualTo(n.HomeSettlementId), n.Name + " lives and works at home.");
                for (var d = 0; d < 7; d++) Assert.That(w.FindLocation(n.Schedule[d]), Is.Not.Null);
                Assert.That(n.Goals.Any(g => g.IsActive), Is.True);
                Assert.That(n.Memories, Is.Empty, "No invented history.");
                Assert.That(n.Knowledge.Any(k => k.Topic == KnowledgeTopic.SettlementFood && k.SubjectId == n.HomeSettlementId.ToString() && k.Status == KnowledgeStatus.Known),
                    Is.True, "They know their own settlement first-hand.");
                Assert.That(n.Knowledge.All(k => k.Hops == 0), Is.True, "Nothing second-hand yet.");
                Assert.That(n.BirthDate < w.CurrentSimulationDate && n.AgeOn(w.CurrentSimulationDate) >= 18, Is.True);
            }

            var possiblePairs = w.Npcs.Count * (w.Npcs.Count - 1) / 2;
            Assert.That(w.NpcRelations.Count, Is.LessThan(possiblePairs / 2), "Relationships are not all-pairs.");
            Assert.That(w.NpcRelations.Select(r => r.Origin).Distinct(),
                Is.SubsetOf(new[] { NpcRelationCause.SharedHousehold, NpcRelationCause.SharedWorkplace, NpcRelationCause.FactionHierarchy }),
                "At start, only ties the present facts imply.");
        }

        [Test]
        public void Locations_AreOnSitesTheLocalLayoutBuilds()
        {
            var w = Phase2Worlds.Generated(4242);
            foreach (var s in w.Settlements)
            {
                var layout = LocalEnvironmentGenerator.Generate(w.Seed, s.SettlementId, s.RegionId, s.SettlementType, Array.Empty<ResidentInput>());
                var sites = new HashSet<string>(layout.Buildings.Select(b => b.Key)) { SettlementSites.Plaza, SettlementSites.Gate };
                var mine = w.Locations.Where(l => l.SettlementId == s.SettlementId).ToList();
                Assert.That(mine.Select(l => l.Kind), Has.Member(LocationKind.Tavern).And.Member(LocationKind.Market).And.Member(LocationKind.Square));
                foreach (var l in mine) Assert.That(sites, Has.Member(l.LayoutKey), $"{s.Name}: {l.Name} is on a site the layout builds.");
                Assert.That(mine.Count(l => l.Kind == LocationKind.Residence), Is.GreaterThan(0));
            }
        }

        [Test]
        public void SameSeed_GivesTheSamePeople_AndTheSameMonth()
        {
            var a = Advance(Phase2Worlds.Generated(99), 30);
            var b = Advance(Phase2Worlds.Generated(99), 30);
            Assert.That(NpcState(b), Is.EqualTo(NpcState(a)));
            var c = Advance(Phase2Worlds.Generated(100), 30);
            Assert.That(NpcState(c), Is.Not.EqualTo(NpcState(a)), "Another seed, other people.");
        }

        [Test]
        public void Tier1_IsUpdatedDaily_Tier2_OnceAWeek_OnTheirOwnDay()
        {
            var w = Phase2Worlds.Generated(7);
            var notable = w.Npcs.First(n => n.Tier == NpcTier.Notable).NpcId;
            var resident = w.Npcs.First(n => n.Tier == NpcTier.Resident).NpcId;
            var updates = new Dictionary<NpcId, int> { [notable] = 0, [resident] = 0 };
            for (var day = 0; day < 14; day++)
            {
                w = Advance(w, 1);
                foreach (var id in updates.Keys.ToList())
                    if (w.FindNpc(id).LastUpdatedOn == w.CurrentSimulationDate) updates[id]++;
            }

            Assert.That(updates[notable], Is.EqualTo(14));
            Assert.That(updates[resident], Is.EqualTo(2), "Tier 2: once a week.");
        }

        [Test]
        public void Tier3_IsTheRestOfThePopulation_AndNamedPeopleDieBeforeTheyCanBeCountedTwice()
        {
            // Six people, all of them named; the granary is empty and starvation begins tomorrow.
            var w = TestWorlds.SingleSettlement(6, 0, 0, StandardSimulationSystems.AllIds.ToArray());
            var hamlet = w.Settlements[0];
            for (var i = 0; i < 6; i++)
                w.Npcs.Add(new NpcData { NpcId = new NpcId(new Guid(i + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), Name = "Person " + i, HomeSettlementId = hamlet.SettlementId, Role = NpcRole.Labourer });
            NpcGenesis.EnsurePeople(w);
            hamlet.Economy.DaysWithoutFood = new Realmkeeper.Simulation.Systems.EconomyRules().Food.StarvationGraceDays;
            w = Advance(w, 1);
            hamlet = w.FindSettlement(hamlet.SettlementId);
            var living = w.Npcs.Where(n => n.HomeSettlementId == hamlet.SettlementId && n.IsAlive).ToList();
            Assert.That(living.Count, Is.LessThanOrEqualTo(hamlet.Population), "Named people are members of the population, never extra.");
            var dead = w.Npcs.Where(n => n.HomeSettlementId == hamlet.SettlementId && n.IsDeceased).ToList();
            Assert.That(dead.Count, Is.GreaterThanOrEqualTo(1), "Starvation took someone, and the named count followed.");
            Assert.That(dead.All(d => d.DiedOn == w.CurrentSimulationDate && d.CurrentLocationId.IsEmpty), Is.True);
            Assert.That(w.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.NpcDied), Is.EqualTo(dead.Count));
            Assert.That(w.HistoricalEvents.Where(e => e.EventType == HistoricalEventType.NpcDied).All(e => e.NpcIds.Count == 1), Is.True);
            foreach (var f in w.Factions.Where(f => !f.LeaderNpcId.IsEmpty))
                Assert.That(w.FindNpc(f.LeaderNpcId).IsAlive, Is.True, "No faction is led by the dead.");
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True);
        }

        [Test]
        public void PeopleGoWhereTheirRoutineSays_UnlessANeedOverridesIt()
        {
            var w = Advance(Phase2Worlds.Generated(21), 3);
            foreach (var n in w.Npcs.Where(n => n.IsAlive && n.LastUpdatedOn == w.CurrentSimulationDate))
                Assert.That(n.CurrentLocationId, Is.EqualTo(NpcSystem.PlaceFor(w, n, w.CurrentSimulationDate)));

            var someone = w.Npcs.First(n => n.IsAlive && n.Tier == NpcTier.Notable);
            someone.Condition.Health = 20; // ill: they should stay home
            w = Advance(w, 1);
            someone = w.FindNpc(someone.NpcId);
            Assert.That(someone.Goals.Any(g => g.IsActive && g.Type == NpcGoalType.Recover), Is.True);
            Assert.That(someone.CurrentLocationId, Is.EqualTo(someone.ResidenceId), "A pressing need overrides the routine.");
        }

        // ----- Knowledge: no omniscience ----------------------------------------------------------------

        [Test]
        public void NobodyKnowsAnotherSettlement_UntilTold_AndHearsayIsMarkedAsSuch()
        {
            var w = Phase2Worlds.Generated(31);
            var other = w.Settlements[1].SettlementId.ToString();
            var stranger = w.Npcs.First(n => n.HomeSettlementId != w.Settlements[1].SettlementId);
            Assert.That(NpcMind.Find(stranger, KnowledgeTopic.SettlementFood, other), Is.Null, "Not known without being seen or told.");

            // Tell one person; let the town talk for a month.
            var teller = w.Npcs.First(n => n.HomeSettlementId == stranger.HomeSettlementId && n.NpcId != stranger.NpcId);
            NpcMind.Learn(teller, KnowledgeTopic.SettlementFood, other, "short", KnowledgeStatus.Believed, 90, w.CurrentSimulationDate, default, 0, default,
                () => new KnowledgeId(Guid.NewGuid()));
            w = Advance(w, 30);
            var heard = w.Npcs.Where(n => n.HomeSettlementId == stranger.HomeSettlementId && n.NpcId != teller.NpcId)
                .Select(n => NpcMind.Find(n, KnowledgeTopic.SettlementFood, other)).Where(k => k != null).ToList();
            Assert.That(heard, Is.Not.Empty, "Facts travel by conversation.");
            Assert.That(heard.All(k => k.Hops >= 1 && k.Status != KnowledgeStatus.Known && !k.SourceNpcId.IsEmpty), Is.True, "Second-hand, with its source.");
            Assert.That(heard.All(k => k.Confidence < 90), Is.True, "Confidence falls with each telling.");
            Assert.That(heard.All(k => k.Hops <= new NpcRules().MaxHops), Is.True);
        }

        [Test]
        public void Learn_ObservationWins_HearsayNeverOverridesWhatIsKnown_AndKnowledgeIsBounded()
        {
            var n = new NpcData { NpcId = NpcId.NewRandom(), Name = "Test" };
            var d = new WorldDate(412, 3, 1);
            Func<KnowledgeId> id = () => new KnowledgeId(Guid.NewGuid());
            NpcMind.Learn(n, KnowledgeTopic.SettlementFood, "s", "plenty", KnowledgeStatus.Known, 100, d, default, 0, default, id);
            NpcMind.Learn(n, KnowledgeTopic.SettlementFood, "s", "starving", KnowledgeStatus.Rumor, 99, d.AddDays(1), NpcId.NewRandom(), 1, default, id);
            Assert.That(NpcMind.Find(n, KnowledgeTopic.SettlementFood, "s").Value, Is.EqualTo("plenty"), "Hearsay does not override first-hand knowledge.");
            NpcMind.Learn(n, KnowledgeTopic.SettlementFood, "s", "short", KnowledgeStatus.Known, 100, d.AddDays(2), default, 0, default, id);
            Assert.That(NpcMind.Find(n, KnowledgeTopic.SettlementFood, "s").Value, Is.EqualTo("short"), "A new observation does.");
            for (var i = 0; i < NpcBounds.MaxKnowledge + 10; i++)
                NpcMind.Learn(n, KnowledgeTopic.Whereabouts, "p" + i, "x", KnowledgeStatus.Rumor, 20 + i, d, default, 1, default, id);
            Assert.That(n.Knowledge.Count, Is.EqualTo(NpcBounds.MaxKnowledge));
            Assert.That(NpcMind.Find(n, KnowledgeTopic.SettlementFood, "s"), Is.Not.Null, "The weakest, not the firmest, is dropped.");
        }

        [Test]
        public void KnownFacts_GoStale_AndRumoursExpire()
        {
            var w = Phase2Worlds.Generated(41);
            var n = w.Npcs.First(x => x.Tier == NpcTier.Resident);
            NpcMind.Learn(n, KnowledgeTopic.Whereabouts, "someone", "x", KnowledgeStatus.Known, 100, w.CurrentSimulationDate, default, 0, default, () => new KnowledgeId(Guid.NewGuid()));
            NpcMind.Learn(n, KnowledgeTopic.Whereabouts, "other", "y", KnowledgeStatus.Rumor, 60, w.CurrentSimulationDate, NpcId.NewRandom(), 1, default, () => new KnowledgeId(Guid.NewGuid()));
            w = Advance(w, 60);
            n = w.FindNpc(n.NpcId);
            Assert.That(NpcMind.Find(n, KnowledgeTopic.Whereabouts, "someone")?.Status, Is.EqualTo(KnowledgeStatus.Believed), "Unrefreshed first-hand facts become beliefs.");
            Assert.That(NpcMind.Find(n, KnowledgeTopic.Whereabouts, "other"), Is.Null, "Old rumours are forgotten.");
        }

        // ----- Memories, relationships, reactions ---------------------------------------------------------

        [Test]
        public void Memories_AreBounded_ForgettingTheLeastSalient_AndFadeWeekly()
        {
            var n = new NpcData { NpcId = NpcId.NewRandom(), Name = "Test" };
            for (var i = 0; i < NpcBounds.MaxMemories + 5; i++)
                NpcMind.Remember(n, new NpcMemoryData { MemoryId = new MemoryId(Guid.NewGuid()), Date = new WorldDate(412, 3, 1), Salience = 10 + i, Subject = "m" + i });
            Assert.That(n.Memories.Count, Is.EqualTo(NpcBounds.MaxMemories));
            Assert.That(n.Memories.Min(m => m.Salience), Is.EqualTo(15), "The five least salient were forgotten.");

            var w = Phase2Worlds.Generated(51);
            var p = w.Npcs[0];
            NpcMind.Remember(p, new NpcMemoryData { MemoryId = new MemoryId(Guid.NewGuid()), Date = w.CurrentSimulationDate, Salience = 20, Subject = "faint" });
            w = Advance(w, 21);
            Assert.That(w.FindNpc(p.NpcId).Memories.Any(m => m.Subject == "faint"), Is.False, "Low-salience memories fade away.");
        }

        [Test]
        public void Relationships_FormOnlyWithACause_AndAreCapped()
        {
            var w = Phase2Worlds.Generated(61);
            var a = w.Npcs[0];
            var others = w.Npcs.Skip(1).ToList();
            var created = 0;
            foreach (var o in others)
                if (NpcMind.Relate(w, a.NpcId, o.NpcId, NpcRelationCause.Conversation, NpcRelationKind.Acquaintance, w.CurrentSimulationDate) != null) created++;
            Assert.That(NpcMind.RelationCount(w, a.NpcId), Is.LessThanOrEqualTo(NpcBounds.MaxRelationsPerNpc));
            Assert.That(NpcMind.Relate(w, a.NpcId, a.NpcId, NpcRelationCause.Conversation, NpcRelationKind.Acquaintance, w.CurrentSimulationDate), Is.Null);
            Assert.That(w.NpcRelations.All(r => r.NpcA.CompareTo(r.NpcB) < 0), Is.True, "Stored once per pair, in ID order.");

            w = Advance(Phase2Worlds.Generated(61), 60);
            Assert.That(w.NpcRelations.Any(r => r.Origin == NpcRelationCause.Conversation), Is.True, "Conversations make new acquaintances.");
            Assert.That(w.NpcRelations.All(r => Enum.IsDefined(typeof(NpcRelationCause), r.Origin) && r.Since.IsValidDate), Is.True);
            Assert.That(WorldValidator.Validate(w).IsValid, Is.True);
        }

        [Test]
        public void AFoodShortage_IsRemembered_ByThoseWhoLiveThere_LinkedToTheEvent()
        {
            var w = Phase2Worlds.Generated(71);
            var s = w.Settlements[0];
            s.ResourceInventory.Set(Realmkeeper.Domain.Economy.ResourceTypes.Food, 0);
            HistoricalEvent shortage = null;
            for (var i = 0; i < 10 && shortage == null; i++)
            {
                w = Advance(w, 1);
                shortage = w.HistoricalEvents.FirstOrDefault(e => (e.EventType == HistoricalEventType.FoodShortageBegan || e.EventType == HistoricalEventType.FoodReservesExhausted)
                                                                  && e.SettlementId == s.SettlementId);
            }

            Assert.That(shortage, Is.Not.Null, "The economy reports the shortage.");
            var residents = w.Npcs.Where(n => n.IsAlive && n.HomeSettlementId == s.SettlementId).ToList();
            foreach (var n in residents)
            {
                var m = n.Memories.FirstOrDefault(x => x.EventId == shortage.EventId);
                Assert.That(m, Is.Not.Null, n.Name + " remembers it.");
                Assert.That(m.Category, Is.EqualTo(MemoryCategory.Hardship));
                Assert.That(m.Source, Is.EqualTo(MemorySource.Experienced));
            }

            var elsewhere = w.Npcs.Where(n => n.HomeSettlementId != s.SettlementId && n.FactionId != s.FactionId).ToList();
            Assert.That(elsewhere.All(n => n.Memories.All(m => m.EventId != shortage.EventId)), Is.True, "Not by people it did not touch.");
        }

        [Test]
        public void ChronicleStaysReadable_NoDailyNpcSpam()
        {
            var w = Advance(Phase2Worlds.Generated(81), 120);
            var npcEvents = w.HistoricalEvents.Count(e => e.EventType >= HistoricalEventType.NpcDied);
            Assert.That(npcEvents, Is.LessThan(120), $"{npcEvents} NPC events in 120 days.");
        }

        [Test]
        public void ExplorationShowsPeopleWhereTheWorldSaysTheyAre()
        {
            var w = Advance(Phase2Worlds.Generated(91), 2);
            var s = w.Settlements.First(x => x.SettlementType == SettlementType.Town);
            var layout = Realmkeeper.Exploration.Application.ExplorationModule.LayoutFor(w, s);
            var present = w.Npcs.Where(n => n.IsAlive && w.FindLocation(n.CurrentLocationId)?.SettlementId == s.SettlementId).ToList();
            Assert.That(layout.Residents.Select(r => r.NpcId), Is.EquivalentTo(present.Select(n => n.NpcId)));
            foreach (var spot in layout.Residents)
                Assert.That(spot.SiteKey, Is.EqualTo(w.FindLocation(w.FindNpc(spot.NpcId).CurrentLocationId).LayoutKey));
        }
    }
}
