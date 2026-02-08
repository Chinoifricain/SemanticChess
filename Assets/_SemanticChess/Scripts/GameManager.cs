using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private ChessBoard _board;
    [SerializeField] private MenuUI _menuUI;
    [SerializeField] private GameUI _gameUI;
    [SerializeField] private RoomManager _roomManager;

    [Header("Tutorial")]
    [SerializeField] private GameObject _tutorialCardPrefab;

    [Header("Game Log")]
    [SerializeField] private GameLog _gameLog;

    private IGameMode _currentMode;
    private OnlineGameMode _onlineMode;
    private BoardLayoutData _activeLayout;
    private TooltipUI _tooltip;

    // Video mode activation: V → I → D sequence
    private int _videoSeqIndex;
    private float _videoSeqTimer;
    private static readonly KeyCode[] VideoSequence = { KeyCode.V, KeyCode.I, KeyCode.D };

    public ChessBoard Board => _board;
    public GameUI GameUI => _gameUI;
    public RoomManager RoomManager => _roomManager;

    public int AIDifficulty { get; set; }

    public Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

    private void CreateTooltip()
    {
        DestroyTooltip();
        if (_gameUI.TooltipPrefab == null) return;
        var canvas = _gameUI.GetCanvas();
        _tooltip = Instantiate(_gameUI.TooltipPrefab, canvas.transform);
        _tooltip.Init(_board);
    }

    private void DestroyTooltip()
    {
        if (_tooltip != null)
        {
            Destroy(_tooltip.gameObject);
            _tooltip = null;
        }
    }

    private void Awake()
    {
        Instance = this;

        // Create RoomManager if not assigned in inspector
        if (_roomManager == null)
            _roomManager = gameObject.AddComponent<RoomManager>();
    }

    private void Start()
    {
        ElementCollection.Load();
        BoardLayout.Load();

        _gameUI.Hide();

        if (!ElementCollection.HasPlayedBefore)
        {
            StartCoroutine(StartTutorial());
        }
        else
        {
            _menuUI.Show();
        }
    }

    private IEnumerator StartTutorial()
    {
        yield return null; // wait one frame for board init
        if (!_board.IsInitialized) yield break;

        _menuUI.Hide();
        _board.SetFlipped(false);

        // Create tutorial UI (card display)
        var tutoUI = new TutorialManager();
        tutoUI.Init(_tutorialCardPrefab);

        // Create tutorial game mode
        var tutoMode = new TutorialGameMode();
        tutoMode.SetTutorialUI(tutoUI);
        _currentMode = tutoMode;

        // Only subscribe to turn changes (no game over or collection during tutorial)
        _board.OnTurnChanged += OnTurnChanged;

        _currentMode.OnMatchStart(_board);

        _gameUI.Show();
        _gameUI.SetBackToMenuVisible(false);
        _gameUI.UpdateTurn(PieceColor.White);
        AudioManager.Instance?.PlayGameStart();
    }

    public void EndTutorial()
    {
        if (_currentMode != null)
        {
            _currentMode.OnDeactivate();
            _currentMode = null;
        }

        _board.OnTurnChanged -= OnTurnChanged;
        _board.ClearBoard();

        ElementCollection.HasPlayedBefore = true;

        _gameUI.Hide();
        _menuUI.Show();
    }

    private IEnumerator StartVideo()
    {
        yield return null; // wait one frame for board init
        if (!_board.IsInitialized) yield break;

        _menuUI.Hide();
        _board.SetFlipped(false);

        var videoUI = new VideoUI();
        videoUI.Init(_gameUI.GetCanvas(), _board.CaptionFont);

        var videoMode = new VideoGameMode();
        videoMode.SetVideoUI(videoUI);
        _currentMode = videoMode;

        _board.OnTurnChanged += OnTurnChanged;

        _currentMode.OnMatchStart(_board);

        _gameUI.Show();
        _gameUI.SetBackToMenuVisible(false);
        _gameUI.SetTurnText("");
        if (_gameLog != null) _gameLog.gameObject.SetActive(false);
    }

    public void EndVideo()
    {
        if (_currentMode != null)
        {
            _currentMode.OnDeactivate();
            _currentMode = null;
        }

        _board.OnTurnChanged -= OnTurnChanged;
        _board.ClearBoard();

        if (_gameLog != null) _gameLog.gameObject.SetActive(true);
        _gameUI.Hide();
        _menuUI.Show();
    }

    private void Update()
    {
        _currentMode?.OnUpdate();

        // V-I-D sequence on menu → start video mode
        if (_currentMode == null)
        {
            if (_videoSeqIndex > 0)
            {
                _videoSeqTimer -= Time.deltaTime;
                if (_videoSeqTimer <= 0f)
                    _videoSeqIndex = 0;
            }

            if (Input.GetKeyDown(VideoSequence[_videoSeqIndex]))
            {
                _videoSeqIndex++;
                _videoSeqTimer = 1f;
                if (_videoSeqIndex >= VideoSequence.Length)
                {
                    _videoSeqIndex = 0;
                    StartCoroutine(StartVideo());
                }
            }
            else if (Input.anyKeyDown)
            {
                // Wrong key — restart if it matches the first key
                _videoSeqIndex = Input.GetKeyDown(VideoSequence[0]) ? 1 : 0;
                _videoSeqTimer = 1f;
            }
        }
    }

    public void StartMatch(GameModeType modeType, BoardLayoutData layout = null)
    {
        // Wait for board to be initialized
        if (!_board.IsInitialized) return;

        _activeLayout = layout;
        _menuUI.Hide();

        _board.SetFlipped(false);
        _board.ResetBoard(layout);

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
                // Online mode is started via StartOnlineMatch — shouldn't reach here directly
                return;
        }

        // Subscribe to board events
        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;
        _board.OnCaptureResult += OnCaptureForCollection;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.Show();
        _gameUI.UpdateTurn(PieceColor.White);
        CreateTooltip();
        AudioManager.Instance?.PlayGameStart();
    }

    /// <summary>
    /// Start an online match after room is ready and both players connected.
    /// Called by MenuUI when RoomManager.OnGameStart fires.
    /// </summary>
    public void StartOnlineMatch(PieceColor localColor, BoardLayoutData layout = null)
    {
        if (!_board.IsInitialized) return;

        _activeLayout = layout;
        _menuUI.Hide();
        _board.SetFlipped(localColor == PieceColor.Black);
        _board.ResetBoard(layout);

        _onlineMode = new OnlineGameMode();
        _onlineMode.SetRoom(_roomManager, localColor);
        _currentMode = _onlineMode;

        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;
        _board.OnCaptureResult += OnCaptureForCollection;

        // Subscribe to online-specific events
        _roomManager.OnRematchStart += OnOnlineRematchStart;
        _roomManager.OnOpponentDisconnect += OnOpponentDisconnect;
        _roomManager.OnOpponentReconnect += OnOpponentReconnect;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.Show();
        _gameUI.SetOnlineMode(true);
        _gameUI.UpdateOnlineTurn(localColor == PieceColor.White);
        CreateTooltip();
        AudioManager.Instance?.PlayGameStart();
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
        _board.OnCaptureResult -= OnCaptureForCollection;

        if (!ElementCollection.HasPlayedBefore)
            ElementCollection.HasPlayedBefore = true;

        if (_onlineMode != null)
        {
            _roomManager.OnRematchStart -= OnOnlineRematchStart;
            _roomManager.OnOpponentDisconnect -= OnOpponentDisconnect;
            _roomManager.OnOpponentReconnect -= OnOpponentReconnect;
            _roomManager.Disconnect();
            _onlineMode = null;
        }

        DestroyTooltip();
        _gameUI.SetOnlineMode(false);
        _gameUI.Hide();
        _menuUI.Show();
    }

    public void RequestRematch()
    {
        if (_currentMode == null) return;

        // Online rematch: send request and wait for opponent
        if (_onlineMode != null)
        {
            _roomManager.SendRematch();
            _gameUI.ShowWaitingRematch();
            return;
        }

        var modeType = _currentMode.ModeType;
        _currentMode.OnDeactivate();
        _board.OnTurnChanged -= OnTurnChanged;
        _board.OnGameOver -= OnGameOverEvent;
        _board.OnCaptureResult -= OnCaptureForCollection;

        _board.SetFlipped(false);
        _board.ResetBoard(_activeLayout);

        // Re-create fresh mode
        switch (modeType)
        {
            case GameModeType.Local:
                _currentMode = new LocalGameMode();
                break;
            case GameModeType.VsAI:
                _currentMode = new AIGameMode();
                break;
        }

        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;
        _board.OnCaptureResult += OnCaptureForCollection;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.HideGameOver();
        _gameUI.UpdateTurn(PieceColor.White);
        CreateTooltip();
    }

    private void OnOnlineRematchStart()
    {
        if (_onlineMode == null) return;

        var localColor = _onlineMode.LocalColor;
        _currentMode.OnDeactivate();
        _board.OnTurnChanged -= OnTurnChanged;
        _board.OnGameOver -= OnGameOverEvent;
        _board.OnCaptureResult -= OnCaptureForCollection;

        _board.SetFlipped(localColor == PieceColor.Black);
        _board.ResetBoard(_activeLayout);

        _onlineMode = new OnlineGameMode();
        _onlineMode.SetRoom(_roomManager, localColor);
        _currentMode = _onlineMode;

        _board.OnTurnChanged += OnTurnChanged;
        _board.OnGameOver += OnGameOverEvent;
        _board.OnCaptureResult += OnCaptureForCollection;

        _currentMode.OnMatchStart(_board);
        _currentMode.OnTurnStart(PieceColor.White);

        _gameUI.HideGameOver();
        _gameUI.UpdateOnlineTurn(localColor == PieceColor.White);
        CreateTooltip();
    }

    private void OnOpponentDisconnect()
    {
        _gameUI.ShowOpponentDisconnected(true);
    }

    private void OnOpponentReconnect()
    {
        _gameUI.ShowOpponentDisconnected(false);
    }

    private void OnTurnChanged(PieceColor color)
    {
        if (_onlineMode == null && _currentMode?.ModeType != GameModeType.Video)
            _gameUI.UpdateTurn(color);
        // Online mode updates turn text via OnlineGameMode.OnTurnStart

        _currentMode?.OnTurnStart(color);
    }

    private void OnGameOverEvent(MatchResult result)
    {
        _currentMode?.OnMatchEnd(result);
        _gameUI.ShowGameOver(result);
    }

    private void OnCaptureForCollection(int from, int to, ElementMixResult mix, ElementReactionResult reaction)
    {
        if (mix == null || string.IsNullOrEmpty(mix.newElement)) return;

        // At event time, _board[from] still has attacker with original element,
        // _board[to] still has defender (board mutation happens after event)
        ChessPiece attacker = _board.GetPieceAt(from);
        ChessPiece defender = _board.GetPieceAt(to);
        if (attacker == null || defender == null) return;

        bool shouldCollect = _currentMode?.ModeType switch
        {
            GameModeType.Local => true,
            GameModeType.VsAI => attacker.Color == PieceColor.White,
            GameModeType.Online => _onlineMode != null && attacker.Color == _onlineMode.LocalColor,
            _ => false
        };

        if (shouldCollect)
        {
            ElementCollection.AddElement(mix.newElement, mix.emoji, attacker.Element, defender.Element);
            ElementCollection.Save();
        }
    }
}
