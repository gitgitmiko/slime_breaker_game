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
        [Header("Status card")]
        /// <summary>Value cells of the status card, in <see cref="StatKeys"/> order.</summary>
        public Text[] statValues;
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
        public Image skillInfoAccent;
        public Image skillInfoIcon;
        public Text skillInfoTitle;
        public Image skillInfoLevelPill;
        public Text skillInfoLevel;
        public Text skillInfoDescription;
        public Text[] skillInfoChips;
        public Text skillInfoBonus;
        public Image skillInfoBossDot;
        public Text skillInfoBoss;

        [Header("Boss HP")]
        public GameObject bossBar;
        public Text bossBarName;
        public Image bossBarTag;
        public Text bossBarTagText;
        public Image bossBarFill;
        public Image bossBarTrail;
        public Text bossBarValue;

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
            RefreshBossBar();

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

        EnemyHealth trackedBoss;
        float trail01 = 1f;
        float trailHold;
        int lastBossHp = -1;

        public bool BossBarVisible => bossBar != null && bossBar.activeSelf;
        public float BossBarFill01 => bossBarFill != null ? bossBarFill.fillAmount : 0f;
        public string BossBarText => bossBarValue != null ? bossBarValue.text : string.Empty;
        public string BossBarName => bossBarName != null ? bossBarName.text : string.Empty;

        static EnemyHealth FindBoss()
        {
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy != null && !enemy.IsDead && enemy.IsBoss)
                    return enemy;
            }
            return null;
        }

        /// <summary>Top-centre boss HP bar: name + BOSS tag, red fill, a white trail that catches up, and "7.250 / 10.000".</summary>
        void RefreshBossBar()
        {
            if (bossBar == null)
                return;
            if (trackedBoss == null || trackedBoss.IsDead)
            {
                trackedBoss = FindBoss();
                lastBossHp = -1;
                trail01 = 1f;
            }
            bool on = trackedBoss != null && trackedBoss.Max > 0;
            if (bossBar.activeSelf != on)
                bossBar.SetActive(on);
            if (!on)
                return;
            int hp = Mathf.Max(0, trackedBoss.Current);
            int max = trackedBoss.Max;
            float value01 = Mathf.Clamp01((float)hp / max);
            if (bossBarFill != null)
                bossBarFill.fillAmount = value01;
            if (hp != lastBossHp)
            {
                if (lastBossHp >= 0 && hp < lastBossHp)
                    trailHold = 0.35f;
                if (bossBarValue != null)
                    bossBarValue.text = Thousands(hp) + " / " + Thousands(max);
                lastBossHp = hp;
            }
            float dt = Time.unscaledDeltaTime;
            if (trailHold > 0f)
                trailHold -= dt;
            else
                trail01 = Mathf.MoveTowards(trail01, value01, dt * 1.5f);
            trail01 = Mathf.Max(trail01, value01);
            if (bossBarTrail != null)
                bossBarTrail.fillAmount = trail01;

            var controller = trackedBoss.GetComponent<BossController>();
            bool enraged = controller != null && controller.Enraged;
            if (bossBarName != null)
                bossBarName.text = controller != null ? BossCatalog.DisplayName(controller.kind).ToUpperInvariant() : "BOSS";
            if (bossBarTagText != null)
                bossBarTagText.text = enraged ? "ENRAGE" : "BOSS";
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            if (bossBarTag != null)
                bossBarTag.color = enraged ? Color.Lerp(new Color(0.85f, 0.1f, 0.08f), new Color(1f, 0.45f, 0.2f), pulse) : new Color(0.78f, 0.14f, 0.18f);
            if (bossBarFill != null)
                bossBarFill.color = enraged
                    ? Color.Lerp(new Color(0.95f, 0.18f, 0.1f), new Color(1f, 0.5f, 0.18f), pulse)
                    : new Color(0.9f, 0.2f, 0.26f);
        }

        /// <summary>10000 → "10.000" (Indonesian grouping, culture-independent).</summary>
        public static string Thousands(int value)
        {
            string digits = Mathf.Abs(value).ToString();
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    builder.Append('.');
                builder.Append(digits[i]);
            }
            return (value < 0 ? "-" : string.Empty) + builder;
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

        /// <summary>All visible text of the skill card, for tests and accessibility.</summary>
        public string SkillInfo
        {
            get
            {
                var parts = new System.Text.StringBuilder();
                Append(parts, skillInfoTitle);
                Append(parts, skillInfoLevel);
                Append(parts, skillInfoDescription);
                if (skillInfoChips != null)
                {
                    foreach (var chip in skillInfoChips)
                        Append(parts, chip);
                }
                Append(parts, skillInfoBonus);
                Append(parts, skillInfoBoss);
                return parts.ToString();
            }
        }

        static void Append(System.Text.StringBuilder parts, Text text)
        {
            if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text))
                parts.Append(text.text).Append('\n');
        }

        /// <summary>Shows the description card of owned skill <paramref name="index"/> (long press on its HUD slot).</summary>
        public bool ShowSkillInfo(int index)
        {
            if (skills == null || index < 0 || index >= skills.Count || skillInfoPanel == null)
                return false;
            var skill = skills.Skills[index];
            var d = skill.definition;
            if (d == null)
                return false;
            int level = skill.level;
            bool max = d.IsMax(level);
            Color accent = Color.Lerp(d.color, Color.white, 0.2f);
            if (skillInfoAccent != null)
                skillInfoAccent.color = accent;
            if (skillInfoIcon != null)
            {
                skillInfoIcon.sprite = d.icon;
                skillInfoIcon.enabled = d.icon != null;
            }
            if (skillInfoTitle != null)
            {
                skillInfoTitle.text = d.title;
                skillInfoTitle.color = Color.Lerp(d.color, Color.white, 0.45f);
            }
            if (skillInfoLevel != null)
            {
                skillInfoLevel.text = max ? "MAX" : "Lv " + level + "/" + d.maxLevel;
                skillInfoLevel.color = max ? new Color(0.16f, 0.1f, 0.02f) : Color.white;
            }
            if (skillInfoLevelPill != null)
                skillInfoLevelPill.color = max ? MaxLevelColor : new Color(d.color.r * 0.55f, d.color.g * 0.55f, d.color.b * 0.55f, 0.95f);
            if (skillInfoDescription != null)
                skillInfoDescription.text = d.description;

            var chips = new System.Collections.Generic.List<string> { "DMG  " + d.DamageAt(level) };
            switch (d.kind)
            {
                case SkillKind.IceOrbit:
                    chips.Add("SHARD  " + d.CountAt(level));
                    chips.Add("RADIUS  " + d.RadiusAt(level).ToString("0.0") + " m");
                    break;
                case SkillKind.LightningChain:
                    chips.Add("JUMP  " + d.CountAt(level));
                    chips.Add("CD  " + d.CooldownAt(level).ToString("0.0") + " s");
                    break;
                case SkillKind.FireNova:
                    chips.Add("RADIUS  " + d.RadiusAt(level).ToString("0.0") + " m");
                    chips.Add("CD  " + d.CooldownAt(level).ToString("0.0") + " s");
                    break;
                case SkillKind.ArcaneMissile:
                    chips.Add("MISSILE  " + d.CountAt(level));
                    chips.Add("CD  " + d.CooldownAt(level).ToString("0.0") + " s");
                    break;
            }
            if (skillInfoChips != null)
            {
                for (int i = 0; i < skillInfoChips.Length; i++)
                {
                    if (skillInfoChips[i] == null)
                        continue;
                    bool on = i < chips.Count;
                    skillInfoChips[i].transform.parent.gameObject.SetActive(on);
                    if (on)
                        skillInfoChips[i].text = chips[i];
                }
            }
            if (skillInfoBonus != null)
            {
                string bonus = d.kind switch
                {
                    SkillKind.IceOrbit => "slow hampir membeku",
                    SkillKind.LightningChain => "lompatan tanpa penurunan damage",
                    SkillKind.FireNova => "bakar 2× lebih kuat",
                    _ => "+2 misil"
                };
                skillInfoBonus.text = (max ? "★ Bonus MAX aktif: " : "☆ Bonus MAX: ") + bonus;
                skillInfoBonus.color = max ? MaxLevelColor : new Color(0.72f, 0.74f, 0.82f);
            }
            bool breaker = SkillController.BossBreakerActive;
            if (skillInfoBoss != null)
            {
                skillInfoBoss.text = breaker ? "Boss Breaker aktif — bisa melukai boss" : "Tidak melukai boss tanpa Boss Breaker";
                skillInfoBoss.color = breaker ? SkillController.BreakerColor : new Color(0.66f, 0.68f, 0.76f);
            }
            if (skillInfoBossDot != null)
                skillInfoBossDot.color = breaker ? SkillController.BreakerColor : new Color(0.5f, 0.52f, 0.6f);
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
                    floorProgress.value = 1f;
                else
                    floorProgress.value = quota <= 0 ? 0f : Mathf.Clamp01((float)kills / quota);
            }
        }

        /// <summary>Stat cells of the status card, left-to-right, top-to-bottom.</summary>
        public static readonly string[] StatKeys = { "DMG", "ATK SPD", "CRIT", "CRIT RATE", "RANGE", "MOVE", "LS", "EXP", "DEF", "TAKEN" };

        static readonly Color StatDim = new Color(0.62f, 0.64f, 0.72f);
        static readonly Color StatGold = new Color(1f, 0.82f, 0.3f);
        static readonly Color StatGreen = new Color(0.45f, 1f, 0.55f);
        static readonly Color StatBlue = new Color(0.5f, 0.78f, 1f);

        readonly string[] statText = new string[10];

        /// <summary>"DMG 12  ATK SPD 1.82/s  …" — every stat with its current value.</summary>
        public string StatusSummary
        {
            get
            {
                var builder = new System.Text.StringBuilder();
                for (int i = 0; i < StatKeys.Length; i++)
                    builder.Append(StatKeys[i]).Append(' ').Append(statText[i]).Append("  ");
                return builder.ToString();
            }
        }

        public string StatValue(string key)
        {
            int index = System.Array.IndexOf(StatKeys, key);
            return index >= 0 ? statText[index] : null;
        }

        void RefreshStatus(RunStats stats)
        {
            if (stats == null)
                return;
            if (damageText != null)
                damageText.text = "ATK " + stats.damage;
            int expBonus = Mathf.RoundToInt(Mathf.Max(0f, stats.xpMultiplier - 1f) * 100f);
            int defense = Mathf.RoundToInt(stats.damageReduction * 100f);
            int steal = Mathf.RoundToInt(stats.lifesteal * 100f);
            SetStat(0, stats.damage.ToString(), Color.white);
            SetStat(1, stats.AttacksPerSecond.ToString("0.00") + "/s", Color.white);
            SetStat(2, stats.HasCrit ? stats.critMultiplier.ToString("0.0") + "x" : "—", stats.HasCrit ? StatGold : StatDim);
            SetStat(3, (stats.HasCrit ? Mathf.RoundToInt(stats.critChance * 100f) : 0) + "%", stats.HasCrit ? StatGold : StatDim);
            SetStat(4, stats.attackRange.ToString("0.0") + " m", Color.white);
            SetStat(5, stats.moveSpeed.ToString("0.0"), Color.white);
            SetStat(6, steal + "%", steal > 0 ? StatGreen : StatDim);
            SetStat(7, "+" + expBonus + "%", expBonus > 0 ? Color.white : StatDim);
            SetStat(8, defense + "%", defense > 0 ? StatBlue : StatDim);
            SetStat(9, (100 - defense) + "%", defense > 0 ? StatBlue : StatDim);
        }

        void SetStat(int index, string value, Color color)
        {
            statText[index] = value;
            if (statValues == null || index >= statValues.Length || statValues[index] == null)
                return;
            if (statValues[index].text != value)
                statValues[index].text = value;
            statValues[index].color = color;
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
