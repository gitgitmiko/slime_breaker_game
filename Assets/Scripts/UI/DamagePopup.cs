using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Floating world-space numbers for damage the hero deals and receives.</summary>
    public class DamagePopup : MonoBehaviour
    {
        public enum Kind
        {
            Enemy,
            Crit,
            Hero
        }

        static readonly Queue<DamagePopup> Pool = new Queue<DamagePopup>(24);
        static Font font;

        TextMesh label;
        float age;
        float life = 0.72f;
        Vector3 velocity;
        Color color;

        public static void Show(Vector3 world, int amount, Kind kind)
        {
            if (amount <= 0)
                return;
            var popup = Pool.Count > 0 ? Pool.Dequeue() : Create();
            popup.Begin(world + Vector3.up * 1.1f + Random.insideUnitSphere * 0.15f, amount, kind);
        }

        static DamagePopup Create()
        {
            var go = new GameObject("DamagePopup");
            Object.DontDestroyOnLoad(go);
            var popup = go.AddComponent<DamagePopup>();
            popup.label = go.AddComponent<TextMesh>();
            popup.label.anchor = TextAnchor.MiddleCenter;
            popup.label.alignment = TextAlignment.Center;
            popup.label.fontStyle = FontStyle.Bold;
            popup.label.characterSize = 0.08f;
            popup.label.fontSize = 48;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
                popup.label.font = font;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (font != null && font.material != null)
                    renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return popup;
        }

        void Begin(Vector3 world, int amount, Kind kind)
        {
            transform.position = world;
            age = 0f;
            gameObject.SetActive(true);
            bool crit = kind == Kind.Crit;
            color = kind switch
            {
                Kind.Hero => new Color(1f, 0.28f, 0.28f),
                Kind.Crit => new Color(1f, 0.78f, 0.18f),
                _ => new Color(1f, 0.95f, 0.78f)
            };
            label.text = crit ? "CRIT\n" + amount : amount.ToString();
            label.color = color;
            label.characterSize = crit ? 0.11f : kind == Kind.Hero ? 0.09f : 0.075f;
            velocity = Vector3.up * (crit ? 2.4f : 1.8f) + Random.insideUnitSphere * 0.35f;
            velocity.y = Mathf.Abs(velocity.y);
        }

        void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            transform.position += velocity * Time.unscaledDeltaTime;
            velocity *= 0.96f;
            float t = Mathf.Clamp01(age / life);
            var fade = color;
            fade.a = 1f - t * t;
            label.color = fade;
            var camera = Camera.main;
            if (camera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
            if (age >= life)
            {
                gameObject.SetActive(false);
                Pool.Enqueue(this);
            }
        }
    }
}
