using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Phase 13 (ADR-0065): the places the player knows of, on the strategic map. A rumoured place is listed by name and
    /// rough direction only; a discovered one has a marker where it lies and can be entered (by its known ways in).
    /// Markers and the list are rebuilt from the world's view of what the player knows; they hold nothing themselves.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        private ScrollView _placeList;
        private GameObject _placeMarkers;
        private Material _markerMaterial;
        private Material _markerDoneMaterial;

        /// <summary>Marker objects by place id (tests and captures read them).</summary>
        public Dictionary<string, GameObject> PlaceMarkers { get; } = new Dictionary<string, GameObject>();

        public ScrollView PlaceList => _placeList;

        private void BindPlaces()
        {
            var nav = Root.Q<VisualElement>("nav");
            var after = Root.Q<ScrollView>("settlement-list");
            if (nav == null || after == null) return;
            var button = new Button { name = "nav-places", text = "Places" };
            button.AddToClassList("rk-nav-item");
            _placeList = new ScrollView { name = "place-list" };
            _placeList.AddToClassList("rk-settlement-list");
            button.clicked += () => _placeList.ToggleInClassList("rk-hidden");
            var index = nav.IndexOf(after);
            nav.Insert(index + 1, button);
            nav.Insert(index + 2, _placeList);
        }

        private void RefreshPlaces()
        {
            if (_placeList == null) return;
            _placeList.Clear();
            var places = _session != null && _session.HasWorld ? _session.GetKnownPlaces() : new List<PlaceSummaryView>();
            if (places.Count == 0)
            {
                var none = new Label("No places known yet. Ask about rumours.");
                none.AddToClassList("rk-muted");
                _placeList.Add(none);
            }

            foreach (var p in places)
            {
                var row = new VisualElement { name = "place-" + p.PlaceId };
                row.AddToClassList("rk-place-row");
                var title = new Label(p.Name + (p.HasQuest ? " ◆" : string.Empty));
                title.AddToClassList("rk-place-name");
                row.Add(title);
                var state = p.Discovery == PlaceDiscovery.Rumored ? "rumoured" : p.Discovery.ToString().ToLowerInvariant();
                var line = new Label($"{p.Kind} · {state} · {p.Direction}" + (p.Discovery >= PlaceDiscovery.Visited ? " · " + p.Threat : string.Empty) + (p.State.Count > 0 ? " · " + string.Join(", ", p.State) : string.Empty)); // Phase 14: place states
                line.AddToClassList("rk-place-line");
                row.Add(line);
                foreach (var (id, label) in p.Entrances)
                {
                    var placeId = p.PlaceId;
                    var entrance = id;
                    var enter = new Button { name = "place-enter-" + p.PlaceId + "-" + id, text = "Enter: " + label };
                    enter.AddToClassList("rk-button");
                    enter.clicked += () => EnterPlace(placeId, entrance, label.Contains("locked") ? PlaceAction.Force : (PlaceAction?)null);
                    row.Add(enter);
                }

                _placeList.Add(row);
            }

            RefreshPlaceMarkers(places);
        }

        /// <summary>Turns the map camera to a known place's marker (its whereabouts must be known).</summary>
        public bool FocusPlace(string placeId)
        {
            if (!PlaceMarkers.TryGetValue(placeId, out var marker) || marker == null || _camera == null) return false;
            _camera.FocusOn(marker.transform.position);
            return true;
        }

        /// <summary>Goes into a known place by one of its ways in; the view switches when the world says the character is inside.</summary>
        public CommandResult EnterPlace(string placeId, string entranceId = "entrance", PlaceAction? approach = null)
        {
            if (_session == null) return NoSession();
            var result = _session.Dispatch(new EnterPlaceCommand(placeId, entranceId, approach));
            ShowMessage(result);
            return result;
        }

        private void RefreshPlaceMarkers(IReadOnlyList<PlaceSummaryView> places)
        {
            var map = _worldView != null ? _worldView.Map : null;
            if (map == null) return;
            if (_placeMarkers == null) _placeMarkers = new GameObject("Place markers");
            if (_markerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _markerMaterial = new Material(shader) { color = new Color(0.82f, 0.56f, 0.22f) };
                _markerDoneMaterial = new Material(shader) { color = new Color(0.45f, 0.62f, 0.38f) };
            }

            var seen = new HashSet<string>();
            foreach (var p in places)
            {
                if (p.Cell == null) continue; // a rumour has no place on the map
                seen.Add(p.PlaceId);
                if (!PlaceMarkers.TryGetValue(p.PlaceId, out var marker) || marker == null)
                {
                    marker = new GameObject("Place marker " + p.Name);
                    marker.transform.SetParent(_placeMarkers.transform, false);
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(post.GetComponent<Collider>());
                    post.name = "Post";
                    post.transform.SetParent(marker.transform, false);
                    post.transform.localScale = new Vector3(0.35f, 2.2f, 0.35f);
                    post.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(head.GetComponent<Collider>());
                    head.name = "Head";
                    head.transform.SetParent(marker.transform, false);
                    head.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                    head.transform.localPosition = new Vector3(0f, 5.0f, 0f);
                    head.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    PlaceMarkers[p.PlaceId] = marker;
                }

                marker.transform.position = MapSampling.CellToWorld(map, p.Cell.Value);
                var mat = p.Discovery >= PlaceDiscovery.Cleared ? _markerDoneMaterial : _markerMaterial;
                foreach (var r in marker.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            }

            foreach (var id in new List<string>(PlaceMarkers.Keys))
                if (!seen.Contains(id))
                {
                    if (PlaceMarkers[id] != null) Destroy(PlaceMarkers[id]);
                    PlaceMarkers.Remove(id);
                }
        }
    }
}
