using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Simulation.Npcs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 5 of the settlement visit: the living world and the player's belongings. Time runs minute by
    /// minute while the player walks the settlement; the light follows it; residents walk the streets to
    /// where the world says they are this hour and do what it says they are doing; shops show their stock
    /// on their shelves and trade at the counter; the Player Hub shows the character and everything they
    /// own, beside a live view of the body that wears it. Every change goes through a command; the scene
    /// only shows world data.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private readonly Dictionary<string, ShopInteriorView> _shops = new Dictionary<string, ShopInteriorView>(StringComparer.Ordinal);
        private readonly Dictionary<NpcId, (LocationId place, ActivityKind activity)> _planned = new Dictionary<NpcId, (LocationId, ActivityKind)>();
        private readonly Dictionary<NpcId, LayoutBuilding> _inside = new Dictionary<NpcId, LayoutBuilding>();
        private long _lastAbsolute = -1;
        private bool _wasPlaying;
        private GeneratedAssets _assets;
        private Label _clock;
        private Button _pauseButton;
        private Button _speedButton;

        public TradeWindow Trade { get; private set; }
        public PlayerHubWindow HubWindow { get; private set; }
        public CharacterPreview Preview { get; private set; }
        public IReadOnlyDictionary<string, ShopInteriorView> Shops => _shops;

        /// <summary>The shop whose window is open, or null.</summary>
        public ShopInteriorView TradingAt { get; private set; }

        public bool WindowOpen => (Trade != null && Trade.IsOpen) || (HubWindow != null && HubWindow.IsOpen);

        /// <summary>The atmosphere (lighting) of the current visit.</summary>
        public ExplorationAtmosphere Atmosphere => _atmosphere;

        // ----- Setup -------------------------------------------------------------------------------------

        private PlayerLook CurrentLook() => PlayerLook.From(_session?.GetPlayerHub());

        private GeneratedAssets Assets()
        {
            if (_assets != null) return _assets;
            if (EnvironmentRoot == null) return null;
            _assets = EnvironmentRoot.GetComponent<GeneratedAssets>() ?? EnvironmentRoot.AddComponent<GeneratedAssets>();
            return _assets;
        }

        private void SetupLiving(LocalEnvironmentLayout layout)
        {
            _planned.Clear();
            _inside.Clear();
            _lastAbsolute = -1;
            if (Catalog != null && _atmosphere != null) _atmosphere.PrepareNight(EnvironmentRoot.transform, layout, Catalog);

            // Shop interiors in the buildings the world says are shops.
            if (Catalog != null)
            {
                var interiors = new GameObject("Shop interiors").transform;
                foreach (var shop in _session.GetShops(layout.SettlementId))
                {
                    var b = LocalPaths.Find(layout, shop.LayoutKey);
                    if (b == null || b.Fit == ShopFit.None) continue;
                    var view = ShopInteriorView.Create(interiors, b, layout, shop.ShopId, Catalog, Assets());
                    view.TradeRequested += OpenTrade;
                    _shops[b.Key] = view;
                    _detector.Register(view);
                }
            }

            // Windows and the clock in the HUD.
            if (Hud != null && Hud.Root != null)
            {
                Trade = new TradeWindow(Hud.Root);
                Trade.BuyRequested += (item, price) => Buy(item, price);
                Trade.SellRequested += (stack, price) => Sell(stack, price);
                Trade.CloseRequested += CloseWindows;
                HubWindow = new PlayerHubWindow(Hud.Root);
                HubWindow.CloseRequested += CloseWindows;
                HubWindow.EquipRequested += s => HubCommand(new EquipItemCommand(s));
                HubWindow.UnequipRequested += s => HubCommand(new UnequipItemCommand(s));
                HubWindow.UseRequested += s => HubCommand(new UseItemCommand(s));
                HubWindow.SplitRequested += (s, n) => HubCommand(new SplitStackCommand(s, n));
                HubWindow.CombineRequested += (a, b) => HubCommand(new CombineStacksCommand(a, b));
                HubWindow.DiscardRequested += (s, n) => HubCommand(new DiscardItemCommand(s, n));
                HubWindow.ProfileSaved += (n, age, bg, d) => HubCommand(new SetPlayerProfileCommand(n, age, bg, d));
                HubWindow.AppearanceChosen += a => HubCommand(new SetPlayerAppearanceCommand(a));
                HubWindow.PreviewTurned += d => { Preview?.Rotate(d); Preview?.RenderNow(); };
                HubWindow.PreviewZoomed += d => { Preview?.Zoom(d); Preview?.RenderNow(); };
                AddClock();
            }

            if (CameraRig != null) CameraRig.Camera.cullingMask &= ~(1 << CharacterPreview.Layer);

            var playback = _runtime?.Playback;
            if (playback != null)
            {
                _wasPlaying = playback.IsPlaying;
                playback.LivingWorld = true;
                playback.Play(); // the settlement lives while the player walks it; P pauses
            }

            RefreshLiving(snap: true);
        }

        private void TeardownLiving()
        {
            var playback = _runtime?.Playback;
            if (playback != null && playback.LivingWorld)
            {
                playback.LivingWorld = false;
                if (!_wasPlaying) playback.Pause();
            }

            foreach (var shop in _shops.Values) if (shop != null) shop.TradeRequested -= OpenTrade;
            _shops.Clear();
            _planned.Clear();
            _inside.Clear();
            Trade = null;
            HubWindow = null;
            Preview = null;
            TradingAt = null;
            _assets = null;
            _clock = null;
        }

        private void AddClock()
        {
            var panel = Hud.Root.Q<VisualElement>(className: "xk-location");
            if (panel == null) return;
            var row = new VisualElement { name = "xk-clock-row" };
            row.AddToClassList("xk-clock-row");
            _clock = new Label("--:--") { name = "xk-clock" };
            _clock.AddToClassList("xk-clock");
            row.Add(_clock);
            _pauseButton = TimeButton(row, "Pause (P)", "xk-time-pause", TogglePause);
            _speedButton = TimeButton(row, "×1", "xk-time-speed", CycleSpeed);
            TimeButton(row, "Wait 1 h (T)", "xk-time-wait", () => WaitHour());
            TimeButton(row, "Hub (H)", "xk-open-hub", () => ToggleHub());
            panel.Add(row);
        }

        private static Button TimeButton(VisualElement row, string text, string name, Action act)
        {
            var b = new Button(act) { text = text, name = name };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-time-button");
            row.Add(b);
            return b;
        }

        // ----- The living settlement -----------------------------------------------------------------------

        /// <summary>Brings the scene in line with world data after a change: light, people, shops, the player's body and windows.</summary>
        private void RefreshLiving(bool snap)
        {
            if (!IsExploring || _session == null || !_session.HasWorld) return;
            var time = _session.GetWorldTime();
            if (time == null) return;
            if (_atmosphere != null && _atmosphere.Sun != null) _atmosphere.SetTime(time.MinuteOfDay);
            RefreshPresence(time, snap);
            RefreshShops();
            RefreshPlayerBody();
            RefreshClock(time);
            if (Trade != null && Trade.IsOpen && TradingAt != null) Trade.Show(_session.GetShopDetail(TradingAt.ShopId));
        }

        /// <summary>
        /// Gives every resident here their place and activity this hour. Someone whose place changed walks
        /// there along the streets; after a jump in time (waiting, a fast clock) they are placed directly.
        /// </summary>
        public void RefreshPresence(WorldTimeSummary time, bool snap)
        {
            var layout = _streamer.Current;
            if (layout == null) return;
            var absolute = time.Date.ToDayNumber() * 1440L + time.MinuteOfDay;
            var jumped = _lastAbsolute >= 0 && absolute - _lastAbsolute > 30;
            _lastAbsolute = absolute;

            var presences = _session.GetPresenceIn(layout.SettlementId);
            var slots = new Dictionary<string, List<NpcPresence>>(StringComparer.Ordinal);
            foreach (var p in presences)
            {
                var key = (p.LayoutKey ?? "-") + "|" + p.LocationKind;
                if (!slots.TryGetValue(key, out var list)) slots[key] = list = new List<NpcPresence>();
                list.Add(p);
            }

            foreach (var list in slots.Values)
                list.Sort((a, b) => a.IsShopkeeper != b.IsShopkeeper ? (a.IsShopkeeper ? -1 : 1) : string.CompareOrdinal(a.NpcId.ToString(), b.NpcId.ToString()));

            var seen = new HashSet<NpcId>();
            foreach (var p in presences)
            {
                seen.Add(p.NpcId);
                if (!_residents.TryGetValue(p.NpcId, out var view)) view = SpawnArrival(p, layout);
                if (view == null) continue;
                if (!snap && _planned.TryGetValue(p.NpcId, out var prev) && prev.place == p.LocationId && prev.activity == p.Activity) continue;
                _planned[p.NpcId] = (p.LocationId, p.Activity);

                var list = slots[(p.LayoutKey ?? "-") + "|" + p.LocationKind];
                var n = list.IndexOf(p);
                var building = LocalPaths.Find(layout, p.LayoutKey);
                var keeperHere = p.IsShopkeeper && building != null && building.Fit != ShopFit.None && n == 0;
                var at = LocalPaths.SpotAt(layout, p.LayoutKey, p.LocationKind, n, keeperHere);
                LocalPoint spot;
                float wander;
                var indoors = false;
                if (at.HasValue)
                {
                    spot = at.Value.spot;
                    wander = at.Value.wander;
                    indoors = at.Value.indoors;
                }
                else
                {
                    spot = new LocalPoint(view.Destination.x, view.Destination.z);
                    wander = 1.5f;
                }

                var hide = p.Activity == ActivityKind.Sleeping || p.LocationKind == LocationKind.Residence;
                float? facing = null;
                if (building != null)
                {
                    if (indoors && keeperHere) facing = LocalPaths.FacingOut(building);
                    else if (!indoors && (p.Activity == ActivityKind.Working || p.Activity == ActivityKind.Trading)) facing = LocalPaths.FacingOut(building) + 180f;
                }

                _inside.TryGetValue(p.NpcId, out var from);
                var to = indoors ? building : null;
                var here = new LocalPoint(view.transform.localPosition.x, view.transform.localPosition.z);
                var route = LocalPaths.Route(layout, here, view.IsHidden ? null : from, spot, to);
                var points = new List<Vector3>(route.Count);
                foreach (var q in route) points.Add(new Vector3(q.X, 0f, q.Z));
                if (view.IsHidden && building != null && !hide && from != null) points[0] = new Vector3(LocalPaths.DoorOutside(from).X, 0f, LocalPaths.DoorOutside(from).Z);
                view.Plan(p.LocationId, p.Activity, p.Reason, new Vector3(spot.X, 0f, spot.Z), wander, facing, points, hide, snap || jumped);
                if (to != null) _inside[p.NpcId] = to;
                else _inside.Remove(p.NpcId);
                if (hide && building != null) _inside[p.NpcId] = building; // indoors at home
            }

            // Anyone no longer here this hour has left the settlement: they walk out by the gate.
            foreach (var pair in _residents)
            {
                if (seen.Contains(pair.Key) || pair.Value == null || pair.Value.IsHidden) continue;
                if (_planned.TryGetValue(pair.Key, out var prev) && prev.place.IsEmpty) continue;
                _planned[pair.Key] = (default, ActivityKind.None);
                var gate = new LocalPoint(2.5f, -layout.HalfExtent + 2f);
                var here = new LocalPoint(pair.Value.transform.localPosition.x, pair.Value.transform.localPosition.z);
                _inside.TryGetValue(pair.Key, out var from);
                var route = LocalPaths.Route(layout, here, from, gate, null);
                var points = new List<Vector3>();
                foreach (var q in route) points.Add(new Vector3(q.X, 0f, q.Z));
                pair.Value.Plan(default, ActivityKind.None, "Away from the settlement.", new Vector3(gate.X, 0f, gate.Z), 0f, null, points, true, snap || jumped);
                _inside.Remove(pair.Key);
            }
        }

        private ResidentView SpawnArrival(NpcPresence p, LocalEnvironmentLayout layout)
        {
            var npc = _session.FindNpc(p.NpcId);
            if (npc == null || Player == null) return null;
            var parent = _residentsRoot;
            var gate = new LayoutResidentSpot { NpcId = p.NpcId, Role = p.Role, Position = new LocalPoint(2.5f, -layout.HalfExtent + 3f), WanderRadius = 1.5f };
            var view = ResidentView.Create(parent, gate, npc, _session, Player.transform, _materials, Catalog);
            _residents[p.NpcId] = view;
            _detector.Register(view);
            return view;
        }

        private void RefreshShops()
        {
            foreach (var view in _shops.Values)
                if (view != null) view.Refresh(_session.GetShopDetail(view.ShopId));
        }

        private void RefreshPlayerBody()
        {
            if (Player == null) return;
            var hub = _session.GetPlayerHub();
            if (hub == null) return;
            Player.SpeedMultiplier = hub.SpeedFactor;
            var look = PlayerLook.From(hub);
            if (Catalog != null && look.Signature != Player.LookSignature) Player.RebuildBody(look, Catalog, Assets());
            if (HubWindow != null && HubWindow.IsOpen) ShowHub(null, false);
        }

        private void RefreshClock(WorldTimeSummary time)
        {
            if (_clock == null) return;
            _clock.text = time.Clock + " · " + time.DayPart;
            var playback = _runtime?.Playback;
            if (_pauseButton != null) _pauseButton.text = playback != null && playback.IsPlaying ? "Pause (P)" : "Play (P)";
            if (_speedButton != null)
            {
                var speed = _session.GetOverview().Speed;
                _speedButton.text = speed == SimulationSpeed.Slow ? "×¼" : speed == SimulationSpeed.Normal ? "×1" : speed == SimulationSpeed.Fast ? "×5" : "×30";
            }
        }

        // ----- Time controls ----------------------------------------------------------------------------

        public void TogglePause()
        {
            _runtime?.Playback?.Toggle();
            var time = _session?.GetWorldTime();
            if (time != null) RefreshClock(time);
        }

        public void CycleSpeed()
        {
            if (_session == null) return;
            var next = _session.GetOverview().Speed switch
            {
                SimulationSpeed.Slow => SimulationSpeed.Normal,
                SimulationSpeed.Normal => SimulationSpeed.Fast,
                SimulationSpeed.Fast => SimulationSpeed.VeryFast,
                _ => SimulationSpeed.Slow,
            };
            _session.Dispatch(new SetSimulationSpeedCommand(next));
            var time = _session.GetWorldTime();
            if (time != null) RefreshClock(time);
        }

        /// <summary>Lets an hour pass (residents are placed where the next hour finds them).</summary>
        public AdvanceResult WaitHour(int minutes = 60)
        {
            if (_session == null) return null;
            Hud?.HideResponse();
            TalkingTo = null;
            var result = _session.Execute(new AdvanceTimeCommand(minutes));
            if (!result.IsSuccess) Hud?.ShowMessage(result);
            return result;
        }

        // ----- Trade -------------------------------------------------------------------------------------

        /// <summary>Opens the trade window of a shop here (at its counter, or asked of its keeper).</summary>
        public void OpenTrade(ShopInteriorView shop)
        {
            if (shop == null || Trade == null) return;
            Hud?.HideResponse();
            HubWindow?.Hide();
            TradingAt = shop;
            Trade.Show(_session.GetShopDetail(shop.ShopId));
            SetWindowMode(true);
        }

        public void OpenTrade(ShopId shopId)
        {
            foreach (var view in _shops.Values)
                if (view.ShopId == shopId) { OpenTrade(view); return; }
        }

        public CommandResult Buy(string itemId, int unitPrice)
        {
            if (TradingAt == null) return CommandResult.InvalidState("No shop is open.");
            var result = _session.Dispatch(new BuyItemCommand(TradingAt.ShopId, itemId, 1, unitPrice));
            Trade?.Show(_session.GetShopDetail(TradingAt.ShopId), ResidentView.Describe(result), !result.IsSuccess);
            if (result.IsSuccess) _residentKeeperTalk(TradingAt);
            return result;
        }

        public CommandResult Sell(ItemStackId stack, int unitPrice)
        {
            if (TradingAt == null) return CommandResult.InvalidState("No shop is open.");
            var result = _session.Dispatch(new SellItemCommand(TradingAt.ShopId, stack, 1, unitPrice));
            Trade?.Show(_session.GetShopDetail(TradingAt.ShopId), ResidentView.Describe(result), !result.IsSuccess);
            if (result.IsSuccess) _residentKeeperTalk(TradingAt);
            return result;
        }

        private void _residentKeeperTalk(ShopInteriorView shop)
        {
            var summary = _session.GetShopDetail(shop.ShopId)?.Summary;
            if (summary != null && _residents.TryGetValue(summary.KeeperId, out var keeper)) keeper.Animation?.Talk(2.5f);
        }

        // ----- Player Hub --------------------------------------------------------------------------------

        public void ToggleHub()
        {
            if (HubWindow == null) return;
            if (HubWindow.IsOpen) CloseWindows();
            else ShowHub(null, false);
        }

        private void ShowHub(string message, bool failed)
        {
            if (HubWindow == null) return;
            Trade?.Hide();
            TradingAt = null;
            var hub = _session.GetPlayerHub();
            if (Preview == null && Catalog != null && EnvironmentRoot != null)
            {
                Preview = CharacterPreview.Create(EnvironmentRoot.transform);
                HubWindow.SetPreview(Preview.Texture);
            }

            if (Preview != null)
            {
                Preview.SetActive(true);
                Preview.Show(PlayerLook.From(hub), Catalog, Assets());
                Preview.RenderNow();
            }

            Hud?.HideResponse();
            HubWindow.Show(hub, _session.GetWorldTime(), message, failed);
            SetWindowMode(true);
        }

        private void HubCommand<T>(T command) where T : ICommand
        {
            var result = _session.Dispatch(command);
            ShowHub(ResidentView.Describe(result), !result.IsSuccess);
        }

        public void CloseWindows()
        {
            Trade?.Hide();
            HubWindow?.Hide();
            Preview?.SetActive(false);
            TradingAt = null;
            SetWindowMode(false);
        }

        private void SetWindowMode(bool open)
        {
            if (Player != null) Player.MovementEnabled = !open;
            if (open) SetCursorFree(true);
        }

        /// <summary>Phase 5 keys: H hub, T wait an hour, P pause, Esc closes a window.</summary>
        private void UpdateLivingInput()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.hKey.wasPressedThisFrame) ToggleHub();
            if (WindowOpen && keys.escapeKey.wasPressedThisFrame) CloseWindows();
            if (WindowOpen) return;
            if (keys.tKey.wasPressedThisFrame) WaitHour();
            if (keys.pKey.wasPressedThisFrame) TogglePause();
        }
    }
}
