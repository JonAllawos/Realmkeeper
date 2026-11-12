using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Local file store: one JSON file per slot in a root directory supplied by the caller
    /// (in Unity, Application.persistentDataPath/Saves). Writes are atomic: the new file is
    /// fully written to a temporary file before it replaces the old one.
    /// </summary>
    public sealed class JsonWorldStore : IWorldStore
    {
        public const string Extension = ".rkworld.json";
        public const int MaxFileBytes = 32 * 1024 * 1024;

        private readonly string _root;
        private readonly JsonWorldSerializer _serializer;

        public JsonWorldStore(string rootDirectory, JsonWorldSerializer serializer = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            _root = Path.GetFullPath(rootDirectory);
            _serializer = serializer ?? new JsonWorldSerializer();
        }

        public string RootDirectory => _root;

        /// <summary>Slot names: 1-48 chars of letters, digits, '-' or '_'. Rejects path tricks. One rule: <see cref="SaveSlot"/>.</summary>
        public static bool IsValidSlotName(string slot) => SaveSlot.IsValidName(slot);

        public string PathFor(string slot)
        {
            if (!IsValidSlotName(slot)) throw new ArgumentException($"Invalid save slot name '{slot}'.", nameof(slot));
            return Path.Combine(_root, slot + Extension);
        }

        public bool Exists(string slotName) => IsValidSlotName(slotName) && File.Exists(PathFor(slotName));

        public SaveResult Save(WorldData world, string slotName)
        {
            if (!IsValidSlotName(slotName)) return SaveResult.Failure($"Invalid save slot name '{slotName}'.");

            string json;
            try
            {
                json = _serializer.Serialize(world);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return SaveResult.Failure(ex.Message);
            }

            var path = PathFor(slotName);
            var temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_root);
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return SaveResult.Success(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temp);
                return SaveResult.Failure("Could not write save file: " + ex.Message);
            }
        }

        public LoadResult Load(string slotName)
        {
            if (!IsValidSlotName(slotName)) return LoadResult.Failure(LoadStatus.NotFound, $"Invalid save slot name '{slotName}'.");
            var path = PathFor(slotName);
            if (!File.Exists(path)) return LoadResult.Failure(LoadStatus.NotFound, $"No save file at {path}.");

            string json;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaxFileBytes) return LoadResult.Failure(LoadStatus.Corrupt, "Save file is unreasonably large.");
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return LoadResult.Failure(LoadStatus.Corrupt, "Could not read save file: " + ex.Message);
            }

            return _serializer.Deserialize(json);
        }

        /// <summary>
        /// Phase 19: the saved worlds, newest first, for the world-selection screen. Only each file's header is read
        /// (format, schema, save time, world name, seed and date sit in the first few hundred bytes), never the world.
        /// A file whose header cannot be read is listed with <see cref="SaveSummary.Readable"/> false.
        /// </summary>
        public List<SaveSummary> ListSaves()
        {
            var list = new List<SaveSummary>();
            if (!Directory.Exists(_root)) return list;
            foreach (var path in Directory.GetFiles(_root, "*" + Extension))
            {
                var slot = Path.GetFileName(path);
                slot = slot.Substring(0, slot.Length - Extension.Length);
                if (!IsValidSlotName(slot)) continue;
                list.Add(ReadSummary(slot, path));
            }

            list.Sort((a, b) => b.LastWriteUtc.CompareTo(a.LastWriteUtc));
            return list;
        }

        /// <summary>Phase 19: deletes a saved world. The caller confirms with the player first. Fails safely.</summary>
        public SaveResult Delete(string slotName)
        {
            if (!IsValidSlotName(slotName)) return SaveResult.Failure($"Invalid save slot name '{slotName}'.");
            var path = PathFor(slotName);
            if (!File.Exists(path)) return SaveResult.Failure($"There is no saved world '{slotName}'.");
            try
            {
                File.Delete(path);
                return SaveResult.Success(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return SaveResult.Failure("Could not delete the save: " + ex.Message);
            }
        }

        private static readonly Regex HeaderField = new Regex("\"(schemaVersion|savedAtUtc|worldName|seed|currentSimulationDate)\"\\s*:\\s*(\"([^\"]*)\"|(-?[0-9]+))", RegexOptions.CultureInvariant);

        private static SaveSummary ReadSummary(string slot, string path)
        {
            var summary = new SaveSummary { Slot = slot };
            try
            {
                var info = new FileInfo(path);
                summary.Bytes = info.Length;
                summary.LastWriteUtc = info.LastWriteTimeUtc;
                var buffer = new char[4096];
                int read;
                using (var reader = new StreamReader(path, Encoding.UTF8)) read = reader.ReadBlock(buffer, 0, buffer.Length);
                var head = new string(buffer, 0, read);
                foreach (Match m in HeaderField.Matches(head))
                {
                    var value = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                    switch (m.Groups[1].Value)
                    {
                        case "schemaVersion": if (int.TryParse(value, out var v)) summary.SchemaVersion = v; break;
                        case "savedAtUtc": summary.SavedAtUtc = value; break;
                        case "worldName": if (summary.WorldName == null) summary.WorldName = value; break;
                        case "seed": if (summary.Seed == null) summary.Seed = value; break;
                        case "currentSimulationDate": if (summary.Date == null) summary.Date = value; break;
                    }
                }

                summary.Readable = summary.WorldName != null;
                summary.Compatible = summary.SchemaVersion > 0 && summary.SchemaVersion <= WorldData.CurrentSchemaVersion;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                summary.Readable = false;
            }

            return summary;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort cleanup of a temporary file.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
