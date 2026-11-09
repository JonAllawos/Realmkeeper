using System;
using System.IO;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>Generated worlds and sessions with the production rules (as the game runs them).</summary>
    internal static class Phase2Worlds
    {
        public static readonly WorldId FixedId = new WorldId(new Guid("0b5cf6a2-1d8e-4f3e-9a5b-6c7d8e9f0a2b"));

        public static WorldData Generated(long seed) => Generate(seed).World;

        public static GeneratedWorld Generate(long seed)
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            return new RegionGenerator(new Xoshiro256RandomFactory()).Generate(new WorldGenerationRequest("Phaseland", seed, settings, FixedId));
        }

        public static string TempDirectory() => Path.Combine(Path.GetTempPath(), "realmkeeper-phase2-" + Guid.NewGuid().ToString("N"));

        public static WorldSession Session(string directory, long seed = 123456789)
        {
            var session = RealmkeeperComposition.CreateSession(directory);
            var result = session.Execute(new CreateWorldCommand("Phaseland", seed));
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
            return session;
        }
    }
}
