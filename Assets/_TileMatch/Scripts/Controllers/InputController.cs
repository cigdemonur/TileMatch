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

        private void Update()
        {
            // TODO: read tap/click from new InputSystem:
            //   Mouse.current.leftButton.wasPressedThisFrame
            //   Touchscreen.current.primaryTouch.press.wasPressedThisFrame
            // TODO: if tapped this frame -> HandleTap(pointerPosition);
        }

        /// <summary>
        /// Screen-space tap -> world-space raycast -> pick topmost unblocked tile.
        /// </summary>
        private void HandleTap(Vector2 screenPos)
        {
            if (gameCamera == null) return;

            // TODO: Vector3 worldPos = gameCamera.ScreenToWorldPoint(screenPos);
            // TODO: Collider2D[] hits = Physics2D.OverlapPointAll(worldPos, tileLayerMask);
            //       (or RaycastAll — either works for 2D)
            // TODO: scan hits, collect every TileController that isn't blocked
            // TODO: pick the one with the highest TileModel.Layer (topmost)
            // TODO: topTile.OnTapped();
        }
    }
}
