using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Full-screen fade used when walking through an exit portal into the next arena.</summary>
    public class ScreenFader : MonoBehaviour
    {
        public CanvasGroup group;
        public float defaultDuration = 0.4f;

        void Awake()
        {
            if (group == null)
                group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        public IEnumerator FadeOut(float duration = -1f)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator FadeIn(float duration = -1f)
        {
            yield return FadeTo(0f, duration);
        }

        IEnumerator FadeTo(float target, float duration)
        {
            if (group == null)
                yield break;
            if (duration < 0f)
                duration = defaultDuration;
            duration = Mathf.Max(0.05f, duration);
            float start = group.alpha;
            float t = 0f;
            group.blocksRaycasts = target > 0.5f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = target;
            group.blocksRaycasts = target > 0.5f;
        }
    }
}
