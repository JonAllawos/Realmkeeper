using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 20A (ADR-0103): the lineage benefits are played through the existing systems. The goliath's six giant
    /// ancestries go through the feature, reaction and teleport paths and spend the ancestry's uses; a tiefling's legacy
    /// cantrip goes through the spell pipeline with an innate casting ability (Charisma); Chill Touch stops healing. What
    /// still has no system (the gnome's utility magic) stays flagged.
    /// </summary>
    public sealed class Phase20ALineageTests
    {
        private string _dir;
        private int _guids;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _guids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private Guid NextGuid(string purpose) => new Guid(++_guids, 0, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x2A });

        // ----- Set-up ------------------------------------------------------------------------------------

        private static CharacterDraft Draft(string species, string cls, string choice, string option)
        {
            var d = new CharacterDraft();
            d.Choices.Name = "Ysolde Marr";
            d.SetSpecies(species);
            d.SetBackground("hermit");
            d.SetClass(cls);
            var c = RpgCatalog.FindClass(cls);
            foreach (var s in c.SkillOptions) if (d.Picked(ClassDefinition.SkillChoiceId).Count < c.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var ch in c.Choices.Where(x => x.Kind == ChoiceKind.Option)) d.Toggle(ch.Id, ch.Options[0].Id);
            foreach (var ch in RpgCatalog.FindSpecies(species).Choices.Where(x => x.Kind == ChoiceKind.Skill)) d.Toggle(ch.Id, nameof(Skill.Insight));
            if (choice != null) d.Toggle(choice, option);
            if (species == "tiefling") d.Toggle(RpgCatalog.LegacyAbilityChoiceId, "charisma"); // Phase 22 (rules 10): the legacy's ability, as these tests assume
            return d;
        }

        /// <summary>A world whose player is a level 1 <paramref name="cls"/> of the given species and lineage.</summary>
        private WorldData Hero(string species, string choice, string option, string cls = "fighter")
        {
            var dir = Path.Combine(_dir, species + option + cls);
            Directory.CreateDirectory(dir);
            var s = Phase2Worlds.Session(dir);
            var r = s.Dispatch(new CreateCharacterCommand(Draft(species, cls, choice, option).Clean()));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
            var w = s.CurrentWorldSnapshotForTests();
            var rules = new CharacterRules(w.Player.Rpg);
            foreach (var pair in rules.ResourceMaximums()) w.Player.Rpg.Resources[pair.Key] = pair.Value;
            return w;
        }

        private WorldData Goliath(string ancestry) => Hero("goliath", "species:giant_ancestry", ancestry);

        private static ParticipantSetup Player(int x = 0, int y = 0) => new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player };

        private static ParticipantSetup Creature(string id, int x, int y, string side = "foes") =>
            new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = id, Side = side, Position = new GridPos(x, y) };

        private CombatEngine Begin(WorldData w, params ParticipantSetup[] ps)
        {
            var enc = CombatEngine.Create(w, new EncounterSetup { Name = "Lineage test", Kind = EncounterKind.Other, Participants = ps.ToList() }, NextGuid, out var errors);
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

        private static CombatantData P(CombatEngine e) => e.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);
        private static CombatantData Foe(CombatEngine e) => e.Encounter.Combatants.First(c => c.Kind != CombatantKind.Player);

        private static void Tough(CombatantData c, int hp = 300)
        {
            c.MaxHp = hp;
            c.CurrentHp = hp;
            c.State = VitalState.Conscious;
            c.Conditions.Clear();
        }

        private static UseFeatureAction F(string id, GridPos? point = null) => new UseFeatureAction(id, default, null, 0, point);

        private static int Uses(WorldData w) => w.Player.Rpg.Resources["stones_endurance"];

        /// <summary>Lands an unarmed hit on <paramref name="foe"/> on a fresh turn (the dice are deterministic; retries until one hits).</summary>
        private static void HitOnce(CombatEngine e, CombatantData p, CombatantData foe)
        {
            for (var i = 0; i < 60; i++)
            {
                Tough(foe);
                TurnOf(e, p);
                if (e.ResolveAttack(p, foe, e.Stats(p).Find("unarmed"), false).Hit) return;
            }

            Assert.Fail("No hit in 60 tries.");
        }

        // ----- Data: what is played, and what still is not ----------------------------------------------

        [Test]
        public void TheGoliathAndTieflingLineages_AreNoLongerFlagged_TheGnomesUtilityMagicStillIs()
        {
            foreach (var o in RpgCatalog.FindSpecies("goliath").Choices.Single().Options)
                Assert.That(o.Needs | CharacterRules.NeedsOf(o.Effects), Is.EqualTo(IntegrationNeed.None), "goliath " + o.Id + " is played by combat now.");
            foreach (var o in RpgCatalog.FindSpecies("tiefling").Choices.Single(c => c.Id == "species:fiendish_legacy").Options)
                Assert.That(o.Needs | CharacterRules.NeedsOf(o.Effects), Is.EqualTo(IntegrationNeed.None), "tiefling " + o.Id + " is played now.");
            foreach (var o in RpgCatalog.FindSpecies("gnome").Choices.Single().Options)
                Assert.That((o.Needs | CharacterRules.NeedsOf(o.Effects)) & IntegrationNeed.Magic, Is.EqualTo(IntegrationNeed.Magic),
                    "gnome " + o.Id + ": Minor Illusion, Speak with Animals and Mending have no system yet, and say so.");

            Assert.That(CharacterRules.IsPlayedCombatRule("breath-weapon"), Is.False, "The dragonborn's breath weapon is still not played, and stays flagged.");
            Assert.That(CharacterRules.IsPlayedInnateMagic("minor-illusion"), Is.False);
            foreach (var key in new[] { "teleport", "extra-fire-damage", "extra-cold-slow", "knock-prone", "reduce-damage", "retaliate-thunder" })
                Assert.That(CharacterRules.IsPlayedCombatRule(key), Is.True, key);

            var chill = SpellCatalog.Find("chill_touch");
            Assert.That(chill, Is.Not.Null);
            Assert.That(chill.IsCantrip && chill.Touch && chill.Resolution == SpellResolution.Attack, Is.True, "SRD 5.2.1: a touch, a melee spell attack.");
            Assert.That(chill.Effects.Any(x => x.Kind == CombatEffectKind.NoHealing && x.UntilCastersNextTurnEnds), Is.True);
            Assert.That(chill.Classes, Is.Empty, "Not added to any class list (that would change generated casters' picks).");
            Assert.That(SpellCatalog.Validate(), Is.Empty);
        }

        [Test]
        public void EachBenefit_IsShownAsActive_Conditional_OrNotYetPlayed()
        {
            ChoiceOption Option(string species, string id) => RpgCatalog.FindSpecies(species).Choices.Single(c => c.Kind == ChoiceKind.Option && c.Id != RpgCatalog.LegacyAbilityChoiceId).Options.Single(o => o.Id == id);

            foreach (var a in new[] { "cloud", "fire", "frost", "hill", "stone", "storm" })
            {
                var (state, text) = BenefitStatus.Of(Option("goliath", a));
                Assert.That(state, Is.EqualTo(BenefitState.Conditional), a);
                Assert.That(text, Does.StartWith("Played in a fight"), a);
            }

            var infernal = BenefitStatus.Of(Option("tiefling", "infernal"));
            Assert.That(infernal.state, Is.EqualTo(BenefitState.Conditional), "Fire resistance is always on; the cantrip is cast when chosen.");
            Assert.That(infernal.text, Does.Contain("cantrip").And.Contain("spellcasting ability")); // Phase 22: the ability is chosen (rules 10)
            Assert.That(BenefitStatus.Of(Option("gnome", "forest")).state, Is.EqualTo(BenefitState.NotYetPlayed));
            Assert.That(BenefitStatus.Of(Option("gnome", "forest")).text, Does.Contain("magic"));
            Assert.That(BenefitStatus.Of(Option("gnome", "rock")).state, Is.EqualTo(BenefitState.NotYetPlayed), "Tinker's tools work, but Mending does not: the option says so.");
            Assert.That(BenefitStatus.Of(Option("dragonborn", "fire")).state, Is.EqualTo(BenefitState.Active), "A resistance alone: always in effect.");
            var dark = RpgCatalog.FindSpecies("tiefling").Traits.First(t => t.Effects.Any(e => e.Kind == EffectKind.Darkvision));
            Assert.That(BenefitStatus.Of(dark).state, Is.EqualTo(BenefitState.Active));
            var breath = RpgCatalog.FindSpecies("dragonborn").Traits.Single(t => t.Id == "breath_weapon");
            Assert.That(BenefitStatus.Of(breath).state, Is.EqualTo(BenefitState.NotYetPlayed), "The breath weapon is still not played, and says so.");
        }

        // ----- Goliath ------------------------------------------------------------------------------------

        [Test]
        public void FiresBurn_FollowsAHit_DealsFire_SpendsAUse_AndAnswersEachHitOnce()
        {
            var w = Goliath("fire");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            Tough(bandit);
            TurnOf(e, p);
            Assert.That(e.FeaturesOf(p).Select(x => x.feature.Id), Has.Member("fires_burn").And.No.Member("frosts_chill").And.No.Member("clouds_jaunt"), "Only the chosen ancestry.");
            Assert.That(e.Unavailable(p, F("fires_burn")), Does.Contain("Hit a creature"));

            var uses = Uses(w);
            Assert.That(uses, Is.EqualTo(2), "Proficiency bonus uses at level 1.");
            HitOnce(e, p, bandit);
            var before = e.Hp(bandit);
            var o = e.Act(p.CombatantId, F("fires_burn"));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(Uses(w), Is.EqualTo(uses - 1));
            Assert.That(e.Hp(bandit), Is.LessThan(before), "1d10 fire.");
            Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("Fire's Burn") && l.Text.Contains("fire")), Is.True);
            Assert.That(e.Unavailable(p, F("fires_burn")), Does.Contain("already answered"), "One boon per hit.");

            HitOnce(e, p, bandit);
            Assert.That(e.Act(p.CombatantId, F("fires_burn")).Ok, Is.True, "A new hit can be answered again.");
            Assert.That(Uses(w), Is.EqualTo(0));
            HitOnce(e, p, bandit);
            Assert.That(e.Unavailable(p, F("fires_burn")), Does.Contain("Giant Ancestry"), "No uses left: refused, by name.");
        }

        [Test]
        public void ACompanionGoliath_AnswersItsOwnHit_WithItsAncestry()
        {
            var w = Goliath("frost");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            TurnOf(e, p);
            Assert.That(CombatAi.ForCompanion(e, p, out _), Is.Not.InstanceOf<UseFeatureAction>(), "Nothing to answer before a hit.");
            HitOnce(e, p, bandit);
            var choice = CombatAi.ForCompanion(e, p, out var reason);
            Assert.That(choice, Is.InstanceOf<UseFeatureAction>());
            Assert.That(((UseFeatureAction)choice).FeatureId, Is.EqualTo("frosts_chill"));
            Assert.That(reason, Does.Contain("Frost's Chill"));
            Assert.That(e.Act(p.CombatantId, choice).Ok, Is.True);
            var after = CombatAi.ForCompanion(e, p, out _);
            Assert.That(after is UseFeatureAction f && f.FeatureId == "frosts_chill", Is.False, "Once per hit.");
        }

        [Test]
        public void FrostsChill_SlowsByTenFeet_UntilTheGoliathsNextTurn()
        {
            var w = Goliath("frost");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            HitOnce(e, p, bandit);
            var speed = e.Speed(bandit);
            var before = e.Hp(bandit);
            Assert.That(e.Act(p.CombatantId, F("frosts_chill")).Ok, Is.True);
            Assert.That(e.Hp(bandit), Is.LessThan(before), "1d6 cold.");
            Assert.That(e.Speed(bandit), Is.EqualTo(speed - 10));
            Assert.That(e.Encounter.Effects.Any(x => x.TargetId == bandit.CombatantId && x.Name == "Frost's Chill" && x.Expiry == EffectExpiry.StartOfTurn), Is.True);
        }

        [Test]
        public void HillsTumble_KnocksProne_ButNotWhatIsAlreadyDown()
        {
            var w = Goliath("hill");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            HitOnce(e, p, bandit);
            Assert.That(e.Act(p.CombatantId, F("hills_tumble")).Ok, Is.True);
            Assert.That(bandit.Conditions.Any(c => c.Type == ConditionType.Prone), Is.True);
            p.Turn.LastHitBoonUsed = false; // the same target, already prone
            Assert.That(e.Unavailable(p, F("hills_tumble")), Does.Contain("already prone"));
            Assert.That(e.SizeOf(bandit), Is.EqualTo(CreatureSize.Medium));
            Assert.That(e.SizeOf(p), Is.EqualTo(CreatureSize.Medium), "A goliath is Medium.");
        }

        [Test]
        public void CloudsJaunt_TeleportsUpTo30Feet_AsABonusAction_ToAFreeSquare()
        {
            var w = Goliath("cloud");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, F("clouds_jaunt")), Does.Contain("Choose a square"));
            Assert.That(e.Unavailable(p, F("clouds_jaunt", new GridPos(7, 0))), Does.Contain("30 ft"));
            Assert.That(e.Unavailable(p, F("clouds_jaunt", new GridPos(1, 0))), Does.Contain("occupied"));
            var uses = Uses(w);
            var o = e.Act(p.CombatantId, F("clouds_jaunt", new GridPos(-5, 2)));
            Assert.That(o.Ok, Is.True, o.Error);
            Assert.That(p.Position, Is.EqualTo(new GridPos(-5, 2)));
            Assert.That(p.Turn.BonusActionUsed, Is.True);
            Assert.That(p.Turn.MovementUsed, Is.EqualTo(0), "Teleporting is not movement.");
            Assert.That(Uses(w), Is.EqualTo(uses - 1));
            Assert.That(e.Unavailable(p, F("clouds_jaunt", new GridPos(-4, 2))), Does.Contain("bonus action"));

            var record = ActionCodec.ToRecord(F("clouds_jaunt", new GridPos(3, 4)));
            Assert.That(((UseFeatureAction)ActionCodec.FromRecord(record)).Point, Is.EqualTo(new GridPos(3, 4)), "The destination survives a suspended action's record.");
        }

        [Test]
        public void StonesEndurance_IsOfferedWhenAnAttackHurts_AndReducesTheDamage()
        {
            var w = Goliath("stone");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            var asked = false;
            for (var i = 0; i < 40 && !asked; i++)
            {
                w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp;
                TurnOf(e, bandit);
                p.Turn = new TurnResources();
                var o = e.Act(bandit.CombatantId, new AttackAction(p.CombatantId, "scimitar"));
                if (o.Pending == null) continue;
                asked = true;
                Assert.That(o.Pending.Opportunity.Kind, Is.EqualTo(ReactionKind.StonesEndurance));
                Assert.That(o.Pending.Opportunity.Cost, Does.Contain("Giant Ancestry"));
                Assert.That(o.Pending.Opportunity.Consequence, Does.Contain("1d12"));
                var uses = Uses(w);
                var answer = e.Respond(o.Pending.Id, true);
                Assert.That(answer.Ok, Is.True, answer.Error);
                Assert.That(Uses(w), Is.EqualTo(uses - 1));
                Assert.That(p.Turn.ReactionUsed, Is.True);
                Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("Stone's Endurance")), Is.True);
            }

            Assert.That(asked, Is.True, "A hit was offered to Stone's Endurance.");

            w.Player.Rpg.Resources["stones_endurance"] = 0;
            for (var i = 0; i < 20; i++)
            {
                w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp;
                TurnOf(e, bandit);
                p.Turn = new TurnResources();
                Assert.That(e.Act(bandit.CombatantId, new AttackAction(p.CombatantId, "scimitar")).Pending, Is.Null, "No uses left: never offered.");
            }
        }

        [Test]
        public void StormsThunder_AnswersTheAttacker_WithThunder()
        {
            var w = Goliath("storm");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            var asked = false;
            for (var i = 0; i < 40 && !asked; i++)
            {
                w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp;
                Tough(bandit);
                TurnOf(e, bandit);
                p.Turn = new TurnResources();
                var o = e.Act(bandit.CombatantId, new AttackAction(p.CombatantId, "scimitar"));
                if (o.Pending == null) continue;
                asked = true;
                Assert.That(o.Pending.Opportunity.Kind, Is.EqualTo(ReactionKind.StormsThunder));
                var before = e.Hp(bandit);
                var answer = e.Respond(o.Pending.Id, true);
                Assert.That(answer.Ok, Is.True, answer.Error);
                Assert.That(e.Hp(bandit), Is.LessThan(before), "1d8 thunder.");
                Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("Storm's Thunder") && l.Text.Contains("thunder")), Is.True);
            }

            Assert.That(asked, Is.True);
        }

        [Test]
        public void AComputerGoliath_DecidesItsStonesEndurance_ThroughTheSamePath()
        {
            var w = Goliath("stone");

            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            p.Control = CombatControl.Ai; // as a companion or a computer-run character is
            var bandit = Foe(e);
            var used = false;
            for (var i = 0; i < 40 && !used; i++)
            {
                w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp;
                TurnOf(e, bandit);
                p.Turn = new TurnResources();
                var o = e.Act(bandit.CombatantId, new AttackAction(p.CombatantId, "scimitar"));
                Assert.That(o.Pending, Is.Null, "Nobody is asked.");
                used = p.Turn.ReactionUsed;
            }

            Assert.That(used, Is.True, "A big enough blow is braced against.");
        }

        // ----- Tiefling -----------------------------------------------------------------------------------

        [TestCase("infernal", "fire_bolt")]
        [TestCase("abyssal", "poison_spray")]
        [TestCase("chthonic", "chill_touch")]
        public void ATieflingFighter_CastsTheLegacyCantrip_WithCharisma(string legacy, string spell)
        {
            var w = Hero("tiefling", "species:fiendish_legacy", legacy);
            var rules = new CharacterRules(w.Player.Rpg);
            Assert.That(rules.Class.Spellcasting, Is.Null, "A fighter: no class spellcasting at all.");
            Assert.That(rules.InnateSpells.Select(x => x.spellId), Is.EqualTo(new[] { spell }));
            Assert.That(rules.InnateSpells[0].ability, Is.EqualTo(Ability.Charisma));

            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            Assert.That(e.SpellsOf(p).Select(s => s.Id), Is.EqualTo(new[] { spell }));
            var def = SpellCatalog.Find(spell);
            Assert.That(e.InnateAbility(p, def), Is.EqualTo(Ability.Charisma));
            var expected = rules.InnateSpellAttack(Ability.Charisma).Total;
            Assert.That(expected, Is.EqualTo(rules.ProficiencyBonus + rules.Modifier(Ability.Charisma)));

            var hit = false;
            for (var i = 0; i < 40 && !hit; i++)
            {
                Tough(bandit);
                TurnOf(e, p);
                var o = e.Act(p.CombatantId, new CastSpellAction(spell, 0, new[] { bandit.CombatantId }));
                Assert.That(o.Ok, Is.True, o.Error);
                var roll = o.Spells[0].Targets[0].Attacks[0];
                Assert.That(roll.AttackBonus.Total, Is.EqualTo(expected), "Proficiency + Charisma, not a class's ability.");
                hit = roll.Hit;
                if (hit) Assert.That(e.Hp(bandit), Is.LessThan(300));
            }

            Assert.That(hit, Is.True);
        }

        [Test]
        public void ATieflingWizard_CastsFireBoltWithIntelligence_WhenTheClassAlreadyReadiesIt()
        {
            var w = Hero("tiefling", "species:fiendish_legacy", "infernal", "wizard");
            w.Player.Rpg.Choices["class:cantrips"] = new System.Collections.Generic.List<string> { "fire_bolt", "ray_of_frost", "shocking_grasp" };
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            Assert.That(e.SpellsOf(p).Count(s => s.Id == "fire_bolt"), Is.EqualTo(1), "Listed once.");
            Assert.That(e.InnateAbility(p, SpellCatalog.Find("fire_bolt")), Is.Null, "The class's own Fire Bolt (Intelligence).");
        }

        [Test]
        public void ChillTouch_StopsTheTargetHealing_UntilTheCastersNextTurnEnds()
        {
            var w = Hero("tiefling", "species:fiendish_legacy", "chthonic");
            var e = Begin(w, Player(), Creature("bandit", 1, 0));
            var p = P(e);
            var bandit = Foe(e);
            var hit = false;
            for (var i = 0; i < 40 && !hit; i++)
            {
                Tough(bandit, 50);
                TurnOf(e, p);
                var o = e.Act(p.CombatantId, new CastSpellAction("chill_touch", 0, new[] { bandit.CombatantId }));
                Assert.That(o.Ok, Is.True, o.Error);
                hit = o.Spells[0].Targets[0].Attacks[0].Hit;
            }

            Assert.That(hit, Is.True);
            Assert.That(e.Encounter.Effects.Any(x => x.TargetId == bandit.CombatantId && x.Kind == CombatEffectKind.NoHealing && x.Expiry == EffectExpiry.EndOfTurn), Is.True);
            var hp = e.Hp(bandit);
            e.Heal(bandit, 10, "a test potion");
            Assert.That(e.Hp(bandit), Is.EqualTo(hp), "No hit points regained.");
            Assert.That(e.Encounter.Log.Any(l => l.Text.Contains("cannot regain hit points")), Is.True);
        }

        [Test]
        public void TheGnome_KeepsItsLineage_AndKnowsNoCastableInnateSpell()
        {
            var forest = new CharacterRules(Draft("gnome", "fighter", "species:gnome_lineage", "forest").Clean() is var c ? CharacterCommandHandlers.BuildSheet(c, out _) : null);
            Assert.That(forest.InnateSpells, Is.Empty, "Minor Illusion and Speak with Animals are not catalog spells; nothing is faked.");
            Assert.That(forest.Features.Any(f => f.feature.Id == "species:gnome_lineage:forest" && (CharacterRules.Needs(f.feature) & IntegrationNeed.Magic) != 0), Is.True,
                "Flagged as waiting for magic.");
        }
    }
}
