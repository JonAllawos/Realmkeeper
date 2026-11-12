using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 13 (ADR-0062 … ADR-0066): places in exploration. A place loads as its own environment from the world's view
    /// of it (what the player may see); walking from room to room records the move through a command (which explores
    /// the room, meets its hazard, notices what passive Perception finds and starts fights); stepping on an armed trap
    /// springs it; acting on anything offers the actions the world allows. Doors into buildings stand in the street.
    /// The scene holds no state of its own: when the world changes, it is rebuilt from the world.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private PlaceSceneView _placeScene;
        private GameObject _placeGeometry;
        private GameObject _placeDynamic;
        private GeneratedAssets _placeAssets;
        private string _placeSignature;
        private string _dynamicSignature;
        private readonly List<GameObject> _followerViews = new List<GameObject>();
        private readonly List<PlaceInteractable> _placeTargets = new List<PlaceInteractable>();
        private readonly List<PlaceInteractable> _buildingDoors = new List<PlaceInteractable>();
        private List<(string Label, Action Act)> _placeChoices;
        private VisualElement _placePanel;
        private Label _placeName;
        private Label _placeWhere;
        private Label _placeStatus;
        private VisualElement _placeMap;
        private Light _lantern;
        private (int, int, int) _lastTrapCell = (int.MinValue, 0, 0);
        private LocalPose _lastGoodPose;

        public bool InPlace => _placeScene != null;
        public PlaceSceneView PlaceScene => _placeScene;
        public GameObject PlaceGeometry => _placeGeometry;
        public IReadOnlyList<PlaceInteractable> PlaceTargets => _placeTargets;
        public IReadOnlyList<PlaceInteractable> BuildingDoors => _buildingDoors;
        public VisualElement PlaceMapPanel => _placeMap;
        public string LastPlaceMessage { get; private set; }

        // ----- Loading -------------------------------------------------------------------------

        private void SyncPlace(PlayerSummary player, bool forceReload)
        {
            var scene = _session.GetPlaceScene();
            if (scene == null)
            {
                UnloadEnvironment();
                return;
            }

            var key = "place:" + scene.PlaceId;
            if (forceReload || _streamer.Current == null || _streamer.Current.PlaceKey != key)
            {
                UnloadEnvironment();
                LoadPlace(player, scene);
            }
            else RefreshPlace(scene, snap: false);

            if (CameraRig != null && CameraRig.Perspective != player.Perspective)
            {
                CameraRig.SetPerspective(player.Perspective);
                Hud?.SetPerspective(player.Perspective);
            }
        }

        private void LoadPlace(PlayerSummary player, PlaceSceneView scene)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var layout = new LocalEnvironmentLayout { SettlementId = player.SettlementId, RegionId = player.RegionId, PlaceKey = "place:" + scene.PlaceId, HalfExtent = 1000f, ArrivalPose = player.Pose };
            _materials = new ExplorationMaterials();
            _renderSnapshot = SettlementEnvironmentBuilder.RenderSettingsSnapshot.Capture();
            _atmosphere = new ExplorationAtmosphere();
            _atmosphere.Capture();
            _streamer.Load(layout, scene.Name);
            // Phase 14: places use the CC0 kit's textured materials and props too (and the character's own body), when present.
            var kit = ArtCatalog.Load();
            Catalog = kit != null && kit.Modules.Count > 0 ? kit : null;

            // Underground or indoors: dim ambient light, no sun.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = scene.Category == PlaceCategory.Interior ? new Color(0.30f, 0.27f, 0.24f) : new Color(0.16f, 0.15f, 0.14f);
            RenderSettings.fog = false;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.scene == _streamer.Scene) l.enabled = false;

            EnvironmentRoot = new GameObject("Place environment");
            _placeScene = scene;
            BuildPlaceGeometry();

            Player = PlayerCharacter.Create(null, _materials.Get(new Color(0.24f, 0.36f, 0.52f)), _materials.Get(new Color(0.55f, 0.42f, 0.30f)), Catalog,
                Catalog != null ? CurrentLook() : null, Catalog != null ? Assets() : null);
            Player.Input = _input;
            Player.Teleport(player.Pose);
            _lastGoodPose = player.Pose;
            CameraRig = ExplorationCameraRig.Create(null, Player);
            CameraRig.Input = _input;
            CameraRig.SetPerspective(player.Perspective);
            CameraRig.SetView(player.Pose.Yaw, 12f);
            CameraRig.Snap();
            var lanternGo = new GameObject("Lantern");
            lanternGo.transform.SetParent(Player.transform, false);
            lanternGo.transform.localPosition = new Vector3(0.3f, 1.4f, 0.3f);
            _lantern = lanternGo.AddComponent<Light>();
            _lantern.type = LightType.Point;
            _lantern.shadows = LightShadows.None;

            _detector.Clear();
            Hud = ExplorationHud.Create(null);
            Hud.ReturnRequested += OnReturnRequested;
            Hud.SaveRequested += OnSaveRequested;
            Hud.SetPerspective(player.Perspective);
            SetupLiving(layout);
            SetupPlaceHud();
            RefreshPlace(scene, snap: true);
            _input?.Enable();
            SetCursorFree(false);
            Debug.Log($"Realmkeeper: entered {scene.Name} ({scene.Category}): {scene.Rooms.Count} rooms on {scene.Floors.Count} floors, built in {clock.ElapsedMilliseconds} ms; character at {player.Pose}.");
        }

        private string Signature(PlaceSceneView s) =>
            string.Join(",", s.Rooms.Select(r => r.Id)) + "|" + string.Join(",", s.Passages.Select(p => p.Id));

        private void BuildPlaceGeometry()
        {
            if (_placeGeometry != null) UnityEngine.Object.Destroy(_placeGeometry);
            _placeGeometry = PlaceEnvironmentBuilder.Build(_placeScene, _materials, out _placeAssets, Catalog);
            _placeGeometry.transform.SetParent(EnvironmentRoot.transform, true);
            _placeSignature = Signature(_placeScene);
        }

        /// <summary>Rebuilds what may have changed: doors, things, people, loot, and the geometry when a hidden way was found.</summary>
        private void RefreshPlace(PlaceSceneView scene, bool snap)
        {
            if (scene == null || EnvironmentRoot == null) return;
            _placeScene = scene;
            if (Signature(scene) != _placeSignature) BuildPlaceGeometry();
            // Only what changed is rebuilt: the clock ticking (the place lives while the player is in it) changes nothing here.
            var dynamicSignature = DynamicSignature(scene);
            if (!snap && dynamicSignature == _dynamicSignature && _placeDynamic != null) { RefreshPlaceHud(); RefreshConditionsHud(); return; }
            _dynamicSignature = dynamicSignature;
            foreach (var t in _placeTargets) if (t != null) _detector.Unregister(t);
            _placeTargets.Clear();
            if (_placeDynamic != null) UnityEngine.Object.Destroy(_placeDynamic);
            _followerViews.Clear();
            _placeDynamic = new GameObject("Place — doors, things, people");
            _placeDynamic.transform.SetParent(EnvironmentRoot.transform, false);
            var root = _placeDynamic.transform;

            PlaceInteractable Target(PlaceInteractable.TargetKind kind, string id, string label, Vector3 at)
            {
                var t = PlaceInteractable.Create(root, kind, id, label, at);
                t.Handler = OnPlaceInteract;
                _placeTargets.Add(t);
                _detector.Register(t);
                return t;
            }

            foreach (var p in scene.Passages)
            {
                var a = scene.Room(p.A);
                if (a == null) continue;
                switch (p.Kind)
                {
                    case ConnectionKind.Door:
                    case ConnectionKind.Secret:
                        PlaceEnvironmentBuilder.Door(root, scene, p, _materials);
                        var (centre, _, _) = PlaceEnvironmentBuilder.DoorFrame(scene, p);
                        var verb = p.Door == DoorState.Open ? "Close" : p.Door == DoorState.Broken ? "Broken door" : p.Door == DoorState.Locked ? "Locked door" : p.Door == DoorState.Barred ? "Barred door" : "Open";
                        Target(PlaceInteractable.TargetKind.Passage, p.Id, (p.Kind == ConnectionKind.Secret ? "Hidden way: " : "Door: ") + verb + (p.Guarded ? " (guarded)" : string.Empty), centre + Vector3.up * 1.1f);
                        break;
                    case ConnectionKind.Stairs:
                    case ConnectionKind.Ladder:
                    {
                        var b = scene.Room(p.B);
                        var upFromA = b != null && PlaceEnvironmentBuilder.Elevation(scene, b.Floor) > PlaceEnvironmentBuilder.Elevation(scene, a.Floor);
                        var foot = PlaceEnvironmentBuilder.CellCentre(scene, a.Floor, p.Rect.X, p.Rect.Z);
                        Target(PlaceInteractable.TargetKind.Stairs, p.Id, (upFromA ? "Climb the " : "Go down the ") + p.Label.ToLowerInvariant(), foot + Vector3.up * 1.0f);
                        if (b != null)
                        {
                            var landing = PlaceEnvironmentBuilder.CellCentre(scene, b.Floor, p.ToX, p.ToZ);
                            Target(PlaceInteractable.TargetKind.Stairs, p.Id, (upFromA ? "Go down the " : "Climb the ") + p.Label.ToLowerInvariant(), landing + Vector3.up * 1.0f);
                        }

                        break;
                    }
                    case ConnectionKind.Entrance:
                    {
                        var at = PlaceEnvironmentBuilder.CellCentre(scene, a.Floor, p.Rect.X + p.Rect.W / 2, a.Rect.Z);
                        Target(PlaceInteractable.TargetKind.Exit, p.Id, "Leave " + scene.Name, at + Vector3.up * 1.1f);
                        break;
                    }
                }
            }

            foreach (var f in scene.Features)
            {
                if (f.Hidden) continue;
                var obj = PlaceEnvironmentBuilder.Feature(root, scene, f, _materials);
                var room = scene.Room(f.Room);
                var at = PlaceEnvironmentBuilder.CellCentre(scene, room?.Floor ?? 0, f.X, f.Z);
                var label = f.Kind == FeatureKind.Trap ? (f.Armed ? "Trap: " : "Disarmed trap: ") + f.Name
                    : f.Kind == FeatureKind.Container ? f.Name + ((f.Flags & FeatureFlags.Opened) != 0 ? " (opened)" : string.Empty)
                    : f.Kind == FeatureKind.Captive ? ((f.Flags & FeatureFlags.Freed) != 0 ? "Where someone was held" : "Someone pinned under timber")
                    : f.Name;
                if (f.Kind == FeatureKind.Captive && (f.Flags & FeatureFlags.Freed) != 0) continue;
                Target(PlaceInteractable.TargetKind.Feature, f.Id, label, at + Vector3.up * 0.9f);
            }

            foreach (var pile in scene.LootPiles)
            {
                var room = scene.Room(pile.Room);
                var at = PlaceEnvironmentBuilder.CellCentre(scene, room?.Floor ?? 0, pile.X, pile.Z);
                var sack = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.Object.Destroy(sack.GetComponent<Collider>());
                sack.name = "Loot pile";
                sack.transform.SetParent(root, false);
                sack.transform.position = at + Vector3.up * 0.25f;
                sack.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
                sack.GetComponent<Renderer>().sharedMaterial = _materials.Get(new Color(0.55f, 0.45f, 0.30f));
                Target(PlaceInteractable.TargetKind.Loot, pile.Key, "Look through what the fight left", at + Vector3.up * 0.8f);
            }

            // People and foes.
            var followerSlot = 0;
            foreach (var fig in scene.Figures)
            {
                Vector3 at;
                var withPlayer = fig.Role == "follower" && fig.Waiting == null && fig.Room == scene.PlayerRoom;
                if (withPlayer && Player != null)
                    at = Player.transform.position - Player.transform.forward * (1.4f + followerSlot++ * 0.9f) + Player.transform.right * 0.6f;
                else
                {
                    var room = scene.Room(fig.Room);
                    if (room == null) continue;
                    at = PlaceEnvironmentBuilder.CellCentre(scene, room.Floor, fig.X, fig.Z);
                }

                if (fig.Role == "captive") continue; // drawn with its feature
                if (!InFight)
                {
                    var body = PlaceEnvironmentBuilder.Figure(root, scene, fig, _materials, at);
                    if (withPlayer) _followerViews.Add(body);
                    else if (fig.Role == "follower") body.name += " (waiting: " + (fig.Waiting ?? "the way is shut") + ")";
                    // Phase 14 (ADR-0070): someone who has just changed rooms walks the real way, through the doorways, to where they are now.
                    if (fig.Role == "resident" && fig.FromRoom != null && fig.FromRoom != fig.Room && fig.Route.Count > 0 && !snap)
                        PlaceWalker.Walk(body, PlaceEnvironmentBuilder.RouteWaypoints(scene, fig.FromRoom, fig.Route, at));
                }
                if (fig.Role == "foe" || fig.Role == "guard" && fig.NpcId.IsEmpty)
                {
                    if (fig.GroupId != null && !_placeTargets.Exists(t => t.TargetId == fig.GroupId))
                        Target(PlaceInteractable.TargetKind.Foes, fig.GroupId, (fig.Role == "guard" ? "Guards: " : "Attack ") + fig.Name, at + Vector3.up * 1.2f);
                }
                else Target(PlaceInteractable.TargetKind.Person, fig.NpcId.ToString(), "Talk to " + fig.Name, at + Vector3.up * 1.2f);
            }

            // Phase 14 (ADR-0071): fire burning or burnt out, bridges as they stand, drained water.
            PlaceEnvironmentBuilder.Hazards(root, scene, _materials, Catalog);

            if (_lantern != null)
            {
                _lantern.intensity = scene.PlayerHasLight ? 1.5f : 0.45f;
                _lantern.range = scene.PlayerHasLight ? 9f : 3.5f;
                _lantern.color = scene.PlayerHasLight ? new Color(1f, 0.82f, 0.55f) : new Color(0.7f, 0.72f, 0.8f);
            }

            RefreshPlaceHud();
            RefreshConditionsHud(); // Phase 14: followers, conditions and rest change with every step
        }

        private string DynamicSignature(PlaceSceneView s) =>
            s.PlayerRoom + "|" + s.PlayerHasLight + "|" + InFight + "|" + string.Join(",", s.Passages.Select(p => p.Id + p.Door + p.Guarded)) + "|" +
            string.Join(",", s.Features.Select(f => f.Id + (int)f.Flags + f.Hidden + f.Armed)) + "|" + string.Join(",", s.Figures.Select(f => f.Name + f.Room + f.Role + f.X + f.Z + f.Activity)) + "|" +
            string.Join(",", s.LootPiles.Select(l => l.Key)) + "|" + string.Join(",", s.Fires.Select(f => f.Key + f.Value)) + "|" + string.Join(",", s.Bridges.Select(b => b.Key + b.Value)) + "|" +
            s.Drained + "|" + string.Join(",", s.Figures.Select(f => f.Waiting ?? string.Empty));

        private void TeardownPlace()
        {
            _dynamicSignature = null;
            foreach (var t in _placeTargets) if (t != null) _detector.Unregister(t);
            _placeTargets.Clear();
            _buildingDoors.Clear();
            _placeScene = null;
            _placeGeometry = null;
            _placeDynamic = null;
            _placeAssets = null;
            _placeSignature = null;
            _placePanel = null;
            _placeMap = null;
            _placeChoices = null;
            _lantern = null;
            _lastTrapCell = (int.MinValue, 0, 0);
        }

        // ----- HUD -----------------------------------------------------------------------------

        private void SetupPlaceHud()
        {
            if (Hud?.Root == null) return;
            _placePanel = new VisualElement { name = "xk-place" };
            _placePanel.AddToClassList("xk-place");
            _placePanel.pickingMode = PickingMode.Ignore;
            _placeName = new Label { name = "xk-place-name" };
            _placeName.AddToClassList("xk-place-name");
            _placeWhere = new Label { name = "xk-place-where" };
            _placeWhere.AddToClassList("xk-place-where");
            _placeStatus = new Label { name = "xk-place-status" };
            _placeStatus.AddToClassList("xk-place-status");
            var hint = new Label("E: act · R: search the room · N: map of what you have seen · 1–9: choose");
            hint.AddToClassList("xk-place-hint");
            _placePanel.Add(_placeName);
            _placePanel.Add(_placeWhere);
            _placePanel.Add(_placeStatus);
            _placePanel.Add(hint);
            Hud.Root.Add(_placePanel);
        }

        private void RefreshPlaceHud()
        {
            if (_placeScene == null || Hud == null) return;
            var room = _placeScene.Room(_placeScene.PlayerRoom);
            Hud.SetLocation(_placeScene.Name, (_placeScene.FloorName ?? "-") + (room != null ? " · " + room.Name : string.Empty), _session.GetOverview().Date.ToString());
            if (_placeName == null) return;
            _placeName.text = _placeScene.Name;
            _placeWhere.text = (_placeScene.FloorName ?? string.Empty) + (room != null ? " — " + room.Name : string.Empty);
            _placeStatus.text = _placeScene.Status ?? string.Empty;
            _placeStatus.EnableInClassList("xk-hidden", string.IsNullOrEmpty(_placeScene.Status));
            if (_placeMap != null) ShowPlaceMap(true);
        }

        /// <summary>Opens or closes the map of what the player has seen of the place (floors, rooms explored, passages between them).</summary>
        public void ShowPlaceMap(bool show)
        {
            if (Hud?.Root == null) return;
            _placeMap?.RemoveFromHierarchy();
            _placeMap = null;
            if (!show || _session == null) return;
            var map = _session.GetPlaceMap();
            if (map == null) return;
            _placeMap = new VisualElement { name = "xk-place-map" };
            _placeMap.AddToClassList("xk-place-map");
            _placeMap.pickingMode = PickingMode.Ignore;
            var title = new Label(map.Name);
            title.AddToClassList("xk-place-map-title");
            _placeMap.Add(title);
            foreach (var floor in map.Floors.Where(f => f.Index == map.CurrentFloor).Concat(map.Floors.Where(f => f.Index != map.CurrentFloor)).Take(2))
            {
                var name = new Label(floor.Name + (floor.Index == map.CurrentFloor ? " (you are here)" : string.Empty));
                name.AddToClassList("xk-place-map-floor");
                _placeMap.Add(name);
                var canvas = new VisualElement { name = "xk-place-map-floor-" + floor.Index };
                canvas.AddToClassList("xk-place-map-canvas");
                canvas.style.height = floor.Index == map.CurrentFloor ? 240 : 120;
                var minX = floor.Rooms.Min(r => r.Rect.X);
                var minZ = floor.Rooms.Min(r => r.Rect.Z);
                var maxX = floor.Rooms.Max(r => r.Rect.MaxX);
                var maxZ = floor.Rooms.Max(r => r.Rect.MaxZ);
                var w = 400f;
                var h = floor.Index == map.CurrentFloor ? 240f : 120f;
                var scale = Mathf.Min(w / Mathf.Max(1, maxX - minX + 2), h / Mathf.Max(1, maxZ - minZ + 2));
                foreach (var p in floor.Passages)
                {
                    if (p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder || p.Kind == ConnectionKind.Entrance) continue;
                    var e = new VisualElement { name = "xk-place-map-passage-" + p.Id };
                    e.AddToClassList("xk-place-map-passage");
                    if (p.Kind == ConnectionKind.Bridge && _placeScene != null && _placeScene.Bridges.TryGetValue(p.Id, out var bridge)) // Phase 14: bridges as they stand
                    {
                        e.AddToClassList("xk-place-map-bridge");
                        e.EnableInClassList("xk-place-map-bridge--damaged", bridge == BridgeState.Damaged);
                        e.EnableInClassList("xk-place-map-bridge--destroyed", bridge == BridgeState.Destroyed);
                        e.tooltip = "Bridge: " + bridge.ToString().ToLowerInvariant();
                    }

                    e.style.left = (p.Rect.X - minX + 1) * scale;
                    e.style.top = h - (p.Rect.MaxZ - minZ + 1) * scale;
                    e.style.width = Mathf.Max(2f, p.Rect.W * scale);
                    e.style.height = Mathf.Max(2f, p.Rect.D * scale);
                    canvas.Add(e);
                }

                foreach (var r in floor.Rooms)
                {
                    var e = new Label(r.Name) { name = "xk-place-map-room-" + r.Id };
                    e.AddToClassList("xk-place-map-room");
                    e.EnableInClassList("xk-place-map-room--here", r.Id == map.PlayerRoom);
                    e.EnableInClassList("xk-place-map-room--secret", r.Secret);
                    e.EnableInClassList("xk-place-map-fire", _placeScene != null && _placeScene.Fires.TryGetValue(r.Id, out var fire) && fire == "burning");
                    e.style.left = (r.Rect.X - minX + 1) * scale;
                    e.style.top = h - (r.Rect.MaxZ - minZ + 1) * scale;
                    e.style.width = r.Rect.W * scale;
                    e.style.height = r.Rect.D * scale;
                    e.style.fontSize = 9;
                    e.style.color = new Color(0.12f, 0.10f, 0.08f);
                    canvas.Add(e);
                }

                _placeMap.Add(canvas);
            }

            var legend = new Label("Only rooms you have seen are drawn. Gold: where you stand. Violet: a hidden room you found." +
                                   (_placeScene != null && _placeScene.Bridges.Count > 0 ? " Plank-coloured: a bridge (ringed red if damaged; dark if fallen)." : string.Empty) +
                                   (_placeScene != null && _placeScene.Fires.Values.Any(f => f == "burning") ? " A room on fire: " + string.Join(", ", _placeScene.Fires.Where(f => f.Value == "burning").Select(f => _placeScene.Room(f.Key)?.Name)) + "." : string.Empty));
            legend.AddToClassList("xk-place-map-legend");
            _placeMap.Add(legend);
            Hud.Root.Add(_placeMap);
        }

        // ----- Each frame -------------------------------------------------------------------------

        private void UpdatePlace(Keyboard keys)
        {
            if (_placeScene == null || Player == null || _session == null) return;
            if (keys != null && keys.nKey.wasPressedThisFrame) ShowPlaceMap(_placeMap == null);
            if (keys != null && keys.rKey.wasPressedThisFrame && !InFight && !WindowOpen && _placeScene.PlayerRoom != null) { SearchRoom(); return; }
            if (_placeChoices != null && keys != null)
                for (var i = 0; i < _placeChoices.Count && i < 9; i++)
                    if (keys[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        var act = _placeChoices[i].Act;
                        _placeChoices = null;
                        Hud?.ShowTopics(null);
                        act();
                        return;
                    }

            // Followers keep close behind the player.
            for (var i = 0; i < _followerViews.Count; i++)
            {
                var f = _followerViews[i];
                if (f == null) continue;
                var want = Player.transform.position - Player.transform.forward * (1.4f + i * 0.9f) + Player.transform.right * 0.6f;
                f.transform.position = Vector3.Lerp(f.transform.position, want, Mathf.Clamp01(Time.deltaTime * 4f));
            }

            if (InFight || WindowOpen) return;
            var pos = Player.transform.position;
            var cx = Mathf.FloorToInt(pos.x / PlaceEnvironmentBuilder.Cell);
            var cz = Mathf.FloorToInt(pos.z / PlaceEnvironmentBuilder.Cell);
            var floor = _placeScene.PlayerFloor;
            var room = _placeScene.Rooms.Find(r => r.Floor == floor && r.Rect.Contains(cx, cz));
            if (room != null && room.Id != _placeScene.PlayerRoom)
            {
                var r = _session.Dispatch(new MoveInPlaceCommand(room.Id, Player.CurrentPose));
                if (!r.IsSuccess)
                {
                    Player.Teleport(_lastGoodPose); // the world says no (a guarded way): back where it allows
                    Hud?.ShowMessage(r);
                }
                else if (_session.GetPlaceScene() is PlaceSceneView now && now.PlayerRoom != null && now.PlayerRoom != room.Id && _session.GetPlayer() is PlayerSummary after)
                {
                    // Phase 14: the world kept the player where they were (a bridge gave way under them): back to its pose.
                    Player.Teleport(after.Pose);
                    _lastGoodPose = after.Pose;
                    if (!string.IsNullOrEmpty(r.Message)) Say(room.Name, r.Message);
                }
                else
                {
                    _lastGoodPose = Player.CurrentPose;
                    if (!string.IsNullOrEmpty(r.Message)) Say(room.Name, r.Message);
                }

                return;
            }

            if (room != null) _lastGoodPose = Player.CurrentPose;

            // Stepping onto a trap that is armed springs it (found or not: a found one can be stepped around).
            foreach (var f in _placeScene.Features)
            {
                if (f.Kind != FeatureKind.Trap || !f.Armed || f.X != cx || f.Z != cz) continue;
                var fr = _placeScene.Room(f.Room);
                if (fr == null || fr.Floor != floor || _lastTrapCell == (cx, cz, floor)) continue;
                _lastTrapCell = (cx, cz, floor);
                var r = _session.Dispatch(new SpringTrapCommand(f.Id));
                Say(f.Name, r.IsSuccess ? r.Message : ResidentView.Describe(r));
                return;
            }

            if (_lastTrapCell.Item1 != int.MinValue && (_lastTrapCell.Item1 != cx || _lastTrapCell.Item2 != cz)) _lastTrapCell = (int.MinValue, 0, 0);
        }

        private void Say(string title, string text)
        {
            LastPlaceMessage = text;
            Hud?.ShowResponse(new InteractionResponse(title, text, InteractionKind.StateChanging, CommandResult.Ok(text)));
        }

        // ----- Acting -----------------------------------------------------------------------------

        private InteractionResponse OnPlaceInteract(PlaceInteractable t)
        {
            switch (t.Target)
            {
                case PlaceInteractable.TargetKind.Stairs:
                    return Respond(t.Label, _session.Dispatch(new TraversePlaceCommand(t.TargetId)), snapToPose: true);
                case PlaceInteractable.TargetKind.Exit:
                    return Respond(t.Label, LeavePlace());
                case PlaceInteractable.TargetKind.Loot:
                    OpenStorage(t.TargetId);
                    return new InteractionResponse(t.Label, "What the fight left.", InteractionKind.Informational);
                case PlaceInteractable.TargetKind.Person:
                {
                    // Phase 14 (ADR-0069): talk, or attack — an assault, with witnesses and everything that follows.
                    var npcId = new Realmkeeper.Domain.Identifiers.NpcId(Guid.Parse(t.TargetId));
                    _placeChoices = new List<(string, Action)>
                    {
                        ("Talk", () => { var r = StartDialogue(npcId); if (!r.IsSuccess) Hud?.ShowMessage(r); }),
                        ("Attack (a crime: anyone who sees it will remember)", () => Respond("Attack", _session.Dispatch(new AttackPersonCommand(npcId)))),
                    };
                    Hud?.ShowTopics(_placeChoices.Select(c => (c.Label, c.Act)).ToList());
                    return new InteractionResponse(t.Label, "Choose what to do (number keys or click).", InteractionKind.Informational);
                }

                case PlaceInteractable.TargetKind.BuildingDoor:
                {
                    RecordPose();
                    var entered = _session.Dispatch(new EnterBuildingCommand(t.TargetId));
                    if (entered.IsSuccess || !entered.Message.Contains("locked for the night")) return Respond(t.Label, entered);
                    // Phase 14 (ADR-0069): locked for the night — getting in is breaking in.
                    var key = t.TargetId;
                    _placeChoices = new List<(string, Action)>
                    {
                        ("Pick the lock (Sleight of Hand; breaking in)", () => { RecordPose(); Respond("Pick the lock", _session.Dispatch(new EnterBuildingCommand(key, PlaceAction.Pick))); }),
                        ("Force the door (Athletics; loud, and breaking in)", () => { RecordPose(); Respond("Force the door", _session.Dispatch(new EnterBuildingCommand(key, PlaceAction.Force))); }),
                        ("Leave it", () => Say(t.Label, "You leave the door alone.")),
                    };
                    Hud?.ShowTopics(_placeChoices.Select(c => (c.Label, c.Act)).ToList());
                    return new InteractionResponse(t.Label, entered.Message, InteractionKind.Informational);
                }
            }

            // A feature, a door, the room, a group: the actions the world allows here.
            var options = _session.GetPlaceActions(t.TargetId);
            var enabled = options.Where(o => o.Enabled).ToList();
            if (enabled.Count == 0)
            {
                var why = options.Count == 0 ? "You need to be in the same room." : string.Join(" ", options.Select(o => o.Label + ": " + o.Reason).Distinct());
                return new InteractionResponse(t.Label, why, InteractionKind.Informational);
            }

            if (enabled.Count == 1) return Act(t, enabled[0].Action, enabled[0].Label);
            _placeChoices = new List<(string, Action)>();
            foreach (var o in enabled)
            {
                var opt = o;
                _placeChoices.Add(($"{o.Label}{(o.Check != null ? " (" + o.Check + ")" : string.Empty)}", () => Act(t, opt.Action, opt.Label)));
            }

            foreach (var o in options.Where(o => !o.Enabled)) _placeChoices.Add(($"— {o.Label}: {o.Reason}", () => Say(o.Label, o.Reason)));
            Hud?.ShowTopics(_placeChoices.Select(c => (c.Label, c.Act)).ToList());
            return new InteractionResponse(t.Label, "Choose what to do (number keys or click).", InteractionKind.Informational);
        }

        /// <summary>Acts on a target in the place through the one place command (tests and the HUD use this).</summary>
        public InteractionResponse Act(PlaceInteractable t, PlaceAction action, string label = null)
        {
            _placeChoices = null;
            Hud?.ShowTopics(null);
            var result = _session.Dispatch(new PlaceActionCommand(t.TargetId, action));
            var response = Respond(label ?? t.Label, result);
            if (result.IsSuccess && action == PlaceAction.Open && t.Target == PlaceInteractable.TargetKind.Feature)
            {
                var f = _placeScene?.Features.Find(x => x.Id == t.TargetId);
                if (f?.ContainerKey != null) OpenStorage(f.ContainerKey);
            }

            return response;
        }

        /// <summary>Searches the room the player is in (the R key).</summary>
        public CommandResult SearchRoom()
        {
            if (_session == null || _placeScene?.PlayerRoom == null) return CommandResult.InvalidState("Step into a room first.");
            var result = _session.Dispatch(new PlaceActionCommand(_placeScene.PlayerRoom, PlaceAction.Search));
            Respond("Search", result);
            return result;
        }

        public CommandResult ActOn(string targetId, PlaceAction action)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.Dispatch(new PlaceActionCommand(targetId, action));
            Respond(action.ToString(), result);
            return result;
        }

        public CommandResult Climb(string connectionId)
        {
            var r = _session.Dispatch(new TraversePlaceCommand(connectionId));
            Respond("Climb", r, snapToPose: true);
            return r;
        }

        public CommandResult LeavePlace()
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var r = _session.Dispatch(new ExitPlaceCommand());
            if (!r.IsSuccess) Hud?.ShowMessage(r);
            return r;
        }

        public CommandResult EnterBuilding(string layoutKey)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            RecordPose();
            var r = _session.Dispatch(new EnterBuildingCommand(layoutKey));
            if (!r.IsSuccess) Hud?.ShowMessage(r);
            return r;
        }

        /// <summary>Walks the character to a room (as if they had walked there): for tests and captures.</summary>
        public CommandResult StepInto(string roomId)
        {
            var room = _placeScene?.Room(roomId);
            if (room == null || Player == null) return CommandResult.NotFound("No such room.");
            var pose = new LocalPose((room.Rect.CenterX + 0.5f) * PlaceEnvironmentBuilder.Cell, PlaceEnvironmentBuilder.Elevation(_placeScene, room.Floor),
                (room.Rect.CenterZ + 0.5f) * PlaceEnvironmentBuilder.Cell, Player.transform.eulerAngles.y);
            var r = _session.Dispatch(new MoveInPlaceCommand(roomId, pose));
            if (r.IsSuccess)
            {
                Player.Teleport(pose);
                _lastGoodPose = pose;
                if (!string.IsNullOrEmpty(r.Message)) Say(room.Name, r.Message);
            }
            else LastPlaceMessage = ResidentView.Describe(r);

            return r;
        }

        private InteractionResponse Respond(string title, CommandResult result, bool snapToPose = false)
        {
            var text = result.IsSuccess ? result.Message : ResidentView.Describe(result);
            LastPlaceMessage = text;
            var response = new InteractionResponse(title, text, InteractionKind.StateChanging, result);
            Hud?.ShowResponse(response);
            if (snapToPose && result.IsSuccess && Player != null && _session.GetPlayer() is PlayerSummary p)
            {
                Player.Teleport(p.Pose);
                _lastGoodPose = p.Pose;
            }

            return response;
        }

        // ----- Doors into buildings, in a settlement ---------------------------------------------

        private void SetupBuildingDoors(LocalEnvironmentLayout layout)
        {
            _buildingDoors.Clear();
            if (_session == null || EnvironmentRoot == null) return;
            var root = new GameObject("Building doors").transform;
            foreach (var b in layout.Buildings)
            {
                if (b.Kind == BuildingKind.Hall || b.Fit != ShopFit.None || b.Function == null || !InteriorGenerator.HasInterior(b.Function.Value)) continue;
                var door = LocalPaths.DoorOutside(b);
                var y = LocalPaths.StandingHeight(layout, door.X, door.Z);
                var name = _session.GetLocationName(layout.SettlementId, b.Key) ?? Readable(b.Function.Value);
                var t = PlaceInteractable.Create(root, PlaceInteractable.TargetKind.BuildingDoor, b.Key, "Enter " + name, new Vector3(door.X, y + 1.2f, door.Z));
                t.Handler = OnPlaceInteract;
                _buildingDoors.Add(t);
                _detector.Register(t);
            }
        }

        private static string Readable(Realmkeeper.Domain.Settlements.BuildingFunction f)
        {
            switch (f)
            {
                case Realmkeeper.Domain.Settlements.BuildingFunction.GuardPost: return "the guardhouse";
                case Realmkeeper.Domain.Settlements.BuildingFunction.Religious: return "the chapel";
                case Realmkeeper.Domain.Settlements.BuildingFunction.LargeHome: return "the large house";
                case Realmkeeper.Domain.Settlements.BuildingFunction.Civic: return "the hall of records";
                default: return "the " + f.ToString().ToLowerInvariant();
            }
        }
    }
}
