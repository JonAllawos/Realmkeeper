using System;
using System.IO;
using System.Text;
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

        /// <summary>Slot names: 1-48 chars of letters, digits, '-' or '_'. Rejects path tricks.</summary>
        public static bool IsValidSlotName(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length > 48) return false;
            foreach (var c in slot)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return false;
            }

            return true;
        }

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
