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

        SaveData save;

        void Update()
        {
            if (showcase != null)
                showcase.Rotate(0f, showcaseSpin * Time.deltaTime, 0f, Space.World);
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
            Refresh();
        }

        public void Play()
        {
            AudioService.Instance?.Play("ui");
            SceneManager.LoadScene("GameArena");
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
