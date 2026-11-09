using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Exploration.Interaction
{
    /// <summary>What the exploration controls read each frame. Tests supply scripted values.</summary>
    public interface IExplorationInput
    {
        /// <summary>Movement, x = right, y = forward, each −1..1.</summary>
        Vector2 Move { get; }

        /// <summary>Look delta this frame, in pointer pixels.</summary>
        Vector2 Look { get; }

        bool Sprint { get; }
        bool JumpPressed { get; }
        bool TogglePerspectivePressed { get; }
        bool InteractPressed { get; }
        bool ReturnToMapPressed { get; }
        bool SavePressed { get; }

        void Enable();
        void Disable();
    }

    /// <summary>
    /// Keyboard and mouse bindings as named Input System actions, built in code: one
    /// <see cref="InputActionMap"/> ("Exploration"), so bindings can be overridden or rebound
    /// later (InputActionRebindingExtensions) without touching gameplay code.
    /// </summary>
    public sealed class InputSystemExplorationInput : IExplorationInput, IDisposable
    {
        private readonly InputActionMap _map = new InputActionMap("Exploration");
        private readonly InputAction _move;
        private readonly InputAction _look;
        private readonly InputAction _sprint;
        private readonly InputAction _jump;
        private readonly InputAction _perspective;
        private readonly InputAction _interact;
        private readonly InputAction _returnToMap;
        private readonly InputAction _save;

        public InputSystemExplorationInput()
        {
            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _look = _map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            _sprint = _map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _jump = _map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _perspective = _map.AddAction("TogglePerspective", InputActionType.Button, "<Keyboard>/v");
            _interact = _map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _returnToMap = _map.AddAction("ReturnToMap", InputActionType.Button, "<Keyboard>/m");
            _save = _map.AddAction("Save", InputActionType.Button, "<Keyboard>/f5");
        }

        public InputActionMap Map => _map;

        public Vector2 Move => Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
        public Vector2 Look => _look.ReadValue<Vector2>();
        public bool Sprint => _sprint.IsPressed();
        public bool JumpPressed => _jump.WasPressedThisFrame();
        public bool TogglePerspectivePressed => _perspective.WasPressedThisFrame();
        public bool InteractPressed => _interact.WasPressedThisFrame();
        public bool ReturnToMapPressed => _returnToMap.WasPressedThisFrame();
        public bool SavePressed => _save.WasPressedThisFrame();

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();

        public void Dispose()
        {
            _map.Disable();
            _map.Dispose();
        }
    }

    /// <summary>Input with fixed values, for tests and scripted runs.</summary>
    public sealed class ScriptedExplorationInput : IExplorationInput
    {
        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool Sprint { get; set; }
        public bool JumpPressed { get; set; }
        public bool TogglePerspectivePressed { get; set; }
        public bool InteractPressed { get; set; }
        public bool ReturnToMapPressed { get; set; }
        public bool SavePressed { get; set; }

        public void Enable() { }
        public void Disable() { }

        /// <summary>Clears one-frame presses (call after each simulated frame).</summary>
        public void ClearPresses()
        {
            JumpPressed = TogglePerspectivePressed = InteractPressed = ReturnToMapPressed = SavePressed = false;
        }
    }
}
