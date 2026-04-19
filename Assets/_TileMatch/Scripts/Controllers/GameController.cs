using System;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;

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
            // TODO: create models (Board, Rack) using gameConfig
            // TODO: hand models to subsystem controllers
            // TODO: call boardController.SpawnBoard(levelData)
            // TODO: call orderController.Initialize(levelData.orders, gameConfig.simultaneousOrders)
        }

        /// <summary>
        /// Route a tapped tile to the correct destination: order tray or rack.
        /// Called by TileController via InputController.
        /// </summary>
        public void RouteTile(TileModel tile)
        {
            // TODO: ask OrderController.FindMatchingOrder(tile.TileType)
            // TODO: if match -> matchedOrder.Collect(); animate tile to order tray
            // TODO: else -> Rack.TryAdd(tile); if false -> SetState(Fail)
            // TODO: Board.RemoveTile(tile) (fire-and-forget)
        }

        public void SetState(GameState newState)
        {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        public void RestartLevel()
        {
            // TODO: Board.Clear(), Rack.Clear(), OrderController.Reset()
            // TODO: boardController.SpawnBoard(levelData)
            // TODO: SetState(GameState.Playing)
        }
    }
}
