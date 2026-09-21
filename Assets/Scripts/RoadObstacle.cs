using UnityEngine;

/// <summary>
/// An obstacle on the road that applies a physics effect to the player car on contact.
/// Supports three types of hit behavior: SlowDown, SpinOut, and FullStop.
/// 
/// OBSTACLE TYPES:
///   SlowDown  — temporarily reduces the car's maxSpeed by a factor, then restores it.
///   SpinOut   — applies an angular velocity impulse and halves forward speed.
///   FullStop  — zeroes velocity and pushes the car away from the obstacle.
/// 
/// TRIGGER vs COLLISION:
///   Use OnTriggerEnter2D for soft obstacles like oil slicks (player passes through).
///   Use OnCollisionEnter2D for solid obstacles like cones (player bounces off).
///   Both are implemented — the right one fires depending on the Collider2D's Is Trigger setting.
/// 
/// REUSABLE vs DESTROY:
///   destroyOnHit = true  → removes the obstacle after one hit (cones, barrels).
///   isReusable = true    → hides for reuseCooldown seconds, then becomes active again (oil spills).
/// 
/// HOW TO SET UP:
///   1. Add to an obstacle sprite with a Collider2D.
///   2. Choose the obstacle type and whether it is trigger/solid.
///   3. The player car must have the tag "Player" to be affected.
/// </summary>
public class RoadObstacle : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Obstacle type enum
    // ------------------------------------------------------------------ //

    public enum ObstacleType
    {
        SlowDown,  // Temporarily reduces max speed
        SpinOut,   // Applies a random spin and cuts speed
        FullStop   // Zeroes velocity and pushes away
    }

    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Obstacle Behavior")]
    [SerializeField] private ObstacleType type = ObstacleType.SlowDown;

    [Header("SlowDown Settings")]
    [Tooltip("Speed multiplier when driving through (0.5 = half speed).")]
    // e.g. 0.4 = car drives at 40% max speed while the slow effect is active.
    [SerializeField] private float slowFactor = 0.4f;
    // How long the slow effect lasts in seconds.
    [SerializeField] private float slowDuration = 1f;

    [Header("SpinOut Settings")]
    [Tooltip("Angular impulse applied on hit.")]
    // Higher values = more violent spin. The direction is randomized each hit.
    [SerializeField] private float spinForce = 300f;

    [Header("FullStop Settings")]
    // Force applied away from the obstacle to prevent the player from getting stuck.
    [SerializeField] private float bounceForce = 3f;

    [Header("Visuals")]
    [Tooltip("Destroy or disable the obstacle after being hit?")]
    [SerializeField] private bool destroyOnHit = true;

    [Header("Reusable (oil spills)")]
    [Tooltip("If true, the obstacle resets after a cooldown instead of being destroyed. Overrides destroyOnHit.")]
    // Use this for oil slicks that should come back after the player drives over them.
    [SerializeField] private bool isReusable = false;
    // Seconds before the obstacle becomes active again after a hit.
    [SerializeField] private float reuseCooldown = 3f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Guards against the trigger/collision firing twice on the same frame.
    private bool _alreadyHit;

    // ------------------------------------------------------------------ //
    //  Trigger path (non-solid obstacles — oil slicks, speed pads)
    // ------------------------------------------------------------------ //

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only the player car is affected by obstacles.
        if (_alreadyHit || !other.CompareTag("Player")) return;

        var car = other.GetComponent<TopDownCarController>();
        var rb  = other.GetComponent<Rigidbody2D>();
        if (car == null || rb == null) return;

        _alreadyHit = true;

        switch (type)
        {
            case ObstacleType.SlowDown:
                // ApplySlow is a coroutine — it handles reset/destroy itself so we return early.
                StartCoroutine(ApplySlow(rb));
                return;

            case ObstacleType.SpinOut:
                // Random left or right spin with 50/50 chance.
                float spinDir = Random.value > 0.5f ? 1f : -1f;
                rb.angularVelocity += spinForce * spinDir;
                rb.linearVelocity  *= 0.5f; // cut forward speed
                break;

            case ObstacleType.FullStop:
                rb.linearVelocity = Vector2.zero;
                // Push the car away from the obstacle's position.
                Vector2 pushDir = (other.transform.position - transform.position).normalized;
                rb.AddForce(pushDir * bounceForce, ForceMode2D.Impulse);
                break;
        }

        HandleAfterHit();
    }

    // ------------------------------------------------------------------ //
    //  Collision path (solid obstacles — cones, barrels)
    // ------------------------------------------------------------------ //

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_alreadyHit || !collision.gameObject.CompareTag("Player")) return;

        var rb = collision.rigidbody;
        if (rb == null) return;

        _alreadyHit = true;

        switch (type)
        {
            case ObstacleType.FullStop:
                // Almost entirely stop the car on impact.
                rb.linearVelocity *= 0.1f;
                break;
            case ObstacleType.SpinOut:
                float spinDir = Random.value > 0.5f ? 1f : -1f;
                rb.angularVelocity += spinForce * spinDir;
                break;
            default:
                // SlowDown through a solid object just reduces speed.
                rb.linearVelocity *= slowFactor;
                break;
        }

        HandleAfterHit();
    }

    // ------------------------------------------------------------------ //
    //  Shared post-hit logic
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Decides whether to destroy, deactivate, or start a reuse cooldown
    /// after the obstacle has been hit. Called by both trigger and collision paths.
    /// </summary>
    private void HandleAfterHit()
    {
        if (isReusable)
            StartCoroutine(ReuseCooldownRoutine());
        else if (destroyOnHit)
            Destroy(gameObject);
    }

    /// <summary>
    /// Temporarily reduces the player car's max speed, then restores it.
    /// Called only for SlowDown-type trigger obstacles.
    /// </summary>
    private System.Collections.IEnumerator ApplySlow(Rigidbody2D rb)
    {
        var   car        = rb.GetComponent<TopDownCarController>();
        float originalMax = car != null ? car.maxSpeed : 12f;

        // Apply the speed penalty immediately.
        if (car != null)
            car.maxSpeed *= slowFactor;

        yield return new WaitForSeconds(slowDuration);

        // Restore the original speed after the duration.
        if (car != null)
            car.maxSpeed = originalMax;

        // Clean up the obstacle itself after the effect has run.
        if (isReusable)
            StartCoroutine(ReuseCooldownRoutine());
        else if (destroyOnHit)
            Destroy(gameObject);
    }

    /// <summary>
    /// Waits for the reuse cooldown, then re-arms the obstacle
    /// so it can affect the player again on the next contact.
    /// </summary>
    private System.Collections.IEnumerator ReuseCooldownRoutine()
    {
        yield return new WaitForSeconds(reuseCooldown);
        // Reset the guard flag — the obstacle is active again.
        _alreadyHit = false;
    }
}
