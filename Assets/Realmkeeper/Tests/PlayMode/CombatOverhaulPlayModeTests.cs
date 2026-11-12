using System.Collections;
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
    /// The combat overhaul in the shipped scene: the HUD leaves the battlefield clear; moving is a click on the ground
    /// along a planned, previewed route, twice in a turn; a move that needs Dash asks first and spends nothing until the
    /// player agrees (and nothing when they cancel); a square that cannot be reached says why; the bar's groups, End turn,
    /// the collapsed log and the optional grid; and what happens shows in the world, once.
    /// </summary>
    public sealed class CombatOverhaulPlayModeTests
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

        /// <summary>A practice fight, stepped to the player's first turn.</summary>
        private static IEnumerator ToPlayersTurn(ExplorationController c)
        {
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.AutoAi = false;
            c.MouseDrivesCombat = false; // previews and clicks through the API, as a player's cursor would send them
            Assert.That(c.StartPracticeFight().IsSuccess, Is.True);
            yield return PlaceDriver.Frames(4);
            for (var i = 0; i < 12 && !c.Combat.Snapshot.IsPlayersTurn; i++) { c.RunAiTurn(); yield return PlaceDriver.Frames(2); }
            Assume.That(c.Combat.Snapshot.IsPlayersTurn, Is.True);
        }

        /// <summary>A square near the player whose plan meets the condition (searched outwards, as a player would pick a spot).</summary>
        private static GridPos? Find(WorldSession s, GridPos from, System.Func<MovePlan, bool> ok, int min = 1, int max = 14)
        {
            for (var r = min; r <= max; r++)
                for (var dx = -r; dx <= r; dx++)
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                        var p = new GridPos(from.X + dx, from.Y + dy, from.Z);
                        if (ok(s.PlanPlayerMove(p))) return p;
                    }

            return null;
        }

        private static IEnumerator WaitWalk(ExplorationController c)
        {
            var until = Time.realtimeSinceStartup + 6f; // batch frames are short: wait on time, not frames
            while (c.PlayerWalking && Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest]
        public IEnumerator TheHud_LeavesTheBattlefieldClear_WithItsPiecesOnScreen()
        {
            var c = Controller;
            yield return ToPlayersTurn(c);
            yield return PlaceDriver.Frames(3);
            var panel = c.Hud.Root.worldBound;
            var pieces = new[] { "xk-cbt-top", "xk-cbt-bar", "xk-cbt-card", "xk-cbt-log" }.Select(n => c.Combat.Root.Q<VisualElement>(n)).ToList();
            Assert.That(pieces.All(p => p != null && p.resolvedStyle.display != DisplayStyle.None), Is.True, "Top strip, bar, card and log are shown.");
            var area = pieces.Sum(p => p.worldBound.width * p.worldBound.height);
            Assert.That(area / (panel.width * panel.height), Is.LessThan(0.30f), "Together they cover under 30 % of the screen (the old panel: about 46 %).");
            foreach (var p in pieces)
                Assert.That(panel.Contains(p.worldBound.min) && panel.Contains(p.worldBound.max - Vector2.one), Is.True, p.name + " lies within the screen.");
            Assert.That(c.Combat.GridVisible, Is.False, "The grid is not the way to move: hidden until asked for.");
            Assert.That(c.Combat.LogOpen, Is.False, "The log is collapsed by default.");
            Assert.That(c.Combat.Root.Q<VisualElement>("xk-cbt-pop").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "No list of choices until a group is opened.");
            Assert.That(c.Combat.Root.Q<Label>("xk-cbt-slot-action"), Is.Not.Null);
            Assert.That(c.Combat.Root.Q<Label>("xk-cbt-slot-bonus"), Is.Not.Null);
            Assert.That(c.Combat.Root.Q<Label>("xk-cbt-move").text, Does.Contain("ft"));

            // The grid and the log open on demand and close again.
            c.Combat.ToggleGrid();
            yield return null;
            Assert.That(c.Combat.GridVisible, Is.True);
            c.Combat.ToggleGrid();
            LevelUpDriver.Click(c.Combat.Root, "xk-cbt-log-toggle");
            yield return null;
            Assert.That(c.Combat.LogOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator ClickingTheGround_Moves_AlongThePreviewedRoute_TwiceInATurn()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return ToPlayersTurn(c);
            var start = c.Combat.Snapshot.Player.Position;
            var left = c.Combat.Snapshot.Player.MovementLeft;
            var near = Find(s, start, p => p.Reachable && p.Cost == 10 && p.Provokes.Count == 0, 2, 2) ?? Find(s, start, p => p.Reachable && p.Cost <= left - 5 && p.Provokes.Count == 0);
            Assume.That(near, Is.Not.Null, "No free square two away.");

            // Hover: the route, its cost and what is left, before anything happens.
            var plan = c.PreviewCombatMove(c.SquareCentre(near.Value) + new Vector3(0.2f, 0f, -0.2f));
            Assert.That(plan.Destination, Is.EqualTo(near.Value), "The point on the ground falls in that square.");
            Assert.That(c.Combat.HoverText, Does.Contain(plan.Cost + " ft").And.Contain("left after"));
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.positionCount >= 2), Is.True, "The route is drawn on the ground.");
            Assert.That(c.Combat.Snapshot.Player.MovementLeft, Is.EqualTo(left), "Hovering spends nothing.");

            // Click: the engine moves the player; the body walks there.
            Assert.That(c.CommitCombatMove().IsSuccess, Is.True, c.Combat.ResultText);
            Assert.That(c.Combat.Snapshot.Player.Position, Is.EqualTo(near.Value));
            Assert.That(c.Combat.Snapshot.Player.MovementLeft, Is.EqualTo(left - plan.Cost));
            Assert.That(c.PlayerWalking, Is.True, "The body walks; it does not jump.");
            yield return WaitWalk(c);
            Assert.That(Vector3.Distance(Flat(c.Player.transform.position), Flat(c.SquareCentre(near.Value))), Is.LessThan(0.2f));

            // A second move in the same turn, with what is left.
            var left2 = c.Combat.Snapshot.Player.MovementLeft;
            var next = Find(s, near.Value, p => p.Reachable && p.Cost <= left2 && p.Provokes.Count == 0) ?? Find(s, near.Value, p => p.Reachable && p.Cost <= left2);
            Assume.That(next, Is.Not.Null, "Nowhere left to go with " + left2 + " ft.");
            var cost2 = s.PlanPlayerMove(next.Value).Cost;
            Assert.That(c.CombatMoveToWorld(c.SquareCentre(next.Value)).IsSuccess, Is.True, c.Combat.ResultText);
            if (c.Combat.Snapshot.Player.State == VitalState.Conscious) Assert.That(c.Combat.Snapshot.Player.MovementLeft, Is.EqualTo(left2 - cost2), "Each move deducted once.");
            Assert.That(c.Combat.Snapshot.Player.ActionUsed, Is.False, "Moving twice costs no action.");
        }

        [UnityTest]
        public IEnumerator AMoveThatNeedsDash_AsksFirst_CancelSpendsNothing_YesDashesAndGoes()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            yield return ToPlayersTurn(c);
            var start = c.Combat.Snapshot.Player.Position;
            var far = Find(s, start, p => p.HasRoute && p.NeedsDash && p.DashReaches && p.Provokes.Count == 0, 5, 14) ?? Find(s, start, p => p.HasRoute && p.NeedsDash && p.DashReaches, 5, 14);
            Assume.That(far, Is.Not.Null, "No square in Dash range here.");
            var plan = c.PreviewCombatMove(c.SquareCentre(far.Value));
            Assert.That(c.Combat.HoverText, Does.Contain("needs Dash"));

            // Click: a question, nothing spent.
            Assert.That(c.CommitCombatMove().IsSuccess, Is.True);
            Assert.That(c.Combat.ConfirmOpen, Is.True);
            Assert.That(c.Combat.ConfirmText, Does.Contain("Dash uses your action"));
            Assert.That(c.Combat.Snapshot.Player.ActionUsed, Is.False);
            Assert.That(c.Combat.Snapshot.Player.Position, Is.EqualTo(start));

            // Cancel: still nothing spent.
            LevelUpDriver.Click(c.Combat.Root, "xk-cbt-confirm-no");
            yield return null;
            Assert.That(c.Combat.ConfirmOpen, Is.False);
            Assert.That(c.Combat.Snapshot.Player.ActionUsed, Is.False, "Cancelled: the action is still there.");

            // Again, and yes: Dash, then the move, each once.
            c.PreviewCombatMove(c.SquareCentre(far.Value));
            c.CommitCombatMove();
            c.Combat.Confirm();
            yield return null;
            Assert.That(c.Combat.Snapshot.Player.ActionUsed, Is.True, "Dash used the action.");
            if (c.Combat.Snapshot.Player.State == VitalState.Conscious && plan.Provokes.Count == 0) // an opportunity attack can stop a move
            {
                Assert.That(c.Combat.Snapshot.Player.Position, Is.EqualTo(far.Value));
                Assert.That(c.Combat.Snapshot.Player.MovementLeft, Is.EqualTo(c.Combat.Snapshot.Player.Speed * 2 - plan.Cost));
            }
            Assert.That(c.Combat.Snapshot.PlayerOptions.First(o => o.Action is DashAction).Unavailable, Is.Not.Null, "No second Dash.");
        }

        [UnityTest]
        public IEnumerator AnUnreachableSquare_SaysWhy_AndNothingMoves()
        {
            var c = Controller;
            yield return ToPlayersTurn(c);
            var s = c.Combat.Snapshot;
            var enemy = s.Order.First(o => o.HostileToPlayer && o.State == VitalState.Conscious);
            var plan = Bootstrap.Session.PlanPlayerMove(enemy.Position);
            Assert.That(plan.Problem, Does.Contain("stands there"));
            var left = s.Player.MovementLeft;
            var r = c.CombatMoveToWorld(c.SquareCentre(new GridPos(s.Player.Position.X + 40, s.Player.Position.Y)));
            Assert.That(r.IsSuccess, Is.False, "Far outside the fight.");
            Assert.That(c.Combat.Snapshot.Player.MovementLeft, Is.EqualTo(left));
            Assert.That(c.Combat.ResultText, Does.Contain("outside the fight").Or.Contain("no way").Or.Contain("too far"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBar_OpensAGroup_Attacks_AndTheWorldShowsWhatHappened_ThenEndsTheTurn()
        {
            var c = Controller;
            yield return ToPlayersTurn(c);
            LevelUpDriver.Click(c.Combat.Root, "xk-cbt-group-attacks");
            yield return null;
            Assert.That(c.Combat.OpenGroup, Is.EqualTo("attacks"));
            Assert.That(c.Combat.Root.Q<VisualElement>("xk-cbt-pop").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            // Close in on the nearest enemy if needed (clicking the ground), then attack from the pop-up.
            for (var tries = 0; tries < 3; tries++)
            {
                var s = c.Combat.Snapshot;
                var i = s.PlayerOptions.ToList().FindIndex(o => o.Action is AttackAction && o.Unavailable == null);
                if (i >= 0)
                {
                    var before = c.CombatFeedback.Count;
                    LevelUpDriver.Click(c.Combat.Root, "xk-combat-act-" + i);
                    yield return PlaceDriver.Frames(3);
                    Assert.That(c.CombatFeedback.Count, Is.GreaterThan(before), "The attack shows in the world (a number, a miss, a critical).");
                    Assert.That(c.CombatFeedback.Skip(before).Any(f => f == "Miss" || f.StartsWith("-") || f == "Critical!"), Is.True, string.Join(", ", c.CombatFeedback));
                    break;
                }

                var enemy = s.Order.Where(o => o.HostileToPlayer && o.State == VitalState.Conscious).OrderBy(o => o.Position.DistanceFeet(s.Player.Position)).First();
                var spot = Find(Bootstrap.Session, enemy.Position, p => p.Reachable, 1, 1);
                if (spot == null) break;
                c.CombatMoveToWorld(c.SquareCentre(spot.Value));
                yield return WaitWalk(c);
            }

            // The same entries are never shown twice: refreshing changes nothing.
            var shown = c.CombatFeedback.Count;
            c.Combat.Inspect(c.Combat.Snapshot.Player.Id);
            c.FocusOnActive();
            yield return PlaceDriver.Frames(2);
            Assert.That(c.CombatFeedback.Count, Is.EqualTo(shown));

            if (c.Combat.Snapshot.IsPlayersTurn)
            {
                LevelUpDriver.Click(c.Combat.Root, "xk-cbt-end");
                yield return null;
                Assert.That(c.Combat.Snapshot.IsPlayersTurn, Is.False, "End turn from the bar.");
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
