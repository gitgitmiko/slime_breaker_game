using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// One large map for the whole run. Floors 1–100. Power-ups drop every five hero levels.
    /// Every ten floors a unique boss is announced, the camera zooms in, and regular spawning
    /// pauses until that boss falls.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        public ArenaGenerator generator;
        public PowerUpSpawner powerUps;
        public EnemySpawner spawner;
        public ScreenFader fader;
        public GameObject bossPrefab;
        public EnemyDefinition[] bossDefinitions;
        public GameObject hostileBolt;
        public GameObject lightningPrefab;
        public GameObject minionPrefab;
        public FollowCamera cameraRig;

        public int Floor { get; private set; } = 1;
        public int FloorKills { get; private set; }
        public int Stage => Floor;
        public bool Intermission { get; private set; }
        public ArenaTheme Theme { get; private set; }
        public int HandledPowerTiers { get; private set; }
        public int HandledBossTiers { get; private set; }
        public int RunSeed { get; private set; }
        public BossKind ActiveBoss { get; private set; }

        bool introducing;

        void Awake()
        {
            Instance = this;
            if (generator != null)
                generator.generateOnStart = false;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        void OnDisable()
        {
            EnemyHealth.AnyDied -= OnEnemyDied;
        }

        void Start()
        {
            var resume = RunSaveService.PeekResume();
            if (resume != null && resume.level > 0)
            {
                Floor = Mathf.Clamp(resume.floor > 0 ? resume.floor : Mathf.Max(1, resume.stage), 1, RunRules.MaxFloor);
                FloorKills = Mathf.Max(0, resume.floorKills);
                RunSeed = resume.runSeed != 0 ? resume.runSeed : resume.themeSeed;
                Theme = resume.ToTheme();
                HandledPowerTiers = resume.level / RunRules.PowerUpEvery;
                HandledBossTiers = Floor / RunRules.BossEvery;
                if (resume.intermission)
                    HandledBossTiers = Mathf.Max(0, HandledBossTiers - 1);
                if (spawner != null)
                {
                    spawner.Stage = Floor;
                    if (resume.intermission)
                        spawner.Paused = true;
                }
                generator?.Generate(Theme);
                if (resume.intermission)
                    StartCoroutine(RestoreBoss());
            }
            else
            {
                RunSeed = Random.Range(1, int.MaxValue);
                Theme = ArenaTheme.First(RunSeed);
                generator?.Generate(Theme);
                if (spawner != null)
                    spawner.Stage = 1;
            }
        }

        void Update()
        {
            if (introducing)
                return;
            var flow = GameFlowController.Instance;
            if (flow == null || flow.Stats == null)
                return;
            if (flow.State != GameState.Playing)
                return;

            int level = flow.Stats.level;
            int power = level / RunRules.PowerUpEvery;
            while (HandledPowerTiers < power)
            {
                HandledPowerTiers++;
                powerUps?.Spawn();
            }

            if (BossAlive() || Intermission)
                return;
            int bosses = Floor / RunRules.BossEvery;
            if (RunRules.IsBossFloor(Floor) && bosses > HandledBossTiers)
                StartCoroutine(BeginBoss(bosses));
        }

        IEnumerator RestoreBoss()
        {
            yield return null;
            int bosses = Floor / RunRules.BossEvery;
            if (bosses > 0)
                StartCoroutine(BeginBoss(bosses));
        }

        IEnumerator BeginBoss(int tier)
        {
            if (introducing)
                yield break;
            introducing = true;
            Intermission = true;
            HandledBossTiers = tier;
            ActiveBoss = (BossKind)Mathf.Clamp(tier, 1, 10);
            VanishMinions();
            if (spawner != null)
                spawner.Paused = true;

            var flow = GameFlowController.Instance;
            flow?.BeginBossIntro();
            flow?.ui?.ShowBossWarning(BossCatalog.DisplayName(ActiveBoss), BossCatalog.Tint(ActiveBoss), Floor);
            flow?.ui?.ShowToast("BOSS LANTAI " + Floor + " — " + BossCatalog.DisplayName(ActiveBoss), BossCatalog.Tint(ActiveBoss));
            AudioService.Instance?.Play("bosswarn");

            var boss = SpawnBoss(ActiveBoss);
            if (boss != null && cameraRig != null)
                cameraRig.Focus(boss.transform, 2.5f);

            float wait = 2.6f;
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            flow?.EndBossIntro();
            introducing = false;
            flow?.PersistRun();
        }

        GameObject SpawnBoss(BossKind kind)
        {
            if (bossPrefab == null)
                return null;
            Vector3 position = new Vector3(0f, 0f, 8.5f);
            if (Obstacles.Overlaps(position, 1.4f))
                position = new Vector3(7f, 0f, 0f);
            var instance = Instantiate(bossPrefab, position, Quaternion.LookRotation(Vector3.back, Vector3.up));
            var definition = DefinitionFor(kind);
            var health = instance.GetComponent<EnemyHealth>();
            health?.Bind(definition);
            health?.SetMaxHealth(BossCatalog.HealthForFloor(Floor));
            health?.SetContactDamage(BossCatalog.ContactDamageForFloor(Floor));
            health?.TintBody(BossCatalog.Tint(kind));
            var boss = instance.GetComponent<BossController>();
            if (boss == null)
                boss = instance.AddComponent<BossController>();
            boss.Configure(kind, hostileBolt, lightningPrefab, minionPrefab, Floor);
            boss.arenaRadius = RunRules.ArenaRadius;
            return instance;
        }

        EnemyDefinition DefinitionFor(BossKind kind)
        {
            int index = (int)kind - 1;
            if (bossDefinitions != null && index >= 0 && index < bossDefinitions.Length)
                return bossDefinitions[index];
            return null;
        }

        void OnEnemyDied(EnemyHealth enemy)
        {
            if (enemy == null)
                return;
            if (enemy.IsBoss)
            {
                var kind = ActiveBoss;
                ActiveBoss = BossKind.None;
                Intermission = false;
                if (spawner != null)
                {
                    spawner.Paused = false;
                    spawner.Stage = Floor;
                }
                if (BossCatalog.IsFinal(kind) || Floor >= RunRules.MaxFloor)
                {
                    GameFlowController.Instance?.EndRun(true);
                    return;
                }
                GameFlowController.Instance?.ui?.ShowToast(BossCatalog.DisplayName(kind) + " kalah!", new Color(1f, 0.85f, 0.4f));
                AdvanceFloor();
                return;
            }

            if (introducing || Intermission || RunRules.IsBossFloor(Floor))
                return;
            FloorKills++;
            if (FloorKills >= RunRules.KillQuota(Floor))
                AdvanceFloor();
        }

        void AdvanceFloor()
        {
            if (Floor >= RunRules.MaxFloor)
                return;
            Floor++;
            FloorKills = 0;
            if (spawner != null)
                spawner.Stage = Floor;
            if (!RunRules.IsBossFloor(Floor))
                GameFlowController.Instance?.ui?.ShowToast("Lantai " + Floor, new Color(0.55f, 0.9f, 1f));
            GameFlowController.Instance?.PersistRun();
        }

        /// <summary>Playtest helper: jump to a floor without walking the kill quota.</summary>
        public void ForceFloor(int floor)
        {
            Floor = Mathf.Clamp(floor, 1, RunRules.MaxFloor);
            FloorKills = 0;
            HandledBossTiers = (Floor - 1) / RunRules.BossEvery;
            Intermission = false;
            introducing = false;
            ActiveBoss = BossKind.None;
            VanishMinions();
            if (spawner != null)
            {
                spawner.Stage = Floor;
                spawner.Paused = false;
            }
        }

        public void EnterPortal(ExitPortal portal)
        {
            if (portal != null)
                Destroy(portal.gameObject);
        }

        static void VanishMinions()
        {
            var snapshot = EnemyHealth.Active.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null && !snapshot[i].IsBoss)
                    snapshot[i].Vanish();
            }
        }

        static bool BossAlive()
        {
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy != null && !enemy.IsDead && enemy.IsBoss)
                    return true;
            }
            return false;
        }
    }
}
