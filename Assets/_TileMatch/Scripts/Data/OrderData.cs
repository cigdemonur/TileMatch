using System;

namespace TileMatch.Data
{
    /// <summary>
    /// One order in a level. An order requires collecting 3 tiles of the same
    /// tileType. Serialized inside LevelDataSO.
    /// </summary>
    [Serializable]
    public class OrderData
    {
        public const int RequiredCount = 3;

        public TileTypeSO tileType;
    }
}
