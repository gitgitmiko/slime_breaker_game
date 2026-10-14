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

        int queuedLevelUps;
        UpgradeDefinition[] currentChoices;

        void Awake()
        {
            Instance = this;
            var save = SaveService.Load();
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
            Time.timeScale = 1f;
            State = GameState.Playing;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Time.timeScale = 1f;
        }

        public void BeginLevelUp()
        {
            if (State == GameState.Victory || State == GameState.Defeat)
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
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
                ui?.SetPaused(true);
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
            var save = SaveService.Load();
            int level = Stats != null ? Stats.level : 1;
            int gold = balance == null
                ? 10
                : (victory ? balance.victoryGold : balance.defeatGold) + level * balance.goldPerLevel;
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
