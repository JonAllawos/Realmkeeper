using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 16: a session on a connected world (six regions, new-game settings), shared by the world, travel, caravan
    /// and event tests. The world is made once per fixture class and copied for each test through a save, so every test
    /// starts from the same state.
    /// </summary>
    public abstract class Phase16Fixture
    {
        public const long Seed = 123456789;
        private static readonly Dictionary<long, string> Saved = new Dictionary<long, string>();

        protected string Dir;
        protected WorldSession S;

        protected virtual long WorldSeed => Seed;

        [SetUp]
        public void SetUp()
        {
            Dir = Phase2Worlds.TempDirectory();
            Directory.CreateDirectory(Dir);
            S = RealmkeeperComposition.CreateSession(Dir);
            ExplorationModule.Register(S);
            if (!Saved.TryGetValue(WorldSeed, out var json))
            {
                var made = S.Execute(new CreateWorldCommand("Atlasland", WorldSeed, WorldSession.NewGameSettings()));
                Assert.That(made.IsSuccess, Is.True, made.Message + " " + string.Join("; ", made.Errors));
                json = new JsonWorldSerializer().Serialize(S.CurrentWorldSnapshotForTests());
                Saved[WorldSeed] = json;
            }

            File.WriteAllText(new JsonWorldStore(Dir).PathFor("start"), json);
            var load = S.Execute(new LoadWorldCommand("start"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        }

        protected WorldData W => S.CurrentWorldSnapshotForTests();

        protected static void Ok(CommandResult r) => Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));

        protected void Valid()
        {
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        /// <summary>Changes the world through a save and a reload (the tests' licence; every check still runs on load).</summary>
        protected void Edit(System.Action<WorldData> change)
        {
            var w = W;
            change(w);
            var saved = new JsonWorldStore(Dir).Save(w, "edit");
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            Ok(S.Execute(new LoadWorldCommand("edit")));
        }

        protected string WorldJson() => JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"].ToString();

        protected RegionData Home => W.Regions[0];
        protected IEnumerable<SettlementData> In(RegionData r) => W.Settlements.Where(s => s.RegionId == r.RegionId);
    }
}
