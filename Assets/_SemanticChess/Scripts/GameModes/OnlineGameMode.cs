using UnityEngine;

public class OnlineGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.Online;

    private ChessBoard _board;
    private RoomManager _room;
    private Camera _cam;
    private readonly LocalGameMode _localInput = new LocalGameMode();
    private PieceColor _localColor = PieceColor.White;
    private bool _isLocalTurn;

    public PieceColor LocalColor => _localColor;

    public void SetRoom(RoomManager room, PieceColor localColor)
    {
        _room = room;
        _localColor = localColor;
    }

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _localInput.OnMatchStart(board);

        // Subscribe to board events for relaying to opponent
        _board.OnMoveMade += OnLocalMoveMade;
        _board.OnCaptureResult += OnLocalCaptureResult;
        _board.OnHoverChanged += OnLocalHoverChanged;
        _board.OnPieceSelected += OnLocalPieceSelected;
        _board.OnPieceDeselected += OnLocalPieceDeselected;

        // Subscribe to opponent events from RoomManager
        if (_room != null)
        {
            _room.OnOpponentMove += OnOpponentMove;
            _room.OnOpponentCaptureResult += OnOpponentCaptureResult;
            _room.OnOpponentHover += OnOpponentHover;
            _room.OnOpponentSelect += OnOpponentSelect;
            _room.OnOpponentDeselect += OnOpponentDeselect;
            _room.OnOpponentResign += OnOpponentResign;
        }
    }

    public void OnMatchEnd(MatchResult result)
    {
        _localInput.OnMatchEnd(result);
    }

    public void OnTurnStart(PieceColor color)
    {
        _isLocalTurn = (color == _localColor);

        if (_isLocalTurn)
        {
            _localInput.OnTurnStart(color);
            GameManager.Instance.GameUI.UpdateOnlineTurn(true);
        }
        else
        {
            GameManager.Instance.GameUI.UpdateOnlineTurn(false);
        }
    }

    public void OnUpdate()
    {
        if (_board == null) return;

        // Always allow hovering so the player can inspect elements on either turn
        if (!_board.IsMoving && !_board.IsGameOver)
            UpdateHover();

        // Only allow clicks/selection on local player's turn
        if (_isLocalTurn && !_board.IsMoving && !_board.IsGameOver)
            _localInput.OnUpdate();
    }

    private void UpdateHover()
    {
        if (_cam == null) _cam = Camera.main;
        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        int index = _board.WorldToTileIndex(world);

        int newHovered = -1;
        if (index >= 0 && _board.GetPiece(index) != null)
            newHovered = index;

        _board.SetHoveredIndex(newHovered);
    }

    public void OnDeactivate()
    {
        _localInput.OnDeactivate();

        if (_board != null)
        {
            _board.OnMoveMade -= OnLocalMoveMade;
            _board.OnCaptureResult -= OnLocalCaptureResult;
            _board.OnHoverChanged -= OnLocalHoverChanged;
            _board.OnPieceSelected -= OnLocalPieceSelected;
            _board.OnPieceDeselected -= OnLocalPieceDeselected;
        }

        if (_room != null)
        {
            _room.OnOpponentMove -= OnOpponentMove;
            _room.OnOpponentCaptureResult -= OnOpponentCaptureResult;
            _room.OnOpponentHover -= OnOpponentHover;
            _room.OnOpponentSelect -= OnOpponentSelect;
            _room.OnOpponentDeselect -= OnOpponentDeselect;
            _room.OnOpponentResign -= OnOpponentResign;
        }
    }

    // --- Local player actions → send to opponent ---

    private bool _pendingCapture;

    private void OnLocalMoveMade(int from, int to)
    {
        if (!_isLocalTurn || _room == null) return;

        // Always send the move immediately so the opponent sees the piece move right away
        _room.SendMove(from, to);

        ChessPiece target = _board.GetPiece(to);
        if (target != null && !target.HasEffect(EffectType.Shield))
            _pendingCapture = true;
    }

    private void OnLocalCaptureResult(int from, int to, ElementMixResult mix, ElementReactionResult reaction)
    {
        if (!_pendingCapture || _room == null) return;
        _pendingCapture = false;
        _room.SendCaptureResult(from, to, mix, reaction);
    }

    private void OnLocalHoverChanged(int index)
    {
        if (!_isLocalTurn || _room == null) return;
        _room.SendHover(index);
    }

    private void OnLocalPieceSelected(int index)
    {
        if (!_isLocalTurn || _room == null) return;
        _room.SendSelect(index);
    }

    private void OnLocalPieceDeselected()
    {
        if (!_isLocalTurn || _room == null) return;
        _room.SendDeselect();
    }

    // --- Opponent actions → apply locally ---

    private void OnOpponentMove(int from, int to)
    {
        if (_board == null || _isLocalTurn) return;

        PieceColor opponentColor = _localColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // If this move is a capture, tell the board to wait for capture data
        // instead of calling the API — the data will arrive via capture_result message
        ChessPiece target = _board.GetPiece(to);
        if (target != null && !target.HasEffect(EffectType.Shield))
            _board.WaitForPendingData = true;

        _board.DeselectPiece();
        _board.SubmitMove(new MoveRequest
        {
            FromIndex = from,
            ToIndex = to,
            Player = opponentColor
        });
    }

    private void OnOpponentCaptureResult(int from, int to, ElementMixResult mix, ElementReactionResult reaction)
    {
        if (_board == null || _isLocalTurn) return;

        // HandleCapture is already running and waiting for this data
        _board.SetPendingReaction(mix, reaction);
    }

    private void OnOpponentHover(int index)
    {
        if (_board == null || _isLocalTurn) return;
        _board.SetHoveredIndex(index);
    }

    private void OnOpponentSelect(int index)
    {
        if (_board == null || _isLocalTurn) return;
        _board.SelectPiece(index);
    }

    private void OnOpponentDeselect()
    {
        if (_board == null || _isLocalTurn) return;
        _board.DeselectPiece();
    }

    private void OnOpponentResign()
    {
        if (_board == null) return;
        var winner = _localColor;
        GameManager.Instance.GameUI.ShowGameOver(new MatchResult
        {
            Outcome = MatchOutcome.Checkmate,
            Winner = winner
        });
    }
}
