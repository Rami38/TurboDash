using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Race manager for Levels 1 and 2 — tracks laps for the player and opponents,
/// determines win/lose, and handles scene transitions.
/// 
/// RESPONSIBILITIES:
///   • Counts how many times the player crosses the finish line.
///   • Tracks opponent lap counts via IncrementLap() calls from FinishLine.
///   • If any opponent finishes before the player, triggers a lose sequence.
///   • If the player finishes all laps first, triggers a win sequence.
///   • Updates a HUD position label showing the player's current race position.
/// 
/// COUNTDOWN INTEGRATION:
///   If a RaceCountdown is in the scene, the race waits for StartRace() to be called.
///   If not (e.g. testing without a countdown), autoStartIfNoCountdown allows
///   the race to begin immediately in Start().
/// 
/// HOW TO SET UP:
///   1. Add this to any persistent GameObject in the scene.
///   2. Assign the opponents and SplineOpponentAI arrays.
///   3. Wire the UI text fields in the Inspector.
///   4. Set nextLevelSceneIndex to the correct build index for the next level.
/// </summary>
public class RaceManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Scene Navigation")]
    // Build index of the main menu (for the Escape key in the lose screen).
    [SerializeField] private int mainMenuSceneIndex = 0;
    // Build index of the scene to load when the player wins this level.
    [SerializeField] private int nextLevelSceneIndex = 2;

    [Header("Race Settings")]
    // Total number of finish-line crossings required to win.
    [SerializeField] private int totalLaps = 3;

    [Header("Racers")]
    // Waypoint-based opponents (Level 2 uses these).
    [SerializeField] private OpponentAI[] opponents;
    // Spline-based opponents (Level 1 uses these).
    [SerializeField] private SplineOpponentAI[] splineOpponents;

    [Header("UI")]
    // Shows "Lap X / Y" in the HUD.
    [SerializeField] private TextMeshProUGUI lapCounterText;
    // Shows "YOU WIN!" or "YOU LOST!" at race end.
    [SerializeField] private TextMeshProUGUI raceResultText;
    // Shows the player's current position ("1st", "2nd", etc.) in the HUD.
    [SerializeField] private TextMeshProUGUI positionText;

    [Header("Countdown (optional)")]
    [Tooltip("If no RaceCountdown is in the scene, the race auto-starts immediately.")]
    // Allows testing race logic without needing a countdown in the scene.
    [SerializeField] private bool autoStartIfNoCountdown = true;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Number of times the player has crossed the finish line this race.
    private int  _playerLaps;
    // Prevents the race from ending more than once (e.g. two opponents finish on the same frame).
    private bool _raceFinished;
    // Set to true either by RaceCountdown or immediately if no countdown is present.
    private bool _raceStarted;

    // ------------------------------------------------------------------ //
    //  Public read-only state (read by PauseMenu to decide if pausing is allowed)
    // ------------------------------------------------------------------ //

    public bool RaceStarted  => _raceStarted;
    public bool RaceFinished => _raceFinished;
    public int  PlayerLaps   => _playerLaps;
    public int  TotalLaps    => totalLaps;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        _raceFinished = false;
        _raceStarted  = false;

        UpdateLapUI();
        UpdatePositionUI();

        // Hide the result text until the race ends.
        if (raceResultText != null)
            raceResultText.gameObject.SetActive(false);

        // If no RaceCountdown is present, start immediately (useful for testing).
        if (autoStartIfNoCountdown && FindAnyObjectByType<RaceCountdown>() == null)
            StartRace();
    }

    private void Update()
    {
        // Nothing to update while frozen by countdown or after the race ends.
        if (_raceFinished || !_raceStarted) return;

        UpdatePositionUI();
        CheckOpponentsFinished();
    }

    // ------------------------------------------------------------------ //
    //  Public API (called by RaceCountdown and FinishLine)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Called by RaceCountdown when the countdown reaches GO.
    /// Allows the Update loop and finish line detection to begin.
    /// </summary>
    public void StartRace()
    {
        _raceStarted = true;
    }

    /// <summary>
    /// Called by FinishLine when the player crosses it.
    /// Increments the lap counter, plays a sound, and checks for win.
    /// </summary>
    public void OnPlayerCrossedFinishLine()
    {
        if (_raceFinished || !_raceStarted) return;

        _playerLaps++;
        UpdateLapUI();

        // Play a lap-complete chime through the global AudioManager.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLapComplete();

        // Check if this crossing was the final lap.
        if (_playerLaps >= totalLaps)
            StartCoroutine(FinishRace(true));
    }

    /// <summary>
    /// Called by FinishLine when a waypoint-based opponent crosses it.
    /// Just increments that opponent's internal lap counter.
    /// </summary>
    public void OnOpponentCrossedFinishLine(OpponentAI opponent)
    {
        if (_raceFinished || !_raceStarted) return;
        opponent.IncrementLap();
    }

    /// <summary>
    /// Called by FinishLine when a spline-based opponent crosses it.
    /// Just increments that opponent's internal lap counter.
    /// </summary>
    public void OnSplineOpponentCrossedFinishLine(SplineOpponentAI opponent)
    {
        if (_raceFinished || !_raceStarted) return;
        opponent.IncrementLap();
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Checks every opponent's lap count each frame.
    /// If any opponent has completed all laps, the player loses.
    /// </summary>
    private void CheckOpponentsFinished()
    {
        if (opponents != null)
        {
            foreach (var opp in opponents)
            {
                if (opp != null && opp.LapsCompleted >= totalLaps)
                {
                    StartCoroutine(FinishRace(false));
                    return; // stop checking after the first opponent finishes
                }
            }
        }

        if (splineOpponents != null)
        {
            foreach (var opp in splineOpponents)
            {
                if (opp != null && opp.LapsCompleted >= totalLaps)
                {
                    StartCoroutine(FinishRace(false));
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Calculates the player's current race position (1 = first place).
    /// 
    /// HOW IT WORKS:
    ///   Start at position 1. For each opponent that has more laps than the player,
    ///   or the same laps but is closer to the next waypoint, increment the position.
    /// </summary>
    public int GetPlayerPosition()
    {
        int position = 1; // assume first until an opponent proves otherwise

        if (opponents != null)
        {
            foreach (var opp in opponents)
            {
                if (opp == null) continue;

                if (opp.LapsCompleted > _playerLaps)
                    position++; // opponent is a full lap ahead
                else if (opp.LapsCompleted == _playerLaps &&
                         opp.DistanceToNextWaypoint() < GetPlayerDistanceEstimate())
                    position++; // same lap but opponent is closer to the next waypoint
            }
        }

        if (splineOpponents != null)
        {
            foreach (var opp in splineOpponents)
            {
                if (opp == null) continue;
                if (opp.LapsCompleted > _playerLaps)
                    position++;
            }
        }

        return position;
    }

    /// <summary>
    /// Returns an estimate of the player's distance to the next waypoint.
    /// Currently returns 0 (player is always assumed slightly ahead within a lap)
    /// since the player car doesn't have a waypoint target to measure against.
    /// </summary>
    private float GetPlayerDistanceEstimate()
    {
        // A real implementation would find the nearest waypoint on the player's
        // expected path and measure the remaining distance. For now, 0 biases
        // the position calculation slightly in the player's favour.
        return 0f;
    }

    /// <summary>Updates the lap counter HUD text to "Lap X / Y".</summary>
    private void UpdateLapUI()
    {
        if (lapCounterText != null)
            // Min() clamps the displayed lap so it never shows "Lap 4 / 3" after the final crossing.
            lapCounterText.text = $"Lap {Mathf.Min(_playerLaps + 1, totalLaps)} / {totalLaps}";
    }

    /// <summary>Updates the HUD position label to the player's current race position.</summary>
    private void UpdatePositionUI()
    {
        if (positionText == null) return;

        int    pos    = GetPlayerPosition();
        // English ordinal suffixes: 1st, 2nd, 3rd, 4th, 5th...
        string suffix = pos switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        positionText.text = $"{pos}{suffix}";
    }

    /// <summary>
    /// Handles the race end sequence for both win and lose outcomes.
    ///
    /// Win  → shows result text, unlocks the next level, loads the next scene after 3 s.
    /// Lose → shows result text, then waits for R (retry) or Escape (main menu).
    /// </summary>
    private IEnumerator FinishRace(bool playerWon)
    {
        _raceFinished = true;

        if (raceResultText != null)
        {
            raceResultText.gameObject.SetActive(true);
            raceResultText.text = playerWon
                ? "YOU WIN!\nLevel Complete!"
                : "YOU LOST!\nPress R to Retry";
        }

        if (playerWon)
        {
            yield return new WaitForSeconds(3f); // brief celebration pause

            // Unlock the next level in the progress system so it appears in Level Select.
            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.UnlockLevel(nextLevelSceneIndex);

            SceneManager.LoadScene(nextLevelSceneIndex);
        }
        else
        {
            // Wait indefinitely until the player presses R or Escape.
            while (true)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.rKey.wasPressedThisFrame)
                    {
                        // Reload the current scene from the beginning.
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                        yield break;
                    }
                    if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        SceneManager.LoadScene(mainMenuSceneIndex);
                        yield break;
                    }
                }
                yield return null; // check again next frame
            }
        }
    }
}
