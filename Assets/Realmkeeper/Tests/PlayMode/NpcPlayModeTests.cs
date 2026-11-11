using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 4 in the shipped scene: the People list and NPC inspector on the map, the DM's tools,
    /// residents in the settlement inspector, and a real conversation in exploration with a resident
    /// standing where the world says they are.
    /// </summary>
    public sealed class NpcPlayModeTests
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator PeopleList_Filters_AndOpensTheInspector_WithTenSections_AndDmTools()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var hud = bootstrap.Hud;
            yield return null;
            hud.ShowPeople(true);
            yield return null;
            Assert.That(hud.PeoplePanelVisible, Is.True);
            Assert.That(hud.PeopleList.Query<Button>().ToList().Count, Is.EqualTo(24), "Every named person is listed."); // Phase 10: the captain, guard, priest and warden the places call for (15 → 19); Phase 13: the watch fort's garrison of five (19 → 24)

            var town = bootstrap.Session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);
            hud.SetPeopleFilter(settlement: town.SettlementId);
            Assert.That(hud.PeopleList.Query<Button>().ToList().Count, Is.EqualTo(9)); // Phase 10: the town gains its captain, a guard and a priest (6 → 9)
            hud.SetPeopleFilter(role: NpcRole.Elder);
            Assert.That(hud.PeopleList.Query<Button>().ToList().Count, Is.EqualTo(1));
            hud.SetPeopleFilter(settlement: default(SettlementId), clearRole: true);

            var person = bootstrap.Session.GetNpcs().First(n => n.Tier == NpcTier.Resident);
            hud.SelectNpc(person.NpcId);
            yield return null;
            Assert.That(hud.NpcInspectorVisible, Is.True);
            Assert.That(hud.InspectorVisible, Is.False, "One inspector at a time.");
            foreach (var section in new[] { "identity", "personality", "skills", "condition", "goals", "whereabouts", "relationships", "memories", "knowledge", "history" })
                Assert.That(hud.NpcInspectorBody.Q<Label>("ninsp-section-" + section), Is.Not.Null, section);
            Assert.That(hud.NpcInspectorBody.Q("ninsp-tools"), Is.Null, "DM tools only when managing.");

            hud.SetManagingNpc(true);
            yield return null;
            var tools = hud.NpcInspectorBody.Q("ninsp-tools");
            Assert.That(tools, Is.Not.Null);
            tools.Q<TextField>("tool-name").value = "Hild Underhill";
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = tools.Q<Button>("tool-rename"); tools.Q<Button>("tool-rename").SendEvent(click); }
            yield return null;
            Assert.That(bootstrap.Session.FindNpc(person.NpcId).Name, Is.EqualTo("Hild Underhill"), "The DM tool dispatched the command.");
            Assert.That(bootstrap.Session.GetRecentEvents(1)[0].Summary, Does.StartWith("By the DM:"));
            Assert.That(hud.NpcInspectorBody.parent.parent.Q<Label>("ninsp-name").text, Is.EqualTo("Hild Underhill"), "The inspector refreshed.");
        }

        [UnityTest]
        public IEnumerator SettlementInspector_ListsResidents_ThatOpenTheirInspector()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var town = bootstrap.WorldView.Settlements.Values.Single(v => v.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            yield return null;
            var links = bootstrap.Hud.SettlementResidents.Query<Button>().ToList();
            Assert.That(links.Count, Is.EqualTo(9)); // Phase 10: the town gains its captain, a guard and a priest (6 → 9)
            var first = links[0];
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = first; first.SendEvent(click); }
            yield return null;
            Assert.That(bootstrap.Hud.NpcInspectorVisible, Is.True);
            Assert.That(bootstrap.Hud.InspectorVisible, Is.False);
            Assert.That(first.name, Is.EqualTo("resident-" + bootstrap.Hud.SelectedNpcId));
        }

        [UnityTest]
        public IEnumerator InExploration_YouTalkToAResident_WhereTheWorldPutsThem_AndTheyAnswerFromWhatTheyKnow()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var installer = Object.FindAnyObjectByType<ExplorationInstaller>();
            var controller = installer.Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var session = bootstrap.Session;
            var town = session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);

            // The DM sends someone to the tavern today: exploration must show them inside it.
            var person = session.GetNpcs(new NpcFilter { Settlement = town.SettlementId }).First(n => n.Tier == NpcTier.Resident);
            var tavern = session.GetLocations(town.SettlementId).First(l => l.Kind == LocationKind.Tavern);
            Assert.That(session.Dispatch(new MoveNpcCommand(person.NpcId, tavern.LocationId)).IsSuccess, Is.True);

            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return null;
            yield return null;
            var spot = controller.Streamer.Current.Residents.Single(r => r.NpcId == person.NpcId);
            Assert.That(spot.SiteKey, Is.EqualTo(SettlementSites.Hall), "Placed at the site of their authoritative location.");
            var hall = controller.Streamer.Current.Buildings.First(b => b.Key == SettlementSites.Hall);
            Assert.That(Mathf.Abs(spot.Position.X - hall.Center.X) < hall.Width / 2f && Mathf.Abs(spot.Position.Z - hall.Center.Z) < hall.Depth / 2f, Is.True, "Inside the tavern.");

            // Walk up and talk.
            var view = controller.Residents[person.NpcId];
            view.enabled = false; // hold still
            var p = view.transform.position;
            controller.Player.Teleport(new LocalPose(p.x, 0.1f, p.z - 1.3f, 0f));
            controller.CameraRig.SetView(0f, 5f);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(controller.CurrentTarget, Is.SameAs(view), "The resident is the interaction target.");
            var greeting = controller.Interact();
            Assert.That(greeting.Succeeded, Is.True, greeting.Text);
            Assert.That(controller.Hud.TopicsVisible, Is.True, "Conversation topics are offered.");
            Assert.That(controller.Hud.Topics.Query<Button>().ToList().Count, Is.EqualTo(ExplorationController.Topics.Count + 1), "The topics, and (Phase 12) Talk… for the full conversation.");

            var answer = controller.AskResident(ConversationTopic.Settlement);
            Assert.That(answer.IsSuccess, Is.True, answer.ToString());
            Assert.That(controller.Hud.ResponseText.text, Is.EqualTo(answer.Message));
            yield return null;
            Assert.That(view.Animation.Talking, Is.True, "They talk as they answer.");
            Assert.That(session.GetNpcDetail(person.NpcId).Memories.Any(m => m.Value.Contains("Talked with")), Is.True, "They remember the conversation.");
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.True);
        }
    }
}
