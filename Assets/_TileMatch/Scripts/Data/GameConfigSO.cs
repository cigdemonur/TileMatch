using UnityEngine;

namespace TileMatch.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "TileMatch/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Orders")]
        [Tooltip("How many orders are shown/active at once. Drops to 1 when only 1 order remains in the queue.")]
        public int simultaneousOrders = 2;

        [Header("Grid")]
        [Tooltip("Width of the dot map (number of possible tile-center columns).")]
        public int dotMapWidth = 11;

        [Tooltip("Height of the dot map (number of possible tile-center rows).")]
        public int dotMapHeight = 15;

        [Tooltip("World-space size of one mini-square. A tile occupies 2x2 mini-squares.")]
        public float miniSquareSize = 0.5f;

        [Tooltip("Horizontal spacing multiplier applied on top of miniSquareSize. Use <1 to tighten columns when the tile sprite is taller than wide (e.g. has a 3D shadow at the bottom).")]
        [Range(0.5f, 1.5f)]
        public float columnSpacingScale = 1f;

        [Tooltip("Vertical world-space offset applied to every tile after placement. Use a NEGATIVE value to pull tiles down so the anchor dot lands at the tile's visual center (compensates for the 3D shadow at the bottom of the sprite). Gizmo dots are NOT shifted — only tiles.")]
        [Range(-1f, 1f)]
        public float tileYOffset = 0f;

        [Header("Rack")]
        [Tooltip("How many tiles the rack can hold before Fail is triggered.")]
        public int rackCapacity = 6;
    }
}
