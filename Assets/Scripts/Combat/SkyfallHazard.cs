using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Telegraphed sky drop: a soft ground shadow that tracks the falling body,
    /// then a projectile that hurts the hero on impact.
    /// </summary>
    public class SkyfallHazard : MonoBehaviour
    {
        public int damage = 12;
        public float delay = 1.15f;
        public float radius = 1.15f;
        public Color tint = new Color(0.45f, 0.42f, 0.4f);

        Transform penumbra;
        Transform umbra;
        Transform ring;
        Transform body;
        float age;
        bool struck;
        float apex;
        Vector3 lightStretch;
        static Material shadowMat;
        static Material bodyMat;
        static Texture2D blobTex;
        static Texture2D ringTex;

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
            apex = Random.Range(8.4f, 10.2f);
            // Distant sun from the upper-right: contact shadow stretches away from the light.
            lightStretch = new Vector3(1.28f, 1f, 0.86f);
            penumbra = Blob("Penumbra", 0.018f, Color.black);
            umbra = Blob("Umbra", 0.028f, Color.black);
            ring = Ring("Mark", 0.024f, new Color(0.08f, 0.05f, 0.04f, 0.55f));
            body = MakeBody();
        }

        Transform Blob(string name, float height, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, Random.Range(-12f, 12f), 0f);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ShadowMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ApplyColor(renderer, color, BlobTexture());
            return go.transform;
        }

        Transform Ring(string name, float height, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ShadowMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ApplyColor(renderer, color, RingTexture());
            return go.transform;
        }

        Transform MakeBody()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Rock";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            go.transform.localScale = Vector3.one * (radius * 0.72f);
            go.transform.rotation = Random.rotation;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BodyMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ApplyColor(renderer, tint, null);
            return go.transform;
        }

        static void ApplyColor(Renderer renderer, Color color, Texture texture)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            if (texture != null)
            {
                block.SetTexture("_BaseMap", texture);
                block.SetTexture("_MainTex", texture);
            }
            renderer.SetPropertyBlock(block);
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            age += Time.deltaTime;
            float windup = Mathf.Clamp01(age / delay);
            float y = BodyHeight(windup);
            if (body != null && !struck)
            {
                body.localPosition = new Vector3(0f, y, 0f);
                float spin = Mathf.Lerp(70f, 220f, 1f - Mathf.Clamp01(y / apex));
                body.Rotate(spin * Time.deltaTime, spin * 0.55f * Time.deltaTime, spin * 0.28f * Time.deltaTime);
            }

            float height01 = Mathf.Clamp01(y / apex);
            // Higher rock = smaller, paler, softer shadow. Near the ground it fills the impact zone.
            float umbraSize = radius * Mathf.Lerp(1.08f, 0.28f, height01);
            float penumbraSize = radius * Mathf.Lerp(2.05f, 0.85f, HeightEase(height01));
            float wobble = 1f + Mathf.Sin(age * 3.1f) * 0.025f * (1f - height01);
            Vector3 stretch = lightStretch * wobble;
            // Offset the umbra slightly away from the light as the rock gets higher (cast shadow).
            Vector3 offset = new Vector3(0.22f, 0f, -0.12f) * height01 * radius;

            if (penumbra != null)
            {
                penumbra.localPosition = new Vector3(offset.x * 1.35f, 0.018f, offset.z * 1.35f);
                penumbra.localScale = new Vector3(penumbraSize * stretch.x, 1f, penumbraSize * stretch.z);
                Paint(penumbra, new Color(0f, 0f, 0f, Mathf.Lerp(0.34f, 0.05f, height01)));
            }
            if (umbra != null)
            {
                umbra.localPosition = new Vector3(offset.x, 0.028f, offset.z);
                umbra.localScale = new Vector3(umbraSize * stretch.x, 1f, umbraSize * stretch.z);
                Paint(umbra, new Color(0f, 0f, 0f, Mathf.Lerp(0.72f, 0.10f, height01)));
            }
            if (ring != null)
            {
                float mark = Mathf.SmoothStep(0.35f, 1f, windup);
                float pulse = 1f + Mathf.Sin(age * 5.2f) * 0.04f * mark;
                ring.localScale = new Vector3(radius * 2.05f, 1f, radius * 2.05f) * pulse;
                Paint(ring, new Color(0.12f, 0.04f, 0.03f, 0.18f + 0.42f * mark));
            }

            if (!struck && age >= delay)
                Strike();
            if (age > delay + 0.45f)
                Destroy(gameObject);
        }

        float BodyHeight(float windup)
        {
            // Tossed up with ease-out, then falls with gravity (t^2) so the last metres are fast.
            const float toss = 0.28f;
            if (windup < toss)
            {
                float t = windup / toss;
                float ease = 1f - (1f - t) * (1f - t);
                return Mathf.Lerp(1.7f, apex, ease);
            }
            float fall = (windup - toss) / (1f - toss);
            float gravity = fall * fall;
            return Mathf.Lerp(apex, 0.38f, gravity);
        }

        static float HeightEase(float height01)
        {
            return height01 * height01;
        }

        static void Paint(Transform target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
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
            if (umbra != null)
                Paint(umbra, new Color(0f, 0f, 0f, 0.82f));
        }

        static Material ShadowMaterial()
        {
            if (shadowMat != null)
                return shadowMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            shadowMat = new Material(shader);
            if (shadowMat.HasProperty("_Surface"))
                shadowMat.SetFloat("_Surface", 1f);
            if (shadowMat.HasProperty("_ZWrite"))
                shadowMat.SetFloat("_ZWrite", 0f);
            if (shadowMat.HasProperty("_SrcBlend"))
                shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (shadowMat.HasProperty("_DstBlend"))
                shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shadowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shadowMat.SetOverrideTag("RenderType", "Transparent");
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

        static Texture2D BlobTexture()
        {
            if (blobTex != null)
                return blobTex;
            const int size = 64;
            blobTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            blobTex.wrapMode = TextureWrapMode.Clamp;
            blobTex.filterMode = FilterMode.Bilinear;
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - centre) / centre;
                    float ny = (y - centre) / centre;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    // Soft, slightly oval blob: dense core, long penumbra, no hard rim.
                    float core = Mathf.Clamp01(1f - d / 0.42f);
                    float haze = Mathf.Clamp01(1f - d);
                    float alpha = core * core * 0.75f + haze * haze * haze * 0.55f;
                    blobTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            blobTex.Apply(false, true);
            return blobTex;
        }

        static Texture2D RingTexture()
        {
            if (ringTex != null)
                return ringTex;
            const int size = 64;
            ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            ringTex.wrapMode = TextureWrapMode.Clamp;
            ringTex.filterMode = FilterMode.Bilinear;
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - centre) / centre;
                    float ny = (y - centre) / centre;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.08f, 2f));
                    float inner = Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.05f, 2f)) * 0.35f;
                    ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + inner)));
                }
            }
            ringTex.Apply(false, true);
            return ringTex;
        }
    }
}
