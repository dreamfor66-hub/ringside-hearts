using System;
using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Press anywhere (except on a button) and drag; reports the pull in canvas units, y up.
    /// Works for mouse and touch through the Input System's Pointer.
    /// </summary>
    public sealed class AimInput : MonoBehaviour
    {
        public event Action<Vec2> PullChanged;
        public event Action<Vec2> Released;
        public event Action Cancelled;

        /// <summary>While true, presses are ignored and an ongoing drag is cancelled.</summary>
        public bool Locked;

        Canvas _canvas;
        bool _dragging;
        Vector2 _start;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public void Init(Canvas canvas) => _canvas = canvas;

        void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (Locked)
            {
                if (_dragging)
                {
                    _dragging = false;
                    Cancelled?.Invoke();
                }
                return;
            }

            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame && !IsOverButton(position))
            {
                _dragging = true;
                _start = position;
            }

            if (!_dragging) return;

            Vec2 pull = ToCanvas(position - _start);
            if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            {
                _dragging = false;
                Released?.Invoke(pull);
            }
            else
            {
                PullChanged?.Invoke(pull);
            }
        }

        /// <summary>True when a screen position is over a button (or any Selectable).</summary>
        public bool IsOverButton(Vector2 screenPosition)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            _hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPosition }, _hits);
            foreach (RaycastResult hit in _hits)
                if (hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            return false;
        }

        Vec2 ToCanvas(Vector2 screenDelta)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            return new Vec2(screenDelta.x / scale, screenDelta.y / scale);
        }
    }
}
