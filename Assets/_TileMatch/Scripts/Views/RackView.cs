using UnityEngine;
using UnityEngine.UI;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// Renders the 6-slot rack (bottom of the screen).
    /// Subscribes to RackModel.OnSlotFilled to light up the next slot whenever
    /// a non-matching tile is parked. Lives on the same World-Space Canvas as
    /// OrderView so tween targets stay in world coords.
    ///
    /// NOTE: the current design never removes items from the rack mid-run
    /// (rack-full == Fail), so this view only needs a "fill" path. On
    /// RestartLevel, Clear() wipes all slots.
    /// </summary>
    public class RackView : MonoBehaviour
    {
        [Header("Slot Refs")]
        [Tooltip("One slot Image per rack capacity (size == GameConfigSO.rackCapacity, typically 6).")]
        [SerializeField] private Image[] slotIcons;

        [Tooltip("Sprite shown on an empty slot.")]
        [SerializeField] private Sprite emptySlotSprite;

        private RackModel _rack;

        /// <summary>Called by GameController once the RackModel is created.</summary>
        public void Bind(RackModel rack)
        {
            if (_rack != null)
                _rack.OnSlotFilled -= HandleSlotFilled;

            _rack = rack;
            if (_rack == null) return;

            _rack.OnSlotFilled += HandleSlotFilled;

            // Initial render: clear every slot (a fresh RackModel is always empty)
            ResetVisuals();
        }

        /// <summary>World position of a given rack slot — used as tween target for parked tiles.</summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIcons == null || slotIndex < 0 || slotIndex >= slotIcons.Length)
                return transform.position;

            var slot = slotIcons[slotIndex];
            return slot != null ? slot.transform.position : transform.position;
        }

        /// <summary>
        /// Index of the next slot that will be filled (== current FilledCount).
        /// Useful for callers that want to aim an animation at a slot before
        /// the model has been mutated.
        /// </summary>
        public int GetNextSlotIndex()
        {
            return _rack != null ? _rack.FilledCount : 0;
        }

        private void HandleSlotFilled(int slotIndex, TileModel tile)
        {
            if (slotIcons == null || slotIndex < 0 || slotIndex >= slotIcons.Length) return;

            var slot = slotIcons[slotIndex];
            if (slot == null) return;

            if (tile != null && tile.TileType != null)
                slot.sprite = tile.TileType.icon;

            // TODO: small pop/punch-scale on slot when filled
        }

        /// <summary>Clear every slot — used on RestartLevel.</summary>
        public void Clear()
        {
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            if (slotIcons == null) return;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] != null) slotIcons[i].sprite = emptySlotSprite;
            }
        }

        private void OnDisable()
        {
            if (_rack != null)
                _rack.OnSlotFilled -= HandleSlotFilled;
        }
    }
}
