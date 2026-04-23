using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// Renders the N-slot rack (bottom of the screen).
    /// Delegates per-slot rendering to RackSlotView components — this class
    /// only wires model events to the right child slot.
    ///
    /// Subscribes to:
    ///   - RackModel.OnSlotFilled  -> slots[i].Fill(tileType)
    ///   - RackModel.OnSlotCleared -> slots[i].ClearSlot() (auto-collect path)
    /// </summary>
    public class RackView : MonoBehaviour
    {
        [Header("Slot Refs")]
        [Tooltip("One RackSlotView per rack capacity (size == GameConfigSO.rackCapacity, typically 6).")]
        [SerializeField] private RackSlotView[] slots;

        private RackModel _rack;

        /// <summary>Called by GameController once the RackModel is created.</summary>
        public void Bind(RackModel rack)
        {
            Unbind();

            _rack = rack;
            if (_rack == null) return;

            _rack.OnSlotFilled += HandleSlotFilled;
            _rack.OnSlotCleared += HandleSlotCleared;

            // Initial render — a fresh RackModel is empty.
            ClearAllVisuals();
        }

        private void Unbind()
        {
            if (_rack == null) return;
            _rack.OnSlotFilled -= HandleSlotFilled;
            _rack.OnSlotCleared -= HandleSlotCleared;
            _rack = null;
        }

        /// <summary>World position of a given rack slot's icon — used as tween origin/target.</summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return transform.position;

            var slot = slots[slotIndex];
            return slot != null ? slot.GetIconWorldPosition() : transform.position;
        }

        /// <summary>
        /// Index of the next slot that will be filled (== current FilledCount).
        /// Aim tile-flies-to-rack tweens at this before calling RackModel.TryAdd.
        /// </summary>
        public int GetNextSlotIndex()
        {
            return _rack != null ? _rack.FilledCount : 0;
        }

        private void HandleSlotFilled(int slotIndex, TileModel tile)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slot = slots[slotIndex];
            if (slot == null || tile == null) return;

            slot.Fill(tile.TileType);
            // TODO: small pop/punch-scale on slot when filled
        }

        private void HandleSlotCleared(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slot = slots[slotIndex];
            if (slot == null) return;

            slot.ClearSlot();
            // TODO: fade-out / poof when auto-collected
        }

        /// <summary>Clear every slot — used on RestartLevel.</summary>
        public void Clear()
        {
            ClearAllVisuals();
        }

        private void ClearAllVisuals()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].ClearSlot();
            }
        }

        private void OnDisable() => Unbind();
    }
}
