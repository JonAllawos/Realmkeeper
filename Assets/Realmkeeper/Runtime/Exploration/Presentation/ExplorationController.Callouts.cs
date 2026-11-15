using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 27: shopkeepers call out to the player passing by. The lines are data (<see cref="ShopCallouts"/>, by shop kind).
    /// The rules keep it natural:
    /// <list type="bullet">
    /// <item>only the shop's own keeper, out in the street or at the counter, and the shop open;</item>
    /// <item>only when the player is 3–12 m away;</item>
    /// <item>never during a window, a fight or a conversation;</item>
    /// <item>one call at a time anywhere (14 s apart), and each shop at most once a minute, its lines in turn.</item>
    /// </list>
    /// Calling never opens the shop: trading is still by walking in and interacting.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public const float CalloutNear = 3f;
        public const float CalloutFar = 12f;
        public const float CalloutGap = 14f;
        public const float CalloutShopGap = 60f;
        private const float CalloutShown = 4.5f;

        private readonly Dictionary<ShopId, (NpcId keeper, ShopKind kind)> _shopKeepers = new Dictionary<ShopId, (NpcId, ShopKind)>();
        private readonly Dictionary<ShopId, (float at, int count)> _shopCalls = new Dictionary<ShopId, (float, int)>();
        private float _nextCalloutCheck;
        private float _lastCallout = -999f;
        private TextMesh _calloutText;
        private float _calloutUntil;
        private Transform _calloutBy;

        /// <summary>The last call (for tests and the log): who, which shop, the line and when.</summary>
        public (NpcId keeper, ShopId shop, string line, float at) LastCallout { get; private set; }

        public int CalloutCount { get; private set; }

        /// <summary>Why the last check made no call (for tests and diagnosis).</summary>
        public string CalloutWhyNot { get; private set; }

        private void UpdateCallouts()
        {
            if (_calloutText != null)
            {
                var show = Time.unscaledTime < _calloutUntil && _calloutBy != null;
                if (_calloutText.gameObject.activeSelf != show) _calloutText.gameObject.SetActive(show);
                if (show && CameraRig != null)
                {
                    _calloutText.transform.position = _calloutBy.position + Vector3.up * 2.45f;
                    _calloutText.transform.rotation = Quaternion.LookRotation(_calloutText.transform.position - CameraRig.transform.position);
                }
            }

            if (Time.unscaledTime < _nextCalloutCheck) return;
            _nextCalloutCheck = Time.unscaledTime + 1f;
            if (_session == null || Player == null || InOverland || InPlace || WindowOpen || InFight || TalkingTo != null || PauseOpen) { CalloutWhyNot = "busy: window " + WindowOpen + ", fight " + InFight + ", talking " + (TalkingTo != null) + ", place " + InPlace; return; }
            if (Time.unscaledTime - _lastCallout < CalloutGap) { CalloutWhyNot = "gap"; return; }

            var p = Player.transform.position;
            ResidentView best = null;
            ShopInteriorView bestShop = null;
            var bestD = float.MaxValue;
            foreach (var view in _shops.Values)
            {
                if (view == null || !view.IsOpen) continue;
                if (!_shopKeepers.TryGetValue(view.ShopId, out var k))
                {
                    var d0 = _session.GetShopDetail(view.ShopId)?.Summary;
                    k = (d0?.KeeperId ?? default, d0?.Kind ?? ShopKind.Unspecified);
                    _shopKeepers[view.ShopId] = k;
                }

                if (k.keeper.IsEmpty || !_residents.TryGetValue(k.keeper, out var keeper) || keeper == null || keeper.IsHidden) continue;
                if (_shopCalls.TryGetValue(view.ShopId, out var last) && Time.unscaledTime - last.at < CalloutShopGap) continue;
                var d = Vector3.Distance(keeper.transform.position, p);
                if (d < CalloutNear || d > CalloutFar || d >= bestD) continue;
                best = keeper;
                bestShop = view;
                bestD = d;
            }

            if (best == null) { CalloutWhyNot = "no keeper in reach (" + _shops.Count + " shops)"; return; }
            _shopCalls.TryGetValue(bestShop.ShopId, out var calls);
            var line = ShopCallouts.Pick(_shopKeepers[bestShop.ShopId].kind, calls.count);
            _shopCalls[bestShop.ShopId] = (Time.unscaledTime, calls.count + 1);
            _lastCallout = Time.unscaledTime;
            if (_calloutText == null)
            {
                var go = new GameObject("Shop callout");
                go.transform.SetParent(transform, false);
                _calloutText = WorldText(go, string.Empty, new Color(1f, 0.9f, 0.7f), 0.045f);
            }

            _calloutText.text = line;
            _calloutBy = best.transform;
            _calloutUntil = Time.unscaledTime + CalloutShown;
            best.Animation?.Talk(2.5f);
            best.TurnToFace(p);
            LastCallout = (best.NpcId, bestShop.ShopId, line, Time.unscaledTime);
            CalloutCount++;
        }
    }
}
