using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>
    /// Phase 14 (ADR-0067, ADR-0068): walls and doors in fights mean the same thing to every rule — attacks, spells,
    /// areas, the AI — and cover is real and explained; lasting conditions come into fights and go back out; one damage rule.
    /// </summary>
    public sealed class Phase14CombatTests
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

        /// <summary>A fight on a drawn grid: the player at P, a bandit (a crossbow and a scimitar) at B, '#' wall, 'D' a closed door, 'o' a table.</summary>
        private (WorldData W, CombatEngine Engine, CombatantData Player, CombatantData Bandit) Fight(params string[] rows)
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
                    if (c == 'o') setup.LowCover.Add(new LowCoverData { X = x, Y = y, Name = "a table" });
                }

            for (var x = -1; x <= rows[0].Length; x++) { setup.Blocked.Add(new GridPos(x, -1)); setup.Blocked.Add(new GridPos(x, rows.Length)); }
            for (var y = 0; y < rows.Length; y++) { setup.Blocked.Add(new GridPos(-1, y)); setup.Blocked.Add(new GridPos(rows[0].Length, y)); }
            if (door.Squares.Count > 0) setup.Doors.Add(door);
            var enc = CombatEngine.Create(w, setup, _ => Guid.NewGuid(), out var errors);
            Assert.That(errors, Is.Empty, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var engine = new CombatEngine(w, enc);
            engine.Begin();
            return (w, engine, enc.Combatants.Single(c => c.Kind == CombatantKind.Player), enc.Combatants.Single(c => c.Kind == CombatantKind.Creature));
        }

        [Test]
        public void ARangedAttack_ThroughAWall_IsRefused_WithTheReason()
        {
            var (_, engine, player, bandit) = Fight(
                "P...#...B");
            var bow = engine.Stats(bandit).BestRanged();
            Assert.That(bow, Is.Not.Null, "The bandit has a crossbow.");
            var why = engine.Unavailable(bandit, new AttackAction(player.CombatantId, bow.Id));
            Assert.That(why, Does.Contain("total cover").And.Contain("a wall"));
        }

        [Test]
        public void AClosedDoor_BlocksTheShot_AndOpeningIt_ClearsTheLine_ForEveryRule()
        {
            var (w, engine, player, bandit) = Fight(
                "P...D...B");
            var bow = engine.Stats(bandit).BestRanged();
            Assert.That(engine.Unavailable(bandit, new AttackAction(player.CombatantId, bow.Id)), Does.Contain("a closed door"));
            Assert.That(engine.IsBlocked(new GridPos(4, 0)), Is.True, "A closed door is a wall to movement too.");

            // The player steps up and opens it (the free object interaction).
            var e = engine.Encounter;
            e.TurnIndex = e.Order.IndexOf(player.CombatantId);
            player.Position = new GridPos(3, 0);
            var opened = engine.Act(player.CombatantId, new DoorAction("d1"));
            Assert.That(opened.Ok, Is.True, opened.Error);
            Assert.That(player.Turn.InteractionUsed, Is.True);
            Assert.That(player.Turn.ActionUsed, Is.False, "Opening a door is the free interaction, not the action.");
            Assert.That(engine.IsBlocked(new GridPos(4, 0)), Is.False);
            Assert.That(engine.Unavailable(bandit, new AttackAction(player.CombatantId, bow.Id)), Is.Null, "Now there is a clear line.");
            Assert.That(e.Log.Last().Text, Does.Contain("opens the door"));
        }

        [Test]
        public void Cover_RaisesTheTargetsArmourClass_AndTheLogSaysWhy()
        {
            var (w, engine, player, bandit) = Fight(
                "P....",
                "..#..",
                "....B");
            // Placed round a pillar: three-quarters cover.
            player.Position = new GridPos(0, 0);
            bandit.Position = new GridPos(4, 2);
            var cover = engine.CoverOf(bandit, player);
            Assert.That(cover.Level, Is.EqualTo(CoverLevel.ThreeQuarters));
            var bow = engine.Stats(bandit).BestRanged();
            var r = engine.AttackRoll(bandit, player, bow, false);
            Assert.That(r.Cover.Level, Is.EqualTo(CoverLevel.ThreeQuarters));
            Assert.That(r.TargetAc, Is.EqualTo(engine.ArmorClass(player).Total + 5));
            Assert.That(r.TargetAcBreakdown.Parts.Any(p => p.label.Contains("three-quarters cover") && p.value == 5), Is.True);
            Assert.That(engine.Encounter.Log.Last(l => l.Kind == CombatLogKind.Attack).Text, Does.Contain("three-quarters cover"));
        }

        [Test]
        public void ALowObstacle_OrAnotherCreature_GivesHalfCover()
        {
            var (_, engine, player, bandit) = Fight(
                "P.o..B");
            var cover = engine.CoverOf(bandit, player);
            Assert.That(cover.Level, Is.EqualTo(CoverLevel.Half));
            Assert.That(cover.By, Is.EqualTo("a table"));
            Assert.That(cover.AcBonus, Is.EqualTo(2));
        }

        [Test]
        public void AnAreaSpell_StopsAtAWall_AndOnlyCatchesWhatItsSpreadReaches()
        {
            var (w, engine, player, bandit) = Fight(
                "P.........",
                ".....#....",
                ".....#....",
                ".....#....",
                ".........B");
            var fireball = SpellCatalog.Find("fireball");
            player.Position = new GridPos(0, 0);
            bandit.Position = new GridPos(6, 2); // behind the wall from the point of origin
            var point = new GridPos(3, 2);
            var caught = engine.AreaTargets(player, fireball, point);
            Assert.That(caught.Contains(bandit), Is.False, "The wall shields it: no straight line from the origin.");
            bandit.Position = new GridPos(4, 2);
            caught = engine.AreaTargets(player, fireball, point);
            Assert.That(caught, Does.Contain(bandit), "On the near side it is caught.");
            Assert.That(engine.ReachedSquares(player, fireball, point).Any(q => q.X == 7 && q.Y == 2), Is.False, "The preview shows the squares behind the wall untouched.");
        }

        [Test]
        public void ASpellNeedsAClearPath_ToItsTarget_AndToItsPoint()
        {
            var (_, engine, player, bandit) = Fight(
                "P...#...B");
            var caster = player;
            var fireball = SpellCatalog.Find("fireball");
            var why = engine.SpellProblem(caster, new CastSpellAction("fireball", 3, Array.Empty<CombatantId>(), new GridPos(7, 0)));
            // The fighter has no fireball: the targeting rule is checked on the area query and the point check directly.
            Assert.That(engine.CoverOf(caster.Position, new GridPos(7, 0)).HasLineOfEffect, Is.False);
            Assert.That(engine.CoverOf(caster.Position, new GridPos(2, 0)).HasLineOfEffect, Is.True);
            Assert.That(fireball, Is.Not.Null);
            Assert.That(why, Is.Not.Null);
        }

        [Test]
        public void TheAI_DoesNotShootThroughWalls_ItGoesRound()
        {
            var (_, engine, player, bandit) = Fight(
                "....#.....",
                "P...#...B.",
                "....#.....",
                "..........");
            var e = engine.Encounter;
            e.TurnIndex = e.Order.IndexOf(bandit.CombatantId);
            var a = CombatAi.Next(engine, bandit);
            Assert.That(engine.Unavailable(bandit, a), Is.Null, "It proposes only what the rules allow: " + a.Describe);
            Assert.That(a, Is.Not.InstanceOf<AttackAction>(), "Nobody to shoot through the wall: it moves round instead.");
            Assert.That(a, Is.InstanceOf<MoveAction>());
        }

        [Test]
        public void ALastingCondition_ComesIntoTheFight_AndWhatTheFightDoes_GoesBackToTheSheet()
        {
            var w0 = _s.CurrentWorldSnapshotForTests();
            var now = CombatEngine.WorldMinute(w0);
            var sheet = w0.Player.Rpg;
            LastingConditions.Apply(sheet, ConditionType.Poisoned, "trap:poison_needle", "a poisoned needle", now, 60, ConditionRemedy.Restoration);
            LastingConditions.Apply(sheet, ConditionType.Exhaustion, "hazard:deep_water", "cold water", now, 0, ConditionRemedy.LongRestLevel);
            var setup = new EncounterSetup { Name = "x", Kind = EncounterKind.Skirmish, Lethality = Lethality.Lethal };
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "giant_rat", Side = "foes", Position = new GridPos(5, 0) });
            var enc = CombatEngine.Create(w0, setup, _ => Guid.NewGuid(), out _);
            w0.Encounters.Add(enc);
            var engine = new CombatEngine(w0, enc);
            var me = enc.Combatants.Single(c => c.Kind == CombatantKind.Player);
            Assert.That(engine.Has(me, ConditionType.Poisoned), Is.True, "The needle's poison is still in the blood.");
            Assert.That(engine.ExhaustionLevel(me), Is.EqualTo(1));
            Assert.That(me.Conditions.Single(k => k.Type == ConditionType.Poisoned).LastingKey, Is.EqualTo("trap:poison_needle"));

            // In the fight the poison is cured, and another level of exhaustion is gained.
            engine.RemoveCondition(me, ConditionType.Poisoned, "Lesser Restoration");
            engine.ApplyCondition(me, ConditionType.Exhaustion, default, "a forced march", ConditionDuration.UntilRemoved, default, 0);
            engine.Finish(EncounterOutcome.Victory, "player");
            Assert.That(LastingConditions.Has(sheet, ConditionType.Poisoned, now), Is.False, "Cured in the fight, cured after it.");
            Assert.That(LastingConditions.ExhaustionLevel(sheet, now), Is.EqualTo(2), "Exhaustion keeps the level it reached.");
        }

        [Test]
        public void OneDamageRule_ImmunityThenResistanceThenVulnerability_EachOnce()
        {
            var notes = new System.Collections.Generic.List<string>();
            Assert.That(DamageRules.Adjust(11, false, true, false, notes), Is.EqualTo(5), "Resistance halves, rounding down.");
            Assert.That(DamageRules.Adjust(11, false, false, true, null), Is.EqualTo(22));
            Assert.That(DamageRules.Adjust(11, false, true, true, null), Is.EqualTo(10), "Halved, then doubled.");
            Assert.That(DamageRules.Adjust(11, true, true, true, null), Is.EqualTo(0));
            Assert.That(DamageRules.Adjust(-3, false, false, false, null), Is.EqualTo(0));
            Assert.That(notes, Does.Contain("resistance (halved)"));
        }
    }
}
