using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Jagged line flash between two points; destroys itself after a short time.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LightningBolt : MonoBehaviour
    {
        public float duration = 0.18f;
        public int segments = 7;
        public float jitter = 0.35f;

        LineRenderer line;
        float age;
        Vector3 from;
        Vector3 to;
        Color baseColor;

        public static void Flash(GameObject prefab, Vector3 from, Vector3 to)
        {
            if (prefab == null)
                return;
            var instance = Instantiate(prefab, from, Quaternion.identity);
            var bolt = instance.GetComponent<LightningBolt>();
            if (bolt == null)
                bolt = instance.AddComponent<LightningBolt>();
            bolt.Set(from, to);
        }

        void Set(Vector3 start, Vector3 end)
        {
            from = start;
            to = end;
            line = GetComponent<LineRenderer>();
            baseColor = line.startColor;
            Rebuild();
        }

        void Rebuild()
        {
            if (line == null)
                return;
            line.positionCount = segments + 1;
            Vector3 dir = to - from;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
                side = Vector3.right;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 point = Vector3.Lerp(from, to, t);
                if (i > 0 && i < segments)
                {
                    float amount = jitter * Mathf.Sin(t * Mathf.PI);
                    point += side * Random.Range(-amount, amount) + Vector3.up * Random.Range(-amount * 0.6f, amount * 0.6f);
                }
                line.SetPosition(i, point);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.frameCount % 2 == 0)
                Rebuild();
            if (line != null)
            {
                float fade = 1f - age / duration;
                var color = baseColor;
                color.a = fade;
                line.startColor = color;
                line.endColor = color;
            }
        }
    }
}
