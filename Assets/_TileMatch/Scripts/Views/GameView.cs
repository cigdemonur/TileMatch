using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
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

        [Tooltip("Button on the Win panel that loads the next level.")]
        [SerializeField] private Button nextLevelButton;

        [Tooltip("Label on the Next-Level button (e.g. 'Level 5'). Leave blank to skip.")]
        [SerializeField] private TMP_Text nextLevelLabel;
        [Tooltip("Format string used for the Next-Level button label. {0} is the next level number.")]
        [SerializeField] private string nextLevelLabelFormat = "Level {0}";

        [Header("Panel Animation")]
        [SerializeField] private float panelPopDuration = 0.35f;
        [SerializeField] private Ease panelPopEase = Ease.OutBack;
        [SerializeField] private float panelStartScale = 0.6f;

        [Header("HUD")]
        [Tooltip("Label that shows the current level number, e.g. 'Level 3'. Optional.")]
        [SerializeField] private TMP_Text levelLabel;
        [Tooltip("Format string used for the level label. {0} is the level number.")]
        [SerializeField] private string levelLabelFormat = "Level {0}";

        [Tooltip("Label on the Fail panel, e.g. 'Level 3 Failed'. Optional.")]
        [SerializeField] private TMP_Text failLevelLabel;
        [Tooltip("Format string used for the Fail panel label. {0} is the current level number.")]
        [SerializeField] private string failLevelLabelFormat = "Level {0} Failed";

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

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
                nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
            }

            // Initial render — a fresh controller is in Playing state
            HandleStateChanged(_game.State);
        }

        /// <summary>Update the level number shown in the HUD.</summary>
        public void SetLevelNumber(int levelNumber)
        {
            if (levelLabel != null)
                levelLabel.text = string.Format(levelLabelFormat, levelNumber);
            if (failLevelLabel != null)
                failLevelLabel.text = string.Format(failLevelLabelFormat, levelNumber);
        }

        private void HandleStateChanged(GameState state)
        {
            if (winPanel != null)
            {
                bool show = state == GameState.Win;
                winPanel.SetActive(show);
                if (show) PlayPanelPop(winPanel.transform);
            }
            if (failPanel != null)
            {
                bool show = state == GameState.Fail;
                failPanel.SetActive(show);
                if (show) PlayPanelPop(failPanel.transform);
            }

            if (state == GameState.Win) RefreshNextLevelButton();
        }

        private void PlayPanelPop(Transform t)
        {
            t.DOKill();
            t.localScale = Vector3.one * panelStartScale;
            t.DOScale(Vector3.one, panelPopDuration).SetEase(panelPopEase);
        }

        private void RefreshNextLevelButton()
        {
            var next = _game != null ? _game.PeekNextLevel() : null;
            bool hasNext = next != null;

            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(hasNext);
            if (nextLevelLabel != null && hasNext)
                nextLevelLabel.text = string.Format(nextLevelLabelFormat, next.levelNumber);
        }

        private void HandleRetryClicked()
        {
            if (_game != null) _game.RestartLevel();
        }

        private void HandleNextLevelClicked()
        {
            if (_game != null) _game.LoadNextLevel();
        }

        private void OnDisable()
        {
            if (_game != null)
                _game.OnStateChanged -= HandleStateChanged;

            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetryClicked);
            if (nextLevelButton != null)
                nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
        }
    }
}
