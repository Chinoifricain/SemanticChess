public interface IGameMode
{
    GameModeType ModeType { get; }
    void OnMatchStart(ChessBoard board);
    void OnMatchEnd(MatchResult result);
    void OnTurnStart(PieceColor color);
    void OnUpdate();
    void OnDeactivate();
}
