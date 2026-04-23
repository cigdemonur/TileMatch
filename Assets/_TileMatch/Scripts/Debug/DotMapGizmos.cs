using UnityEngine;
using TileMatch.Data;

namespace TileMatch.Debugging
{
    /// <summary>
    /// DEBUG ONLY — draws every dot in the dot map as a Gizmo sphere so you can
    /// verify tile positions match what you expect.
    ///
    /// Uses the same centering math as BoardController.DotToWorld, so origin
    /// (dot 0,0) appears at the bottom-left of the map and the map is centred
    /// around world origin (0,0,0).
    ///
    /// Usage:
    ///   1. Attach to any empty GameObject in the scene (position doesn't matter).
    ///   2. Drag your GameConfigSO into the Game Config field.
    ///   3. Toggle Gizmos in Scene view to show/hide.
    ///   4. Delete this script and GameObject once dots look correct.
    /// </summary>
    public class DotMapGizmos : MonoBehaviour
    {
        [SerializeField] private GameConfigSO gameConfig;

        [Header("Appearance")]
        [SerializeField] private Color dotColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private float dotRadius = 0.05f;

        [Header("Highlights")]
        [Tooltip("Tint the (0,0) dot differently so you can find the origin.")]
        [SerializeField] private Color originColor = Color.red;
        [Tooltip("Tint every 5th dot (grid reference lines).")]
        [SerializeField] private Color majorStepColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        [SerializeField] private int majorStep = 5;

        [Header("Board bounds outline")]
        [SerializeField] private bool drawBounds = true;
        [SerializeField] private Color boundsColor = new Color(1f, 1f, 1f, 0.4f);

        private void OnDrawGizmos()
        {
            if (gameConfig == null) return;

            float s = gameConfig.miniSquareSize;
            int w = gameConfig.dotMapWidth;
            int h = gameConfig.dotMapHeight;

            // Match BoardController.DotToWorld exactly: X uses columnSpacingScale.
            float sx = s * gameConfig.columnSpacingScale;
            float sy = s;
            float offsetX = (w - 1) * 0.5f * sx;
            float offsetY = (h - 1) * 0.5f * sy;

            Vector3 origin = transform.position;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector3 pos = origin + new Vector3(x * sx - offsetX, y * sy - offsetY, 0f);

                    if (x == 0 && y == 0) Gizmos.color = originColor;
                    else if (majorStep > 0 && x % majorStep == 0 && y % majorStep == 0) Gizmos.color = majorStepColor;
                    else Gizmos.color = dotColor;

                    Gizmos.DrawSphere(pos, dotRadius);
                }
            }

            if (drawBounds)
            {
                Gizmos.color = boundsColor;
                // Board extends one mini-square beyond the first/last dot on each side
                // because a tile is 2x2 and centered on the dot.
                float halfW = offsetX + sx;
                float halfH = offsetY + sy;
                Vector3 size = new Vector3(halfW * 2f, halfH * 2f, 0f);
                Gizmos.DrawWireCube(origin, size);
            }
        }
    }
}
