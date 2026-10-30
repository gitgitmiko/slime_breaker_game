using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Floors 1–100. Each floor rebuilds the arena: weather and day/night rotate every floor,
    /// biome changes every ten floors. Power-ups drop every few hero levels (see <see cref="DifficultyProfile.powerUpEvery"/>).
    /// The hero walks in through the south door of the arena. Clearing the floor kill quota (N×10 slimes on lantai N) or beating a boss
    /// opens the north door; walking through it advances the floor. Entering a boss floor plays an ad first.
    /// Every ten floors a unique boss is announced, the camera zooms in, and regular spawning
    /// pauses until that boss falls. Bosses summon minions so the hero can keep levelling.
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
        public GameObject portalPrefab;
        public FollowCamera cameraRig;
        public ArenaDoor entryDoor;
        public ArenaDoor exitDoor;

        public int Floor { get; private set; } = 1;
        public int FloorKills { get; private set; }
        public int KillQuota => RunRules.KillQuota(Floor);
        public bool FloorCleared { get; private set; }
        public int Stage => Floor;
        public bool Intermission { get; private set; }
        public ArenaTheme Theme { get; private set; }
        public int HandledPowerTiers { get; private set; }
        public int HandledBossTiers { get; private set; }
        public int RunSeed { get; private set; }
        public BossKind ActiveBoss { get; private set; }

        bool introducing;
        bool transitioning;
        Coroutine entrance;

        /// <summary>Where the hero stands, out of sight in the south door, before walking in.</summary>
        static readonly Vector3 DoorwayOutside = new Vector3(0f, 0f, -24.6f);
        static readonly Vector3 DoorwayInside = new Vector3(0f, 0f, -19.5f);
        /// <summary>Beyond the north door; the hero keeps walking there while the screen fades.</summary>
        static readonly Vector3 ExitBeyond = new Vector3(0f, 0f, 25f);
        static readonly Vector3 ExitTrigger = new Vector3(0f, 0f, 22.3f);
        const float DoorWalkSpeed = 5.2f;

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
                FloorCleared = FloorKills >= RunRules.KillQuota(Floor) && !RunRules.IsBossFloor(Floor);
                RunSeed = resume.runSeed != 0 ? resume.runSeed : resume.themeSeed;
                Theme = resume.ToTheme();
                HandledPowerTiers = resume.level / Difficulty.Profile.powerUpEvery;
                HandledBossTiers = Floor / RunRules.BossEvery;
                if (resume.intermission)
                    HandledBossTiers = Mathf.Max(0, HandledBossTiers - 1);
                if (spawner != null)
                {
                    spawner.Stage = Floor;
                    if (resume.intermission)
                        spawner.Paused = true;
                }
                ApplyTheme(true);
                if (resume.intermission)
                    StartCoroutine(RestoreBoss());
                else if (FloorCleared)
                    OfferPortal();
            }
            else
            {
                RunSeed = Random.Range(1, int.MaxValue);
                Theme = ArenaTheme.ForFloor(1, RunSeed);
                ApplyTheme(true);
                if (spawner != null)
                {
                    spawner.Stage = 1;
                    spawner.Paused = true;
                }
                entrance = StartCoroutine(EnterFloor());
            }
        }

        /// <summary>South door opens, the hero walks in, it shuts behind them, and only then does the floor start.</summary>
        IEnumerator EnterFloor()
        {
            transitioning = true;
            yield return WalkIn();
            transitioning = false;
            entrance = null;
        }

        IEnumerator WalkIn()
        {
            exitDoor?.SetOpen(false, true);
            entryDoor?.SetOpen(true, true);
            StageHeroOutside();
            var walker = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetComponent<PlayerController>() : null;
            walker?.WalkTo(DoorwayInside, DoorWalkSpeed);
            while (walker != null && walker.IsWalking)
                yield return null;
            walker?.ReleaseWalk();
            entryDoor?.SetOpen(false);
            if (spawner != null)
                spawner.Paused = RunRules.IsBossFloor(Floor);
        }

        void StageHeroOutside()
        {
            var hero = PlayerHealth.Instance;
            if (hero == null)
                return;
            var walker = hero.GetComponent<PlayerController>();
            walker?.HoldAt(DoorwayOutside);
            hero.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            cameraRig?.Recenter();
        }

        void Update()
        {
            if (introducing || transitioning)
                return;
            var flow = GameFlowController.Instance;
            if (flow == null || flow.Stats == null)
                return;
            if (flow.State != GameState.Playing)
                return;

            int level = flow.Stats.level;
            int power = level / Difficulty.Profile.powerUpEvery;
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
            AudioService.Instance?.SetBossMusic(true);

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
            // The hero has just walked in from the south door; the middle of the arena is always free of obstacles.
            Vector3 position = Vector3.zero;
            if (Obstacles.Overlaps(position, 1.4f))
                position = new Vector3(7f, 0f, 0f);
            var instance = Instantiate(bossPrefab, position, Quaternion.LookRotation(Vector3.back, Vector3.up));
            var definition = DefinitionFor(kind);
            var health = instance.GetComponent<EnemyHealth>();
            health?.Bind(definition);
            health?.SetMaxHealth(BossCatalog.HealthForFloor(Floor));
            health?.SetContactDamage(BossCatalog.ContactDamageForFloor(Floor));
            health?.TintBody(BossCatalog.Tint(kind));
            if (Theme.IsNight)
                AddNightLight(instance.transform, kind);
            var boss = instance.GetComponent<BossController>();
            if (boss == null)
                boss = instance.AddComponent<BossController>();
            boss.Configure(kind, hostileBolt, lightningPrefab, minionPrefab, Floor);
            boss.breakerPickupPrefab = powerUps != null ? powerUps.pickupPrefab : null;
            boss.arenaRadius = RunRules.ArenaRadius;
            return instance;
        }

        /// <summary>At night every boss carries a light above its head, like the hero, tinted to its colour.</summary>
        static void AddNightLight(Transform boss, BossKind kind)
        {
            var lightObject = new GameObject("BossLight");
            lightObject.transform.SetParent(boss, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            var bossLight = lightObject.AddComponent<Light>();
            bossLight.type = LightType.Point;
            bossLight.range = 9f;
            bossLight.intensity = 2.6f;
            // The rainbow boss glows warm white; the others take a lightened version of their own colour.
            bossLight.color = kind == BossKind.Royal ? new Color(1f, 0.92f, 0.8f) : Color.Lerp(BossCatalog.Tint(kind), Color.white, 0.55f);
            bossLight.shadows = LightShadows.None;
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
                AudioService.Instance?.SetBossMusic(false);
                PlayerHealth.Instance?.Emote("Victory");
                Intermission = false;
                VanishMinions();
                if (spawner != null)
                {
                    spawner.Paused = true;
                    spawner.Stage = Floor;
                }
                if (BossCatalog.IsFinal(kind) || Floor >= RunRules.MaxFloor)
                {
                    GameFlowController.Instance?.EndRun(true);
                    return;
                }
                OfferPortal(BossCatalog.DisplayName(kind) + " kalah! Masuk pintu");
                return;
            }

            if (transitioning || introducing || Intermission || RunRules.IsBossFloor(Floor))
                return;
            if (FloorCleared || ExitPortal.Active != null)
                return;
            FloorKills++;
            if (FloorKills >= RunRules.KillQuota(Floor))
                OfferPortal();
        }

        void OfferPortal(string message = null)
        {
            if (transitioning || Floor >= RunRules.MaxFloor)
                return;
            FloorCleared = true;
            if (ExitPortal.Active != null)
                return;
            exitDoor?.SetOpen(true);
            SpawnPortal();
            GameFlowController.Instance?.ui?.ShowToast(message ?? "Pintu terbuka — masuk untuk naik lantai", new Color(0.55f, 0.9f, 1f));
            GameFlowController.Instance?.PersistRun();
        }

        void SpawnPortal()
        {
            if (portalPrefab == null)
                return;
            Instantiate(portalPrefab, ExitTrigger, Quaternion.identity);
        }

        /// <summary>Playtest helper: ends the walk through the south door at once, with the hero in the middle of the arena.</summary>
        public void SkipEntrance()
        {
            if (entrance == null)
                return;
            StopCoroutine(entrance);
            entrance = null;
            transitioning = false;
            var walker = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetComponent<PlayerController>() : null;
            walker?.ReleaseWalk();
            PlaceHero(Vector3.zero);
            cameraRig?.Recenter();
            entryDoor?.SetOpen(false, true);
            if (spawner != null)
                spawner.Paused = RunRules.IsBossFloor(Floor);
        }

        /// <summary>Playtest helper: jump to a floor without walking the kill quota.</summary>
        public void ForceFloor(int floor)
        {
            SkipEntrance();
            entryDoor?.SetOpen(false, true);
            exitDoor?.SetOpen(false, true);
            Floor = Mathf.Clamp(floor, 1, RunRules.MaxFloor);
            FloorKills = 0;
            FloorCleared = false;
            HandledBossTiers = (Floor - 1) / RunRules.BossEvery;
            Intermission = false;
            introducing = false;
            transitioning = false;
            ActiveBoss = BossKind.None;
            AudioService.Instance?.SetBossMusic(false);
            ItemPickup.CollectAll();
            VanishMinions();
            if (ExitPortal.Active != null)
                Destroy(ExitPortal.Active.gameObject);
            Theme = ArenaTheme.ForFloor(Floor, RunSeed != 0 ? RunSeed : 2024, Theme);
            ApplyTheme(true);
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
            if (Floor >= RunRules.MaxFloor || transitioning)
                return;
            StartCoroutine(AdvanceFloor());
        }

        IEnumerator AdvanceFloor()
        {
            transitioning = true;
            FloorCleared = true;
            VanishMinions();
            if (ExitPortal.Active != null)
                Destroy(ExitPortal.Active.gameObject);
            if (spawner != null)
                spawner.Paused = true;
            ItemPickup.CollectAll();
            var walker = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetComponent<PlayerController>() : null;
            walker?.WalkTo(ExitBeyond, DoorWalkSpeed);
            if (fader != null)
                yield return fader.FadeOut(0.32f);

            Floor++;
            FloorKills = 0;
            FloorCleared = false;
            Theme = ArenaTheme.ForFloor(Floor, RunSeed != 0 ? RunSeed : 2024, Theme);
            ApplyTheme(false);
            exitDoor?.SetOpen(false, true);
            entryDoor?.SetOpen(true, true);
            StageHeroOutside();
            if (spawner != null)
                spawner.Stage = Floor;

            // The boss scene only appears once the ad has been closed; the screen stays black behind it.
            if (RunRules.IsBossFloor(Floor))
                yield return PlayBossFloorAd();

            if (fader != null)
                StartCoroutine(fader.FadeIn(0.32f));
            yield return WalkIn();
            transitioning = false;
            if (!RunRules.IsBossFloor(Floor))
                GameFlowController.Instance?.ui?.ShowToast("Lantai " + Floor + " — " + Theme.Title, new Color(0.55f, 0.9f, 1f));
            GameFlowController.Instance?.PersistRun();
        }

        IEnumerator PlayBossFloorAd()
        {
            var ads = GameFlowController.Instance != null ? GameFlowController.Instance.ads : null;
            if (ads == null)
                yield break;
            bool closed = false;
            Time.timeScale = 0f;
            ads.ShowInterstitial(() => closed = true);
            while (!closed)
                yield return null;
            Time.timeScale = 1f;
        }

        void ApplyTheme(bool immediate)
        {
            generator?.Generate(Theme);
            if (immediate)
                PlaceHero(PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero);
        }

        static void PlaceHero(Vector3 position)
        {
            var hero = PlayerHealth.Instance;
            if (hero == null)
                return;
            position.y = 0f;
            if (Obstacles.Overlaps(position, 0.6f))
                position = Vector3.zero;
            var body = hero.GetComponent<CharacterController>();
            if (body != null)
                body.enabled = false;
            hero.transform.position = position;
            if (body != null)
                body.enabled = true;
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
