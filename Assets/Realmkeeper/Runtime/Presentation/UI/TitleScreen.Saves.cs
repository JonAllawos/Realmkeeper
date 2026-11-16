using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// M1.1 (ADR-0123): the release save screens. New Game asks for one of three slots; Save Game writes one of them;
    /// Load Game lists them with the three autosaves (and, in development builds only, every older save); Continue loads
    /// the save played most recently and, if that fails, offers the next. Nothing is overwritten or deleted without the
    /// player confirming it.
    /// </summary>
    public sealed partial class TitleScreen
    {
        /// <summary>Developer entries (named, seeded worlds; campaigns; older saves) appear in the editor and development builds only.</summary>
        public static bool DeveloperBuild => UnityEngine.Application.isEditor || Debug.isDebugBuild;

        private readonly List<Texture2D> _thumbnails = new List<Texture2D>();

        public JsonWorldStore Store => _store;

        // ----- Continue -----------------------------------------------------------------------------

        /// <summary>
        /// The saves Continue may load, most recently played first: Ready and Will-be-updated saves in the manual and
        /// autosave slots (and, in development builds, older saves too). Too-new and Damaged saves are skipped.
        /// </summary>
        public List<SaveSummary> ContinueCandidates() => _store.ContinueOrder(DeveloperBuild);

        /// <summary>Loads the most recently played save; if it fails, says so and offers the next. Never changes a file.</summary>
        public void ContinueGame() => ContinueFrom(ContinueCandidates(), 0);

        private void ContinueFrom(List<SaveSummary> list, int index)
        {
            if (index >= list.Count)
            {
                Say(index == 0 ? "There is no saved game yet: start a new one." : "No other save could be loaded.");
                RefreshMenu();
                return;
            }

            var s = list[index];
            var result = LoadSlot(s.Slot);
            if (result.IsSuccess) return;
            var why = $"{SlotLabel(s)} could not be loaded: {result.Message} Nothing was changed or deleted.";
            if (index + 1 < list.Count)
            {
                var next = list[index + 1];
                RkModal.Confirm(_document.rootVisualElement, "That save could not be loaded", why + $" Load the next most recent save, {SlotLabel(next)}?",
                    "Load " + SaveSlots.DisplayName(next.Slot), () => ContinueFrom(list, index + 1));
            }

            Say(why);
        }

        public static string SlotLabel(SaveSummary s)
        {
            var who = s.CharacterName ?? s.WorldName;
            return string.IsNullOrEmpty(who) ? SaveSlots.DisplayName(s.Slot) : $"{SaveSlots.DisplayName(s.Slot)} ({who})";
        }

        // ----- New Game -----------------------------------------------------------------------------

        private void BuildNewGame()
        {
            var panel = Panel("New Game", "newworld");
            var intro = new Label("Choose a slot for your new game. The game is saved there as it begins, and whenever you choose Save Game. Autosaves are kept separately.");
            intro.AddToClassList("rk-wrap");
            intro.AddToClassList("rk-secondary");
            panel.Add(intro);
            var list = SlotList(panel, "title-newgame-list");
            foreach (var slot in SaveSlots.Manual) list.Add(SlotCard(slot, _store.Inspect(slot, false), "new"));
            panel.Add(BackRow());
        }

        /// <summary>Starts a new game in a manual slot, confirming first when it holds a save or a game is running.</summary>
        public void ChooseNewGameSlot(string slot)
        {
            var existing = _store.Inspect(slot, false);
            var inGame = _bootstrap.Runtime.Host.IsActive && !_launch;
            if (existing == null && !inGame)
            {
                CreateNewGame(slot);
                return;
            }

            var body = existing != null
                ? $"{SlotLabel(existing)} holds a save. Starting a new game here replaces it when the new game is first saved; the old save is kept as this slot's backup until the slot is saved again."
                : string.Empty;
            if (inGame) body = (body.Length > 0 ? body + " " : string.Empty) + "The game you are playing stays as it was last saved; anything since then is lost.";
            RkModal.Confirm(_document.rootVisualElement, existing != null ? "Replace this save?" : "Leave this game?", body, "Start the new game", () => CreateNewGame(slot), existing != null);
        }

        /// <summary>
        /// Founds the New Game world (<see cref="NewGameDefinition"/>) and saves it at once to <paramref name="slot"/>,
        /// which becomes the active slot. Character creation is unchanged (decision D4 belongs to M3).
        /// </summary>
        public CommandResult CreateNewGame(string slot)
        {
            if (!SaveSlots.IsManual(slot)) return Fail("Choose Slot 1, 2 or 3.");
            var runtime = _bootstrap.Runtime;
            if (runtime.Host.IsActive) runtime.Host.Shutdown();
            var result = runtime.Host.StartNew(new CreateWorldCommand(NewGameDefinition.WorldName, NewGameDefinition.Seed, WorldSession.NewGameSettings()));
            if (!result.IsSuccess) return Fail(result.Message);
            runtime.SelectSlot(slot);
            var saved = runtime.Save(slot);
            Say(saved.IsSuccess ? $"A new game has begun in {SaveSlots.DisplayName(slot)}." : $"The new game began, but its first save failed: {saved.Message} {string.Join(" ", saved.Errors)}");
            Close();
            return result;
        }

        // ----- Save Game ----------------------------------------------------------------------------

        private void BuildSaveGame()
        {
            var panel = Panel("Save Game", "save");
            var session = _bootstrap.Session;
            var blocker = session == null ? "There is no game to save." : session.GetSaveBlocker();
            var intro = new Label(blocker ?? "Choose a slot. Saving over a slot keeps its previous save as a backup until the slot is saved again.");
            intro.name = "title-save-reason";
            intro.AddToClassList("rk-wrap");
            intro.AddToClassList(blocker != null ? "rk-hint" : "rk-secondary");
            if (blocker != null) intro.style.color = new Color(0.89f, 0.48f, 0.39f);
            panel.Add(intro);
            var list = SlotList(panel, "title-save-list");
            foreach (var slot in SaveSlots.Manual) list.Add(SlotCard(slot, _store.Inspect(slot, false), "save", blocker));
            panel.Add(BackRow());
        }

        /// <summary>Saves the running game to a manual slot, confirming first when the slot holds a save.</summary>
        public void ChooseSaveSlot(string slot)
        {
            var session = _bootstrap.Session;
            var blocker = session == null ? "There is no game to save." : session.GetSaveBlocker();
            if (blocker != null) { Say(blocker); return; }
            var existing = _store.Inspect(slot, false);
            if (existing == null) { SaveToSlot(slot); return; }
            RkModal.Confirm(_document.rootVisualElement, "Save over this slot?",
                $"{SlotLabel(existing)} will be replaced by your current game. The save it replaces is kept as this slot's backup until the slot is saved again.",
                "Save here", () => SaveToSlot(slot));
        }

        /// <summary>Writes the running game to a manual slot (no confirmation: <see cref="ChooseSaveSlot"/> asks). The slot becomes active.</summary>
        public CommandResult SaveToSlot(string slot)
        {
            if (!SaveSlots.IsManual(slot)) return Fail("Choose Slot 1, 2 or 3.");
            var session = _bootstrap.Session;
            var blocker = session == null ? "There is no game to save." : session.GetSaveBlocker();
            if (blocker != null) return Fail(blocker);
            var runtime = _bootstrap.Runtime;
            var result = runtime.Save(slot);
            if (result.IsSuccess) runtime.SelectSlot(slot);
            Say(result.IsSuccess ? $"Saved to {SaveSlots.DisplayName(slot)}." : $"Not saved: {result.Message} {string.Join(" ", result.Errors)}");
            if (Page == "save") ShowPage("save");
            return result;
        }

        // ----- Load Game ----------------------------------------------------------------------------

        private void BuildLoadGame()
        {
            var panel = Panel("Load Game", "load");
            var saves = _store.ListSaves(true);
            var bySlot = saves.ToDictionary(s => s.Slot, StringComparer.Ordinal);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "title-world-list" };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(scroll);

            scroll.Add(Heading("Saved games"));
            foreach (var slot in SaveSlots.Manual) scroll.Add(SlotCard(slot, bySlot.TryGetValue(slot, out var s) ? s : null, "load"));
            scroll.Add(Heading("Autosaves"));
            foreach (var slot in SaveSlots.Auto) scroll.Add(SlotCard(slot, bySlot.TryGetValue(slot, out var s) ? s : null, "load"));

            if (DeveloperBuild)
            {
                var others = saves.Where(x => x.Kind == SaveSlotKind.Other).ToList();
                scroll.Add(Heading("Other saves (development builds only)"));
                if (others.Count == 0)
                {
                    var none = new Label("None.");
                    none.AddToClassList("rk-hint");
                    scroll.Add(none);
                }

                foreach (var o in others) scroll.Add(OtherCard(o));
            }

            panel.Add(BackRow());
        }

        /// <summary>Restores a damaged slot's backup (only once the backup has loaded) and loads it.</summary>
        public void ChooseLoadBackup(string slot)
        {
            RkModal.Confirm(_document.rootVisualElement, "Load the backup?",
                $"{SaveSlots.DisplayName(slot)} is damaged. Its backup, the save before it, will be checked and, if it loads, put back in the slot. The damaged file is kept beside it.",
                "Load backup", () =>
                {
                    var restored = _store.RestoreBackup(slot);
                    if (!restored.IsSuccess) { Say(restored.Error); ShowPage("load"); return; }
                    LoadSlot(slot);
                });
        }

        private void ChooseLoad(string slot)
        {
            if (_bootstrap.Runtime.Host.IsActive && !_launch)
            {
                RkModal.Confirm(_document.rootVisualElement, "Load this save?", "The game you are playing stays as it was last saved; anything since then is lost.", "Load", () => LoadSlot(slot));
                return;
            }

            LoadSlot(slot);
        }

        private void ChooseDelete(string slot, string label)
        {
            RkModal.Confirm(_document.rootVisualElement, "Delete this save?",
                $"{label} will be deleted from this computer, with its backup and picture. This cannot be undone.", "Delete save", () =>
                {
                    var r = _store.Delete(slot);
                    Say(r.IsSuccess ? $"Deleted {label}." : r.Error);
                    RefreshMenu();
                    ShowPage(Page);
                }, danger: true);
        }

        // ----- Cards --------------------------------------------------------------------------------

        private VisualElement SlotList(VisualElement panel, string name)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = name };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(scroll);
            return scroll;
        }

        private static Label Heading(string text)
        {
            var h = new Label(text);
            h.AddToClassList("rk-h2");
            h.style.marginTop = 10;
            h.style.marginBottom = 4;
            return h;
        }

        private VisualElement BackRow()
        {
            var buttons = new VisualElement();
            buttons.AddToClassList("rk-modal-buttons");
            var back = new Button(() => ShowPage(null)) { name = "title-page-back", text = "Back" };
            back.AddToClassList("rk-btn");
            buttons.Add(back);
            return buttons;
        }

        /// <summary>One release slot: picture, character, place, date, playtime, when saved, and whether it loads.</summary>
        private VisualElement SlotCard(string slot, SaveSummary s, string mode, string blocker = null)
        {
            var card = new VisualElement { name = "slot-card-" + slot };
            card.AddToClassList("rk-card");
            card.EnableInClassList("rk-card--selected", s != null && slot == _bootstrap.Runtime.ActiveSlot.Name && _bootstrap.Runtime.Host.IsActive);
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;

            var picture = new Image { name = "slot-thumb-" + slot, scaleMode = ScaleMode.ScaleAndCrop };
            picture.style.width = 128;
            picture.style.height = 72;
            picture.style.marginRight = 14;
            picture.style.flexShrink = 0;
            picture.style.backgroundColor = new Color(0.12f, 0.1f, 0.08f);
            var tex = s != null ? SaveThumbnail.Load(s.ThumbnailPath) : null;
            if (tex != null) { picture.image = tex; _thumbnails.Add(tex); }
            card.Add(picture);

            var text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            var kind = new Label(SaveSlots.DisplayName(slot)) { name = "slot-kind-" + slot };
            kind.AddToClassList("rk-hint");
            text.Add(kind);
            var title = new Label(s == null ? "Empty" : s.CharacterName ?? s.WorldName ?? slot) { name = "slot-title-" + slot };
            title.AddToClassList("rk-h2");
            text.Add(title);
            if (s != null)
            {
                foreach (var line in DetailLines(s))
                {
                    var l = new Label(line);
                    l.AddToClassList("rk-hint");
                    l.AddToClassList("rk-wrap");
                    text.Add(l);
                }

                var badge = new Label(s.StateText) { name = "slot-state-" + slot };
                badge.AddToClassList("rk-badge");
                badge.AddToClassList(s.State == SaveState.Ready ? "rk-badge--info" : s.State == SaveState.NeedsUpdate ? "rk-badge--warn" : "rk-badge--bad");
                badge.style.alignSelf = Align.FlexStart;
                if (s.Problem != null) badge.tooltip = s.Problem;
                text.Add(badge);
            }

            card.Add(text);
            var buttons = new VisualElement();
            buttons.style.flexShrink = 0;
            card.Add(buttons);
            switch (mode)
            {
                case "load":
                    if (s == null) break;
                    var load = CardButton(buttons, "slot-load-" + slot, "Load", true, () => ChooseLoad(slot));
                    load.SetEnabled(s.CanLoad);
                    if (!s.CanLoad) load.tooltip = s.Problem ?? s.StateText;
                    if (s.State == SaveState.Damaged && s.HasGoodBackup) CardButton(buttons, "slot-backup-" + slot, "Load backup", false, () => ChooseLoadBackup(slot));
                    var delete = CardButton(buttons, "slot-delete-" + slot, "Delete", false, () => ChooseDelete(slot, SlotLabel(s)));
                    delete.AddToClassList("rk-btn--danger");
                    break;
                case "save":
                    var save = CardButton(buttons, "slot-save-" + slot, "Save here", true, () => ChooseSaveSlot(slot));
                    save.SetEnabled(blocker == null);
                    if (blocker != null) save.tooltip = blocker;
                    break;
                case "new":
                    CardButton(buttons, "slot-new-" + slot, s == null ? "Start here" : "Replace", true, () => ChooseNewGameSlot(slot));
                    break;
            }

            return card;
        }

        private static Button CardButton(VisualElement parent, string name, string text, bool primary, Action act)
        {
            var b = new Button(act) { name = name, text = text };
            b.AddToClassList("rk-btn");
            if (primary) b.AddToClassList("rk-btn--primary");
            b.style.marginBottom = 4;
            parent.Add(b);
            return b;
        }

        /// <summary>The detail lines of a slot card.</summary>
        public static List<string> DetailLines(SaveSummary s)
        {
            var lines = new List<string>();
            var who = new List<string>();
            if (!string.IsNullOrEmpty(s.ClassName)) who.Add(s.ClassName);
            if (s.Level > 0) who.Add("level " + s.Level);
            if (!string.IsNullOrEmpty(s.LocationText)) who.Add(s.LocationText);
            if (who.Count > 0) lines.Add(string.Join(" · ", who));
            var when = FormatDate(s.Date);
            if (s.MinuteOfDay.HasValue) when += " · " + (s.MinuteOfDay.Value / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s.MinuteOfDay.Value % 60).ToString("00", CultureInfo.InvariantCulture);
            lines.Add(when);
            lines.Add($"Played {FormatPlaytime(s.PlaytimeSeconds)} · saved {Local(s.LastPlayedUtc)}");
            return lines;
        }

        /// <summary>"3 h 05 min", "12 min", "under a minute"; "—" when unknown (saves from before v28).</summary>
        public static string FormatPlaytime(double? seconds)
        {
            if (!seconds.HasValue || seconds.Value <= 0) return "—";
            var minutes = (long)(seconds.Value / 60);
            if (minutes < 1) return "under a minute";
            return minutes < 60 ? minutes + " min" : $"{minutes / 60} h {minutes % 60:00} min";
        }

        /// <summary>An older or developer save (development builds only), as Phase 19 listed it.</summary>
        private VisualElement OtherCard(SaveSummary s)
        {
            var card = new VisualElement { name = "world-card-" + s.Slot };
            card.AddToClassList("rk-card");
            card.EnableInClassList("rk-card--selected", s.Slot == _bootstrap.Runtime.ActiveSlot.Name && _bootstrap.Runtime.Host.IsActive);
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            var icon = RkIcons.Make("world", "xl", s.Readable ? "accent" : "bad");
            icon.style.marginRight = 14;
            card.Add(icon);
            var text = new VisualElement();
            text.style.flexGrow = 1;
            var name = new Label(s.WorldName ?? s.Slot);
            name.AddToClassList("rk-h2");
            text.Add(name);
            var detail = new Label(s.Readable
                ? $"{FormatDate(s.Date)} · seed {s.Seed} · saved {Local(s.LastPlayedUtc)} · {s.Bytes / 1024 / 1024f:0.0} MB · slot '{s.Slot}'"
                : $"This file's header could not be read; it may be damaged. Slot '{s.Slot}'.");
            detail.AddToClassList("rk-hint");
            text.Add(detail);
            if (s.State != SaveState.Ready)
            {
                var warn = new Label(s.State == SaveState.TooNew ? $"Saved by a newer build (schema v{s.SchemaVersion}); this build reads up to v{WorldData.CurrentSchemaVersion}." : s.StateText);
                warn.AddToClassList("rk-badge");
                warn.AddToClassList(s.CanLoad ? "rk-badge--warn" : "rk-badge--bad");
                if (s.Problem != null) warn.tooltip = s.Problem;
                text.Add(warn);
            }

            card.Add(text);
            var slot = s.Slot;
            var load = new Button(() => LoadSlot(slot)) { name = "world-load-" + slot, text = "Load" };
            load.AddToClassList("rk-btn");
            load.AddToClassList("rk-btn--primary");
            load.SetEnabled(s.CanLoad);
            if (!s.CanLoad) load.tooltip = s.Problem ?? s.StateText;
            var delete = new Button(() => ConfirmDelete(slot, s.WorldName ?? slot)) { name = "world-delete-" + slot, text = "Delete" };
            delete.AddToClassList("rk-btn");
            delete.AddToClassList("rk-btn--danger");
            card.Add(load);
            card.Add(delete);
            return card;
        }

        private void ReleaseThumbnails()
        {
            foreach (var t in _thumbnails) if (t != null) Destroy(t);
            _thumbnails.Clear();
        }
    }
}
