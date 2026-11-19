using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Two-handed presses this frame (design v0.2 0): left hand = mouse left / A / ← / left half of
    /// the touch screen, right hand = mouse right / D / → / right half.
    /// </summary>
    public static class HandInput
    {
        public static void Read(List<Hand> into)
        {
            into.Clear();
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) into.Add(Hand.Left);
                if (mouse.rightButton.wasPressedThisFrame) into.Add(Hand.Right);
            }
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) into.Add(Hand.Left);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) into.Add(Hand.Right);
            }
            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (TouchControl t in touch.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;
                    into.Add(t.position.ReadValue().x < Screen.width * 0.5f ? Hand.Left : Hand.Right);
                }
            }
        }
    }
}
