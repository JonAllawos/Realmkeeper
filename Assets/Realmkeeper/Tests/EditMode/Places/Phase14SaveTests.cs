using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 (schema v16, ADR-0071): a Phase 13 save — with explored rooms, opened chests, dead creatures and an active
    /// quest — gains the new places exactly once and keeps everything it had; every new field survives save and load;
    /// loading twice changes nothing.
    /// </summary>
    public sealed class Phase14SaveTests : Phase14Fixture
    {
        [Test]
        public void APhase13Save_WithProgress_GainsTheNewPlacesOnce_AndKeepsWhatItHad()
        {
            // Play some of Lost Below: into the mine, through rooms, a fight won.
            H.Hero();
            var giver = W.FindNpc(new Realmkeeper.Domain.Identifiers.NpcId(System.Guid.Parse(W.Quests.Single(q => q.DefinitionId == QuestCatalog.LostBelow).Bound("giver"))));
            var mine = Enter(PlaceCatalog.Mine);
            var den = mine.Layout.Features.Single(f => f.Kind == FeatureKind.Captive).Room;
            H.WalkTo(den);
            var before = W;
            var explored = before.FindPlace(mine.PlaceId).ExploredRooms.ToList();
            var dead = before.Creatures.Count(c => c.State == CreatureInstanceState.Dead);
            var phase13Places = before.Places.Count(p => !Phase14Places.All.Contains(p.DefinitionId));
            var newPlaces = before.Places.Where(p => Phase14Places.All.Contains(p.DefinitionId)).Select(p => p.PlaceId).OrderBy(x => x).ToList();
            Assert.That(newPlaces.Count, Is.InRange(3, 5));
            Assert.That(dead, Is.GreaterThan(0), "Something died on the way down.");

            // Back to a Phase 13 save (v15), then loaded by Phase 14.
            var doc = JObject.Parse(new JsonWorldSerializer().Serialize(before));
            Phase2Worlds.StripToV15(doc);
            Assert.That(((JArray)doc["world"]["places"]).Count, Is.EqualTo(phase13Places));
            File.WriteAllText(Path.Combine(Dir, "v15" + JsonWorldStore.Extension), doc.ToString());
            var load = S.Execute(new LoadWorldCommand("v15"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
            var w = W;
            Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(w.Places.Where(p => Phase14Places.All.Contains(p.DefinitionId)).Select(p => p.PlaceId).OrderBy(x => x), Is.EqualTo(newPlaces), "The same new places a fresh world of this seed has.");
            Assert.That(w.FindPlace(mine.PlaceId).ExploredRooms, Is.EqualTo(explored), "The mine as it was left.");
            Assert.That(w.Creatures.Count(c => c.State == CreatureInstanceState.Dead), Is.EqualTo(dead), "The dead stay dead.");
            Assert.That(w.Player.PlaceRoom, Is.EqualTo(den), "Still standing in the den.");
            Assert.That(w.Quests.Count(q => q.DefinitionId == QuestCatalog.LostBelow), Is.EqualTo(1));
            foreach (var id in new[] { QuestCatalog.FoundersRegister, QuestCatalog.FaneLantern, QuestCatalog.RoadTolls, QuestCatalog.Culverts })
                Assert.That(w.Quests.Count(q => q.DefinitionId == id), Is.LessThanOrEqualTo(1), id + " once at most.");
            Assert.That(w.Crimes, Is.Empty);
            Assert.That(w.NextCrimeNumber, Is.EqualTo(1));
            Assert.That(w.FindNpc(giver.NpcId), Is.Not.Null);
            Valid();

            // Loading again adds nothing (idempotent).
            var once = new JsonWorldSerializer().Serialize(W);
            Assert.That(new JsonWorldStore(Dir).Save(W, "again").IsSuccess, Is.True);
            var again = S.Execute(new LoadWorldCommand("again"));
            Assert.That(again.IsSuccess, Is.True, again.Message);
            Assert.That(W.Places.Count, Is.EqualTo(w.Places.Count));
            Assert.That(W.Quests.Count, Is.EqualTo(w.Quests.Count));
            Assert.That(JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"].ToString(), Is.EqualTo(JObject.Parse(once)["world"].ToString()));
        }

        [Test]
        public void EveryNewField_SurvivesSaveAndLoad_Exactly()
        {
            H.Hero();
            var works = Place(Phase14Places.Works);
            var bridge = works.Layout.Connections.FirstOrDefault(c => c.Kind == ConnectionKind.Bridge);
            H.Edit(w =>
            {
                var now = Realmkeeper.Simulation.Quests.QuestClock.Now(w);
                LastingConditions.Apply(w.Player.Rpg, ConditionType.Poisoned, "trap:poison_needle", "a needle", now, 60, ConditionRemedy.Restoration);
                LastingConditions.Apply(w.Player.Rpg, ConditionType.Exhaustion, "cold", "cold water", now, 0, ConditionRemedy.LongRestLevel, 0, 2);
                LastingConditions.Apply(w.Player.Rpg, ConditionType.Restrained, "trap:snare_net", "a net", now, 0, ConditionRemedy.Escape, 12);
                w.Player.Rpg.Conditions.Single(k => k.Type == ConditionType.Restrained).EscapeTries = 2;
                var p = w.FindPlace(works.PlaceId);
                if (bridge != null) p.Bridges[bridge.Id] = BridgeState.Destroyed;
                p.GroupOf(p.Layout.Groups.First().Id).Woken = true;
                var npc = w.Npcs.First(n => !n.IsDeceased);
                w.Crimes.Add(new CrimeRecordData
                {
                    CrimeId = "crime:1", Kind = CrimeKind.Theft, Status = CrimeStatus.Witnessed, At = now, SettlementId = npc.HomeSettlementId, VictimNpcId = npc.NpcId, Detail = "took a ring",
                    Taken = { ["gold_ring"] = 1 }, Coin = 4, Bounty = 30, DiscoveredAt = now,
                    Witnesses = { new CrimeWitnessData { NpcId = npc.NpcId, Recognized = true, Description = "you", How = "in the same room" } },
                });
                w.NextCrimeNumber = 2;
            });

            var serializer = new JsonWorldSerializer();
            var first = serializer.Serialize(W);
            var loaded = serializer.Deserialize(first);
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(JObject.Parse(serializer.Serialize(loaded.World))["world"].ToString(), Is.EqualTo(JObject.Parse(first)["world"].ToString()), "Exact round trip.");
            var w = loaded.World;
            Assert.That(w.Player.Rpg.Conditions.Count, Is.EqualTo(3));
            Assert.That(w.Player.Rpg.Conditions.Single(k => k.Type == ConditionType.Restrained).EscapeTries, Is.EqualTo(2));
            Assert.That(w.Player.Rpg.Conditions.Single(k => k.Type == ConditionType.Exhaustion).Level, Is.EqualTo(2));
            Assert.That(w.Crimes.Single().Witnesses.Single().Recognized, Is.True);
            Assert.That(w.Crimes.Single().Taken["gold_ring"], Is.EqualTo(1));
            Assert.That(w.NextCrimeNumber, Is.EqualTo(2));
            if (bridge != null) Assert.That(w.FindPlace(works.PlaceId).BridgeOf(w.FindPlace(works.PlaceId).Layout.Connection(bridge.Id)), Is.EqualTo(BridgeState.Destroyed));
            Assert.That(w.FindPlace(works.PlaceId).GroupOf(works.Layout.Groups.First().Id).Woken, Is.True);
        }

        [Test]
        public void AFightsDoorsAndLowCover_SurviveSaveAndLoad_MidFight()
        {
            H.Hero();
            var crypt = Enter(PlaceCatalog.Crypt);
            var g = crypt.Layout.Groups.First();
            H.Edit(w =>
            {
                var p = w.FindPlace(crypt.PlaceId);
                var r = p.Layout.Room(g.Room);
                w.Player.PlaceRoom = r.Id;
                w.Player.PlaceFloor = r.Floor;
                w.Player.Pose = PlaceRules.PoseAt(p, r.Floor, r.Rect.CenterX, r.Rect.CenterZ, 0f);
            });
            Ok(S.Dispatch(new PlaceActionCommand(g.Id, PlaceAction.Attack)));
            Assume.That(S.GetEncounter()?.Status, Is.EqualTo(EncounterStatus.Active));
            var serializer = new JsonWorldSerializer();
            var first = serializer.Serialize(W);
            var loaded = serializer.Deserialize(first);
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var e = loaded.World.ActiveEncounter;
            var original = W.ActiveEncounter;
            Assert.That(e.Doors.Count, Is.EqualTo(original.Doors.Count));
            Assert.That(e.LowCover.Count, Is.EqualTo(original.LowCover.Count));
            Assert.That(JObject.Parse(serializer.Serialize(loaded.World))["world"].ToString(), Is.EqualTo(JObject.Parse(first)["world"].ToString()));
        }
    }
}
