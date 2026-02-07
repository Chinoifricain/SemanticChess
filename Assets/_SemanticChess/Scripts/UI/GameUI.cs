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

    public void UpdateTurn(PieceColor color)
    {
        _turnIndicator.text = color == PieceColor.White ? "White's Turn" : "Black's Turn";
    }

    public void ShowGameOver(MatchResult result)
    {
        _gameOverPanel.SetActive(true);

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

    public void HideGameOver()
    {
        _gameOverPanel.SetActive(false);
    }
}
