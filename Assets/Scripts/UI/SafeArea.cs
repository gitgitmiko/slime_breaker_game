using UnityEngine;

namespace SlimeBreaker
{
    public class SafeArea : MonoBehaviour
    {
        Rect last;

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            if (last != Screen.safeArea)
                Apply();
        }

        void Apply()
        {
            last = Screen.safeArea;
            var rect = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0)
                return;
            var min = last.position;
            var max = last.position + last.size;
            rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
