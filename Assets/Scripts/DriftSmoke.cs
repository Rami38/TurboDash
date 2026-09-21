using UnityEngine;

/// <summary>
/// Emits tyre smoke behind the player car whenever it drifts or steers hard at speed.
/// 
/// HOW IT WORKS:
///   Each frame we check two conditions:
///     • "drift smoke"  — DriftInput is held AND the car is above speedThreshold.
///     • "turn smoke"   — steering input exceeds steerThreshold AND car is at speed.
///   The emission rate is set accordingly and smoothly ramped to avoid abrupt cuts.
/// 
/// PARTICLE SYSTEM:
///   If no ParticleSystem is assigned in the Inspector, one is created automatically
///   at the rear of the car using BuildSmokeSystem(). The auto-created system uses
///   the URP Particles/Unlit shader (or Sprites/Default as fallback) so it renders
///   correctly in URP without turning magenta.
/// 
/// HOW TO SET UP:
///   1. Add to the same GameObject as TopDownCarController (requires Rigidbody2D too).
///   2. Optionally assign a pre-made ParticleSystem child in the Inspector.
///   3. Tweak driftEmissionRate and turnEmissionRate to taste.
/// </summary>
[RequireComponent(typeof(TopDownCarController))]
[RequireComponent(typeof(Rigidbody2D))]
public class DriftSmoke : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Particle System (leave null to auto-create at rear of car)")]
    // Assign a ParticleSystem here to use a custom one; otherwise one is generated.
    [SerializeField] private ParticleSystem smokeSystem;

    [Header("Emission Rates")]
    [Tooltip("Particles/second while holding the drift key.")]
    [SerializeField] private float driftEmissionRate = 50f;
    [Tooltip("Particles/second while turning hard without drifting.")]
    [SerializeField] private float turnEmissionRate = 25f;

    [Header("Thresholds")]
    [Tooltip("Absolute steering input (0–1) required to trigger turn smoke.")]
    // Below this steering value, no smoke is emitted during normal cornering.
    [SerializeField] private float steerThreshold = 0.5f;
    [Tooltip("Minimum speed (units/s) required to produce any smoke.")]
    // The car must be moving fast enough for tyres to actually smoke.
    [SerializeField] private float speedThreshold = 1f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private TopDownCarController _car;
    private Rigidbody2D _rb;
    // Cached emission module — avoids getting it from the particle system every frame.
    private ParticleSystem.EmissionModule _emission;
    // Tracks the smoothed emission rate so it doesn't snap between values.
    private float _currentRate;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _car = GetComponent<TopDownCarController>();
        _rb  = GetComponent<Rigidbody2D>();

        // Build a default particle system if none was assigned in the Inspector.
        if (smokeSystem == null)
            smokeSystem = BuildSmokeSystem();

        // Cache the emission module and start at zero emission.
        _emission             = smokeSystem.emission;
        _emission.rateOverTime = 0f;
    }

    private void Update()
    {
        if (_car == null || _rb == null) return;

        float speed = _rb.linearVelocity.magnitude;
        float steer = Mathf.Abs(_car.SteeringInput);
        bool  fast  = speed > speedThreshold;

        // Choose the target emission rate based on driving state.
        float targetRate = 0f;

        if (fast && _car.IsDrifting)
        {
            // Full drift smoke while drift key is held at speed.
            targetRate = driftEmissionRate;
        }
        else if (fast && steer > steerThreshold)
        {
            // Lighter turn smoke that scales with steering amount above the threshold.
            // InverseLerp maps steerThreshold..1 → 0..1 for a smooth ramp.
            targetRate = turnEmissionRate * Mathf.InverseLerp(steerThreshold, 1f, steer);
        }

        // Smoothly ramp toward the target so smoke doesn't cut in or out instantly.
        // 120f/s means the rate changes at 120 particles/s per second — fast but not instant.
        _currentRate            = Mathf.MoveTowards(_currentRate, targetRate, 120f * Time.deltaTime);
        _emission.rateOverTime  = _currentRate;

        // Play/stop the particle system based on whether we have meaningful emission.
        if (_currentRate > 0.5f && !smokeSystem.isPlaying)
            smokeSystem.Play();
        else if (_currentRate < 0.5f && smokeSystem.isPlaying)
            // StopEmitting lets existing particles finish their lifetime before disappearing.
            smokeSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // ------------------------------------------------------------------ //
    //  Procedural particle system builder
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Creates a smoke ParticleSystem at the rear of the car.
    /// Called automatically in Awake when no ParticleSystem is assigned.
    /// Configures shape, color, size, and rotation for a realistic tyre-smoke look.
    /// </summary>
    private ParticleSystem BuildSmokeSystem()
    {
        // Parent the system to the car so it moves with it.
        var go = new GameObject("SmokeRear");
        go.transform.SetParent(transform, false);
        // Position at the car's rear in local space.
        go.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        var ps = go.AddComponent<ParticleSystem>();

        // ---- Main module ----
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);   // particles last 0.5–1 s
        main.startSize       = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);   // vary puff sizes
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);    // slow drift sideways
        main.gravityModifier = 0f;                                             // 2D — no gravity
        main.simulationSpace = ParticleSystemSimulationSpace.World;            // smoke stays in place
        main.maxParticles    = 120;
        main.loop            = true;
        main.playOnAwake     = false;   // we start it manually
        // Slightly transparent grey to white — gives a smoky, dusty appearance.
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.72f, 0.72f, 0.7f),
            new Color(0.95f, 0.95f, 0.95f, 0.3f));

        // ---- Emission — start at zero, controlled each frame by Update ----
        var em = ps.emission;
        em.rateOverTime = 0f;

        // ---- Shape — wide cone so puffs fan out sideways when sliding ----
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 25f;   // fan spread in degrees
        shape.radius    = 0.06f; // narrow emission origin

        // ---- Color over lifetime — fade alpha to zero so puffs dissolve ----
        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f),        new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // ---- Size over lifetime — puffs grow as they age (expand and thin out) ----
        var sizeOL   = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1f)));

        // ---- Rotation over lifetime — slow random spin for organic look ----
        var rot     = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z       = new ParticleSystem.MinMaxCurve(-60f, 60f); // degrees/s

        // ---- Renderer — ensure correct shader for URP ----
        var psr             = go.GetComponent<ParticleSystemRenderer>();
        psr.sortingLayerName = "Default";
        psr.sortingOrder     = 1; // draw on top of the road

        // URP projects show default particle materials as magenta (missing shader).
        // Use the correct URP shader; fall back to the legacy Sprites/Default if absent.
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            var mat  = new Material(sh);
            mat.color = Color.white;
            psr.material = mat;
        }

        return ps;
    }
}
