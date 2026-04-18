using System;
using UnityEngine;
using TileMatch.Data;

namespace TileMatch.Models
{
    /// <summary>
    /// Runtime state of a single tile on the board.
    /// Pure C# — no MonoBehaviour, no Unity GameObject.
    /// Views subscribe to OnBlockedChanged to render visual changes.
    /// </summary>
    public class TileModel
    {
        public TileTypeSO TileType { get; }
        public Vector2Int DotPos { get; }   // center position on the dot map
        public int Layer { get; }

        public bool IsBlocked { get; private set; }
        public event Action<bool> OnBlockedChanged;

        public TileModel(TileTypeSO tileType, Vector2Int dotPos, int layer)
        {
            TileType = tileType;
            DotPos = dotPos;
            Layer = layer;
            IsBlocked = false;
        }

        public void SetBlocked(bool blocked)
        {
            if (IsBlocked == blocked) return;
            IsBlocked = blocked;
            OnBlockedChanged?.Invoke(blocked);
        }

        /// <summary>
        /// Position-overlap check (ignores layer — caller checks layer separately).
        /// Two tiles overlap if their 2x2 footprints intersect or share an edge,
        /// but NOT if they only share a single corner point.
        /// Rule: dx <= 2 AND dy <= 2 AND NOT (dx == 2 AND dy == 2)
        /// </summary>
        public bool Overlaps(TileModel other)
        {
            int dx = Mathf.Abs(DotPos.x - other.DotPos.x);
            int dy = Mathf.Abs(DotPos.y - other.DotPos.y);
            return dx <= 2 && dy <= 2 && !(dx == 2 && dy == 2);
        }
    }
}
