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
            // Normal: drag right turns the view right, drag up looks up (camera drops toward the ground).
            // Inverted (Settings): the world follows the finger, so both signs flip.
            float sign = ControlSettings.InvertCamera ? -1f : 1f;
            rig.Rotate(sign * eventData.delta.x * degreesPerPixel);
            rig.Tilt(-sign * eventData.delta.y * tiltDegreesPerPixel);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Dragging = false;
        }
    }
}
