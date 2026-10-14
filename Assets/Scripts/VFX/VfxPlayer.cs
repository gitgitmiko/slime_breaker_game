using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class VfxPlayer : MonoBehaviour
    {
        public static VfxPlayer Instance { get; private set; }
        public GameObject hit;
        public GameObject dash;
        public GameObject levelUp;
        public GameObject slam;

        Dictionary<string, GameObject> lookup;

        void Awake()
        {
            Instance = this;
            lookup = new Dictionary<string, GameObject>
            {
                { "hit", hit },
                { "dash", dash },
                { "levelup", levelUp },
                { "slam", slam }
            };
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Play(string id, Vector3 position)
        {
            if (lookup == null || !lookup.TryGetValue(id, out var prefab) || prefab == null)
                return;
            var instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, 1.6f);
        }
    }
}
