using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Main-menu history: every stage boss the player has beaten (floor 10, 20, ...) stands in an arc behind the hero.
    /// The models are pre-placed (inactive) by the setup tool; this only switches on the earned ones and tints them.
    /// </summary>
    public class MenuTrophies : MonoBehaviour
    {
        /// <summary>Index 0 = the floor-10 boss, 9 = the floor-100 boss.</summary>
        public GameObject[] trophies;

        MaterialPropertyBlock block;

        void Start()
        {
            Apply(SaveService.Load().bossesDefeated);
        }

        public int Shown { get; private set; }

        public void Apply(int defeated)
        {
            Shown = 0;
            for (int i = 0; trophies != null && i < trophies.Length; i++)
            {
                bool earned = i < defeated;
                if (trophies[i] == null)
                    continue;
                trophies[i].SetActive(earned);
                if (earned)
                {
                    Tint(trophies[i], (BossKind)(i + 1));
                    Shown++;
                }
            }
        }

        void Tint(GameObject trophy, BossKind kind)
        {
            block ??= new MaterialPropertyBlock();
            Color body = BossCatalog.Tint(kind);
            foreach (var renderer in trophy.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var mat = materials[m];
                    string name = mat != null ? mat.name : string.Empty;
                    string lower = name.ToLowerInvariant();
                    renderer.GetPropertyBlock(block, m);
                    Color part = BossCatalog.ColorForMaterial(name, kind, body);
                    if (part.a > 0.01f)
                    {
                        block.SetColor("_BaseColor", part);
                        block.SetColor("_Color", part);
                    }
                    Color selfLit = lower.Contains("star") ? BossCatalog.CrownColor * 0.9f : lower.Contains("blush") ? new Color(1f, 0.5f, 0.55f) * 0.3f : Color.black;
                    block.SetColor("_EmissionColor", selfLit);
                    block.SetFloat("_Rainbow", kind == BossKind.Royal && mat != null && mat.HasProperty("_Rainbow") ? 1f : 0f);
                    renderer.SetPropertyBlock(block, m);
                }
            }
        }
    }
}
