using UnityEngine;
using TileMatch.Models;

namespace TileMatch.Controllers
{
    /// <summary>
    /// Thin wrapper around RackModel. Exposes TryAdd to GameController
    /// and handles the "rack full -> Fail" signal.
    /// The RackModel itself is owned by GameController and injected here.
    /// </summary>
    public class RackController : MonoBehaviour
    {
        private RackModel _rack;

        /// <summary>Called by GameController once the RackModel is created.</summary>
        public void Bind(RackModel rack)
        {
            _rack = rack;
        }

        /// <summary>
        /// Try to place a tile in the rack.
        /// Returns false and triggers Fail if the rack is already full.
        /// </summary>
        public bool TryAdd(TileModel tile)
        {
            // TODO: if (_rack.TryAdd(tile)) return true;
            // TODO: else -> GameController.Instance.SetState(GameState.Fail); return false;
            return false;
        }

        public void Reset()
        {
            // TODO: _rack.Clear();
            // Called by GameController.RestartLevel
        }
    }
}
