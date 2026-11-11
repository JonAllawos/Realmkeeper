using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Tests.EditMode.Character;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>Phase 8: armour, the combat engine (initiative, turns, actions, movement, attacks, damage, death, conditions) and the baseline AI.</summary>
    public sealed class CombatTests
    {
        private string _dir;
        private WorldSession _s;
        private int _guids;

        // Paladin and wizard with Str 8, Dex 14 (+2): point buy 8/14/13/12/10/15; the hermit background raises Con, Wis and Cha only.
        internal static readonly int[] Scores = { 8, 14, 13, 12, 10, 15 };

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

        // ----- Helpers -------------------------------------------------------------------------------------

        internal static void Create(WorldSession s, string cls, int[] scores = null)
        {
            var r = s.Dispatch(new CreateCharacterCommand(ProgressionTests.Choices(cls, "human", "hermit", scores)));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
        }

        private Guid NextGuid(string purpose)
        {
            _guids++;
            var b = new byte[16];
            BitConverter.GetBytes(_guids).CopyTo(b, 0);
            b[15] = 0x58;
            return new Guid(b);
        }

        /// <summary>Enters the first town (practice fights happen in a settlement; a town has a guard post).</summary>
        internal static void EnterTown(WorldSession s)
        {
            var town = s.CurrentWorldSnapshotForTests().Settlements.First(x => x.SettlementType == SettlementType.Town);
            var r = s.Dispatch(new EnterSettlementCommand(town.SettlementId));
            Assert.That(r.IsSuccess, Is.True, r.Message);
        }

        internal static void Equip(PlayerCharacterData p, EquipmentSlot slot, string itemId)
        {
            if (itemId == null) { p.Equipment.Remove(slot); return; }
            p.Equipment[slot] = new ItemStackData { StackId = new ItemStackId(Guid.NewGuid()), ItemId = itemId, Quantity = 1 };
        }

        private static ParticipantSetup Player(int x = 0, int y = 0) => new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(x, y), Control = CombatControl.Player };

        private static ParticipantSetup Creature(string id, string side, int x, int y, string name = null) =>
            new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = id, Side = side, Position = new GridPos(x, y), Name = name };

        private CombatEngine Begin(WorldData w, Lethality lethality, params ParticipantSetup[] ps)
        {
            var setup = new EncounterSetup { Name = "Test fight", Kind = EncounterKind.Other, Lethality = lethality, Participants = ps.ToList() };
            var enc = CombatEngine.Create(w, setup, NextGuid, out var errors);
            Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
            w.Encounters.Add(enc);
            var e = new CombatEngine(w, enc);
            e.Begin();
            return e;
        }

        /// <summary>Makes it this combatant's turn with fresh resources (the order itself is tested separately).</summary>
        private static void TurnOf(CombatEngine e, CombatantData c)
        {
            e.Encounter.TurnIndex = e.Encounter.Order.IndexOf(c.CombatantId);
            c.Turn = new TurnResources();
        }

        private static CombatantData Named(CombatEngine e, string name) => e.Encounter.Combatants.Single(c => c.Name == name);
        private static CombatantData ThePlayer(CombatEngine e) => e.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);

        private WorldData Fighter(string weapon = "sword_arming")
        {
            Create(_s, "fighter");
            var w = W;
            Equip(w.Player, EquipmentSlot.MainHand, weapon);
            Equip(w.Player, EquipmentSlot.OffHand, null);
            return w;
        }

        private static void Revive(CombatantData c)
        {
            c.State = VitalState.Conscious;
            c.CurrentHp = c.MaxHp;
            c.TemporaryHp = 0;
            c.Conditions.Clear();
        }

        // ===== Armour (SRD 5.2.1) ============================================================================

        [Test]
        public void ArmourClass_FollowsSrdArmourCategories_AndTheShield()
        {
            Create(_s, "paladin", Scores);
            var p = W.Player;
            int Ac() => new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).ArmorClass(p).Total;
            Equip(p, EquipmentSlot.OffHand, null);

            Equip(p, EquipmentSlot.Chest, null);
            Assert.That(Ac(), Is.EqualTo(10 + 2), "Unarmoured: 10 + Dex.");
            Equip(p, EquipmentSlot.Chest, "jerkin_leather");
            Assert.That(Ac(), Is.EqualTo(11 + 2), "Leather: 11 + Dex.");
            Equip(p, EquipmentSlot.Chest, "jerkin_forester");
            Assert.That(Ac(), Is.EqualTo(12 + 2), "Studded leather: 12 + Dex.");
            Equip(p, EquipmentSlot.Chest, "chain_shirt");
            Assert.That(Ac(), Is.EqualTo(13 + 2), "Chain shirt: 13 + Dex (max 2).");
            Equip(p, EquipmentSlot.Chest, "chain_mail");
            Assert.That(Ac(), Is.EqualTo(16), "Chain mail: 16, no Dex.");
            Equip(p, EquipmentSlot.OffHand, "shield_round");
            Assert.That(Ac(), Is.EqualTo(18), "A shield adds 2.");
            var text = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).ArmorClass(p).Text;
            Assert.That(text, Does.Contain("Chain mail").IgnoreCase.Or.Contain("chain").IgnoreCase, "The breakdown names the armour.");
        }

        [Test]
        public void HeavyArmour_WithoutTheStrength_CostsTenFeetOfSpeed()
        {
            Create(_s, "paladin", Scores);
            var p = W.Player;
            var bare = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
            Equip(p, EquipmentSlot.Chest, "chain_mail");
            var heavy = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
            Assert.That(heavy.Score(Ability.Strength), Is.LessThan(13));
            Assert.That(heavy.ArmorTooHeavy, Is.True);
            Equip(p, EquipmentSlot.Chest, "jerkin_leather");
            Assert.That(new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).Speed, Is.EqualTo(30));
            Equip(p, EquipmentSlot.Chest, "chain_mail");
            Assert.That(heavy.Speed, Is.EqualTo(20), "Strength 8 < 13: speed −10.");
            Assert.That(bare.ArmorTooHeavy, Is.False);
        }

        [Test]
        public void UntrainedArmour_GivesDisadvantage_AndAnUntrainedShieldGivesNoArmourClass()
        {
            Create(_s, "wizard", Scores);
            var p = W.Player;
            Equip(p, EquipmentSlot.Chest, "jerkin_leather");
            Equip(p, EquipmentSlot.OffHand, "shield_round");
            var rules = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
            Assert.That(rules.UntrainedArmor, Is.True);
            Assert.That(rules.ArmorClass(p).Total, Is.EqualTo(11 + 2), "Leather counts; the untrained shield does not.");
            Assert.That(rules.ArmorClass(p).Text, Does.Contain("shield").IgnoreCase, "The breakdown explains the shield.");
            Assert.That(rules.RollFor("attack:Strength").Disadvantage, Is.Not.Empty);
            Assert.That(rules.RollFor("save:Dexterity").Disadvantage, Is.Not.Empty);
            Assert.That(rules.RollFor("save:Wisdom").Disadvantage, Is.Empty, "Only Strength and Dexterity tests.");
        }

        [Test]
        public void Items_CarrySrdWeaponAndArmourData()
        {
            var sword = ItemCatalog.Find("sword_arming");
            Assert.That(sword.Weapon.Damage.Sides, Is.EqualTo(8));
            Assert.That(sword.Weapon.Versatile.Sides, Is.EqualTo(10));
            Assert.That(sword.Weapon.DamageType, Is.EqualTo(DamageType.Slashing));
            var dagger = ItemCatalog.Find("dagger_iron").Weapon;
            Assert.That(dagger.Has(WeaponProperty.Finesse) && dagger.Has(WeaponProperty.Light) && dagger.Has(WeaponProperty.Thrown), Is.True);
            Assert.That((dagger.Range, dagger.LongRange), Is.EqualTo((20, 60)));
            Assert.That(ItemCatalog.Find("chain_mail").Armor.StrengthRequirement, Is.EqualTo(13));
            Assert.That(ItemCatalog.Find("chain_mail").Armor.StealthDisadvantage, Is.True);
            foreach (var item in ItemCatalog.Items.Where(i => i.Weapon != null || i.Armor != null))
                Assert.That(item.RulesSource, Is.Not.Null.And.Not.Empty, item.Id + " names its rules source.");
        }

        // ===== Encounters and initiative =====================================================================

        [Test]
        public void Encounter_RefusesDuplicates_NoHostility_AndASecondActiveFight()
        {
            var w = Fighter();
            var setup = new EncounterSetup { Participants = { Player(), Player(1, 1) } };
            Assert.That(CombatEngine.Create(w, setup, NextGuid, out var e1), Is.Null);
            Assert.That(e1, Has.Some.Contains("only once"));

            var guard = w.Npcs.First(n => !n.IsDeceased);
            setup = new EncounterSetup
            {
                Participants =
                {
                    new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = guard.NpcId, Side = "a", Position = new GridPos(0, 0) },
                    new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = guard.NpcId, Side = "b", Position = new GridPos(3, 0) },
                },
            };
            Assert.That(CombatEngine.Create(w, setup, NextGuid, out var e2), Is.Null);
            Assert.That(e2, Has.Some.Contains("already taking part"), "A resident is one person: never duplicated.");

            setup = new EncounterSetup { Participants = { Player(), Creature("bandit", "player", 1, 0) } };
            Assert.That(CombatEngine.Create(w, setup, NextGuid, out var e3), Is.Null);
            Assert.That(e3, Has.Some.Contains("hostile"));

            Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 3, 0));
            setup = new EncounterSetup { Participants = { Player(), Creature("bandit", "bandits", 3, 0) } };
            Assert.That(CombatEngine.Create(w, setup, NextGuid, out var e4), Is.Null);
            Assert.That(e4, Has.Some.Contains("still going on"));
        }

        [Test]
        public void Initiative_IsOrderedHighestFirst_WithDeterministicTieBreaks()
        {
            var w = Fighter();
            var ps = new List<ParticipantSetup> { Player() };
            for (var i = 0; i < 6; i++) ps.Add(Creature("wolf", "wolves", 4 + i, 4));
            for (var i = 0; i < 6; i++) ps.Add(Creature("bandit", "bandits", -4 - i, -4));
            var e = Begin(w, Lethality.Lethal, ps.ToArray());
            var order = e.Encounter.Order.Select(id => e.Encounter.Find(id)).ToList();
            Assert.That(order.Count, Is.EqualTo(13));
            for (var i = 0; i + 1 < order.Count; i++)
            {
                Assert.That(order[i].Initiative, Is.GreaterThanOrEqualTo(order[i + 1].Initiative));
                if (order[i].Initiative == order[i + 1].Initiative)
                    Assert.That(e.Stats(order[i]).Modifier(Ability.Dexterity), Is.GreaterThanOrEqualTo(e.Stats(order[i + 1]).Modifier(Ability.Dexterity)), "Ties: higher Dexterity first.");
            }

            foreach (var c in order)
                Assert.That(c.Initiative, Is.EqualTo(c.InitiativeRoll + e.Stats(c).InitiativeBonus().Total), "Initiative = d20 + Dexterity (and bonuses).");

            // The same encounter (same seed, same IDs) gives the same order.
            _guids = 0;
            var w2 = W;
            Equip(w2.Player, EquipmentSlot.MainHand, "sword_arming");
            Equip(w2.Player, EquipmentSlot.OffHand, null);
            var again = Begin(w2, Lethality.Lethal, ps.ToArray());
            Assert.That(again.Encounter.Order, Is.EqualTo(e.Encounter.Order));
        }

        [Test]
        public void Reinforcements_JoinInInitiativeOrder_WithoutChangingWhoseTurnItIs()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 3, 0));
            var active = e.Encounter.Active;
            Assert.That(e.Join(Creature("wolf", "bandits", 5, 5, "Grey wolf"), NextGuid), Is.Null);
            Assert.That(e.Encounter.Active, Is.SameAs(active));
            Assert.That(e.Encounter.Order.Count, Is.EqualTo(3));
            Assert.That(e.Encounter.Log.Any(l => l.Kind == CombatLogKind.Joined), Is.True);
            Assert.That(e.Join(Creature("wolf", "bandits", 5, 5), NextGuid), Does.Contain("stand on"));
        }

        // ===== Action economy ================================================================================

        [Test]
        public void OneAction_PerTurn_AndEndTurnPassesToTheNext()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            bandit.MaxHp = bandit.CurrentHp = 500; // survives the attack, so the fight goes on
            TurnOf(e, p);
            var ok = e.Act(p.CombatantId, new AttackAction(bandit.CombatantId, "weapon"));
            Assert.That(ok.Ok, Is.True, ok.Error);
            if (bandit.State != VitalState.Conscious) Revive(bandit);
            Assert.That(e.Unavailable(p, new AttackAction(bandit.CombatantId, "weapon")), Is.EqualTo("The action this turn is used."));
            Assert.That(e.Unavailable(p, new DashAction()), Is.EqualTo("The action this turn is used."));
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(0, 1))), Is.Null, "Movement is separate from the action.");
            Assert.That(e.Act(bandit.CombatantId, new EndTurnAction()).Ok, Is.False, "Only the active combatant acts.");
            var round = e.Encounter.Round;
            Assert.That(e.Act(p.CombatantId, new EndTurnAction()).Ok, Is.True);
            Assert.That(e.Encounter.Active, Is.Not.SameAs(p));
            Assert.That(e.Encounter.Round, Is.GreaterThanOrEqualTo(round));
        }

        [Test]
        public void Dash_Disengage_Dodge_AndSecondWind_UseTheRightResources()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            var speed = e.Speed(p);
            Assert.That(e.Act(p.CombatantId, new DashAction()).Ok, Is.True);
            Assert.That(e.MovementLeft(p), Is.EqualTo(speed * 2));

            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new DodgeAction()).Ok, Is.True);
            Assert.That(p.Turn.Dodging, Is.True);

            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new DisengageAction()).Ok, Is.True);
            Assert.That(e.Act(p.CombatantId, new MoveAction(new GridPos(-1, 0), new GridPos(-2, 0))).Ok, Is.True);
            Assert.That(bandit.Turn.ReactionUsed, Is.False, "Disengaging provokes no opportunity attack.");

            TurnOf(e, p);
            var sheet = w.Player.Rpg;
            sheet.CurrentHp = 1;
            var uses = sheet.Resources["second_wind"];
            Assert.That(e.Act(p.CombatantId, new BonusAbilityAction("second_wind")).Ok, Is.True);
            Assert.That(sheet.Resources["second_wind"], Is.EqualTo(uses - 1));
            Assert.That(sheet.CurrentHp, Is.GreaterThan(1));
            Assert.That(e.Unavailable(p, new BonusAbilityAction("second_wind")), Is.EqualTo("The bonus action this turn is used."));
            Assert.That(p.Turn.ActionUsed, Is.False, "A bonus action leaves the action.");
        }

        // ===== Movement ======================================================================================

        [Test]
        public void Movement_CostsFiveFeetASquare_DoubleInDifficultTerrain_AndCannotPassEnemies()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 10, 10));
            e.Encounter.DifficultTerrain.Add(new GridPos(0, 1));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            var speed = e.Speed(p);
            var straight = Enumerable.Range(1, speed / 5 + 1).Select(i => new GridPos(i, 0)).ToArray();
            Assert.That(e.Unavailable(p, new MoveAction(straight)), Does.Contain("needs " + (speed + 5) + " ft"));
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(2, 0))), Does.Contain("not next to"));
            Assert.That(e.Act(p.CombatantId, new MoveAction(new GridPos(0, 1))).Ok, Is.True);
            Assert.That(e.MovementLeft(p), Is.EqualTo(speed - 10), "Difficult terrain costs 10 ft.");
            Assert.That(e.Act(p.CombatantId, new MoveAction(new GridPos(1, 2))).Ok, Is.True, "Diagonals cost 5 ft.");
            Assert.That(e.MovementLeft(p), Is.EqualTo(speed - 15));

            bandit.Position = new GridPos(2, 3);
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(2, 3), new GridPos(3, 4))), Does.Contain("blocks"));
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(2, 3))), Does.Contain("blocks").Or.Contain("occupied"));
        }

        [Test]
        public void LeavingReach_ProvokesOneOpportunityAttack_PerReaction()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            w.Player.Rpg.CurrentHp = 200 > w.Player.Rpg.CurrentHp ? w.Player.Rpg.CurrentHp : 200;
            TurnOf(e, p);
            Assert.That(e.Act(p.CombatantId, new MoveAction(new GridPos(0, 1))).Ok, Is.True, "Moving within reach provokes nothing.");
            Assert.That(bandit.Turn.ReactionUsed, Is.False);
            var outcome = e.Act(p.CombatantId, new MoveAction(new GridPos(-1, 1)));
            Assert.That(outcome.Ok, Is.True);
            Assert.That(bandit.Turn.ReactionUsed, Is.True, "Leaving the bandit's reach provokes its opportunity attack.");
            Assert.That(outcome.Attacks.Count(a => a.Opportunity), Is.EqualTo(1));
            if (p.State == VitalState.Conscious)
            {
                p.Position = new GridPos(0, 0);
                var again = e.Act(p.CombatantId, new MoveAction(new GridPos(-1, 0)));
                Assert.That(again.Attacks.Count, Is.Zero, "One reaction per round.");
            }
        }

        [Test]
        public void Prone_HalvesMovementToStand_AndGrappledHasNoSpeed()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            var speed = e.Speed(p);
            e.ApplyCondition(p, ConditionType.Prone, default, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.StepCost(p, new GridPos(0, 1)), Is.EqualTo(10), "Crawling costs double.");
            Assert.That(e.Act(p.CombatantId, new StandAction()).Ok, Is.True);
            Assert.That(e.MovementLeft(p), Is.EqualTo(speed - speed / 2));
            Assert.That(e.Has(p, ConditionType.Prone), Is.False);

            e.ApplyCondition(p, ConditionType.Grappled, bandit.CombatantId, "test", ConditionDuration.UntilRemoved, default, 0, 1, 12);
            Assert.That(e.Speed(p), Is.Zero);
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(0, 1))), Does.Contain("speed 0"));
            bandit.Position = new GridPos(5, 5);
            TurnOf(e, bandit);
            e.Act(bandit.CombatantId, new MoveAction(new GridPos(5, 6)));
            Assert.That(e.Has(p, ConditionType.Grappled), Is.False, "A grapple ends when the grappler is no longer next to its target.");
        }

        // ===== Attacks =======================================================================================

        [Test]
        public void Attacks_FollowTheD20Rules_NaturalOneMisses_NaturalTwentyCrits()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            var option = e.Stats(p).Find("weapon");
            int hits = 0, misses = 0, crits = 0;
            for (var i = 0; i < 400; i++)
            {
                Revive(bandit);
                var r = e.ResolveAttack(p, bandit, option, false);
                Assert.That(r.Total, Is.EqualTo(r.Roll.Natural + r.AttackBonus.Total));
                Assert.That(r.TargetAc, Is.EqualTo(12), "Bandit AC 12 (leather armour).");
                if (r.Roll.Natural == 1) Assert.That(!r.Hit && r.MissReason == "a natural 1 always misses", Is.True, "A natural 1 misses, and says why.");
                else if (r.Roll.Natural == 20) Assert.That(r.Hit && r.Critical, Is.True);
                else Assert.That(r.Hit, Is.EqualTo(r.Total >= r.TargetAc));
                if (r.Critical) Assert.That(r.DamageDice.Count, Is.EqualTo(2 * option.Damage.Count), "A critical rolls the damage dice twice.");
                if (r.Hit)
                {
                    Assert.That(r.Damage, Is.Not.Null);
                    Assert.That(r.DamageDice.All(d => d >= 1 && d <= option.Damage.Sides), Is.True);
                    hits++;
                }
                else misses++;
                if (r.Critical) crits++;
            }

            Assert.That(hits, Is.GreaterThan(0));
            Assert.That(misses, Is.GreaterThan(0));
            Assert.That(crits, Is.GreaterThan(0));
        }

        [Test]
        public void AdvantageAndDisadvantage_RollTwoDice_AndCancelOut()
        {
            var w = Fighter("bow_hunting");
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            var bow = e.Stats(p).Find("weapon");
            Assert.That(bow.Melee, Is.False);

            // An enemy within 5 ft: disadvantage on a ranged attack.
            var r = e.ResolveAttack(p, bandit, bow, false);
            Assert.That(r.Roll.Disadvantage, Is.True);
            Assert.That(r.Roll.DisadvantageSources, Has.Some.Contains("within 5 ft"));
            Assert.That(r.Roll.Natural, Is.EqualTo(Math.Min(r.Roll.First, r.Roll.Second)));

            // A prone target within 5 ft gives advantage too: they cancel to a single die.
            Revive(bandit);
            e.ApplyCondition(bandit, ConditionType.Prone, default, "test", ConditionDuration.UntilRemoved, default, 0);
            r = e.ResolveAttack(p, bandit, bow, false);
            Assert.That(r.Roll.Advantage || r.Roll.Disadvantage, Is.False);
            Assert.That(r.Roll.ModeText, Does.Contain("cancel"));

            // Long range: disadvantage; restrained target: advantage (melee).
            Revive(bandit);
            bandit.Position = new GridPos(30, 0);
            r = e.ResolveAttack(p, bandit, bow, false);
            Assert.That(r.Roll.DisadvantageSources, Has.Some.Contains("long range"));
            Revive(bandit);
            bandit.Position = new GridPos(1, 0);
            e.ApplyCondition(bandit, ConditionType.Restrained, default, "test", ConditionDuration.UntilRemoved, default, 0);
            r = e.ResolveAttack(p, bandit, e.Stats(p).Find("unarmed"), false);
            Assert.That(r.Roll.Advantage, Is.True);
            Assert.That(r.Roll.Natural, Is.EqualTo(Math.Max(r.Roll.First, r.Roll.Second)));
        }

        [Test]
        public void AttackRange_AndReach_AreEnforced()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 2, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            TurnOf(e, p);
            Assert.That(e.Unavailable(p, new AttackAction(bandit.CombatantId, "weapon")), Does.Contain("10 ft away"));
            Assert.That(e.Unavailable(p, new AttackAction(p.CombatantId, "weapon")), Is.EqualTo("Choose a target."));
            Assert.That(e.Unavailable(p, new AttackAction(bandit.CombatantId, "nonsense")), Is.EqualTo("That attack is not available."));
            bandit.Position = new GridPos(1, 1);
            Assert.That(e.Unavailable(p, new AttackAction(bandit.CombatantId, "weapon")), Is.Null, "Diagonally adjacent is within 5 ft.");
        }

        [Test]
        public void GrappleAndShove_UseTheTargetsSave_AgainstEightPlusStrengthPlusProficiency()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            var stats = e.Stats(p);
            var grapple = stats.Find("grapple");
            Assert.That(grapple, Is.Not.Null, "A one-handed weapon leaves a hand free to grapple.");
            var dc = 8 + stats.Modifier(Ability.Strength) + stats.ProficiencyBonus;
            Assert.That(grapple.SaveDc, Is.EqualTo(dc));
            Assert.That(stats.Find("shove").SaveDc, Is.EqualTo(dc));
            bool grappled = false, resisted = false;
            for (var i = 0; i < 60 && !(grappled && resisted); i++)
            {
                Revive(bandit);
                var r = e.ResolveAttack(p, bandit, grapple, false);
                Assert.That(r.Save, Is.Not.Null);
                Assert.That(r.Save.Dc, Is.EqualTo(dc));
                Assert.That(r.Hit, Is.EqualTo(!r.Save.Success));
                Assert.That(e.Has(bandit, ConditionType.Grappled), Is.EqualTo(r.Hit));
                if (r.Hit) grappled = true; else resisted = true;
            }

            Assert.That(grappled && resisted, Is.True);
            Revive(bandit);
            var shove = e.ResolveAttack(p, bandit, stats.Find("shove"), false);
            Assert.That(e.Has(bandit, ConditionType.Prone), Is.EqualTo(shove.Hit));
            Equip(w.Player, EquipmentSlot.OffHand, "shield_round");
            Assert.That(new CombatEngine(w, e.Encounter).Stats(p).Find("grapple"), Is.Null, "Weapon and shield: no free hand (statistics are computed per command).");
        }

        // ===== Damage ========================================================================================

        [Test]
        public void Damage_RespectsResistance_AndTemporaryHitPointsFirst()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var bandit = Named(e, "Bandit");
            e.ApplyCondition(bandit, ConditionType.Petrified, default, "test", ConditionDuration.UntilRemoved, default, 0);
            var r = e.ApplyDamage(bandit, 9, DamageType.Slashing, false, "test");
            Assert.That(r.Final, Is.EqualTo(4), "Petrified: resistance to all damage, halved and rounded down.");
            Assert.That(r.Adjustments, Has.Some.Contains("resistance"));

            Revive(bandit);
            bandit.TemporaryHp = 5;
            r = e.ApplyDamage(bandit, 7, DamageType.Slashing, false, "test");
            Assert.That(r.TemporaryAbsorbed, Is.EqualTo(5));
            Assert.That(r.HitPointsLost, Is.EqualTo(2));
            Assert.That(bandit.CurrentHp, Is.EqualTo(bandit.MaxHp - 2));

            var sheet = w.Player.Rpg;
            sheet.TemporaryHp = 3;
            var hp = sheet.CurrentHp;
            r = e.ApplyDamage(ThePlayer(e), 4, DamageType.Piercing, false, "test");
            Assert.That(r.TemporaryAbsorbed, Is.EqualTo(3));
            Assert.That(sheet.CurrentHp, Is.EqualTo(hp - 1), "The player's hit points live on the character sheet.");
        }

        [Test]
        public void ACreature_AtZero_Dies_AndTheFightEnds()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var bandit = Named(e, "Bandit");
            var r = e.ApplyDamage(bandit, 50, DamageType.Slashing, false, "test");
            Assert.That(r.Killed, Is.True);
            Assert.That(bandit.State, Is.EqualTo(VitalState.Dead));
            e.CheckEnd();
            Assert.That(e.Encounter.IsActive, Is.False);
            Assert.That(e.Encounter.Outcome, Is.EqualTo(EncounterOutcome.Victory));
            Assert.That(e.Encounter.WinningSide, Is.EqualTo("player"));
            Assert.That(e.Chronicle.Count(n => n.Type == HistoricalEventType.EncounterEnded), Is.EqualTo(1));
            Assert.That(e.Act(ThePlayer(e).CombatantId, new EndTurnAction()).Ok, Is.False, "A finished fight takes no actions.");
        }

        [Test]
        public void ThePlayer_AtZero_FallsUnconscious_AndMakesDeathSaves()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("guard", "player", -1, 0, "Ally"), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var sheet = w.Player.Rpg;
            var r = e.ApplyDamage(p, sheet.CurrentHp, DamageType.Slashing, false, "Bandit");
            Assert.That(r.Downed, Is.True);
            Assert.That(p.State, Is.EqualTo(VitalState.Dying));
            Assert.That(e.Has(p, ConditionType.Unconscious) && e.Has(p, ConditionType.Prone), Is.True);
            Assert.That(e.Chronicle.Any(n => n.Type == HistoricalEventType.CombatantDowned), Is.True);

            r = e.ApplyDamage(p, 1, DamageType.Slashing, false, "Bandit");
            Assert.That(sheet.DeathSaveFailures, Is.EqualTo(1), "Damage at 0 is a failed death save.");
            r = e.ApplyDamage(p, 1, DamageType.Slashing, true, "Bandit");
            Assert.That(r.DeathSaveFailuresAdded, Is.EqualTo(2), "A critical is two.");
            Assert.That(r.Killed, Is.True, "Three failures: dead.");
            Assert.That(p.State, Is.EqualTo(VitalState.Dead));
            Assert.That(sheet.IsDead, Is.True);
            Assert.That(CharacterVitals.ChangeHitPoints(sheet, 5), Does.Contain("dead"), "There is no resurrection.");
            Assert.That(e.Heal(p, 5, "test"), Does.Contain("dead"));
            Assert.That(e.Chronicle.Any(n => n.Type == HistoricalEventType.CombatantKilled), Is.True);
            Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty);
        }

        [Test]
        public void DeathSaves_AtTheStartOfTheTurn_EndInStabilityDeathOrAWakening()
        {
            var outcomes = new HashSet<VitalState>();
            for (var attempt = 0; attempt < 16; attempt++)
            {
                _guids = attempt * 100;
                var w = attempt == 0 ? Fighter() : W;
                if (attempt > 0)
                {
                    Equip(w.Player, EquipmentSlot.MainHand, "sword_arming");
                }

                var e = Begin(w, Lethality.Lethal, Player(), Creature("guard", "player", -1, 0, "Ally"), Creature("bandit", "bandits", 6, 6));
                var p = ThePlayer(e);
                TurnOf(e, Named(e, "Ally"));
                e.ApplyDamage(p, w.Player.Rpg.CurrentHp, DamageType.Slashing, false, "test");
                Assert.That(p.State, Is.EqualTo(VitalState.Dying));
                for (var guard = 0; guard < 40 && p.State == VitalState.Dying && e.Encounter.IsActive; guard++)
                {
                    var active = e.Encounter.Active;
                    Assert.That(e.Act(active.CombatantId, new EndTurnAction()).Ok, Is.True);
                    var s = w.Player.Rpg;
                    Assert.That(s.DeathSaveSuccesses, Is.InRange(0, 3));
                    Assert.That(s.DeathSaveFailures, Is.InRange(0, 3));
                }

                Assert.That(p.State, Is.Not.EqualTo(VitalState.Dying));
                outcomes.Add(p.State);
                var sheet = w.Player.Rpg;
                if (p.State == VitalState.Stable) Assert.That(sheet.IsStable && sheet.CurrentHp == 0, Is.True);
                if (p.State == VitalState.Dead) Assert.That(sheet.IsDead, Is.True);
                if (p.State == VitalState.Conscious) Assert.That(sheet.CurrentHp, Is.EqualTo(1), "A natural 20 regains 1 hit point.");
                Assert.That(e.Encounter.Log.Any(l => l.Kind == CombatLogKind.DeathSave), Is.True);
                Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty);
            }

            Assert.That(outcomes.Count, Is.GreaterThanOrEqualTo(2), "Different seeds reach different ends.");
        }

        [Test]
        public void MassiveDamage_Kills_NonLethalFights_KnockOut_AndAFightsEndStabilizesTheDying()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p = ThePlayer(e);
            var max = e.MaxHp(p);
            var r = e.ApplyDamage(p, w.Player.Rpg.CurrentHp + max, DamageType.Bludgeoning, false, "test");
            Assert.That(r.Killed, Is.True, "Damage beyond 0 equal to the maximum kills outright.");

            _guids = 1000;
            var w2 = W;
            Equip(w2.Player, EquipmentSlot.MainHand, "sword_arming");
            var soft = Begin(w2, Lethality.NonLethal, Player(), Creature("bandit", "bandits", 1, 0));
            var p2 = ThePlayer(soft);
            r = soft.ApplyDamage(p2, w2.Player.Rpg.CurrentHp + 200, DamageType.Bludgeoning, false, "test");
            Assert.That(r.Killed, Is.False);
            Assert.That(p2.State, Is.EqualTo(VitalState.Stable), "Non-lethal: knocked out and stable.");
            var bandit = Named(soft, "Bandit");
            soft.ApplyDamage(bandit, 100, DamageType.Bludgeoning, false, "test");
            Assert.That(bandit.State, Is.EqualTo(VitalState.Stable), "Even creatures are only knocked out.");

            _guids = 2000;
            var w3 = W;
            Equip(w3.Player, EquipmentSlot.MainHand, "sword_arming");
            var third = Begin(w3, Lethality.Lethal, Player(), Creature("guard", "player", -1, 0, "Ally"), Creature("bandit", "bandits", 1, 0));
            var p3 = ThePlayer(third);
            third.ApplyDamage(p3, w3.Player.Rpg.CurrentHp, DamageType.Bludgeoning, false, "test");
            Assert.That(p3.State, Is.EqualTo(VitalState.Dying));
            third.Finish(EncounterOutcome.Ended, null);
            Assert.That(p3.State, Is.EqualTo(VitalState.Stable), "The dying player is stabilized when the fight ends.");
            Assert.That(w3.Player.Rpg.IsStable, Is.True);
            Assert.That(CharacterVitals.ChangeHitPoints(w3.Player.Rpg, 3), Is.Null);
            Assert.That(w3.Player.Rpg.IsStable, Is.False, "Healing ends the stable state.");
            Assert.That(CharacterValidation.CheckSheet(w3.Player.Rpg), Is.Empty);
        }

        [Test]
        public void Healing_WakesTheDying_AndStabilize_NeedsReach()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("guard", "player", 3, 3, "Ally"), Creature("bandit", "bandits", 6, 6));
            var p = ThePlayer(e);
            var ally = Named(e, "Ally");
            e.ApplyDamage(p, w.Player.Rpg.CurrentHp, DamageType.Slashing, false, "test");
            TurnOf(e, ally);
            Assert.That(e.Unavailable(ally, new StabilizeAction(p.CombatantId)), Does.Contain("out of reach"));
            ally.Position = new GridPos(1, 0);
            Assert.That(e.Unavailable(ally, new StabilizeAction(p.CombatantId)), Is.Null);
            Assert.That(e.Unavailable(ally, new StabilizeAction(ally.CombatantId)), Does.Contain("Only a dying"));
            Assert.That(e.Heal(p, 4, "a potion"), Is.Null);
            Assert.That(p.State, Is.EqualTo(VitalState.Conscious));
            Assert.That(e.Has(p, ConditionType.Unconscious), Is.False);
            Assert.That(w.Player.Rpg.CurrentHp, Is.EqualTo(4));
            Assert.That(w.Player.Rpg.DeathSaveFailures + w.Player.Rpg.DeathSaveSuccesses, Is.Zero);
        }

        // ===== Conditions ====================================================================================

        [Test]
        public void TheFifteenConditions_Exist_AndDoNotStack_ExceptExhaustion()
        {
            Assert.That(Enum.GetValues(typeof(ConditionType)).Length, Is.EqualTo(15));
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var bandit = Named(e, "Bandit");
            e.ApplyCondition(bandit, ConditionType.Poisoned, default, "a", ConditionDuration.UntilRemoved, default, 0);
            e.ApplyCondition(bandit, ConditionType.Poisoned, default, "b", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(bandit.Conditions.Count(k => k.Type == ConditionType.Poisoned), Is.EqualTo(1));

            e.ApplyCondition(bandit, ConditionType.Exhaustion, default, "a", ConditionDuration.UntilRemoved, default, 0, 2);
            e.ApplyCondition(bandit, ConditionType.Exhaustion, default, "b", ConditionDuration.UntilRemoved, default, 0, 1);
            Assert.That(e.ExhaustionLevel(bandit), Is.EqualTo(3));
            Assert.That(e.Speed(bandit), Is.EqualTo(30 - 15), "−5 ft per level.");
            e.ApplyCondition(bandit, ConditionType.Exhaustion, default, "c", ConditionDuration.UntilRemoved, default, 0, 3);
            Assert.That(bandit.State, Is.EqualTo(VitalState.Dead), "Exhaustion 6 is death.");
        }

        [Test]
        public void Conditions_HaveTheirMechanics()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0), Creature("wolf", "bandits", 5, 5));
            var p = ThePlayer(e);
            var bandit = Named(e, "Bandit");
            var wolf = Named(e, "Wolf");

            // Unconscious, stunned, paralyzed, petrified: incapacitated; Str and Dex saves fail automatically.
            foreach (var t in new[] { ConditionType.Stunned, ConditionType.Paralyzed, ConditionType.Petrified, ConditionType.Unconscious, ConditionType.Incapacitated })
            {
                Revive(bandit);
                e.ApplyCondition(bandit, t, default, "test", ConditionDuration.UntilRemoved, default, 0);
                Assert.That(e.Incapacitated(bandit), Is.True, t.ToString());
                TurnOf(e, bandit);
                Assert.That(e.Unavailable(bandit, new DashAction()), Does.Contain("incapacitated"), t.ToString());
                if (t != ConditionType.Incapacitated)
                    Assert.That(e.SavingThrow(bandit, Ability.Dexterity, 10, "test").AutomaticFailure, Is.Not.Null, t.ToString());
            }

            // Petrified: immune to the poisoned condition.
            Revive(bandit);
            e.ApplyCondition(bandit, ConditionType.Petrified, default, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.ApplyCondition(bandit, ConditionType.Poisoned, default, "test", ConditionDuration.UntilRemoved, default, 0), Does.Contain("immune"));

            // Frightened: cannot move closer to the source. Charmed: cannot attack the charmer.
            Revive(bandit);
            TurnOf(e, p);
            e.ApplyCondition(p, ConditionType.Frightened, wolf.CombatantId, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(1, 1))), Does.Contain("frightened"));
            Assert.That(e.Unavailable(p, new MoveAction(new GridPos(-1, -1))), Is.Null);
            e.ApplyCondition(p, ConditionType.Charmed, bandit.CombatantId, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.Unavailable(p, new AttackAction(bandit.CombatantId, "weapon")), Does.Contain("charmed"));

            // Restrained: speed 0, disadvantage on Dexterity saves.
            Revive(bandit);
            e.ApplyCondition(bandit, ConditionType.Restrained, default, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.Speed(bandit), Is.Zero);
            Assert.That(e.SavingThrow(bandit, Ability.Dexterity, 10, "test").Roll.Disadvantage, Is.True);

            // Blinded: no opportunity attacks; invisible attackers have advantage.
            Revive(bandit);
            e.ApplyCondition(bandit, ConditionType.Blinded, default, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.CanReact(bandit), Is.False);
            Revive(bandit);
            p.Conditions.Clear();
            e.ApplyCondition(p, ConditionType.Invisible, default, "test", ConditionDuration.UntilRemoved, default, 0);
            Assert.That(e.ResolveAttack(p, bandit, e.Stats(p).Find("weapon"), false).Roll.AdvantageSources, Has.Some.Contains("invisible"));
        }

        [Test]
        public void TimedConditions_EndOnTheirTurn()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 6, 6));
            var bandit = Named(e, "Bandit");
            e.ApplyCondition(bandit, ConditionType.Poisoned, default, "test", ConditionDuration.StartOfTurn, bandit.CombatantId, e.Encounter.Round + 1);
            for (var guard = 0; guard < 10 && e.Has(bandit, ConditionType.Poisoned); guard++) e.Act(e.Encounter.Active.CombatantId, new EndTurnAction());
            Assert.That(e.Has(bandit, ConditionType.Poisoned), Is.False);
            Assert.That(e.Encounter.Log.Any(l => l.Kind == CombatLogKind.ConditionRemoved && l.Text.Contains("time ran out")), Is.True);
        }

        // ===== AI ============================================================================================

        [Test]
        public void Ai_ProposesValidActions_Deterministically_AndFinishesAFight()
        {
            string Run()
            {
                _guids = 0;
                var w = Fighter();
                var e = Begin(w, Lethality.Lethal, Creature("guard", "town", 0, 0, "Guard A"), Creature("guard", "town", 1, 0, "Guard B"),
                    Creature("bandit", "bandits", 4, 6), Creature("bandit", "bandits", -4, 7, "Bandit 2"), Creature("wolf", "bandits", 6, -6));
                for (var step = 0; step < 2000 && e.Encounter.IsActive; step++)
                {
                    var actor = e.Encounter.Active;
                    var proposal = CombatAi.Next(e, actor);
                    Assert.That(e.Unavailable(actor, proposal), Is.Null, $"The AI proposed something the rules refuse: {proposal.Describe}");
                    var again = CombatAi.Next(e, actor);
                    Assert.That(again.Describe, Is.EqualTo(proposal.Describe), "Same state, same proposal.");
                    if (proposal is AttackAction a)
                        Assert.That(CombatEngine.Hostile(actor, e.Encounter.Find(a.Target)), Is.True, "Never attacks an ally.");
                    Assert.That(e.Act(actor.CombatantId, proposal).Ok, Is.True);
                }

                Assert.That(e.Encounter.IsActive, Is.False, "The fight ends.");
                Assert.That(e.Encounter.Round, Is.LessThan(60));
                return string.Join("\n", e.Encounter.Log.Select(l => l.Text));
            }

            var first = Run();
            TearDown();
            SetUp();
            Assert.That(Run(), Is.EqualTo(first), "The same fight plays out the same way.");
        }

        [Test]
        public void Ai_HurtCombatants_GetAway()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var bandit = Named(e, "Bandit");
            bandit.CurrentHp = 1;
            TurnOf(e, bandit);
            Assert.That(CombatAi.Next(e, bandit), Is.InstanceOf<DisengageAction>());
            e.Act(bandit.CombatantId, new DisengageAction());
            Assert.That(CombatAi.Next(e, bandit), Is.InstanceOf<MoveAction>());
        }

        // ===== Encounter state ===============================================================================

        [Test]
        public void TheDiceStream_IsReproducible_FromTheSavedCounter()
        {
            var w = Fighter();
            var e = Begin(w, Lethality.Lethal, Player(), Creature("bandit", "bandits", 1, 0));
            var copy = w.DeepClone();
            var e2 = new CombatEngine(copy, copy.Encounters[0]);
            for (var i = 0; i < 50; i++) Assert.That(e2.Die(20), Is.EqualTo(e.Die(20)));
            Assert.That(e.Encounter.RollCount, Is.EqualTo(e2.Encounter.RollCount));
        }
    }
}
