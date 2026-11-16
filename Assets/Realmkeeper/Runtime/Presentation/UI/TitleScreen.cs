using System;
using System.Globalization;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Hosting;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Phase 19: the entry experience (title, world selection and creation, campaigns, settings, credits). It is an
    /// overlay over the running strategic view, so the living world is the backdrop. It uses only existing commands:
    /// <see cref="WorldSessionHost.StartNew"/> and <see cref="WorldSessionHost.StartFromSave"/>, the runtime's save
    /// slots, <see cref="StartCampaignCommand"/>; saves are listed and deleted through <see cref="JsonWorldStore"/>.
    /// Shown when the game starts (the scene is the first one loaded), and from the map's Menu button.
    /// </summary>
    public sealed partial class TitleScreen : MonoBehaviour
    {
        private static string _firstScene;

        private RealmkeeperBootstrap _bootstrap;
        private UIDocument _document;
        private JsonWorldStore _store;
        private bool _launch;

        public VisualElement Root { get; private set; }
        public VisualElement Content { get; private set; }
        public Label Message { get; private set; }
        public Button ContinueButton { get; private set; }

        /// <summary>M1.1: New Game (a slot, then the fixed world).</summary>
        public Button NewGameButton { get; private set; }

        /// <summary>M1.1: Load Game (the slots, the autosaves, and older saves in development builds).</summary>
        public Button LoadButton { get; private set; }

        /// <summary>M1.1: Save Game, shown only in a game.</summary>
        public Button SaveGameButton { get; private set; }

        /// <summary>Developer entry (editor and development builds only; null otherwise): a named world from a seed.</summary>
        public Button NewWorldButton { get; private set; }

        /// <summary>Developer entry (editor and development builds only; null otherwise).</summary>
        public Button CampaignsButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button CreditsButton { get; private set; }
        public Button ExitButton { get; private set; }
        public RkSettingsWindow Settings { get; private set; }

        /// <summary>The page on the right: "load", "save", "newgame", "credits", the developer pages "new" and "campaigns", or null ("worlds" opens "load").</summary>
        public string Page { get; private set; }

        public bool IsOpen => Root != null && Root.style.display != DisplayStyle.None;

        public event Action Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLaunch() => _firstScene = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RecordLaunch()
        {
            if (_firstScene == null) _firstScene = SceneManager.GetActiveScene().path;
        }

        /// <summary>True when this scene is the one the game started with (not one a test or another scene loaded later).</summary>
        public static bool IsLaunchScene(Scene scene) =>
            scene.IsValid() && scene.buildIndex >= 0 && (_firstScene == null || _firstScene == scene.path); // a game scene, opened first (tests use their own scene)

        public static TitleScreen Create(RealmkeeperBootstrap bootstrap)
        {
            var go = new GameObject("Title Screen");
            go.SetActive(false);
            go.transform.SetParent(bootstrap.transform, false);
            var title = go.AddComponent<TitleScreen>();
            title._bootstrap = bootstrap;
            title._document = go.AddComponent<UIDocument>();
            title._document.panelSettings = RkSettings.Panel;
            title._document.sortingOrder = 100;
            go.SetActive(true);
            title._store = new JsonWorldStore(bootstrap.Runtime.SaveRoot); // M1.1: the runtime's folder (a test may point it elsewhere)
            title.Build();
            title.Hide();
            return title;
        }

        /// <summary>
        /// Opens the screen. Launch: "Continue" loads the most recently played save; in a game: "Resume" closes it, and
        /// the world's clock runs again if it was running. M1.1: <paramref name="page"/> opens a page at once ("save", "load").
        /// </summary>
        public void Open(bool launch, string page = null)
        {
            if (!IsOpen) _resumeOnClose = !launch && _bootstrap.Playback != null && _bootstrap.Playback.IsPlaying;
            _launch = launch;
            _bootstrap.Playback?.Pause();
            if (_bootstrap.Hud != null) _bootstrap.Hud.SetVisible(false);
            Root.style.display = DisplayStyle.Flex;
            RkMusic.TitleOpen = true; // 2026-11-15 audio pass
            RefreshMenu();
            ShowPage(page);
            ContinueButton.Focus();
        }

        private bool _resumeOnClose;

        public void Hide()
        {
            Settings?.Close();
            Root.style.display = DisplayStyle.None;
            RkMusic.TitleOpen = false;
            if (_bootstrap != null && _bootstrap.Hud != null) _bootstrap.Hud.SetVisible(_bootstrap.StrategicVisible);
        }

        private void Close()
        {
            Hide();
            ReleaseThumbnails();
            if (_resumeOnClose && _bootstrap.Runtime.Host.IsActive) _bootstrap.Playback?.Play(); // M1.1: back to the game as it was
            _resumeOnClose = false;
            Closed?.Invoke();
        }

        // ----- Layout ----------------------------------------------------------------------------------

        private void Build()
        {
            var doc = _document.rootVisualElement;
            Root = new VisualElement { name = "title-root" };
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.backgroundColor = new Color(0.06f, 0.05f, 0.04f, 0.62f);
            doc.Add(Root);
            RkTooltips.Attach(doc);
            RkRoot.Attach(doc); // Phase 25: text size, contrast, motion, width classes
            RkUiSound.Attach(doc);

            // Left: the wordmark and the menu, on a darker band so the text reads over any scenery.
            var left = new VisualElement { name = "title-menu" };
            left.style.width = 420;
            left.style.paddingLeft = 64;
            left.style.paddingRight = 32;
            left.style.justifyContent = Justify.Center;
            left.style.backgroundColor = new Color(0.07f, 0.055f, 0.04f, 0.86f);
            left.style.borderRightWidth = 1;
            left.style.borderRightColor = new Color(0.42f, 0.35f, 0.26f, 0.6f);
            Root.Add(left);
            doc.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var narrow = doc.resolvedStyle.width < RkRoot.CompactWidth;
                left.style.width = narrow ? 340 : 420;
                left.style.paddingLeft = narrow ? 36 : 64;
                var wordmark = left.Q<Label>("title-wordmark");
                if (wordmark != null) { wordmark.style.fontSize = narrow ? 34 : 44; wordmark.style.letterSpacing = narrow ? 3 : 6; }
            }); // Phase 25: narrow screens keep room for the page

            var mark = new Label("REALMKEEPER") { name = "title-wordmark" };
            mark.AddToClassList("rk-title");
            mark.style.fontSize = 44;
            mark.style.letterSpacing = 6;
            left.Add(mark);
            var sub = new Label("A living world, kept by your choices");
            sub.AddToClassList("rk-serif-italic");
            sub.style.fontSize = 18;
            sub.style.color = new Color(0.76f, 0.71f, 0.61f);
            sub.style.marginBottom = 8;
            left.Add(sub);
            var rule = new VisualElement();
            rule.AddToClassList("rk-rule");
            rule.style.marginBottom = 18;
            left.Add(rule);

            // M1.1 (ADR-0123): the release menu.
            ContinueButton = MenuButton(left, "title-continue", "Continue", "play", OnContinue);
            NewGameButton = MenuButton(left, "title-newgame", "New Game", "newworld", () => ShowPage("newgame"));
            LoadButton = MenuButton(left, "title-load", "Load Game", "load", () => ShowPage("load"));
            SaveGameButton = MenuButton(left, "title-save", "Save Game", "save", () => ShowPage("save"));
            SettingsButton = MenuButton(left, "title-settings", "Settings", "settings", OpenSettings);
            CreditsButton = MenuButton(left, "title-credits", "Credits", "credits", () => ShowPage("credits"));
            ExitButton = MenuButton(left, "title-exit", "Exit Game", "exit", OnExit);

            // Developer entries: named worlds from a seed, and campaigns (editor and development builds only).
            if (DeveloperBuild)
            {
                var dev = new Label("Developer") { name = "title-developer" };
                dev.AddToClassList("rk-hint");
                dev.style.marginTop = 12;
                left.Add(dev);
                NewWorldButton = MenuButton(left, "title-new", "New world (name and seed)", "newworld", () => ShowPage("new"));
                CampaignsButton = MenuButton(left, "title-campaigns", "Campaigns", "campaign", () => ShowPage("campaigns"));
            }

            Message = new Label { name = "title-message" };
            Message.AddToClassList("rk-hint");
            Message.style.marginTop = 14;
            Message.style.minHeight = 40;
            left.Add(Message);

            var version = new Label("Pre-release build · Unity 6 · " + DateTime.Now.Year);
            version.AddToClassList("rk-hint");
            version.style.position = Position.Absolute;
            version.style.bottom = 18;
            version.style.left = 64;
            left.Add(version);

            // Right: the page for the chosen entry.
            var right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.alignItems = Align.Center;
            right.style.justifyContent = Justify.Center;
            right.style.paddingLeft = right.style.paddingRight = 48;
            Root.Add(right);
            Content = new VisualElement { name = "title-page" };
            Content.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            Content.style.maxWidth = 860;
            Content.style.maxHeight = new StyleLength(new Length(86, LengthUnit.Percent));
            right.Add(Content);

            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape || Settings != null && Settings.IsOpen) return;
                if (Page != null) ShowPage(null);
                else if (!_launch) Close();
                e.StopPropagation();
            });
        }

        private static Button MenuButton(VisualElement parent, string name, string text, string icon, Action act)
        {
            var b = new Button(act) { name = name, text = text };
            b.AddToClassList("rk-btn");
            b.AddToClassList("rk-btn--menu");
            b.AddToClassList("rk-serif");
            RkIcons.Lead(b, icon, "m");
            parent.Add(b);
            return b;
        }

        private void RefreshMenu()
        {
            // Headers only here (fast); Continue and Load Game check each file's checksum when used.
            var saves = _store.ListSaves(false).Where(s => s.Kind != SaveSlotKind.Other || DeveloperBuild).ToList();
            if (_launch)
            {
                ContinueButton.text = "Continue";
                var newest = saves.Where(s => s.CanLoad).OrderByDescending(s => s.LastPlayedUtc).FirstOrDefault();
                ContinueButton.SetEnabled(newest != null);
                ContinueButton.tooltip = newest != null
                    ? $"Load {SlotLabel(newest)}: {FormatDate(newest.Date)}, played {Local(newest.LastPlayedUtc)}."
                    : "No saved game yet: start a new one.";
                RkIcons.Lead(ContinueButton, "play", "m");
            }
            else
            {
                ContinueButton.text = "Resume";
                ContinueButton.SetEnabled(true);
                ContinueButton.tooltip = "Return to the game as it is.";
            }

            var inGame = !_launch && _bootstrap.Runtime.Host.IsActive;
            SaveGameButton.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
            var blocker = inGame && _bootstrap.Session != null ? _bootstrap.Session.GetSaveBlocker() : null;
            SaveGameButton.tooltip = blocker ?? "Save your game in one of three slots.";
            LoadButton.SetEnabled(saves.Count > 0);
            LoadButton.tooltip = saves.Count > 0 ? $"{saves.Count} save(s)." : "No saved games yet.";
            ExitButton.tooltip = UnityEngine.Application.isEditor ? "Ends play in the built game (the editor keeps running)." : "Close Realmkeeper.";
        }

        // ----- Actions ---------------------------------------------------------------------------------

        private void OnContinue()
        {
            if (!_launch) { Close(); return; }
            ContinueGame(); // M1.1: the most recently played save, falling back on failure
        }

        private void OnExit()
        {
            // Phase 25: leaving asks first, and in a game says what would be lost. M1.1: an autosave is made first when allowed.
            var inGame = !_launch && _bootstrap.Runtime.Host.IsActive;
            var body = !inGame ? "Realmkeeper will close."
                : RkSettings.Autosave ? "Realmkeeper will close. An autosave is made first unless you are in a fight or a conversation, or saved in the last minute; to be sure, use Save Game first."
                : "Realmkeeper will close. Autosave is off: anything since your last save will be lost. Use Save Game first to keep it.";
            RkModal.Confirm(_document.rootVisualElement, "Exit the game?", body, "Exit Game", () =>
            {
                if (inGame) RkHooks.RaiseBeforeLeavingGame("exit");
                if (UnityEngine.Application.isEditor) { Say("Exit closes the built game; in the editor, stop Play mode instead."); return; }
                UnityEngine.Application.Quit();
            }, true);
        }

        private void OpenSettings()
        {
            Settings = new RkSettingsWindow(_document.rootVisualElement, () => SettingsButton.Focus());
        }

        /// <summary>Loads a saved world: into the running session when there is one, otherwise as a new session.</summary>
        public CommandResult LoadSlot(string slot)
        {
            var runtime = _bootstrap.Runtime;
            var result = runtime.Host.IsActive ? runtime.Load(slot) : runtime.Host.StartFromSave(slot);
            if (result.IsSuccess && !runtime.Host.IsActive) result = CommandResult.InvalidState("The world did not start.");
            if (result.IsSuccess) runtime.SelectSlot(slot);
            Say(result.IsSuccess ? $"Loaded '{slot}'." : result.Message);
            if (result.IsSuccess) Close();
            return result;
        }

        /// <summary>Starts a new world with the given name, seed and starting speed; it gets its own save slot.</summary>
        public CommandResult CreateWorld(string worldName, long seed, SimulationSpeed speed)
        {
            worldName = (worldName ?? string.Empty).Trim();
            if (worldName.Length == 0) return Fail("Give the world a name.");
            if (worldName.Length > 40) return Fail("World names are at most 40 characters.");
            var settings = WorldSession.NewGameSettings();
            settings.SimulationSpeed = speed;
            var runtime = _bootstrap.Runtime;
            if (runtime.Host.IsActive) runtime.Host.Shutdown();
            var result = runtime.Host.StartNew(new CreateWorldCommand(worldName, seed, settings));
            if (!result.IsSuccess) return Fail(result.Message);
            var slot = FreeSlot(worldName);
            runtime.SelectSlot(slot);
            var saved = runtime.Save();
            Say(saved.IsSuccess ? $"'{worldName}' was founded and saved as '{slot}'." : $"'{worldName}' was founded, but the first save failed: {saved.Message}");
            Close();
            return result;
        }

        private CommandResult Fail(string message)
        {
            Say(message);
            return CommandResult.Invalid(message);
        }

        /// <summary>A save slot from the world name ("The Vale" → "the-vale"), numbered when taken.</summary>
        public string FreeSlot(string worldName)
        {
            var chars = worldName.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '-').ToArray();
            var baseName = new string(chars).Trim('-');
            while (baseName.Contains("--")) baseName = baseName.Replace("--", "-");
            if (baseName.Length == 0) baseName = "world";
            if (baseName.Length > 40) baseName = baseName.Substring(0, 40).Trim('-');
            var slot = baseName;
            for (var n = 2; _store.Exists(slot) || slot == SaveSlot.DefaultName; n++) slot = baseName + "-" + n;
            return slot;
        }

        private void Say(string text) => Message.text = text ?? string.Empty;

        // ----- Pages -----------------------------------------------------------------------------------

        public void ShowPage(string page)
        {
            if (page == "worlds") page = "load"; // Phase 19's name for the saves page
            Page = page;
            Content.Clear();
            ReleaseThumbnails();
            switch (page)
            {
                case "load": BuildLoadGame(); break;
                case "save": BuildSaveGame(); break;
                case "newgame": BuildNewGame(); break;
                case "new": BuildNewWorld(); break;
                case "campaigns": BuildCampaigns(); break;
                case "credits": BuildCredits(); break;
            }
        }

        private VisualElement Panel(string titleText, string icon)
        {
            var panel = new VisualElement();
            panel.AddToClassList("rk-window");
            panel.style.paddingLeft = panel.style.paddingRight = 28;
            panel.style.paddingTop = panel.style.paddingBottom = 22;
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("rk-frame");
            panel.Add(frame);
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            var i = RkIcons.Make(icon, "l", "accent");
            i.style.marginRight = 12;
            head.Add(i);
            var t = new Label(titleText);
            t.AddToClassList("rk-title");
            head.Add(t);
            panel.Add(head);
            var rule = new VisualElement();
            rule.AddToClassList("rk-rule");
            panel.Add(rule);
            Content.Add(panel);
            return panel;
        }

        private void ConfirmDelete(string slot, string worldName)
        {
            RkModal.Confirm(_document.rootVisualElement, "Delete this world?",
                $"'{worldName}' (slot '{slot}') will be deleted from this computer. This cannot be undone. A world that is running now keeps running, but its save file is gone.",
                "Delete world", () =>
                {
                    var r = _store.Delete(slot);
                    Say(r.IsSuccess ? $"Deleted '{worldName}'." : r.Error);
                    RefreshMenu();
                    ShowPage("worlds");
                }, danger: true);
        }

        private void BuildNewWorld()
        {
            var panel = Panel("Found a new world", "newworld");
            var intro = new Label("A new world is generated from its name and seed: six connected regions of settlements, roads, peoples and factions, with a history that runs while you play. The same seed always makes the same world.");
            intro.AddToClassList("rk-wrap");
            intro.AddToClassList("rk-secondary");
            panel.Add(intro);

            var name = new TextField("World name") { name = "new-world-name", value = "The Vale of Ash", maxLength = 40 };
            var seedRow = new VisualElement();
            seedRow.style.flexDirection = FlexDirection.Row;
            seedRow.style.alignItems = Align.FlexEnd;
            var seed = new TextField("Seed") { name = "new-world-seed", value = NewSeed().ToString(CultureInfo.InvariantCulture), maxLength = 18 };
            seed.style.flexGrow = 1;
            var reroll = new Button(() => seed.value = NewSeed().ToString(CultureInfo.InvariantCulture)) { name = "new-world-reroll", text = "New seed" };
            reroll.AddToClassList("rk-btn");
            RkIcons.Lead(reroll, "dice");
            seedRow.Add(seed);
            seedRow.Add(reroll);
            var speeds = new[] { SimulationSpeed.Slow, SimulationSpeed.Normal, SimulationSpeed.Fast, SimulationSpeed.VeryFast };
            var speed = new DropdownField("Starting speed", speeds.Select(s => s == SimulationSpeed.VeryFast ? "Very fast" : s.ToString()).ToList(), 1) { name = "new-world-speed" };

            panel.Add(Spaced(name));
            panel.Add(Spaced(seedRow));
            panel.Add(Spaced(speed));
            var defaults = WorldSession.NewGameSettings();
            var facts = new Label($"Starts on {defaults.StartingDate} · {defaults.Regions} connected regions · all simulation systems on · you can change the speed at any time.");
            facts.AddToClassList("rk-hint");
            panel.Add(facts);
            var problem = new Label { name = "new-world-problem" };
            problem.AddToClassList("rk-hint");
            problem.style.color = new Color(0.89f, 0.48f, 0.39f);
            panel.Add(problem);

            var buttons = new VisualElement();
            buttons.AddToClassList("rk-modal-buttons");
            var cancel = new Button(() => ShowPage(null)) { text = "Back" };
            cancel.AddToClassList("rk-btn");
            var go = new Button(() =>
            {
                if (!long.TryParse(seed.value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seedValue)) { problem.text = "The seed must be a whole number."; return; }
                var begin = (Action)(() => { var r = CreateWorld(name.value, seedValue, speeds[Mathf.Clamp(speed.index, 0, speeds.Length - 1)]); if (!r.IsSuccess) problem.text = r.Message; });
                if (_bootstrap.Runtime.Host.IsActive && !_launch)
                    RkModal.Confirm(_document.rootVisualElement, "Leave this world?",
                        $"The world you are playing stays as it was last saved (slot '{_bootstrap.Runtime.ActiveSlot}'). Anything since that save is lost.",
                        "Found the new world", begin);
                else begin();
            }) { name = "new-world-create", text = "Found this world" };
            go.AddToClassList("rk-btn");
            go.AddToClassList("rk-btn--primary");
            name.RegisterValueChangedCallback(e => { var ok = e.newValue.Trim().Length > 0; go.SetEnabled(ok); problem.text = ok ? string.Empty : "Give the world a name."; });
            buttons.Add(cancel);
            buttons.Add(go);
            panel.Add(buttons);
        }

        private void BuildCampaigns()
        {
            var panel = Panel("Campaigns", "campaign");
            var session = _bootstrap.Session;
            if (session == null || !session.HasWorld)
            {
                var none = new Label("Campaigns run inside a world. Found or load a world first.");
                none.AddToClassList("rk-empty");
                panel.Add(none);
                return;
            }

            var intro = new Label($"A campaign is an authored story that runs inside '{session.GetOverview().WorldName}'. Without one, the world is open-ended: its people and factions still make their own history.");
            intro.AddToClassList("rk-wrap");
            intro.AddToClassList("rk-secondary");
            panel.Add(intro);
            var view = session.GetCampaigns();
            foreach (var c in view.Campaigns)
            {
                var card = new VisualElement();
                card.AddToClassList("rk-card");
                var t = new Label(c.Title);
                t.AddToClassList("rk-h2");
                card.Add(t);
                var state = new Label($"{c.State}{(string.IsNullOrEmpty(c.CurrentChapter) ? string.Empty : " · " + c.CurrentChapter)}{(string.IsNullOrEmpty(c.Started) ? string.Empty : " · begun " + c.Started)}");
                state.AddToClassList("rk-badge");
                state.AddToClassList("rk-badge--info");
                state.style.alignSelf = Align.FlexStart;
                card.Add(state);
                var s = new Label(c.Summary);
                s.AddToClassList("rk-wrap");
                s.AddToClassList("rk-secondary");
                card.Add(s);
                panel.Add(card);
            }

            foreach (var o in view.Offers)
            {
                var card = new VisualElement { name = "campaign-offer-" + o.CampaignId };
                card.AddToClassList("rk-card");
                var t = new Label(o.Title + (o.Provisional ? "  (provisional)" : string.Empty));
                t.AddToClassList("rk-h2");
                card.Add(t);
                var s = new Label(o.Summary);
                s.AddToClassList("rk-wrap");
                s.AddToClassList("rk-secondary");
                card.Add(s);
                var id = o.CampaignId;
                var start = new Button(() =>
                {
                    var r = session.Dispatch(new StartCampaignCommand(id));
                    Say(r.Message);
                    if (r.IsSuccess) ShowPage("campaigns");
                }) { name = "campaign-start-" + id, text = "Begin this campaign" };
                start.AddToClassList("rk-btn");
                start.AddToClassList("rk-btn--primary");
                start.style.alignSelf = Align.FlexStart;
                start.style.marginTop = 8;
                start.SetEnabled(o.CanStart);
                if (!o.CanStart) { start.tooltip = o.Why; var why = new Label("Cannot begin here: " + o.Why); why.AddToClassList("rk-hint"); card.Add(why); }
                card.Add(start);
                panel.Add(card);
            }

            if (view.Campaigns.Count == 0 && view.Offers.Count == 0)
            {
                var none = new Label("No campaign is available in this world.");
                none.AddToClassList("rk-empty");
                panel.Add(none);
            }
        }

        private void BuildCredits()
        {
            var panel = Panel("Credits", "credits");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "title-credits-text" };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(scroll);
            void Para(string text, string cls = "rk-secondary")
            {
                var l = new Label(text);
                l.AddToClassList("rk-wrap");
                l.AddToClassList(cls);
                l.style.marginBottom = 8;
                scroll.Add(l);
            }

            void Head(string text)
            {
                var l = new Label(text);
                l.AddToClassList("rk-h2");
                l.style.marginTop = 8;
                scroll.Add(l);
            }

            Head("Rules");
            Para(Credits.SrdNotice);
            Para(Credits.Srd51Notice);
            Para(Credits.RulesUse, "rk-hint");
            Head("Art, icons and fonts");
            foreach (var a in Credits.Assets) Para($"{a.Work} — {a.Author} — {a.Licence}");
        }

        // ----- Helpers ---------------------------------------------------------------------------------

        private static VisualElement Spaced(VisualElement e)
        {
            e.style.marginTop = 10;
            return e;
        }

        private static long NewSeed() => (long)(UnityEngine.Random.value * 899999999) + 100000000;

        private static string Local(DateTime utc) => utc.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

        /// <summary>"0412-03-01" → "1 Seedtide, Year 412" (the game's calendar).</summary>
        public static string FormatDate(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "date unknown";
            var parts = stored.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m) && int.TryParse(parts[2], out var d) && WorldDate.IsValid(y, m, d))
                return new WorldDate(y, m, d).ToString();
            return stored;
        }
    }
}
