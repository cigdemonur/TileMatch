using System;
using System.Collections.Generic;

namespace TileMatch.Models
{
    /// <summary>
    /// Runtime collection of every tile currently on the board.
    /// Pure C# — no MonoBehaviour. Views and Controllers subscribe to events
    /// to react when tiles are added or removed.
    /// </summary>
    public class BoardModel
    {
        private readonly List<TileModel> _tiles = new List<TileModel>();

        public IReadOnlyList<TileModel> Tiles => _tiles;
        public int Count => _tiles.Count;
        public bool IsEmpty => _tiles.Count == 0;

        public event Action<TileModel> OnTileAdded;
        public event Action<TileModel> OnTileRemoved;

        public void AddTile(TileModel tile)
        {
            _tiles.Add(tile);
            OnTileAdded?.Invoke(tile);
        }

        public void RemoveTile(TileModel tile)
        {
            if (!_tiles.Remove(tile)) return;
            OnTileRemoved?.Invoke(tile);
        }

        public void Clear()
        {
            for (int i = _tiles.Count - 1; i >= 0; i--)
            {
                var tile = _tiles[i];
                _tiles.RemoveAt(i);
                OnTileRemoved?.Invoke(tile);
            }
        }
    }
}
