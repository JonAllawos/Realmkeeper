using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Magic
{
    /// <summary>Phase 9: class features through the shared activation path and the shared attack, save, damage and effect routines.</summary>
    public sealed class FeatureTests
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

        private Guid NextGuid(string purpose)
        {
            _guids++;
            var b = new byte[16];
            BitConverter.GetBytes(_guids).CopyTo(b, 0);
            b[15] = 0x5A;
            return new Guid(b);
        }

        private static ParticipantSetup Player(int x = 0, int y = 0) => new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player };

        private static ParticipantSetup Creature(string id, string side, int x, int y, string name = null) =>
            new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = id, Side = side, Position = new GridPos(x, y), Name = name };

        private CombatEngine Begin(WorldData w, params ParticipantSetup[] ps)
        {
            var enc = CombatEngine.Create(w, new EncounterSetup { Name = "Feature test", Kind = EncounterKind.Other, Participants = ps.ToList() }, NextGuid, out var errors);
            Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var e = new CombatEngine(w, enc);
            e.Begin();
            return e;
        }

        private static void TurnOf(CombatEngine e, CombatantData c)
        {
            e.Encounter.TurnIndex = e.Encounter.Order.IndexOf(c.CombatantId);
            c.Turn = new TurnResources();
        }

        private static CombatantData Named(CombatEngine e, string name) => e.Encounter.Combatants.First(c => c.Name == name);
        private static CombatantData P(CombatEngine e) => e.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);

        private static void Tough(CombatantData c, int hp = 300)
        {
            c.MaxHp = hp;
            c.CurrentHp = hp;
            c.State = VitalState.Conscious;
            c.Conditions.Clear();
        }

        private WorldData Hero(string cls, int level, string weapon = null, bool freshSession = false)
        {
            var w = SpellTests.Caster(freshSession ? Phase2Worlds.Session(_dir) : _s, cls, level);
            if (weapon != null) CombatTests.Equip(w.Player, EquipmentSlot.MainHand, weapon);
            CombatTests.Equip(w.Player, EquipmentSlot.OffHand, null);
            CombatTests.Equip(w.Player, EquipmentSlot.Chest, null);
            return w;
        }

        private static UseFeatureAction F(string id, CombatantData target = null, string option = null, int amount = 0) => new UseFeatureAction(id, target?.CombatantId ?? default, option, amount);

        [Test]
        public void Rage_AddsDamage_ResistsPhysicalHarm_AndEndsWhenNotKeptUp()
        {
            var w = Hero("barbarian", 3, "greataxe");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            TurnOf(e, p);
            var uses = w.Player.Rpg.Resources["rage"];
            Assert.That(e.Act(p.CombatantId, F("rage")).Ok, Is.True);
            Assert.That(w.Player.Rpg.Resources["rage"], Is.EqualTo(uses - 1));
            Assert.That(e.HasEffect(p, CombatEffectKind.Raging), Is.True);
            Assert.That(e.Unavailable(p, F("rage")), Is.EqualTo("The bonus action this turn is used."));
            var r = e.ApplyDamage(p, 10, DamageType.Slashing, false, "test");
            Assert.That(r.Final, Is.EqualTo(5), "Resistance to slashing while raging.");
            Assert.That(e.ApplyDamage(p, 0 + 10, DamageType.Fire, false, "test").Final, Is.EqualTo(10), "Not to fire.");
            w.Player.Rpg.CurrentHp = e.MaxHp(p);
            for (var i = 0; i < 30; i++)
            {
                Tough(bandit);
                var hit = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
                if (!hit.Hit) continue;
                Assert.That(hit.DamageBreakdown.Parts.Any(x => x.label == "Rage" && x.value == 2), Is.True, hit.DamageBreakdown.Text);
                break;
            }

            Assert.That(e.SpellProblem(p, new CastSpellAction("fire_bolt", 0, new[] { bandit.CombatantId })), Does.Contain("not have").Or.Contain("raging"));
            // A turn with no attack ends the rage.
            TurnOf(e, p);
            e.Act(p.CombatantId, new EndTurnAction());
            Assert.That(e.HasEffect(p, CombatEffectKind.Raging), Is.False, "No attack, no forced save, no extension: the rage ends.");
        }

        [Test]
        public void RecklessAttack_GivesAdvantage_AndExposesTheBarbarian()
        {
            var w = Hero("barbarian", 2, "greataxe");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, F("reckless_attack")).Ok, Is.True);
            var r = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
            Assert.That(r.Roll.AdvantageSources, Has.Member("Reckless Attack"));
            var back = e.ResolveAttack(bandit, p, e.Stats(bandit).BestMelee(), false);
            Assert.That(back.Roll.AdvantageSources, Has.Some.Contains("recklessly"));
            Assert.That(e.Unavailable(p, F("reckless_attack")), Is.Not.Null);
        }

        [Test]
        public void SneakAttack_AddsDice_OncePerTurn_WithAdvantageOrAnAlly()
        {
            var w = Hero("rogue", 5, "rapier");
            var e = Begin(w, Player(), Creature("guard", "player", 2, 1, "Ally"), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            var found = false;
            for (var i = 0; i < 40 && !found; i++)
            {
                Tough(bandit);
                p.Turn = new TurnResources();
                var r = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
                if (!r.Hit) continue;
                Assert.That(r.DamageBreakdown.Text, Does.Contain("Sneak Attack 3d6").Or.Contain("Sneak Attack 6d6"), "The ally next to the bandit allows it.");
                var second = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
                if (second.Hit) Assert.That(second.DamageBreakdown.Text, Does.Not.Contain("Sneak Attack"), "Once per turn.");
                found = true;
            }

            Assert.That(found, Is.True);
        }

        [Test]
        public void ActionSurge_GivesAnotherAction_OncePerTurn_NotForMagic()
        {
            var w = Hero("fighter", 2, "sword_arming");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, F("action_surge")), Does.Contain("Use your action first"));
            Assert.That(e.Act(p.CombatantId, new AttackAction(bandit.CombatantId, "weapon")).Ok, Is.True);
            Assert.That(e.Act(p.CombatantId, F("action_surge")).Ok, Is.True);
            Assert.That(p.Turn.ActionUsed, Is.False);
            Assert.That(e.Act(p.CombatantId, new AttackAction(bandit.CombatantId, "weapon")).Ok, Is.True, "The second action.");
            Assert.That(e.Unavailable(p, F("action_surge")), Does.Contain("once per turn").IgnoreCase.Or.Contain("No Action Surge"));
        }

        [Test]
        public void SecondWind_AndCunningAction_StillWork_ThroughTheSharedPath()
        {
            var w = Hero("fighter", 1, "sword_arming");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 5, 0));
            var p = P(e);
            TurnOf(e, p);
            w.Player.Rpg.CurrentHp = 1;
            Assert.That(e.Act(p.CombatantId, new BonusAbilityAction("second_wind")).Ok, Is.True);
            Assert.That(w.Player.Rpg.CurrentHp, Is.GreaterThan(1));

            var rogue = Hero("rogue", 2, "rapier", true);
            _guids = 500;
            var e2 = Begin(rogue, Player(), Creature("bandit", "bandits", 5, 0));
            var p2 = P(e2);
            TurnOf(e2, p2);
            var speed = e2.Speed(p2);
            Assert.That(e2.Act(p2.CombatantId, F("cunning_dash")).Ok, Is.True);
            Assert.That(e2.MovementLeft(p2), Is.EqualTo(2 * speed));
        }

        [Test]
        public void DivineSmite_FollowsAMeleeHit_AndSpendsASlot()
        {
            var w = Hero("paladin", 2, "sword_arming");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            Assert.That(e.SpellsOf(p).Any(s => s.Id == "divine_smite"), Is.True, "Paladin's Smite: always prepared.");
            Assert.That(e.Unavailable(p, new CastSpellAction("divine_smite", 1, new[] { bandit.CombatantId })), Does.Contain("follows a melee hit"));
            var hit = false;
            for (var i = 0; i < 30 && !hit; i++)
            {
                Tough(bandit);
                p.Turn = new TurnResources();
                hit = e.Act(p.CombatantId, new AttackAction(bandit.CombatantId, "weapon")).Attacks.Single().Hit;
            }

            Assume.That(hit, Is.True);
            var slots = w.Player.Rpg.Resources["spell_slots_1"];
            var o = e.Act(p.CombatantId, new CastSpellAction("divine_smite", 1, new[] { bandit.CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(o.Spells.Single().Targets.Single().Damage, Is.InRange(2, 32));
            Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(slots - 1));
        }

        [Test]
        public void LayOnHands_HealsFromThePool_AndBardicInspiration_SavesAFailure()
        {
            var w = Hero("paladin", 3, "sword_arming");
            var e = Begin(w, Player(), Creature("guard", "player", 1, 0, "Ally"), Creature("bandit", "bandits", 6, 6));
            var p = P(e);
            var ally = Named(e, "Ally");
            Tough(ally, 40);
            ally.CurrentHp = 10;
            TurnOf(e, p);
            var pool = w.Player.Rpg.Resources["lay_on_hands"];
            Assert.That(pool, Is.EqualTo(15));
            Assert.That(e.Act(p.CombatantId, F("lay_on_hands", ally, amount: 12)).Ok, Is.True);
            Assert.That(ally.CurrentHp, Is.EqualTo(22));
            Assert.That(w.Player.Rpg.Resources["lay_on_hands"], Is.EqualTo(3));
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, F("lay_on_hands", ally, amount: 5)), Does.Contain("1–3"));

            _guids = 700;
            var bard = Hero("bard", 1, null, true);
            var e2 = Begin(bard, Player(), Creature("guard", "player", 1, 0, "Friend"), Creature("bandit", "bandits", 6, 6));
            var b = P(e2);
            var friend = Named(e2, "Friend");
            TurnOf(e2, b);
            Assert.That(e2.Unavailable(b, F("bardic_inspiration", b)), Does.Contain("someone else"));
            Assert.That(e2.Act(b.CombatantId, F("bardic_inspiration", friend)).Ok, Is.True);
            Assert.That(e2.HasEffect(friend, CombatEffectKind.InspirationDie), Is.True);
            var used = false;
            for (var i = 0; i < 40 && !used; i++)
            {
                e2.SavingThrow(friend, Ability.Wisdom, 15, "test");
                used = !e2.HasEffect(friend, CombatEffectKind.InspirationDie);
            }

            Assert.That(used, Is.True, "The die is spent on a failure it can turn.");
            Assert.That(e2.Encounter.Log.Any(l => l.Text.Contains("Bardic Inspiration +")), Is.True);
        }

        [Test]
        public void WildShape_Foundation_UsesTheBeastsStatistics_AndEnds()
        {
            var w = Hero("druid", 2);
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, F("wild_shape")).Ok, Is.True);
            var form = new CombatEngine(w, e.Encounter);
            Assert.That(form.Stats(p).Form?.Id, Is.EqualTo("wolf"));
            Assert.That(form.Stats(p).ArmorClass.Total, Is.EqualTo(12));
            Assert.That(form.Speed(p), Is.EqualTo(40));
            Assert.That(form.Stats(p).Find("bite"), Is.Not.Null);
            Assert.That(w.Player.Rpg.TemporaryHp, Is.EqualTo(2), "Temporary hit points equal to the druid level.");
            Assert.That(form.SpellProblem(p, new CastSpellAction("thunderwave", 1, point: new GridPos(2, 0))), Does.Contain("Wild Shape").Or.Contain("not have"));
            TurnOf(form, p);
            Assert.That(form.Act(p.CombatantId, F("leave_wild_shape")).Ok, Is.True);
            Assert.That(p.FormCreatureId, Is.Null);
        }

        [Test]
        public void Monk_FlurryOfBlows_AndStunningStrike_UseFocus()
        {
            var w = Hero("monk", 5);
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            Assert.That(e.Stats(p).Find("unarmed").Damage.Sides, Is.EqualTo(8), "Martial Arts: d8 at level 5.");
            TurnOf(e, p);
            var focus = w.Player.Rpg.Resources["focus_points"];
            Assert.That(e.Unavailable(p, F("martial_arts_strike", bandit)), Does.Contain("Attack action first"));
            Assert.That(e.Act(p.CombatantId, F("flurry_of_blows", bandit)).Ok, Is.True);
            Assert.That(w.Player.Rpg.Resources["focus_points"], Is.EqualTo(focus - 1));
            var stunned = false;
            for (var i = 0; i < 40 && !stunned; i++)
            {
                Tough(bandit);
                p.Turn = new TurnResources();
                w.Player.Rpg.Resources["focus_points"] = 5;
                if (!e.ResolveAttack(p, bandit, e.Stats(p).Find("unarmed"), false).Hit) continue;
                Assert.That(e.Act(p.CombatantId, F("stunning_strike")).Ok, Is.True);
                Assert.That(e.Unavailable(p, F("stunning_strike")), Does.Contain("once per turn"));
                stunned = e.Has(bandit, ConditionType.Stunned);
            }

            Assert.That(stunned, Is.True, "A failed Constitution save stuns.");
        }

        [Test]
        public void SorceryPoints_FontOfMagic_AndMetamagic()
        {
            var w = SpellTests.Caster(_s, "sorcerer", 3, new[] { "fire_bolt" }, new[] { "magic_missile", "hold_person" });
            var sheet = w.Player.Rpg;
            var meta = sheet.Choices.Keys.First(k => k.EndsWith("metamagic", StringComparison.Ordinal));
            sheet.Choices[meta] = new System.Collections.Generic.List<string> { "quickened", "twinned" };
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0, "A"), Creature("bandit", "bandits", 5, 2, "B"));
            var p = P(e);
            var a = Named(e, "A");
            var b = Named(e, "B");
            TurnOf(e, p);
            var points = sheet.Resources["sorcery_points"];
            var o = e.Act(p.CombatantId, new CastSpellAction("fire_bolt", 0, new[] { a.CombatantId }, null, new[] { "quickened" }));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(p.Turn.BonusActionUsed && !p.Turn.ActionUsed, Is.True, "Quickened: a bonus action.");
            Assert.That(sheet.Resources["sorcery_points"], Is.EqualTo(points - 2));
            o = e.Act(p.CombatantId, new CastSpellAction("hold_person", 2, new[] { a.CombatantId, b.CombatantId }, null, new[] { "twinned" }));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(o.Spells.Single().Targets.Count, Is.EqualTo(2), "Twinned: one more target.");
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, new CastSpellAction("magic_missile", 1, new[] { a.CombatantId }, null, new[] { "careful" })), Does.Contain("not one of your"));

            TurnOf(e, p);
            sheet.Resources["spell_slots_1"] = 0;
            sheet.Resources["sorcery_points"] = 3;
            Assert.That(e.Act(p.CombatantId, F("font_create_slot", option: "1")).Ok, Is.True);
            Assert.That(sheet.Resources["spell_slots_1"], Is.EqualTo(1));
            Assert.That(sheet.Resources["sorcery_points"], Is.EqualTo(1));
            Assert.That(e.Act(p.CombatantId, F("font_convert_slot", option: "1")).Ok, Is.True);
            Assert.That(sheet.Resources["sorcery_points"], Is.EqualTo(2));
        }

        [Test]
        public void AgonizingBlast_AddsCharisma_ToEachBeam()
        {
            var w = SpellTests.Caster(_s, "warlock", 5, new[] { "eldritch_blast" });
            var inv = w.Player.Rpg.Choices.Keys.First(k => k.EndsWith("invocations", StringComparison.Ordinal));
            w.Player.Rpg.Choices[inv] = new System.Collections.Generic.List<string> { "agonizing_blast" };
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new CastSpellAction("eldritch_blast", 0, new[] { bandit.CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            if (o.Spells.Single().Targets.Single().Attacks.Any(x => x.Hit))
                Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("Agonizing Blast")), Is.True);
        }

        [Test]
        public void WeaponMastery_Vex_GivesAdvantage_OnTheNextAttack()
        {
            var w = Hero("fighter", 1, "rapier");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Assert.That(ItemCatalog.Find("rapier").Weapon.Mastery, Is.EqualTo("Vex"));
            for (var i = 0; i < 30; i++)
            {
                Tough(bandit);
                var r = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
                if (!r.Hit) continue;
                var next = e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false);
                Assert.That(next.Roll.AdvantageSources, Has.Member("advantage on the next attack"));
                return;
            }

            Assert.Fail("No hit in 30 attacks.");
        }

        [Test]
        public void DeferredFeatures_AreNotOfferedAsUsable()
        {
            var w = Hero("rogue", 5, "rapier");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var offered = e.FeaturesOf(p).Select(f => f.feature.Id).ToList();
            Assert.That(offered, Does.Not.Contain("cunning_strike"), "Cunning Strike is not simulated, so it is not offered.");
            Assert.That(offered, Does.Contain("cunning_dash"));
            Assert.That(CombatFeatureCatalog.All.Where(f => f.Status != ImplementationStatus.Functional).All(f => !string.IsNullOrEmpty(f.StatusNote)), Is.True);
            Assert.That(CombatFeatureCatalog.All.Select(f => f.Id).Distinct().Count(), Is.EqualTo(CombatFeatureCatalog.All.Count));
        }
    }
}
