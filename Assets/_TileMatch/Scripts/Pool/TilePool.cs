using System.Collections.Generic;
using UnityEngine;
using TileMatch.Views;

namespace TileMatch.Pool
{
    /// <summary>
    /// Simple stack-based object pool for TileView instances.
    /// BoardController.SpawnBoard() calls Get() for every placement; BoardView
    /// calls Return() when BoardModel.OnTileRemoved fires.
    ///
    /// Why pool: a medium level can spawn 80-150 tiles, and Restart should be
    /// instant. Destroy/Instantiate on a low-end device is the worst offender
    /// for GC spikes in a tile-match game.
    /// </summary>
    public class TilePool : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("TileView prefab — must have TileController + Collider2D on the root.")]
        [SerializeField] private TileView tilePrefab;

        [Tooltip("Parent transform for pooled (inactive) tiles. Defaults to this transform.")]
        [SerializeField] private Transform poolParent;

        [Header("Prewarm")]
        [Tooltip("Instantiate this many tiles on Awake to avoid first-spawn hitch.")]
        [SerializeField] private int prewarmCount = 32;

        private readonly Stack<TileView> _free = new Stack<TileView>();

        private void Awake()
        {
            if (poolParent == null) poolParent = transform;

            for (int i = 0; i < prewarmCount; i++)
                _free.Push(CreateNew());
        }

        /// <summary>Get a TileView from the pool (or instantiate if empty). Caller is responsible for binding + positioning.</summary>
        public TileView Get(Transform activeParent)
        {
            TileView view = _free.Count > 0 ? _free.Pop() : CreateNew();

            if (activeParent != null) view.transform.SetParent(activeParent, worldPositionStays: false);
            view.gameObject.SetActive(true);
            return view;
        }

        /// <summary>Return a TileView to the pool. Safe to call with null.</summary>
        public void Return(TileView view)
        {
            if (view == null) return;

            view.gameObject.SetActive(false);
            view.transform.SetParent(poolParent, worldPositionStays: false);
            _free.Push(view);
        }

        private TileView CreateNew()
        {
            var view = Instantiate(tilePrefab, poolParent);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
