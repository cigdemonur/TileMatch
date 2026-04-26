using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;
using TileMatch.Pool;
using TileMatch.Views;

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
        [Tooltip("Parent transform for all spawned tile GameObjects.")]
        [SerializeField] private Transform tileParent;

        [Tooltip("Pool that owns the tile prefab and recycles TileView instances.")]
        [SerializeField] private TilePool tilePool;

        [Tooltip("BoardView that maps models to views.")]
        [SerializeField] private BoardView boardView;

        private BoardModel _board;

        /// <summary>Called by GameController once the BoardModel is created.</summary>
        public void Bind(BoardModel board)
        {
            if (_board != null)
                _board.OnTileRemoved -= HandleTileRemoved;

            _board = board;
            if (_board != null)
                _board.OnTileRemoved += HandleTileRemoved;
        }

        /// <summary>
        /// Instantiate every tile defined in levelData, wrap each in a TileModel,
        /// add to the BoardModel, then run an initial blocking-state refresh.
        /// </summary>
        public void SpawnBoard(LevelDataSO levelData)
        {
            if (_board == null || levelData == null || gameConfig == null || tilePool == null)
            {
                Debug.LogError("BoardController.SpawnBoard: missing dependencies.", this);
                return;
            }

            float s = gameConfig.miniSquareSize;

            foreach (var placement in levelData.tiles)
            {
                if (placement.tileType == null) continue;

                // 1. create model
                var model = new TileModel(
                    placement.tileType,
                    new Vector2Int(placement.dotX, placement.dotY),
                    placement.layer
                );

                // 2. get a TileView from the pool, position it
                var view = tilePool.Get(tileParent);
                view.transform.localPosition = DotToWorld(placement.dotX, placement.dotY, placement.layer, s);

                // 3. bind controller (which binds view)
                var controller = view.GetComponent<TileController>();
                if (controller == null)
                {
                    Debug.LogError("Tile prefab is missing TileController.", view);
                    continue;
                }
                controller.Bind(model);

                // 4. register model + view + add to board (fires OnTileAdded if subscribed later)
                if (boardView != null) boardView.RegisterView(model, view);
                _board.AddTile(model);
            }

            RecomputeLayers();
            RefreshBlockedState();
        }

        /// <summary>
        /// Re-derive every tile's Layer as "depth from the top of its stack."
        /// Top-of-stack tiles (nothing covering them) become layer 0, so all
        /// currently-tappable tiles share the same layer value. Uses each
        /// tile's immutable OriginalLayer to determine the static "is above"
        /// partial order so repeated calls remain consistent.
        /// </summary>
        public void RecomputeLayers()
        {
            if (_board == null) return;
            var tiles = _board.Tiles;

            // Process from highest OriginalLayer (top) to lowest (bottom) so
            // each tile can read the already-assigned depth of any tile above it.
            var sorted = new List<TileModel>(tiles);
            sorted.Sort((a, b) => b.OriginalLayer.CompareTo(a.OriginalLayer));

            var newDepth = new Dictionary<TileModel, int>(sorted.Count);
            foreach (var t in sorted)
            {
                int max = -1;
                foreach (var kv in newDepth)
                {
                    var s = kv.Key;
                    if (s.OriginalLayer > t.OriginalLayer && s.Overlaps(t) && kv.Value > max)
                        max = kv.Value;
                }
                newDepth[t] = max + 1;
            }

            float s2 = gameConfig.miniSquareSize;
            foreach (var kv in newDepth)
            {
                var t = kv.Key;
                if (t.Layer == kv.Value) continue;
                t.SetLayer(kv.Value);

                if (boardView != null)
                {
                    var view = boardView.GetViewFor(t);
                    if (view != null)
                        view.transform.localPosition = DotToWorld(t.DotPos.x, t.DotPos.y, kv.Value, s2);
                }
            }
        }

        /// <summary>
        /// Remove every tile from the board. BoardModel.Clear fires OnTileRemoved
        /// per tile, and BoardView returns each view to the pool.
        /// </summary>
        public void ClearBoard()
        {
            if (_board == null) return;
            _board.Clear();
        }

        /// <summary>
        /// Recomputes IsBlocked for every tile currently on the board.
        /// A tile is blocked iff any higher-layer tile overlaps it.
        /// Runs after any tile removal (fire-and-forget: no coroutines, no awaits).
        /// </summary>
        public void RefreshBlockedState()
        {
            if (_board == null) return;
            // Layer is depth-from-top after RecomputeLayers, so anything with
            // a non-zero depth has at least one overlapping tile above it.
            foreach (var t in _board.Tiles)
                t.SetBlocked(t.Layer > 0);
        }

        private Vector3 DotToWorld(int dotX, int dotY, int layer, float s)
        {
            // X and Y spacing can differ if the tile sprite isn't square.
            float sx = s * gameConfig.columnSpacingScale;
            float sy = s;

            // Centre board around origin so the camera doesn't need to offset.
            float offsetX = (gameConfig.dotMapWidth - 1) * 0.5f * sx;
            float offsetY = (gameConfig.dotMapHeight - 1) * 0.5f * sy;

            // Depth rules (smaller Z = closer to camera = rendered on top).
            // Layer is depth-from-top: 0 means the tile is on top of its stack.
            // So smaller Layer → smaller Z → renders on top. Within a layer,
            // lower dotY pulls closer so front-row tiles cover the 3D shadow
            // on the top edge of the tiles behind them (painter's algorithm).
            // Layer step (0.1) is much larger than the per-row step (0.001) so
            // layers always dominate dot-Y sorting.
            float z = layer * 0.1f + dotY * 0.001f;
            // Pull tile visually down (or up) so the anchor dot sits at the sprite's
            // visual center — gizmo dots are NOT shifted, only the tile render.
            return new Vector3(dotX * sx - offsetX,
                               dotY * sy - offsetY + gameConfig.tileYOffset,
                               z);
        }

        private void HandleTileRemoved(TileModel tile)
        {
            RecomputeLayers();
            RefreshBlockedState();
        }

        private void OnDestroy()
        {
            if (_board != null)
                _board.OnTileRemoved -= HandleTileRemoved;
        }
    }
}
