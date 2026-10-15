using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBreaker
{
    public class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }
        public static bool IsPlaying => Instance != null && Instance.State == GameState.Playing;

        public GameState State { get; private set; } = GameState.Playing;
        public BalanceConfig balance;
        public RunStats Stats { get; private set; }
        public UIController ui;
        public UpgradeManager upgrades;
        public RunManager run;
        public AudioService audioService;
        public PowerUpSpawner powerUps;

        int queuedLevelUps;
        UpgradeDefinition[] currentChoices;

        void Awake()
        {
            Instance = this;
            var save = SaveService.Load();
            var resume = RunSaveService.PeekResume();
            if (resume != null && resume.stats != null)
            {
                Stats = resume.stats;
                Stats.level = Mathf.Max(1, resume.level);
            }
            else
            {
                Stats = new RunStats();
                if (balance != null)
                {
                    Stats.damage = balance.baseDamage + save.damageRank * 2;
                    Stats.moveSpeed = balance.baseMoveSpeed * (1f + 0.05f * save.speedRank);
                    Stats.maxHealth = balance.baseHealth + save.healthRank * 15;
                    Stats.attackCooldown = balance.baseAttackCooldown;
                    Stats.attackRange = balance.baseAttackRange;
                    Stats.dashCooldown = balance.baseDashCooldown;
                }
            }
            Time.timeScale = 1f;
            State = GameState.Playing;
        }

        IEnumerator Start()
        {
            var resume = RunSaveService.PeekResume();
            if (resume != null)
                ApplyResume(resume);
            yield return null;
            RunSaveService.FinishResume();
            PersistRun();
        }

        void ApplyResume(RunSave data)
        {
            if (data.stats != null)
            {
                Stats = data.stats;
                Stats.level = Mathf.Max(1, data.level);
            }
            PlayerHealth.Instance?.Restore(data.hp);
            XPManager.Instance?.Restore(data.xp);
            run?.Restore(data.elapsed);
            var catalog = powerUps != null ? powerUps.skills : null;
            SkillController.Instance?.RestoreFrom(data.skillKinds, data.skillLevels, catalog);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Time.timeScale = 1f;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                PersistRun();
        }

        void OnApplicationQuit()
        {
            PersistRun();
        }

        public void BeginBossIntro()
        {
            if (State == GameState.Victory || State == GameState.Defeat)
                return;
            State = GameState.BossIntro;
        }

        public void EndBossIntro()
        {
            if (State != GameState.BossIntro)
                return;
            State = GameState.Playing;
            Time.timeScale = 1f;
        }

        public void BeginLevelUp()
        {
            if (State == GameState.Victory || State == GameState.Defeat || State == GameState.BossIntro)
                return;
            if (Stats.level >= RunRules.MaxHeroLevel)
                return;
            Stats.level++;
            if (State == GameState.LevelUp)
            {
                queuedLevelUps++;
                return;
            }
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            currentChoices = upgrades != null ? upgrades.RollThree() : null;
            ui?.ShowLevelUp(currentChoices);
            audioService?.Play("levelup");
            if (PlayerHealth.Instance != null)
                VfxPlayer.Instance?.Play("levelup", PlayerHealth.Instance.transform.position + Vector3.up * 0.6f);
        }

        public void ChooseUpgrade(int index)
        {
            if (State != GameState.LevelUp || currentChoices == null)
                return;
            if (index >= 0 && index < currentChoices.Length && currentChoices[index] != null)
            {
                upgrades.Apply(currentChoices[index], Stats);
                audioService?.Play("ui");
            }
            if (queuedLevelUps > 0)
            {
                queuedLevelUps--;
                currentChoices = upgrades.RollThree();
                ui?.ShowLevelUp(currentChoices);
                return;
            }
            ui?.HideLevelUp();
            State = GameState.Playing;
            Time.timeScale = 1f;
            PersistRun();
        }

        public void TogglePause()
        {
            if (State == GameState.BossIntro)
                return;
            if (State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
                ui?.SetPaused(true);
                PersistRun();
            }
            else if (State == GameState.Paused)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
                ui?.SetPaused(false);
            }
        }

        public void EndRun(bool victory)
        {
            if (State == GameState.Victory || State == GameState.Defeat)
                return;
            State = victory ? GameState.Victory : GameState.Defeat;
            Time.timeScale = 0f;
            RunSaveService.Clear();
            var save = SaveService.Load();
            int floor = StageManager.Instance != null ? StageManager.Instance.Floor : 1;
            int gold = balance == null
                ? 10
                : (victory ? balance.victoryGold : balance.defeatGold) + floor * balance.goldPerLevel;
            save.gold += gold;
            float elapsed = run != null ? run.Elapsed : 0f;
            if (elapsed > save.bestRunTime)
                save.bestRunTime = elapsed;
            SaveService.Save(save);
            ui?.ShowResult(victory, gold, elapsed, save.gold);
            audioService?.Play(victory ? "victory" : "defeat");
        }

        public void Restart()
        {
            RunSaveService.Clear();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitToMenu()
        {
            PersistRun();
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void PersistRun()
        {
            if (Stats == null)
                return;
            if (State == GameState.Victory || State == GameState.Defeat)
                return;
            var health = PlayerHealth.Instance;
            if (health != null && health.IsDead)
                return;
            RunSaveService.Save(CaptureRun());
        }

        public RunSave CaptureRun()
        {
            var data = new RunSave
            {
                level = Stats != null ? Stats.level : 1,
                stats = Stats ?? new RunStats(),
                elapsed = run != null ? run.Elapsed : 0f,
                xp = XPManager.Instance != null ? XPManager.Instance.Xp : 0,
                hp = PlayerHealth.Instance != null ? PlayerHealth.Instance.Current : (Stats != null ? Stats.maxHealth : 100)
            };
            var stage = StageManager.Instance;
            if (stage != null)
            {
                data.stage = stage.Floor;
                data.floor = stage.Floor;
                data.floorKills = stage.FloorKills;
                data.runSeed = stage.RunSeed;
                data.intermission = stage.Intermission;
                data.biome = (int)stage.Theme.biome;
                data.weather = (int)stage.Theme.weather;
                data.time = (int)stage.Theme.time;
                data.themeSeed = stage.Theme.seed;
            }
            var skills = SkillController.Instance;
            if (skills != null && skills.Count > 0)
            {
                data.skillKinds = new int[skills.Count];
                data.skillLevels = new int[skills.Count];
                for (int i = 0; i < skills.Count; i++)
                {
                    var skill = skills.Skills[i];
                    data.skillKinds[i] = skill.definition != null ? (int)skill.definition.kind : 0;
                    data.skillLevels[i] = skill.level;
                }
            }
            return data;
        }
    }
}
