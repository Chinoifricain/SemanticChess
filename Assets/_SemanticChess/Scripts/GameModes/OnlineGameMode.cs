using UnityEngine;

public class OnlineGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.Online;

    private ChessBoard _board;
    private readonly LocalGameMode _localInput = new LocalGameMode();
    private PieceColor _localColor = PieceColor.White;

    // TODO: WebSocket connection to Cloudflare Workers Durable Object
    // TODO: Room management (create/join private, create/join public)
    // TODO: Real-time events (hover, select) for juiciness
    // TODO: One player is authority for element reactions

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _localInput.OnMatchStart(board);
    }

    public void OnMatchEnd(MatchResult result)
    {
        _localInput.OnMatchEnd(result);
        // TODO: Notify opponent via WebSocket
    }

    public void OnTurnStart(PieceColor color)
    {
        if (color == _localColor)
        {
            _localInput.OnTurnStart(color);
        }
        else
        {
            // TODO: Wait for opponent's move via WebSocket
            Debug.Log("[OnlineGameMode] Waiting for opponent... (not implemented)");
        }
    }

    public void OnUpdate()
    {
        if (_board == null) return;

        if (_board.CurrentTurn == _localColor)
            _localInput.OnUpdate();

        // TODO: Poll WebSocket for opponent moves and real-time events
    }

    public void OnDeactivate()
    {
        _localInput.OnDeactivate();
        // TODO: Disconnect WebSocket, leave room
    }
}
