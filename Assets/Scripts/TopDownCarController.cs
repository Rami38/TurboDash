using UnityEngine;

/// <summary>
/// Physics-based top-down car controller with grip, drift, and wall-collision response.
/// 
/// MOVEMENT MODEL:
///   The car uses a Rigidbody2D and applies forces/velocity directly each FixedUpdate.
///   There is no Unity Wheel Collider — all physics is hand-tuned for a top-down 2D feel.
/// 
/// DRIVING KEYS (set by CarInputHandler, not directly here):
///   W / S  — throttle forward / reverse   (AccelerationInput: -1 to +1)
///   A / D  — steer left / right           (SteeringInput: -1 to +1)
///   Space or Left Shift — drift           (DriftInput: bool)
/// 
/// DRIFT SYSTEM:
///   Normally, lateral slip is cancelled aggressively each frame (gripStrength).
///   When DriftInput is true AND the car is above driftMinSpeed, grip drops to
///   driftGripStrength, allowing the rear to slide. Steering responsiveness is
///   boosted (driftSteerBoost) so the driver can control the angle of the slide.
///   After releasing drift, grip ramps back to normal over driftGripRampTime seconds
///   instead of snapping instantly — this prevents the car from flipping direction mid-slide.
/// 
/// WALL COLLISIONS:
///   OnCollisionEnter2D kills most velocity and applies a small push away from the wall.
///   Collisions with other cars (Player/Opponent tags) are ignored.
/// 
/// FREEZE / UNFREEZE:
///   Used by RaceCountdown to disable movement during the countdown.
///   Freeze() disables Rigidbody simulation entirely. Unfreeze() re-enables it.
/// 
/// HOW TO SET UP:
///   1. Add a Rigidbody2D: Gravity Scale = 0, no frozen constraints.
///   2. Add a PolygonCollider2D or BoxCollider2D matching the car shape.
///   3. Add CarInputHandler to the same GameObject.
///   4. Tune the public fields in the Inspector.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TopDownCarController : MonoBehaviour
{
    [Header("Movement")]
    // Force (in Newtons) applied each FixedUpdate when throttle is held.
    public float accelerationForce = 20f;
    // Maximum speed going forward (units/s). Hard-capped in ClampSpeed().
    public float maxSpeed = 8f;
    // Maximum speed when reversing — lower than forward to feel realistic.
    public float reverseMaxSpeed = 3f;
    // How quickly the car decelerates when no throttle key is held.
    public float coastDrag = 14f;

    [Header("Steering")]
    [Tooltip("Max degrees per second of rotation at reference speed.")]
    public float turnDegreesPerSecond = 150f;
    [Tooltip("At max speed, steering is reduced to this fraction (0-1). Keeps high-speed driving stable.")]
    // e.g. 0.6 = at max speed the car turns at only 60% of its full turn rate.
    public float highSpeedTurnFactor = 0.6f;

    [Header("Grip / Drift")]
    [Tooltip("How strongly lateral slip is removed each second. Higher = tighter cornering.")]
    // Cancels sideways velocity to simulate tyre friction.
    public float gripStrength = 10f;
    [Tooltip("Grip while holding the drift key. Lower = more slide.")]
    // Greatly reduced during drift to allow the rear to swing out freely.
    public float driftGripStrength = 1.5f;
    [Tooltip("Minimum forward speed (units/s) to allow drifting.")]
    // Below this speed the drift key does nothing.
    public float driftMinSpeed = 3f;
    [Tooltip("Steering responsiveness multiplier while drifting.")]
    // Gives the player extra control to catch / angle the slide.
    public float driftSteerBoost = 1.5f;
    [Tooltip("Seconds to ramp lateral grip back to normal after releasing drift.")]
    // Prevents abrupt snap from slide to full grip — feels more natural.
    public float driftGripRampTime = 0.3f;
    [Tooltip("Fraction of acceleration force applied while drifting (reduces forward traction).")]
    // Stops the engine from driving the car straight during a slide.
    public float driftAccelMultiplier = 0.65f;

    [Header("Wall Collision")]
    [Tooltip("How much velocity is retained on wall hit (0 = full stop, 1 = full bounce).")]
    public float wallSpeedRetention = 0.2f;
    [Tooltip("Small push force away from the wall on collision.")]
    // Impulse that prevents the car from wedging into the wall.
    public float wallPushForce = 1.5f;

    // Set every Update by CarInputHandler — hidden to avoid duplicate editing in Inspector.
    [HideInInspector] public float AccelerationInput; // -1 reverse, 0 coast, +1 forward
    [HideInInspector] public float SteeringInput;     // -1 left, 0 centre, +1 right
    [HideInInspector] public bool  DriftInput;        // true while Space/LShift held

    /// <summary>True while the car is actively drifting (drift key + above min speed).</summary>
    public bool  IsDrifting     => _isDrifting;
    /// <summary>Signed current speed in the car's forward direction (units/s). Negative = reversing.</summary>
    public float CurrentSpeed   => _currentSpeed;
    /// <summary>Speed normalised 0–1 vs maxSpeed. Used by CarAnimator and CarEngineAudio.</summary>
    public float NormalizedSpeed => Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxSpeed);

    private Rigidbody2D _rb;
    // Speed projected onto the car's local forward axis (positive = forward, negative = reverse).
    private float _currentSpeed;
    private bool  _isDrifting;
    // Becomes true when the first driving key is pressed or Unfreeze() is called.
    private bool  _hasStarted;
    // Fully disables physics while true (during race countdown).
    private bool  _frozen;
    // 0 = just released drift (low grip), 1 = fully recovered to normal grip.
    private float _gripRampT = 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Called by CarInputHandler on the first driving key press, and also by
    /// RaceCountdown at GO so the car begins moving without an extra key press.
    /// Guards against being triggered while frozen during the countdown.
    /// </summary>
    public void NotifyInputReceived()
    {
        if (_hasStarted || _frozen) return;
        _hasStarted = true;
        _rb.WakeUp(); // ensure Rigidbody is active and ready to receive forces
    }

    /// <summary>
    /// Disables physics simulation entirely — called by RaceCountdown at race start.
    /// Also resets _hasStarted so the car needs a NotifyInputReceived() before driving again.
    /// </summary>
    public void Freeze()
    {
        _frozen     = true;
        _hasStarted = false;
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated       = false; // stops all physics including collision detection
    }

    /// <summary>
    /// Re-enables physics and marks the car as started.
    /// Called by RaceCountdown at GO — marks _hasStarted = true so the player can
    /// drive immediately without pressing any key first.
    /// </summary>
    public void Unfreeze()
    {
        _frozen             = false;
        _hasStarted         = true;
        _rb.simulated       = true;
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.WakeUp();
    }

    /// <summary>
    /// Alternative entry point for enabling driving outside of a countdown.
    /// Functionally identical to Unfreeze() — exists for semantic clarity in
    /// scenarios where no countdown is involved.
    /// </summary>
    public void BeginDriving()
    {
        _rb.simulated       = true;
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _frozen             = false;
        _hasStarted         = true;
        _rb.WakeUp();
    }

    private void FixedUpdate()
    {
        // All movement is gated behind _hasStarted to prevent any motion during countdown.
        if (!_hasStarted) return;

        // Project velocity onto the car's own forward axis to get signed speed.
        _currentSpeed = Vector2.Dot(_rb.linearVelocity, transform.up);

        // Drift is only active when drift key is held AND the car is above the minimum speed.
        _isDrifting = DriftInput && Mathf.Abs(_currentSpeed) > driftMinSpeed;

        // Manage the grip recovery ramp:
        //   0 = no grip (actively drifting or just released)
        //   1 = full normal grip (fully recovered)
        if (_isDrifting)
            _gripRampT = 0f;
        else
            _gripRampT = Mathf.MoveTowards(
                _gripRampT, 1f, Time.fixedDeltaTime / Mathf.Max(driftGripRampTime, 0.01f));

        ApplyThrottle();
        ApplySteering();
        ApplyLateralFriction();
        ClampSpeed();
    }

    /// <summary>
    /// Applies forward/reverse acceleration force when a throttle key is held.
    /// When coasting (no key held), MoveTowards decelerates the car by coastDrag units/s.
    /// During drift, forward force is reduced by driftAccelMultiplier to allow sliding.
    /// </summary>
    private void ApplyThrottle()
    {
        if (AccelerationInput != 0f)
        {
            float accelMod = _isDrifting ? driftAccelMultiplier : 1f;
            _rb.AddForce(transform.up * AccelerationInput * accelerationForce * accelMod,
                         ForceMode2D.Force);
        }
        else
        {
            // Gradually bring velocity to zero when no throttle is applied.
            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity, Vector2.zero, coastDrag * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Adjusts angular velocity to steer the car.
    ///
    /// KEY DETAILS:
    ///   • A minimum speedRatio (0.45) keeps steering responsive at low speeds.
    ///   • highSpeedTurnFactor reduces maximum turn rate at high speed for stability.
    ///   • Steering direction flips when reversing (negative currentSpeed) so A/D feel correct.
    ///   • driftSteerBoost multiplies turn rate while drifting so the player can catch slides.
    ///   • MoveTowards smooths angular velocity changes to avoid instant snapping.
    /// </summary>
    private void ApplySteering()
    {
        if (Mathf.Approximately(SteeringInput, 0f))
        {
            // Dampen angular velocity quickly when no steering key is pressed.
            _rb.angularVelocity = Mathf.MoveTowards(
                _rb.angularVelocity, 0f, 600f * Time.fixedDeltaTime);
            return;
        }

        // speedRatio floors at 0.45 so the car steers even from a near-stop.
        float speedRatio  = Mathf.Max(0.45f,
            Mathf.Clamp01(Mathf.Abs(_currentSpeed) / (maxSpeed * 0.35f)));

        // Reduce turn rate at high speed.
        float speedDampen = Mathf.Lerp(1f, highSpeedTurnFactor,
            Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxSpeed));

        // Flip steer direction when reversing.
        float steerDir    = _currentSpeed >= 0f ? 1f : -1f;
        float driftBoost  = _isDrifting ? driftSteerBoost : 1f;

        float targetAngVel = -SteeringInput * turnDegreesPerSecond
                             * speedRatio * speedDampen * steerDir * driftBoost;

        _rb.angularVelocity = Mathf.MoveTowards(
            _rb.angularVelocity, targetAngVel,
            turnDegreesPerSecond * 6f * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Cancels the velocity component pointing sideways (perpendicular to the car's forward).
    /// This simulates tyre friction — without it the car would slide freely on any surface.
    ///
    /// During drift the grip is reduced (more slide).
    /// After releasing drift, grip ramps back to normal over driftGripRampTime
    /// using the _gripRampT value updated in FixedUpdate.
    /// </summary>
    private void ApplyLateralFriction()
    {
        Vector2 rightDir     = transform.right;
        float   lateralSpeed = Vector2.Dot(_rb.linearVelocity, rightDir);

        float grip = _isDrifting
            ? driftGripStrength
            : Mathf.Lerp(driftGripStrength, gripStrength, _gripRampT);

        // Subtract the lateral velocity component scaled by grip strength.
        _rb.linearVelocity -= rightDir * lateralSpeed * grip * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Hard caps velocity magnitude to prevent physics edge cases from exceeding the speed limits.
    /// Uses reverseMaxSpeed when braking/reversing.
    /// </summary>
    private void ClampSpeed()
    {
        float limit = AccelerationInput < 0f ? reverseMaxSpeed : maxSpeed;
        if (_rb.linearVelocity.magnitude > limit)
            _rb.linearVelocity = _rb.linearVelocity.normalized * limit;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Ignore collisions before the race starts to prevent spawn-area jitter.
        if (!_hasStarted) return;

        // Car-to-car collisions are handled naturally by physics — don't override them.
        if (collision.gameObject.CompareTag("Player"))   return;
        if (collision.gameObject.CompareTag("Opponent")) return;
        if (collision.contacts.Length == 0)              return;

        // Wall collision: kill most velocity and push the car away from the surface.
        Vector2 normal = collision.contacts[0].normal;
        _rb.linearVelocity  *= wallSpeedRetention;
        _rb.AddForce(normal * wallPushForce, ForceMode2D.Impulse);
        // Zero out spin to prevent wall-clip tumbling.
        _rb.angularVelocity  = 0f;
    }
}
