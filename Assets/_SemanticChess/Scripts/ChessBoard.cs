using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1)]
public class ChessBoard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _tileContainer;
    [SerializeField] private GameObject _piecePrefab;

    [Header("Move Indicators")]
    [SerializeField] private Sprite _moveIndicatorSprite;

    private readonly ChessPiece[] _board = new ChessPiece[64];
    private PieceColor _currentTurn = PieceColor.White;
    private int _selectedIndex = -1;
    private readonly List<int> _validMoves = new List<int>();
    private readonly List<GameObject> _indicators = new List<GameObject>();
    private bool _isMoving;
    private static readonly Color Transparent = new Color(0, 0, 0, 0);

    private void Start()
    {
        RegisterTileClicks();
        SetupBoard();
    }

    // --- Board Setup ---

    private void SetupBoard()
    {
        PieceType[] backRank =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int col = 0; col < 8; col++)
        {
            SpawnPiece(col, backRank[col], PieceColor.Black);       // Row 0: Black back rank
            SpawnPiece(8 + col, PieceType.Pawn, PieceColor.Black);  // Row 1: Black pawns
            SpawnPiece(48 + col, PieceType.Pawn, PieceColor.White); // Row 6: White pawns
            SpawnPiece(56 + col, backRank[col], PieceColor.White);  // Row 7: White back rank
        }
    }

    private void SpawnPiece(int index, PieceType type, PieceColor color)
    {
        Transform tile = _tileContainer.GetChild(index);
        GameObject go = Instantiate(_piecePrefab, tile);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        ChessPiece piece = go.GetComponent<ChessPiece>();
        piece.Init(type, color);

        _board[index] = piece;
    }

    // --- Tile Click Registration ---

    private void RegisterTileClicks()
    {
        for (int i = 0; i < 64; i++)
        {
            Transform tile = _tileContainer.GetChild(i);

            // Add transparent image so the tile is raycastable
            Image img = tile.gameObject.AddComponent<Image>();
            img.color = Transparent;

            // Add button for click handling
            Button btn = tile.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            int tileIndex = i;
            btn.onClick.AddListener(() => OnTileClicked(tileIndex));
        }
    }

    // --- Selection & Movement ---

    private void OnTileClicked(int index)
    {
        if (_isMoving) return;

        // Nothing selected yet
        if (_selectedIndex == -1)
        {
            TrySelect(index);
            return;
        }

        // Clicked the same tile -> deselect
        if (index == _selectedIndex)
        {
            Deselect();
            return;
        }

        // Clicked another piece of the same color -> re-select
        if (_board[index] != null && _board[index].Color == _currentTurn)
        {
            Deselect();
            TrySelect(index);
            return;
        }

        // Clicked a valid move target -> clear indicators, move while lifted, drop on arrival
        if (_validMoves.Contains(index))
        {
            int from = _selectedIndex;
            ClearIndicators();
            _selectedIndex = -1;
            _validMoves.Clear();
            ExecuteMove(from, index);
            return;
        }

        // Invalid click -> deselect
        Deselect();
    }

    private void TrySelect(int index)
    {
        ChessPiece piece = _board[index];
        if (piece == null || piece.Color != _currentTurn) return;

        _selectedIndex = index;
        piece.Select();
        _validMoves.Clear();
        _validMoves.AddRange(piece.GetPossibleMoves(index, _board));
        ShowIndicators();
    }

    private void ExecuteMove(int from, int to)
    {
        _isMoving = true;

        ChessPiece piece = _board[from];
        RectTransform pieceRT = piece.GetComponent<RectTransform>();
        Transform targetTile = _tileContainer.GetChild(to);

        // Unparent to Canvas so the piece renders above everything while moving
        piece.transform.SetParent(_tileContainer.parent, true);

        // Compute target world position
        Vector3 targetPos = targetTile.position;

        pieceRT.DOMove(targetPos, 0.17f).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            // Capture
            if (_board[to] != null)
                Destroy(_board[to].gameObject);

            // Reparent to target tile
            piece.transform.SetParent(targetTile, false);
            pieceRT.anchorMin = Vector2.zero;
            pieceRT.anchorMax = Vector2.one;
            pieceRT.sizeDelta = Vector2.zero;
            pieceRT.anchoredPosition = Vector2.zero;

            _board[to] = piece;
            _board[from] = null;
            piece.HasMoved = true;
            piece.Deselect();

            _isMoving = false;
            ToggleTurn();
        });
    }

    private void ToggleTurn()
    {
        _currentTurn = (_currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
    }

    // --- Move Indicators ---

    private void ShowIndicators()
    {
        int fromCol = _selectedIndex % 8;
        int fromRow = _selectedIndex / 8;

        foreach (int idx in _validMoves)
        {
            Transform tile = _tileContainer.GetChild(idx);

            GameObject dot = new GameObject("MoveIndicator");
            dot.transform.SetParent(tile, false);
            dot.transform.localScale = Vector3.zero;

            RectTransform rt = dot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image img = dot.AddComponent<Image>();
            img.sprite = _moveIndicatorSprite;
            img.raycastTarget = false;

            int dc = (idx % 8) - fromCol;
            int dr = (idx / 8) - fromRow;
            float dist = Mathf.Sqrt(dc * dc + dr * dr);
            float delay = dist * 0.012f;

            dot.transform.DOScale(Vector3.one, 0.17f).SetEase(Ease.OutCubic).SetDelay(delay);

            _indicators.Add(dot);
        }
    }

    private void ClearIndicators()
    {
        foreach (GameObject dot in _indicators)
        {
            GameObject d = dot;
            d.transform.DOScale(Vector3.zero, 0.057f).SetEase(Ease.OutCubic)
                .OnComplete(() => Destroy(d));
        }
        _indicators.Clear();
    }

    private void Deselect()
    {
        if (_selectedIndex == -1) return;
        _board[_selectedIndex]?.Deselect();
        ClearIndicators();
        _selectedIndex = -1;
        _validMoves.Clear();
    }
}
