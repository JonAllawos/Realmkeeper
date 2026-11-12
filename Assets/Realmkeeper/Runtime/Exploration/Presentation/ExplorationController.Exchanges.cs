using System.Collections.Generic;
using Realmkeeper.Application;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 18 (ADR-0094): what people are saying to each other, overheard. Every two seconds the settlement's real
    /// exchanges (<see cref="NpcExchanges"/>) are asked for; a pair standing together near the player shows its line above
    /// their heads, facing the camera, and the two turn to each other. Presentation only: nothing here changes the world.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public const float OverhearDistance = 14f;
        private readonly Dictionary<string, TextMesh> _exchangeTexts = new Dictionary<string, TextMesh>();
        private float _nextExchange;

        /// <summary>The lines currently shown (for tests and captures).</summary>
        public IReadOnlyCollection<string> OverheardLines
        {
            get
            {
                var lines = new List<string>();
                foreach (var t in _exchangeTexts.Values) if (t != null) lines.Add(t.text);
                return lines;
            }
        }

        private void UpdateExchanges()
        {
            if (Time.unscaledTime < _nextExchange) return;
            _nextExchange = Time.unscaledTime + 2f;
            var keep = new HashSet<string>();
            var list = _session != null && _session.HasWorld && !InPlace ? _session.GetExchanges() : null;
            if (list != null && Player != null)
            {
                var cam = CameraRig != null ? CameraRig.transform : Player.transform;
                foreach (var x in list)
                {
                    if (!_residents.TryGetValue(x.A, out var a) || !_residents.TryGetValue(x.B, out var b) || a == null || b == null || a.IsHidden || b.IsHidden) continue;
                    var pa = a.transform.position;
                    var pb = b.transform.position;
                    if (Vector3.Distance(pa, pb) > 8f) continue;
                    var mid = (pa + pb) * 0.5f;
                    if (Vector3.Distance(mid, Player.transform.position) > OverhearDistance) continue;
                    var key = x.A + "|" + x.B;
                    keep.Add(key);
                    if (!_exchangeTexts.TryGetValue(key, out var t) || t == null)
                    {
                        var go = new GameObject("Exchange " + key);
                        go.transform.SetParent(transform, false);
                        t = WorldText(go, string.Empty, x.Quarrel ? new Color(1f, 0.72f, 0.6f) : new Color(1f, 0.96f, 0.85f), 0.045f);
                        _exchangeTexts[key] = t;
                    }

                    t.text = Wrap(x.Line, 46);
                    t.transform.position = mid + Vector3.up * 2.55f;
                    t.transform.rotation = Quaternion.LookRotation(t.transform.position - cam.position);
                    a.TurnToFace(pb);
                    b.TurnToFace(pa);
                }
            }

            var gone = new List<string>();
            foreach (var pair in _exchangeTexts) if (!keep.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var key in gone)
            {
                if (_exchangeTexts[key] != null) Destroy(_exchangeTexts[key].gameObject);
                _exchangeTexts.Remove(key);
            }
        }

        private static string Wrap(string s, int width)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= width) return s;
            var sb = new System.Text.StringBuilder();
            var line = 0;
            foreach (var word in s.Split(' '))
            {
                if (line > 0 && line + word.Length + 1 > width) { sb.Append('\n'); line = 0; }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(word);
                line += word.Length;
            }

            return sb.ToString();
        }
    }
}
