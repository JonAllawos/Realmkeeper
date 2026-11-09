using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// UI Toolkit interface: top bar (world, date, time controls, speed, save/load), left
    /// navigation with the settlement list, right inspector for the selected settlement and a
    /// bottom activity area. Reads only query snapshots and sends commands. It refreshes from
    /// <see cref="WorldSession.WorldChanged"/>, playback and selection events, never by polling.
    /// </summary>
    public sealed class RealmkeeperHud : MonoBehaviour
    {
        public const string HudResource = "RealmkeeperHud";
        public const string ThemeResource = "RealmkeeperTheme";
        public const string PanelSettingsResource = "RealmkeeperPanelSettings";
        public const string SaveSlot = "autosave";
        /// <summary>Rows that fit the bottom panel at the reference resolution.</summary>
        public const int EventCount = 7;

        private static readonly string[] ComingLater = { "Factions", "Characters", "Adventures", "Chronicle", "World Editor" };

        private static readonly (SimulationSpeed speed, string label)[] Speeds =
        {
            (SimulationSpeed.Slow, "Slow"),
            (SimulationSpeed.Normal, "Normal"),
            (SimulationSpeed.Fast, "Fast"),
            (SimulationSpeed.VeryFast, "Very fast"),
        };

        private readonly Dictionary<SettlementId, Button> _settlementButtons = new Dictionary<SettlementId, Button>();

        private WorldSession _session;
        private SimulationPlayback _playback;
        private SettlementSelection _selection;
        private IsometricCameraController _camera;
        private WorldView _worldView;
        private UIDocument _document;
        private PanelSettings _ownedPanelSettings;
        private WorldDate _startDate;
        private bool _updatingSpeed;

        public VisualElement Root { get; private set; }

        public Label DateLabel { get; private set; }
        public Label WorldNameLabel { get; private set; }
        public Button PlayPauseButton { get; private set; }
        public Button AdvanceDayButton { get; private set; }
        public Button AdvanceWeekButton { get; private set; }
        public Button AdvanceMonthButton { get; private set; }
        public Button SaveButton { get; private set; }
        public Button LoadButton { get; private set; }
        public DropdownField SpeedField { get; private set; }
        public VisualElement Inspector { get; private set; }
        public Label InspectorName { get; private set; }
        public Label InspectorPopulation { get; private set; }
        public Label InspectorFood { get; private set; }
        public Label InspectorStatus { get; private set; }

        /// <summary>Economy table rows (one per resource) for the selected settlement.</summary>
        public VisualElement EconomyTable => Root?.Q<VisualElement>("insp-economy");

        /// <summary>Trade route lines for the selected settlement.</summary>
        public VisualElement TradeList => Root?.Q<VisualElement>("insp-trade");

        /// <summary>Shortage warning lines for the selected settlement.</summary>
        public VisualElement WarningList => Root?.Q<VisualElement>("insp-warnings");

        /// <summary>The recent-events list in the bottom panel.</summary>
        public VisualElement EventList => Root?.Q<VisualElement>("event-list");
        public Label MessageLabel { get; private set; }

        /// <summary>"Enter settlement" in the inspector: sends the player character into the selected settlement.</summary>
        public Button EnterSettlementButton { get; private set; }

        /// <summary>Saves through the runtime (so exploration can record the character first). Null: the session saves directly.</summary>
        public Func<string, CommandResult> SaveHandler { get; set; }

        /// <summary>False while the exploration view is shown.</summary>
        public bool IsVisible => Root == null || Root.style.display != DisplayStyle.None;

        /// <summary>Hides the HUD without disabling the document (re-enabling a UIDocument would rebuild its tree).</summary>
        public void SetVisible(bool visible)
        {
            if (Root != null) Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) RefreshAll();
        }

        public bool InspectorVisible => Inspector != null && !Inspector.ClassListContains("rk-hidden");

        public static RealmkeeperHud Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<RealmkeeperHud>();
            hud._document = go.AddComponent<UIDocument>();
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            if (panelSettings == null)
            {
                // Fallback (Unity warns about the missing theme until it is assigned below).
                panelSettings = hud._ownedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "Realmkeeper Runtime Panel";
                panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
            }

            hud._document.panelSettings = panelSettings;
            hud._document.visualTreeAsset = Resources.Load<VisualTreeAsset>(HudResource);
            go.SetActive(true);
            return hud;
        }

        public void Initialize(WorldSession session, SimulationPlayback playback, SettlementSelection selection,
            IsometricCameraController cameraController, WorldView worldView)
        {
            _session = session;
            _playback = playback;
            _selection = selection;
            _camera = cameraController;
            _worldView = worldView;

            BindElements();

            _session.WorldChanged += OnWorldChanged;
            _playback.PlayStateChanged += OnPlayStateChanged;
            _selection.SelectionChanged += OnSelectionChanged;

            if (_session.HasWorld) _startDate = _session.GetOverview().Date;
            RefreshAll();
        }

        /// <summary>True when a screen position (origin bottom-left) is over an interactive HUD element.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panel = Root?.panel;
            if (panel == null) return false;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            return picked != null && picked != Root && picked != _document.rootVisualElement;
        }

        // ----- Binding ---------------------------------------------------------------------

        private void BindElements()
        {
            var documentRoot = _document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;
            Root = documentRoot.Q<VisualElement>("hud-root");
            if (Root == null)
            {
                Debug.LogError($"Realmkeeper: HUD layout '{HudResource}' could not be loaded from Resources.");
                return;
            }

            // The template container wraps the layout; it must fill the screen and not block clicks.
            foreach (var child in documentRoot.Children())
            {
                child.pickingMode = PickingMode.Ignore;
                child.style.flexGrow = 1;
            }

            WorldNameLabel = Root.Q<Label>("world-name");
            DateLabel = Root.Q<Label>("date");
            PlayPauseButton = Root.Q<Button>("play-pause");
            AdvanceDayButton = Root.Q<Button>("advance-day");
            AdvanceWeekButton = Root.Q<Button>("advance-week");
            AdvanceMonthButton = Root.Q<Button>("advance-month");
            SaveButton = Root.Q<Button>("save");
            LoadButton = Root.Q<Button>("load");
            SpeedField = Root.Q<DropdownField>("speed");
            Inspector = Root.Q<VisualElement>("inspector");
            InspectorName = Root.Q<Label>("insp-name");
            InspectorPopulation = Root.Q<Label>("insp-population");
            InspectorFood = Root.Q<Label>("insp-food");
            InspectorStatus = Root.Q<Label>("insp-status");
            MessageLabel = Root.Q<Label>("message");
            EnterSettlementButton = Root.Q<Button>("insp-enter");
            EnterSettlementButton.clicked += () => EnterSelectedSettlement();

            PlayPauseButton.clicked += () => _playback.Toggle();
            AdvanceDayButton.clicked += () => Advance(1);
            AdvanceWeekButton.clicked += () => Advance(WorldDate.DaysPerWeek);
            AdvanceMonthButton.clicked += () => Advance(WorldDate.DaysPerMonth);
            SaveButton.clicked += Save;
            LoadButton.clicked += Load;
            Root.Q<Button>("insp-close").clicked += () => _selection.Clear();

            var speedLabels = new List<string>();
            foreach (var s in Speeds) speedLabels.Add(s.label);
            SpeedField.choices = speedLabels;
            SpeedField.RegisterValueChangedCallback(OnSpeedChanged);

            var list = Root.Q<ScrollView>("settlement-list");
            Root.Q<Button>("nav-world").clicked += () =>
            {
                _selection.Clear();
                _camera.FrameRegion(_worldView.Bounds, _worldView.Map != null ? _worldView.Map.WaterWorldHeight : 0f);
            };
            Root.Q<Button>("nav-settlements").clicked += () => list.ToggleInClassList("rk-hidden");

            var later = Root.Q<VisualElement>("nav-later");
            foreach (var name in ComingLater)
            {
                var item = new Button { text = name };
                item.AddToClassList("rk-nav-item");
                var tag = new Label("Coming later");
                tag.AddToClassList("rk-tag");
                item.Add(tag);
                item.SetEnabled(false);
                later.Add(item);
            }
        }

        // ----- Commands --------------------------------------------------------------------

        private void Advance(int days)
        {
            var result = days == 1 ? _session.Execute(new AdvanceDayCommand()) : _session.Execute(new AdvanceDaysCommand(days));
            if (!result.IsSuccess) ShowMessage(result);
        }

        private void Save() => ShowMessage(SaveHandler != null ? SaveHandler(SaveSlot) : _session.Execute(new SaveWorldCommand(SaveSlot)));

        /// <summary>Dispatches <see cref="EnterSettlementCommand"/> for the selected settlement; the view switches when world data says the character is inside.</summary>
        public CommandResult EnterSelectedSettlement()
        {
            var result = _selection.HasSelection
                ? _session.Dispatch(new EnterSettlementCommand(_selection.SelectedId))
                : CommandResult.Invalid("Select a settlement first.");
            if (!result.IsSuccess) ShowMessage(result);
            return result;
        }

        private void Load()
        {
            _playback.Pause();
            ShowMessage(_session.Execute(new LoadWorldCommand(SaveSlot)));
        }

        private void OnSpeedChanged(ChangeEvent<string> evt)
        {
            if (_updatingSpeed) return;
            foreach (var s in Speeds)
            {
                if (s.label != evt.newValue) continue;
                var result = _session.Execute(new SetSimulationSpeedCommand(s.speed));
                if (!result.IsSuccess) ShowMessage(result);
                return;
            }
        }

        private void ShowMessage(CommandResult result)
        {
            if (MessageLabel == null) return;
            var text = result.Message;
            if (result.Errors.Count > 0) text += " " + string.Join(" ", result.Errors);
            MessageLabel.text = text;
            MessageLabel.EnableInClassList("rk-message--error", !result.IsSuccess);
        }

        // ----- Events ----------------------------------------------------------------------

        private void OnWorldChanged(WorldChange change)
        {
            if (change.GeographyChanged && _session.HasWorld) _startDate = _session.GetOverview().Date;
            if (change.GeographyChanged) RebuildSettlementList();
            RefreshTopBar();
            RefreshInspector();
            RefreshActivity();
        }

        private void OnPlayStateChanged(bool playing)
        {
            RefreshTopBar();
            RefreshActivity();
        }

        private void OnSelectionChanged(SettlementId id)
        {
            foreach (var pair in _settlementButtons) pair.Value.EnableInClassList("rk-settlement-item--selected", pair.Key == id);
            RefreshInspector();
        }

        // ----- Refresh ---------------------------------------------------------------------

        private void RefreshAll()
        {
            if (Root == null) return;
            RebuildSettlementList();
            RefreshTopBar();
            RefreshInspector();
            RefreshActivity();
        }

        private void RefreshTopBar()
        {
            if (Root == null) return;
            var overview = _session.GetOverview();
            WorldNameLabel.text = overview?.WorldName ?? "No world";
            DateLabel.text = overview != null ? overview.Date.ToString() : "-";
            PlayPauseButton.text = _playback.IsPlaying ? "Pause" : "Play";

            if (overview != null)
            {
                _updatingSpeed = true;
                SpeedField.SetValueWithoutNotify(LabelFor(overview.Speed));
                _updatingSpeed = false;
            }
        }

        private void RebuildSettlementList()
        {
            if (Root == null) return;
            var list = Root.Q<ScrollView>("settlement-list");
            list.Clear();
            _settlementButtons.Clear();
            foreach (var s in _session.GetSettlements())
            {
                var id = s.SettlementId;
                var item = new Button(() =>
                {
                    _selection.Select(id);
                    var view = _worldView.FindSettlementView(id);
                    if (view != null) _camera.FocusOn(view.Center);
                }) { text = s.Name };
                item.AddToClassList("rk-settlement-item");
                var type = new Label(s.SettlementType.ToString());
                type.AddToClassList("rk-settlement-type");
                item.Add(type);
                item.EnableInClassList("rk-settlement-item--selected", id == _selection.SelectedId);
                list.Add(item);
                _settlementButtons[id] = item;
            }
        }

        private void RefreshInspector()
        {
            if (Root == null) return;
            var s = _selection.HasSelection ? _session.FindSettlement(_selection.SelectedId) : null;
            Inspector.EnableInClassList("rk-hidden", s == null);
            if (s == null) return;

            InspectorName.text = s.Name;
            var region = _session.FindRegion(s.RegionId);
            Root.Q<Label>("insp-region").text = region != null ? region.Name : "No region";
            var specialization = s.Specialization.ToString();
            if (s.SecondarySpecialization != SettlementSpecialization.None) specialization += " · " + s.SecondarySpecialization;
            Root.Q<Label>("insp-type").text = $"{s.SettlementType} — {specialization}";
            InspectorPopulation.text = N(s.Population);
            Root.Q<Label>("insp-workers").text = $"{N(s.Employed)} of {N(s.Workforce)}";
            Root.Q<Label>("insp-faction").text = string.IsNullOrEmpty(s.FactionName) ? "Independent" : s.FactionName;

            InspectorStatus.text = StatusText(s.Status);
            foreach (EconomicStatus status in Enum.GetValues(typeof(EconomicStatus)))
                InspectorStatus.EnableInClassList("rk-status--" + status.ToString().ToLowerInvariant(), status == s.Status);
            Root.Q<Label>("insp-shortage").EnableInClassList("rk-hidden", !s.IsInFoodShortage);

            SetBar("insp-prosperity", s.Prosperity);
            SetBar("insp-stability", s.Stability);
            SetTrend("insp-prosperity-trend", s.ProsperityTrend, s.ProsperityTarget);
            SetTrend("insp-stability-trend", s.StabilityTrend, s.StabilityTarget);

            // Food at a glance.
            var food = s.Food;
            InspectorFood.text = N(food.Stock);
            Root.Q<Label>("insp-food-produced").text = N(food.Produced);
            Root.Q<Label>("insp-food-consumed").text = N(food.Demand);
            SetSigned(Root.Q<Label>("insp-food-net"), s.NetFoodPerDay);
            var days = s.DaysOfFoodRemaining;
            Root.Q<Label>("insp-food-days").text = days.HasValue ? $"{N(days.Value)} days" : "Not running down";
            Root.Q<Label>("insp-food-days").EnableInClassList("rk-value--negative", days.HasValue && days.Value < 30);

            RefreshWarnings(s);
            RefreshEconomyTable(s);
            RefreshProduction(s);
            RefreshTrade(s);
        }

        /// <summary>Shortage warnings, one line each, most urgent first.</summary>
        private void RefreshWarnings(SettlementSummary s)
        {
            var warnings = Root.Q<VisualElement>("insp-warnings");
            warnings.Clear();
            void Add(string text)
            {
                var label = new Label(text);
                label.AddToClassList("rk-warning");
                warnings.Add(label);
            }

            if (s.DaysWithoutFood > 0) Add($"No food for {s.DaysWithoutFood} day(s): people are going hungry.");
            foreach (var row in s.Economy)
            {
                if (!row.InShortage) continue;
                var name = EventFormatter.ResourceName(row.ResourceTypeId.Value);
                Add(row.Shortfall > 0
                    ? $"{name} shortage: {N(row.Shortfall)} of {N(row.Demand)} needed went unmet."
                    : $"{name} running low: {N(row.Stock)} left at {N(row.Demand)} a day.");
            }
        }

        private void RefreshEconomyTable(SettlementSummary s)
        {
            var table = Root.Q<VisualElement>("insp-economy");
            table.Clear();
            foreach (var r in s.Economy)
            {
                var row = new VisualElement();
                row.AddToClassList("rk-table-row");
                row.EnableInClassList("rk-table-row--short", r.InShortage);
                row.Add(Cell(EventFormatter.ResourceName(r.ResourceTypeId.Value), "rk-col-name"));
                var capacity = r.Capacity == int.MaxValue ? string.Empty : "/" + Compact(r.Capacity);
                row.Add(Cell(Compact(r.Stock) + capacity, "rk-col-num", "rk-col-stock"));
                row.Add(Cell(r.Produced > 0 ? "+" + N(r.Produced) : "0", "rk-col-num"));
                row.Add(Cell(r.Demand > 0 ? "−" + N(r.Consumed) : "0", "rk-col-num"));
                var trade = r.Imported - r.Exported;
                row.Add(SignedCell(trade));
                row.Add(SignedCell(r.NetChange));
                table.Add(row);
            }
        }

        private void RefreshProduction(SettlementSummary s)
        {
            var list = Root.Q<VisualElement>("insp-production");
            list.Clear();
            foreach (var p in s.Production)
            {
                var row = new VisualElement();
                row.AddToClassList("rk-row");
                var favoured = s.LabourFocus.HasValue && s.LabourFocus.Value == p.SourceType ? " · notice posted" : string.Empty;
                row.Add(Cell($"{p.SourceType} ({N(p.Workers)}/{N(p.WorkerCapacity)}){favoured}", "rk-key"));
                row.Add(Cell($"+{N(p.LastOutput)} {EventFormatter.ResourceName(p.Output.Value).ToLowerInvariant()}", "rk-value"));
                list.Add(row);
            }
        }

        private void RefreshTrade(SettlementSummary s)
        {
            var list = Root.Q<VisualElement>("insp-trade");
            list.Clear();
            var routes = _session.GetTradeRoutes(s.SettlementId);
            if (routes.Count == 0)
            {
                list.Add(Cell("No trade routes.", "rk-trade-line"));
                return;
            }

            foreach (var r in routes)
            {
                var outgoing = r.OriginId == s.SettlementId;
                var partner = outgoing ? r.DestinationName : r.OriginName;
                string text;
                if (r.HasShipmentOnRoad)
                {
                    var goods = $"{N(r.InTransitQuantity)} {EventFormatter.ResourceName(r.InTransitResource.Value).ToLowerInvariant()}";
                    text = r.InTransitReturning
                        ? $"{goods} returning from {partner} (no room there)"
                        : r.Status == TradeRouteStatus.Blocked
                            ? $"{goods} waiting at {partner}: storage full"
                            : $"{(outgoing ? "→" : "←")} {partner}: {goods} on the road, {r.DaysUntilArrival} day(s) left";
                }
                else if (r.LastDeliveryDate.HasValue)
                {
                    text = $"{(outgoing ? "→" : "←")} {partner}: last {N(r.LastDeliveryQuantity)} {EventFormatter.ResourceName(r.LastDeliveryResource.Value).ToLowerInvariant()} on {r.LastDeliveryDate.Value} ({N(r.ShipmentsDelivered)} deliveries)";
                }
                else
                {
                    text = $"{(outgoing ? "→" : "←")} {partner}: {(r.Status == TradeRouteStatus.Idle ? "nothing to trade yet" : "no deliveries yet")}";
                }

                var line = Cell(text, "rk-trade-line");
                line.EnableInClassList("rk-trade-line--blocked", r.Status == TradeRouteStatus.Blocked);
                list.Add(line);
            }

            list.Add(Cell($"Trade activity: {N(s.TradeActivity)}", "rk-trade-line"));
        }

        private void SetTrend(string name, RatingTrend trend, int target)
        {
            var label = Root.Q<Label>(name);
            label.text = trend == RatingTrend.Rising ? $"rising toward {target}" : trend == RatingTrend.Falling ? $"falling toward {target}" : "steady";
            label.EnableInClassList("rk-trend--rising", trend == RatingTrend.Rising);
            label.EnableInClassList("rk-trend--falling", trend == RatingTrend.Falling);
        }

        private static void SetSigned(Label label, int value)
        {
            label.text = (value > 0 ? "+" : value < 0 ? "−" : "") + N(Math.Abs(value));
            label.EnableInClassList("rk-value--negative", value < 0);
            label.EnableInClassList("rk-value--positive", value > 0);
        }

        private static Label SignedCell(int value)
        {
            var label = Cell(string.Empty, "rk-col-num");
            SetSigned(label, value);
            if (value == 0) label.text = "0";
            return label;
        }

        private static Label Cell(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var c in classes) label.AddToClassList(c);
            return label;
        }

        private static string StatusText(EconomicStatus status)
        {
            switch (status)
            {
                case EconomicStatus.Starving: return "Starving";
                case EconomicStatus.Shortage: return "Shortage";
                case EconomicStatus.Strained: return "Strained";
                case EconomicStatus.Thriving: return "Thriving";
                default: return "Stable";
            }
        }

        private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Short number for narrow columns: 1,234 → "1.2k", 1,000,000 → "1M".</summary>
        private static string Compact(int value)
        {
            if (value >= 1000000) return (value / 1000000) + "M";
            if (value >= 10000) return (value / 1000) + "k";
            if (value >= 1000) return (value / 100 / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshActivity()
        {
            if (Root == null) return;
            var events = Root.Q<VisualElement>("event-list");
            events.Clear();
            foreach (var e in _session.GetRecentEvents(EventCount))
            {
                var row = new VisualElement();
                row.AddToClassList("rk-event");
                row.AddToClassList(SeverityClass(e.Severity));
                var date = new Label(e.Date.ToString());
                date.AddToClassList("rk-event-date");
                var text = new Label(e.Summary);
                text.AddToClassList("rk-event-text");
                row.Add(date);
                row.Add(text);
                events.Add(row);
            }

            var state = Root.Q<Label>("sim-state");
            state.text = _playback.IsPlaying ? "Playing" : "Paused";
            state.EnableInClassList("rk-sim-state--playing", _playback.IsPlaying);

            var overview = _session.GetOverview();
            Root.Q<Label>("sim-speed").text = overview != null
                ? $"Speed: {LabelFor(overview.Speed)} ({SimulationPlayback.SecondsPerDay(overview.Speed):0.##} s per day)"
                : "-";
            Root.Q<Label>("sim-days").text = overview != null && _startDate.IsValidDate
                ? $"Days advanced: {_startDate.DaysUntil(overview.Date)}"
                : "-";
        }

        private void SetBar(string name, int value)
        {
            var clamped = Mathf.Clamp(value, SettlementData.MinRating, SettlementData.MaxRating);
            Root.Q<VisualElement>(name + "-fill").style.width = Length.Percent(clamped);
            Root.Q<Label>(name).text = $"{clamped} / {SettlementData.MaxRating}";
        }

        private static string SeverityClass(EventSeverity severity)
        {
            switch (severity)
            {
                case EventSeverity.Critical: return "rk-sev-critical";
                case EventSeverity.Warning: return "rk-sev-warning";
                case EventSeverity.Positive: return "rk-sev-positive";
                default: return "rk-sev-info";
            }
        }

        private static string LabelFor(SimulationSpeed speed)
        {
            foreach (var s in Speeds)
            {
                if (s.speed == speed) return s.label;
            }

            return speed.ToString();
        }

        private void OnDestroy()
        {
            if (_session != null) _session.WorldChanged -= OnWorldChanged;
            if (_playback != null) _playback.PlayStateChanged -= OnPlayStateChanged;
            if (_selection != null) _selection.SelectionChanged -= OnSelectionChanged;
            if (_ownedPanelSettings != null) PresentationMaterials.DestroyObject(_ownedPanelSettings);
        }
    }
}
