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

        [Header("Blocked Fade")]
        [SerializeField] private float blockedAlpha = 0.5f;
        [SerializeField] private float fadeDuration = 0.15f;

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

        private const int TweenIdTap = 1;
        private const int TweenIdMove = 2;
        private const int TweenIdScale = 3;
        private const int TweenIdGrow = 4;

        private TileModel _model;
        private Vector3 _rootLocalScale;
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
            ResetVisualsForPooling();

            _model = model;
            if (iconRenderer != null && model.TileType != null)
                iconRenderer.sprite = model.TileType.icon;

            SetBlockedInstant(model.IsBlocked);

            _model.OnBlockedChanged += HandleBlockedChanged;
        }

        /// <summary>
        /// Small feedback punch on the icon. Used for "became free" hints —
        /// not for the tap-to-collect flow (that uses PlayTapAndFly instead).
        /// </summary>
        public void AnimateTap()
        {
            if (!gameObject.activeInHierarchy || iconRenderer == null) return;
            var t = iconRenderer.transform;

            DOTween.Kill(TweenIdTap);
            t.localScale = _iconLocalScale;
            t.DOPunchScale(_iconLocalScale * 0.2f, 0.18f, 6, 0.5f)
                .SetId(TweenIdTap);
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
            float arrivalScaleFactor = destination == TileDestination.Order
                ? orderArrivalScale
                : rackArrivalScale;

            if (!gameObject.activeInHierarchy || iconRenderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            DOTween.Kill(TweenIdGrow);
            DOTween.Kill(TweenIdMove);
            DOTween.Kill(TweenIdScale);

            // Reset to a known pose in case an earlier tween was interrupted.
            transform.localScale = _rootLocalScale;
            iconRenderer.transform.localScale = _iconLocalScale;

            // Phase 1: grow the whole tile (base + icon).
            transform.DOScale(_rootLocalScale * tapGrowScale, tapGrowDuration)
                .SetEase(tapGrowEase)
                .SetId(TweenIdGrow)
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
                .SetId(TweenIdMove)
                .OnComplete(() => onComplete?.Invoke());

            iconT.DOScale(arrivalLocalScale, moveToTargetDuration)
                .SetEase(moveEase)
                .SetId(TweenIdScale);
        }

        /// <summary>Subtle bump when a tile becomes free (higher tile removed).</summary>
        public void AnimateUnblock()
        {
            if (!gameObject.activeInHierarchy) return;
            AnimateTap();
        }

        private void HandleBlockedChanged(bool blocked)
        {
            if (iconRenderer != null)
            {
                iconRenderer.DOKill();
                iconRenderer.DOFade(blocked ? blockedAlpha : 1f, fadeDuration);
            }
            if (!blocked) AnimateUnblock();
        }

        private void SetBlockedInstant(bool blocked)
        {
            if (iconRenderer == null) return;
            var c = iconRenderer.color;
            c.a = blocked ? blockedAlpha : 1f;
            iconRenderer.color = c;
        }

        /// <summary>Restore base + icon pose so a pooled tile looks fresh on reuse.</summary>
        private void ResetVisualsForPooling()
        {
            transform.localScale = _rootLocalScale;
            if (baseRenderer != null) baseRenderer.enabled = true;
            if (breakParticles != null)
                breakParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (iconRenderer != null)
            {
                var iconT = iconRenderer.transform;
                iconT.localPosition = _iconLocalPos;
                iconT.localScale = _iconLocalScale;
                iconT.localRotation = Quaternion.identity;
                var c = iconRenderer.color;
                c.a = 1f;
                iconRenderer.color = c;
                if (!string.IsNullOrEmpty(_iconOriginalSortingLayer))
                    iconRenderer.sortingLayerName = _iconOriginalSortingLayer;
                iconRenderer.sortingOrder = _iconOriginalSortingOrder;
            }
        }

        private void KillAllTweens()
        {
            DOTween.Kill(TweenIdTap);
            DOTween.Kill(TweenIdMove);
            DOTween.Kill(TweenIdScale);
            DOTween.Kill(TweenIdGrow);
            if (iconRenderer != null) iconRenderer.DOKill();
            transform.DOKill();
        }

        private void Unsubscribe()
        {
            if (_model != null)
                _model.OnBlockedChanged -= HandleBlockedChanged;
            _model = null;
        }

        private void OnDisable()
        {
            KillAllTweens();
            Unsubscribe();
        }
    }
}
