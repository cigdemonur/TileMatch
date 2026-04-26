using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;

namespace TileMatch.EditorTools
{
    /// <summary>
    /// Greedy simulator that decides whether a LevelDataSO is solvable under
    /// the given GameConfig (rack capacity + simultaneous orders).
    ///
    /// Strategy each step:
    ///   1) If a tile already in the rack matches an active order, consume it.
    ///   2) Else look at all tappable tiles (no overlapping tile with a higher
    ///      OriginalLayer); prefer one whose type matches an active order.
    ///   3) Else send the first tappable tile to the rack. Fail if rack is full.
    /// Loops until the board empties (success) or no progress is possible.
    ///
    /// Greedy can produce false negatives on hand-built puzzles that need a
    /// non-obvious tap order, but it never produces false positives.
    /// </summary>
    public static class LevelSolver
    {
        private class Tile
        {
            public int x, y, layer;
            public TileTypeSO type;
            public bool removed;
        }

        private struct ActiveOrder
        {
            public TileTypeSO type;
            public int collected;
        }

        public struct Result
        {
            public bool solvable;
            public string message;
        }

        public static Result Check(LevelDataSO level, GameConfigSO config)
        {
            if (level == null)
                return new Result { solvable = false, message = "No level loaded." };

            int simultaneous = config != null ? Mathf.Max(1, config.simultaneousOrders) : 2;
            int rackCap = config != null ? Mathf.Max(1, config.rackCapacity) : 5;

            var tiles = new List<Tile>();
            foreach (var p in level.tiles)
            {
                if (p.tileType == null) continue;
                tiles.Add(new Tile { x = p.dotX, y = p.dotY, layer = p.layer, type = p.tileType });
            }

            var queue = new Queue<TileTypeSO>();
            foreach (var o in level.orders)
                if (o != null && o.tileType != null) queue.Enqueue(o.tileType);

            // Quick structural checks first.
            if (tiles.Count != queue.Count * 3)
                return new Result { solvable = false,
                    message = $"Tile count {tiles.Count} ≠ orders × 3 ({queue.Count * 3})." };

            var active = new List<ActiveOrder>();
            for (int i = 0; i < simultaneous && queue.Count > 0; i++)
                active.Add(new ActiveOrder { type = queue.Dequeue() });

            var rack = new List<TileTypeSO>();

            int safety = tiles.Count * 8 + 100;
            while (safety-- > 0)
            {
                int remaining = 0;
                foreach (var t in tiles) if (!t.removed) remaining++;
                if (remaining == 0) return new Result { solvable = true, message = "Solvable." };

                // 1) Pull from rack if it matches an active order.
                if (TryConsumeFromRack(rack, active, queue)) continue;

                // 2) Find tappable tiles.
                var tappable = new List<Tile>();
                foreach (var t in tiles)
                    if (!t.removed && IsTappable(t, tiles)) tappable.Add(t);

                if (tappable.Count == 0)
                    return new Result { solvable = false, message = "Stuck: no tile is tappable." };

                // 3) Prefer tappable that matches an active order.
                Tile pick = null;
                foreach (var t in tappable)
                {
                    if (active.FindIndex(a => a.type == t.type) >= 0) { pick = t; break; }
                }

                if (pick != null)
                {
                    pick.removed = true;
                    Collect(pick.type, active, queue);
                }
                else
                {
                    // 4) No match — push to rack.
                    if (rack.Count >= rackCap)
                        return new Result { solvable = false,
                            message = "Rack overflows before any matching tile becomes free." };
                    rack.Add(tappable[0].type);
                    tappable[0].removed = true;
                }
            }

            return new Result { solvable = false, message = "Solver gave up (safety cap)." };
        }

        private static bool TryConsumeFromRack(List<TileTypeSO> rack, List<ActiveOrder> active, Queue<TileTypeSO> queue)
        {
            for (int i = 0; i < rack.Count; i++)
            {
                if (active.FindIndex(a => a.type == rack[i]) < 0) continue;
                var t = rack[i];
                rack.RemoveAt(i);
                Collect(t, active, queue);
                return true;
            }
            return false;
        }

        private static void Collect(TileTypeSO type, List<ActiveOrder> active, Queue<TileTypeSO> queue)
        {
            int idx = active.FindIndex(a => a.type == type);
            if (idx < 0) return;
            var a = active[idx];
            a.collected++;
            if (a.collected >= 3)
            {
                active.RemoveAt(idx);
                if (queue.Count > 0)
                    active.Add(new ActiveOrder { type = queue.Dequeue() });
            }
            else
            {
                active[idx] = a;
            }
        }

        private static bool IsTappable(Tile t, List<Tile> all)
        {
            foreach (var s in all)
            {
                if (s == t || s.removed) continue;
                if (s.layer <= t.layer) continue;
                int dx = Mathf.Abs(s.x - t.x);
                int dy = Mathf.Abs(s.y - t.y);
                if (dx <= 1 && dy <= 1) return false;
            }
            return true;
        }
    }
}
