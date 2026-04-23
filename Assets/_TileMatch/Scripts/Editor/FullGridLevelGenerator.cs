#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TileMatch.Data;

namespace TileMatch.EditorTools
{
    /// <summary>
    /// Generates a demo LevelDataSO that fills the board with tiles in a
    /// brick pattern: tiles laid diagonally at (dx=2, dy=2) offset — the only
    /// dense packing allowed by the overlap rule (corner-touch is fine).
    ///
    /// Result: 3 tiles per row × 8 rows = 24 tiles on a single layer.
    /// 8 distinct tile types × 3 each = 8 orders (one per type).
    /// </summary>
    public static class FullGridLevelGenerator
    {
        private const string LevelAssetPath = "Assets/_TileMatch/Data/Levels/Level_FullGrid.asset";
        private const string TileTypesFolder = "Assets/_TileMatch/Data/TileTypes";

        [MenuItem("TileMatch/Generate Full Grid Level")]
        public static void Generate()
        {
            // --- 1. Load every TileTypeSO in the project ---
            var guids = AssetDatabase.FindAssets("t:TileTypeSO", new[] { TileTypesFolder });
            var types = new List<TileTypeSO>(guids.Length);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<TileTypeSO>(path);
                if (t != null) types.Add(t);
            }

            if (types.Count < 1)
            {
                Debug.LogError($"FullGridLevelGenerator: no TileTypeSO found under {TileTypesFolder}.");
                return;
            }

            // --- 2. Build tile placements in brick pattern ---
            // Rows at y = 0, 2, 4, ..., 14 (8 rows).
            // Even-index rows start at x=0, odd-index rows start at x=2.
            // Columns spaced by 4. 3 tiles/row × 8 rows = 24 tiles.
            //
            // Assign types in GROUPS OF 3 across the placement order so every
            // group of 3 tiles shares a type — and create exactly one order
            // per group. This guarantees orders.Count * 3 == placements.Count
            // and every tile has a matching order, regardless of how many
            // distinct TileTypeSO assets exist.
            var placements = new List<TilePlacement>();
            var orderTypes = new List<TileTypeSO>();

            int required = OrderData.RequiredCount; // 3
            int flatIdx = 0;

            for (int row = 0; row < 8; row++)
            {
                int y = row * 2;
                int xStart = (row % 2 == 0) ? 0 : 2;

                for (int col = 0; col < 3; col++)
                {
                    int x = xStart + col * 4;
                    int groupIdx = flatIdx / required;               // 0..7
                    var type = types[groupIdx % types.Count];        // wrap if <8 types

                    placements.Add(new TilePlacement
                    {
                        dotX = x,
                        dotY = y,
                        layer = 0,
                        tileType = type
                    });

                    // First tile of a new group → register the order for that group.
                    if (flatIdx % required == 0)
                    {
                        orderTypes.Add(type);
                    }

                    flatIdx++;
                }
            }

            // --- 3. Build one order per group ---
            var orders = new List<OrderData>(orderTypes.Count);
            foreach (var t in orderTypes)
            {
                orders.Add(new OrderData { tileType = t });
            }

            // --- 4. Create or overwrite the asset ---
            var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(LevelAssetPath);
            bool isNew = level == null;
            if (isNew)
            {
                level = ScriptableObject.CreateInstance<LevelDataSO>();
            }
            level.tiles = placements;
            level.orders = orders;

            if (isNew)
            {
                AssetDatabase.CreateAsset(level, LevelAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"FullGridLevelGenerator: wrote {placements.Count} tiles and {orders.Count} orders to {LevelAssetPath}");
            EditorGUIUtility.PingObject(level);
        }
    }
}
#endif
