using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads keyboard input and feeds smoothed values to TopDownCarController each frame.
/// 
/// INPUT MAPPING:
///   W              → accelerate forward  (AccelerationInput = +1)
///   S              → reverse             (AccelerationInput = -1)
///   A              → steer left          (SteeringInput = -1)
///   D              → steer right         (SteeringInput = +1)
///   Space / LShift → drift               (DriftInput = true)
/// 
/// INPUT SMOOTHING:
///   Raw keyboard input is either 0 or 1 (digital), which causes jerky starts/stops.
///   We use Mathf.MoveTowards to ramp the value toward its target at a defined rate,
///   making acceleration and steering feel analog even on a keyboard.
///   Drift is a boolean — no smoothing needed, it toggles immediately.
/// 
/// WHY A SEPARATE SCRIPT:
///   Keeping input reading separate from physics means you could swap in a
///   gamepad handler or an AI without touching TopDownCarController at all.
/// </summary>
[RequireComponent(typeof(TopDownCarController))]
public class CarInputHandler : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Input Smoothing")]
    [Tooltip("How fast throttle input ramps up/down (higher = snappier).")]
    // At throttleRampSpeed = 8, it takes ~0.125 s to go from 0 to full throttle.
    [SerializeField] private float throttleRampSpeed = 8f;
    [Tooltip("How fast steering input ramps up/down.")]
    // Slightly faster than throttle so turning feels responsive.
    [SerializeField] private float steeringRampSpeed = 10f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private TopDownCarController _carController;
    // Current smoothed throttle value — ranges from -1 (reverse) to +1 (forward).
    private float _smoothedThrottle;
    // Current smoothed steering value — ranges from -1 (left) to +1 (right).
    private float _smoothedSteering;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _carController = GetComponent<TopDownCarController>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        // Keyboard.current is null if no keyboard is connected.
        if (kb == null) return;

        // ---- Raw digital input ----
        // W and S are mutually exclusive — W wins if both somehow register.
        float rawThrottle = 0f;
        if (kb.wKey.isPressed)      rawThrottle =  1f;
        else if (kb.sKey.isPressed) rawThrottle = -1f;

        float rawSteering = 0f;
        if (kb.dKey.isPressed)      rawSteering =  1f;
        else if (kb.aKey.isPressed) rawSteering = -1f;

        // ---- Smooth the raw values ----
        // MoveTowards steps the current value toward the raw target by at most
        // (rampSpeed * deltaTime) per frame — giving a natural ease in/out.
        _smoothedThrottle = Mathf.MoveTowards(
            _smoothedThrottle, rawThrottle, throttleRampSpeed * Time.deltaTime);
        _smoothedSteering = Mathf.MoveTowards(
            _smoothedSteering, rawSteering, steeringRampSpeed * Time.deltaTime);

        // Write the smoothed values to TopDownCarController's public fields.
        _carController.AccelerationInput = _smoothedThrottle;
        _carController.SteeringInput     = _smoothedSteering;

        // Drift is a raw boolean — no analogue ramp needed.
        _carController.DriftInput = kb.spaceKey.isPressed || kb.leftShiftKey.isPressed;

        // Notify the car controller the instant the player first presses a driving key.
        // This allows the car to start moving immediately after the countdown (no extra press).
        bool anyKeyPressed = kb.wKey.isPressed || kb.sKey.isPressed
                          || kb.aKey.isPressed || kb.dKey.isPressed;
        if (anyKeyPressed)
            _carController.NotifyInputReceived();
    }
}
