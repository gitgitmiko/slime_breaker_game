using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Yaw-free size of an environment prop at scale 1 (measured when the prefab is built). ArenaGenerator sizes solid
    /// colliders from this instead of live renderer bounds, which are cached, inflated by the prop's yaw, and measured
    /// before the prop has been placed.
    /// </summary>
    public class PropShape : MonoBehaviour
    {
        /// <summary>Half of the widest horizontal extent, metres.</summary>
        public float halfWidth = 0.5f;
        /// <summary>Half extents along the prop's own X and Z (yaw-free), so thin props such as arches get thin boxes.</summary>
        public float halfX = 0.5f;
        public float halfZ = 0.5f;
        /// <summary>Height above the ground, metres.</summary>
        public float height = 1f;
        /// <summary>Height of the lowest/solid core to cover (trees: trunk), metres. 0 = full height.</summary>
        public float centreHeight;
    }
}
