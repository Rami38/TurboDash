using UnityEngine;

/// <summary>
/// Central audio manager that persists across all scene loads (DontDestroyOnLoad).
/// Owns two AudioSources: one for looping background music and one for short sound effects.
/// 
/// DESIGN PATTERN — Singleton:
///   A single instance is created in the Main Menu scene and survives for the whole session.
///   Any script that needs audio calls AudioManager.Instance or AudioManager.GetOrCreate().
///   GetOrCreate() is a safety net — if someone presses Play directly on a race scene in the
///   Editor (bypassing Main Menu), it builds a minimal AudioManager on the fly so nothing breaks.
/// 
/// HOW TO SET UP:
///   1. Add this script to a GameObject in the Main Menu scene.
///   2. Add two child AudioSource components; drag them into musicSource and sfxSource.
///   3. Assign all audio clips in the Inspector.
///   4. Music volume and SFX volume can be changed at runtime (e.g., from a settings menu).
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Singleton access
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The one and only AudioManager instance alive in the session.
    /// Null until the first AudioManager Awake() runs.
    /// </summary>
    public static AudioManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Audio Sources")]
    // Dedicated AudioSource for music — set to loop in the Inspector or via code.
    [SerializeField] private AudioSource musicSource;
    // Dedicated AudioSource for one-shot SFX — does NOT loop.
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Clips")]
    // Clip played while the player is in the Main Menu.
    [SerializeField] private AudioClip mainMenuMusic;
    // Clip played during any race level.
    [SerializeField] private AudioClip raceMusic;

    [Header("SFX Clips")]
    // Short click played whenever a UI button is pressed.
    [SerializeField] private AudioClip buttonClickSFX;
    // Impact thud when the player car hits a wall.
    [SerializeField] private AudioClip wallHitSFX;
    // Fanfare played when the player wins a race.
    [SerializeField] private AudioClip winSFX;
    // Negative sting played when the player loses a race.
    [SerializeField] private AudioClip loseSFX;
    // Short chime played each time the player completes a lap.
    [SerializeField] private AudioClip lapCompleteSFX;
    // Crowd cheer triggered by CrowdAudioZone near the grandstand.
    [SerializeField] private AudioClip crowdCheerSFX;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume   = 0.8f;

    // ------------------------------------------------------------------ //
    //  GetOrCreate — safety net factory
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns the existing AudioManager instance.
    /// If none exists (e.g. developer pressed Play on a race scene directly),
    /// builds a minimal one at runtime so audio calls don't silently fail.
    /// 
    /// Clips that cannot be set here (they live in scene assets, not Resources)
    /// will simply be null — non-critical SFX just won't play.
    /// </summary>
    public static AudioManager GetOrCreate()
    {
        // If a real instance already exists, return it immediately.
        if (Instance != null) return Instance;

        // Destroy any leftover auto-created AudioManagers from a previous
        // play session in the Editor (they can persist between Play runs).
        foreach (var stale in FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
        {
            if (stale != null && stale != Instance)
                Destroy(stale.gameObject);
        }

        // Build a brand-new minimal AudioManager.
        var go = new GameObject("AudioManager_Auto");
        var am = go.AddComponent<AudioManager>();

        // Create the two AudioSources programmatically since we have no Inspector.
        am.musicSource             = go.AddComponent<AudioSource>();
        am.musicSource.playOnAwake = false;
        am.musicSource.loop        = true;   // music always loops

        am.sfxSource             = go.AddComponent<AudioSource>();
        am.sfxSource.playOnAwake = false;    // SFX are triggered manually

        // Attempt to load clips that were placed in a Resources folder.
        // These are fallback assets — not required to exist.
        am.lapCompleteSFX = Resources.Load<AudioClip>("LapComplete");
        am.winSFX         = Resources.Load<AudioClip>("Win");
        am.loseSFX        = Resources.Load<AudioClip>("Lose");
        am.wallHitSFX     = Resources.Load<AudioClip>("WallHit");
        am.raceMusic      = Resources.Load<AudioClip>("RaceMusic");

        // Keep this object alive across any future scene loads.
        DontDestroyOnLoad(go);
        return am;
    }

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        // Enforce the singleton pattern:
        // If another AudioManager already exists (e.g. carried over from a previous
        // scene via DontDestroyOnLoad), destroy this duplicate and bail out.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // This is the first (and only) AudioManager — register it and persist.
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Apply serialized volume values to the AudioSources immediately.
        if (musicSource != null) musicSource.volume = musicVolume;
        if (sfxSource   != null) sfxSource.volume   = sfxVolume;
    }

    // ------------------------------------------------------------------ //
    //  Music API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Starts playing the given music clip, looping by default.
    /// Does nothing if the clip is already playing (prevents restart on scene reload).
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;

        // Avoid restarting the same track that is already playing.
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip   = clip;
        musicSource.loop   = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    /// <summary>Plays the main menu background music, looping.</summary>
    public void PlayMenuMusic() => PlayMusic(mainMenuMusic);

    /// <summary>Plays the in-race background music, looping.</summary>
    public void PlayRaceMusic() => PlayMusic(raceMusic);

    /// <summary>Stops whichever music track is currently playing.</summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    // ------------------------------------------------------------------ //
    //  SFX API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Plays a one-shot sound effect through the shared SFX AudioSource.
    /// PlayOneShot allows multiple overlapping sounds on the same source.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        // sfxVolume is passed as the volume scale for this specific shot.
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // Named shortcuts so callers don't need to know which clip is which:
    /// <summary>Plays the UI button click sound.</summary>
    public void PlayButtonClick()  => PlaySFX(buttonClickSFX);
    /// <summary>Plays the wall impact sound.</summary>
    public void PlayWallHit()      => PlaySFX(wallHitSFX);
    /// <summary>Plays the win fanfare.</summary>
    public void PlayWin()          => PlaySFX(winSFX);
    /// <summary>Plays the lose sting.</summary>
    public void PlayLose()         => PlaySFX(loseSFX);
    /// <summary>Plays the lap-complete chime.</summary>
    public void PlayLapComplete()  => PlaySFX(lapCompleteSFX);
    /// <summary>Plays the crowd cheer effect.</summary>
    public void PlayCrowdCheer()   => PlaySFX(crowdCheerSFX);

    // ------------------------------------------------------------------ //
    //  Volume control
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Changes the music volume at runtime.
    /// Value is clamped between 0 (silent) and 1 (full volume).
    /// </summary>
    public void SetMusicVolume(float vol)
    {
        musicVolume = Mathf.Clamp01(vol);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Changes the SFX volume at runtime.
    /// Affects all future PlaySFX calls but not currently playing sounds.
    /// </summary>
    public void SetSFXVolume(float vol)
    {
        sfxVolume = Mathf.Clamp01(vol);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }
}
