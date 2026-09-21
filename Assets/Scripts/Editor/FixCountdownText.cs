using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// <summary>
/// One-shot Editor utility: Tools > Fix Countdown Text
/// 
/// WHAT IT DOES:
///   Opens Level 1 and Level 2 scenes (one at a time), looks for a RaceCountdown component,
///   and if its countdownText field is null it either:
///     A) finds an existing "CountdownText" GameObject and wires it up, or
///     B) creates a new TextMeshProUGUI child inside the /HUD canvas and wires that up.
///   Each scene is then saved so the change persists.
/// 
/// WHEN TO USE:
///   Run this once after adding the RaceCountdown component to a scene where the
///   CountdownText UI doesn't exist yet. After it runs successfully, this script
///   can be deleted from the project (it has no runtime function).
/// 
/// HOW TO RUN:
///   Unity menu bar → Tools → Fix Countdown Text
/// </summary>
public static class FixCountdownText
{
    [MenuItem("Tools/Fix Countdown Text")]
    public static void Run()
    {
        ProcessScene("Assets/Scenes/Level 1.unity");
        ProcessScene("Assets/Scenes/Level 2.unity");
        Debug.Log("[FixCountdownText] Done. You can now delete Assets/Scripts/Editor/FixCountdownText.cs.");
    }

    /// <summary>
    /// Opens a scene, finds the RaceCountdown component, and ensures countdownText is wired.
    /// If the CountdownText GameObject doesn't exist, creates it inside the /HUD canvas.
    /// Saves the scene if any changes were made.
    /// </summary>
    private static void ProcessScene(string scenePath)
    {
        // Open the scene in single mode (replaces the currently open scene).
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Find the RaceCountdown in the newly opened scene.
        var countdown = Object.FindFirstObjectByType<RaceCountdown>();
        if (countdown == null)
        {
            Debug.LogError($"[FixCountdownText] No RaceCountdown found in {scenePath}. Skipping.");
            return;
        }

        // Use SerializedObject to safely access and modify the private countdownText field.
        var so       = new SerializedObject(countdown);
        var textProp = so.FindProperty("countdownText");

        // If already wired, nothing to do.
        if (textProp.objectReferenceValue != null)
        {
            Debug.Log($"[FixCountdownText] {scenePath} already has countdownText wired — skipping.");
            return;
        }

        // ---- Find or create the CountdownText UI object ----
        var existing = GameObject.Find("CountdownText");
        TextMeshProUGUI tmp;

        if (existing != null)
        {
            // Reuse an existing GameObject that matches the name.
            tmp = existing.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            // No CountdownText exists — create one inside the HUD canvas.
            var hud = GameObject.Find("HUD");
            if (hud == null)
            {
                Debug.LogError($"[FixCountdownText] No GameObject named 'HUD' in {scenePath}. Cannot create CountdownText.");
                return;
            }

            // Create the new GameObject and parent it to HUD.
            var go = new GameObject("CountdownText");
            go.SetActive(false); // hidden by default — RaceCountdown shows it at the right time
            go.transform.SetParent(hud.transform, false);

            // Center the text on screen with a large enough rect for big numbers.
            var rt              = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f); // centred anchor
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 50f);    // slightly above centre
            rt.sizeDelta        = new Vector2(300f, 200f);

            // Configure the TextMeshPro component for large, bold countdown numbers.
            tmp               = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = "3";
            tmp.fontSize      = 100;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = Color.white;
            tmp.raycastTarget = false; // countdown text doesn't need to block input
        }

        // ---- Wire the reference and save ----
        textProp.objectReferenceValue = tmp;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FixCountdownText] ✓ CountdownText added and wired in {scenePath}");
    }
}
