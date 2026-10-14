using UnityEngine;

namespace SlimeBreaker
{
    [CreateAssetMenu(fileName = "Skill", menuName = "Slime Breaker/Skill")]
    public class SkillDefinition : ScriptableObject
    {
        public SkillKind kind;
        public string title;
        [TextArea] public string description;
        public Sprite icon;
        public Color color = Color.white;
        public int maxLevel = 4;

        [Header("Balance")]
        public int baseDamage = 10;
        public float cooldown = 2f;
        public float radius = 1.6f;
        public int count = 3;
        public float slowFactor = 0.45f;
        public float effectDuration = 1.6f;

        public int DamageAt(int level) => Mathf.RoundToInt(baseDamage * (1f + 0.35f * (level - 1)));
        public int CountAt(int level) => count + (level - 1);
        public float CooldownAt(int level) => Mathf.Max(0.4f, cooldown * Mathf.Pow(0.85f, level - 1));
        public float RadiusAt(int level) => radius * (1f + 0.12f * (level - 1));
    }
}
