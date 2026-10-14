using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SlimeBreaker
{
    public class UIController : MonoBehaviour
    {
        public Slider hpSlider;
        public Slider xpSlider;
        public Text levelText;
        public Text timerText;
        public Image dashFill;
        public GameObject levelUpPanel;
        public Button[] choiceButtons;
        public Text[] choiceLabels;
        public Image[] choiceIcons;
        public GameObject pausePanel;
        public GameObject resultPanel;
        public Text resultTitle;
        public Text resultBody;
        public Button pauseButton;

        PlayerHealth health;
        DashController dash;

        void Start()
        {
            health = PlayerHealth.Instance;
            dash = health != null ? health.GetComponent<DashController>() : null;
            if (health != null)
                health.Changed += RefreshHealth;
            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    int index = i;
                    choiceButtons[i].onClick.AddListener(() => GameFlowController.Instance.ChooseUpgrade(index));
                }
            }
            RefreshHealth();
            if (levelUpPanel != null)
                levelUpPanel.SetActive(false);
            if (pausePanel != null)
                pausePanel.SetActive(false);
            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (health != null)
                health.Changed -= RefreshHealth;
        }

        void Update()
        {
            var flow = GameFlowController.Instance;
            if (flow == null)
                return;
            if (levelText != null)
                levelText.text = "Lv " + flow.Stats.level;
            if (timerText != null && flow.run != null)
            {
                int seconds = Mathf.FloorToInt(flow.run.Elapsed);
                timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            }
            var xp = XPManager.Instance;
            if (xpSlider != null && xp != null)
                xpSlider.value = xp.XpToNext <= 0 ? 0f : (float)xp.Xp / xp.XpToNext;
            if (dashFill != null && dash != null)
                dashFill.fillAmount = dash.Cooldown01;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                flow.TogglePause();
        }

        public void ShowLevelUp(UpgradeDefinition[] choices)
        {
            if (levelUpPanel != null)
                levelUpPanel.SetActive(true);
            if (choiceButtons == null)
                return;
            int count = choices != null ? choices.Length : 0;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool visible = i < count && choices[i] != null;
                choiceButtons[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                choiceLabels[i].text = choices[i].title + "\n" + choices[i].description;
                if (choiceIcons != null && i < choiceIcons.Length && choiceIcons[i] != null)
                    choiceIcons[i].sprite = choices[i].icon;
            }
        }

        public void HideLevelUp()
        {
            if (levelUpPanel != null)
                levelUpPanel.SetActive(false);
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null)
                pausePanel.SetActive(paused);
        }

        public void ShowResult(bool victory, int gold, float elapsed, int totalGold)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);
            if (resultTitle != null)
                resultTitle.text = victory ? "Menang" : "Kalah";
            int seconds = Mathf.FloorToInt(elapsed);
            if (resultBody != null)
                resultBody.text = "Waktu " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00")
                    + "\nEmas +" + gold + "\nTotal " + totalGold;
        }

        void RefreshHealth()
        {
            if (hpSlider == null || health == null || health.Max <= 0)
                return;
            hpSlider.value = (float)health.Current / health.Max;
        }
    }
}
