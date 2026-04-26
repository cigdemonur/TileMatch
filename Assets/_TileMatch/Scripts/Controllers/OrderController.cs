using System;
using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;
using TileMatch.Views;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Owns the queue of OrderModels and the list of currently active orders.
    /// Exposes FindMatchingOrder for tile routing.
    /// Tells GameController when all orders are complete (Win).
    /// </summary>
    public class OrderController : MonoBehaviour
    {
        [SerializeField] private OrderView orderView;

        private readonly Queue<OrderModel> _queue = new Queue<OrderModel>();
        private readonly List<OrderModel> _activeOrders = new List<OrderModel>();

        private int _simultaneousOrders = 2;

        /// <summary>Currently visible/active orders (up to simultaneousOrders).</summary>
        public IReadOnlyList<OrderModel> ActiveOrders => _activeOrders;

        /// <summary>
        /// Fires right after an order becomes active (Initialize or promotion).
        /// GameController subscribes to auto-collect matching tiles from the rack.
        /// </summary>
        public event Action<OrderModel> OnOrderActivated;

        /// <summary>
        /// Build the queue from level data and fill the active orders slot.
        /// Called by GameController at start.
        /// </summary>
        public void Initialize(IReadOnlyList<OrderData> orders, int simultaneousOrders)
        {
            Reset();
            _simultaneousOrders = Mathf.Max(1, simultaneousOrders);

            if (orders != null)
            {
                foreach (var data in orders)
                {
                    if (data == null || data.tileType == null) continue;
                    _queue.Enqueue(new OrderModel(data.tileType));
                }
            }

            // Promote up to _simultaneousOrders to the active list.
            for (int i = 0; i < _simultaneousOrders; i++)
            {
                if (_queue.Count == 0) break;
                var next = _queue.Dequeue();
                _activeOrders.Add(next);
                next.OnCompleted += () => OnOrderCompleted(next);
                if (orderView != null) orderView.ShowOrder(i, next);
                OnOrderActivated?.Invoke(next);
            }
        }

        /// <summary>
        /// Returns the first active order that can accept a tile of this type,
        /// or null if none match (tile should go to rack instead).
        /// </summary>
        public OrderModel FindMatchingOrder(TileTypeSO type)
        {
            if (type == null) return null;
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                if (_activeOrders[i].Matches(type)) return _activeOrders[i];
            }
            return null;
        }

        public void Reset()
        {
            _queue.Clear();
            _activeOrders.Clear();
            if (orderView != null) orderView.Clear();
        }

        private void OnOrderCompleted(OrderModel completed)
        {
            int slotIndex = orderView != null ? orderView.GetSlotIndexFor(completed) : -1;
            _activeOrders.Remove(completed);

            // Promote the next queued order into the freed slot (if any).
            OrderModel next = null;
            if (_queue.Count > 0 && slotIndex >= 0)
            {
                next = _queue.Dequeue();
                _activeOrders.Add(next);
                next.OnCompleted += () => OnOrderCompleted(next);
            }

            if (slotIndex >= 0 && orderView != null)
                orderView.AnimateCompleteAndShow(slotIndex, next);

            if (next != null)
                OnOrderActivated?.Invoke(next);

            // Win check.
            if (_queue.Count == 0 && _activeOrders.Count == 0)
            {
                if (GameController.Instance != null)
                    GameController.Instance.SetState(GameState.Win);
            }
        }
    }
}
