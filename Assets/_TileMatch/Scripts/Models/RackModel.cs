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

        /// <summary>Fires after the rack is cleared (e.g. on RestartLevel).</summary>
        public event Action OnCleared;

        public RackModel(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Slots = new TileModel[capacity];
        }

        /// <summary>
        /// Try to place a tile in the next free slot.
        /// Returns false if the rack is already full (caller triggers Fail).
        /// </summary>
        public bool TryAdd(TileModel tile)
        {
            if (IsFull) return false;
            int slotIndex = FilledCount;
            Slots[slotIndex] = tile;
            FilledCount++;
            OnSlotFilled?.Invoke(slotIndex, tile);
            return true;
        }

        public void Clear()
        {
            Array.Clear(Slots, 0, Capacity);
            FilledCount = 0;
            OnCleared?.Invoke();
        }
    }
}
