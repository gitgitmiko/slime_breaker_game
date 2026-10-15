using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Telegraphed sky drop: a ground shadow, then a falling projectile that hurts the hero.</summary>
    public class SkyfallHazard : MonoBehaviour
    {
        public int damage = 12;
        public float delay = 1.15f;
        public float radius = 1.15f;
        public Color tint = new Color(0.45f, 0.42f, 0.4f);

        Transform shadow;
        Transform body;
        float age;
        bool struck;
        static Material shadowMat;
        static Material bodyMat;

        public static SkyfallHazard Drop(Vector3 ground, int damage, float delay, Color tint, float radius = 1.15f)
        {
            ground.y = 0.04f;
            if (Obstacles.Overlaps(ground, 0.6f))
            {
                Vector3 push = ground;
                push.y = 0f;
                if (push.sqrMagnitude < 0.01f)
                    push = Vector3.forward;
                ground += push.normalized * 1.2f;
                ground.y = 0.04f;
            }
            var go = new GameObject("Skyfall");
            go.transform.position = ground;
            var hazard = go.AddComponent<SkyfallHazard>();
            hazard.damage = Mathf.Max(1, damage);
            hazard.delay = Mathf.Max(0.35f, delay);
            hazard.tint = tint;
            hazard.radius = radius;
            hazard.Build();
            return hazard;
        }

        void Build()
        {
            shadow = Quad("Shadow", new Vector3(0f, 0.02f, 0f), new Vector3(radius * 1.8f, 1f, radius * 1.8f),
                new Color(0f, 0f, 0f, 0.45f), true);
            body = Quad("Rock", new Vector3(0f, 9f, 0f), Vector3.one * (radius * 0.7f), tint, false);
            body.rotation = Random.rotation;
        }

        Transform Quad(string name, Vector3 local, Vector3 scale, Color color, bool flat)
        {
            var go = GameObject.CreatePrimitive(flat ? PrimitiveType.Quad : PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            if (flat)
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = flat ? ShadowMaterial() : BodyMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            age += Time.deltaTime;
            float windup = Mathf.Clamp01(age / delay);
            if (shadow != null)
            {
                float pulse = 0.75f + Mathf.Sin(age * 10f) * 0.12f;
                shadow.localScale = new Vector3(radius * 1.8f, 1f, radius * 1.8f) * Mathf.Lerp(0.45f, pulse, windup);
            }
            if (body != null && !struck)
            {
                float y;
                if (windup < 0.32f)
                    y = Mathf.Lerp(1.6f, 9.5f, windup / 0.32f);
                else
                    y = Mathf.Lerp(9.5f, 0.4f, (windup - 0.32f) / 0.68f);
                body.localPosition = new Vector3(0f, y, 0f);
                body.Rotate(120f * Time.deltaTime, 80f * Time.deltaTime, 40f * Time.deltaTime);
            }
            if (!struck && age >= delay)
                Strike();
            if (age > delay + 0.45f)
                Destroy(gameObject);
        }

        void Strike()
        {
            struck = true;
            VfxPlayer.Instance?.Play("slam", transform.position);
            AudioService.Instance?.Play("slam");
            var player = PlayerHealth.Instance;
            if (player != null && !player.IsDead)
            {
                Vector3 flat = player.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= radius + 0.35f)
                    player.TakeDamage(damage);
            }
            if (body != null)
                body.gameObject.SetActive(false);
        }

        static Material ShadowMaterial()
        {
            if (shadowMat != null)
                return shadowMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            shadowMat = new Material(shader);
            if (shadowMat.HasProperty("_Surface"))
                shadowMat.SetFloat("_Surface", 1f);
            shadowMat.renderQueue = 3000;
            return shadowMat;
        }

        static Material BodyMaterial()
        {
            if (bodyMat != null)
                return bodyMat;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            bodyMat = new Material(shader);
            return bodyMat;
        }
    }
}
