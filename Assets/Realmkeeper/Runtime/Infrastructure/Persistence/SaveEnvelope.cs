using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// M1.1 (ADR-0123, schema v28): what a save slot shows, kept in the file's envelope so the Load Game screen reads it
    /// without parsing the world. Written on one line near the top of the file.
    /// </summary>
    public sealed class SaveHeaderSummaryDto
    {
        public string WorldName { get; set; }
        public string CharacterName { get; set; }
        public string ClassName { get; set; }
        public int Level { get; set; }
        public string LocationText { get; set; }
        public int MinuteOfDay { get; set; }
        public string Date { get; set; }
        public string CampaignChapter { get; set; }
        public double PlaytimeSeconds { get; set; }
        public string PlayedAtUtc { get; set; }

        /// <summary>"manual", "auto" or "other".</summary>
        public string SlotKind { get; set; }
    }

    /// <summary>
    /// M1.1 (ADR-0123): the v28 file layout and its checksum. The envelope is written by hand so the world's text sits,
    /// byte for byte, after a fixed <c>"world": </c> line; its SHA-256 is stored in the envelope and checked before the
    /// world is parsed, so a truncated or edited body is detected at once.
    /// <code>
    /// {
    ///   "format": "realmkeeper.world",
    ///   "schemaVersion": 28,
    ///   "savedAtUtc": "…",
    ///   "summary": { … one line … },
    ///   "integrity": {"algorithm":"sha256","world":"…64 hex…"},
    ///   "world": { … }
    /// }
    /// </code>
    /// </summary>
    public static class SaveEnvelope
    {
        public const int FirstSchemaWithIntegrity = 28;
        public const string Algorithm = "sha256";

        private const string WorldMarker = "\n  \"world\": ";
        private static readonly Regex IntegrityField = new Regex("\"integrity\"\\s*:\\s*\\{\\s*\"algorithm\"\\s*:\\s*\"sha256\"\\s*,\\s*\"world\"\\s*:\\s*\"([0-9a-f]{64})\"\\s*\\}", RegexOptions.CultureInvariant);
        private static readonly Regex SchemaVersionField = new Regex("\"schemaVersion\"\\s*:\\s*(-?[0-9]+)", RegexOptions.CultureInvariant);
        private static readonly Regex SummaryLine = new Regex("\"summary\"\\s*:\\s*(\\{[^\\n]*\\})\\s*,?\\s*\\n", RegexOptions.CultureInvariant);

        public static string Compose(string format, int schemaVersion, string savedAtUtc, SaveHeaderSummaryDto summary, JsonSerializerSettings settings, string worldJson)
        {
            var compact = new JsonSerializerSettings
            {
                ContractResolver = settings.ContractResolver,
                Formatting = Formatting.None,
                Culture = CultureInfo.InvariantCulture,
                NullValueHandling = NullValueHandling.Include,
            };
            var sb = new StringBuilder(worldJson.Length + 1024);
            sb.Append("{\n");
            sb.Append("  \"format\": ").Append(JsonConvert.ToString(format)).Append(",\n");
            sb.Append("  \"schemaVersion\": ").Append(schemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"savedAtUtc\": ").Append(JsonConvert.ToString(savedAtUtc)).Append(",\n");
            sb.Append("  \"summary\": ").Append(JsonConvert.SerializeObject(summary, compact)).Append(",\n");
            sb.Append("  \"integrity\": {\"algorithm\":\"").Append(Algorithm).Append("\",\"world\":\"").Append(Sha256Hex(worldJson)).Append("\"},");
            sb.Append(WorldMarker).Append(worldJson).Append("\n}");
            return sb.ToString();
        }

        /// <summary>The world's text exactly as written, or null when the layout is not the v28 one.</summary>
        public static string ExtractWorld(string text)
        {
            if (text == null) return null;
            var at = text.IndexOf(WorldMarker, StringComparison.Ordinal);
            if (at < 0) return null;
            var start = at + WorldMarker.Length;
            var end = text.LastIndexOf('}');
            if (end <= start) return null;
            var world = text.Substring(start, end - start).TrimEnd();
            return world.Length == 0 ? null : world;
        }

        /// <summary>The checksum the envelope declares (in the first few kilobytes), or null.</summary>
        public static string DeclaredChecksum(string head)
        {
            if (head == null) return null;
            var m = IntegrityField.Match(head);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Checks a file's body against the checksum its envelope declares. Null when it matches or when none is declared
        /// (saves from before v28, and files rewritten by hand without one), otherwise why not. The checksum catches
        /// accidental damage (a cut-off or altered file); it is not a lock against deliberate editing.
        /// </summary>
        public static string Verify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "The save file is empty.";
            var head = text.Length > 8192 ? text.Substring(0, 8192) : text;
            var version = SchemaVersionField.Match(head);
            if (version.Success && int.TryParse(version.Groups[1].Value, out var v) && v < FirstSchemaWithIntegrity) return null; // only v28+ files carry a checksum
            var declared = DeclaredChecksum(head);
            if (declared == null) return null;
            var world = ExtractWorld(text);
            if (world == null) return "The save is incomplete: its world could not be found.";
            return string.Equals(Sha256Hex(world), declared, StringComparison.Ordinal) ? null : "The save's checksum does not match: the file is damaged or was changed outside the game.";
        }

        /// <summary>The summary line of a v28 header, or null (older files, or one that cannot be read).</summary>
        public static SaveHeaderSummaryDto ReadSummary(string head, JsonSerializerSettings settings)
        {
            if (head == null) return null;
            var m = SummaryLine.Match(head);
            if (!m.Success) return null;
            try
            {
                return JObject.Parse(m.Groups[1].Value).ToObject<SaveHeaderSummaryDto>(JsonSerializer.Create(settings));
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                return null;
            }
        }

        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>What the slot card shows, from the world as it is saved.</summary>
        public static SaveHeaderSummaryDto Summarize(WorldData world, string slotKind, DateTime playedAtUtc)
        {
            var p = world.Player;
            var cls = p?.Rpg != null && !string.IsNullOrEmpty(p.Rpg.ClassId) ? RpgCatalog.FindClass(p.Rpg.ClassId)?.Name ?? p.Rpg.ClassId : null;
            return new SaveHeaderSummaryDto
            {
                WorldName = world.WorldName,
                CharacterName = p?.Name,
                ClassName = cls,
                Level = p?.Rpg != null ? Math.Max(1, p.Rpg.Level) : 1,
                LocationText = LocationText(world),
                MinuteOfDay = world.MinuteOfDay,
                Date = world.CurrentSimulationDate.ToIsoString(),
                CampaignChapter = CampaignChapter(world),
                PlaytimeSeconds = world.PlaytimeSeconds,
                PlayedAtUtc = playedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                SlotKind = slotKind ?? "other",
            };
        }

        /// <summary>Where the character is, as a player would say it: the place, the settlement, or the region's country.</summary>
        public static string LocationText(WorldData world)
        {
            var p = world.Player;
            if (p == null) return null;
            var settlement = p.SettlementId.IsEmpty ? null : world.FindSettlement(p.SettlementId);
            string RegionName(Realmkeeper.Domain.Identifiers.RegionId id)
            {
                if (world.Regions != null) foreach (var r in world.Regions) if (r != null && r.RegionId == id) return r.Name;
                return null;
            }

            switch (p.LocationKind)
            {
                case CharacterLocationKind.Place:
                {
                    PlaceData place = world.FindPlace(p.PlaceId);
                    var name = place?.Name ?? "A place";
                    return settlement != null ? name + ", " + settlement.Name : name;
                }

                case CharacterLocationKind.Settlement:
                    return settlement?.Name ?? "A settlement";
                case CharacterLocationKind.Overland:
                {
                    var region = RegionName(p.RegionId);
                    return region != null ? "The country of " + region : "The country";
                }

                default:
                {
                    if (world.Journey != null) return "On the road";
                    var last = p.LastSettlementId.IsEmpty ? null : world.FindSettlement(p.LastSettlementId);
                    return last != null ? "Near " + last.Name : "The world map";
                }
            }
        }

        private static string CampaignChapter(WorldData world)
        {
            if (world.Campaigns == null) return null;
            foreach (var c in world.Campaigns)
            {
                if (c == null || !c.IsOpen || c.Chapters == null) continue;
                for (var i = c.Chapters.Count - 1; i >= 0; i--)
                    if (c.Chapters[i] != null && c.Chapters[i].State == Realmkeeper.Domain.Campaigns.ChapterState.Active) return c.Chapters[i].Id;
            }

            return null;
        }
    }
}
