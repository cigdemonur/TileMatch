using System.Collections.Generic;
using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// Renders the active orders tray (top of the screen).
    /// Shows up to GameConfigSO.simultaneousOrders orders at once (typically 2).
    /// Each slot displays the order's tile icon and a 3-step progress indicator.
    ///
    /// Lives on a World-Space Canvas child so it shares the same coordinate
    /// space as the board — animations from tile -> order slot don't need a
    /// world->UI conversion.
    ///
    /// OrderController calls ShowOrder(model, slotIndex) when an order becomes
    /// active and ClearSlot(slotIndex) when it completes.
    /// </summary>
    public class OrderView : MonoBehaviour
    {
        [Header("Slot Refs")]
        [Tooltip("One OrderSlotView per simultaneous-order slot (size == GameConfigSO.simultaneousOrders).")]
        [SerializeField] private OrderSlotView[] slots;

        [Header("Style")]
        [Tooltip("Alpha for uncollected icons across every slot. Applied on bind.")]
        [Range(0f, 1f)]
        [SerializeField] private float fadedAlpha = 0.4f;

        // Which OrderModel currently occupies each slot (null when empty).
        private readonly Dictionary<int, OrderModel> _modelsBySlot = new Dictionary<int, OrderModel>();

        /// <summary>
        /// Bind an OrderModel to a slot and begin rendering its progress.
        /// Called by OrderController when an order becomes active.
        /// </summary>
        public void ShowOrder(int slotIndex, OrderModel order)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

            // Clean up any previous model wired to this slot
            ClearSlot(slotIndex);

            _modelsBySlot[slotIndex] = order;
            var slot = slots[slotIndex];
            if (slot != null)
            {
                slot.SetFadedAlpha(fadedAlpha);
                slot.Bind(order);
            }
        }

        /// <summary>Empty a slot (order completed or level restarted).</summary>
        public void ClearSlot(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

            if (_modelsBySlot.TryGetValue(slotIndex, out var existing) && existing != null)
            {
                _modelsBySlot.Remove(slotIndex);
            }

            var slot = slots[slotIndex];
            if (slot != null) slot.Unbind();
        }

        /// <summary>World position of a given slot — used as the tween target for matched tiles.</summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return transform.position;
            var slot = slots[slotIndex];
            return slot != null ? slot.transform.position : transform.position;
        }

        /// <summary>
        /// World position of the NEXT empty icon for a given order — the tween target
        /// a matched tile should fly to. Caller passes the model BEFORE Collect() is
        /// called so CollectedCount still points at the next-to-fill index.
        /// </summary>
        public Vector3 GetNextIconWorldPosition(OrderModel order)
        {
            int slotIndex = GetSlotIndexFor(order);
            if (slotIndex < 0) return transform.position;
            var slot = slots[slotIndex];
            return slot != null
                ? slot.GetIconWorldPosition(order.CollectedCount)
                : transform.position;
        }

        /// <summary>Find which slot currently hosts this order (-1 if none).</summary>
        public int GetSlotIndexFor(OrderModel order)
        {
            foreach (var kv in _modelsBySlot)
                if (kv.Value == order) return kv.Key;
            return -1;
        }

        /// <summary>Clear every slot — used on RestartLevel.</summary>
        public void Clear()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++) ClearSlot(i);
        }
    }
}
