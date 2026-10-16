using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SlimeBreaker
{
    public class UIController : MonoBehaviour
    {
        public Slider hpSlider;
        public Slider xpSlider;
        public Text hpText;
        public Text levelText;
        public Text floorText;
        public Slider floorProgress;
        public Text floorProgressText;
        public Text damageText;
        public Text statusText;
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
        public SettingsPanel settings;
        public ScreenFader fader;

        [Header("Skills")]
        public GameObject[] skillSlots;
        public Image[] skillIcons;
        public Text[] skillLevels;
        public Image[] skillFrames;

        [Header("Skill info (long press)")]
        public GameObject skillInfoPanel;
        public Text skillInfoText;

        [Header("Boss Breaker")]
        public Text breakerText;

        [Header("Toast")]
        public CanvasGroup toastGroup;
        public Text toastText;
        public Image toastBackground;
        public float toastDuration = 2.4f;

        [Header("Boss")]
        public CanvasGroup bossWarningGroup;
        public Text bossWarningTitle;
        public Text bossWarningName;
        public float bossWarningDuration = 2.6f;

        static readonly Color MaxLevelColor = new Color(1f, 0.82f, 0.25f);

        PlayerHealth health;
        DashController dash;
        SkillController skills;
        float toastTimer;
        float bossWarningTimer;

        void Start()
        {
            health = PlayerHealth.Instance;
            dash = health != null ? health.GetComponent<DashController>() : null;
            skills = health != null ? health.GetComponent<SkillController>() : SkillController.Instance;
            if (health != null)
                health.Changed += RefreshHealth;
            if (skills != null)
                skills.Changed += RefreshSkills;
            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    int index = i;
                    choiceButtons[i].onClick.AddListener(() => GameFlowController.Instance.ChooseUpgrade(index));
                }
            }
            RefreshHealth();
            RefreshSkills();
            if (GameFlowController.Instance != null)
                RefreshStatus(GameFlowController.Instance.Stats);
            if (levelUpPanel != null)
                levelUpPanel.SetActive(false);
            if (pausePanel != null)
                pausePanel.SetActive(false);
            if (resultPanel != null)
                resultPanel.SetActive(false);
            if (toastGroup != null)
                toastGroup.alpha = 0f;
            if (bossWarningGroup != null)
                bossWarningGroup.alpha = 0f;
            HideSkillInfo();
            if (breakerText != null)
                breakerText.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (health != null)
                health.Changed -= RefreshHealth;
            if (skills != null)
                skills.Changed -= RefreshSkills;
        }

        void Update()
        {
            var flow = GameFlowController.Instance;
            if (flow == null)
                return;
            if (levelText != null)
                levelText.text = "Lv " + flow.Stats.level;
            RefreshStatus(flow.Stats);
            if (floorText != null)
            {
                int floor = StageManager.Instance != null ? StageManager.Instance.Floor : 1;
                floorText.text = "Lantai " + floor + "/" + RunRules.MaxFloor;
            }
            RefreshFloorProgress();
            if (timerText != null && flow.run != null)
            {
                int seconds = Mathf.FloorToInt(flow.run.Elapsed);
                timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            }
            var xp = XPManager.Instance;
            if (xpSlider != null && xp != null)
                xpSlider.value = xp.XpToNext <= 0 ? 0f : (float)xp.Xp / xp.XpToNext;
            if (dashFill != null && dash != null)
            {
                dashFill.fillAmount = dash.Cooldown01;
                var dashButton = dashFill.GetComponentInParent<Button>();
                if (dashButton != null)
                    dashButton.interactable = dash.CanDash;
            }

            RefreshBreaker();

            if (toastGroup != null && toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                float fadeIn = Mathf.Clamp01((toastDuration - toastTimer) / 0.18f);
                float fadeOut = Mathf.Clamp01(toastTimer / 0.45f);
                toastGroup.alpha = Mathf.Min(fadeIn, fadeOut);
                var rect = toastGroup.transform as RectTransform;
                if (rect != null)
                    rect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, fadeIn);
            }
            if (bossWarningGroup != null && bossWarningTimer > 0f)
            {
                bossWarningTimer -= Time.unscaledDeltaTime;
                float fadeIn = Mathf.Clamp01((bossWarningDuration - bossWarningTimer) / 0.22f);
                float fadeOut = Mathf.Clamp01(bossWarningTimer / 0.5f);
                bossWarningGroup.alpha = Mathf.Min(fadeIn, fadeOut);
                var rect = bossWarningGroup.transform as RectTransform;
                if (rect != null)
                    rect.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, fadeIn);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                flow.TogglePause();
        }

        void RefreshBreaker()
        {
            if (breakerText == null)
                return;
            float left = skills != null ? skills.BreakerLeft : 0f;
            bool on = left > 0f;
            if (breakerText.gameObject.activeSelf != on)
                breakerText.gameObject.SetActive(on);
            if (on)
            {
                breakerText.text = "BOSS BREAKER " + Mathf.CeilToInt(left) + "s";
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                breakerText.color = Color.Lerp(SkillController.BreakerColor, Color.white, pulse * 0.4f);
            }
        }

        public bool SkillInfoVisible => skillInfoPanel != null && skillInfoPanel.activeSelf;
        public string SkillInfo => skillInfoText != null ? skillInfoText.text : string.Empty;

        /// <summary>Shows the description of owned skill <paramref name="index"/> (long press on its HUD slot).</summary>
        public bool ShowSkillInfo(int index)
        {
            if (skills == null || index < 0 || index >= skills.Count || skillInfoPanel == null)
                return false;
            var skill = skills.Skills[index];
            var d = skill.definition;
            if (d == null)
                return false;
            int level = skill.level;
            string levelLabel = d.IsMax(level) ? "MAX" : "Lv " + level + " / " + d.maxLevel;
            string stats = "Damage " + d.DamageAt(level);
            switch (d.kind)
            {
                case SkillKind.IceOrbit:
                    stats += "   Pecahan " + d.CountAt(level) + "   Radius " + d.RadiusAt(level).ToString("0.0") + " m";
                    break;
                case SkillKind.LightningChain:
                    stats += "   Lompatan " + d.CountAt(level) + "   CD " + d.CooldownAt(level).ToString("0.0") + " s";
                    break;
                case SkillKind.FireNova:
                    stats += "   Radius " + d.RadiusAt(level).ToString("0.0") + " m   CD " + d.CooldownAt(level).ToString("0.0") + " s";
                    break;
                case SkillKind.ArcaneMissile:
                    stats += "   Misil " + d.CountAt(level) + "   CD " + d.CooldownAt(level).ToString("0.0") + " s";
                    break;
            }
            string bonus = d.kind switch
            {
                SkillKind.IceOrbit => "Bonus MAX: slow hampir membeku.",
                SkillKind.LightningChain => "Bonus MAX: lompatan tanpa penurunan damage.",
                SkillKind.FireNova => "Bonus MAX: bakar 2× lebih kuat.",
                _ => "Bonus MAX: +2 misil."
            };
            string boss = SkillController.BossBreakerActive
                ? "<color=#FFB13A>Boss Breaker aktif: bisa melukai boss.</color>"
                : "<color=#B8BCC8>Tidak melukai boss tanpa Boss Breaker.</color>";
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(d.color, Color.white, 0.3f));
            skillInfoText.text = "<b><color=#" + hex + ">" + d.title + "</color></b>   " + levelLabel + "\n"
                + d.description + "\n" + stats + "\n" + bonus + "\n" + boss;
            skillInfoPanel.SetActive(true);
            return true;
        }

        public void HideSkillInfo()
        {
            if (skillInfoPanel != null)
                skillInfoPanel.SetActive(false);
        }

        public bool BossWarningVisible => bossWarningGroup != null && bossWarningGroup.alpha > 0.15f;
        public string BossWarningName => bossWarningName != null ? bossWarningName.text : string.Empty;

        public void ShowBossWarning(string name, Color color, int floor = 0)
        {
            if (bossWarningTitle != null)
                bossWarningTitle.text = floor > 0 ? "BOSS LANTAI " + floor : "STAGE BOSS";
            if (bossWarningName != null)
            {
                bossWarningName.text = name;
                bossWarningName.color = Color.Lerp(color, Color.white, 0.35f);
            }
            bossWarningTimer = bossWarningDuration;
            if (bossWarningGroup != null)
            {
                bossWarningGroup.alpha = 0f;
                bossWarningGroup.gameObject.SetActive(true);
            }
        }

        /// <summary>Reuses the boss banner for the enrage cutscene.</summary>
        public void ShowEnrageWarning(string name, Color color)
        {
            ShowBossWarning(name, color);
            if (bossWarningTitle != null)
                bossWarningTitle.text = "ENRAGE!";
        }

        public string BossWarningTitle => bossWarningTitle != null ? bossWarningTitle.text : string.Empty;

        public void ShowToast(string message, Color color)
        {
            if (toastText != null)
                toastText.text = message;
            if (toastBackground != null)
                toastBackground.color = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.82f);
            if (toastText != null)
                toastText.color = Color.Lerp(color, Color.white, 0.55f);
            toastTimer = toastDuration;
            if (toastGroup != null)
                toastGroup.alpha = 0f;
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
                choiceLabels[i].text = choices[i].title + "\n<size=22>" + choices[i].description + "</size>";
                if (choiceIcons != null && i < choiceIcons.Length && choiceIcons[i] != null)
                {
                    choiceIcons[i].sprite = choices[i].icon;
                    choiceIcons[i].enabled = choices[i].icon != null;
                }
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
            if (!paused)
                settings?.Hide();
        }

        public void OpenSettings()
        {
            settings?.Show();
        }

        public void ShowResult(bool victory, int gold, float elapsed, int totalGold)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);
            int level = GameFlowController.Instance != null && GameFlowController.Instance.Stats != null
                ? GameFlowController.Instance.Stats.level
                : 1;
            int damage = GameFlowController.Instance != null && GameFlowController.Instance.Stats != null
                ? GameFlowController.Instance.Stats.damage
                : 0;
            int floor = StageManager.Instance != null ? StageManager.Instance.Floor : 1;
            if (resultTitle != null)
            {
                resultTitle.text = victory
                    ? (floor >= RunRules.MaxFloor ? "Tamat!" : "Menang!")
                    : "Kalah";
            }
            int seconds = Mathf.FloorToInt(elapsed);
            if (resultBody != null)
                resultBody.text = "Lantai " + floor + "/" + RunRules.MaxFloor
                    + "\nLv " + level + "   ATK " + damage
                    + "\nWaktu " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00")
                    + "\nEmas +" + gold + "\nTotal " + totalGold;
        }

        void RefreshFloorProgress()
        {
            var stage = StageManager.Instance;
            int floor = stage != null ? stage.Floor : 1;
            int kills = stage != null ? stage.FloorKills : 0;
            int quota = RunRules.KillQuota(floor);
            bool boss = RunRules.IsBossFloor(floor);
            if (floorProgressText != null)
            {
                if (boss)
                    floorProgressText.text = stage != null && stage.FloorCleared ? "Masuk pintu" : "BOSS";
                else if (stage != null && stage.FloorCleared)
                    floorProgressText.text = kills + " / " + quota + "  pintu terbuka";
                else
                    floorProgressText.text = kills + " / " + quota;
            }
            if (floorProgress != null)
            {
                if (boss)
                    floorProgress.value = stage != null && stage.FloorCleared ? 1f : BossHealth01();
                else
                    floorProgress.value = quota <= 0 ? 0f : Mathf.Clamp01((float)kills / quota);
            }
        }

        static float BossHealth01()
        {
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy != null && !enemy.IsDead && enemy.IsBoss && enemy.Max > 0)
                    return 1f - (float)enemy.Current / enemy.Max;
            }
            return 0f;
        }

        void RefreshStatus(RunStats stats)
        {
            if (stats == null)
                return;
            if (damageText != null)
                damageText.text = "ATK " + stats.damage;
            if (statusText == null)
                return;
            string crit = stats.HasCrit ? stats.critMultiplier.ToString("0.0") + "x" : "—";
            string rate = stats.HasCrit ? Mathf.RoundToInt(stats.critChance * 100f) + "%" : "0%";
            string steal = Mathf.RoundToInt(stats.lifesteal * 100f) + "%";
            int expBonus = Mathf.RoundToInt(Mathf.Max(0f, stats.xpMultiplier - 1f) * 100f);
            int defense = Mathf.RoundToInt(stats.damageReduction * 100f);
            statusText.text =
                "DMG " + stats.damage + "     CRIT " + crit + "\n"
                + "CRIT RATE " + rate + "     ATK SPD " + stats.AttacksPerSecond.ToString("0.00") + "\n"
                + "RANGE " + stats.attackRange.ToString("0.0") + "     MOVE " + stats.moveSpeed.ToString("0.0") + "\n"
                + "EXP +" + expBonus + "%     LS " + steal + "\n"
                + "DEF " + defense + "%     TAKEN " + (100 - defense) + "%";
        }

        void RefreshHealth()
        {
            if (health == null || health.Max <= 0)
                return;
            if (hpSlider != null)
                hpSlider.value = (float)health.Current / health.Max;
            if (hpText != null)
                hpText.text = health.Current + " / " + health.Max;
        }

        void RefreshSkills()
        {
            if (skillSlots == null)
                return;
            int count = skills != null ? skills.Count : 0;
            for (int i = 0; i < skillSlots.Length; i++)
            {
                bool visible = i < count;
                if (skillSlots[i] != null)
                    skillSlots[i].SetActive(visible);
                if (!visible)
                    continue;
                var skill = skills.Skills[i];
                var definition = skill.definition;
                if (skillIcons != null && i < skillIcons.Length && skillIcons[i] != null)
                {
                    skillIcons[i].sprite = definition != null ? definition.icon : null;
                    skillIcons[i].enabled = definition != null && definition.icon != null;
                }
                if (skillLevels != null && i < skillLevels.Length && skillLevels[i] != null)
                {
                    bool max = definition != null && definition.IsMax(skill.level);
                    skillLevels[i].text = max ? "MAX" : "Lv " + skill.level;
                    skillLevels[i].color = max ? MaxLevelColor : Color.white;
                    skillLevels[i].fontStyle = max ? FontStyle.Bold : FontStyle.Normal;
                }
                if (skillFrames != null && i < skillFrames.Length && skillFrames[i] != null && definition != null)
                    skillFrames[i].color = Color.Lerp(definition.color, Color.white, 0.25f);
            }
        }
    }
}
