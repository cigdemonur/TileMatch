#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;

namespace TileMatch.EditorTools
{
    /// <summary>
    /// Editor-only random level generator. Builds a candidate placement plus
    /// orders list, runs <see cref="LevelSolver"/> against it, and retries
    /// until a solvable layout is found (or max attempts exhausted).
    ///
    /// Solvability hint: tiles for early-queue orders are placed on the TOP
    /// layers so they are tappable while those orders are active. Tiles for
    /// late-queue orders sit on the BOTTOM layers, exposed as upper layers
    /// are cleared.
    /// </summary>
    public static class LevelGenerator
    {
        public struct Result
        {
            public bool success;
            public string message;
            public List<TilePlacement> tiles;
            public List<TileTypeSO> orderTypes;
            public int attemptsUsed;
        }

        public static Result Generate(
            IList<TileTypeSO> palette,
            int ordersCount,
            int[] tilesPerLayer,        // index 0 = bottom layer
            GameConfigSO config,
            int maxAttempts = 50,
            int? seed = null)
        {
            if (palette == null || palette.Count == 0)
                return Fail("Palette is empty — pick at least one tile type.");
            if (ordersCount <= 0)
                return Fail("Orders count must be > 0.");
            if (tilesPerLayer == null || tilesPerLayer.Length == 0)
                return Fail("Provide at least one layer.");

            int total = 0;
            foreach (int n in tilesPerLayer) total += n;
            if (total != ordersCount * OrderData.RequiredCount)
                return Fail($"Sum of tiles per layer ({total}) must equal orders × 3 ({ordersCount * OrderData.RequiredCount}).");

            int gridW = config != null ? config.dotMapWidth : 11;
            int gridH = config != null ? config.dotMapHeight : 15;
            if (gridW < 2 || gridH < 2)
                return Fail("Dot map too small for 2×2 tiles.");

            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var orderTypes = PickOrders(palette, ordersCount, rng);
                var bag = BuildQueueOrderedBag(orderTypes); // [order0,order0,order0, order1,...]
                var anchors = TryPlaceAnchors(tilesPerLayer, gridW, gridH, rng);
                if (anchors == null) continue; // retry

                // Assign tile types: TOP layer takes first 3 orders' tiles,
                // BOTTOM layer takes last orders' tiles.
                int L = tilesPerLayer.Length;
                var placements = new List<TilePlacement>(total);
                int bagIdx = 0;
                for (int layer = L - 1; layer >= 0; layer--)
                {
                    foreach (var (x, y) in anchors[layer])
                    {
                        placements.Add(new TilePlacement
                        {
                            dotX = x,
                            dotY = y,
                            layer = layer,
                            tileType = bag[bagIdx++],
                        });
                    }
                }

                // Validate via solver.
                var fake = ScriptableObject.CreateInstance<LevelDataSO>();
                fake.tiles = placements;
                fake.orders = new List<OrderData>(orderTypes.Count);
                foreach (var t in orderTypes) fake.orders.Add(new OrderData { tileType = t });

                var check = LevelSolver.Check(fake, config);
                Object.DestroyImmediate(fake);

                if (check.solvable)
                {
                    return new Result
                    {
                        success = true,
                        message = $"Solvable after {attempt} attempt(s).",
                        tiles = placements,
                        orderTypes = orderTypes,
                        attemptsUsed = attempt,
                    };
                }
            }

            return Fail($"No solvable layout found after {maxAttempts} attempts. Try fewer layers, more rack space, or simpler tile counts.");
        }

        // --- Helpers ---

        private static List<TileTypeSO> PickOrders(IList<TileTypeSO> palette, int count, System.Random rng)
        {
            var picks = new List<TileTypeSO>(count);
            for (int i = 0; i < count; i++)
                picks.Add(palette[rng.Next(palette.Count)]);
            return picks;
        }

        private static List<TileTypeSO> BuildQueueOrderedBag(List<TileTypeSO> orderTypes)
        {
            var bag = new List<TileTypeSO>(orderTypes.Count * OrderData.RequiredCount);
            foreach (var t in orderTypes)
                for (int i = 0; i < OrderData.RequiredCount; i++) bag.Add(t);
            return bag;
        }

        /// <summary>
        /// Place anchors for every layer such that each layer-(L+1) anchor
        /// overlaps at least one layer-L anchor (|dx|<=1 and |dy|<=1), so
        /// upper tiles physically cover lower ones. Layer 0 anchors are
        /// scattered randomly. Returns null on failure (e.g. an empty lower
        /// layer with non-empty layer above it).
        /// </summary>
        private static List<(int x, int y)>[] TryPlaceAnchors(int[] tilesPerLayer, int gridW, int gridH, System.Random rng)
        {
            int L = tilesPerLayer.Length;
            int maxX = gridW - 1;
            int maxY = gridH - 1;
            if (maxX < 0 || maxY < 0) return null;

            var result = new List<(int x, int y)>[L];
            for (int l = 0; l < L; l++) result[l] = new List<(int x, int y)>();

            // Layer 0: spread randomly. Reject anchors that overlap (|Δ|<=1)
            // an existing same-layer anchor — same-layer tiles must not collide.
            for (int i = 0; i < tilesPerLayer[0]; i++)
            {
                int tries = 64;
                while (tries-- > 0)
                {
                    int x = rng.Next(maxX + 1);
                    int y = rng.Next(maxY + 1);
                    if (!OverlapsSameLayer(result[0], x, y)) { result[0].Add((x, y)); break; }
                }
                if (tries < 0) return null;
            }

            for (int l = 1; l < L; l++)
            {
                int n = tilesPerLayer[l];
                if (n == 0) continue;
                if (result[l - 1].Count == 0) return null; // can't stack on empty layer

                for (int i = 0; i < n; i++)
                {
                    int tries = 64;
                    while (tries-- > 0)
                    {
                        var basis = result[l - 1][rng.Next(result[l - 1].Count)];
                        int x = Mathf.Clamp(basis.x + rng.Next(3) - 1, 0, maxX);
                        int y = Mathf.Clamp(basis.y + rng.Next(3) - 1, 0, maxY);
                        if (!OverlapsSameLayer(result[l], x, y)) { result[l].Add((x, y)); break; }
                    }
                    if (tries < 0) return null;
                }
            }
            return result;
        }

        private static bool OverlapsSameLayer(List<(int x, int y)> anchors, int x, int y)
        {
            foreach (var a in anchors)
                if (Mathf.Abs(a.x - x) <= 1 && Mathf.Abs(a.y - y) <= 1) return true;
            return false;
        }

        private static Result Fail(string msg) => new Result { success = false, message = msg };
    }
}
#endif
