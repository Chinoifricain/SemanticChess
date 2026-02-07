using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private ChessBoard _board;
    [SerializeField] private MenuUI _menuUI;
    [SerializeField] private GameUI _gameUI;

    private IGameMode _currentMode;

    public ChessBoard Board => _board;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _gameUI.Hide();
        _menuUI.Show();
    }

    private void Update()
    {
        _currentMode?.OnUpdate();
    }

    public void StartMatch(GameModeType modeType)
    {
        // Wait for board to be initialized
        if (!_board.IsInitialized) return;

        _menuUI.Hide();

        _board.ResetBoard();

        // Create game mode
        switch (modeType)
        {
            case GameModeType.Local:
                _currentMode = new LocalGameMode();
                break;
            case GameModeType.VsAI:
                _currentMode = new AIGameMode();
                break;
            case GameModeType.Online:
                _currentMode = new OnlineGameMode();
                break;
        }

        // Subscribe to board events
        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.Show();
        _gameUI.UpdateTurn(PieceColor.White);
    }

    public void EndMatch()
    {
        if (_currentMode != null)
        {
            _currentMode.OnDeactivate();
            _currentMode = null;
        }

        _board.OnTurnChanged -= OnTurnChanged;
        _board.OnGameOver -= OnGameOverEvent;

        _gameUI.Hide();
        _menuUI.Show();
    }

    public void RequestRematch()
    {
        if (_currentMode == null) return;

        var modeType = _currentMode.ModeType;
        _currentMode.OnDeactivate();
        _board.OnTurnChanged -= OnTurnChanged;
        _board.OnGameOver -= OnGameOverEvent;

        _board.ResetBoard();

        // Re-create fresh mode
        switch (modeType)
        {
            case GameModeType.Local:
                _currentMode = new LocalGameMode();
                break;
            case GameModeType.VsAI:
                _currentMode = new AIGameMode();
                break;
            case GameModeType.Online:
                _currentMode = new OnlineGameMode();
                break;
        }

        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.HideGameOver();
        _gameUI.UpdateTurn(PieceColor.White);
    }

    private void OnTurnChanged(PieceColor color)
    {
        _gameUI.UpdateTurn(color);
        _currentMode?.OnTurnStart(color);
    }

    private void OnGameOverEvent(MatchResult result)
    {
        _currentMode?.OnMatchEnd(result);
        _gameUI.ShowGameOver(result);
    }
}
