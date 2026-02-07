using UnityEngine;

public class LocalGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.Local;

    private ChessBoard _board;
    private Camera _cam;

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _cam = Camera.main;
    }

    public void OnMatchEnd(MatchResult result) { }

    public void OnTurnStart(PieceColor color) { }

    public void OnUpdate()
    {
        if (_board == null) return;

        // Hold click to fast-forward during reaction effects
        if (_board.IsPlayingReaction && Input.GetMouseButton(0))
            Time.timeScale = 4f;
        else if (Time.timeScale != 1f)
            Time.timeScale = 1f;

        if (!_board.IsMoving && !_board.IsGameOver)
            UpdateHover();

        if (_board.IsMoving || _board.IsGameOver) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        int index = _board.WorldToTileIndex(world);
        if (index >= 0)
            OnTileClicked(index);
    }

    public void OnDeactivate()
    {
        if (_board != null)
        {
            _board.DeselectPiece();
            _board.SetHoveredIndex(-1);
            _board.SetHoveredTileIndex(-1);
        }
        Time.timeScale = 1f;
    }

    private void UpdateHover()
    {
        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        int index = _board.WorldToTileIndex(world);

        int newHovered = -1;
        if (index >= 0 && _board.GetPiece(index) != null)
            newHovered = index;

        _board.SetHoveredIndex(newHovered);
        _board.SetHoveredTileIndex(index);
    }

    private void OnTileClicked(int index)
    {
        int selected = _board.SelectedIndex;

        // Nothing selected - try to select
        if (selected == -1)
        {
            _board.SelectPiece(index);
            return;
        }

        // Click same piece - deselect
        if (index == selected)
        {
            _board.DeselectPiece();
            return;
        }

        // Click another friendly piece that isn't stunned - switch selection
        ChessPiece clickedPiece = _board.GetPiece(index);
        if (clickedPiece != null && clickedPiece.Color == _board.CurrentTurn
            && !clickedPiece.HasEffect(EffectType.Stun))
        {
            _board.DeselectPiece();
            _board.SelectPiece(index);
            return;
        }

        // Click a valid move destination - submit move
        var validMoves = _board.GetLegalMovesFor(selected);
        if (validMoves.Contains(index))
        {
            int from = selected;
            _board.DeselectPiece();
            _board.SubmitMove(new MoveRequest
            {
                FromIndex = from,
                ToIndex = index,
                Player = _board.CurrentTurn
            });
            return;
        }

        // Click elsewhere - deselect
        _board.DeselectPiece();
    }
}
