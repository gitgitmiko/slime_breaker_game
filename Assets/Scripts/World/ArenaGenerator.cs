using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeBreaker
{
    /// <summary>
    /// Builds the arena decoration for a theme at runtime: ground material, prop rings, solid obstacles,
    /// weather particles and the lighting/fog mood. Everything lives under one root so a stage change can replace it.
    /// </summary>
    public class ArenaGenerator : MonoBehaviour
    {
        [Serializable]
        public class PropSet
        {
            public GameObject treeA;
            public GameObject treeB;
            public GameObject rockA;
            public GameObject rockB;
            public GameObject mushroom;
            public GameObject bush;
            public GameObject pillar;
            public GameObject grass;
            public GameObject flower;
            public GameObject boundaryStone;
            public GameObject runeRing;
            public GameObject cactus;
            public GameObject iceSpike;
            public GameObject thunderSpire;
            public GameObject swampRoot;
            public GameObject swampReed;
            public GameObject shadowObelisk;
            public GameObject volcanoRock;
            public GameObject volcanoPillar;
            public GameObject stormArch;
            public GameObject crystalSpire;
            public GameObject royalColumn;
        }

        public PropSet props = new PropSet();
        public GameObject[] obstaclePrefabs;
        public Renderer groundRenderer;
        public Material forestGround;
        public Material snowGround;
        public Material sandGround;
        public Material thunderGround;
        public Material swampGround;
        public Material shadowGround;
        public Material volcanoGround;
        public Material stormGround;
        public Material crystalGround;
        public Material royalGround;
        public GameObject rainRig;
        public GameObject snowRig;
        public GameObject firefliesRig;
        public GameObject dustRig;
        public GameObject ashRig;
        public GameObject sporesRig;
        public GameObject sparksRig;
        public GameObject mistRig;
        [Header("Biome cover (moss / snow / sand / ash on upward prop faces)")]
        public Texture2D coverMoss;
        public Texture2D coverSnow;
        public Texture2D coverSand;
        public Texture2D coverAsh;
        public Light sun;
        public Camera arenaCamera;
        public float arenaRadius = RunRules.ArenaRadius;
        public int minObstacles = 10;
        public int maxObstacles = 16;
        public bool generateOnStart = true;

        public ArenaTheme Current { get; private set; }
        public Transform Root { get; private set; }
        public int Generations { get; private set; }
        public IReadOnlyList<Obstacle> PlacedObstacles => obstacles;

        static readonly Color SnowLeaf = new Color(0.86f, 0.93f, 1f);
        static readonly Color SnowGrass = new Color(0.8f, 0.88f, 0.95f);
        static readonly Color DesertLeaf = new Color(0.6f, 0.56f, 0.28f);
        static readonly Color DesertGrass = new Color(0.72f, 0.6f, 0.34f);
        static readonly Color SwampLeaf = new Color(0.28f, 0.45f, 0.18f);
        static readonly Color ThunderLeaf = new Color(0.32f, 0.38f, 0.22f);
        static readonly Color RoyalLeaf = new Color(0.45f, 0.62f, 0.28f);

        /// <summary>Sun and ambient multipliers for the HDR + ACES pipeline (tuned from screenshots).</summary>
        public const float HdrSunBoost = 1.3f;
        public const float HdrAmbientBoost = 1.3f;
        public const float MinDaySun = 1.2f;

        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Vector3> occupied = new List<Vector3>();
        /// <summary>Ground cover instances (grass, flowers, reeds) merged into a few meshes after placement.</summary>
        readonly List<GameObject> scatterInstances = new List<GameObject>();
        /// <summary>Biome-tinted copies of shared foliage materials (a material per tint keeps SRP batching intact).</summary>
        readonly Dictionary<(Material, Color), Material> tintedMaterials = new Dictionary<(Material, Color), Material>();
        readonly List<Mesh> combinedMeshes = new List<Mesh>();
        const int ScatterSectors = 8;
        Light heroLight;

        void Start()
        {
            if (generateOnStart && Generations == 0)
                Generate(ArenaTheme.First(2024));
        }

        public void Generate(ArenaTheme theme)
        {
            Current = theme;
            Generations++;
            Clear();
            var rootObject = new GameObject("Arena_" + theme.biome + "_" + theme.weather + "_" + theme.time);
            Root = rootObject.transform;
            var rng = new System.Random(theme.seed);

            ApplyGround(theme);
            ApplyLighting(theme);
            ApplyCover(theme);
            PlaceProps(theme, rng);
            PlaceObstacles(theme, rng);
            TintFoliage(theme);
            CombineScatter();
            SpawnWeather(theme);
            if (theme.IsNight)
                EnsureHeroLight();
            StaticBatchingUtility.Combine(rootObject);
        }

        void OnDestroy()
        {
            foreach (var material in tintedMaterials.Values)
            {
                if (material != null)
                    Destroy(material);
            }
            tintedMaterials.Clear();
            foreach (var mesh in combinedMeshes)
            {
                if (mesh != null)
                    Destroy(mesh);
            }
            combinedMeshes.Clear();
        }

        /// <summary>
        /// Hundreds of grass/flower cards would each be a draw call (plus shadow passes). Merge them per material
        /// into angular sectors (so frustum culling still drops the off-screen ones); the cards cast no shadows.
        /// </summary>
        void CombineScatter()
        {
            if (scatterInstances.Count == 0 || Root == null)
                return;
            var groups = new Dictionary<(Material, int), List<CombineInstance>>();
            var toDestroy = new List<GameObject>();
            Matrix4x4 rootInverse = Root.worldToLocalMatrix;
            foreach (var instance in scatterInstances)
            {
                if (instance == null)
                    continue;
                bool merged = false;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    var mesh = filter.sharedMesh;
                    if (renderer == null || mesh == null || !mesh.isReadable)
                        continue;
                    Vector3 p = filter.transform.position;
                    int sector = Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) + 0.5f) * ScatterSectors), 0, ScatterSectors - 1);
                    var materials = renderer.sharedMaterials;
                    for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                    {
                        if (materials[sub] == null)
                            continue;
                        var key = (materials[sub], sector);
                        if (!groups.TryGetValue(key, out var list))
                            groups[key] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = rootInverse * filter.transform.localToWorldMatrix });
                    }
                    merged = true;
                }
                if (merged)
                    toDestroy.Add(instance);
                else
                    DisableScatterShadows(instance);
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Scatter_" + pair.Key.Item1.name + "_" + pair.Key.Item2 };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                combinedMeshes.Add(mesh);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Item1;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            foreach (var instance in toDestroy)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
            scatterInstances.Clear();
        }

        static void DisableScatterShadows(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Number of merged ground-cover meshes in the current arena (playtests).</summary>
        public int CombinedScatterMeshes => Root == null ? 0 : CountCombined();

        int CountCombined()
        {
            int count = 0;
            foreach (Transform child in Root)
            {
                if (child.name.StartsWith("Scatter_"))
                    count++;
            }
            return count;
        }

        void Clear()
        {
            obstacles.Clear();
            occupied.Clear();
            scatterInstances.Clear();
            foreach (var mesh in combinedMeshes)
            {
                if (mesh != null)
                    Destroy(mesh);
            }
            combinedMeshes.Clear();
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
                Destroy(Root.gameObject);
                Root = null;
            }
            if (heroLight != null)
            {
                Destroy(heroLight.gameObject);
                heroLight = null;
            }
        }

        // ------------------------------------------------------------------ ground & light

        void ApplyGround(ArenaTheme theme)
        {
            if (groundRenderer == null)
                return;
            var material = theme.biome switch
            {
                Biome.Snow => snowGround,
                Biome.Desert => sandGround,
                Biome.Thunder => thunderGround,
                Biome.Swamp => swampGround,
                Biome.Shadow => shadowGround,
                Biome.Volcano => volcanoGround,
                Biome.Storm => stormGround,
                Biome.Crystal => crystalGround,
                Biome.Royal => royalGround,
                _ => forestGround
            };
            if (material != null)
                groundRenderer.sharedMaterial = material;
        }

        void ApplyLighting(ArenaTheme theme)
        {
            Color sunColor;
            float intensity;
            Vector3 euler;
            Color sky, equator, groundAmbient, fog, background;
            float fogStart, fogEnd;
            switch (theme.biome)
            {
                case Biome.Snow:
                    sunColor = new Color(0.92f, 0.95f, 1f); intensity = 1.15f; euler = new Vector3(40f, -20f, 0f);
                    sky = new Color(0.72f, 0.8f, 0.9f); equator = new Color(0.6f, 0.66f, 0.74f); groundAmbient = new Color(0.3f, 0.32f, 0.36f);
                    fog = new Color(0.78f, 0.84f, 0.9f); fogStart = 20f; fogEnd = 52f; background = new Color(0.7f, 0.78f, 0.86f);
                    break;
                case Biome.Desert:
                    sunColor = new Color(1f, 0.9f, 0.72f); intensity = 1.45f; euler = new Vector3(62f, 10f, 0f);
                    sky = new Color(0.8f, 0.72f, 0.58f); equator = new Color(0.62f, 0.5f, 0.36f); groundAmbient = new Color(0.3f, 0.22f, 0.14f);
                    fog = new Color(0.84f, 0.74f, 0.56f); fogStart = 24f; fogEnd = 58f; background = new Color(0.74f, 0.62f, 0.46f);
                    break;
                case Biome.Thunder:
                    sunColor = new Color(0.82f, 0.86f, 1f); intensity = 1.05f; euler = new Vector3(48f, 40f, 0f);
                    sky = new Color(0.42f, 0.48f, 0.58f); equator = new Color(0.32f, 0.36f, 0.4f); groundAmbient = new Color(0.14f, 0.16f, 0.12f);
                    fog = new Color(0.4f, 0.46f, 0.52f); fogStart = 16f; fogEnd = 48f; background = new Color(0.32f, 0.38f, 0.46f);
                    break;
                case Biome.Swamp:
                    sunColor = new Color(0.78f, 0.86f, 0.62f); intensity = 0.95f; euler = new Vector3(44f, -10f, 0f);
                    sky = new Color(0.42f, 0.52f, 0.36f); equator = new Color(0.3f, 0.38f, 0.24f); groundAmbient = new Color(0.12f, 0.16f, 0.1f);
                    fog = new Color(0.36f, 0.46f, 0.32f); fogStart = 10f; fogEnd = 38f; background = new Color(0.28f, 0.38f, 0.24f);
                    break;
                case Biome.Shadow:
                    sunColor = new Color(0.55f, 0.4f, 0.85f); intensity = 0.7f; euler = new Vector3(36f, 120f, 0f);
                    sky = new Color(0.18f, 0.12f, 0.28f); equator = new Color(0.12f, 0.08f, 0.2f); groundAmbient = new Color(0.05f, 0.04f, 0.1f);
                    fog = new Color(0.1f, 0.06f, 0.16f); fogStart = 10f; fogEnd = 36f; background = new Color(0.08f, 0.05f, 0.12f);
                    break;
                case Biome.Volcano:
                    sunColor = new Color(1f, 0.62f, 0.32f); intensity = 1.2f; euler = new Vector3(58f, 25f, 0f);
                    sky = new Color(0.55f, 0.28f, 0.18f); equator = new Color(0.42f, 0.2f, 0.12f); groundAmbient = new Color(0.18f, 0.08f, 0.06f);
                    fog = new Color(0.42f, 0.18f, 0.1f); fogStart = 14f; fogEnd = 46f; background = new Color(0.32f, 0.12f, 0.08f);
                    break;
                case Biome.Storm:
                    sunColor = new Color(0.75f, 0.82f, 0.95f); intensity = 0.9f; euler = new Vector3(42f, 80f, 0f);
                    sky = new Color(0.48f, 0.54f, 0.62f); equator = new Color(0.36f, 0.4f, 0.46f); groundAmbient = new Color(0.16f, 0.18f, 0.22f);
                    fog = new Color(0.46f, 0.52f, 0.58f); fogStart = 12f; fogEnd = 42f; background = new Color(0.38f, 0.44f, 0.52f);
                    break;
                case Biome.Crystal:
                    sunColor = new Color(0.85f, 0.7f, 1f); intensity = 1.15f; euler = new Vector3(46f, -50f, 0f);
                    sky = new Color(0.48f, 0.36f, 0.7f); equator = new Color(0.36f, 0.28f, 0.52f); groundAmbient = new Color(0.16f, 0.12f, 0.28f);
                    fog = new Color(0.42f, 0.3f, 0.58f); fogStart = 16f; fogEnd = 50f; background = new Color(0.32f, 0.22f, 0.48f);
                    break;
                case Biome.Royal:
                    sunColor = new Color(1f, 0.9f, 0.7f); intensity = 1.3f; euler = new Vector3(54f, -15f, 0f);
                    sky = new Color(0.7f, 0.6f, 0.48f); equator = new Color(0.55f, 0.42f, 0.3f); groundAmbient = new Color(0.22f, 0.16f, 0.1f);
                    fog = new Color(0.62f, 0.5f, 0.36f); fogStart = 22f; fogEnd = 56f; background = new Color(0.52f, 0.4f, 0.28f);
                    break;
                default:
                    sunColor = new Color(1f, 0.95f, 0.86f); intensity = 1.25f; euler = new Vector3(52f, -30f, 0f);
                    sky = new Color(0.58f, 0.7f, 0.78f); equator = new Color(0.4f, 0.5f, 0.38f); groundAmbient = new Color(0.16f, 0.18f, 0.12f);
                    fog = new Color(0.52f, 0.68f, 0.76f); fogStart = 26f; fogEnd = 60f; background = new Color(0.36f, 0.56f, 0.7f);
                    break;
            }

            switch (theme.weather)
            {
                case Weather.Rain:
                    sunColor = Color.Lerp(sunColor, new Color(0.7f, 0.78f, 0.9f), 0.7f); intensity *= 0.55f;
                    sky = Color.Lerp(sky, new Color(0.45f, 0.5f, 0.58f), 0.7f); equator = Color.Lerp(equator, new Color(0.36f, 0.4f, 0.46f), 0.7f);
                    fog = new Color(0.45f, 0.52f, 0.6f); fogStart = 12f; fogEnd = 40f; background = new Color(0.42f, 0.48f, 0.56f);
                    break;
                case Weather.Snowfall:
                    intensity *= 0.9f; fogStart = 14f; fogEnd = 42f;
                    break;
                case Weather.Heat:
                    sunColor = new Color(1f, 0.93f, 0.78f); intensity *= 1.2f; euler = new Vector3(72f, 20f, 0f);
                    sky = Color.Lerp(sky, Color.white, 0.2f); fogStart = 30f; fogEnd = 64f;
                    break;
                case Weather.Ash:
                    sunColor = Color.Lerp(sunColor, new Color(0.7f, 0.4f, 0.25f), 0.5f); intensity *= 0.7f;
                    fogStart = 10f; fogEnd = 34f;
                    break;
                case Weather.Spores:
                    intensity *= 0.75f; fogStart = 8f; fogEnd = 32f;
                    fog = Color.Lerp(fog, new Color(0.4f, 0.55f, 0.28f), 0.5f);
                    break;
                case Weather.Sparks:
                    intensity *= 1.05f; fogStart = 18f; fogEnd = 48f;
                    break;
            }

            if (theme.IsNight)
            {
                sunColor = new Color(0.45f, 0.55f, 0.95f); intensity = 0.42f; euler = new Vector3(38f, 150f, 0f);
                sky = new Color(0.12f, 0.16f, 0.3f); equator = new Color(0.08f, 0.1f, 0.2f); groundAmbient = new Color(0.03f, 0.03f, 0.06f);
                fog = new Color(0.05f, 0.07f, 0.14f); fogStart = 12f; fogEnd = 38f; background = new Color(0.04f, 0.06f, 0.12f);
                if (theme.biome == Biome.Volcano)
                {
                    sunColor = new Color(0.95f, 0.4f, 0.18f); intensity = 0.55f;
                    sky = new Color(0.22f, 0.08f, 0.06f); fog = new Color(0.18f, 0.06f, 0.04f); background = new Color(0.12f, 0.04f, 0.03f);
                }
                else if (theme.biome == Biome.Crystal)
                {
                    sunColor = new Color(0.7f, 0.5f, 1f); intensity = 0.5f;
                    sky = new Color(0.16f, 0.1f, 0.28f); fog = new Color(0.12f, 0.08f, 0.22f); background = new Color(0.08f, 0.05f, 0.16f);
                }
                else if (theme.biome == Biome.Shadow)
                {
                    intensity = 0.28f;
                    sky = new Color(0.06f, 0.04f, 0.12f); background = new Color(0.03f, 0.02f, 0.07f);
                }
                if (theme.weather == Weather.Rain)
                {
                    fogStart = 10f; fogEnd = 32f;
                }
            }

            // Values above were authored for LDR; ACES tonemapping (PostFx) compresses highlights, so drive the sun harder.
            intensity *= HdrSunBoost;
            // Gloomy daytime weather (rain, spores, storm) still has to read clearly: floor the sun, lift the sky fill.
            float ambientBoost = HdrAmbientBoost;
            if (!theme.IsNight && intensity < MinDaySun)
            {
                ambientBoost *= 1.0f + (MinDaySun - intensity) * 0.6f;
                intensity = MinDaySun;
            }
            sky *= ambientBoost;
            equator *= ambientBoost;
            groundAmbient *= ambientBoost;
            PostFx.Instance?.ApplyTheme(theme);
            AudioService.Instance?.SetTheme(theme);

            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = intensity;
                sun.transform.rotation = Quaternion.Euler(euler);
                sun.shadowStrength = theme.IsNight ? 0.55f : 0.78f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = groundAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            var camera = arenaCamera != null ? arenaCamera : Camera.main;
            if (camera != null)
                camera.backgroundColor = background;
        }

        static readonly int CoverMapId = Shader.PropertyToID("_SB_CoverMap");
        static readonly int CoverColorId = Shader.PropertyToID("_SB_CoverColor");
        static readonly int CoverAmountId = Shader.PropertyToID("_SB_CoverAmount");

        /// <summary>Sets the global cover read by SlimeBreaker/Lit (RGB tint, A = smoothness).</summary>
        public static void SetCover(Texture map, Color tint, float amount)
        {
            if (map != null)
                Shader.SetGlobalTexture(CoverMapId, map);
            Shader.SetGlobalColor(CoverColorId, tint);
            Shader.SetGlobalFloat(CoverAmountId, map != null ? amount : 0f);
        }

        public float CoverAmount { get; private set; }

        void ApplyCover(ArenaTheme theme)
        {
            Texture2D map = coverMoss;
            Color tint = new Color(0.27f, 0.35f, 0.13f, 0.18f);
            float amount = 0.36f;
            switch (theme.biome)
            {
                case Biome.Snow:
                    map = coverSnow; tint = new Color(0.96f, 0.98f, 1f, 0.55f); amount = 0.75f; break;
                case Biome.Desert:
                    map = coverSand; tint = new Color(0.86f, 0.72f, 0.5f, 0.12f); amount = 0.42f; break;
                case Biome.Thunder:
                    tint = new Color(0.22f, 0.26f, 0.14f, 0.25f); amount = 0.28f; break;
                case Biome.Swamp:
                    tint = new Color(0.22f, 0.3f, 0.1f, 0.35f); amount = 0.55f; break;
                case Biome.Shadow:
                    amount = 0f; break;
                case Biome.Volcano:
                    map = coverAsh; tint = new Color(0.46f, 0.44f, 0.42f, 0.1f); amount = 0.45f; break;
                case Biome.Storm:
                    tint = new Color(0.24f, 0.31f, 0.15f, 0.5f); amount = 0.28f; break;
                case Biome.Crystal:
                    map = coverSand; tint = new Color(0.62f, 0.52f, 0.86f, 0.3f); amount = 0.2f; break;
                case Biome.Royal:
                    amount = 0.2f; break;
            }
            if (theme.weather == Weather.Snowfall)
            {
                map = coverSnow; tint = new Color(0.96f, 0.98f, 1f, 0.55f); amount = Mathf.Max(amount, 0.85f);
            }
            else if (theme.weather == Weather.Ash)
            {
                map = coverAsh; tint = new Color(0.46f, 0.44f, 0.42f, 0.1f); amount = Mathf.Max(amount, 0.5f);
            }
            CoverAmount = map != null ? amount : 0f;
            SetCover(map, tint, amount);
        }

        void EnsureHeroLight()
        {
            var hero = PlayerHealth.Instance;
            if (hero == null)
                return;
            var lightObject = new GameObject("HeroLight");
            lightObject.transform.SetParent(hero.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            heroLight = lightObject.AddComponent<Light>();
            heroLight.type = LightType.Point;
            heroLight.range = 8f;
            heroLight.intensity = 2.2f;
            heroLight.color = new Color(1f, 0.85f, 0.6f);
            heroLight.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ props

        void PlaceProps(ArenaTheme theme, System.Random rng)
        {
            float r = arenaRadius;
            int treesA = 0;
            int treesB = 0;
            Place(props.runeRing, Vector3.zero, 0f, r / 11.2f, false);
            // The dim sum steamer wall (a scene object) is the arena edge now; every prop that used to stand beyond the
            // boundary lives inside it, in a belt hugging the wall.
            switch (theme.biome)
            {
                case Biome.Snow:
                    treesA = 22; treesB = 19;
                    Ring(props.iceSpike, 10, r * 0.58f, false, rng, 0.25f, 1.15f);
                    Ring(props.rockA, 8, r * 0.44f, false, rng, 0.4f);
                    Ring(props.pillar, 5, r * 0.7f, true, rng);
                    Scatter(props.grass, 90, 2.0f, r * 0.9f, rng);
                    break;
                case Biome.Desert:
                    Ring(props.cactus, 12, r * 0.72f, false, rng, 0.3f, 1.1f);
                    Ring(props.cactus, 8, r * 0.4f, false, rng, 0.45f, 0.85f);
                    Ring(props.rockA, 14, r * 0.62f, false, rng, 0.25f, 1.15f);
                    Ring(props.rockB, 10, r * 0.45f, false, rng, 0.45f);
                    Ring(props.rockA, 10, r * 0.82f, false, rng, 0.3f, 1.6f);
                    Ring(props.pillar, 8, r * 0.7f, true, rng, 0.1f);
                    Ring(props.pillar, 5, r * 0.42f, true, rng, 0.2f, 0.8f);
                    Scatter(props.grass, 60, 2.4f, r * 0.9f, rng);
                    break;
                case Biome.Thunder:
                    treesA = 8; treesB = 7;
                    Ring(props.thunderSpire, 8, r * 0.68f, true, rng, 0.15f);
                    Ring(props.rockA, 10, r * 0.5f, false, rng, 0.3f);
                    Ring(props.pillar, 5, r * 0.38f, true, rng);
                    Scatter(props.grass, 220, 1.8f, r * 0.92f, rng);
                    break;
                case Biome.Swamp:
                    Ring(props.swampRoot, 10, r * 0.7f, false, rng, 0.25f, 1.1f);
                    Ring(props.swampRoot, 7, r * 0.42f, false, rng, 0.4f, 0.9f);
                    Ring(props.mushroom, 12, r * 0.3f, false, rng, 0.3f);
                    Ring(props.rockB, 8, r * 0.55f, false, rng, 0.35f);
                    Scatter(props.swampReed, 90, 2.0f, r * 0.92f, rng);
                    Scatter(props.grass, 140, 2.0f, r * 0.9f, rng);
                    break;
                case Biome.Shadow:
                    Ring(props.shadowObelisk, 10, r * 0.72f, true, rng, 0.12f);
                    Ring(props.shadowObelisk, 6, r * 0.4f, true, rng, 0.2f, 0.85f);
                    Ring(props.rockA, 8, r * 0.55f, false, rng, 0.3f);
                    Ring(props.pillar, 4, r * 0.28f, true, rng);
                    break;
                case Biome.Volcano:
                    Ring(props.volcanoPillar, 8, r * 0.7f, true, rng, 0.15f);
                    Ring(props.volcanoRock, 12, r * 0.52f, false, rng, 0.3f, 1.15f);
                    Ring(props.volcanoRock, 8, r * 0.34f, false, rng, 0.4f, 0.9f);
                    Ring(props.rockA, 8, r * 0.82f, false, rng, 0.25f, 1.4f);
                    break;
                case Biome.Storm:
                    Ring(props.stormArch, 6, r * 0.68f, true, rng, 0.2f);
                    Ring(props.pillar, 8, r * 0.5f, true, rng, 0.25f);
                    Ring(props.rockA, 10, r * 0.4f, false, rng, 0.35f);
                    Ring(props.rockB, 8, r * 0.78f, false, rng, 0.3f, 1.2f);
                    Scatter(props.grass, 110, 2.0f, r * 0.9f, rng);
                    break;
                case Biome.Crystal:
                    Ring(props.crystalSpire, 10, r * 0.7f, false, rng, 0.2f, 1.1f);
                    Ring(props.crystalSpire, 7, r * 0.42f, false, rng, 0.35f, 0.8f);
                    Ring(props.iceSpike, 8, r * 0.55f, false, rng, 0.3f);
                    Ring(props.rockB, 6, r * 0.3f, false, rng, 0.4f);
                    break;
                case Biome.Royal:
                    Ring(props.royalColumn, 10, r * 0.72f, true, rng, 0.08f);
                    Ring(props.royalColumn, 6, r * 0.42f, true, rng, 0.15f, 0.9f);
                    treesA = 8; treesB = 6;
                    Ring(props.bush, 8, r * 0.28f, false, rng, 0.3f);
                    Scatter(props.flower, 80, 2.2f, r * 0.9f, rng);
                    Scatter(props.grass, 200, 1.8f, r * 0.92f, rng);
                    break;
                default:
                    treesA = 22; treesB = 19;
                    Ring(props.rockA, 10, r * 0.62f, false, rng, 0.2f);
                    Ring(props.rockB, 7, r * 0.44f, false, rng, 0.4f);
                    Ring(props.pillar, 6, r * 0.7f, true, rng);
                    Ring(props.mushroom, 9, r * 0.32f, false, rng, 0.3f);
                    Ring(props.bush, 10, r * 0.22f, false, rng, 0.35f);
                    Ring(props.bush, 12, r * 0.64f, false, rng, 0.3f);
                    Scatter(props.grass, 380, 1.6f, r * 0.95f, rng);
                    Scatter(props.flower, 70, 2.2f, r * 0.9f, rng);
                    break;
            }

            // Big trees go in last, anywhere in the arena (centre included), around everything already placed.
            Trees(props.treeA, treesA, 1.0f, 1.3f, rng);
            Trees(props.treeB, treesB, 1.0f, 1.4f, rng);
        }

        /// <summary>Scatters big trees at random spots (they keep clear of the spawn point, the four lanes and other props).</summary>
        void Trees(GameObject prefab, int count, float minScale, float maxScale, System.Random rng)
        {
            if (prefab == null || count <= 0)
                return;
            int placed = 0;
            for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(4.2f, arenaRadius * 0.95f, Mathf.Sqrt((float)rng.NextDouble()));
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                if (!Fits(position, 0.9f * scale))
                    continue;
                var instance = Place(prefab, position, (float)rng.NextDouble() * 360f, scale, true);
                var marker = instance.GetComponent<Obstacle>();
                if (marker != null)
                    marker.footprint = 0.9f * scale;
                placed++;
            }
        }

        void Ring(GameObject prefab, int count, float radius, bool faceCenter, System.Random rng, float jitter = 0f, float scale = 1f)
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count + (float)(rng.NextDouble() - 0.5) * jitter;
                float r = radius + (float)(rng.NextDouble() - 0.5) * jitter * 2f;
                var position = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                float yaw = faceCenter ? -angle * Mathf.Rad2Deg - 90f : (float)rng.NextDouble() * 360f;
                float s = scale * (1f + (float)(rng.NextDouble() - 0.5) * 0.2f);
                Place(prefab, position, yaw, s, radius < arenaRadius);
            }
        }

        void Scatter(GameObject prefab, int count, float minRadius, float maxRadius, System.Random rng)
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)rng.NextDouble()));
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var instance = Place(prefab, position, (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f, false);
                if (!IsSolidProp(prefab.name))
                    scatterInstances.Add(instance);
            }
        }

        GameObject Place(GameObject prefab, Vector3 position, float yaw, float scale, bool blocksSpace)
        {
            var instance = Instantiate(prefab, Root);
            instance.name = prefab.name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
            instance.transform.localScale = prefab.transform.localScale * scale;
            bool solid = IsSolidProp(prefab.name);
            if (solid)
                Solidify(instance, KindFromName(prefab.name));
            if (blocksSpace || solid)
                occupied.Add(position);
            return instance;
        }

        static bool IsSolidProp(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string n = name.ToLowerInvariant();
            return n.Contains("tree") || n.Contains("rock") || n.Contains("pillar") || n.Contains("boulder")
                || n.Contains("boundary") || n.Contains("cactus") || n.Contains("icespike") || n.Contains("spire")
                || n.Contains("obelisk") || n.Contains("arch") || n.Contains("column") || n.Contains("root");
        }

        static Obstacle.Kind KindFromName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("tree") || n.Contains("cactus") || n.Contains("root"))
                return Obstacle.Kind.Tree;
            if (n.Contains("pillar") || n.Contains("ruin") || n.Contains("arch") || n.Contains("column") || n.Contains("obelisk") || n.Contains("spire"))
                return Obstacle.Kind.Ruin;
            return Obstacle.Kind.Rock;
        }

        void Solidify(GameObject instance, Obstacle.Kind kind)
        {
            if (instance == null)
                return;
            SetLayer(instance.transform, Obstacles.Layer);
            if (!HasSolidCollider(instance))
                FitCollider(instance, kind);
            var marker = instance.GetComponent<Obstacle>();
            if (marker == null)
                marker = instance.AddComponent<Obstacle>();
            marker.kind = kind;
            if (marker.footprint < 0.25f)
                marker.footprint = FootprintOf(instance);
            if (!obstacles.Contains(marker))
                obstacles.Add(marker);
        }

        static bool HasSolidCollider(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
            {
                if (collider == null || collider.isTrigger)
                    continue;
                if (collider is CapsuleCollider || collider is BoxCollider || collider is SphereCollider)
                    return true;
            }
            return false;
        }

        static float FootprintOf(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0.8f;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.4f, 1.6f);
        }

        /// <summary>
        /// Tight, upright collider for a generated prop. The model roots carry the FBX axis rotation (local Z = world up), so
        /// a collider added to the root itself lies on its side and sits metres away from the prop; the collider lives on an
        /// unrotated, scale-compensated child instead and is sized from the prop's visible bounds (the trunk, not the canopy).
        /// </summary>
        static void FitCollider(GameObject instance, Obstacle.Kind kind)
        {
            var shape = instance.GetComponent<PropShape>();
            if (shape == null)
                return;
            float uniform = Mathf.Max(0.0001f, instance.transform.lossyScale.x);
            float half = shape.halfWidth * uniform;
            float height = Mathf.Max(0.6f, shape.height * uniform);

            var holder = new GameObject("SolidCollider");
            holder.transform.SetParent(instance.transform, false);
            // Follow only the prop's yaw (not the FBX axis rotation), so boxes keep their thin/wide proportions.
            holder.transform.rotation = Quaternion.Euler(0f, instance.transform.eulerAngles.y, 0f);
            holder.transform.position = instance.transform.position;
            holder.transform.localScale = Vector3.one / uniform;
            holder.layer = instance.layer;
            if (kind == Obstacle.Kind.Tree)
            {
                var capsule = holder.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Clamp(half * 0.14f, 0.3f, 0.5f);
                capsule.height = Mathf.Min(height, 3.2f);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            }
            else if (kind == Obstacle.Kind.Ruin)
            {
                var box = holder.AddComponent<BoxCollider>();
                float width = Mathf.Clamp(shape.halfX * uniform * 2f * 0.85f, 0.45f, 1.5f);
                float depth = Mathf.Clamp(shape.halfZ * uniform * 2f * 0.85f, 0.45f, 1.5f);
                box.size = new Vector3(width, height, depth);
                box.center = new Vector3(0f, height * 0.5f, 0f);
            }
            else
            {
                var sphere = holder.AddComponent<SphereCollider>();
                sphere.radius = Mathf.Clamp(half * 0.85f, 0.38f, 1.15f);
                sphere.center = new Vector3(0f, Mathf.Clamp(height * 0.5f, 0.3f, 0.6f), 0f);
            }
        }

        // ------------------------------------------------------------------ obstacles

        void PlaceObstacles(ArenaTheme theme, System.Random rng)
        {
            if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
                return;
            var allowed = new List<GameObject>();
            foreach (var prefab in obstaclePrefabs)
            {
                if (prefab == null)
                    continue;
                var marker = prefab.GetComponent<Obstacle>();
                if (marker != null && !marker.AllowedIn(theme.biome))
                    continue;
                allowed.Add(prefab);
            }
            if (allowed.Count == 0)
                return;

            int wanted = rng.Next(minObstacles, maxObstacles + 1);
            int placed = 0;
            for (int attempt = 0; attempt < wanted * 14 && placed < wanted; attempt++)
            {
                var prefab = allowed[rng.Next(allowed.Count)];
                var marker = prefab.GetComponent<Obstacle>();
                float footprint = marker != null ? marker.footprint : 1f;
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(3.4f, arenaRadius - 2.2f, (float)rng.NextDouble());
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!Fits(position, footprint))
                    continue;
                float scale = 0.9f + (float)rng.NextDouble() * 0.25f;
                var instance = Place(prefab, position, (float)rng.NextDouble() * 360f, scale, false);
                var obstacle = instance.GetComponent<Obstacle>();
                if (obstacle == null)
                    obstacle = instance.AddComponent<Obstacle>();
                obstacle.footprint = footprint * scale;
                SetLayer(instance.transform, Obstacles.Layer);
                if (!obstacles.Contains(obstacle))
                    obstacles.Add(obstacle);
                placed++;
            }
        }

        bool Fits(Vector3 position, float footprint)
        {
            // Keep the hero's spawn and the centre lane free.
            if (new Vector2(position.x, position.z).magnitude < 3.2f + footprint)
                return false;
            // Keep the four cardinal lanes open so the hero can kite and so movement tests stay honest.
            if (Mathf.Abs(position.x) < 1.6f + footprint * 0.35f || Mathf.Abs(position.z) < 1.6f + footprint * 0.35f)
                return false;
            foreach (var obstacle in obstacles)
            {
                float gap = footprint + obstacle.footprint + 1.6f;
                if ((obstacle.transform.position - position).sqrMagnitude < gap * gap)
                    return false;
            }
            foreach (var point in occupied)
            {
                float gap = footprint + 1.1f;
                if ((point - position).sqrMagnitude < gap * gap)
                    return false;
            }
            return true;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayer(root.GetChild(i), layer);
        }

        /// <summary>Returns true when a disc at the position overlaps a generated obstacle; used by spawners.</summary>
        public bool TouchesObstacle(Vector3 position, float radius)
        {
            foreach (var obstacle in obstacles)
            {
                if (obstacle == null)
                    continue;
                float gap = obstacle.footprint + radius;
                Vector3 delta = obstacle.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < gap * gap)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ biome tint & weather

        Material Tinted(Material source, Color tint)
        {
            var key = (source, tint);
            if (tintedMaterials.TryGetValue(key, out var material) && material != null)
                return material;
            material = new Material(source) { name = source.name + "_tint" };
            Color value = tint;
            if (source.HasProperty("_BaseColor"))
                value *= Color.Lerp(source.GetColor("_BaseColor"), Color.white, 0.6f);
            value.a = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor").a : 1f;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", value);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", value);
            tintedMaterials[key] = material;
            return material;
        }

        void TintFoliage(ArenaTheme theme)
        {
            if (theme.biome == Biome.Forest || Root == null)
                return;
            Color leaf;
            Color grass;
            switch (theme.biome)
            {
                case Biome.Snow:
                    leaf = SnowLeaf; grass = SnowGrass; break;
                case Biome.Desert:
                    leaf = DesertLeaf; grass = DesertGrass; break;
                case Biome.Swamp:
                    leaf = SwampLeaf; grass = SwampLeaf; break;
                case Biome.Thunder:
                    leaf = ThunderLeaf; grass = ThunderLeaf; break;
                case Biome.Royal:
                    leaf = RoyalLeaf; grass = RoyalLeaf; break;
                default:
                    leaf = DesertLeaf; grass = DesertGrass; break;
            }
            foreach (var renderer in Root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                        continue;
                    string name = material.name.ToLowerInvariant();
                    Color? tint = null;
                    if (name.Contains("leaf") || name.Contains("petal"))
                        tint = leaf;
                    else if (name.Contains("grass") || name.Contains("stem") || name.Contains("moss") || name.Contains("bush"))
                        tint = grass;
                    if (tint == null)
                        continue;
                    materials[i] = Tinted(material, tint.Value);
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        void SpawnWeather(ArenaTheme theme)
        {
            GameObject rig = null;
            switch (theme.weather)
            {
                case Weather.Rain: rig = rainRig; break;
                case Weather.Snowfall: rig = snowRig; break;
                case Weather.Heat: rig = dustRig; break;
                case Weather.Ash: rig = ashRig; break;
                case Weather.Spores: rig = sporesRig; break;
                case Weather.Sparks: rig = sparksRig; break;
                default:
                    if (theme.IsNight && (theme.biome == Biome.Forest || theme.biome == Biome.Royal || theme.biome == Biome.Swamp))
                        rig = firefliesRig;
                    break;
            }
            if (rig != null)
            {
                var instance = Instantiate(rig, Root);
                instance.name = rig.name;
                if (instance.GetComponent<WeatherRig>() == null)
                    instance.AddComponent<WeatherRig>();
            }
            SpawnMist(theme);
        }

        public bool HasMist { get; private set; }

        /// <summary>Ground mist for damp or dark scenes, tinted to the fog so it melts into the distance.</summary>
        void SpawnMist(ArenaTheme theme)
        {
            HasMist = false;
            if (mistRig == null)
                return;
            bool damp = theme.biome == Biome.Swamp || theme.biome == Biome.Storm || theme.biome == Biome.Shadow
                || theme.weather == Weather.Rain || (theme.IsNight && theme.biome == Biome.Forest);
            if (!damp)
                return;
            var mist = Instantiate(mistRig, Root);
            mist.name = mistRig.name;
            var particles = mist.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                Color tint = Color.Lerp(RenderSettings.fogColor, Color.white, 0.35f);
                tint.a = theme.biome == Biome.Swamp ? 0.13f : 0.09f;
                main.startColor = tint;
            }
            HasMist = true;
        }
    }
}
