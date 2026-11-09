using System;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Settlement selection. Raycasts from the cursor to a <see cref="SettlementView"/> and keeps
    /// the selected <see cref="SettlementId"/> (never a name). Escape or clicking empty ground
    /// clears it. Shows a glowing ring around the selected settlement.
    /// </summary>
    public sealed class SettlementSelection : MonoBehaviour
    {
        public const float MaxRayDistance = 2000f;

        private WorldSession _session;
        private WorldView _worldView;
        private Camera _camera;
        private SelectionHighlight _highlight;

        /// <summary>Raised with the new selection (<see cref="SettlementId.Empty"/> when cleared).</summary>
        public event Action<SettlementId> SelectionChanged;

        public SettlementId SelectedId { get; private set; }

        public bool HasSelection => !SelectedId.IsEmpty;

        public bool HighlightVisible => _highlight != null && _highlight.IsVisible;

        /// <summary>Optional: returns true when the pointer is over UI, so clicks there are ignored.</summary>
        public Func<Vector2, bool> IsPointerOverUi { get; set; }

        /// <summary>When false, Update ignores devices; tests call the public methods directly.</summary>
        public bool ReadInput { get; set; } = true;

        public void Initialize(WorldSession session, WorldView worldView, Camera targetCamera, Material material)
        {
            _session = session;
            _worldView = worldView;
            _camera = targetCamera;

            var highlightObject = new GameObject("Selection Highlight");
            highlightObject.transform.SetParent(transform, false);
            _highlight = highlightObject.AddComponent<SelectionHighlight>();
            _highlight.Initialize(material);

            _session.WorldChanged += OnWorldChanged;
        }

        public void Select(SettlementId id)
        {
            if (id.IsEmpty || _session?.FindSettlement(id) == null || _worldView.FindSettlementView(id) == null)
            {
                Clear();
                return;
            }

            if (id == SelectedId) return;
            SelectedId = id;
            _highlight.Show(_worldView.Map, _worldView.FindSettlementView(id));
            SelectionChanged?.Invoke(SelectedId);
        }

        /// <summary>Hides or shows the highlight object without changing the selection (used while exploring).</summary>
        public void SetHighlightShown(bool shown)
        {
            if (_highlight != null) _highlight.gameObject.SetActive(shown);
        }

        public void Clear()
        {
            _highlight?.Hide();
            if (SelectedId.IsEmpty) return;
            SelectedId = SettlementId.Empty;
            SelectionChanged?.Invoke(SelectedId);
        }

        /// <summary>Selects the settlement under a screen point, or clears the selection. Returns true if one was selected.</summary>
        public bool SelectAtScreenPoint(Vector2 screenPoint)
        {
            var view = PickSettlement(screenPoint);
            if (view == null)
            {
                Clear();
                return false;
            }

            Select(view.SettlementId);
            return true;
        }

        public SettlementView PickSettlement(Vector2 screenPoint)
        {
            if (_camera == null) return null;
            var ray = _camera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out var hit, MaxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<SettlementView>();
        }

        private void Update()
        {
            if (!ReadInput) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Clear();

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var position = mouse.position.ReadValue();
            if (IsPointerOverUi != null && IsPointerOverUi(position)) return;
            SelectAtScreenPoint(position);
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (!change.GeographyChanged || SelectedId.IsEmpty) return;

            // Visuals were rebuilt: keep the selection if the settlement still exists.
            var id = SelectedId;
            var view = _worldView.FindSettlementView(id);
            if (view == null || _session.FindSettlement(id) == null)
            {
                Clear();
                return;
            }

            _highlight.Show(_worldView.Map, view);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.WorldChanged -= OnWorldChanged;
        }
    }
}
