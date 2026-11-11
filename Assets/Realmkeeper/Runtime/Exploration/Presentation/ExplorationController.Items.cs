using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 11 (ADR-0051, ADR-0055): items in exploration — the item actions the hub and the shop ask for, waiting out
    /// work, the player's chest and the settlement's cache standing in the street, and the loot a fight leaves.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private readonly List<ContainerInteractable> _containers = new List<ContainerInteractable>();

        public IReadOnlyList<ContainerInteractable> Containers => _containers;

        /// <summary>The last item action's result (tests and captures read it).</summary>
        public CommandResult LastItemResult { get; private set; }

        /// <summary>Runs an item action the hub asked for, then shows the hub again with its result.</summary>
        public CommandResult ItemAction(Func<WorldSession, CommandResult> act)
        {
            if (_session == null || act == null) return CommandResult.InvalidState("No world session is active.");
            var result = act(_session) ?? CommandResult.Fail("Nothing happened.");
            LastItemResult = result;
            AfterQuestCommand(); // Phase 12: a journal action, a delivery or a purchase may move a quest
            if (Trade != null && Trade.IsOpen && TradingAt != null)
            {
                Trade.Show(_session.GetShopDetail(TradingAt.ShopId), ResidentView.Describe(result), !result.IsSuccess);
                return result;
            }

            ShowHub(ResidentView.Describe(result), !result.IsSuccess);
            return result;
        }

        /// <summary>Waits out the current work (the world lives through the time) and collects it.</summary>
        public CommandResult FinishWork(string key)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.FinishWork(key);
            LastItemResult = result;
            RefreshLiving(snap: true);
            ShowHub(ResidentView.Describe(result), !result.IsSuccess);
            HubWindow?.Select("Crafting & work");
            return result;
        }

        /// <summary>Opens the hub's Storage section, with a container first (as opening a chest in the street does).</summary>
        public void OpenStorage(string containerKey)
        {
            if (HubWindow == null) return;
            HubWindow.FocusContainer = containerKey;
            ShowHub(null, false);
            HubWindow.Select("Storage");
        }

        /// <summary>The chest and the cache of the settlement the player is in, at the buildings their records name.</summary>
        private void SetupContainers(LocalEnvironmentLayout layout)
        {
            _containers.Clear();
            if (_session == null || EnvironmentRoot == null) return;
            var root = new GameObject("Containers").transform;
            var n = 0;
            foreach (var c in _session.GetReachableContainers())
            {
                if (c.Kind != Realmkeeper.Domain.World.ContainerKind.Personal && c.Kind != Realmkeeper.Domain.World.ContainerKind.Cache) continue;
                var b = string.IsNullOrEmpty(c.LayoutKey) ? null : LocalPaths.Find(layout, c.LayoutKey);
                LocalPoint spot;
                float facing;
                if (b != null)
                {
                    spot = LocalPaths.DoorOutside(b, c.Kind == Realmkeeper.Domain.World.ContainerKind.Personal ? 1.9f : -2.2f);
                    facing = LocalPaths.FacingOut(b);
                }
                else if (layout.Interactables.Count > 0)
                {
                    // An open place (the square) has no door: the cache's flagstone lies beside the square's notice board.
                    var board = layout.Interactables[layout.Interactables.Count - 1];
                    var yawRad = board.Yaw * Mathf.Deg2Rad;
                    spot = new LocalPoint(board.Position.X + 2.6f * Mathf.Cos(yawRad), board.Position.Z + 2.6f * Mathf.Sin(yawRad)); // along the board (its local right is rotated by −yaw)
                    facing = -board.Yaw;
                }
                else continue;

                var x = spot.X;
                var z = spot.Z;
                var view = ContainerInteractable.Create(root, c.Key, c.Name, c.Kind, new Vector3(x, LocalPaths.StandingHeight(layout, x, z), z), facing, _materials);
                _containers.Add(view);
                _detector.Register(view);
                n++;
            }

            if (n > 0) Debug.Log($"Realmkeeper: {n} containers placed in {layout.EnvironmentKey}.");
        }
    }
}
