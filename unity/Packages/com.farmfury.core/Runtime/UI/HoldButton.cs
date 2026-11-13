using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// An on-screen control that reports being held: down on touch / click, up on release or when the finger slides
    /// off it. Releases when disabled (the HUD hid).
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action Pressed;
        public event Action Released;
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => Press();

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void OnDisable()
        {
            if (IsHeld)
            {
                IsHeld = false;
                Released?.Invoke();
            }
        }

        private void Press()
        {
            if (!IsHeld)
            {
                IsHeld = true;
                Pressed?.Invoke();
            }
        }

        private void Release()
        {
            if (IsHeld)
            {
                IsHeld = false;
                Released?.Invoke();
            }
        }
    }
}
