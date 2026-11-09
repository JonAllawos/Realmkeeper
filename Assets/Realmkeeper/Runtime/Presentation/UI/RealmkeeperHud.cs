using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
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
        public const int EventCount = 8;

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
        public Label MessageLabel { get; private set; }

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
            MessageLabel = Root.Q<Label>("message");

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

        private void Save() => ShowMessage(_session.Execute(new SaveWorldCommand(SaveSlot)));

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
            Root.Q<Label>("insp-type").text = s.SettlementType.ToString();
            InspectorPopulation.text = s.Population.ToString("N0", CultureInfo.InvariantCulture);
            Root.Q<Label>("insp-faction").text = string.IsNullOrEmpty(s.FactionName) ? "Independent" : s.FactionName;
            SetBar("insp-prosperity", s.Prosperity);
            SetBar("insp-stability", s.Stability);

            InspectorFood.text = s.FoodReserves.ToString("N0", CultureInfo.InvariantCulture);
            var net = Root.Q<Label>("insp-food-net");
            net.text = (s.NetFoodPerDay > 0 ? "+" : "") + s.NetFoodPerDay.ToString("N0", CultureInfo.InvariantCulture);
            net.EnableInClassList("rk-value--negative", s.NetFoodPerDay < 0);
            net.EnableInClassList("rk-value--positive", s.NetFoodPerDay > 0);
            var days = s.DaysOfFoodRemaining;
            Root.Q<Label>("insp-food-days").text = days.HasValue ? $"{days.Value} days" : "Stable";
            Root.Q<Label>("insp-shortage").EnableInClassList("rk-hidden", !s.IsInFoodShortage);

            var resources = Root.Q<VisualElement>("insp-resources");
            resources.Clear();
            foreach (var stack in s.Resources)
            {
                var row = new VisualElement();
                row.AddToClassList("rk-row");
                var key = new Label(DisplayName(stack.ResourceTypeId.Value));
                key.AddToClassList("rk-key");
                var value = new Label(stack.Quantity.ToString("N0", CultureInfo.InvariantCulture));
                value.AddToClassList("rk-value");
                row.Add(key);
                row.Add(value);
                resources.Add(row);
            }
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
                row.AddToClassList(e.Severity == EventSeverity.Critical ? "rk-sev-critical" : e.Severity == EventSeverity.Warning ? "rk-sev-warning" : "rk-sev-info");
                var date = new Label(e.Date.ToString());
                date.AddToClassList("rk-event-date");
                var text = new Label(e.Description);
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

        private static string LabelFor(SimulationSpeed speed)
        {
            foreach (var s in Speeds)
            {
                if (s.speed == speed) return s.label;
            }

            return speed.ToString();
        }

        /// <summary>Presentation-side display names for resource keys (content keys are not shown raw).</summary>
        private static string DisplayName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "-";
            return char.ToUpperInvariant(key[0]) + key.Substring(1).Replace('_', ' ').Replace('.', ' ');
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
