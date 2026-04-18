using System;
using TileMatch.Data;

namespace TileMatch.Models
{
    /// <summary>
    /// Runtime state of a single order. An order requires collecting
    /// exactly OrderData.RequiredCount tiles of a single TileType.
    /// Pure C# — no MonoBehaviour. Views subscribe to events to render progress.
    /// </summary>
    public class OrderModel
    {
        public TileTypeSO TileType { get; }
        public int CollectedCount { get; private set; }
        public bool IsComplete => CollectedCount >= OrderData.RequiredCount;

        /// <summary>Fires whenever a tile is collected. Passes new CollectedCount.</summary>
        public event Action<int> OnProgressChanged;

        /// <summary>Fires exactly once when the order becomes complete.</summary>
        public event Action OnCompleted;

        public OrderModel(TileTypeSO tileType)
        {
            TileType = tileType;
            CollectedCount = 0;
        }

        /// <summary>
        /// Returns true if this order can currently accept a tile of the given type.
        /// False if the order is already complete or the type doesn't match.
        /// </summary>
        public bool Matches(TileTypeSO type) => !IsComplete && TileType == type;

        /// <summary>
        /// Increment the collected count by one. No-op if already complete.
        /// Fires OnProgressChanged, and OnCompleted when reaching RequiredCount.
        /// </summary>
        public void Collect()
        {
            if (IsComplete) return;
            CollectedCount++;
            OnProgressChanged?.Invoke(CollectedCount);
            if (IsComplete) OnCompleted?.Invoke();
        }
    }
}
