using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the Main Menu: shows/hides the main panel and the level-select panel,
/// and handles Play, Level Select, Back, and Quit button callbacks.
/// 
/// PANELS:
///   mainPanel       — the root panel containing the Play, Level Select, and Quit buttons.
///   levelSelectPanel — the LevelSelectMenu panel. Shown when "Level Select" is pressed.
/// 
/// HOW TO SET UP:
///   1. Add this script to a persistent GameObject in the Main Menu scene.
///   2. Create two panels (mainPanel, levelSelectPanel) and drag them in.
///   3. Wire buttons:
///        Play button        → PlayGame()
///        Level Select button → OpenLevelSelect()
///        Back button (in level select panel) → BackToMain()
///        Quit button        → QuitGame()
/// </summary>
public class MainMenu : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Panels")]
    [Tooltip("Root panel of the main menu buttons (Play, Level Select, Quit).")]
    [SerializeField] private GameObject mainPanel;
    [Tooltip("Root panel of the level select screen.")]
    [SerializeField] private GameObject levelSelectPanel;

    // ------------------------------------------------------------------ //
    //  Constants
    // ------------------------------------------------------------------ //

    // Build index of Level 1 — used by the quick-play button.
    private const int Level1SceneIndex = 1;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        // Start the menu music when the scene loads.
        // AudioManager.Instance may be null if the developer jumps directly to the
        // Main Menu scene in the Editor — the null check handles that safely.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();

        // Always open on the main panel regardless of which panel was active last session.
        ShowMainPanel();
    }

    // ------------------------------------------------------------------ //
    //  Button callbacks — wire these in the Inspector
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Quick-start button: skips the level select panel and loads Level 1 directly.
    /// </summary>
    public void PlayGame()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
            AudioManager.Instance.StopMusic(); // stop menu music before loading the race
        }
        SceneManager.LoadSceneAsync(Level1SceneIndex);
    }

    /// <summary>
    /// Switches from the main panel to the level select panel.
    /// </summary>
    public void OpenLevelSelect()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (mainPanel != null)        mainPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }

    /// <summary>
    /// Returns from the level select panel to the main panel.
    /// </summary>
    public void BackToMain()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        ShowMainPanel();
    }

    /// <summary>
    /// Quits the application.
    /// In the Editor, stops Play Mode instead of actually quitting.
    /// </summary>
    public void QuitGame()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

#if UNITY_EDITOR
        // In the Editor, Application.Quit() does nothing — use this instead.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Shows the main button panel and hides the level select panel.
    /// Centralises this logic so it can be called from both Start and BackToMain.
    /// </summary>
    private void ShowMainPanel()
    {
        if (mainPanel != null)        mainPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
    }
}
