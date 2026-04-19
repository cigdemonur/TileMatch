using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Spawns the tile layout defined by a LevelDataSO and keeps the board's
    /// blocking state up-to-date as tiles are removed.
    /// Does NOT own BoardModel — GameController injects it via Bind().
    /// </summary>
    public class BoardController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameConfigSO gameConfig;

        [Header("Scene Refs")]
        [Tooltip("Parent transform for all spawned TileView GameObjects.")]
        [SerializeField] private Transform tileParent;

        [Tooltip("Temporary direct prefab reference. Will be replaced by TilePool.")]
        [SerializeField] private GameObject tilePrefab;

        private BoardModel _board;

        /// <summary>Called by GameController once the BoardModel is created.</summary>
        public void Bind(BoardModel board)
        {
            if (_board != null)
                _board.OnTileRemoved -= HandleTileRemoved;

            _board = board;
            _board.OnTileRemoved += HandleTileRemoved;
        }

        /// <summary>
        /// Instantiate every tile defined in levelData, wrap each in a TileModel,
        /// add to the BoardModel, then run an initial blocking-state refresh.
        /// </summary>
        public void SpawnBoard(LevelDataSO levelData)
        {
            // TODO: for each TilePlacement in levelData.tiles:
            //   - compute world pos = new Vector3(dotX * miniSquareSize, dotY * miniSquareSize, -layer * 0.01f)
            //   - instantiate tilePrefab under tileParent (later: pool.Get())
            //   - create new TileModel(tileType, new Vector2Int(dotX, dotY), layer)
            //   - wire up TileView.Render(model) and TileController reference
            //   - _board.AddTile(model)
            // TODO: RefreshBlockedState() at the end
        }

        /// <summary>
        /// Remove every tile from the board and return its view to the pool.
        /// Used by GameController.RestartLevel.
        /// </summary>
        public void ClearBoard()
        {
            // TODO: _board.Clear(); -> OnTileRemoved will fire per tile; view must be returned to pool there
        }

        /// <summary>
        /// Recomputes IsBlocked for every tile currently on the board.
        /// A tile is blocked iff any higher-layer tile overlaps it.
        /// Runs after any tile removal (fire-and-forget: no coroutines, no awaits).
        /// </summary>
        public void RefreshBlockedState()
        {
            if (_board == null) return;

            // TODO: for each tile A in _board.Tiles:
            //         bool blocked = false;
            //         for each tile B in _board.Tiles where B.Layer > A.Layer:
            //             if B.Overlaps(A) { blocked = true; break; }
            //         A.SetBlocked(blocked);
        }

        private void HandleTileRemoved(TileModel tile)
        {
            RefreshBlockedState();
        }

        private void OnDestroy()
        {
            if (_board != null)
                _board.OnTileRemoved -= HandleTileRemoved;
        }
    }
}
