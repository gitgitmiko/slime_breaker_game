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
        }

        public PropSet props = new PropSet();
        public GameObject[] obstaclePrefabs;
        public Renderer groundRenderer;
        public Material forestGround;
        public Material snowGround;
        public Material sandGround;
        public GameObject rainRig;
        public GameObject snowRig;
        public GameObject firefliesRig;
        public GameObject dustRig;
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

        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Vector3> occupied = new List<Vector3>();
        Light heroLight;
        MaterialPropertyBlock block;

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
            PlaceProps(theme, rng);
            PlaceObstacles(theme, rng);
            TintFoliage(theme);
            SpawnWeather(theme);
            if (theme.IsNight)
                EnsureHeroLight();
            StaticBatchingUtility.Combine(rootObject);
        }

        void Clear()
        {
            obstacles.Clear();
            occupied.Clear();
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
            }

            if (theme.IsNight)
            {
                sunColor = new Color(0.45f, 0.55f, 0.95f); intensity = 0.42f; euler = new Vector3(38f, 150f, 0f);
                sky = new Color(0.12f, 0.16f, 0.3f); equator = new Color(0.08f, 0.1f, 0.2f); groundAmbient = new Color(0.03f, 0.03f, 0.06f);
                fog = new Color(0.05f, 0.07f, 0.14f); fogStart = 12f; fogEnd = 38f; background = new Color(0.04f, 0.06f, 0.12f);
                if (theme.weather == Weather.Rain)
                {
                    fogStart = 10f; fogEnd = 32f;
                }
            }

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
            Place(props.runeRing, Vector3.zero, 0f, r / 11.2f, false);
            Ring(props.boundaryStone, 24, r + 1.2f, true, rng);
            switch (theme.biome)
            {
                case Biome.Snow:
                    Ring(props.treeA, 10, r + 2.6f, false, rng);
                    Ring(props.treeB, 9, r + 4.2f, false, rng, 0.5f);
                    Ring(props.treeA, 12, r + 8f, false, rng, 0.25f, 1.2f);
                    Ring(props.treeB, 10, r + 11f, false, rng, 0.1f, 1.35f);
                    Ring(props.rockA, 12, r * 0.62f, false, rng, 0.2f, 1.1f);
                    Ring(props.rockB, 8, r * 0.44f, false, rng, 0.4f);
                    Ring(props.pillar, 6, r * 0.7f, true, rng);
                    Ring(props.bush, 8, r * 0.64f, false, rng, 0.3f);
                    Scatter(props.grass, 28, 2.4f, r * 0.85f, rng);
                    break;
                case Biome.Desert:
                    Ring(props.treeA, 6, r + 3.2f, false, rng, 0.6f, 1.1f);
                    Ring(props.treeB, 7, r + 7f, false, rng, 0.4f, 1.3f);
                    Ring(props.rockA, 14, r * 0.62f, false, rng, 0.25f, 1.15f);
                    Ring(props.rockB, 10, r * 0.45f, false, rng, 0.45f);
                    Ring(props.rockA, 10, r * 0.82f, false, rng, 0.3f, 1.6f);
                    Ring(props.pillar, 8, r * 0.7f, true, rng, 0.1f);
                    Ring(props.pillar, 5, r * 0.42f, true, rng, 0.2f, 0.8f);
                    Ring(props.bush, 6, r * 0.24f, false, rng, 0.4f, 0.8f);
                    Scatter(props.grass, 14, 3f, r * 0.8f, rng);
                    break;
                default:
                    Ring(props.treeA, 10, r + 2.6f, false, rng);
                    Ring(props.treeB, 9, r + 4.2f, false, rng, 0.5f);
                    Ring(props.treeA, 12, r + 8f, false, rng, 0.25f, 1.2f);
                    Ring(props.treeB, 10, r + 11f, false, rng, 0.1f, 1.35f);
                    Ring(props.rockA, 10, r * 0.62f, false, rng, 0.2f);
                    Ring(props.rockB, 7, r * 0.44f, false, rng, 0.4f);
                    Ring(props.pillar, 6, r * 0.7f, true, rng);
                    Ring(props.mushroom, 9, r * 0.32f, false, rng, 0.3f);
                    Ring(props.bush, 10, r * 0.22f, false, rng, 0.35f);
                    Ring(props.bush, 12, r * 0.64f, false, rng, 0.3f);
                    Scatter(props.grass, 80, 2.2f, r * 0.9f, rng);
                    Scatter(props.flower, 28, 2.6f, r * 0.85f, rng);
                    break;
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
                Place(prefab, position, (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f, false);
            }
        }

        GameObject Place(GameObject prefab, Vector3 position, float yaw, float scale, bool blocksSpace)
        {
            var instance = Instantiate(prefab, Root);
            instance.name = prefab.name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
            instance.transform.localScale = prefab.transform.localScale * scale;
            if (blocksSpace)
                occupied.Add(position);
            return instance;
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

        void TintFoliage(ArenaTheme theme)
        {
            if (theme.biome == Biome.Forest || Root == null)
                return;
            Color leaf = theme.biome == Biome.Snow ? SnowLeaf : DesertLeaf;
            Color grass = theme.biome == Biome.Snow ? SnowGrass : DesertGrass;
            if (block == null)
                block = new MaterialPropertyBlock();
            foreach (var renderer in Root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                var materials = renderer.sharedMaterials;
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
                    Color value = tint.Value;
                    if (material.HasProperty("_BaseColor"))
                        value *= Color.Lerp(material.GetColor("_BaseColor"), Color.white, 0.6f);
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", value);
                    block.SetColor("_Color", value);
                    renderer.SetPropertyBlock(block, i);
                }
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
                default:
                    if (theme.IsNight && theme.biome == Biome.Forest)
                        rig = firefliesRig;
                    break;
            }
            if (rig == null)
                return;
            var instance = Instantiate(rig, Root);
            instance.name = rig.name;
            if (instance.GetComponent<WeatherRig>() == null)
                instance.AddComponent<WeatherRig>();
        }
    }
}
