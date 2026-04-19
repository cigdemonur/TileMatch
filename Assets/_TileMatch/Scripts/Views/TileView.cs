using System;
using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// Visual representation of a single tile.
    /// Subscribes to TileModel.OnBlockedChanged and plays DOTween animations.
    /// Pure render — never mutates the model.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [Header("Sprite Refs")]
        [Tooltip("Renders the icon (apple, banana, etc.). Swapped per TileType.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("Optional: the black base tile. Stays the same across all tiles.")]
        [SerializeField] private SpriteRenderer baseRenderer;

        [Header("Animation Timings")]
        [SerializeField] private float blockedAlpha = 0.5f;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float moveToTargetDuration = 0.35f;

        private TileModel _model;

        /// <summary>
        /// Initial render from the model and subscribe to its events.
        /// Called by TileController.Bind after spawn.
        /// </summary>
        public void Render(TileModel model)
        {
            Unsubscribe();

            _model = model;
            if (iconRenderer != null && model.TileType != null)
                iconRenderer.sprite = model.TileType.icon;

            // Instant initial state (no tween on first render)
            SetBlockedVisual(model.IsBlocked, animated: false);

            _model.OnBlockedChanged += HandleBlockedChanged;
        }

        /// <summary>Punch-scale tween played on tap. Fire-and-forget.</summary>
        public void AnimateTap()
        {
            // TODO: transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 6, 0.5f);
        }

        /// <summary>Arc tween to a world-space target (order tray or rack slot).</summary>
        public void AnimateMoveToTarget(Vector3 worldTarget, Action onComplete = null)
        {
            // TODO: DOPath arc using a bezier through a midpoint above the direct line
            // TODO: duration = moveToTargetDuration, ease OutBack
            // TODO: onComplete?.Invoke() at end
            onComplete?.Invoke(); // placeholder so callers don't hang
        }

        /// <summary>Subtle glow/scale when a tile becomes free (higher tile removed).</summary>
        public void AnimateUnblock()
        {
            // TODO: brief scale bump + color flash
        }

        private void HandleBlockedChanged(bool blocked)
        {
            SetBlockedVisual(blocked, animated: true);
            if (!blocked) AnimateUnblock();
        }

        private void SetBlockedVisual(bool blocked, bool animated)
        {
            // TODO: tween iconRenderer.color.a between 1.0 and blockedAlpha over fadeDuration
            // TODO: also grey-tint via color multiplier if blocked
            // Placeholder (instant): set alpha directly.
            if (iconRenderer != null)
            {
                var c = iconRenderer.color;
                c.a = blocked ? blockedAlpha : 1f;
                iconRenderer.color = c;
            }
        }

        private void Unsubscribe()
        {
            if (_model != null)
                _model.OnBlockedChanged -= HandleBlockedChanged;
            _model = null;
        }

        private void OnDisable() => Unsubscribe();
    }
}
