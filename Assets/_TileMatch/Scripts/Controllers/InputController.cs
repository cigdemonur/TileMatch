using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Converts taps/clicks into TileController.OnTapped() calls.
    /// Uses Unity's new InputSystem. Picks the topmost UNBLOCKED tile at the
    /// tap point to handle overlapping 2x2 tile footprints correctly.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        [Header("Scene Refs")]
        [SerializeField] private Camera gameCamera;

        [Header("Input")]
        [Tooltip("Physics layers that contain tile colliders.")]
        [SerializeField] private LayerMask tileLayerMask = ~0;

        private void Reset()
        {
            gameCamera = Camera.main;
        }

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void Update()
        {
            // Block input outside Playing state so taps don't fire on Win/Fail.
            if (GameController.Instance != null &&
                GameController.Instance.State != GameState.Playing)
                return;

            bool tapped = false;
            Vector2 screenPos = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
                screenPos = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null &&
                     Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                tapped = true;
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }

            // Ignore taps that land on UI (rack / order tray / win-fail panels).
            if (tapped && EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject()) return;

            if (tapped) HandleTap(screenPos);
        }

        /// <summary>
        /// Screen-space tap -> world-space overlap -> pick topmost unblocked tile.
        /// </summary>
        private void HandleTap(Vector2 screenPos)
        {
            if (gameCamera == null) return;

            Vector3 worldPos = gameCamera.ScreenToWorldPoint(screenPos);
            Vector2 point = new Vector2(worldPos.x, worldPos.y);

            var hits = Physics2D.OverlapPointAll(point, tileLayerMask);
            if (hits == null || hits.Length == 0) return;

            // Pick the unblocked tile whose model is on the highest layer.
            // If multiple candidates share the top layer (common — most demo
            // levels are single-layer), break ties by pixel-distance from the
            // tap point to the tile's center so adjacent overlapping colliders
            // don't steal the tap.
            TileController best = null;
            int bestLayer = int.MinValue;
            float bestDistSqr = float.PositiveInfinity;

            // Track the topmost blocked tile too so we can shake-feedback it
            // when no unblocked candidate is available under the tap.
            TileController bestBlocked = null;
            int bestBlockedLayer = int.MinValue;
            float bestBlockedDistSqr = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                var tc = hits[i].GetComponentInParent<TileController>();
                if (tc == null || tc.Model == null) continue;

                int layer = tc.Model.Layer;
                float distSqr = ((Vector2)tc.transform.position - point).sqrMagnitude;

                if (tc.Model.IsBlocked)
                {
                    bool betterB =
                        layer > bestBlockedLayer ||
                        (layer == bestBlockedLayer && distSqr < bestBlockedDistSqr);
                    if (betterB)
                    {
                        bestBlocked = tc;
                        bestBlockedLayer = layer;
                        bestBlockedDistSqr = distSqr;
                    }
                    continue;
                }

                bool better =
                    layer > bestLayer ||
                    (layer == bestLayer && distSqr < bestDistSqr);

                if (better)
                {
                    best = tc;
                    bestLayer = layer;
                    bestDistSqr = distSqr;
                }
            }

            if (best != null) best.OnTapped();
            else if (bestBlocked != null) bestBlocked.OnBlockedTapped();
        }
    }
}
