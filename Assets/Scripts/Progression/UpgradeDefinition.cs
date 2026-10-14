using UnityEngine;

namespace SlimeBreaker
{
    [CreateAssetMenu(fileName = "Upgrade", menuName = "Slime Breaker/Upgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        public UpgradeKind kind;
        public string title;
        public string description;
        public float amount = 0.2f;
        public Sprite icon;
    }
}
