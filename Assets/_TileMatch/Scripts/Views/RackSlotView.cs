using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TileMatch.Data;

namespace TileMatch.Views
{
    /// <summary>
    /// One visible rack slot. Shows the empty-slot background at all times;
    /// when a tile is parked here, the child Icon image turns on with the
    /// tile's sprite. When the rack slot is cleared (auto-collected into an
    /// order), the Icon turns off again.
    ///
    /// RackView owns an array of these (size == GameConfigSO.rackCapacity).
    /// </summary>
    public class RackSlotView : MonoBehaviour
    {
        [Header("Background")]
        [Tooltip("Always-visible empty-slot background. Never toggled.")]
        [SerializeField] private Image background;

        [Header("Tile Icon (overlay)")]
        [Tooltip("Child Image that displays the parked tile's icon. Starts disabled.")]
        [SerializeField] private Image icon;

        /// <summary>True if this slot currently shows a tile icon.</summary>
        public bool IsFilled => icon != null && icon.gameObject.activeSelf;

        private void Awake()
        {
            // Start empty: background on, icon off.
            if (icon != null) icon.gameObject.SetActive(false);
        }

        /// <summary>Show the given ingredient icon over the empty background.</summary>
        public void Fill(TileTypeSO tileType)
        {
            if (icon == null) return;

            icon.sprite = tileType != null ? tileType.icon : null;
            icon.gameObject.SetActive(tileType != null);
        }

        /// <summary>Hide the icon so only the empty background remains.</summary>
        public void ClearSlot()
        {
            if (icon == null) return;
            icon.sprite = null;
            icon.gameObject.SetActive(false);
        }

        /// <summary>World position of the icon (used as a tween target / origin).</summary>
        public Vector3 GetIconWorldPosition()
        {
            if (icon != null) return icon.transform.position;
            return transform.position;
        }

        /// <summary>
        /// Fly the icon to <paramref name="worldTarget"/>, then clear the slot
        /// and restore the icon's local position so the next Fill renders in place.
        /// </summary>
        public void AnimateIconTo(Vector3 worldTarget, float duration, Action onArrive)
        {
            if (icon == null) { onArrive?.Invoke(); return; }

            var t = icon.transform;
            var originalLocal = t.localPosition;
            t.DOKill();
            t.DOMove(worldTarget, duration).SetEase(Ease.InOutQuad)
                .OnComplete(() =>
                {
                    ClearSlot();
                    t.localPosition = originalLocal;
                    onArrive?.Invoke();
                });
        }
    }
}
