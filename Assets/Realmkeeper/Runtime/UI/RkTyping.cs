using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Whether the player is typing in any interface text field (every UI document, the title screen's too), so that
    /// bound keys (camera pan, rotate and zoom, hotkeys) do not act on the letters typed (owner report, 2026-11-14:
    /// the map turned and zoomed while a new world was being named).
    /// </summary>
    public static class RkTyping
    {
        private static UIDocument[] _documents = new UIDocument[0];
        private static float _refreshedAt = -10f;

        public static bool Active()
        {
            if (Time.unscaledTime - _refreshedAt > 0.5f || _documents.Length == 0)
            {
                _documents = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                _refreshedAt = Time.unscaledTime;
            }

            foreach (var doc in _documents)
            {
                if (doc == null || !doc.isActiveAndEnabled) continue;
                if (IsTextEntry(doc.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement)) return true;
            }

            return false;
        }

        public static bool IsTextEntry(VisualElement focused)
        {
            if (focused == null) return false;
            if (focused is TextField || focused is IntegerField || focused is FloatField) return true;
            return focused.GetFirstAncestorOfType<TextField>() != null || focused.GetFirstAncestorOfType<IntegerField>() != null || focused.GetFirstAncestorOfType<FloatField>() != null;
        }
    }
}
