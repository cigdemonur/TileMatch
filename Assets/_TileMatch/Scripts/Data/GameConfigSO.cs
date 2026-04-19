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

        [Header("Rack")]
        [Tooltip("How many tiles the rack can hold before Fail is triggered.")]
        public int rackCapacity = 6;
    }
}
