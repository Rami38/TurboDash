using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays a 3 → 2 → 1 → GO! countdown at the start of a race and
/// plays the full "3-2-1 Go" audio clip simultaneously.
/// 
/// RESPONSIBILITIES:
///   1. Freezes all racers (player car, opponents, spline opponents, boss) on Start.
///   2. Waits for any InfoPopup to be dismissed before starting the sequence.
///   3. Plays the countdown audio clip once (it naturally says "3-2-1 Go").
///   4. Updates the countdown text label in sync with the audio timing.
///   5. Unfreezes all racers and notifies the race manager(s) to begin.
/// 
/// AUDIO DESIGN:
///   A single audio clip ("3-2-1-go...mp3") is played once at the start.
///   Each scene has its own AudioSource on the RaceCountdown GameObject —
///   no dependency on the global AudioManager singleton.
///   Assign the clip to "Countdown Clip" in the Inspector.
/// 
/// SCENE COMPATIBILITY:
///   Works for all three levels:
///   • Levels 1 & 2 — uses RaceManager + OpponentAI / SplineOpponentAI.
///   • Level 3      — uses Level3RaceManager + BossAI.
///   Fields for unused levels can be left null with no errors.
/// </summary>
public class RaceCountdown : MonoBehaviour
{
    [Header("References (Levels 1 & 2)")]
    // The lap-based race manager for Levels 1 and 2 — called via StartRace() at GO.
    [SerializeField] private RaceManager raceManager;
    // The player car — frozen before the countdown, unfrozen at GO.
    [SerializeField] private TopDownCarController playerCar;
    // Standard waypoint-following opponents (Level 2).
    [SerializeField] private OpponentAI[] opponents;
    // Spline-following opponents (Level 1).
    [SerializeField] private SplineOpponentAI[] splineOpponents;

    [Header("References (Level 3 Boss Race)")]
    // The boss race manager — only needed in Level 3, leave null otherwise.
    [SerializeField] private Level3RaceManager level3RaceManager;
    // The boss AI — only needed in Level 3.
    [SerializeField] private BossAI boss;

    [Header("UI")]
    // TextMeshPro element that shows 3 → 2 → 1 → GO! during the countdown.
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Timing")]
    // Seconds each number stays on screen — should match the natural audio pacing.
    [SerializeField] private float countdownInterval = 1f;
    // How long "GO!" stays on screen before the text is hidden.
    [SerializeField] private float goDisplayDuration = 0.5f;

    [Header("Audio")]
    // The AudioSource on this same GameObject — used to play the clip locally.
    // Self-contained: each scene has its own source, no AudioManager needed.
    [SerializeField] private AudioSource audioSource;
    /// <summary>Single "3-2-1 Go" clip that plays at the start of the countdown sequence.</summary>
    [SerializeField] private AudioClip countdownClip;

    private InfoPopup _infoPopup;

    private void Awake()
    {
        // Find the InfoPopup (if any) so we can wait for it to be dismissed.
        _infoPopup = FindAnyObjectByType<InfoPopup>();
    }

    private void Start()
    {
        // Freeze all racers immediately so nobody moves before the countdown ends.
        FreezeAll();

        // Hide the countdown text until the sequence is ready to start.
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        StartCoroutine(CountdownSequence());
    }

    /// <summary>
    /// Main countdown sequence:
    ///   1. Waits two frames so all other Start() methods have run.
    ///   2. Waits until the InfoPopup is dismissed (if one is in the scene).
    ///   3. Plays the single "3-2-1 Go" audio clip once.
    ///   4. Steps through 3 → 2 → 1 → GO! at countdownInterval second intervals.
    ///   5. Unfreezes all racers and signals the race manager(s) to begin.
    ///   6. Hides the text after a brief GO! display.
    ///
    /// WaitForSecondsRealtime is used (not WaitForSeconds) so the countdown
    /// is not blocked by Time.timeScale = 0, which InfoPopup may set while open.
    /// </summary>
    private IEnumerator CountdownSequence()
    {
        // Wait two frames so every other script in the scene has finished Start().
        yield return null;
        yield return null;

        // Poll until the InfoPopup is dismissed before starting the countdown.
        if (_infoPopup != null && _infoPopup.IsShowing)
            yield return new WaitUntil(() => !_infoPopup.IsShowing);

        // One extra frame to let DismissPopup() restore Time.timeScale to 1.
        yield return null;

        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        // Play the full "3-2-1 Go" clip — it covers the whole countdown in audio.
        if (audioSource != null && countdownClip != null)
            audioSource.PlayOneShot(countdownClip);

        // Update the text display in step with the audio timing.
        SetCountdownText("3");
        yield return new WaitForSecondsRealtime(countdownInterval);

        SetCountdownText("2");
        yield return new WaitForSecondsRealtime(countdownInterval);

        SetCountdownText("1");
        yield return new WaitForSecondsRealtime(countdownInterval);

        // Show GO!, release all cars, and notify the race managers to begin tracking.
        SetCountdownText("GO!");
        UnfreezeAll();

        if (raceManager       != null) raceManager.StartRace();
        if (level3RaceManager != null) level3RaceManager.StartRace();

        yield return new WaitForSecondsRealtime(goDisplayDuration);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    /// <summary>Sets the countdown text label safely (null-checked).</summary>
    private void SetCountdownText(string text)
    {
        if (countdownText != null)
            countdownText.text = text;
    }

    /// <summary>
    /// Freezes all racers so nothing moves during the countdown.
    /// Rigidbody simulation is disabled on AI cars to prevent physics interactions.
    /// </summary>
    private void FreezeAll()
    {
        if (playerCar != null) playerCar.Freeze();

        if (opponents != null)
            foreach (var opp in opponents)
                if (opp != null) opp.Freeze();

        if (splineOpponents != null)
            foreach (var opp in splineOpponents)
                if (opp != null) opp.Freeze();

        if (boss != null) boss.Freeze();
    }

    /// <summary>
    /// Unfreezes all racers once the countdown finishes.
    /// NotifyInputReceived() on the player car ensures it begins moving immediately
    /// without requiring the player to press a key again after GO!
    /// </summary>
    private void UnfreezeAll()
    {
        if (playerCar != null)
        {
            playerCar.Unfreeze();
            // Mark the car as "started" so it reacts to input right away.
            playerCar.NotifyInputReceived();
        }

        if (opponents != null)
            foreach (var opp in opponents)
                if (opp != null) opp.Unfreeze();

        if (splineOpponents != null)
            foreach (var opp in splineOpponents)
                if (opp != null) opp.Unfreeze();

        if (boss != null) boss.Unfreeze();
    }

}
