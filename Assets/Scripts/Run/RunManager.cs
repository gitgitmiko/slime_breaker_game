using UnityEngine;

namespace SlimeBreaker
{
    public class RunManager : MonoBehaviour
    {
        public BalanceConfig balance;
        public EnemySpawner spawner;
        public float Elapsed { get; private set; }

        public void Restore(float elapsed)
        {
            Elapsed = Mathf.Max(0f, elapsed);
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            Elapsed += Time.deltaTime;
        }
    }
}
