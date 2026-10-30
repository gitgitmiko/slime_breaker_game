using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    /// <summary>
    /// Runtime post-processing: one global URP Volume (ACES tonemapping, bloom, colour adjustments, white balance,
    /// split toning, vignette) graded per biome and time of day. Disabled on the Low graphics preset.
    /// </summary>
    public class PostFx : MonoBehaviour
    {
        public static PostFx Instance { get; private set; }

        public struct Look
        {
            public float exposure;
            public float contrast;
            public float saturation;
            public float temperature;
            public float tint;
            public float bloom;
            public float bloomThreshold;
            public float vignette;
            public Color shadowsTone;
            public Color highlightsTone;
        }

        /// <summary>Cover used outside the arena (main menu): moss on the showcase props.</summary>
        public Texture2D menuCover;

        public Volume Volume { get; private set; }
        public VolumeProfile Profile { get; private set; }
        public bool Active => Volume != null && Volume.enabled && Volume.weight > 0f;
        public Look Current { get; private set; }

        Tonemapping tonemapping;
        Bloom bloom;
        ColorAdjustments colorAdjustments;
        WhiteBalance whiteBalance;
        SplitToning splitToning;
        Vignette vignette;
        bool lowQuality;

        public static readonly Look Menu = new Look
        {
            exposure = 0.25f, contrast = 12f, saturation = 6f, temperature = 4f, tint = 0f,
            bloom = 0.55f, bloomThreshold = 0.95f, vignette = 0.24f,
            shadowsTone = new Color(0.45f, 0.48f, 0.62f), highlightsTone = new Color(0.62f, 0.56f, 0.46f)
        };

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Build();
            Apply(Menu);
            if (menuCover != null && FindAnyObjectByType<ArenaGenerator>() == null)
                ArenaGenerator.SetCover(menuCover, new Color(0.27f, 0.35f, 0.13f, 0.18f), 0.36f);
            var graphics = GraphicsService.Instance;
            SetQuality(graphics != null ? graphics.Level : GraphicsService.High);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (Profile != null)
                Destroy(Profile);
        }

        void Build()
        {
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.name = "SlimeBreakerPostFx";
            tonemapping = Profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            bloom = Profile.Add<Bloom>(true);
            bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(false);
            // Quarter-res dual filtering: the same soft glow at a fraction of the bandwidth on mobile GPUs.
            bloom.filter.Override(BloomFilterMode.Dual);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            // Cap the brightness bloom may pick up, so a single over-bright pixel cannot blow up into a white blob.
            bloom.clamp.Override(24f);
            colorAdjustments = Profile.Add<ColorAdjustments>(true);
            whiteBalance = Profile.Add<WhiteBalance>(true);
            splitToning = Profile.Add<SplitToning>(true);
            splitToning.balance.Override(-12f);
            vignette = Profile.Add<Vignette>(true);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.02f, 0.02f, 0.05f));
            Volume = gameObject.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 10f;
            Volume.sharedProfile = Profile;
        }

        public void Apply(Look look)
        {
            Current = look;
            if (Profile == null)
                return;
            colorAdjustments.postExposure.Override(look.exposure);
            colorAdjustments.contrast.Override(look.contrast);
            colorAdjustments.saturation.Override(look.saturation);
            whiteBalance.temperature.Override(look.temperature);
            whiteBalance.tint.Override(look.tint);
            bloom.intensity.Override(look.bloom);
            bloom.threshold.Override(look.bloomThreshold);
            vignette.intensity.Override(look.vignette);
            splitToning.shadows.Override(look.shadowsTone);
            splitToning.highlights.Override(look.highlightsTone);
        }

        /// <summary>Grade for an arena theme: biome sets the base, night and weather shift it.</summary>
        public static Look ForTheme(ArenaTheme theme)
        {
            var look = new Look
            {
                exposure = 0.1f, contrast = 14f, saturation = 6f, temperature = 4f, tint = 0f,
                bloom = 0.5f, bloomThreshold = 1.0f, vignette = 0.22f,
                shadowsTone = new Color(0.46f, 0.5f, 0.58f), highlightsTone = new Color(0.6f, 0.56f, 0.48f)
            };
            switch (theme.biome)
            {
                case Biome.Desert:
                    look.temperature = 10f; look.saturation = -6f; look.contrast = 14f; look.exposure = 0.0f; look.bloom = 0.5f;
                    look.highlightsTone = new Color(0.7f, 0.58f, 0.42f);
                    break;
                case Biome.Snow:
                    look.temperature = -8f; look.saturation = -4f; look.contrast = 12f; look.exposure = -0.25f; look.bloom = 0.5f;
                    look.shadowsTone = new Color(0.4f, 0.48f, 0.66f);
                    break;
                case Biome.Thunder:
                    look.temperature = -6f; look.saturation = -12f; look.contrast = 16f; look.exposure = 0.25f; look.bloom = 0.7f;
                    break;
                case Biome.Swamp:
                    look.tint = 8f; look.saturation = -6f; look.contrast = 12f; look.exposure = 0.3f;
                    look.shadowsTone = new Color(0.42f, 0.52f, 0.42f);
                    break;
                case Biome.Shadow:
                    look.temperature = -10f; look.tint = -12f; look.saturation = -4f; look.contrast = 16f; look.exposure = 0.45f; look.bloom = 1.1f;
                    look.bloomThreshold = 0.85f; look.vignette = 0.32f; look.shadowsTone = new Color(0.46f, 0.4f, 0.62f);
                    break;
                case Biome.Volcano:
                    look.temperature = 22f; look.saturation = 8f; look.contrast = 18f; look.exposure = 0.22f; look.bloom = 1.2f;
                    look.bloomThreshold = 0.85f; look.vignette = 0.3f; look.highlightsTone = new Color(0.72f, 0.5f, 0.36f);
                    break;
                case Biome.Storm:
                    look.temperature = -12f; look.saturation = -14f; look.contrast = 14f; look.exposure = 0.3f;
                    break;
                case Biome.Crystal:
                    look.temperature = -6f; look.tint = -18f; look.saturation = 14f; look.contrast = 14f; look.bloom = 1.0f; look.bloomThreshold = 0.85f;
                    look.shadowsTone = new Color(0.5f, 0.42f, 0.66f);
                    break;
                case Biome.Royal:
                    look.temperature = 8f; look.saturation = 4f; look.contrast = 12f; look.exposure = 0.0f;
                    look.highlightsTone = new Color(0.68f, 0.6f, 0.46f);
                    break;
            }
            switch (theme.weather)
            {
                case Weather.Rain:
                    look.saturation -= 14f; look.temperature -= 6f; look.contrast += 4f;
                    break;
                case Weather.Heat:
                    look.temperature += 8f; look.exposure += 0.1f; look.bloom += 0.2f;
                    break;
                case Weather.Ash:
                    look.saturation -= 10f; look.temperature += 6f;
                    break;
                case Weather.Snowfall:
                    look.saturation -= 6f; look.exposure += 0.05f;
                    break;
            }
            if (theme.IsNight)
            {
                look.temperature -= 14f;
                look.saturation -= 12f;
                look.exposure += 0.15f;
                look.bloom += 0.35f;
                look.bloomThreshold = Mathf.Min(look.bloomThreshold, 0.8f);
                look.vignette += 0.12f;
                look.shadowsTone = new Color(0.4f, 0.44f, 0.66f);
            }
            return look;
        }

        public void ApplyTheme(ArenaTheme theme) => Apply(ForTheme(theme));

        /// <summary>Low turns post-processing off (and the cheap surface paths on); Medium drops bloom; High keeps everything.</summary>
        public void SetQuality(int level)
        {
            lowQuality = level == GraphicsService.Low;
            if (Volume != null)
                Volume.enabled = !lowQuality;
            if (bloom != null)
                bloom.active = level == GraphicsService.High;
            foreach (var camera in Camera.allCameras)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = !lowQuality;
            }
        }
    }
}
