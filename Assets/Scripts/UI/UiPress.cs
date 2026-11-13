using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>Button feel: squashes while held and springs back with a small overshoot (unscaled time, so it works while paused).</summary>
    public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.93f;
        public float speed = 18f;

        Selectable selectable;
        float target = 1f;
        float current = 1f;
        float velocity;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        void OnDisable()
        {
            target = current = 1f;
            velocity = 0f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable())
                target = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            target = 1f;
        }

        void Update()
        {
            if (Mathf.Approximately(current, target) && Mathf.Abs(velocity) < 0.001f)
                return;
            // Critically under-damped spring: a little bounce on release.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            velocity += (target - current) * speed * speed * dt;
            velocity *= Mathf.Exp(-speed * 1.1f * dt);
            current += velocity * dt;
            if (Mathf.Abs(current - target) < 0.0005f && Mathf.Abs(velocity) < 0.01f)
            {
                current = target;
                velocity = 0f;
            }
            transform.localScale = Vector3.one * current;
        }
    }
}
