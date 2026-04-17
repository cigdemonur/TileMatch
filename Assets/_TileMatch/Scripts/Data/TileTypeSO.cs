using UnityEngine;

namespace TileMatch.Data
{
    [CreateAssetMenu(fileName = "TileType_New", menuName = "TileMatch/Tile Type")]
    public class TileTypeSO : ScriptableObject
    {
        public string typeName;
        public Sprite icon;
        public Color tintColor = Color.white;
    }
}
