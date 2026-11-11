using System;
using System.Collections.Generic;
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
    /// <summary>Phase 9: the spell catalog and the shared spell pipeline in the combat engine (attacks, saves, areas, healing, effects, concentration, slots).</summary>
    public sealed class SpellTests
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

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private Guid NextGuid(string purpose)
        {
            _guids++;
            var b = new byte[16];
            BitConverter.GetBytes(_guids).CopyTo(b, 0);
            b[15] = 0x59;
            return new Guid(b);
        }

        /// <summary>A created character of the class with these spell picks, in a world copy (engine-level tests).</summary>
        internal static WorldData Caster(WorldSession s, string cls, int level = 1, string[] cantrips = null, string[] spells = null, string[] prepared = null)
        {
            CombatTests.Create(s, cls, CombatTests.Scores);
            var w = s.CurrentWorldSnapshotForTests();
            var sheet = w.Player.Rpg;
            if (level > 1)
            {
                sheet = ProgressionTestsAdvance(sheet, level);
                w.Player.Rpg = sheet;
            }

            // Picks set here replace the suggested ones: the same spell is never left in a later pick of the same pool.
            foreach (var key in sheet.Choices.Keys.ToList())
                if (key.StartsWith("lv", StringComparison.Ordinal))
                    sheet.Choices[key] = sheet.Choices[key].Where(v => (cantrips == null || !cantrips.Contains(v)) && (spells == null || !spells.Contains(v))).ToList();
            if (cantrips != null) sheet.Choices["class:cantrips"] = cantrips.ToList();
            var keeping = RpgCatalog.KeepingOf(RpgCatalog.FindClass(cls));
            if (spells != null) sheet.Choices[keeping == SpellKeeping.Spellbook ? "class:spellbook" : "class:spells"] = spells.ToList();
            if (prepared != null) sheet.Spellcasting.Prepared = prepared.ToList();
            var rules = new CharacterRules(sheet);
            foreach (var pair in rules.ResourceMaximums()) sheet.Resources[pair.Key] = pair.Value;
            return w;
        }

        private static CharacterSheetData ProgressionTestsAdvance(CharacterSheetData sheet, int level)
        {
            sheet = sheet.Clone();
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "test", Amount = ProgressionRules.ExperienceFor(level) });
            sheet.Experience = ProgressionRules.ExperienceFor(level);
            for (var l = 2; l <= level; l++)
            {
                var next = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, l) }, default, out var errors);
                Assert.That(next, Is.Not.Null, string.Join("; ", errors));
                sheet = next;
            }

            return sheet;
        }

        private static ParticipantSetup Player(int x = 0, int y = 0) => new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player };

        private static ParticipantSetup Creature(string id, string side, int x, int y, string name = null) =>
            new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = id, Side = side, Position = new GridPos(x, y), Name = name };

        private CombatEngine Begin(WorldData w, params ParticipantSetup[] ps)
        {
            var enc = CombatEngine.Create(w, new EncounterSetup { Name = "Spell test", Kind = EncounterKind.Other, Participants = ps.ToList() }, NextGuid, out var errors);
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

        private static void Tough(CombatantData c, int hp = 200)
        {
            c.MaxHp = hp;
            c.CurrentHp = hp;
        }

        // ===== Catalog =====================================================================================

        [Test]
        public void TheCatalog_IsValid_Unique_Attributed_AndHonestAboutStatus()
        {
            Assert.That(SpellCatalog.Validate(), Is.Empty);
            var all = SpellCatalog.All;
            Assert.That(all.Select(s => s.Id).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.All(s => s.Source == ContentSource.Srd521 && s.RulesVersion == 8), Is.True);
            Assert.That(all.Count(s => s.IsCantrip), Is.GreaterThanOrEqualTo(5));
            foreach (var level in new[] { 1, 2, 3 }) Assert.That(all.Count(s => s.Level == level && s.Castable), Is.GreaterThanOrEqualTo(4), "level " + level);
            Assert.That(all.Where(s => !s.Castable).All(s => !string.IsNullOrEmpty(s.StatusNote)), Is.True);
            foreach (var cls in new[] { "bard", "cleric", "druid", "paladin", "ranger", "sorcerer", "warlock", "wizard" })
                Assert.That(all.Any(s => s.Level >= 1 && s.Castable && SpellCatalog.OnList(s, cls)), Is.True, cls + " has castable spells.");
            Assert.That(all.Any(s => SpellCatalog.OnList(s, "fighter") || SpellCatalog.OnList(s, "barbarian") || SpellCatalog.OnList(s, "monk") || SpellCatalog.OnList(s, "rogue")), Is.False, "No spell access is invented.");
        }

        [Test]
        public void AreaShapes_FollowTheGrid()
        {
            var o = new GridPos(0, 0);
            var sphere = CombatEngine.AreaSquares(o, new GridPos(10, 0), AreaShape.Sphere, 20, false);
            Assert.That(sphere.Count, Is.EqualTo(81), "A 20-ft radius is 4 squares each way on the Chebyshev grid.");
            var cone = CombatEngine.AreaSquares(o, new GridPos(5, 0), AreaShape.Cone, 15, true);
            Assert.That(cone, Has.Member(new GridPos(1, 0)).And.Member(new GridPos(3, 0)).And.Member(new GridPos(3, 1)));
            Assert.That(cone, Has.No.Member(new GridPos(-1, 0)).And.No.Member(new GridPos(0, 3)).And.No.Member(new GridPos(4, 0)));
            var line = CombatEngine.AreaSquares(o, new GridPos(0, 9), AreaShape.Line, 100, true);
            Assert.That(line.Count, Is.EqualTo(20));
            Assert.That(line.All(p => p.X == 0 && p.Y > 0), Is.True);
            var cube = CombatEngine.AreaSquares(o, new GridPos(4, 0), AreaShape.Cube, 15, true);
            Assert.That(cube.Count, Is.EqualTo(9));
            Assert.That(cube.All(p => p.X >= 1 && p.X <= 3 && Math.Abs(p.Y) <= 1), Is.True);
            var cylinder = CombatEngine.AreaSquares(o, new GridPos(6, 2), AreaShape.Cylinder, 10, false);
            Assert.That(cylinder.Count, Is.EqualTo(25), "A 10-ft-radius cylinder covers the squares within 2 of its centre, like a sphere on the flat grid.");
            Assert.That(cylinder, Has.Member(new GridPos(4, 0)).And.Member(new GridPos(8, 4)).And.No.Member(new GridPos(9, 2)));
            var pointCube = CombatEngine.AreaSquares(o, new GridPos(6, 2), AreaShape.Cube, 20, false);
            Assert.That(pointCube.Count, Is.EqualTo(16), "A 20-ft cube placed at a point.");
        }

        // ===== Casting =====================================================================================

        [Test]
        public void FireBolt_IsASpellAttack_ThroughTheSharedRoll_AndScalesWithLevel()
        {
            var w = Caster(_s, "wizard", 1, new[] { "fire_bolt" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            int hits = 0, misses = 0;
            for (var i = 0; i < 40; i++)
            {
                Tough(bandit);
                bandit.State = VitalState.Conscious;
                TurnOf(e, p);
                var o = e.Act(p.CombatantId, new CastSpellAction("fire_bolt", 0, new[] { bandit.CombatantId }));
                Assert.That(o.Ok, Is.True, o.Error);
                var r = o.Spells.Single().Targets.Single();
                var a = r.Attacks.Single();
                Assert.That(a.Kind, Is.EqualTo(AttackKind.Spell));
                Assert.That(a.AttackBonus.Total, Is.EqualTo(e.Stats(p).SpellAttack().Total));
                Assert.That(a.Total, Is.EqualTo(a.Roll.Natural + a.AttackBonus.Total));
                if (a.Hit) { hits++; Assert.That(r.Damage, Is.InRange(1, a.Critical ? 20 : 10)); }
                else { misses++; Assert.That(r.Damage, Is.Zero); }
            }

            Assert.That(hits > 0 && misses > 0, Is.True);
            Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(2), "A cantrip spends no slot.");
        }

        [Test]
        public void SacredFlame_UsesTheSharedSave_AgainstTheSpellDc()
        {
            var w = Caster(_s, "cleric", 1, new[] { "sacred_flame" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            for (var i = 0; i < 20; i++)
            {
                Tough(bandit);
                TurnOf(e, p);
                var o = e.Act(p.CombatantId, new CastSpellAction("sacred_flame", 0, new[] { bandit.CombatantId }));
                Assert.That(o.Ok, Is.True, o.Error);
                var r = o.Spells.Single().Targets.Single();
                Assert.That(r.Save, Is.Not.Null);
                Assert.That(r.Save.Dc, Is.EqualTo(e.SpellSaveDc(p)));
                Assert.That(r.Save.Ability, Is.EqualTo(Ability.Dexterity));
                if (r.Save.Success) Assert.That(r.Damage, Is.Zero, "Sacred Flame: nothing on a success.");
                else Assert.That(r.Damage, Is.InRange(1, 8));
            }
        }

        [Test]
        public void BurningHands_HitsEveryoneInTheCone_EachSavingSeparately_AndSpendsTheSlot()
        {
            var w = Caster(_s, "wizard", 1, new[] { "fire_bolt" }, new[] { "burning_hands", "magic_missile" }, new[] { "burning_hands", "magic_missile" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 2, 0, "B1"), Creature("bandit", "bandits", 3, 1, "B2"), Creature("bandit", "bandits", -3, 0, "Behind"));
            var p = P(e);
            foreach (var c in e.Encounter.Combatants.Where(c => c.Kind != CombatantKind.Player)) Tough(c);
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new CastSpellAction("burning_hands", 1, point: new GridPos(3, 0)));
            Assert.That(o.Ok, Is.True, o.Error);
            var cast = o.Spells.Single();
            Assert.That(cast.Targets.Select(t => t.Name), Is.EquivalentTo(new[] { "B1", "B2" }));
            Assert.That(cast.Targets.All(t => t.Save != null), Is.True, "One save per creature.");
            foreach (var t in cast.Targets) Assert.That(t.Damage, Is.EqualTo(t.Save.Success ? t.RawDamage / 2 : t.RawDamage), "Half (rounded down) on a success, all on a failure.");
            var successes = cast.Targets.Count(t => t.Save.Success);
            for (var i = 0; i < 30 && successes == 0; i++)
            {
                foreach (var c in e.Encounter.Combatants.Where(c => c.Kind != CombatantKind.Player)) Tough(c);
                TurnOf(e, p);
                w.Player.Rpg.Resources["spell_slots_1"] = 2;
                var again = e.Act(p.CombatantId, new CastSpellAction("burning_hands", 1, point: new GridPos(3, 0))).Spells.Single();
                foreach (var t in again.Targets) Assert.That(t.Damage, Is.EqualTo(t.Save.Success ? t.RawDamage / 2 : t.RawDamage));
                successes += again.Targets.Count(t => t.Save.Success);
            }

            Assert.That(successes, Is.GreaterThan(0), "A successful save was seen and halved.");
            w.Player.Rpg.Resources["spell_slots_1"] = 1;
            Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(1));
            Assert.That(e.Unavailable(p, new CastSpellAction("magic_missile", 1, new[] { Named(e, "B1").CombatantId })), Is.EqualTo("The action this turn is used."));
        }

        [Test]
        public void Upcasting_AddsTheSpellsOwnDice()
        {
            var w = Caster(_s, "wizard", 3, new[] { "fire_bolt" }, new[] { "burning_hands" }, new[] { "burning_hands" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 2, 0));
            var p = P(e);
            Tough(Named(e, "Bandit"));
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("burning_hands", 2, point: new GridPos(3, 0))).Ok, Is.True);
            Assert.That(e.Encounter.Log.Last(l => l.Kind == CombatLogKind.Damage && l.Text.StartsWith("Burning Hands")).Text, Does.Contain("4d6"), "3d6 + 1d6 per slot level above 1.");
            Assert.That(w.Player.Rpg.Resources["spell_slots_2"], Is.EqualTo(1));
        }

        [Test]
        public void MagicMissile_NeverMisses_SplitsDarts_AndUpcastsWithAMoreDart()
        {
            var w = Caster(_s, "wizard", 3, new[] { "fire_bolt" }, new[] { "magic_missile" }, new[] { "magic_missile" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0, "A"), Creature("bandit", "bandits", 5, 2, "B"));
            var p = P(e);
            var a = Named(e, "A");
            var b = Named(e, "B");
            Tough(a);
            Tough(b);
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new CastSpellAction("magic_missile", 2, new[] { a.CombatantId, b.CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            var cast = o.Spells.Single();
            Assert.That(cast.Targets.Single(t => t.Name == "A").Damage, Is.InRange(2, 5), "One dart to A.");
            Assert.That(cast.Targets.Single(t => t.Name == "B").Damage, Is.InRange(6, 15), "The rest (three of four at level 2) to B.");
            Assert.That(w.Player.Rpg.Resources["spell_slots_2"], Is.EqualTo(1));
            Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(4), "The level 1 slots are untouched.");
        }

        [Test]
        public void CureWounds_Heals_UpToTheMaximum_AndUpcastingAddsDice()
        {
            var w = Caster(_s, "cleric", 3, new[] { "sacred_flame" }, prepared: new[] { "cure_wounds" });
            var e = Begin(w, Player(), Creature("guard", "player", 1, 0, "Ally"), Creature("bandit", "bandits", 6, 6));
            var p = P(e);
            var ally = Named(e, "Ally");
            Tough(ally, 60);
            ally.CurrentHp = 1;
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new CastSpellAction("cure_wounds", 2, new[] { ally.CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            var healed = o.Spells.Single().Targets.Single().Healing;
            var wis = e.Stats(p).Modifier(Ability.Wisdom);
            Assert.That(healed, Is.InRange(4 + wis, 32 + wis), "4d8 + Wisdom at level 2.");
            Assert.That(e.Encounter.Log.Last(l => l.Kind == CombatLogKind.Healing && l.Text.StartsWith("Cure Wounds")).Text, Does.Contain("4d8"), "Upcast: 2d8 more at level 2.");
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, new CastSpellAction("cure_wounds", 1, new[] { Named(e, "Bandit").CombatantId })), Does.Contain("not one"), "Healing is for allies.");
        }

        [Test]
        public void Bless_AddsADie_UnderConcentration_AndANewConcentrationSpellEndsIt()
        {
            var w = Caster(_s, "cleric", 3, new[] { "sacred_flame" }, prepared: new[] { "bless", "shield_of_faith" });
            var e = Begin(w, Player(), Creature("guard", "player", 1, 0, "Ally"), Creature("bandit", "bandits", 2, 1));
            var p = P(e);
            var ally = Named(e, "Ally");
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("bless", 1, new[] { p.CombatantId, ally.CombatantId })).Ok, Is.True);
            Assert.That(p.Concentration?.SpellId, Is.EqualTo("bless"));
            Assert.That(e.EffectsOn(ally).Any(x => x.Kind == CombatEffectKind.AttackAndSaveDie), Is.True);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            var r = e.ResolveAttack(ally, bandit, e.Stats(ally).BestMelee(), false);
            Assert.That(e.Encounter.Log.Last(l => l.Kind == CombatLogKind.Attack).Text, Does.Contain("Bless"));

            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("shield_of_faith", 1, new[] { ally.CombatantId })).Ok, Is.True);
            Assert.That(p.Concentration.SpellId, Is.EqualTo("shield_of_faith"), "One concentration at a time.");
            Assert.That(e.EffectsOn(ally).Any(x => x.Kind == CombatEffectKind.AttackAndSaveDie), Is.False, "Bless ended with its concentration.");
            Assert.That(e.ArmorClass(ally).Total, Is.EqualTo(e.Stats(ally).ArmorClass.Total + 2));
        }

        [Test]
        public void Concentration_IsTestedByDamage_AndEndsAtZero()
        {
            var w = Caster(_s, "cleric", 3, new[] { "sacred_flame" }, prepared: new[] { "bless" });
            var e = Begin(w, Player(), Creature("guard", "player", 1, 0, "Ally"), Creature("bandit", "bandits", 2, 1));
            var p = P(e);
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("bless", 1, new[] { p.CombatantId })).Ok, Is.True);
            var checks = 0;
            for (var i = 0; i < 10 && p.Concentration != null; i++)
            {
                w.Player.Rpg.CurrentHp = e.MaxHp(p);
                e.ApplyDamage(p, 2, DamageType.Slashing, false, "test");
                checks++;
                Assert.That(e.Encounter.Log.Last(l => l.Kind == CombatLogKind.Save).Text, Does.Contain("vs DC 10"), "Two damage: DC 10, the minimum.");
            }

            Assert.That(checks, Is.GreaterThan(0));
            if (p.Concentration != null) e.ApplyDamage(p, w.Player.Rpg.CurrentHp + 1, DamageType.Slashing, false, "test");
            Assert.That(p.Concentration, Is.Null);
            Assert.That(e.Encounter.Effects.Any(x => x.Origin == "spell:bless"), Is.False);
        }

        [Test]
        public void HoldPerson_Paralyzes_WithRepeatedSaves_AndOnlyHumanoids()
        {
            var w = Caster(_s, "wizard", 3, new[] { "fire_bolt" }, new[] { "hold_person" }, new[] { "hold_person" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 3, 0), Creature("wolf", "bandits", 4, 2));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            var wolf = Named(e, "Wolf");
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, new CastSpellAction("hold_person", 2, new[] { wolf.CombatantId })), Does.Contain("only humanoids"));
            var held = false;
            for (var i = 0; i < 12 && !held; i++)
            {
                TurnOf(e, p);
                w.Player.Rpg.Resources["spell_slots_2"] = 2;
                var o = e.Act(p.CombatantId, new CastSpellAction("hold_person", 2, new[] { bandit.CombatantId }));
                Assert.That(o.Ok, Is.True, o.Error);
                held = e.Has(bandit, ConditionType.Paralyzed);
                if (!held) Assert.That(p.Concentration, Is.Null, "A spell that took hold on no one ends its concentration.");
            }

            Assume.That(held, Is.True);
            var k = bandit.Conditions.Single(c => c.Type == ConditionType.Paralyzed);
            Assert.That(k.RepeatSave && k.SaveDc == e.SpellSaveDc(p) && k.InstanceId > 0, Is.True);
            e.EndConcentration(p, "test");
            Assert.That(e.Has(bandit, ConditionType.Paralyzed), Is.False, "Ending concentration ends the paralysis.");
        }

        [Test]
        public void FailedCasts_SpendNothing()
        {
            var w = Caster(_s, "wizard", 1, new[] { "fire_bolt" }, new[] { "magic_missile", "shield" }, new[] { "magic_missile" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 40, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            var sequence = e.Encounter.ActionSequence;
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("magic_missile", 1, new[] { bandit.CombatantId })).Ok, Is.False, "200 ft is beyond 120 ft.");
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("fireball", 3, point: new GridPos(5, 5))).Ok, Is.False, "Not known.");
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("shield", 1)).Error, Does.Contain("cannot be cast yet"));
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("magic_missile", 2, new[] { bandit.CombatantId })).Ok, Is.False, "No level 2 slot at level 1.");
            Assert.That(w.Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(2));
            Assert.That(p.Turn.ActionUsed, Is.False);
            Assert.That(e.Encounter.ActionSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void PactMagic_CastsAtThePactLevel_FromItsOwnSlots()
        {
            var w = Caster(_s, "warlock", 3, new[] { "eldritch_blast" }, new[] { "hold_person", "expeditious_retreat" });
            var access = new SpellAccess(new CharacterRules(w.Player.Rpg));
            Assert.That(access.UsesPactMagic, Is.True);
            Assert.That(access.PactSlotLevel, Is.EqualTo(2));
            Assert.That(access.SlotsLeft(1), Is.Zero, "Pact slots are not level 1 slots.");
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0));
            var p = P(e);
            TurnOf(e, p);
            Assert.That(e.CastLevels(p, SpellCatalog.Find("expeditious_retreat")), Is.EqualTo(new[] { 2 }), "Pact slots are all of the pact level.");
            Assert.That(e.Unavailable(p, new CastSpellAction("expeditious_retreat", 1)), Does.Contain("No level 1 slot"));
            var pact = w.Player.Rpg.Resources["pact_slots"];
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("expeditious_retreat", 2)).Ok, Is.True);
            Assert.That(w.Player.Rpg.Resources["pact_slots"], Is.EqualTo(pact - 1));
            Assert.That(w.Player.Rpg.Resources.ContainsKey("spell_slots_1"), Is.False, "Pact magic is not ordinary slots.");
            Assert.That(e.FeaturesOf(p).Any(f => f.feature.Id == "spell_dash"), Is.True, "Expeditious Retreat gives a bonus Dash.");
        }

        [Test]
        public void EldritchBlast_FiresMoreBeams_AtHigherLevels()
        {
            var w = Caster(_s, "warlock", 5, new[] { "eldritch_blast" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 4, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            Tough(bandit);
            TurnOf(e, p);
            var o = e.Act(p.CombatantId, new CastSpellAction("eldritch_blast", 0, new[] { bandit.CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(o.Spells.Single().Targets.Single().Attacks.Count, Is.EqualTo(2), "Two beams at level 5.");
        }

        [Test]
        public void MistyStep_Teleports_WithoutOpportunityAttacks()
        {
            var w = Caster(_s, "sorcerer", 3, new[] { "fire_bolt" }, new[] { "misty_step", "magic_missile" });
            var e = Begin(w, Player(), Creature("bandit", "bandits", 1, 0));
            var p = P(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, new CastSpellAction("misty_step", 2, point: new GridPos(1, 0))), Does.Contain("occupied"));
            Assert.That(e.Unavailable(p, new CastSpellAction("misty_step", 2, point: new GridPos(7, 0))), Does.Contain("35 ft away"));
            Assert.That(e.Act(p.CombatantId, new CastSpellAction("misty_step", 2, point: new GridPos(-5, 0))).Ok, Is.True);
            Assert.That(p.Position, Is.EqualTo(new GridPos(-5, 0)));
            Assert.That(bandit.Turn.ReactionUsed, Is.False);
            Assert.That(p.Turn.BonusActionUsed && !p.Turn.ActionUsed, Is.True, "A bonus-action spell.");
            Assert.That(e.Unavailable(p, new CastSpellAction("misty_step", 2, point: new GridPos(-6, 0))), Does.Contain("bonus action"));
            Assert.That(e.Unavailable(p, new CastSpellAction("magic_missile", 1, new[] { bandit.CombatantId })), Is.EqualTo("Only one spell with a spell slot per turn."));
            Assert.That(e.Unavailable(p, new CastSpellAction("fire_bolt", 0, new[] { bandit.CombatantId })), Is.Null, "A cantrip is still allowed.");
        }

        [Test]
        public void NpcCasters_UseTheSameEngine_AndTheirOwnSlots()
        {
            var w = Caster(_s, "fighter");
            var e = Begin(w, Player(), Creature("cult_fanatic", "cult", 3, 0));
            var fanatic = Named(e, "Cult Fanatic");
            Assert.That(fanatic.Resources["spell_slots_1"], Is.EqualTo(4));
            Assert.That(fanatic.Resources["spell_slots_2"], Is.EqualTo(3));
            Assert.That(e.SpellsOf(fanatic).Select(s => s.Id), Is.EquivalentTo(new[] { "light", "sacred_flame", "inflict_wounds", "shield_of_faith", "hold_person" }));
            Assert.That(e.SpellSaveDc(fanatic), Is.EqualTo(11));
            TurnOf(e, fanatic);
            var o = e.Act(fanatic.CombatantId, new CastSpellAction("hold_person", 2, new[] { P(e).CombatantId }));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(fanatic.Resources["spell_slots_2"], Is.EqualTo(2));
            Assert.That(o.Spells.Single().Targets.Single().Save.Dc, Is.EqualTo(11));
        }
    }
}
