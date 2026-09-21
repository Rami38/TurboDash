using UnityEngine;

/// <summary>
/// Trigger zone placed near the grandstand that plays a crowd cheer sound
/// when the player car enters the area.
/// 
/// HOW IT WORKS:
///   A CircleCollider2D (set to Is Trigger) detects the Player tag.
///   On enter the cheer plays once. If repeatInterval > 0, it continues
///   to play periodically while the player stays inside the zone.
/// 
/// HOW TO SET UP:
///   1. Add this script to an empty GameObject near the grandstand.
///   2. The script auto-sets the collider to isTrigger = true on Start.
///   3. Set repeatInterval = 0 to play only once on entry.
///   4. Set repeatInterval > 0 (e.g. 5) to repeat while the player stays inside.
///   5. Make sure the player car is tagged "Player".
///   6. Assign the crowd cheer clip to AudioManager.Instance.crowdCheerSFX
///      (or it falls back to AudioManager.GetOrCreate()).
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class CrowdAudioZone : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Tooltip("Seconds between repeated cheers while the player stays in the zone. 0 = play once only.")]
    // Leave at 0 to play a single cheer on entry; set to e.g. 5 for a sustained roar.
    [SerializeField] private float repeatInterval = 0f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Counts down from repeatInterval — when it hits 0 the cheer plays again.
    private float _cooldown;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        // Ensure the collider is a trigger regardless of how it was created.
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    // ------------------------------------------------------------------ //
    //  Trigger callbacks
    // ------------------------------------------------------------------ //

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only react to the player car.
        if (!other.CompareTag("Player")) return;

        PlayCheer();
        // Start the repeat cooldown immediately after the first cheer.
        _cooldown = repeatInterval;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Repeated cheering is disabled when repeatInterval is 0.
        if (repeatInterval <= 0f) return;
        if (!other.CompareTag("Player")) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f)
        {
            PlayCheer();
            // Reset the cooldown for the next repeat.
            _cooldown = repeatInterval;
        }
    }

    // ------------------------------------------------------------------ //
    //  Helper
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Asks the AudioManager to play the crowd cheer clip.
    /// Falls back to GetOrCreate() in case the player jumped directly into this
    /// scene without going through the Main Menu (no AudioManager instance yet).
    /// </summary>
    private void PlayCheer()
    {
        var am = AudioManager.Instance ?? AudioManager.GetOrCreate();
        if (am != null)
            am.PlayCrowdCheer();
    }
}
