using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Spawns short-lived particle bursts by id. Optional colour overrides the burst's start colour.</summary>
    public class VfxPlayer : MonoBehaviour
    {
        public static VfxPlayer Instance { get; private set; }

        [Serializable]
        public class Entry
        {
            public string id;
            public GameObject prefab;
            public float lifetime = 1.6f;
        }

        public Entry[] entries;

        Dictionary<string, Entry> lookup;

        /// <summary>Kept so older tests and scripts can still reach the core bursts.</summary>
        public GameObject hit => Prefab("hit");
        public GameObject dash => Prefab("dash");
        public GameObject levelUp => Prefab("levelup");
        public GameObject slam => Prefab("slam");

        void Awake()
        {
            Instance = this;
            lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id))
                        continue;
                    lookup[entry.id] = entry;
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public GameObject Prefab(string id)
        {
            if (lookup == null)
                Awake();
            return lookup.TryGetValue(id, out var entry) ? entry.prefab : null;
        }

        public GameObject Play(string id, Vector3 position)
        {
            return Spawn(id, position, null);
        }

        public GameObject Play(string id, Vector3 position, Color color)
        {
            return Spawn(id, position, color);
        }

        GameObject Spawn(string id, Vector3 position, Color? color)
        {
            if (lookup == null || id == null || !lookup.TryGetValue(id, out var entry) || entry.prefab == null)
                return null;
            var instance = Instantiate(entry.prefab, position, Quaternion.identity);
            if (color.HasValue)
            {
                foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = particles.main;
                    main.startColor = color.Value;
                }
            }
            Destroy(instance, entry.lifetime);
            return instance;
        }
    }
}
