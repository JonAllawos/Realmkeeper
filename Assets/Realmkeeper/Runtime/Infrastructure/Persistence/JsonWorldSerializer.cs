using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Upgrades a raw save document from <see cref="SourceVersion"/> to SourceVersion + 1.
    /// Migrations operate on the JSON tree, so old DTO classes never need to be kept.
    /// </summary>
    public interface ISaveMigration
    {
        int SourceVersion { get; }
        void Migrate(JObject document);
    }

    /// <summary>
    /// Versioned JSON (de)serializer for world saves. Pure: no file IO, so it is tested
    /// directly. Never throws for bad input; every failure becomes a typed LoadResult.
    /// </summary>
    public sealed class JsonWorldSerializer
    {
        public const string FormatMarker = "realmkeeper.world";

        private readonly Dictionary<int, ISaveMigration> _migrations = new Dictionary<int, ISaveMigration>();
        private readonly JsonSerializerSettings _settings;
        private readonly Func<DateTime> _utcNow;

        public JsonWorldSerializer(IEnumerable<ISaveMigration> migrations = null, Func<DateTime> utcNow = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _settings = new JsonSerializerSettings
            {
                // camelCase property names; event data keys are stored exactly as written.
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true },
                },
                TypeNameHandling = TypeNameHandling.None, // never instantiate types named by the file
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 32,
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
            };

            // Built-in migrations are always available; callers may add more (e.g. tests for older layouts).
            var all = new List<ISaveMigration>(BuiltInMigrations);
            if (migrations != null) all.AddRange(migrations);
            foreach (var m in all)
            {
                if (m.SourceVersion >= WorldData.CurrentSchemaVersion)
                    throw new ArgumentException($"Migration from v{m.SourceVersion} targets a version beyond the current schema.");
                _migrations.Add(m.SourceVersion, m);
            }
        }

        /// <summary>Migrations shipped with this build, one per historical schema version.</summary>
        public static IReadOnlyList<ISaveMigration> BuiltInMigrations { get; } = new ISaveMigration[] { new MigrationV1ToV2(), new MigrationV2ToV3(), new MigrationV3ToV4(), new MigrationV4ToV5(), new MigrationV5ToV6(), new MigrationV6ToV7(), new MigrationV7ToV8(), new MigrationV8ToV9(), new MigrationV9ToV10() };

        /// <summary>First schema with regions, the player character and named residents.</summary>
        public const int FirstSchemaWithRegions = 3;

        /// <summary>First schema with first-class factions, relations, claims and goals.</summary>
        public const int FirstSchemaWithPolitics = 5;

        public const int FirstSchemaWithNpcEngine = 6;
        public const int FirstSchemaWithCommerce = 7;
        public const int FirstSchemaWithStructures = 10;

        public string Serialize(WorldData world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var validation = WorldValidator.Validate(world);
            if (!validation.IsValid) throw new InvalidOperationException("Refusing to save an invalid world: " + validation);

            var file = new WorldSaveFileDto
            {
                Format = FormatMarker,
                SchemaVersion = WorldData.CurrentSchemaVersion,
                SavedAtUtc = _utcNow().ToString("o", CultureInfo.InvariantCulture),
                World = WorldSaveMapper.ToDto(world),
            };
            return JsonConvert.SerializeObject(file, _settings);
        }

        public LoadResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return LoadResult.Failure(LoadStatus.Corrupt, "The save file is empty.");

            JObject document;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                {
                    document = JObject.Load(reader);
                    if (reader.Read()) return LoadResult.Failure(LoadStatus.Corrupt, "Unexpected content after the JSON document.");
                }
            }
            catch (JsonException ex)
            {
                return LoadResult.Failure(LoadStatus.Corrupt, "Malformed JSON: " + ex.Message);
            }

            if (!string.Equals((string)document["format"], FormatMarker, StringComparison.Ordinal))
                return LoadResult.Failure(LoadStatus.Corrupt, "Not a Realmkeeper world file (missing format marker).");

            var versionToken = document["schemaVersion"];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                return LoadResult.Failure(LoadStatus.Corrupt, "Missing or non-integer schemaVersion.");

            var version = (int)versionToken;
            var originalVersion = version;
            if (version > WorldData.CurrentSchemaVersion)
                return LoadResult.Failure(LoadStatus.UnsupportedVersion,
                    $"Save schema v{version} is newer than this build supports (v{WorldData.CurrentSchemaVersion}).");

            while (version < WorldData.CurrentSchemaVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                    return LoadResult.Failure(LoadStatus.UnsupportedVersion, $"No migration path from save schema v{version}.");
                try
                {
                    migration.Migrate(document);
                }
                catch (Exception ex)
                {
                    return LoadResult.Failure(LoadStatus.Corrupt, $"Migration from v{version} failed: {ex.Message}");
                }

                version++;
                document["schemaVersion"] = version;
            }

            WorldSaveFileDto file;
            try
            {
                file = document.ToObject<WorldSaveFileDto>(JsonSerializer.Create(_settings));
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                return LoadResult.Failure(LoadStatus.Corrupt, "Save structure is unreadable: " + ex.Message);
            }

            WorldData world;
            try
            {
                world = WorldSaveMapper.FromDto(file?.World);
            }
            catch (SaveDataException ex)
            {
                return LoadResult.Failure(LoadStatus.Invalid, ex.Message);
            }

            // Saves older than v3 gain their region, player and named residents (deterministic, additive).
            if (originalVersion < FirstSchemaWithRegions)
            {
                try
                {
                    var defaults = new Generation.RegionGeneratorSettings();
                    Generation.WorldPopulationInitializer.EnsureAll(world, defaults.Width, defaults.Depth, new Randomness.Xoshiro256RandomFactory());
                }
                catch (InvalidOperationException ex)
                {
                    return LoadResult.Failure(LoadStatus.Invalid, "The save could not be upgraded to regions: " + ex.Message);
                }
            }

            // Saves older than v5 gain their political structure (deterministic, additive, once).
            if (originalVersion < FirstSchemaWithPolitics)
            {
                Generation.PoliticalInitializer.Ensure(world, new Randomness.Xoshiro256RandomFactory());
            }

            // Saves older than v6 gain the NPC engine's state: locations, identity, inner life (deterministic,
            // additive, once: people already initialised are never touched again; no history is invented).
            if (originalVersion < FirstSchemaWithNpcEngine)
            {
                Simulation.Npcs.NpcGenesis.EnsurePeople(world);
            }

            // Saves older than v7 gain shops (kept by existing residents, stocked from their own settlement's
            // stores) and the player's starting belongings, once.
            if (originalVersion < FirstSchemaWithCommerce)
            {
                Simulation.Commerce.CommerceGenesis.EnsureShops(world);
                Simulation.Commerce.CommerceGenesis.EnsurePlayer(world);
            }

            // Saves older than v10 gain their settlements' structures, built on their existing places and shops, once.
            if (originalVersion < FirstSchemaWithStructures)
            {
                Simulation.Commerce.StructureGenesis.Ensure(world);
            }

            var validation = WorldValidator.Validate(world);
            return validation.IsValid ? LoadResult.Success(world) : LoadResult.Failure(LoadStatus.Invalid, validation.Errors);
        }
    }
}
