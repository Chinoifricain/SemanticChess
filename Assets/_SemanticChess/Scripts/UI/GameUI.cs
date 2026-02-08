using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _turnIndicator;
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private Button _rematchButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private Button _backToMenuButton;

    [Header("Tooltip")]
    [SerializeField] private TooltipUI _tooltipPrefab;

    private bool _isThinking;
    private bool _isOnline;

    public Canvas GetCanvas() => _panel.GetComponentInParent<Canvas>();
    public TooltipUI TooltipPrefab => _tooltipPrefab;

    private void Awake()
    {
        _rematchButton.onClick.AddListener(() => GameManager.Instance.RequestRematch());
        _menuButton.onClick.AddListener(() => GameManager.Instance.EndMatch());
        _backToMenuButton.onClick.AddListener(() => GameManager.Instance.EndMatch());
    }

    public void Show()
    {
        _panel.SetActive(true);
        _gameOverPanel.SetActive(false);
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }

    public void SetOnlineMode(bool online)
    {
        _isOnline = online;
    }

    public void ShowThinking()
    {
        _isThinking = true;
        _turnIndicator.text = "AI is thinking...";
    }

    public void HideThinking()
    {
        _isThinking = false;
    }

    public void UpdateTurn(PieceColor color)
    {
        if (_isThinking) return;
        _turnIndicator.text = color == PieceColor.White ? "White's Turn" : "Black's Turn";
    }

    public void UpdateOnlineTurn(bool isLocalTurn)
    {
        _turnIndicator.text = isLocalTurn ? "Your Turn" : "Opponent's Turn";
    }

    public void ShowOpponentDisconnected(bool disconnected)
    {
        if (disconnected)
            _turnIndicator.text = "Opponent disconnected...";
    }

    public void ShowWaitingRematch()
    {
        _resultText.text = "Waiting for opponent...";
    }

    public void ShowGameOver(MatchResult result)
    {
        _gameOverPanel.SetActive(true);

        if (_isOnline)
        {
            // Online: show win/loss from local player perspective
            var localColor = GameManager.Instance.RoomManager.LocalColor;
            bool localWon = result.Winner == localColor;

            switch (result.Outcome)
            {
                case MatchOutcome.Checkmate:
                    _resultText.text = localWon ? "Checkmate!\nYou win!" : "Checkmate!\nYou lose...";
                    break;
                case MatchOutcome.Stalemate:
                    _resultText.text = "Stalemate!\nDraw";
                    break;
                case MatchOutcome.KingDestroyed:
                    _resultText.text = localWon ? "You win!\nKing destroyed" : "You lose...\nKing destroyed";
                    break;
            }
        }
        else
        {
            switch (result.Outcome)
            {
                case MatchOutcome.Checkmate:
                    _resultText.text = $"Checkmate!\n{result.Winner} wins!";
                    break;
                case MatchOutcome.Stalemate:
                    _resultText.text = "Stalemate!\nDraw";
                    break;
                case MatchOutcome.KingDestroyed:
                    _resultText.text = $"{result.Winner} wins!\nKing destroyed";
                    break;
            }
        }
    }

    public void HideGameOver()
    {
        _gameOverPanel.SetActive(false);
    }

    public void SetBackToMenuVisible(bool visible)
    {
        _backToMenuButton.gameObject.SetActive(visible);
    }

    public void SetTurnText(string text)
    {
        _turnIndicator.text = text;
    }
}
