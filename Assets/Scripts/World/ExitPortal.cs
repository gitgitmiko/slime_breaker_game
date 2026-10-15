using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Glowing gate that appears after an arena is cleared; walking into it moves the run to the next arena.</summary>
    public class ExitPortal : MonoBehaviour
    {
        public static ExitPortal Active { get; private set; }

        public Transform frame;
        public Transform core;
        public Light glow;
        public float enterRadius = 1.35f;
        public float spin = 40f;
        public Color color = new Color(0.3f, 0.9f, 1f);

        float age;
        bool used;
        Vector3 frameScale;
        MaterialPropertyBlock block;

        void OnEnable()
        {
            Active = this;
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        void Start()
        {
            frameScale = frame != null ? frame.localScale : Vector3.one;
            block = new MaterialPropertyBlock();
            Tint();
            var hero = PlayerHealth.Instance;
            if (hero != null)
            {
                Vector3 to = hero.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }

        void Tint()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * 1.5f);
                renderer.SetPropertyBlock(block);
            }
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.startColor = color;
            }
            if (glow != null)
                glow.color = color;
        }

        void Update()
        {
            age += Time.deltaTime;
            float pop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.6f));
            if (frame != null)
            {
                frame.localScale = frameScale * pop * (1f + Mathf.Sin(age * 3f) * 0.03f);
                frame.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
            }
            if (core != null)
                core.Rotate(0f, 0f, -spin * 2.2f * Time.deltaTime, Space.Self);
            if (glow != null)
                glow.intensity = 1.6f * pop + Mathf.Sin(age * 6f) * 0.25f;

            if (used || !GameFlowController.IsPlaying || age < 0.5f)
                return;
            var hero = PlayerHealth.Instance;
            if (hero == null || hero.IsDead)
                return;
            Vector3 flat = hero.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= enterRadius * enterRadius)
                Enter();
        }

        public void Enter()
        {
            if (used)
                return;
            used = true;
            AudioService.Instance?.Play("portal");
            VfxPlayer.Instance?.Play("powerup", transform.position + Vector3.up * 1f, color);
            var stage = StageManager.Instance;
            if (stage != null)
                stage.EnterPortal(this);
            else
                Destroy(gameObject);
        }
    }
}
