using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>The session and helpers Phase 14's place tests share (the Phase 13 helper does the walking).</summary>
    public abstract class Phase14Fixture
    {
        protected string Dir;
        protected WorldSession S;
        protected PlaceSystemTests H;

        [SetUp]
        public void SetUp()
        {
            Dir = Phase2Worlds.TempDirectory();
            S = Phase2Worlds.Session(Dir);
            ExplorationModule.Register(S);
            H = new PlaceSystemTests();
            typeof(PlaceSystemTests).GetField("_dir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(H, Dir);
            typeof(PlaceSystemTests).GetField("_s", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(H, S);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        }

        protected WorldData W => S.CurrentWorldSnapshotForTests();

        protected static void Ok(CommandResult r) => PlaceSystemTests.Ok(r);

        protected string WorldJson() => JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"].ToString();

        protected PlaceData Place(string def) => W.Places.Single(p => p.DefinitionId == def);

        /// <summary>Into a place the player is told of, from the open country, with the hero made.</summary>
        protected PlaceData Enter(string def)
        {
            H.Edit(w => w.Places.Single(p => p.DefinitionId == def).Discovery = PlaceDiscovery.Discovered);
            if (W.Player.IsInSettlement) Ok(S.Dispatch(new ExitSettlementCommand()));
            Ok(S.Dispatch(new EnterPlaceCommand(Place(def).PlaceId)));
            return Place(def);
        }

        /// <summary>Back to the state before a refused command: nothing in the world changed.</summary>
        protected void Refused<T>(T c, string why = null) where T : ICommand
        {
            var before = WorldJson();
            var r = S.Dispatch(c);
            Assert.That(r.IsSuccess, Is.False, "Should have been refused: " + r.Message);
            if (why != null) Assert.That(r.Message, Does.Contain(why));
            Assert.That(WorldJson(), Is.EqualTo(before), "A refused action changes nothing.");
        }

        protected long Now => Realmkeeper.Simulation.Quests.QuestClock.Now(W);

        protected void Valid() => H.Valid();

        protected void Hours(int h)
        {
            var r = S.Execute(new AdvanceTimeCommand(h * 60));
            Assert.That(r.IsSuccess, Is.True, r.Message);
        }
    }
}
