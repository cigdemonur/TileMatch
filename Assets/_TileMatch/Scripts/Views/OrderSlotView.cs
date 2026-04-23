using UnityEngine;
using UnityEngine.UI;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// One visible order slot in the tray.
    /// Layout: a rectangular panel containing 3 icon images (laid out left-to-right)
    /// of the requested ingredient. Each icon starts faded; as tiles are collected
    /// the matching icon becomes full-alpha and a green tick overlay turns on.
    /// Subscribes to OrderModel.OnProgressChanged / OnCompleted while bound.
    /// </summary>
    public class OrderSlotView : MonoBehaviour
    {
        [Header("Icons")]
        [Tooltip("One Image per required tile (size == OrderData.RequiredCount, currently 3). All three show the same ingredient sprite.")]
        [SerializeField] private Image[] iconImages;

        [Tooltip("Green tick overlay per icon (size must match iconImages). Shown once the matching icon is collected.")]
        [SerializeField] private GameObject[] tickOverlays;

        // Alpha for uncollected icons — set by OrderView.Bind via SetFadedAlpha
        // so the faded look is a tray-level style setting, not per-slot.
        private float _fadedAlpha = 0.4f;

        private OrderModel _order;

        /// <summary>Called by OrderView to apply the tray-level faded alpha.</summary>
        public void SetFadedAlpha(float alpha)
        {
            _fadedAlpha = Mathf.Clamp01(alpha);
            if (_order != null) RefreshProgress(_order.CollectedCount);
        }

        /// <summary>Wire this slot to an order and do an initial render.</summary>
        public void Bind(OrderModel order)
        {
            Unbind();

            _order = order;
            if (_order == null) return;

            ApplyIconSprites(_order.TileType != null ? _order.TileType.icon : null);
            RefreshProgress(_order.CollectedCount);

            _order.OnProgressChanged += HandleProgressChanged;
            _order.OnCompleted += HandleCompleted;
        }

        /// <summary>Unsubscribe and reset visuals.</summary>
        public void Unbind()
        {
            if (_order != null)
            {
                _order.OnProgressChanged -= HandleProgressChanged;
                _order.OnCompleted -= HandleCompleted;
            }
            _order = null;

            ApplyIconSprites(null);
            RefreshProgress(0);
        }

        /// <summary>
        /// World position where the (collectedIndex)-th tile should land.
        /// collectedIndex is 0-based; pass CollectedCount BEFORE calling Collect()
        /// so the tween aims at the next empty icon.
        /// </summary>
        public Vector3 GetIconWorldPosition(int collectedIndex)
        {
            if (iconImages == null || collectedIndex < 0 || collectedIndex >= iconImages.Length)
                return transform.position;
            var icon = iconImages[collectedIndex];
            return icon != null ? icon.transform.position : transform.position;
        }

        private void HandleProgressChanged(int collected)
        {
            RefreshProgress(collected);
            // TODO: small punch-scale on the newly-activated icon
        }

        private void HandleCompleted()
        {
            // TODO: burst animation, then OrderController will call OrderView.ClearSlot
        }

        private void ApplyIconSprites(Sprite sprite)
        {
            if (iconImages == null) return;
            for (int i = 0; i < iconImages.Length; i++)
            {
                if (iconImages[i] != null) iconImages[i].sprite = sprite;
            }
        }

        private void RefreshProgress(int collected)
        {
            int count = iconImages != null ? iconImages.Length : 0;
            for (int i = 0; i < count; i++)
            {
                bool isCollected = i < collected;
                SetIconCollected(i, isCollected);
            }
        }

        private void SetIconCollected(int index, bool collected)
        {
            if (iconImages != null && index < iconImages.Length && iconImages[index] != null)
            {
                var img = iconImages[index];
                var c = img.color;
                c.a = collected ? 1f : _fadedAlpha;
                img.color = c;
            }

            if (tickOverlays != null && index < tickOverlays.Length && tickOverlays[index] != null)
            {
                tickOverlays[index].SetActive(collected);
            }
        }

        private void OnDisable() => Unbind();
    }
}
