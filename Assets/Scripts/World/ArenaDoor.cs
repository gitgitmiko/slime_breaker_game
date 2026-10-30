using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Double door standing in a gap of the steamer wall; the leaves swing away from the arena.</summary>
    public class ArenaDoor : MonoBehaviour
    {
        public Transform leftHinge;
        public Transform rightHinge;
        public float openAngle = 100f;
        public float swingSeconds = 0.9f;

        /// <summary>0 = closed, 1 = fully open.</summary>
        public float Openness { get; private set; }
        public bool IsOpen { get; private set; }

        public void SetOpen(bool open, bool instant = false)
        {
            if (!instant && open != IsOpen)
                AudioService.Instance?.Play("door");
            IsOpen = open;
            if (instant)
            {
                Openness = open ? 1f : 0f;
                Pose();
            }
        }

        void Update()
        {
            float wanted = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(Openness, wanted))
                return;
            Openness = Mathf.MoveTowards(Openness, wanted, Time.unscaledDeltaTime / Mathf.Max(0.01f, swingSeconds));
            Pose();
        }

        void Pose()
        {
            float angle = Mathf.SmoothStep(0f, openAngle, Openness);
            if (leftHinge != null)
                leftHinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
            if (rightHinge != null)
                rightHinge.localRotation = Quaternion.Euler(0f, 180f + angle, 0f);
        }
    }
}
