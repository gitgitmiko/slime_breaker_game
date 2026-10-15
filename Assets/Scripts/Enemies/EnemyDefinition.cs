using UnityEngine;

namespace SlimeBreaker
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "Slime Breaker/Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName;
        public int maxHealth = 20;
        public float moveSpeed = 2.2f;
        public int contactDamage = 8;
        public float contactRange = 1.15f;
        public float contactCooldown = 0.7f;
        public int xp = 3;
        public bool isBoss;
        public bool isRanged;
        public float incomingDamageScale = 1f;
        public BossKind bossKind;
        public Color tint = Color.white;
    }
}
