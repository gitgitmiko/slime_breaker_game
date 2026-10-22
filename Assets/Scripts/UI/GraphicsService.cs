using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeBreaker
{
    /// <summary>Applies Low / Medium / High URP presets and remembers the choice in the meta save.</summary>
    public class GraphicsService : MonoBehaviour
    {
        public static GraphicsService Instance { get; private set; }

        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;

        public RenderPipelineAsset[] presets = new RenderPipelineAsset[3];

        public int Level { get; private set; } = High;

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
            Level = Mathf.Clamp(level, Low, High);
            var pipeline = presets != null && Level < presets.Length ? presets[Level] : null;
            if (pipeline != null)
                QualitySettings.renderPipeline = pipeline;
            // Cheap ground path (one texture sample set) and no post-processing on Low.
            if (Level == Low)
                Shader.EnableKeyword("SB_QUALITY_LOW");
            else
                Shader.DisableKeyword("SB_QUALITY_LOW");
            PostFx.Instance?.SetQuality(Level);
            AdaptiveResolution.Instance?.ResetScale();
            if (!persist)
                return;
            var save = SaveService.Load();
            save.graphicsLevel = Level;
            SaveService.Save(save);
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
