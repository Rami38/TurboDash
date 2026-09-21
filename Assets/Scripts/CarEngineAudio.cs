using UnityEngine;

/// <summary>
/// Crossfades between multiple engine sound clips to simulate RPM changes
/// as the player car accelerates and decelerates.
/// 
/// DESIGN — Dual AudioSource crossfade:
///   Two AudioSources (sourceA, sourceB) alternate as "active" and "fading".
///   When the target clip changes, the current active source starts fading out
///   and the inactive one fades in playing the new clip. This creates a smooth
///   blend rather than an abrupt cut between RPM bands.
/// 
/// CLIP BANDS:
///   The engine sound is split into five RPM ranges (idle, low, med, high, maxRPM)
///   plus three deceleration variants (lowOff, medOff, highOff).
///   Clips are selected each frame based on normalised speed and whether the
///   player is pressing the throttle.
/// 
/// HOW TO SET UP:
///   1. Add this to the same GameObject as TopDownCarController.
///   2. Create two child GameObjects, add one AudioSource each, drag them into
///      sourceA and sourceB.
///   3. Assign the clips from the "i6_german_free" audio pack in the Inspector.
/// </summary>
[RequireComponent(typeof(TopDownCarController))]
public class CarEngineAudio : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields — Audio clips
    // ------------------------------------------------------------------ //

    [Header("Engine Clips (assign from i6_german_free folder)")]
    // Played when the car is stationary or moving very slowly.
    [SerializeField] private AudioClip idleClip;
    // Played during low-speed acceleration (0–30% of max speed).
    [SerializeField] private AudioClip lowOnClip;
    // Played during mid-speed acceleration (30–60%).
    [SerializeField] private AudioClip medOnClip;
    // Played during high-speed acceleration (60–90%).
    [SerializeField] private AudioClip highOnClip;
    // Played at full throttle (90–100%).
    [SerializeField] private AudioClip maxRPMClip;

    [Header("Deceleration Clips")]
    // The "off-throttle" variants sound different (less revving, more exhaust pop).
    [SerializeField] private AudioClip lowOffClip;
    [SerializeField] private AudioClip medOffClip;
    [SerializeField] private AudioClip highOffClip;

    [Header("Settings")]
    // Overall volume ceiling for the engine audio.
    [SerializeField] private float maxVolume = 0.7f;
    // Minimum pitch multiplier at low speed.
    [SerializeField] private float pitchMin = 0.8f;
    // Maximum pitch multiplier at full speed — simulates higher RPM whine.
    [SerializeField] private float pitchMax = 1.3f;
    // How fast the crossfade between clips happens (higher = snappier transitions).
    [SerializeField] private float crossfadeSpeed = 4f;

    [Header("Audio Sources (create 2 child AudioSources)")]
    // The two AudioSources alternate roles so clips can overlap/crossfade cleanly.
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private TopDownCarController _car;
    // The AudioSource currently ramping UP toward maxVolume.
    private AudioSource _activeSource;
    // The AudioSource currently fading OUT toward silence.
    private AudioSource _fadingSource;
    // Tracks which clip is playing on the active source to detect band changes.
    private AudioClip _currentClip;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _car           = GetComponent<TopDownCarController>();
        // sourceA starts as active, sourceB starts fading (both begin at 0 volume anyway).
        _activeSource  = sourceA;
        _fadingSource  = sourceB;

        SetupSource(sourceA);
        SetupSource(sourceB);
    }

    /// <summary>
    /// Configures an AudioSource for engine use: looping, not auto-playing, 2D sound.
    /// </summary>
    private void SetupSource(AudioSource src)
    {
        if (src == null) return;
        src.loop        = true;        // Engine clips loop continuously.
        src.playOnAwake = false;       // We control when to start them.
        src.volume      = 0f;          // Begin silent; volume is ramped in Update.
        src.spatialBlend = 0f;         // 2D — sounds the same regardless of camera position.
    }

    private void Start()
    {
        // Begin playing the idle clip immediately so the engine is audible from frame 1.
        if (idleClip != null && _activeSource != null)
        {
            _activeSource.clip   = idleClip;
            _activeSource.volume = maxVolume;
            _activeSource.Play();
            _currentClip = idleClip;
        }
    }

    private void Update()
    {
        if (_car == null) return;

        float normalizedSpeed = _car.NormalizedSpeed;          // 0 = stopped, 1 = max speed
        bool  accelerating    = _car.AccelerationInput > 0.1f; // true if W is held

        // Pick the ideal clip for this speed and throttle state.
        AudioClip targetClip = GetTargetClip(normalizedSpeed, accelerating);

        // Only crossfade if the target clip has changed.
        if (targetClip != _currentClip && targetClip != null)
        {
            CrossfadeTo(targetClip);
            _currentClip = targetClip;
        }

        // Vary pitch continuously within the current band to simulate RPM rise.
        float pitch = Mathf.Lerp(pitchMin, pitchMax, normalizedSpeed);
        if (_activeSource != null) _activeSource.pitch = pitch;
        if (_fadingSource != null) _fadingSource.pitch = pitch;

        // Ramp the active source to full volume and the fading source to silence.
        if (_activeSource != null)
            _activeSource.volume = Mathf.MoveTowards(
                _activeSource.volume, maxVolume, crossfadeSpeed * Time.deltaTime);
        if (_fadingSource != null && _fadingSource.volume > 0f)
            _fadingSource.volume = Mathf.MoveTowards(
                _fadingSource.volume, 0f, crossfadeSpeed * Time.deltaTime);
    }

    // ------------------------------------------------------------------ //
    //  Clip selection
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Selects the most appropriate audio clip based on normalised speed and
    /// whether the throttle is being applied.
    /// Falls back to simpler clips if the preferred one is not assigned.
    /// </summary>
    private AudioClip GetTargetClip(float speed, bool accelerating)
    {
        // At very low speed always use idle regardless of throttle state.
        if (speed < 0.05f)
            return idleClip;

        if (accelerating)
        {
            if (speed < 0.3f) return lowOnClip  ?? idleClip;
            if (speed < 0.6f) return medOnClip  ?? lowOnClip  ?? idleClip;
            if (speed < 0.9f) return highOnClip ?? medOnClip  ?? idleClip;
            return maxRPMClip ?? highOnClip ?? idleClip;
        }
        else
        {
            // Off-throttle clips sound like the engine is decelerating.
            if (speed < 0.3f) return lowOffClip  ?? lowOnClip  ?? idleClip;
            if (speed < 0.6f) return medOffClip  ?? medOnClip  ?? idleClip;
            return highOffClip ?? highOnClip ?? idleClip;
        }
    }

    // ------------------------------------------------------------------ //
    //  Crossfade logic
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Swaps the active and fading AudioSources and starts the new clip
    /// playing at zero volume on the newly active source.
    /// The volume ramp in Update() then blends the two over crossfadeSpeed seconds.
    /// </summary>
    private void CrossfadeTo(AudioClip clip)
    {
        // Swap roles: the current active becomes the fading-out source.
        var temp      = _activeSource;
        _activeSource = _fadingSource;
        _fadingSource = temp;

        if (_activeSource == null) return;

        // Start the new clip silently — Update() ramps it to maxVolume.
        _activeSource.clip   = clip;
        _activeSource.volume = 0f;
        _activeSource.Play();
    }
}
