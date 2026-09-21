using UnityEngine;

/// <summary>
/// Adds a looping sine-wave scale pulse and/or a color flicker animation to a sprite.
/// Used on grandstand tribunes and light-pole decorations to add life to the scene.
/// 
/// ANIMATION TYPES:
///   ScalePulse   — the sprite oscillates slightly in X (fans waving side to side).
///   ColorFlicker — the sprite color lerps between colorA and colorB (flashing lights).
///   Both         — applies both effects simultaneously.
///   None         — disables all animation (useful to turn off individual objects).
/// 
/// PHASE OFFSET:
///   randomizePhaseOnAwake gives each instance a different starting angle in the
///   sine cycle, so nearby tribunes don't all pulse in perfect lockstep.
/// 
/// HOW TO SET UP:
///   1. Add to any GameObject that has a SpriteRenderer.
///   2. Choose an AnimationType in the Inspector.
///   3. Adjust pulse/flicker parameters as desired.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FanAnimation : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Animation type enum
    // ------------------------------------------------------------------ //

    public enum AnimationType { None, ScalePulse, ColorFlicker, Both }

    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Animation Type")]
    [SerializeField] private AnimationType animationType = AnimationType.Both;

    [Header("Scale Pulse")]
    [Tooltip("Base scale of the sprite (1 = original size).")]
    // Kept separate from transform.localScale so we can add the pulse on top.
    [SerializeField] private float baseScale = 1f;
    [Tooltip("How much the scale oscillates above and below baseScale.")]
    // e.g. 0.05 means the sprite swings between 0.95× and 1.05× of its original width.
    [SerializeField] private float pulseAmplitude = 0.05f;
    [Tooltip("Oscillations per second.")]
    [SerializeField] private float pulseFrequency = 1.8f;

    [Header("Color Flicker")]
    [Tooltip("Color A of the flicker cycle.")]
    [SerializeField] private Color colorA = Color.white;
    [Tooltip("Color B of the flicker cycle.")]
    // A warm yellow-orange, simulating a flashing race light.
    [SerializeField] private Color colorB = new Color(1f, 0.85f, 0.4f, 1f);
    [Tooltip("Flicker speed — higher = faster color transitions.")]
    [SerializeField] private float flickerFrequency = 0.8f;

    [Header("Phase Offset")]
    [Tooltip("Random phase offset so nearby fans don't all pulse in sync.")]
    // Set manually or randomized at runtime — controls where in the sine cycle we start.
    [SerializeField] private float phaseOffset;
    [Tooltip("If true, picks a random phase offset each time the scene loads.")]
    [SerializeField] private bool randomizePhaseOnAwake = true;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private SpriteRenderer _spriteRenderer;
    // Captured on Awake so pulse is applied relative to the correct starting scale.
    private Vector3 _originalScale;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalScale  = transform.localScale;

        // Each object picks a different random starting phase so they animate out of sync.
        if (randomizePhaseOnAwake)
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        // Offset Time.time by the phase so different instances are at different cycle positions.
        float time = Time.time + phaseOffset;

        if (animationType == AnimationType.ScalePulse || animationType == AnimationType.Both)
            ApplyScalePulse(time);

        if (animationType == AnimationType.ColorFlicker || animationType == AnimationType.Both)
            ApplyColorFlicker(time);
    }

    // ------------------------------------------------------------------ //
    //  Animation methods
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Oscillates the sprite's X scale using a sine wave.
    /// Y scale stays fixed so the sprite appears to "wave" sideways like a cheering fan.
    /// </summary>
    private void ApplyScalePulse(float time)
    {
        // sin returns -1..+1; multiplied by amplitude and added to 1.0 gives 0.95..1.05 (with 0.05 amplitude).
        float pulse = 1f + Mathf.Sin(time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
        transform.localScale = new Vector3(
            _originalScale.x * pulse, // only X oscillates — fans waving
            _originalScale.y,          // Y stays fixed
            _originalScale.z
        );
    }

    /// <summary>
    /// Smoothly lerps the sprite color between colorA and colorB using a sine wave.
    /// Mapped 0..1 so Lerp gets a smooth 0→1→0 input each cycle.
    /// </summary>
    private void ApplyColorFlicker(float time)
    {
        // (sin + 1) / 2 remaps -1..1 → 0..1 for use as a Lerp t value.
        float t = (Mathf.Sin(time * flickerFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        _spriteRenderer.color = Color.Lerp(colorA, colorB, t);
    }
}
