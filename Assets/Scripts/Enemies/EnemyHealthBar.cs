using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>World-space HP bar that faces the camera. Built at runtime so every slime and boss gets one.</summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        public EnemyHealth health;
        Transform fill;
        float width = 0.85f;
        static Material shared;

        public static void Attach(EnemyHealth health)
        {
            if (health == null || health.GetComponentInChildren<EnemyHealthBar>() != null)
                return;
            var root = new GameObject("HealthBar");
            root.transform.SetParent(health.transform, false);
            var bar = root.AddComponent<EnemyHealthBar>();
            bar.health = health;
            bar.Build();
        }

        void Build()
        {
            bool boss = health != null && health.IsBoss;
            width = boss ? 1.55f : 0.85f;
            Quad("Back", new Color(0.06f, 0.06f, 0.08f, 0.8f), width + 0.06f, 0.11f, 0.02f);
            var fillObject = Quad("Fill", boss ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.9f, 0.22f, 0.28f), width, 0.08f, 0f);
            fill = fillObject.transform;
        }

        GameObject Quad(string name, Color color, float w, float h, float z)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localScale = new Vector3(w, h, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return go;
        }

        static Material SharedMaterial()
        {
            if (shared != null)
                return shared;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            shared = new Material(shader);
            if (shared.HasProperty("_Surface"))
                shared.SetFloat("_Surface", 1f);
            if (shared.HasProperty("_BaseColor"))
                shared.SetColor("_BaseColor", Color.white);
            shared.renderQueue = 3000;
            return shared;
        }

        void LateUpdate()
        {
            if (health == null || health.IsDead)
            {
                gameObject.SetActive(false);
                return;
            }
            float height = health.HitHeight + (health.IsBoss ? 1.05f : 0.62f);
            transform.position = health.transform.position + Vector3.up * height;
            var camera = Camera.main;
            if (camera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
            float ratio = health.Max > 0 ? (float)health.Current / health.Max : 0f;
            if (fill != null)
            {
                fill.localScale = new Vector3(width * Mathf.Max(0.001f, ratio), 0.08f, 1f);
                fill.localPosition = new Vector3(-width * 0.5f * (1f - ratio), 0f, 0f);
            }
        }
    }
}
