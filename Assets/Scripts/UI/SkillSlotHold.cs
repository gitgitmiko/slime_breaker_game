using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeBreaker
{
    /// <summary>Press and hold an owned skill slot to show that power-up's description; release to hide it.</summary>
    public class SkillSlotHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public UIController ui;
        public int index;
        public float holdSeconds = 0.45f;

        bool pressed;
        bool showing;
        float heldFor;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            heldFor = 0f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Release()
        {
            pressed = false;
            if (showing && ui != null)
                ui.HideSkillInfo();
            showing = false;
        }

        void Update()
        {
            if (!pressed || showing)
                return;
            heldFor += Time.unscaledDeltaTime;
            if (heldFor >= holdSeconds && ui != null)
                showing = ui.ShowSkillInfo(index);
        }
    }
}
