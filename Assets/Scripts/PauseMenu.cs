using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds and controls an in-game pause menu entirely in code.
/// No prefab or scene YAML wiring required — everything is created at runtime in Start().
/// 
/// DESIGN CHOICE — Pure code UI:
///   Scene-wired UI breaks silently when GameObjects are renamed or restructured.
///   By building the pause overlay in code we guarantee it always appears correctly
///   regardless of how the scene hierarchy changes.
/// 
/// WHAT IT CREATES:
///   • A small "II" pause button anchored to the top-right of the best Canvas.
///   • A full-screen dimming overlay with a neon-pink bordered popup containing
///     RESUME, RESTART, and MAIN MENU buttons.
/// 
/// PAUSE GUARD:
///   Pausing is only allowed while the race is running and not yet finished.
///   The CanPause property checks both RaceManager and Level3RaceManager
///   so this script works in all three race levels without modification.
/// 
/// INPUT:
///   Escape key also toggles pause, in addition to the "II" button.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    // Used in Levels 1 and 2 (lap-based race).
    [SerializeField] private RaceManager raceManager;
    // Used in Level 3 (boss race — different manager class).
    [SerializeField] private Level3RaceManager level3RaceManager;
    // Legacy scene-wired panel kept so old YAML references don't cause missing errors;
    // it is immediately hidden in Start() and the code-built overlay is used instead.
    [SerializeField] private GameObject pausePanel;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    private bool _isPaused;
    // The "II" button shown in the top-right corner during the race.
    private GameObject _pauseButtonGO;
    // The full-screen overlay shown when paused.
    private GameObject _overlayGO;
    // The canvas we attach our UI to (highest sorting order canvas in the scene).
    private Canvas _targetCanvas;

    // ------------------------------------------------------------------ //
    //  Visual style constants
    // ------------------------------------------------------------------ //

    // Neon pink — used for borders, text, and button highlights.
    private static readonly Color NeonPink = new Color(1f, 0.11f, 0.71f, 1f);
    // Very dark purple panel background.
    private static readonly Color PanelBg  = new Color(0.04f, 0.01f, 0.10f, 0.97f);
    // Slightly lighter dark purple for button backgrounds.
    private static readonly Color ButtonBg = new Color(0.07f, 0.02f, 0.16f, 1f);

    // ------------------------------------------------------------------ //
    //  Pause guard property
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns true only when the race has started AND has not yet finished.
    /// Prevents pausing during the countdown or after the win/lose screen appears.
    /// </summary>
    private bool CanPause
    {
        get
        {
            if (level3RaceManager != null)
                return level3RaceManager.RaceStarted && !level3RaceManager.RaceFinished;
            if (raceManager != null)
                return raceManager.RaceStarted && !raceManager.RaceFinished;
            return true; // fallback: always allow if no manager is assigned
        }
    }

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        // Hide the old scene-wired panel so it doesn't render on top of our code-built one.
        if (pausePanel != null) pausePanel.SetActive(false);

        _isPaused = false;

        // Guarantee an EventSystem exists — required for UI button clicks to register.
        EnsureEventSystem();

        // Find the topmost canvas to attach our UI to.
        _targetCanvas = FindBestCanvas();

        // Build the pause overlay and button programmatically.
        BuildPauseOverlay();
        BuildPauseButton();
    }

    private void Update()
    {
        // Show/hide the pause button based on whether pausing is currently allowed.
        if (_pauseButtonGO != null)
            _pauseButtonGO.SetActive(CanPause && !_isPaused);

        if (Keyboard.current == null) return;

        // Escape key toggles pause.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused) Resume();
            else if (CanPause) Pause();
        }
    }

    // ------------------------------------------------------------------ //
    //  Public API (called by button OnClick listeners wired in code)
    // ------------------------------------------------------------------ //

    /// <summary>Pauses the game and shows the pause overlay.</summary>
    public void Pause()
    {
        if (!CanPause) return;
        _isPaused = true;
        Time.timeScale = 0f;           // freeze everything
        if (_overlayGO    != null) _overlayGO.SetActive(true);
        if (_pauseButtonGO != null) _pauseButtonGO.SetActive(false);
    }

    /// <summary>Resumes the game and hides the pause overlay.</summary>
    public void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;           // restore normal time
        if (_overlayGO != null) _overlayGO.SetActive(false);
    }

    /// <summary>Reloads the current scene (retry from the start of the level).</summary>
    public void Restart()
    {
        Time.timeScale = 1f;           // always restore before a scene load
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Exits to the main menu (scene index 0).</summary>
    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    // ------------------------------------------------------------------ //
    //  UI construction — Pause overlay
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Builds the full-screen dark overlay containing a centered neon popup with
    /// RESUME, RESTART, and MAIN MENU buttons.
    /// The overlay is hidden (SetActive false) until Pause() is called.
    /// </summary>
    private void BuildPauseOverlay()
    {
        if (_targetCanvas == null) return;

        // ---- Full-screen dim overlay ----
        // This fills the entire canvas and blocks clicks from reaching the game world.
        _overlayGO = new GameObject("PauseOverlay_Auto");
        _overlayGO.transform.SetParent(_targetCanvas.transform, false);

        var oRect = _overlayGO.AddComponent<RectTransform>();
        oRect.anchorMin       = Vector2.zero;   // stretch to fill the whole canvas
        oRect.anchorMax       = Vector2.one;
        oRect.sizeDelta       = Vector2.zero;
        oRect.anchoredPosition = Vector2.zero;

        var oImg = _overlayGO.AddComponent<Image>();
        oImg.color         = new Color(0f, 0f, 0f, 0.72f); // semi-transparent black
        oImg.raycastTarget = true;                           // blocks clicks through overlay

        // ---- Neon border (outer shell) ----
        var borderGO = MakeRect("PauseBorder", _overlayGO.transform, new Vector2(400f, 350f), Vector2.zero);
        borderGO.AddComponent<Image>().color = NeonPink;

        // ---- Dark panel (inner, inset 4px to expose the neon border) ----
        var panelGO = MakeRect("PausePanel", borderGO.transform, new Vector2(392f, 342f), Vector2.zero);
        panelGO.AddComponent<Image>().color = PanelBg;

        // ---- "PAUSED" title ----
        var titleGO   = MakeRect("PausedTitle", panelGO.transform, new Vector2(0f, 70f), Vector2.zero);
        var titleRect  = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);   // pin to top of panel
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "PAUSED";
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontSize  = 52f;
        titleTmp.color     = NeonPink;
        titleTmp.fontStyle = FontStyles.Bold;

        // ---- Three action buttons stacked vertically ----
        float startY = -115f; // Y offset from panel centre for the first button
        AddMenuButton(panelGO.transform, "RESUME",    startY,         Resume);
        AddMenuButton(panelGO.transform, "RESTART",   startY - 75f,   Restart);
        AddMenuButton(panelGO.transform, "MAIN MENU", startY - 150f,  QuitToMenu);

        // Begin hidden — shown only when Pause() is called.
        _overlayGO.SetActive(false);
    }

    /// <summary>
    /// Creates a single neon-styled button and adds it to the pause panel.
    /// Each button is a bordered outer rect containing a darker inner rect
    /// with the action label on top. The Button component is on the outer rect
    /// with the inner image as its target graphic for hover/press tinting.
    /// </summary>
    private void AddMenuButton(Transform parent, string label, float yOffset, UnityEngine.Events.UnityAction action)
    {
        // ---- Outer border rect ----
        var btnGO = MakeRect(label + "_Btn", parent, new Vector2(280f, 55f), new Vector2(0f, yOffset));
        var bRect = btnGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 1f); // anchor to top-centre of parent
        bRect.anchorMax = new Vector2(0.5f, 1f);
        bRect.pivot     = new Vector2(0.5f, 0.5f);
        btnGO.AddComponent<Image>().color = NeonPink; // neon border colour

        // ---- Inner dark fill ----
        var innerGO  = MakeRect("Inner", btnGO.transform, new Vector2(276f, 51f), Vector2.zero);
        var innerImg = innerGO.AddComponent<Image>();
        innerImg.color = ButtonBg;

        // ---- Button component ----
        // Targets the inner image so hover/pressed tint applies to the dark fill,
        // not the neon border (which should stay pink at all times).
        var btn    = btnGO.AddComponent<Button>();
        btn.targetGraphic = innerImg;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.35f, 0.9f, 1f);  // light purple on hover
        colors.pressedColor     = new Color(1f, 0.11f, 0.71f, 0.5f); // dimmed neon on click
        btn.colors = colors;
        btn.onClick.AddListener(action);

        // ---- Button label ----
        var lblGO    = MakeRect("Label", innerGO.transform, Vector2.zero, Vector2.zero);
        var lblRect  = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero; // fill the inner rect
        var tmp      = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 22f;
        tmp.color     = NeonPink;
        tmp.fontStyle = FontStyles.Bold;
    }

    // ------------------------------------------------------------------ //
    //  UI construction — Pause button
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Builds the small "II" pause button anchored to the top-right corner
    /// of the HUD canvas. Visible only when the race is in progress.
    /// </summary>
    private void BuildPauseButton()
    {
        if (_targetCanvas == null) return;

        _pauseButtonGO = new GameObject("PauseButton_Auto");
        _pauseButtonGO.transform.SetParent(_targetCanvas.transform, false);

        // ---- Anchor to top-right corner ----
        var rect = _pauseButtonGO.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);   // top-right anchor
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 1f);   // pivot at top-right corner
        rect.anchoredPosition = new Vector2(-20f, -20f); // inset from the edge
        rect.sizeDelta        = new Vector2(60f, 32f);

        // ---- Background image ----
        var bg   = _pauseButtonGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.03f, 0.2f, 0.85f); // semi-transparent dark purple

        // ---- Button component ----
        var btn    = _pauseButtonGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.1f, 0.5f, 1f); // lighter purple on hover
        colors.pressedColor     = NeonPink;                          // flash pink on click
        btn.colors = colors;
        btn.onClick.AddListener(Pause);

        // ---- "II" label (the universal pause symbol) ----
        var lblGO   = new GameObject("Label");
        lblGO.transform.SetParent(_pauseButtonGO.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero;
        var tmp     = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "II";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 18f;
        tmp.color     = NeonPink;
        // Remove the default TMP shadow/outline so the button stays clean.
        tmp.fontSharedMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontSharedMaterial.DisableKeyword("UNDERLAY_ON");
        tmp.fontSharedMaterial.DisableKeyword("OUTLINE_ON");

        // Hidden until the race starts.
        _pauseButtonGO.SetActive(false);
    }

    // ------------------------------------------------------------------ //
    //  Shared UI helper
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Creates a new GameObject with a RectTransform centred at the given anchoredPosition
    /// relative to its parent. All pause UI elements are created with this helper.
    /// </summary>
    private static GameObject MakeRect(string name, Transform parent, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f); // centred anchor
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = sizeDelta;
        rect.anchoredPosition = anchoredPos;
        return go;
    }

    // ------------------------------------------------------------------ //
    //  Scene helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Finds the Canvas with the highest sorting order in the scene — this is
    /// typically the topmost HUD canvas, which is where our pause UI should live
    /// so it draws on top of all game elements.
    /// Also ensures a GraphicRaycaster is present so button clicks register.
    /// </summary>
    private Canvas FindBestCanvas()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (canvases == null || canvases.Length == 0) return null;

        Canvas best = canvases[0];
        foreach (var c in canvases)
            if (c.sortingOrder >= best.sortingOrder)
                best = c;

        // GraphicRaycaster is required for Unity UI buttons to receive pointer events.
        if (best.GetComponent<GraphicRaycaster>() == null)
            best.gameObject.AddComponent<GraphicRaycaster>();

        return best;
    }

    /// <summary>
    /// Makes sure an EventSystem exists in the scene.
    /// Without one, no UI button will ever register a click.
    /// Also swaps the legacy StandaloneInputModule for InputSystemUIInputModule
    /// if the new Input System is active but the old module is still on the EventSystem.
    /// </summary>
    private static void EnsureEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>();

        if (es == null)
        {
            // No EventSystem exists — create one with the correct input module.
            var esGO = new GameObject("EventSystem_Auto");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
            return;
        }

        // If only the old StandaloneInputModule is present, replace it with the new one.
        if (es.GetComponent<StandaloneInputModule>() != null &&
            es.GetComponent<InputSystemUIInputModule>() == null)
        {
            Object.Destroy(es.GetComponent<StandaloneInputModule>());
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
