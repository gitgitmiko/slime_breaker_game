using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeBreaker
{
    /// <summary>
    /// Touch joystick. Fixed mode (default): only a touch on the base steers, and the base
    /// stays put. Free mode (Settings): this object is the touch zone, the base jumps to
    /// wherever the thumb lands, follows the thumb past the rim, and returns on release.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform handle;
        public float radius = 90f;
        public Vector2 Direction { get; private set; }
        public bool Held { get; private set; }

        /// <summary>Extra grab margin around the base in fixed mode, in canvas units.</summary>
        public float fixedGrabMargin = 40f;

        // Anchored (not local) so the rest spot survives canvas scaling and safe-area layout.
        Vector2 restPosition;
        bool hasRest;
        bool tracking;

        bool Free => ControlSettings.FreeJoystick;

        void Awake()
        {
            StoreRest();
        }

        void StoreRest()
        {
            if (hasRest || background == null)
                return;
            restPosition = background.anchoredPosition;
            hasRest = true;
        }

        RectTransform Zone => (RectTransform)transform;

        public void OnPointerDown(PointerEventData eventData)
        {
            StoreRest();
            if (background != null && background != Zone)
            {
                if (Free)
                {
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(Zone, eventData.position, eventData.pressEventCamera, out var world))
                        background.position = world;
                }
                else
                {
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out var onBase))
                        return;
                    float grab = Mathf.Max(background.rect.width, background.rect.height) * 0.5f + fixedGrabMargin;
                    if ((onBase - background.rect.center).magnitude > grab)
                        return;
                }
            }
            tracking = true;
            Held = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!tracking)
                return;
            var frame = background != null ? background : Zone;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, eventData.position, eventData.pressEventCamera, out var local))
                return;
            // Local point is relative to the base's pivot, so the base centre is the stick's origin.
            Vector2 centre = frame.rect.center;
            Vector2 offset = local - centre;
            if (Free && background != null && background != Zone && offset.magnitude > radius)
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
            tracking = false;
            Held = false;
            Direction = Vector2.zero;
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
            if (background != null && background != Zone && hasRest)
                background.anchoredPosition = restPosition;
        }
    }
}
