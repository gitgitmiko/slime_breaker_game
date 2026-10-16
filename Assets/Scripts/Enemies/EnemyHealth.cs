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
        public int Max { get; private set; }
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
        /// <summary>Contact damage after per-stage scaling.</summary>
        public int ContactDamage { get; private set; }
        int extraDamage;
        float immuneShownAt = -10f;

        /// <summary>Bosses shrug off power-up skills unless the hero holds Boss Breaker; basic attacks always land.</summary>
        public bool ImmuneToSkills => IsBoss && !SkillController.BossBreakerActive;

        /// <summary>Damage from a power-up skill. Returns false (and flashes IMMUNE) when the target ignores skills.</summary>
        public bool TakeSkillDamage(int amount, bool crit)
        {
            if (IsDead)
                return false;
            if (ImmuneToSkills)
            {
                ShowImmune();
                return false;
            }
            TakeDamage(amount, false, true, crit);
            return true;
        }

        public void ShowImmune()
        {
            if (Time.time - immuneShownAt < 0.8f)
                return;
            immuneShownAt = Time.time;
            DamagePopup.Show(transform.position + Vector3.up * (HitHeight + 0.6f), 1, DamagePopup.Kind.Immune);
        }

        Animator animator;
        Renderer[] renderers;
        BossController cachedBoss;
        float slowTimer;
        float slowFactor = 1f;
        int burnTicks;
        int burnDamage;
        float burnInterval;
        float burnTimer;
        bool tinted;
        float flashTimer;
        Color bodyTint = Color.white;
        bool hasBodyTint;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>();
            cachedBoss = GetComponent<BossController>();
            Bind(definition);
            MeasureBody();
            EnemyHealthBar.Attach(this);
        }

        public void Bind(EnemyDefinition def)
        {
            definition = def;
            Max = def != null ? def.maxHealth : 20;
            Current = Max;
            ContactDamage = def != null ? def.contactDamage : 8;
            extraDamage = 0;
            if (def != null && def.tint != Color.white)
                TintBody(def.tint);
        }

        public void SetMaxHealth(int max)
        {
            Max = Mathf.Max(1, max);
            Current = Max;
        }

        public void SetContactDamage(int damage)
        {
            ContactDamage = Mathf.Max(1, damage);
        }

        void MeasureBody()
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled || IsBarRenderer(renderer))
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

        /// <summary>Raises health and contact damage every floor. Floor 1 is the authored baseline.</summary>
        public void Scale(int floor)
        {
            if (definition == null)
                return;
            floor = Mathf.Max(1, floor);
            Current = RunRules.SlimeMaxHealth(definition.maxHealth, floor);
            Max = Current;
            extraDamage = Mathf.Max(0, floor - 1) * RunRules.SlimeDamagePerFloor;
            ContactDamage = RunRules.SlimeContactDamage(definition.contactDamage, floor);
        }

        public void TintBody(Color color)
        {
            bodyTint = color;
            hasBodyTint = true;
            tinted = true;
            RefreshTint();
        }

        /// <summary>Removes the enemy without XP, death VFX, or notifying the run (used when an arena is cleared).</summary>
        public void Vanish()
        {
            if (IsDead)
            {
                Destroy(gameObject);
                return;
            }
            IsDead = true;
            slowTimer = 0f;
            burnTicks = 0;
            var chase = GetComponent<EnemyChaseAI>();
            if (chase != null)
                chase.enabled = false;
            var boss = GetComponent<BossController>();
            if (boss != null)
                boss.enabled = false;
            Destroy(gameObject);
        }

        public static void VanishAll()
        {
            var snapshot = Active.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                    snapshot[i].Vanish();
            }
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, true, true, false);
        }

        public void TakeDamage(int amount, bool playHitSound, bool flash = true)
        {
            TakeDamage(amount, playHitSound, flash, false);
        }

        public void TakeDamage(int amount, bool playHitSound, bool flash, bool crit)
        {
            if (IsDead || amount <= 0)
                return;
            if (definition != null && definition.incomingDamageScale > 0f && definition.incomingDamageScale < 0.999f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * definition.incomingDamageScale));
            Current = Mathf.Max(0, Current - amount);
            DamagePopup.Show(transform.position + Vector3.up * (HitHeight + 0.35f), amount,
                crit ? DamagePopup.Kind.Crit : DamagePopup.Kind.Enemy);
            if ((playHitSound || flash) && amount > 0)
            {
                var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
                if (stats != null && stats.lifesteal > 0f)
                    PlayerHealth.Instance?.Lifesteal(Mathf.Max(1, Mathf.RoundToInt(amount * stats.lifesteal)));
            }
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
            if (renderers == null)
                return;
            bool wants = slowTimer > 0f || burnTicks > 0 || flashTimer > 0f || hasBodyTint;
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
            var bossKind = definition != null ? definition.bossKind : BossKind.None;
            if (bossKind == BossKind.None && cachedBoss != null)
                bossKind = cachedBoss.kind;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || IsBarRenderer(renderer))
                    continue;
                if (!wants)
                {
                    renderer.SetPropertyBlock(null);
                    int extra = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
                    for (int m = 0; m < extra; m++)
                        renderer.SetPropertyBlock(null, m);
                    continue;
                }
                var materials = renderer.sharedMaterials;
                int count = materials != null && materials.Length > 0 ? materials.Length : 1;
                for (int m = 0; m < count; m++)
                {
                    renderer.GetPropertyBlock(block, m);
                    if (hasBodyTint)
                    {
                        var mat = materials != null && m < materials.Length ? materials[m] : renderer.sharedMaterial;
                        string matName = mat != null ? mat.name : "";
                        Color part = bossKind != BossKind.None
                            ? BossCatalog.ColorForMaterial(matName, bossKind, bodyTint)
                            : FaceSafeBody(matName, bodyTint);
                        if (part.a > 0.01f)
                        {
                            block.SetColor("_BaseColor", part);
                            block.SetColor("_Color", part);
                        }
                    }
                    block.SetColor("_EmissionColor", emission);
                    renderer.SetPropertyBlock(block, m);
                }
            }
            tinted = wants;
        }

        static Color FaceSafeBody(string materialName, Color body)
        {
            return BossCatalog.IsFaceMaterial(materialName) ? Color.clear : body;
        }

        static bool IsBarRenderer(Renderer renderer)
        {
            var t = renderer.transform;
            while (t != null)
            {
                if (t.name == "HealthBar")
                    return true;
                t = t.parent;
            }
            return false;
        }
    }
}
