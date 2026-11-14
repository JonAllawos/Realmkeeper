using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: what every document root carries. Call <see cref="Attach"/> once per UIDocument root.
    /// <list type="bullet">
    /// <item>Accessibility classes from <see cref="RkSettings"/>: <c>rk-text-115</c> / <c>rk-text-130</c> (text size),
    /// <c>rk-contrast</c>, <c>rk-reduced-motion</c>. The token sheet turns these into new values for every screen.</item>
    /// <item>Width and height classes from the root's laid-out size in panel pixels (after the interface scale):
    /// <c>rk-w-compact</c> below <see cref="CompactWidth"/>, <c>rk-w-narrow</c> below <see cref="NarrowWidth"/>,
    /// <c>rk-w-wide</c> at <see cref="WideWidth"/> or more (ultrawide), <c>rk-h-short</c> below <see cref="ShortHeight"/>.
    /// Screens restyle with these instead of absolute positions.</item>
    /// </list>
    /// </summary>
    public static class RkRoot
    {
        public const float CompactWidth = 1400f;
        public const float NarrowWidth = 1150f;
        public const float WideWidth = 2300f;
        public const float ShortHeight = 760f;

        private static readonly List<VisualElement> Roots = new List<VisualElement>();
        private static bool _subscribed;

        public static void Attach(VisualElement root)
        {
            if (root == null || Roots.Contains(root)) return;
            Roots.Add(root);
            if (!_subscribed)
            {
                RkSettings.Changed += ApplyAll;
                _subscribed = true;
            }

            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySize(root));
            root.RegisterCallback<DetachFromPanelEvent>(_ => Roots.Remove(root));
            Apply(root);
            ApplySize(root);
        }

        /// <summary>Re-applies the settings classes to every attached root (after a settings change).</summary>
        public static void ApplyAll()
        {
            for (var i = Roots.Count - 1; i >= 0; i--)
            {
                if (Roots[i] == null) { Roots.RemoveAt(i); continue; }
                Apply(Roots[i]);
            }
        }

        public static void Apply(VisualElement root)
        {
            var text = RkSettings.TextSize;
            root.EnableInClassList("rk-text-115", text >= 1.15f && text < 1.3f);
            root.EnableInClassList("rk-text-130", text >= 1.3f);
            root.EnableInClassList("rk-contrast", RkSettings.HighContrast);
            root.EnableInClassList("rk-reduced-motion", RkSettings.ReducedMotion);
        }

        public static void ApplySize(VisualElement root)
        {
            var w = root.resolvedStyle.width;
            var h = root.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f) return;
            root.EnableInClassList("rk-w-compact", w < CompactWidth);
            root.EnableInClassList("rk-w-narrow", w < NarrowWidth);
            root.EnableInClassList("rk-w-wide", w >= WideWidth);
            root.EnableInClassList("rk-h-short", !float.IsNaN(h) && h > 0f && h < ShortHeight);
        }

        /// <summary>The width class a root has now ("narrow", "compact", "wide" or "regular"), for tests and the audit.</summary>
        public static string WidthClass(VisualElement root) =>
            root == null ? "regular" : root.ClassListContains("rk-w-narrow") ? "narrow" : root.ClassListContains("rk-w-compact") ? "compact" : root.ClassListContains("rk-w-wide") ? "wide" : "regular";
    }
}
