using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Combat;
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
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 15 helpers shared by the party's PlayMode tests and captures.</summary>
    internal static class PartyDriver
    {
        /// <summary>
        /// <paramref name="n"/> residents drawn in this town made willing to travel (they know, trust and like the player;
        /// someone with a post stands in as a resident), through an edit of the saved world. Returns their IDs.
        /// </summary>
        public static IEnumerator Willing(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, int n, List<NpcId> into)
        {
            var ids = c.Residents.Keys.Where(id => s.FindNpc(id) is NpcSummary x && x.IsAlive).OrderBy(id => RecruitRules.HoldsPost(s.FindNpc(id).Role) ? 1 : 0).ThenBy(id => id).Take(n).ToList();
            Assume.That(ids.Count, Is.EqualTo(n), "Not enough people drawn in this town.");
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                foreach (JObject npc in (JArray)w["npcs"])
                {
                    if (!ids.Any(id => id.ToString() == (string)npc["npcId"])) continue;
                    if (RecruitRules.HoldsPost(System.Enum.TryParse<NpcRole>((string)npc["role"], out var r) ? r : NpcRole.Resident)) npc["role"] = "Resident";
                    var regard = npc["playerRegard"] as JObject ?? new JObject();
                    regard["familiarity"] = 40;
                    regard["trust"] = 40;
                    regard["affinity"] = 20;
                    npc["playerRegard"] = regard;
                }
            });
            into.AddRange(ids);
        }

        /// <summary>Asks them to join in conversation, as a player would (Talk, then "Travel with me"), and closes the window.</summary>
        public static IEnumerator Recruit(ExplorationController c, WorldSession s, NpcId id)
        {
            var talk = c.StartDialogue(id);
            Assert.That(talk.IsSuccess, Is.True, talk.Message);
            yield return null;
            var r = c.ChooseResponse("recruit");
            Assert.That(r.IsSuccess, Is.True, r.Message);
            yield return null;
            c.CloseWindows();
            yield return PlaceDriver.Frames(4);
            Assert.That(s.GetParty().Members.Any(m => m.NpcId == id), Is.True, "Joined: " + string.Join(" / ", s.GetConversation()?.Transcript.Select(t => t.Text) ?? Enumerable.Empty<string>()));
        }

        /// <summary>Walks the player forward for a while (scripted input).</summary>
        public static IEnumerator Walk(ExplorationController c, Vector2 move, float seconds, bool sprint = false)
        {
            var input = new ScriptedExplorationInput { Move = move, Sprint = sprint };
            c.SetInput(input);
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
            input.Move = Vector2.zero;
            input.Sprint = false;
            yield return PlaceDriver.Frames(2);
        }
    }

    /// <summary>
    /// Phase 15 in the shipped scene: a resident asked in conversation joins the party; their resident body gives way to
    /// one companion body (never two), which keeps up as the player walks and runs, without being teleported; the HUD
    /// strip shows them; the Party section shows their sheet and items and its buttons are commands (wait here, follow);
    /// a practice fight puts them beside the player, their decisions explained in the combat log; and parting ways
    /// returns them to their own life.
    /// </summary>
    public sealed class Phase15PartyPlayModeTests
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

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        [UnityTest]
        public IEnumerator Recruited_TheyFollow_InOneBody_AndKeepUp_WithoutTeleporting()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 2, ids);
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);
            yield return PlaceDriver.Frames(10);

            foreach (var id in ids)
            {
                Assert.That(c.Companions.ContainsKey(id), Is.True, "A companion body for each member.");
                Assert.That(c.Residents.ContainsKey(id), Is.False, "Their resident body gave way: never drawn twice.");
            }

            Assert.That(Object.FindObjectsByType<CompanionFollower>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(c.PartyStripText, Does.Contain(s.FindNpc(ids[0]).Name));
            Assert.That(c.Hud.Root.Q<VisualElement>("xk-party"), Is.Not.Null, "The HUD's party strip.");

            // Walking and running: they keep up on foot.
            yield return PartyDriver.Walk(c, new Vector2(0f, 1f), 3f);
            yield return PartyDriver.Walk(c, new Vector2(0.3f, 1f), 3f, sprint: true);
            yield return PartyDriver.Walk(c, Vector2.zero, 1.5f);
            foreach (var id in ids)
            {
                var f = c.Companions[id];
                var d = Vector3.Distance(f.transform.position, c.Player.transform.position);
                Assert.That(d, Is.LessThan(8f), $"{f.DisplayName} kept up ({d:0.0} m).");
                Assert.That(d, Is.GreaterThan(0.6f), "…without standing inside the player.");
            }

            Assert.That(Vector3.Distance(c.Companions[ids[0]].transform.position, c.Companions[ids[1]].transform.position), Is.GreaterThan(0.5f), "Spaced apart.");
            Assert.That(c.CompanionRecoveries, Is.EqualTo(0), "Nobody had to be recovered (no teleporting while following).");
        }

        [UnityTest]
        public IEnumerator ThePartySection_ShowsTheirSheetAndItems_AndItsButtonsAreCommands()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 1, ids);
            yield return PartyDriver.Recruit(c, s, ids[0]);
            var name = s.FindNpc(ids[0]).Name;

            c.OpenParty();
            yield return null;
            Assert.That(c.HubWindow.IsOpen, Is.True);
            Assert.That(c.HubWindow.Section, Is.EqualTo("Party"));
            var content = c.HubWindow.Content;
            Assert.That(content.Q<VisualElement>("xk-party-card-" + ids[0]), Is.Not.Null, "A card for the member.");
            var detail = content.Q<VisualElement>("xk-party-detail");
            Assert.That(detail, Is.Not.Null);
            var texts = detail.Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(texts.Any(t => t.Contains("Armour class")), Is.True, "Their sheet.");
            Assert.That(texts.Any(t => t.Contains("Loyalty")), Is.True, "Their loyalty.");
            Assert.That(texts.Any(t => t.StartsWith("What " + name + " carries")), Is.True, "Their items.");

            ItemDriver.ClickText(content, "Wait here");
            yield return PlaceDriver.Frames(4);
            Assert.That(s.GetParty().Members.Single().Mode, Is.EqualTo(FollowMode.Wait), "Hub said: " + c.HubWindow.LastMessage);
            c.CloseWindows();
            yield return PlaceDriver.Frames(4);
            var stood = c.Companions[ids[0]].transform.position;
            yield return PartyDriver.Walk(c, new Vector2(0f, 1f), 2.5f);
            Assert.That(Vector3.Distance(c.Companions[ids[0]].transform.position, stood), Is.LessThan(1f), "Waiting where they were told.");

            c.OpenParty();
            yield return null;
            ItemDriver.ClickText(c.HubWindow.Content, "Follow me");
            yield return PlaceDriver.Frames(4);
            Assert.That(s.GetParty().Members.Single().Mode, Is.EqualTo(FollowMode.Follow));

            ItemDriver.ClickText(c.HubWindow.Content, "Part ways");
            yield return PlaceDriver.Frames(4);
            c.CloseWindows();
            yield return PlaceDriver.Frames(4);
            Assert.That(s.GetParty().Members, Is.Empty);
            Assert.That(c.Companions.ContainsKey(ids[0]), Is.False);
            Assert.That(c.Residents.ContainsKey(ids[0]), Is.True, "Back in their own body and their own life.");
        }

        [UnityTest]
        public IEnumerator InAFight_TheyStandBesideThePlayer_AndTheLogSaysWhy()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 1, ids);
            yield return PartyDriver.Recruit(c, s, ids[0]);
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            Assert.That(c.StartPracticeFight(false).IsSuccess, Is.True);
            yield return PlaceDriver.Frames(6);
            var snap = c.Combat.Snapshot;
            var companion = snap.Order.SingleOrDefault(o => o.Name == s.FindNpc(ids[0]).Name);
            Assert.That(companion, Is.Not.Null, "The companion is in the fight.");
            Assert.That(companion.HostileToPlayer, Is.False);
            Assert.That(c.BodySourceOf(companion.Id), Is.Not.Null, "Drawn in a body of their own.");
            Assert.That(c.Companions[ids[0]].GetComponentInChildren<Animator>(true)?.gameObject.activeInHierarchy ?? false, Is.False, "The exploration body steps aside during the fight.");

            for (var i = 0; i < 400 && c.InFight; i++)
            {
                CombatDriver.Step(c);
                yield return null;
                if (c.Combat.Root.Query<Label>(className: "xk-combat-log--reason").ToList().Count > 0) break;
            }

            var reasons = c.Combat.Root.Query<Label>(className: "xk-combat-log--reason").ToList().Select(l => l.text).ToList();
            Assert.That(reasons.Any(t => t.StartsWith(companion.Name + ": ")), Is.True, "Their choices are explained in the log: " + string.Join(" | ", reasons));
            if (c.InFight) c.EndFight(false);
            yield return null;
            c.CloseFight();
            yield return PlaceDriver.Frames(4);
        }
    }
}
