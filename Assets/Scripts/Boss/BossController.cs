using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Unique slime bosses. Each kind keeps the slam/charge skeleton and adds a signature attack.</summary>
    public class BossController : MonoBehaviour
    {
        public Transform ring;
        public GameObject boltPrefab;
        public GameObject lightningPrefab;
        public GameObject minionPrefab;
        public float slamRadius = 3.6f;
        public float arenaRadius = RunRules.ArenaRadius;
        public BossKind kind = BossKind.Rock;

        public bool Shielded { get; private set; }
        public bool Enraged => health != null && health.Max > 0 && (float)health.Current / health.Max <= RunRules.BossEnrageHealth;
        public int EnrageSkillCount { get; private set; }

        enum Pattern
        {
            Idle,
            Telegraph,
            Slam,
            Charge,
            Cast
        }

        Pattern pattern = Pattern.Idle;
        float timer;
        float patternTimer = 1.3f;
        float specialTimer = 2.2f;
        Vector3 chargeDirection;
        bool chargeHit;
        Animator animator;
        EnemyHealth health;
        Color tint = Color.white;
        float hiddenUntil;
        float shieldUntil;
        float auraTick;
        int nextEnrage;
        bool announcedEnrage;
        float minionTimer = 8f;

        public void Configure(BossKind bossKind, GameObject bolt, GameObject lightning, GameObject minion, int floor)
        {
            kind = bossKind;
            boltPrefab = bolt;
            lightningPrefab = lightning;
            minionPrefab = minion;
            tint = BossCatalog.Tint(kind);
            slamRadius = kind == BossKind.Rock ? 4.1f : 3.6f;
            patternTimer = kind == BossKind.Volt || kind == BossKind.Shadow ? 0.95f : 1.3f;
            EnrageSkillCount = BossCatalog.EnrageSkillCount(floor);
        }

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<EnemyHealth>();
            if (ring != null)
                ring.gameObject.SetActive(false);
        }

        void Start()
        {
            if (kind != BossKind.None)
                health?.TintBody(BossCatalog.Tint(kind));
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || health == null || health.IsDead)
                return;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;

            if (kind == BossKind.Crystal)
                TickShield();
            if (kind == BossKind.Frost)
                TickFrostAura(player);
            if (kind == BossKind.Shadow)
                TickShadowCloak();
            TickEnrage();
            TickAdds();

            timer -= Time.deltaTime;
            specialTimer -= Time.deltaTime;
            switch (pattern)
            {
                case Pattern.Idle:
                    Chase(player.transform.position, (health.definition != null ? health.definition.moveSpeed : 2.2f) * health.SpeedMultiplier * (Enraged ? 1.25f : 1f));
                    if (specialTimer <= 0f)
                    {
                        specialTimer = Enraged ? 1.15f : SpecialCooldown();
                        if (Enraged ? TryEnrage(player) : TrySpecial(player))
                            break;
                    }
                    if (timer <= 0f)
                        BeginTelegraph(player.transform.position);
                    break;
                case Pattern.Telegraph:
                    if (timer <= 0f)
                        Slam(player);
                    break;
                case Pattern.Slam:
                    if (timer <= 0f)
                        BeginCharge(player.transform.position);
                    break;
                case Pattern.Charge:
                    TickCharge(player);
                    break;
                case Pattern.Cast:
                    if (timer <= 0f)
                    {
                        pattern = Pattern.Idle;
                        timer = patternTimer;
                    }
                    break;
            }
        }

        float SpecialCooldown()
        {
            return kind switch
            {
                BossKind.Volt => 2.4f,
                BossKind.Flame => 3.2f,
                BossKind.Toxic => 2.1f,
                BossKind.Storm => 2.8f,
                BossKind.Magma => 3.4f,
                BossKind.Royal => 2.6f,
                _ => 3.6f
            };
        }

        bool TrySpecial(PlayerHealth player)
        {
            return CastElement(player, 1);
        }

        int EnrageDamage => 24 + (int)kind * 6;

        void TickEnrage()
        {
            if (!Enraged || announcedEnrage)
                return;
            announcedEnrage = true;
            GameFlowController.Instance?.ui?.ShowToast("ENRAGE! " + BossCatalog.DisplayName(kind), new Color(1f, 0.25f, 0.2f));
            AudioService.Instance?.Play("bosswarn");
            health?.TintBody(Color.Lerp(tint, new Color(1f, 0.15f, 0.1f), 0.55f));
        }

        bool TryEnrage(PlayerHealth player)
        {
            nextEnrage++;
            int extra = Mathf.Max(1, EnrageSkillCount);
            return CastElement(player, extra);
        }

        bool CastElement(PlayerHealth player, int intensity)
        {
            Vector3 here = transform.position;
            Vector3 target = player.transform.position;
            int dmg = Enraged ? EnrageDamage : health.ContactDamage;
            int count = Mathf.Clamp(intensity, 1, 10);
            switch (kind)
            {
                case BossKind.Rock:
                    Rain(target, dmg, count + 2, BossCatalog.Tint(kind), 1.2f);
                    AudioService.Instance?.Play("cast");
                    return true;
                case BossKind.Flame:
                    FireBolts(target, dmg, count + 1);
                    return true;
                case BossKind.Frost:
                    Rain(target, dmg, count + 1, BossCatalog.Tint(kind), 1.1f);
                    player.Chill(0.55f, 1.8f);
                    return true;
                case BossKind.Volt:
                    for (int i = 0; i < count; i++)
                        SkyfallHazard.Drop(Around(target, 2.4f), dmg, 0.7f + i * 0.12f, BossCatalog.Tint(kind), 1.0f);
                    Zap(player);
                    return true;
                case BossKind.Toxic:
                    Rain(target, dmg, count, BossCatalog.Tint(kind), 1.15f);
                    HazardZone.Drop(target, new Color(0.4f, 0.9f, 0.2f), Mathf.Max(4, dmg / 5), 1.6f, 3.6f);
                    return true;
                case BossKind.Shadow:
                    Blink(target + (here - target).normalized * 2.2f);
                    Rain(target, dmg, count, BossCatalog.Tint(kind), 1.05f);
                    hiddenUntil = Time.time + 1.0f;
                    return true;
                case BossKind.Magma:
                    Rain(target, dmg + 2, count + 1, new Color(1f, 0.28f, 0.05f), 1.35f);
                    return true;
                case BossKind.Storm:
                    Push(player, 3.2f);
                    for (int i = 0; i < count; i++)
                        SkyfallHazard.Drop(Around(target, 2.8f), dmg, 0.65f + i * 0.1f, BossCatalog.Tint(kind), 1.05f);
                    return true;
                case BossKind.Crystal:
                    SpikeRing(here);
                    Rain(target, dmg, count, BossCatalog.Tint(kind), 1.0f);
                    return true;
                case BossKind.Royal:
                    FireBolts(target, dmg, 2);
                    Rain(target, dmg, count, BossCatalog.Tint(kind), 1.2f);
                    if (Random.value < 0.5f)
                        Zap(player);
                    return true;
                default:
                    return false;
            }
        }

        void Rain(Vector3 around, int damage, int count, Color tint, float radius)
        {
            pattern = Pattern.Cast;
            timer = 0.45f;
            if (animator != null)
                animator.SetTrigger("Telegraph");
            for (int i = 0; i < count; i++)
                SkyfallHazard.Drop(Around(around, 3.2f), damage, 0.85f + i * 0.12f, tint, radius);
        }

        void FireBolts(Vector3 target, int damage, int count)
        {
            pattern = Pattern.Cast;
            timer = 0.4f;
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
            for (int i = 0; i < count; i++)
            {
                Vector3 aim = target + new Vector3(Random.Range(-1.6f, 1.6f), 0.6f, Random.Range(-1.6f, 1.6f));
                var shot = HostileProjectile.Launch(boltPrefab, origin, aim, damage, 8.5f + i * 0.4f);
                if (shot != null)
                    shot.hitVfx = "fire";
            }
            VfxPlayer.Instance?.Play("fire", origin);
            AudioService.Instance?.Play("fire");
        }

        static Vector3 Around(Vector3 center, float spread)
        {
            Vector3 p = center + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            p.y = 0f;
            if (p.magnitude > RunRules.ArenaRadius - 1.4f)
                p = p.normalized * (RunRules.ArenaRadius - 1.4f);
            return p;
        }

        void TickAdds()
        {
            minionTimer -= Time.deltaTime;
            if (minionTimer > 0f)
                return;
            minionTimer = 20f;
            SpawnAdds(10);
        }

        public void SpawnAdds(int count)
        {
            if (minionPrefab == null || health == null || health.IsDead)
                return;
            int band = Mathf.Max(1, (int)kind);
            int live = 0;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                if (EnemyHealth.Active[i] != null && !EnemyHealth.Active[i].IsBoss)
                    live++;
            }
            int want = Mathf.Min(count, Mathf.Max(0, 14 - live));
            for (int i = 0; i < want; i++)
            {
                Vector3 pos = Around(transform.position, 6f);
                if ((pos - transform.position).sqrMagnitude < 4f)
                    pos = Around(Vector3.zero, 8f);
                var instance = Instantiate(minionPrefab, pos, Quaternion.identity);
                instance.GetComponent<EnemyHealth>()?.Scale(band);
                var ai = instance.GetComponent<EnemyChaseAI>();
                if (ai != null)
                    ai.arenaRadius = arenaRadius;
            }
        }

        void SlamEnrage(PlayerHealth player, int damage)
        {
            pattern = Pattern.Slam;
            timer = 0.4f;
            float radius = slamRadius * 1.4f;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.localScale = new Vector3(radius, 1f, radius);
            }
            if (animator != null)
                animator.SetTrigger("Slam");
            AudioService.Instance?.Play("slam");
            VfxPlayer.Instance?.Play("slam", transform.position);
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= radius)
                player.TakeDamage(damage);
        }

        void Volley(Vector3 target, int damage, int count)
        {
            pattern = Pattern.Cast;
            timer = 0.4f;
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
            for (int i = 0; i < count; i++)
            {
                Vector3 aim = target + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                HostileProjectile.Launch(boltPrefab, origin, aim, damage, 8.2f + i * 0.35f);
            }
            AudioService.Instance?.Play("cast");
        }

        void ZapEnrage(PlayerHealth player, int damage)
        {
            pattern = Pattern.Cast;
            timer = 0.3f;
            Vector3 from = transform.position + Vector3.up * 1.3f;
            Vector3 to = player.transform.position + Vector3.up * 0.8f;
            LightningBolt.Flash(lightningPrefab, from, to);
            if (!Obstacles.Blocked(from, to))
                player.TakeDamage(damage);
            VfxPlayer.Instance?.Play("zap", to);
            AudioService.Instance?.Play("lightning");
        }

        void TickCharge(PlayerHealth player)
        {
            Vector3 step = chargeDirection * 9.5f * health.SpeedMultiplier * Time.deltaTime;
            if (Obstacles.Cast(transform.position + Vector3.up * 0.6f, step, 0.7f, out _))
            {
                transform.position = Obstacles.Resolve(transform.position, 0.7f);
                pattern = Pattern.Idle;
                timer = patternTimer;
                if (animator != null)
                    animator.SetFloat("Speed", 0f);
                return;
            }
            transform.position += step;
            transform.position = Obstacles.Resolve(transform.position, 0.7f);
            ClampArena();
            if (kind == BossKind.Magma && Time.frameCount % 4 == 0)
                HazardZone.Drop(transform.position, new Color(1f, 0.3f, 0.05f), 3, 0.9f, 1.6f);
            if (!chargeHit && (player.transform.position - transform.position).sqrMagnitude < 1.6f * 1.6f)
            {
                chargeHit = true;
                player.TakeDamage(health.ContactDamage);
                VfxPlayer.Instance?.Play("hit", player.transform.position + Vector3.up * 0.7f);
            }
            if (timer <= 0f)
            {
                pattern = Pattern.Idle;
                timer = patternTimer;
                if (animator != null)
                    animator.SetFloat("Speed", 0f);
            }
        }

        void Chase(Vector3 target, float speed)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 4f)
            {
                Vector3 steer = Obstacles.Steer(transform.position, to.normalized, 0.7f, 1.8f);
                transform.position += steer * speed * Time.deltaTime;
                transform.position = Obstacles.Resolve(transform.position, 0.7f);
                if (steer.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(steer.normalized, Vector3.up);
                if (animator != null)
                    animator.SetFloat("Speed", 1f);
            }
            else if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
            ClampArena();
        }

        void BeginTelegraph(Vector3 playerPosition)
        {
            pattern = Pattern.Telegraph;
            timer = 0.95f;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.localScale = new Vector3(0.2f, 1f, 0.2f);
            }
            if (animator != null)
                animator.SetTrigger("Telegraph");
            AudioService.Instance?.Play("bosswarn");
            Vector3 to = playerPosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        void Slam(PlayerHealth player)
        {
            pattern = Pattern.Slam;
            timer = 0.45f;
            if (ring != null)
                ring.localScale = new Vector3(slamRadius, 1f, slamRadius);
            if (animator != null)
                animator.SetTrigger("Slam");
            AudioService.Instance?.Play("slam");
            VfxPlayer.Instance?.Play("slam", transform.position);
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= slamRadius)
            {
                player.TakeDamage(health.ContactDamage + 6);
                if (kind == BossKind.Frost)
                    player.Chill(0.45f, 2f);
                if (kind == BossKind.Flame)
                    HazardZone.Drop(player.transform.position, new Color(1f, 0.4f, 0.1f), 3, 1.2f, 2.4f);
            }
        }

        void BeginCharge(Vector3 playerPosition)
        {
            pattern = Pattern.Charge;
            timer = 0.55f;
            chargeHit = false;
            if (ring != null)
                ring.gameObject.SetActive(false);
            chargeDirection = playerPosition - transform.position;
            chargeDirection.y = 0f;
            if (chargeDirection.sqrMagnitude < 0.01f)
                chargeDirection = transform.forward;
            chargeDirection.Normalize();
            transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);
            if (animator != null)
                animator.SetFloat("Speed", 1f);
            VfxPlayer.Instance?.Play("dash", transform.position + Vector3.up * 0.5f);
        }

        void Throw(Vector3 target, int damage, float speed)
        {
            pattern = Pattern.Cast;
            timer = 0.35f;
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
            HostileProjectile.Launch(boltPrefab, origin, target, damage, speed);
            AudioService.Instance?.Play("cast");
        }

        void FireBurst(Vector3 center, float radius, int damage)
        {
            pattern = Pattern.Cast;
            timer = 0.4f;
            VfxPlayer.Instance?.Play("fire", center + Vector3.up * 0.3f);
            AudioService.Instance?.Play("fire");
            var player = PlayerHealth.Instance;
            if (player == null)
                return;
            Vector3 flat = player.transform.position - center;
            flat.y = 0f;
            if (flat.magnitude <= radius)
                player.TakeDamage(damage);
        }

        void Zap(PlayerHealth player)
        {
            pattern = Pattern.Cast;
            timer = 0.3f;
            Vector3 from = transform.position + Vector3.up * 1.3f;
            Vector3 to = player.transform.position + Vector3.up * 0.8f;
            LightningBolt.Flash(lightningPrefab, from, to);
            if (!Obstacles.Blocked(from, to))
                player.TakeDamage(Mathf.Max(6, health.ContactDamage));
            VfxPlayer.Instance?.Play("zap", to);
            AudioService.Instance?.Play("lightning");
        }

        void Blink(Vector3 around)
        {
            Vector3 dest = around + Random.insideUnitSphere * 1.6f;
            dest.y = 0f;
            if (dest.magnitude > arenaRadius - 1.5f)
                dest = dest.normalized * (arenaRadius - 1.5f);
            if (!Obstacles.Overlaps(dest, 0.8f))
                transform.position = dest;
            VfxPlayer.Instance?.Play("dash", transform.position + Vector3.up * 0.5f);
            AudioService.Instance?.Play("dash");
        }

        void Erupt(Vector3 target)
        {
            pattern = Pattern.Cast;
            timer = 0.55f;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.position = new Vector3(target.x, 0.05f, target.z);
                ring.localScale = new Vector3(2.2f, 1f, 2.2f);
            }
            StartCoroutine(EruptAfter(target, 0.7f));
        }

        System.Collections.IEnumerator EruptAfter(Vector3 target, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (health == null || health.IsDead)
                yield break;
            VfxPlayer.Instance?.Play("slam", target);
            AudioService.Instance?.Play("slam");
            HazardZone.Drop(target, new Color(1f, 0.25f, 0.05f), 6, 1.8f, 2.2f);
            var player = PlayerHealth.Instance;
            if (player != null && (player.transform.position - target).sqrMagnitude < 2.1f * 2.1f)
                player.TakeDamage(health.ContactDamage + 4);
            if (ring != null)
                ring.gameObject.SetActive(false);
        }

        void Push(PlayerHealth player, float distance)
        {
            Vector3 away = player.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            var body = player.GetComponent<CharacterController>();
            Vector3 dest = player.transform.position + away.normalized * distance;
            if (dest.magnitude > arenaRadius)
                dest = dest.normalized * arenaRadius;
            if (body != null)
                body.Move(dest - player.transform.position);
            else
                player.transform.position = dest;
        }

        void Strike(Vector3 target)
        {
            pattern = Pattern.Cast;
            timer = 0.4f;
            VfxPlayer.Instance?.Play("zap", target + Vector3.up);
            AudioService.Instance?.Play("lightning");
            var player = PlayerHealth.Instance;
            if (player != null && (player.transform.position - target).sqrMagnitude < 1.8f * 1.8f)
                player.TakeDamage(health.ContactDamage);
        }

        void SpikeRing(Vector3 center)
        {
            pattern = Pattern.Cast;
            timer = 0.4f;
            VfxPlayer.Instance?.Play("arcane", center + Vector3.up * 0.4f);
            AudioService.Instance?.Play("arcane");
            var player = PlayerHealth.Instance;
            if (player == null)
                return;
            float d = Vector3.Distance(new Vector3(player.transform.position.x, 0f, player.transform.position.z), new Vector3(center.x, 0f, center.z));
            if (d > 1.6f && d < 4.2f)
                player.TakeDamage(health.ContactDamage);
        }

        void TickFrostAura(PlayerHealth player)
        {
            auraTick -= Time.deltaTime;
            if (auraTick > 0f)
                return;
            auraTick = 0.6f;
            if ((player.transform.position - transform.position).sqrMagnitude < 4.5f * 4.5f)
                player.Chill(0.7f, 0.8f);
        }

        void TickShield()
        {
            if (Time.time < shieldUntil)
            {
                Shielded = true;
                return;
            }
            Shielded = false;
            if (specialTimer < -1.5f)
            {
                Shielded = true;
                shieldUntil = Time.time + 2.4f;
                specialTimer = 4f;
                VfxPlayer.Instance?.Play("powerup", transform.position + Vector3.up, BossCatalog.Tint(BossKind.Crystal));
            }
        }

        void TickShadowCloak()
        {
            bool hide = Time.time < hiddenUntil;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer.transform.name == "HealthBar" || renderer.transform.parent != null && renderer.transform.parent.name == "HealthBar")
                    continue;
                renderer.enabled = true;
            }
            if (hide)
                health?.TintBody(Color.Lerp(tint, Color.black, 0.65f));
            else
                health?.TintBody(tint);
        }

        void ClampArena()
        {
            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.magnitude > arenaRadius)
                flat = flat.normalized * arenaRadius;
            transform.position = new Vector3(flat.x, 0f, flat.z);
        }
    }
}
