using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>Low / Medium / High graphics buttons plus a close control. Used from the menu and the pause panel.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public GameObject panel;
        public Button lowButton;
        public Button mediumButton;
        public Button highButton;
        public Button closeButton;
        public Button joystickButton;
        public Text joystickLabel;
        public Text title;
        public Image[] highlights;

        void Start()
        {
            if (lowButton != null)
                lowButton.onClick.AddListener(() => Choose(GraphicsService.Low));
            if (mediumButton != null)
                mediumButton.onClick.AddListener(() => Choose(GraphicsService.Medium));
            if (highButton != null)
                highButton.onClick.AddListener(() => Choose(GraphicsService.High));
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (joystickButton != null)
                joystickButton.onClick.AddListener(ToggleJoystick);
            if (panel != null)
                panel.SetActive(false);
            Refresh();
        }

        public void Show()
        {
            AudioService.Instance?.Play("ui");
            if (panel != null)
                panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        public void Toggle()
        {
            if (panel != null && panel.activeSelf)
                Hide();
            else
                Show();
        }

        void Choose(int level)
        {
            AudioService.Instance?.Play("ui");
            GraphicsService.Instance?.Apply(level);
            Refresh();
        }

        public void ToggleJoystick()
        {
            AudioService.Instance?.Play("ui");
            ControlSettings.FreeJoystick = !ControlSettings.FreeJoystick;
            Refresh();
        }

        void Refresh()
        {
            bool free = ControlSettings.FreeJoystick;
            if (joystickLabel != null)
                joystickLabel.text = "Joystick: " + (free ? "Free" : "Fixed");
            Mark(joystickButton, free);
            int current = GraphicsService.Instance != null ? GraphicsService.Instance.Level : GraphicsService.High;
            if (title != null)
                title.text = "Grafis: " + GraphicsService.Label(current);
            Mark(lowButton, current == GraphicsService.Low);
            Mark(mediumButton, current == GraphicsService.Medium);
            Mark(highButton, current == GraphicsService.High);
        }

        static void Mark(Button button, bool selected)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? new Color(1f, 0.92f, 0.55f) : Color.white;
        }
    }
}
