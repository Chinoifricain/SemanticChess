using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

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
    [SerializeField] private SpriteRenderer _boardSprite;
    [SerializeField] private GameObject _piecePrefab;

    [Header("Move Indicators")]
    [SerializeField] private Sprite _moveIndicatorSprite;

    [Header("Tile Effect Sprites")]
    [SerializeField] private TileEffectSpriteEntry[] _tileEffectSprites;

    [Header("Floating Text")]
    [SerializeField] private TMP_FontAsset _floatingTextFont;

    private readonly ChessPiece[] _board = new ChessPiece[64];
    public ChessPiece GetPiece(int index) => _board[index];

    // --- Board geometry ---
    private readonly Vector3[] _tilePositions = new Vector3[64];
    private float _tileSize;
    private Camera _cam;

    // --- Element Service ---
    private ElementService _elementService;
    private EmojiLoader _emojiService;

    private static readonly string[] BackRankElements = { "Water", "Fire", "Plant", "Fire", "Water", "Plant", "Fire", "Water" };
    private static readonly string[] PawnElements =     { "Fire", "Plant", "Water", "Fire", "Plant", "Water", "Fire", "Plant" };

    private static readonly Dictionary<string, string> BaseEmojis = new()
    {
        { "Fire",  "\U0001F525" }, // 🔥
        { "Water", "\U0001F4A7" }, // 💧
        { "Plant", "\U0001F33F" }, // 🌿
    };

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
    private int _hoveredIndex = -1;

    private void Start()
    {
        _cam = Camera.main;

        for (int i = 0; i < 64; i++)
            _tileEffects[i] = new List<TileEffect>();

        _elementService = gameObject.AddComponent<ElementService>();
        _emojiService = gameObject.AddComponent<EmojiLoader>();

        ComputeTilePositions();
        SetupBoard();
    }

    // --- Board Geometry ---

    private void ComputeTilePositions()
    {
        Bounds b = _boardSprite.bounds;
        _tileSize = b.size.x / 8f;
        Vector3 topLeft = new Vector3(b.min.x, b.max.y, 0f);

        for (int i = 0; i < 64; i++)
        {
            int col = i % 8;
            int row = i / 8;
            _tilePositions[i] = new Vector3(
                topLeft.x + (col + 0.5f) * _tileSize,
                topLeft.y - (row + 0.5f) * _tileSize,
                0f);
        }
    }

    public Vector3 GetTilePosition(int index) => _tilePositions[index];
    public float TileSize => _tileSize;

    // --- Input ---

    private void Update()
    {
        UpdateHover();

        if (_isMoving) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        Bounds b = _boardSprite.bounds;
        float localX = world.x - b.min.x;
        float localY = b.max.y - world.y;
        int col = Mathf.FloorToInt(localX / _tileSize);
        int row = Mathf.FloorToInt(localY / _tileSize);

        if (col >= 0 && col < 8 && row >= 0 && row < 8)
            OnTileClicked(row * 8 + col);
    }

    private void UpdateHover()
    {
        if (_hoveredIndex >= 0 && _board[_hoveredIndex] == null)
            _hoveredIndex = -1;

        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        Bounds b = _boardSprite.bounds;
        float localX = world.x - b.min.x;
        float localY = b.max.y - world.y;
        int col = Mathf.FloorToInt(localX / _tileSize);
        int row = Mathf.FloorToInt(localY / _tileSize);

        int newHovered = -1;
        if (col >= 0 && col < 8 && row >= 0 && row < 8)
        {
            int idx = row * 8 + col;
            if (_board[idx] != null)
                newHovered = idx;
        }

        if (newHovered == _hoveredIndex) return;

        if (_hoveredIndex >= 0 && _board[_hoveredIndex] != null)
            _board[_hoveredIndex].ShowHover(false);

        _hoveredIndex = newHovered;

        if (_hoveredIndex >= 0)
            _board[_hoveredIndex].ShowHover(true);
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
            SpawnPiece(col, backRank[col], PieceColor.Black);
            SpawnPiece(8 + col, PieceType.Pawn, PieceColor.Black);
            SpawnPiece(48 + col, PieceType.Pawn, PieceColor.White);
            SpawnPiece(56 + col, backRank[col], PieceColor.White);
        }
    }

    private void SpawnPiece(int index, PieceType type, PieceColor color)
    {
        GameObject go = Instantiate(_piecePrefab);
        go.transform.position = _tilePositions[index];

        ChessPiece piece = go.GetComponent<ChessPiece>();
        piece.Init(type, color);

        int col = index % 8;
        int row = index / 8;
        bool isPawn = (color == PieceColor.White) ? row == 6 : row == 1;
        string element = isPawn ? PawnElements[col] : BackRankElements[col];
        string emoji = BaseEmojis.TryGetValue(element, out string e) ? e : "";
        piece.SetElement(element, emoji, _emojiService, _floatingTextFont);

        _board[index] = piece;
    }

    // --- Selection & Movement ---

    private void OnTileClicked(int index)
    {
        if (_isMoving) return;

        if (_selectedIndex == -1)
        {
            TrySelect(index);
            return;
        }

        if (index == _selectedIndex)
        {
            Deselect();
            return;
        }

        if (_board[index] != null && _board[index].Color == _currentTurn
            && !_board[index].HasEffect(EffectType.Stun))
        {
            Deselect();
            TrySelect(index);
            return;
        }

        if (_validMoves.Contains(index))
        {
            int from = _selectedIndex;
            ClearIndicators();
            _selectedIndex = -1;
            _validMoves.Clear();
            ExecuteMove(from, index);
            return;
        }

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
        bool isCapture = _board[to] != null && !_board[to].HasEffect(EffectType.Shield);

        AnimateToTile(piece, to, 0.17f, () =>
        {
            if (isCapture)
            {
                StartCoroutine(HandleCapture(from, to, piece, _board[to]));
            }
            else
            {
                FinishMove(from, to, piece);
            }
        });
    }

    private IEnumerator HandleCapture(int from, int to, ChessPiece attacker, ChessPiece defender)
    {
        string atkElem = attacker.Element;
        string defElem = defender.Element;

        attacker.PlayFightParticle();

        ElementMixResult result = null;
        yield return _elementService.GetElementMix(atkElem, defElem, r => result = r);

        attacker.StopFightParticle();

        if (result == null)
        {
            result = new ElementMixResult
            {
                newElement = atkElem,
                emoji = attacker.Emoji,
                winningElement = atkElem,
                reasoning = "API unavailable"
            };
        }

        Destroy(defender.gameObject);
        _board[to] = attacker;
        _board[from] = null;
        attacker.HasMoved = true;
        attacker.Deselect();

        attacker.SetElement(result.newElement, result.emoji, _emojiService, _floatingTextFont);

        SpawnMixText(to, result, atkElem, defElem);

        yield return new WaitForSeconds(1.5f);

        if (TileHasEffect(to, TileEffectType.Ice))
        {
            int dirCol = System.Math.Sign((to % 8) - (from % 8));
            int dirRow = System.Math.Sign((to / 8) - (from / 8));
            int slideTarget = CalculateIceSlide(to, dirCol, dirRow);
            if (slideTarget != to)
            {
                _board[slideTarget] = attacker;
                _board[to] = null;
                bool sliding = true;
                AnimateToTile(attacker, slideTarget, 0.15f, () => sliding = false);
                while (sliding) yield return null;
                _isMoving = false;
                ToggleTurn();
                yield break;
            }
        }

        _isMoving = false;
        ToggleTurn();
    }

    private void FinishMove(int from, int to, ChessPiece piece)
    {
        _board[to] = piece;
        _board[from] = null;
        piece.HasMoved = true;
        piece.Deselect();

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
    }

    private void AnimateToTile(ChessPiece piece, int tileIndex, float duration, TweenCallback onComplete = null)
    {
        piece.SetSortingLayer("front");

        piece.transform.DOMove(_tilePositions[tileIndex], duration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            piece.SetSortingLayer("piece");
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
                if (effect.Type == EffectType.Convert)
                    piece.SetColor(piece.OriginalColor);
            }
        }
    }

    // --- Effects ---

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
                return;

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

            if (TileHasEffect(i, TileEffectType.Plant) && piece != null)
            {
                if (!piece.HasEffect(EffectType.Stun))
                {
                    piece.AddEffect(new ChessEffect(EffectType.Stun, 2));
                    RemoveTileEffectByType(i, TileEffectType.Plant);
                }
            }

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
            GameObject dot = new GameObject("MoveIndicator");
            dot.transform.position = _tilePositions[idx];
            dot.transform.localScale = Vector3.zero;

            SpriteRenderer sr = dot.AddComponent<SpriteRenderer>();
            sr.sprite = _moveIndicatorSprite;
            sr.sortingLayerName = "front";
            sr.sortingOrder = 0;

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

        GameObject go = new GameObject($"TileEffect_{effect.Type}");
        go.transform.position = _tilePositions[index];

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "default";
        sr.sortingOrder = 0;

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
        Vector3 pos = _tilePositions[index];

        GameObject go = new GameObject("FloatingText");
        go.transform.position = pos;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("front");
        label.sortingOrder = 1;

        go.transform.DOMoveY(pos.y + _tileSize * 0.6f, 0.6f).SetEase(Ease.OutCubic);
        label.DOFade(0f, 0.6f).SetEase(Ease.InCubic).OnComplete(() => Destroy(go));
    }

    private void SpawnMixText(int index, ElementMixResult result, string atkElem, string defElem)
    {
        SpawnFloatingTextStyled(_tilePositions[index], $"\u2192 {result.newElement}", Color.white, 0f, 0f);

        string winText;
        Color winColor;
        if (result.winningElement == "draw" || string.IsNullOrEmpty(result.winningElement))
        {
            winText = "Even match";
            winColor = new Color(0.8f, 0.8f, 0.8f);
        }
        else if (string.Equals(result.winningElement, atkElem, System.StringComparison.OrdinalIgnoreCase))
        {
            winText = $"{atkElem} wins!";
            winColor = new Color(0.3f, 1f, 0.3f);
        }
        else
        {
            winText = $"{defElem} resists!";
            winColor = new Color(1f, 0.5f, 0.3f);
        }

        SpawnFloatingTextStyled(_tilePositions[index], winText, winColor, 0.25f, -_tileSize * 0.2f);
    }

    private void SpawnFloatingTextStyled(Vector3 pos, string text, Color color, float delay, float yOffset)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.position = pos + new Vector3(0f, yOffset, 0f);

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 3.5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(color.r, color.g, color.b, 0f);
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("front");
        label.sortingOrder = 1;

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        seq.Append(label.DOFade(1f, 0.15f));
        seq.AppendInterval(0.8f);
        seq.Append(go.transform.DOMoveY(go.transform.position.y + _tileSize * 0.4f, 0.5f).SetEase(Ease.OutCubic));
        seq.Join(label.DOFade(0f, 0.5f).SetEase(Ease.InCubic));
        seq.OnComplete(() => Destroy(go));
    }
}
