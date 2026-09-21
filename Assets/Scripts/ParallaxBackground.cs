using UnityEngine;

/// <summary>
/// Parallax scrolling effect for the Level 3 vertical scrolling race.
/// Creates a sense of depth by moving background layers at different speeds
/// relative to how much the camera has moved.
/// 
/// HOW PARALLAX WORKS:
///   A parallaxFactor of 0 means the layer doesn't move at all (appears infinitely far away).
///   A parallaxFactor of 1 means the layer moves exactly with the camera (no parallax effect).
///   Values between 0 and 1 create the illusion of depth — lower = further away.
/// 
/// TYPICAL SETUP FOR A VERTICAL RACER:
///   • Road tiles       — no parallax needed (they're the gameplay world)
///   • Near scenery     — parallaxFactor ≈ 0.9  (trees close to the road)
///   • Far scenery      — parallaxFactor ≈ 0.5  (distant buildings / hills)
///   • Sky / far fill   — parallaxFactor ≈ 0.3  (barely moves)
/// 
/// INFINITE SCROLLING:
///   When infiniteScroll is enabled, the layer repositions itself (jumps by tileHeight
///   or tileWidth) once it drifts too far from the camera. This creates seamless looping
///   without needing a very large background sprite.
/// 
/// HOW TO SET UP:
///   1. Create an empty parent GameObject (e.g. "ParallaxLayer_Far").
///   2. Place all side-decoration sprites as children of that parent.
///   3. Add this script to the parent.
///   4. Tune parallaxFactor to the desired depth illusion.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Parallax Settings")]
    [Tooltip("0 = completely static, 1 = moves with camera (no effect). Use 0.3-0.9 for visible parallax.")]
    [Range(0f, 1f)]
    // Lower = appears further away and moves less. Higher = closer and moves more.
    [SerializeField] private float parallaxFactor = 0.5f;

    [Header("Axis Control")]
    // Disable X or Y to create a purely horizontal or purely vertical parallax layer.
    [SerializeField] private bool parallaxOnX = true;
    [SerializeField] private bool parallaxOnY = true;

    [Header("Infinite Scrolling (optional)")]
    [Tooltip("If enabled, repositions this layer to loop infinitely. Requires the children to tile seamlessly.")]
    [SerializeField] private bool infiniteScroll;
    [Tooltip("The height of one full tile set (used for Y looping). Measure your background sprites total height.")]
    [SerializeField] private float tileHeight = 20f;
    [Tooltip("The width of one full tile set (used for X looping).")]
    [SerializeField] private float tileWidth = 20f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Cached reference to Main Camera's transform — read each LateUpdate.
    private Transform _cameraTransform;
    // Camera position from the previous frame — used to calculate how much the camera moved.
    private Vector3 _previousCameraPosition;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        if (_cameraTransform != null)
            _previousCameraPosition = _cameraTransform.position;
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        // How far the camera moved since last frame.
        Vector3 cameraDelta = _cameraTransform.position - _previousCameraPosition;
        // Update baseline for next frame.
        _previousCameraPosition = _cameraTransform.position;

        // ---- Parallax offset calculation ----
        // We want the layer to move LESS than the camera by (1 - parallaxFactor).
        // Example: parallaxFactor = 0.7 → the layer moves 30% of the camera movement.
        // The layer is moved AGAINST the camera direction to counteract camera movement,
        // which makes it appear to "lag behind" and thus look further away.
        float offsetX = parallaxOnX ? cameraDelta.x * (1f - parallaxFactor) : 0f;
        float offsetY = parallaxOnY ? cameraDelta.y * (1f - parallaxFactor) : 0f;

        transform.position -= new Vector3(offsetX, offsetY, 0f);

        // ---- Infinite scroll looping ----
        // Once the layer's position drifts more than one tile span from the camera,
        // snap it back by exactly one tile span. This creates seamless looping.
        if (infiniteScroll)
        {
            Vector3 pos  = transform.position;
            float   camX = _cameraTransform.position.x;
            float   camY = _cameraTransform.position.y;

            if (parallaxOnY && tileHeight > 0f)
            {
                float distY = camY - pos.y;
                // If the layer has drifted a full tile away, jump it back.
                if (Mathf.Abs(distY) > tileHeight)
                    pos.y += Mathf.Sign(distY) * tileHeight;
            }

            if (parallaxOnX && tileWidth > 0f)
            {
                float distX = camX - pos.x;
                if (Mathf.Abs(distX) > tileWidth)
                    pos.x += Mathf.Sign(distX) * tileWidth;
            }

            transform.position = pos;
        }
    }
}
