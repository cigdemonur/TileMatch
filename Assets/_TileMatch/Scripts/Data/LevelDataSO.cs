using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.Data
{
    /// <summary>
    /// One complete level: tile placements on the dot map + the ordered
    /// list of orders to fulfill. Created as an asset and fed to the
    /// GameController at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_New", menuName = "TileMatch/Level")]
    public class LevelDataSO : ScriptableObject
    {
        [Tooltip("All tiles placed on the board at level start.")]
        public List<TilePlacement> tiles = new List<TilePlacement>();

        [Tooltip("Orders to complete, in the sequence they appear in the queue.")]
        public List<OrderData> orders = new List<OrderData>();
    }
}
