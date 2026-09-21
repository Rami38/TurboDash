using UnityEngine;

/// <summary>
/// Container that holds an ordered list of waypoints for AI opponents to follow.
/// 
/// HOW IT WORKS:
///   Add child GameObjects under this GameObject — each child becomes one waypoint.
///   The order in the hierarchy is the order the AI drives through them.
///   OpponentAI calls GetWaypoints() in Awake() to retrieve the array and stores it locally.
/// 
/// GIZMO:
///   In the Scene view, yellow lines and spheres are drawn between all waypoints so you
///   can verify the route without entering Play Mode. The path loops (last → first).
/// 
/// HOW TO SET UP:
///   1. Create an empty GameObject named "WaypointPath" and add this script.
///   2. Create child GameObjects (empty Transforms) at each turn/waypoint of the road.
///   3. Assign this WaypointPath to the OpponentAI's "Waypoint Path" field in the Inspector.
/// </summary>
public class WaypointPath : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns an array of all child Transforms in hierarchy order.
    /// Called by OpponentAI.Awake() to populate its internal waypoints array.
    /// The returned array is a snapshot — later hierarchy changes won't affect it.
    /// </summary>
    public Transform[] GetWaypoints()
    {
        var wps = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            wps[i] = transform.GetChild(i);
        return wps;
    }

    // ------------------------------------------------------------------ //
    //  Editor gizmo
    // ------------------------------------------------------------------ //

    private void OnDrawGizmos()
    {
        // Need at least two waypoints to draw a path.
        if (transform.childCount < 2) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform cur = transform.GetChild(i);
            // Wrap around using modulo so the last waypoint connects back to the first.
            Transform nxt = transform.GetChild((i + 1) % transform.childCount);

            // Draw a small sphere at each waypoint and a line to the next one.
            Gizmos.DrawWireSphere(cur.position, 0.25f);
            Gizmos.DrawLine(cur.position, nxt.position);
        }
    }
}
