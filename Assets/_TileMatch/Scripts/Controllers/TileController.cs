using UnityEngine;
using TileMatch.Models;

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
        // Forward reference — TileView lives in the Views layer (to be written).
        // Using object for now so this stub compiles standalone; swap to TileView
        // once the Views layer exists.
        // TODO: change to: [SerializeField] private TileMatch.Views.TileView view;

        public TileModel Model { get; private set; }

        /// <summary>
        /// Called by BoardController after spawning this tile.
        /// Binds the model and triggers the initial view render.
        /// </summary>
        public void Bind(TileModel model)
        {
            Model = model;
            // TODO: view.Render(model);
            // TODO: model.OnBlockedChanged += view.OnBlockedChanged; (or view subscribes itself)
        }

        /// <summary>
        /// Called by InputController when this tile is the topmost unblocked
        /// tile under the tap point.
        /// </summary>
        public void OnTapped()
        {
            if (Model == null || Model.IsBlocked) return;
            // TODO: GameController.Instance.RouteTile(Model);
        }
    }
}
