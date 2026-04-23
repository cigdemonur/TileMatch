using System.Collections.Generic;
using UnityEngine;
using TileMatch.Models;
using TileMatch.Pool;

namespace TileMatch.Views
{
    /// <summary>
    /// Maps TileModels to their TileView GameObjects and handles view
    /// lifecycle in response to BoardModel events.
    /// BoardController spawns the view and registers it here; BoardView
    /// reacts to OnTileRemoved to play exit animations and return to pool.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("TilePool to return views to when their model is removed.")]
        [SerializeField] private TilePool tilePool;

        private BoardModel _board;
        private readonly Dictionary<TileModel, TileView> _views = new Dictionary<TileModel, TileView>();

        /// <summary>Called by GameController after BoardModel is created.</summary>
        public void Bind(BoardModel board)
        {
            if (_board != null)
            {
                _board.OnTileRemoved -= HandleTileRemoved;
            }
            _board = board;
            _board.OnTileRemoved += HandleTileRemoved;
        }

        /// <summary>
        /// Called by BoardController right after spawning a tile so the view
        /// can be looked up later (e.g. to animate it toward the order tray).
        /// </summary>
        public void RegisterView(TileModel model, TileView view)
        {
            _views[model] = view;
        }

        /// <summary>Lookup the TileView associated with a given TileModel.</summary>
        public TileView GetViewFor(TileModel model)
        {
            _views.TryGetValue(model, out var view);
            return view;
        }

        private void HandleTileRemoved(TileModel tile)
        {
            if (!_views.TryGetValue(tile, out var view)) return;
            _views.Remove(tile);

            // GameController plays tap/move animations *before* calling Board.RemoveTile(),
            // so by the time we get here the view is visually done. Just pool it.
            ReturnToPool(view);
        }

        private void OnDisable()
        {
            if (_board != null)
                _board.OnTileRemoved -= HandleTileRemoved;
        }

        /// <summary>Clear everything — used on RestartLevel.</summary>
        public void Clear()
        {
            foreach (var kv in _views)
                ReturnToPool(kv.Value);
            _views.Clear();
        }

        private void ReturnToPool(TileView view)
        {
            if (view == null) return;

            if (tilePool != null)
            {
                tilePool.Return(view);
            }
            else
            {
                // Pool not wired in the scene — fall back to deactivation so we don't
                // leak stale tiles on top of the board. Not ideal; assign tilePool.
                view.gameObject.SetActive(false);
            }
        }
    }
}
