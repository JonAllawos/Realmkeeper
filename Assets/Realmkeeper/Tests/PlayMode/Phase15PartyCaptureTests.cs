using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Evidence capture for Phase 15's party (automated, not manual verification): asking a person to join in
    /// conversation, the party following through town, the Party section, a companion's sheet and items, a fight with the
    /// party (their decisions explained), a companion casting a spell, and a rest taken together. Runs only when
    /// RK_CAPTURE_DIR is set; each caption says what the picture shows. A performance pass measures frame time with and
    /// without three followers.
    /// </summary>
    public sealed class Phase15PartyCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
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
            File.WriteAllLines(Path.Combine(_dir, "p15_party_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Three willing townsfolk, the first a shrine priest (a cleric), asked to join in conversation.</summary>
        private static IEnumerator Party(ExplorationController c, WorldSession s, List<NpcId> ids, Action<string, string> shoot = null)
        {
            yield return PartyDriver.Willing(Runtime, c, s, 3, ids);
            yield return ItemDriver.Give(Runtime, c, s, w =>
            {
                var priest = ((JArray)w["npcs"]).Cast<JObject>().First(n => (string)n["npcId"] == ids[0].ToString());
                priest["character"] = new JObject
                {
                    ["archetypeId"] = "priest", ["occupation"] = "shrine priest", ["body"] = "chain_shirt", ["shield"] = "shield_round", ["mainHand"] = "mace_flanged",
                    ["carried"] = new JArray("holy_symbol"), ["purse"] = 25,
                };
            });
        }

        [UnityTest]
        public IEnumerator Capture_Phase15Party()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            if (c.CameraRig.Perspective != CameraPerspective.ThirdPerson) c.TogglePerspective();
            var ids = new List<NpcId>();
            yield return Party(c, s, ids);

            // 1. Dialogue: asking the priest to join, and the answer.
            Assert.That(c.StartDialogue(ids[0]).IsSuccess, Is.True);
            yield return null;
            Assert.That(c.ChooseResponse("recruit").IsSuccess, Is.True);
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_party_01_dialogue_recruit", "Conversation: \"Travel with me\" — " + string.Join(" / ", s.GetConversation().Transcript.Skip(Math.Max(0, s.GetConversation().Transcript.Count - 3)).Select(t => t.Text)));
            c.CloseWindows();
            yield return PlaceDriver.Frames(4);
            yield return PartyDriver.Recruit(c, s, ids[1]);
            yield return PartyDriver.Recruit(c, s, ids[2]);

            // 2. Exploration with the party: walk, stop, turn the camera to see them behind.
            yield return PartyDriver.Walk(c, new Vector2(0f, 1f), 3.5f);
            yield return Wait(1.2f);
            c.CameraRig.SetView(c.Player.transform.eulerAngles.y + 160f, 12f);
            c.CameraRig.Snap();
            yield return Wait(0.3f);
            var spread = string.Join(", ", c.Companions.Values.Select(f => $"{f.DisplayName} {Vector3.Distance(f.transform.position, c.Player.transform.position):0.0} m"));
            yield return Shoot(c, "p15_party_02_exploration", $"Town: three companions following in formation (distances to the player: {spread}; recoveries {c.CompanionRecoveries}). HUD party strip at lower left.");

            // 3–5. The Party section: line-up, the priest's sheet, items.
            c.OpenParty();
            c.HubWindow.SelectedCompanion = ids[0];
            c.HubWindow.Select("Party");
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_party_03_party_ui", "Hub → Party: the line-up with HP, AC, state, loyalty, and each member's commands (wait, reserve, move up, lead, talk, part ways).");
            var scroll = c.HubWindow.Content as ScrollView;
            var detail = c.HubWindow.Content.Q<VisualElement>("xk-party-detail");
            scroll?.ScrollTo(detail);
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_party_04_companion_sheet", "The priest's sheet, made by the rules engine on joining: class, HP, AC, abilities, saves, skills, features, spells and resources; loyalty and its reasons.");
            if (scroll != null) scroll.scrollOffset = new Vector2(0f, scroll.contentContainer.layout.height); // the items and the player's pack, at the end
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_party_05_inventory", "The priest's own items (their gear became real items once, equipped where worn) beside the player's pack, with Equip / Take off / Take / Give.");
            c.CloseWindows();
            yield return PlaceDriver.Frames(4);

            // 6–7. A fight with the party; the priest's spell.
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            Assert.That(c.StartPracticeFight(false, true).IsSuccess, Is.True);
            yield return PlaceDriver.Frames(8);
            var priest = c.Combat.Snapshot.Order.First(o => o.Name == s.FindNpc(ids[0]).Name);
            c.CameraRig.SetView(c.CameraRig.Yaw, 32f);
            c.CameraRig.Snap();
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_party_06_combat", "Practice fight with the party: " + string.Join("; ", c.Combat.Snapshot.Order.Select(o => o.Name + (o.HostileToPlayer ? " (foe)" : " (ally)"))));
            var cast = false;
            for (var i = 0; i < 300 && c.InFight && !cast; i++)
            {
                CombatDriver.Step(c);
                yield return null;
                var spell = c.Combat.Snapshot.Log.LastOrDefault(l => l.Kind == CombatLogKind.Spell && l.Text.StartsWith(priest.Name));
                if (spell == null) continue;
                cast = true;
                yield return Wait(0.25f);
                var log = c.Combat.Snapshot.Log.ToList();
                var why = log.Take(log.IndexOf(spell)).LastOrDefault(l => l.Kind == CombatLogKind.Reason && l.Text.StartsWith(priest.Name));
                yield return Shoot(c, "p15_party_07_companion_spell", $"The priest casts in the fight: \"{spell.Text}\". Reason logged: \"{why?.Text}\". Motions asked of the body: {string.Join(", ", c.CombatAnimatorOf(priest.Id)?.History ?? new List<CombatMotion>())}.");
            }

            if (!cast) Log.Add("p15_party_07_companion_spell: not captured — no spell from the priest in the steps run");
            if (c.InFight) c.EndFight(false);
            yield return null;
            c.CloseFight();
            yield return PlaceDriver.Frames(6);

            // 8. A long rest together.
            Assert.That(s.Execute(new AdvanceTimeCommand(60 * 16)).IsSuccess, Is.True); // past the once-a-day limit of the morning's rest, if any
            c.ToggleHub();
            c.HubWindow.Select("Spells & rest");
            yield return null;
            LevelUpDriver.Click(c.HubWindow.Content, "xk-rest-long");
            yield return Wait(0.4f);
            yield return Shoot(c, "p15_party_08_party_rest", "Long rest together: the player's recovery and each companion's, applied once each — " + c.HubWindow.LastMessage.Replace("\n", " / "));
            c.CloseWindows();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_Phase15PartyPerformance()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Wait(1f);
            var alone = new List<double>();
            yield return Measure(c, alone);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 3, ids);
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);
            yield return Wait(1f);
            var party = new List<double>();
            yield return Measure(c, party);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 200; i++) s.GetParty();
            sw.Stop();
            Log.Add($"performance: walking in town {alone.Average():0.00} ms/frame alone, {party.Average():0.00} ms/frame with three followers (p95 {Percentile(alone, 0.95):0.00} / {Percentile(party, 0.95):0.00}); " +
                    $"GetParty {sw.Elapsed.TotalMilliseconds / 200:0.000} ms each; recoveries {c.CompanionRecoveries}");
        }

        private static IEnumerator Measure(ExplorationController c, List<double> into)
        {
            var input = new Realmkeeper.Exploration.Interaction.ScriptedExplorationInput { Move = new Vector2(0.2f, 1f) };
            c.SetInput(input);
            for (var i = 0; i < 10; i++) yield return null;
            for (var i = 0; i < 240; i++)
            {
                into.Add(Time.unscaledDeltaTime * 1000.0);
                yield return null;
                if (i % 60 == 59) input.Move = new Vector2(-input.Move.x, input.Move.y * -1f);
            }

            input.Move = Vector2.zero;
        }

        private static double Percentile(List<double> v, double p)
        {
            var sorted = v.OrderBy(x => x).ToList();
            return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
        }

        private IEnumerator Shoot(ExplorationController c, string name, string note, int width = 1920, int height = 1080)
        {
            Log.Add(name + ": " + note);
            var camera = c.CameraRig.Camera;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(x => (x, x.targetTexture, x.clearColor)).ToList();
            foreach (var x in settings) { x.targetTexture = rt; x.clearColor = false; }
            yield return null;
            var cameraTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = cameraTarget;
            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            foreach (var (x, target, clear) in previous) { x.targetTexture = target; x.clearColor = clear; }
            rt.Release();
            Object.Destroy(rt);
            Debug.Log("Captured " + name);
            yield return null;
        }
    }
}
