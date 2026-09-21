using UnityEngine;

/// <summary>
/// Finish line trigger for Level 3's one-on-one boss race.
/// Unlike the regular FinishLine (which tracks laps), this simply
/// reports who crossed first — first to cross wins, no laps involved.
/// 
/// HOW IT WORKS:
///   A Collider2D set to Is Trigger detects the Player and the boss (tagged "Opponent").
///   When either crosses, the corresponding method is called on Level3RaceManager
///   which then triggers the win/lose sequence.
/// 
/// HOW TO SET UP:
///   1. Add a Collider2D (Is Trigger = true) spanning the width of the finish line.
///   2. Drag the Level3RaceManager into raceManager.
///   3. Ensure the player car is tagged "Player" and the boss is tagged "Opponent".
/// </summary>
public class Level3FinishLine : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // The Level 3 race manager — handles outcome once someone crosses.
    [SerializeField] private Level3RaceManager raceManager;

    // ------------------------------------------------------------------ //
    //  Trigger callback
    // ------------------------------------------------------------------ //

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (raceManager == null) return;

        if (other.CompareTag("Player"))
        {
            // Player reached the finish first — player wins!
            raceManager.OnPlayerFinished();
        }
        else if (other.CompareTag("Opponent"))
        {
            // Boss reached the finish first — player loses.
            raceManager.OnBossFinished();
        }
    }
}
