using UnityEditor;
using UnityEngine;

// Menu: Tools > Bake EdgeCollider2D Transforms
// Finds every EdgeCollider2D in the current scene whose parent transform has
// non-zero position or non-unit scale and bakes those values directly into the
// collider points, then zeros the transform so world-space collision is unchanged.
public static class BakeEdgeColliders
{
    [MenuItem("Tools/Bake EdgeCollider2D Transforms")]
    static void Bake()
    {
        var colliders = Object.FindObjectsByType<EdgeCollider2D>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var col in colliders)
        {
            var t = col.transform;

            bool hasOffset = t.localPosition != Vector3.zero;
            bool hasScale  = t.localScale != Vector3.one;

            if (!hasOffset && !hasScale) continue;

            Undo.RecordObject(col,       "Bake EdgeCollider2D Points");
            Undo.RecordObject(t.gameObject, "Bake EdgeCollider2D Transform");

            Vector2[] pts = col.points;
            for (int i = 0; i < pts.Length; i++)
            {
                // Transform point from local to world (no rotation assumed)
                pts[i] = new Vector2(
                    t.localPosition.x + pts[i].x * t.localScale.x,
                    t.localPosition.y + pts[i].y * t.localScale.y);
            }
            col.points = pts;

            t.localPosition = Vector3.zero;
            t.localScale    = Vector3.one;

            EditorUtility.SetDirty(col);
            EditorUtility.SetDirty(t.gameObject);
            count++;
        }

        if (count == 0)
            Debug.Log("BakeEdgeColliders: no colliders with non-identity transforms found.");
        else
            Debug.Log($"BakeEdgeColliders: baked {count} EdgeCollider2D(s). Save the scene to persist.");
    }
}
