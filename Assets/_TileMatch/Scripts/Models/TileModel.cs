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

        /// <summary>Current depth-from-top. 0 = nothing covers this tile (tappable).</summary>
        public int Layer { get; private set; }

        /// <summary>Layer as authored in level data. Immutable; used to decide
        /// the static "is above" partial order when recomputing depths.</summary>
        public int OriginalLayer { get; }

        public bool IsBlocked { get; private set; }
        public event Action<bool> OnBlockedChanged;
        public event Action<int> OnLayerChanged;

        public TileModel(TileTypeSO tileType, Vector2Int dotPos, int layer)
        {
            TileType = tileType;
            DotPos = dotPos;
            Layer = layer;
            OriginalLayer = layer;
            IsBlocked = false;
        }

        public void SetBlocked(bool blocked)
        {
            if (IsBlocked == blocked) return;
            IsBlocked = blocked;
            OnBlockedChanged?.Invoke(blocked);
        }

        public void SetLayer(int newLayer)
        {
            if (Layer == newLayer) return;
            Layer = newLayer;
            OnLayerChanged?.Invoke(newLayer);
        }

        /// <summary>
        /// Position-overlap check (ignores layer — caller checks layer separately).
        /// Two 2x2 tiles overlap iff their anchors are within 1 cell on both axes,
        /// i.e. their footprints share at least one mini-square. Edge-touching
        /// neighbours (anchors 2 apart) do NOT overlap and do not block each other.
        /// </summary>
        public bool Overlaps(TileModel other)
        {
            int dx = Mathf.Abs(DotPos.x - other.DotPos.x);
            int dy = Mathf.Abs(DotPos.y - other.DotPos.y);
            return dx <= 1 && dy <= 1;
        }
    }
}
