using UnityEngine;

/// <summary>
/// Simple camera for Level 3's vertical scrolling race.
/// Locks the camera's XY to the player car's position every LateUpdate.
/// 
/// DIFFERENCES FROM CameraFollow:
///   VerticalRaceCamera does NO smoothing, look-ahead, or bounds clamping.
///   It is intentionally simple — Level 3's camera is meant to scroll directly
///   with the player without any lag, since the road is always straight upward.
///   Use CameraFollow for Levels 1 and 2 where smooth look-ahead and bounds
///   clamping matter.
/// 
/// HOW TO SET UP:
///   1. Attach to the Main Camera in the Level 3 scene.
///   2. Drag the player car Transform into the "target" field.
///   3. Set orthographicSize to your desired zoom level.
/// </summary>
[RequireComponent(typeof(Camera))]
public class VerticalRaceCamera : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // The Transform to follow — should be the player car.
    [SerializeField] private Transform target;
    // Controls how much of the world fits on screen (smaller = more zoomed in).
    [SerializeField] private float orthographicSize = 5f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private Camera _cam;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        // Set the zoom level immediately so there is no one-frame pop.
        _cam.orthographicSize = orthographicSize;
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all car movement (FixedUpdate) and input (Update)
        // have completed, so the camera always sees the car's final position for the frame.
        if (target == null) return;

        // Lock camera XY to the target. Z = -10 keeps the camera behind the 2D world.
        transform.position = new Vector3(target.position.x, target.position.y, -10f);
    }
}
