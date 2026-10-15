using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Short-lived puddle or eruption that damages the hero while they stand in it.</summary>
    public class HazardZone : MonoBehaviour
    {
        public int damage = 4;
        public float radius = 1.2f;
        public float interval = 0.45f;
        public float lifetime = 3.2f;
        public bool chill;
        public bool burnVisual;

        float age;
        float tick;

        public static HazardZone Drop(Vector3 position, Color color, int damage, float radius, float lifetime, bool chill = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Hazard";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = position + Vector3.up * 0.04f;
            go.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.sharedMaterial = new Material(shader);
            var block = new MaterialPropertyBlock();
            color.a = 0.55f;
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            var zone = go.AddComponent<HazardZone>();
            zone.damage = damage;
            zone.radius = radius;
            zone.lifetime = lifetime;
            zone.chill = chill;
            return zone;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            tick -= Time.deltaTime;
            if (tick > 0f)
                return;
            tick = interval;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > radius * radius)
                return;
            player.TakeDamage(damage);
            if (chill)
                player.Chill(0.55f, 1.4f);
            if (burnVisual)
                VfxPlayer.Instance?.Play("burn", player.transform.position + Vector3.up * 0.6f);
        }
    }
}
