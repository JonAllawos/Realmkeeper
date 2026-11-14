using System;
using System.Collections;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 23 (ADR-0110, ADR-0112): the walkable country — the third kind of local environment, beside settlements and
    /// places — and the transitions between environments behind a loading screen.
    /// - The country of the player's region is built from its terrain (<see cref="OverlandWorld"/>), with its sites.
    /// - Walking records the position through <see cref="WalkOverlandCommand"/> (which also finds places by sight) and
    ///   advances the one clock by the travel rules: the living world's ambient minutes run as in a settlement; slow ground
    ///   (forest, marsh, fords) adds the extra time its travel cost asks, through <see cref="AdvanceTimeCommand"/>.
    /// - A compass, a location line and a region map (N) show where the player is and the sites they know.
    /// - Every move between environments (into a settlement or place, the road on, a gateway, out into the country, back to
    ///   the map) goes through <see cref="Travel"/>: the loading screen, the command, the rebuild, the screen off; a refusal
    ///   leaves everything as it was and says why.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private OverlandWorld _overland;
        private OverlandView _overlandView;
        private readonly List<OverlandSiteView> _siteViews = new List<OverlandSiteView>();
        private readonly List<Material> _siteMaterials = new List<Material>();
        private Vector3 _lastStep;
        private float _stepTimer;
        private double _owedMinutes;
        private float _ambientSince;
        private VisualElement _compass;
        private VisualElement _compassMarks;
        private Label _whereLine;
        private VisualElement _regionMap;
        private Texture2D _regionTexture;
        private LoadingScreen _loading;
        private bool _travelling;

        public bool InOverland => _overland != null;
        public OverlandWorld Overland => _overland;
        public OverlandView OverlandView => _overlandView;
        public IReadOnlyList<OverlandSiteView> OverlandSites => _siteViews;
        public LoadingScreen Loading => _loading;
        public bool Travelling => _travelling;

        /// <summary>Milliseconds the last transition took, from the screen shown to the environment ready.</summary>
        public float LastTransitionMs { get; private set; }

        /// <summary>The last transition's message (refusals included).</summary>
        public string LastTransitionMessage { get; private set; }

        // ----- Loading -----------------------------------------------------------------------------------------

        private static string OverlandKey(PlayerSummary p) => "overland:" + (string.IsNullOrEmpty(p.RealmId) ? RealmIds.Surface : p.RealmId) + ":" + p.RegionId;

        private void SyncOverland(PlayerSummary player, bool forceReload)
        {
            var key = OverlandKey(player);
            if (_arriving) return; // the country is being built behind the loading screen
            if (_streamer.Current == null && !_travelling && isActiveAndEnabled)
            {
                // Arriving from the world map (or a loaded save): the same loading screen as every other transition.
                StartCoroutine(ArriveFromMap(key));
                return;
            }

            if (forceReload || _streamer.Current == null || _streamer.Current.PlaceKey != key)
            {
                UnloadEnvironment();
                LoadOverland(player, key);
            }
            else RefreshOverland();

            if (CameraRig != null && CameraRig.Perspective != player.Perspective)
            {
                CameraRig.SetPerspective(player.Perspective);
                Hud?.SetPerspective(player.Perspective);
            }
        }

        private bool _arriving;
        private Transform _sitesRoot;

        /// <summary>Distance within which a settlement is shown in its own kit buildings (beyond it, blocks under the haze).</summary>
        private const float KitSilhouetteMetres = 1400f;

        /// <summary>Milliseconds the last kit silhouette took to build (diagnostics).</summary>
        public float LastSilhouetteMs { get; private set; }

        /// <summary>Settlements shown in their own kit buildings now.</summary>
        public int KitSilhouettes { get { var n = 0; foreach (var v in _siteViews) if (v != null && v.KitShown) n++; return n; } }

        private void UpgradeNearestSilhouette(Vector3 at)
        {
            OverlandSiteView best = null;
            var bestD = KitSilhouetteMetres * KitSilhouetteMetres;
            foreach (var v in _siteViews)
            {
                if (v == null || !v.CanUpgrade) continue;
                var d = (v.Site.X - at.x) * (v.Site.X - at.x) + (v.Site.Z - at.z) * (v.Site.Z - at.z);
                if (d < bestD) { bestD = d; best = v; }
            }

            if (best == null) return;
            LastSilhouetteMs = best.UpgradeSilhouette();
            Debug.Log($"Realmkeeper: {best.Site.Name} shown in its own buildings from the country ({LastSilhouetteMs:0} ms).");
        }

        private IEnumerator ArriveFromMap(string key)
        {
            _arriving = true;
            _travelling = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            if (_loading == null) _loading = LoadingScreen.Create(transform);
            var region = _session.FindRegion(_session.GetPlayer().RegionId);
            _loading.Show(region != null ? region.Name : "The country", "Going out into the country…");
            yield return null;
            yield return null; // the screen is drawn before the work starts
            _loading.Progress(0.4f, "Laying out the land…");
            var player = _session != null && _session.HasWorld ? _session.GetPlayer() : null;
            _arriving = false;
            if (player != null && player.IsOverland)
            {
                UnloadEnvironment();
                LoadOverland(player, OverlandKey(player));
            }

            LastTransitionMs = (float)clock.Elapsed.TotalMilliseconds;
            _loading.Progress(1f, null);
            yield return null;
            _loading.Hide();
            _travelling = false;
            Debug.Log($"Realmkeeper: into the country from the map in {LastTransitionMs:0} ms.");
        }

        private void LoadOverland(PlayerSummary player, string key)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var view = _session.GetOverland();
            if (view == null)
            {
                Debug.LogError("Realmkeeper: the player is in the country, but it has no map.");
                return;
            }

            _overlandView = view;
            var layout = new LocalEnvironmentLayout { RegionId = view.RegionId, PlaceKey = key, HalfExtent = view.Terrain.SizeX, ArrivalPose = player.Pose };
            _materials = new ExplorationMaterials();
            _renderSnapshot = SettlementEnvironmentBuilder.RenderSettingsSnapshot.Capture();
            _atmosphere = new ExplorationAtmosphere();
            _atmosphere.Capture();
            _streamer.Load(layout, view.RegionName);
            var kit = ArtCatalog.Load();
            Catalog = kit != null && kit.Modules.Count > 0 ? kit : null;
            EnvironmentRoot = new GameObject("Overland environment");
            _overland = OverlandWorld.Build(EnvironmentRoot.transform, view, Catalog);
            if (Catalog != null) _atmosphere.Apply(EnvironmentRoot.transform, Catalog);
            else SettlementEnvironmentBuilder.ApplyAtmosphere();
            _atmosphere.FogScale = 0.32f; // the country sees about a kilometre and a half
            _atmosphere.RealmTint = view.Profile == RealmProfiles.AncientPrototype ? new Color(0.62f, 0.5f, 0.78f) : Color.white;
            RenderSettings.fogStartDistance = 250f; // for the vertex-colour ground's linear haze
            RenderSettings.fogEndDistance = 1700f;

            Player = PlayerCharacter.Create(null, _materials.Get(new Color(0.24f, 0.36f, 0.52f)), _materials.Get(new Color(0.55f, 0.42f, 0.30f)), Catalog,
                Catalog != null ? CurrentLook() : null, Catalog != null ? Assets() : null);
            Player.Input = _input;
            _overland.Follow = Player.transform;
            _overland.Prewarm(new Vector3(player.Pose.X, 0f, player.Pose.Z)); // ground and barriers exist before the player stands on them
            Player.Teleport(player.Pose);
            _lastStep = Player.transform.position;
            _owedMinutes = 0;
            _ambientSince = Time.unscaledTime;
            CameraRig = ExplorationCameraRig.Create(null, Player);
            CameraRig.Input = _input;
            if (Catalog != null) ExplorationAtmosphere.ConfigureCamera(CameraRig.Camera);
            CameraRig.Camera.farClipPlane = 2600f;
            CameraRig.SetPerspective(player.Perspective);
            CameraRig.SetView(player.Pose.Yaw, 12f);
            CameraRig.Snap();

            _detector.Clear();
            _detector.Range = 9f; // landmarks, not doors
            BuildSites(view);
            UpgradeNearestSilhouette(new Vector3(player.Pose.X, 0f, player.Pose.Z)); // the town beside the arrival, in its own buildings before the screen lifts
            Hud = ExplorationHud.Create(null);
            Hud.ReturnRequested += OnReturnRequested;
            Hud.SaveRequested += OnSaveRequested;
            Hud.SetPerspective(player.Perspective);
            SetupLiving(layout);
            SetupOverlandHud();
            RefreshPracticeButton(); // the settlement-edge fights are not offered in the country
            SetupOverlandLife(view); // Phase 24: landmarks, travellers, encounters, ground weather
            _hudGround = null;
            RefreshOverlandHud();
            _input?.Enable();
            SetCursorFree(false);
            Debug.Log($"Realmkeeper: in the country of {view.RegionName} ({view.RealmName}): {_overland.DetailChunks} detailed chunks, {_siteViews.Count} sites, built in {clock.ElapsedMilliseconds} ms; character at {player.Pose}.");
        }

        private void BuildSites(OverlandView view)
        {
            foreach (var s in _siteViews) if (s != null) _detector.Unregister(s);
            _siteViews.Clear();
            var stone = _materials.Get(new Color(0.48f, 0.46f, 0.43f));
            var wood = _materials.Get(new Color(0.45f, 0.33f, 0.22f));
            var roof = _materials.Get(new Color(0.55f, 0.28f, 0.20f));
            var glow = _materials.Get(new Color(0.62f, 0.45f, 0.95f));
            if (_sitesRoot != null) Destroy(_sitesRoot.gameObject); // a rebuild (a gateway opened) replaces the sites, never doubles them
            var root = new GameObject("Sites").transform;
            root.SetParent(EnvironmentRoot.transform, false);
            _sitesRoot = root;
            foreach (var site in view.Sites)
            {
                LocalEnvironmentLayout layout = null;
                if (site.Kind == OverlandSiteKind.Settlement && Guid.TryParse(site.Id, out var g))
                {
                    var s = _session.FindSettlement(new SettlementId(g));
                    if (s != null) layout = Realmkeeper.Exploration.Application.ExplorationModule.LayoutFor(_session, s);
                }

                var v = OverlandSiteView.Create(root, site, view.Terrain, layout, Catalog, stone, wood, glow, roof);
                v.Handler = OnSiteChosen;
                _siteViews.Add(v);
                _detector.Register(v);
            }
        }

        /// <summary>The country is rebuilt only on a new region; otherwise its sites learn what changed (a place found, a gateway opened).</summary>
        private void RefreshOverland()
        {
            var view = _session.GetOverland();
            if (view == null) return;
            var rebuild = false;
            foreach (var site in view.Sites)
            {
                var v = _siteViews.Find(x => x.Site.Kind == site.Kind && x.Site.Id == site.Id);
                if (v == null) continue;
                if (site.Kind == OverlandSiteKind.Gateway && v.Site.Usable != site.Usable) rebuild = true; // its look changes
                v.Refresh(site);
            }

            _overlandView = view;
            if (rebuild) BuildSites(view);
            RefreshOverlandHud();
        }

        private void TeardownOverland()
        {
            if (_overland == null) return;
            TeardownOverlandLife(); // Phase 24
            _sitesRoot = null;
            foreach (var s in _siteViews) if (s != null) _detector.Unregister(s);
            _siteViews.Clear();
            _overland = null;
            _overlandView = null;
            _detector.Range = 2.6f;
            if (_regionTexture != null) { Destroy(_regionTexture); _regionTexture = null; }
            _compass = null;
            _regionMap = null;
            _whereLine = null;
        }

        // ----- Walking and the clock -----------------------------------------------------------------------------

        private void UpdateOverland()
        {
            if (!InOverland || Player == null || _session == null || _travelling) return;
            var keys = Keyboard.current;
            if (keys != null && keys.nKey.wasPressedThisFrame && !WindowOpen) ToggleRegionMap();
            UpdateCompass();
            UpdateOverlandLife(); // Phase 24
            _stepTimer -= Time.unscaledDeltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = 0.5f;
            var at = Player.transform.position;
            var moved = Vector3.Distance(new Vector3(at.x, 0, at.z), new Vector3(_lastStep.x, 0, _lastStep.z));
            if (moved < 0.5f) { _ambientSince = Time.unscaledTime; return; }
            StepClock(_lastStep, at);
            _lastStep = at;
            UpgradeNearestSilhouette(at); // one town at a time, as the player comes near
            var result = _session.Dispatch(new WalkOverlandCommand(Player.CurrentPose));
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Message)) Hud?.ShowMessage(result); // "Found: …", or what was met
            if (!result.IsSuccess && result.Message != null && result.Message.Contains("ford")) { Hud?.ShowMessage(result); Player.Teleport(_session.GetPlayer().Pose); } // Phase 24: a ford in flood turns the player back
            var pending = _session.GetPendingOverlandEncounter();
            if (pending != null && Vector2.Distance(new Vector2(pending.X, pending.Z), new Vector2(at.x, at.z)) > 200f) AnswerEncounter(OverlandChoice.BackOff); // walked away: left unresolved, as backing off
            RefreshEncounterPanel();
        }

        /// <summary>
        /// The travel time of a stretch walked, beyond the ambient minutes the living world already ran while it was walked
        /// (running on a road matches the ambient pace; slow ground adds the rest). Whole minutes are advanced; the fraction carries.
        /// </summary>
        private void StepClock(Vector3 from, Vector3 to)
        {
            var terrain = _overlandView?.Terrain;
            if (terrain == null) return;
            var time = _session.GetWorldTime();
            var night = time != null && TravelRules.IsNight(time.MinuteOfDay);
            var travel = terrain.WalkMinutes(from.x, from.z, to.x, to.z, night);
            var overview = _session.GetOverview();
            var playing = _runtime?.Playback != null && _runtime.Playback.IsPlaying;
            var ambient = playing ? (Time.unscaledTime - _ambientSince) * Realmkeeper.Application.SimulationPlayback.LivingMinutesPerSecond(overview.Speed) : 0.0;
            _ambientSince = Time.unscaledTime;
            _owedMinutes += Math.Max(0.0, travel - ambient);
            var whole = (int)Math.Floor(_owedMinutes);
            if (whole < 1) return;
            _owedMinutes -= whole;
            _session.Execute(new AdvanceTimeCommand(Math.Min(whole, AdvanceTimeCommand.MaxMinutes)));
        }

        /// <summary>Phase 23: the time owed to the clock not yet advanced (whole minutes are advanced as they accrue).</summary>
        public double OwedMinutes => _owedMinutes;

        // ----- Transitions -----------------------------------------------------------------------------------------

        private void OnSiteChosen(OverlandSiteView v)
        {
            var site = v.Site;
            switch (site.Kind)
            {
                case OverlandSiteKind.Settlement:
                    if (Guid.TryParse(site.Id, out var s)) Travel(site.Name, "Entering the settlement…", () => _session.Dispatch(new EnterSettlementFromOverlandCommand(new SettlementId(s))));
                    break;
                case OverlandSiteKind.Place:
                    Travel(site.Name, "Going inside…", () => _session.Dispatch(new Realmkeeper.Application.Places.EnterPlaceCommand(site.Id)));
                    break;
                case OverlandSiteKind.Gateway:
                    Travel(site.Usable ? "Beyond the gateway" : site.Name, "Passing through…", () => _session.Dispatch(new UseGatewayCommand(site.Id)));
                    break;
                default:
                    if (Guid.TryParse(site.Id, out var r)) Travel(site.Name.Replace("The way on to ", string.Empty), "Taking the road on…", () => _session.Dispatch(new CrossRegionCommand(new RegionId(r))));
                    break;
            }
        }

        /// <summary>Out into the country, from a settlement (button or O).</summary>
        public void GoOutIntoCountry()
        {
            if (_session == null || InOverland || InPlace) return;
            var here = _streamer.Current != null ? _session.FindSettlement(_streamer.Current.SettlementId)?.Name : null;
            RecordPose();
            Travel(here != null ? "The country around " + here : "The country", "Going out…", () => _session.Dispatch(new EnterOverlandCommand()));
        }

        /// <summary>
        /// A transition behind the loading screen: shown, a frame for it to draw, the command (whose world change unloads
        /// this environment and builds the next), a frame, hidden. A refusal shows why and leaves everything as it was.
        /// </summary>
        public void Travel(string destination, string status, Func<CommandResult> act)
        {
            if (_travelling || act == null) return;
            if (_loading == null) _loading = LoadingScreen.Create(transform);
            StartCoroutine(TravelRoutine(destination, status, act));
        }

        private IEnumerator TravelRoutine(string destination, string status, Func<CommandResult> act)
        {
            _travelling = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            if (InOverland) StepClock(_lastStep, Player != null ? Player.transform.position : _lastStep); // the last steps count
            if (InOverland && Player != null) _session.Dispatch(new WalkOverlandCommand(Player.CurrentPose));
            _loading.Show(destination, status);
            yield return null;
            yield return null; // the screen is drawn before the work starts
            _loading.Progress(0.35f, status);
            CommandResult result;
            try { result = act(); }
            catch (Exception e) { result = CommandResult.InvalidState("Something went wrong: " + e.Message); Debug.LogException(e); }
            LastTransitionMessage = ResidentView.Describe(result);
            if (!result.IsSuccess)
            {
                _loading.Fail(LastTransitionMessage);
                yield return new WaitForSecondsRealtime(1.6f);
                _loading.Hide();
                Hud?.ShowMessage(result);
                _travelling = false;
                yield break;
            }

            _loading.Progress(0.85f, "Arriving…");
            yield return null;
            LastTransitionMs = (float)clock.Elapsed.TotalMilliseconds;
            _loading.Progress(1f, null);
            yield return null;
            _loading.Hide();
            Hud?.ShowMessage(result);
            _travelling = false;
            Debug.Log($"Realmkeeper: travelled to {destination} in {LastTransitionMs:0} ms.");
        }

        // ----- The HUD: compass, where, the region map ------------------------------------------------------------

        private void SetupOverlandHud()
        {
            if (Hud?.Root == null) return;
            _compass = new VisualElement { name = "xk-compass" };
            _compass.style.position = Position.Absolute;
            _compass.style.top = 8;
            _compass.style.left = Length.Percent(30);
            _compass.style.right = Length.Percent(30);
            _compass.style.height = 30;
            _compass.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.72f);
            _compass.style.borderBottomWidth = 1;
            _compass.style.borderBottomColor = new Color(0.72f, 0.54f, 0.29f);
            _compass.style.overflow = Overflow.Hidden;
            _compass.pickingMode = PickingMode.Ignore;
            _compassMarks = new VisualElement();
            _compassMarks.style.position = Position.Absolute;
            _compassMarks.style.left = 0;
            _compassMarks.style.right = 0;
            _compassMarks.style.top = 0;
            _compassMarks.style.bottom = 0;
            _compass.Add(_compassMarks);
            Hud.Root.Add(_compass);

            _whereLine = new Label { name = "xk-overland-where" };
            _whereLine.style.position = Position.Absolute;
            _whereLine.style.top = 42;
            _whereLine.style.left = Length.Percent(30);
            _whereLine.style.right = Length.Percent(30);
            _whereLine.style.unityTextAlign = TextAnchor.MiddleCenter;
            _whereLine.style.color = new Color(0.92f, 0.86f, 0.72f);
            _whereLine.style.fontSize = 14;
            _whereLine.pickingMode = PickingMode.Ignore;
            Hud.Root.Add(_whereLine);
        }

        private void RefreshOverlandHud()
        {
            if (_whereLine == null || _overlandView == null || Player == null) return;
            var t = _overlandView.Terrain;
            var at = Player.transform.position;
            var cell = t.CellAt(at.x, at.z);
            OverlandSite nearest = null;
            var best = float.MaxValue;
            foreach (var s in _overlandView.Sites)
            {
                if (!s.Known) continue;
                var d = Vector2.Distance(new Vector2(s.X, s.Z), new Vector2(at.x, at.z));
                if (d < best) { best = d; nearest = s; }
            }

            var realm = _overlandView.RealmId == RealmIds.Surface ? string.Empty : " · " + _overlandView.RealmName;
            var ground = cell.Road ? "on the road" : Words(cell.Biome);
            if (ground != _hudGround) // the HUD's location panel: where, and the ground underfoot (set when it changes)
            {
                _hudGround = ground;
                Hud?.SetLocation(_overlandView.RegionName, "In the country" + realm + " · " + ground, _session.GetOverview().Date.ToString());
            }

            _whereLine.text = $"{_overlandView.RegionName}{realm} · {(cell.Road ? "on the road" : "the " + Words(cell.Biome))}" +
                              (nearest != null ? $" · {nearest.Name} {Distance(best)}" : string.Empty);
        }

        private string _hudGround;

        private static string Distance(float m) => m < 1000f ? $"{m:0} m" : $"{m / 1000f:0.0} km";

        private static string Words(Biome b)
        {
            switch (b)
            {
                case Biome.DenseForest: return "deep forest";
                case Biome.Forest: return "forest";
                case Biome.Grassland: return "grassland";
                case Biome.Meadow: return "meadows";
                case Biome.Hills: return "hills";
                case Biome.Rocky: return "rocky slopes";
                case Biome.Marsh: return "marsh";
                case Biome.Valley: return "valley floor";
                case Biome.Heath: return "heath";
                case Biome.Shore: return "shore";
                case Biome.Shallows: return "shallows";
                default: return b.ToString().ToLowerInvariant();
            }
        }

        private float _compassTimer;

        private void UpdateCompass()
        {
            if (_compassMarks == null || CameraRig == null || _overlandView == null) return;
            _compassTimer -= Time.unscaledDeltaTime;
            if (_compassTimer > 0f) return;
            _compassTimer = 0.1f;
            RefreshOverlandHud();
            _compassMarks.Clear();
            var fwd = CameraRig.transform.forward;
            var heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            void Mark(float bearing, string text, Color colour, int size)
            {
                var delta = Mathf.DeltaAngle(heading, bearing);
                if (Mathf.Abs(delta) > 90f) return;
                var l = new Label(text);
                l.style.position = Position.Absolute;
                l.style.left = Length.Percent(50f + delta / 180f * 100f);
                l.style.top = 4;
                l.style.translate = new Translate(Length.Percent(-50), 0);
                l.style.color = colour;
                l.style.fontSize = size;
                l.pickingMode = PickingMode.Ignore;
                _compassMarks.Add(l);
            }

            Mark(0f, "N", new Color(0.95f, 0.82f, 0.5f), 15);
            Mark(90f, "E", new Color(0.85f, 0.8f, 0.7f), 13);
            Mark(180f, "S", new Color(0.85f, 0.8f, 0.7f), 13);
            Mark(-90f, "W", new Color(0.85f, 0.8f, 0.7f), 13);
            var at = Player.transform.position;
            foreach (var s in _overlandView.Sites)
            {
                if (!s.Known) continue; // nothing undiscovered is given away
                var bearing = Mathf.Atan2(s.X - at.x, s.Z - at.z) * Mathf.Rad2Deg;
                var colour = s.Kind == OverlandSiteKind.Settlement ? new Color(0.95f, 0.8f, 0.55f) : s.Kind == OverlandSiteKind.Gateway ? new Color(0.75f, 0.6f, 1f) : s.Kind == OverlandSiteKind.RegionExit ? new Color(0.7f, 0.85f, 0.95f) : new Color(0.85f, 0.75f, 0.65f);
                Mark(bearing, s.Kind == OverlandSiteKind.Settlement ? "◆" : s.Kind == OverlandSiteKind.RegionExit ? "➜" : "●", colour, 12);
            }

            // Phase 24: found landmarks, and the waypoint (or the way on toward it) in gold.
            foreach (var l in _landmarkStates)
            {
                if (!l.Found) continue;
                Mark(Mathf.Atan2(l.Landmark.X - at.x, l.Landmark.Z - at.z) * Mathf.Rad2Deg, "·", new Color(0.8f, 0.78f, 0.7f), 16);
            }

            if (_waypoint != null)
            {
                var target = _waypoint.InThisRegion ? new Vector2(_waypoint.X, _waypoint.Z) : _overlandView.Sites.Find(s => s.Kind == OverlandSiteKind.RegionExit && s.Id == _waypoint.NextExitId) is OverlandSite exit ? new Vector2(exit.X, exit.Z) : (Vector2?)null;
                if (target != null) Mark(Mathf.Atan2(target.Value.x - at.x, target.Value.y - at.z) * Mathf.Rad2Deg, "▼", new Color(1f, 0.78f, 0.25f), 16);
            }
        }

        public bool RegionMapShown => _regionMap != null && _regionMap.style.display == DisplayStyle.Flex;

        /// <summary>The region map (N): the ground's colours from the terrain, the roads, the known sites, and the player.</summary>
        public void ToggleRegionMap()
        {
            if (Hud?.Root == null || _overlandView == null) return;
            if (_regionMap == null)
            {
                var t = _overlandView.Terrain;
                var map = t.Map;
                _regionTexture = new Texture2D(map.Width, map.Depth, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, name = "Region map" };
                for (var z = 0; z < map.Depth; z++)
                    for (var x = 0; x < map.Width; x++)
                    {
                        var c = t.Cell(x, z);
                        Color col;
                        if (c.Road) col = new Color(0.62f, 0.5f, 0.33f);
                        else if (!c.Passable && c.Biome != Biome.Mountain) col = new Color(0.22f, 0.36f, 0.46f);
                        else if (c.Biome == Biome.Shallows || c.Biome == Biome.River) col = new Color(0.32f, 0.48f, 0.56f);
                        else if (c.Biome == Biome.Mountain) col = new Color(0.55f, 0.52f, 0.5f);
                        else col = BiomeMapColour(c.Biome);
                        var shade = 0.85f + 0.3f * map.GetElevation(x, z);
                        _regionTexture.SetPixel(x, z, new Color(col.r * shade, col.g * shade, col.b * shade));
                    }

                _regionTexture.Apply();
                _regionMap = new VisualElement { name = "xk-region-map" };
                _regionMap.style.position = Position.Absolute;
                _regionMap.style.right = 20;
                _regionMap.style.top = 80;
                _regionMap.style.width = 420;
                _regionMap.style.height = 446;
                _regionMap.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.92f);
                _regionMap.style.borderTopWidth = _regionMap.style.borderBottomWidth = _regionMap.style.borderLeftWidth = _regionMap.style.borderRightWidth = 1;
                _regionMap.style.borderTopColor = _regionMap.style.borderBottomColor = _regionMap.style.borderLeftColor = _regionMap.style.borderRightColor = new Color(0.72f, 0.54f, 0.29f);
                var title = new Label(_overlandView.RegionName + "  (N to close)");
                title.style.color = new Color(0.91f, 0.81f, 0.61f);
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.height = 26;
                _regionMap.Add(title);
                var image = new VisualElement { name = "xk-region-map-image" };
                image.style.width = 400;
                image.style.height = 400;
                image.style.marginLeft = 10;
                image.style.backgroundImage = new StyleBackground(_regionTexture);
                _regionMap.Add(image);
                Hud.Root.Add(_regionMap);
                _regionMap.style.display = DisplayStyle.None;
            }

            var show = !RegionMapShown;
            _regionMap.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) DrawRegionMapMarks();
        }

        private void DrawRegionMapMarks()
        {
            var image = _regionMap?.Q<VisualElement>("xk-region-map-image");
            if (image == null || _overlandView == null || Player == null) return;
            image.Clear();
            var size = _overlandView.Terrain.SizeX;
            void Dot(float x, float z, string text, Color colour, int font)
            {
                var l = new Label(text);
                l.style.position = Position.Absolute;
                l.style.left = Length.Percent(x / size * 100f);
                l.style.top = Length.Percent(100f - z / size * 100f); // north up: the texture's first row (z = 0, south) is drawn at the bottom
                l.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                l.style.color = colour;
                l.style.fontSize = font;
                l.pickingMode = PickingMode.Ignore;
                image.Add(l);
            }

            foreach (var s in _overlandView.Sites)
            {
                if (!s.Known) continue;
                var mark = s.Kind == OverlandSiteKind.Settlement ? "◆ " + s.Name : s.Kind == OverlandSiteKind.RegionExit ? "➜" : s.Kind == OverlandSiteKind.Gateway ? "◎" : "●";
                Dot(s.X, s.Z, mark, s.Kind == OverlandSiteKind.Gateway ? new Color(0.75f, 0.6f, 1f) : new Color(1f, 0.92f, 0.75f), s.Kind == OverlandSiteKind.Settlement ? 11 : 13);
            }

            foreach (var l in _landmarkStates) if (l.Found) Dot(l.Landmark.X, l.Landmark.Z, "·", new Color(0.92f, 0.9f, 0.82f), 18); // Phase 24
            if (_waypoint != null && _waypoint.InThisRegion) Dot(_waypoint.X, _waypoint.Z, "▼", new Color(1f, 0.78f, 0.25f), 14);
            var p = Player.transform.position;
            Dot(p.x, p.z, "▲", new Color(1f, 0.35f, 0.25f), 16);
        }

        private static Color BiomeMapColour(Biome b)
        {
            switch (b)
            {
                case Biome.DenseForest: return new Color(0.18f, 0.33f, 0.15f);
                case Biome.Forest: return new Color(0.26f, 0.42f, 0.2f);
                case Biome.Hills: return new Color(0.5f, 0.54f, 0.33f);
                case Biome.Rocky: return new Color(0.56f, 0.53f, 0.46f);
                case Biome.Marsh: return new Color(0.33f, 0.4f, 0.27f);
                case Biome.Heath: return new Color(0.55f, 0.47f, 0.34f);
                case Biome.Shore: return new Color(0.74f, 0.68f, 0.5f);
                case Biome.Meadow: return new Color(0.5f, 0.62f, 0.3f);
                default: return new Color(0.42f, 0.57f, 0.27f);
            }
        }
    }
}
