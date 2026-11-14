using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    /// <summary>Phase 26: the codex's categories. Only those backed by records the game keeps.</summary>
    public enum CodexCategory
    {
        People = 0,
        Companions = 1,
        Factions = 2,
        Settlements = 3,
        Places = 4,
        Creatures = 5,
        Discoveries = 6,
        Events = 7,
    }

    /// <summary>One codex entry: what the player knows of one thing, with how each fact is known.</summary>
    public sealed class CodexEntryView
    {
        public string Id { get; internal set; }
        public CodexCategory Category { get; internal set; }
        public string Name { get; internal set; }
        public string Subtitle { get; internal set; }

        /// <summary>How well it is known, in words ("Met", "Heard of", "Visited", "Studied" …).</summary>
        public string Status { get; internal set; }

        /// <summary>The known facts: label, value, and how the player knows it.</summary>
        public List<(string Label, string Value, string How)> Lines { get; } = new List<(string, string, string)>();

        public string Text { get; internal set; }

        /// <summary>Other entries and quests this one is connected to, as the player knows them.</summary>
        public List<string> Links { get; } = new List<string>();

        public string Source { get; internal set; }

        /// <summary>When it was learned, in minutes since the calendar's start, or -1 when not recorded.</summary>
        public long At { get; internal set; } = -1;

        /// <summary>What knowing more would add (said, never shown blank), or null.</summary>
        public string Withheld { get; internal set; }
    }

    public sealed class CodexView
    {
        public List<CodexEntryView> Entries { get; } = new List<CodexEntryView>();

        /// <summary>Entries per category before the search and category filter (the chips' counts).</summary>
        public Dictionary<CodexCategory, int> Counts { get; } = new Dictionary<CodexCategory, int>();

        public int Total { get; internal set; }
    }

    /// <summary>
    /// Phase 26 (ADR-0117): the codex. It gathers what the player has learned from records the game already keeps. There
    /// is no codex state of its own, so it cannot drift from the journal, cannot duplicate, and survives save and load as
    /// those records do.
    /// <list type="bullet">
    /// <item><b>People:</b> those the player has met or spoken with (the Phase 20 known-person rules decide each fact).</item>
    /// <item><b>Companions:</b> those who travel or have travelled with the player.</item>
    /// <item><b>Factions:</b> those the player has a standing with, or that govern a settlement the player has visited.</item>
    /// <item><b>Settlements:</b> visited, or heard of.</item>
    /// <item><b>Places:</b> rumoured, found or visited.</item>
    /// <item><b>Creatures:</b> the bestiary record (ADR-0116).</item>
    /// <item><b>Discoveries</b> and <b>events:</b> the journal's.</item>
    /// </list>
    /// Nothing unknown is listed. Nothing is invented: names and descriptions are the world's own.
    /// </summary>
    public sealed partial class WorldSession
    {
        public CodexView GetCodex(string search = null, CodexCategory? category = null, bool recentFirst = false)
        {
            var v = new CodexView();
            var w = _world;
            var p = w?.Player;
            if (p == null) return v;
            var all = new List<CodexEntryView>();
            var s = p.Social;
            var journal = GetJournal();

            // People and companions.
            var companionIds = new HashSet<NpcId>(w.Companions.Select(c => c.NpcId));
            foreach (var n in w.Npcs)
            {
                var a = KnownPersonQueries.AcquaintanceOf(w, n);
                var companion = companionIds.Contains(n.NpcId);
                if (a == Acquaintance.Seen && !companion) continue; // never met: not in the codex
                var k = KnownPersonQueries.View(w, n);
                var e = new CodexEntryView
                {
                    Id = (companion ? "companion:" : "person:") + n.NpcId, Category = companion ? CodexCategory.Companions : CodexCategory.People, Name = n.Name,
                    Status = n.IsDeceased ? "Dead" : k.AcquaintanceText ?? a.ToString(), Withheld = k.Withheld, Source = "Meeting them",
                    At = n.HasMetPlayer ? n.MetPlayerOn.ToDayNumber() * (long)WorldTime.MinutesPerDay : -1,
                };
                foreach (var l in k.Lines) e.Lines.Add((l.Label, l.Value, l.How));
                e.Subtitle = k.Lines.FirstOrDefault(l => l.Label == "Occupation")?.Value;
                if (companion)
                {
                    var rec = w.Companions.First(c => c.NpcId == n.NpcId);
                    e.Lines.Add(("With you", rec.Standing == CompanionStanding.InParty ? "Travels with you" : rec.Standing.ToString(), "travels with you"));
                }

                foreach (var q in journal.Active.Concat(journal.Available).Concat(journal.Completed).Concat(journal.Failed))
                    if (q.Giver == n.Name) e.Links.Add("Quest: " + q.Name);
                all.Add(e);
            }

            // Factions: a standing, or governing a settlement the player has been to.
            var t = ReputationThresholds.Default;
            var factions = new Dictionary<FactionId, CodexEntryView>();
            foreach (var f in s.FactionStandings)
            {
                var fd = w.FindFaction(f.FactionId);
                if (fd == null) continue;
                var e = Faction(fd);
                e.Lines.Add(("Your standing", t.ForStanding(f.Standing, s.Notoriety).ToString(), "your dealings with them"));
                factions[fd.FactionId] = e;
            }

            foreach (var sid in s.VisitedSettlements)
            {
                var st = w.FindSettlement(sid);
                if (st == null || st.FactionId.IsEmpty) continue;
                var fd = w.FindFaction(st.FactionId);
                if (fd == null) continue;
                if (!factions.TryGetValue(fd.FactionId, out var e)) factions[fd.FactionId] = e = Faction(fd);
                e.Links.Add("Governs " + st.Name);
            }

            all.AddRange(factions.Values);

            // Settlements: visited, or heard of.
            foreach (var st in w.Settlements)
            {
                var visited = s.VisitedSettlements.Contains(st.SettlementId);
                var heard = s.KnownPlaces.Contains(st.SettlementId.ToString());
                if (!visited && !heard) continue;
                var e = new CodexEntryView
                {
                    Id = "settlement:" + st.SettlementId, Category = CodexCategory.Settlements, Name = st.Name, Subtitle = st.SettlementType.ToString(),
                    Status = visited ? "Visited" : "Heard of", Source = visited ? "Your travels" : "Word of mouth",
                };
                e.Lines.Add(("Region", w.FindRegion(st.RegionId)?.Name ?? "—", visited ? "been there" : "heard"));
                if (visited)
                {
                    e.Lines.Add(("Kind", st.SettlementType.ToString(), "been there"));
                    e.Lines.Add(("People", st.Population.ToString(System.Globalization.CultureInfo.InvariantCulture), "been there"));
                    var gov = w.FindFaction(st.FactionId);
                    if (gov != null) { e.Lines.Add(("Governed by", gov.Name, "been there")); e.Links.Add("Faction: " + gov.Name); }
                }
                else e.Withheld = "Go there to learn more of it.";

                all.Add(e);
            }

            // Places: rumoured, found or visited.
            foreach (var pl in w.Places)
            {
                if (pl.Discovery == PlaceDiscovery.Unknown) continue;
                var e = new CodexEntryView
                {
                    Id = "place:" + pl.PlaceId, Category = CodexCategory.Places, Name = pl.Name, Subtitle = pl.Category.ToString(),
                    Status = pl.Discovery == PlaceDiscovery.Visited ? "Visited" : pl.Discovery == PlaceDiscovery.Discovered ? "Found" : "Rumoured",
                    Source = pl.Discovery == PlaceDiscovery.Rumored ? "A rumour" : "Your travels",
                };
                e.Lines.Add(("Region", w.FindRegion(pl.RegionId)?.Name ?? "—", pl.Discovery == PlaceDiscovery.Rumored ? "heard" : "been near"));
                if (!pl.SettlementId.IsEmpty) e.Lines.Add(("Near", w.FindSettlement(pl.SettlementId)?.Name ?? "—", "heard"));
                if (pl.Discovery >= PlaceDiscovery.Discovered) e.Text = pl.Description;
                else e.Withheld = "Find it to learn more.";
                foreach (var d in s.Discoveries) if (d.PlaceId == pl.PlaceId) e.Links.Add("Discovery: " + d.Text);
                all.Add(e);
            }

            // Creatures: the bestiary.
            foreach (var b in GetBestiary().Entries)
            {
                var e = new CodexEntryView
                {
                    Id = "creature:" + b.CreatureId, Category = CodexCategory.Creatures, Name = b.Name, Subtitle = b.CategoryName, Status = b.Studied ? "Studied" : "Seen",
                    Text = b.Behaviour, Source = "Fighting them",
                };
                e.Lines.Add(("Fights", b.Met.ToString(), "seen in a fight"));
                e.Lines.Add(("Defeated", b.Defeated.ToString(), "seen in a fight"));
                if (b.Habitats != null) e.Lines.Add(("Lives in", b.Habitats, "seen in a fight"));
                if (b.Studied) { e.Lines.Add(("Armour class", b.ArmourClass, "defeated one")); e.Lines.Add(("Hit points", b.HitPoints, "defeated one")); }
                else e.Withheld = "Defeat one to learn its strength (the bestiary in the hub shows it all).";
                all.Add(e);
            }

            // Discoveries and events: the journal's own.
            foreach (var d in journal.DiscoveryEntries)
            {
                var e = new CodexEntryView
                {
                    Id = "discovery:" + d.Key, Category = CodexCategory.Discoveries, Name = d.Text.Length > 64 ? d.Text.Substring(0, 61) + "…" : d.Text, Subtitle = d.Category.ToString(),
                    Status = d.Certainty.ToString(), Text = d.Text, Source = d.Place ?? "Your journal",
                };
                e.Lines.Add(("Learned", d.When, "your journal"));
                if (d.Place != null) e.Links.Add("Place: " + d.Place);
                all.Add(e);
            }

            for (var i = 0; i < journal.Events.Count; i++)
            {
                var (when, text) = journal.Events[i];
                all.Add(new CodexEntryView
                {
                    Id = "event:" + i + ":" + when, Category = CodexCategory.Events, Name = text.Length > 64 ? text.Substring(0, 61) + "…" : text, Subtitle = when,
                    Status = "Known", Text = text, Source = "The chronicle, as you know it",
                });
            }

            foreach (CodexCategory c in Enum.GetValues(typeof(CodexCategory))) v.Counts[c] = all.Count(e => e.Category == c);
            v.Total = all.Count;
            IEnumerable<CodexEntryView> shown = all;
            if (category.HasValue) shown = shown.Where(e => e.Category == category.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                shown = shown.Where(e => Contains(e.Name, q) || Contains(e.Subtitle, q) || Contains(e.Text, q) || e.Lines.Any(l => Contains(l.Value, q)));
            }

            shown = recentFirst ? shown.OrderByDescending(e => e.At).ThenBy(e => e.Name, StringComparer.Ordinal) : shown.OrderBy(e => e.Category).ThenBy(e => e.Name, StringComparer.Ordinal);
            v.Entries.AddRange(shown);
            return v;

            CodexEntryView Faction(FactionData fd)
            {
                var e = new CodexEntryView
                {
                    Id = "faction:" + fd.FactionId, Category = CodexCategory.Factions, Name = fd.Name, Subtitle = fd.Type.ToString(), Status = fd.Status.ToString(),
                    Text = string.IsNullOrEmpty(fd.Description) ? null : fd.Description, Source = "Your dealings and travels",
                };
                e.Lines.Add(("Kind", fd.Type.ToString(), "common knowledge"));
                var leader = w.FindNpc(fd.LeaderNpcId);
                if (leader != null && KnownPersonQueries.AcquaintanceOf(w, leader) != Acquaintance.Seen) e.Lines.Add(("Led by", leader.Name, "met them"));
                return e;
            }
        }

        private static bool Contains(string text, string q) => text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
