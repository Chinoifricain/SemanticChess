using UnityEngine;

public class AIGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.VsAI;

    private ChessBoard _board;
    private readonly LocalGameMode _localInput = new LocalGameMode();
    private PieceColor _humanColor = PieceColor.White;

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _localInput.OnMatchStart(board);
    }

    public void OnMatchEnd(MatchResult result)
    {
        _localInput.OnMatchEnd(result);
    }

    public void OnTurnStart(PieceColor color)
    {
        if (color == _humanColor)
        {
            _localInput.OnTurnStart(color);
        }
        else
        {
            // TODO: Send board state to Gemini and get AI move
            Debug.Log("[AIGameMode] AI thinking... (not implemented)");
        }
    }

    public void OnUpdate()
    {
        if (_board == null) return;

        // Only process local input on human's turn
        if (_board.CurrentTurn == _humanColor)
            _localInput.OnUpdate();
    }

    public void OnDeactivate()
    {
        _localInput.OnDeactivate();
    }
}
