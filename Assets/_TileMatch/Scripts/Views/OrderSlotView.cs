using UnityEngine;
using UnityEngine.UI;
using TileMatch.Data;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>
    /// One visible order slot in the tray.
    /// Shows the target tile icon and N progress pips (N == OrderData.RequiredCount).
    /// Subscribes to OrderModel.OnProgressChanged / OnCompleted while bound.
    /// </summary>
    public class OrderSlotView : MonoBehaviour
    {
        [Header("Icon")]
        [SerializeField] private Image iconImage;

        [Header("Progress Pips")]
        [Tooltip("One pip per required tile (size == OrderData.RequiredCount, currently 3).")]
        [SerializeField] private Image[] pips;

        [Tooltip("Sprite shown on a filled pip.")]
        [SerializeField] private Sprite pipFilledSprite;

        [Tooltip("Sprite shown on an empty pip.")]
        [SerializeField] private Sprite pipEmptySprite;

        private OrderModel _order;

        /// <summary>Wire this slot to an order and do an initial render.</summary>
        public void Bind(OrderModel order)
        {
            Unbind();

            _order = order;
            if (_order == null) return;

            if (iconImage != null && _order.TileType != null)
                iconImage.sprite = _order.TileType.icon;

            RefreshPips(_order.CollectedCount);

            _order.OnProgressChanged += HandleProgressChanged;
            _order.OnCompleted += HandleCompleted;
        }

        /// <summary>Unsubscribe and clear visuals.</summary>
        public void Unbind()
        {
            if (_order != null)
            {
                _order.OnProgressChanged -= HandleProgressChanged;
                _order.OnCompleted -= HandleCompleted;
            }
            _order = null;

            if (iconImage != null) iconImage.sprite = null;
            RefreshPips(0);
        }

        private void HandleProgressChanged(int collected)
        {
            RefreshPips(collected);
            // TODO: small punch-scale on the icon or the newly-filled pip
        }

        private void HandleCompleted()
        {
            // TODO: burst animation, then OrderController will call OrderView.ClearSlot
        }

        private void RefreshPips(int collected)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].sprite = i < collected ? pipFilledSprite : pipEmptySprite;
            }
        }

        private void OnDisable() => Unbind();
    }
}
