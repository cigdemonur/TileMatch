using System;

namespace TileMatch.Data
{
    /// <summary>
    /// A single tile placement in a level. Serialized inside LevelDataSO.
    /// Position is on the dot map (tile center); actual world position is
    /// computed at runtime using GameConfigSO.miniSquareSize.
    /// </summary>
    [Serializable]
    public struct TilePlacement
    {
        public int dotX;
        public int dotY;
        public int layer;
        public TileTypeSO tileType;
    }
}
