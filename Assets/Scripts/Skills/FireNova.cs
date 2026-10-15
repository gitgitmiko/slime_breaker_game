using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Expanding ring of fire centred on the hero. A flat annulus mesh grows outward while a radial flame burst
    /// follows its edge; enemies are hit the moment the ring front reaches them.
    /// </summary>
    public class FireNova : MonoBehaviour
    {
        public Transform ring;
        public Transform trail;
        public ParticleSystem flames;
        public ParticleSystem embers;
        public float duration = 0.55f;
        public float radius = 2.6f;
        public Color color = new Color(1f, 0.5f, 0.15f);
        public float ringWidth = 0.55f;

        int damage;
        int burnDamage;
        float age;
        bool armed;
        MeshRenderer ringRenderer;
        MeshRenderer trailRenderer;
        MaterialPropertyBlock block;
        readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();

        /// <summary>Builds a unit-radius flat annulus with soft inner/outer edges in the vertex colour.</summary>
        public static Mesh BuildAnnulus(int segments, float inner, float mid, string name)
        {
            var mesh = new Mesh { name = name };
            float[] radii = { inner, mid, 1f };
            float[] alpha = { 0f, 1f, 0f };
            var vertices = new Vector3[(segments + 1) * 3];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            for (int ringIndex = 0; ringIndex < 3; ringIndex++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    int index = ringIndex * (segments + 1) + i;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * radii[ringIndex], 0f, Mathf.Sin(angle) * radii[ringIndex]);
                    uv[index] = new Vector2((float)i / segments, ringIndex / 2f);
                    float a = alpha[ringIndex];
                    colors[index] = new Color(a, a, a, a);
                }
            }
            var triangles = new List<int>();
            for (int ringIndex = 0; ringIndex < 2; ringIndex++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = ringIndex * (segments + 1) + i;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    // Double sided so the ring also reads from low camera angles.
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Awake()
        {
            ringRenderer = ring != null ? ring.GetComponent<MeshRenderer>() : null;
            trailRenderer = trail != null ? trail.GetComponent<MeshRenderer>() : null;
            block = new MaterialPropertyBlock();
            if (ring != null)
                ring.localScale = new Vector3(0.01f, 1f, 0.01f);
            if (trail != null)
                trail.localScale = new Vector3(0.01f, 1f, 0.01f);
        }

        public void Begin(float targetRadius, int hitDamage, int burnPerTick)
        {
            radius = Mathf.Max(0.5f, targetRadius);
            damage = hitDamage;
            burnDamage = burnPerTick;
            armed = true;
            float speed = radius / Mathf.Max(0.1f, duration);
            if (flames != null)
            {
                var main = flames.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.05f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.8f, duration * 1.25f);
                main.startColor = color;
                flames.Play();
            }
            if (embers != null)
            {
                var main = embers.main;
                main.startColor = Color.Lerp(color, Color.yellow, 0.5f);
                embers.Play();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float t = Mathf.Clamp01(age / duration);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            float front = radius * eased;
            float fade = 1f - t * t;

            if (ring != null)
            {
                // Scale so the ring's bright mid line sits at the front; the band stays roughly ringWidth wide.
                float scale = front + ringWidth * 0.5f;
                ring.localScale = new Vector3(scale, 1f, scale);
                ring.Rotate(0f, 90f * dt, 0f, Space.World);
                Tint(ringRenderer, color * (1.6f * fade));
            }
            if (trail != null)
            {
                float lag = Mathf.Max(0.01f, front * 0.65f);
                trail.localScale = new Vector3(lag, 1f, lag);
                trail.Rotate(0f, -60f * dt, 0f, Space.World);
                Tint(trailRenderer, color * (0.9f * fade * fade));
            }

            if (armed)
                Sweep(front);

            if (age >= duration + 1.1f)
                Destroy(gameObject);
            else if (age >= duration && ringRenderer != null)
            {
                ringRenderer.enabled = false;
                if (trailRenderer != null)
                    trailRenderer.enabled = false;
            }
        }

        void Tint(MeshRenderer renderer, Color value)
        {
            if (renderer == null)
                return;
            value.a = 1f;
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", value);
            block.SetColor("_Color", value);
            renderer.SetPropertyBlock(block);
        }

        void Sweep(float front)
        {
            Vector3 center = transform.position;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead || hit.Contains(enemy))
                    continue;
                Vector3 flat = enemy.transform.position - center;
                flat.y = 0f;
                float distance = flat.magnitude - enemy.HitRadius;
                if (distance > front || distance > radius)
                    continue;
                if (Obstacles.Blocked(center + Vector3.up * 0.5f, enemy.transform.position + Vector3.up * enemy.HitHeight))
                {
                    hit.Add(enemy);
                    continue;
                }
                hit.Add(enemy);
                bool crit;
                int rolled = CombatMath.ApplyCrit(damage, out crit);
                enemy.TakeDamage(rolled, false, true, crit);
                if (burnDamage > 0)
                    enemy.ApplyBurn(burnDamage, 3, 0.5f);
                VfxPlayer.Instance?.Play("burn", enemy.transform.position + Vector3.up * enemy.HitHeight);
            }
        }
    }
}
