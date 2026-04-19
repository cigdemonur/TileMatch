using UnityEngine;
using UnityEngine.UI;
using TileMatch.Controllers;

namespace TileMatch.Views
{
    /// <summary>
    /// Top-level HUD view: shows the Win and Fail panels and wires the Retry
    /// button. Subscribes to GameController.OnStateChanged and swaps panels.
    ///
    /// BoardView / OrderView / RackView are owned by their respective
    /// controllers; this view only handles the end-of-run screens plus any
    /// shared HUD (timer, moves-left, etc. — not in MVP).
    /// </summary>
    public class GameView : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Shown when every order is complete.")]
        [SerializeField] private GameObject winPanel;

        [Tooltip("Shown when the rack overflows.")]
        [SerializeField] private GameObject failPanel;

        [Header("Buttons")]
        [Tooltip("Retry button on both Win and Fail panels (wire both to the same handler).")]
        [SerializeField] private Button retryButton;

        private GameController _game;

        /// <summary>Called by GameController in Awake/Start after the controller wakes.</summary>
        public void Bind(GameController game)
        {
            if (_game != null)
                _game.OnStateChanged -= HandleStateChanged;

            _game = game;
            if (_game == null) return;

            _game.OnStateChanged += HandleStateChanged;

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(HandleRetryClicked);
                retryButton.onClick.AddListener(HandleRetryClicked);
            }

            // Initial render — a fresh controller is in Playing state
            HandleStateChanged(_game.State);
        }

        private void HandleStateChanged(GameState state)
        {
            if (winPanel != null) winPanel.SetActive(state == GameState.Win);
            if (failPanel != null) failPanel.SetActive(state == GameState.Fail);

            // TODO: scale-in tween on whichever panel just appeared (DOTween)
        }

        private void HandleRetryClicked()
        {
            if (_game != null) _game.RestartLevel();
        }

        private void OnDisable()
        {
            if (_game != null)
                _game.OnStateChanged -= HandleStateChanged;

            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetryClicked);
        }
    }
}
