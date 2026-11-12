using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 (ADR-0068): conditions that last, harm through the one damage rule, and rest that depends on where you
    /// are — tested through the commands and the session's own rest, with the world clock doing the expiring.
    /// </summary>
    public sealed class Phase14ConditionRestTests : Phase14Fixture
    {
        private static HarmSpec Needle => new HarmSpec
        {
            Source = "trap:test_needle", Name = "a test needle", Damage = new Dice(1, 4), DamageType = DamageType.Poison, Condition = ConditionType.Poisoned,
            ConditionMinutes = 60, Remedies = ConditionRemedy.Restoration,
        };

        [Test]
        public void ATrapsPoison_Lasts_SurvivesSaveAndLoad_GivesDisadvantage_AndWearsOffWithTime()
        {
            H.Hero();
            H.Edit(w => PlaceHarm.Apply(w, Needle, "test-1", out _));
            var k = W.Player.Rpg.Conditions.Single();
            Assert.That(k.Type, Is.EqualTo(ConditionType.Poisoned));
            Assert.That(k.ExpiresAt, Is.EqualTo(k.AppliedAt + 60));
            Assert.That(S.GetConditions().Single().Effect, Does.Contain("Disadvantage"));
            Assert.That(S.GetConditions().Single().HowToEnd, Does.Contain("wears off in").And.Contain("Lesser Restoration"));

            // Saved and loaded (Edit round-trips through the store): still there, the same.
            H.Edit(w => { });
            Assert.That(W.Player.Rpg.Conditions.Single().ExpiresAt, Is.EqualTo(k.ExpiresAt));

            // A check while poisoned rolls twice and keeps the lower.
            var roll = PlaceRules.Check(W, Skill.Perception, 10, "probe");
            Assert.That(roll.Text, Does.Contain("poisoned"));

            // Re-applying the same poison refreshes it; it never stacks.
            H.Edit(w => PlaceHarm.Apply(w, Needle, "test-2", out _));
            Assert.That(W.Player.Rpg.Conditions.Count, Is.EqualTo(1));

            Hours(2);
            Assert.That(W.Player.Rpg.Conditions, Is.Empty, "An hour's poison is gone after two.");
            Assert.That(S.LastExpired, Does.Contain("Poisoned"));
            Valid();
        }

        [Test]
        public void HarmGoesThroughTheOneDamageRule_TemporaryHitPointsFirst_AndTheLastHitPointIsKept()
        {
            H.Hero();
            H.Edit(w => { w.Player.Rpg.TemporaryHp = 3; });
            var hp = W.Player.Rpg.CurrentHp;
            DamageResult r = null;
            H.Edit(w => PlaceHarm.Apply(w, new HarmSpec { Source = "t", Name = "a blow", Damage = new Dice(1, 1, 5), DamageType = DamageType.Bludgeoning }, "blow", out r));
            Assert.That(W.Player.Rpg.TemporaryHp, Is.EqualTo(0));
            Assert.That(W.Player.Rpg.CurrentHp, Is.EqualTo(hp - 3), "6 damage: 3 to temporary hit points, 3 to hit points.");

            H.Edit(w => w.Player.Rpg.CurrentHp = 2);
            H.Edit(w => PlaceHarm.Apply(w, new HarmSpec { Source = "t", Name = "a big blow", Damage = new Dice(1, 1, 30), DamageType = DamageType.Bludgeoning }, "big", out r));
            Assert.That(W.Player.Rpg.CurrentHp, Is.EqualTo(1), "Outside a fight the last hit point is kept (ADR-0064, now in the shared rule).");
            Assert.That(W.Player.Rpg.IsDead, Is.False);
        }

        [Test]
        public void ADwarf_ResistsPoison_SoATrapsPoisonDamageIsHalved()
        {
            var r = S.Dispatch(new CreateCharacterCommand(Realmkeeper.Tests.EditMode.Character.ProgressionTests.Choices("fighter", "dwarf", "hermit", new[] { 15, 14, 13, 8, 10, 12 })));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
            var sheet = W.Player.Rpg.Clone();
            var result = DamageRules.ApplyToSheet(sheet, 9, DamageType.Poison);
            Assert.That(result.Final, Is.EqualTo(4));
            Assert.That(result.Adjustments, Does.Contain("resistance (halved)"));
            Assert.That(DamageRules.ApplyToSheet(W.Player.Rpg.Clone(), 9, DamageType.Fire).Final, Is.EqualTo(9), "Only poison.");
        }

        [Test]
        public void ANet_Restrains_NoMovingOrResting_UntilBrokenFree_AndNobodyIsTrappedForEver()
        {
            H.Hero();
            var mine = Enter(PlaceCatalog.Mine);
            var room = mine.Layout.Room(W.Player.PlaceRoom);
            H.Edit(w => LastingConditions.Apply(w.Player.Rpg, ConditionType.Restrained, "trap:snare_net", "a snare net", Realmkeeper.Simulation.Quests.QuestClock.Now(w), 0, ConditionRemedy.Escape, 40));
            var next = mine.Layout.Connections.First(c => !c.IsVertical && c.Kind != ConnectionKind.Entrance && c.Joins(room.Id));
            var other = mine.Layout.Room(next.Other(room.Id));
            Refused(new MoveInPlaceCommand(other.Id, PlaceRules.PoseAt(mine, other.Floor, other.Rect.CenterX, other.Rect.CenterZ, 0f)), "restrained");
            Refused(new ExitPlaceCommand(), "restrained");
            Assert.That(S.RestProblem(RestKind.Short), Does.Contain("restrained"));

            // DC 40: no roll frees them; after three tries they work free (ten minutes), so nothing traps them for ever.
            var minute = W.MinuteOfDay;
            for (var i = 0; i < 3; i++) Ok(S.BreakFree());
            Assert.That(LastingConditions.Has(W.Player.Rpg, ConditionType.Restrained, Now), Is.True);
            var free = S.BreakFree();
            Ok(free);
            Assert.That(free.Message, Does.Contain("work yourself free"));
            Assert.That(W.Player.Rpg.Conditions, Is.Empty);
            Assert.That(W.MinuteOfDay - minute, Is.EqualTo(3 + 10), "Each try took a minute; working free took ten.");
            Valid();
        }

        [Test]
        public void ALongRest_TakesOneLevelOfExhaustion_ButDoesNotCureThePoison()
        {
            H.Hero();
            H.Edit(w =>
            {
                var now = Realmkeeper.Simulation.Quests.QuestClock.Now(w);
                LastingConditions.Apply(w.Player.Rpg, ConditionType.Exhaustion, "cold", "cold water", now, 0, ConditionRemedy.LongRestLevel, 0, 2);
                LastingConditions.Apply(w.Player.Rpg, ConditionType.Poisoned, "bite", "a lingering venom", now, 0, ConditionRemedy.Restoration);
            });
            var rest = S.LongRest();
            Assert.That(rest.IsSuccess, Is.True, rest.Message);
            Assert.That(rest.Lines, Has.Some.Contain("Exhaustion: level 1"));
            Assert.That(rest.Lines, Has.Some.Contain("Still:").And.Some.Contain("poisoned"));
            Assert.That(LastingConditions.ExhaustionLevel(W.Player.Rpg, Now), Is.EqualTo(1));
            Assert.That(LastingConditions.Has(W.Player.Rpg, ConditionType.Poisoned, Now), Is.True);
            var again = S.LongRest();
            Assert.That(again.IsSuccess, Is.False, "One long rest per 24 hours still holds: rest cannot be repeated to clear conditions.");
        }

        [Test]
        public void Rest_DependsOnWhereYouAre()
        {
            H.Hero();
            // In a settlement: safe lodging.
            CombatTests.EnterTown(S);
            Assert.That(S.RestHere(RestKind.Long).Problem, Is.Null);
            Assert.That(S.RestHere(RestKind.Long).Site, Is.EqualTo(RestSite.Lodging));

            // Someone else's house: no.
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(W.Player.SettlementId));
            var home = layout.Buildings.First(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None && InteriorGenerator.HasInterior(b.Function.Value) && InteriorGenerator.Plan(b.Function.Value) == "home");
            Ok(S.Dispatch(new EnterBuildingCommand(home.Key)));
            Assert.That(S.RestProblem(RestKind.Short), Does.Contain("someone's house"));
            Ok(S.Dispatch(new ExitPlaceCommand()));

            // The mine: the entrance is quiet, but the spiders and rats still live below — the night will be broken.
            var mine = Enter(PlaceCatalog.Mine);
            var check = S.RestHere(RestKind.Long);
            Assert.That(check.Problem, Is.Null);
            Assert.That(check.Interrupter, Is.Not.Null);
            var hp = W.Player.Rpg.CurrentHp;
            var minute = W.MinuteOfDay;
            var rest = S.LongRest();
            Assert.That(rest.IsSuccess, Is.False, "No long rest is gained.");
            Assert.That(rest.MinutesPassed, Is.EqualTo(CharacterRest.LongRestMinutes / 2));
            Assert.That(S.GetEncounter()?.Status, Is.EqualTo(EncounterStatus.Active), "They found the sleeper: a fight in this room.");
            Assert.That(S.GetEncounter().Name, Does.Contain(Place(PlaceCatalog.Mine).Name));
            Assert.That(W.Player.Rpg.Spellcasting.RestLog.Count(x => x.Kind == RestKind.Long), Is.EqualTo(0));
            EncounterGenerationTests.FightToTheEnd(S);
            Valid();
        }

        [Test]
        public void Rest_IsRefused_InAHazardousRoom_AndWithFoesInSight()
        {
            H.Hero();
            var crypt = Enter(PlaceCatalog.Crypt);
            // A room with foul air, by the test's hand (a room of this crypt).
            var target = crypt.Layout.Rooms.First(r => r.Id != W.Player.PlaceRoom && !r.Secret && r.Floor == 0);
            H.Edit(w =>
            {
                var p = w.FindPlace(crypt.PlaceId);
                p.Layout.Room(W.Player.PlaceRoom).Hazard = "toxic_air";
            });
            Assert.That(S.RestProblem(RestKind.Short), Does.Contain("foul air"));
            // Standing among the crypt's dead (the test puts the player in their room, quietly).
            var g = crypt.Layout.Groups.First();
            var theirs = crypt.Layout.Room(g.Room);
            H.Edit(w =>
            {
                var p = w.FindPlace(crypt.PlaceId);
                p.Layout.Room(W.Player.PlaceRoom).Hazard = null;
                p.Layout.Room(theirs.Id).Hazard = null;
                w.Player.PlaceRoom = theirs.Id;
                w.Player.PlaceFloor = theirs.Floor;
                w.Player.Pose = PlaceRules.PoseAt(p, theirs.Floor, theirs.Rect.CenterX, theirs.Rect.CenterZ, 0f);
                p.GroupOf(g.Id).State = GroupState.Present;
                p.GroupOf(g.Id).Woken = true;
            });
            Assert.That(S.RestProblem(RestKind.Short), Does.Contain("so close"));
            Assert.That(target, Is.Not.Null);
        }
    }
}
