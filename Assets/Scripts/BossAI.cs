using UnityEngine;

/// <summary>
/// Controls the boss car in Level 3's vertical scrolling race.
/// 
/// BEHAVIOR OVERVIEW:
///   The boss follows a pre-placed array of Transform waypoints that trace the road.
///   Each physics frame it:
///     1. Rotates smoothly toward the next waypoint.
///     2. Scales its speed down on sharp turns (so it doesn't cut corners unrealistically).
///     3. Optionally slows near obstacles detected by a forward raycast.
///     4. Applies a velocity toward the waypoint using MoveTowards (no overshooting).
///     5. Advances to the next waypoint when it gets close enough.
///     6. Runs a stuck-detection check and snaps itself free if it hasn't moved enough.
/// 
/// DIFFERENCES FROM OpponentAI:
///   - Single boss car (not an array) — simpler, no rubber-banding.
///   - Designed for a vertical track (movement is mostly upward on screen).
///   - Has a public LapsCompleted field for Level3RaceManager to query.
/// 
/// HOW TO SET UP:
///   1. Add a Rigidbody2D (gravity scale 0, no frozen axes).
///   2. Tag this GameObject "Opponent" so Level3FinishLine can detect it.
///   3. Place empty GameObjects along the road and assign them to the Waypoints array.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossAI : MonoBehaviour
{
    [Header("Waypoints")]
    // The ordered list of targets the boss drives toward, one by one.
    [SerializeField] private Transform[] waypoints;

    [Header("Speed")]
    // Normal driving speed in world units per second.
    [SerializeField] private float baseSpeed = 3.5f;
    [Tooltip("Speed when approaching a sharp turn.")]
    // Reduced speed used when the car is facing a tight corner.
    [SerializeField] private float turnSpeed = 2f;
    // How quickly the boss reaches its target speed (units/s per second).
    [SerializeField] private float acceleration = 15f;
    // Maximum angular velocity in degrees/second for rotating toward waypoints.
    [SerializeField] private float rotationSpeed = 280f;

    [Header("Turn Detection")]
    // If the angle between the car's forward and the waypoint direction
    // exceeds this value (degrees), the car switches to turnSpeed.
    [SerializeField] private float sharpTurnAngle = 35f;

    [Header("Obstacle Avoidance (optional)")]
    [Tooltip("If true, boss slows slightly near obstacles.")]
    // Toggle the forward raycast obstacle check on/off from the Inspector.
    [SerializeField] private bool avoidsObstacles = true;
    // Length of the forward detection ray in world units.
    [SerializeField] private float avoidanceRayLength = 2f;
    // Only objects on this layer are considered obstacles by the raycast.
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Stuck Recovery")]
    [Tooltip("Seconds without meaningful movement before triggering recovery.")]
    // How long the boss must be nearly stationary before recovery kicks in.
    [SerializeField] private float stuckTimeThreshold = 1.5f;
    [Tooltip("Minimum distance moved per second to be considered not stuck.")]
    // Anything slower than this (units/s) counts as "not moving".
    [SerializeField] private float stuckSpeedThreshold = 0.3f;
    [Tooltip("Units pushed toward the next waypoint when recovery triggers.")]
    // A small position nudge that breaks contact with whatever is blocking the boss.
    [SerializeField] private float recoveryNudge = 1.2f;

    [Header("Finish")]
    // Incremented by Level3FinishLine when the boss crosses the finish line.
    // Public so Level3RaceManager can check whether the boss finished first.
    public int LapsCompleted;

    private Rigidbody2D _rb;

    // Index into the waypoints array — points at the waypoint we are currently driving toward.
    private int _currentWaypointIndex;

    // The speed we are currently trying to reach this frame (changes on turns/obstacles).
    private float _currentTargetSpeed;

    // When true, all movement is disabled (used during race countdown).
    private bool _frozen;

    // When true, the boss has reached its final waypoint and stops.
    private bool _finished;

    // ---- Stuck detection ----
    // Position recorded at the start of each physics frame for movement measurement.
    private Vector2 _lastCheckedPosition;
    // Accumulates time the boss has been moving below stuckSpeedThreshold.
    private float _stuckTimer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _currentTargetSpeed = baseSpeed;

        // Record spawn position as the baseline for stuck detection.
        _lastCheckedPosition = _rb.position;
    }

    // ------------------------------------------------------------------ //
    //  Public API (called by RaceCountdown and Level3RaceManager)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Completely stops the boss and disables physics simulation.
    /// Called by RaceCountdown at the start of the level so the boss
    /// doesn't move until the 3-2-1-GO sequence finishes.
    /// </summary>
    public void Freeze()
    {
        _frozen = true;
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        // Disabling simulation also stops collision detection — no sliding during countdown.
        _rb.simulated = false;
    }

    /// <summary>
    /// Re-enables physics and allows the boss to start driving.
    /// Called by RaceCountdown once the countdown reaches GO.
    /// </summary>
    public void Unfreeze()
    {
        _rb.simulated       = true;
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _frozen             = false;
    }

    /// <summary>Increments the lap counter. Called by Level3FinishLine on trigger.</summary>
    public void IncrementLap() { LapsCompleted++; }

    /// <summary>
    /// Returns the straight-line distance to the current target waypoint.
    /// Used by Level3RaceManager to show "ahead/behind" status to the player.
    /// </summary>
    public float DistanceToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return float.MaxValue;
        if (_currentWaypointIndex >= waypoints.Length)  return 0f;
        return Vector2.Distance(_rb.position, waypoints[_currentWaypointIndex].position);
    }

    private void FixedUpdate()
    {
        // Skip all movement while frozen or after reaching the final waypoint.
        if (_frozen || _finished) return;
        if (waypoints == null || waypoints.Length == 0) return;

        // The boss has run out of waypoints — stop and mark as finished.
        if (_currentWaypointIndex >= waypoints.Length)
        {
            _finished = true;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPos      = waypoints[_currentWaypointIndex].position;
        Vector2 toTarget       = targetPos - _rb.position;
        float   distToWaypoint = toTarget.magnitude;

        // ---- Step 1: Rotate toward the waypoint ----
        // Convert the direction vector into a target angle in degrees.
        // The -90° offset accounts for Unity's "up = 0°" sprite convention.
        float targetAngle  = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        // DeltaAngle gives the shortest signed angle between current and target heading.
        float angleError   = Mathf.DeltaAngle(_rb.rotation, targetAngle);
        // Map the angle error to an angular velocity target, clamped to our max rotation speed.
        float targetAngVel = Mathf.Clamp(angleError * 6f, -rotationSpeed, rotationSpeed);
        // Smoothly move toward that angular velocity rather than snapping.
        _rb.angularVelocity = Mathf.MoveTowards(
            _rb.angularVelocity,
            targetAngVel,
            rotationSpeed * 8f * Time.fixedDeltaTime
        );

        // ---- Step 2: Decide target speed ----
        // Measure how far off-axis the waypoint is relative to the car's forward direction.
        float angleToWaypoint = Vector2.Angle(transform.up, toTarget.normalized);

        // If we are facing a sharp corner, blend from baseSpeed down toward turnSpeed.
        _currentTargetSpeed = angleToWaypoint > sharpTurnAngle
            ? Mathf.Lerp(baseSpeed, turnSpeed, (angleToWaypoint - sharpTurnAngle) / 60f)
            : baseSpeed;

        // ---- Step 3: Obstacle avoidance (optional) ----
        // Cast a ray directly ahead; if something is in the way, slow down by 40%.
        if (avoidsObstacles)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                _rb.position, transform.up, avoidanceRayLength, obstacleLayer);
            if (hit.collider != null)
                _currentTargetSpeed *= 0.6f;
        }

        // ---- Step 4: Apply velocity ----
        // Desired velocity is directly forward at the chosen speed.
        Vector2 desiredVelocity = transform.up * _currentTargetSpeed;
        // MoveTowards gives a smooth acceleration without overshooting.
        _rb.linearVelocity = Vector2.MoveTowards(
            _rb.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );

        // Hard cap to prevent edge cases from exceeding the speed limit.
        if (_rb.linearVelocity.magnitude > baseSpeed * 1.1f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * baseSpeed * 1.1f;

        // ---- Step 5: Advance to next waypoint when close enough ----
        if (distToWaypoint < 1.2f)
            _currentWaypointIndex++;

        // ---- Step 6: Stuck recovery ----
        CheckAndRecoverIfStuck();
    }

    // ------------------------------------------------------------------ //
    //  Stuck recovery helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Compares the boss's current position against where it was last frame.
    /// If it has been moving slower than stuckSpeedThreshold for stuckTimeThreshold
    /// seconds, calls RecoverFromStuck() to break free.
    /// 
    /// This handles situations like the boss getting wedged against a wall after a
    /// physics collision and spinning in place without going anywhere.
    /// </summary>
    private void CheckAndRecoverIfStuck()
    {
        // Convert distance moved since last check into an approximate speed.
        float distanceMoved = Vector2.Distance(_rb.position, _lastCheckedPosition);

        if (distanceMoved / Time.fixedDeltaTime < stuckSpeedThreshold)
        {
            // Boss is barely moving — accumulate stuck time.
            _stuckTimer += Time.fixedDeltaTime;

            if (_stuckTimer >= stuckTimeThreshold)
            {
                // Stuck for long enough — trigger recovery and reset the timer.
                RecoverFromStuck();
                _stuckTimer = 0f;
            }
        }
        else
        {
            // Boss is moving normally — reset the stuck timer.
            _stuckTimer = 0f;
        }

        // Update the baseline position for the next frame's comparison.
        _lastCheckedPosition = _rb.position;
    }

    /// <summary>
    /// Snaps the boss out of a stuck state by:
    ///   1. Pointing it directly at the current waypoint.
    ///   2. Physically nudging it in that direction to clear wall contact.
    ///   3. Giving it a small starting velocity so it immediately begins moving again.
    /// </summary>
    private void RecoverFromStuck()
    {
        if (waypoints == null || _currentWaypointIndex >= waypoints.Length) return;

        // Direction vector from current position to the target waypoint.
        Vector2 toTarget = (waypoints[_currentWaypointIndex].position - (Vector3)_rb.position).normalized;

        // Instantly face the waypoint so the car drives straight toward it.
        float angle         = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        _rb.rotation        = angle;
        _rb.angularVelocity = 0f;

        // Push the car clear of whatever is blocking it, then give it a gentle kick.
        _rb.position       += toTarget * recoveryNudge;
        _rb.linearVelocity  = toTarget * baseSpeed * 0.5f;
    }
}
