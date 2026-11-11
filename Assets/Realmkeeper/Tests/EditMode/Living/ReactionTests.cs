using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Magic;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 10 (ADR-0045): reactions and decisions are asked, not taken. The player's action waits with nothing of it
    /// applied; the answer replays it exactly; questions are answered once; a waiting question survives save and load;
    /// computer-controlled reactors decide through the same path; nothing is offered without the resource it costs.
    /// </summary>
    public sealed class ReactionTests
    {
        private string _dir;
        private WorldSession _s;
        private int _guids;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            _guids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private Guid NextGuid(string purpose) => new Guid(++_guids, 0, 0, new byte[8]);

        private CombatEngine Begin(WorldData w, params ParticipantSetup[] ps)
        {
            var enc = CombatEngine.Create(w, new EncounterSetup { Name = "Reaction test", Kind = EncounterKind.Other, Participants = ps.ToList() }, NextGuid, out var errors);
            Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var e = new CombatEngine(w, enc);
            e.Begin();
            return e;
        }

        private static ParticipantSetup Player(int x, int y) => new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player };

        private static ParticipantSetup Creature(string id, int x, int y) => new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = id, Side = "foes", Position = new GridPos(x, y) };

        private static void TurnOf(CombatEngine e, CombatantData c)
        {
            e.Encounter.TurnIndex = e.Encounter.Order.IndexOf(c.CombatantId);
            c.Turn = new TurnResources();
        }

        private static CombatantData P(CombatEngine e) => e.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);
        private static CombatantData Foe(CombatEngine e) => e.Encounter.Combatants.First(c => c.Kind != CombatantKind.Player);

        [Test]
        public void AnOpportunityAttack_IsAsked_TheMoveWaits_AndDecliningLetsItGo()
        {
            var w = SpellTests.Caster(_s, "fighter");
            var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
            var bandit = Foe(e);
            bandit.MaxHp = bandit.CurrentHp = 200;
            TurnOf(e, bandit);
            var rolls = e.Encounter.RollCount;
            var log = e.Encounter.Log.Count;

            var o = e.Act(bandit.CombatantId, new MoveAction(new GridPos(2, 0), new GridPos(3, 0)));
            Assert.That(o.Ok, Is.True);
            Assert.That(o.Pending, Is.Not.Null, "Leaving the player's reach asks the player.");
            Assert.That(o.Pending.Opportunity.Kind, Is.EqualTo(ReactionKind.OpportunityAttack));
            Assert.That(o.Pending.Opportunity.ReactorId, Is.EqualTo(P(e).CombatantId));
            Assert.That(o.Pending.Opportunity.Cost, Does.Contain("reaction"));
            Assert.That(bandit.Position, Is.EqualTo(new GridPos(1, 0)), "Nothing of the move has happened.");
            Assert.That(e.Encounter.RollCount, Is.EqualTo(rolls));
            Assert.That(e.Encounter.Log.Count, Is.EqualTo(log));
            Assert.That(e.Act(bandit.CombatantId, new DodgeAction()).Ok, Is.False, "Nothing else happens while a question waits.");

            var answer = e.Respond(o.Pending.Id, false);
            Assert.That(answer.Ok, Is.True, answer.Error);
            Assert.That(bandit.Position, Is.EqualTo(new GridPos(3, 0)));
            Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("opportunity attack")), Is.False);
            Assert.That(P(e).Turn.ReactionUsed, Is.False, "Declining spends nothing.");
            Assert.That(e.Encounter.PendingReaction, Is.Null);
        }

        [Test]
        public void AcceptingAnOpportunityAttack_ResolvesIt_ExactlyAsIfAnsweredAtOnce()
        {
            string Run(bool viaSave)
            {
                var s = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
                _guids = 0;
                var w = SpellTests.Caster(s, "fighter");
                var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
                var bandit = Foe(e);
                bandit.MaxHp = bandit.CurrentHp = 200;
                TurnOf(e, bandit);
                var o = e.Act(bandit.CombatantId, new MoveAction(new GridPos(2, 0)));
                Assert.That(o.Pending, Is.Not.Null);
                if (viaSave)
                {
                    // The waiting question survives a save and a load; the loaded world answers it.
                    var serializer = new JsonWorldSerializer();
                    var loaded = serializer.Deserialize(serializer.Serialize(w));
                    Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
                    w = loaded.World;
                    var pending = w.ActiveEncounter.PendingReaction;
                    Assert.That(pending, Is.Not.Null);
                    Assert.That(pending.Opportunity.Kind, Is.EqualTo(ReactionKind.OpportunityAttack));
                    e = new CombatEngine(w, w.ActiveEncounter);
                }

                var r = e.Respond(e.Encounter.PendingReaction.Id, true);
                Assert.That(r.Ok, Is.True, r.Error);
                Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("makes an opportunity attack")), Is.True);
                Assert.That(P(e).Turn.ReactionUsed, Is.True);
                return string.Join("\n", e.Encounter.Log.Select(l => l.Text)) + "|" + e.Encounter.RollCount;
            }

            Assert.That(Run(true), Is.EqualTo(Run(false)), "The same rolls and the same outcome after a save and load.");
        }

        [Test]
        public void AQuestion_IsAnsweredOnce_AndStaleAnswersAreRefused()
        {
            var w = SpellTests.Caster(_s, "fighter");
            var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
            var bandit = Foe(e);
            bandit.MaxHp = bandit.CurrentHp = 200;
            TurnOf(e, bandit);
            var o = e.Act(bandit.CombatantId, new MoveAction(new GridPos(2, 0)));
            Assert.That(e.Respond(o.Pending.Id + 1, true).Ok, Is.False, "Not the question asked.");
            Assert.That(e.Respond(o.Pending.Id, true).Ok, Is.True);
            Assert.That(e.Respond(o.Pending.Id, true).Ok, Is.False, "Already answered: the reaction cannot happen twice.");
            Assert.That(e.Encounter.Log.Count(l => l.Text.Contains("makes an opportunity attack")), Is.EqualTo(1));
        }

        [Test]
        public void Shield_IsOfferedWhenAHitLands_SpendsASlot_AndRaisesTheArmourClass()
        {
            var w = SpellTests.Caster(_s, "wizard", 1, new[] { "fire_bolt" }, new[] { "shield", "magic_missile" }, new[] { "shield", "magic_missile" });
            var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
            var bandit = Foe(e);
            var asked = false;
            for (var i = 0; i < 40 && !asked; i++)
            {
                w.Player.Rpg.CurrentHp = new Realmkeeper.Domain.Rpg.CharacterRules(w.Player.Rpg).MaxHp;
                TurnOf(e, bandit);
                var o = e.Act(bandit.CombatantId, new AttackAction(P(e).CombatantId, "scimitar"));
                if (o.Pending == null) continue;
                asked = true;
                Assert.That(o.Pending.Opportunity.Kind, Is.EqualTo(ReactionKind.Shield));
                Assert.That(o.Pending.Opportunity.Consequence, Does.Contain("+5 AC"));
                var slots = w.Player.Rpg.Resources["spell_slots_1"];
                var answer = e.Respond(o.Pending.Id, true);
                Assert.That(answer.Ok, Is.True, answer.Error);
                Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(slots - 1), "A level 1 slot is spent.");
                Assert.That(P(e).Turn.ReactionUsed, Is.True);
                Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("casts Shield as a reaction")), Is.True);
                Assert.That(e.Encounter.Effects.Any(x => x.TargetId == P(e).CombatantId && x.Name == "Shield" && x.Value == 5), Is.True, "The +5 lasts until the wizard's next turn.");
            }

            Assert.That(asked, Is.True, "A hit was offered to Shield.");
        }

        [Test]
        public void NothingIsOffered_WithoutTheResourceItCosts()
        {
            var w = SpellTests.Caster(_s, "wizard", 1, new[] { "fire_bolt" }, new[] { "shield", "magic_missile" }, new[] { "shield", "magic_missile" });
            w.Player.Rpg.Resources["spell_slots_1"] = 0;
            var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
            var bandit = Foe(e);
            for (var i = 0; i < 20; i++)
            {
                w.Player.Rpg.CurrentHp = new Realmkeeper.Domain.Rpg.CharacterRules(w.Player.Rpg).MaxHp;
                TurnOf(e, bandit);
                var o = e.Act(bandit.CombatantId, new AttackAction(P(e).CombatantId, "scimitar"));
                Assert.That(o.Pending, Is.Null, "No slot left: Shield is never offered.");
            }
        }

        [Test]
        public void ComputerReactors_DecideThroughTheSamePath_OpportunityAttacksAndParry()
        {
            var w = SpellTests.Caster(_s, "fighter");
            var e = Begin(w, Player(0, 0), Creature("bandit", 1, 0));
            var p = P(e);
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new MoveAction(new GridPos(-1, 0), new GridPos(-2, 0)));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(o.Pending, Is.Null, "The bandit decides by itself; nobody is asked.");
            Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("Bandit makes an opportunity attack")), Is.True);

            // A bandit captain parries a melee hit when it would turn it and the blow matters.
            e.Finish(EncounterOutcome.Stalemate, null);
            var e2 = Begin(w, Player(10, 10), Creature("bandit_captain", 11, 10));
            var captain = Foe(e2);
            captain.CurrentHp = 10;
            var parried = false;
            for (var i = 0; i < 60 && !parried; i++)
            {
                captain.CurrentHp = 10;
                captain.Turn = new TurnResources();
                TurnOf(e2, P(e2));
                var melee = e2.Stats(P(e2)).BestMelee();
                e2.Act(P(e2).CombatantId, new AttackAction(captain.CombatantId, melee.Id));
                parried = e2.Encounter.Log.Any(l => l.Text.Contains("Bandit Captain parries"));
            }

            Assert.That(parried, Is.True, "Parry was used by its own decision.");
            Assert.That(e2.Encounter.PendingReaction, Is.Null);
        }
    }
}
