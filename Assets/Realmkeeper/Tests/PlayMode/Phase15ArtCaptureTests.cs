using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
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
    /// Evidence capture for the Phase 15 art (automated, not manual verification): skeletons in their bodies with their
    /// blades, a skeleton's attack, the player's attack, hits and a death, people in a practice fight and a caster's spell,
    /// the player jumping in town, and the local-only Puglin beside the placeholder a clone would see (a staged pair).
    /// Runs only when RK_CAPTURE_DIR is set; each caption says what the picture shows and what motion was asked for.
    /// </summary>
    public sealed class Phase15ArtCaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p15_capture_log.txt"), Log);
            LocalCreatureModels.ForceMissing = false;
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

        /// <summary>The camera behind the player, looking at a point (the player does not move).</summary>
        private static IEnumerator Aim(ExplorationController c, Vector3 at, float pitch = 14f)
        {
            var from = c.Player.transform.position;
            var dir = new Vector3(at.x - from.x, 0f, at.z - from.z);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            c.CameraRig.SetView(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, pitch);
            c.CameraRig.Snap();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_Phase15Art()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);

            // The player jumping and walking in town (exploration controller).
            var driver = c.Player.GetComponentInChildren<CharacterAnimationDriver>();
            if (c.CameraRig.Perspective != CameraPerspective.ThirdPerson) c.TogglePerspective();
            c.CameraRig.SetView(c.CameraRig.Yaw + 150f, 8f);
            c.CameraRig.Snap();
            driver?.Jump();
            yield return Wait(0.35f);
            yield return Shoot(c, "p15_01_player_jump_town", "Exploration: the player's body in its jump state (Jump trigger on the extended locomotion controller).");

            // The local-only Puglin beside the placeholder a clone without it would show (a staged pair, not a fight).
            var stage = new GameObject("Staged bodies");
            var ahead = c.Player.transform.position + Quaternion.Euler(0f, c.CameraRig.Yaw, 0f) * Vector3.forward * 4f;
            var goblin = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(Guid.NewGuid()), Name = "Goblin", Kind = CombatantKind.Creature, CreatureId = "goblin" };
            var local = CombatantBodies.Create(goblin, c.Catalog, stage.transform);
            if (local != null) local.Root.transform.position = ahead + Quaternion.Euler(0f, c.CameraRig.Yaw, 0f) * Vector3.left * 1.2f;
            LocalCreatureModels.ForceMissing = true;
            var fallback = CombatantBodies.Create(goblin, c.Catalog, stage.transform);
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(placeholder.GetComponent<Collider>());
            placeholder.transform.SetParent(stage.transform, false);
            placeholder.transform.position = ahead + Quaternion.Euler(0f, c.CameraRig.Yaw, 0f) * Vector3.right * 1.2f + Vector3.up * 0.9f;
            placeholder.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            LocalCreatureModels.ForceMissing = false;
            c.CameraRig.SetView(c.CameraRig.Yaw, 10f);
            c.CameraRig.Snap();
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_02_goblin_local_vs_fallback", $"Staged: left, a goblin's body on this machine ({local?.Source ?? "none: no local Bestiary"}); right, what a clone without the local Bestiary shows (Create returned {(fallback == null ? "null, so the placeholder capsule" : fallback.Source)}).");
            Object.Destroy(stage);

            // A practice fight: people in humanoid bodies; the caster's spell.
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            Assert.That(c.StartPracticeFight(false, true).IsSuccess, Is.True); // non-lethal: the fight can run long enough for a spell
            yield return PlaceDriver.Frames(6);
            var people = string.Join("; ", c.Combat.Snapshot.Order.Where(o => o.Kind != CombatantKind.Player).Select(o => o.Name + ": " + (c.BodySourceOf(o.Id) ?? "placeholder")));
            var fanatic = c.Combat.Snapshot.Order.FirstOrDefault(o => o.CreatureId == "cult_fanatic");
            if (fanatic != null) yield return Aim(c, c.SquareCentre(fanatic.Position));
            yield return Wait(0.3f);
            yield return Shoot(c, "p15_03_practice_fight_people", "Practice fight, people in the towns' humanoid bodies: " + people);
            var cast = false;
            for (var i = 0; i < 120 && c.InFight && !cast; i++)
            {
                CombatDriver.Step(c); // the player fights back; the others take their turns
                yield return null;
                var fa = fanatic != null ? c.CombatAnimatorOf(fanatic.Id) : null;
                if (fa != null && fa.History.Contains(CombatMotion.Cast))
                {
                    cast = true;
                    yield return Aim(c, c.SquareCentre(c.Combat.Snapshot.Order.First(o => o.Id == fanatic.Id).Position));
                    yield return Wait(0.3f);
                    yield return Shoot(c, "p15_04_caster_spell", $"The Cult Fanatic casts (motions asked for: {string.Join(", ", fa.History)}; in its cast state now: {fa.InState("Cast")}). Log: \"{c.Combat.Snapshot.Log.LastOrDefault(l => l.Kind == CombatLogKind.Spell)?.Text}\"");
                }
            }

            if (!cast) Log.Add("p15_04_caster_spell: not captured — the fanatic cast no spell in the steps run");
            c.EndFight(false);
            yield return null;
            c.CloseFight();
            yield return PlaceDriver.Frames(4);

        }

        [UnityTest]
        public IEnumerator Capture_Phase15Crypt()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            c.AutoAi = false;
            c.MouseDrivesCombat = false;

            // The crypt: skeletons and zombies in their own bodies.
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p => p["discovery"] = "Discovered", ("hooded_lantern", 1, null), ("thieves_tools", 1, null));
            var crypt = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, crypt.PlaceId);
            var scene = c.PlaceScene;
            var foeRoom = scene.Figures.Where(f => f.Role == "foe").Select(f => f.Room).FirstOrDefault(r => PlaceDriver.Path(scene, scene.PlayerRoom, r) != null);
            if (foeRoom != null)
            {
                var path = PlaceDriver.Path(scene, scene.PlayerRoom, foeRoom);
                if (path.Count > 1) yield return PlaceDriver.WalkTo(c, s, path[path.Count - 2].Room);
                if (!c.InFight)
                {
                    var via = c.PlaceScene.Passages.First(p => p.Id == path[path.Count - 1].Via);
                    for (var i = 0; i < 4 && via.Kind != ConnectionKind.Opening && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Open && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Broken; i++)
                    {
                        c.ActOn(via.Id, c.PlaceScene.Passages.First(p => p.Id == via.Id).Door == DoorState.Closed ? PlaceAction.Open : PlaceAction.Force);
                        yield return PlaceDriver.Frames(2);
                    }

                    if (!c.InFight) c.StepInto(foeRoom);
                    yield return PlaceDriver.Frames(6);
                }
            }

            if (c.InFight)
            {
                var undead = c.Combat.Snapshot.Order.Where(o => o.CreatureId == "skeleton" || o.CreatureId == "zombie").ToList();
                var bodies = string.Join("; ", undead.Select(u => u.Name + ": " + c.BodySourceOf(u.Id)));
                if (undead.Count > 0) yield return Aim(c, c.SquareCentre(undead[0].Position), 12f);
                yield return Wait(0.4f);
                yield return Shoot(c, "p15_05_crypt_skeletons", "The crypt's dead in their KayKit bodies, idling (skeleton idle on the override controller): " + bodies);

                var shotAttack = false;
                var shotPlayer = false;
                var shotHit = false;
                var shotDeath = false;
                for (var step = 0; step < 120 && c.InFight && !(shotAttack && shotPlayer && shotHit && shotDeath); step++)
                {
                    var counts = c.Combat.Snapshot.Order.ToDictionary(o => o.Id, o => c.CombatAnimatorOf(o.Id)?.History.Count ?? 0);
                    CombatDriver.Step(c);
                    yield return null;
                    foreach (var o in c.Combat.Snapshot.Order)
                    {
                        var a = c.CombatAnimatorOf(o.Id);
                        if (a == null || a.History.Count == counts[o.Id]) continue;
                        var last = a.History.Last();
                        var isPlayer = o.Kind == CombatantKind.Player;
                        string name = null, note = null;
                        if (!isPlayer && last <= CombatMotion.AttackBow && !shotAttack) { shotAttack = true; name = "p15_06_skeleton_attack"; }
                        else if (isPlayer && last <= CombatMotion.AttackBow && !shotPlayer) { shotPlayer = true; name = "p15_07_player_attack"; }
                        else if (!isPlayer && last == CombatMotion.Hit && !shotHit) { shotHit = true; name = "p15_08_skeleton_hit"; }
                        if (name == null) continue;
                        yield return Aim(c, c.SquareCentre(isPlayer ? c.Combat.Snapshot.Order.First(x => x.HostileToPlayer && x.State == VitalState.Conscious || x.HostileToPlayer).Position : o.Position), 12f);
                        yield return Wait(0.3f);
                        note = $"{o.Name} ({c.BodySourceOf(o.Id) ?? "the player's body"}): motion {last}; attack/hit state now: {a.InState("Attack") || a.InState("Hit")}. Log: \"{c.Combat.Snapshot.Log.LastOrDefault(l => l.Kind == CombatLogKind.Attack)?.Text}\"";
                        yield return Shoot(c, name, note);
                        break;
                    }

                    var dead = undead.Select(u => (u, a: c.CombatAnimatorOf(u.Id))).FirstOrDefault(x => x.a != null && x.a.Dead);
                    if (!shotDeath && dead.a != null)
                    {
                        shotDeath = true;
                        yield return Aim(c, c.SquareCentre(c.Combat.Snapshot.Order.First(o => o.Id == dead.u.Id).Position), 18f);
                        yield return Wait(1.2f);
                        yield return Shoot(c, "p15_09_skeleton_death", $"{dead.u.Name} fell: in its death state {dead.a.InState("Death")} (skeleton death on the override controller), and it stays there.");
                    }
                }

                if (!shotAttack) Log.Add("p15_06_skeleton_attack: not captured in the steps run");
                if (!shotHit) Log.Add("p15_08_skeleton_hit: not captured in the steps run");
            }
            else Log.Add("crypt: the fight did not begin; no crypt shots");
        }

        [UnityTest]
        public IEnumerator Capture_Phase15Spell()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            yield return MagicDriver.Enter(Bootstrap, Runtime, c, "wizard");
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            yield return MagicDriver.StartAtPlayersTurn(s, c);
            if (!c.InFight) { Log.Add("p15_04_player_spell: no fight"); yield break; }
            var w = c.Combat;
            w.OpenGroupPopup("spells");
            yield return null;
            LevelUpDriver.Click(w.Root, "xk-combat-spell-fire_bolt");
            yield return null;
            var target = w.Root.Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("xk-combat-cast-target-Bandit", StringComparison.Ordinal) && b.enabledSelf);
            if (target == null) { Log.Add("p15_04_player_spell: no target"); yield break; }
            if (!target.ClassListContains("xk-w-choice--on")) LevelUpDriver.Click(w.Root, target.name);
            var bandit = w.Snapshot.Order.FirstOrDefault(o => o.CreatureId == "bandit");
            if (bandit != null) yield return Aim(c, c.SquareCentre(bandit.Position), 10f);
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return Wait(0.3f);
            var pa = c.CombatAnimatorOf(w.Snapshot.Player.Id);
            var spellLine = w.Snapshot.Log.LastOrDefault(l => l.Kind == CombatLogKind.Spell)?.Text;
            yield return Shoot(c, "p15_04_player_spell", "A wizard casts Fire Bolt: motions " + string.Join(", ", pa?.History ?? new List<CombatMotion>()) + "; in the cast state now: " + (pa != null && pa.InState("Cast")) + ". Log: \"" + spellLine + "\"");
        }

        /// <summary>Frame time in town with no extra bodies, then with 24 animated skeletons and 12 animated people walking.</summary>
        [UnityTest]
        public IEnumerator Capture_Phase15Performance()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to measure.");
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Wait(1f);
            float Measure(List<float> frames) => frames.Count == 0 ? 0f : frames.Sum() / frames.Count * 1000f;
            var baseline = new List<float>();
            for (var i = 0; i < 240; i++) { yield return null; baseline.Add(Time.unscaledDeltaTime); }
            var root = new GameObject("Perf bodies");
            var origin = c.Player.transform.position;
            var made = new List<CombatantAnimator>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 36; i++)
            {
                var v = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(Guid.NewGuid()), Name = "Body " + i, Kind = CombatantKind.Creature, CreatureId = i < 24 ? (i % 4 == 0 ? "zombie" : "skeleton") : "bandit" };
                var holder = new GameObject("Body " + i);
                holder.transform.SetParent(root.transform, false);
                holder.transform.position = origin + new Vector3((i % 6) * 1.6f - 4f, 0f, 3f + (i / 6) * 1.6f);
                var b = CombatantBodies.Create(v, c.Catalog, holder.transform);
                if (b != null) made.Add(b.Animator);
            }

            var build = sw.Elapsed.TotalMilliseconds;
            yield return Wait(0.5f);
            var busy = new List<float>();
            for (var i = 0; i < 240; i++)
            {
                foreach (var a in made) a.SetSpeed(i % 120 < 60 ? 1.5f : 0f);
                if (i % 40 == 0) foreach (var a in made) a.Play(CombatMotion.Attack1H);
                yield return null;
                busy.Add(Time.unscaledDeltaTime);
            }

            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Length;
            Log.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "performance: town baseline {0:0.00} ms/frame; with {1} animated bodies (24 skeletons and zombies, 12 people; walking, attacking every 40 frames) {2:0.00} ms/frame; building the {1} bodies took {3:0} ms; animators in the scene {4}; editor, windowed, {5}x{6}",
                Measure(baseline), made.Count, Measure(busy), build, animators, Screen.width, Screen.height));
            yield return Shoot(c, "p15_10_many_animated_bodies", made.Count + " animated bodies in town for the frame-time measurement (skeletons, zombies and people).");
            Object.Destroy(root);
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
        }
    }
}
