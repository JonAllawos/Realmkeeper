using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// An interaction point in the environment. The granary ledger is informational: it reads
    /// the settlement's food data and changes nothing. The labour notice board is state-changing:
    /// it dispatches <see cref="PostLabourNoticeCommand"/>, which the simulation then honours.
    /// </summary>
    public sealed class EnvironmentInteractable : MonoBehaviour, IInteractable
    {
        public InteractableKind InteractableKind { get; private set; }
        public SettlementId SettlementId { get; private set; }

        public InteractionKind Kind =>
            InteractableKind == InteractableKind.GranaryLedger ? InteractionKind.Informational : InteractionKind.StateChanging;

        public string Prompt => InteractableKind == InteractableKind.GranaryLedger ? "Read the granary ledger" : "Post a labour notice";

        public Vector3 InteractionPoint => transform.position;

        public static EnvironmentInteractable Create(Transform parent, LayoutInteractable spec, SettlementId settlementId, ExplorationMaterials m)
        {
            var go = new GameObject(spec.Kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(spec.Position.X, 0f, spec.Position.Z);
            go.transform.localRotation = Quaternion.Euler(0f, -spec.Yaw, 0f);
            var t = go.transform;
            var dark = m.Get(ExplorationMaterials.DarkTimber);
            var parchment = m.Get(ExplorationMaterials.Parchment);

            if (spec.Kind == InteractableKind.GranaryLedger)
            {
                SettlementEnvironmentBuilder.Box(t, "Lectern", new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 1.1f, 0.4f), dark);
                var book = SettlementEnvironmentBuilder.Box(t, "Ledger", new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.06f, 0.45f), parchment);
                book.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }
            else
            {
                SettlementEnvironmentBuilder.Box(t, "Post L", new Vector3(-0.9f, 1f, 0f), new Vector3(0.14f, 2f, 0.14f), dark);
                SettlementEnvironmentBuilder.Box(t, "Post R", new Vector3(0.9f, 1f, 0f), new Vector3(0.14f, 2f, 0.14f), dark);
                SettlementEnvironmentBuilder.Box(t, "Board", new Vector3(0f, 1.45f, 0f), new Vector3(1.9f, 1.1f, 0.08f), m.Get(ExplorationMaterials.Timber));
                SettlementEnvironmentBuilder.Box(t, "Notice A", new Vector3(-0.45f, 1.5f, -0.05f), new Vector3(0.5f, 0.65f, 0.02f), parchment);
                SettlementEnvironmentBuilder.Box(t, "Notice B", new Vector3(0.4f, 1.4f, -0.05f), new Vector3(0.6f, 0.45f, 0.02f), parchment);
                SettlementEnvironmentBuilder.Box(t, "Cap", new Vector3(0f, 2.1f, 0f), new Vector3(2.2f, 0.12f, 0.4f), dark);
            }

            var view = go.AddComponent<EnvironmentInteractable>();
            view.InteractableKind = spec.Kind;
            view.SettlementId = settlementId;
            return view;
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            var s = session.FindSettlement(SettlementId);
            if (s == null) return new InteractionResponse(Prompt, "This settlement no longer exists.", Kind);

            if (InteractableKind == InteractableKind.GranaryLedger)
            {
                var food = s.Food;
                var days = s.DaysOfFoodRemaining;
                var text = $"Food in store: {N(food.Stock)}. Brought in yesterday: {N(food.Produced)}. Eaten: {N(food.Demand)}. " +
                           (days.HasValue ? $"At this rate the stores last about {N(days.Value)} days." : "Stores are not running down.") +
                           (s.LabourFocus.HasValue ? $" Notice on the board: labour favours {Lower(s.LabourFocus.Value)}." : string.Empty);
                return new InteractionResponse($"{s.Name} granary ledger", text, Kind);
            }

            var focus = NextFocus(s.Production, s.LabourFocus);
            if (!focus.HasValue) return new InteractionResponse("Labour notice", $"{s.Name} has no work to favour.", Kind);
            var result = session.Dispatch(new PostLabourNoticeCommand(SettlementId, focus.Value));
            return new InteractionResponse("Labour notice", result.IsSuccess ? result.Message : ResidentView.Describe(result), Kind, result);
        }

        /// <summary>The next production source after the current focus, cycling through the settlement's sources.</summary>
        public static ProductionSourceType? NextFocus(IReadOnlyList<ProductionSummary> production, ProductionSourceType? current)
        {
            if (production == null || production.Count == 0) return null;
            var index = -1;
            for (var i = 0; i < production.Count; i++)
            {
                if (current.HasValue && production[i].SourceType == current.Value) index = i;
            }

            var next = production[(index + 1) % production.Count].SourceType;
            return current.HasValue && next == current.Value ? (ProductionSourceType?)null : next;
        }

        private static string Lower(ProductionSourceType t) => t.ToString().ToLowerInvariant();
        private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
