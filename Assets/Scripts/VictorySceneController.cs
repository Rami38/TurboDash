using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the post-game victory screen shown after the player completes Level 3.
/// 
/// VISUAL DESIGN:
///   Multiple background layers scroll upward at different speeds (parallax depth).
///   Tiles that exit the top of the screen are recycled back to the bottom seamlessly,
///   creating an infinite upward scroll effect without needing a very tall background.
/// 
/// TEXT SEQUENCE:
///   Title and subtitle text fade in over time (using a coroutine) rather than appearing
///   instantly, creating a cinematic reveal. The subtitle then blinks to prompt the player
///   to press any key.
/// 
/// LAYER DATA STRUCT:
///   Each parallax layer stores a flat array of its children (individual tile Transforms),
///   the total Y span those tiles cover, and the threshold Y above which a tile is recycled.
///   This structure is calculated once in Start() so Update() is pure arithmetic.
/// 
/// HOW TO SET UP:
///   1. Create background layers as child GameObjects, each with tiled sprite children.
///   2. Drag the layer root Transforms into parallaxLayers.
///   3. Match layerSpeeds indices to parallaxLayers — slower speeds = further away.
///   4. Assign titleText and subtitleText TextMeshProUGUI components in the Inspector.
/// </summary>
public class VictorySceneController : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields
    // ------------------------------------------------------------------ //

    [Header("Parallax Layers (slowest → fastest)")]
    [Tooltip("Assign each ParallaxLayer root Transform here, ordered far-to-near.")]
    // These are the root GameObjects whose children are the tiled background sprites.
    [SerializeField] private Transform[] parallaxLayers;
    [Tooltip("Scroll speed for each layer — index matches parallaxLayers.")]
    // Layer 0 moves slowest (appears furthest away), last layer moves fastest (nearest).
    [SerializeField] private float[] layerSpeeds = { 1.5f, 3f, 5f };

    [Header("Text")]
    // The large victory title text (e.g. "CONGRATULATIONS!").
    [SerializeField] private TextMeshProUGUI titleText;
    // The smaller subtitle / prompt text (e.g. "Press any key to continue").
    [SerializeField] private TextMeshProUGUI subtitleText;
    [Tooltip("Seconds after the scene loads before the text starts fading in.")]
    [SerializeField] private float textFadeDelay = 1.5f;
    [Tooltip("Duration of the title text fade-in.")]
    [SerializeField] private float textFadeDuration = 2f;

    [Header("Navigation")]
    // Build index of the main menu — loaded when the player presses any key.
    [SerializeField] private int mainMenuSceneIndex = 0;
    [Tooltip("Seconds before the 'press any key' prompt appears.")]
    // Gives the player time to read and appreciate the victory screen before prompting them.
    [SerializeField] private float promptDelay = 4f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //

    // Set to true after the prompt appears — only then do we listen for key presses.
    private bool _canReturn;

    /// <summary>
    /// Holds all data needed to scroll and recycle one parallax layer.
    /// Computed once in Start() to avoid per-frame calculations.
    /// </summary>
    private struct LayerData
    {
        // The child Transforms (individual tile sprites) of this layer.
        public Transform[] tiles;
        // Total Y distance covered by all tiles (used for recycling math).
        public float totalHeight;
        // Threshold Y in world space — tiles above this are snapped back down.
        public float recycleY;
    }

    private LayerData[] _layerData;
    // Y position above which a tile is considered fully off-screen (camera top + margin).
    private float _exitY;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        // Calculate the Y level at which tiles exit the top of the visible area.
        // Adding a small margin (4 units) ensures the recycle pop is invisible.
        Camera cam = Camera.main;
        _exitY = cam != null ? cam.transform.position.y + cam.orthographicSize + 4f : 20f;

        // ---- Pre-compute per-layer tile data ----
        _layerData = new LayerData[parallaxLayers.Length];

        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            int count = parallaxLayers[i].childCount;
            var tiles = new Transform[count];
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            // Collect all child tile Transforms and measure the Y range they occupy.
            for (int t = 0; t < count; t++)
            {
                tiles[t] = parallaxLayers[i].GetChild(t);
                float wy = tiles[t].position.y;
                if (wy < minY) minY = wy;
                if (wy > maxY) maxY = wy;
            }

            // Estimate the total height the tiles span.
            // "spread" = distance between the bottommost and topmost tile centres.
            // "tileStep" = average gap between adjacent tiles.
            // totalHeight = spread + one tileStep so the last tile's bottom aligns with the next cycle's top.
            float spread      = (count > 1) ? (maxY - minY) : 0f;
            float tileStep    = (count > 1) ? spread / (count - 1) : 2f;
            float totalHeight = spread + tileStep;

            _layerData[i] = new LayerData
            {
                tiles       = tiles,
                totalHeight = totalHeight,
                recycleY    = _exitY,
            };
        }

        // ---- Initialize text — start invisible so we can fade in ----
        if (titleText   != null) SetAlpha(titleText,   0f);
        if (subtitleText != null) SetAlpha(subtitleText, 0f);

        StartCoroutine(RunSequence());
    }

    private void Update()
    {
        // ---- Scroll each layer upward and recycle tiles that exit the top ----
        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            float speed = i < layerSpeeds.Length ? layerSpeeds[i] : 1f;
            float delta = speed * Time.deltaTime;

            // Move the entire layer root upward — all children move with it.
            parallaxLayers[i].position += Vector3.up * delta;

            LayerData ld = _layerData[i];
            if (ld.tiles == null) continue;

            // Check each tile individually — if it's above the exit threshold, recycle it.
            foreach (Transform tile in ld.tiles)
            {
                if (tile == null) continue;
                if (tile.position.y > ld.recycleY)
                    // Jump the tile down by one full tile span to appear below the screen.
                    tile.position -= new Vector3(0f, ld.totalHeight, 0f);
            }
        }

        // ---- Accept input to return to the main menu ----
        if (!_canReturn) return;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            SceneManager.LoadScene(mainMenuSceneIndex);
    }

    // ------------------------------------------------------------------ //
    //  Text sequence coroutine
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Runs the timed text reveal sequence:
    ///   1. Waits textFadeDelay seconds.
    ///   2. Fades in the title text over textFadeDuration.
    ///   3. Fades in the subtitle at 60% of the title duration.
    ///   4. Waits promptDelay seconds then enables key input and starts the subtitle blink.
    /// </summary>
    private IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(textFadeDelay);

        yield return StartCoroutine(FadeIn(titleText, textFadeDuration));

        yield return new WaitForSeconds(0.4f); // brief pause between title and subtitle

        yield return StartCoroutine(FadeIn(subtitleText, textFadeDuration * 0.6f));

        yield return new WaitForSeconds(promptDelay);

        // Now accept any key to return to main menu.
        _canReturn = true;

        if (subtitleText != null)
            StartCoroutine(BlinkText(subtitleText));
    }

    // ------------------------------------------------------------------ //
    //  Text animation helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Smoothly increases the text alpha from 0 to 1 over the given duration.
    /// Uses unscaled Time.deltaTime so it works even if timeScale is 0.
    /// </summary>
    private IEnumerator FadeIn(TextMeshProUGUI text, float duration)
    {
        if (text == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(text, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        SetAlpha(text, 1f); // snap to fully visible at the end
    }

    /// <summary>
    /// Alternates the text alpha between 0.2 (dim) and 1.0 (full) at blinkInterval seconds.
    /// Loops indefinitely — stopped when the scene loads.
    /// </summary>
    private IEnumerator BlinkText(TextMeshProUGUI text)
    {
        const float blinkInterval = 0.6f;
        while (true)
        {
            SetAlpha(text, 0.2f);                         // dim
            yield return new WaitForSeconds(blinkInterval);
            SetAlpha(text, 1f);                           // bright
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    /// <summary>
    /// Sets the alpha (transparency) of a TextMeshProUGUI without touching its RGB values.
    /// Alpha 0 = invisible, Alpha 1 = fully opaque.
    /// </summary>
    private static void SetAlpha(TextMeshProUGUI text, float alpha)
    {
        Color c = text.color;
        c.a        = alpha;
        text.color = c;
    }
}
