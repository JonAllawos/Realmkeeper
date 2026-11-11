using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 11 (ADR-0051): a container in the street — the player's chest beside the tavern door, or the loose
    /// flagstone that hides a settlement's cache. Built from simple shapes (no imported art). Interacting opens it
    /// (a command marks it opened) and the controller shows its contents in the hub's Storage section; what it
    /// holds lives in the world's records, never in this object.
    /// </summary>
    public sealed class ContainerInteractable : MonoBehaviour, IInteractable
    {
        public string Key { get; private set; }
        public string DisplayName { get; private set; }
        public ContainerKind ContainerKind { get; private set; }

        public string Prompt => ContainerKind == ContainerKind.Cache ? "Lift the loose flagstone" : "Open your chest";
        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => transform.position;

        public static ContainerInteractable Create(Transform parent, string key, string name, ContainerKind kind, Vector3 position, float yaw, ExplorationMaterials m)
        {
            var go = new GameObject("Container " + key);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var t = go.transform;
            var timber = m.Get(ExplorationMaterials.Timber);
            var dark = m.Get(ExplorationMaterials.DarkTimber);
            var iron = m.Get(ExplorationMaterials.Slate, 0.45f);
            if (kind == ContainerKind.Cache)
            {
                var stone = m.Get(ExplorationMaterials.Stone);
                var slab = SettlementEnvironmentBuilder.Box(t, "Flagstone", new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.08f, 0.7f), stone);
                slab.transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
                SettlementEnvironmentBuilder.Box(t, "Gap", new Vector3(0.38f, 0.02f, 0f), new Vector3(0.12f, 0.03f, 0.66f), dark);
            }
            else
            {
                SettlementEnvironmentBuilder.Box(t, "Chest body", new Vector3(0f, 0.25f, 0f), new Vector3(0.95f, 0.5f, 0.55f), timber);
                SettlementEnvironmentBuilder.Box(t, "Chest lid", new Vector3(0f, 0.56f, 0f), new Vector3(0.99f, 0.12f, 0.59f), dark);
                SettlementEnvironmentBuilder.Box(t, "Band L", new Vector3(-0.3f, 0.32f, 0f), new Vector3(0.05f, 0.66f, 0.61f), iron);
                SettlementEnvironmentBuilder.Box(t, "Band R", new Vector3(0.3f, 0.32f, 0f), new Vector3(0.05f, 0.66f, 0.61f), iron);
                SettlementEnvironmentBuilder.Box(t, "Lock", new Vector3(0f, 0.45f, -0.29f), new Vector3(0.1f, 0.12f, 0.03f), iron);
            }

            var view = go.AddComponent<ContainerInteractable>();
            view.Key = key;
            view.DisplayName = name;
            view.ContainerKind = kind;
            return view;
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            var result = session.Dispatch(new OpenContainerCommand(Key));
            var view = session.GetContainer(Key);
            if (!result.IsSuccess || view == null) return new InteractionResponse(DisplayName, ResidentView.Describe(result), Kind, result);
            var count = 0;
            foreach (var r in view.Rows) count += r.Quantity;
            var text = view.Rows.Count == 0 && view.Coin == 0 ? "It is empty." : $"{count} item{(count == 1 ? string.Empty : "s")}{(view.Coin > 0 ? $" and {view.Coin} coin" : string.Empty)}.";
            return new InteractionResponse(view.Name, text, Kind, result);
        }
    }
}
