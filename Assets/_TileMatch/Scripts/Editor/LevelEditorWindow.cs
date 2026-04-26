using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TileMatch.Data;

namespace TileMatch.EditorTools
{
    /// <summary>
    /// Custom EditorWindow for authoring LevelDataSO assets without touching YAML.
    /// Three panels: tile palette + tools (left), clickable dot grid (center),
    /// orders list + save controls (right). Open via Window → TileMatch → Level Editor.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // Working state
        private LevelDataSO _level;
        private GameConfigSO _gameConfig;
        private int _selectedLayer = 0;
        private bool _autoLayer = true;
        private int _layerFilter = -1; // -1 = show all
        private int _addOrderTypeIndex = 0;

        // Cache
        private List<TileTypeSO> _tileTypes;
        private Vector2 _ordersScroll;
        private Vector2 _trayScroll;
        private Vector2 _rightPanelScroll;

        // Solver / Generator UI state
        private string _solverMessage = "";
        private bool _showGenerator;
        private int _genOrders = 6;
        private List<int> _genTilesPerLayer = new List<int> { 6, 6, 6 };
        private int _genMaxAttempts = 50;
        private HashSet<TileTypeSO> _genPalette = new HashSet<TileTypeSO>();
        private string _genMessage = "";

        // Drag state — set when the user grabs a chip from the tray.
        private TileTypeSO _draggingType;
        private Vector2 _dragMousePos;
        private Rect _gridRect; // last drawn grid rect, for drop hit-testing

        // Layout constants
        private const float TrayWidth = 180f;
        private const float OrdersWidth = 240f;
        private const float CellSize = 30f;

        [MenuItem("Window/TileMatch/Level Editor")]
        public static void Open()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            RefreshTileTypes();
            if (_gameConfig == null)
                _gameConfig = AssetDatabase.LoadAssetAtPath<GameConfigSO>(
                    "Assets/_TileMatch/Data/GameConfig.asset");
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawTrayPanel();
            DrawGridPanel();
            DrawOrdersPanel();
            EditorGUILayout.EndHorizontal();

