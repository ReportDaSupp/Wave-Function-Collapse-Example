#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class HexGridGizmoWithLabels : MonoBehaviour {
    [Header("Grid Settings")]
    public int gridWidth = 10;   // Range for q from -gridWidth to gridWidth
    public int gridHeight = 10;  // Range for r determined by gridHeight (see below)
    public float hexSize = 6.3f;   // The "radius" of the hex tile

    [Header("Gizmo Settings")]
    public bool drawGizmos = true; // Enable/disable drawing in the editor

    public bool enableText = true;
    public Color gizmoColor = Color.yellow;
    public Color labelColor = Color.white;
    
    

    /// <summary>
    /// Converts axial coordinates (q, r) to world space for a flat-topped hex grid.
    /// </summary>
    Vector3 HexToWorldPosition(int q, int r) {
        // For a flat-topped hex grid, the conversion is:
        // x = hexSize * 1.5 * q
        // z = hexSize * √3 * (r + q/2)
        float x = hexSize * 1.5f * q;
        float z = hexSize * Mathf.Sqrt(3f) * (r + q / 2f);
        return new Vector3(x, 0, z) + transform.position;
    }

    /// <summary>
    /// Returns the six corner points for a hex centered at the given position.
    /// </summary>
    Vector3[] GetHexCorners(Vector3 center) {
        Vector3[] corners = new Vector3[6];
        // For flat-topped hexes, the corners are at angles:
        // 0°, 60°, 120°, 180°, 240°, 300°
        for (int i = 0; i < 6; i++) {
            float angle_deg = 60 * i;
            float angle_rad = Mathf.Deg2Rad * angle_deg;
            corners[i] = new Vector3(
                center.x + hexSize * Mathf.Cos(angle_rad),
                center.y,
                center.z + hexSize * Mathf.Sin(angle_rad)
            );
        }
        return corners;
    }

    /// <summary>
    /// Draw the hex grid and coordinate labels as gizmos in the Scene view.
    /// </summary>
    private void OnDrawGizmos() {
        if (!drawGizmos)
            return;

        Gizmos.color = gizmoColor;

        // Loop over axial coordinates for the grid.
        for (int q = -gridWidth; q <= gridWidth; q++) {
            int r1 = Mathf.Max(-gridHeight, -q - gridHeight);
            int r2 = Mathf.Min(gridHeight, -q + gridHeight);
            for (int r = r1; r <= r2; r++) {
                Vector3 center = HexToWorldPosition(q, r);
                Vector3[] corners = GetHexCorners(center);

                // Draw the hex outline.
                for (int i = 0; i < corners.Length; i++) {
                    Vector3 currentCorner = corners[i];
                    Vector3 nextCorner = corners[(i + 1) % corners.Length];
                    Gizmos.DrawLine(currentCorner, nextCorner);
                }

                // Draw a label at the center of the hex showing its (q, r) coordinate.
                #if UNITY_EDITOR
                if (enableText)
                {
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = labelColor;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                // Adjust the size if needed.
                Handles.Label(center, $"({q}, {r})", labelStyle);
                }
                #endif
            }
        }
    }
}