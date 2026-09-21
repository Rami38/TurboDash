using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Shows a small tutorial / control-info popup at the start of a level.
/// The popup can optionally pause the game while shown so the player has time
/// to read without the countdown running behind it.
/// 
/// FLOW:
///   1. On Start(), ShowPopup() is called automatically (if showOnStart = true).
///   2. Time.timeScale is set to 0 (if pauseWhileShown = true), freezing everything.
///   3. RaceCountdown's coroutine is waiting on WaitUntil(!_infoPopup.IsShowing),
///      so the countdown cannot begin until this popup is dismissed.
///   4. The player presses any key (or clicks the "OK" button) → DismissPopup().
///   5. Time.timeScale is restored to 1, and the countdown starts.
/// 
/// HOW TO SET UP:
///   1. Create a Canvas > Panel with your info text inside.
///   2. Add a Button ("Got it") and wire its OnClick to DismissPopup().
///   3. Drag the Panel into infoPanel.
///   4. Optionally drag a TextMeshProUGUI into infoText (if you want to set text in code).
///   5. Edit the message string in the Inspector.
/// </summary>
public class InfoPopup : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // The root panel GameObject to show/hide.
    [SerializeField] private GameObject infoPanel;
    // Optional text component — if assigned, its text is set to the message string below.
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Settings")]
    [Tooltip("If true, pauses the game while popup is shown.")]
    // Setting timeScale to 0 freezes physics and animations — nothing moves while the popup is up.
    [SerializeField] private bool pauseWhileShown = true;
    [Tooltip("If true, any key press dismisses the popup.")]
    // Lets players skip through the tutorial info quickly.
    [SerializeField] private bool dismissOnAnyKey = true;
    [Tooltip("Auto-show on level start.")]
    // Set to false if you want to trigger the popup manually from another script.
    [SerializeField] private bool showOnStart = true;

    [Header("Message")]
    [TextArea(3, 8)]
    // The text displayed in the popup — edit this in the Inspector per-level.
    [SerializeField] private string message = "WASD to drive\nSpace/Shift to drift\nComplete 3 laps to win!\n\nPress any key to start...";

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Tracks whether the popup is currently visible.
    private bool _isShowing;

    // ------------------------------------------------------------------ //
    //  Public read-only state (used by RaceCountdown to wait for dismissal)
    // ------------------------------------------------------------------ //

    /// <summary>True while the info popup is on screen. RaceCountdown polls this.</summary>
    public bool IsShowing => _isShowing;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        if (showOnStart)
            ShowPopup();
        else if (infoPanel != null)
            infoPanel.SetActive(false); // ensure panel is hidden if not auto-shown
    }

    private void Update()
    {
        // Only listen for key presses while the popup is visible.
        if (!_isShowing) return;

        // Any key on the keyboard dismisses the popup if that option is enabled.
        if (dismissOnAnyKey && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            DismissPopup();
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Shows the popup panel and optionally pauses the game.
    /// Can be called from other scripts to show the popup at any time.
    /// </summary>
    public void ShowPopup()
    {
        _isShowing = true;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        // Write the configured message into the text component if one is assigned.
        if (infoText != null)
            infoText.text = message;

        // Pause time so nothing moves while the player reads the popup.
        if (pauseWhileShown)
            Time.timeScale = 0f;
    }

    /// <summary>
    /// Hides the popup and restores game time.
    /// Also wired to the "OK" / "Got it" button's OnClick event in the Inspector.
    /// The guard prevents double-dismiss (e.g. key press AND button click on the same frame).
    /// </summary>
    public void DismissPopup()
    {
        // Guard: do nothing if already dismissed to prevent double timeScale restoration.
        if (!_isShowing) return;

        _isShowing = false;

        if (infoPanel != null)
            infoPanel.SetActive(false);

        // Restore normal game speed so the countdown can begin.
        if (pauseWhileShown)
            Time.timeScale = 1f;
    }
}
