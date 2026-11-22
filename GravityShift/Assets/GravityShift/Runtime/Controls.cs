using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GravityShift
{
    [Flags]
    internal enum ActionButton
    {
        Jump = 1, Dash = 2, Attack = 4, Style = 8, Retry = 16, NewRun = 32,
        Flip = 64, Rotate = 128, Primary = 256, Secondary = 512, Pause = 1024
    }

    internal struct Controls
    {
        public Vector2 Move;
        public Vector2 Pointer;
        public Vector2 PrimaryPointer;
        public Vector2 SecondaryPointer;
        public bool PointerInRoom;
        public bool JumpHeld;
        public bool SecondaryHeld;
        public ActionButton Presses;
        public int Wheel;
        private ActionButton previousKeys;

        public bool Has(ActionButton button) => (Presses & button) != 0;

        public void Poll(Camera camera)
        {
            var keys = Keyboard.current;
            if (keys != null)
            {
                int x = ((keys.dKey.isPressed || keys.rightArrowKey.isPressed) ? 1 : 0) -
                        ((keys.aKey.isPressed || keys.leftArrowKey.isPressed) ? 1 : 0);
                int y = ((keys.wKey.isPressed || keys.upArrowKey.isPressed) ? 1 : 0) -
                        ((keys.sKey.isPressed || keys.downArrowKey.isPressed) ? 1 : 0);
                Move = new Vector2(x, y);
                JumpHeld = keys.spaceKey.isPressed;
                CaptureKey(ActionButton.Jump, keys.spaceKey);
                CaptureKey(ActionButton.Dash, keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed,
                    keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame);
                CaptureKey(ActionButton.Attack, keys.jKey);
                CaptureKey(ActionButton.Style, keys.tabKey);
                CaptureKey(ActionButton.Retry, keys.rKey);
                CaptureKey(ActionButton.NewRun, keys.nKey);
                CaptureKey(ActionButton.Flip, keys.fKey);
                CaptureKey(ActionButton.Rotate, keys.qKey);
                CaptureKey(ActionButton.Pause, keys.escapeKey);
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                PointerInRoom = false;
                SecondaryHeld = false;
                return;
            }
            Vector2 screen = mouse.position.ReadValue();
            Pointer = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            PointerInRoom = Pointer.x >= 0 && Pointer.x < RoomMap.Width && Pointer.y <= 0 && Pointer.y > -RoomMap.Height;
            SecondaryHeld = mouse.rightButton.isPressed;
            if (!PointerInRoom) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Presses |= ActionButton.Primary;
                PrimaryPointer = Pointer;
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                Presses |= ActionButton.Secondary;
                SecondaryPointer = Pointer;
            }
            Wheel += Math.Sign(mouse.scroll.y.ReadValue());
        }

        private void CaptureKey(ActionButton button, ButtonControl key) =>
            CaptureKey(button, key.isPressed, key.wasPressedThisFrame);

        private void CaptureKey(ActionButton button, bool held, bool pressedThisFrame)
        {
            // Retain short taps and detect a transition when the current device supplies only its held state.
            if (pressedThisFrame || (held && (previousKeys & button) == 0)) Presses |= button;
            if (held) previousKeys |= button;
            else previousKeys &= ~button;
        }

        public void ConsumePresses()
        {
            Presses = 0;
            Wheel = 0;
        }
        public void ConsumePresses(ActionButton buttons) => Presses &= ~buttons;

        public void DiscardGameplayPresses()
        {
            Presses &= ActionButton.Pause | ActionButton.Retry | ActionButton.NewRun;
            Wheel = 0;
        }

    }
}
