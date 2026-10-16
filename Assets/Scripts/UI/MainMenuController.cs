using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlimeBreaker
{
    public class MainMenuController : MonoBehaviour
    {
        public Text goldText;
        public Text bestText;
        public Button damageButton;
        public Button healthButton;
        public Button speedButton;
        public Text damageLabel;
        public Text healthLabel;
        public Text speedLabel;
        public int baseCost = 30;
        public Transform showcase;
        public float showcaseSpin = 16f;
        /// <summary>The showcase sways ±this many degrees around its start facing instead of spinning, so the wave stays toward the camera.</summary>
        public float showcaseSway = 22f;
        public Button continueButton;
        public Text continueLabel;
        public SettingsPanel settings;

        SaveData save;
        Quaternion showcaseRest;
        bool hasRest;

        void Update()
        {
            if (showcase == null)
                return;
            if (!hasRest)
            {
                showcaseRest = showcase.rotation;
                hasRest = true;
            }
            float yaw = Mathf.Sin(Time.time * showcaseSpin * Mathf.Deg2Rad * 4f) * showcaseSway;
            showcase.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * showcaseRest;
        }

        void Start()
        {
            save = SaveService.Load();
            if (damageButton != null)
                damageButton.onClick.AddListener(() => Buy(ref save.damageRank, damageLabel, "Damage"));
            if (healthButton != null)
                healthButton.onClick.AddListener(() => Buy(ref save.healthRank, healthLabel, "Nyawa"));
            if (speedButton != null)
                speedButton.onClick.AddListener(() => Buy(ref save.speedRank, speedLabel, "Laju"));
            if (continueButton != null)
                continueButton.onClick.AddListener(Continue);
            Refresh();
        }

        public void Play()
        {
            AudioService.Instance?.Play("ui");
            RunSaveService.Clear();
            SceneManager.LoadScene("GameArena");
        }

        public void Continue()
        {
            if (!RunSaveService.Exists())
                return;
            AudioService.Instance?.Play("ui");
            RunSaveService.ResumeRequested = true;
            SceneManager.LoadScene("GameArena");
        }

        public void OpenSettings()
        {
            settings?.Show();
        }

        void Buy(ref int rank, Text label, string title)
        {
            int cost = baseCost * (rank + 1);
            if (save.gold < cost)
                return;
            AudioService.Instance?.Play("ui");
            save.gold -= cost;
            rank++;
            SaveService.Save(save);
            Refresh();
        }

        void Refresh()
        {
            if (goldText != null)
                goldText.text = "Emas " + save.gold;
            if (bestText != null)
            {
                int seconds = Mathf.FloorToInt(save.bestRunTime);
                bestText.text = "Rekor " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            }
            SetButton(damageLabel, damageButton, "Damage", save.damageRank);
            SetButton(healthLabel, healthButton, "Nyawa", save.healthRank);
            SetButton(speedLabel, speedButton, "Laju", save.speedRank);
            var run = RunSaveService.Exists() ? RunSaveService.Load() : null;
            bool canContinue = run != null && run.level > 0;
            if (continueButton != null)
                continueButton.gameObject.SetActive(canContinue);
            if (continueLabel != null && canContinue)
                continueLabel.text = "Lanjutkan (Lv " + run.level + " · Lantai " + Mathf.Max(1, run.floor > 0 ? run.floor : run.stage) + ")";
        }

        void SetButton(Text label, Button button, string title, int rank)
        {
            int cost = baseCost * (rank + 1);
            if (label != null)
                label.text = title + " +" + rank + "\n" + cost + " emas";
            if (button != null)
                button.interactable = save.gold >= cost;
        }
    }
}
