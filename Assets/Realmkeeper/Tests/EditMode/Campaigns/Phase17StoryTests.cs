using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Party;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0085, ADR-0087): companions in the story, stories the world offers, and the campaign's state through
    /// saves, reloads, migrations and repeated runs.
    /// </summary>
    public sealed class Phase17StoryTests : Phase17Fixture
    {
        /// <summary>
        /// <paramref name="n"/> grown residents of the home town who will come (not cast in the story, no post, steady nerves),
        /// with the personalities given, recruited. Returns their IDs.
        /// </summary>
        private List<NpcId> Companions(params (NpcTrait trait, int value)[][] personalities)
        {
            var town = QuestGenesis.FirstTown(W);
            var cast = new HashSet<string>(C?.Bindings.Values ?? Enumerable.Empty<string>());
            var givers = new HashSet<string>(W.Quests.Where(q => q.IsOpen || q.State == QuestState.Offered || q.State == QuestState.Available).Select(q => q.Bound("giver")).Where(x => x != null));
            var picks = W.Npcs.Where(x => !x.IsDeceased && x.HomeSettlementId == town.SettlementId && (!x.BirthDate.IsValidDate || x.AgeOn(W.CurrentSimulationDate) >= 18)
                                          && !cast.Contains(x.NpcId.ToString()) && !givers.Contains(x.NpcId.ToString()) && W.FindEscort(x.NpcId) == null && PlacePresence.PostedAt(W, x.NpcId) == null)
                .OrderBy(x => RecruitRules.HoldsPost(x.Role)).ThenBy(x => x.NpcId).Take(personalities.Length).Select(x => x.NpcId).ToList();
            if (picks.Count < personalities.Length) Assert.Inconclusive($"Only {picks.Count} suitable people in this town.");
            Edit(w =>
            {
                for (var i = 0; i < picks.Count; i++)
                {
                    var x = w.FindNpc(picks[i]);
                    if (RecruitRules.HoldsPost(x.Role)) x.Role = NpcRole.Resident;
                    x.PlayerRegard.Familiarity = 40;
                    x.PlayerRegard.Trust = 40;
                    x.PlayerRegard.Affinity = 20;
                    foreach (NpcTrait t in System.Enum.GetValues(typeof(NpcTrait))) x.Personality[t] = 50;
                    x.Personality[NpcTrait.Courage] = 60;
                    foreach (var (trait, value) in personalities[i]) x.Personality[trait] = value;
                }
            });
            foreach (var id in picks) Ok(S.Dispatch(new RecruitCompanionCommand(id)));
            return picks;
        }

        private void ToTheQuestion()
        {
            ChapterOne(lookAtTally: false);
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
        }

        // 56, 57, 59 -------------------------------------------------------------------------------------------

        [Test]
        public void Companions_JudgeADecision_ByTheirOwnNature_AndFallOutOverIt()
        {
            Hero();
            Begin();
            var ids = Companions(new[] { (NpcTrait.Ambition, 90), (NpcTrait.Compassion, 20) }, new[] { (NpcTrait.Ambition, 15), (NpcTrait.Compassion, 90) });
            var hard = ids[0];
            var soft = ids[1];
            var before = (W.FindCompanion(hard).Loyalty, W.FindCompanion(soft).Loyalty);
            ToTheQuestion();
            Ok(Say("home"));
            Assert.That(W.FindCompanion(hard).Loyalty, Is.GreaterThan(before.Item1), "The ambitious one approves (56).");
            Assert.That(W.FindCompanion(soft).Loyalty, Is.LessThan(before.Item2), "The compassionate one objects.");
            Assert.That(W.FindCompanion(soft).Approvals.Last().Reason, Does.Contain("objected when you told"), "With a reason in words.");
            var rel = W.FindNpcRelation(hard, soft);
            Assert.That(rel, Is.Not.Null);
            Assert.That(rel.Rivalry, Is.GreaterThanOrEqualTo(12), "They fell out over it (59).");
            Assert.That(W.FindCompanion(soft).Remarks.Any(r => !r.Heard), Is.True, "They have something to say (57).");

            // 57: asked, they say it — once.
            Talk(W.FindNpc(soft));
            Assert.That(Options(), Does.Contain("remark"));
            Ok(Say("remark"));
            var line = DialogueEngine.View(W).NpcLine;
            Assert.That(line, Is.Not.Empty.And.Not.EqualTo("…"));
            // 59: a second disagreement (the toll-keeper: the hard one approves of exposing them, the soft one does not), and the quarrel shows.
            Ok(S.Dispatch(new EndConversationCommand()));
            About(Npc("counterpart"), "keeper", "giver");
            Ok(Say("accept"));
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            Ok(Say("expose"));
            Assert.That(W.FindNpcRelation(hard, soft).Rivalry, Is.GreaterThanOrEqualTo(CompanionStories.QuarrelAt));
            Talk(W.FindNpc(soft));
            Assert.That(Options(), Does.Contain("reconcile"), "The player can try to talk the two round.");
            var rivalry = W.FindNpcRelation(hard, soft).Rivalry;
            Ok(Say("reconcile"));
            Assert.That(W.FindNpcRelation(hard, soft).Rivalry, Is.Not.EqualTo(rivalry), "The quarrel eased or hardened, by the roll.");
        }

        // 60 --------------------------------------------------------------------------------------------------

        [Test]
        public void AReserveCompanion_IsNotThere_AndDoesNotReact()
        {
            Hero();
            Begin();
            var ids = Companions(new[] { (NpcTrait.Ambition, 95) });
            Ok(S.Dispatch(new SetPartyRoleCommand(ids[0], PartyRole.Reserve)));
            var loyalty = W.FindCompanion(ids[0]).Loyalty;
            ToTheQuestion();
            Ok(Say("home"));
            Assert.That(W.FindCompanion(ids[0]).Loyalty, Is.EqualTo(loyalty));
            Assert.That(W.FindCompanion(ids[0]).Remarks, Is.Empty);
        }

        // 54, 55, 58 ------------------------------------------------------------------------------------------

        [Test]
        public void ATrustedCompanion_AsksTheirSecondStory_ItAdvancesFromRealEvents_AndPersists()
        {
            Hero();
            var ids = Companions(new[] { (NpcTrait.Courage, 70) });
            var id = ids[0];
            var npc = W.FindNpc(id);
            var elsewhere = W.Npcs.Where(n => !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId != npc.HomeSettlementId && W.FindSettlement(n.HomeSettlementId) != null)
                .OrderBy(n => n.NpcId).First();
            Edit(w =>
            {
                var r = NpcMind.Relate(w, id, elsewhere.NpcId, NpcRelationCause.Quarrel, NpcRelationKind.Rival, w.CurrentSimulationDate);
                r.Rivalry = 50;
                var rec = w.FindCompanion(id);
                rec.Loyalty = 80;
                rec.PersonalQuestId = "done-before"; // the first story is behind them (its quest is not in this world: treated as closed)
            });
            Days(1);
            var rec2 = W.FindCompanion(id);
            Assert.That(rec2.StoryQuestId, Is.Not.Null, "Their second story is asked (54).");
            var q = W.FindQuest(rec2.StoryQuestId);
            Assert.That(q.Bound("rival"), Is.EqualTo(elsewhere.NpcId.ToString()), "Bound to the real person.");
            Talk(W.FindNpc(id));
            Ok(Say("hook:" + q.InstanceId + ":giver"));
            Ok(Say("accept"));
            Save("story");
            Load("story");
            Assert.That(W.FindCompanion(id).StoryQuestId, Is.EqualTo(q.InstanceId), "Kept through a save (58).");
            GoTo(W.FindSettlement(elsewhere.HomeSettlementId));
            Assert.That(W.FindQuest(q.InstanceId).Progress("go").Done, Is.True, "Arriving with them advances it.");
            var loyalty = W.FindCompanion(id).Loyalty;
            Ok(S.Dispatch(new AbandonQuestCommand(q.InstanceId)));
            Assert.That(W.FindCompanion(id).Loyalty, Is.LessThan(loyalty), "Giving it up costs their loyalty (55).");
        }

        [Test]
        public void ACompanionArrivingWhereTheirPeopleLive_SaysSo_Once()
        {
            Hero();
            var ids = Companions(new[] { (NpcTrait.Sociability, 70) });
            var id = ids[0];
            var home = W.FindNpc(id).HomeSettlementId;
            var friendTown = W.Settlements.Where(s => s.SettlementId != home && W.Npcs.Any(n => !n.IsDeceased && n.HomeSettlementId == s.SettlementId)).OrderBy(s => s.SettlementId).First();
            var friend = W.Npcs.Where(n => !n.IsDeceased && n.HomeSettlementId == friendTown.SettlementId).OrderBy(n => n.NpcId).First();
            Edit(w => NpcMind.Relate(w, id, friend.NpcId, NpcRelationCause.Conversation, NpcRelationKind.Friend, w.CurrentSimulationDate).Affinity = 60);
            GoTo(friendTown);
            GoTo(friendTown);
            Assert.That(W.FindCompanion(id).Remarks.Count(r => r.Key.StartsWith("home:")), Is.EqualTo(1));
            Assert.That(W.FindCompanion(id).Remarks.Single(r => r.Key.StartsWith("home:")).Text, Does.Contain(friend.Name));
        }

        // 61–63, 68–70 --------------------------------------------------------------------------------------

        private WorldEventData LoseACaravan()
        {
            var w = W;
            var g = w.Groups.FirstOrDefault(x => x.Kind == TravelGroupKind.Caravan && x.IsActive && x.Segments.Count > 0);
            Assume.That(g, Is.Not.Null, "No caravan on the road in this world.");
            var route = w.FindRoute(g.Segments[0]);
            string id = null;
            Edit(x =>
            {
                var e = WorldEvents.Raise(x, WorldEventType.CaravanLost, "test-lost:" + g.GroupId, QuestClock.Now(x), 2, "outlaws took a caravan", ev =>
                {
                    ev.RouteId = route.RouteId;
                    ev.GroupId = g.GroupId;
                    ev.RegionId = route.RegionA;
                }, out _);
                WorldEvents.TellSettlements(x, e, new[] { route.A, route.B }, x.CurrentSimulationDate.ToDayNumber());
                id = e.EventId;
            });
            return W.FindWorldEvent(id);
        }

        [Test]
        public void ALostCaravan_BecomesAStory_TheAccountIsTheEventsOwn_AndItIsResolvedInPlay()
        {
            Hero();
            Days(2); // caravans set out
            var e = LoseACaravan();
            // Phase 18 (lifecycle audit): real caravan losses in these days now make stories too (a closed loss used to be
            // skipped), and one new story is made a day — so this one may wait its turn behind them for a day or two.
            QuestInstanceData q = null;
            for (var day = 0; day < 5 && q == null; day++)
            {
                Days(1);
                q = W.Quests.SingleOrDefault(x => x.SourceKey == StorySynthesis.Prefix + "caravan:" + e.EventId);
            }

            Assert.That(q, Is.Not.Null, "A story is made of it (62).");
            Assert.That(q.Variables["account"], Is.EqualTo(WorldEvents.Telling(W, W.FindWorldEvent(e.EventId))), "Nothing invented: the account is the event's own (70).");
            var giver = QuestEffects.Person(W, q, "giver");
            Assert.That(NpcMind.Find(giver, KnowledgeTopic.RecentEvent, q.SourceKey), Is.Not.Null, "The giver carries it as news (68).");
            var witness = QuestEffects.Person(W, q, "witness");
            Assert.That(NpcMind.Find(witness, KnowledgeTopic.RecentEvent, e.EventId), Is.Not.Null, "The witness really heard of it.");
            Days(3);
            Assert.That(W.Quests.Count(x => x.SourceKey == q.SourceKey), Is.EqualTo(1), "Never made twice (69).");

            var stability = W.FindSettlement(QuestEffects.SettlementOf(W, q, "settlement").SettlementId).Stability;
            Talk(giver);
            Ok(Say("hook:" + q.InstanceId + ":giver"));
            Ok(Say("accept"));
            Talk(witness);
            Ok(Say("hook:" + q.InstanceId + ":witness"));
            Ok(Say("thanks"));
            Talk(QuestEffects.Person(W, q, "giver"));
            Ok(Say("hook:" + q.InstanceId + ":giver"));
            Ok(Say("report"));
            Assert.That(W.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Completed), "Resolved in play (63).");
            Assert.That(W.FindSettlement(QuestEffects.SettlementOf(W, q, "settlement").SettlementId).Stability, Is.EqualTo(System.Math.Min(100, stability + 2)), "Its result stays in the world.");
        }

        // 64–67 --------------------------------------------------------------------------------------------------

        private (SettlementData hungry, SettlementData full) Famine()
        {
            var w = W;
            foreach (var r in w.Routes.Where(x => x.Passable).OrderBy(x => x.RouteId))
            {
                var a = w.FindSettlement(r.A);
                var b = w.FindSettlement(r.B);
                if (a == null || b == null) continue;
                Edit(x =>
                {
                    var h = x.FindSettlement(a.SettlementId);
                    h.ResourceInventory.Set(Realmkeeper.Domain.Economy.ResourceTypes.Food, 0);
                    h.IsInFoodShortage = true;
                    var f = x.FindSettlement(b.SettlementId);
                    f.IsInFoodShortage = false;
                    var food = Realmkeeper.Domain.Economy.ResourceTypes.Food;
                    var cap = f.Economy.StorageCapacity.Get(food);
                    f.ResourceInventory.Set(food, cap > 0 ? cap : System.Math.Max(f.ResourceInventory.Get(food), f.Population * 4)); // full stores (within what it can hold)
                });
                return (W.FindSettlement(a.SettlementId), W.FindSettlement(b.SettlementId));
            }

            Assert.Inconclusive("No open road.");
            return default;
        }

        [Test]
        public void AHungryTown_AsksForGrainFromAFullOne_AndTheGrainReallyMoves()
        {
            Hero(purse: 200);
            var (hungry, full) = Famine();
            Days(1);
            var q = W.Quests.FirstOrDefault(x => x.SourceKey != null && x.SourceKey.StartsWith(StorySynthesis.Prefix + "grain:" + hungry.SettlementId));
            Assume.That(q, Is.Not.Null, "The town's shortage ended overnight or no town could spare grain.");
            var supplier = QuestEffects.SettlementOf(W, q, "supplier");
            var supplierFood = supplier.ResourceInventory.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food);
            Talk(QuestEffects.Person(W, q, "giver"));
            Ok(Say("hook:" + q.InstanceId + ":giver"));
            Ok(Say("accept"));
            Talk(QuestEffects.Person(W, q, "merchant"));
            Ok(Say("hook:" + q.InstanceId + ":merchant"));
            Ok(Say("pay"));
            var hungryFood = W.FindSettlement(hungry.SettlementId).ResourceInventory.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food);
            Talk(QuestEffects.Person(W, q, "giver"));
            Ok(Say("hook:" + q.InstanceId + ":giver"));
            Ok(Say("report"));
            Assert.That(W.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.FindSettlement(hungry.SettlementId).ResourceInventory.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food), Is.EqualTo(hungryFood + StorySynthesis.ShareFood), "The grain arrived (67).");
            Assert.That(W.FindSettlement(supplier.SettlementId).ResourceInventory.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food), Is.LessThan(supplierFood), "…from the town that had it.");
            Assert.That(full.SettlementId, Is.Not.EqualTo(default(SettlementId)));
        }

        [Test]
        public void AnIgnoredShortage_LapsesWhenItEnds()
        {
            Hero();
            var (hungry, _) = Famine();
            Days(1);
            var q = W.Quests.FirstOrDefault(x => x.SourceKey != null && x.SourceKey.StartsWith(StorySynthesis.Prefix + "grain:" + hungry.SettlementId));
            Assume.That(q, Is.Not.Null);
            Edit(x => { var s = x.FindSettlement(hungry.SettlementId); s.IsInFoodShortage = false; var cap = s.Economy.StorageCapacity.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food); s.ResourceInventory.Set(Realmkeeper.Domain.Economy.ResourceTypes.Food, cap > 0 ? cap : s.Population * 4); });
            Days(1);
            Assert.That(W.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Unavailable), "Ignored, the request lapses when the town finds food (66).");
        }

        // 71–75: persistence and migration -------------------------------------------------------------------

        [Test]
        public void SaveAndLoad_MidStory_AfterDecisions_WithPendingConsequences_AfterADeath_AndAfterTheEnd()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            Save("arcs");
            Load("arcs");
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Active), "Several arcs active across a reload (71).");
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Active));
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
            Ok(Say("far"));
            var json1 = WorldJson();
            Save("decided");
            Load("decided");
            Assert.That(WorldJson(), Is.EqualTo(json1), "A reload changes nothing (72, 73).");
            var keeper = Npc("tollkeeper");
            Kill(keeper, Npc("tollkeeper_kin")); // nobody left who knows the count: that part of the story is lost
            Save("dead");
            Load("dead");
            Assert.That(W.FindNpc(keeper.NpcId).IsDeceased, Is.True, "A death survives a reload (74).");
            Days(7);
            Assert.That(C.Pending.Single().Fired, Is.True);
            Assert.That(Arc("keeper").IsClosed, Is.True);
            if (C.State == CampaignState.Active)
            {
                About(Npc("home_voice"), "accord", "giver");
                Ok(Say("accept"));
                Ok(Say("tell"));
            }

            Assert.That(C.State, Is.EqualTo(CampaignState.Completed));
            var json2 = WorldJson();
            Save("ended");
            Load("ended");
            Assert.That(WorldJson(), Is.EqualTo(json2), "Completed stays completed (75).");
            Valid();
        }

        [Test]
        public void ASchema18Save_LoadsAsASandbox_WithItsCompanionsIntact()
        {
            Hero();
            var json = new JsonWorldSerializer().Serialize(W);
            var doc = JObject.Parse(json);
            doc["schemaVersion"] = 18;
            var world = (JObject)doc["world"];
            world.Remove("campaigns");
            foreach (var c in (JArray)world["companions"] ?? new JArray()) { ((JObject)c).Remove("remarks"); ((JObject)c).Remove("storyQuestId"); }
            var systems = (JArray)world["settings"]["enabledSimulationSystems"];
            foreach (var t in systems.ToList()) if ((string)t == CampaignSystem.Id) t.Remove();
            var result = new JsonWorldSerializer().Deserialize(doc.ToString());
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors ?? new string[0]));
            var loaded = result.World;
            Assert.That(loaded.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(WorldData.CurrentSchemaVersion, Is.EqualTo(20)); // Phase 18 (v20)
            Assert.That(loaded.Campaigns, Is.Empty, "An old world is a sandbox until a campaign is begun.");
            Assert.That(loaded.WorldSettings.EnabledSimulationSystems, Does.Contain(CampaignSystem.Id), "The campaign pass runs in it from now on.");
            Assert.That(WorldValidator.Validate(loaded).IsValid, Is.True);
        }

        // Determinism ---------------------------------------------------------------------------------------

        [Test]
        public void TheSameWorld_AndTheSameChoices_GiveTheSameStory()
        {
            string Run()
            {
                SetUp();
                Hero();
                Begin();
                ChapterOne(lookAtTally: false);
                About(Npc("counterpart"), "hold", "giver");
                Ok(Say("accept"));
                Ok(Say("home"));
                Days(8);
                var c = C;
                return string.Join("|", c.Bindings.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) + "#" + string.Join("|", c.History.Select(h => h.At + h.Text))
                       + "#" + WorldFingerprint.Of(W);
            }

            var a = Run();
            TearDown();
            var b = Run();
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
