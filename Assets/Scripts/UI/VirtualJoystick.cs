using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeBreaker
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform handle;
        public float radius = 90f;
        public Vector2 Direction { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local))
                return;
            var clamped = Vector2.ClampMagnitude(local, radius);
            if (handle != null)
                handle.anchoredPosition = clamped;
            Direction = clamped / Mathf.Max(1f, radius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }
    }
}