            HandleGlobalDrag();
            DrawDragGhost();
        }

        private void RefreshTileTypes()
        {
            _tileTypes = new List<TileTypeSO>();
            var guids = AssetDatabase.FindAssets("t:TileTypeSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<TileTypeSO>(path);
                if (t != null) _tileTypes.Add(t);
            }
            _tileTypes.Sort((a, b) => string.Compare(a.name, b.name));
            Debug.Log($"[LevelEditor] RefreshTileTypes found {_tileTypes.Count} TileTypeSO assets.");
        }

        // -------- Tray panel (drag tiles onto grid) --------

        /// <summary>
        /// Shows one chip per tile type demanded by the current orders, with a
        /// "remaining = orders*3 - placed" count. Mouse-down on a chip starts a
        /// drag; release over the grid drops a tile at the hovered anchor.
        /// </summary>
        private void DrawTrayPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(TrayWidth));

            EditorGUILayout.LabelField("Layer (drop)", EditorStyles.boldLabel);
            _autoLayer = EditorGUILayout.ToggleLeft("Auto (stack on top)", _autoLayer);
            using (new EditorGUI.DisabledScope(_autoLayer))
                _selectedLayer = EditorGUILayout.IntSlider(_selectedLayer, 0, 5);

            EditorGUILayout.LabelField("Show layers", EditorStyles.miniBoldLabel);
            _layerFilter = EditorGUILayout.Popup(_layerFilter + 1,
                new[] { "All", "0", "1", "2", "3", "4", "5" }) - 1;

            EditorGUILayout.LabelField("Game Config", EditorStyles.miniBoldLabel);
            _gameConfig = (GameConfigSO)EditorGUILayout.ObjectField(
                _gameConfig, typeof(GameConfigSO), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tray", EditorStyles.boldLabel);

            if (_level == null || _level.orders == null || _level.orders.Count == 0)
            {
                EditorGUILayout.HelpBox("Add orders on the right — the tray fills with 3 tiles per order.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var demand = ComputeDemand();
            var placed = ComputePlaced();

            _trayScroll = EditorGUILayout.BeginScrollView(_trayScroll);
            foreach (var kv in demand)
            {
                var type = kv.Key;
                int remaining = kv.Value - (placed.TryGetValue(type, out int p) ? p : 0);

                var chipRect = GUILayoutUtility.GetRect(TrayWidth - 10, 44, GUILayout.Width(TrayWidth - 10));
                var bg = remaining > 0 ? new Color(0.22f, 0.28f, 0.35f) : new Color(0.18f, 0.18f, 0.18f);
                EditorGUI.DrawRect(chipRect, bg);

                if (type.icon != null)
                {
                    var iconRect = new Rect(chipRect.x + 4, chipRect.y + 4, 36, 36);
                    GUI.DrawTexture(iconRect, type.icon.texture, ScaleMode.ScaleToFit);
                }
                var label = $"{type.name}\n×{remaining}";
                GUI.Label(new Rect(chipRect.x + 44, chipRect.y + 2, chipRect.width - 46, chipRect.height - 4),
                    label, EditorStyles.miniLabel);

                var ev = Event.current;
                if (ev.type == EventType.MouseDown && ev.button == 0 && chipRect.Contains(ev.mousePosition) && remaining > 0)
                {
                    _draggingType = type;
                    _dragMousePos = ev.mousePosition;
                    ev.Use();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox("Drag a chip onto the grid to place it at the selected layer.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private Dictionary<TileTypeSO, int> ComputeDemand()
        {
            var d = new Dictionary<TileTypeSO, int>();
            if (_level == null) return d;
            foreach (var o in _level.orders)
            {
                if (o == null || o.tileType == null) continue;
                d.TryGetValue(o.tileType, out int c);
                d[o.tileType] = c + 3;
            }
            return d;
        }

        private Dictionary<TileTypeSO, int> ComputePlaced()
        {
            var d = new Dictionary<TileTypeSO, int>();
            if (_level == null) return d;
            foreach (var p in _level.tiles)
            {
                if (p.tileType == null) continue;
                d.TryGetValue(p.tileType, out int c);
                d[p.tileType] = c + 1;
            }
            return d;
        }

        private void HandleGlobalDrag()
        {
            if (_draggingType == null) return;
            var ev = Event.current;
            if (ev.type == EventType.MouseDrag || ev.type == EventType.MouseMove)
            {
                _dragMousePos = ev.mousePosition;
                Repaint();
            }
            if (ev.type == EventType.MouseUp)
            {
                if (_gridRect.Contains(ev.mousePosition))
                    DropOnGrid(ev.mousePosition);
                _draggingType = null;
                Repaint();
                ev.Use();
            }
        }

        private void DropOnGrid(Vector2 mouse)
        {
            if (_level == null || _draggingType == null) return;

            int w = _gameConfig != null ? _gameConfig.dotMapWidth : 11;
            int h = _gameConfig != null ? _gameConfig.dotMapHeight : 15;

            // Outer map is (w+1)×(h+1) cells; tile is 2×2 cells, so anchors span 0..w-1 × 0..h-1.
            int cx = Mathf.FloorToInt((mouse.x - _gridRect.x) / CellSize);
            int cyFromTop = Mathf.FloorToInt((mouse.y - _gridRect.y) / CellSize);
            int anchorX = Mathf.Clamp(cx, 0, w - 1);
            int anchorY = Mathf.Clamp(h - 1 - cyFromTop, 0, h - 1);

            // Auto-layer: stack above the highest tile whose 2x2 footprint overlaps
            // the drop anchor (anchors within 1 cell in both axes overlap).
            int targetLayer;
            if (_autoLayer)
            {
                int top = -1;
                foreach (var t in _level.tiles)
                {
                    if (Mathf.Abs(t.dotX - anchorX) <= 1 &&
                        Mathf.Abs(t.dotY - anchorY) <= 1 &&
                        t.layer > top)
                    {
                        top = t.layer;
                    }
                }
                targetLayer = top + 1;
            }
            else
            {
                targetLayer = _selectedLayer;
            }

            // Respect remaining budget: if there's already a tile of this type at
            // (anchor, layer) we'd just replace it, which doesn't change counts.
            var demand = ComputeDemand();
            var placed = ComputePlaced();
            int remaining = (demand.TryGetValue(_draggingType, out int dem) ? dem : 0)
                           - (placed.TryGetValue(_draggingType, out int pl) ? pl : 0);

            bool replacingSameType = _level.tiles.Exists(t =>
                t.dotX == anchorX && t.dotY == anchorY && t.layer == targetLayer && t.tileType == _draggingType);

            if (remaining <= 0 && !replacingSameType) return;

            Undo.RecordObject(_level, "Drop Tile");
            _level.tiles.RemoveAll(p => p.dotX == anchorX && p.dotY == anchorY && p.layer == targetLayer);
            _level.tiles.Add(new TilePlacement
            {
                dotX = anchorX,
                dotY = anchorY,
                layer = targetLayer,
                tileType = _draggingType,
            });
            EditorUtility.SetDirty(_level);
        }

        private void DrawDragGhost()
        {
            if (_draggingType == null || _draggingType.icon == null) return;
            var size = CellSize * 2;
            var r = new Rect(_dragMousePos.x - size * 0.5f, _dragMousePos.y - size * 0.5f, size, size);
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.8f);
            GUI.DrawTexture(r, _draggingType.icon.texture, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        // -------- Grid panel --------

        private void DrawGridPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Dot Grid (click to place / erase)", EditorStyles.boldLabel);

            int w = _gameConfig != null ? _gameConfig.dotMapWidth : 11;
            int h = _gameConfig != null ? _gameConfig.dotMapHeight : 15;

            // Dots are centers of 2x2 tiles. 11×15 dots → 10×14 mini-boxes between
            // them, plus a half-cell margin on every side gives a (w+1)×(h+1)
            // mini-box map = 12×16 lines (one per box edge).
            float outerW = (w + 1) * CellSize;
            float outerH = (h + 1) * CellSize;
            var outerRect = GUILayoutUtility.GetRect(outerW, outerH, GUILayout.Width(outerW), GUILayout.Height(outerH));
            var rect = outerRect;
            _gridRect = outerRect;

            // Background
            EditorGUI.DrawRect(outerRect, new Color(0.18f, 0.18f, 0.18f));

            // 12×16 grid lines spanning the full mini-box map.
            var lineCol = new Color(0.3f, 0.3f, 0.3f);
            for (int x = 0; x <= w + 1; x++)
                EditorGUI.DrawRect(new Rect(outerRect.x + x * CellSize, outerRect.y, 1, outerH), lineCol);
            for (int y = 0; y <= h + 1; y++)
                EditorGUI.DrawRect(new Rect(outerRect.x, outerRect.y + y * CellSize, outerW, 1), lineCol);

            // Tiles (draw bottom layer first; flip y so dotY=0 is at bottom of screen)
            if (_level != null)
            {
                var sorted = new List<TilePlacement>(_level.tiles);
                sorted.Sort((a, b) => a.layer.CompareTo(b.layer));

                foreach (var p in sorted)
                {
                    if (_layerFilter >= 0 && p.layer != _layerFilter) continue;

                    float px = rect.x + p.dotX * CellSize;
                    float py = rect.y + (h - 1 - p.dotY) * CellSize; // 2x2 footprint, flip y
                    var tileRect = new Rect(px, py, CellSize * 2, CellSize * 2);

                    // Layer-tinted fill
                    var fill = LayerColor(p.layer);
                    if (_layerFilter < 0 && p.layer < HighestLayerAt(p.dotX, p.dotY))
                        fill.a *= 0.45f; // dim if a higher-layer tile sits on the same anchor
                    EditorGUI.DrawRect(tileRect, fill);

                    // Icon
                    if (p.tileType != null && p.tileType.icon != null)
                    {
                        var iconRect = new Rect(px + 4, py + 4, CellSize * 2 - 8, CellSize * 2 - 8);
                        GUI.DrawTexture(iconRect, p.tileType.icon.texture, ScaleMode.ScaleToFit);
                    }

                    // Layer label
                    var labelRect = new Rect(px + 2, py + 2, 20, 14);
                    EditorGUI.DrawRect(labelRect, new Color(0, 0, 0, 0.6f));
                    GUI.Label(labelRect, "L" + p.layer, EditorStyles.whiteMiniLabel);
                }
            }

            // Hover highlight + click handling
            var ev = Event.current;
            if (outerRect.Contains(ev.mousePosition))
            {
                int cx = Mathf.FloorToInt((ev.mousePosition.x - rect.x) / CellSize);
                int cyFromTop = Mathf.FloorToInt((ev.mousePosition.y - rect.y) / CellSize);
                int anchorX = Mathf.Clamp(cx, 0, w - 1);
                int anchorY = Mathf.Clamp(h - 1 - cyFromTop, 0, h - 1);

                float hx = rect.x + anchorX * CellSize;
                float hy = rect.y + (h - 1 - anchorY) * CellSize;
                EditorGUI.DrawRect(new Rect(hx, hy, CellSize * 2, CellSize * 2),
                    new Color(1f, 1f, 0.4f, 0.15f));

                if (ev.type == EventType.MouseDown && ev.button == 0 && _draggingType == null)
                {
                    EraseAt(anchorX, anchorY);
                    ev.Use();
                }
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "Drag tiles from the tray to place. Click a tile to erase it (uses the layer filter, or top tile if 'All').",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private int HighestLayerAt(int dotX, int dotY)
        {
            int max = 0;
            if (_level == null) return 0;
            foreach (var p in _level.tiles)
                if (p.dotX == dotX && p.dotY == dotY && p.layer > max) max = p.layer;
            return max;
        }

        private Color LayerColor(int layer)
        {
            switch (layer % 6)
            {
                case 0: return new Color(0.30f, 0.50f, 0.85f, 0.85f); // blue
                case 1: return new Color(0.85f, 0.55f, 0.30f, 0.85f); // orange
                case 2: return new Color(0.40f, 0.75f, 0.40f, 0.85f); // green
                case 3: return new Color(0.85f, 0.40f, 0.65f, 0.85f); // pink
                case 4: return new Color(0.75f, 0.75f, 0.30f, 0.85f); // yellow
                default: return new Color(0.55f, 0.40f, 0.80f, 0.85f); // purple
            }
        }

        private void EraseAt(int anchorX, int anchorY)
        {
            if (_level == null) return;

            Undo.RecordObject(_level, "Erase Tile");

            if (_layerFilter >= 0)
            {
                _level.tiles.RemoveAll(p => p.dotX == anchorX && p.dotY == anchorY && p.layer == _layerFilter);
            }
            else
            {
                int top = -1;
                foreach (var p in _level.tiles)
                    if (p.dotX == anchorX && p.dotY == anchorY && p.layer > top) top = p.layer;
                if (top >= 0)
                    _level.tiles.RemoveAll(p => p.dotX == anchorX && p.dotY == anchorY && p.layer == top);
            }

            EditorUtility.SetDirty(_level);
        }

        // -------- Orders panel --------

        private void DrawOrdersPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(OrdersWidth));
            _rightPanelScroll = EditorGUILayout.BeginScrollView(_rightPanelScroll);
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

            _level = (LevelDataSO)EditorGUILayout.ObjectField(_level, typeof(LevelDataSO), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New")) CreateNewLevel();
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("Save"))
                {
                    EditorUtility.SetDirty(_level);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Clear Tiles") && _level != null)
                {
                    if (EditorUtility.DisplayDialog("Clear Tiles",
                        $"Remove all {_level.tiles.Count} tiles from this level?", "Yes", "Cancel"))
                    {
                        Undo.RecordObject(_level, "Clear Tiles");
                        _level.tiles.Clear();
                        EditorUtility.SetDirty(_level);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_level == null)
            {
                EditorGUILayout.HelpBox("Drag a LevelDataSO here, or click 'New' to create one.",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"Tiles: {_level.tiles.Count}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orders", EditorStyles.boldLabel);

            _ordersScroll = EditorGUILayout.BeginScrollView(_ordersScroll, GUILayout.Height(160));
            for (int i = 0; i < _level.orders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28));
                if (_level.orders[i] == null) _level.orders[i] = new OrderData();
                var newType = (TileTypeSO)EditorGUILayout.ObjectField(
                    _level.orders[i].tileType, typeof(TileTypeSO), false);
                if (newType != _level.orders[i].tileType)
                {
                    Undo.RecordObject(_level, "Edit Order");
                    _level.orders[i].tileType = newType;
                    EditorUtility.SetDirty(_level);
                }
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    Undo.RecordObject(_level, "Remove Order");
                    _level.orders.RemoveAt(i);
                    EditorUtility.SetDirty(_level);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (_tileTypes != null && _tileTypes.Count > 0)
            {
                var names = new string[_tileTypes.Count];
                for (int i = 0; i < _tileTypes.Count; i++) names[i] = _tileTypes[i].name;
                _addOrderTypeIndex = Mathf.Clamp(_addOrderTypeIndex, 0, _tileTypes.Count - 1);
                _addOrderTypeIndex = EditorGUILayout.Popup(_addOrderTypeIndex, names);

                if (GUILayout.Button("+ Add Order", GUILayout.Width(96)))
                {
                    Undo.RecordObject(_level, "Add Order");
                    _level.orders.Add(new OrderData { tileType = _tileTypes[_addOrderTypeIndex] });
                    EditorUtility.SetDirty(_level);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No TileTypeSO assets found.", MessageType.Warning);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawValidationSummary();

            EditorGUILayout.Space();
            DrawSolvabilitySection();

            EditorGUILayout.Space();
            DrawGeneratorSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSolvabilitySection()
        {
            EditorGUILayout.LabelField("Solvability", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("Check Solvability"))
                {
                    var r = LevelSolver.Check(_level, _gameConfig);
                    _solverMessage = (r.solvable ? "✓ " : "✗ ") + r.message;
                }
            }
            if (!string.IsNullOrEmpty(_solverMessage))
            {
                var type = _solverMessage.StartsWith("✓") ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(_solverMessage, type);
            }
        }

        private void DrawGeneratorSection()
        {
            _showGenerator = EditorGUILayout.Foldout(_showGenerator, "Generator", true);
            if (!_showGenerator) return;

            _genOrders = Mathf.Max(1, EditorGUILayout.IntField("Orders", _genOrders));

            // Layer count + per-layer tile counts
            int layerCount = Mathf.Max(1, EditorGUILayout.IntField("Layers", _genTilesPerLayer.Count));
            while (_genTilesPerLayer.Count < layerCount) _genTilesPerLayer.Add(0);
            while (_genTilesPerLayer.Count > layerCount) _genTilesPerLayer.RemoveAt(_genTilesPerLayer.Count - 1);

            for (int i = 0; i < _genTilesPerLayer.Count; i++)
            {
                string label = i == 0 ? "L0 (bottom)" : (i == _genTilesPerLayer.Count - 1 ? $"L{i} (top)" : $"L{i}");
                _genTilesPerLayer[i] = Mathf.Max(0, EditorGUILayout.IntField(label, _genTilesPerLayer[i]));
            }

            int sum = 0;
            foreach (int n in _genTilesPerLayer) sum += n;
            EditorGUILayout.LabelField($"Sum: {sum}   Need: {_genOrders * 3}");

            _genMaxAttempts = Mathf.Max(1, EditorGUILayout.IntField("Max Attempts", _genMaxAttempts));

            // Palette multi-select
            EditorGUILayout.LabelField("Palette (types the generator may use)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButton))
            {
                _genPalette.Clear();
                foreach (var t in _tileTypes) if (t != null) _genPalette.Add(t);
            }
            if (GUILayout.Button("None", EditorStyles.miniButton)) _genPalette.Clear();
            EditorGUILayout.EndHorizontal();

            if (_tileTypes != null)
            {
                foreach (var t in _tileTypes)
                {
                    if (t == null) continue;
                    bool was = _genPalette.Contains(t);
                    bool now = EditorGUILayout.ToggleLeft(t.name, was);
                    if (now != was)
                    {
                        if (now) _genPalette.Add(t); else _genPalette.Remove(t);
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("Generate Into Current Level"))
                    RunGenerator();
            }
            if (!string.IsNullOrEmpty(_genMessage))
            {
                var type = _genMessage.StartsWith("✓") ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(_genMessage, type);
            }
        }

        private void RunGenerator()
        {
            if (_level == null) return;

            var palette = new List<TileTypeSO>(_genPalette);
            if (palette.Count == 0)
            {
                _genMessage = "✗ Pick at least one tile type for the palette.";
                return;
            }

            var result = LevelGenerator.Generate(
                palette,
                _genOrders,
                _genTilesPerLayer.ToArray(),
                _gameConfig,
                _genMaxAttempts);

            if (!result.success)
            {
                _genMessage = "✗ " + result.message;
                return;
            }

            Undo.RecordObject(_level, "Generate Level");
            _level.tiles = result.tiles;
            _level.orders = new List<OrderData>(result.orderTypes.Count);
            foreach (var t in result.orderTypes)
                _level.orders.Add(new OrderData { tileType = t });
            EditorUtility.SetDirty(_level);
            _genMessage = "✓ " + result.message;
            _solverMessage = ""; // stale
        }

        private void DrawValidationSummary()
        {
            var counts = new Dictionary<TileTypeSO, int>();
            foreach (var p in _level.tiles)
            {
                if (p.tileType == null) continue;
                counts.TryGetValue(p.tileType, out int c);
                counts[p.tileType] = c + 1;
            }

            int totalTiles = _level.tiles.Count;
            int totalOrderTiles = _level.orders.Count * 3;

            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Tiles total: {totalTiles}");
            EditorGUILayout.LabelField($"Order capacity (3 per): {totalOrderTiles}");

            if (totalTiles % 3 != 0)
                EditorGUILayout.HelpBox("Tile count is not divisible by 3 — level isn't completable.", MessageType.Warning);
            else if (totalTiles != totalOrderTiles)
                EditorGUILayout.HelpBox($"Tiles ({totalTiles}) and order capacity ({totalOrderTiles}) don't match.", MessageType.Warning);

            foreach (var kv in counts)
            {
                if (kv.Value % 3 != 0)
                {
                    EditorGUILayout.HelpBox(
                        $"{kv.Key.name}: {kv.Value} tiles — not a multiple of 3.",
                        MessageType.Warning);
                }
            }
        }

        // -------- New Level --------

        private void CreateNewLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Level",
                "Level_New",
                "asset",
                "Choose a name and folder for the new LevelDataSO.");
            if (string.IsNullOrEmpty(path)) return;

            // Make sure folder exists (SaveFilePanelInProject already restricts to inside Assets)
            var dir = Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                EditorUtility.DisplayDialog("Level Editor",
                    "Pick a folder inside Assets/.", "OK");
                return;
            }

            var so = ScriptableObject.CreateInstance<LevelDataSO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            _level = so;
            Selection.activeObject = so;
        }
    }
}
