using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class EnemyHealth : MonoBehaviour
    {
        public static readonly List<EnemyHealth> Active = new List<EnemyHealth>();
        public static event Action<EnemyHealth> AnyDied;

        static readonly Color SlowTint = new Color(0.55f, 0.8f, 1f);
        static readonly Color BurnTint = new Color(1f, 0.55f, 0.3f);
        static MaterialPropertyBlock block;

        public EnemyDefinition definition;
        public GameObject xpOrbPrefab;
        public int Current { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsBoss => definition != null && definition.isBoss;

        /// <summary>Movement multiplier applied by chase/boss AI. 1 = normal, lower = slowed.</summary>
        public float SpeedMultiplier => slowTimer > 0f ? slowFactor : 1f;
        public bool IsSlowed => slowTimer > 0f;
        public bool IsBurning => burnTicks > 0;

        /// <summary>Approximate body centre height for projectiles and orbiting shards.</summary>
        public float HitHeight { get; private set; } = 0.5f;
        /// <summary>Horizontal body radius used to widen contact checks.</summary>
        public float HitRadius { get; private set; } = 0.45f;

        Animator animator;
        Renderer[] renderers;
        float slowTimer;
        float slowFactor = 1f;
        int burnTicks;
        int burnDamage;
        float burnInterval;
        float burnTimer;
        bool tinted;
        float flashTimer;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            Current = definition != null ? definition.maxHealth : 20;
            renderers = GetComponentsInChildren<Renderer>();
            MeasureBody();
        }

        void MeasureBody()
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled)
                    continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (!any)
                return;
            HitHeight = Mathf.Max(0.2f, bounds.center.y - transform.position.y);
            HitRadius = Mathf.Max(0.25f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        void Update()
        {
            if (IsDead)
                return;
            float dt = Time.deltaTime;
            if (slowTimer > 0f)
                slowTimer -= dt;
            if (burnTicks > 0)
            {
                burnTimer -= dt;
                if (burnTimer <= 0f)
                {
                    burnTicks--;
                    burnTimer = burnInterval;
                    VfxPlayer.Instance?.Play("burn", transform.position + Vector3.up * HitHeight, BurnTint);
                    TakeDamage(burnDamage, playHitSound: false, flash: false);
                    if (IsDead)
                        return;
                }
            }
            if (flashTimer > 0f)
                flashTimer -= dt;
            RefreshTint();
        }

        public void ApplySlow(float factor, float seconds)
        {
            if (IsDead)
                return;
            factor = Mathf.Clamp(factor, 0.1f, 1f);
            // Bosses shrug off most of the slow so the fight keeps its rhythm.
            if (IsBoss)
                factor = Mathf.Lerp(factor, 1f, 0.5f);
            if (slowTimer <= 0f || factor <= slowFactor)
                slowFactor = factor;
            slowTimer = Mathf.Max(slowTimer, seconds);
        }

        public void ApplyBurn(int damagePerTick, int ticks, float interval)
        {
            if (IsDead || ticks <= 0 || damagePerTick <= 0)
                return;
            burnDamage = Mathf.Max(burnDamage, damagePerTick);
            burnTicks = Mathf.Max(burnTicks, ticks);
            burnInterval = Mathf.Max(0.1f, interval);
            if (burnTimer <= 0f)
                burnTimer = burnInterval;
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, true, true);
        }

        public void TakeDamage(int amount, bool playHitSound, bool flash = true)
        {
            if (IsDead || amount <= 0)
                return;
            Current = Mathf.Max(0, Current - amount);
            if (flash)
            {
                flashTimer = 0.09f;
                if (animator != null && !IsBoss)
                    animator.SetTrigger("Hit");
            }
            if (playHitSound)
                AudioService.Instance?.Play("hit");
            if (Current > 0)
                return;
            Die();
        }

        void Die()
        {
            IsDead = true;
            slowTimer = 0f;
            burnTicks = 0;
            RefreshTint();
            if (animator != null)
            {
                animator.ResetTrigger("Hit");
                animator.SetTrigger("Death");
            }
            AudioService.Instance?.Play(IsBoss ? "slam" : "enemydeath");
            VfxPlayer.Instance?.Play("death", transform.position + Vector3.up * HitHeight, BodyColor());
            AnyDied?.Invoke(this);
            if (xpOrbPrefab != null && definition != null && definition.xp > 0)
            {
                int orbs = definition.xp >= 6 ? 2 : 1;
                for (int i = 0; i < orbs; i++)
                {
                    Vector3 scatter = orbs > 1 ? UnityEngine.Random.insideUnitSphere * 0.35f : Vector3.zero;
                    scatter.y = 0f;
                    XPOrb.Spawn(xpOrbPrefab, transform.position + scatter + Vector3.up * 0.45f, Mathf.Max(1, definition.xp / orbs));
                }
            }
            var chase = GetComponent<EnemyChaseAI>();
            if (chase != null)
                chase.enabled = false;
            var boss = GetComponent<BossController>();
            if (boss != null)
                boss.enabled = false;
            Destroy(gameObject, IsBoss ? 1.1f : 0.6f);
        }

        Color BodyColor()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || renderer.sharedMaterial == null)
                    continue;
                var material = renderer.sharedMaterial;
                if (material.HasProperty("_BaseColor"))
                    return material.GetColor("_BaseColor");
                if (material.HasProperty("_Color"))
                    return material.GetColor("_Color");
            }
            return new Color(0.4f, 0.9f, 0.5f);
        }

        void RefreshTint()
        {
            bool wants = slowTimer > 0f || burnTicks > 0 || flashTimer > 0f;
            if (!wants && !tinted)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            Color emission = Color.black;
            if (flashTimer > 0f)
                emission = Color.white * 0.4f;
            else if (burnTicks > 0)
                emission = BurnTint * 0.5f;
            else if (slowTimer > 0f)
                emission = SlowTint * 0.35f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;
                if (!wants)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                renderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", emission);
                renderer.SetPropertyBlock(block);
            }
            tinted = wants;
        }
    }
}
