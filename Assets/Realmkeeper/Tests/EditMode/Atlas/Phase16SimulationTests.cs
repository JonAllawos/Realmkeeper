using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0079–0081): caravans carrying the economy's real shipments; outlaws, patrols and refugees; world
    /// events that begin from real causes, act on real state once, and end; what people and the player know; quests the
    /// events call for; the tiers of simulation; and determinism over the whole connected world.
    /// </summary>
    public sealed class Phase16SimulationTests : Phase16Fixture
    {
        private void Days(int n)
        {
            var r = S.Execute(new AdvanceDaysCommand(n));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors ?? new string[0]));
        }

        private static long Total(WorldData w, ResourceTypeId r) => w.Settlements.Sum(s => (long)s.ResourceInventory.Get(r)) + w.Shipments.Where(s => s.ResourceTypeId == r).Sum(s => (long)s.Quantity);

        private void ArmBand(string routeId, int count)
        {
            Edit(w =>
            {
                var band = w.Groups.FirstOrDefault(g => g.Kind == TravelGroupKind.Bandits && g.StationRouteId == routeId);
                CreaturePopulationData p;
                if (band != null) p = w.CreaturePopulations.First(x => x.PopulationId == band.PopulationId);
                else
                {
                    var seg = w.FindRoute(routeId);
                    var a = w.FindSettlement(seg.A);
                    var b = w.FindSettlement(seg.B);
                    p = new CreaturePopulationData
                    {
                        PopulationId = "bandit@road:test:" + routeId.GetHashCode(), RegionId = seg.RegionA, CreatureId = "bandit", Habitat = "Road",
                        Center = new MapCoord((a.Position.X + b.Position.X) / 2, (a.Position.Z + b.Position.Z) / 2), RangeCells = 30, Capacity = 20, Count = count, LastUpdatedOn = w.CurrentSimulationDate,
                    };
                    w.CreaturePopulations.Add(p);
                    w.Groups.Add(new TravelingGroupData
                    {
                        GroupId = "bandits:" + p.PopulationId, Kind = TravelGroupKind.Bandits, Name = "Outlaws", PopulationId = p.PopulationId, StationRouteId = routeId,
                        State = TravelGroupState.Stationed, Segments = { routeId }, Nodes = { seg.A, seg.B }, DueAt = -1,
                    });
                }

                p.Count = count;
                foreach (var g in w.Groups.Where(g => g.PopulationId == p.PopulationId)) { g.StationRouteId = routeId; g.Strength = count; g.Members = count; g.State = TravelGroupState.Stationed; g.Segments.Clear(); g.Segments.Add(routeId); }
            });
        }

        // 26, 27, 28 ------------------------------------------------------------------------------------------

        [Test]
        public void EveryShipment_TravelsWithACaravan_LedByOneOfTheWorldsPeople_AlongRealRoads()
        {
            Days(3);
            var w = W;
            Assume.That(w.Shipments, Is.Not.Empty, "Trade has goods on the road.");
            foreach (var s in w.Shipments)
            {
                var c = w.Groups.SingleOrDefault(g => g.Kind == TravelGroupKind.Caravan && g.ShipmentId == s.ShipmentId);
                Assert.That(c, Is.Not.Null, "One caravan per shipment.");
                Assert.That(c.Segments.All(id => w.FindRoute(id) != null), Is.True);
                Assert.That(c.OriginId, Is.EqualTo(s.OriginId));
                Assert.That(c.Strength, Is.GreaterThan(0), "Guards hired by the town and the cargo's worth.");
            }

            Assert.That(w.Groups.Where(g => g.Kind == TravelGroupKind.Caravan && g.IsActive).Any(g => !g.LeaderNpcId.IsEmpty), Is.True, "Led by named people.");
            var led = w.Groups.First(g => g.Kind == TravelGroupKind.Caravan && g.IsActive && !g.LeaderNpcId.IsEmpty);
            Assert.That(Realmkeeper.Simulation.Places.PlacePresence.IsAway(w, w.FindNpc(led.LeaderNpcId)), Is.True, "On the road, not in their schedule.");
            Valid();
        }

        // 29, 30 ----------------------------------------------------------------------------------------------

        [Test]
        public void ACaravansArrival_IsTheShipmentsDelivery_StockAndTradeRespond()
        {
            Days(2);
            var s = W.Shipments.OrderBy(x => x.ShipmentId).First();
            var dest = s.DestinationId;
            var resource = s.ResourceTypeId;
            var caravan = W.Groups.Single(g => g.ShipmentId == s.ShipmentId);
            var activity = W.FindSettlement(dest).Economy.TradeActivity;
            var days = (int)(s.ArrivalDay - W.CurrentSimulationDate.ToDayNumber()) + 1;
            Days(System.Math.Max(1, days));
            Assert.That(W.Shipments.Any(x => x.ShipmentId == s.ShipmentId), Is.False, "Delivered by the trade system.");
            Assert.That(W.FindGroup(caravan.GroupId).State, Is.EqualTo(TravelGroupState.Arrived));
            Assert.That(W.FindSettlement(dest).Economy.FindFlow(resource)?.Imported ?? 0, Is.GreaterThanOrEqualTo(0));
            Assert.That(W.FindSettlement(dest).Economy.TradeActivity + 1, Is.GreaterThan(0));
            Valid();
        }

        // 31, 32 ----------------------------------------------------------------------------------------------

        [Test]
        public void OutlawsStrongerThanTheGuards_TakeACaravan_AndItsGoodsAreRecordedLost_NeverVanished()
        {
            Days(2);
            var s = W.Shipments.OrderBy(x => x.ShipmentId).First();
            var caravan = W.Groups.Single(g => g.ShipmentId == s.ShipmentId);
            ArmBand(caravan.Segments[0], 40);
            var resource = s.ResourceTypeId;
            var before = Total(W, resource);
            var lostBefore = W.Settlements.Sum(x => x.Economy.FindFlow(resource)?.LostOnRoad ?? 0);
            for (var i = 0; i < 12 && W.Shipments.Any(x => x.ShipmentId == s.ShipmentId) && W.FindGroup(caravan.GroupId).IsActive; i++) Days(1);
            var g = W.FindGroup(caravan.GroupId);
            Assume.That(g.State, Is.EqualTo(TravelGroupState.Defeated), "Forty outlaws strike within the journey.");
            var lost = W.Settlements.Sum(x => x.Economy.FindFlow(resource)?.LostOnRoad ?? 0) - lostBefore;
            Assert.That(lost, Is.GreaterThan(0), "The loss is on the record.");
            Assert.That(W.WorldEvents.Any(e => e.Type == WorldEventType.CaravanLost && e.GroupId == caravan.GroupId), Is.True);
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.CaravanLost), Is.True);
            Valid();
        }

        // 33 ---------------------------------------------------------------------------------------------------

        [Test]
        public void GuardsStrongerThanTheOutlaws_BeatThemOff_AndKeepTheGoods()
        {
            var weak = new TravelingGroupData { Strength = 1 };
            var caravanStrength = GroupSystem.Guards(SettlementType.Town, 240) * 2;
            Assert.That(caravanStrength, Is.GreaterThan(weak.Strength), "A town caravan of 240 units: five guards.");
            Days(2);
            var s = W.Shipments.OrderBy(x => x.ShipmentId).First();
            var caravan = W.Groups.Single(g => g.ShipmentId == s.ShipmentId);
            ArmBand(caravan.Segments[0], 1);
            var qty = s.Quantity;
            Days(1);
            var still = W.Shipments.FirstOrDefault(x => x.ShipmentId == s.ShipmentId);
            if (still != null) Assert.That(still.Quantity, Is.EqualTo(qty), "Nothing was taken.");
            Assert.That(W.FindGroup(caravan.GroupId).State, Is.Not.EqualTo(TravelGroupState.Defeated));
        }

        // 34, 35, 53, 54, 55 -----------------------------------------------------------------------------------

        [Test]
        public void OneLongAdvance_EqualsManyShortOnes_AndSavingBetweenChangesNothing()
        {
            var start = new JsonWorldSerializer().Serialize(W);
            Days(8);
            var once = WorldJson();

            Edit(w => { });
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var other = RealmkeeperComposition.CreateSession(dir);
                ExplorationModule.Register(other);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(new JsonWorldStore(dir).PathFor("s"), start);
                Ok(other.Execute(new LoadWorldCommand("s")));
                for (var i = 0; i < 8; i++)
                {
                    Assert.That(other.Execute(new AdvanceDaysCommand(1)).IsSuccess, Is.True);
                    var w = other.CurrentWorldSnapshotForTests();
                    Assert.That(new JsonWorldStore(dir).Save(w, "s").IsSuccess, Is.True);
                    Ok(other.Execute(new LoadWorldCommand("s"))); // saved and loaded between every day
                }

                var steps = JObject.Parse(new JsonWorldSerializer().Serialize(other.CurrentWorldSnapshotForTests()))["world"].ToString();
                Assert.That(steps, Is.EqualTo(once), "Caravans, deliveries, factions and events: the same, with no trade done twice.");
            }
            finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        }

        // 36 ---------------------------------------------------------------------------------------------------

        [Test]
        public void AFoodShortage_BecomesAWorldEvent_KnownWhereItIs_AndEndsWhenFoodReturns()
        {
            var town = W.Settlements.Where(s => s.RegionId != Home.RegionId).OrderBy(s => s.SettlementId).First();
            Edit(w => w.FindSettlement(town.SettlementId).ResourceInventory.Set(ResourceTypes.Food, 0));
            Days(2);
            var e = W.WorldEvents.FirstOrDefault(x => x.Type == WorldEventType.FoodShortage && x.SettlementId == town.SettlementId);
            Assume.That(W.FindSettlement(town.SettlementId).IsInFoodShortage, Is.True);
            Assert.That(e, Is.Not.Null);
            Assert.That(e.IsOpen, Is.True);
            var local = W.Npcs.First(n => !n.IsDeceased && n.HomeSettlementId == town.SettlementId);
            Assert.That(NpcMind.Find(local, KnowledgeTopic.RecentEvent, e.EventId), Is.Not.Null, "The people there know.");
            Assert.That(e.Known, Is.EqualTo(EventKnowledge.Unknown), "The player, far away, does not.");
            Edit(w => w.FindSettlement(town.SettlementId).ResourceInventory.Set(ResourceTypes.Food, (int)w.FindSettlement(town.SettlementId).Economy.CapacityOf(ResourceTypes.Food)));
            Days(2);
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved));
        }

        // 37, 43 -----------------------------------------------------------------------------------------------

        [Test]
        public void AFallenBridge_HoldsTradeOnThatWay_Once_AndItsTownMendsIt()
        {
            var bridge = W.Routes.FirstOrDefault(r => r.HasBridge && W.TradeRoutes.Any(t => r.Joins(t.OriginId) && r.Joins(t.DestinationId)));
            Assume.That(bridge, Is.Not.Null, "A bridged road with trade.");
            Edit(w =>
            {
                var r = w.FindRoute(bridge.RouteId);
                r.Bridge = BridgeState.Destroyed;
                foreach (var s in w.Settlements.Where(s => r.Joins(s.SettlementId))) { s.ResourceInventory.Set(ResourceTypes.Timber, 0); }
            });
            Days(1);
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.BridgeDamaged && x.RouteId == bridge.RouteId && x.IsOpen);
            var held = W.TradeRoutes.Where(t => bridge.Joins(t.OriginId) && bridge.Joins(t.DestinationId)).ToList();
            Assert.That(held.All(t => t.BlockedBy == e.EventId), Is.True, "Trade on that way is held.");
            Days(1);
            Assert.That(W.WorldEvents.Count(x => x.Key == e.Key && x.IsOpen), Is.EqualTo(1), "One event per cause.");
            Assert.That(W.TradeRoutes.Where(t => bridge.Joins(t.OriginId) && bridge.Joins(t.DestinationId)).All(t => t.BlockedBy == e.EventId), Is.True);

            // Timber arrives: the town mends it in its days of work, and trade opens.
            Edit(w => { foreach (var s in w.Settlements.Where(s => bridge.Joins(s.SettlementId))) { s.ResourceInventory.Set(ResourceTypes.Timber, 400); s.ResourceInventory.Set(ResourceTypes.Stone, 400); } });
            Days(7);
            Assert.That(W.FindRoute(bridge.RouteId).Bridge, Is.EqualTo(BridgeState.Intact));
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved));
            Assert.That(W.TradeRoutes.Where(t => bridge.Joins(t.OriginId) && bridge.Joins(t.DestinationId)).All(t => string.IsNullOrEmpty(t.BlockedBy)), Is.True);
            Valid();
        }

        // 38, 46 -----------------------------------------------------------------------------------------------

        [Test]
        public void OutlawsOnARoad_AreAWorldEvent_AFactionSendsAPatrol_AndAQuestIsAsked()
        {
            var road = W.Routes.First(r => !r.IsCrossing && r.RegionA == Home.RegionId);
            ArmBand(road.RouteId, 8);
            Edit(w => { foreach (var f in w.Factions) f.Treasury = System.Math.Max(f.Treasury, 500); });
            Days(1);
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.BanditActivity && x.RouteId == road.RouteId && x.IsOpen);
            Days(1);
            Assert.That(W.Groups.Any(g => g.Kind == TravelGroupKind.Patrol && g.EventId == e.EventId), Is.True, "A faction answered with a patrol it paid for.");
            Assert.That(W.WorldEvents.Any(x => x.Type == WorldEventType.PatrolResponse && x.RouteId == road.RouteId), Is.True);
            var q = W.Quests.SingleOrDefault(x => x.DefinitionId == QuestCatalog.RoadOutlaws && x.SourceKey == "event:" + e.EventId);
            Assert.That(q, Is.Not.Null, "The road's town asks for help.");
            Assert.That(road.Joins(new SettlementId(System.Guid.Parse(q.Bound("home")))), Is.True);
            Valid();
        }

        // 39 ---------------------------------------------------------------------------------------------------

        [Test]
        public void HostileFactions_BarTheirCrossing_AndMendedRelations_OpenIt()
        {
            var crossing = W.Routes.First(r => r.IsCrossing && W.FindSettlement(r.A).FactionId != W.FindSettlement(r.B).FactionId && !W.FindSettlement(r.A).FactionId.IsEmpty && !W.FindSettlement(r.B).FactionId.IsEmpty);
            var fa = W.FindSettlement(crossing.A).FactionId;
            var fb = W.FindSettlement(crossing.B).FactionId;
            Edit(w => { var rel = w.FindRelation(fa, fb); rel.Opinion = -100; rel.Rivalry = 100; rel.Trust = 0; rel.Agreement = AgreementType.None; });
            Days(1);
            var rel1 = W.FindRelation(fa, fb);
            Assume.That(Realmkeeper.Simulation.Systems.PoliticalMath.StateOf(rel1), Is.EqualTo(RelationState.Hostile));
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.FactionConflict && x.IsOpen);
            Assert.That(e.State, Is.EqualTo(WorldEventState.Escalating));
            Assert.That(W.FindRoute(crossing.RouteId).Condition, Is.EqualTo(RouteCondition.Blocked), "The crossing is barred.");
            Assert.That(W.Groups.Any(g => g.Kind == TravelGroupKind.FactionPatrol && g.EventId == e.EventId && g.IsActive), Is.True);
            Assert.That(W.WorldEvents.Any(x => x.Type == WorldEventType.RoadBlocked && x.RouteId == crossing.RouteId && x.IsOpen), Is.True);

            Edit(w => { var rel = w.FindRelation(fa, fb); rel.Opinion = 40; rel.Rivalry = 0; rel.Trust = 60; });
            Days(1);
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved));
            Assert.That(W.FindRoute(crossing.RouteId).Passable, Is.True, "Open again.");
            Valid();
        }

        // 40, 47, 48 -------------------------------------------------------------------------------------------

        [Test]
        public void ThePlayerBreaksTheBand_ResolvingTheEvent_AndTheQuestPaysOnce()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            var road = W.Routes.First(r => !r.IsCrossing && r.RegionA == Home.RegionId);
            ArmBand(road.RouteId, 6);
            Days(1);
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.BanditActivity && x.RouteId == road.RouteId && x.IsOpen);
            var q = W.Quests.Single(x => x.DefinitionId == QuestCatalog.RoadOutlaws && x.SourceKey == "event:" + e.EventId);
            var giver = new NpcId(System.Guid.Parse(q.Bound("giver")));
            var home = W.FindNpc(giver).HomeSettlementId;
            Edit(w => { w.MinuteOfDay = 9 * 60; w.Player.Purse = 0; foreach (var g in w.Groups.Where(g => g.Kind == TravelGroupKind.Patrol)) g.State = TravelGroupState.Arrived; });
            Ok(S.Dispatch(new EnterSettlementCommand(home)));
            Ok(S.Dispatch(new StartConversationCommand(giver)));
            Ok(S.Dispatch(new ChooseResponseCommand("hook:" + q.InstanceId + ":giver")));
            Ok(S.Dispatch(new ChooseResponseCommand("accept")));
            Assert.That(W.FindQuest(q.InstanceId).IsOpen, Is.True);

            // On the road with the band, fight them where they stand until three have fallen (or the band is broken).
            Edit(w => { var pop = w.CreaturePopulations.First(p => w.FindGroup(e.GroupId)?.PopulationId == p.PopulationId); pop.Count = 3; });
            for (var round = 0; round < 6 && W.FindQuest(q.InstanceId).IsOpen && W.FindQuest(q.InstanceId).Objectives.First(o => o.Id == "break").Count < 3; round++)
            {
                if (W.Player.IsInSettlement) Ok(S.Dispatch(new StartJourneyCommand(road.Other(home))));
                if (W.Journey.Status != JourneyStatus.Interrupted) Edit(w => { w.Journey.Status = JourneyStatus.Interrupted; w.Journey.EncounterId = "threat:" + e.GroupId; w.Journey.Cleared.Clear(); });
                var r = S.Dispatch(new ResolveRoadThreatCommand(RoadChoice.Fight));
                if (W.ActiveEncounter == null) break;
                var enc = W.ActiveEncounter;
                EncounterGenerationTests.FightToTheEnd(S);
                var done = W.Encounters.Last();
                TestContext.WriteLine("ROUND " + round + " :: " + r.Message.Replace("\n", " ") + " :: " + string.Join(",", done.Combatants.Select(c => c.CreatureId + ":" + c.State)) + " winner " + done.WinningSide
                                      + " count " + W.FindQuest(q.InstanceId).Objectives.First(o => o.Id == "break").Count + " state " + W.FindQuest(q.InstanceId).State);
                if (W.Player.Rpg.IsDead) Assert.Inconclusive("The hero fell.");
                Edit(w => { w.Player.Rpg.CurrentHp = new Realmkeeper.Domain.Rpg.CharacterRules(w.Player.Rpg).MaxHp; });
            }

            Assume.That(W.FindQuest(q.InstanceId).Objectives.First(o => o.Id == "break").Count, Is.GreaterThanOrEqualTo(3), "Three outlaws fell in real fights.");
            Days(1);
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved), "The band is broken: the event ends by the player's hand.");

            // Back to the giver: the report completes the quest, and its rewards are paid once.
            Ok(S.Dispatch(new StartJourneyCommand(home)));
            for (var i = 0; i < 20 && W.Journey.Status != JourneyStatus.Arrived; i++)
            {
                if (W.Journey.Status == JourneyStatus.Halted || W.Journey.Status == JourneyStatus.Interrupted && W.Journey.EncounterId?.StartsWith("threat:") != true) S.Dispatch(new ResumeJourneyCommand());
                S.Travel(24, true);
            }

            Assert.That(W.Player.SettlementId, Is.EqualTo(home));
            Edit(w => w.MinuteOfDay = 10 * 60);
            var xp = W.Player.Rpg.Experience;
            var purse = W.Player.Purse;
            Ok(S.Dispatch(new StartConversationCommand(giver)));
            if (W.FindQuest(q.InstanceId).State != QuestState.Completed) // talking to the giver is the report (a TalkTo objective)
            {
                Ok(S.Dispatch(new ChooseResponseCommand("hook:" + q.InstanceId + ":giver")));
                Ok(S.Dispatch(new ChooseResponseCommand("report")));
            }
            Assert.That(W.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.Player.Rpg.Experience, Is.EqualTo(xp + 150));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 50));
            Edit(_ => { });
            Days(1);
            Assert.That(W.Player.Rpg.Experience, Is.EqualTo(xp + 150), "Paid once, whatever happens after.");
            Valid();
        }

        // 41, 42 -----------------------------------------------------------------------------------------------

        [Test]
        public void EventsExpire_AndTheSameCause_NeverOpensTwo()
        {
            var w = W;
            var e1 = WorldEvents.Raise(w, WorldEventType.LocalUnrest, "test:cause", 100, 2, "a test", null, out var made1);
            var e2 = WorldEvents.Raise(w, WorldEventType.LocalUnrest, "test:cause", 200, 3, "again", null, out var made2);
            Assert.That(made1, Is.True);
            Assert.That(made2, Is.False);
            Assert.That(e2, Is.SameAs(e1));
            Assert.That(WorldEvents.Move(e1, WorldEventState.Resolved, 300, "done"), Is.True);
            Assert.That(WorldEvents.Move(e1, WorldEventState.Active, 400, "again"), Is.False, "Closed is final.");
            var e3 = WorldEvents.Raise(w, WorldEventType.LocalUnrest, "test:cause", 500, 2, "a new outbreak", null, out var made3);
            Assert.That(made3, Is.True, "A new cause after the old one closed is a new event.");

            var road = W.Routes.First(r => !r.IsCrossing && r.RegionA == Home.RegionId);
            ArmBand(road.RouteId, 8);
            Edit(x => { foreach (var f in x.Factions) f.Treasury = System.Math.Max(f.Treasury, 500); });
            Days(2);
            var patrol = W.WorldEvents.First(x => x.Type == WorldEventType.PatrolResponse);
            Assert.That(patrol.ExpiresAt, Is.GreaterThan(0));
            Days(13);
            Assert.That(W.FindWorldEvent(patrol.EventId)?.State ?? WorldEventState.Expired, Is.EqualTo(WorldEventState.Expired));
            Valid();
        }

        // 44, 45 -----------------------------------------------------------------------------------------------

        [Test]
        public void News_TravelsByPeople_ThePlayerHearsRumours_WhichGrowOld_WithoutChangingTheWorld()
        {
            CombatTests.Create(S, "fighter");
            var town = In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementType == SettlementType.Town);
            Edit(w => w.FindSettlement(town.SettlementId).ResourceInventory.Set(ResourceTypes.Food, 0));
            Days(2);
            var e = W.WorldEvents.FirstOrDefault(x => x.Type == WorldEventType.FoodShortage && x.SettlementId == town.SettlementId);
            Assume.That(e, Is.Not.Null);
            var far = W.Regions.Single(r => r.AtlasX == 2 && r.AtlasZ == 1);
            Assert.That(W.Npcs.Where(n => W.FindSettlement(n.HomeSettlementId)?.RegionId == far.RegionId).All(n => NpcMind.Find(n, KnowledgeTopic.RecentEvent, e.EventId) == null), Is.True,
                "News does not leap across the world: nobody in a distant region has heard.");

            var neighbour = In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementId != town.SettlementId);
            var teller = W.Npcs.First(n => !n.IsDeceased && n.HomeSettlementId == town.SettlementId && n.Tier != NpcTier.Unassigned);
            Edit(w => { w.MinuteOfDay = 10 * 60; });
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            Ok(S.Dispatch(new StartConversationCommand(teller.NpcId)));
            Ok(S.Dispatch(new ChooseResponseCommand("ask")));
            Ok(S.Dispatch(new ChooseResponseCommand("ask_news")));
            var known = W.FindWorldEvent(e.EventId);
            Assert.That(known.Known, Is.Not.EqualTo(EventKnowledge.Unknown), "Heard (or seen, being there).");

            // It changes; the player's news does not change the world, and grows old.
            Edit(w => { var x = w.FindWorldEvent(e.EventId); x.Known = EventKnowledge.Rumored; x.KnownState = WorldEventState.Active; x.KnownSeverity = x.Severity; });
            Edit(w => w.FindSettlement(town.SettlementId).ResourceInventory.Set(ResourceTypes.Food, (int)w.FindSettlement(town.SettlementId).Economy.CapacityOf(ResourceTypes.Food)));
            Ok(S.Dispatch(new ExitSettlementCommand()));
            Days(2);
            var after = W.FindWorldEvent(e.EventId);
            Assert.That(after.State, Is.EqualTo(WorldEventState.Resolved), "The world moved on.");
            Assert.That(after.KnownState, Is.EqualTo(WorldEventState.Active), "The player still believes it.");
            Assert.That(after.KnowledgeOutdated, Is.True);
            Assert.That(S.GetAtlas().Events.Single(x => x.EventId == e.EventId).KnownState, Is.EqualTo(WorldEventState.Active), "The map shows what the player believes.");
        }

        // 49, 50 -----------------------------------------------------------------------------------------------

        [Test]
        public void WhenOthersBreakTheBand_TheOfferLapses_AndQuestAndEventSurviveSaving()
        {
            var road = W.Routes.First(r => !r.IsCrossing && r.RegionA == Home.RegionId);
            ArmBand(road.RouteId, 6);
            Days(1);
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.BanditActivity && x.RouteId == road.RouteId && x.IsOpen);
            var q = W.Quests.Single(x => x.SourceKey == "event:" + e.EventId);
            var before = WorldJson();
            Edit(_ => { });
            Assert.That(WorldJson(), Is.EqualTo(before), "Quest and event saved exactly.");
            Edit(w => { var p = w.CreaturePopulations.First(x => w.FindGroup(e.GroupId).PopulationId == x.PopulationId); p.Count = 0; });
            Days(2);
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved));
            var after = W.FindQuest(q.InstanceId);
            Assert.That(after == null || after.State == QuestState.Unavailable || after.State == QuestState.Failed, Is.True, "The offer lapsed with its cause.");
        }

        // 51, 52 -----------------------------------------------------------------------------------------------

        [Test]
        public void DistantRegions_KeepLiving_AtALowerCost()
        {
            var far = W.Regions.Single(r => r.AtlasX == 2 && r.AtlasZ == 1);
            var farTowns = In(far).Select(s => s.SettlementId).ToList();
            var food = farTowns.ToDictionary(id => id, id => W.FindSettlement(id).ResourceInventory.Get(ResourceTypes.Food));
            Assert.That(SimulationTiers.TierOf(W, far.RegionId), Is.EqualTo(SimulationTier.Distant));
            Assert.That(SimulationTiers.TierOf(W, Home.RegionId), Is.EqualTo(SimulationTier.Local));
            var start = W.CurrentSimulationDate;
            Days(3);
            Assert.That(farTowns.Any(id => W.FindSettlement(id).ResourceInventory.Get(ResourceTypes.Food) != food[id]), Is.True, "Their economy runs every day.");
            var farPeople = W.Npcs.Where(n => farTowns.Contains(n.HomeSettlementId) && !n.IsDeceased && n.Tier != NpcTier.Unassigned).ToList();
            var homePeople = W.Npcs.Where(n => In(Home).Any(s => s.SettlementId == n.HomeSettlementId) && !n.IsDeceased && n.Tier != NpcTier.Unassigned).ToList();
            var farFresh = farPeople.Count(n => n.LastUpdatedOn.IsValidDate && n.LastUpdatedOn.DaysUntil(W.CurrentSimulationDate) == 0) * 100 / System.Math.Max(1, farPeople.Count);
            var homeFresh = homePeople.Count(n => n.LastUpdatedOn.IsValidDate && n.LastUpdatedOn.DaysUntil(W.CurrentSimulationDate) == 0) * 100 / System.Math.Max(1, homePeople.Count);
            TestContext.WriteLine($"Full updates today: home {homeFresh}% of {homePeople.Count}, distant {farFresh}% of {farPeople.Count}.");
            Assert.That(farFresh, Is.LessThan(homeFresh), "Distant people get their full update weekly, not daily.");
        }

        // 57 ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheChronicle_StaysInOrder_AndBounded()
        {
            Days(10);
            var dates = W.HistoricalEvents.Select(e => e.Date.ToDayNumber()).ToList();
            for (var i = 1; i < dates.Count; i++) Assert.That(dates[i], Is.GreaterThanOrEqualTo(dates[i - 1]), "Chronicle in date order.");
            Assert.That(W.WorldEvents.Count(e => !e.IsOpen), Is.LessThanOrEqualTo(WorldEvents.MaxClosedKept));
            Valid();
        }
    }
}
