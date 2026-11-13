using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Pops a panel in when it is enabled: quick fade plus a scale with overshoot (unscaled time).</summary>
    public class UiPop : MonoBehaviour
    {
        public float duration = 0.22f;
        public float fromScale = 0.86f;
        /// <summary>What scales; the fade always covers this object's CanvasGroup (if it has one).</summary>
        public RectTransform target;

        /// <summary>Skips the animation (playtests capture the frame a panel opens).</summary>
        public static bool Instant;

        CanvasGroup group;
        float age;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (target == null)
                target = transform as RectTransform;
        }

        void OnEnable()
        {
            age = Instant ? duration : 0f;
            Apply(Instant ? 1f : 0f);
        }

        void OnDisable()
        {
            Apply(1f);
        }

        void Update()
        {
            if (age >= duration)
                return;
            age += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(age / duration));
        }

        void Apply(float t)
        {
            // Ease-out-back.
            const float overshoot = 1.9f;
            float p = t - 1f;
            float eased = p * p * ((overshoot + 1f) * p + overshoot) + 1f;
            if (target != null)
                target.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, eased);
            if (group != null)
                group.alpha = Mathf.Clamp01(t * 2.5f);
        }
    }
}
