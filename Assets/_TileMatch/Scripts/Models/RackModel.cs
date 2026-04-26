using System;

namespace TileMatch.Models
{
    /// <summary>
    /// Runtime state of the player's rack (inventory).
    /// Tiles go here when they don't match any active order.
    /// When the rack is full, TryAdd returns false — controller triggers Fail.
    /// Pure C# — no MonoBehaviour. Capacity is passed in from GameConfigSO.
    /// </summary>
    public class RackModel
    {
        public int Capacity { get; }
        public TileModel[] Slots { get; }
        public int FilledCount { get; private set; }
        public bool IsFull => FilledCount >= Capacity;

        /// <summary>Fires after a tile is placed. Passes (slotIndex, tile). FilledCount already updated.</summary>
        public event Action<int, TileModel> OnSlotFilled;

        /// <summary>Fires after a tile is removed from a specific slot (e.g. auto-collected into an order). Passes slotIndex.</summary>
        public event Action<int> OnSlotCleared;

        /// <summary>Fires after the rack is cleared (e.g. on RestartLevel).</summary>
        public event Action OnCleared;

        public RackModel(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Slots = new TileModel[capacity];
        }

        /// <summary>
        /// Try to place a tile in the leftmost free slot.
        /// Returns false if every slot is occupied (caller triggers Fail).
        /// Note: auto-collection can leave gaps, so we scan for the first
        /// empty index instead of assuming it == FilledCount.
        /// </summary>
        public bool TryAdd(TileModel tile)
        {
            int slotIndex = FirstFreeSlotIndex();
            if (slotIndex < 0) return false;

            Slots[slotIndex] = tile;
            FilledCount++;
            OnSlotFilled?.Invoke(slotIndex, tile);
            return true;
        }

        /// <summary>
        /// Place a tile at a specific slot index. Used after a flight that
        /// reserved that slot at tap time so concurrent rack-bound flights
        /// don't all land on the same slot.
        /// </summary>
        public bool TryAddAt(int slotIndex, TileModel tile)
        {
            if (slotIndex < 0 || slotIndex >= Capacity) return false;
            if (Slots[slotIndex] != null) return false;

            Slots[slotIndex] = tile;
            FilledCount++;
            OnSlotFilled?.Invoke(slotIndex, tile);
            return true;
        }

        /// <summary>
        /// Remove the tile in a specific slot (used for auto-collection into orders).
        /// Returns false if the slot is already empty or out of range.
        /// </summary>
        public bool TryRemoveAt(int slotIndex, out TileModel removed)
        {
            removed = null;
            if (slotIndex < 0 || slotIndex >= Capacity) return false;
            if (Slots[slotIndex] == null) return false;

            removed = Slots[slotIndex];
            Slots[slotIndex] = null;
            FilledCount--;
            OnSlotCleared?.Invoke(slotIndex);
            return true;
        }

        /// <summary>Index of the leftmost empty slot, or -1 if the rack is full.</summary>
        public int FirstFreeSlotIndex()
        {
            for (int i = 0; i < Capacity; i++)
                if (Slots[i] == null) return i;
            return -1;
        }

        public void Clear()
        {
            Array.Clear(Slots, 0, Capacity);
            FilledCount = 0;
            OnCleared?.Invoke();
        }
    }
}
