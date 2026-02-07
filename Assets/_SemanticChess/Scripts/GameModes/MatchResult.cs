public enum MatchOutcome
{
    Checkmate,
    Stalemate,
    KingDestroyed
}

public struct MatchResult
{
    public MatchOutcome Outcome;
    public PieceColor Winner;
}
