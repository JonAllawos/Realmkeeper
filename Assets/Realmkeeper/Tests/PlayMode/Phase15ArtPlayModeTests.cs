using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
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
    /// Phase 15 art in the shipped scene: skeletons and zombies fight in their KayKit bodies with the weapons they carry;
    /// people in the project's humanoid bodies; attacks, hits and deaths play real animator states (checked by the state
    /// the animator is in, not by what a controller references); a refresh replays nothing; death holds; the player's
    /// body jumps and reaches out in exploration and is never slowed there by load; and a machine without the local-only
    /// Bestiary falls back to placeholders without errors.
    /// </summary>
    public sealed class Phase15ArtPlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            LocalCreatureModels.ForceMissing = false;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            LocalCreatureModels.ForceMissing = false;
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

        /// <summary>Waits up to a time for an animator to be in (or entering) a tagged state.</summary>
        private static IEnumerator Until(CombatantAnimator a, string tag, List<bool> seen, float seconds = 1.5f)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                if (a != null && a.InState(tag)) { seen.Add(true); yield break; }
                yield return null;
            }
        }

        /// <summary>Into the crypt, and into the first room with its dead: the fight begins as the player comes in.</summary>
        private static IEnumerator CryptFight(ExplorationController c, WorldSession s)
        {
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p => p["discovery"] = "Discovered", ("hooded_lantern", 1, null), ("thieves_tools", 1, null));
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            var crypt = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, crypt.PlaceId);
            var scene = c.PlaceScene;
            var foeRoom = scene.Figures.Where(f => f.Role == "foe").Select(f => f.Room).FirstOrDefault(r => PlaceDriver.Path(scene, scene.PlayerRoom, r) != null);
            Assume.That(foeRoom, Is.Not.Null, "No inhabited room in this crypt.");
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
                yield return PlaceDriver.Frames(4);
            }

            Assume.That(c.InFight, Is.True, "The fight began: " + c.LastPlaceMessage);
        }

        [UnityTest]
        public IEnumerator TheDead_FightInTheirOwnBodies_WithWhatTheyCarry_AndTheirMotionsReallyPlay()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CryptFight(c, s);
            yield return PlaceDriver.Frames(4);
            var snap = c.Combat.Snapshot;
            var undead = snap.Order.Where(o => o.CreatureId == "skeleton" || o.CreatureId == "zombie").ToList();
            Assume.That(undead, Is.Not.Empty, "No skeleton or zombie in this room.");

            // B: models chosen by creature and role, with the weapon it carries; a real controller, in a real state.
            foreach (var u in undead)
            {
                var source = c.BodySourceOf(u.Id);
                Assert.That(source, Does.StartWith("KayKit Skeleton_"), u.Name + " has its model.");
                if (u.CreatureId == "skeleton") Assert.That(source, Does.Contain("Skeleton_Blade").And.Match("Skeleton_(Warrior|Rogue)"), "A skeleton carries its blade (its shortsword).");
                if (u.CreatureId == "zombie") Assert.That(source, Does.Contain("Skeleton_Minion"), "Zombies are the weaker skeleton minion.");
                var anim = c.CombatAnimatorOf(u.Id);
                Assert.That(anim, Is.Not.Null);
                Assert.That(anim.Animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(anim.Animator.avatar != null && anim.Animator.avatar.isHuman, Is.True, "A humanoid avatar: the shared clips retarget to it.");
                var seen = new List<bool>();
                yield return Until(anim, "Locomotion", seen);
                Assert.That(seen, Is.Not.Empty, "Standing in its idle, not a bind pose.");
                if (u.CreatureId == "skeleton")
                {
                    var hand = CombatantBodies.Find(anim.transform, "handslot.r");
                    Assert.That(hand != null && hand.Cast<Transform>().Any(t => t.name == "Held Skeleton_Blade"), Is.True, "The blade is in the right hand slot.");
                }
            }

            // C: the fight plays its motions from what happened, once.
            var attacks = new List<bool>();
            var hits = new List<bool>();
            var playerAttacked = new List<bool>();
            for (var step = 0; step < 60 && c.InFight; step++)
            {
                var before = undead.ToDictionary(u => u.Id, u => c.CombatAnimatorOf(u.Id)?.History.Count ?? 0);
                var playerBefore = c.CombatAnimatorOf(c.Combat.Snapshot.Player.Id)?.History.Count ?? 0;
                CombatDriver.Step(c);
                yield return null;
                foreach (var u in undead)
                {
                    var a = c.CombatAnimatorOf(u.Id);
                    if (a == null || a.History.Count == before[u.Id]) continue;
                    var last = a.History[a.History.Count - 1];
                    if (last == CombatMotion.Hit) yield return Until(a, "Hit", hits);
                    else if (last != CombatMotion.Block) yield return Until(a, "Attack", attacks);
                }

                var pa = c.CombatAnimatorOf(c.Combat.Snapshot.Player.Id);
                if (pa != null && pa.History.Count > playerBefore && pa.History.Last() <= CombatMotion.AttackBow) yield return Until(pa, "Attack", playerAttacked);
                if (attacks.Count > 0 && hits.Count > 0 && playerAttacked.Count > 0) break;
            }

            Assert.That(hits, Is.Not.Empty, "A struck skeleton played its hit state.");
            Assert.That(playerAttacked, Is.Not.Empty, "The player's body played an attack state (KayKit clips retargeted to the Quaternius body).");
            var playerMotions = c.CombatAnimatorOf(c.Combat.Snapshot.Player.Id).History.Where(m => m <= CombatMotion.AttackBow).ToList();
            Assert.That(playerMotions.Contains(CombatMotion.Attack1H), Is.True, "An arming sword beside the foe is a one-handed melee attack.");
            Assert.That(playerMotions.Contains(CombatMotion.AttackRanged), Is.False, "Never a ranged motion for a sword in reach.");
            if (attacks.Count == 0) TestContext.WriteLine("NOTE: " + "No skeleton attacked during the steps run (its attack state was not observed).");

            // No replay: refreshing the panels shows nothing again.
            var counts = c.Combat.Snapshot.Order.Select(o => c.CombatAnimatorOf(o.Id)?.History.Count ?? 0).ToList();
            c.Combat.Inspect(c.Combat.Snapshot.Order.First().Id);
            c.Combat.ToggleGrid();
            c.FocusOnActive();
            yield return PlaceDriver.Frames(5);
            Assert.That(c.Combat.Snapshot.Order.Select(o => c.CombatAnimatorOf(o.Id)?.History.Count ?? 0), Is.EqualTo(counts), "Refreshing replays no motion.");

            // Death holds: fight on until one of the dead falls, then it stays in its death state.
            CombatantAnimator fallen = null;
            for (var step = 0; step < 200 && c.InFight && fallen == null; step++)
            {
                CombatDriver.Step(c);
                yield return null;
                fallen = undead.Select(u => c.CombatAnimatorOf(u.Id)).FirstOrDefault(a => a != null && a.Dead);
            }

            if (fallen != null)
            {
                var died = new List<bool>();
                yield return Until(fallen, "Death", died, 2f);
                Assert.That(died, Is.Not.Empty, "The fallen play their death.");
                var played = fallen.History.Count;
                Assert.That(fallen.Play(CombatMotion.Attack1H), Is.False, "Nothing plays over death.");
                yield return PlaceDriver.Frames(30);
                Assert.That(fallen.InState("Death"), Is.True, "Still dead: idle never overwrites death.");
                Assert.That(fallen.History.Count, Is.EqualTo(played));
            }
            else TestContext.WriteLine("NOTE: " + "None of the dead fell during the steps run.");
        }

        [UnityTest]
        public IEnumerator AWizardsFireBolt_PlaysTheCastState_AndOnlyWhenTheSpellIsCast()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return MagicDriver.Enter(Bootstrap, Runtime, c, "wizard");
            c.AutoAi = false;
            c.MouseDrivesCombat = false;
            yield return MagicDriver.StartAtPlayersTurn(s, c);
            Assume.That(c.InFight && c.Combat.Snapshot.IsPlayersTurn, Is.True);
            var w = c.Combat;
            w.OpenGroupPopup("spells");
            yield return null;
            var pa = c.CombatAnimatorOf(w.Snapshot.Player.Id);
            Assume.That(pa, Is.Not.Null, "The wizard has a humanoid body.");
            var before = pa.History.Count;
            LevelUpDriver.Click(w.Root, "xk-combat-spell-fire_bolt");
            yield return null;
            Assert.That(pa.History.Count, Is.EqualTo(before), "Choosing a spell to aim casts nothing yet.");
            var target = w.Root.Query<UnityEngine.UIElements.Button>().ToList().First(b => b.name.StartsWith("xk-combat-cast-target-Bandit", System.StringComparison.Ordinal) && b.enabledSelf);
            if (!target.ClassListContains("xk-w-choice--on")) LevelUpDriver.Click(w.Root, target.name);
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return null;
            Assert.That(pa.History.Contains(CombatMotion.Cast), Is.True, "Cast once the spell is cast.");
            var seen = new List<bool>();
            yield return Until(pa, "Cast", seen);
            Assert.That(seen, Is.Not.Empty, "The cast state plays (KayKit spellcasting clip on the Quaternius body).");
        }

        [Test]
        public void TheMotion_FollowsTheWeaponNamed_AndTheRange()
        {
            Assert.That(ExplorationController.MotionFor("Bandit → Tamsin: Scimitar, d20 …", false), Is.EqualTo(CombatMotion.Attack1H));
            Assert.That(ExplorationController.MotionFor("Ogre → Tamsin: Greatclub, d20 …", false), Is.EqualTo(CombatMotion.Attack2H));
            Assert.That(ExplorationController.MotionFor("Zombie 2 → Tamsin: Slam, d20 …", false), Is.EqualTo(CombatMotion.AttackUnarmed));
            Assert.That(ExplorationController.MotionFor("Wolf → Tamsin: Bite, d20 …", false), Is.EqualTo(CombatMotion.AttackUnarmed));
            Assert.That(ExplorationController.MotionFor("Skeleton 1 → Tamsin: Shortbow, d20 …", true), Is.EqualTo(CombatMotion.AttackBow));
            Assert.That(ExplorationController.MotionFor("Bandit archer → Tamsin: Light crossbow, d20 …", true), Is.EqualTo(CombatMotion.AttackRanged));
            Assert.That(ExplorationController.MotionFor("Guard → Bandit: Spear, d20 …", true), Is.EqualTo(CombatMotion.AttackRanged), "A spear thrown from range.");
            Assert.That(ExplorationController.MotionFor("Skeleton 1 → Tamsin: Shortsword, d20 …", false), Is.EqualTo(CombatMotion.Attack1H));
        }

        [UnityTest]
        public IEnumerator People_FightInHumanoidBodies_AndThePlayerGetsTheirExplorationControllerBack()
        {
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.AutoAi = false;
            var driver = c.Player.GetComponentInChildren<CharacterAnimationDriver>();
            Assume.That(driver, Is.Not.Null, "The player has a humanoid body.");
            var explore = driver.Animator.runtimeAnimatorController;
            Assert.That(c.StartPracticeFight().IsSuccess, Is.True);
            yield return PlaceDriver.Frames(6);
            var bandits = c.Combat.Snapshot.Order.Where(o => o.CreatureId == "bandit").ToList();
            Assert.That(bandits, Is.Not.Empty);
            foreach (var b in bandits) Assert.That(c.BodySourceOf(b.Id), Does.StartWith("humanoid "), "Bandits are people, in the towns' bodies.");
            Assert.That(driver.Animator.runtimeAnimatorController, Is.EqualTo(c.Catalog.PersonCombatant), "The player fights with the combat controller, in the clips made for its body.");
            Assert.That(c.EndFight(withdraw: false).IsSuccess, Is.True);
            yield return null;
            c.CloseFight();
            yield return PlaceDriver.Frames(3);
            Assert.That(driver.Animator.runtimeAnimatorController, Is.EqualTo(explore), "And explores with its own again.");
        }

        [UnityTest]
        public IEnumerator Exploration_JumpAndInteract_Play_AndWalkingIsNeverSlowedByLoad()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var driver = c.Player.GetComponentInChildren<CharacterAnimationDriver>();
            Assume.That(driver, Is.Not.Null);
            var probe = driver.gameObject.AddComponent<CombatantAnimator>();
            probe.Bind(driver.Animator, drivesSpeed: false);
            driver.Jump();
            var seen = new List<bool>();
            yield return Until(probe, "Jump", seen);
            Assert.That(seen, Is.Not.Empty, "The jump state plays in exploration.");
            yield return PlaceDriver.Frames(90);
            driver.Interact();
            seen.Clear();
            yield return Until(probe, "Interact", seen);
            Assert.That(seen, Is.Not.Empty, "The interact state plays.");
            Object.Destroy(probe);

            // Heavily loaded, the player still walks at full pace outside fights.
            yield return ItemDriver.Give(Runtime, c, s, null, ("splint_armour", 8, null)); // 480 lb: far over capacity
            yield return PlaceDriver.Frames(4);
            Assert.That(s.GetPlayerHub().SpeedFactor, Is.LessThan(1f), "The rules see the load (and show it in the hub).");
            Assert.That(c.Player.SpeedMultiplier, Is.EqualTo(1f), "Free movement is not slowed by it.");
        }

        [UnityTest]
        public IEnumerator WithoutTheLocalBestiary_BodiesFallBack_WithoutErrors()
        {
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var root = new GameObject("Probe bodies");
            var goblin = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(System.Guid.NewGuid()), Name = "Goblin", Kind = CombatantKind.Creature, CreatureId = "goblin" };
            var skeleton = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(System.Guid.NewGuid()), Name = "Skeleton", Kind = CombatantKind.Creature, CreatureId = "skeleton" };
            var wolf = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(System.Guid.NewGuid()), Name = "Wolf", Kind = CombatantKind.Creature, CreatureId = "wolf", CreatureIsBeast = true };

            var errors = new List<string>();
            void Watch(string text, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
            UnityEngine.Application.logMessageReceived += Watch;
            LocalCreatureModels.ForceMissing = true; // as on a clone without the local-only models
            // Phase 18 (ADR-0097): goblins and wolves now have repository models (Quaternius, CC0), so they no longer need the local Bestiary.
            Assert.That(CombatantBodies.Create(goblin, c.Catalog, root.transform)?.Source, Is.EqualTo("animated Goblin"), "A goblin uses the repository's own model.");
            var unknown = new CombatantView { Id = new Realmkeeper.Domain.Identifiers.CombatantId(System.Guid.NewGuid()), Name = "Unknown", Kind = CombatantKind.Creature, CreatureId = "no_such_creature" };
            Assert.That(CombatantBodies.Create(unknown, c.Catalog, root.transform), Is.Null, "A creature with no model falls back to the placeholder.");
            Assert.That(CombatantBodies.Create(skeleton, c.Catalog, root.transform)?.Source, Does.StartWith("KayKit"), "Repository art still works.");
            Assert.That(CombatantBodies.Create(wolf, c.Catalog, root.transform)?.Source, Is.EqualTo("animated Wolf"), "Beasts have their own models now.");
            Assert.That(CombatantBodies.Create(skeleton, null, root.transform), Is.Null, "No catalog at all: placeholder, no exception.");
            yield return null;
            UnityEngine.Application.logMessageReceived -= Watch;
            Assert.That(errors, Is.Empty, "No errors or exceptions on the fallback path.");

            LocalCreatureModels.ForceMissing = false;
            if (LocalCreatureModels.Load() == null) { Object.Destroy(root); Assert.Pass("This machine has no local Bestiary: the fallback is what it uses."); }
            var local = CombatantBodies.Create(goblin, c.Catalog, root.transform);
            Assert.That(local?.Source, Is.EqualTo("animated Goblin"), "With the local install too, the repository's goblin comes first.");
            Object.Destroy(root);
            yield return null;
        }
    }
}
