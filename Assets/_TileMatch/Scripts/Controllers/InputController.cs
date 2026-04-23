using UnityEngine;
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

            TileController best = null;
            int bestLayer = int.MinValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var tc = hits[i].GetComponentInParent<TileController>();
                if (tc == null || tc.Model == null) continue;
                if (tc.Model.IsBlocked) continue;

                if (tc.Model.Layer > bestLayer)
                {
                    best = tc;
                    bestLayer = tc.Model.Layer;
                }
            }

            if (best != null) best.OnTapped();
        }
    }
}
