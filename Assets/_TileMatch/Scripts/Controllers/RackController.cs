using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Thin wrapper around RackModel. Exposes TryAdd to GameController
    /// and handles the "rack full -> Fail" signal.
    /// The RackModel itself is owned by GameController and injected here.
    /// </summary>
    public class RackController : MonoBehaviour
    {
        private RackModel _rack;

        // Slots that have been promised to in-flight tiles but aren't filled yet.
        // Prevents two near-simultaneous rack-bound flights from aiming at the same slot.
        private readonly System.Collections.Generic.HashSet<int> _reservedSlots
            = new System.Collections.Generic.HashSet<int>();

        public RackModel Rack => _rack;

        /// <summary>Called by GameController once the RackModel is created.</summary>
        public void Bind(RackModel rack)
        {
            _rack = rack;
        }

        /// <summary>
        /// Try to place a tile in the rack.
        /// Returns false and triggers Fail if the rack is already full.
        /// </summary>
        public bool TryAdd(TileModel tile)
        {
            if (_rack == null) return false;

            if (_rack.TryAdd(tile))
            {
                // Fail the moment the rack hits capacity — no need to wait for an overflow attempt.
                if (_rack.FilledCount >= _rack.Capacity && GameController.Instance != null)
                    GameController.Instance.SetState(GameState.Fail);
                return true;
            }

            if (GameController.Instance != null)
                GameController.Instance.SetState(GameState.Fail);
            return false;
        }

        /// <summary>
        /// Pull a specific tile out of the rack (used for auto-collect into a new order).
        /// Returns true if the tile was found and removed.
        /// </summary>
        public bool TryRemove(TileModel tile, out int slotIndex)
        {
            slotIndex = -1;
            if (_rack == null || tile == null) return false;

            for (int i = 0; i < _rack.Capacity; i++)
            {
                if (_rack.Slots[i] == tile)
                {
                    if (_rack.TryRemoveAt(i, out _))
                    {
                        slotIndex = i;
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        /// <summary>Index of the next free slot — aim tile-flies-to-rack tweens at this.</summary>
        public int PeekNextFreeSlot()
        {
            return _rack != null ? _rack.FirstFreeSlotIndex() : -1;
        }

        /// <summary>
        /// Find the leftmost slot that is neither filled nor already reserved
        /// for an in-flight tile, mark it reserved, and return its index.
        /// Returns -1 (caller should Fail) if every slot is taken.
        /// </summary>
        public int ReserveNextFreeSlot()
        {
            if (_rack == null) return -1;
            for (int i = 0; i < _rack.Capacity; i++)
            {
                if (_rack.Slots[i] == null && !_reservedSlots.Contains(i))
                {
                    _reservedSlots.Add(i);
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Drop the reservation without placing a tile (e.g. flight aborted).
        /// </summary>
        public void ReleaseReservation(int slotIndex)
        {
            _reservedSlots.Remove(slotIndex);
        }

        /// <summary>
        /// Commit a reserved slot: place the tile there and clear the reservation.
        /// Triggers Fail if this fills the rack to capacity.
        /// </summary>
        public bool CommitReserved(int slotIndex, TileModel tile)
        {
            _reservedSlots.Remove(slotIndex);
            if (_rack == null) return false;

            if (!_rack.TryAddAt(slotIndex, tile))
            {
                if (GameController.Instance != null)
                    GameController.Instance.SetState(GameState.Fail);
                return false;
            }

            if (_rack.FilledCount >= _rack.Capacity && GameController.Instance != null)
                GameController.Instance.SetState(GameState.Fail);
            return true;
        }

        public void Reset()
        {
            _reservedSlots.Clear();
            if (_rack != null) _rack.Clear();
        }
    }
}
