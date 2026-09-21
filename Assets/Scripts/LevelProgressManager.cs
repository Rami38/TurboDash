using UnityEngine;

/// <summary>
/// Persistent singleton that tracks which levels have been unlocked by the player.
/// Data is saved to PlayerPrefs (Unity's simple key-value storage) so progress
/// survives between play sessions.
/// 
/// DESIGN — Bitmask:
///   Each level corresponds to a single bit in an integer.
///   Level 1 = bit 0, Level 2 = bit 1, Level 3 = bit 2.
///   A bitwise OR sets a bit (unlock). An AND check reads it.
///   This is compact and easy to extend for more levels.
/// 
/// SAVE VERSION:
///   SaveVersion is a hardcoded integer. If the game's level layout changes
///   (e.g. a level is added or removed), bump SaveVersion to automatically
///   wipe old save data so players don't load an inconsistent state.
/// 
/// DESIGN — Singleton:
///   Lives in the Main Menu scene with DontDestroyOnLoad so it persists through
///   all level loads. Access from anywhere via LevelProgressManager.Instance.
/// </summary>
public class LevelProgressManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Save data constants
    // ------------------------------------------------------------------ //

    // Bump this whenever the save format changes to force a fresh start.
    private const int SaveVersion = 2;
    // PlayerPrefs key for the version number (used to detect stale saves).
    private const string VersionKey  = "ProgressSaveVersion";
    // PlayerPrefs key for the bitmask of unlocked levels.
    private const string ProgressKey = "UnlockedLevelsBitmask";

    // Each level maps to a specific bit position in the bitmask.
    private const int Level1Bit = 1 << 0; // 0b001 — Level 1 (build index 1)
    private const int Level2Bit = 1 << 1; // 0b010 — Level 2 (build index 2)
    private const int Level3Bit = 1 << 2; // 0b100 — Level 3 (build index 3)

    // ------------------------------------------------------------------ //
    //  Singleton
    // ------------------------------------------------------------------ //

    public static LevelProgressManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // The in-memory bitmask of unlocked levels — kept in sync with PlayerPrefs.
    private int _unlockedBitmask;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        // Enforce singleton — destroy any duplicate that loads in a later scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Wipe stale save data if the version has changed, otherwise load it.
        MigrateIfNeeded();

        // Level 1 is always unlocked — ensure this is always true on every launch.
        _unlockedBitmask |= Level1Bit;
        SaveProgress();
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns true if the level at the given build index has been unlocked.
    /// Called by LevelSelectMenu to decide whether to enable/disable buttons.
    /// </summary>
    public bool IsUnlocked(int levelBuildIndex)
    {
        int bit = BitForLevel(levelBuildIndex);
        // bit == 0 means the build index is not a recognised level.
        return bit != 0 && (_unlockedBitmask & bit) != 0;
    }

    /// <summary>
    /// Marks the level as unlocked and immediately saves to PlayerPrefs.
    /// Called by RaceManager / Level3RaceManager when the player wins.
    /// </summary>
    public void UnlockLevel(int levelBuildIndex)
    {
        int bit = BitForLevel(levelBuildIndex);
        if (bit == 0) return; // unrecognised index — do nothing

        // Set the bit (OR is safe to call even if the bit is already set).
        _unlockedBitmask |= bit;
        SaveProgress();
    }

    /// <summary>
    /// Resets progress to only Level 1 unlocked.
    /// Useful for a "reset save" button in a settings menu.
    /// </summary>
    public void ResetProgress()
    {
        _unlockedBitmask = Level1Bit;
        SaveProgress();
    }

    // ------------------------------------------------------------------ //
    //  Internal helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Loads saved progress from PlayerPrefs, or resets to Level 1 only if
    /// the saved version is older than the current SaveVersion constant.
    /// </summary>
    private void MigrateIfNeeded()
    {
        int savedVersion = PlayerPrefs.GetInt(VersionKey, 0);

        if (savedVersion < SaveVersion)
        {
            // Saved data is outdated — start fresh so no level is accidentally pre-unlocked.
            _unlockedBitmask = Level1Bit;
            PlayerPrefs.SetInt(VersionKey, SaveVersion);
            PlayerPrefs.Save();
        }
        else
        {
            // Load the previously saved bitmask (default to Level 1 only).
            _unlockedBitmask = PlayerPrefs.GetInt(ProgressKey, Level1Bit);
        }
    }

    /// <summary>
    /// Writes the current bitmask and version to PlayerPrefs and flushes to disk.
    /// PlayerPrefs.Save() forces an immediate write so data is not lost on crash.
    /// </summary>
    private void SaveProgress()
    {
        PlayerPrefs.SetInt(ProgressKey, _unlockedBitmask);
        PlayerPrefs.SetInt(VersionKey,  SaveVersion);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Converts a scene build index to the corresponding bitmask bit.
    /// Returns 0 for unrecognised indices so callers can detect invalid input.
    /// </summary>
    private static int BitForLevel(int buildIndex) => buildIndex switch
    {
        1 => Level1Bit,
        2 => Level2Bit,
        3 => Level3Bit,
        _ => 0,  // unknown build index
    };
}
