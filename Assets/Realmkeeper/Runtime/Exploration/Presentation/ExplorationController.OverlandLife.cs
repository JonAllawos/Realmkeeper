using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 24 (ADR-0113, ADR-0114, ADR-0115): the living country in the view. Landmarks stand where the world puts them;
    /// the world's own travelling groups walk its roads (bodies only near the player, pooled); a waiting encounter is put to
    /// the player with its choices; the ground's weather slows the player as it slows the clock, bars flooded fords and
    /// shows on the ground; the HUD names the waypoint, the ground, and the way back onto a waiting journey.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private const float TravellerShowMetres = 600f;
        private const float TravellerDropMetres = 800f;

        private Transform _lifeRoot;
        private readonly List<LandmarkView> _landmarkViews = new List<LandmarkView>();
        private readonly Dictionary<string, TravellerGroupView> _travellerViews = new Dictionary<string, TravellerGroupView>();
        private float _lifeTimer;
        private GroundConditions _ground = GroundConditions.Calm;
        private Label _groundLine;
        private Label _waypointLine;
        private VisualElement _overlandActions;
        private Button _rejoinButton;
        private Button _clearMarkButton;
        private VisualElement _encounterPanel;
        private string _encounterShown;
        private List<WorldSession.LandmarkView> _landmarkStates = new List<WorldSession.LandmarkView>();
        private WaypointView _waypoint;

        public IReadOnlyList<LandmarkView> LandmarkViews => _landmarkViews;
        public IReadOnlyCollection<TravellerGroupView> TravellerViews => _travellerViews.Values;
        public GroundConditions OverlandGround => _ground;
        public bool EncounterPanelShown => _encounterPanel != null && _encounterPanel.style.display == DisplayStyle.Flex;

        /// <summary>Milliseconds the last refresh of the living country took (diagnostics).</summary>
        public float LastLifeRefreshMs { get; private set; }

        private void SetupOverlandLife(OverlandView view)
        {
            _lifeRoot = new GameObject("Living country").transform;
            _lifeRoot.SetParent(EnvironmentRoot.transform, false);
            var stone = _materials.Get(new Color(0.55f, 0.53f, 0.49f));
            var wood = _materials.Get(new Color(0.45f, 0.33f, 0.22f));
            foreach (var l in _session.GetLandmarks())
            {
                var v = LandmarkView.Create(_lifeRoot, l.Landmark, view.Terrain, Catalog, stone, wood);
                v.Searched = l.Searched;
                v.Handler = OnLandmarkChosen;
                _landmarkViews.Add(v);
                _detector.Register(v);
            }

            SetupLifeHud();
            _lifeTimer = 0f;
            _ground = null;
            RefreshOverlandLife(true);
        }

        private void TeardownOverlandLife()
        {
            foreach (var v in _landmarkViews) if (v != null) _detector.Unregister(v);
            foreach (var v in _travellerViews.Values) if (v != null) _detector.Unregister(v);
            _landmarkViews.Clear();
            _travellerViews.Clear();
            _lifeRoot = null;
            _groundLine = _waypointLine = null;
            _overlandActions = _encounterPanel = null;
            _rejoinButton = _clearMarkButton = null;
            _landmarkStates.Clear();
            _waypoint = null;
            _encounterShown = null;
            if (Player != null) Player.SpeedMultiplier = 1f;
        }

        private void UpdateOverlandLife()
        {
            if (_lifeRoot == null) return;
            var keys = Keyboard.current;
            if (keys != null && !WindowOpen && keys.jKey.wasPressedThisFrame) RejoinJourney();
            _lifeTimer -= Time.unscaledDeltaTime;
            if (_lifeTimer > 0f) return;
            _lifeTimer = 1f;
            RefreshOverlandLife(false);
        }

        /// <summary>Once a second: the travellers near the player, the ground, the HUD lines, the waiting encounter.</summary>
        private void RefreshOverlandLife(bool snap)
        {
            if (_lifeRoot == null || _overlandView == null || Player == null) return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var at = Player.transform.position;

            // Travellers: bodies for the groups near the player, where the simulation has them now.
            var seen = new HashSet<string>();
            foreach (var t in _session.GetTravellers())
            {
                var d = Vector2.Distance(new Vector2(t.X, t.Z), new Vector2(at.x, at.z));
                if (_travellerViews.TryGetValue(t.GroupId, out var v))
                {
                    if (d > TravellerDropMetres) continue;
                    v.Place(t, false);
                    seen.Add(t.GroupId);
                }
                else if (d <= TravellerShowMetres)
                {
                    v = TravellerGroupView.Create(_lifeRoot, t, _overlandView.Terrain, Catalog, _materials);
                    v.Handler = OnTravellersChosen;
                    _travellerViews[t.GroupId] = v;
                    _detector.Register(v);
                    seen.Add(t.GroupId);
                }
            }

            foreach (var id in new List<string>(_travellerViews.Keys))
            {
                if (seen.Contains(id)) continue;
                var v = _travellerViews[id];
                _detector.Unregister(v);
                Destroy(v.gameObject);
                _travellerViews.Remove(id);
            }

            // The ground: walking pace, flooded fords, the ground's look; said once when it changes.
            var g = _session.GetGroundConditions();
            if (_ground == null || g.State != _ground.State || g.FordsFlooded != _ground.FordsFlooded || g.Line != _ground.Line)
            {
                _ground = g;
                _overland.SetFordsFlooded(g.FordsFlooded);
                _overland.SetGroundWeather(g.Wetness >= 40 ? Mathf.InverseLerp(40f, 90f, g.Wetness) : 0f, g.Snow >= 10 ? Mathf.InverseLerp(10f, 60f, g.Snow) : 0f);
            }

            var cell = _overlandView.Terrain.CellAt(at.x, at.z);
            Player.SpeedMultiplier = 1f / Mathf.Max(1f, _ground.FactorFor(cell)); // the same factor the clock charges, felt underfoot

            _landmarkStates = _session.GetLandmarks();
            RefreshLifeHud();
            RefreshEncounterPanel();
            LastLifeRefreshMs = (float)clock.Elapsed.TotalMilliseconds;
        }

        // ----- Interactions -------------------------------------------------------------------------------------

        private void OnLandmarkChosen(LandmarkView v)
        {
            var result = _session.Dispatch(new UseLandmarkCommand(v.Landmark.Id));
            Hud?.ShowMessage(result);
            if (!result.IsSuccess) return;
            if (v.Landmark.Searchable)
            {
                v.Searched = true;
                var key = Landmarks.CacheKey(v.Landmark.Id);
                if (_session.GetContainer(key) is ContainerView box && (box.Rows.Count > 0 || box.Coin > 0)) OpenStorage(key);
            }

            RefreshOverlandHud();
        }

        private void OnTravellersChosen(TravellerGroupView v)
        {
            var t = v.Traveller;
            CommandResult result;
            if (v.Talked && t.Kind == TravelGroupKind.Refugees) result = _session.Dispatch(new HelpTravellersCommand(t.GroupId));
            else
            {
                result = _session.Dispatch(new TalkToTravellersCommand(t.GroupId));
                if (result.IsSuccess) v.Talked = true;
            }

            Hud?.ShowMessage(result);
        }

        /// <summary>Back onto the waiting journey (J, or the button), when standing where the road was left.</summary>
        public CommandResult RejoinJourney()
        {
            if (_session == null || !InOverland) return CommandResult.InvalidState("Not in the country.");
            if (_session.GetHaltedJourneyPoint() == null) { var none = CommandResult.InvalidState("No journey waits on the road here."); Hud?.ShowMessage(none); return none; }
            RecordPose();
            Travel("The road", "Taking to the road again…", () => _session.Dispatch(new RejoinJourneyCommand()));
            return CommandResult.Ok();
        }

        /// <summary>Marks a known destination on the compass (or clears it with null).</summary>
        public CommandResult MarkWaypoint(string key)
        {
            var r = _session.Dispatch(new SetWaypointCommand(key));
            Hud?.ShowMessage(r);
            RefreshLifeHud();
            return r;
        }

        /// <summary>Answers the waiting encounter (the panel's buttons).</summary>
        public CommandResult AnswerEncounter(OverlandChoice choice)
        {
            var pending = _session.GetPendingOverlandEncounter();
            if (pending == null) return CommandResult.InvalidState("Nothing waits on you.");
            RecordPose();
            var result = _session.Dispatch(new ResolveOverlandEncounterCommand(pending.Id, choice));
            if (result.IsSuccess && _session.HasActiveFight)
            {
                // A fight: the one combat engine, on the ground where the player stands.
                _gridOriginSet = false;
                RefreshCombat(result.Message, false);
            }
            else Hud?.ShowMessage(result);

            RefreshEncounterPanel();
            return result;
        }

        // ----- The HUD --------------------------------------------------------------------------------------------

        private void SetupLifeHud()
        {
            if (Hud?.Root == null) return;
            Label Line(string name, int top, Color colour)
            {
                var l = new Label { name = name };
                l.style.position = Position.Absolute;
                l.style.top = top;
                l.style.left = Length.Percent(25);
                l.style.right = Length.Percent(25);
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.color = colour;
                l.style.fontSize = 13;
                l.pickingMode = PickingMode.Ignore;
                Hud.Root.Add(l);
                return l;
            }

            _waypointLine = Line("xk-overland-waypoint", 62, new Color(0.98f, 0.82f, 0.45f));
            _groundLine = Line("xk-overland-ground", 80, new Color(0.75f, 0.85f, 0.95f));

            _overlandActions = new VisualElement { name = "xk-overland-actions" };
            _overlandActions.style.position = Position.Absolute;
            _overlandActions.style.left = 16;
            _overlandActions.style.bottom = 60;
            _overlandActions.style.flexDirection = FlexDirection.Column;
            _rejoinButton = new Button(() => RejoinJourney()) { name = "xk-overland-rejoin", text = "Back on the road (J)" };
            _clearMarkButton = new Button(() => MarkWaypoint(null)) { name = "xk-overland-clear-mark", text = "Clear the mark" };
            foreach (var b in new[] { _rejoinButton, _clearMarkButton }) { b.AddToClassList("xk-button"); _overlandActions.Add(b); }
            Hud.Root.Add(_overlandActions);

            _encounterPanel = new VisualElement { name = "xk-overland-encounter" };
            _encounterPanel.style.position = Position.Absolute;
            _encounterPanel.style.left = Length.Percent(30);
            _encounterPanel.style.right = Length.Percent(30);
            _encounterPanel.style.bottom = 150;
            _encounterPanel.style.paddingLeft = _encounterPanel.style.paddingRight = 14;
            _encounterPanel.style.paddingTop = _encounterPanel.style.paddingBottom = 10;
            _encounterPanel.style.backgroundColor = new Color(0.10f, 0.07f, 0.06f, 0.92f);
            _encounterPanel.style.borderTopWidth = _encounterPanel.style.borderBottomWidth = _encounterPanel.style.borderLeftWidth = _encounterPanel.style.borderRightWidth = 1;
            _encounterPanel.style.borderTopColor = _encounterPanel.style.borderBottomColor = _encounterPanel.style.borderLeftColor = _encounterPanel.style.borderRightColor = new Color(0.78f, 0.36f, 0.25f);
            _encounterPanel.style.display = DisplayStyle.None;
            Hud.Root.Add(_encounterPanel);
        }

        private void RefreshLifeHud()
        {
            if (_waypointLine == null) return;
            var wp = _waypoint = _session.GetWaypoint();
            _waypointLine.text = wp == null ? string.Empty : "◆ " + wp.Line;
            _groundLine.text = _ground?.Line ?? string.Empty;
            var halted = _session.GetHaltedJourneyPoint();
            var near = halted != null && Player != null && Vector2.Distance(new Vector2(halted.Value.x, halted.Value.z), new Vector2(Player.transform.position.x, Player.transform.position.z)) <= OverlandRoads.RejoinMetres;
            _rejoinButton.style.display = near ? DisplayStyle.Flex : DisplayStyle.None;
            _clearMarkButton.style.display = wp != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshEncounterPanel()
        {
            if (_encounterPanel == null) return;
            var e = _session.GetPendingOverlandEncounter();
            if (e == null || _session.HasActiveFight)
            {
                _encounterPanel.style.display = DisplayStyle.None;
                _encounterShown = null;
                return;
            }

            if (_encounterShown == e.Id && _encounterPanel.style.display == DisplayStyle.Flex) return;
            _encounterShown = e.Id;
            _encounterPanel.Clear();
            var text = new Label(e.Text) { name = "xk-overland-encounter-text" };
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.color = new Color(0.96f, 0.90f, 0.80f);
            text.style.fontSize = 15;
            text.style.marginBottom = 8;
            _encounterPanel.Add(text);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            void Choice(string name, string label, OverlandChoice c)
            {
                var b = new Button(() => AnswerEncounter(c)) { name = name, text = label };
                b.AddToClassList("xk-button");
                row.Add(b);
            }

            Choice("xk-encounter-fight", "Fight", OverlandChoice.Fight);
            Choice("xk-encounter-slip", "Slip away (Stealth)", OverlandChoice.SlipAway);
            Choice("xk-encounter-back", "Back off", OverlandChoice.BackOff);
            if (e.Kind == OverlandEncounterKind.Bandits) Choice("xk-encounter-pay", "Pay their toll", OverlandChoice.Pay);
            _encounterPanel.Add(row);
            _encounterPanel.style.display = DisplayStyle.Flex;
        }
    }
}
