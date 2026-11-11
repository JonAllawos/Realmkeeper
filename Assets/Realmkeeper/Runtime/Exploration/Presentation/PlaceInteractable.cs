using System;
using Realmkeeper.Application;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 13 (ADR-0064): something in a place the player can act on — a feature, a door or passage, stairs, the way
    /// out, a loot pile, a person or a group of foes — or a building's door in a settlement. It holds only an id and a
    /// prompt; acting asks the controller, which offers the actions the world allows and dispatches the chosen one.
    /// </summary>
    public sealed class PlaceInteractable : MonoBehaviour, IInteractable
    {
        public enum TargetKind
        {
            Feature = 0,
            Passage = 1,
            Stairs = 2,
            Exit = 3,
            Loot = 4,
            Person = 5,
            Foes = 6,
            BuildingDoor = 7,
            Room = 8,
        }

        public TargetKind Target { get; private set; }
        public string TargetId { get; private set; }
        public string Label { get; private set; }
        public Vector3 Point { get; private set; }

        /// <summary>Set by the controller: performs the interaction (offering choices where there are several).</summary>
        public Func<PlaceInteractable, InteractionResponse> Handler { get; set; }

        public string Prompt => Label;
        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => Point;

        public static PlaceInteractable Create(Transform parent, TargetKind kind, string id, string label, Vector3 point)
        {
            var go = new GameObject("Interact " + kind + " " + id);
            go.transform.SetParent(parent, false);
            go.transform.position = point;
            var p = go.AddComponent<PlaceInteractable>();
            p.Target = kind;
            p.TargetId = id;
            p.Label = label;
            p.Point = point;
            return p;
        }

        public InteractionResponse Interact(WorldSession session, Transform player) =>
            Handler != null ? Handler(this) : new InteractionResponse(Label, "Nothing happens.", InteractionKind.Informational);
    }
}
