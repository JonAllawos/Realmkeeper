using System;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application.Ports
{
    public sealed class WorldGenerationRequest
    {
        public WorldGenerationRequest(string worldName, long seed, WorldSettings settings, WorldId worldId = default)
        {
            WorldName = worldName;
            Seed = seed;
            Settings = settings;
            WorldId = worldId;
        }

        public string WorldName { get; }
        public long Seed { get; }
        public WorldSettings Settings { get; }

        /// <summary>Optional. When empty a new random world identity is assigned.</summary>
        public WorldId WorldId { get; }

        /// <summary>
        /// False generates the world as schema v6 did: no shops and no starting belongings. Only for building
        /// faithful pre-Phase-5 saves in migration tests; the game always generates with commerce.
        /// </summary>
        public bool IncludeCommerce { get; set; } = true;
    }

    /// <summary>Generation output: authoritative world data plus the derived geography it sits on.</summary>
    public sealed class GeneratedWorld
    {
        public GeneratedWorld(WorldData world, RegionMap map)
        {
            World = world;
            Map = map;
        }

        public WorldData World { get; }
        public RegionMap Map { get; }
    }

    /// <summary>Thrown by generators when a seed cannot produce a valid world.</summary>
    public sealed class WorldGenerationException : Exception
    {
        public WorldGenerationException(string message) : base(message) { }
    }

    /// <summary>Port implemented by Realmkeeper.Infrastructure.</summary>
    public interface IWorldGenerator
    {
        /// <summary>Version stamped into generated worlds. Bump whenever output for a given seed changes.</summary>
        int GeneratorVersion { get; }

        bool SupportsVersion(int generatorVersion);

        GeneratedWorld Generate(WorldGenerationRequest request);

        /// <summary>Rebuilds the geography of an existing world from its seed and generator version.</summary>
        RegionMap RegenerateRegion(WorldData world);

        /// <summary>
        /// Fills in economy data (production, storage, trade routes) that an older save lacks,
        /// derived from the regenerated geography. Leaves already-initialized settlements alone.
        /// </summary>
        void InitializeEconomy(WorldData world, RegionMap map);
    }
}
