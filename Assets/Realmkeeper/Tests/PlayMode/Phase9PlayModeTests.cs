using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Shared steps for Phase 9 tests and captures: a character of a class in the town, and the fight brought to the player's turn.</summary>
    internal static class MagicDriver
    {
        public static IEnumerator Enter(RealmkeeperBootstrap bootstrap, RealmkeeperRuntime runtime, ExplorationController controller, string cls) =>
            CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller, cls);

        /// <summary>
        /// Starts the practice fight and brings it to the player's turn with the player standing. A level-1 caster
        /// (8 hit points) can be dropped by the bandit archer's opening shot before acting; then the fight is called off
        /// and the character recovers the in-game way (a hit die on a short rest, a day passing, a long rest) before the
        /// next try. The dice are not touched.
        /// </summary>
        public static IEnumerator StartAtPlayersTurn(WorldSession session, ExplorationController controller)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                Assert.That(controller.StartPracticeFight().IsSuccess, Is.True);
                yield return null;
                var w = controller.Combat;
                for (var i = 0; i < 12 && controller.InFight && !w.Snapshot.IsPlayersTurn && w.Snapshot.Player.Hp > 0; i++)
                {
                    LevelUpDriver.Click(w.Root, "xk-combat-continue");
                    yield return null;
                }

                if (controller.InFight && w.Snapshot.IsPlayersTurn && w.Snapshot.Player.Hp > 0) yield break;
                if (controller.InFight) LevelUpDriver.Click(w.Root, "xk-combat-calloff");
                yield return null;
                LevelUpDriver.Click(w.Root, "xk-combat-close");
                yield return null;
                Assume.That(controller.PlayerIsDead, Is.False, "The character died before acting; nothing left to test.");
                if (session.GetCharacter().Player.Rpg.CurrentHp < 1) Assert.That(session.ShortRest(1).IsSuccess, Is.True, "A hit die on a short rest.");
                Assert.That(session.Execute(new AdvanceTimeCommand(17 * 60)).IsSuccess, Is.True);
                Assert.That(session.LongRest().IsSuccess, Is.True, "A long rest restores the character.");
            }

            Assert.Fail("The player never reached a turn standing in six practice fights.");
        }

        public static int OptionIndex(ExplorationController controller, string label)
        {
            var o = controller.Combat.Snapshot.PlayerOptions;
            for (var i = 0; i < o.Count; i++) if (o[i].Label.StartsWith(label, System.StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    /// <summary>
    /// Phase 9 in the shipped scene, through the real UI: the Hub's spellbook (filters, inspection, preparation), a
    /// spell cast through the encounter panel's composer (targets, slot, area aimed on the grid), a class feature from
    /// the panel, rests from the Hub with the recovered resources shown, and spellcasting state across save and load.
    /// </summary>
    public sealed class Phase9PlayModeTests
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
        public IEnumerator Spellbook_FiltersInspectsAndPrepares()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return MagicDriver.Enter(bootstrap, runtime, controller, "wizard");
            Assert.That(session.Dispatch(new ChooseSpellCommand("class:spellbook", "burning_hands")).IsSuccess, Is.True);

            controller.ToggleHub();
            yield return null;
            var hub = controller.HubWindow;
            LevelUpDriver.Click(hub.Root, "xk-hub-tab-Spells & rest");
            yield return null;
            var c = hub.Content;
            Assert.That(c.Q<VisualElement>("xk-sp-stats"), Is.Not.Null, "Ability, save DC and attack.");
            Assert.That(c.Q<Label>("xk-sp-slot-1").text, Does.Contain("2/2"));
            Assert.That(c.Q<Button>("xk-sp-spell-fire_bolt"), Is.Not.Null, "The chosen cantrip is listed.");

            // Filters: level and search.
            LevelUpDriver.Click(c, "xk-sp-level-0");
            yield return null;
            Assert.That(hub.Content.Q<Button>("xk-sp-spell-magic_missile"), Is.Null, "Cantrips only.");
            LevelUpDriver.Click(hub.Content, "xk-sp-level-all");
            hub.SetSpellFilter(search: "burn");
            yield return null;
            Assert.That(hub.Content.Q<Button>("xk-sp-spell-burning_hands"), Is.Not.Null);
            Assert.That(hub.Content.Q<Button>("xk-sp-spell-fire_bolt"), Is.Null);
            hub.SetSpellFilter(search: string.Empty);
            yield return null;

            // Inspect.
            LevelUpDriver.Click(hub.Content, "xk-sp-spell-burning_hands");
            yield return null;
            var detail = hub.Content.Q<VisualElement>("xk-sp-detail");
            Assert.That(detail, Is.Not.Null);
            var text = string.Join(" ", detail.Query<Label>().ToList().Select(l => l.text));
            Assert.That(text, Does.Contain("15-ft cone").And.Contain("Dexterity save").And.Contain("Functional").And.Contain("SRD 5.2.1"));

            // Prepare from the book.
            LevelUpDriver.Click(hub.Content, "xk-sp-prep-burning_hands");
            yield return null;
            LevelUpDriver.Click(hub.Content, "xk-sp-prep-magic_missile");
            yield return null;
            LevelUpDriver.Click(hub.Content, "xk-sp-prepare-save");
            yield return null;
            Assert.That(session.GetSpellbook().Prepared, Is.EquivalentTo(new[] { "burning_hands", "magic_missile" }));
        }

        [UnityTest]
        public IEnumerator Combat_CastsASpellAttack_AndAnAreaSpell_ThroughTheComposer()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return MagicDriver.Enter(bootstrap, runtime, controller, "wizard");
            Assert.That(session.Dispatch(new ChooseSpellCommand("class:spellbook", "thunderwave")).IsSuccess, Is.True);
            Assert.That(session.Dispatch(new PrepareSpellsCommand(new[] { "magic_missile", "thunderwave" })).IsSuccess, Is.True);
            controller.AutoAi = false;
            yield return MagicDriver.StartAtPlayersTurn(session, controller);
            var w = controller.Combat;
            Assume.That(controller.InFight, Is.True);
            Assert.That(w.Snapshot.IsPlayersTurn, Is.True, "Brought to the player's turn; active: " + w.Snapshot.Active?.Name);
            Assert.That(w.Snapshot.PlayerSpells.Count, Is.GreaterThan(0), "The wizard has spells ready; cantrips known: " + session.GetSpellbook().Cantrips.Count);
            Assert.That(w.Root.Q<Foldout>("xk-combat-fold-spells"), Is.Not.Null, "The panel lists spells.");
            w.Root.Q<Foldout>("xk-combat-fold-actions").value = false; // the player folds the attacks away

            // A cantrip spell attack.
            LevelUpDriver.Click(w.Root, "xk-combat-spell-fire_bolt");
            yield return null;
            Assert.That(w.Composing, Is.True);
            var target = w.Root.Query<Button>().ToList().First(b => b.name.StartsWith("xk-combat-cast-target-Bandit", System.StringComparison.Ordinal) && b.enabledSelf);
            if (!target.ClassListContains("xk-w-choice--on")) LevelUpDriver.Click(w.Root, target.name);
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return null;
            Assert.That(w.Snapshot.Log.Any(l => l.Kind == CombatLogKind.Spell && l.Text.Contains("casts Fire Bolt")), Is.True);
            Assert.That(w.Snapshot.Log.Any(l => l.Kind == CombatLogKind.Attack && l.Text.Contains("Fire Bolt")), Is.True, "Resolved by the shared attack roll.");
            Assert.That(w.ResultText, Does.Contain("Fire Bolt"));
            var folded = w.Root.Q<Foldout>("xk-combat-fold-actions");
            if (folded != null) Assert.That(folded.value, Is.False, "A folded section stays folded when the panel refreshes.");

            // Next turn: Thunderwave, aimed on the grid, spends a slot.
            for (var i = 0; i < 20 && controller.InFight && !(w.Snapshot.IsPlayersTurn && !w.Snapshot.Player.ActionUsed); i++)
            {
                if (w.Snapshot.IsPlayersTurn) LevelUpDriver.Click(w.Root, "xk-combat-act-" + (w.Snapshot.PlayerOptions.Count - 1));
                else LevelUpDriver.Click(w.Root, "xk-combat-continue");
                yield return null;
            }

            Assume.That(controller.InFight, Is.True);
            var slots = session.GetSpellbook().Slots.First(x => x.level == 1).left;
            LevelUpDriver.Click(w.Root, "xk-combat-spell-thunderwave");
            yield return null;
            var me = w.Snapshot.Player.Position;
            w.Aim(new GridPos(me.X, me.Y + 3));
            yield return null;
            Assert.That(w.Root.Query<Button>(className: "xk-combat-cell--area").ToList().Count, Is.EqualTo(9), "The 15-ft cube is previewed on the grid.");
            LevelUpDriver.Click(w.Root, "xk-combat-cast-confirm");
            yield return null;
            Assert.That(w.Snapshot.Log.Any(l => l.Text.Contains("casts Thunderwave")), Is.True, w.ResultText);
            Assert.That(session.GetSpellbook().Slots.First(x => x.level == 1).left, Is.EqualTo(slots - 1), "A level 1 slot spent.");
        }

        [UnityTest]
        public IEnumerator Combat_ActivatesAClassFeature_FromThePanel()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return MagicDriver.Enter(bootstrap, runtime, controller, "barbarian");
            controller.AutoAi = false;
            yield return MagicDriver.StartAtPlayersTurn(session, controller);
            Assume.That(controller.InFight, Is.True);
            var rage = MagicDriver.OptionIndex(controller, "Rage");
            Assert.That(rage, Is.GreaterThanOrEqualTo(0), "Rage is offered with the bonus actions.");
            var uses = session.GetCharacter().Player.Rpg.Resources["rage"];
            LevelUpDriver.Click(controller.Combat.Root, "xk-combat-act-" + rage);
            yield return null;
            Assert.That(session.GetCharacter().Player.Rpg.Resources["rage"], Is.EqualTo(uses - 1));
            Assert.That(controller.Combat.Snapshot.Player.Effects, Has.Some.Contains("Rage"));
            controller.Combat.Inspect(controller.Combat.Snapshot.Player.Id);
            Assert.That(controller.Combat.InspectorText, Does.Contain("Effects: Rage"), "The inspector names the effect.");
            Assert.That(controller.Combat.Root.Q<VisualElement>("xk-combat-order").Query<Button>().ToList().Any(b => b.text.Contains("Rage")), Is.True, "So does the initiative row.");
            Assert.That(controller.Combat.Snapshot.Log.Any(l => l.Kind == CombatLogKind.Feature && l.Text.Contains("rages")), Is.True);
            var again = MagicDriver.OptionIndex(controller, "Rage");
            Assert.That(controller.Combat.Snapshot.PlayerOptions[again].Unavailable, Is.Not.Null, "Shown unavailable, with the reason.");
        }

        [UnityTest]
        public IEnumerator Rests_FromTheHub_RecoverAndShowResources_AndStateSurvivesReload()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return MagicDriver.Enter(bootstrap, runtime, controller, "warlock");
            Assert.That(session.Dispatch(new SpendResourceCommand("pact_slots", 1)).IsSuccess, Is.True);
            Assert.That(session.Dispatch(new ChangeHitPointsCommand(-3)).IsSuccess, Is.True);
            controller.ToggleHub();
            yield return null;
            controller.HubWindow.Select("Spells & rest");
            yield return null;
            Assert.That(controller.HubWindow.Content.Q<Label>("xk-sp-slot-1").text, Does.Contain("0/1"));
            var minute = session.GetWorldTime().MinuteOfDay;
            LevelUpDriver.Click(controller.HubWindow.Content, "xk-rest-dice-plus");
            yield return null;
            LevelUpDriver.Click(controller.HubWindow.Content, "xk-rest-short");
            yield return null;
            Assert.That(controller.LastRest.IsSuccess, Is.True, controller.LastRest.Message);
            Assert.That(controller.HubWindow.Content.Q<Label>("xk-sp-slot-1").text, Does.Contain("1/1"), "Pact slots back after a short rest.");
            Assert.That(session.GetWorldTime().MinuteOfDay, Is.EqualTo((minute + 60) % 1440), "An hour passed.");
            Assert.That(controller.HubWindow.LastMessage, Does.Contain("Short rest"));
            LevelUpDriver.Click(controller.HubWindow.Content, "xk-rest-long");
            yield return null;
            Assert.That(controller.LastRest.IsSuccess, Is.True, controller.LastRest.Message);
            var c = session.GetCharacter();
            Assert.That(c.Player.Rpg.CurrentHp, Is.EqualTo(c.Rules.MaxHp));
            Assert.That(controller.HubWindow.Content.Q<Button>("xk-rest-long").enabledSelf, Is.False, "Once per 24 hours.");

            // Save and reload: the rest history and spellcasting state hold.
            var rests = session.GetSpellbook().Rests.Count;
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(session.GetSpellbook().Rests.Count, Is.EqualTo(rests));
            Assert.That(session.GetSpellbook().LongRestProblem, Does.Contain("24 hours"));
        }
    }
}
