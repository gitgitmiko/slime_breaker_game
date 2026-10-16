using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeBreaker
{
    /// <summary>
    /// Free (floating) joystick: this object is the touch zone, and the visible base
    /// jumps to wherever the thumb lands. The base follows the thumb if it is dragged
    /// past the rim, and returns to its rest spot on release.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform handle;
        public float radius = 90f;
        public Vector2 Direction { get; private set; }
        public bool Held { get; private set; }

        Vector3 restPosition;
        bool hasRest;

        void Awake()
        {
            StoreRest();
        }

        void StoreRest()
        {
            if (hasRest || background == null)
                return;
            restPosition = background.localPosition;
            hasRest = true;
        }

        RectTransform Zone => (RectTransform)transform;

        public void OnPointerDown(PointerEventData eventData)
        {
            StoreRest();
            Held = true;
            if (background != null && background != Zone
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(Zone, eventData.position, eventData.pressEventCamera, out var local))
            {
                background.localPosition = new Vector3(local.x, local.y, restPosition.z);
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            var frame = background != null ? background : Zone;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, eventData.position, eventData.pressEventCamera, out var local))
                return;
            // Local point is relative to the base's pivot, so the base centre is the stick's origin.
            Vector2 centre = frame.rect.center;
            Vector2 offset = local - centre;
            if (background != null && background != Zone && offset.magnitude > radius)
            {
                // Drag the base along so the stick never "sticks" at the rim.
                Vector2 excess = offset - offset.normalized * radius;
                background.localPosition += (Vector3)excess;
                offset -= excess;
            }
            var clamped = Vector2.ClampMagnitude(offset, radius);
            if (handle != null)
                handle.anchoredPosition = clamped;
            Direction = clamped / Mathf.Max(1f, radius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
            Direction = Vector2.zero;
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
            if (background != null && background != Zone && hasRest)
                background.localPosition = restPosition;
        }
    }
}
