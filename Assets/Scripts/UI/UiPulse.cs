using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Idle motion for call-to-action art: a slow breathing scale and/or a constant spin (unscaled time).</summary>
    public class UiPulse : MonoBehaviour
    {
        public float scaleAmount = 0.04f;
        public float scaleSpeed = 3.2f;
        public float spin;
        public float bob;

        Vector2 rest;
        RectTransform rect;

        void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null)
                rest = rect.anchoredPosition;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (scaleAmount != 0f)
                transform.localScale = Vector3.one * (1f + scaleAmount * Mathf.Sin(t * scaleSpeed));
            if (spin != 0f)
                transform.localRotation = Quaternion.Euler(0f, 0f, t * spin);
            if (bob != 0f && rect != null)
                rect.anchoredPosition = rest + new Vector2(0f, Mathf.Sin(t * scaleSpeed) * bob);
        }
    }
}
