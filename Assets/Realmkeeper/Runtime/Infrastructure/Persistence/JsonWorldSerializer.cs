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
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                TypeNameHandling = TypeNameHandling.None, // never instantiate types named by the file
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 32,
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
            };

            if (migrations == null) return;
            foreach (var m in migrations)
            {
                if (m.SourceVersion >= WorldData.CurrentSchemaVersion)
                    throw new ArgumentException($"Migration from v{m.SourceVersion} targets a version beyond the current schema.");
                _migrations.Add(m.SourceVersion, m);
            }
        }

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

            var validation = WorldValidator.Validate(world);
            return validation.IsValid ? LoadResult.Success(world) : LoadResult.Failure(LoadStatus.Invalid, validation.Errors);
        }
    }
}
