using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 17 helpers shared by the campaign PlayMode tests and captures.</summary>
    internal static class CampaignDriver
    {
        public const string Waystone = CampaignCatalog.Waystone;

        /// <summary>
        /// Puts the player at a settlement's gate on the world map (a save edit: the tests' licence for distance) and
        /// enters it through the ordinary command, so arriving still counts for quests.
        /// </summary>
        public static IEnumerator GoTo(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, SettlementId id)
        {
            if (s.GetConversation() != null) s.Dispatch(new Realmkeeper.Application.EndConversationCommand());
            if (c.IsExploring && s.GetPlayer().IsInSettlement && s.GetPlayer().SettlementId == id) yield break;
            if (c.IsExploring) { c.CloseWindows(); Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True); }
            for (var i = 0; i < 4; i++) yield return null;
            var region = s.FindSettlement(id).RegionId;
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                var p = (JObject)w["player"];
                p["locationKind"] = "WorldMap";
                p["settlementId"] = null;
                p["regionId"] = region.ToString();
                p["lastSettlementId"] = id.ToString();
                w["journey"] = null;
                w["minuteOfDay"] = 10 * 60;
            });
            var r = s.Dispatch(new EnterSettlementCommand(id));
            Assert.That(r.IsSuccess, Is.True, r.Message);
            for (var i = 0; i < 90 && !c.IsExploring; i++) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(c.IsExploring, Is.True);
        }

        /// <summary>Talks to a campaign person (going to their town first) and follows the hook of a campaign quest.</summary>
        public static IEnumerator About(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, string role, string questKey, string questRole)
        {
            var npc = s.GetCampaignPerson(Waystone, role);
            Assert.That(npc.IsEmpty, Is.False, "Nobody plays " + role);
            yield return GoTo(runtime, c, s, s.FindNpc(npc).HomeSettlementId);
            c.CloseWindows();
            var talk = c.StartDialogue(npc);
            Assert.That(talk.IsSuccess, Is.True, talk.Message);
            yield return null;
            var hook = "hook:" + s.GetCampaignQuest(Waystone, questKey) + ":" + questRole;
            Assert.That(c.Dialogue.View.Responses.Any(x => x.Id == hook), Is.True, "No hook " + hook + ": " + string.Join(", ", c.Dialogue.View.Responses.Select(x => x.Id)));
            QuestDriver.Say(c, hook);
            yield return null;
        }

        public static Button Response(ExplorationController c, string id) => c.Dialogue.Root.Q<Button>("xk-dialogue-response-" + id);

        public static IEnumerator Journal(ExplorationController c, string view = null)
        {
            c.CloseWindows();
            if (!c.HubWindow.IsOpen) c.ToggleHub();
            c.HubWindow.SelectJournal("Campaign");
            yield return null;
            if (view != null)
            {
                LevelUpDriver.Click(c.HubWindow.Root, "xk-campaign-view-" + view);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Phase 17 in the shipped scene, through the real UI: a fight on the road entering the combat scene and back to the
    /// road; the campaign begun, read and set aside from the journal; a decision put in conversation, with a choice
    /// shown disabled and why, and the decision recorded.
    /// </summary>
    public sealed class Phase17PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        [UnityTest]
        public IEnumerator AFightOnTheRoad_IsFoughtInTheCombatScene_AndThePartyReturnsToTheRoad()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["player"]["purse"] = 2000);
            yield return AtlasDriver.ArmTheRoads(Runtime, c, s, 3); // a band a lone fighter can face
            yield return AtlasDriver.ToAtlas(Bootstrap, c);
            var to = AtlasDriver.Beyond(hud); // a longer road, across the border: more hours for the band to show
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            for (var i = 0; i < 60 && hud.AtlasSnapshot.Journey != null && !hud.AtlasSnapshot.Journey.ThreatPending && hud.AtlasSnapshot.Journey.Status != JourneyStatus.Arrived; i++)
            {
                if (AtlasDriver.Has(hud, "rk-atlas-night")) AtlasDriver.Click(hud, "rk-atlas-night");
                else if (AtlasDriver.Has(hud, "rk-atlas-travel-hour")) AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                else break;
                yield return null;
            }

            if (hud.AtlasSnapshot.Journey == null || !hud.AtlasSnapshot.Journey.ThreatPending) Assert.Inconclusive("No outlaws barred the road on this journey.");

            // The player chooses to fight: the view changes to the ground by itself (no AI playing the player's part).
            AtlasDriver.Click(hud, "rk-atlas-threat-fight");
            for (var i = 0; i < 90 && !c.IsExploring; i++) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Exploration), "The combat scene (16).");
            Assert.That(c.RoadStage, Is.Not.Null, "Staged on the road's ground.");
            Assert.That(c.InFight, Is.True, "The existing combat panel, on the player's fight.");
            Assert.That(s.GetPlayer().IsInSettlement, Is.False, "The party is still on the road in the world.");
            Assert.That(c.Combat.Snapshot.Order.Count(x => x.HostileToPlayer), Is.GreaterThan(0), "The outlaws are there.");
            var fight = s.GetRoadFight();
            Assert.That(fight.EncounterId, Is.EqualTo(c.Combat.Snapshot.Id.ToString()));

            // Played to its end as a player would, through the panel: attack, step towards the outlaws, answer what is asked.
            c.AutoAi = false;
            for (var i = 0; i < 400 && c.InFight; i++)
            {
                var snap = c.Combat.Snapshot;
                if (snap.Prompt != null) c.AnswerReaction(snap.Prompt.Id, true);
                else CombatDriver.Step(c);
                yield return null;
            }

            if (s.GetCharacter().IsDead) Assert.Inconclusive("The hero fell to the outlaws.");
            Assert.That(c.InFight, Is.False, "The fight was fought to its end.");
            Assert.That(s.GetRoadFight(), Is.Not.Null, "The finished fight stays on screen until the player leaves it.");

            // Closing the panel puts the party back on the road, the journey halted; travel resumes only when asked.
            LevelUpDriver.Click(c.Combat.Root, "xk-combat-close");
            for (var i = 0; i < 30 && c.IsExploring; i++) yield return null;
            Assert.That(c.IsExploring, Is.False, "Back to the map (17).");
            Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Strategic));
            var j = s.GetAtlas().Journey;
            Assert.That(j, Is.Not.Null);
            Assert.That(j.Status, Is.EqualTo(JourneyStatus.Halted), "Halted on the road.");
            hud.ToggleAtlas(true);
            yield return null;
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-resume"), Is.True, "The player resumes when ready.");
        }

        /// <summary>
        /// The Phase 16 atlas and travel checklist (Phase 17, §3), through the real window: every region and layer, a
        /// destination and its ways, a journey halted, resumed, turned back, saved and reloaded on the road, and an arrival
        /// with the party.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAtlasChecklist_RegionsLayersRoutesAndAJourney_SavedAndReloadedOnTheRoad()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            var rt = Runtime;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, rt, c);
            var ids = new System.Collections.Generic.List<NpcId>();
            yield return PartyDriver.Willing(rt, c, s, 2, ids);
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);
            yield return ItemDriver.Give(rt, c, s, w => w["player"]["purse"] = 2000);
            yield return AtlasDriver.ToAtlas(Bootstrap, c);

            // Open and close; every region; every layer.
            hud.ToggleAtlas(false);
            yield return null;
            Assert.That(hud.AtlasOpen, Is.False);
            hud.ToggleAtlas(true);
            yield return null;
            var v = hud.AtlasSnapshot;
            Assert.That(v.Regions.Count, Is.EqualTo(6), "Six regions.");
            foreach (var r in v.Regions)
            {
                AtlasDriver.Click(hud, "rk-atlas-mark-region:" + r.RegionId);
                yield return null;
                Assert.That(hud.AtlasSelection, Is.EqualTo("region:" + r.RegionId));
                var side = string.Join(" ", hud.AtlasRoot.Q("rk-atlas-side").Query<Label>().ToList().Select(l => l.text));
                Assert.That(side, Does.Contain(r.Name), "The inspector names " + r.Name + " (uncharted lands stay uncharted).");
            }

            foreach (var layer in RealmkeeperHud.AtlasLayers)
            {
                hud.SetAtlasLayer(layer, false);
                yield return null;
                Assert.That(hud.AtlasLayersOn.Contains(layer), Is.False);
                hud.SetAtlasLayer(layer, true);
                yield return null;
                Assert.That(hud.AtlasLayersOn.Contains(layer), Is.True);
            }

            // A destination beyond the border: the best way and its alternatives, each a walkable way.
            var far = AtlasDriver.Beyond(hud, far: false);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + far.SettlementId);
            yield return null;
            var options = s.GetTravelOptions(far.SettlementId);
            Assert.That(options, Is.Not.Empty);
            Assert.That(options.All(o => !double.IsInfinity(o.Hours)), Is.True, "No unusable way is offered.");
            Assert.That(options.Select(o => o.Hours), Is.Ordered, "The best way first.");
            for (var i = 0; i < options.Count; i++) Assert.That(AtlasDriver.Has(hud, "rk-atlas-go-" + i), Is.True);

            // Begin, travel, halt, resume.
            var origin = s.GetPlayer().LastSettlementId;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            AtlasDriver.Click(hud, "rk-atlas-travel-hour");
            yield return null;
            if (hud.AtlasSnapshot.Journey.ThreatPending) { AtlasDriver.Click(hud, "rk-atlas-threat-pay"); yield return null; }
            AtlasDriver.Click(hud, "rk-atlas-halt");
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey.Status, Is.EqualTo(JourneyStatus.Halted));
            AtlasDriver.Click(hud, "rk-atlas-resume");
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));

            // Save on the road, change nothing, reload: the journey and the party as they were.
            var before = hud.AtlasSnapshot.Journey;
            var pct = before.PercentDone;
            var party = hud.AtlasSnapshot.Party.ToArray();
            Assert.That(rt.Save().IsSuccess, Is.True, "Saved on the road.");
            Assert.That(s.Execute(new LoadWorldCommand(rt.ActiveSlot.Name)).IsSuccess, Is.True, "Reloaded on the road.");
            for (var i = 0; i < 4; i++) yield return null;
            hud.ToggleAtlas(true);
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey.Destination, Is.EqualTo(before.Destination));
            Assert.That(hud.AtlasSnapshot.Journey.PercentDone, Is.EqualTo(pct));
            Assert.That(hud.AtlasSnapshot.Party, Is.EqualTo(party), "The party still with the player.");

            // Turn back: a new journey from where the party stands, back to where it set out.
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + origin);
            yield return null;
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-go-0"), Is.True, "The way back is offered from the road.");
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey.Destination, Is.EqualTo(s.FindSettlement(origin).Name));

            // Arrive, with the party.
            yield return AtlasDriver.TravelOn(hud);
            for (var i = 0; i < 120 && !c.IsExploring; i++) yield return null;
            for (var i = 0; i < 10; i++) yield return null;
            Assert.That(s.GetPlayer().IsInSettlement, Is.True, "Arrived and entered.");
            Assert.That(s.GetPlayer().SettlementId, Is.EqualTo(origin));
            Assert.That(c.Companions.Count, Is.EqualTo(ids.Count), "The party arrived together.");
        }

        [UnityTest]
        public IEnumerator TheCampaignJournal_BeginsTheStory_ShowsIt_AndSetsItAside()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return CampaignDriver.Journal(c);
            Assert.That(ItemDriver.Texts(c.HubWindow.Root), Does.Contain("open-ended"), "A sandbox world says so.");
            Assert.That(c.HubWindow.Root.Q<Button>("xk-campaign-start-" + CampaignDriver.Waystone), Is.Not.Null, "The story that could begin here, with its button.");
            LevelUpDriver.Click(c.HubWindow.Root, "xk-campaign-start-" + CampaignDriver.Waystone);
            yield return null;
            var v = s.GetCampaigns().Campaigns.Single();
            Assert.That(v.State, Is.EqualTo(CampaignState.Active));
            Assert.That(ItemDriver.Texts(c.HubWindow.Root), Does.Contain("provisional demonstration"), "Marked as demonstration content.");
            Assert.That(c.HubWindow.Root.Q<Label>("xk-campaign-premise")?.text, Does.Contain(s.FindSettlement(s.GetCampaignSettlement(CampaignDriver.Waystone, "home_town")).Name), "The premise names the world's own town.");

            yield return CampaignDriver.Journal(c, "story");
            Assert.That(c.HubWindow.Root.Q("xk-campaign-arc-letter"), Is.Not.Null, "The first arc is shown.");
            Assert.That(c.HubWindow.Root.Q("xk-campaign-arc-accord"), Is.Null, "A later chapter is not shown before it opens.");

            LevelUpDriver.Click(c.HubWindow.Root, "xk-campaign-suspend");
            yield return null;
            Assert.That(s.GetCampaigns().Campaigns.Single().State, Is.EqualTo(CampaignState.Suspended));
            LevelUpDriver.Click(c.HubWindow.Root, "xk-campaign-resume");
            yield return null;
            Assert.That(s.GetCampaigns().Campaigns.Single().State, Is.EqualTo(CampaignState.Active));
        }

        [UnityTest]
        public IEnumerator ADecisionInConversation_ShowsWhatIsOutOfReach_AndIsRecorded()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var rt = Runtime;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, rt, c);
            Assert.That(s.Dispatch(new StartCampaignCommand(CampaignDriver.Waystone)).IsSuccess, Is.True);
            yield return CampaignDriver.About(rt, c, s, "reeve", "letter", "giver");
            QuestDriver.Say(c, "accept");
            yield return null;
            yield return CampaignDriver.GoTo(rt, c, s, s.GetCampaignSettlement(CampaignDriver.Waystone, "far_town"));
            yield return CampaignDriver.About(rt, c, s, "counterpart", "letter", "recipient");
            QuestDriver.Say(c, "give");
            yield return null;
            Assert.That(s.GetCampaigns().Campaigns.Single().Chapters.Any(ch => ch.Id == "two_accounts"), Is.True, "Chapter two opened.");

            yield return CampaignDriver.About(rt, c, s, "counterpart", "hold", "giver");
            QuestDriver.Say(c, "accept");
            yield return null;
            var shared = CampaignDriver.Response(c, "shared");
            Assert.That(shared, Is.Not.Null, "The out-of-reach choice is shown…");
            Assert.That(shared.enabledSelf, Is.False, "…disabled…");
            Assert.That(c.Dialogue.Root.Q<Label>("xk-dialogue-why-shared")?.text, Does.Contain("proof"), "…with the reason.");
            Assert.That(CampaignDriver.Response(c, "home").text, Does.Contain("[decision]"), "A decision is marked as one.");
            QuestDriver.Say(c, "apart");
            yield return null;
            var v = s.GetCampaigns().Campaigns.Single();
            Assert.That(v.Decisions.Single().DecisionId, Is.EqualTo("road_claim"));
            yield return CampaignDriver.Journal(c, "decisions");
            Assert.That(c.HubWindow.Root.Q("xk-campaign-decision-road_claim"), Is.Not.Null, "The journal records it.");
        }
    }
}
