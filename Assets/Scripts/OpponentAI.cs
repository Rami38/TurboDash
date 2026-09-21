using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Waypoint-following AI for top-down racing opponents (used in Level 2).
/// 
/// BEHAVIOR OVERVIEW:
///   Each physics frame the AI:
///     1. Runs a stuck-recovery check — if it hasn't moved enough since the
///        last check, it snaps toward the current waypoint and briefly reverses.
///     2. Steers toward the current waypoint by adjusting angular velocity.
///     3. Accelerates forward at a speed reduced on sharp turns.
///     4. Applies lateral grip to prevent the car from sliding sideways.
///     5. Advances to the next waypoint once it gets close enough.
/// 
/// SPEED VARIATION:
///   Each AI picks a random base speed in Awake so opponents don't all drive identically.
/// 
/// RUBBER-BANDING (optional):
///   When a playerTransform is assigned and rubberBandStrength > 0, the AI speeds up
///   when behind the player and slows slightly when far ahead, keeping races close.
/// 
/// TAG REQUIREMENT:
///   Tag this GameObject "Opponent" so FinishLine can detect it.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class OpponentAI : MonoBehaviour
{
    [Header("Waypoints")]
    [SerializeField] private WaypointPath waypointPath;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private int startWaypointIndex;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("How quickly the car reaches its target speed (units/s per second).")]
    [FormerlySerializedAs("accelerationForce")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float rotationSpeed = 260f;
    [SerializeField] private float waypointReachDistance = 1.2f;

    [Header("Grip")]
    [Tooltip("Reduces sideways slide so AI cars corner like the player.")]
    [SerializeField] private float gripStrength = 12f;

    [Header("Turn Slowdown")]
    [Tooltip("Angle threshold (degrees) above which the AI slows down.")]
    [SerializeField] private float sharpTurnAngle = 40f;
    [Tooltip("Minimum speed fraction during a sharp turn (0-1).")]
    [SerializeField] private float sharpTurnSpeedFraction = 0.4f;

    [Header("Variation")]
    [SerializeField] private float speedVariation = 0.6f;

    [Header("Stuck Recovery")]
    [Tooltip("Seconds without meaningful movement before triggering recovery.")]
    [SerializeField] private float stuckCheckInterval = 0.6f;
    [Tooltip("How long to reverse when stuck.")]
    [SerializeField] private float reverseTime = 0.5f;
    [Tooltip("Units pushed toward the next waypoint when recovery triggers.")]
    [SerializeField] private float recoveryNudge = 1.0f;

    [Header("Rubber-Banding (optional)")]
    [Tooltip("Reference to the player transform for rubber-banding. Leave null to disable.")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("How much to adjust speed based on distance to player (0 = disabled).")]
    [SerializeField] private float rubberBandStrength = 0f;

    public int LapsCompleted { get; private set; }

    private Rigidbody2D _rb;
    private int _currentWaypointIndex;
    private float _baseSpeed;
    private bool _frozen;

    private Vector2 _lastCheckedPosition;
    private float _stuckTimer;
    private float _reverseTimer;
    private int _stuckRecoveryCount;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _baseSpeed = moveSpeed + Random.Range(-speedVariation, speedVariation);

        if (waypointPath != null)
            waypoints = waypointPath.GetWaypoints();

        if (waypoints != null && waypoints.Length > 0)
            _currentWaypointIndex = Mathf.Clamp(startWaypointIndex, 0, waypoints.Length - 1);
    }

    private void Start()
    {
        _lastCheckedPosition = _rb.position;
        AdvancePastNulls();
    }

    /// <summary>Freeze the AI (used during countdown).</summary>
    public void Freeze()
    {
        _frozen = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated = false;
    }

    /// <summary>Unfreeze the AI (called when countdown finishes).</summary>
    public void Unfreeze()
    {
        _rb.simulated = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _frozen = false;
        _lastCheckedPosition = _rb.position;
        _stuckTimer = 0f;
    }

    /// <summary>Called by RaceManager via FinishLine when this opponent crosses the finish.</summary>
    public void IncrementLap()
    {
        LapsCompleted++;
    }

    /// <summary>Returns distance to the current waypoint target (used for position calculation).</summary>
    public float DistanceToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return float.MaxValue;
        if (waypoints[_currentWaypointIndex] == null) return float.MaxValue;
        return Vector2.Distance(_rb.position, waypoints[_currentWaypointIndex].position);
    }

    private void FixedUpdate()
    {
        if (_frozen) return;
        if (waypoints == null || waypoints.Length == 0) return;
        if (waypoints[_currentWaypointIndex] == null) { AdvancePastNulls(); return; }

        UpdateStuckRecovery();

        if (_reverseTimer > 0f)
        {
            _reverseTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity,
                -transform.up * _baseSpeed * 0.5f,
                acceleration * Time.fixedDeltaTime
            );
            ApplyLateralGrip();
            return;
        }

        SteerTowardsWaypoint();
        MoveForward();
        CheckWaypointReached();
    }

    private void UpdateStuckRecovery()
    {
        _stuckTimer += Time.fixedDeltaTime;
        if (_stuckTimer < stuckCheckInterval) return;

        float moved = Vector2.Distance(_rb.position, _lastCheckedPosition);
        if (moved < 0.25f)
        {
            _stuckRecoveryCount++;

            // On the first stuck event just nudge forward toward the waypoint.
            // On repeated consecutive stuck events (truly wedged), also skip
            // the waypoint so the car doesn't keep targeting an unreachable point.
            if (_stuckRecoveryCount >= 2)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
                AdvancePastNulls();
                _stuckRecoveryCount = 0;
            }

            // Snap heading toward current target then nudge forward to break contact.
            SnapTowardCurrentWaypoint();
            _reverseTimer = reverseTime;
        }
        else
        {
            // Moving normally — reset the consecutive stuck counter.
            _stuckRecoveryCount = 0;
        }

        _lastCheckedPosition = _rb.position;
        _stuckTimer = 0f;
    }

    /// <summary>
    /// Instantly aligns the car's rotation toward the current waypoint and
    /// pushes it slightly in that direction to clear any obstacle contact.
    /// </summary>
    private void SnapTowardCurrentWaypoint()
    {
        if (waypoints == null || waypoints[_currentWaypointIndex] == null) return;

        Vector2 toTarget = (Vector2)waypoints[_currentWaypointIndex].position - _rb.position;
        float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;

        // Face the waypoint and clear any spin.
        _rb.rotation = angle;
        _rb.angularVelocity = 0f;

        // Nudge in the forward direction to break wall contact.
        _rb.position += (Vector2)transform.up * recoveryNudge;
        _rb.linearVelocity = Vector2.zero;
    }

    private void SteerTowardsWaypoint()
    {
        Vector2 toTarget = (Vector2)waypoints[_currentWaypointIndex].position - _rb.position;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        float angleError = Mathf.DeltaAngle(_rb.rotation, targetAngle);
        float targetAngVel = Mathf.Clamp(angleError * 6f, -rotationSpeed, rotationSpeed);
        _rb.angularVelocity = Mathf.MoveTowards(
            _rb.angularVelocity,
            targetAngVel,
            rotationSpeed * 8f * Time.fixedDeltaTime
        );
    }

    private void MoveForward()
    {
        Vector2 toTarget = ((Vector2)waypoints[_currentWaypointIndex].position - _rb.position).normalized;

        float angleToWaypoint = Vector2.Angle(transform.up, toTarget);
        float turnFactor = angleToWaypoint > sharpTurnAngle
            ? Mathf.Lerp(1f, sharpTurnSpeedFraction, (angleToWaypoint - sharpTurnAngle) / 90f)
            : 1f;

        // Always drive at full base speed — do not scale by alignment so the car
        // doesn't lose speed when slightly off-angle and drift wide off the path.
        float targetSpeed = _baseSpeed * turnFactor;

        if (playerTransform != null && rubberBandStrength > 0f)
        {
            Vector2 toPlayer = (Vector2)playerTransform.position - _rb.position;
            float behindAmount = Vector2.Dot(transform.up, toPlayer);
            if (behindAmount > 0f)
                targetSpeed *= 1f + rubberBandStrength * 0.5f;
            else
                targetSpeed *= 1f - rubberBandStrength * 0.3f;
        }

        Vector2 desiredVelocity = transform.up * targetSpeed;
        _rb.linearVelocity = Vector2.MoveTowards(
            _rb.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );

        ApplyLateralGrip();

        if (_rb.linearVelocity.magnitude > _baseSpeed * 1.15f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _baseSpeed * 1.15f;
    }

    private void ApplyLateralGrip()
    {
        Vector2 rightDir = transform.right;
        float lateralSpeed = Vector2.Dot(_rb.linearVelocity, rightDir);
        _rb.linearVelocity -= rightDir * lateralSpeed * gripStrength * Time.fixedDeltaTime;
    }

    private void CheckWaypointReached()
    {
        float distance = Vector2.Distance(_rb.position, waypoints[_currentWaypointIndex].position);
        if (distance > waypointReachDistance) return;

        _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
        AdvancePastNulls();
    }

    private void AdvancePastNulls()
    {
        if (waypoints == null) return;
        int safety = waypoints.Length;
        while (safety-- > 0 && waypoints[_currentWaypointIndex] == null)
            _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
    }
}
