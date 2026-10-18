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
        /// <summary>Easy / Normal / Hard picker for the next new run (index = DifficultyLevel).</summary>
        public Dropdown difficultyDropdown;
        public Text difficultyHint;

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
            if (difficultyDropdown != null)
                difficultyDropdown.onValueChanged.AddListener(value => ChooseDifficulty((DifficultyLevel)value));
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

        /// <summary>Difficulty of the next new run (a saved run keeps the one it started on).</summary>
        public void ChooseDifficulty(DifficultyLevel level)
        {
            AudioService.Instance?.Play("ui");
            // Re-read the save first: the sound sliders write to it too, and a stale copy would undo them.
            save = SaveService.Load();
            save.difficulty = (int)level;
            SaveService.Save(save);
            Refresh();
        }

        void Buy(ref int rank, Text label, string title)
        {
            save = SaveService.Load();
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
            var current = (DifficultyLevel)Mathf.Clamp(save.difficulty, 0, 2);
            if (difficultyDropdown != null)
                difficultyDropdown.SetValueWithoutNotify((int)current);
            if (difficultyHint != null)
                difficultyHint.text = Difficulty.Summary(current);
            var run = RunSaveService.Exists() ? RunSaveService.Load() : null;
            bool canContinue = run != null && run.level > 0;
            if (continueButton != null)
                continueButton.gameObject.SetActive(canContinue);
            if (continueLabel != null && canContinue)
                continueLabel.text = "Lanjutkan (Lv " + run.level + " · Lantai " + Mathf.Max(1, run.floor > 0 ? run.floor : run.stage) + " · " + Difficulty.Label((DifficultyLevel)Mathf.Clamp(run.difficulty, 0, 2)) + ")";
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
