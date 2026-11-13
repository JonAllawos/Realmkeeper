using System;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 21 (ADR-0107): a work station standing in a shop (the smith's forge, the leatherworker's bench, the baker's
    /// oven …). Interacting with it opens crafting at this station: the work is then started through the ordinary craft
    /// command naming this shop, which checks that the shop is open and its keeper there. The station's state is the
    /// shop's; this is only where it stands.
    /// </summary>
    public sealed class WorkStationView : MonoBehaviour, IInteractable
    {
        public ShopId ShopId { get; private set; }
        public StationKind Station { get; private set; }
        public string ShopName { get; set; } = "the shop";

        public event Action<WorkStationView> WorkRequested;

        public string Prompt => $"Work at the {StationWords.Name(Station)} ({ShopName})";
        public InteractionKind Kind => InteractionKind.Informational;
        public Vector3 InteractionPoint => transform.position;

        public static WorkStationView Create(Transform parent, Vector3 localPosition, ShopId shop, StationKind station)
        {
            var go = new GameObject("Station: " + station);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var view = go.AddComponent<WorkStationView>();
            view.ShopId = shop;
            view.Station = station;
            return view;
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            WorkRequested?.Invoke(this);
            return new InteractionResponse(ShopName, $"You step up to the {StationWords.Name(Station)}.", Kind, CommandResult.Ok());
        }
    }
}
