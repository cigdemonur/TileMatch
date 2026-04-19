using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Models;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Owns the queue of OrderModels and the list of currently active orders.
    /// Exposes FindMatchingOrder for tile routing.
    /// Tells GameController when all orders are complete (Win).
    /// </summary>
    public class OrderController : MonoBehaviour
    {
        private readonly Queue<OrderModel> _queue = new Queue<OrderModel>();
        private readonly List<OrderModel> _activeOrders = new List<OrderModel>();

        private int _simultaneousOrders = 2;

        /// <summary>Currently visible/active orders (up to simultaneousOrders).</summary>
        public IReadOnlyList<OrderModel> ActiveOrders => _activeOrders;

        /// <summary>
        /// Build the queue from level data and fill the active orders slot.
        /// Called by GameController at start.
        /// </summary>
        public void Initialize(IReadOnlyList<OrderData> orders, int simultaneousOrders)
        {
            _simultaneousOrders = simultaneousOrders;
            _queue.Clear();
            _activeOrders.Clear();

            // TODO: wrap each OrderData in an OrderModel and enqueue
            // TODO: promote first N orders to _activeOrders (subscribe to OnCompleted)
        }

        /// <summary>
        /// Returns the first active order that can accept a tile of this type,
        /// or null if none match (tile should go to rack instead).
        /// </summary>
        public OrderModel FindMatchingOrder(TileTypeSO type)
        {
            // TODO: scan _activeOrders left-to-right, return first one where order.Matches(type)
            return null;
        }

        public void Reset()
        {
            // TODO: clear queue + activeOrders, unsubscribe listeners
            // Called by GameController.RestartLevel
        }

        private void OnOrderCompleted(OrderModel completed)
        {
            // TODO: remove from _activeOrders, unsubscribe
            // TODO: if _queue has more -> promote next to active
            // TODO: if queue empty AND _activeOrders empty -> GameController.SetState(Win)
        }
    }
}
