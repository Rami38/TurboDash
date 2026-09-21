using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the Level Select panel in the Main Menu UI.
/// 
/// HOW IT WORKS:
///   Each level is represented by a LevelButton struct containing:
///     • A UI Button           — clickable if the level is unlocked.
///     • A TextMeshProUGUI     — displays the level name/number.
///     • A locked overlay GO   — shown as a dark overlay when locked.
///     • A scene build index   — which scene to load when clicked.
/// 
///   RefreshButtons() is called in Awake and OnEnable so the button states
///   are always up-to-date when the panel opens (e.g. after completing a level
///   and returning to the menu).
/// 
///   Currently ALL levels are unlocked (button.interactable = true, overlay hidden).
///   To restore unlock-gating, replace the RefreshButtons body with a call to
///   LevelProgressManager.Instance.IsUnlocked(lb.sceneBuildIndex).
/// 
/// HOW TO SET UP:
///   1. Add this script to the Level Select panel root GameObject.
///   2. Fill in the levelButtons array: one entry per level.
///   3. Wire each button's OnClick to this script's LoadLevel(sceneBuildIndex).
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Data structure
    // ------------------------------------------------------------------ //

    [System.Serializable]
    public struct LevelButton
    {
        [Tooltip("The UI Button for this level.")]
        public Button button;

        [Tooltip("Text shown inside the button (e.g. 'Level 1').")]
        public TextMeshProUGUI label;

        [Tooltip("Dark overlay shown when the level is locked.")]
        public GameObject lockedOverlay;

        [Tooltip("Build index of the scene to load.")]
        public int sceneBuildIndex;
    }

    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // One entry per level — fill in the Inspector.
    [SerializeField] private LevelButton[] levelButtons;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        // Refresh immediately so buttons are correct the instant the panel is visible.
        RefreshButtons();
    }

    private void OnEnable()
    {
        // Re-check on every open in case progress changed while the menu was hidden
        // (e.g. the player beat a level, came back to the menu, and opened Level Select).
        RefreshButtons();
    }

    // ------------------------------------------------------------------ //
    //  Public API (wired to button OnClick events in the Inspector)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Loads the level scene with the given build index.
    /// Also plays a button click sound and stops the menu music.
    /// Wire this to each level button's OnClick in the Inspector.
    /// </summary>
    public void LoadLevel(int sceneBuildIndex)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
            // Stop music so it doesn't overlap with the race music in the next scene.
            AudioManager.Instance.StopMusic();
        }

        // LoadSceneAsync loads in the background, showing the current frame while loading.
        SceneManager.LoadSceneAsync(sceneBuildIndex);
    }

    // ------------------------------------------------------------------ //
    //  Internal helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Updates each button's interactable state and locked overlay visibility
    /// to reflect the current unlock status from LevelProgressManager.
    /// Currently all levels are always available (interactable = true).
    /// </summary>
    private void RefreshButtons()
    {
        foreach (LevelButton lb in levelButtons)
        {
            if (lb.button == null) continue;

            // All levels are unlocked — every button is interactable.
            lb.button.interactable = true;

            // Hide the locked overlay since all levels are accessible.
            if (lb.lockedOverlay != null)
                lb.lockedOverlay.SetActive(false);
        }
    }
}
