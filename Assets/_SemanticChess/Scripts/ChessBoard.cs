using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct TileEffectSpriteEntry
{
    public TileEffectType type;
    public Sprite sprite;
}

[DefaultExecutionOrder(-1)]
public class ChessBoard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _tileContainer;
    [SerializeField] private GameObject _piecePrefab;

    [Header("Move Indicators")]
    [SerializeField] private Sprite _moveIndicatorSprite;

    [Header("Tile Effect Sprites")]
    [SerializeField] private TileEffectSpriteEntry[] _tileEffectSprites;

    [Header("Floating Text")]
    [SerializeField] private TMP_FontAsset _floatingTextFont;

    private readonly ChessPiece[] _board = new ChessPiece[64];
    public ChessPiece GetPiece(int index) => _board[index];

    // --- Tile Effects ---
    private readonly List<TileEffect>[] _tileEffects = new List<TileEffect>[64];
    private readonly ChessPiece[] _tileOccupant = new ChessPiece[64];
    private readonly int[] _burningTurnCount = new int[64];
    private readonly Dictionary<TileEffect, GameObject> _tileEffectVisuals = new Dictionary<TileEffect, GameObject>();

    private PieceColor _currentTurn = PieceColor.White;
    private int _selectedIndex = -1;
    private readonly List<int> _validMoves = new List<int>();
    private readonly List<GameObject> _indicators = new List<GameObject>();
    private bool _isMoving;
    private static readonly Color Transparent = new Color(0, 0, 0, 0);

    private void Start()
    {
        for (int i = 0; i < 64; i++)
            _tileEffects[i] = new List<TileEffect>();

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
        if (_board[index] != null && _board[index].Color == _currentTurn
            && !_board[index].HasEffect(EffectType.Stun))
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
        if (piece.HasEffect(EffectType.Stun)) return;

        _selectedIndex = index;
        piece.Select();
        _validMoves.Clear();
        _validMoves.AddRange(piece.GetPossibleMoves(index, _board));
        _validMoves.RemoveAll(i => TileHasEffect(i, TileEffectType.Occupied));
        ShowIndicators();
    }

    private void ExecuteMove(int from, int to)
    {
        _isMoving = true;

        ChessPiece piece = _board[from];

        AnimateToTile(piece, to, 0.17f, () =>
        {
            // Capture (safety: skip if shielded)
            if (_board[to] != null && !_board[to].HasEffect(EffectType.Shield))
                Destroy(_board[to].gameObject);

            _board[to] = piece;
            _board[from] = null;
            piece.HasMoved = true;
            piece.Deselect();

            // Ice: slide in movement direction
            if (TileHasEffect(to, TileEffectType.Ice))
            {
                int dirCol = System.Math.Sign((to % 8) - (from % 8));
                int dirRow = System.Math.Sign((to / 8) - (from / 8));
                int slideTarget = CalculateIceSlide(to, dirCol, dirRow);
                if (slideTarget != to)
                {
                    _board[slideTarget] = piece;
                    _board[to] = null;
                    AnimateToTile(piece, slideTarget, 0.15f, () =>
                    {
                        _isMoving = false;
                        ToggleTurn();
                    });
                    return;
                }
            }

            _isMoving = false;
            ToggleTurn();
        });
    }

    private void AnimateToTile(ChessPiece piece, int tileIndex, float duration, TweenCallback onComplete = null)
    {
        Transform targetTile = _tileContainer.GetChild(tileIndex);
        RectTransform pieceRT = piece.GetComponent<RectTransform>();

        // Unparent to Canvas so the piece renders above everything while moving
        piece.transform.SetParent(_tileContainer.parent, true);

        pieceRT.DOMove(targetTile.position, duration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            piece.transform.SetParent(targetTile, false);
            pieceRT.anchorMin = Vector2.zero;
            pieceRT.anchorMax = Vector2.one;
            pieceRT.sizeDelta = Vector2.zero;
            pieceRT.anchoredPosition = Vector2.zero;

            onComplete?.Invoke();
        });
    }

    private void ToggleTurn()
    {
        TickAllEffects();
        ProcessTileEffects();
        _currentTurn = (_currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
    }

    private void TickAllEffects()
    {
        for (int i = 0; i < 64; i++)
        {
            ChessPiece piece = _board[i];
            if (piece == null) continue;

            var expired = piece.TickEffects();
            if (expired == null) continue;

            foreach (var effect in expired)
            {
                // Convert reverts to original color on expiry
                if (effect.Type == EffectType.Convert)
                    piece.SetColor(piece.OriginalColor);
            }
        }
    }

    // --- Effects ---

    /// <summary>
    /// Applies an effect to the piece at the given board index.
    /// Immediate effects (Damage, Push, Swap, Convert) execute their action right away.
    /// Persistent effects (Stun, Shield) are tagged on the piece and checked during gameplay.
    /// </summary>
    public void ApplyEffect(int index, ChessEffect effect)
    {
        ChessPiece piece = _board[index];
        if (piece == null) return;

        switch (effect.Type)
        {
            case EffectType.Damage:
                if (piece.HasEffect(EffectType.Shield)) return;
                SpawnFloatingText(index, "Destroy!");
                if (index == _selectedIndex) Deselect();
                _board[index] = null;
                Destroy(piece.gameObject);
                return; // no tag to add

            case EffectType.Push:
                SpawnFloatingText(index, "Push!");
                piece.AddEffect(effect);
                ExecutePush(index, effect.PushDirCol, effect.PushDirRow, effect.PushDistance);
                return;

            case EffectType.Swap:
                SpawnFloatingText(index, "Swap!");
                piece.AddEffect(effect);
                ExecuteSwap(index, effect.SwapTargetIndex);
                return;

            case EffectType.Convert:
                SpawnFloatingText(index, "Convert!");
                piece.AddEffect(effect);
                PieceColor newColor = (piece.Color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                piece.SetColor(newColor);
                return;

            case EffectType.Stun:
            case EffectType.Shield:
                piece.AddEffect(effect);
                if (effect.Type == EffectType.Stun && index == _selectedIndex)
                    Deselect();
                return;
        }
    }

    private void ExecutePush(int index, int dirCol, int dirRow, int distance)
    {
        ChessPiece piece = _board[index];
        if (piece == null) return;

        int col = index % 8;
        int row = index / 8;
        int finalCol = col;
        int finalRow = row;

        for (int step = 1; step <= distance; step++)
        {
            int nc = col + dirCol * step;
            int nr = row + dirRow * step;
            if (nc < 0 || nc >= 8 || nr < 0 || nr >= 8) break;
            if (_board[nr * 8 + nc] != null) break;
            finalCol = nc;
            finalRow = nr;
        }

        int targetIndex = finalRow * 8 + finalCol;
        if (targetIndex == index) return;

        _board[targetIndex] = piece;
        _board[index] = null;
        AnimateToTile(piece, targetIndex, 0.2f);
    }

    private void ExecuteSwap(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= 64 || indexB < 0 || indexB >= 64) return;

        ChessPiece pieceA = _board[indexA];
        ChessPiece pieceB = _board[indexB];
        if (pieceA == null && pieceB == null) return;

        _board[indexA] = pieceB;
        _board[indexB] = pieceA;

        if (pieceA != null) AnimateToTile(pieceA, indexB, 0.2f);
        if (pieceB != null) AnimateToTile(pieceB, indexA, 0.2f);
    }

    // --- Tile Effects ---

    public void AddTileEffect(int index, TileEffect effect)
    {
        _tileEffects[index].Add(effect);
        CreateTileEffectVisual(index, effect);
    }

    public void RemoveTileEffect(int index, TileEffect effect)
    {
        _tileEffects[index].Remove(effect);
        DestroyTileEffectVisual(effect);
    }

    public IReadOnlyList<TileEffect> GetTileEffects(int index) => _tileEffects[index];

    public bool TileHasEffect(int index, TileEffectType type)
    {
        var list = _tileEffects[index];
        for (int i = 0; i < list.Count; i++)
            if (list[i].Type == type) return true;
        return false;
    }

    private void RemoveTileEffectByType(int index, TileEffectType type)
    {
        var list = _tileEffects[index];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Type == type)
            {
                DestroyTileEffectVisual(list[i]);
                list.RemoveAt(i);
                return;
            }
        }
    }

    private int CalculateIceSlide(int from, int dirCol, int dirRow)
    {
        int col = from % 8;
        int row = from / 8;
        int curCol = col;
        int curRow = row;

        while (true)
        {
            int nc = curCol + dirCol;
            int nr = curRow + dirRow;
            if (nc < 0 || nc >= 8 || nr < 0 || nr >= 8) break;
            int nextIdx = nr * 8 + nc;
            if (_board[nextIdx] != null) break;

            curCol = nc;
            curRow = nr;

            // Stop if this tile is not ice (we've slid off onto solid ground)
            if (!TileHasEffect(nextIdx, TileEffectType.Ice)) break;
        }

        return curRow * 8 + curCol;
    }

    private void ProcessTileEffects()
    {
        for (int i = 0; i < 64; i++)
        {
            var list = _tileEffects[i];
            if (list.Count == 0) { _burningTurnCount[i] = 0; _tileOccupant[i] = null; continue; }

            ChessPiece piece = _board[i];

            // Burning
            if (TileHasEffect(i, TileEffectType.Burning))
            {
                if (piece != null)
                {
                    if (piece != _tileOccupant[i])
                        _burningTurnCount[i] = 0;
                    _tileOccupant[i] = piece;
                    _burningTurnCount[i]++;
                    if (_burningTurnCount[i] >= 3)
                    {
                        _burningTurnCount[i] = 0;
                        _tileOccupant[i] = null;
                        ApplyEffect(i, new ChessEffect(EffectType.Damage));
                    }
                }
                else
                {
                    _burningTurnCount[i] = 0;
                    _tileOccupant[i] = null;
                }
            }

            // Plant (one-shot trap)
            if (TileHasEffect(i, TileEffectType.Plant) && piece != null)
            {
                if (!piece.HasEffect(EffectType.Stun))
                {
                    piece.AddEffect(new ChessEffect(EffectType.Stun, 2));
                    RemoveTileEffectByType(i, TileEffectType.Plant);
                }
            }

            // Tick tile effect durations, remove expired
            for (int j = list.Count - 1; j >= 0; j--)
            {
                if (list[j].Tick())
                {
                    DestroyTileEffectVisual(list[j]);
                    list.RemoveAt(j);
                }
            }
        }
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

            Canvas dotCanvas = dot.AddComponent<Canvas>();
            dotCanvas.overrideSorting = true;
            dotCanvas.sortingOrder = 10;

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

    // --- Tile Effect Visuals ---

    private Sprite GetTileEffectSprite(TileEffectType type)
    {
        if (_tileEffectSprites == null) return null;
        foreach (var entry in _tileEffectSprites)
            if (entry.type == type) return entry.sprite;
        return null;
    }

    private void CreateTileEffectVisual(int index, TileEffect effect)
    {
        Sprite sprite = GetTileEffectSprite(effect.Type);
        if (sprite == null) return;

        Transform tile = _tileContainer.GetChild(index);
        GameObject go = new GameObject($"TileEffect_{effect.Type}");
        go.transform.SetParent(tile, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        _tileEffectVisuals[effect] = go;
    }

    private void DestroyTileEffectVisual(TileEffect effect)
    {
        if (_tileEffectVisuals.TryGetValue(effect, out GameObject go))
        {
            if (go != null) Destroy(go);
            _tileEffectVisuals.Remove(effect);
        }
    }

    // --- Floating Text ---

    private void SpawnFloatingText(int index, string text)
    {
        Transform tile = _tileContainer.GetChild(index);

        GameObject go = new GameObject("FloatingText");
        go.transform.SetParent(_tileContainer.parent, true);
        go.transform.position = tile.position;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(3f, 0.5f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 0.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        rt.DOAnchorPosY(rt.anchoredPosition.y + 40f, 0.6f).SetEase(Ease.OutCubic);
        label.DOFade(0f, 0.6f).SetEase(Ease.InCubic).OnComplete(() => Destroy(go));
    }
}
