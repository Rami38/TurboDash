using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Race manager for Level 3 — a straight-line boss race with no laps.
/// The first racer (player or boss) to cross the Level3FinishLine trigger wins.
/// 
/// RESPONSIBILITIES:
///   • Tracks whether the race has started and whether it has finished.
///   • Updates the HUD status label ("YOU'RE AHEAD!", "BOSS IS AHEAD!", etc.)
///     based on vertical Y position comparison each frame.
///   • Handles win condition: displays result text, unlocks next scene, then loads it.
///   • Handles lose condition: shows retry prompt and waits for R or Escape input.
/// 
/// START / COUNTDOWN INTEGRATION:
///   If a RaceCountdown is in the scene, _raceStarted stays false until
///   RaceCountdown calls StartRace() at the end of its GO sequence.
///   If no RaceCountdown exists, the race starts immediately (useful for testing).
/// </summary>
public class Level3RaceManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Scene Navigation")]
    // Build index of the main menu scene — used for the "Escape to menu" option.
    [SerializeField] private int mainMenuSceneIndex = 0;
    // Build index of the victory / next scene loaded after the player wins.
    [SerializeField] private int nextLevelSceneIndex = 4;

    [Header("Racers")]
    // The player's transform — used for Y-position comparison to show ahead/behind status.
    [SerializeField] private Transform playerTransform;
    // The boss AI component — used for position tracking and freezing on race end.
    [SerializeField] private BossAI boss;
    [Tooltip("Reference to the player's car controller so it can be frozen on race end.")]
    // We freeze both cars the moment someone crosses the finish line.
    [SerializeField] private TopDownCarController playerCar;

    [Header("UI")]
    // Shows real-time status: "YOU'RE AHEAD!" / "BOSS IS AHEAD!" / "NECK AND NECK!"
    [SerializeField] private TextMeshProUGUI raceStatusText;
    // Shows the final result: "YOU WIN!" or "BOSS WINS!" — hidden until race ends.
    [SerializeField] private TextMeshProUGUI raceResultText;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Prevents the finish line from firing twice (e.g. both cars crossing near-simultaneously).
    private bool _raceFinished;
    // Set to true when RaceCountdown calls StartRace() — nothing happens before this.
    private bool _raceStarted;

    // ------------------------------------------------------------------ //
    //  Public read-only state (read by PauseMenu to know if pausing is allowed)
    // ------------------------------------------------------------------ //

    public bool RaceStarted  => _raceStarted;
    public bool RaceFinished => _raceFinished;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        _raceFinished = false;

        // If no RaceCountdown exists in the scene, start immediately.
        // Otherwise, wait for RaceCountdown.StartRace() to be called.
        _raceStarted = FindAnyObjectByType<RaceCountdown>() == null;

        // Keep the result text hidden until someone crosses the finish.
        if (raceResultText != null)
            raceResultText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Don't update the UI before the race starts or after it ends.
        if (_raceFinished || !_raceStarted) return;

        UpdateStatusUI();
    }

    // ------------------------------------------------------------------ //
    //  Public API (called by RaceCountdown and Level3FinishLine)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Called by RaceCountdown when the GO phase completes.
    /// Allows the Update loop and finish-line detection to begin.
    /// </summary>
    public void StartRace()
    {
        _raceStarted = true;
    }

    /// <summary>
    /// Called by Level3FinishLine when the player's collider enters the trigger.
    /// Starts the win sequence coroutine.
    /// </summary>
    public void OnPlayerFinished()
    {
        if (_raceFinished) return;
        StartCoroutine(EndRace(true));
    }

    /// <summary>
    /// Called by Level3FinishLine when the boss's collider enters the trigger.
    /// Starts the lose sequence coroutine.
    /// </summary>
    public void OnBossFinished()
    {
        if (_raceFinished) return;
        StartCoroutine(EndRace(false));
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Compares the player's Y position against the boss's Y position and
    /// displays a real-time "who's winning" message on the HUD.
    /// Works because Level 3 is a vertical race — higher Y = further ahead.
    /// </summary>
    private void UpdateStatusUI()
    {
        if (raceStatusText == null || playerTransform == null || boss == null) return;

        float playerY = playerTransform.position.y;
        float bossY   = boss.transform.position.y;
        float diff    = playerY - bossY;

        // Show different messages depending on who is ahead.
        if (diff > 1f)
            raceStatusText.text = "YOU'RE AHEAD!";
        else if (diff < -1f)
            raceStatusText.text = "BOSS IS AHEAD!";
        else
            raceStatusText.text = "NECK AND NECK!";
    }

    /// <summary>
    /// Handles the race end sequence:
    ///   Win  → freeze cars, show result, unlock next scene, load it after 3 seconds.
    ///   Lose → freeze cars, show result, wait for R (retry) or Escape (main menu).
    /// </summary>
    private IEnumerator EndRace(bool playerWon)
    {
        _raceFinished = true;

        // Immediately stop both cars so neither keeps rolling toward the finish.
        if (playerCar != null) playerCar.Freeze();
        if (boss != null)      boss.Freeze();

        // Hide the status label and show the result panel.
        if (raceStatusText != null)
            raceStatusText.gameObject.SetActive(false);

        if (raceResultText != null)
        {
            raceResultText.gameObject.SetActive(true);
            raceResultText.text = playerWon
                ? "YOU WIN!\nBoss Defeated!"
                : "BOSS WINS!\nPress R to Retry";
        }

        if (playerWon)
        {
            // Brief celebration pause before transitioning.
            yield return new WaitForSeconds(3f);

            // Unlock the victory scene in the level progress system.
            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.UnlockLevel(nextLevelSceneIndex);

            SceneManager.LoadScene(nextLevelSceneIndex);
        }
        else
        {
            // Wait indefinitely for player input after a loss.
            while (true)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.rKey.wasPressedThisFrame)
                    {
                        // Reload the current scene to retry the race from the beginning.
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                        yield break;
                    }
                    if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        // Quit to main menu.
                        SceneManager.LoadScene(mainMenuSceneIndex);
                        yield break;
                    }
                }
                yield return null; // wait one frame before checking again
            }
        }
    }
}
