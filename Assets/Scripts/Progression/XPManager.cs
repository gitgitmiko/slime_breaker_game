using UnityEngine;

namespace SlimeBreaker
{
    public class XPManager : MonoBehaviour
    {
        public static XPManager Instance { get; private set; }
        public int Xp { get; private set; }
        public int Level => GameFlowController.Instance != null ? GameFlowController.Instance.Stats.level : 1;
        public int XpToNext => 8 + (Level - 1) * 5;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Restore(int xp)
        {
            Xp = Mathf.Max(0, xp);
        }

        public void Add(int amount)
        {
            if (!GameFlowController.IsPlaying || amount <= 0)
                return;
            var stats = GameFlowController.Instance.Stats;
            Xp += Mathf.Max(1, Mathf.RoundToInt(amount * stats.xpMultiplier));
            while (Xp >= XpToNext && GameFlowController.Instance.Stats.level < RunRules.MaxLevel)
            {
                Xp -= XpToNext;
                GameFlowController.Instance.BeginLevelUp();
            }
        }
    }
}
