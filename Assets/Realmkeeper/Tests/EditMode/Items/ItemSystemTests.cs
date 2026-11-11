using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Items;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Living;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Items
{
    /// <summary>
    /// Phase 11 (ADR-0050–0055): the item model, holders and atomic transfers, the weight limit, equipment and magic
    /// through the rules engine, condition and repair, attunement and curses, crafting, gathering, loot, merchants,
    /// and saving it all.
    /// </summary>
    public sealed class ItemSystemTests
    {
        private string _dir;
        private WorldSession _s;
        private int _ids;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
            _ids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private void Ready(string cls = "fighter")
        {
            CombatTests.Create(_s, cls, CombatTests.Scores);
            CombatTests.EnterTown(_s);
        }

        /// <summary>Changes the world the way a save edited on disk would, and loads it (the change goes through the validator).</summary>
        private void Edit(Action<WorldData> change)
        {
            var w = W;
            change(w);
            var saved = new JsonWorldStore(_dir).Save(w, "edit");
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            var load = _s.Execute(new LoadWorldCommand("edit"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
        }

        private ItemStackData NewStack(string itemId, int quantity = 1)
        {
            _ids++;
            var b = new byte[16];
            BitConverter.GetBytes(_ids).CopyTo(b, 0);
            b[15] = 0x77;
            return ItemStacks.Create(new ItemStackId(new Guid(b)), ItemCatalog.Find(itemId), quantity, "test");
        }

        private ItemStackData Give(string itemId, int quantity = 1, Action<ItemStackData> state = null)
        {
            var def = ItemCatalog.Find(itemId);
            var stacks = new List<ItemStackData>();
            if (def.Stackable) stacks.Add(NewStack(itemId, quantity));
            else for (var i = 0; i < quantity; i++) stacks.Add(NewStack(itemId));
            foreach (var s in stacks) state?.Invoke(s);
            Edit(w => w.Player.Inventory.AddRange(stacks));
            return stacks[0];
        }

        /// <summary>Sets the clock to a time this shop is open with its keeper at the counter (or fails the assumption).</summary>
        private void OpenShop(ShopId id)
        {
            var w = W;
            var shop = w.FindShop(id);
            for (var m = shop.OpensAt; m < shop.ClosesAt; m += 15)
            {
                w.MinuteOfDay = m;
                if (!Realmkeeper.Simulation.Commerce.CommerceMath.Availability(w, shop).IsOpen) continue;
                var minute = m;
                Edit(x => x.MinuteOfDay = minute);
                return;
            }

            Assume.That(false, "The shop is never open with its keeper there.");
        }

        private ItemStackId Pack(string itemId) => W.Player.Inventory.First(x => x.ItemId == itemId).StackId;

        private void Ok(CommandResult r) => Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));

        private void Refused(CommandResult r, string text)
        {
            Assert.That(r.IsSuccess, Is.False, "Expected a refusal: " + text);
            Assert.That(r.Message, Does.Contain(text));
        }

        private void Valid()
        {
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        private SettlementData Town => W.Settlements.First(x => x.SettlementId == W.Player.SettlementId);

        // ----- Catalog and data ---------------------------------------------------------------------

        [Test]
        public void TheCatalog_TablesAndRecipes_AreConsistent()
        {
            Assert.That(ItemCatalog.Validate().ToList(), Is.Empty);
            Assert.That(LootTables.Validate(), Is.Empty);
            Assert.That(RecipeCatalog.Validate(), Is.Empty);
            var all = ItemCatalog.Items.ToList();
            Assert.That(all.Count(d => d.Weapon != null), Is.GreaterThanOrEqualTo(30));
            Assert.That(all.Count(d => d.Armor != null), Is.GreaterThanOrEqualTo(12));
            Assert.That(all.Count(d => d.IsMagic), Is.GreaterThanOrEqualTo(10), string.Join(", ", all.Where(d => d.IsMagic).Select(d => d.Id)));
            Assert.That(all.Count(d => d.MaterialFor.Count > 0), Is.GreaterThanOrEqualTo(10), "Crafting materials (including the iron bar and timber plank of the settlement economy).");
            Assert.That(all.Select(d => d.Id).Distinct().Count(), Is.EqualTo(all.Count));
            TestContext.Out.WriteLine($"Catalog v{ItemCatalog.Version}: {all.Count} items; " + string.Join(", ", all.GroupBy(d => d.Category).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
            TestContext.Out.WriteLine("Rarity: " + string.Join(", ", all.GroupBy(d => d.Rarity).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")) + $"; magic {all.Count(d => d.IsMagic)}; SRD-sourced {all.Count(d => d.Source == ContentSource.Srd521)}");
            TestContext.Out.WriteLine($"Recipes {RecipeCatalog.Recipes.Count}; loot tables {LootTables.All.Count()}; carried-gear lines for {LootTables.Carried.Count} creatures");
            foreach (var d in all.Where(d => d.Magic?.MaxCharges > 0))
                Assert.That(NewStack(d.Id).Charges, Is.EqualTo(d.Magic.MaxCharges), "A new wand starts full.");
        }

        [Test]
        public void Genesis_GivesEachSettlementAChestACacheAndSites_Deterministically_AndOnce()
        {
            var w = W;
            foreach (var s in w.Settlements)
            {
                var chest = w.FindContainer(ItemGenesis.ChestKey(s.SettlementId));
                Assert.That(chest, Is.Not.Null);
                Assert.That(chest.AcceptsDeposits && chest.PlayerAccess, Is.True);
                Assert.That(w.FindContainer(ItemGenesis.CacheKey(s.SettlementId)), Is.Not.Null);
                Assert.That(w.GatheringSites.Count(x => x.SettlementId == s.SettlementId), Is.GreaterThanOrEqualTo(1));
            }

            Assert.That(w.Containers.Where(c => c.Kind == ContainerKind.Cache).Sum(c => c.Items.Count + c.Coin), Is.GreaterThan(0), "Caches hold something.");
            var other = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
            var w2 = other.CurrentWorldSnapshotForTests();
            var ser = new JsonWorldSerializer();
            Assert.That(JArray.FromObject(w2.Containers.Select(c => c.Key + c.Coin + string.Join(",", c.Items.Select(i => i.ItemId + i.Quantity + i.StackId)))).ToString(),
                Is.EqualTo(JArray.FromObject(w.Containers.Select(c => c.Key + c.Coin + string.Join(",", c.Items.Select(i => i.ItemId + i.Quantity + i.StackId)))).ToString()), "Same seed, same caches.");
            var copy = W;
            Assert.That(ItemGenesis.Ensure(copy), Is.False, "Nothing is added twice.");
            Valid();
        }

        // ----- Holders and transfers ----------------------------------------------------------------

        [Test]
        public void Chest_TakesAndGivesBack_KeepingEachItemsIdentity_AndOneHolder()
        {
            Ready();
            Give("potion_healing", 3);
            var chest = ItemGenesis.ChestKey(Town.SettlementId);
            var potions = Pack("potion_healing");
            Ok(_s.Dispatch(new PutInContainerCommand(chest, potions, 2)));
            Assert.That(W.Player.Inventory.Single(x => x.ItemId == "potion_healing").Quantity, Is.EqualTo(1));
            var inChest = W.FindContainer(chest).Items.Single(x => x.ItemId == "potion_healing");
            Assert.That(inChest.Quantity, Is.EqualTo(2));
            Assert.That(inChest.StackId, Is.Not.EqualTo(potions), "The part moved is a new instance; the part left keeps its ID.");
            Valid();

            Ok(_s.Dispatch(new TakeFromContainerCommand(chest, inChest.StackId, 2)));
            Assert.That(W.Player.Inventory.Where(x => x.ItemId == "potion_healing").Sum(x => x.Quantity), Is.EqualTo(3), "Nothing lost, nothing duplicated.");
            Assert.That(W.FindContainer(chest).Items, Is.Empty);
            Valid();

            Ok(_s.Dispatch(new MoveContainerCoinCommand(chest, -10)));
            Assert.That(W.FindContainer(chest).Coin, Is.EqualTo(10));
            Refused(_s.Dispatch(new MoveContainerCoinCommand(chest, 11)), "holds 10 coin");
            Ok(_s.Dispatch(new MoveContainerCoinCommand(chest, 10)));
        }

        [Test]
        public void Containers_RefuseWhatTheyCannotDo_AndAFailedTransferChangesNothing()
        {
            Ready();
            Give("potion_healing");
            var town = Town.SettlementId;
            var cache = ItemGenesis.CacheKey(town);
            Refused(_s.Dispatch(new PutInContainerCommand(cache, Pack("potion_healing"), 1)), "cannot leave things");
            var elsewhere = W.Settlements.First(s => s.SettlementId != town);
            Refused(_s.Dispatch(new TakeAllFromContainerCommand(ItemGenesis.ChestKey(elsewhere.SettlementId))), elsewhere.Name);

            // Too heavy to take everything: nothing moves.
            Edit(w =>
            {
                var c = w.FindContainer(cache);
                for (var i = 0; i < 12; i++) c.Items.Add(NewStack("plate_armour_like" == "x" ? "x" : "iron_ore", 50));
            });
            var before = W;
            Refused(_s.Dispatch(new TakeAllFromContainerCommand(cache)), "Too heavy");
            Assert.That(W.FindContainer(cache).Items.Count, Is.EqualTo(before.FindContainer(cache).Items.Count));
            Assert.That(W.Player.Inventory.Count, Is.EqualTo(before.Player.Inventory.Count));
            Assert.That(W.Player.Purse, Is.EqualTo(before.Player.Purse));
        }

        [Test]
        public void ThePack_IsLimitedByWeight_AndOverCapacityTheSpeedIs5Feet()
        {
            Ready();
            var p = W.Player;
            var limit = ItemCommandHandlers.PackLimitGrams(p);
            var capacity = PlayerMovement.UnencumberedFor(p);
            Assert.That(limit, Is.EqualTo(2 * capacity));
            Assert.That(capacity, Is.EqualTo(Carrying.CapacityGrams(new CharacterRules(p.Rpg).Score(Ability.Strength), 100)));

            // Load to just over capacity: slowed to 5 ft, and the rules say why.
            var ore = ItemCatalog.Find("iron_ore").WeightGrams;
            var need = (capacity - ItemWeights.CarriedGrams(p)) / ore + 1;
            Give("iron_ore", Math.Min(50, need));
            if (need > 50) Give("iron_ore", need - 50);
            var rules = new CharacterRules(W.Player.Rpg, CharacterRules.LoadoutOf(W.Player));
            Assert.That(rules.Encumbered, Is.True);
            Assert.That(rules.Speed, Is.EqualTo(5));
            Assert.That(_s.GetPlayerHub().SpeedReason, Does.Contain("over carrying capacity"));

            // Past twice capacity nothing more can be taken.
            var chest = ItemGenesis.ChestKey(Town.SettlementId);
            Edit(w => { for (var i = 0; i < 10; i++) w.FindContainer(chest).Items.Add(NewStack("iron_ore", 50)); });
            var heavy = W.FindContainer(chest).Items.First().StackId;
            var r = _s.Dispatch(new TakeFromContainerCommand(chest, heavy, 50));
            Refused(r, "Too heavy");
        }

        // ----- Equipment and rules ------------------------------------------------------------------

        [Test]
        public void Rings_GoOnEitherHand_AndALightWeapon_InTheOffHand_GivesABonusAttack()
        {
            Ready();
            Give("gold_ring", 2);
            Ok(_s.Dispatch(new EquipItemCommand(Pack("gold_ring"))));
            Ok(_s.Dispatch(new EquipItemCommand(Pack("gold_ring"))));
            Assert.That(W.Player.Equipped(EquipmentSlot.RingLeft), Is.Not.Null);
            Assert.That(W.Player.Equipped(EquipmentSlot.RingRight), Is.Not.Null);

            Give("dagger_iron", 2);
            Ok(_s.Dispatch(new EquipItemCommand(Pack("dagger_iron"))));
            Ok(_s.Dispatch(new EquipItemCommand(Pack("dagger_iron"), EquipmentSlot.OffHand)));
            var stats = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" });
            Assert.That(stats.Attacks.Any(a => a.OffHand), Is.True, "Two-weapon fighting: an off-hand attack.");
            Give("battleaxe");
            Refused(_s.Dispatch(new EquipItemCommand(Pack("battleaxe"), EquipmentSlot.OffHand)), "Light");
            Valid();
        }

        [Test]
        public void MagicWeaponsAndArmour_AddToTheRules_AndConditionChangesThem()
        {
            Ready();
            var before = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" });
            Give("longsword_plus1");
            var cmp = _s.CompareItem(Pack("longsword_plus1"));
            Assert.That(cmp.Lines.Any(l => l.Label == "Main attack"), Is.True);
            Ok(_s.Dispatch(new EquipItemCommand(Pack("longsword_plus1"))));
            var after = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" });
            var sword = after.Attacks.First(a => a.ItemId == "longsword_plus1");
            Assert.That(sword.AttackBonus.Parts.Any(x => x.label.Contains("+1") || x.label.ToLowerInvariant().Contains("magic")), Is.True, "The +1 is a named part of the attack bonus.");

            Give("shield_plus1");
            var hadShield = W.Player.Equipped(EquipmentSlot.OffHand) != null;
            var ac0 = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total;
            Ok(_s.Dispatch(new EquipItemCommand(Pack("shield_plus1"))));
            var ac1 = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total;
            Assert.That(ac1, Is.EqualTo(ac0 + (hadShield ? 1 : 3)), "Shield +2, magic +1 (a plain shield worn before gave the +2).");

            Edit(w => w.Player.Equipped(EquipmentSlot.OffHand).Condition = ItemCondition.Damaged);
            Assert.That(CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total, Is.EqualTo(ac1 - 1), "Damaged: −1.");
            Edit(w => w.Player.Equipped(EquipmentSlot.OffHand).Condition = ItemCondition.Broken);
            Assert.That(CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total, Is.EqualTo(ac1 - 3), "Broken: no AC from it.");
        }

        [Test]
        public void Attunement_TakesAShortRest_IsLimitedToThree_AndCursesBind()
        {
            Ready();
            Give("cloak_of_protection");
            var cloak = Pack("cloak_of_protection");
            Ok(_s.Dispatch(new EquipItemCommand(cloak)));
            var ac0 = CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total;
            Ok(_s.Dispatch(StartWorkCommand.Attune(cloak)));
            Refused(_s.Dispatch(new CollectWorkCommand(_s.GetWork().Key)), "Not finished");
            Ok(_s.FinishWork(_s.GetWork().Key));
            Assert.That(W.Player.Equipped(EquipmentSlot.Back) ?? W.Player.Equipment.Values.First(x => x.StackId == cloak), Is.Not.Null);
            Assert.That(W.Player.Equipment.Values.First(x => x.StackId == cloak).Attuned, Is.True);
            Assert.That(CombatStats.For(W, new CombatantData { Kind = CombatantKind.Player, Name = "p" }).ArmorClass.Total, Is.EqualTo(ac0 + 1), "Attuned: +1 AC.");

            // Three at most.
            Edit(w =>
            {
                w.Player.Inventory.Add(NewStack("ring_of_protection"));
                var a = NewStack("gauntlets_of_ogre_power");
                a.Attuned = true;
                var b = NewStack("cloak_of_protection");
                b.Attuned = true;
                w.Player.Inventory.Add(a);
                w.Player.Inventory.Add(b);
            });
            Refused(_s.Dispatch(StartWorkCommand.Attune(Pack("ring_of_protection"))), "attuned to 3");
            Ok(_s.Dispatch(new EndAttunementCommand(Pack("gauntlets_of_ogre_power"))));
            Ok(_s.Dispatch(StartWorkCommand.Attune(Pack("ring_of_protection"))));
            Ok(_s.Dispatch(new CancelWorkCommand(_s.GetWork().Key)));

            // A cursed axe binds itself when attuned; only a shrine lifts it.
            Ok(_s.Dispatch(new EndAttunementCommand(W.Player.Inventory.First(x => x.ItemId == "cloak_of_protection" && x.Attuned).StackId)));
            Give("berserker_axe");
            Ok(_s.Dispatch(new EquipItemCommand(Pack("berserker_axe"))));
            var axe = W.Player.Equipped(EquipmentSlot.MainHand).StackId;
            Ok(_s.Dispatch(StartWorkCommand.Attune(axe)));
            Ok(_s.FinishWork(_s.GetWork().Key));
            Assert.That(W.Player.Equipped(EquipmentSlot.MainHand).Bound, Is.True);
            Refused(_s.Dispatch(new UnequipItemCommand(EquipmentSlot.MainHand)), "curse");
            Refused(_s.Dispatch(new EndAttunementCommand(axe)), "curse");
            Give("dagger_iron");
            Refused(_s.Dispatch(new EquipItemCommand(Pack("dagger_iron"))), "curse");
            Assert.That(_s.InspectItem(axe).Warnings.Any(x => x.StartsWith("Cursed")), Is.True);
            if (ItemCommandHandlers.Shrine(W, W.Player) != null)
            {
                Edit(w => w.Player.Purse = 500);
                Ok(_s.Dispatch(new LiftCurseCommand(axe, ItemCommandHandlers.LiftCursePrice)));
                Assert.That(W.Player.Equipped(EquipmentSlot.MainHand).Bound, Is.False);
                Ok(_s.Dispatch(new UnequipItemCommand(EquipmentSlot.MainHand)));
            }

            Valid();
        }

        [Test]
        public void Potions_HealOutsideFights_AndWands_RechargeOnALongRest()
        {
            Ready();
            Give("potion_healing", 2);
            Refused(_s.Dispatch(new UseItemCommand(Pack("potion_healing"))), "unhurt");
            Edit(w => w.Player.Rpg.CurrentHp = 1);
            var r = _s.Dispatch(new UseItemCommand(Pack("potion_healing")));
            Ok(r);
            Assert.That(W.Player.Rpg.CurrentHp, Is.InRange(1 + 4, 1 + 10), "2d4+2.");
            Assert.That(W.Player.Inventory.Single(x => x.ItemId == "potion_healing").Quantity, Is.EqualTo(1));

            Give("wand_of_magic_missiles", 1, s => s.Charges = 0);
            Edit(w => w.Player.Rpg.CurrentHp = new CharacterRules(w.Player.Rpg).MaxHp);
            var rest = _s.LongRest();
            Assert.That(rest.Message ?? string.Empty, Does.Not.Contain("interrupted"));
            var wand = W.Player.Inventory.Single(x => x.ItemId == "wand_of_magic_missiles");
            Assert.That(wand.Charges, Is.InRange(2, 7), "1d6+1 back.");
        }

        // ----- Repair, crafting, gathering ----------------------------------------------------------

        [Test]
        public void ASmith_MendsForAPrice_AndThePlayer_CanMendWithTools()
        {
            Ready();
            Give("shortsword", 1, s => s.Condition = ItemCondition.Damaged);
            var smith = W.Shops.FirstOrDefault(s => s.SettlementId == W.Player.SettlementId && ItemCommandHandlers.Repairs(s));
            Assume.That(smith, Is.Not.Null, "This town has no armourer.");
            OpenShop(smith.ShopId);
            var quotes = _s.GetRepairQuotes(smith.ShopId);
            var q = quotes.Single(x => x.Row.ItemId == "shortsword");
            Assert.That(q.Price, Is.EqualTo(ItemStacks.RepairPrice(ItemCatalog.Find("shortsword"), W.Player.Inventory.First(x => x.ItemId == "shortsword"))));
            Refused(_s.Dispatch(new RepairAtShopCommand(smith.ShopId, Pack("shortsword"), q.Price + 1)), "price has changed");
            var purse = W.Player.Purse;
            Ok(_s.Dispatch(new RepairAtShopCommand(smith.ShopId, Pack("shortsword"), q.Price)));
            Assert.That(W.Player.Inventory.First(x => x.ItemId == "shortsword").Condition, Is.EqualTo(ItemCondition.Good));
            Assert.That(W.Player.Purse, Is.EqualTo(purse - q.Price));

            // Self-repair: tools, time, a check; the item is held by the work meanwhile.
            Give("mace_flanged", 1, s => s.Condition = ItemCondition.Worn);
            Refused(_s.Dispatch(StartWorkCommand.Repair(Pack("mace_flanged"))), "blacksmithing tools");
            Give("smiths_tools");
            Ok(_s.Dispatch(StartWorkCommand.Repair(Pack("mace_flanged"))));
            Assert.That(W.Player.Inventory.Any(x => x.ItemId == "mace_flanged"), Is.False, "The work holds it.");
            Valid();
            var job = W.WorkJobs.Single(j => !j.Done);
            Ok(_s.FinishWork(job.Key));
            var mace = W.Player.Inventory.Single(x => x.ItemId == "mace_flanged");
            Assert.That(mace.Condition, Is.EqualTo(job.Succeeded ? ItemCondition.Good : ItemCondition.Worn));
            Valid();
        }

        [Test]
        public void Crafting_HoldsMaterials_IsDeterministic_AndCancelling_ReturnsEverything()
        {
            Ready();
            Give("leatherworkers_tools");
            Give("cured_leather", 6);
            var recipe = RecipeCatalog.Find("stitch_jerkin");
            Assert.That(_s.GetWorkshop().Recipes.Single(r => r.Id == recipe.Id).CanStart, Is.True);

            Ok(_s.Dispatch(StartWorkCommand.Craft(recipe.Id)));
            Assert.That(ItemCommandHandlers.CountInPack(W.Player, "cured_leather"), Is.EqualTo(3), "Three held by the work.");
            Refused(_s.Dispatch(StartWorkCommand.Craft(recipe.Id)), "already busy");
            Ok(_s.Dispatch(new CancelWorkCommand(_s.GetWork().Key)));
            Assert.That(ItemCommandHandlers.CountInPack(W.Player, "cured_leather"), Is.EqualTo(6), "Cancelled: all back.");
            Valid();

            var key = W;
            Ok(_s.Dispatch(StartWorkCommand.Craft(recipe.Id)));
            var job = W.WorkJobs.Single(j => !j.Done);
            Assert.That(job.Roll, Is.InRange(1, 20));
            Assert.That(job.Succeeded, Is.EqualTo(job.Total >= job.Dc));
            Ok(_s.FinishWork(job.Key));
            if (job.Succeeded)
            {
                var made = W.Player.Inventory.Single(x => x.ItemId == "jerkin_leather");
                Assert.That(made.Quality, Is.EqualTo(job.Total >= job.Dc + 5 ? ItemQuality.Fine : ItemQuality.Common));
                Assert.That(made.Provenance, Is.EqualTo("Made by you"));
                Assert.That(ItemCommandHandlers.CountInPack(W.Player, "cured_leather"), Is.EqualTo(3));
            }
            else Assert.That(ItemCommandHandlers.CountInPack(W.Player, "cured_leather"), Is.EqualTo(3 + 3 * (100 - recipe.FailureLossPercent) / 100));

            Valid();
        }

        [Test]
        public void Crafting_IsRefused_WithoutStationToolOrMaterials()
        {
            Ready();
            var r = RecipeCatalog.Find("brew_healing");
            Refused(_s.Dispatch(StartWorkCommand.Craft(r.Id)), "need");
            Give("alchemists_supplies");
            Give("silverleaf", 2);
            Give("glass_vial");
            Refused(_s.Dispatch(StartWorkCommand.Craft(r.Id)), "silverleaf");
        }

        [Test]
        public void Gathering_IsFinite_AndRecoversWeekly()
        {
            Ready();
            var site = W.GatheringSites.First(s => s.SettlementId == W.Player.SettlementId && s.Kind == "foraging");
            if (ItemCommandHandlers.Tool(W.Player, null, "foraging") == null)
            {
                Refused(_s.Dispatch(StartWorkCommand.Gather(site.Key)), "foraging tool");
                Give("herbalism_kit");
            }

            var before = site.Available;
            Ok(_s.Dispatch(StartWorkCommand.Gather(site.Key)));
            Assert.That(W.GatheringSites.Single(s => s.Key == site.Key).Available, Is.EqualTo(before - site.Yield));
            Ok(_s.FinishWork(_s.GetWork().Key));
            Assert.That(ItemCommandHandlers.CountInPack(W.Player, site.ItemId), Is.EqualTo(site.Yield));

            Edit(w => w.GatheringSites.Single(s => s.Key == site.Key).Available = 0);
            Refused(_s.Dispatch(StartWorkCommand.Gather(site.Key)), "worked out");
            var copy = W;
            var day = copy.CurrentSimulationDate.ToDayNumber();
            var toWeek = (int)((7 - day % 7) % 7);
            _s.Execute(new AdvanceDaysCommand(toWeek == 0 ? 7 : toWeek));
            Assert.That(W.GatheringSites.Single(s => s.Key == site.Key).Available, Is.EqualTo(Math.Min(site.Capacity, site.PerWeek)));
        }

        // ----- Loot ---------------------------------------------------------------------------------

        [Test]
        public void Loot_IsLeftInAContainer_FilledOnce_FromWhatTheFoesCarried()
        {
            CombatTests.Create(_s, "fighter", CombatTests.Scores);
            CombatTests.EnterTown(_s);
            Ok(_s.Dispatch(new StartWorldEncounterCommand(EncounterPurpose.Road)));
            var packBefore = W.Player.Inventory.Sum(x => x.Quantity);
            var purseBefore = W.Player.Purse;
            EncounterGenerationTests.FightToTheEnd(_s);
            var w = W;
            var ended = w.Encounters.Last(x => x.Origin != null);
            var pile = w.FindContainer(WorldEncounters.LootKey(ended.EncounterId));
            var won = ended.WinningSide == "player" && ended.Combatants.Any(c => c.CreatureId != null && c.State == VitalState.Dead);
            Assume.That(won, Is.True, "The fighter lost or no foe fell this time.");
            Assert.That(w.Player.Inventory.Where(x => x.ItemId != "arrows" && x.ItemId != "crossbow_bolts" && x.ItemId != "sling_bullets").Sum(x => x.Quantity),
                Is.LessThanOrEqualTo(packBefore), "Nothing is dropped straight into the pack.");
            Assert.That(w.Player.Purse, Is.EqualTo(purseBefore));
            Assert.That(pile, Is.Not.Null, "The fallen leave their gear.");
            Assert.That(pile.Kind, Is.EqualTo(ContainerKind.Loot));
            Assert.That(pile.Items.All(i => !ItemCatalog.Find(i.ItemId).Wears || i.Condition >= ItemCondition.Good), Is.True);

            // Filling again changes nothing.
            var copy = W;
            var endedCopy = copy.Encounters.Last(x => x.Origin != null);
            var dead = endedCopy.Combatants.Where(c => c.State == VitalState.Dead).ToList();
            var again = WorldEncounters.FillLoot(new CommandContext(_s, copy, new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory()), copy, endedCopy, dead);
            Assert.That(again.Items.Select(i => i.StackId), Is.EqualTo(pile.Items.Select(i => i.StackId)));
            Assert.That(copy.Containers.Count, Is.EqualTo(w.Containers.Count));

            var view = _s.GetEncounterLoot(ended.EncounterId);
            Assert.That(view.Reachable, Is.True);
            var items = pile.Items.Sum(i => i.Quantity);
            var r = _s.Dispatch(new TakeAllFromContainerCommand(pile.Key));
            if (r.IsSuccess)
            {
                Assert.That(W.FindContainer(pile.Key).IsEmpty, Is.True);
                Assert.That(W.Player.Purse, Is.EqualTo(purseBefore + pile.Coin));
            }

            Valid();
        }

        [Test]
        public void LootTables_AreDeterministic_AndRespectRarity()
        {
            var ctx = new LootContext { MaxRarity = ItemRarity.Common };
            for (ulong seed = 1; seed < 200; seed++)
            {
                var a = LootTables.Roll("old_cache", ctx, seed);
                var b = LootTables.Roll("old_cache", ctx, seed);
                Assert.That(string.Join(",", a.Items.Select(i => i.ItemId + i.Quantity)) + a.Coin, Is.EqualTo(string.Join(",", b.Items.Select(i => i.ItemId + i.Quantity)) + b.Coin));
                Assert.That(a.Items.All(i => ItemCatalog.Find(i.ItemId).Rarity <= ItemRarity.Common), Is.True);
            }
        }

        // ----- Merchants ----------------------------------------------------------------------------

        [Test]
        public void ADamagedSword_SellsForLess_AndBecomesThatShopsParticularGoods()
        {
            Ready();
            var shop = W.Shops.FirstOrDefault(s => s.SettlementId == W.Player.SettlementId && s.Categories.Contains(ShopCategory.WeaponsAndArmour));
            Assume.That(shop, Is.Not.Null);
            Edit(w =>
            {
                var sh = w.FindShop(shop.ShopId);
                w.FindNpc(sh.ShopkeeperNpcId).Condition.Wealth = 5000;
                sh.Stock.RemoveAll(x => x.ItemId == "shortsword");
            });
            OpenShop(shop.ShopId);
            Give("shortsword", 1, s => s.Condition = ItemCondition.Damaged);
            var offer = _s.GetSaleOffer(shop.ShopId, Pack("shortsword"));
            Assume.That(offer.Accepted, Is.True, offer.Refusal);
            var plain = Realmkeeper.Simulation.Commerce.CommerceMath.BuyingPrice(W, W.FindShop(shop.ShopId), ItemCatalog.Find("shortsword")).UnitPrice;
            Assert.That(offer.UnitPrice, Is.EqualTo(Math.Max(1, plain * 50 / 100)), "Damaged: half.");
            var id = Pack("shortsword");
            Ok(_s.Dispatch(new SellItemCommand(shop.ShopId, id, 1, offer.UnitPrice)));
            var goods = W.FindShop(shop.ShopId).Goods.Single(g => g.ItemId == "shortsword");
            Assert.That(goods.Condition, Is.EqualTo(ItemCondition.Damaged), "It keeps its state in the shop.");
            Assert.That(W.FindShop(shop.ShopId).QuantityOf("shortsword"), Is.EqualTo(0), "Not counted as fresh stock.");
            var price = _s.GetShopGoods(shop.ShopId).Single(x => x.Row.StackId == goods.StackId).Price;
            Edit(w => w.Player.Purse = 1000);
            Ok(_s.Dispatch(new BuyGoodsCommand(shop.ShopId, goods.StackId, price)));
            Assert.That(W.Player.Inventory.Single(x => x.ItemId == "shortsword").Condition, Is.EqualTo(ItemCondition.Damaged));
            Valid();
        }

        // ----- Queries ------------------------------------------------------------------------------

        [Test]
        public void TheInventory_Filters_Searches_AndSorts_Stably()
        {
            Ready();
            Give("potion_healing", 2);
            Give("iron_ore", 5);
            Give("gold_ring");
            var page = _s.GetInventory();
            Assert.That(page.Groups.Select(g => g.Group), Has.Member("Consumables"));
            Assert.That(_s.GetInventory("Consumables").Rows.All(r => r.Group == "Consumables"), Is.True);
            Assert.That(_s.GetInventory(search: "ore").Rows.Select(r => r.ItemId), Has.Member("iron_ore"));
            var byWeight = _s.GetInventory(sort: InventorySort.Weight, descending: true).Rows;
            Assert.That(byWeight.Select(r => r.TotalGrams), Is.Ordered.Descending);
            Assert.That(_s.GetInventory(sort: InventorySort.Name).Rows.Select(r => r.StackId), Is.EqualTo(_s.GetInventory(sort: InventorySort.Name).Rows.Select(r => r.StackId)));
            Assert.That(page.ArmorClassText, Is.Not.Empty);
            var inspect = _s.InspectItem(Pack("potion_healing"));
            Assert.That(inspect.Lines.Any(l => l.Label == "Heals"), Is.True);
            Assert.That(inspect.Source, Does.Contain("SRD"));
        }

        // ----- Saves --------------------------------------------------------------------------------

        [Test]
        public void EveryItemState_SurvivesSaveAndLoad()
        {
            Ready();
            Give("wand_of_magic_missiles", 1, s => { s.Charges = 3; s.Condition = ItemCondition.Worn; s.Quality = ItemQuality.Fine; s.Provenance = "A gift"; s.AcquiredAt = 77; });
            Give("leatherworkers_tools");
            Give("cured_leather", 3);
            Ok(_s.Dispatch(StartWorkCommand.Craft("stitch_jerkin")));
            var ser = new JsonWorldSerializer();
            var json = ser.Serialize(W);
            var back = ser.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            var wand = back.World.Player.Inventory.Single(x => x.ItemId == "wand_of_magic_missiles");
            Assert.That((wand.Charges, wand.Attuned, wand.Condition, wand.Quality, wand.Provenance, wand.AcquiredAt), Is.EqualTo((3, false, ItemCondition.Worn, ItemQuality.Fine, "A gift", 77L)));
            Assert.That(back.World.WorkJobs.Single(j => !j.Done).Held.Sum(h => h.Quantity), Is.EqualTo(3));
            Assert.That(back.World.Containers.Count, Is.EqualTo(W.Containers.Count));
            Assert.That(back.World.GatheringSites.Count, Is.EqualTo(W.GatheringSites.Count));
            Assert.That(JObject.Parse(ser.Serialize(back.World))["world"].ToString(), Is.EqualTo(JObject.Parse(json)["world"].ToString()), "Round trip is exact (the save time aside).");
        }

        [Test]
        public void AVersion12Save_LoadsWithItsItemsIntact_AndGainsTheItemWorldOnce()
        {
            Ready();
            Give("potion_healing", 2);
            var ser = new JsonWorldSerializer();
            var doc = JObject.Parse(ser.Serialize(W));
            Phase2Worlds.StripToV12(doc);
            foreach (var field in new[] { "containers", "gatheringSites" }) Assert.That(doc["world"][field], Is.Null);
            var loaded = ser.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.World.Player.Inventory.Single(x => x.ItemId == "potion_healing").Quantity, Is.EqualTo(2));
            Assert.That(loaded.World.ItemCatalogVersion, Is.EqualTo(ItemCatalog.Version));
            Assert.That(loaded.World.WorldSettings.EnabledSimulationSystems, Has.Member(GatheringSystem.Id));
            Assert.That(ItemGenesis.Ensure(loaded.World), Is.True, "The chests and sites arrive once…");
            Assert.That(ItemGenesis.Ensure(loaded.World), Is.False, "…and only once.");
            Assert.That(WorldValidator.Validate(loaded.World).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(loaded.World).Errors));
        }

        // ----- Fault injection ----------------------------------------------------------------------

        [Test]
        public void TheValidator_CatchesImpossibleItemStates()
        {
            Ready();
            Give("potion_healing");
            string Errors(Action<WorldData> breakIt)
            {
                var w = W;
                breakIt(w);
                return string.Join("; ", WorldValidator.Validate(w).Errors);
            }

            Assert.That(Errors(w => w.FindContainer(ItemGenesis.ChestKey(w.Player.SettlementId)).Items.Add(w.Player.Inventory[0].Clone())), Does.Contain("more than one holder"));
            Assert.That(Errors(w => w.Player.Inventory.Add(NewStackWith("wand_of_magic_missiles", s => s.Charges = 99))), Does.Contain("charges"));
            Assert.That(Errors(w => w.Player.Inventory.Add(NewStackWith("berserker_axe", s => s.Bound = true))), Does.Contain("bound without"));
            Assert.That(Errors(w => w.Player.Inventory.Add(NewStackWith("potion_healing", s => s.Attuned = true))), Does.Contain("needs no attunement"));
            Assert.That(Errors(w => w.Containers.Add(w.Containers[0].Clone())), Does.Contain("duplicated"));
            Assert.That(Errors(w => { for (var i = 0; i < 4; i++) w.Player.Inventory.Add(NewStackWith("ring_of_protection", s => s.Attuned = true)); }), Does.Contain("attuned to"));
            Assert.That(Errors(w => w.GatheringSites[0].Available = w.GatheringSites[0].Capacity + 1), Does.Contain("impossible"));
            Assert.That(Errors(w => w.Player.Equipment[EquipmentSlot.OffHand] = NewStackWith("battleaxe", null)), Does.Contain("cannot be worn"));
        }

        private ItemStackData NewStackWith(string itemId, Action<ItemStackData> state)
        {
            var s = NewStack(itemId);
            state?.Invoke(s);
            return s;
        }

        [Test]
        public void CorruptItemStateInASave_IsRejected_NotGuessed()
        {
            Ready();
            Give("shortsword");
            var ser = new JsonWorldSerializer();
            var doc = JObject.Parse(ser.Serialize(W));
            var stack = ((JArray)doc["world"]["player"]["inventory"]).Cast<JObject>().First(x => (string)x["itemId"] == "shortsword");
            stack["condition"] = "Shiny";
            Assert.That(ser.Deserialize(doc.ToString()).IsSuccess, Is.False);
            stack["condition"] = "Broken";
            Assert.That(ser.Deserialize(doc.ToString()).IsSuccess, Is.True);
            doc["world"]["containers"][0]["kind"] = "Bottomless";
            Assert.That(ser.Deserialize(doc.ToString()).IsSuccess, Is.False);
        }
    }
}
