using UnityEngine;

namespace TurboDash.Track
{
    /// <summary>
    /// Marks a wall or barrier as a track boundary and automatically applies
    /// a PhysicsMaterial2D with configurable bounciness to its colliders.
    ///
    /// WHY THIS EXISTS:
    ///   Rather than manually creating and assigning a PhysicsMaterial2D asset in the
    ///   Project window for every wall in the scene, this script generates the material
    ///   at runtime so wall feel (bounce, friction) can be adjusted from a single
    ///   Inspector without hunting through asset files.
    ///
    /// STATIC HELPER:
    ///   IsTrackBoundary() is a utility method used by other scripts (e.g. car controllers)
    ///   to distinguish wall collisions from car-to-car collisions without relying on tags.
    ///
    /// EDITOR GIZMO:
    ///   Draws a transparent red box in the Scene view so boundaries are visible
    ///   even when the sprite / edge is hard to see.
    ///
    /// HOW TO SET UP:
    ///   1. Add to any wall GameObject that has an EdgeCollider2D or BoxCollider2D.
    ///   2. Adjust bounciness (0 = absorbs all impact, 1 = full elastic bounce).
    ///   3. Adjust friction (0 = ice-like, 1 = lots of sliding resistance).
    ///   The PhysicsMaterial2D is created and assigned automatically in Awake().
    /// </summary>
    public class TrackBoundary : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Static helper
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns true if the given collider (or its parent) has a TrackBoundary component.
        /// Use this to identify wall contacts in OnCollisionEnter2D without needing a tag.
        /// </summary>
        public static bool IsTrackBoundary(Collider2D collider)
        {
            if (collider == null) return false;
            return collider.GetComponent<TrackBoundary>() != null
                || collider.GetComponentInParent<TrackBoundary>() != null;
        }

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Tooltip("Optional label to identify this boundary in the editor.")]
        // Purely for organization — shown in the generated PhysicsMaterial2D name.
        [SerializeField] private string boundaryLabel = "Wall";

        [Header("Bounciness")]
        [Tooltip("How bouncy the wall is (0 = no bounce, 1 = full elastic bounce).")]
        [Range(0f, 1f)]
        // 0.35 gives a slight bounce that feels physical without being unrealistic.
        [SerializeField] private float bounciness = 0.35f;

        [Tooltip("Friction coefficient on the wall surface.")]
        [Range(0f, 1f)]
        // Low friction (0.1) so the car doesn't get dragged along the wall surface.
        [SerializeField] private float friction = 0.1f;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            ApplyPhysicsMaterial();
        }

        private void Reset()
        {
            // When this component is first added via the Inspector, ensure a collider exists.
            // EdgeCollider2D is the default — replace with BoxCollider2D for solid rectangles.
            if (GetComponent<Collider2D>() == null)
                gameObject.AddComponent<EdgeCollider2D>();
        }

        // ------------------------------------------------------------------ //
        //  Material application
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a PhysicsMaterial2D with the configured bounciness and friction,
        /// then assigns it to every Collider2D on this GameObject.
        /// Called in Awake() so it runs before any collision can occur.
        /// </summary>
        private void ApplyPhysicsMaterial()
        {
            var mat = new PhysicsMaterial2D($"TrackBoundary_{boundaryLabel}")
            {
                bounciness = bounciness,
                friction   = friction
            };

            // Apply to all colliders on this object (e.g. a wall may have multiple segments).
            foreach (var col in GetComponents<Collider2D>())
                col.sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ //
        //  Editor gizmo
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw a transparent red box over BoxCollider2D boundaries in the Scene view.
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
            }
        }
#endif
    }
}
