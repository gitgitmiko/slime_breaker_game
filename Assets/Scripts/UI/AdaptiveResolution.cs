using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    /// <summary>
    /// Holds the frame rate of the current graphics level by trading render resolution: when frames run long the URP render
    /// scale drops in small steps; when there is headroom it climbs back. The level also caps the internal resolution
    /// (<see cref="GraphicsProfile.maxRenderHeight"/>), since extra pixels are barely visible at phone size but cost fill rate. Inactive in the editor so
    /// playtests and the shared URP assets stay deterministic.
    /// </summary>
    public class AdaptiveResolution : MonoBehaviour
    {
        public static AdaptiveResolution Instance { get; private set; }

        [Range(0.5f, 1f)] public float minScale = 0.55f;
        public bool runInEditor;

        public float Scale { get; private set; } = 1f;

        UniversalRenderPipelineAsset bound;
        float originalScale = 1f;
        float average = 1f / 60f;
        float timer;
        float upBlockedUntil;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            Unbind();
            if (Instance == this)
                Instance = null;
        }

        void OnApplicationQuit()
        {
            Unbind();
        }

        bool Active => runInEditor || !Application.isEditor;

        float Ceiling => GraphicsService.Current.RenderScale(Mathf.Min(Screen.width, Screen.height), minScale);

        /// <summary>Called when the graphics preset changes: forget the old asset and start from the ceiling.</summary>
        public void ResetScale()
        {
            Unbind();
            timer = 0f;
            upBlockedUntil = Time.unscaledTime + 2f;
        }

        void Bind(UniversalRenderPipelineAsset asset)
        {
            Unbind();
            bound = asset;
            if (bound == null)
                return;
            originalScale = bound.renderScale;
            Scale = Mathf.Min(originalScale, Ceiling);
            bound.renderScale = Scale;
        }

        void Unbind()
        {
            if (bound != null)
                bound.renderScale = originalScale;
            bound = null;
        }

        void Update()
        {
            if (!Active)
                return;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != bound)
                Bind(asset);
            if (bound == null)
                return;

            float dt = Time.unscaledDeltaTime;
            // Loading hitches and paused/backgrounded frames say nothing about steady-state cost.
            if (dt <= 0f || dt > 0.25f || Time.timeScale == 0f)
                return;
            average = Mathf.Lerp(average, dt, 0.08f);
            timer += dt;
            if (timer < 0.75f)
                return;
            timer = 0f;

            float budget = 1f / GraphicsService.Current.targetFps;
            float next = Scale;
            if (average > budget * 1.1f)
            {
                // Over budget: step down harder the further behind we are, and don't try going up again for a while.
                next -= average > budget * 1.35f ? 0.1f : 0.05f;
                upBlockedUntil = Time.unscaledTime + 6f;
            }
            else if (average < budget * 1.02f && Time.unscaledTime > upBlockedUntil)
            {
                next += 0.025f;
            }
            next = Mathf.Clamp(next, minScale, Ceiling);
            if (Mathf.Abs(next - Scale) < 0.001f)
                return;
            Scale = next;
            bound.renderScale = Scale;
        }
    }
}
