using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Marker for solid arena props. Their colliders live on the Obstacle layer.</summary>
    public class Obstacle : MonoBehaviour
    {
        public enum Kind
        {
            Rock,
            Tree,
            Ruin
        }

        public Kind kind = Kind.Rock;
        /// <summary>Horizontal radius used to keep obstacles apart and away from spawn points.</summary>
        public float footprint = 1f;

        public bool AllowedIn(Biome biome)
        {
            // Deserts have no living trees; everything else fits every biome.
            return !(biome == Biome.Desert && kind == Kind.Tree);
        }
    }

    /// <summary>Physics queries against arena obstacles: line of sight, projectile blocking, and pushing bodies out of props.</summary>
    public static class Obstacles
    {
        public const string LayerName = "Obstacle";
        const float ProbeHeight = 0.35f;
        static int mask = -1;
        static readonly Collider[] buffer = new Collider[12];

        public static int Layer
        {
            get
            {
                int layer = LayerMask.NameToLayer(LayerName);
                return layer >= 0 ? layer : 0;
            }
        }

        public static int Mask
        {
            get
            {
                if (mask < 0)
                {
                    int layer = LayerMask.NameToLayer(LayerName);
                    mask = layer >= 0 ? 1 << layer : 0;
                }
                return mask;
            }
        }

        public static bool Blocked(Vector3 from, Vector3 to)
        {
            return Mask != 0 && Physics.Linecast(from, to, Mask, QueryTriggerInteraction.Ignore);
        }

        public static bool Cast(Vector3 from, Vector3 delta, float radius, out RaycastHit hit)
        {
            hit = default;
            if (Mask == 0 || delta.sqrMagnitude < 0.000001f)
                return false;
            float distance = delta.magnitude;
            if (radius > 0.001f)
                return Physics.SphereCast(from, radius, delta / distance, out hit, distance, Mask, QueryTriggerInteraction.Ignore);
            return Physics.Raycast(from, delta / distance, out hit, distance, Mask, QueryTriggerInteraction.Ignore);
        }

        public static bool Overlaps(Vector3 position, float radius)
        {
            return Mask != 0 && Physics.CheckSphere(position + Vector3.up * ProbeHeight, radius, Mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Returns the position pushed out of any obstacle it overlaps (XZ only).</summary>
        public static Vector3 Resolve(Vector3 position, float radius)
        {
            if (Mask == 0)
                return position;
            Vector3 probe = position + Vector3.up * ProbeHeight;
            int count = Physics.OverlapSphereNonAlloc(probe, radius, buffer, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i];
                if (collider == null)
                    continue;
                Vector3 closest = collider.ClosestPoint(probe);
                Vector3 push = probe - closest;
                push.y = 0f;
                float distance = push.magnitude;
                if (distance < 0.001f)
                {
                    // Probe is inside the collider: leave through the nearest side of its bounds.
                    push = probe - collider.bounds.center;
                    push.y = 0f;
                    if (push.sqrMagnitude < 0.0001f)
                        push = Vector3.forward;
                    position += push.normalized * radius;
                }
                else if (distance < radius)
                {
                    position += push / distance * (radius - distance);
                }
                probe = position + Vector3.up * ProbeHeight;
            }
            return position;
        }

        /// <summary>Bends a desired move direction around an obstacle directly ahead so chasers slide along props instead of sticking.</summary>
        public static Vector3 Steer(Vector3 position, Vector3 direction, float radius, float lookAhead)
        {
            if (Mask == 0 || direction.sqrMagnitude < 0.0001f)
                return direction;
            Vector3 origin = position + Vector3.up * ProbeHeight;
            Vector3 forward = direction.normalized;
            if (!Physics.SphereCast(origin, radius * 0.7f, forward, out var hit, lookAhead, Mask, QueryTriggerInteraction.Ignore))
                return direction;
            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f)
                return direction;
            normal.Normalize();
            Vector3 slide = Vector3.ProjectOnPlane(forward, normal);
            if (slide.sqrMagnitude < 0.05f)
            {
                // Head-on: pick the tangent that keeps us closer to the original heading.
                Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                slide = Vector3.Dot(tangent, forward) >= 0f ? tangent : -tangent;
            }
            float urgency = 1f - Mathf.Clamp01(hit.distance / lookAhead);
            Vector3 blended = Vector3.Lerp(forward, slide.normalized, Mathf.Lerp(0.55f, 1f, urgency));
            return blended.normalized * direction.magnitude;
        }
    }
}
