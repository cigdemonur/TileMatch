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

            if (_rack.TryAdd(tile)) return true;

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

        public void Reset()
        {
            if (_rack != null) _rack.Clear();
        }
    }
}
