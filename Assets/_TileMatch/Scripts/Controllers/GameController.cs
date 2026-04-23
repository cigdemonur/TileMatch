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
            if (gameView != null) gameView.Bind(this);

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

            // 1) Tap feedback on the view.
            var tileView = boardView != null ? boardView.GetViewFor(tile) : null;
            if (tileView != null) tileView.AnimateTap();

            // 2) Try to match an active order first.
            var match = orderController != null
                ? orderController.FindMatchingOrder(tile.TileType)
                : null;

            if (match != null)
            {
                Vector3 target = orderView != null
                    ? orderView.GetNextIconWorldPosition(match)
                    : (tileView != null ? tileView.transform.position : Vector3.zero);

                if (tileView != null)
                {
                    tileView.AnimateMoveToTarget(target, () =>
                    {
                        match.Collect();
                        if (Board != null) Board.RemoveTile(tile);
                    });
                }
                else
                {
                    match.Collect();
                    if (Board != null) Board.RemoveTile(tile);
                }
                return;
            }

            // 3) No match — send to rack (triggers Fail if rack is full).
            int rackSlot = rackController != null ? rackController.PeekNextFreeSlot() : -1;
            Vector3 rackTarget = (rackSlot >= 0 && rackView != null)
                ? rackView.GetSlotWorldPosition(rackSlot)
                : (tileView != null ? tileView.transform.position : Vector3.zero);

            if (tileView != null)
            {
                tileView.AnimateMoveToTarget(rackTarget, () =>
                {
                    if (rackController != null) rackController.TryAdd(tile);
                    if (Board != null) Board.RemoveTile(tile);
                });
            }
            else
            {
                if (rackController != null) rackController.TryAdd(tile);
                if (Board != null) Board.RemoveTile(tile);
            }
        }

        /// <summary>
        /// Scan the rack for tiles that match a newly-active order and
        /// auto-collect them. Called when OrderController promotes an order.
        /// </summary>
        private void HandleOrderActivated(OrderModel order)
        {
            if (order == null || Rack == null || rackController == null) return;

            // Snapshot — TryRemove mutates the slot array.
            for (int i = 0; i < Rack.Capacity && !order.IsComplete; i++)
            {
                var slotTile = Rack.Slots[i];
                if (slotTile == null) continue;
                if (!order.Matches(slotTile.TileType)) continue;

                if (rackController.TryRemove(slotTile, out _))
                {
                    order.Collect();
                }
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
            if (boardController != null) boardController.ClearBoard();
            if (rackController != null) rackController.Reset();
            if (orderController != null) orderController.Reset();

            SetState(GameState.Playing);

            if (boardController != null) boardController.SpawnBoard(levelData);
            if (orderController != null)
                orderController.Initialize(levelData.orders, gameConfig.simultaneousOrders);
        }
    }
}
