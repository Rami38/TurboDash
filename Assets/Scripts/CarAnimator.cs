using UnityEngine;

/// <summary>
/// Drives visual animations on the player car based on TopDownCarController state.
/// All visual feedback (particles, squish) is purely cosmetic — removing this
/// component has zero effect on gameplay physics.
/// 
/// FEATURES:
///   • Animator integration — sets float "Speed" (0-1) and bool "IsDrifting" each frame
///     so an Animator Controller can drive tyre-spin or tilt animations.
///   • Particle systems — exhaust smoke while accelerating, drift smoke while drifting.
///   • Squish / stretch — the sprite child squishes on acceleration and recovers smoothly,
///     giving a cartoon "weight" feel.
/// 
/// ANIMATOR PARAMETERS TO CREATE:
///   float "Speed"       — normalised speed 0..1, drives tyre rotation etc.
///   bool  "IsDrifting"  — true while drift key is held at speed.
/// 
/// HOW TO SET UP:
///   1. Add this to the same GameObject as TopDownCarController.
///   2. Optionally assign an Animator (on the sprite child).
///   3. Optionally assign exhaust / drift ParticleSystems.
///   4. Optionally assign the sprite child Transform for the squish effect.
/// </summary>
[RequireComponent(typeof(TopDownCarController))]
public class CarAnimator : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Animator (optional)")]
    [Tooltip("Animator on the car sprite child. Leave null to skip.")]
    [SerializeField] private Animator carAnimator;

    [Header("Particle Effects (optional)")]
    [Tooltip("Exhaust smoke — plays while accelerating.")]
    [SerializeField] private ParticleSystem exhaustParticles;
    [Tooltip("Tyre smoke/dust — plays while drifting.")]
    [SerializeField] private ParticleSystem driftParticles;

    [Header("Squish / Stretch (optional)")]
    [Tooltip("The sprite child Transform to apply squish on. Leave null to skip.")]
    [SerializeField] private Transform spriteTransform;
    [Tooltip("How strongly the sprite squishes on acceleration (0 = none).")]
    [SerializeField] private float squishAmount = 0.08f;
    [Tooltip("Speed at which squish snaps back to normal.")]
    [SerializeField] private float squishSmoothing = 10f;

    // ------------------------------------------------------------------ //
    //  Cached Animator parameter hashes
    // ------------------------------------------------------------------ //

    // Pre-hashing string names avoids a dictionary lookup every frame in Animator.SetFloat/SetBool.
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int IsDriftingHash = Animator.StringToHash("IsDrifting");

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private TopDownCarController _car;
    // The original scale of the sprite child — squish is applied relative to this.
    private Vector3 _baseScale;
    // Speed from the previous frame — used to detect acceleration delta for squish.
    private float _prevSpeed;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _car = GetComponent<TopDownCarController>();

        // Cache the sprite child's default scale so squish always returns to the same size.
        if (spriteTransform != null)
            _baseScale = spriteTransform.localScale;
    }

    private void Update()
    {
        // Read current car state — these properties are updated by TopDownCarController.FixedUpdate.
        float speed    = _car.NormalizedSpeed;
        bool  drifting = _car.IsDrifting;

        UpdateAnimator(speed, drifting);
        UpdateParticles(speed, drifting);
        UpdateSquish(speed);

        _prevSpeed = speed;
    }

    // ------------------------------------------------------------------ //
    //  Per-frame update helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Forwards current speed and drift state to the Animator so it can
    /// drive any animation transitions defined in the Animator Controller.
    /// </summary>
    private void UpdateAnimator(float speed, bool drifting)
    {
        if (carAnimator == null) return;
        carAnimator.SetFloat(SpeedHash, speed);
        carAnimator.SetBool(IsDriftingHash, drifting);
    }

    /// <summary>
    /// Starts/stops exhaust and drift particle systems based on driving state.
    /// We check isPlaying before calling Play/Stop to avoid restarting a system
    /// that is already in the correct state (which would cause a visual pop).
    /// </summary>
    private void UpdateParticles(float speed, bool drifting)
    {
        if (exhaustParticles != null)
        {
            // Show exhaust whenever the car is moving, stop when idle.
            bool shouldExhaust = speed > 0.1f;
            if (shouldExhaust && !exhaustParticles.isPlaying)  exhaustParticles.Play();
            else if (!shouldExhaust && exhaustParticles.isPlaying) exhaustParticles.Stop();
        }

        if (driftParticles != null)
        {
            // Show drift smoke only while the drift key is actively held.
            if (drifting && !driftParticles.isPlaying)    driftParticles.Play();
            else if (!drifting && driftParticles.isPlaying) driftParticles.Stop();
        }
    }

    /// <summary>
    /// Applies a squish-and-stretch deformation to the sprite child Transform.
    /// 
    /// How it works:
    ///   - speedDelta is the change in normalised speed since last frame (positive = accelerating).
    ///   - On hard acceleration, the sprite stretches vertically (Y) and squishes horizontally (X).
    ///   - The deformation is clamped to prevent extreme distortion.
    ///   - Vector3.Lerp smoothly returns the sprite to its base scale each frame.
    /// </summary>
    private void UpdateSquish(float speed)
    {
        if (spriteTransform == null || squishAmount <= 0f) return;

        float speedDelta = speed - _prevSpeed;

        // Vertical stretch on acceleration, horizontal compress to conserve "volume".
        float squishY = 1f + speedDelta * squishAmount * 10f;
        float squishX = 1f - speedDelta * squishAmount * 5f;

        Vector3 target = new Vector3(
            _baseScale.x * Mathf.Clamp(squishX, 0.85f, 1.15f),
            _baseScale.y * Mathf.Clamp(squishY, 0.85f, 1.15f),
            _baseScale.z
        );

        // Lerp back toward the target each frame — creates the springy snap-back effect.
        spriteTransform.localScale = Vector3.Lerp(
            spriteTransform.localScale,
            target,
            squishSmoothing * Time.deltaTime
        );
    }
}
