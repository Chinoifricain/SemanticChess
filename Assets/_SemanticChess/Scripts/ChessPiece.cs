using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }
public enum PieceColor { White, Black }

[System.Serializable]
public struct PieceSpriteEntry
{
    public PieceType pieceType;
    public Sprite whiteSprite;
    public Sprite blackSprite;
}

public class ChessPiece : MonoBehaviour
{
    [Header("Piece to Sprite")]
    [SerializeField] private PieceSpriteEntry[] _spriteEntries;

    [Header("Image References")]
    [SerializeField] private Image _image;
    [SerializeField] private Image _dropShadowImage;

    public PieceType PieceType { get; private set; }
    public PieceColor Color { get; private set; }
    public bool HasMoved { get; set; }

    public void Init(PieceType type, PieceColor color)
    {
        PieceType = type;
        Color = color;
        HasMoved = false;

        Sprite sprite = GetSprite(type, color);
        _image.sprite = sprite;
        _dropShadowImage.sprite = sprite;
    }

    private Sprite GetSprite(PieceType type, PieceColor color)
    {
        foreach (var entry in _spriteEntries)
        {
            if (entry.pieceType == type)
                return color == PieceColor.White ? entry.whiteSprite : entry.blackSprite;
        }
        return null;
    }

    public List<int> GetPossibleMoves(int fromIndex, ChessPiece[] board)
    {
        int col = fromIndex % 8;
        int row = fromIndex / 8;
        var moves = new List<int>();

        switch (PieceType)
        {
            case PieceType.Pawn:   AddPawnMoves(col, row, moves, board);                 break;
            case PieceType.Knight: AddKnightMoves(col, row, moves, board);               break;
            case PieceType.Bishop: AddSlidingMoves(col, row, moves, board, BishopDirs);  break;
            case PieceType.Rook:   AddSlidingMoves(col, row, moves, board, RookDirs);    break;
            case PieceType.Queen:  AddSlidingMoves(col, row, moves, board, AllDirs);     break;
            case PieceType.King:   AddKingMoves(col, row, moves, board);                 break;
        }

        return moves;
    }

    // --- Direction arrays ---

    private static readonly (int dc, int dr)[] BishopDirs =
        { (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private static readonly (int dc, int dr)[] RookDirs =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private static readonly (int dc, int dr)[] AllDirs =
        { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private static readonly (int dc, int dr)[] KnightOffsets =
        { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };

    // --- Movement helpers ---

    private void AddPawnMoves(int col, int row, List<int> moves, ChessPiece[] board)
    {
        // White moves up (row decreases), Black moves down (row increases)
        int dir = (Color == PieceColor.White) ? -1 : 1;
        int startRow = (Color == PieceColor.White) ? 6 : 1;

        // Forward one
        int fwdRow = row + dir;
        if (InBounds(col, fwdRow) && board[fwdRow * 8 + col] == null)
        {
            moves.Add(fwdRow * 8 + col);

            // Forward two from starting row
            int fwd2Row = row + 2 * dir;
            if (row == startRow && board[fwd2Row * 8 + col] == null)
                moves.Add(fwd2Row * 8 + col);
        }

        // Diagonal captures
        foreach (int dc in new[] { -1, 1 })
        {
            int nc = col + dc;
            if (InBounds(nc, fwdRow))
            {
                ChessPiece target = board[fwdRow * 8 + nc];
                if (target != null && target.Color != Color)
                    moves.Add(fwdRow * 8 + nc);
            }
        }
    }

    private void AddKnightMoves(int col, int row, List<int> moves, ChessPiece[] board)
    {
        foreach (var (dc, dr) in KnightOffsets)
        {
            int nc = col + dc, nr = row + dr;
            if (!InBounds(nc, nr)) continue;

            ChessPiece target = board[nr * 8 + nc];
            if (target == null || target.Color != Color)
                moves.Add(nr * 8 + nc);
        }
    }

    private void AddSlidingMoves(int col, int row, List<int> moves, ChessPiece[] board, (int dc, int dr)[] dirs)
    {
        foreach (var (dc, dr) in dirs)
        {
            int nc = col + dc, nr = row + dr;
            while (InBounds(nc, nr))
            {
                int idx = nr * 8 + nc;
                if (board[idx] == null)
                {
                    moves.Add(idx);
                }
                else
                {
                    if (board[idx].Color != Color)
                        moves.Add(idx);
                    break;
                }
                nc += dc;
                nr += dr;
            }
        }
    }

    private void AddKingMoves(int col, int row, List<int> moves, ChessPiece[] board)
    {
        foreach (var (dc, dr) in AllDirs)
        {
            int nc = col + dc, nr = row + dr;
            if (!InBounds(nc, nr)) continue;

            ChessPiece target = board[nr * 8 + nc];
            if (target == null || target.Color != Color)
                moves.Add(nr * 8 + nc);
        }
    }

    private static bool InBounds(int col, int row)
    {
        return col >= 0 && col < 8 && row >= 0 && row < 8;
    }
}
