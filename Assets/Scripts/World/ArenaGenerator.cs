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
            // Solid obstacles / rings
            public GameObject bamboo;
            public GameObject dumplingA;
            public GameObject dumplingB;
            public GameObject chopsticks;
            public GameObject sauce;
            public GameObject teacup;
            public GameObject chili;
            public GameObject rockCandy;
            public GameObject crystalCandy;
            // Garnish (not solid)
            public GameObject bokChoy;
            public GameObject scallion;
            public GameObject shiitake;
            public GameObject bambooLeaves;
            public GameObject garnish;
            // Table decor outside the steamer wall
            public GameObject lid;
            public GameObject tier;
            public GameObject chopLying;
        }

        public PropSet props = new PropSet();
        public GameObject[] obstaclePrefabs;
        public Renderer groundRenderer;
        public Renderer tableRenderer;
        /// <summary>Steamer floor (woven bamboo mat) per biome, indexed by <see cref="Biome"/>.</summary>
        public Material[] floorMaterials = new Material[ArenaTheme.BiomeCount];
        /// <summary>Glitter tabletop outside the steamer per biome.</summary>
        public Material[] tableMaterials = new Material[ArenaTheme.BiomeCount];
        public GameObject rainRig;
        public GameObject snowRig;
        public GameObject firefliesRig;
        public GameObject dustRig;
        public GameObject ashRig;
        public GameObject sporesRig;
        public GameObject sparksRig;
        public GameObject mistRig;
        public GameObject sparkleRig;
        public GameObject bokehRig;
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

        /// <summary>Each biome is a dumpling flavour: jelly tint of the mini dumplings, leaf tint for the greens.</summary>
        static readonly Color[] JellyTints =
        {
            new Color(0.70f, 1.00f, 0.78f), // Forest: jasmine / jade
            new Color(1.00f, 0.86f, 0.46f), // Desert: golden custard
            new Color(0.90f, 0.96f, 1.00f), // Snow: coconut
            new Color(0.80f, 0.64f, 1.00f), // Thunder: taro
            new Color(0.62f, 0.86f, 0.46f), // Swamp: matcha
            new Color(0.58f, 0.52f, 0.78f), // Shadow: black sesame
            new Color(1.00f, 0.52f, 0.42f), // Volcano: chili
            new Color(0.58f, 0.68f, 1.00f), // Storm: blueberry
            new Color(1.00f, 0.74f, 0.96f), // Crystal: rock candy
            new Color(1.00f, 0.90f, 0.56f), // Royal: egg yolk
        };

        static readonly Color[] LeafTints =
        {
            Color.white,
            new Color(1.0f, 0.92f, 0.62f),
            new Color(0.84f, 0.96f, 1.0f),
            new Color(0.82f, 0.86f, 0.66f),
            new Color(0.78f, 1.0f, 0.62f),
            new Color(0.66f, 0.62f, 0.86f),
            new Color(1.0f, 0.78f, 0.58f),
            new Color(0.76f, 0.88f, 0.96f),
            new Color(1.0f, 0.86f, 1.0f),
            new Color(0.96f, 1.0f, 0.7f),
        };

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
            PlaceTableDecor(theme, rng);
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
            int index = Mathf.Clamp((int)theme.biome, 0, ArenaTheme.BiomeCount - 1);
            if (groundRenderer != null && floorMaterials != null && index < floorMaterials.Length && floorMaterials[index] != null)
                groundRenderer.sharedMaterial = floorMaterials[index];
            if (tableRenderer != null && tableMaterials != null && index < tableMaterials.Length && tableMaterials[index] != null)
                tableRenderer.sharedMaterial = tableMaterials[index];
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
                    sky = new Color(0.74f, 0.72f, 0.72f); equator = new Color(0.6f, 0.52f, 0.44f); groundAmbient = new Color(0.3f, 0.22f, 0.17f);
                    fog = new Color(0.72f, 0.5f, 0.52f); fogStart = 40f; fogEnd = 95f; background = new Color(0.4f, 0.15f, 0.2f);
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
                sky = new Color(0.22f, 0.25f, 0.44f); equator = new Color(0.16f, 0.18f, 0.32f); groundAmbient = new Color(0.07f, 0.07f, 0.12f);
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
            // Props are glossy toys, not mossy rocks: only the snow flavour (and snowfall) dusts them with powdered sugar.
            Texture2D map = null;
            Color tint = new Color(0.96f, 0.98f, 1f, 0.55f);
            float amount = 0f;
            if (theme.biome == Biome.Snow || theme.weather == Weather.Snowfall)
            {
                map = coverSnow;
                amount = theme.weather == Weather.Snowfall ? 0.7f : 0.45f;
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
            // Every floor is the inside of the same bamboo steamer; the biome (flavour) decides what is on the tray.
            Scatter(props.bambooLeaves, theme.biome == Biome.Snow ? 70 : 120, 1.6f, r * 0.96f, rng);
            switch (theme.biome)
            {
                case Biome.Snow: // coconut + powdered sugar
                    Ring(props.teacup, 8, r * 0.62f, true, rng, 0.25f);
                    Ring(props.rockCandy, 10, r * 0.58f, false, rng, 0.3f, 1.1f);
                    Ring(props.dumplingA, 6, r * 0.44f, true, rng, 0.4f);
                    Ring(props.dumplingB, 10, r * 0.8f, true, rng, 0.2f);
                    Ring(props.chopsticks, 5, r * 0.7f, true, rng, 0.15f);
                    Ring(props.scallion, 8, r * 0.34f, false, rng, 0.4f);
                    break;
                case Biome.Desert: // golden custard + chili
                    Ring(props.chili, 12, r * 0.66f, false, rng, 0.35f);
                    Ring(props.chili, 7, r * 0.38f, false, rng, 0.5f, 0.85f);
                    Ring(props.sauce, 8, r * 0.52f, false, rng, 0.3f);
                    Ring(props.dumplingA, 8, r * 0.78f, true, rng, 0.2f);
                    Ring(props.chopsticks, 8, r * 0.7f, true, rng, 0.12f);
                    Ring(props.bamboo, 7, r * 0.97f, false, rng, 0.15f);
                    Ring(props.scallion, 6, r * 0.3f, false, rng, 0.4f);
                    break;
                case Biome.Thunder: // taro
                    Ring(props.chopsticks, 9, r * 0.68f, true, rng, 0.12f);
                    Ring(props.bamboo, 9, r * 0.95f, false, rng, 0.12f);
                    Ring(props.dumplingA, 6, r * 0.5f, true, rng, 0.3f);
                    Ring(props.dumplingB, 10, r * 0.82f, true, rng, 0.2f);
                    Ring(props.teacup, 6, r * 0.36f, true, rng, 0.4f);
                    Ring(props.bokChoy, 8, r * 0.28f, false, rng, 0.4f);
                    break;
                case Biome.Swamp: // matcha
                    Ring(props.bokChoy, 14, r * 0.66f, false, rng, 0.3f);
                    Ring(props.bokChoy, 8, r * 0.34f, false, rng, 0.4f, 0.9f);
                    Ring(props.scallion, 12, r * 0.5f, false, rng, 0.4f);
                    Ring(props.bamboo, 10, r * 0.95f, false, rng, 0.12f);
                    Ring(props.dumplingA, 6, r * 0.78f, true, rng, 0.25f);
                    Ring(props.scallion, 8, r * 0.42f, false, rng, 0.4f);
                    Ring(props.sauce, 5, r * 0.58f, false, rng, 0.3f);
                    break;
                case Biome.Shadow: // black sesame
                    Ring(props.chopsticks, 10, r * 0.72f, true, rng, 0.12f);
                    Ring(props.dumplingA, 8, r * 0.56f, true, rng, 0.25f);
                    Ring(props.sauce, 8, r * 0.44f, false, rng, 0.3f);
                    Ring(props.teacup, 5, r * 0.82f, true, rng, 0.2f);
                    Ring(props.scallion, 6, r * 0.3f, false, rng, 0.4f);
                    break;
                case Biome.Volcano: // chili oil
                    Ring(props.chili, 14, r * 0.62f, false, rng, 0.3f);
                    Ring(props.sauce, 10, r * 0.46f, false, rng, 0.3f);
                    Ring(props.dumplingA, 8, r * 0.78f, true, rng, 0.2f);
                    Ring(props.chopsticks, 6, r * 0.7f, true, rng, 0.15f);
                    Ring(props.dumplingB, 8, r * 0.3f, true, rng, 0.4f);
                    break;
                case Biome.Storm: // blueberry
                    Ring(props.teacup, 8, r * 0.6f, true, rng, 0.25f);
                    Ring(props.chopsticks, 8, r * 0.7f, true, rng, 0.12f);
                    Ring(props.bamboo, 8, r * 0.95f, false, rng, 0.12f);
                    Ring(props.dumplingA, 6, r * 0.4f, true, rng, 0.35f);
                    Ring(props.dumplingB, 8, r * 0.8f, true, rng, 0.2f);
                    Ring(props.scallion, 6, r * 0.3f, false, rng, 0.4f);
                    break;
                case Biome.Crystal: // rock candy
                    Ring(props.rockCandy, 10, r * 0.7f, false, rng, 0.2f, 1.1f);
                    Ring(props.crystalCandy, 9, r * 0.5f, false, rng, 0.3f);
                    Ring(props.dumplingA, 6, r * 0.8f, true, rng, 0.2f);
                    Ring(props.dumplingB, 8, r * 0.3f, true, rng, 0.4f);
                    Ring(props.teacup, 5, r * 0.58f, true, rng, 0.3f);
                    break;
                case Biome.Royal: // golden egg yolk
                    Ring(props.chopsticks, 10, r * 0.72f, true, rng, 0.08f);
                    Ring(props.chopsticks, 6, r * 0.42f, true, rng, 0.15f);
                    Ring(props.teacup, 8, r * 0.58f, true, rng, 0.2f);
                    Ring(props.dumplingA, 7, r * 0.82f, true, rng, 0.15f);
                    Ring(props.bamboo, 10, r * 0.96f, false, rng, 0.1f);
                    Ring(props.bokChoy, 8, r * 0.28f, false, rng, 0.3f);
                    Ring(props.sauce, 5, r * 0.5f, false, rng, 0.3f);
                    Scatter(props.garnish, 50, 2.2f, r * 0.9f, rng);
                    break;
                default: // Forest: jasmine tea
                    Ring(props.bamboo, 12, r * 0.96f, false, rng, 0.12f);
                    Ring(props.dumplingA, 7, r * 0.6f, true, rng, 0.2f);
                    Ring(props.dumplingB, 8, r * 0.42f, true, rng, 0.4f);
                    Ring(props.chopsticks, 5, r * 0.7f, true, rng, 0.15f);
                    Ring(props.bokChoy, 10, r * 0.66f, false, rng, 0.3f);
                    Ring(props.bokChoy, 8, r * 0.24f, false, rng, 0.4f, 0.85f);
                    Ring(props.dumplingB, 5, r * 0.3f, true, rng, 0.4f);
                    Ring(props.scallion, 9, r * 0.5f, false, rng, 0.4f);
                    Scatter(props.garnish, 40, 2.2f, r * 0.9f, rng);
                    break;
            }
        }

        /// <summary>Giant table decor beyond the steamer wall: a leaning lid, a second steamer, chopsticks and oversized tableware.</summary>
        void PlaceTableDecor(ArenaTheme theme, System.Random rng)
        {
            float wall = arenaRadius + 3.6f;
            void Spot(GameObject prefab, float angleDeg, float radius, float scale, float yaw, bool solid = false)
            {
                if (prefab == null)
                    return;
                float a = angleDeg * Mathf.Deg2Rad;
                var instance = Instantiate(prefab, Root);
                instance.name = prefab.name;
                instance.transform.position = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
                instance.transform.localScale = prefab.transform.localScale * scale;
            }
            float baseAngle = (float)rng.NextDouble() * 360f;
            Spot(props.tier, baseAngle, wall + 12f, 1f, baseAngle + 90f);
            Spot(props.lid, baseAngle + 70f, wall + 12f, 1f, baseAngle + 70f + 180f);
            Spot(props.lid, baseAngle + 215f, wall + 16f, 0.8f, baseAngle + 215f + 180f);
            Spot(props.chopLying, baseAngle + 130f, wall + 8f, 1f, baseAngle + 130f + 90f);
            Spot(props.chopLying, baseAngle + 300f, wall + 11f, 0.8f, baseAngle + 300f + 80f);
            for (int i = 0; i < 6; i++)
                Spot(i % 2 == 0 ? props.dumplingA : props.dumplingB, baseAngle + 20f + i * 58f, wall + 6f + (float)rng.NextDouble() * 22f, 4.2f + (float)rng.NextDouble() * 2.4f, (float)rng.NextDouble() * 360f);
            Spot(props.teacup, baseAngle + 95f, wall + 7f, 4.5f, (float)rng.NextDouble() * 360f);
            Spot(props.teacup, baseAngle + 250f, wall + 13f, 4f, (float)rng.NextDouble() * 360f);
            Spot(props.sauce, baseAngle + 170f, wall + 6f, 4.2f, (float)rng.NextDouble() * 360f);
            Spot(props.sauce, baseAngle + 340f, wall + 9f, 3.6f, (float)rng.NextDouble() * 360f);
            Spot(props.bamboo, baseAngle + 40f, wall + 4f, 2.2f, (float)rng.NextDouble() * 360f);
            Spot(props.bamboo, baseAngle + 195f, wall + 5f, 2.6f, (float)rng.NextDouble() * 360f);
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
            if (n.Contains("leaves") || n.Contains("garnish") || n.Contains("flower"))
                return false;
            return n.Contains("dumpling") || n.Contains("bamboo") || n.Contains("chopsticks") || n.Contains("teacup") || n.Contains("sauce")
                || n.Contains("chili") || n.Contains("icespike") || n.Contains("spire");
        }

        static Obstacle.Kind KindFromName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("bamboo") || n.Contains("chili"))
                return Obstacle.Kind.Tree;
            if (n.Contains("chopsticks") || n.Contains("spire"))
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

        static void FitCollider(GameObject instance, Obstacle.Kind kind)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer)
                    continue;
                bounds.Encapsulate(renderers[i].bounds);
            }
            float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.7f, 0.38f, 1.15f);
            float height = Mathf.Max(0.9f, bounds.size.y);
            if (kind == Obstacle.Kind.Tree)
            {
                var capsule = instance.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, height * 0.48f, 0f);
                capsule.radius = Mathf.Clamp(radius * 0.55f, 0.38f, 0.85f);
                capsule.height = height;
            }
            else if (kind == Obstacle.Kind.Ruin)
            {
                var box = instance.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, height * 0.5f, 0f);
                box.size = new Vector3(radius * 1.5f, height, radius * 1.5f);
            }
            else
            {
                var sphere = instance.AddComponent<SphereCollider>();
                sphere.center = new Vector3(0f, Mathf.Min(0.75f, height * 0.45f), 0f);
                sphere.radius = radius;
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

        /// <summary>Flavour tint: jelly dumplings take the biome's colour, greens get a matching cast.</summary>
        void TintFoliage(ArenaTheme theme)
        {
            if (Root == null)
                return;
            int index = Mathf.Clamp((int)theme.biome, 0, ArenaTheme.BiomeCount - 1);
            Color jelly = JellyTints[index];
            Color leaf = LeafTints[index];
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
                    if (name.Contains("jelly"))
                        tint = jelly;
                    else if ((name.Contains("leaf") || name.Contains("jade") || name.Contains("bamboogreen")) && index != 0)
                        tint = leaf;
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
            SpawnAmbientGlitter();
        }

        /// <summary>Floating glitter glints and soft bokeh lights follow the hero (skipped on the Low preset).</summary>
        void SpawnAmbientGlitter()
        {
            var graphics = GraphicsService.Instance;
            if (graphics != null && graphics.Level == GraphicsService.Low)
                return;
            foreach (var rig in new[] { sparkleRig, bokehRig })
            {
                if (rig == null)
                    continue;
                var instance = Instantiate(rig, Root);
                instance.name = rig.name;
            }
        }

        public bool HasMist { get; private set; }

        /// <summary>Ground mist for damp or dark scenes, tinted to the fog so it melts into the distance.</summary>
        void SpawnMist(ArenaTheme theme)
        {
            HasMist = false;
            if (mistRig == null)
                return;
            var mist = Instantiate(mistRig, Root);
            mist.name = mistRig.name;
            var particles = mist.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                // Steam rising from the tray: warm white, a little denser in damp flavours.
                Color tint = Color.Lerp(RenderSettings.fogColor, new Color(1f, 0.97f, 0.9f), 0.6f);
                tint.a = theme.biome == Biome.Swamp || theme.weather == Weather.Rain ? 0.12f : 0.07f;
                main.startColor = tint;
            }
            HasMist = true;
        }
    }
}
