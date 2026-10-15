using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Every five hero levels the arena is cleared: living slimes vanish, a power-up drops, and an exit portal
    /// opens to a new random arena (biome × weather × time of day).
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        public ArenaGenerator generator;
        public PowerUpSpawner powerUps;
        public EnemySpawner spawner;
        public GameObject portalPrefab;
        public ScreenFader fader;
        public int everyLevels = 5;
        public float portalDelay = 1.15f;

        public int Stage { get; private set; } = 1;
        public bool Intermission { get; private set; }
        public ArenaTheme Theme { get; private set; }
        public int HandledTier { get; private set; }
        public int RunSeed { get; private set; }

        bool transitioning;

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

        void Start()
        {
            var resume = RunSaveService.PeekResume();
            if (resume != null && resume.level > 0)
            {
                Stage = Mathf.Max(1, resume.stage);
                RunSeed = resume.runSeed != 0 ? resume.runSeed : resume.themeSeed;
                Theme = resume.ToTheme();
                HandledTier = resume.level / Mathf.Max(1, everyLevels);
                if (resume.intermission)
                    HandledTier = Mathf.Max(0, HandledTier - 1);
                if (spawner != null)
                {
                    spawner.Stage = Stage;
                    if (resume.intermission)
                        spawner.Paused = true;
                }
                generator?.Generate(Theme);
                Announce(false);
                if (resume.intermission)
                    StartCoroutine(RestoreIntermission());
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
            if (Intermission || transitioning)
                return;
            var flow = GameFlowController.Instance;
            if (flow == null || flow.Stats == null)
                return;
            if (flow.State == GameState.Victory || flow.State == GameState.Defeat)
                return;
            if (BossAlive())
                return;
            int tier = flow.Stats.level / Mathf.Max(1, everyLevels);
            if (tier > HandledTier)
                BeginIntermission();
        }

        public void BeginIntermission()
        {
            if (Intermission)
                return;
            Intermission = true;
            HandledTier = (GameFlowController.Instance != null && GameFlowController.Instance.Stats != null)
                ? GameFlowController.Instance.Stats.level / Mathf.Max(1, everyLevels)
                : HandledTier + 1;
            EnemyHealth.VanishAll();
            if (spawner != null)
                spawner.Paused = true;
            powerUps?.Spawn();
            GameFlowController.Instance?.ui?.ShowToast("Arena bersih! Ambil power-up dan masuk portal.", new Color(1f, 0.85f, 0.4f));
            GameFlowController.Instance?.PersistRun();
            StartCoroutine(SpawnPortalSoon());
        }

        IEnumerator RestoreIntermission()
        {
            yield return null;
            BeginIntermission();
        }

        IEnumerator SpawnPortalSoon()
        {
            float wait = portalDelay;
            while (wait > 0f)
            {
                wait -= Time.deltaTime;
                yield return null;
            }
            if (!Intermission || portalPrefab == null)
                yield break;
            Vector3 position = FindPortalSpot();
            var instance = Instantiate(portalPrefab, position, Quaternion.identity);
            if (instance.GetComponent<ExitPortal>() == null)
                instance.AddComponent<ExitPortal>();
            AudioService.Instance?.Play("portal");
            GameFlowController.Instance?.PersistRun();
        }

        Vector3 FindPortalSpot()
        {
            Vector3 hero = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
            Vector3 pickup = PowerUpPickup.Active.Count > 0 ? PowerUpPickup.Active[0].transform.position : hero + Vector3.right * 4f;
            Vector3 best = -hero.normalized * 6.5f;
            if (best.sqrMagnitude < 1f)
                best = Vector3.forward * 6.5f;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                float radius = 6.2f + (i % 3) * 0.6f;
                var candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Obstacles.Overlaps(candidate, 1.4f))
                    continue;
                if (generator != null && generator.TouchesObstacle(candidate, 1.4f))
                    continue;
                float fromHero = Vector3.Distance(candidate, hero);
                float fromPickup = Vector3.Distance(candidate, pickup);
                float score = fromHero + fromPickup * 0.7f;
                if (fromHero < 3.2f)
                    continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        public void EnterPortal(ExitPortal portal)
        {
            if (transitioning)
                return;
            StartCoroutine(Transition(portal));
        }

        IEnumerator Transition(ExitPortal portal)
        {
            transitioning = true;
            Time.timeScale = 1f;
            if (fader != null)
                yield return fader.FadeOut(0.35f);

            if (portal != null)
                Destroy(portal.gameObject);
            for (int i = PowerUpPickup.Active.Count - 1; i >= 0; i--)
            {
                if (PowerUpPickup.Active[i] != null)
                    Destroy(PowerUpPickup.Active[i].gameObject);
            }
            EnemyHealth.VanishAll();

            Stage++;
            var rng = new System.Random(unchecked(RunSeed * 73856093 + Stage * 19349663));
            Theme = ArenaTheme.Roll(Theme, rng);
            generator?.Generate(Theme);

            var hero = PlayerHealth.Instance;
            if (hero != null)
            {
                var body = hero.GetComponent<CharacterController>();
                if (body != null)
                    body.enabled = false;
                hero.transform.position = Vector3.zero;
                hero.transform.rotation = Quaternion.identity;
                if (body != null)
                    body.enabled = true;
            }

            Intermission = false;
            transitioning = false;
            if (spawner != null)
            {
                spawner.Stage = Stage;
                spawner.Paused = false;
            }
            Announce(true);
            GameFlowController.Instance?.PersistRun();

            if (fader != null)
                yield return fader.FadeIn(0.45f);
        }

        void Announce(bool arrived)
        {
            string prefix = arrived ? "Arena baru: " : "Arena: ";
            GameFlowController.Instance?.ui?.ShowToast(prefix + Theme.Title, new Color(0.7f, 0.9f, 1f));
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
