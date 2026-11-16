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
    /// <para>
    /// M1.1 (ADR-0123): the temporary file is read back and compared before it replaces anything, and the replaced file is
    /// kept as <c>&lt;slot&gt;.rkworld.json.bak</c>, so a slot always has a good save or a good backup. A stale temporary
    /// file (an interrupted save) is removed at start-up. File operations go through <see cref="ISaveFileSystem"/>.
    /// </para>
    /// </summary>
    public sealed class JsonWorldStore : IWorldStore
    {
        public const string Extension = ".rkworld.json";
        public const string BackupSuffix = ".bak";
        public const string TempSuffix = ".tmp";
        public const string DamagedSuffix = ".damaged";
        public const string ThumbnailExtension = ".jpg";
        public const int MaxFileBytes = 32 * 1024 * 1024;

        private readonly string _root;
        private readonly JsonWorldSerializer _serializer;
        private readonly ISaveFileSystem _files;

        public JsonWorldStore(string rootDirectory, JsonWorldSerializer serializer = null, ISaveFileSystem files = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            _root = Path.GetFullPath(rootDirectory);
            _serializer = serializer ?? new JsonWorldSerializer();
            _files = files ?? SaveFileSystem.Instance;
        }

        public string RootDirectory => _root;

        /// <summary>Slot names: 1-48 chars of letters, digits, '-' or '_'. Rejects path tricks. One rule: <see cref="SaveSlot"/>.</summary>
        public static bool IsValidSlotName(string slot) => SaveSlot.IsValidName(slot);

        public string PathFor(string slot)
        {
            if (!IsValidSlotName(slot)) throw new ArgumentException($"Invalid save slot name '{slot}'.", nameof(slot));
            return Path.Combine(_root, slot + Extension);
        }

        public string BackupPathFor(string slot) => PathFor(slot) + BackupSuffix;

        public string ThumbnailPathFor(string slot)
        {
            if (!IsValidSlotName(slot)) throw new ArgumentException($"Invalid save slot name '{slot}'.", nameof(slot));
            return Path.Combine(_root, slot + ThumbnailExtension);
        }

        public bool Exists(string slotName) => IsValidSlotName(slotName) && File.Exists(PathFor(slotName));

        public bool HasBackup(string slotName) => IsValidSlotName(slotName) && File.Exists(BackupPathFor(slotName));

        public SaveResult Save(WorldData world, string slotName)
        {
            if (!IsValidSlotName(slotName)) return SaveResult.Failure($"Invalid save slot name '{slotName}'.");

            string json;
            try
            {
                json = _serializer.Serialize(world, SlotKindText(slotName));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return SaveResult.Failure(ex.Message);
            }

            var path = PathFor(slotName);
            var temp = path + TempSuffix;
            try
            {
                Directory.CreateDirectory(_root);
                _files.WriteAllText(temp, json);

                // M1.1: never let an incomplete file replace a good one.
                if (!string.Equals(_files.ReadAllText(temp), json, StringComparison.Ordinal))
                {
                    TryDelete(temp);
                    return SaveResult.Failure("The save could not be written completely (the file read back differently). Your previous save is unchanged.");
                }

                if (File.Exists(path)) _files.Replace(temp, path, path + BackupSuffix);
                else _files.Move(temp, path);
                return SaveResult.Success(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temp);
                return SaveResult.Failure("Could not write save file: " + ex.Message + " Your previous save is unchanged.");
            }
        }

        public LoadResult Load(string slotName)
        {
            if (!IsValidSlotName(slotName)) return LoadResult.Failure(LoadStatus.NotFound, $"Invalid save slot name '{slotName}'.");
            return LoadFile(PathFor(slotName));
        }

        /// <summary>M1.1: loads a slot's backup without changing any file.</summary>
        public LoadResult LoadBackup(string slotName)
        {
            if (!IsValidSlotName(slotName)) return LoadResult.Failure(LoadStatus.NotFound, $"Invalid save slot name '{slotName}'.");
            return LoadFile(BackupPathFor(slotName));
        }

        private LoadResult LoadFile(string path)
        {
            if (!File.Exists(path)) return LoadResult.Failure(LoadStatus.NotFound, $"No save file at {path}.");

            string json;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaxFileBytes) return LoadResult.Failure(LoadStatus.Corrupt, "Save file is unreasonably large.");
                json = _files.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return LoadResult.Failure(LoadStatus.Corrupt, "Could not read save file: " + ex.Message);
            }

            // M1.1: a damaged body is caught before the world is parsed.
            var problem = SaveEnvelope.Verify(json);
            if (problem != null) return LoadResult.Failure(LoadStatus.Corrupt, problem);
            return _serializer.Deserialize(json);
        }

        /// <summary>
        /// M1.1: puts a slot's backup back in place, only after the backup has loaded successfully. The damaged file is kept
        /// beside it as <c>.damaged</c> (nothing is deleted). The caller asks the player first.
        /// </summary>
        public SaveResult RestoreBackup(string slotName)
        {
            if (!IsValidSlotName(slotName)) return SaveResult.Failure($"Invalid save slot name '{slotName}'.");
            var backup = BackupPathFor(slotName);
            if (!File.Exists(backup)) return SaveResult.Failure("This save has no backup.");
            var check = LoadBackup(slotName);
            if (!check.IsSuccess) return SaveResult.Failure("The backup could not be loaded either: " + string.Join(" ", check.Errors));
            var path = PathFor(slotName);
            try
            {
                if (File.Exists(path)) File.Copy(path, path + DamagedSuffix, true);
                File.Copy(backup, path, true);
                return SaveResult.Success(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return SaveResult.Failure("Could not restore the backup: " + ex.Message);
            }
        }

        /// <summary>
        /// M1.1: removes temporary files left by an interrupted save. The saves and backups beside them are untouched.
        /// Returns the files removed.
        /// </summary>
        public List<string> CleanStaleTemps()
        {
            var removed = new List<string>();
            if (!Directory.Exists(_root)) return removed;
            foreach (var path in Directory.GetFiles(_root, "*" + Extension + TempSuffix))
            {
                if (!path.EndsWith(Extension + TempSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (TryDelete(path)) removed.Add(Path.GetFileName(path));
            }

            return removed;
        }

        /// <summary>M1.1: writes a slot's thumbnail (JPEG bytes). Best effort: a failure never affects the save.</summary>
        public bool WriteThumbnail(string slotName, byte[] jpeg)
        {
            if (!IsValidSlotName(slotName) || jpeg == null || jpeg.Length == 0) return false;
            var path = ThumbnailPathFor(slotName);
            var temp = path + TempSuffix;
            try
            {
                Directory.CreateDirectory(_root);
                File.WriteAllBytes(temp, jpeg);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temp);
                return false;
            }
        }

        /// <summary>
        /// Phase 19, extended in M1.1: the saves, most recently played first. Only each file's header is read (format,
        /// schema, save time, the v28 summary, world name, seed and date sit in the first few kilobytes); with
        /// <paramref name="verify"/>, a v28 file is also checked against its checksum and each backup is inspected.
        /// </summary>
        public List<SaveSummary> ListSaves(bool verify = true)
        {
            var list = new List<SaveSummary>();
            if (!Directory.Exists(_root)) return list;
            foreach (var path in Directory.GetFiles(_root, "*" + Extension))
            {
                if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;
                var slot = Path.GetFileName(path);
                slot = slot.Substring(0, slot.Length - Extension.Length);
                if (!IsValidSlotName(slot)) continue;
                list.Add(Inspect(slot, verify));
            }

            list.Sort((a, b) => b.LastPlayedUtc.CompareTo(a.LastPlayedUtc));
            return list;
        }

        /// <summary>
        /// M1.1: the saves Continue may load, most recently played first (the summary's playedAtUtc, not the file time):
        /// Ready and Will-be-updated saves in the manual and autosave slots, plus older and developer saves when
        /// <paramref name="includeOtherSlots"/>. Too-new and Damaged saves are left out.
        /// </summary>
        public List<SaveSummary> ContinueOrder(bool includeOtherSlots)
        {
            var list = ListSaves(true).FindAll(s => s.CanLoad && (includeOtherSlots || s.Kind != SaveSlotKind.Other));
            list.Sort((a, b) => b.LastPlayedUtc.CompareTo(a.LastPlayedUtc));
            return list;
        }

        /// <summary>M1.1: one slot's summary, or null when it has no save.</summary>
        public SaveSummary Inspect(string slotName, bool verify = true)
        {
            if (!Exists(slotName)) return null;
            var summary = ReadSummary(slotName, PathFor(slotName), verify);
            var thumb = ThumbnailPathFor(slotName);
            summary.ThumbnailPath = File.Exists(thumb) ? thumb : null;
            if (verify && HasBackup(slotName))
            {
                var bak = ReadSummary(slotName, BackupPathFor(slotName), true);
                summary.HasGoodBackup = bak.CanLoad;
            }

            return summary;
        }

        /// <summary>Phase 19, extended in M1.1: deletes a save with its backup and thumbnail. The caller confirms with the player first. Fails safely.</summary>
        public SaveResult Delete(string slotName)
        {
            if (!IsValidSlotName(slotName)) return SaveResult.Failure($"Invalid save slot name '{slotName}'.");
            var path = PathFor(slotName);
            if (!File.Exists(path)) return SaveResult.Failure($"There is no saved world '{slotName}'.");
            try
            {
                File.Delete(path);
                TryDelete(path + BackupSuffix);
                TryDelete(ThumbnailPathFor(slotName));
                return SaveResult.Success(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return SaveResult.Failure("Could not delete the save: " + ex.Message);
            }
        }

        private static string SlotKindText(string slot)
        {
            switch (SaveSlots.KindOf(slot))
            {
                case SaveSlotKind.Manual: return "manual";
                case SaveSlotKind.Auto: return "auto";
                default: return "other";
            }
        }

        private static readonly Regex HeaderField = new Regex("\"(schemaVersion|savedAtUtc|worldName|seed|currentSimulationDate)\"\\s*:\\s*(\"([^\"]*)\"|(-?[0-9]+))", RegexOptions.CultureInvariant);

        private SaveSummary ReadSummary(string slot, string path, bool verify)
        {
            var summary = new SaveSummary { Slot = slot };
            try
            {
                var info = new FileInfo(path);
                summary.Bytes = info.Length;
                summary.LastWriteUtc = info.LastWriteTimeUtc;
                var buffer = new char[8192];
                int read;
                using (var reader = new StreamReader(path, Encoding.UTF8)) read = reader.ReadBlock(buffer, 0, buffer.Length);
                var head = new string(buffer, 0, read);
                foreach (Match m in HeaderField.Matches(head))
                {
                    var value = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                    switch (m.Groups[1].Value)
                    {
                        case "schemaVersion": if (summary.SchemaVersion == 0 && int.TryParse(value, out var v)) summary.SchemaVersion = v; break;
                        case "savedAtUtc": if (summary.SavedAtUtc == null) summary.SavedAtUtc = value; break;
                        case "worldName": if (summary.WorldName == null) summary.WorldName = value; break;
                        case "seed": if (summary.Seed == null) summary.Seed = value; break;
                        case "currentSimulationDate": if (summary.Date == null) summary.Date = value; break;
                    }
                }

                var header = SaveEnvelope.ReadSummary(head, _serializer.Settings);
                if (header != null)
                {
                    if (!string.IsNullOrEmpty(header.WorldName)) summary.WorldName = header.WorldName;
                    summary.CharacterName = header.CharacterName;
                    summary.ClassName = header.ClassName;
                    summary.Level = header.Level;
                    summary.LocationText = header.LocationText;
                    summary.MinuteOfDay = header.MinuteOfDay;
                    if (!string.IsNullOrEmpty(header.Date)) summary.Date = header.Date;
                    summary.CampaignChapter = header.CampaignChapter;
                    summary.PlaytimeSeconds = header.PlaytimeSeconds;
                    summary.PlayedAtUtc = header.PlayedAtUtc;
                }

                summary.Readable = summary.WorldName != null;
                summary.Compatible = summary.SchemaVersion > 0 && summary.SchemaVersion <= WorldData.CurrentSchemaVersion;
                if (!summary.Readable || summary.SchemaVersion <= 0)
                {
                    summary.State = SaveState.Damaged;
                    summary.Problem = "The file's header could not be read.";
                }
                else if (!summary.Compatible) summary.State = SaveState.TooNew;
                else
                {
                    summary.State = summary.SchemaVersion < WorldData.CurrentSchemaVersion ? SaveState.NeedsUpdate : SaveState.Ready;
                    if (summary.SchemaVersion >= SaveEnvelope.FirstSchemaWithIntegrity)
                    {
                        if (verify)
                        {
                            if (info.Length > MaxFileBytes)
                            {
                                summary.State = SaveState.Damaged;
                                summary.Problem = "Save file is unreasonably large.";
                            }
                            else
                            {
                                var problem = SaveEnvelope.Verify(_files.ReadAllText(path));
                                if (problem != null)
                                {
                                    summary.State = SaveState.Damaged;
                                    summary.Problem = problem;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                summary.Readable = false;
                summary.State = SaveState.Damaged;
                summary.Problem = "The file could not be read: " + ex.Message;
            }

            return summary;
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false; // Best effort cleanup of a temporary file.
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
