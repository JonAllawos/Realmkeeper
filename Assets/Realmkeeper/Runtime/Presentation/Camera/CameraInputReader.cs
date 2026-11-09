using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Reads keyboard and mouse through the Input System and turns them into a
    /// <see cref="CameraInput"/>. Contains no motion maths.
    /// Controls: WASD / arrow keys pan, middle-mouse drag pans, wheel zooms, Q / E rotate,
    /// optional screen-edge pan.
    /// </summary>
    public sealed class CameraInputReader
    {
        public const float EdgeMarginPixels = 8f;

        public bool EdgePanEnabled { get; set; }

        public CameraInput Read()
        {
            var input = CameraInput.None;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null)
            {
                var pan = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                input.Pan = pan;

                if (keyboard.qKey.wasPressedThisFrame) input.RotateSteps -= 1;
                if (keyboard.eKey.wasPressedThisFrame) input.RotateSteps += 1;
            }

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Windows reports 120 per notch; other platforms report about 1.
                    input.Zoom = Mathf.Abs(scroll) >= 60f ? scroll / 120f : Mathf.Sign(scroll);
                }

                if (mouse.middleButton.isPressed) input.DragPixels = mouse.delta.ReadValue();

                if (EdgePanEnabled && input.Pan == Vector2.zero && UnityEngine.Application.isFocused)
                {
                    var p = mouse.position.ReadValue();
                    if (p.x <= EdgeMarginPixels) input.Pan.x = -1f;
                    else if (p.x >= Screen.width - EdgeMarginPixels) input.Pan.x = 1f;
                    if (p.y <= EdgeMarginPixels) input.Pan.y = -1f;
                    else if (p.y >= Screen.height - EdgeMarginPixels) input.Pan.y = 1f;
                }
            }

            if (input.Pan.sqrMagnitude > 1f) input.Pan.Normalize();
            return input;
        }
    }
}
