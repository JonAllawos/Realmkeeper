using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 26.1 (ADR-0118): the compact local map in the exploration HUD's bottom-right corner. Heading-up: the player's
    /// mark stays at the centre pointing up, and the ground and every mark turn beneath it with the player's heading.
    /// It draws only what the controller hands it (ground shapes from the settlement plan or the region's terrain, and
    /// marks for things the game already holds); it keeps no records of its own.
    /// <list type="bullet">
    /// <item>The ground is drawn once per area into a canvas that is moved and turned by its style (no redraw as you walk
    /// or turn); it is redrawn only when the player has walked a good way from where it was drawn, or the area changes.</item>
    /// <item>The marks are drawn in one layer (shapes by kind, never glyphs), redrawn each frame from cached positions.</item>
    /// <item>Labels are a small pool of reused elements; which marks get one is decided a few times a second.</item>
    /// </list>
    /// </summary>
    public sealed class LocalHudMap
    {
        public const int MaxLabels = 5;
        private float LabelFont = 12f; // the labels' resolved size (the Text size setting reaches it)
        private float LabelHeight => Mathf.Ceil(LabelFont * 1.34f);

        // Cartographer's Ledger, kept to the tokens' hues (the region map's key uses the same).
        public static readonly Color Ink = new Color(0.086f, 0.078f, 0.067f, 0.86f);
        public static readonly Color Bronze = new Color(0.72f, 0.54f, 0.29f);
        public static readonly Color Parchment = new Color(0.92f, 0.85f, 0.68f);

        public static Color KindColour(LocalMarkKind k)
        {
            switch (k)
            {
                case LocalMarkKind.Companion: return new Color(0.56f, 0.86f, 0.5f);
                case LocalMarkKind.Person: return new Color(0.45f, 0.76f, 0.72f);
                case LocalMarkKind.Traveller: return new Color(0.45f, 0.76f, 0.72f);
                case LocalMarkKind.Merchant:
                case LocalMarkKind.Shop: return new Color(0.93f, 0.71f, 0.32f);
                case LocalMarkKind.Service: return new Color(0.53f, 0.67f, 0.86f);
                case LocalMarkKind.Settlement: return new Color(0.53f, 0.67f, 0.86f);
                case LocalMarkKind.Building: return Parchment;
                case LocalMarkKind.Gate: return Parchment;
                case LocalMarkKind.Landmark: return new Color(0.74f, 0.62f, 0.93f);
                case LocalMarkKind.Place: return Parchment;
                case LocalMarkKind.Waypoint: return new Color(1f, 0.78f, 0.25f);
                case LocalMarkKind.Hostile: return new Color(0.86f, 0.33f, 0.28f);
                default: return Parchment;
            }
        }

        /// <summary>The ground: roads, water, buildings and the land's cells, in world metres.</summary>
        public sealed class Ground
        {
            public readonly List<(Vector2[] line, float width, Color colour)> Lines = new List<(Vector2[], float, Color)>();
            public readonly List<(Vector2 centre, float width, float depth, float yaw, Color colour)> Rects = new List<(Vector2, float, float, float, Color)>();
            public readonly List<(Vector2 centre, float radius, Color colour)> Discs = new List<(Vector2, float, Color)>();
            public readonly List<(Vector2 min, float size, Color colour)> Cells = new List<(Vector2, float, Color)>();
            public Color Background = new Color(0.16f, 0.17f, 0.12f, 1f);
        }

        /// <summary>A mark as the map holds it: what it shows and where it is now (updated by its source).</summary>
        public sealed class Mark
        {
            public LocalMapMark Data = new LocalMapMark();
            public Transform Follow; // a moving thing: its position is read each frame
            public float X, Z;

            // Where it was drawn this frame (pixels from the centre), and whether it was.
            public float ScreenX, ScreenY;
            public bool Drawn;
            public int LabelSide;
            public bool Mover;
            public int Stamp;
            public string Short;
        }

        private readonly Dictionary<string, Mark> _byKey = new Dictionary<string, Mark>();
        private int _stamp;
        private readonly List<(float l, float t, float r, float b)> _keepClear = new List<(float, float, float, float)>();
        private float _northX, _northY;
        private float _declutteredHeading = float.NaN;
        private Vector2 _declutteredAt;

        public VisualElement Root { get; }
        private readonly VisualElement _clip;
        private readonly VisualElement _canvas;
        private readonly VisualElement _marksLayer;
        private readonly VisualElement _player;
        private readonly Label _north;
        private readonly Label _range;
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Mark> _marks = new List<Mark>();
        private readonly List<(float x, float y, float labelWidth, int priority)> _declutterInput = new List<(float, float, float, int)>();
        private readonly List<int> _declutterIndex = new List<int>();
        private int[] _labelSides = new int[64];

        private Ground _ground;
        private Vector2 _anchor; // the world point at the canvas's centre
        private float _extent; // metres from the anchor to the canvas's edge
        private float _drawnScale;
        private float _shownHeading;
        private float _declutterTimer;
        private bool _groundDirty = true;
        private float _half = 90f;

        /// <summary>The map's range: metres from the player to the edge. Two steps per kind of country.</summary>
        public float Radius { get; private set; } = 60f;

        public float[] Ranges { get; private set; } = { 45f, 90f };
        public int RangeIndex { get; private set; }

        /// <summary>The heading shown (degrees), after smoothing.</summary>
        public float ShownHeading => _shownHeading;

        public float PixelsPerMetre => _half / Radius;
        public IReadOnlyList<Mark> Marks => _marks;
        public int LabelsShown { get; private set; }
        public bool ReducedMotion { get; set; }

        public LocalHudMap(VisualElement parent)
        {
            Root = new VisualElement { name = "xk-localmap" };
            Root.AddToClassList("xk-localmap");
            Root.tooltip = "Nearby: the map turns with you. Click to change its range.";
            _clip = new VisualElement { name = "xk-localmap-clip", pickingMode = PickingMode.Ignore };
            _clip.AddToClassList("xk-localmap-clip");
            Root.Add(_clip);
            _canvas = new VisualElement { name = "xk-localmap-ground", pickingMode = PickingMode.Ignore };
            _canvas.style.position = Position.Absolute;
            _canvas.generateVisualContent += DrawGround;
            _clip.Add(_canvas);
            _marksLayer = new VisualElement { name = "xk-localmap-marks", pickingMode = PickingMode.Ignore };
            _marksLayer.AddToClassList("xk-localmap-layer");
            _marksLayer.generateVisualContent += DrawMarks;
            _clip.Add(_marksLayer);
            for (var i = 0; i < MaxLabels; i++)
            {
                var l = new Label { name = "xk-localmap-label-" + i, pickingMode = PickingMode.Ignore };
                l.AddToClassList("xk-localmap-label");
                l.style.display = DisplayStyle.None;
                _clip.Add(l);
                _labels.Add(l);
            }

            _player = new VisualElement { name = "xk-localmap-player", pickingMode = PickingMode.Ignore };
            _player.AddToClassList("xk-localmap-layer");
            _player.generateVisualContent += DrawPlayer;
            _clip.Add(_player);
            _north = new Label("N") { name = "xk-localmap-north", pickingMode = PickingMode.Ignore };
            _north.AddToClassList("xk-localmap-north");
            _clip.Add(_north);
            _range = new Label { name = "xk-localmap-range", pickingMode = PickingMode.Ignore };
            _range.AddToClassList("xk-localmap-range");
            Root.Add(_range);
            Root.RegisterCallback<ClickEvent>(_ => CycleRange());
            Root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var w = _clip.resolvedStyle.width;
                if (w > 10f && Math.Abs(w / 2f - _half) > 0.5f)
                {
                    _half = w / 2f;
                    _groundDirty = true;
                    _player.MarkDirtyRepaint();
                }
            });
            parent.Add(Root);
            SetRanges(Ranges);
        }

        public bool Visible
        {
            get => !Root.ClassListContains("xk-hidden");
            set { if (Root.ClassListContains("xk-hidden") == value) Root.EnableInClassList("xk-hidden", !value); } // a class, never an inline style: the window rule in the style sheet must still win
        }

        /// <summary>The two ranges for this kind of country (metres); the first is shown unless the player chose the second.</summary>
        public void SetRanges(float[] ranges)
        {
            Ranges = ranges;
            RangeIndex = Math.Min(RangeIndex, ranges.Length - 1);
            Radius = ranges[RangeIndex];
            _range.text = Mathf.RoundToInt(Radius) + " m";
            _groundDirty = true;
        }

        public void CycleRange()
        {
            RangeIndex = (RangeIndex + 1) % Ranges.Length;
            Radius = Ranges[RangeIndex];
            _range.text = Mathf.RoundToInt(Radius) + " m";
            _groundDirty = true;
            _declutterTimer = 0f;
        }

        /// <summary>A new area: its ground (drawn on the next frame) and an empty set of marks.</summary>
        public void SetGround(Ground ground)
        {
            _ground = ground;
            _groundDirty = true;
            if (ground != null) _clip.style.backgroundColor = ground.Background;
        }

        public void ClearMarks()
        {
            _marks.Clear();
            _byKey.Clear();
        }

        /// <summary>
        /// Adds a mark, or updates the one with this key (marks are reused, never made afresh each refresh). A
        /// <paramref name="mover"/> is kept only while each refresh renews it (see <see cref="BeginMovers"/>).
        /// </summary>
        public Mark SetMark(string key, LocalMarkKind kind, string label, float x, float z, Transform follow = null, bool pin = false, bool mover = false)
        {
            if (!_byKey.TryGetValue(key, out var m))
            {
                m = new Mark();
                m.Data.Key = key;
                _byKey[key] = m;
                _marks.Add(m);
                _declutterTimer = 0f;
            }

            if (m.Data.Label != label || m.Data.Kind != kind) _declutterTimer = 0f;
            m.Follow = follow;
            m.X = x;
            m.Z = z;
            m.Data.Kind = kind;
            if (m.Data.Label != label) m.Short = LocalMapGeometry.Shorten(label, 18); // shortened once, not every frame
            m.Data.Label = label;
            m.Data.X = x;
            m.Data.Z = z;
            m.Data.PinAtEdge = pin;
            m.Mover = mover;
            m.Stamp = _stamp;
            return m;
        }

        public bool HasMark(string key) => _byKey.ContainsKey(key);

        /// <summary>A refresh of the moving marks begins: those not renewed before <see cref="EndMovers"/> leave the map.</summary>
        public void BeginMovers() => _stamp++;

        public void EndMovers()
        {
            for (var i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                if (!m.Mover || m.Stamp == _stamp) continue;
                _marks.RemoveAt(i);
                _byKey.Remove(m.Data.Key);
                _declutterTimer = 0f;
            }
        }

        /// <summary>
        /// One frame: follows the player (canvas moved and turned by its style), reads moving marks' positions, places
        /// the marks, and every quarter second chooses the labels again.
        /// </summary>
        /// <summary>Phase 28: where the map last drew the player (local metres): for checking it never shows a stale position.</summary>
        public Vector2 PlayerAt { get; private set; }

        public void Tick(Vector3 player, float heading, float deltaSeconds)
        {
            if (!Visible) return;
            PlayerAt = new Vector2(player.x, player.z);
            _shownHeading = LocalMapGeometry.SmoothHeading(_shownHeading, heading, deltaSeconds, ReducedMotion);
            var ppm = PixelsPerMetre;
            var p = new Vector2(player.x, player.z);
            // Redraw the ground only when the scale changed, or the player has walked far from where it was drawn.
            if (_groundDirty || Math.Abs(ppm - _drawnScale) > 1e-4f || (p - _anchor).magnitude > Radius * 0.6f)
            {
                _anchor = p;
                _extent = Radius * 2f;
                _drawnScale = ppm;
                var size = _extent * 2f * ppm;
                _canvas.style.width = size;
                _canvas.style.height = size;
                _canvas.MarkDirtyRepaint();
                _groundDirty = false;
            }

            // The canvas point under the player sits at the map's centre; the canvas turns about it.
            var cx = (_extent + (p.x - _anchor.x)) * ppm;
            var cy = (_extent - (p.y - _anchor.y)) * ppm;
            _canvas.style.left = _half - cx;
            _canvas.style.top = _half - cy;
            _canvas.style.transformOrigin = new TransformOrigin(cx, cy, 0f);
            _canvas.style.rotate = new Rotate(new Angle(-_shownHeading, AngleUnit.Degree));

            // North, on the rim, where north lies.
            LocalMapGeometry.Project(0f, 0f, _shownHeading, 0f, 1f, 1f, out var nx, out var ny);
            var rim = _half - 9f;
            _north.style.left = _half + nx * rim - 6f;
            _north.style.top = _half + ny * rim - 8f;
            _northX = nx * rim;
            _northY = ny * rim;

            foreach (var m in _marks)
            {
                if (m.Follow != null)
                {
                    var t = m.Follow.position;
                    m.X = t.x;
                    m.Z = t.z;
                }

                LocalMapGeometry.Project(player.x, player.z, _shownHeading, m.X, m.Z, ppm, out var x, out var y);
                m.Drawn = LocalMapGeometry.Inside(x, y, _half, 4f);
                if (!m.Drawn && m.Data.PinAtEdge)
                {
                    LocalMapGeometry.ClampToEdge(ref x, ref y, _half, 6f);
                    m.Drawn = true;
                }

                m.ScreenX = x;
                m.ScreenY = y;
            }

            _declutterTimer -= deltaSeconds;
            // Labels are chosen again as soon as the view has turned or moved noticeably, not only on the timer.
            if (Math.Abs(LocalMapGeometry.DeltaAngle(_declutteredHeading, _shownHeading)) > 6f || (p - _declutteredAt).sqrMagnitude > 2.25f) _declutterTimer = 0f;
            if (_declutterTimer <= 0f)
            {
                _declutterTimer = 0.25f;
                Declutter();
                _declutteredHeading = _shownHeading;
                _declutteredAt = p;
            }

            PlaceLabels();
            _marksLayer.MarkDirtyRepaint();
        }

        private void Declutter()
        {
            var font = _range.resolvedStyle.fontSize; // the same token as the labels, and always shown
            if (font > 6f && !float.IsNaN(font)) LabelFont = font;
            _declutterInput.Clear();
            _declutterIndex.Clear();
            for (var i = 0; i < _marks.Count; i++)
            {
                var m = _marks[i];
                m.LabelSide = 0;
                if (!m.Drawn || string.IsNullOrEmpty(m.Data.Label)) continue;
                var text = m.Short;
                _declutterInput.Add((m.ScreenX, m.ScreenY, LocalMapGeometry.LabelWidth(text, LabelFont), LocalMapGeometry.Priority(m.Data.Kind)));
                _declutterIndex.Add(i);
            }

            if (_labelSides.Length < _declutterInput.Count) _labelSides = new int[_declutterInput.Count * 2];
            // Keep the labels off the map's furniture: the range chip and the north letter.
            _keepClear.Clear();
            var cb = _clip.worldBound;
            var rb = _range.worldBound;
            if (cb.width > 0f && rb.width > 0f) _keepClear.Add((rb.xMin - cb.xMin - _half - 2f, rb.yMin - cb.yMin - _half - 2f, rb.xMax - cb.xMin - _half + 2f, rb.yMax - cb.yMin - _half + 2f));
            _keepClear.Add((_northX - 9f, _northY - 10f, _northX + 9f, _northY + 10f));
            LocalMapGeometry.Declutter(_declutterInput, _half, LabelHeight, MaxLabels, _labelSides, _keepClear);
            for (var k = 0; k < _declutterIndex.Count; k++) _marks[_declutterIndex[k]].LabelSide = _labelSides[k];
        }

        private void PlaceLabels()
        {
            var used = 0;
            foreach (var m in _marks)
            {
                if (m.LabelSide == 0 || !m.Drawn || used >= _labels.Count) continue;
                var l = _labels[used++];
                var text = m.Short;
                if (l.text != text) l.text = text;
                var w = LocalMapGeometry.LabelWidth(text, LabelFont);
                l.style.display = DisplayStyle.Flex;
                l.style.left = Mathf.Clamp(_half + (m.LabelSide > 0 ? m.ScreenX + 7f : m.ScreenX - 7f - w), 2f, 2f * _half - w - 2f); // never cut at the edge
                l.style.top = _half + m.ScreenY - LabelHeight / 2f;
                l.style.width = w;
                l.style.unityTextAlign = m.LabelSide > 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            }

            for (var i = used; i < _labels.Count; i++)
                if (_labels[i].style.display != DisplayStyle.None) _labels[i].style.display = DisplayStyle.None;
            LabelsShown = used;
        }

        // ----- Drawing ---------------------------------------------------------------------------------------------------

        private Vector2 CanvasPoint(Vector2 world) => new Vector2((_extent + (world.x - _anchor.x)) * _drawnScale, (_extent - (world.y - _anchor.y)) * _drawnScale);

        private void DrawGround(MeshGenerationContext mgc)
        {
            if (_ground == null || _drawnScale <= 0f) return;
            var p2 = mgc.painter2D;
            var minX = _anchor.x - _extent;
            var maxX = _anchor.x + _extent;
            var minZ = _anchor.y - _extent;
            var maxZ = _anchor.y + _extent;
            foreach (var (min, size, colour) in _ground.Cells)
            {
                if (min.x > maxX || min.x + size < minX || min.y > maxZ || min.y + size < minZ) continue;
                var a = CanvasPoint(new Vector2(min.x, min.y + size));
                var s = size * _drawnScale;
                p2.fillColor = colour;
                p2.BeginPath();
                p2.MoveTo(a);
                p2.LineTo(a + new Vector2(s + 0.5f, 0f));
                p2.LineTo(a + new Vector2(s + 0.5f, s + 0.5f));
                p2.LineTo(a + new Vector2(0f, s + 0.5f));
                p2.ClosePath();
                p2.Fill();
            }

            foreach (var (centre, radius, colour) in _ground.Discs)
            {
                if (centre.x - radius > maxX || centre.x + radius < minX || centre.y - radius > maxZ || centre.y + radius < minZ) continue;
                p2.fillColor = colour;
                p2.BeginPath();
                p2.Arc(CanvasPoint(centre), radius * _drawnScale, 0f, 360f);
                p2.Fill();
            }

            p2.lineJoin = LineJoin.Round;
            p2.lineCap = LineCap.Round;
            foreach (var (line, width, colour) in _ground.Lines)
            {
                if (line.Length < 2 || !Touches(line, minX, maxX, minZ, maxZ)) continue;
                p2.strokeColor = colour;
                p2.lineWidth = Mathf.Max(1.2f, width * _drawnScale);
                p2.BeginPath();
                p2.MoveTo(CanvasPoint(line[0]));
                for (var i = 1; i < line.Length; i++) p2.LineTo(CanvasPoint(line[i]));
                p2.Stroke();
            }

            foreach (var (centre, width, depth, yaw, colour) in _ground.Rects)
            {
                if (centre.x > maxX + 20f || centre.x < minX - 20f || centre.y > maxZ + 20f || centre.y < minZ - 20f) continue;
                var r = yaw * Mathf.Deg2Rad;
                var cos = Mathf.Cos(r);
                var sin = Mathf.Sin(r);
                Vector2 Corner(float lx, float lz) => CanvasPoint(new Vector2(centre.x + lx * cos - lz * sin, centre.y + lx * sin + lz * cos));
                p2.fillColor = colour;
                p2.BeginPath();
                p2.MoveTo(Corner(-width / 2f, -depth / 2f));
                p2.LineTo(Corner(width / 2f, -depth / 2f));
                p2.LineTo(Corner(width / 2f, depth / 2f));
                p2.LineTo(Corner(-width / 2f, depth / 2f));
                p2.ClosePath();
                p2.Fill();
            }
        }

        private static bool Touches(Vector2[] line, float minX, float maxX, float minZ, float maxZ)
        {
            float lx = float.MaxValue, hx = float.MinValue, lz = float.MaxValue, hz = float.MinValue;
            foreach (var v in line)
            {
                lx = Mathf.Min(lx, v.x);
                hx = Mathf.Max(hx, v.x);
                lz = Mathf.Min(lz, v.y);
                hz = Mathf.Max(hz, v.y);
            }

            return !(lx > maxX || hx < minX || lz > maxZ || hz < minZ);
        }

        private void DrawMarks(MeshGenerationContext mgc)
        {
            var p2 = mgc.painter2D;
            var outline = new Color(0.07f, 0.06f, 0.05f, 0.95f);
            // The less important first, so the important are drawn over them.
            for (var pass = 0; pass < 2; pass++)
                foreach (var m in _marks)
                {
                    if (!m.Drawn) continue;
                    var important = LocalMapGeometry.Priority(m.Data.Kind) >= 5;
                    if (important != (pass == 1)) continue;
                    var c = new Vector2(_half + m.ScreenX, _half + m.ScreenY);
                    DrawShape(p2, m.Data.Kind, c, KindColour(m.Data.Kind), outline);
                }
        }

        /// <summary>Each kind's shape (the colour only helps): a dot, a ringed dot, a square, a diamond, a triangle, a bar, a ring.</summary>
        public static void DrawShape(Painter2D p2, LocalMarkKind kind, Vector2 c, Color fill, Color outline)
        {
            p2.lineWidth = 1.5f;
            p2.strokeColor = outline;
            p2.fillColor = fill;
            p2.BeginPath();
            switch (kind)
            {
                case LocalMarkKind.Person:
                    p2.Arc(c, 3.6f, 0f, 360f);
                    break;
                case LocalMarkKind.Merchant:
                    p2.Arc(c, 4.2f, 0f, 360f);
                    break;
                case LocalMarkKind.Companion:
                    p2.Arc(c, 4.6f, 0f, 360f);
                    p2.Fill();
                    p2.strokeColor = new Color(0.95f, 0.93f, 0.85f);
                    p2.Stroke();
                    return;
                case LocalMarkKind.Traveller:
                case LocalMarkKind.Place:
                    p2.Arc(c, 4.4f, 0f, 360f);
                    p2.strokeColor = fill;
                    p2.lineWidth = 2f;
                    p2.Stroke();
                    return;
                case LocalMarkKind.Shop:
                case LocalMarkKind.Service:
                    p2.MoveTo(c + new Vector2(-4.5f, -4.5f));
                    p2.LineTo(c + new Vector2(4.5f, -4.5f));
                    p2.LineTo(c + new Vector2(4.5f, 4.5f));
                    p2.LineTo(c + new Vector2(-4.5f, 4.5f));
                    p2.ClosePath();
                    break;
                case LocalMarkKind.Gate:
                    p2.MoveTo(c + new Vector2(-6f, -2.2f));
                    p2.LineTo(c + new Vector2(6f, -2.2f));
                    p2.LineTo(c + new Vector2(6f, 2.2f));
                    p2.LineTo(c + new Vector2(-6f, 2.2f));
                    p2.ClosePath();
                    break;
                case LocalMarkKind.Settlement:
                    Diamond(p2, c, 7f);
                    break;
                case LocalMarkKind.Building:
                case LocalMarkKind.Landmark:
                    Diamond(p2, c, 5.5f);
                    break;
                case LocalMarkKind.Waypoint:
                    p2.MoveTo(c + new Vector2(-5.5f, -4.5f));
                    p2.LineTo(c + new Vector2(5.5f, -4.5f));
                    p2.LineTo(c + new Vector2(0f, 5.5f));
                    p2.ClosePath();
                    break;
                case LocalMarkKind.Hostile:
                    p2.MoveTo(c + new Vector2(0f, -6f));
                    p2.LineTo(c + new Vector2(5.5f, 4.5f));
                    p2.LineTo(c + new Vector2(-5.5f, 4.5f));
                    p2.ClosePath();
                    break;
                default:
                    p2.Arc(c, 3.5f, 0f, 360f);
                    break;
            }

            p2.Fill();
            p2.Stroke();
        }

        private static void Diamond(Painter2D p2, Vector2 c, float r)
        {
            p2.MoveTo(c + new Vector2(0f, -r));
            p2.LineTo(c + new Vector2(r, 0f));
            p2.LineTo(c + new Vector2(0f, r));
            p2.LineTo(c + new Vector2(-r, 0f));
            p2.ClosePath();
        }

        /// <summary>The player: a bronze arrowhead pointing up (the heading), with a soft cone ahead.</summary>
        private void DrawPlayer(MeshGenerationContext mgc)
        {
            var p2 = mgc.painter2D;
            var c = new Vector2(_half, _half);
            p2.fillColor = new Color(0.95f, 0.85f, 0.6f, 0.12f);
            p2.BeginPath();
            p2.MoveTo(c);
            p2.Arc(c, _half * 0.55f, -125f, -55f);
            p2.ClosePath();
            p2.Fill();
            p2.fillColor = new Color(0.98f, 0.9f, 0.68f);
            p2.strokeColor = new Color(0.07f, 0.06f, 0.05f, 0.95f);
            p2.lineWidth = 1.5f;
            p2.BeginPath();
            p2.MoveTo(c + new Vector2(0f, -8f));
            p2.LineTo(c + new Vector2(6f, 6f));
            p2.LineTo(c + new Vector2(0f, 3f));
            p2.LineTo(c + new Vector2(-6f, 6f));
            p2.ClosePath();
            p2.Fill();
            p2.Stroke();
        }

        /// <summary>The player layer is drawn about the centre; it must be redrawn when the map's size changes.</summary>
        public void Resized() => _player.MarkDirtyRepaint();
    }
}
