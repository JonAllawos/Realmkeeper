using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Phase 16: the world atlas. It draws the connected world from <see cref="WorldSession.GetAtlas"/>: regions (blank
    /// until heard of), settlements, roads and crossings (closed ones dark red), groups the player can see, events as the
    /// player knows them (rumoured or confirmed, as of when), and the party's place on the road. Eight layers switch what
    /// is drawn. Selecting a settlement plans the ways there (estimates from the route data) and sets out; the travel strip
    /// runs the journey hour by hour and answers what bars the road. Every change is a command; nothing here is state.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        public static readonly IReadOnlyList<string> AtlasLayers = new[] { "Terrain", "Settlements", "Roads", "Factions", "Economy", "Threats", "Events", "Travel" };

        private VisualElement _atlas;
        private VisualElement _atlasMap;
        private VisualElement _atlasMarks;
        private ScrollView _atlasSide;
        private VisualElement _atlasTravel;
        private Label _atlasLegend;
        private readonly HashSet<string> _layersOn = new HashSet<string>(AtlasLayers);
        private AtlasView _atlasView;
        private string _atlasSelected;

        /// <summary>The atlas as last drawn (for tests and captures).</summary>
        public AtlasView AtlasSnapshot => _atlasView;
        public VisualElement AtlasRoot => _atlas;
        public bool AtlasOpen => _atlas != null && !_atlas.ClassListContains("rk-hidden");
        public string AtlasSelection => _atlasSelected;
        public IReadOnlyCollection<string> AtlasLayersOn => _layersOn;

        private void BindAtlas()
        {
            var nav = Root.Q<VisualElement>("nav");
            var world = Root.Q<Button>("nav-world");
            if (nav == null || world == null) return;
            var button = new Button(() => ToggleAtlas()) { name = "nav-atlas", text = "World atlas (A)" };
            button.AddToClassList("rk-nav-item");
            nav.Insert(nav.IndexOf(world) + 1, button);

            _atlas = new VisualElement { name = "rk-atlas" };
            _atlas.AddToClassList("rk-panel");
            _atlas.AddToClassList("rk-atlas");
            _atlas.AddToClassList("rk-hidden");

            var head = new VisualElement();
            head.AddToClassList("rk-atlas-head");
            var title = new Label("World atlas") { name = "rk-atlas-title" };
            title.AddToClassList("rk-atlas-title");
            head.Add(title);
            foreach (var layer in AtlasLayers)
            {
                var name = layer;
                var toggle = new Button(() => { if (!_layersOn.Remove(name)) _layersOn.Add(name); RefreshAtlas(); }) { name = "rk-atlas-layer-" + name, text = name };
                toggle.AddToClassList("rk-atlas-layer");
                head.Add(toggle);
            }

            var grow = new VisualElement();
            grow.AddToClassList("rk-grow");
            head.Add(grow);
            var close = new Button(() => ToggleAtlas(false)) { name = "rk-atlas-close", text = "Close" };
            close.AddToClassList("rk-button");
            head.Add(close);
            _atlas.Add(head);

            var body = new VisualElement();
            body.AddToClassList("rk-atlas-body");
            _atlasMap = new VisualElement { name = "rk-atlas-map" };
            _atlasMap.AddToClassList("rk-atlas-map");
            _atlasMap.generateVisualContent += DrawAtlas;
            _atlasMap.RegisterCallback<GeometryChangedEvent>(_ => PlaceMarks());
            _atlasMarks = new VisualElement { name = "rk-atlas-marks", pickingMode = PickingMode.Ignore };
            _atlasMarks.AddToClassList("rk-atlas-marks");
            _atlasMap.Add(_atlasMarks);
            body.Add(_atlasMap);
            _atlasSide = new ScrollView { name = "rk-atlas-side" };
            _atlasSide.AddToClassList("rk-atlas-side");
            body.Add(_atlasSide);
            _atlas.Add(body);

            _atlasTravel = new VisualElement { name = "rk-atlas-travel" };
            _atlasTravel.AddToClassList("rk-atlas-travel");
            _atlas.Add(_atlasTravel);
            _atlasLegend = new Label { name = "rk-atlas-legend" };
            _atlasLegend.AddToClassList("rk-atlas-legend");
            _atlas.Add(_atlasLegend);
            Root.Add(_atlas);
            Root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.A && !(e.target is TextField)) ToggleAtlas(); });
        }

        public void ToggleAtlas(bool? open = null)
        {
            if (_atlas == null) return;
            var show = open ?? !AtlasOpen;
            _atlas.EnableInClassList("rk-hidden", !show);
            if (show) _atlas.BringToFront(); // over the inspector and the event list, which stay where they were
            if (show) RefreshAtlas();
        }

        public void SelectOnAtlas(string key)
        {
            _atlasSelected = key;
            RefreshAtlas();
        }

        public void SetAtlasLayer(string layer, bool on)
        {
            if (on) _layersOn.Add(layer); else _layersOn.Remove(layer);
            RefreshAtlas();
        }

        private bool On(string layer) => _layersOn.Contains(layer);

        private void RefreshAtlas()
        {
            if (_atlas == null || !AtlasOpen) return;
            _atlasView = _session != null && _session.HasWorld ? _session.GetAtlas() : new AtlasView();
            foreach (var layer in AtlasLayers) _atlas.Q<Button>("rk-atlas-layer-" + layer)?.EnableInClassList("rk-atlas-layer--on", On(layer));
            _atlas.Q<Label>("rk-atlas-title").text = "World atlas · " + _atlasView.Clock;
            _atlasMap.MarkDirtyRepaint();
            PlaceMarks();
            FillSide();
            FillTravel();
            _atlasLegend.text = "Gold: you and your party" + (_atlasView.Party.Count > 0 ? " (" + string.Join(", ", _atlasView.Party) + ")" : string.Empty) +
                                ". Roads: pale (open), ochre (crossing), dark red (closed). Groups: C caravan, P patrol, B outlaws, R refugees. ! a matter you know of (? rumoured, ~ your news is old). " +
                                "Blank lands: not yet heard of.";
        }

        // ----- Geometry -----------------------------------------------------------------------------

        private (float w, float h) Extent()
        {
            if (_atlasView == null || _atlasView.Regions.Count == 0) return (128, 128);
            return ((_atlasView.Regions.Max(r => r.AtlasX) + 1) * AtlasQueries.RegionCells, (_atlasView.Regions.Max(r => r.AtlasZ) + 1) * AtlasQueries.RegionCells);
        }

        /// <summary>Atlas cells to the map's pixels: north up, the whole world fitted.</summary>
        private Vector2 ToScreen(float x, float z)
        {
            var (w, h) = Extent();
            var rect = _atlasMap.contentRect;
            var scale = Mathf.Min(rect.width / w, rect.height / h);
            if (scale <= 0 || float.IsNaN(scale)) scale = 1;
            var ox = (rect.width - w * scale) / 2;
            var oy = (rect.height - h * scale) / 2;
            return new Vector2(ox + x * scale, oy + (h - z) * scale);
        }

        private static Color KindColour(RegionKind k)
        {
            switch (k)
            {
                case RegionKind.Highlands: return new Color(0.42f, 0.40f, 0.36f);
                case RegionKind.Woodland: return new Color(0.20f, 0.34f, 0.20f);
                case RegionKind.Lakeland: return new Color(0.22f, 0.36f, 0.40f);
                case RegionKind.Lowlands: return new Color(0.42f, 0.44f, 0.24f);
                default: return new Color(0.30f, 0.40f, 0.24f);
            }
        }

        private static Color FactionColour(FactionId id)
        {
            var h = Mathf.Abs(id.GetHashCode() % 360) / 360f;
            return Color.HSVToRGB(h, 0.55f, 0.85f);
        }

        private void DrawAtlas(MeshGenerationContext ctx)
        {
            var v = _atlasView;
            if (v == null) return;
            var p = ctx.painter2D;
            foreach (var r in v.Regions)
            {
                var a = ToScreen(r.AtlasX * AtlasQueries.RegionCells, (r.AtlasZ + 1) * AtlasQueries.RegionCells);
                var b = ToScreen((r.AtlasX + 1) * AtlasQueries.RegionCells, r.AtlasZ * AtlasQueries.RegionCells);
                var fill = r.Discovery == RegionDiscovery.Unknown ? new Color(0.10f, 0.10f, 0.09f) : On("Terrain") ? KindColour(r.Kind) : new Color(0.22f, 0.22f, 0.20f);
                if (r.Discovery == RegionDiscovery.Rumored || r.Discovery == RegionDiscovery.Discovered) fill *= 0.8f;
                fill.a = 1f;
                p.fillColor = fill;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(new Vector2(b.x, a.y));
                p.LineTo(b);
                p.LineTo(new Vector2(a.x, b.y));
                p.ClosePath();
                p.Fill();
                p.strokeColor = r.RegionId == v.PlayerRegion ? new Color(0.86f, 0.72f, 0.40f) : new Color(0.05f, 0.05f, 0.05f);
                p.lineWidth = r.RegionId == v.PlayerRegion ? 3f : 1.5f;
                p.Stroke();
            }

            if (On("Factions"))
                foreach (var s in v.Settlements)
                {
                    if (s.FactionId.IsEmpty) continue;
                    var c = FactionColour(s.FactionId);
                    c.a = 0.28f;
                    p.fillColor = c;
                    p.BeginPath();
                    p.Arc(ToScreen(s.X, s.Z), 34f, 0f, 360f);
                    p.Fill();
                }

            if (On("Roads"))
                foreach (var r in v.Routes)
                {
                    p.strokeColor = !r.Passable ? new Color(0.55f, 0.10f, 0.08f) : r.Crossing ? new Color(0.80f, 0.62f, 0.30f) : new Color(0.82f, 0.78f, 0.66f);
                    p.lineWidth = r.Crossing ? 3f : 2f;
                    p.BeginPath();
                    p.MoveTo(ToScreen(r.AX, r.AZ));
                    p.LineTo(ToScreen(r.BX, r.BZ));
                    p.Stroke();
                }

            if (On("Threats"))
                foreach (var e in v.Events)
                {
                    if (e.Type != WorldEventType.BanditActivity && e.Type != WorldEventType.RoadBlocked && e.Type != WorldEventType.FactionConflict) continue;
                    p.fillColor = new Color(0.75f, 0.16f, 0.10f, 0.35f);
                    p.BeginPath();
                    p.Arc(ToScreen(e.X, e.Z), 22f, 0f, 360f);
                    p.Fill();
                }

            if (On("Travel") && v.Journey != null && _session != null)
            {
                p.strokeColor = new Color(0.95f, 0.80f, 0.35f);
                p.lineWidth = 4f;
                var stops = v.Journey.Stops.Select(name => v.Settlements.FirstOrDefault(s => s.Name == name)).Where(s => s != null).ToList();
                if (stops.Count >= 2)
                {
                    p.BeginPath();
                    p.MoveTo(ToScreen(stops[0].X, stops[0].Z));
                    for (var i = 1; i < stops.Count; i++) p.LineTo(ToScreen(stops[i].X, stops[i].Z));
                    p.Stroke();
                }
            }
        }

        private void PlaceMarks()
        {
            if (_atlasMarks == null || _atlasView == null) return;
            _atlasMarks.Clear();
            var v = _atlasView;
            foreach (var r in v.Regions)
            {
                var at = ToScreen(r.AtlasX * AtlasQueries.RegionCells + 6, (r.AtlasZ + 1) * AtlasQueries.RegionCells - 6);
                Mark("region:" + r.RegionId, r.Name, at, "rk-atlas-region");
            }

            if (On("Settlements"))
                foreach (var s in v.Settlements)
                {
                    var b = Mark("settlement:" + s.SettlementId, s.Name, ToScreen(s.X, s.Z), "rk-atlas-settlement");
                    b.EnableInClassList("rk-atlas-settlement--short", On("Economy") && s.Shortage);
                    b.EnableInClassList("rk-atlas-settlement--here", s.IsHere);
                }

            foreach (var g in v.Groups)
            {
                var letter = g.Kind == TravelGroupKind.Caravan ? "C" : g.Kind == TravelGroupKind.Bandits ? "B" : g.Kind == TravelGroupKind.Refugees ? "R" : "P";
                if (g.Kind == TravelGroupKind.Bandits && !On("Threats")) continue;
                if (g.Kind != TravelGroupKind.Bandits && !On("Travel") && !On("Economy")) continue;
                var gm = Mark("group:" + g.GroupId, g.Seen ? letter : letter + "?", ToScreen(g.X, g.Z), "rk-atlas-group"); // Phase 17: "?" — reported, not seen
                gm.EnableInClassList("rk-atlas-group--reported", !g.Seen);
                gm.tooltip = g.Seen ? g.Name : $"{g.Name} — reported {g.ReportedAt}{(g.ReportedFrom != null ? " by " + g.ReportedFrom : string.Empty)}; where they are now, you cannot say.";
            }

            if (On("Events"))
                foreach (var e in v.Events)
                    Mark("event:" + e.EventId, e.Outdated ? "~" : e.Known == EventKnowledge.Rumored ? "?" : "!", ToScreen(e.X, e.Z) + new Vector2(10, -10), "rk-atlas-event");

            if (On("Travel"))
            {
                var you = Mark("player", v.Party.Count > 0 ? "You + " + v.Party.Count : "You", ToScreen(v.PlayerX, v.PlayerZ) + new Vector2(0, 14), "rk-atlas-player");
                you.tooltip = v.Party.Count > 0 ? "Your party: " + string.Join(", ", v.Party) : "You travel alone.";
            }
        }

        private Button Mark(string key, string text, Vector2 at, string cls)
        {
            var b = new Button(() => SelectOnAtlas(key)) { name = "rk-atlas-mark-" + key, text = text };
            b.AddToClassList("rk-atlas-mark");
            b.AddToClassList(cls);
            b.EnableInClassList("rk-atlas-mark--selected", key == _atlasSelected);
            b.style.left = at.x;
            b.style.top = at.y;
            b.pickingMode = PickingMode.Position;
            _atlasMarks.Add(b);
            return b;
        }

        // ----- The inspector ------------------------------------------------------------------------

        private Label Line(string text, string cls = "rk-atlas-line")
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            _atlasSide.Add(l);
            return l;
        }

        private Button Act(string name, string text, Action act)
        {
            var b = new Button(() => { act(); RefreshAtlas(); }) { name = name, text = text };
            b.AddToClassList("rk-button");
            b.AddToClassList("rk-atlas-act");
            _atlasSide.Add(b);
            return b;
        }

        private void FillSide()
        {
            _atlasSide.Clear();
            var v = _atlasView;
            if (v == null) return;
            if (_session != null && _session.HasWorld && v.Regions.Count == 1)
            {
                Line("This world is a single vale. The roads could lead further.", "rk-muted");
                Act("rk-atlas-expand", "Discover the lands beyond", () => ShowMessage(_session.DiscoverLandsBeyond()));
            }

            var key = _atlasSelected ?? "region:" + v.PlayerRegion;
            var colon = key.IndexOf(':');
            var kind = colon < 0 ? key : key.Substring(0, colon);
            var id = colon < 0 ? null : key.Substring(colon + 1);
            switch (kind)
            {
                case "region": Region(v.Regions.FirstOrDefault(r => r.RegionId.ToString() == id)); break;
                case "settlement": Settlement(v, v.Settlements.FirstOrDefault(s => s.SettlementId.ToString() == id)); break;
                case "route": Route(v.Routes.FirstOrDefault(r => r.RouteId == id)); break;
                case "group": Group(v.Groups.FirstOrDefault(g => g.GroupId == id)); break;
                case "event": Event(v.Events.FirstOrDefault(e => e.EventId == id)); break;
                case "player": Party(v); break;
            }
        }

        private void Region(AtlasRegionView r)
        {
            if (r == null) return;
            Line(r.Name, "rk-atlas-heading");
            if (r.Discovery == RegionDiscovery.Unknown) { Line("You have not heard of these lands.", "rk-muted"); return; }
            Line($"{r.Kind} · {r.Discovery.ToString().ToLowerInvariant()} · simulated as {r.Tier.ToString().ToLowerInvariant()}");
            if (r.Landmarks.Count > 0) Line("Landmarks: " + string.Join(", ", r.Landmarks));
            if (r.Settlements == 0) { Line("Known by name only.", "rk-muted"); return; }
            Line($"Economy: {r.Settlements} settlements, {r.Population} people, prosperity {r.Prosperity}, stability {r.Stability}, about {r.FoodDays} days of food, " +
                 $"{r.Shortages} short of food, {r.CaravansOnRoad} caravans out.", "rk-atlas-econ");
            Line($"Outlaws you know of on its roads: {r.KnownOutlaws}.");
            foreach (var f in r.Factions) Line($"Influence: {f.Name} — {f.SharePercent}% of the people ({f.Settlements} settlement{(f.Settlements == 1 ? string.Empty : "s")}).", "rk-atlas-faction");
        }

        private void Settlement(AtlasView v, AtlasSettlementView s)
        {
            if (s == null) return;
            Line(s.Name, "rk-atlas-heading");
            Line($"{s.Type} · {s.Faction ?? "independent"} · {s.Population} people · prosperity {s.Prosperity} · stability {s.Stability}{(s.Shortage ? " · short of food" : string.Empty)}");
            // What the player knows is happening here or on its roads (as they know it).
            static bool Over(AtlasEventView e) => e.KnownState != WorldEventState.Active && e.KnownState != WorldEventState.Escalating;
            foreach (var e in v.Events.Where(e => e.Touches.Contains(s.SettlementId)).Reverse().OrderBy(Over)) // open first, newest first
            {
                var key = "event:" + e.EventId;
                var state = Over(e) ? ", " + e.KnownState.ToString().ToLowerInvariant() : string.Empty;
                Act("rk-atlas-news-" + e.EventId, $"{(e.Known == EventKnowledge.Rumored ? "Rumour" : "News")}: {e.Telling} (as of {e.KnownAt}{state}{(e.Outdated ? ", out of date" : string.Empty)})", () => _atlasSelected = key)
                    .EnableInClassList("rk-atlas-act--over", Over(e));
            }

            foreach (var r in v.Routes.Where(r => r.A == s.SettlementId || r.B == s.SettlementId))
            {
                var other = v.Settlements.FirstOrDefault(x => x.SettlementId == (r.A == s.SettlementId ? r.B : r.A))?.Name;
                Act("rk-atlas-road-" + r.RouteId, $"Road to {other}: {r.Kind}, {r.Km:0} km{(r.Passable ? string.Empty : " (closed)")}", () => _atlasSelected = "route:" + r.RouteId);
            }

            if (s.IsHere) { Line("You are here.", "rk-muted"); return; }
            var options = _session.GetTravelOptions(s.SettlementId);
            if (options.Count == 0) { Line("No open way leads there from where you are.", "rk-atlas-warn"); return; }
            Line("Ways there (from the route data):", "rk-atlas-subheading");
            foreach (var o in options)
            {
                Line($"{(o.Index == 0 ? "Best" : "Alternative " + o.Index)}: {o.Km:0} km, {o.Duration} on foot; arrive {o.Arrival}{(o.Via.Count > 0 ? "; via " + string.Join(", ", o.Via) : string.Empty)}.", "rk-atlas-option");
                foreach (var n in o.Notes) Line("· " + n, "rk-muted");
                var index = o.Index;
                Act("rk-atlas-go-" + index, index == 0 ? "Travel by the best way" : "Travel by alternative " + index,
                    () => ShowMessage(_session.Dispatch(new StartJourneyCommand(s.SettlementId, index))));
            }
        }

        private void Route(AtlasRouteView r)
        {
            if (r == null) return;
            string Of(Domain.Identifiers.SettlementId id) => _atlasView?.Settlements.Find(x => x.SettlementId == id)?.Name ?? "?";
            Line($"{(r.Crossing ? "Border crossing" : "Road")} between {Of(r.A)} and {Of(r.B)} ({r.Kind.ToString().ToLowerInvariant()})", "rk-atlas-heading");
            Line($"{r.Km:0} km, {(double.IsInfinity(r.Hours) ? "no way through on foot" : "about " + TravelRules.Duration(r.Hours) + " on foot")} · {r.Condition.ToString().ToLowerInvariant()}{(r.HasBridge ? " · bridge " + r.Bridge.ToString().ToLowerInvariant() : string.Empty)}{(r.Passable ? string.Empty : " · CLOSED")}");
        }

        private void Group(AtlasGroupView g)
        {
            if (g == null) return;
            Line(g.Name, "rk-atlas-heading");
            if (!g.Seen) // Phase 17: a report, not a sighting
            {
                Line($"{g.Kind}{(g.Faction != null ? " · " + g.Faction : string.Empty)} · reported {g.ReportedAt}{(g.ReportedFrom != null ? " by " + g.ReportedFrom : string.Empty)}");
                Line("Marked where the report placed them. Where they are now, and how many, you cannot say from here.", "rk-atlas-warn");
                return;
            }

            Line($"{g.Kind} · {g.State.ToString().ToLowerInvariant()} · {g.Members} people{(g.Strength > 0 ? ", strength " + g.Strength : string.Empty)}{(g.Faction != null ? " · " + g.Faction : string.Empty)} · seen now");
            if (g.Leader != null) Line("Led by " + g.Leader);
            if (g.From != null) Line($"From {g.From} to {g.To}");
            if (g.Cargo != null) Line("Carrying " + g.Cargo);
            foreach (var h in g.History) Line("· " + h, "rk-muted");
        }

        private void Event(AtlasEventView e)
        {
            if (e == null) return;
            Line(e.Telling, "rk-atlas-heading");
            Line($"{(e.Known == EventKnowledge.Confirmed ? "Seen for yourself" : "Rumoured")} · as of {e.KnownAt}{(string.IsNullOrEmpty(e.KnownFrom) ? string.Empty : " · from " + e.KnownFrom)}");
            Line($"As you know it: {e.KnownState.ToString().ToLowerInvariant()}, severity {e.KnownSeverity} of 5.");
            if (e.Outdated) Line("Your news is old: what you see here is not what you heard.", "rk-atlas-warn");
            foreach (var h in e.History) Line("· " + h, "rk-muted");
        }

        private void Party(AtlasView v)
        {
            Line("Your party", "rk-atlas-heading");
            Line(v.Party.Count == 0 ? "You travel alone." : "With you: " + string.Join(", ", v.Party));
        }

        // ----- Travel -------------------------------------------------------------------------------

        private void FillTravel()
        {
            _atlasTravel.Clear();
            var j = _atlasView?.Journey;
            var fight = _session?.GetEncounter();
            if (j == null && fight == null) { _atlasTravel.Add(new Label("Not travelling. Choose a settlement to see the ways there.") { name = "rk-atlas-travel-status" }); return; }
            var status = new Label(j == null ? "A fight on the road." :
                $"To {j.Destination}: {j.PercentDone}% done, {j.KmLeft:0} km and about {TravelRules.Duration(j.HoursLeft)} left (next: {j.NextStop}). {j.Status}{(string.IsNullOrEmpty(j.Reason) ? string.Empty : ": " + j.Reason)}.") { name = "rk-atlas-travel-status" };
            status.AddToClassList("rk-atlas-travel-status");
            _atlasTravel.Add(status);
            void Button(string name, string text, Func<CommandResult> act)
            {
                var b = new Button(() => { ShowMessage(act()); RefreshAtlas(); }) { name = name, text = text };
                b.AddToClassList("rk-button");
                _atlasTravel.Add(b);
            }

            if (fight != null && fight.Status == Domain.Combat.EncounterStatus.Active)
            {
                // Phase 17 (ADR-0086): the fight is fought on the ground (the view changes to it by itself). Letting the AI play the
                // player's turns stays only as an explicit, labelled choice; it is never what happens by default.
                Button("rk-atlas-fight-out", "Let it play out (the AI plays your turns)", () => _session.FightItOut());
                Button("rk-atlas-withdraw", "Withdraw", () => _session.Dispatch(new EndEncounterCommand(fight.Id, true)));
                return;
            }

            if (j == null) return;
            if (j.ThreatPending)
            {
                Button("rk-atlas-threat-fight", "Fight", () => _session.Dispatch(new ResolveRoadThreatCommand(RoadChoice.Fight)));
                Button("rk-atlas-threat-slip", "Slip past (Stealth)", () => _session.Dispatch(new ResolveRoadThreatCommand(RoadChoice.SlipPast)));
                Button("rk-atlas-threat-pay", $"Pay {j.Toll} coin", () => _session.Dispatch(new ResolveRoadThreatCommand(RoadChoice.PayToll)));
                return;
            }

            if (j.Status == JourneyStatus.Travelling)
            {
                Button("rk-atlas-travel-hour", "Travel 1 h", () => _session.Travel(1, true));
                Button("rk-atlas-travel-on", "Travel on (until night or trouble)", () => _session.Travel(24, false));
                Button("rk-atlas-halt", "Halt", () => _session.Dispatch(new HaltJourneyCommand()));
            }
            else if (j.Status == JourneyStatus.Halted || j.Status == JourneyStatus.Interrupted)
            {
                Button("rk-atlas-resume", "Resume", () => _session.Dispatch(new ResumeJourneyCommand()));
                Button("rk-atlas-night", "Press on through the night", () => { var r = _session.Dispatch(new ResumeJourneyCommand()); return r.IsSuccess ? _session.Travel(24, true) : r; });
            }

            if (j.Status != JourneyStatus.Abandoned) Button("rk-atlas-abandon", "Give up the journey", () => _session.Dispatch(new AbandonJourneyCommand()));
        }
    }
}
