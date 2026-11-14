using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    /// <summary>Phase 25: one thing worth telling the player, in words. Kinds match the interface's notification kinds.</summary>
    public sealed class PlayerNotice
    {
        public string Key { get; internal set; }
        public string Kind { get; internal set; }
        public string Title { get; internal set; }
        public string Body { get; internal set; }
        public bool Important { get; internal set; }

        public override string ToString() => Title + (Body == null ? string.Empty : " — " + Body);
    }

    /// <summary>
    /// Phase 25: what the player can know at one moment, reduced to what notices compare. It is taken from the world
    /// with the same visibility rules as the journal (no hidden quest, objective or event).
    /// </summary>
    public sealed class NoticeSnapshot
    {
        internal readonly Dictionary<string, (QuestState State, string Name, string Next)> Quests = new Dictionary<string, (QuestState, string, string)>(StringComparer.Ordinal);
        internal readonly HashSet<string> Discoveries = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> Places = new HashSet<string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, int> Items = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly Dictionary<EquipmentSlot, string> Equipped = new Dictionary<EquipmentSlot, string>();
        internal readonly Dictionary<string, OverlandEncounterState> Encounters = new Dictionary<string, OverlandEncounterState>(StringComparer.Ordinal);
        internal readonly Dictionary<string, int> Creatures = new Dictionary<string, int>(StringComparer.Ordinal);
        internal int Ledger;
        internal int Rumours;
        internal int Events;
        internal bool CanAdvance;
        internal int Level;
        internal string Journey;
        internal bool Valid;
        internal int Generation;

        // Texts the notices may quote (filled with the snapshot, never looked up later).
        internal readonly Dictionary<string, string> DiscoveryTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> EncounterTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<int, (string Text, bool Good)> LedgerTexts = new Dictionary<int, (string, bool)>();
        internal readonly List<(string Key, string Text)> EventTexts = new List<(string, string)>();
        internal Func<string, string> PlaceNamer;

        /// <summary>True when the snapshot was taken from a world with a player (an empty one compares to nothing).</summary>
        public bool IsValid => Valid;

        internal string DiscoveryText(string key) => DiscoveryTexts.TryGetValue(key, out var t) ? t : null;
        internal string EncounterText(string id) => EncounterTexts.TryGetValue(id, out var t) ? t : null;
        internal string PlaceName(string key) => PlaceNamer?.Invoke(key) ?? key;
        internal void SetLedgerText(int index, string text, bool good) => LedgerTexts[index] = (text, good);
        internal (string Text, bool Good) LedgerText(int index) => LedgerTexts.TryGetValue(index, out var t) ? t : ("Your standing changed.", true);
    }

    /// <summary>
    /// Phase 25: the notification rule. It compares two snapshots and says what changed, in the player's words:
    /// <list type="bullet">
    /// <item>quests offered, accepted, advanced, completed or failed;</item>
    /// <item>discoveries, places learnt and rumours;</item>
    /// <item>readiness to level up;</item>
    /// <item>items received and equipment changed;</item>
    /// <item>reputation changes;</item>
    /// <item>overland encounters met and settled;</item>
    /// <item>bestiary entries;</item>
    /// <item>major world events where the player has been;</item>
    /// <item>a journey's arrival or interruption.</item>
    /// </list>
    /// It is pure and engine-free. It is bounded: at most <see cref="MaxPerChange"/> notices, the rest summed up in one.
    /// It has no state of its own: the caller keeps the last snapshot. A load or a new world starts from a fresh snapshot,
    /// so nothing loaded is announced as new.
    /// </summary>
    public static class PlayerNotices
    {
        public const int MaxPerChange = 8;

        public static List<PlayerNotice> Compare(NoticeSnapshot before, NoticeSnapshot after)
        {
            var list = new List<PlayerNotice>();
            if (before == null || after == null || !before.Valid || !after.Valid || before.Generation != after.Generation) return list; // a load or a new world is not news

            foreach (var pair in after.Quests)
            {
                var (state, name, next) = pair.Value;
                if (!before.Quests.TryGetValue(pair.Key, out var was))
                {
                    if (state == QuestState.Discovered || state == QuestState.Offered) Add(list, "quest:" + pair.Key + ":new", "quest", "New lead: " + name, next);
                    else if (state == QuestState.Accepted || state == QuestState.Active) Add(list, "quest:" + pair.Key + ":active", "quest", "Quest begun: " + name, next, true);
                    continue;
                }

                if (was.State == state)
                {
                    if ((state == QuestState.Active || state == QuestState.Accepted) && next != null && next != was.Next)
                        Add(list, "quest:" + pair.Key + ":next:" + next, "quest", "Quest updated: " + name, "Next: " + next);
                    continue;
                }

                switch (state)
                {
                    case QuestState.Accepted:
                    case QuestState.Active: Add(list, "quest:" + pair.Key + ":active", "quest", "Quest begun: " + name, next, true); break;
                    case QuestState.Completed: Add(list, "quest:" + pair.Key + ":done", "quest", "Quest completed: " + name, next, true); break;
                    case QuestState.Failed: Add(list, "quest:" + pair.Key + ":failed", "error", "Quest failed: " + name, next, true); break;
                    case QuestState.Abandoned: Add(list, "quest:" + pair.Key + ":abandoned", "quest", "Quest abandoned: " + name, null); break;
                }
            }

            if (after.CanAdvance && !before.CanAdvance)
                Add(list, "level:" + (after.Level + 1), "level", "You can advance to level " + (after.Level + 1), "Open the level-up (L) when you are ready; nothing is chosen for you.", true);
            else if (after.Level > before.Level)
                Add(list, "levelled:" + after.Level, "level", "Level " + after.Level + " reached", null, true);

            foreach (var d in after.Discoveries) if (!before.Discoveries.Contains(d)) Add(list, "discovery:" + d, "discovery", "Discovery", after.DiscoveryText(d));
            foreach (var p in after.Places) if (!before.Places.Contains(p)) Add(list, "place:" + p, "discovery", "New place known", after.PlaceName(p));
            if (after.Rumours > before.Rumours) Add(list, "rumour:" + after.Rumours, "discovery", after.Rumours - before.Rumours == 1 ? "A rumour heard" : (after.Rumours - before.Rumours) + " rumours heard", "In the journal, under Knowledge.");

            foreach (var pair in after.Items)
            {
                before.Items.TryGetValue(pair.Key, out var had);
                if (pair.Value > had) Add(list, "item:" + pair.Key + ":" + pair.Value, "item", "Received: " + (ItemCatalog.Find(pair.Key)?.Name ?? pair.Key) + (pair.Value - had > 1 ? " ×" + (pair.Value - had) : string.Empty), null);
            }

            foreach (var pair in after.Equipped)
            {
                before.Equipped.TryGetValue(pair.Key, out var had);
                if (pair.Value != had && pair.Value != null) Add(list, "equip:" + pair.Key + ":" + pair.Value, "equipment", "Equipped: " + (ItemCatalog.Find(pair.Value)?.Name ?? pair.Value), SlotName(pair.Key));
            }

            foreach (var pair in before.Equipped)
                if (pair.Value != null && !after.Equipped.ContainsKey(pair.Key)) Add(list, "unequip:" + pair.Key + ":" + pair.Value, "equipment", "Taken off: " + (ItemCatalog.Find(pair.Value)?.Name ?? pair.Value), SlotName(pair.Key));

            for (var i = Math.Max(before.Ledger, after.Ledger - MaxPerChange); i < after.Ledger; i++)
            {
                var (text, good) = after.LedgerText(i);
                Add(list, "rep:" + i, good ? "reputation" : "reputation-down", "Reputation", text);
            }

            foreach (var pair in after.Encounters)
            {
                before.Encounters.TryGetValue(pair.Key, out var was);
                var isNew = !before.Encounters.ContainsKey(pair.Key);
                if (isNew && pair.Value == OverlandEncounterState.Pending) Add(list, "enc:" + pair.Key, "encounter", "Something on the way", after.EncounterText(pair.Key), true);
                else if (!isNew && was == OverlandEncounterState.Pending && pair.Value != OverlandEncounterState.Pending)
                    Add(list, "enc:" + pair.Key + ":" + pair.Value, "encounter", "Encounter: " + EncounterWord(pair.Value), after.EncounterText(pair.Key));
            }

            foreach (var pair in after.Creatures)
            {
                var name = CreatureCatalog.Find(pair.Key)?.Name ?? pair.Key;
                if (!before.Creatures.TryGetValue(pair.Key, out var defeated)) Add(list, "beast:" + pair.Key, "bestiary", "Bestiary: " + name, "A new entry, from what you saw of it.");
                else if (defeated == 0 && pair.Value > 0) Add(list, "beast:" + pair.Key + ":studied", "bestiary", "Bestiary: " + name + " studied", "Its armour, strength and attacks are now known.");
            }

            for (var i = 0; i < after.EventTexts.Count; i++) Add(list, "event:" + after.EventTexts[i].Key, "world", "News", after.EventTexts[i].Text);

            if (after.Journey != before.Journey && after.Journey != null && before.Journey != null)
            {
                if (after.Journey.StartsWith("Arrived", StringComparison.Ordinal)) Add(list, "journey:" + after.Journey, "travel", "Journey's end", after.Journey.Substring(after.Journey.IndexOf(':') + 1).Trim());
                else if (after.Journey.StartsWith("Interrupted", StringComparison.Ordinal)) Add(list, "journey:" + after.Journey, "travel", "The journey stops", "Something on the road needs dealing with (the map shows it).", true);
            }

            if (list.Count > MaxPerChange)
            {
                var extra = list.Count - (MaxPerChange - 1);
                list.RemoveRange(MaxPerChange - 1, list.Count - (MaxPerChange - 1));
                list.Add(new PlayerNotice { Key = "more:" + Guid.NewGuid().ToString("N"), Kind = "notify", Title = extra + " more changes", Body = "See the journal for the rest." });
            }

            return list;
        }

        private static void Add(List<PlayerNotice> list, string key, string kind, string title, string body, bool important = false) =>
            list.Add(new PlayerNotice { Key = key, Kind = kind, Title = title, Body = body, Important = important });

        private static string EncounterWord(OverlandEncounterState s) => s switch
        {
            OverlandEncounterState.Fought => "fought",
            OverlandEncounterState.Avoided => "slipped away",
            OverlandEncounterState.Paid => "toll paid",
            OverlandEncounterState.Left => "backed off",
            _ => "settled",
        };

        public static string SlotName(EquipmentSlot slot)
        {
            var s = slot.ToString();
            var b = new System.Text.StringBuilder();
            for (var i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) b.Append(' ');
                b.Append(i == 0 ? s[i] : char.ToLowerInvariant(s[i]));
            }

            return b.ToString();
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 25: how many times the session's world was replaced wholesale (new, loaded, reconnected), not by commands.</summary>
        public int ReplaceCount { get; private set; }

        /// <summary>Phase 25: the notice snapshot of the world as the player knows it (cheap: no strings built for unchanged things beyond names).</summary>
        public NoticeSnapshot GetNoticeSnapshot()
        {
            var s = new NoticeSnapshot();
            var w = _world;
            var p = w?.Player;
            if (p == null) return s;
            s.Valid = true;
            s.Generation = ReplaceCount;
            foreach (var q in w.Quests)
            {
                if (q.State == QuestState.Unavailable || q.State == QuestState.Available) continue; // not the player's yet
                var sum = Summary(q);
                s.Quests[q.InstanceId] = (q.State, sum.Name, sum.Next);
            }

            var social = p.Social;
            foreach (var d in social.Discoveries) if (d?.Key != null) { s.Discoveries.Add(d.Key); s.DiscoveryTexts[d.Key] = d.Text; }
            foreach (var k in social.KnownPlaces) if (k != null) s.Places.Add(k);
            s.PlaceNamer = PlaceNameOf;
            s.Rumours = social.Rumors.Count;
            s.Ledger = social.Ledger.Count;
            for (var i = Math.Max(0, social.Ledger.Count - PlayerNotices.MaxPerChange * 2); i < social.Ledger.Count; i++)
            {
                var e = social.Ledger[i];
                s.SetLedgerText(i, $"{Reputation.TargetName(w, e.Target)}: {e.Dimension.ToLowerInvariant()} {(e.Amount >= 0 ? "+" : string.Empty)}{e.Amount}" + (string.IsNullOrEmpty(e.Reason) ? string.Empty : $" ({e.Reason})"),
                    e.Amount >= 0 != (e.Dimension == "Fear" || e.Dimension == "Hostility"));
            }

            foreach (var st in p.Inventory)
            {
                if (st?.ItemId == null) continue;
                s.Items.TryGetValue(st.ItemId, out var n);
                s.Items[st.ItemId] = n + st.Quantity;
            }

            foreach (var pair in p.Equipment)
            {
                if (pair.Value?.ItemId == null) continue;
                s.Equipped[pair.Key] = pair.Value.ItemId;
                // What is worn counts as owned too, so moving an item between the pack and the body is not "received".
                s.Items.TryGetValue(pair.Value.ItemId, out var n);
                s.Items[pair.Value.ItemId] = n + Math.Max(1, pair.Value.Quantity);
            }
            foreach (var e in w.Overland.Encounters) { s.Encounters[e.Id] = e.State; s.EncounterTexts[e.Id] = e.Text; }
            foreach (var k in p.KnownCreatures) s.Creatures[k.CreatureId] = k.Defeated;
            s.Level = p.Rpg?.Level ?? 0;
            s.CanAdvance = p.Rpg != null && Domain.Rpg.CharacterAdvancement.LevelsAvailable(p.Rpg) > 0;

            // Major and notable events the player can know, since the snapshot was last taken (the caller passes the last count).
            s.Events = w.HistoricalEvents.Count;
            var j = w.Journey;
            s.Journey = j == null ? null : j.Status == JourneyStatus.Arrived ? "Arrived: " + (w.FindSettlement(j.DestinationId)?.Name ?? "your destination") : j.Status + ":" + j.JourneyId;
            return s;
        }

        /// <summary>
        /// Phase 25: adds to <paramref name="after"/> the texts of events new since <paramref name="before"/> that the
        /// player can know and that matter (notable or major). Kept apart so the event scan only covers new entries.
        /// </summary>
        public void FillNoticeEvents(NoticeSnapshot before, NoticeSnapshot after)
        {
            if (before == null || after == null || !before.Valid || !after.Valid || _world == null) return;
            var from = before.Events;
            if (from > after.Events) return; // the chronicle was trimmed: say nothing rather than guess
            var s = _world.Player.Social;
            for (var i = Math.Max(from, after.Events - 20); i < after.Events; i++)
            {
                var e = _world.HistoricalEvents[i];
                if (e.Importance == EventImportance.Minor || !PlayerKnows(e, s)) continue;
                var source = e.Get("source");
                if (source == "player" || source == "quest" || source == "combat") continue; // the player's own doings are announced by their own notices
                after.EventTexts.Add((i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + e.Date, EventFormatter.Format(e, NameOf, FactionNameOf)));
            }
        }

        private string PlaceNameOf(string key)
        {
            if (key == null || _world == null) return key;
            var place = _world.FindPlace(key);
            if (place != null) return place.Name;
            if (Guid.TryParse(key, out var g)) return _world.FindSettlement(new SettlementId(g))?.Name ?? key;
            return key;
        }
    }
}
