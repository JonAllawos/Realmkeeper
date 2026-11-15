using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 26.1 (ADR-0118): feeds the local HUD map from what exploration already holds. Nothing here is a new record:
    /// <list type="bullet">
    /// <item><b>In a settlement:</b> the persisted plan's roads, water, fields and buildings as ground; shops (the shop views,
    /// at their doors); services and the plan's landmarks (buildings anyone can see); the gates; residents who are out and
    /// near (named once met, otherwise by trade); companions.</item>
    /// <item><b>In the country:</b> the region's terrain cells and roads as ground; known sites; found landmarks; the
    /// waypoint; travelling groups on the road near you (outlaws only within sight); companions.</item>
    /// <item><b>In a place</b> (an interior, a mine) the map is not shown.</item>
    /// </list>
    /// Static marks are made once per area; moving ones are refreshed every 0.4 s and followed by their transforms each frame.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private static readonly float[] TownRanges = { 45f, 90f };
        private static readonly float[] CountryRanges = { 160f, 320f };
        private const float SightMetres = 70f; // outlaws are shown only this near (they must be seen to be marked)

        private GameObject _localMapArea; // the environment the map was built for: a new one (town, country, reload) rebuilds it
        private float _localMapRefresh;
        private readonly Dictionary<NpcId, (string label, bool merchant, string key)> _mapPeople = new Dictionary<NpcId, (string, bool, string)>();
        private float _mapPeopleAge;
        private List<WorldSession.LandmarkView> _mapLandmarks;
        private readonly System.Diagnostics.Stopwatch _mapWatch = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch _mapRefreshWatch = new System.Diagnostics.Stopwatch();

        /// <summary>Phase 26.1: the compact local map (bottom right of the exploration HUD).</summary>
        public LocalHudMap LocalMap { get; private set; }

        /// <summary>Time spent in the map's last frame update and last refresh (milliseconds), for the performance probe.</summary>
        public double LocalMapTickMs { get; private set; }

        public double LocalMapRefreshMs { get; private set; }

        private void UpdateLocalMap()
        {
            if (Hud?.Root == null || Player == null) return;
            if (LocalMap == null) LocalMap = new LocalHudMap(Hud.Root);
            else if (LocalMap.Root.parent != Hud.Root) Hud.Root.Add(LocalMap.Root); // the HUD's tree was rebuilt: the map moves to the live one
            var shown = !InPlace && EnvironmentRoot != null && (InOverland || _streamer.Current != null);
            LocalMap.Visible = shown;
            if (!shown) { _localMapArea = null; return; }
            if (Hud.WindowOpen) return; // hidden behind windows by the style sheet; no work while hidden

            _mapWatch.Restart();
            if (!ReferenceEquals(EnvironmentRoot, _localMapArea))
            {
                _localMapArea = EnvironmentRoot;
                _mapPeople.Clear();
                _mapLandmarks = null;
                _mapPeopleAge = 0f;
                BuildLocalMapArea();
                _localMapRefresh = 0f;
            }

            _localMapRefresh -= Time.unscaledDeltaTime;
            _mapPeopleAge += Time.unscaledDeltaTime;
            if (_localMapRefresh <= 0f)
            {
                _localMapRefresh = 0.4f;
                _mapRefreshWatch.Restart();
                RefreshLocalMapMovers();
                LocalMapRefreshMs = _mapRefreshWatch.Elapsed.TotalMilliseconds;
            }

            LocalMap.ReducedMotion = Realmkeeper.UI.RkSettings.ReducedMotion;
            LocalMap.Tick(Player.transform.position, Player.Heading, Time.unscaledDeltaTime);
            LocalMapTickMs = _mapWatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>The area's ground and its still marks (made once on arrival in a settlement or a region).</summary>
        private void BuildLocalMapArea()
        {
            LocalMap.ClearMarks();
            if (InOverland) BuildCountryMap();
            else BuildTownMap();
        }

        private Vector3 LayoutToWorld(float x, float z)
        {
            var root = EnvironmentRoot != null ? EnvironmentRoot.transform : null;
            return root != null ? root.TransformPoint(new Vector3(x, 0f, z)) : new Vector3(x, 0f, z);
        }

        private void BuildTownMap()
        {
            var layout = _streamer.Current;
            if (layout == null) return;
            LocalMap.SetRanges(TownRanges);
            var g = new LocalHudMap.Ground { Background = new Color(0.17f, 0.19f, 0.13f, 1f) };
            Vector2 W(float x, float z) { var v = LayoutToWorld(x, z); return new Vector2(v.x, v.z); }
            var plan = layout.Plan;
            if (plan != null)
            {
                foreach (var f in plan.Fields)
                    g.Rects.Add((W(f.Center.X, f.Center.Z), f.Width, f.Depth, f.Yaw, f.Kind == FieldKind.Garden || f.Kind == FieldKind.Yard ? new Color(0.27f, 0.28f, 0.17f) : new Color(0.25f, 0.27f, 0.16f)));
                foreach (var w in plan.Waterways) g.Lines.Add((Points(w.Line, W), Mathf.Max(2f, w.Width), new Color(0.27f, 0.42f, 0.5f)));
                g.Discs.Add((W(0f, 0f), layout.PlazaRadius + 1.2f, new Color(0.42f, 0.4f, 0.35f)));
                foreach (var road in plan.Roads)
                {
                    var colour = road.Surface == RoadSurface.Dirt ? new Color(0.45f, 0.38f, 0.27f) : road.Surface == RoadSurface.Gravel ? new Color(0.5f, 0.46f, 0.38f) : new Color(0.58f, 0.54f, 0.46f);
                    g.Lines.Add((Points(road.Line, W), Mathf.Max(1.5f, road.Width), colour));
                }

                if (plan.HasWalls && plan.Wall.Count > 2)
                {
                    var wall = Points(plan.Wall, W);
                    var closed = new Vector2[wall.Length + 1];
                    wall.CopyTo(closed, 0);
                    closed[wall.Length] = wall[0];
                    g.Lines.Add((closed, 1.6f, new Color(0.55f, 0.52f, 0.47f)));
                }
            }
            else
            {
                g.Discs.Add((W(0f, 0f), layout.PlazaRadius, new Color(0.42f, 0.4f, 0.35f)));
                foreach (var s in layout.Streets) g.Lines.Add((new[] { W(s.From.X, s.From.Z), W(s.To.X, s.To.Z) }, s.Width, new Color(0.5f, 0.46f, 0.38f)));
            }

            foreach (var b in layout.Buildings)
                g.Rects.Add((W(b.Center.X, b.Center.Z), b.Width, b.Depth, b.Yaw, new Color(0.33f, 0.28f, 0.23f)));
            LocalMap.SetGround(g);

            // Shops at their doors, by the shop's own name.
            var shopBuildings = new HashSet<string>();
            foreach (var pair in _shops)
            {
                var view = pair.Value;
                if (view == null) continue;
                var b = view.Building;
                shopBuildings.Add(b.Key);
                var door = LocalPaths.ToWorld(b, LocalPaths.DoorwayX(b), -b.Depth / 2f - 0.8f);
                var at = LayoutToWorld(door.X, door.Z);
                LocalMap.SetMark("shop:" + b.Key, LocalMarkKind.Shop, view.ShopName, at.x, at.z);
            }

            if (plan == null) return;

            // The plan's landmarks (the keep, the chapel on the rise, the watchtower): buildings anyone can see.
            var landmarkBuildings = new HashSet<string>();
            foreach (var l in plan.Landmarks)
            {
                var at = LayoutToWorld(l.Position.X, l.Position.Z);
                if (!string.IsNullOrEmpty(l.FeatureKey))
                {
                    landmarkBuildings.Add(l.FeatureKey);
                    var fb = plan.FindBuilding(l.FeatureKey);
                    if (fb != null) at = LayoutToWorld(fb.Center.X, fb.Center.Z);
                }

                LocalMap.SetMark("landmark:" + l.Key, LocalMarkKind.Building, l.Name, at.x, at.z);
            }

            // Services by what the building is (words only, nothing invented): the tavern, the market hall, the hall.
            foreach (var b in layout.Buildings)
            {
                if (b.Function == null || shopBuildings.Contains(b.Key) || landmarkBuildings.Contains(b.Key)) continue;
                var word = ServiceWord(b.Function.Value);
                if (word == null) continue;
                var door = LocalPaths.ToWorld(b, LocalPaths.DoorwayX(b), -b.Depth / 2f - 0.8f);
                var at = LayoutToWorld(door.X, door.Z);
                LocalMap.SetMark("service:" + b.Key, LocalMarkKind.Service, word, at.x, at.z);
            }

            for (var i = 0; i < plan.Gates.Count; i++)
            {
                var at = LayoutToWorld(plan.Gates[i].X, plan.Gates[i].Z);
                LocalMap.SetMark("gate:" + i, LocalMarkKind.Gate, "Gate", at.x, at.z, null, true);
            }
        }

        public static string ServiceWord(BuildingFunction f)
        {
            switch (f)
            {
                case BuildingFunction.Tavern: return "Tavern";
                case BuildingFunction.MarketHall: return "Market hall";
                case BuildingFunction.Civic: return "Hall";
                case BuildingFunction.Religious: return "Shrine";
                case BuildingFunction.Smithy: return "Smithy";
                case BuildingFunction.Mill: return "Mill";
                case BuildingFunction.GuardPost: return "Guard post";
                case BuildingFunction.Stable: return "Stable";
                default: return null;
            }
        }

        /// <summary>Corner cutting: each segment keeps its middle half, so a path through cell centres reads as a road, not a staircase.</summary>
        private static Vector2[] Smooth(Vector2[] p)
        {
            if (p.Length < 3) return p;
            var r = new Vector2[(p.Length - 1) * 2];
            for (var i = 0; i + 1 < p.Length; i++)
            {
                r[2 * i] = Vector2.Lerp(p[i], p[i + 1], i == 0 ? 0f : 0.25f);
                r[2 * i + 1] = Vector2.Lerp(p[i], p[i + 1], i + 2 == p.Length ? 1f : 0.75f);
            }

            return r;
        }

        private static Vector2[] Points(List<PlanPoint> line, System.Func<float, float, Vector2> w)
        {
            var a = new Vector2[line.Count];
            for (var i = 0; i < a.Length; i++) a[i] = w(line[i].X, line[i].Z);
            return a;
        }

        private void BuildCountryMap()
        {
            var view = _overlandView;
            if (view?.Terrain == null) return;
            LocalMap.SetRanges(CountryRanges);
            var t = view.Terrain;
            var map = t.Map;
            var g = new LocalHudMap.Ground { Background = new Color(0.17f, 0.2f, 0.13f, 1f) };
            const float cell = OverlandScale.MetresPerCell;
            for (var z = 0; z < map.Depth; z++)
                for (var x = 0; x < map.Width; x++)
                {
                    var c = t.Cell(x, z);
                    Color col;
                    if ((!c.Passable && c.Biome != Biome.Mountain) || c.Biome == Biome.Shallows || c.Biome == Biome.River) col = new Color(0.25f, 0.38f, 0.46f);
                    else if (c.Biome == Biome.Mountain) col = new Color(0.4f, 0.38f, 0.36f);
                    else
                    {
                        var b = BiomeMapColour(c.Biome);
                        col = Color.Lerp(new Color(0.2f, 0.24f, 0.15f), new Color(b.r * 0.6f, b.g * 0.6f, b.b * 0.6f), 0.5f); // a soft patchwork, not a checkerboard
                    }

                    g.Cells.Add((new Vector2(x * cell, z * cell), cell, col));
                }

            foreach (var road in view.Roads)
            {
                var pts = new Vector2[road.Cells.Count];
                for (var i = 0; i < pts.Length; i++)
                {
                    var (cx, cz) = OverlandScale.CellCentre(road.Cells[i]);
                    pts[i] = new Vector2(cx, cz);
                }

                g.Lines.Add((Smooth(Smooth(pts)), 7f, new Color(0.62f, 0.52f, 0.36f))); // the road through its cells, its corners rounded (Chaikin)
            }

            LocalMap.SetGround(g);
            foreach (var s in view.Sites)
            {
                if (!s.Known) continue;
                var kind = s.Kind == OverlandSiteKind.Settlement ? LocalMarkKind.Settlement : s.Kind == OverlandSiteKind.RegionExit ? LocalMarkKind.Gate : LocalMarkKind.Place;
                LocalMap.SetMark("site:" + s.Id, kind, s.Name, s.X, s.Z, null, s.Kind == OverlandSiteKind.Settlement);
            }

            foreach (var l in _landmarkStates)
                if (l.Found) LocalMap.SetMark("landmark:" + l.Landmark.Id, LocalMarkKind.Landmark, l.Landmark.Name, l.Landmark.X, l.Landmark.Z);
        }

        /// <summary>Everything that moves or changes: people, companions, travellers, the waypoint, newly found landmarks.</summary>
        private void RefreshLocalMapMovers()
        {
            LocalMap.BeginMovers();
            var p = Player.transform.position;
            var reach = LocalMap.Ranges[LocalMap.Ranges.Length - 1] * 1.5f;
            var reach2 = reach * reach;
            var reread = _mapPeopleAge > 2f;
            if (reread) { _mapPeople.Clear(); _mapPeopleAge = 0f; } // acquaintance changes after a conversation; landmarks are found

            foreach (var c in _companionList)
                if (c != null && c.gameObject.activeInHierarchy)
                    LocalMap.SetMark("companion:" + c.NpcId, LocalMarkKind.Companion, c.DisplayName, c.transform.position.x, c.transform.position.z, c.transform, false, true);

            if (!InOverland)
            {
                foreach (var pair in _residents)
                {
                    var r = pair.Value;
                    if (r == null || r.IsHidden || !r.gameObject.activeInHierarchy) continue;
                    var at = r.transform.position;
                    if ((at.x - p.x) * (at.x - p.x) + (at.z - p.z) * (at.z - p.z) > reach2) continue;
                    if (!_mapPeople.TryGetValue(pair.Key, out var who))
                    {
                        var merchant = false;
                        var label = _session != null ? _session.MapLabelFor(pair.Key, out _, out merchant) : r.DisplayName;
                        who = (label, merchant, "person:" + pair.Key);
                        _mapPeople[pair.Key] = who;
                    }

                    LocalMap.SetMark(who.key, who.merchant ? LocalMarkKind.Merchant : LocalMarkKind.Person, who.label, at.x, at.z, r.transform, false, true);
                }

                LocalMap.EndMovers();
                return;
            }

            foreach (var tv in _travellerViews.Values)
            {
                if (tv == null || !tv.gameObject.activeInHierarchy) continue;
                var at = tv.transform.position;
                var d2 = (at.x - p.x) * (at.x - p.x) + (at.z - p.z) * (at.z - p.z);
                var hostile = tv.Traveller.Hostile;
                if (d2 > (hostile ? SightMetres * SightMetres : reach2)) continue; // outlaws only once within sight
                LocalMap.SetMark("traveller:" + tv.Traveller.GroupId, hostile ? LocalMarkKind.Hostile : LocalMarkKind.Traveller, hostile ? "Outlaws" : tv.Traveller.Name, at.x, at.z, tv.transform, false, true);
            }

            // Landmarks found since arriving join the still marks.
            if ((reread || _mapLandmarks == null) && _session != null) _mapLandmarks = _session.GetLandmarks(); // the session's own list, however a landmark was found
            foreach (var l in _mapLandmarks ?? _landmarkStates)
            {
                if (!l.Found) continue;
                var key = "landmark:" + l.Landmark.Id;
                if (!LocalMap.HasMark(key)) LocalMap.SetMark(key, LocalMarkKind.Landmark, l.Landmark.Name, l.Landmark.X, l.Landmark.Z);
            }

            if (_waypoint != null && _waypoint.InThisRegion)
                LocalMap.SetMark("waypoint", LocalMarkKind.Waypoint, _waypoint.Name, _waypoint.X, _waypoint.Z, null, true, true);
            LocalMap.EndMovers();
        }
    }
}
