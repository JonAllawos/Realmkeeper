using System.Collections.Generic;
using Realmkeeper.Application;
using UnityEngine;

namespace Realmkeeper.Exploration.Interaction
{
    /// <summary>Whether an interaction changes the authoritative world or only reads it.</summary>
    public enum InteractionKind
    {
        /// <summary>Reads world data and shows it; changes nothing.</summary>
        Informational = 0,

        /// <summary>Dispatches a command through the registry; consequences persist.</summary>
        StateChanging = 1,
    }

    /// <summary>What the player sees after an interaction.</summary>
    public sealed class InteractionResponse
    {
        public InteractionResponse(string title, string text, InteractionKind kind, CommandResult command = null)
        {
            Title = title;
            Text = text;
            Kind = kind;
            Command = command;
        }

        public string Title { get; }
        public string Text { get; }
        public InteractionKind Kind { get; }

        /// <summary>The command result for state-changing interactions (null for informational ones).</summary>
        public CommandResult Command { get; }

        public bool Succeeded => Command == null || Command.IsSuccess;
    }

    /// <summary>Something in the local environment the player can interact with.</summary>
    /// <summary>An interactable that is sometimes unavailable (Phase 5: a resident indoors or asleep cannot be approached).</summary>
    public interface IConditionalInteractable
    {
        bool IsAvailable { get; }
    }

    public interface IInteractable
    {
        /// <summary>Prompt text, e.g. "Talk to Aldric Ashby (Elder)".</summary>
        string Prompt { get; }

        InteractionKind Kind { get; }

        /// <summary>Point the player must be near (world space).</summary>
        Vector3 InteractionPoint { get; }

        /// <summary>Performs the interaction. State changes go only through <paramref name="session"/>'s command registry.</summary>
        InteractionResponse Interact(WorldSession session, Transform player);
    }

    /// <summary>
    /// Finds the interactable the player is facing: within <see cref="Range"/> metres and within a
    /// forward cone, nearest first. Pure geometry over registered interactables (no physics),
    /// so it is cheap and testable.
    /// </summary>
    public sealed class InteractionDetector
    {
        private readonly List<IInteractable> _interactables = new List<IInteractable>();

        public float Range { get; set; } = 2.6f;

        /// <summary>Cosine of the half-angle of the facing cone (0.35 ≈ 70°).</summary>
        public float MinFacingDot { get; set; } = 0.35f;

        public IReadOnlyList<IInteractable> Interactables => _interactables;

        public void Register(IInteractable interactable)
        {
            if (interactable != null && !_interactables.Contains(interactable)) _interactables.Add(interactable);
        }

        /// <summary>Phase 13: a place's things come and go as the world changes.</summary>
        public void Unregister(IInteractable interactable) => _interactables.Remove(interactable);

        public void Clear() => _interactables.Clear();

        public IInteractable FindTarget(Vector3 position, Vector3 forward)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            IInteractable best = null;
            var bestDistance = float.MaxValue;
            foreach (var i in _interactables)
            {
                if (i is IConditionalInteractable c && !c.IsAvailable) continue;
                var to = i.InteractionPoint - position;
                to.y = 0f;
                var distance = to.magnitude;
                if (distance > Range) continue;
                if (distance > 0.3f && Vector3.Dot(forward, to / distance) < MinFacingDot) continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }
    }
}
