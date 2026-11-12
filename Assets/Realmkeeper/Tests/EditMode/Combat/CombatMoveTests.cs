using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>
    /// Combat Overhaul: the route planner behind point-and-click movement. It goes round walls and shut doors, never cuts
    /// a corner, counts difficult terrain, refuses squares it cannot reach with the reason, and says when Dash is needed.
    /// Every route it proposes is one the engine accepts, and the engine still deducts the movement: two moves in a turn,
    /// Dash, and no free movement from planning.
    /// </summary>
    public sealed class CombatMoveTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            CombatTests.Create(_s, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>A drawn fight: P the player, B a bandit, '#' wall, 'D' a closed door, '~' difficult terrain. It is the player's turn.</summary>
        private (CombatEngine Engine, CombatantData Player, CombatantData Bandit) Fight(params string[] rows)
        {
            var w = _s.CurrentWorldSnapshotForTests();
            var setup = new EncounterSetup { Name = "Drawn", Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal };
            var door = new EncounterDoorData { Id = "d1", Name = "door" };
            for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < rows[y].Length; x++)
                {
                    var c = rows[y][x];
                    if (c == 'P') setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player });
                    if (c == 'B') setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Name = "Bandit", Side = "foes", Position = new GridPos(x, y) });
                    if (c == '#') setup.Blocked.Add(new GridPos(x, y));
                    if (c == 'D') door.Squares.Add(new GridPos(x, y));
                    if (c == '~') setup.DifficultTerrain.Add(new GridPos(x, y));
                }

            for (var x = -1; x <= rows[0].Length; x++) { setup.Blocked.Add(new GridPos(x, -1)); setup.Blocked.Add(new GridPos(x, rows.Length)); }
            for (var y = 0; y < rows.Length; y++) { setup.Blocked.Add(new GridPos(-1, y)); setup.Blocked.Add(new GridPos(rows[0].Length, y)); }
            if (door.Squares.Count > 0) setup.Doors.Add(door);
            var enc = CombatEngine.Create(w, setup, _ => Guid.NewGuid(), out var errors);
            Assert.That(errors, Is.Empty, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var engine = new CombatEngine(w, enc);
            engine.Begin();
            var player = enc.Combatants.Single(c => c.Kind == CombatantKind.Player);
            enc.TurnIndex = enc.Order.IndexOf(player.CombatantId); // the player's turn, fresh
            return (engine, player, enc.Combatants.Single(c => c.Kind == CombatantKind.Creature));
        }

        private static void Walk(CombatEngine engine, CombatantData who, MovePlan plan)
        {
            var r = engine.Act(who.CombatantId, new MoveAction(plan.Path.ToArray()));
            Assert.That(r.Ok, Is.True, r.Error);
        }

        [Test]
        public void ARoute_GoesRoundAWall_AndTheEngineAcceptsIt()
        {
            var (engine, player, _) = Fight(
                "......",
                "P.#...",
                "..#...",
                "..#.x.",
                "......",
                "....B.");
            var plan = CombatPaths.Plan(engine, player, new GridPos(4, 1));
            Assert.That(plan.Problem, Is.Null);
            Assert.That(plan.Path.Any(p => engine.IsBlocked(p)), Is.False, "Never through the wall.");
            Assert.That(plan.Path.Last(), Is.EqualTo(new GridPos(4, 1)));
            Assert.That(plan.Cost, Is.EqualTo(plan.Path.Count * 5));
            Assert.That(engine.Unavailable(player, new MoveAction(plan.Path.ToArray())), Is.Null, "Every planned route is one the rules accept.");
            Walk(engine, player, plan);
            Assert.That(player.Position, Is.EqualTo(new GridPos(4, 1)));
            Assert.That(engine.MovementLeft(player), Is.EqualTo(engine.Speed(player) - plan.Cost));
        }

        [Test]
        public void ADiagonal_NeverCutsAWallsCorner()
        {
            var (engine, player, _) = Fight(
                "P#....",
                "......",
                "....B.");
            var plan = CombatPaths.Plan(engine, player, new GridPos(2, 0));
            Assert.That(plan.Problem, Is.Null);
            Assert.That(plan.Path.Contains(new GridPos(1, 0)), Is.False);
            var squeezes = plan.Path.Zip(plan.Path.Skip(1), (a, b) => (a, b)).Prepend((player.Position, plan.Path[0]))
                .Count(s => s.Item1.X != s.Item2.X && s.Item1.Y != s.Item2.Y && (engine.IsBlocked(new GridPos(s.Item2.X, s.Item1.Y)) || engine.IsBlocked(new GridPos(s.Item1.X, s.Item2.Y))));
            Assert.That(squeezes, Is.EqualTo(0));
        }

        [Test]
        public void DifficultTerrain_CostsDouble_AndTheRouteAvoidsItWhenThatIsCheaper()
        {
            var (engine, player, _) = Fight(
                "P~~~.",
                ".....",
                "....B");
            var plan = CombatPaths.Plan(engine, player, new GridPos(4, 0));
            Assert.That(plan.DifficultSquares, Is.EqualTo(0), "Round the mud, not through it.");
            Assert.That(plan.Cost, Is.EqualTo(20));
            var into = CombatPaths.Plan(engine, player, new GridPos(2, 0));
            Assert.That(into.DifficultSquares, Is.EqualTo(1));
            Assert.That(into.Cost, Is.EqualTo(15), "One plain step, then the mud square at 10 ft.");
        }

        [Test]
        public void AShutDoor_IsNoWay_UntilItIsOpened()
        {
            var (engine, player, _) = Fight(
                "..#....",
                ".PD....",
                "..#..B.");
            var shut = CombatPaths.Plan(engine, player, new GridPos(4, 1));
            Assert.That(shut.Problem, Does.Contain("no way"));
            Assert.That(CombatPaths.Plan(engine, player, new GridPos(2, 1)).Problem, Does.Contain("shut door"), "Clicking the door itself says so.");
            Assert.That(engine.Act(player.CombatantId, new DoorAction("d1")).Ok, Is.True);
            var open = CombatPaths.Plan(engine, player, new GridPos(4, 1));
            Assert.That(open.Problem, Is.Null);
            Assert.That(open.Path.Contains(new GridPos(2, 1)), Is.True);
        }

        [Test]
        public void AnEnemysSquare_IsRefused_WithItsName_AndEnemiesBlockTheWay()
        {
            var (engine, player, bandit) = Fight(
                "P.B.");
            Assert.That(CombatPaths.Plan(engine, player, bandit.Position).Problem, Does.Contain("Bandit stands there"));
            Assert.That(CombatPaths.Plan(engine, player, new GridPos(3, 0)).Problem, Does.Contain("no way"), "One row: the bandit is in the way.");
        }

        [Test]
        public void ARestrainedFighter_CannotMove_AndIsToldWhy()
        {
            var (engine, player, _) = Fight(
                "P....B");
            player.Conditions.Add(new ConditionInstance { Type = ConditionType.Restrained });
            Assert.That(CombatPaths.Plan(engine, player, new GridPos(2, 0)).Problem, Does.Contain("restrained"));
        }

        [Test]
        public void TwoMovesInOneTurn_EachDeductedOnce_ThenDash_AddsWhatIsNeeded_Once()
        {
            var (engine, player, _) = Fight(
                "P..................",
                "...................",
                "..................B");
            var speed = engine.Speed(player);
            Assert.That(speed, Is.EqualTo(30));

            var first = CombatPaths.Plan(engine, player, new GridPos(2, 0));
            Assert.That(first.Reachable, Is.True);
            Walk(engine, player, first);
            Assert.That(engine.MovementLeft(player), Is.EqualTo(20));

            // Planning does not spend anything.
            for (var i = 0; i < 5; i++) CombatPaths.Plan(engine, player, new GridPos(5, 0));
            Assert.That(engine.MovementLeft(player), Is.EqualTo(20));

            var second = CombatPaths.Plan(engine, player, new GridPos(5, 0));
            Walk(engine, player, second);
            Assert.That(engine.MovementLeft(player), Is.EqualTo(5), "Twice in one turn, each move deducted once.");
            Walk(engine, player, CombatPaths.Plan(engine, player, new GridPos(6, 0)));
            Assert.That(engine.MovementLeft(player), Is.EqualTo(0), "The last 5 ft can be used too.");

            var far = CombatPaths.Plan(engine, player, new GridPos(12, 0));
            Assert.That(far.NeedsDash, Is.True);
            Assert.That(far.DashReaches, Is.True, "30 ft away: Dash's 30 ft is enough.");
            Assert.That(far.ReachableSteps, Is.EqualTo(0));
            Assert.That(engine.Unavailable(player, new MoveAction(far.Path.ToArray())), Does.Contain("needs 30 ft"), "Without Dash the engine refuses it.");

            Assert.That(engine.Act(player.CombatantId, new DashAction()).Ok, Is.True);
            var dashed = CombatPaths.Plan(engine, player, new GridPos(12, 0));
            Assert.That(dashed.Reachable, Is.True);
            Walk(engine, player, dashed);
            Assert.That(engine.MovementLeft(player), Is.EqualTo(0));

            var further = CombatPaths.Plan(engine, player, new GridPos(14, 0));
            Assert.That(further.NeedsDash && !further.DashReaches, Is.True, "The action is spent: no second Dash.");
            Assert.That(engine.Act(player.CombatantId, new DashAction()).Ok, Is.False);
        }

        [Test]
        public void LeavingAnEnemysReach_IsShownAsAnOpportunityAttack()
        {
            var (engine, player, _) = Fight(
                "..PB.",
                ".....");
            Assert.That(CombatPaths.Plan(engine, player, new GridPos(2, 1)).Provokes, Is.Empty, "Still beside the bandit: no attack.");
            var away = CombatPaths.Plan(engine, player, new GridPos(0, 0));
            Assert.That(away.Provokes.Contains("Bandit"), Is.True, "Stepping away leaves its reach.");
            player.Turn.Disengaged = true;
            Assert.That(CombatPaths.Plan(engine, player, new GridPos(0, 0)).Provokes, Is.Empty, "Disengaged: nothing provoked.");
        }

        [Test]
        public void ThePlan_IsTheSame_EveryTime()
        {
            var (engine, player, _) = Fight(
                "P..#....",
                "...#....",
                "........",
                ".......B");
            var a = CombatPaths.Plan(engine, player, new GridPos(5, 0));
            var b = CombatPaths.Plan(engine, player, new GridPos(5, 0));
            Assert.That(a.Path, Is.EqualTo(b.Path));
            Assert.That(a.Cost, Is.EqualTo(b.Cost));
        }

        [Test]
        public void TheSession_PlansWithoutChangingTheWorld_AndRefusesWhenItIsNotYourTurn()
        {
            Assert.That(_s.PlanPlayerMove(new GridPos(1, 1)).Problem, Does.Contain("no fight"));
            Realmkeeper.Exploration.Application.ExplorationModule.Register(_s);
            CombatTests.EnterTown(_s);
            var started = _s.Dispatch(new StartPracticeEncounterCommand(true, false));
            Assert.That(started.IsSuccess, Is.True, started.Message);
            var before = Newtonsoft.Json.Linq.JObject.Parse(new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Serialize(_s.CurrentWorldSnapshotForTests()))["world"].ToString();
            var snap = _s.GetEncounter();
            var plan = _s.PlanPlayerMove(new GridPos(snap.Order.First(c => c.Kind == CombatantKind.Player).Position.X + 1, snap.Order.First(c => c.Kind == CombatantKind.Player).Position.Y));
            if (!snap.IsPlayersTurn) Assert.That(plan.Problem, Does.Contain("not your turn"));
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Serialize(_s.CurrentWorldSnapshotForTests()))["world"].ToString(), Is.EqualTo(before), "Planning changes nothing.");
        }
    }
}
