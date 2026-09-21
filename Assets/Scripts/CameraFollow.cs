using UnityEngine;

/// <summary>
/// Smooth camera that follows the player car with look-ahead and world-space clamping.
/// 
/// HOW IT WORKS:
///   Every LateUpdate (after all movement has finished for the frame) the camera:
///     1. Computes a look-ahead offset based on the car's velocity direction,
///        so the player can see more road ahead of them rather than behind.
///     2. Smoothly lerps the camera's XY toward (target position + look-ahead).
///     3. Clamps the result so the camera never slides outside the defined track bounds.
///   The Z position is always fixed (offset.z) so rendering depth stays constant.
/// 
/// BOUNDS:
///   Either assign a BoxCollider2D in the Inspector (its world-space bounds are used
///   automatically) or set the min/max values manually.
/// 
/// HOW TO SET UP:
///   1. Attach to the Main Camera.
///   2. Drag the player car Transform into "target".
///   3. Either assign a BoxCollider2D for bounds, or fill in the manual min/max fields.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // The Transform to follow — should be the player car.
    [SerializeField] private Transform target;
    // Higher smoothSpeed = snappier camera, lower = more cinematic lag.
    [SerializeField] private float smoothSpeed = 8f;
    // X/Y offset from target (usually 0), and Z sets the camera depth (-10 for 2D).
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Look-Ahead")]
    [Tooltip("How far ahead of the car the camera looks, based on velocity direction.")]
    // At max speed the camera shifts this many world units in the direction the car is going.
    [SerializeField] private float lookAheadDistance = 1.5f;
    // Lower = slower look-ahead response (more dramatic), higher = instant.
    [SerializeField] private float lookAheadSmooth = 4f;

    [Header("Zoom")]
    [Tooltip("Desired orthographic size. Smaller = more zoomed in.")]
    // Controls how much of the world fits on screen.
    [SerializeField] private float orthographicSize = 3.5f;

    [Header("Bounds")]
    [Tooltip("Optional: assign a BoxCollider2D whose bounds define the camera area. Overrides manual min/max values.")]
    // If assigned, this collider's world bounds replace the manual min/max below.
    [SerializeField] private BoxCollider2D boundsCollider;

    [Header("Manual Bounds (used if no boundsCollider is assigned)")]
    [SerializeField] private float minX = -12f;
    [SerializeField] private float maxX =  12f;
    [SerializeField] private float minY = -6f;
    [SerializeField] private float maxY =  6f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private Camera _cam;
    // The smoothed look-ahead offset in world units — interpolated each frame.
    private Vector3 _lookAheadOffset;
    // Cached Rigidbody2D on the target — used to read its velocity for look-ahead.
    private Rigidbody2D _targetRb;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        // Set orthographic size immediately so there is no one-frame pop at startup.
        _cam.orthographicSize = orthographicSize;
    }

    private void Start()
    {
        // Cache the Rigidbody2D for velocity-based look-ahead.
        if (target != null)
            _targetRb = target.GetComponent<Rigidbody2D>();

        // Override manual bounds with the collider's actual bounds if one is assigned.
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            minX = b.min.x;
            maxX = b.max.x;
            minY = b.min.y;
            maxY = b.max.y;
        }

        // Snap to the target on frame 1 — prevents the camera flying in from (0,0).
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, offset.z);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // ---- Look-ahead ----
        // Only look ahead if the car is moving (sqrMagnitude avoids a sqrt call).
        Vector3 targetLookAhead = Vector3.zero;
        if (_targetRb != null && _targetRb.linearVelocity.sqrMagnitude > 0.5f)
        {
            // Shift in the direction the car is moving, scaled by lookAheadDistance.
            targetLookAhead = (Vector3)_targetRb.linearVelocity.normalized * lookAheadDistance;
        }
        // Lerp smooths out sudden direction changes so the camera doesn't jerk.
        _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, targetLookAhead, lookAheadSmooth * Time.deltaTime);

        // ---- Desired position ----
        // Combine target XY with the look-ahead offset.
        Vector2 desiredXY  = (Vector2)target.position + (Vector2)_lookAheadOffset;
        Vector2 currentXY  = transform.position;
        // Lerp the camera toward the desired position for smooth lag.
        Vector2 smoothedXY = Vector2.Lerp(currentXY, desiredXY, smoothSpeed * Time.deltaTime);

        // ---- Bounds clamping ----
        // The visible half-extents of the camera in world units.
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        // If the bounds region is large enough to scroll, clamp to keep edges on-screen.
        // If the bounds are smaller than the camera view, just centre the camera on the bounds.
        float camX = (maxX - minX) > halfW * 2f
            ? Mathf.Clamp(smoothedXY.x, minX + halfW, maxX - halfW)
            : (minX + maxX) * 0.5f;

        float camY = (maxY - minY) > halfH * 2f
            ? Mathf.Clamp(smoothedXY.y, minY + halfH, maxY - halfH)
            : (minY + maxY) * 0.5f;

        // Z is always fixed — never modified by the lerp above.
        transform.position = new Vector3(camX, camY, offset.z);
    }
}
