using System;
using DG.Tweening;
using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Views
{
    /// <summary>Where a tapped tile is heading — drives arrival size.</summary>
    public enum TileDestination
    {
        Order,
        Rack
    }


    /// <summary>
    /// Visual representation of a single tile.
    /// On tap: the whole tile (base + icon) grows briefly, then the base
    /// disappears and the icon flies to the target slot while shrinking to
    /// its arrival size. All visuals are reset on Render() so pooled tiles
    /// come back fresh.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [Header("Sprite Refs")]
        [Tooltip("Renders the icon (apple, banana, etc.). Swapped per TileType. Animated on tap.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("The base/background sprite. Hidden on tap so only the icon flies.")]
        [SerializeField] private SpriteRenderer baseRenderer;

        [Tooltip("Optional particle burst fired when the base 'breaks'. Play On Awake should be OFF on the prefab.")]
        [SerializeField] private ParticleSystem breakParticles;

        [Header("Blocked Tint")]
        [Tooltip("Color tint applied to base + icon when the tile is blocked (non-tappable). Alpha stays opaque.")]
        [SerializeField] private Color blockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private float fadeDuration = 0.06f;

        [Header("Blocked Shake")]
        [Tooltip("Horizontal shake amplitude (local units) when a blocked tile is tapped.")]
        [SerializeField] private float blockedShakeAmplitude = 0.12f;
        [Tooltip("Total duration of the blocked-shake wiggle.")]
        [SerializeField] private float blockedShakeDuration = 0.28f;
        [Tooltip("How many oscillations the shake performs over its duration. Higher = more wiggle.")]
        [SerializeField] private int blockedShakeVibrato = 10;

        [Header("Tap Grow (whole tile)")]
        [Tooltip("How much the whole tile scales up before the base breaks.")]
        [SerializeField] private float tapGrowScale = 1.2f;
        [SerializeField] private float tapGrowDuration = 0.12f;
        [SerializeField] private Ease tapGrowEase = Ease.OutBack;

        [Header("Fly To Target (icon only)")]
        [SerializeField] private float moveToTargetDuration = 0.32f;
        [Tooltip("Easing curve for the straight flight to the target.")]
        [SerializeField] private Ease moveEase = Ease.InOutSine;
        [Tooltip("Icon scale (multiplier of original local scale) when it lands in an order slot.")]
        [SerializeField] private float orderArrivalScale = 0.7f;
        [Tooltip("Icon scale (multiplier of original local scale) when it lands in a rack slot.")]
        [SerializeField] private float rackArrivalScale = 0.55f;
        [Tooltip("Sorting layer used while the icon is flying, so it renders on top of the UI canvas. Must exist in Project Settings -> Tags and Layers.")]
        [SerializeField] private string flyingSortingLayer = "UI";
        [Tooltip("Sorting order used during flight so the icon draws above any canvas on the same layer.")]
        [SerializeField] private int flyingSortingOrder = 32000;

        private TileModel _model;
        private bool _isFlying;
        public bool IsFlying => _isFlying;
        private Vector3 _rootLocalScale;
        private bool _isShaking;
        private Vector3 _iconLocalPos;
        private Vector3 _iconLocalScale;
        private string _iconOriginalSortingLayer;
        private int _iconOriginalSortingOrder;

        private void Awake()
        {
            _rootLocalScale = transform.localScale;
            if (iconRenderer != null)
            {
                _iconLocalPos = iconRenderer.transform.localPosition;
                _iconLocalScale = iconRenderer.transform.localScale;
                _iconOriginalSortingLayer = iconRenderer.sortingLayerName;
                _iconOriginalSortingOrder = iconRenderer.sortingOrder;
            }
        }

        /// <summary>
        /// Initial render from the model and subscribe to its events.
        /// Called by TileController.Bind after spawn. Also resets visuals so
        /// a recycled pooled view starts fresh.
        /// </summary>
        public void Render(TileModel model)
        {
            Unsubscribe();
            KillAllTweens();
            _isFlying = false;
            _isShaking = false;
            ResetVisualsForPooling();

            _model = model;
            if (iconRenderer != null && model.TileType != null)
                iconRenderer.sprite = model.TileType.icon;

            ApplyLayerSorting(model.Layer);

            SetBlockedInstant(model.IsBlocked);

            _model.OnBlockedChanged += HandleBlockedChanged;
            _model.OnLayerChanged += HandleLayerChanged;
        }

        private void HandleLayerChanged(int newLayer)
        {
            ApplyLayerSorting(newLayer);
        }

        /// <summary>
        /// Layer is depth-from-top: 0 = top of stack. So lower layer needs
        /// higher sortingOrder to render above the tile beneath. The icon
        /// always sits one above its own base, preventing layer-N base
        /// drawing above layer-N icon.
        /// </summary>
        private void ApplyLayerSorting(int layer)
        {
            int layerOrderBase = -layer * 10;
            if (baseRenderer != null) baseRenderer.sortingOrder = layerOrderBase;
            if (iconRenderer != null) iconRenderer.sortingOrder = layerOrderBase + 1;
        }

        /// <summary>
        /// Small feedback punch on the icon. Used for "became free" hints —
        /// not for the tap-to-collect flow (that uses PlayTapAndFly instead).
        /// </summary>
        public void AnimateTap()
        {
            if (!gameObject.activeInHierarchy || iconRenderer == null) return;
            var t = iconRenderer.transform;

            t.DOKill();
            t.localScale = _iconLocalScale;
            t.DOPunchScale(_iconLocalScale * 0.2f, 0.18f, 6, 0.5f);
        }

        /// <summary>
        /// Full tap-to-collect sequence:
        ///   1) whole tile grows (base + icon together)
        ///   2) base disappears (future: break VFX)
        ///   3) icon flies to worldTarget while shrinking to arrivalScaleFactor.
        ///
        /// Caller says where the tile is going (Order / Rack) and the arrival
        /// size comes from the inspector fields on this view.
        ///
        /// onComplete fires after the icon lands. The whole view gets returned
        /// to the pool in that callback (via Board.RemoveTile).
        /// </summary>
        public void PlayTapAndFly(Vector3 worldTarget, TileDestination destination, Action onComplete = null)
        {
            // Ignore re-taps while a flight is already in progress so the tween
            // isn't restarted from the grown pose halfway through.
            if (_isFlying) return;

            float arrivalScaleFactor = destination == TileDestination.Order
                ? orderArrivalScale
                : rackArrivalScale;

            if (!gameObject.activeInHierarchy || iconRenderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _isFlying = true;
            transform.DOKill();
            iconRenderer.transform.DOKill();

            // Reset to a known pose in case an earlier tween was interrupted.
            transform.localScale = _rootLocalScale;
            iconRenderer.transform.localScale = _iconLocalScale;

            // Phase 1: grow the whole tile (base + icon).
            transform.DOScale(_rootLocalScale * tapGrowScale, tapGrowDuration)
                .SetEase(tapGrowEase)
                .OnComplete(() => BreakAndFly(worldTarget, arrivalScaleFactor, onComplete));
        }

        private void BreakAndFly(Vector3 worldTarget, float arrivalScaleFactor, Action onComplete)
        {
            if (iconRenderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Reset root scale back to normal and push the "grown" look onto the
            // icon itself, so the flying icon starts at the grown size.
            transform.localScale = _rootLocalScale;
            var iconT = iconRenderer.transform;
            iconT.localScale = _iconLocalScale * tapGrowScale;

            // Base disappears and the break particles fire in its place.
            if (baseRenderer != null) baseRenderer.enabled = false;
            if (breakParticles != null)
            {
                // Re-enable in case Stop Action / pool churn left the GO inactive.
                if (!breakParticles.gameObject.activeSelf)
                    breakParticles.gameObject.SetActive(true);
                breakParticles.Clear(true);
                breakParticles.Play(true);
            }

            // Bump the icon to a high sorting layer/order so it draws on top of
            // the UI canvas during the flight.
            if (!string.IsNullOrEmpty(flyingSortingLayer))
                iconRenderer.sortingLayerName = flyingSortingLayer;
            iconRenderer.sortingOrder = flyingSortingOrder;

            // Keep the icon's current Z so it doesn't drift behind any canvas plane.
            Vector3 flatTarget = new Vector3(worldTarget.x, worldTarget.y, iconT.position.z);
            Vector3 arrivalLocalScale = _iconLocalScale * arrivalScaleFactor;

            iconT.DOMove(flatTarget, moveToTargetDuration)
                .SetEase(moveEase)
                .OnComplete(() =>
                {
                    _isFlying = false;
                    onComplete?.Invoke();
                });

            iconT.DOScale(arrivalLocalScale, moveToTargetDuration)
                .SetEase(moveEase);
        }

        /// <summary>
        /// Side-to-side wiggle for "you tapped a blocked tile, can't take it yet".
        /// Captures the live localPosition as origin so it works at any board slot,
        /// and refuses to restart while a previous shake is still in progress.
        /// </summary>
        public void AnimateBlockedShake()
        {
            if (!gameObject.activeInHierarchy || _isFlying || _isShaking) return;

            var t = transform;
            t.DOKill();
            Vector3 origin = t.localPosition;
            _isShaking = true;

            // DOShakePosition with randomness=0 and fadeOut=true produces a
            // smooth, decaying horizontal sine wiggle that returns to origin.
            t.DOShakePosition(
                    blockedShakeDuration,
                    new Vector3(blockedShakeAmplitude, 0f, 0f),
                    vibrato: blockedShakeVibrato,
                    randomness: 0f,
                    snapping: false,
                    fadeOut: true)
                .OnComplete(() =>
                {
                    t.localPosition = origin;
                    _isShaking = false;
                });
        }

        /// <summary>Subtle bump when a tile becomes free (higher tile removed).</summary>
        public void AnimateUnblock()
        {
            if (!gameObject.activeInHierarchy) return;
            AnimateTap();
        }

        private void HandleBlockedChanged(bool blocked)
        {
            Color target = blocked ? blockedTint : Color.white;
            if (iconRenderer != null)
            {
                iconRenderer.DOKill();
                iconRenderer.DOColor(target, fadeDuration);
            }
            if (baseRenderer != null)
            {
                baseRenderer.DOKill();
                baseRenderer.DOColor(target, fadeDuration);
            }
        }

        private void SetBlockedInstant(bool blocked)
        {
            Color target = blocked ? blockedTint : Color.white;
            if (iconRenderer != null) iconRenderer.color = target;
            if (baseRenderer != null) baseRenderer.color = target;
        }

        /// <summary>Force the dimmed tint regardless of model state — used on Fail.</summary>
        public void ForceDim()
        {
            if (iconRenderer != null)
            {
                iconRenderer.DOKill();
                iconRenderer.color = blockedTint;
            }
            if (baseRenderer != null)
            {
                baseRenderer.DOKill();
                baseRenderer.color = blockedTint;
            }
        }

        /// <summary>Restore base + icon pose so a pooled tile looks fresh on reuse.</summary>
        private void ResetVisualsForPooling()
        {
            transform.localScale = _rootLocalScale;
            if (baseRenderer != null)
            {
                baseRenderer.enabled = true;
                baseRenderer.color = Color.white;
            }
            if (breakParticles != null)
                breakParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (iconRenderer != null)
            {
                var iconT = iconRenderer.transform;
                iconT.localPosition = _iconLocalPos;
                iconT.localScale = _iconLocalScale;
                iconT.localRotation = Quaternion.identity;
                iconRenderer.color = Color.white;
                iconRenderer.enabled = true;
                if (!string.IsNullOrEmpty(_iconOriginalSortingLayer))
                    iconRenderer.sortingLayerName = _iconOriginalSortingLayer;
                iconRenderer.sortingOrder = _iconOriginalSortingOrder;
            }
        }

        private void KillAllTweens()
        {
            transform.DOKill();
            if (iconRenderer != null)
            {
                iconRenderer.transform.DOKill();
                iconRenderer.DOKill();
            }
        }

        private void Unsubscribe()
        {
            if (_model != null)
            {
                _model.OnBlockedChanged -= HandleBlockedChanged;
                _model.OnLayerChanged -= HandleLayerChanged;
            }
            _model = null;
        }

        private void OnDisable()
        {
            KillAllTweens();
            Unsubscribe();
        }
    }
}
