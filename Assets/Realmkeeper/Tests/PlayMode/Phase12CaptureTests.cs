using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// <summary>
    /// Evidence capture for Phase 12, not a pass/fail test: the vertical slice "Short Measures" played through the shipped
    /// UI over a live world — the conversation and its choices, the offer, the journal and the quest's detail, the ledger in
    /// the street, a skill check, the delivery, the reward, reputation, the giver's changed greeting, the Chronicle, and the
    /// completed quest. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase12CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "p12_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Capture1_ShortMeasures_TheWholeSequence()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600, ("grain_sack", 4, null));
            var id = s.FindQuestInstance(QuestCatalog.GranaryShortfall);
            Assert.That(id, Is.Not.Null);
            var b = s.GetQuestBindings(id);
            QuestDriver.Role(s, id, "giver");
            var giver = QuestDriver.Role(s, id, "giver");
            var keeper = QuestDriver.Role(s, id, "keeper");
            var suspect = QuestDriver.Role(s, id, "suspect");
            Log.Add($"Quest {id}: giver {s.FindNpc(giver)?.Name}, keeper {s.FindNpc(keeper)?.Name}, suspect {s.FindNpc(suspect)?.Name}; missing grain recorded in the Chronicle when the quest came to be.");
            void Do(CommandResult r) => Log.Add((r.IsSuccess ? "ok: " : "refused: ") + r.Message);

            // Stand the player before the giver if they are in the street.
            var camera = c.CameraRig.Camera;
            if (c.Residents.TryGetValue(giver, out var resident))
            {
                var p = resident.transform.position;
                var yaw = Mathf.Atan2(p.x - (p.x - 2.2f), p.z - (p.z - 2.2f)) * Mathf.Rad2Deg;
                c.Player.Teleport(new LocalPose(p.x - 2.2f, p.y, p.z - 2.2f, yaw));
                c.CameraRig.SetView(yaw, 14f);
                c.CameraRig.Snap();
                yield return Settle(10);
            }

            Do(c.StartDialogue(giver));
            yield return Settle();
            yield return Shoot(camera, "p12_01_conversation", $"Talking to {c.Dialogue.View.Name}: who they are, how they regard the player ({DialogueWindow.Tier(c.Dialogue.View.Tier)}), their greeting by mood and activity, and the responses — the quest hook first.");
            QuestDriver.Say(c, "ask");
            yield return Settle(2);
            QuestDriver.Say(c, "ask_settlement");
            yield return Settle();
            yield return Shoot(camera, "p12_02_dialogue_choices", "Asked about the place: the answer comes from the person's own knowledge of the stores (Phase 4), and the topics stay open.");
            QuestDriver.Say(c, "back");
            yield return Settle(2);
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return Settle();
            yield return Shoot(camera, "p12_03_quest_offer", "The offer: the giver puts the real problem (the measures missing from the stores) and the player may accept, negotiate (a Persuasion check, shown before choosing) or decline.");
            QuestDriver.Say(c, "accept");
            yield return Settle(2);
            QuestDriver.Say(c, "ok");
            yield return Settle(2);

            c.ToggleHub();
            c.HubWindow.SelectJournal("Active", id);
            yield return Settle();
            yield return Shoot(camera, "p12_04_journal_active", "The Journal: the quest under Active with its next step; its detail shows only the objectives the player knows (the later ones are hidden), the people involved and the rewards promised.");
            c.CloseWindows();

            // The ledger in the street.
            var ledger = c.Interactables.First(i => i.InteractableKind == InteractableKind.GranaryLedger);
            var lp = ledger.transform.position;
            c.Player.Teleport(new LocalPose(lp.x - 2f, lp.y, lp.z - 2f, 45f));
            c.CameraRig.SetView(45f, 12f);
            c.CameraRig.Snap();
            yield return Settle(8);
            var answer = ledger.Interact(s, c.Player.transform);
            c.Hud.ShowResponse(answer);
            c.AfterQuestCommand();
            yield return Settle(2);
            yield return Shoot(camera, "p12_05_world_interaction", "Examining the granary ledger in the street: the stores, and the shortfall in a hand that is not the keeper's. The objective completes; the discovery is recorded.");

            c.StartDialogue(keeper);
            yield return Settle(2);
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return Settle(2);
            QuestDriver.Say(c, "insight");
            yield return Settle();
            yield return Shoot(camera, "p12_06_skill_check", $"An Insight check with the keeper, rolled with the world's dice: {c.Dialogue.LastMessage} {c.Dialogue.View.Transcript.LastOrDefault().Text}");
            QuestDriver.Say(c, "thanks");
            yield return Settle(2);
            c.StartDialogue(suspect);
            yield return Settle(2);
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return Settle(2);
            QuestDriver.Say(c, "press");
            yield return Settle();
            yield return Shoot(camera, "p12_07_confrontation", "The suspect cornered: three ways to end it — persuade, intimidate (tagged as a threat), or take the silver — besides making the loss good.");
            c.EndDialogue();
            c.ToggleHub();
            c.HubWindow.SelectJournal("Active", id);
            yield return Settle();
            yield return Shoot(camera, "p12_08_quest_progress", "The quest's detail now: the ledger and the keeper done, the suspect found, the alternative endings revealed by what the player learned.");
            c.CloseWindows();

            c.StartDialogue(keeper);
            yield return Settle(2);
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return Settle(2);
            QuestDriver.Say(c, "sacks");
            yield return Settle(2);
            c.StartDialogue(giver);
            yield return Settle(2);
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return Settle(2);
            QuestDriver.Say(c, "report_replaced");
            yield return Settle(4);
            yield return Shoot(camera, "p12_09_quest_reward", $"The report: {c.LastQuestNotice} Experience and coin through the existing ledger and purse; the stores made whole.");

            c.ToggleHub();
            c.HubWindow.SelectJournal("Reputation");
            yield return Settle();
            yield return Shoot(camera, "p12_10_reputation", "Reputation: the town's standing, the giver's gratitude and trust as separate dimensions, and the latest changes with their reasons.");
            c.HubWindow.SelectJournal("Events");
            yield return Settle();
            yield return Shoot(camera, "p12_11_chronicle", "The Chronicle as the player knows it: the grain going missing, the quest offered, accepted, the discovery, the delivery and the completion.");
            c.HubWindow.SelectJournal("Completed", id);
            yield return Settle();
            yield return Shoot(camera, "p12_12_completed_quest", "The completed quest: every objective, the ending chosen, the history with world times.");
            c.CloseWindows();

            c.StartDialogue(giver);
            yield return Settle();
            yield return Shoot(camera, "p12_13_npc_reaction", $"The giver afterwards: \"{c.Dialogue.View.NpcLine}\" — {c.Dialogue.View.Mood}.");
            c.EndDialogue();
        }

        private IEnumerator Shoot(Camera camera, string name, string note)
        {
            Log.Add(name + ": " + note);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(s => (s, s.targetTexture, s.clearColor)).ToList();
            foreach (var s in settings)
            {
                s.targetTexture = rt;
                s.clearColor = false;
            }

            var cameraTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = cameraTarget;
            yield return null;
            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            foreach (var (s, target, clear) in previous)
            {
                s.targetTexture = target;
                s.clearColor = clear;
            }

            rt.Release();
            Object.Destroy(rt);
        }
    }
}
