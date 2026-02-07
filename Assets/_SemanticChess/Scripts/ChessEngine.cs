using System.Collections.Generic;

/// <summary>
/// Lightweight minimax chess engine with alpha-beta pruning.
/// Operates on int[64] — no Unity dependencies.
/// </summary>
public class ChessEngine
{
    // Piece encoding: positive = White, negative = Black
    private const int PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6;
    private const int INF = 999999;

    private readonly int[] _board = new int[64];
    private bool _whiteToMove;

    // --- Direction tables ---

    private static readonly int[][] KnightOffsets = {
        new[]{1,2}, new[]{2,1}, new[]{2,-1}, new[]{1,-2},
        new[]{-1,-2}, new[]{-2,-1}, new[]{-2,1}, new[]{-1,2}
    };

    private static readonly int[][] BishopDirs = { new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1} };
    private static readonly int[][] RookDirs   = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };
    private static readonly int[][] AllDirs    = {
        new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1},
        new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1}
    };

    // --- Material values ---

    private static readonly int[] PieceValue = { 0, 100, 320, 330, 500, 900, 20000 };

    // --- Piece-square tables (from White's perspective, index 0 = a8) ---

    private static readonly int[] PawnTable = {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 25, 25, 10,  5,  5,
         0,  0,  0, 20, 20,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };

    private static readonly int[] KnightTable = {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50
    };

    private static readonly int[] BishopTable = {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    };

    private static readonly int[] RookTable = {
         0,  0,  0,  0,  0,  0,  0,  0,
         5, 10, 10, 10, 10, 10, 10,  5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
         0,  0,  0,  5,  5,  0,  0,  0
    };

    private static readonly int[] QueenTable = {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5,  5,  5,  5,  0, -5,
          0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    };

    private static readonly int[] KingTable = {
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
    };

    private static readonly int[][] PSTables = { null, PawnTable, KnightTable, BishopTable, RookTable, QueenTable, KingTable };

    // --- Public API ---

    public void LoadPosition(ChessBoard board)
    {
        for (int i = 0; i < 64; i++)
        {
            ChessPiece p = board.GetPiece(i);
            if (p == null) { _board[i] = 0; continue; }
            int v = p.PieceType switch
            {
                PieceType.Pawn   => PAWN,
                PieceType.Knight => KNIGHT,
                PieceType.Bishop => BISHOP,
                PieceType.Rook   => ROOK,
                PieceType.Queen  => QUEEN,
                PieceType.King   => KING,
                _ => 0
            };
            _board[i] = p.Color == PieceColor.White ? v : -v;
        }
        _whiteToMove = board.CurrentTurn == PieceColor.White;
    }

    public List<(int from, int to, int score)> GetTopMoves(int count, int depth)
    {
        var moves = GenerateLegalMoves(_whiteToMove);
        if (moves.Count == 0) return new List<(int, int, int)>();

        // Order moves: captures first (MVV-LVA)
        moves.Sort((a, b) => MoveOrder(b) - MoveOrder(a));

        var scored = new List<(int from, int to, int score)>();
        foreach (var (from, to) in moves)
        {
            var undo = MakeMove(from, to);
            int score = -Negamax(depth - 1, -INF, INF, !_whiteToMove);
            UnmakeMove(from, to, undo);
            scored.Add((from, to, score));
        }

        // Sort best-first (highest score for the moving side)
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        if (count > 0 && scored.Count > count)
            scored.RemoveRange(count, scored.Count - count);

        return scored;
    }

    // --- Search ---

    private int Negamax(int depth, int alpha, int beta, bool whiteToMove)
    {
        if (depth <= 0) return Evaluate(whiteToMove);

        var moves = GenerateLegalMoves(whiteToMove);
        if (moves.Count == 0)
        {
            // No legal moves: checkmate or stalemate
            return IsKingAttacked(whiteToMove) ? -INF + (100 - depth) : 0;
        }

        moves.Sort((a, b) => MoveOrder(b) - MoveOrder(a));

        int best = -INF;
        foreach (var (from, to) in moves)
        {
            var undo = MakeMove(from, to);
            int score = -Negamax(depth - 1, -beta, -alpha, !whiteToMove);
            UnmakeMove(from, to, undo);

            if (score > best) best = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta) break;
        }
        return best;
    }

    private int MoveOrder((int from, int to) move)
    {
        int victim = _board[move.to];
        if (victim == 0) return 0;
        // MVV-LVA: prioritize capturing high-value with low-value
        int victimVal = PieceValue[System.Math.Abs(victim)];
        int attackerVal = PieceValue[System.Math.Abs(_board[move.from])];
        return victimVal * 10 - attackerVal;
    }

    // --- Evaluation ---

    private int Evaluate(bool whiteToMove)
    {
        int score = 0;
        for (int i = 0; i < 64; i++)
        {
            int p = _board[i];
            if (p == 0) continue;

            int abs = System.Math.Abs(p);
            int sign = p > 0 ? 1 : -1;

            // Material
            score += sign * PieceValue[abs];

            // Piece-square table (flip vertically for black)
            int[] table = PSTables[abs];
            if (table != null)
            {
                int tableIdx = p > 0 ? i : ((7 - i / 8) * 8 + i % 8);
                score += sign * table[tableIdx];
            }
        }
        // Return relative to the side to move
        return whiteToMove ? score : -score;
    }

    // --- Move generation ---

    private List<(int from, int to)> GenerateLegalMoves(bool whiteToMove)
    {
        var pseudo = new List<(int from, int to)>();
        for (int i = 0; i < 64; i++)
        {
            int p = _board[i];
            if (p == 0) continue;
            if (whiteToMove && p < 0) continue;
            if (!whiteToMove && p > 0) continue;

            int abs = System.Math.Abs(p);
            switch (abs)
            {
                case PAWN:   GenPawnMoves(i, whiteToMove, pseudo);    break;
                case KNIGHT: GenJumpMoves(i, KnightOffsets, pseudo);  break;
                case BISHOP: GenSlidingMoves(i, BishopDirs, pseudo);  break;
                case ROOK:   GenSlidingMoves(i, RookDirs, pseudo);    break;
                case QUEEN:  GenSlidingMoves(i, AllDirs, pseudo);     break;
                case KING:   GenJumpMoves(i, AllDirs, pseudo);        break;
            }
        }

        // Filter out illegal moves (leaves own king in check)
        var legal = new List<(int from, int to)>();
        foreach (var (from, to) in pseudo)
        {
            var undo = MakeMove(from, to);
            if (!IsKingAttacked(whiteToMove))
                legal.Add((from, to));
            UnmakeMove(from, to, undo);
        }
        return legal;
    }

    private void GenPawnMoves(int idx, bool isWhite, List<(int, int)> moves)
    {
        int col = idx % 8, row = idx / 8;
        int dir = isWhite ? -1 : 1;
        int startRow = isWhite ? 6 : 1;
        int promoRow = isWhite ? 0 : 7;

        // Forward
        int fwd = (row + dir) * 8 + col;
        if (InBounds(col, row + dir) && _board[fwd] == 0)
        {
            moves.Add((idx, fwd));
            // Double push
            int fwd2 = (row + 2 * dir) * 8 + col;
            if (row == startRow && _board[fwd2] == 0)
                moves.Add((idx, fwd2));
        }

        // Captures
        foreach (int dc in new[] { -1, 1 })
        {
            int nc = col + dc, nr = row + dir;
            if (!InBounds(nc, nr)) continue;
            int target = _board[nr * 8 + nc];
            if (target != 0 && ((isWhite && target < 0) || (!isWhite && target > 0)))
                moves.Add((idx, nr * 8 + nc));
        }
    }

    private void GenJumpMoves(int idx, int[][] offsets, List<(int, int)> moves)
    {
        int col = idx % 8, row = idx / 8;
        bool isWhite = _board[idx] > 0;
        foreach (var off in offsets)
        {
            int nc = col + off[0], nr = row + off[1];
            if (!InBounds(nc, nr)) continue;
            int target = _board[nr * 8 + nc];
            if (target == 0 || (isWhite && target < 0) || (!isWhite && target > 0))
                moves.Add((idx, nr * 8 + nc));
        }
    }

    private void GenSlidingMoves(int idx, int[][] dirs, List<(int, int)> moves)
    {
        int col = idx % 8, row = idx / 8;
        bool isWhite = _board[idx] > 0;
        foreach (var d in dirs)
        {
            int nc = col + d[0], nr = row + d[1];
            while (InBounds(nc, nr))
            {
                int ti = nr * 8 + nc;
                int target = _board[ti];
                if (target == 0)
                {
                    moves.Add((idx, ti));
                }
                else
                {
                    if ((isWhite && target < 0) || (!isWhite && target > 0))
                        moves.Add((idx, ti));
                    break;
                }
                nc += d[0];
                nr += d[1];
            }
        }
    }

    // --- Make / Unmake ---

    private struct UndoInfo { public int captured; public int movedPiece; }

    private UndoInfo MakeMove(int from, int to)
    {
        var undo = new UndoInfo { captured = _board[to], movedPiece = _board[from] };
        _board[to] = _board[from];
        _board[from] = 0;

        // Auto-promote pawns to queen
        int piece = _board[to];
        int row = to / 8;
        if (System.Math.Abs(piece) == PAWN && (row == 0 || row == 7))
            _board[to] = piece > 0 ? QUEEN : -QUEEN;

        return undo;
    }

    private void UnmakeMove(int from, int to, UndoInfo undo)
    {
        _board[from] = undo.movedPiece;
        _board[to] = undo.captured;
    }

    // --- King safety ---

    private bool IsKingAttacked(bool whiteKing)
    {
        int kingVal = whiteKing ? KING : -KING;
        int kingIdx = -1;
        for (int i = 0; i < 64; i++)
            if (_board[i] == kingVal) { kingIdx = i; break; }
        if (kingIdx < 0) return true; // king captured — shouldn't happen

        return IsSquareAttacked(kingIdx, !whiteKing);
    }

    private bool IsSquareAttacked(int idx, bool byWhite)
    {
        int col = idx % 8, row = idx / 8;

        // Knight attacks
        foreach (var off in KnightOffsets)
        {
            int nc = col + off[0], nr = row + off[1];
            if (!InBounds(nc, nr)) continue;
            int p = _board[nr * 8 + nc];
            if (byWhite && p == KNIGHT) return true;
            if (!byWhite && p == -KNIGHT) return true;
        }

        // Pawn attacks
        int pawnDir = byWhite ? 1 : -1; // direction the attacking pawn came from
        foreach (int dc in new[] { -1, 1 })
        {
            int nc = col + dc, nr = row + pawnDir;
            if (!InBounds(nc, nr)) continue;
            int p = _board[nr * 8 + nc];
            if (byWhite && p == PAWN) return true;
            if (!byWhite && p == -PAWN) return true;
        }

        // King attacks
        foreach (var d in AllDirs)
        {
            int nc = col + d[0], nr = row + d[1];
            if (!InBounds(nc, nr)) continue;
            int p = _board[nr * 8 + nc];
            if (byWhite && p == KING) return true;
            if (!byWhite && p == -KING) return true;
        }

        // Sliding attacks: rook/queen on straights, bishop/queen on diagonals
        foreach (var d in RookDirs)
        {
            int nc = col + d[0], nr = row + d[1];
            while (InBounds(nc, nr))
            {
                int p = _board[nr * 8 + nc];
                if (p != 0)
                {
                    int abs = System.Math.Abs(p);
                    if ((abs == ROOK || abs == QUEEN) && ((byWhite && p > 0) || (!byWhite && p < 0)))
                        return true;
                    break;
                }
                nc += d[0]; nr += d[1];
            }
        }

        foreach (var d in BishopDirs)
        {
            int nc = col + d[0], nr = row + d[1];
            while (InBounds(nc, nr))
            {
                int p = _board[nr * 8 + nc];
                if (p != 0)
                {
                    int abs = System.Math.Abs(p);
                    if ((abs == BISHOP || abs == QUEEN) && ((byWhite && p > 0) || (!byWhite && p < 0)))
                        return true;
                    break;
                }
                nc += d[0]; nr += d[1];
            }
        }

        return false;
    }

    private static bool InBounds(int col, int row) => col >= 0 && col < 8 && row >= 0 && row < 8;
}
