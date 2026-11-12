using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Living;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 18 (ADR-0088, ADR-0089): the new places and their mechanisms, the discovery kinds and certainty, and what the
    /// player can do with what they find. Scenarios 1–14 of the Phase 18 acceptance list.
    /// </summary>
    public sealed class Phase18ExplorationTests : Phase14Fixture
    {
        private void Give(string item, int n = 1) =>
            H.Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find(item), n, "test")));

        private static bool Has(WorldData w, string key) => w.Player.Social.Knows(key);

        // 1. Generation: every new place, across many seeds, valid, with its puzzle solvable.
        [Test]
        public void TheNewPlaces_GenerateValid_WithSolvablePuzzles_AcrossManySeeds()
        {
            foreach (var def in Phase18Places.Definitions())
            {
                var a = ArchetypeCatalog.Find(def.ArchetypeId);
                for (var seed = 1UL; seed <= 60; seed++)
                {
                    var l = PlaceGenerator.Generate(a, seed * 7919UL, def);
                    Assert.That(PlaceValidator.Validate(l), Is.Empty, $"{def.Id} seed {seed}");
                    foreach (var (feature, _, required) in def.Story)
                        if (required) Assert.That(l.Features.Any(f => f.Id == "story:" + feature), Is.True, $"{def.Id}: required {feature}");
                    var entrance = l.Entrance.A;
                    switch (def.Id)
                    {
                        case Phase18Places.Vault:
                        {
                            var bell = l.Feature("story:bell_ferry");
                            var seal = l.Connection(bell.Get("opens"));
                            Assert.That(seal.Door, Is.EqualTo(DoorState.Locked));
                            Assert.That(seal.LockDc, Is.EqualTo(22), "The door can still be picked or forced — very hard (another way through).");
                            var near = Phase18Places.Reach(l, entrance, seal);
                            foreach (var id in new[] { "story:ringers_verse", "story:bell_ferry", "story:bell_mill", "story:bell_forge" })
                                Assert.That(near.Contains(l.Feature(id).Room), Is.True, $"{id} lies before the door it opens (seed {seed}).");
                            Assert.That(near.Contains(l.Feature("story:ringers_tomb").Room), Is.False, "The tomb is behind the ringers' door.");
                            break;
                        }

                        case Phase18Places.Drift:
                        {
                            var silver = l.Feature("story:silver_vein");
                            var flooded = l.Room(silver.Room);
                            Assert.That(flooded.Hazard, Is.EqualTo("deep_water"));
                            Assert.That(l.Feature("story:drift_pump").Room, Is.Not.EqualTo(flooded.Id), "The pump stands on dry ground.");
                            Assert.That(l.Feature("story:intake_grate").Room, Is.Not.EqualTo(flooded.Id));
                            break;
                        }

                        case Phase18Places.Hollin:
                        {
                            var lintel = l.Feature("story:scored_lintel");
                            var way = l.Connection(lintel.Get("reveals"));
                            Assert.That(way.Kind, Is.EqualTo(ConnectionKind.Secret));
                            Assert.That(way.Joins(lintel.Room), Is.True, "The sign is beside the hidden way it points to.");
                            Assert.That(l.Room(l.Feature("story:reeves_strongbox").Room).Secret, Is.True, "The strongbox is in the hidden room.");
                            break;
                        }
                    }
                }
            }
        }

        // 2. The world: each region its own Phase 18 places, bound to their quests, once.
        [Test]
        public void TheWorld_HasItsPhase18Places_AndTheirChains_Once()
        {
            var w = W;
            var mine = w.Places.Where(p => Phase18Places.All.Contains(p.DefinitionId)).ToList();
            Assert.That(mine.Count, Is.InRange(3, 4));
            Assert.That(w.Quests.Count(q => q.DefinitionId == QuestCatalog.RecordHouse), Is.LessThanOrEqualTo(1));
            Assert.That(w.Quests.Count(q => q.DefinitionId == QuestCatalog.DrownedDrift), Is.EqualTo(1), "The drift's quest is bound.");
            var drift = w.Quests.Single(q => q.DefinitionId == QuestCatalog.DrownedDrift);
            Assert.That(drift.Bound("place"), Is.EqualTo(Place(Phase18Places.Drift).PlaceId));
            // Ensured again, nothing changes.
            var before = w.Places.Count;
            Assert.That(Realmkeeper.Infrastructure.Generation.PlaceGenesis.EnsurePhase18(w, new Realmkeeper.Infrastructure.Generation.RegionGenerator(new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory()).RegenerateRegion(w)), Is.False);
            Assert.That(w.Places.Count, Is.EqualTo(before));
            var v = WorldValidator.Validate(w);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        // 3–5. The ringers' bells: a verse to read, the wrong order hurts and starts again, the right order opens the door, and it all persists.
        [Test]
        public void TheRingersBells_WrongOrderHurtsAndResets_RightOrderOpens_AndItPersists()
        {
            H.Hero(5);
            var vault = Enter(Phase18Places.Vault);
            var verse = vault.Layout.Feature("story:ringers_verse");
            H.WalkTo(verse.Room);
            Give("hooded_lantern");
            Ok(S.Dispatch(new PlaceActionCommand(verse.Id, PlaceAction.Read)));
            Assert.That(Has(W, "ringers_order"), Is.True, "The verse tells the order.");
            var d = W.Player.Social.FindDiscovery("ringers_order");
            Assert.That(d.Category, Is.EqualTo(DiscoveryCategory.Cultural));
            Assert.That(d.Major, Is.True);

            var forge = vault.Layout.Feature("story:bell_forge");
            var ferry = vault.Layout.Feature("story:bell_ferry");
            var mill = vault.Layout.Feature("story:bell_mill");
            var seal = vault.Layout.Connection(ferry.Get("opens"));
            H.WalkTo(forge.Room);
            var hp = W.Player.Rpg.CurrentHp;
            Ok(S.Dispatch(new PlaceActionCommand(forge.Id, PlaceAction.Activate))); // the forge first: wrong
            Assert.That(Place(Phase18Places.Vault).Variables["seq:bells"], Is.EqualTo(string.Empty), "The sequence starts again.");
            Assert.That(Place(Phase18Places.Vault).Variables["seq:bells:fails"], Is.EqualTo("1"));
            Assert.That(W.Player.Rpg.CurrentHp, Is.LessThanOrEqualTo(hp), "Grit and stone: a cost for the wrong bell (a save may halve it).");
            Assert.That(Place(Phase18Places.Vault).DoorOf(seal), Is.EqualTo(DoorState.Locked));

            // Persisted: save, load, the failure is still counted and the door still shut.
            H.Edit(_ => { });
            Assert.That(Place(Phase18Places.Vault).Variables["seq:bells:fails"], Is.EqualTo("1"));

            foreach (var bell in new[] { ferry, mill })
            {
                H.WalkTo(bell.Room);
                Ok(S.Dispatch(new PlaceActionCommand(bell.Id, PlaceAction.Activate)));
            }

            Assert.That(Place(Phase18Places.Vault).Variables["seq:bells"], Is.EqualTo("12"), "Two of three, in order.");
            H.Edit(_ => { }); // mid-sequence, saved and loaded
            H.WalkTo(forge.Room);
            Ok(S.Dispatch(new PlaceActionCommand(forge.Id, PlaceAction.Activate)));
            var after = Place(Phase18Places.Vault);
            Assert.That(after.Variables["seq:bells"], Is.EqualTo("solved"));
            Assert.That(after.DoorOf(seal), Is.EqualTo(DoorState.Open), "The ringers' door opens.");
            Assert.That(Has(W, "ringers_door_open"), Is.True);
            Assert.That(PlaceActions.Options(W, after, after.Layout.Room(forge.Room), forge.Id).Any(o => o.Action == PlaceAction.Activate), Is.False, "Solved: the bells are not rung again.");
            H.Edit(_ => { });
            Assert.That(Place(Phase18Places.Vault).DoorOf(seal), Is.EqualTo(DoorState.Open), "Never reset on load.");
            H.Valid();
        }

        // 6. Two wrong peals wake the dead by the tomb.
        [Test]
        public void TheRingersBells_SecondWrongPeal_WakesTheDead()
        {
            H.Hero(5);
            var vault = Enter(Phase18Places.Vault);
            var mill = vault.Layout.Feature("story:bell_mill");
            H.WalkTo(mill.Room);
            Ok(S.Dispatch(new PlaceActionCommand(mill.Id, PlaceAction.Activate)));
            Assert.That(S.GetEncounter()?.Status, Is.Not.EqualTo(Realmkeeper.Domain.Combat.EncounterStatus.Active), "Once: only grit.");
            Ok(S.Dispatch(new PlaceActionCommand(mill.Id, PlaceAction.Activate)));
            var tomb = vault.Layout.Feature("story:ringers_tomb");
            var sleepers = vault.Layout.Groups.Single(g => g.WakesOn == tomb.Id);
            Assert.That(Place(Phase18Places.Vault).GroupOf(sleepers.Id).Woken, Is.True, "Twice: the ringers' dead wake.");
        }

        // 7–9. The drift's pump: worked choked it cracks (and stays cracked), mended with the right tool, cleared and worked it drains in stages.
        [Test]
        public void TheDriftPump_CracksIfChoked_MendsWithATool_DrainsInStages_AndPersists()
        {
            H.Hero(5);
            var drift = Enter(Phase18Places.Drift);
            var pump = drift.Layout.Feature("story:drift_pump");
            var grate = drift.Layout.Feature("story:intake_grate");
            H.WalkTo(pump.Room);
            Ok(S.Dispatch(new PlaceActionCommand(pump.Id, PlaceAction.Activate)));
            Assert.That(Place(Phase18Places.Drift).Variables["pump"], Is.EqualTo("cracked"), "Worked with the grate choked, the barrel splits.");
            H.Edit(_ => { });
            var room = W.FindPlace(drift.PlaceId).Layout.Room(pump.Room);
            var opts = PlaceActions.Options(W, Place(Phase18Places.Drift), room, pump.Id);
            Assert.That(opts.Any(o => o.Action == PlaceAction.Activate), Is.False, "A cracked pump cannot be worked.");
            Assert.That(opts.Single(o => o.Action == PlaceAction.Mend).Enabled, Is.False, "Mending needs the right tool.");
            Give("pump_wrench");
            Ok(S.Dispatch(new PlaceActionCommand(pump.Id, PlaceAction.Mend)));
            Assert.That(Place(Phase18Places.Drift).Variables.ContainsKey("pump"), Is.False);

            // Clearing the grate is a check; a failure costs blood and the attempt. Here it is cleared for the test of the stages.
            H.WalkTo(grate.Room);
            var tried = S.Dispatch(new PlaceActionCommand(grate.Id, PlaceAction.Clear));
            Assert.That(tried.IsSuccess, Is.True, tried.Message);
            if (!Place(Phase18Places.Drift).Variables.ContainsKey("intake")) H.Edit(w => w.FindPlace(drift.PlaceId).Variables["intake"] = "clear");

            H.WalkTo(pump.Room);
            Ok(S.Dispatch(new PlaceActionCommand(pump.Id, PlaceAction.Activate)));
            Assert.That(PlaceActions.Water(Place(Phase18Places.Drift)), Is.EqualTo(1), "HALF.");
            H.Edit(_ => { });
            Assert.That(PlaceActions.Water(Place(Phase18Places.Drift)), Is.EqualTo(1), "Kept across a save.");
            Ok(S.Dispatch(new PlaceActionCommand(pump.Id, PlaceAction.Activate)));
            Assert.That(PlaceActions.Water(Place(Phase18Places.Drift)), Is.EqualTo(0), "DRY.");
            Assert.That(Place(Phase18Places.Drift).Variables.ContainsKey("drained"), Is.True, "The flooded gallery no longer needs swimming.");
            Assert.That(Has(W, "drift_drained"), Is.True);
            H.Valid();
        }

        // 10–11. The reeve's lintel: only by a flame held close; then the hidden way is found and the discovery is major.
        [Test]
        public void TheScoredLintel_NeedsAFlame_RevealsTheHiddenWay_MajorDiscoveryOnce()
        {
            H.Hero(5);
            var tower = Enter(Phase18Places.Hollin);
            var lintel = tower.Layout.Feature("story:scored_lintel");
            var way = tower.Layout.Connection(lintel.Get("reveals"));
            H.WalkTo(lintel.Room);
            var room = W.FindPlace(tower.PlaceId).Layout.Room(lintel.Room);
            if (W.Player.Inventory.Any(s => ItemCatalog.Find(s.ItemId)?.Tags.Contains("light") == true))
                H.Edit(w => w.Player.Inventory.RemoveAll(s => ItemCatalog.Find(s.ItemId)?.Tags.Contains("light") == true));
            var opt = PlaceActions.Options(W, Place(Phase18Places.Hollin), room, lintel.Id).Single(o => o.Action == PlaceAction.Illuminate);
            Assert.That(opt.Enabled, Is.False, "Without a flame, the scoring stays unreadable.");
            Give("shuttered_lantern");
            var xp = W.Player.Rpg.Experience;
            Ok(S.Dispatch(new PlaceActionCommand(lintel.Id, PlaceAction.Illuminate)));
            Assert.That(Place(Phase18Places.Hollin).Found(way), Is.True, "The hidden way is found.");
            var d = W.Player.Social.FindDiscovery("hollin_reeves_step");
            Assert.That(d.Category, Is.EqualTo(DiscoveryCategory.Arcane));
            Assert.That(d.Major, Is.True);
            Assert.That(W.Player.Rpg.Experience, Is.EqualTo(xp + QuestRuntime.MajorDiscoveryXp), "A major discovery: experience once.");
            Assert.That(PlaceActions.Options(W, Place(Phase18Places.Hollin), room, lintel.Id).Any(o => o.Action == PlaceAction.Illuminate), Is.False, "Read once.");
            Assert.That(QuestRuntime.Discover(W, new DiscoveryData { Key = "hollin_reeves_step", Text = "again", Major = true }), Is.False, "Never twice.");
        }

        // 12. Every clue now records what it tells, under its place and kind; guesses wait for their proof.
        [Test]
        public void GenericClues_RecordWhatTheyTell_ByKind_AndGuessesAreConfirmedByTheirProof()
        {
            var w = W;
            var social = w.Player.Social;
            Assert.That(QuestRuntime.Discover(w, new DiscoveryData { Key = "wild_cave:claw_marks", Text = "A bear.", Category = DiscoveryCategory.Natural, Certainty = DiscoveryCertainty.Unverified,
                ConfirmedBy = "group:a bear's den@place:wild_cave" }), Is.True);
            Assert.That(social.FindDiscovery("wild_cave:claw_marks").Certainty, Is.EqualTo(DiscoveryCertainty.Unverified));
            Assert.That(QuestRuntime.Confirm(w, "group:a bear's den@place:wild_cave"), Is.EqualTo(1));
            Assert.That(social.FindDiscovery("wild_cave:claw_marks").Certainty, Is.EqualTo(DiscoveryCertainty.Confirmed), "Seen with their own eyes.");

            // A proof found first: the guess is confirmed as it is recorded.
            QuestRuntime.Discover(w, new DiscoveryData { Key = "cairn_watch_log", Text = "log", Category = DiscoveryCategory.Faction });
            QuestRuntime.Discover(w, new DiscoveryData { Key = "cairn_boundary", Text = "stone", Category = DiscoveryCategory.Historical, Certainty = DiscoveryCertainty.Unverified, ConfirmedBy = "cairn_watch_log" });
            Assert.That(social.FindDiscovery("cairn_boundary").Certainty, Is.EqualTo(DiscoveryCertainty.Confirmed));

            // A rumour of the same key is verified by the discovery.
            social.Rumors.Add(new RumorData { Key = "drift_silver", Text = "silver below the water", Confidence = 40 });
            QuestRuntime.Discover(w, new DiscoveryData { Key = "drift_silver", Text = "silver", Category = DiscoveryCategory.Economic });
            Assert.That(social.Rumors.Single(r => r.Key == "drift_silver").Verified, Is.True);

            // Every categorised feature with an insight is a real discovery; categories are all used.
            foreach (var f in FeatureCatalog.All.Where(f => f.Insight != null && f.Kind != FeatureKind.Container))
                if (f.Category == DiscoveryCategory.None) Assert.That(f.Discovery, Is.Not.Null, $"{f.Id}: an insight that records nothing");
            var used = FeatureCatalog.All.Select(f => f.Category).Where(c => c != DiscoveryCategory.None).Distinct().ToList();
            Assert.That(used.Count, Is.EqualTo(10), "Every discovery kind is used by something in the world.");
        }

        // 13. Telling what you found: the right people care, once per settlement; it survives save and load.
        [Test]
        public void Discoveries_AreToldToThoseTheyConcern_OncePerSettlement_AndSurviveSaveLoad()
        {
            H.Hero();
            var w0 = W;
            // History matters to an elder (the first named elder of the world, in their own town, by day).
            var elder = w0.Npcs.Where(n => !n.IsDeceased && n.Role == NpcRole.Elder && n.Tier != NpcTier.Unassigned).OrderBy(n => n.NpcId).First();
            var town = elder.HomeSettlementId;
            H.Edit(w =>
            {
                var p = w.Player;
                p.LocationKind = CharacterLocationKind.WorldMap;
                p.SettlementId = default;
                p.RegionId = w.FindSettlement(town).RegionId;
                p.LastSettlementId = town;
                w.Journey = null;
                w.MinuteOfDay = 10 * 60;
                foreach (var g in w.Groups) if (g.LeaderNpcId == elder.NpcId) g.LeaderNpcId = default;
            });
            Ok(S.Dispatch(new EnterSettlementCommand(town)));
            var merchant = w0.Npcs.FirstOrDefault(n => n.HomeSettlementId == town && n.Role == NpcRole.Merchant && !n.IsDeceased);
            H.Edit(w =>
            {
                w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "ashgrave_founders", Text = "The founders' burials.", Category = DiscoveryCategory.Historical, Major = true });
                w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "wild_cave:claw_marks", Text = "A bear, maybe.", Category = DiscoveryCategory.Natural, Certainty = DiscoveryCertainty.Unverified });
            });
            var w = W;
            var offered = DiscoveryRules.Offerable(w, w.FindNpc(elder.NpcId));
            Assert.That(offered.Select(d => d.Key), Does.Contain("ashgrave_founders"), "History matters to an elder.");
            Assert.That(offered.Select(d => d.Key), Does.Not.Contain("wild_cave:claw_marks"), "A guess is not worth passing on.");
            if (merchant != null) Assert.That(DiscoveryRules.Offerable(w, w.FindNpc(merchant.NpcId)).Select(d => d.Key), Does.Not.Contain("ashgrave_founders"), "Nor to a merchant.");

            var rep = w.Player.Social.ReputationIn(town);
            Ok(S.Dispatch(new StartConversationCommand(elder.NpcId)));
            Ok(S.Dispatch(new ChooseResponseCommand("share:ashgrave_founders")));
            Assert.That(W.Player.Social.ReputationIn(town), Is.GreaterThan(rep), "Bringing home the town's history raises its regard.");
            Assert.That(W.Conversation.Variables["answer"], Is.Not.Empty);
            Ok(S.Dispatch(new EndConversationCommand()));
            Assert.That(DiscoveryRules.Offerable(W, W.FindNpc(elder.NpcId)).Any(d => d.Key == "ashgrave_founders"), Is.False, "Told once in this settlement.");

            H.Edit(_ => { });
            var again = W.Player.Social.FindDiscovery("ashgrave_founders");
            Assert.That(again.SharedIn, Does.Contain(town.ToString()));
            Assert.That(again.Category, Is.EqualTo(DiscoveryCategory.Historical));
            Assert.That(W.Player.Social.FindDiscovery("wild_cave:claw_marks").Certainty, Is.EqualTo(DiscoveryCertainty.Unverified), "Certainty survives save and load.");
        }

        // 14. The journal groups discoveries by kind and shows certainty — and shows nothing the player has not found.
        [Test]
        public void TheJournal_GroupsDiscoveries_ByKind_WithCertainty_AndHidesTheUnfound()
        {
            H.Edit(w =>
            {
                w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "a", Text = "A danger.", Category = DiscoveryCategory.Dangerous, Major = true });
                w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "b", Text = "A guess.", Category = DiscoveryCategory.Natural, Certainty = DiscoveryCertainty.Unverified });
            });
            var j = S.GetJournal();
            Assert.That(j.DiscoveryEntries.Single(e => e.Key == "a").Category, Is.EqualTo(DiscoveryCategory.Dangerous));
            Assert.That(j.DiscoveryEntries.Single(e => e.Key == "b").Certainty, Is.EqualTo(DiscoveryCertainty.Unverified));
            Assert.That(j.DiscoveryEntries.Any(e => e.Key == "drift_silver"), Is.False, "Only what the player has found.");
        }
    }
}
