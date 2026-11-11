using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
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
    /// Evidence capture for Phase 9, not a pass/fail test: the spellbook, inspection and picks (a wizard); casting
    /// through the encounter panel — selection, targets, a spell attack, a saving throw, an area; concentration (a
    /// cleric's Bless); a class feature (a barbarian's Rage); an NPC caster; and short and long rests. Runs only when
    /// RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase9CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p9_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private IEnumerator Setup(string cls, Action<WorldSession> prepare = null)
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return MagicDriver.Enter(bootstrap, runtime, controller, cls);
            prepare?.Invoke(bootstrap.Session);
            var layout = controller.Streamer.Current;
            controller.Player.Teleport(new Realmkeeper.Domain.World.LocalPose(1.5f, 0.1f, -layout.HalfExtent + 9f, 0f));
            controller.CameraRig.SetView(0f, 28f);
            controller.CameraRig.Snap();
            for (var i = 0; i < 10; i++) yield return null;
        }

        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
        private static WorldSession Session => Object.FindAnyObjectByType<RealmkeeperBootstrap>().Session;

        [UnityTest]
        public IEnumerator Capture1_Spellbook_AndCasting()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            yield return Setup("wizard", s =>
            {
                s.Dispatch(new ChooseSpellCommand("class:spellbook", "thunderwave"));
                s.Dispatch(new ChooseSpellCommand("class:spellbook", "burning_hands"));
                s.Dispatch(new PrepareSpellsCommand(new[] { "magic_missile", "thunderwave", "burning_hands" }));
            });
            var c = Controller;
            var camera = c.CameraRig.Camera;
            c.ToggleHub();
            c.HubWindow.Select("Spells & rest");
            yield return Settle(4);
            yield return Shoot(camera, "p9_01_spellbook", "The wizard's spellbook: Intelligence, save DC and attack with their breakdowns, level 1 slots, the open spell picks, the preparation editor, and the spell list with filters.");
            c.HubWindow.InspectSpell("burning_hands");
            yield return Settle(2);
            var detail = c.HubWindow.Content.Q<VisualElement>("xk-sp-detail");
            if (detail != null) c.HubWindow.Content.Q<ScrollView>()?.ScrollTo(detail);
            (c.HubWindow.Content as ScrollView)?.ScrollTo(detail);
            yield return Settle(2);
            yield return Shoot(camera, "p9_02_spell_details", "Inspecting Burning Hands: casting time, 15-ft cone, components, duration, effects (Dexterity save, 3d6 fire, half on a save), upcasting, classes, status and SRD source.");
            (c.HubWindow.Content as ScrollView)?.ScrollTo(c.HubWindow.Content.Q<VisualElement>("xk-sp-prepare") ?? detail);
            yield return Settle(2);
            yield return Shoot(camera, "p9_03_preparation_and_picks", "Open spell picks (spellbook and cantrips left to choose) and the preparation editor: prepared spells toggled, saved with the button.");
            c.CloseWindows();
            yield return Settle(2);

            c.AutoAi = false;
            yield return MagicDriver.StartAtPlayersTurn(Session, c);
            yield return Settle(3);
            if (!c.InFight) yield break;
            var w = c.Combat;
            var actions = w.Root.Q<Foldout>("xk-combat-fold-actions");
            if (actions != null) actions.value = false; // folded away by its toggle, so the Spells section is in view
            yield return Settle(2);
            yield return Shoot(camera, "p9_06_combat_spell_selection", "The encounter panel's Spells section: each spell with its cost (cantrip, slot levels) and why it cannot be cast now; the attacks section folded away above, class features below.");
            LevelUpDriver.Click(w.Root, "xk-combat-spell-fire_bolt");
            yield return Settle(2);
            yield return Shoot(camera, "p9_07_target_selection", "The cast composer for Fire Bolt: the action, range and targets (out-of-range creatures disabled, with the reason).");
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return Settle(3);
            Log.Add("Fire Bolt: " + w.ResultText.Replace('\n', ' '));
            yield return Shoot(camera, "p9_08_spell_attack_result", "Fire Bolt resolved by the shared attack roll: d20, spell attack bonus, total against AC, damage dice — in the result and the log.");

            for (var i = 0; i < 20 && c.InFight && !(w.Snapshot.IsPlayersTurn && !w.Snapshot.Player.ActionUsed); i++)
            {
                if (w.Snapshot.IsPlayersTurn) LevelUpDriver.Click(w.Root, "xk-combat-act-" + (w.Snapshot.PlayerOptions.Count - 1));
                else LevelUpDriver.Click(w.Root, "xk-combat-continue");
                yield return Settle(1);
            }

            if (!c.InFight) yield break;
            LevelUpDriver.Click(w.Root, "xk-combat-spell-burning_hands");
            yield return Settle(2);
            var foe = w.Snapshot.Order.Where(x => x.HostileToPlayer && x.State == VitalState.Conscious).OrderBy(x => x.Position.DistanceFeet(w.Snapshot.Player.Position)).FirstOrDefault();
            if (foe != null) w.Aim(foe.Position);
            yield return Settle(2);
            yield return Shoot(camera, "p9_10_area_aim", "Burning Hands aimed on the grid: the 15-ft cone's squares shaded before casting; slot level 1.");
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return Settle(3);
            Log.Add("Burning Hands: " + w.ResultText.Replace('\n', ' '));
            yield return Shoot(camera, "p9_09_saving_throw_and_area_result", "The cone resolved: every creature in it makes its own Dexterity save against the spell DC; damage in full or halved, each line explained.");
            c.ToggleHub();
            c.HubWindow.Select("Spells & rest");
            yield return Settle(3);
            yield return Shoot(camera, "p9_05_slot_display", "Back in the Hub during the fight: the slots spent show as empty pips (1 of 2 level 1 slots left).");
            c.CloseWindows();
        }

        [UnityTest]
        public IEnumerator Capture2_Concentration_AndSheet()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            yield return Setup("cleric", s => s.Dispatch(new PrepareSpellsCommand(new[] { "bless", "cure_wounds", "guiding_bolt" })));
            var c = Controller;
            var camera = c.CameraRig.Camera;
            c.ToggleHub();
            c.HubWindow.Select("Spells & rest");
            yield return Settle(4);
            yield return Shoot(camera, "p9_04_sheet_spellcasting_section", "A cleric's spellcasting section: Wisdom, DC and attack, slots, prepared spells (3 of 4), the spell list with its filters, and the rest controls.");
            c.CloseWindows();
            c.AutoAi = false;
            yield return MagicDriver.StartAtPlayersTurn(Session, c);
            yield return Settle(3);
            if (!c.InFight) yield break;
            var w = c.Combat;
            LevelUpDriver.Click(w.Root, "xk-combat-spell-bless");
            yield return Settle(2);
            foreach (var b in w.Root.Query<Button>().ToList().Where(b => b.name.StartsWith("xk-combat-cast-target-", StringComparison.Ordinal) && b.enabledSelf && !b.ClassListContains("xk-w-choice--on")).Take(2).ToList())
                LevelUpDriver.Click(w.Root, b.name);
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return Settle(3);
            w.Inspect(w.Snapshot.Player.Id);
            yield return Settle(2);
            Log.Add("Bless: " + w.ResultText.Replace('\n', ' '));
            yield return Shoot(camera, "p9_11_concentration", "Bless under concentration: the initiative rows and the inspector show the concentration and the effect on each target (+1d4 to attacks and saves).");
        }

        [UnityTest]
        public IEnumerator Capture3_Feature_AndNpcCaster()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            yield return Setup("barbarian");
            var c = Controller;
            var camera = c.CameraRig.Camera;
            c.AutoAi = false;
            Assert.That(c.StartPracticeFight(true, true).IsSuccess, Is.True);
            yield return Settle(3);
            var w = c.Combat;
            var npcCast = false;
            for (var i = 0; i < 16 && c.InFight && !w.Snapshot.IsPlayersTurn; i++)
            {
                LevelUpDriver.Click(w.Root, "xk-combat-continue");
                yield return Settle(1);
                if (!npcCast && w.Snapshot.Log.Any(l => l.Kind == CombatLogKind.Spell && l.Text.StartsWith("Cult Fanatic", StringComparison.Ordinal)))
                {
                    npcCast = true;
                    Log.Add("NPC cast: " + w.Snapshot.Log.Last(l => l.Kind == CombatLogKind.Spell).Text);
                    yield return Shoot(camera, "p9_13_npc_spellcasting", "The Cult Fanatic (SRD caster) casting on its own turn through the same engine: its spell, the save or attack, and its slots in the inspector.");
                }
            }

            if (!c.InFight || !w.Snapshot.IsPlayersTurn) yield break;
            var rage = MagicDriver.OptionIndex(c, "Rage");
            if (rage >= 0) LevelUpDriver.Click(w.Root, "xk-combat-act-" + rage);
            yield return Settle(3);
            w.Inspect(w.Snapshot.Player.Id);
            yield return Settle(2);
            yield return Shoot(camera, "p9_12_class_feature", "Rage from the panel (a bonus action, one use spent): the effect on the barbarian, the log line, and Rage now shown unavailable with the reason.");
            if (!npcCast)
            {
                for (var i = 0; i < 30 && c.InFight && !npcCast; i++)
                {
                    if (w.Snapshot.IsPlayersTurn) LevelUpDriver.Click(w.Root, "xk-combat-act-" + (w.Snapshot.PlayerOptions.Count - 1));
                    else LevelUpDriver.Click(w.Root, "xk-combat-continue");
                    yield return Settle(1);
                    npcCast = w.Snapshot.Log.Any(l => l.Kind == CombatLogKind.Spell && l.Text.StartsWith("Cult Fanatic", StringComparison.Ordinal));
                }

                if (npcCast) yield return Shoot(camera, "p9_13_npc_spellcasting", "The Cult Fanatic (SRD caster) casting on its own turn through the same engine.");
            }
        }

        [UnityTest]
        public IEnumerator Capture4_Rests()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            yield return Setup("warlock", s =>
            {
                s.Dispatch(new SpendResourceCommand("pact_slots", 1));
                s.Dispatch(new ChangeHitPointsCommand(-4));
            });
            var c = Controller;
            var camera = c.CameraRig.Camera;
            c.ToggleHub();
            c.HubWindow.Select("Spells & rest");
            yield return Settle(3);
            LevelUpDriver.Click(c.HubWindow.Content, "xk-rest-dice-plus");
            yield return Settle(1);
            LevelUpDriver.Click(c.HubWindow.Content, "xk-rest-short");
            yield return Settle(3);
            (c.HubWindow.Content as ScrollView)?.ScrollTo(c.HubWindow.Content.Q<Button>("xk-rest-short"));
            yield return Settle(2);
            Log.Add("Short rest: " + c.LastRest?.Message + " " + string.Join(" ", c.LastRest?.Lines ?? new string[0]));
            yield return Shoot(camera, "p9_14_short_rest", "After a short rest (one hit die spent): an hour passed, hit points regained, the pact slot back; the rest recorded.");
            LevelUpDriver.Click(c.HubWindow.Content, "xk-rest-long");
            yield return Settle(3);
            (c.HubWindow.Content as ScrollView)?.ScrollTo(c.HubWindow.Content.Q<Button>("xk-rest-long"));
            yield return Settle(2);
            Log.Add("Long rest: " + c.LastRest?.Message + " " + string.Join(" ", c.LastRest?.Lines ?? new string[0]));
            yield return Shoot(camera, "p9_15_long_rest", "After a long rest: eight hours passed, everything restored, and the long rest now unavailable for 24 hours (the reason shown).");
        }

        private static IEnumerator Settle(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
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
