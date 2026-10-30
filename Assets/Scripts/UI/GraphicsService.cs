using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeBreaker
{
    /// <summary>What one graphics level costs: frame cap, internal resolution and how much the arena holds.</summary>
    public struct GraphicsProfile
    {
        public int targetFps;
        /// <summary>Cap on the short side of the render target, in pixels.</summary>
        public float maxRenderHeight;
        public float treeDensity;
        public float propDensity;
        public float scatterDensity;
        public float weatherDensity;
        public bool mist;
        /// <summary>Half-resolution textures (saves memory and GPU bandwidth).</summary>
        public int textureMipLimit;

        /// <summary>URP render scale that holds <see cref="maxRenderHeight"/> on a screen with this short side.</summary>
        public float RenderScale(float screenShortSide, float minScale)
        {
            return screenShortSide <= 0f ? 1f : Mathf.Clamp(maxRenderHeight / screenShortSide, minScale, 1f);
        }
    }

    /// <summary>Applies Low / Medium / High presets and remembers the choice in the meta save.</summary>
    public class GraphicsService : MonoBehaviour
    {
        public static GraphicsService Instance { get; private set; }

        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;

        public RenderPipelineAsset[] presets = new RenderPipelineAsset[3];

        public int Level { get; private set; } = High;

        /// <summary>The profile in use; without a service (tools, tests) the High numbers apply.</summary>
        public static GraphicsProfile Current => For(Instance != null ? Instance.Level : High);

        public static GraphicsProfile For(int level)
        {
            switch (level)
            {
                case Low:
                    return new GraphicsProfile
                    {
                        targetFps = 30, maxRenderHeight = 720f, treeDensity = 0.35f, propDensity = 0.5f, scatterDensity = 0.25f,
                        weatherDensity = 0.4f, mist = false, textureMipLimit = 1
                    };
                case Medium:
                    return new GraphicsProfile
                    {
                        targetFps = 30, maxRenderHeight = 900f, treeDensity = 0.6f, propDensity = 0.75f, scatterDensity = 0.6f,
                        weatherDensity = 0.7f, mist = true, textureMipLimit = 0
                    };
                default:
                    return new GraphicsProfile
                    {
                        targetFps = 60, maxRenderHeight = 1080f, treeDensity = 0.8f, propDensity = 1f, scatterDensity = 1f,
                        weatherDensity = 1f, mist = true, textureMipLimit = 0
                    };
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            var save = SaveService.Load();
            Level = Mathf.Clamp(save.graphicsLevel, Low, High);
            Apply(Level, persist: false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Apply(int level, bool persist = true)
        {
            int previous = Level;
            Level = Mathf.Clamp(level, Low, High);
            // The quality levels list the URP presets, which is also what keeps their shader variants in the build.
            if (QualitySettings.names.Length > Level)
                QualitySettings.SetQualityLevel(Level, true);
            var pipeline = presets != null && Level < presets.Length ? presets[Level] : null;
            if (pipeline != null)
                QualitySettings.renderPipeline = pipeline;
            var profile = For(Level);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = profile.targetFps;
            QualitySettings.globalTextureMipmapLimit = profile.textureMipLimit;
            // Cheap surface paths (no normal maps or biome cover) and no post-processing on Low.
            if (Level == Low)
                Shader.EnableKeyword("SB_QUALITY_LOW");
            else
                Shader.DisableKeyword("SB_QUALITY_LOW");
            PostFx.Instance?.SetQuality(Level);
            AdaptiveResolution.Instance?.ResetScale();
            if (Level != previous)
                RebuildArenas();
            if (!persist)
                return;
            var save = SaveService.Load();
            save.graphicsLevel = Level;
            SaveService.Save(save);
        }

        /// <summary>Trees, ground cover and weather come from the generator, so a new level needs the arena placed again.</summary>
        static void RebuildArenas()
        {
            foreach (var generator in FindObjectsByType<ArenaGenerator>(FindObjectsSortMode.None))
            {
                if (generator.Generations > 0)
                    generator.Generate(generator.Current);
            }
        }

        public static string Label(int level)
        {
            return level switch
            {
                Low => "Low",
                Medium => "Medium",
                _ => "High"
            };
        }
    }
}
