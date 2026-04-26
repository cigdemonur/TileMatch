using System;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;
using TileMatch.Views;

namespace TileMatch.Controllers
{
    public enum GameState
    {
        Playing,
        Win,
        Fail
    }

    /// <summary>
    /// Central game orchestrator. Owns runtime models, drives the state machine,
    /// and routes taps to the correct subsystem.
    /// Singleton — one instance per scene.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private GameConfigSO gameConfig;
        [SerializeField] private LevelDataSO levelData;
        [Tooltip("Ordered list of levels. 'Next Level' picks the entry whose levelNumber == current+1.")]
        [SerializeField] private LevelDataSO[] levels;

        [Header("Subsystem Controllers")]
        [SerializeField] private BoardController boardController;
        [SerializeField] private OrderController orderController;
        [SerializeField] private RackController rackController;

        [Header("Views")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private RackView rackView;
        [SerializeField] private OrderView orderView;
        [SerializeField] private GameView gameView;

        // Runtime models (created on start)
        public BoardModel Board { get; private set; }
        public RackModel Rack { get; private set; }

        public GameState State { get; private set; } = GameState.Playing;
        public event Action<GameState> OnStateChanged;

        // Tiles already launched at an order but not yet arrived. Used to space
        // rapid same-type taps onto consecutive icon slots.
        private readonly System.Collections.Generic.Dictionary<OrderModel, int> _inFlightByOrder
            = new System.Collections.Generic.Dictionary<OrderModel, int>();

        // Tracks rack tiles already mid-flight to an order so the snapshot scan
        // and the OnSlotFilled handler don't both dispatch the same tile.
        private readonly System.Collections.Generic.HashSet<TileModel> _rackTilesFlyingToOrder
            = new System.Collections.Generic.HashSet<TileModel>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (gameConfig == null || levelData == null)
            {
                Debug.LogError("GameController: missing GameConfig or LevelData.", this);
                return;
            }

            // 1. Build runtime models
            Board = new BoardModel();
            Rack = new RackModel(gameConfig.rackCapacity);
            Rack.OnSlotFilled += HandleRackSlotFilled;

            // 2. Wire controllers
            if (boardController != null) boardController.Bind(Board);
            if (rackController != null) rackController.Bind(Rack);

            // Subscribe BEFORE Initialize so the first batch of activated
            // orders can still auto-collect from the (initially empty) rack.
            if (orderController != null)
            {
                orderController.OnOrderActivated -= HandleOrderActivated;
                orderController.OnOrderActivated += HandleOrderActivated;
                orderController.Initialize(levelData.orders, gameConfig.simultaneousOrders);
            }

            // 3. Wire views
            if (boardView != null) boardView.Bind(Board);
            if (rackView != null) rackView.Bind(Rack);
            if (gameView != null)
            {
                gameView.Bind(this);
                gameView.SetLevelNumber(levelData.levelNumber);
            }

            // 4. Spawn the board
            if (boardController != null) boardController.SpawnBoard(levelData);

            // 5. We are playing
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Route a tapped tile to the correct destination: order tray or rack.
        /// Called by TileController via InputController.
        /// </summary>
        public void RouteTile(TileModel tile)
        {
            if (tile == null || State != GameState.Playing) return;

            var tileView = boardView != null ? boardView.GetViewFor(tile) : null;

            // A second tap on a tile that's already mid-flight would otherwise
            // restart the tween or double-reserve an order icon — ignore.
            if (tileView != null && tileView.IsFlying) return;

            // 1) Try to match an active order first.
            var match = orderController != null
                ? orderController.FindMatchingOrder(tile.TileType)
                : null;

            if (match != null)
            {
                _inFlightByOrder.TryGetValue(match, out int inFlight);
                int iconIndex = match.CollectedCount + inFlight;
                _inFlightByOrder[match] = inFlight + 1;

                Vector3 target = orderView != null
                    ? orderView.GetIconWorldPositionAt(match, iconIndex)
                    : (tileView != null ? tileView.transform.position : Vector3.zero);

                if (tileView != null)
                {
                    tileView.PlayTapAndFly(target, TileDestination.Order, () =>
                    {
                        DecrementInFlight(match);
                        ResolveOrderArrival(tile, match);
                        if (Board != null) Board.RemoveTile(tile);
                    });
                }
                else
                {
                    DecrementInFlight(match);
                    ResolveOrderArrival(tile, match);
                    if (Board != null) Board.RemoveTile(tile);
                }
                return;
            }

            // 2) No match — send to rack. Reserve a slot up-front so two rapid
            //    rack-bound flights don't both target the same index.
            int rackSlot = rackController != null ? rackController.ReserveNextFreeSlot() : -1;
            if (rackSlot < 0)
            {
                // Rack visibly full — fail immediately.
                SetState(GameState.Fail);
                return;
            }

            Vector3 rackTarget = rackView != null
                ? rackView.GetSlotWorldPosition(rackSlot)
                : (tileView != null ? tileView.transform.position : Vector3.zero);

            if (tileView != null)
            {
                tileView.PlayTapAndFly(rackTarget, TileDestination.Rack, () =>
                {
                    if (rackController != null) rackController.CommitReserved(rackSlot, tile);
                    if (Board != null) Board.RemoveTile(tile);
                });
            }
            else
            {
                if (rackController != null) rackController.CommitReserved(rackSlot, tile);
                if (Board != null) Board.RemoveTile(tile);
            }
        }

        /// <summary>
        /// The tile was launched at <paramref name="originalMatch"/>, but the order
        /// may have completed mid-flight (e.g. 4 same-type taps for a 3-tile order).
        /// Re-check at arrival: collect if still accepted; else hand off to another
        /// active order; else fall back to the rack so the tile isn't lost.
        /// </summary>
        private void DecrementInFlight(OrderModel order)
        {
            if (order == null) return;
            if (!_inFlightByOrder.TryGetValue(order, out int n)) return;
            n--;
            if (n <= 0) _inFlightByOrder.Remove(order);
            else _inFlightByOrder[order] = n;
        }

        private void ResolveOrderArrival(TileModel tile, OrderModel originalMatch)
        {
            if (tile == null) return;

            if (originalMatch != null && originalMatch.Matches(tile.TileType))
            {
                originalMatch.Collect();
                return;
            }

            var alt = orderController != null
                ? orderController.FindMatchingOrder(tile.TileType)
                : null;
            if (alt != null)
            {
                alt.Collect();
                return;
            }

            if (rackController != null) rackController.TryAdd(tile);
        }

        /// <summary>
        /// Scan the rack for tiles that match a newly-active order and
        /// auto-collect them. Called when OrderController promotes an order.
        /// Timings live on the views: OrderView owns the grow-in delay,
        /// RackView owns the rack→order flight duration.
        /// </summary>
        private void HandleOrderActivated(OrderModel order)
        {
            if (order == null || Rack == null || rackController == null) return;
            float delay = orderView != null ? orderView.RackFlightStartDelay : 0f;
            StartCoroutine(FlyRackTilesAfterDelay(order, delay));
        }

        private System.Collections.IEnumerator FlyRackTilesAfterDelay(OrderModel order, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (order == null || order.IsComplete) yield break;

            // Snapshot AFTER the delay so any rack adds during the slot's grow-in
            // animation are picked up too.
            for (int i = 0; i < Rack.Capacity; i++)
            {
                if (order.IsComplete) yield break;

                var slotTile = Rack.Slots[i];
                if (slotTile == null) continue;
                if (!order.Matches(slotTile.TileType)) continue;

                StartRackToOrderFlight(i, slotTile, order);
            }
        }

        /// <summary>
        /// Catches the race where an order activates while a tile is mid-flight
        /// to the rack: the snapshot scan misses it, but OnSlotFilled fires once
        /// it lands and we re-check active orders.
        /// </summary>
        private void HandleRackSlotFilled(int slotIndex, TileModel tile)
        {
            if (tile == null || State != GameState.Playing) return;
            var order = orderController != null
                ? orderController.FindMatchingOrder(tile.TileType)
                : null;
            if (order == null) return;
            StartRackToOrderFlight(slotIndex, tile, order);
        }

        /// <summary>
        /// Reserve an order icon slot, fly the rack icon there, and on arrival
        /// remove it from the rack and collect it. No-ops if the tile is already
        /// flying or the order is full.
        /// </summary>
        private void StartRackToOrderFlight(int slotIndex, TileModel tile, OrderModel order)
        {
            if (tile == null || order == null || order.IsComplete) return;
            if (_rackTilesFlyingToOrder.Contains(tile)) return;

            _inFlightByOrder.TryGetValue(order, out int inFlight);
            int iconIndex = order.CollectedCount + inFlight;
            if (iconIndex >= OrderData.RequiredCount) return;
            _inFlightByOrder[order] = inFlight + 1;
            _rackTilesFlyingToOrder.Add(tile);

            Vector3 target = orderView != null
                ? orderView.GetIconWorldPositionAt(order, iconIndex)
                : Vector3.zero;

            if (rackView == null)
            {
                DecrementInFlight(order);
                _rackTilesFlyingToOrder.Remove(tile);
                if (rackController != null) rackController.TryRemove(tile, out _);
                order.Collect();
            }
            else
            {
                rackView.AnimateSlotIconTo(slotIndex, target, rackView.RackToOrderDuration, () =>
                {
                    DecrementInFlight(order);
                    _rackTilesFlyingToOrder.Remove(tile);
                    if (rackController != null) rackController.TryRemove(tile, out _);
                    order.Collect();
                });
            }
        }

        public void SetState(GameState newState)
        {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        public void RestartLevel()
        {
            LoadLevel(levelData);
        }

        /// <summary>The level currently in play. Null only before Start.</summary>
        public LevelDataSO CurrentLevel => levelData;

        /// <summary>Returns the level whose levelNumber equals current+1, or null if none.</summary>
        public LevelDataSO PeekNextLevel()
        {
            if (levelData == null || levels == null) return null;
            int target = levelData.levelNumber + 1;
            foreach (var l in levels)
                if (l != null && l.levelNumber == target) return l;
            return null;
        }

        public void LoadNextLevel()
        {
            var next = PeekNextLevel();
            if (next != null) LoadLevel(next);
        }

        private void LoadLevel(LevelDataSO data)
        {
            if (data == null) return;
            levelData = data;

            _inFlightByOrder.Clear();
            _rackTilesFlyingToOrder.Clear();

            if (boardController != null) boardController.ClearBoard();
            if (rackController != null) rackController.Reset();
            if (orderController != null) orderController.Reset();

            SetState(GameState.Playing);

            if (boardController != null) boardController.SpawnBoard(levelData);
            if (orderController != null)
                orderController.Initialize(levelData.orders, gameConfig.simultaneousOrders);
            if (gameView != null) gameView.SetLevelNumber(levelData.levelNumber);
        }
    }
}
