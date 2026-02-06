using System.Collections.Generic;
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

        // Clicked a valid move target -> execute
        if (_validMoves.Contains(index))
        {
            ExecuteMove(_selectedIndex, index);
            Deselect();
            ToggleTurn();
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
        _validMoves.Clear();
        _validMoves.AddRange(piece.GetPossibleMoves(index, _board));
        ShowIndicators();
    }

    private void ExecuteMove(int from, int to)
    {
        // Capture
        if (_board[to] != null)
            Destroy(_board[to].gameObject);

        // Reparent piece to target tile
        ChessPiece piece = _board[from];
        Transform targetTile = _tileContainer.GetChild(to);
        piece.transform.SetParent(targetTile, false);

        RectTransform rt = piece.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        _board[to] = piece;
        _board[from] = null;
        piece.HasMoved = true;
    }

    private void ToggleTurn()
    {
        _currentTurn = (_currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
    }

    // --- Move Indicators ---

    private void ShowIndicators()
    {
        foreach (int idx in _validMoves)
        {
            Transform tile = _tileContainer.GetChild(idx);

            GameObject dot = new GameObject("MoveIndicator");
            dot.transform.SetParent(tile, false);

            RectTransform rt = dot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image img = dot.AddComponent<Image>();
            img.sprite = _moveIndicatorSprite;
            img.raycastTarget = false;

            _indicators.Add(dot);
        }
    }

    private void ClearIndicators()
    {
        foreach (GameObject dot in _indicators)
            Destroy(dot);
        _indicators.Clear();
    }

    private void Deselect()
    {
        if (_selectedIndex == -1) return;
        ClearIndicators();
        _selectedIndex = -1;
        _validMoves.Clear();
    }
}
