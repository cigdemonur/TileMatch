using UnityEngine;
using TileMatch.Models;
using TileMatch.Views;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Glue component on each tile GameObject.
    /// Links a TileModel (state) with its TileView (visuals).
    /// Receives taps from InputController and routes them to GameController.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TileController : MonoBehaviour
    {
        [Tooltip("TileView on the same GameObject (or a child). Renders the model.")]
        [SerializeField] private TileView view;

        public TileModel Model { get; private set; }
        public TileView View => view;

        private void Awake()
        {
            if (view == null) view = GetComponent<TileView>();
        }

        /// <summary>
        /// Called by BoardController after spawning this tile.
        /// Binds the model and triggers the initial view render.
        /// </summary>
        public void Bind(TileModel model)
        {
            Model = model;
            if (view != null) view.Render(model);
        }

        /// <summary>
        /// Called by InputController when this tile is the topmost unblocked
        /// tile under the tap point.
        /// </summary>
        public void OnTapped()
        {
            if (Model == null || Model.IsBlocked) return;
            if (GameController.Instance != null)
                GameController.Instance.RouteTile(Model);
        }

        /// <summary>
        /// Called by InputController when the topmost tile under the tap is
        /// blocked. Plays a side-to-side shake to signal "can't tap me yet".
        /// </summary>
        public void OnBlockedTapped()
        {
            if (Model == null) return;
            if (view != null) view.AnimateBlockedShake();
        }
    }
}
