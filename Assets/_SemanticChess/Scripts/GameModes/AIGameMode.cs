using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.VsAI;

    private ChessBoard _board;
    private ElementService _elementService;
    private readonly LocalGameMode _localInput = new LocalGameMode();
    private readonly PieceColor _humanColor = PieceColor.White;
    private readonly ChessEngine _engine = new ChessEngine();
    private bool _aiThinking;

    // Difficulty: 0=Easy, 1=Medium, 2=Hard
    private static readonly int[] SearchDepth    = { 2, 3, 4 };
    private static readonly int[] CandidateCount = { 0, 8, 4 }; // 0 = all legal moves

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _elementService = board.ElementService;
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
            _aiThinking = true;
            GameManager.Instance.GameUI.ShowThinking();
            GameManager.Instance.RunCoroutine(DoAITurn(color));
        }
    }

    public void OnUpdate()
    {
        if (_board == null) return;
        if (_board.CurrentTurn == _humanColor && !_aiThinking)
            _localInput.OnUpdate();
    }

    public void OnDeactivate()
    {
        _localInput.OnDeactivate();
    }

    private IEnumerator DoAITurn(PieceColor aiColor)
    {
        // Wait for any ongoing move/reaction animations
        while (_board.IsMoving || _board.IsPlayingReaction)
            yield return null;

        // --- Engine phase ---
        int difficulty = Mathf.Clamp(GameManager.Instance.AIDifficulty, 0, 2);
        int depth = SearchDepth[difficulty];
        int candidateCount = CandidateCount[difficulty];

        _engine.LoadPosition(_board);
        var allMoves = _engine.GetTopMoves(0, depth); // get all moves scored

        // Remove illegal moves the engine doesn't know about (effects)
        allMoves.RemoveAll(m =>
        {
            var p = _board.GetPiece(m.from);
            if (p != null && p.HasEffect(EffectType.Stun)) return true;
            if (_board.TileHasEffect(m.to, TileEffectType.Occupied)) return true;
            return false;
        });

        if (allMoves.Count == 0) yield break;

        // Select candidates based on difficulty
        List<(int from, int to, int score)> candidates;
        if (candidateCount <= 0 || candidateCount >= allMoves.Count)
            candidates = allMoves;
        else
            candidates = allMoves.GetRange(0, candidateCount);

        // Best engine move as fallback
        var bestEngineMove = allMoves[0];

        // --- Gemini phase ---
        string boardState = _board.SerializeBoardForAI();
        string movesStr = ElementPrompts.FormatCandidateMoves(candidates, _board.GetPiece);
        string prompt = ElementPrompts.BuildChessMovePrompt(boardState, movesStr);

        (int from, int to) chosen = (-1, -1);
        var rejected = new List<string>();

        for (int attempt = 0; attempt < 3 && chosen.from < 0; attempt++)
        {
            // Rebuild prompt excluding rejected moves
            if (attempt > 0 && rejected.Count > 0)
            {
                var filtered = candidates.FindAll(c =>
                    !rejected.Contains(ChessBoard.IndexToAlgebraic(c.from) + ChessBoard.IndexToAlgebraic(c.to)));
                if (filtered.Count == 0) break;
                movesStr = ElementPrompts.FormatCandidateMoves(filtered, _board.GetPiece);
                prompt = ElementPrompts.BuildChessMovePrompt(boardState, movesStr);
            }

            string moveStr = null;
            yield return _elementService.GetAIMove(prompt, m => moveStr = m);

            // Strip leading piece letter (AI sometimes returns e.g. "pg7f6")
            if (moveStr != null && moveStr.Length == 5 && char.IsLetter(moveStr[0]))
                moveStr = moveStr[1..];

            if (moveStr != null && moveStr.Length == 4)
            {
                int from = ChessBoard.AlgebraicToIndex(moveStr[..2]);
                int to = ChessBoard.AlgebraicToIndex(moveStr[2..]);
                if (from >= 0 && to >= 0 && candidates.Exists(c => c.from == from && c.to == to))
                    chosen = (from, to);
                else
                {
                    rejected.Add(moveStr);
                    Debug.LogWarning($"[AIGameMode] Illegal/off-list move from AI: {moveStr} (attempt {attempt + 1}), excluding from retry");
                }
            }
            else
            {
                Debug.LogWarning($"[AIGameMode] Bad response from AI: {moveStr} (attempt {attempt + 1})");
            }
        }

        // Fallback: use engine's best move
        if (chosen.from < 0)
        {
            chosen = (bestEngineMove.from, bestEngineMove.to);
            Debug.Log($"[AIGameMode] Falling back to engine best: {ChessBoard.IndexToAlgebraic(chosen.from)}{ChessBoard.IndexToAlgebraic(chosen.to)}");
        }

        // Juice: select the piece briefly, then move
        GameManager.Instance.GameUI.HideThinking();
        _board.SelectPiece(chosen.from);
        yield return new WaitForSeconds(0.4f);
        _board.DeselectPiece();

        _aiThinking = false;
        _board.SubmitMove(new MoveRequest
        {
            FromIndex = chosen.from,
            ToIndex = chosen.to,
            Player = aiColor
        });
    }
}
