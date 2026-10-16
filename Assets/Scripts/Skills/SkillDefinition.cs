using UnityEngine;

namespace SlimeBreaker
{
    [CreateAssetMenu(fileName = "Skill", menuName = "Slime Breaker/Skill")]
    public class SkillDefinition : ScriptableObject
    {
        /// <summary>Every power-up skill tops out here; level 10 also unlocks the skill's max-level bonus.</summary>
        public const int MaxSkillLevel = 10;

        public SkillKind kind;
        public string title;
        [TextArea] public string description;
        public Sprite icon;
        public Color color = Color.white;
        public int maxLevel = MaxSkillLevel;

        [Header("Balance")]
        public int baseDamage = 10;
        public float cooldown = 2f;
        public float radius = 1.6f;
        public int count = 3;
        public float slowFactor = 0.45f;
        public float effectDuration = 1.6f;

        // Level 1 → 10: damage ×3.7, one extra shard/jump/missile every two levels,
        // cooldown ×0.47, radius ×1.72. Level 10 adds a per-skill bonus (see SkillController).
        public int DamageAt(int level) => Mathf.RoundToInt(baseDamage * (1f + 0.3f * (Clamp(level) - 1)));
        public int CountAt(int level) => count + Clamp(level) / 2 + (IsMax(level) && kind == SkillKind.ArcaneMissile ? 2 : 0);
        public float CooldownAt(int level) => Mathf.Max(0.4f, cooldown * Mathf.Pow(0.92f, Clamp(level) - 1));
        public float RadiusAt(int level) => radius * (1f + 0.08f * (Clamp(level) - 1));
        public bool IsMax(int level) => level >= maxLevel;

        int Clamp(int level) => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    }
}
