using UnityEngine;
using UnityEngine.EventSystems;
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
        public Button cameraButton;
        public Text cameraLabel;
        public Button fpsButton;
        public Text fpsLabel;
        public Text title;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Text musicValue;
        public Text sfxValue;
        public Image[] highlights;
        /// <summary>Face for the active option (solid accent); others use <see cref="normalSprite"/> (glass).</summary>
        public Sprite selectedSprite;
        public Sprite normalSprite;

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
            if (cameraButton != null)
                cameraButton.onClick.AddListener(ToggleCamera);
            if (fpsButton != null)
                fpsButton.onClick.AddListener(ToggleFps);
            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(value => AudioService.Instance?.SetMusicLevel(value));
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(value => AudioService.Instance?.SetSfxLevel(value));
                // Let go of the game-sound slider and you hear the new level (a short blip), like any volume control.
                var release = sfxSlider.gameObject.AddComponent<EventTrigger>();
                var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                up.callback.AddListener(_ => AudioService.Instance?.Play("ui"));
                release.triggers.Add(up);
            }
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

        public void ToggleCamera()
        {
            AudioService.Instance?.Play("ui");
            ControlSettings.InvertCamera = !ControlSettings.InvertCamera;
            Refresh();
        }

        public void ToggleFps()
        {
            AudioService.Instance?.Play("ui");
            var graphics = GraphicsService.Instance;
            graphics?.SetFps(graphics.Fps == 60 ? 30 : 60);
            Refresh();
        }

        void Refresh()
        {
            int fps = GraphicsService.CurrentFps;
            if (fpsLabel != null)
                fpsLabel.text = "FPS: " + fps;
            Mark(fpsButton, fps == 60);
            bool inverted = ControlSettings.InvertCamera;
            if (cameraLabel != null)
                cameraLabel.text = "Kamera: " + (inverted ? "Inverted" : "Normal");
            Mark(cameraButton, inverted);
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
            var audio = AudioService.Instance;
            if (audio != null)
            {
                if (musicSlider != null)
                    musicSlider.SetValueWithoutNotify(audio.MusicLevel);
                if (sfxSlider != null)
                    sfxSlider.SetValueWithoutNotify(audio.SfxLevel);
                ShowPercent(musicValue, audio.MusicLevel);
                ShowPercent(sfxValue, audio.SfxLevel);
            }
        }

        public void ShowPercent(Text text, float level)
        {
            if (text != null)
                text.text = Mathf.RoundToInt(level * 100f) + "%";
        }

        void Update()
        {
            // Keep the percentage labels in step while a slider is being dragged.
            if (panel == null || !panel.activeSelf)
                return;
            if (musicSlider != null)
                ShowPercent(musicValue, musicSlider.value);
            if (sfxSlider != null)
                ShowPercent(sfxValue, sfxSlider.value);
        }

        void Mark(Button button, bool selected)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image == null)
                return;
            if (selectedSprite != null && normalSprite != null)
            {
                image.sprite = selected ? selectedSprite : normalSprite;
                image.color = Color.white;
            }
            else
            {
                image.color = selected ? new Color(1f, 0.92f, 0.55f) : Color.white;
            }
        }
    }
}
