using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Spline-following AI opponent for the top-down 2D racing game (used in Level 1).
/// 
/// HOW IT WORKS:
///   Each FixedUpdate, the car:
///     1. Re-projects onto the nearest point of the spline from its current position.
///        This corrects drift caused by physics collisions — without it the car would
///        wander further off the line every time it is bumped.
///     2. Calculates a look-ahead point some distance ahead on the spline.
///     3. Steers toward the look-ahead (not the car's facing direction) so it follows
///        curves smoothly without oscillating.
///     4. Drives forward at a speed that is reduced on sharp turns.
///     5. Applies lateral grip so the car doesn't slide sideways.
///     6. Pushes away from nearby opponents (separation force) so cars don't overlap.
///     7. Checks a stuck recovery timer and snaps free if the car hasn't moved enough.
/// 
/// SPLINE vs WAYPOINTS:
///   Unlike OpponentAI (which follows a list of points in a straight line),
///   SplineOpponentAI follows a smooth Unity Spline curve. This gives much smoother
///   movement through corners without needing dozens of waypoints per turn.
/// 
/// HOW TO SET UP:
///   1. Add a SplineContainer to a GameObject tracing the race line in the Scene.
///   2. Add a Rigidbody2D to the opponent car (gravity 0, no frozen axes).
///   3. Tag the car "Opponent" so FinishLine and separation logic work correctly.
///   4. Assign the SplineContainer to the Track Spline field in the Inspector.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SplineOpponentAI : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector
    // ------------------------------------------------------------------ //

    [Header("Spline")]
    [Tooltip("The SplineContainer that defines the race line.")]
    // Assign the GameObject that has a SplineContainer component tracing the track.
    [SerializeField] private SplineContainer trackSpline;
    [Tooltip("How far ahead on the spline (in world units) the car aims for. " +
             "Higher values = smoother but wider corners.")]
    // A higher look-ahead means smoother but wider cornering.
    // A lower look-ahead means tighter but more jittery line-following.
    [SerializeField] private float lookAheadDistance = 3.5f;

    [Header("Movement")]
    // Normal forward driving speed in world units per second.
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("How quickly the car reaches its target speed (units/s²).")]
    [SerializeField] private float acceleration = 20f;
    // Maximum degrees/second of rotation.
    [SerializeField] private float rotationSpeed = 260f;
    [Tooltip("Random per-car speed variation so cars don't bunch up.")]
    // Each car gets a random offset applied once in Awake so the field naturally spreads.
    [SerializeField] private float speedVariation = 0.5f;

    [Header("Grip")]
    [Tooltip("Cancels lateral sliding so the car corners like a real car.")]
    // Applied every FixedUpdate — removes the velocity component pointing sideways.
    [SerializeField] private float gripStrength = 12f;

    [Header("Turn Slowdown")]
    [Tooltip("Angle (degrees) between heading and look-ahead above which the car brakes.")]
    // Below this angle the car drives at full speed. Above it, speed is reduced.
    [SerializeField] private float sharpTurnAngle = 35f;
    [Tooltip("Speed fraction at the sharpest turns (0–1).")]
    // e.g. 0.45 means the car slows to 45% of base speed at the tightest corners.
    [SerializeField] private float sharpTurnSpeedFraction = 0.45f;

    [Header("Stuck Recovery")]
    [Tooltip("Seconds without meaningful movement before recovery triggers.")]
    [SerializeField] private float stuckCheckInterval = 0.6f;
    [Tooltip("Units pushed toward the look-ahead point when stuck.")]
    [SerializeField] private float recoveryNudge = 1.2f;

    [Header("Separation")]
    [Tooltip("Radius within which nearby opponents are detected for separation.")]
    // Cars within this radius of each other will push apart to prevent overlapping.
    [SerializeField] private float separationRadius = 1.2f;
    [Tooltip("Strength of the lateral push away from nearby opponents.")]
    [SerializeField] private float separationForce = 8f;

    // ------------------------------------------------------------------ //
    //  Public state (read by RaceManager / FinishLine)
    // ------------------------------------------------------------------ //

    // Incremented by FinishLine each time this car crosses it.
    public int LapsCompleted { get; private set; }

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private Rigidbody2D _rb;
    // Base speed randomized in Awake so cars in the same race don't drive identically.
    private float _baseSpeed;
    // When true, all physics is disabled (during countdown).
    private bool _frozen;

    // Normalised t-value (0–1) on the spline representing the car's current progress.
    private float _splineT;

    // Position recorded at the last stuck check — compared each interval to measure movement.
    private Vector2 _lastCheckedPosition;
    // Accumulates time since the last stuck check.
    private float _stuckTimer;

    // Counts down FixedUpdate frames during which the car is in active collision.
    // Used to apply extra steering and braking immediately after a collision.
    private int _collisionFrames;

    // Pre-allocated buffer for Physics2D.OverlapCircleNonAlloc in ApplySeparation().
    // Non-alloc variant avoids a garbage allocation on every physics frame.
    private readonly Collider2D[] _separationBuffer = new Collider2D[8];

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        // Add random variation so no two cars have the same top speed.
        _baseSpeed = moveSpeed + Random.Range(-speedVariation, speedVariation);
    }

    private void Start()
    {
        _lastCheckedPosition = _rb.position;

        // Snap the t-value to the nearest spline point on spawn so the car
        // always starts tracking from the correct position even if placed mid-track.
        if (trackSpline != null)
            SnapToNearestSplinePoint();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        // Hold for 8 physics frames — during that window steer and drive more cautiously.
        _collisionFrames = 8;
    }

    // ------------------------------------------------------------------ //
    //  Public API (called by RaceManager / FinishLine)
    // ------------------------------------------------------------------ //

    /// <summary>Freeze the AI during countdown.</summary>
    public void Freeze()
    {
        _frozen = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated = false;
    }

    /// <summary>Unfreeze the AI when the race starts.</summary>
    public void Unfreeze()
    {
        _rb.simulated = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _frozen = false;
        _lastCheckedPosition = _rb.position;
        _stuckTimer = 0f;
    }

    /// <summary>Called by FinishLine when this car crosses it.</summary>
    public void IncrementLap() => LapsCompleted++;

    // ------------------------------------------------------------------ //
    //  Physics loop
    // ------------------------------------------------------------------ //

    private void FixedUpdate()
    {
        if (_frozen || trackSpline == null) return;

        // Always re-project onto the spline every physics step so the car
        // tracks the line exactly regardless of physics pushback.
        ReprojectOntoSpline();

        if (_collisionFrames > 0) _collisionFrames--;

        Vector2 lookAheadPoint = GetLookAheadWorldPoint();

        SteerToward(lookAheadPoint);
        Drive(lookAheadPoint);
        ApplyLateralGrip();
        ApplySeparation();
        CheckStuckRecovery(lookAheadPoint);
    }

    // ------------------------------------------------------------------ //
    //  Spline helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Full re-projection: finds the nearest point on the spline from the
    /// car's current world position and advances t by lookAheadDistance.
    /// This corrects drift caused by collisions or physics interference.
    /// </summary>
    private void ReprojectOntoSpline()
    {
        Vector3 localPos = trackSpline.transform.InverseTransformPoint(_rb.position);

        SplineUtility.GetNearestPoint(
            trackSpline.Spline,
            localPos,
            out _,
            out float nearestT
        );

        // Only accept the new t if it is ahead of the current one (within
        // half a lap), so the car never suddenly reverses its progress.
        if (IsAheadOnSpline(nearestT, _splineT))
            _splineT = nearestT;
    }

    /// <summary>
    /// Returns a world-space point that is <see cref="lookAheadDistance"/>
    /// ahead of the car's current spline position.
    /// </summary>
    private Vector2 GetLookAheadWorldPoint()
    {
        float splineLength = trackSpline.Spline.GetLength();
        float tStep = lookAheadDistance / splineLength;
        float lookT = (_splineT + tStep) % 1f;

        Vector3 localPoint = trackSpline.Spline.EvaluatePosition(lookT);
        return trackSpline.transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Returns true if <paramref name="candidate"/> is ahead of
    /// <paramref name="current"/> and within half a lap forward.
    /// Handles the 0→1 wrap at the start/finish line.
    /// </summary>
    private static bool IsAheadOnSpline(float candidate, float current)
    {
        float delta = candidate - current;
        if (delta < 0f) delta += 1f;
        return delta < 0.5f; // must be within half a lap forward
    }

    // ------------------------------------------------------------------ //
    //  Movement
    // ------------------------------------------------------------------ //

    private void SteerToward(Vector2 target)
    {
        Vector2 toTarget = target - _rb.position;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        float angleError = Mathf.DeltaAngle(_rb.rotation, targetAngle);

        // After a collision, steer more aggressively to recover heading.
        float steerMult = _collisionFrames > 0 ? 3f : 6f;
        float targetAngVel = Mathf.Clamp(angleError * steerMult, -rotationSpeed, rotationSpeed);
        _rb.angularVelocity = Mathf.MoveTowards(
            _rb.angularVelocity,
            targetAngVel,
            rotationSpeed * 8f * Time.fixedDeltaTime
        );
    }

    private void Drive(Vector2 target)
    {
        // Use the direction to the look-ahead point (not transform.up) so
        // the car still makes progress even when its heading is wrong.
        Vector2 toTarget = (target - _rb.position).normalized;
        float angleToTarget = Vector2.Angle(transform.up, toTarget);

        float turnFactor = angleToTarget > sharpTurnAngle
            ? Mathf.Lerp(1f, sharpTurnSpeedFraction, (angleToTarget - sharpTurnAngle) / 90f)
            : 1f;

        // Slow down harder when fresh off a collision to avoid bouncing back.
        if (_collisionFrames > 0)
            turnFactor *= 0.5f;

        float targetSpeed = _baseSpeed * turnFactor;

        // Drive along transform.up (car's own forward) to keep it realistic,
        // but only at the speed allowed by the look-ahead angle.
        Vector2 desiredVelocity = (Vector2)transform.up * targetSpeed;
        _rb.linearVelocity = Vector2.MoveTowards(
            _rb.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );

        if (_rb.linearVelocity.magnitude > _baseSpeed * 1.15f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _baseSpeed * 1.15f;
    }

    private void ApplyLateralGrip()
    {
        Vector2 right = transform.right;
        float lateral = Vector2.Dot(_rb.linearVelocity, right);
        _rb.linearVelocity -= right * lateral * gripStrength * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Detects nearby opponents and applies a perpendicular push force so cars
    /// don't overlap. The force is purely lateral (relative to this car's
    /// forward) so it doesn't fight the spline-following drive logic.
    /// </summary>
    private void ApplySeparation()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            _rb.position, separationRadius, _separationBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D other = _separationBuffer[i];
            if (other == null || other.attachedRigidbody == null) continue;
            if (other.attachedRigidbody == _rb) continue;
            // Only push away from other AI opponents, not the player or walls
            if (!other.CompareTag("Opponent")) continue;

            Vector2 away = _rb.position - (Vector2)other.transform.position;
            float dist = away.magnitude;
            if (dist < 0.001f) continue;

            // Scale force: stronger when closer, zero at edge of radius
            float strength = (1f - dist / separationRadius) * separationForce;

            // Apply as a lateral-only push (project onto this car's right axis)
            // so it nudges sideways rather than braking or accelerating.
            Vector2 lateral = transform.right * Vector2.Dot(away.normalized, transform.right);
            _rb.AddForce(lateral * strength, ForceMode2D.Force);
        }
    }

    // ------------------------------------------------------------------ //
    //  Stuck recovery
    // ------------------------------------------------------------------ //

    private void CheckStuckRecovery(Vector2 lookAheadPoint)
    {
        _stuckTimer += Time.fixedDeltaTime;
        if (_stuckTimer < stuckCheckInterval) return;

        float moved = Vector2.Distance(_rb.position, _lastCheckedPosition);
        if (moved < 0.25f)
            RecoverFromStuck(lookAheadPoint);

        _lastCheckedPosition = _rb.position;
        _stuckTimer = 0f;
    }

    private void RecoverFromStuck(Vector2 lookAheadPoint)
    {
        Vector2 toTarget = (lookAheadPoint - _rb.position).normalized;

        // Snap heading toward the look-ahead and push the car clear.
        float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        _rb.rotation = angle;
        _rb.angularVelocity = 0f;
        _rb.position += toTarget * recoveryNudge;
        _rb.linearVelocity = toTarget * (_baseSpeed * 0.5f);

        // Re-project onto the spline from the new position.
        ReprojectOntoSpline();
    }

    // ------------------------------------------------------------------ //
    //  Initialisation helper
    // ------------------------------------------------------------------ //

    /// <summary>
    /// On Start, finds the closest point on the spline so the car begins
    /// tracking from the right position even if placed mid-track.
    /// </summary>
    private void SnapToNearestSplinePoint()
    {
        Vector3 localPos = trackSpline.transform.InverseTransformPoint(_rb.position);
        SplineUtility.GetNearestPoint(trackSpline.Spline, localPos, out _, out _splineT);
    }
}
