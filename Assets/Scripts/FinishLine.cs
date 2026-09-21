using UnityEngine;

/// <summary>
/// Trigger collider that detects when the player or an opponent crosses the finish line.
/// Notifies the RaceManager so it can update lap counts and check win/lose conditions.
/// 
/// DETECTION LOGIC:
///   • "Player" tag  → RaceManager.OnPlayerCrossedFinishLine()
///   • "Opponent" tag → looks for OpponentAI first, then SplineOpponentAI
///     on the colliding GameObject (or its parent) and calls the corresponding method.
/// 
/// AUTO-DISABLE:
///   After the player crosses the finish line disableAfterPlayerCrossings times
///   (should equal the total lap count), this GameObject is disabled so the
///   finish line doesn't keep triggering after the race ends.
/// 
/// HOW TO SET UP:
///   1. Add a Collider2D (Is Trigger = true) to this GameObject.
///   2. Drag the RaceManager into the raceManager field.
///   3. Set disableAfterPlayerCrossings to match the total lap count (default 3).
/// </summary>
public class FinishLine : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // Reference to the scene's RaceManager — called when a car crosses.
    [SerializeField] private RaceManager raceManager;
    [Tooltip("Disable this GameObject after this many player crossings (should match total laps).")]
    // Once the player completes all laps, the finish line disables itself
    // so it doesn't keep firing during the win/transition sequence.
    [SerializeField] private int disableAfterPlayerCrossings = 3;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Counts how many times the player has crossed the line this race.
    private int _playerCrossings;

    // ------------------------------------------------------------------ //
    //  Trigger callback
    // ------------------------------------------------------------------ //

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (raceManager == null) return;

        if (other.CompareTag("Player"))
        {
            // Notify the race manager a player lap has been completed.
            raceManager.OnPlayerCrossedFinishLine();

            _playerCrossings++;
            // Disable after all laps are done to prevent ghost triggers.
            if (_playerCrossings >= disableAfterPlayerCrossings)
                gameObject.SetActive(false);
        }
        else if (other.CompareTag("Opponent"))
        {
            // Try to find an OpponentAI on the collider's object or its parent
            // (in case the car's hitbox is on a child object).
            var opponentAI = other.GetComponent<OpponentAI>()
                          ?? other.GetComponentInParent<OpponentAI>();

            if (opponentAI != null)
            {
                raceManager.OnOpponentCrossedFinishLine(opponentAI);
                return; // handled — don't fall through to spline check
            }

            // Also check for spline-based opponents (Level 1 uses SplineOpponentAI).
            var splineAI = other.GetComponent<SplineOpponentAI>()
                        ?? other.GetComponentInParent<SplineOpponentAI>();

            if (splineAI != null)
                raceManager.OnSplineOpponentCrossedFinishLine(splineAI);
        }
    }
}
