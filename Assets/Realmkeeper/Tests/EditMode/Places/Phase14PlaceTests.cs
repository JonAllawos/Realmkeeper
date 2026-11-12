using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Living;
using Realmkeeper.Tests.EditMode.Phase2;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 (ADR-0071): the five new places as real places in real worlds — generated with their stories, chosen by
    /// the world, bound to quests — and the features Phase 13 left as data: creatures that wake, bridges, fire, keys.
    /// </summary>
    public sealed class Phase14PlaceTests : Phase14Fixture
    {
        [Test]
        public void TheFiveNewPlaces_GenerateWithTheirStories_ValidAcrossManySeeds()
        {
            var stats = new Dictionary<string, int>();
            foreach (var def in Phase14Places.Definitions())
            {
                var a = ArchetypeCatalog.Find(def.ArchetypeId);
                for (var seed = 1UL; seed <= 60; seed++)
                {
                    var l = PlaceGenerator.Generate(a, seed * 977UL, def);
                    Assert.That(PlaceValidator.Validate(l), Is.Empty, $"{def.Id} seed {seed}");
                    foreach (var (feature, _, required) in def.Story)
                        if (required) Assert.That(l.Features.Any(f => f.Id == "story:" + feature), Is.True, $"{def.Id}: required {feature}, seed {seed}");
                    foreach (var (feature, _, _, _) in def.Waking)
                    {
                        var f = l.Features.FirstOrDefault(x => x.Id == "story:" + feature);
                        if (f == null) continue;
                        var g = l.Groups.SingleOrDefault(x => x.WakesOn == f.Id);
                        Assert.That(g, Is.Not.Null, $"{def.Id}: nobody sleeps by {feature}");
                        Assert.That(g.Room, Is.EqualTo(f.Room), "They sleep beside what wakes them.");
                    }

                    if (def.FireRoom != null) Assert.That(l.Rooms.Count(r => r.Hazard == "fire"), Is.EqualTo(1), $"{def.Id} burns in one room");
                    if (def.Bridges > 0 && l.Connections.Any(c => c.Kind == ConnectionKind.Bridge))
                    {
                        stats[def.Id] = (stats.TryGetValue(def.Id, out var n) ? n : 0) + 1;
                        var b = l.Connections.First(c => c.Kind == ConnectionKind.Bridge);
                        Assert.That(b.Bridge, Is.EqualTo(BridgeState.Damaged));
                        // A fallen bridge never cuts off what the place's story needs (on a loop it cuts off nothing at all).
                        var place = new PlaceData { Layout = l };
                        foreach (var other in l.Connections) if (other.Kind == ConnectionKind.Door) place.Doors[other.Id] = DoorState.Open; // locks can be picked or forced
                        place.Bridges[b.Id] = BridgeState.Destroyed;
                        var reach = PlaceRoutes.Reachable(place, l.Entrance.A, Mover.Resident);
                        foreach (var f in l.Features.Where(f => f.Required || f.Id.StartsWith("story:") || f.Get("key") != null))
                            Assert.That(reach.Contains(f.Room) || l.Room(f.Room).Secret, Is.True, $"{def.Id} seed {seed}: a fallen bridge cut off {f.Id}");
                    }

                    if (def.KeyItem != null)
                    {
                        var locked = l.Connections.FirstOrDefault(c => c.KeyItem == def.KeyItem);
                        Assert.That(locked, Is.Not.Null, $"{def.Id}: a lock takes the key");
                        var holder = l.Features.FirstOrDefault(f => f.Get("key") == def.KeyItem);
                        if (holder != null)
                        {
                            var place = new PlaceData { Layout = l };
                            foreach (var other in l.Connections) if (other.Kind == ConnectionKind.Door) place.Doors[other.Id] = DoorState.Open; // other locks can be picked or forced
                            place.Doors[locked.Id] = DoorState.Locked;
                            Assert.That(PlaceRoutes.Reachable(place, l.Entrance.A, Mover.Follower).Contains(holder.Room) || holder.Room == l.Entrance.A, Is.True,
                                $"{def.Id} seed {seed}: the key is behind its own lock");
                        }
                    }
                }
            }

            Assert.That(stats.TryGetValue(Phase14Places.Works, out var bridged) ? bridged : 0, Is.GreaterThan(30), "Most culverts have their bridge.");
            TestContext.WriteLine("Culverts with a bridge: " + string.Join(", ", stats.Select(p => p.Key + " " + p.Value)));
        }

        [Test]
        public void EachWorld_ChoosesThreeToFive_ByItsOwnContext_AndTheSameSeedAlwaysChoosesTheSame()
        {
            var seen = new HashSet<string>();
            foreach (var seed in new[] { 123456789L, 42L, 7L, 1001L, 31337L, 99L })
            {
                var w = Phase2Worlds.Generated(seed);
                var chosen = w.Places.Where(p => Phase14Places.All.Contains(p.DefinitionId)).Select(p => p.DefinitionId).OrderBy(x => x).ToList();
                Assert.That(chosen.Count, Is.InRange(3, 5), "seed " + seed);
                Assert.That(chosen.Distinct().Count(), Is.EqualTo(chosen.Count), "No place twice.");
                var again = Phase2Worlds.Generated(seed).Places.Where(p => Phase14Places.All.Contains(p.DefinitionId)).Select(p => p.DefinitionId).OrderBy(x => x).ToList();
                Assert.That(again, Is.EqualTo(chosen), "Deterministic.");
                foreach (var p in w.Places.Where(p => Phase14Places.All.Contains(p.DefinitionId)))
                {
                    Assert.That(PlaceValidator.Validate(p.Layout), Is.Empty);
                    Assert.That(p.Discovery, Is.EqualTo(PlaceDiscovery.Unknown));
                    var quest = p.DefinitionId == Phase14Places.Catacomb ? QuestCatalog.FoundersRegister : p.DefinitionId == Phase14Places.Temple ? QuestCatalog.FaneLantern :
                        p.DefinitionId == Phase14Places.Hideout ? QuestCatalog.RoadTolls : p.DefinitionId == Phase14Places.Works ? QuestCatalog.Culverts : null;
                    if (quest != null) Assert.That(w.Quests.Count(q => q.DefinitionId == quest && q.Bound("place") == p.PlaceId), Is.LessThanOrEqualTo(1), "Bound once at most.");
                    if (p.DefinitionId == Phase14Places.Temple) Assert.That(p.Variables.ContainsKey("built_by"), Is.True, "The fane belongs to a faction's past.");
                }

                seen.UnionWith(chosen);
                var v = WorldValidator.Validate(w);
                Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
            }

            Assert.That(seen, Is.EquivalentTo(Phase14Places.All), "Across worlds, every new place appears somewhere.");
        }

        [Test]
        public void TheSealedVault_WakesItsSleepers_Once_AndTheDeadStayDead()
        {
            H.Hero();
            var cat = Enter(Phase14Places.Catacomb);
            var vault = cat.Layout.Features.Single(f => f.Id == "story:sealed_vault");
            var sleepers = cat.Layout.Groups.Single(g => g.WakesOn == vault.Id);
            H.WalkTo(vault.Room);
            Assert.That(S.GetEncounter()?.Status, Is.Not.EqualTo(EncounterStatus.Active), "They sleep until disturbed.");
            Ok(S.Dispatch(new PlaceActionCommand(vault.Id, PlaceAction.Open)));
            Assert.That(S.GetEncounter().Status, Is.EqualTo(EncounterStatus.Active), "Opening it wakes them.");
            Assert.That(Place(Phase14Places.Catacomb).GroupOf(sleepers.Id).Woken, Is.True);
            EncounterGenerationTests.FightToTheEnd(S);
            Assert.That(Place(Phase14Places.Catacomb).GroupStateOf(sleepers.Id), Is.EqualTo(GroupState.Defeated));
            var dead = W.Creatures.Count(c => c.OriginId == "place:" + cat.PlaceId + ":" + sleepers.Id && c.State == CreatureInstanceState.Dead);
            Assert.That(dead, Is.EqualTo(sleepers.Members.Count));

            // Out and back in: nothing wakes again, nobody new appears.
            var creatures = W.Creatures.Count;
            H.WalkTo(cat.Layout.Entrance.A);
            Ok(S.Dispatch(new ExitPlaceCommand()));
            Ok(S.Dispatch(new EnterPlaceCommand(cat.PlaceId)));
            H.WalkTo(vault.Room, fight: false);
            Assert.That(S.GetEncounter()?.Status, Is.Not.EqualTo(EncounterStatus.Active));
            Assert.That(W.Creatures.Count, Is.EqualTo(creatures), "No creature spawned twice.");
            Valid();
        }

        [Test]
        public void NobodyWakes_ThroughAWall_TheyAwaitThePlayer()
        {
            H.Hero();
            var cat = Enter(Phase14Places.Catacomb);
            var vault = cat.Layout.Features.Single(f => f.Id == "story:sealed_vault");
            var sleepers = cat.Layout.Groups.Single(g => g.WakesOn == vault.Id);
            // Fault injection: the sleepers lie in another room, behind a shut door.
            var other = cat.Layout.Rooms.First(r => r.Id != vault.Room && !r.Secret && r.Floor == cat.Layout.Room(vault.Room).Floor && !PlaceRules.InSight(cat, r.Id, vault.Room));
            H.Edit(w =>
            {
                var g = w.FindPlace(cat.PlaceId).Layout.Group(sleepers.Id);
                g.Room = other.Id;
                for (var i = 0; i < g.Members.Count; i++) { g.Members[i].X = other.Rect.X + i; g.Members[i].Z = other.Rect.Z; }
            });
            H.WalkTo(vault.Room);
            var r = S.Dispatch(new PlaceActionCommand(vault.Id, PlaceAction.Open));
            Ok(r);
            Assert.That(S.GetEncounter()?.Status, Is.Not.EqualTo(EncounterStatus.Active), "They cannot see the player: no fight through the wall.");
            Assert.That(r.Message, Does.Contain("something stirs"));
            Assert.That(Place(Phase14Places.Catacomb).GroupOf(sleepers.Id).Woken, Is.True);
            var woke = W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.CreaturesWoke);
            Assert.That(woke, Is.EqualTo(1));
            var again = S.Dispatch(new PlaceActionCommand(vault.Id, PlaceAction.Open)); // opening it again wakes nobody a second time
            Assert.That(again.Message ?? string.Empty, Does.Not.Contain("something stirs"));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.CreaturesWoke), Is.EqualTo(woke), "They woke once.");
            H.WalkTo(other.Id);
            Assert.That(W.Encounters.Any(e => e.Origin?.GroupId == sleepers.Id), Is.True, "Awake, they fight on sight when found.");
        }

        [Test]
        public void ABridge_HasAState_AndAFallenOne_IsNoWayForAnyone()
        {
            H.Hero();
            var works = Enter(Phase14Places.Works);
            var bridge = works.Layout.Connections.FirstOrDefault(c => c.Kind == ConnectionKind.Bridge);
            Assume.That(bridge, Is.Not.Null, "This world's culverts have their bridge.");
            Assert.That(S.GetPlaceScene().Bridges[bridge.Id], Is.EqualTo(BridgeState.Damaged));
            H.Edit(w => w.FindPlace(works.PlaceId).Bridges[bridge.Id] = BridgeState.Destroyed);
            H.WalkTo(bridge.A, fight: true);
            var far = works.Layout.Room(bridge.B);
            Refused(new MoveInPlaceCommand(far.Id, PlaceRules.PoseAt(works, far.Floor, far.Rect.CenterX, far.Rect.CenterZ, 0f)), "bridge is gone");
            Assert.That(PlaceRoutes.Passable(Place(Phase14Places.Works), bridge, bridge.A, Mover.Resident), Is.False);
            Assert.That(PlaceRoutes.Passable(Place(Phase14Places.Works), bridge, bridge.A, Mover.Follower), Is.False);
            Valid();
        }

        [Test]
        public void ADamagedBridge_TakesAnAcrobaticsCheck_AndAFallBringsItDownForGood()
        {
            H.Hero();
            var works = Enter(Phase14Places.Works);
            var bridge = works.Layout.Connections.FirstOrDefault(c => c.Kind == ConnectionKind.Bridge);
            Assume.That(bridge, Is.Not.Null);
            H.WalkTo(bridge.A);
            var far = works.Layout.Room(bridge.B);
            var hp = W.Player.Rpg.CurrentHp;
            var r = S.Dispatch(new MoveInPlaceCommand(far.Id, PlaceRules.PoseAt(works, far.Floor, far.Rect.CenterX, far.Rect.CenterZ, 0f)));
            Ok(r);
            Assert.That(r.Message, Does.Contain("Acrobatics"));
            var state = Place(Phase14Places.Works).BridgeOf(bridge);
            if (W.Player.PlaceRoom == far.Id) Assert.That(state, Is.EqualTo(BridgeState.Damaged), "Crossed: it held, still damaged.");
            else
            {
                Assert.That(state, Is.EqualTo(BridgeState.Destroyed));
                Assert.That(W.Player.PlaceRoom, Is.EqualTo(bridge.A), "Back on the side they started from.");
                Assert.That(W.Player.Rpg.CurrentHp, Is.LessThan(hp));
            }

            Valid();
        }

        [Test]
        public void Fire_BurnsForItsTime_ThroughTheSharedRules_ThenIsOut()
        {
            H.Hero();
            var temple = Place(Phase14Places.Temple);
            var burning = temple.Layout.Rooms.Single(r => r.Hazard == "fire");
            Enter(Phase14Places.Temple);
            var hp = W.Player.Rpg.CurrentHp;
            H.WalkTo(burning.Id);
            Assert.That(S.GetPlaceScene().Fires[burning.Id], Is.EqualTo("burning"));
            Assert.That(S.RestProblem(Realmkeeper.Domain.Rpg.RestKind.Short), Does.Contain("fire"));
            Assert.That(W.Player.Rpg.CurrentHp, Is.LessThan(hp).Or.EqualTo(hp), "A Dexterity save: fire damage, halved on a success.");
            Hours(5);
            Assert.That(S.GetPlaceScene().Fires[burning.Id], Is.EqualTo("burnt out"));
            Assert.That(PlaceRules.FireState(W, Place(Phase14Places.Temple), burning), Is.EqualTo("burnt out"));
            Valid();
        }

        [Test]
        public void TheOutlaws_Parley_AndTheRoadIsFree_ByWordOrByBlade()
        {
            H.Hero();
            // The den is no place for a beginner: a captain with three attacks. A seasoned fighter (level 6) for the blade route.
            var need = Realmkeeper.Domain.Rpg.ProgressionRules.ExperienceFor(6) - W.Player.Rpg.Experience;
            Ok(S.Dispatch(new AwardExperienceCommand(need, Realmkeeper.Domain.Rpg.ExperienceSource.Milestone, null, "test")));
            while (W.Player.Rpg.Level < 6) Ok(S.Dispatch(new AdvanceCharacterCommand(Realmkeeper.Domain.Rpg.CharacterAdvancement.SuggestChoices(W.Player.Rpg, W.Player.Rpg.Level + 1))));
            var den = Enter(Phase14Places.Hideout);
            var lookouts = den.Layout.Groups.Single(g => g.Behaviour == GroupBehaviour.Parley);
            var quest = W.Quests.SingleOrDefault(q => q.DefinitionId == QuestCatalog.RoadTolls);
            H.WalkTo(lookouts.Room);
            Assert.That(S.GetEncounter()?.Status, Is.Not.EqualTo(EncounterStatus.Active), "They talk before they fight.");
            var opts = S.GetPlaceActions(lookouts.Id);
            Assert.That(opts.Select(o => o.Action), Is.SupersetOf(new[] { PlaceAction.PayToll, PlaceAction.Persuade, PlaceAction.Intimidate, PlaceAction.Attack }));
            for (var i = 0; i < 3 && !Place(Phase14Places.Hideout).Variables.ContainsKey("road") && S.GetEncounter()?.Status != EncounterStatus.Active; i++)
            {
                var r = S.Dispatch(new PlaceActionCommand(lookouts.Id, PlaceAction.Persuade));
                if (!r.IsSuccess) break;
            }

            if (S.GetEncounter()?.Status == EncounterStatus.Active) EncounterGenerationTests.FightToTheEnd(S);
            if (!Place(Phase14Places.Hideout).Variables.ContainsKey("road"))
            {
                // The talk did not land: drive them off instead, room by room (again, for any who fled and came back).
                for (var pass = 0; pass < 8 && !Place(Phase14Places.Hideout).Variables.ContainsKey("road"); pass++)
                {
                H.Edit(w => { w.Player.Rpg.CurrentHp = new Realmkeeper.Domain.Rpg.CharacterRules(w.Player.Rpg).MaxHp; w.Player.Rpg.IsStable = false; w.Player.Rpg.DeathSaveSuccesses = w.Player.Rpg.DeathSaveFailures = 0; }); // a breather between attempts
                foreach (var g in Place(Phase14Places.Hideout).Layout.Groups.Where(x => x.WakesOn == null))
                    if (Place(Phase14Places.Hideout).GroupStateOf(g.Id) != GroupState.Defeated && Place(Phase14Places.Hideout).GroupStateOf(g.Id) != GroupState.Departed)
                    {
                        H.WalkTo(g.Room);
                        var st = Place(Phase14Places.Hideout).GroupStateOf(g.Id);
                        if (S.GetEncounter()?.Status != EncounterStatus.Active && st != GroupState.Defeated && st != GroupState.Departed) Ok(S.Dispatch(new PlaceActionCommand(g.Id, PlaceAction.Attack)));
                        if (S.GetEncounter()?.Status == EncounterStatus.Active) EncounterGenerationTests.FightToTheEnd(S);
                    }
                }
            }

            var after = Place(Phase14Places.Hideout);
            Assert.That(after.Variables["road"], Is.EqualTo("left").Or.EqualTo("cleared"));
            foreach (var r in W.TradeRoutes) Assert.That(RoadThreat.PercentTaken(W, r), Is.EqualTo(0), "Nobody on the road now.");
            if (quest != null)
            {
                var q = W.FindQuest(quest.InstanceId);
                Assert.That(q.Objectives.Any(o => o.Id == "deal"), Is.True);
            }

            Valid();
        }

        [Test]
        public void TheCulvertsSluice_DrainsTheChannels_ForGood()
        {
            H.Hero();
            var works = Enter(Phase14Places.Works);
            var wheel = works.Layout.Features.Single(f => f.Id == "story:sluice_wheel");
            H.WalkTo(wheel.Room);
            Ok(S.Dispatch(new PlaceActionCommand(wheel.Id, PlaceAction.Activate)));
            var after = Place(Phase14Places.Works);
            Assert.That(after.Variables["drained"], Is.EqualTo("yes"));
            foreach (var r in after.Layout.Rooms.Where(r => r.Hazard == "deep_water"))
                Assert.That(PlaceRules.HazardOf(after, r), Is.Null, "The water there is gone.");
            Assert.That(S.GetPlaceScene().Drained, Is.True);
            Valid();
        }
    }
}
