using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeBreaker
{
    /// <summary>Invisible touch zone over the right half of the screen: dragging sideways orbits the camera around the hero.</summary>
    public class CameraDragZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public FollowCamera cameraRig;
        /// <summary>Degrees of orbit per screen pixel dragged.</summary>
        public float degreesPerPixel = 0.16f;
        public float tiltDegreesPerPixel = 0.12f;

        public bool Dragging { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Dragging || Time.timeScale <= 0f)
                return;
            var rig = cameraRig != null ? cameraRig : (Camera.main != null ? Camera.main.GetComponent<FollowCamera>() : null);
            if (rig == null || rig.IsFocusing)
                return;
            // Dragging right turns the view left, so the world follows the finger.
            rig.Rotate(-eventData.delta.x * degreesPerPixel);
            // Dragging up lifts the camera toward a top-down view, dragging down lowers it (the world follows the finger).
            rig.Tilt(eventData.delta.y * tiltDegreesPerPixel);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Dragging = false;
        }
    }
}
