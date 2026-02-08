using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;

[System.Serializable]
public struct TileEffectSpriteEntry
{
    public TileEffectType type;
    public Sprite sprite;
}

public struct CapturedPieceInfo
{
    public PieceType PieceType;
    public PieceColor Color;
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

    [Header("Effects")]
    [SerializeField] private ParticleSystem _effectHitPrefab;

    [Header("Floating Text")]
    [SerializeField] private TMP_FontAsset _floatingTextFont;

    [Header("Video Mode")]
    [SerializeField] private TMP_FontAsset _captionFont;
    public TMP_FontAsset CaptionFont => _captionFont;

    [Header("Tile Coordinates")]
    [SerializeField] private TMP_FontAsset _tileCoordFont;
    [SerializeField] private Sprite _infoIconSprite;

    private readonly ChessPiece[] _board = new ChessPiece[64];
    public ChessPiece GetPiece(int index) => _board[index];

    // --- Board geometry ---
    private readonly Vector3[] _tilePositions = new Vector3[64];
    private float _tileSize;
    private Camera _cam;

    // --- Element Service ---
    private ElementService _elementService;
    private EmojiLoader _emojiService;

    public static readonly string[] BackRankElements = { "Water", "Fire", "Plant", "Air", "Water", "Plant", "Fire", "Air" };
    public static readonly string[] PawnElements =     { "Fire", "Plant", "Water", "Air", "Fire", "Plant", "Water", "Air" };

    private BoardLayoutData _activeLayout;

    private static readonly Dictionary<string, string> BaseEmojis = new()
    {
        { "Fire",  "\U0001F525" }, // 🔥
        { "Water", "\U0001F4A7" }, // 💧
        { "Plant", "\U0001F33F" }, // 🌿
        { "Air",   "\U0001F4A8" }, // 💨
    };

    // --- Tile Effects ---
    private readonly List<TileEffect>[] _tileEffects = new List<TileEffect>[64];
    private readonly ChessPiece[] _tileOccupant = new ChessPiece[64];
    private readonly int[] _burningTurnCount = new int[64];
    private readonly ChessPiece[] _plantOccupant = new ChessPiece[64];
    private readonly int[] _plantTurnCount = new int[64];
    private readonly Dictionary<TileEffect, GameObject> _tileEffectVisuals = new Dictionary<TileEffect, GameObject>();

    // --- Captured Pieces ---
    private readonly List<CapturedPieceInfo> _capturedWhitePieces = new List<CapturedPieceInfo>();
    private readonly List<CapturedPieceInfo> _capturedBlackPieces = new List<CapturedPieceInfo>();

    // --- Board attraction ---
    private const float BoardAttractRadius = 12f;
    private const float BoardAttractStrength = 0.03f;
    private Vector3 _boardRestPos;
    private readonly HashSet<Transform> _staticChildren = new HashSet<Transform>();

    private bool _flipped;

    private PieceColor _currentTurn = PieceColor.White;
    private int _selectedIndex = -1;
    private readonly List<int> _validMoves = new List<int>();
    private readonly List<GameObject> _indicators = new List<GameObject>();
    private bool _isMoving;
    private bool _isPlayingReaction;
    private bool _gameOver;
    private int _hoveredIndex = -1;
    private int _hoveredTileIndex = -1;
    private TextMeshPro _tileCoordLabel;
    private Tween _tileCoordTween;
    private SpriteRenderer _infoIcon;
    private Tween _infoIconTween;
    private Tween _infoIconHoverTween;
    private static readonly float InfoIconRestScale = 1.5f;
    private static readonly float InfoIconHoverScale = 2.0f;

    // --- Public API ---
    public PieceColor CurrentTurn => _currentTurn;
    public bool IsMoving => _isMoving;
    public bool IsGameOver => _gameOver;
    public bool IsPlayingReaction => _isPlayingReaction;
    public int SelectedIndex => _selectedIndex;
    public SpriteRenderer BoardSprite => _boardSprite;
    public bool IsFlipped => _flipped;
    public ElementService ElementService => _elementService;
    public EmojiLoader EmojiService => _emojiService;
    public TMP_FontAsset FloatingTextFont => _floatingTextFont;
    public ChessPiece GetPieceAt(int index) => (index >= 0 && index < 64) ? _board[index] : null;
    public int HoveredTileIndex => _hoveredTileIndex;
    public float TileSize => _tileSize;

    // --- Events ---
    public event Action<PieceColor> OnTurnChanged;
    public event Action<MatchResult> OnGameOver;
    public event Action<int, int> OnMoveMade;
    public event Action<int, int, ElementMixResult, ElementReactionResult> OnCaptureResult;
    public event Action<int> OnHoverChanged;
    public event Action<int> OnTileHoverChanged;
    public event Action<int> OnPieceSelected;
    public event Action OnPieceDeselected;
    public event Action<CapturedPieceInfo> OnPieceCaptured;
    public event Action OnBoardReset;

    // --- Tutorial pause hook ---
    public System.Func<string, int, ElementMixResult, IEnumerator> TutorialPause;

    // --- Online: pre-computed reaction from opponent ---
    private ElementMixResult _pendingMix;
    private ElementReactionResult _pendingReaction;

    /// <summary>
    /// When true, HandleCapture will wait for SetPendingReaction() instead of calling the API.
    /// Set by OnlineGameMode when receiving an opponent move that is a capture.
    /// </summary>
    public bool WaitForPendingData { get; set; }

    public void SetPendingReaction(ElementMixResult mix, ElementReactionResult reaction)
    {
        _pendingMix = mix;
        _pendingReaction = reaction;
    }

    /// <summary>
    /// Play a reaction directly at a tile (no capture needed). Used by video mode.
    /// </summary>
    public Coroutine PlayReaction(int centerIndex, ElementReactionResult reaction, PieceColor attackerColor, string tradeOutcome)
    {
        return StartCoroutine(ApplyReaction(centerIndex, reaction, attackerColor, tradeOutcome));
    }

    public void SetFlipped(bool flipped)
    {
        _flipped = flipped;
        ComputeTilePositions();
    }

    /// <summary>
    /// Whether the board has been initialized (Start has run).
    /// GameManager checks this to know when it's safe to call ResetBoard.
    /// </summary>
    public bool IsInitialized { get; private set; }

    private void Start()
    {
        _cam = Camera.main;
        _boardRestPos = transform.position;

        for (int i = 0; i < 64; i++)
            _tileEffects[i] = new List<TileEffect>();

        _elementService = gameObject.AddComponent<ElementService>();
        _emojiService = gameObject.AddComponent<EmojiLoader>();

        ComputeTilePositions();

        // Tile coordinate label (shown on hover)
        GameObject coordGo = new GameObject("TileCoordLabel");
        coordGo.transform.SetParent(transform, false);
        _tileCoordLabel = coordGo.AddComponent<TextMeshPro>();
        _tileCoordLabel.rectTransform.sizeDelta = new Vector2(1f, 0.3f);
        if (_tileCoordFont != null) _tileCoordLabel.font = _tileCoordFont;
        _tileCoordLabel.fontSize = 2f;
        _tileCoordLabel.alignment = TextAlignmentOptions.Center;
        _tileCoordLabel.color = new Color(1f, 1f, 1f, 0.45f);
        _tileCoordLabel.raycastTarget = false;
        _tileCoordLabel.sortingOrder = 10;
        coordGo.SetActive(false);

        // Info icon (shown on hover when tile has content)
        GameObject infoGo = new GameObject("InfoIcon");
        infoGo.transform.SetParent(transform, false);
        _infoIcon = infoGo.AddComponent<SpriteRenderer>();
        _infoIcon.sprite = _infoIconSprite;
        _infoIcon.color = new Color(1f, 1f, 1f, 0.5f);
        _infoIcon.sortingLayerName = "Front";
        _infoIcon.sortingOrder = 10;
        infoGo.SetActive(false);

        // Track scene children so ResetBoard doesn't destroy them
        foreach (Transform child in transform)
            _staticChildren.Add(child);

        IsInitialized = true;
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
            int visualCol = _flipped ? (7 - col) : col;
            int visualRow = _flipped ? (7 - row) : row;
            Vector3 worldPos = new Vector3(
                topLeft.x + (visualCol + 0.5f) * _tileSize,
                topLeft.y - (visualRow + 0.5f) * _tileSize,
                0f);
            _tilePositions[i] = transform.InverseTransformPoint(worldPos);
        }
    }

    public Vector3 GetTilePosition(int index) => transform.TransformPoint(_tilePositions[index]);
    public Vector3 GetTileLocalPosition(int index) => _tilePositions[index];

    /// <summary>
    /// Convert a world position to board tile index. Returns -1 if outside the board.
    /// </summary>
    public int WorldToTileIndex(Vector3 worldPos)
    {
        Bounds b = _boardSprite.bounds;
        float localX = worldPos.x - b.min.x;
        float localY = b.max.y - worldPos.y;
        int col = Mathf.FloorToInt(localX / _tileSize);
        int row = Mathf.FloorToInt(localY / _tileSize);
        if (col >= 0 && col < 8 && row >= 0 && row < 8)
        {
            if (_flipped) { col = 7 - col; row = 7 - row; }
            return row * 8 + col;
        }
        return -1;
    }

    // --- Public Move API ---

    /// <summary>
    /// Single entry point for all game modes to submit moves.
    /// Validates the move is legal, then executes it.
    /// </summary>
    public bool SubmitMove(MoveRequest move)
    {
        if (_isMoving || _gameOver) return false;
        if (move.Player != _currentTurn) return false;

        ChessPiece piece = _board[move.FromIndex];
        if (piece == null || piece.Color != _currentTurn) return false;

        var legal = GetLegalMoves(move.FromIndex);
        if (!legal.Contains(move.ToIndex)) return false;

        OnMoveMade?.Invoke(move.FromIndex, move.ToIndex);
        ExecuteMove(move.FromIndex, move.ToIndex);
        return true;
    }

    /// <summary>
    /// Get legal moves for a piece at the given index. Returns empty list if invalid.
    /// </summary>
    public List<int> GetLegalMovesFor(int index)
    {
        return GetLegalMoves(index);
    }

    // --- Selection (called by game modes) ---

    public void SelectPiece(int index)
    {
        TrySelect(index);
    }

    public void DeselectPiece()
    {
        Deselect();
    }

    public void SetHoveredIndex(int index)
    {
        if (index == _hoveredIndex) return;

        if (_hoveredIndex >= 0 && _board[_hoveredIndex] != null)
            _board[_hoveredIndex].ShowHover(false);

        _hoveredIndex = index;

        if (_hoveredIndex >= 0 && _board[_hoveredIndex] != null)
            _board[_hoveredIndex].ShowHover(true);

        OnHoverChanged?.Invoke(index);
    }

    public void SetHoveredTileIndex(int index)
    {
        if (index == _hoveredTileIndex) return;
        _hoveredTileIndex = index;

        _tileCoordTween?.Kill();
        _infoIconTween?.Kill();

        if (index < 0)
        {
            _tileCoordLabel.gameObject.SetActive(false);
            _infoIcon.gameObject.SetActive(false);
            OnTileHoverChanged?.Invoke(index);
            return;
        }

        float half = _tileSize * 0.5f;
        _tileCoordLabel.transform.localPosition = _tilePositions[index] + new Vector3(-half + _tileSize * 0.15f, -half + _tileSize * 0.15f, 0f);
        _tileCoordLabel.text = IndexToAlgebraic(index);
        _tileCoordLabel.transform.localScale = Vector3.one * 2.0f;
        _tileCoordLabel.gameObject.SetActive(true);
        _tileCoordTween = _tileCoordLabel.transform.DOScale(Vector3.one, 0.08f).SetEase(Ease.OutCubic);

        // Info icon at top-left (only if tile has content)
        bool hasContent = _board[index] != null || _tileEffects[index].Count > 0;
        if (hasContent)
        {
            _infoIcon.transform.localPosition = _tilePositions[index] + new Vector3(-half + _tileSize * 0.15f, half - _tileSize * 0.15f, 0f);
            _infoIcon.transform.localScale = Vector3.one * (InfoIconRestScale * 2f);
            _infoIcon.gameObject.SetActive(true);
            _infoIconTween = _infoIcon.transform.DOScale(Vector3.one * InfoIconRestScale, 0.08f).SetEase(Ease.OutCubic);
        }
        else
        {
            _infoIcon.gameObject.SetActive(false);
        }

        OnTileHoverChanged?.Invoke(index);
    }

    public bool IsInfoIconHovered(Vector3 mouseWorldPos)
    {
        if (!_infoIcon.gameObject.activeSelf) return false;
        float radius = _tileSize * 0.35f;
        return Vector3.Distance(mouseWorldPos, _infoIcon.transform.position) < radius;
    }

    public void SetInfoIconHovered(bool hovered)
    {
        if (_infoIcon == null || !_infoIcon.gameObject.activeSelf) return;
        _infoIconHoverTween?.Kill();
        float target = hovered ? InfoIconHoverScale : InfoIconRestScale;
        _infoIconHoverTween = _infoIcon.transform
            .DOScale(Vector3.one * target, 0.1f)
            .SetEase(hovered ? Ease.OutBack : Ease.OutCubic);
    }

    public Vector3 GetTileWorldPosition(int index)
    {
        if (index < 0 || index >= 64) return Vector3.zero;
        return transform.TransformPoint(_tilePositions[index]);
    }

    // --- AI Helpers ---

    public List<(int from, int to)> GetAllLegalMoves(PieceColor color)
    {
        var result = new List<(int, int)>();
        for (int i = 0; i < 64; i++)
        {
            ChessPiece p = _board[i];
            if (p == null || p.Color != color) continue;
            if (p.HasEffect(EffectType.Stun)) continue;
            foreach (int to in GetLegalMoves(i))
                result.Add((i, to));
        }
        return result;
    }

    public string SerializeBoardForAI()
    {
        var sb = new StringBuilder();

        // Board grid
        for (int row = 0; row < 8; row++)
        {
            sb.Append($"{8 - row}: ");
            for (int col = 0; col < 8; col++)
            {
                if (col > 0) sb.Append(' ');
                ChessPiece p = _board[row * 8 + col];
                if (p == null) { sb.Append('.'); continue; }
                char c = p.PieceType switch
                {
                    PieceType.Pawn   => 'P',
                    PieceType.Knight => 'N',
                    PieceType.Bishop => 'B',
                    PieceType.Rook   => 'R',
                    PieceType.Queen  => 'Q',
                    PieceType.King   => 'K',
                    _ => '?'
                };
                sb.Append(p.Color == PieceColor.Black ? char.ToLower(c) : c);
            }
            sb.AppendLine();
        }
        sb.AppendLine("   a b c d e f g h");

        // Elements
        var elements = new List<string>();
        for (int i = 0; i < 64; i++)
        {
            ChessPiece p = _board[i];
            if (p == null || string.IsNullOrEmpty(p.Element)) continue;
            string colorLetter = p.Color == PieceColor.White ? "W" : "B";
            char pieceLetter = p.PieceType switch
            {
                PieceType.Pawn   => 'P',
                PieceType.Knight => 'N',
                PieceType.Bishop => 'B',
                PieceType.Rook   => 'R',
                PieceType.Queen  => 'Q',
                PieceType.King   => 'K',
                _ => '?'
            };
            elements.Add($"{IndexToAlgebraic(i)}={p.Element}{p.Emoji}({colorLetter}{pieceLetter})");
        }
        if (elements.Count > 0)
            sb.AppendLine($"Elements: {string.Join(", ", elements)}");

        // Active effects (pieces + tiles) — exclude environmental effects the AI can't act on
        var effects = new List<string>();
        for (int i = 0; i < 64; i++)
        {
            ChessPiece p = _board[i];
            if (p != null)
            {
                foreach (var e in p.Effects)
                    if (e.Type != EffectType.Burning && e.Type != EffectType.Plant)
                        effects.Add($"{IndexToAlgebraic(i)}:{e.Type}({e.Duration})");
            }
            foreach (var te in _tileEffects[i])
                effects.Add($"{IndexToAlgebraic(i)}-tile:{te.Type}({te.Duration})");
        }
        if (effects.Count > 0)
            sb.AppendLine($"Effects: {string.Join(", ", effects)}");

        sb.AppendLine($"Turn: {_currentTurn}");
        return sb.ToString();
    }

    // --- Board Lifecycle ---

    /// <summary>
    /// Reset the board for a new game. Clears all pieces, effects, and state,
    /// then sets up a fresh starting position.
    /// </summary>
    public void ResetBoard(BoardLayoutData layout = null)
    {
        _activeLayout = layout;
        ClearBoard();
        SetupBoard();
        OnBoardReset?.Invoke();
    }

    /// <summary>
    /// Clears all pieces, effects, and state without spawning new pieces.
    /// Used by ResetBoard and TutorialGameMode.
    /// </summary>
    public void ClearBoard()
    {
        StopAllCoroutines();
        TutorialPause = null;

        // Kill all tweens
        foreach (Transform child in transform)
            DOTween.Kill(child);

        // Destroy all pieces
        for (int i = 0; i < 64; i++)
        {
            if (_board[i] != null)
            {
                Destroy(_board[i].gameObject);
                _board[i] = null;
            }
        }

        // Clear tile effects
        for (int i = 0; i < 64; i++)
        {
            foreach (var te in _tileEffects[i])
                DestroyTileEffectVisual(te);
            _tileEffects[i].Clear();
            _burningTurnCount[i] = 0;
            _tileOccupant[i] = null;
            _plantTurnCount[i] = 0;
            _plantOccupant[i] = null;
        }

        // Destroy leftover dynamic GameObjects (floating text, indicators, fight titles)
        var toDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (_staticChildren.Contains(child)) continue;
            toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy)
            Destroy(go);

        // Reset state
        _currentTurn = PieceColor.White;
        _isMoving = false;
        _isPlayingReaction = false;
        _gameOver = false;
        _hoveredIndex = -1;
        _selectedIndex = -1;
        _pendingMix = null;
        _pendingReaction = null;
        WaitForPendingData = false;
        _validMoves.Clear();
        _indicators.Clear();
        _tileEffectVisuals.Clear();
        _capturedWhitePieces.Clear();
        _capturedBlackPieces.Clear();

        Time.timeScale = 1f;
    }

    /// <summary>
    /// Set up the board visually for the config screen (no game state).
    /// </summary>
    public void SetupForConfig(BoardLayoutData layout)
    {
        ResetBoard(layout);
    }

    // --- Input ---

    private void Update()
    {
        UpdateBoardAttraction();
    }

    private void UpdateBoardAttraction()
    {
        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Vector3 toMouse = mouseWorld - _boardRestPos;
        toMouse.z = 0f;
        float dist = toMouse.magnitude;

        float t = 1f - Mathf.Clamp01(dist / BoardAttractRadius);
        Vector3 offset = toMouse.normalized * (t * BoardAttractStrength);

        float smooth = Time.deltaTime * 8f;
        transform.position = Vector3.Lerp(transform.position, _boardRestPos + offset, smooth);
    }

    // Hover is now managed by game modes via SetHoveredIndex()

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
        GameObject go = Instantiate(_piecePrefab, transform);
        go.transform.localPosition = _tilePositions[index];

        ChessPiece piece = go.GetComponent<ChessPiece>();
        piece.Init(type, color);

        // Check custom layout first
        string element = null;
        string emoji = null;

        if (_activeLayout != null)
        {
            var slots = color == PieceColor.White ? _activeLayout.whiteSlots : _activeLayout.blackSlots;
            var custom = slots?.Find(s => s.index == index);
            if (custom != null)
            {
                element = custom.element;
                emoji = custom.emoji;
            }
        }

        // Fall back to defaults
        if (element == null)
        {
            int col = index % 8;
            int row = index / 8;
            bool isPawn = (color == PieceColor.White) ? row == 6 : row == 1;
            element = isPawn ? PawnElements[col] : BackRankElements[col];
            emoji = BaseEmojis.TryGetValue(element, out string e) ? e : "";
        }

        piece.SetElement(element, emoji, _emojiService, _floatingTextFont);

        _board[index] = piece;
    }

    /// <summary>
    /// Spawn a piece with an explicit element. Used by TutorialGameMode for custom board setups.
    /// </summary>
    public void SpawnPieceWithElement(int index, PieceType type, PieceColor color, string element, string emoji)
    {
        GameObject go = Instantiate(_piecePrefab, transform);
        go.transform.localPosition = _tilePositions[index];

        ChessPiece piece = go.GetComponent<ChessPiece>();
        piece.Init(type, color);
        piece.SetElement(element, emoji, _emojiService, _floatingTextFont);

        _board[index] = piece;
    }

    // --- Selection & Movement ---

    // OnTileClicked logic is now in game modes (LocalGameMode, etc.)

    private void TrySelect(int index)
    {
        ChessPiece piece = _board[index];
        if (piece == null || piece.Color != _currentTurn) return;
        if (piece.HasEffect(EffectType.Stun)) return;

        _selectedIndex = index;
        piece.Select();
        AudioManager.Instance?.PlayPieceSelect();
        _validMoves.Clear();
        _validMoves.AddRange(GetLegalMoves(index));
        ShowIndicators();
        OnPieceSelected?.Invoke(index);
    }

    private void ExecuteMove(int from, int to)
    {
        _isMoving = true;

        // Clear hover so ShowHover won't re-enable elements during fight
        if (_hoveredIndex >= 0 && _board[_hoveredIndex] != null)
            _board[_hoveredIndex].ShowHover(false);
        _hoveredIndex = -1;

        ChessPiece piece = _board[from];
        bool isCapture = _board[to] != null && !_board[to].HasEffect(EffectType.Shield);

        // Detect castling: king moves 2 squares horizontally
        bool isCastling = piece.PieceType == PieceType.King && Mathf.Abs((to % 8) - (from % 8)) == 2;

        AnimateToTile(piece, to, 0.17f, () =>
        {
            if (isCastling)
            {
                int row = from / 8;
                bool kingside = (to % 8) > (from % 8);
                int rookFrom = kingside ? row * 8 + 7 : row * 8;
                int rookTo = kingside ? row * 8 + 5 : row * 8 + 3;
                ChessPiece rook = _board[rookFrom];
                _board[rookTo] = rook;
                _board[rookFrom] = null;
                rook.HasMoved = true;
                AnimateToTile(rook, rookTo, 0.15f, () => FinishMove(from, to, piece));
            }
            else if (isCapture)
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
        PieceType defenderType = defender.PieceType;

        // Build reaction context before capture changes the board
        int combinedPower = GetPieceValue(attacker.PieceType) + GetPieceValue(defenderType);
        var (nearbyEnemies, nearbyFriendlies) = CountNearbyPieces(to, attacker.Color);
        var reactionCtx = new ReactionContext
        {
            CaptureSquare = IndexToAlgebraic(to),
            PieceType = attacker.PieceType.ToString(),
            PieceColor = attacker.Color.ToString(),
            CombinedPower = combinedPower,
            PowerTier = GetPowerTier(combinedPower),
            AttackerElement = atkElem,
            DefenderElement = defElem,
            NearbyEnemies = nearbyEnemies,
            NearbyFriendlies = nearbyFriendlies
        };

        // Hide elements during fight
        attacker.HideElement();
        defender.HideElement();
        attacker.PlayFightParticle();
        AudioManager.Instance?.PlayCapture(attacker.PieceType);
        GameObject fightTitle = SpawnFightTitle(to, atkElem, defElem);

        if (TutorialPause != null)
            yield return StartCoroutine(TutorialPause("fight_start", to, null));

        ElementMixResult mixResult = null;
        ElementReactionResult reactionResult = null;
        bool usedPending = false;

        // Pre-computed results (tutorial / online opponent)
        if (_pendingMix != null)
        {
            mixResult = _pendingMix;
            reactionResult = _pendingReaction;
            _pendingMix = null;
            _pendingReaction = null;
            WaitForPendingData = false;
            usedPending = true;

            // Let the fight animation play briefly when there's no API wait
            yield return new WaitForSeconds(1.5f);
        }
        else if (WaitForPendingData)
        {
            // Online: opponent's move arrived first, capture data coming separately
            // Wait for SetPendingReaction() to be called with the data
            WaitForPendingData = false;
            float timeout = 30f, waited = 0f;
            while (_pendingMix == null && waited < timeout)
            {
                yield return null;
                waited += Time.deltaTime;
            }
            if (_pendingMix != null)
            {
                mixResult = _pendingMix;
                reactionResult = _pendingReaction;
                _pendingMix = null;
                _pendingReaction = null;
                usedPending = true;
            }
        }
        else
        {
            int mergeDepth = Mathf.Max(ElementCollection.GetDepth(atkElem), ElementCollection.GetDepth(defElem)) + 1;
            bool mixWasCached = _elementService.HasCachedMix(atkElem, defElem);

            if (mixWasCached)
            {
                // L1 hit: cached mix — get it instantly, fire reaction call in background
                yield return _elementService.GetElementMix(atkElem, defElem, mergeDepth, r => mixResult = r);

                StartCoroutine(_elementService.GetElementReaction(
                    mixResult, reactionCtx,
                    r => reactionResult = r));
            }
            else
            {
                // L1 miss: check persistent server (L2)
                yield return _elementService.CheckServerMerge(atkElem, defElem, r => mixResult = r);

                if (mixResult != null)
                {
                    // L2 hit: mix loaded from server (now in L1 too), fetch fresh reaction
                    StartCoroutine(_elementService.GetElementReaction(
                        mixResult, reactionCtx,
                        r => reactionResult = r));
                }
                else
                {
                    // L2 miss: combined AI call for mix + reaction
                    yield return _elementService.GetElementMixAndReaction(
                        atkElem, defElem, reactionCtx, mergeDepth,
                        (mix, reaction) => { mixResult = mix; reactionResult = reaction; });

                    // Save to persistent server in background
                    if (mixResult != null)
                        StartCoroutine(_elementService.SaveServerMerge(atkElem, defElem, mixResult));
                }
            }
        }

        // Wait for reaction if the background call is still in progress (Path B)
        // This keeps the fight animation playing while the reaction loads
        if (reactionResult == null && !usedPending)
        {
            float timeout = 10f, waited = 0f;
            while (reactionResult == null && waited < timeout)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        if (mixResult == null)
        {
            mixResult = new ElementMixResult
            {
                newElement = atkElem,
                emoji = attacker.Emoji,
                winningElement = atkElem,
                reasoning = "API unavailable"
            };
        }

        // Determine outcome
        bool isDraw = mixResult.winningElement == "draw" || string.IsNullOrEmpty(mixResult.winningElement);
        bool attackerWins = !isDraw && string.Equals(mixResult.winningElement, atkElem, System.StringComparison.OrdinalIgnoreCase);

        // Fallback if no reaction was produced
        if (reactionResult == null)
            reactionResult = GenerateFallbackReaction(to, attacker, attackerWins, isDraw);

        // Notify listeners BEFORE animations so the opponent gets data immediately
        // and can play trade text / reactions in sync
        OnCaptureResult?.Invoke(from, to, mixResult, reactionResult);

        attacker.StopFightParticle();
        DismissFightTitle(fightTitle);

        // Trade result text animation + sound
        if (isDraw) AudioManager.Instance?.PlayTradeDraw();
        else if (attackerWins) AudioManager.Instance?.PlayTradeWon();
        else AudioManager.Instance?.PlayTradeLost();

        float tradeDuration = PlayTradeResultText(to, attackerWins, isDraw);
        yield return new WaitForSeconds(tradeDuration);

        if (TutorialPause != null)
            yield return StartCoroutine(TutorialPause("fight_end", to, mixResult));

        // Resolve capture
        RecordCapture(defender);
        Destroy(defender.gameObject);
        _board[to] = attacker;
        _board[from] = null;
        attacker.HasMoved = true;
        attacker.Deselect();

        // Auto-promote pawn to Queen on last rank
        if (attacker.PieceType == PieceType.Pawn)
        {
            int row = to / 8;
            if ((attacker.Color == PieceColor.White && row == 0) || (attacker.Color == PieceColor.Black && row == 7))
                attacker.SetPieceType(PieceType.Queen);
        }

        // Reveal new element
        attacker.SetElement(mixResult.newElement, mixResult.emoji, _emojiService, _floatingTextFont);
        attacker.RevealNewElement();
        AudioManager.Instance?.PlayElementReveal();

        yield return new WaitForSeconds(1.8f);

        // Apply elemental reaction effects
        string tradeOutcome = isDraw ? "draw" : (attackerWins ? "won" : "lost");
        yield return StartCoroutine(ApplyReaction(to, reactionResult, attacker.Color, tradeOutcome));

        if (TutorialPause != null)
            yield return StartCoroutine(TutorialPause("post_reaction", to, mixResult));

        // Ice slide check
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

    private float PlayTradeResultText(int index, bool attackerWins, bool isDraw)
    {
        Vector3 pos = _tilePositions[index];

        string text;
        Color textColor;
        if (isDraw)
        {
            text = "Even Trade";
            textColor = new Color(0.8f, 0.8f, 0.8f);
        }
        else if (attackerWins)
        {
            text = "Trade Won!";
            textColor = new Color(0.3f, 1f, 0.3f);
        }
        else
        {
            text = "Trade Lost...";
            textColor = new Color(1f, 0.5f, 0.3f);
        }

        GameObject go = new GameObject("TradeResultText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * 1.5f;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(5f, 1f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 2;

        bool useLostAnim = !isDraw && !attackerWins;

        Sequence seq = DOTween.Sequence();

        // Fade in
        seq.Append(label.DOFade(1f, 0.3f).SetEase(Ease.InOutQuad));

        if (useLostAnim)
        {
            // Lost: scale 1.5 -> 1 with InCubic, then shake at impact
            seq.Join(go.transform.DOScale(1f, 0.6f).SetEase(Ease.InCubic));
            seq.Append(go.transform.DOShakePosition(0.5f, 0.15f, 14, 90f, false, true));
        }
        else
        {
            // Won/Draw: scale 1.5 -> 1 with OutBack, then hold
            seq.Join(go.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.5f);
        }

        // Fade out + scale to 0
        seq.Append(label.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        seq.Join(go.transform.DOScale(0f, 0.3f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(go));

        return seq.Duration();
    }

    private void FinishMove(int from, int to, ChessPiece piece)
    {
        AudioManager.Instance?.PlayPieceMove(piece.PieceType);
        _board[to] = piece;
        _board[from] = null;
        piece.HasMoved = true;
        piece.Deselect();

        // Auto-promote pawn to Queen on last rank
        if (piece.PieceType == PieceType.Pawn)
        {
            int row = to / 8;
            if ((piece.Color == PieceColor.White && row == 0) || (piece.Color == PieceColor.Black && row == 7))
                piece.SetPieceType(PieceType.Queen);
        }

        // Clear tile-bound effects when moving to a tile without them
        if (!TileHasEffect(to, TileEffectType.Burning) && piece.HasEffect(EffectType.Burning))
            piece.RemoveEffect(EffectType.Burning);
        if (!TileHasEffect(to, TileEffectType.Plant) && piece.HasEffect(EffectType.Plant))
            piece.RemoveEffect(EffectType.Plant);

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
        piece.SetSortingLayer("Front");

        piece.transform.DOLocalMove(_tilePositions[tileIndex], duration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            piece.SetSortingLayer("Pieces");
            onComplete?.Invoke();
        });
    }

    private void ToggleTurn()
    {
        // Tick effects per-color: each player's effects tick on their own turn
        PieceColor justPlayed = _currentTurn;
        TickPieceEffects(justPlayed);
        ProcessTileEffectLogic(justPlayed);    // logic first: burn/plant check before tiles can expire
        TickTileEffectDurations(justPlayed);   // then tick/expire tile durations

        // Check if effects destroyed a king (poison expiry, burn damage, etc.)
        if (FindKing(justPlayed) < 0)
        {
            _gameOver = true;
            PieceColor winner = (justPlayed == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            Debug.Log($"[Chess] {winner} wins! {justPlayed} king destroyed by effects!");
            OnGameOver?.Invoke(new MatchResult { Outcome = MatchOutcome.KingDestroyed, Winner = winner });
            return;
        }

        _currentTurn = (_currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        OnTurnChanged?.Invoke(_currentTurn);

        bool inCheck = IsInCheck(_currentTurn);
        bool hasLegalMoves = HasAnyLegalMoves(_currentTurn);

        if (inCheck && !hasLegalMoves)
        {
            _gameOver = true;
            PieceColor winner = (_currentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            int kingIdx = FindKing(_currentTurn);
            if (kingIdx >= 0)
                SpawnFloatingTextStyled(_tilePositions[kingIdx], "Checkmate!", new Color(1f, 0.2f, 0.2f), 0f, 0f);
            AudioManager.Instance?.PlayCheckmate();
            Debug.Log($"[Chess] Checkmate! {winner} wins!");
            OnGameOver?.Invoke(new MatchResult { Outcome = MatchOutcome.Checkmate, Winner = winner });
        }
        else if (!inCheck && !hasLegalMoves)
        {
            _gameOver = true;
            int kingIdx = FindKing(_currentTurn);
            if (kingIdx >= 0)
                SpawnFloatingTextStyled(_tilePositions[kingIdx], "Stalemate!", new Color(0.7f, 0.7f, 0.7f), 0f, 0f);
            AudioManager.Instance?.PlayStalemate();
            Debug.Log("[Chess] Stalemate!");
            OnGameOver?.Invoke(new MatchResult { Outcome = MatchOutcome.Stalemate, Winner = PieceColor.White });
        }
        else if (inCheck)
        {
            int kingIdx = FindKing(_currentTurn);
            if (kingIdx >= 0)
                SpawnFloatingTextStyled(_tilePositions[kingIdx], "Check!", new Color(1f, 0.85f, 0.3f), 0f, 0f);
            AudioManager.Instance?.PlayCheck();
        }
    }

    private void TickPieceEffects(PieceColor color)
    {
        for (int i = 0; i < 64; i++)
        {
            ChessPiece piece = _board[i];
            if (piece == null || piece.Color != color) continue;

            var expired = piece.TickEffects();
            if (expired == null) continue;

            foreach (var effect in expired)
            {
                if (effect.Type == EffectType.Convert)
                    piece.SetColor(piece.OriginalColor);

                if (effect.Type == EffectType.Transform)
                    piece.SetPieceType(piece.OriginalType);

                if (effect.Type == EffectType.Poison)
                {
                    ApplyEffect(i, new ChessEffect(EffectType.Damage));
                    break; // piece is destroyed, skip remaining effects
                }
            }
        }
    }

    // --- Check & Checkmate ---

    private static readonly (int dc, int dr)[] _bishopDirs =
        { (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private static readonly (int dc, int dr)[] _rookDirs =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private static readonly (int dc, int dr)[] _allDirs =
        { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private static readonly (int dc, int dr)[] _knightOffsets =
        { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };

    private int FindKing(PieceColor color)
    {
        for (int i = 0; i < 64; i++)
            if (_board[i] != null && _board[i].PieceType == PieceType.King && _board[i].Color == color)
                return i;
        return -1;
    }

    private bool IsSquareAttacked(int square, PieceColor byColor)
    {
        int col = square % 8;
        int row = square / 8;

        // Pawn attacks
        int pawnRow = (byColor == PieceColor.White) ? row + 1 : row - 1;
        if (pawnRow >= 0 && pawnRow < 8)
        {
            for (int dc = -1; dc <= 1; dc += 2)
            {
                int pc = col + dc;
                if (pc >= 0 && pc < 8)
                {
                    ChessPiece p = _board[pawnRow * 8 + pc];
                    if (p != null && p.Color == byColor && !p.HasEffect(EffectType.Stun) && p.PieceType == PieceType.Pawn)
                        return true;
                }
            }
        }

        // Knight attacks
        foreach (var (dc, dr) in _knightOffsets)
        {
            int nc = col + dc, nr = row + dr;
            if (nc >= 0 && nc < 8 && nr >= 0 && nr < 8)
            {
                ChessPiece p = _board[nr * 8 + nc];
                if (p != null && p.Color == byColor && !p.HasEffect(EffectType.Stun) && p.PieceType == PieceType.Knight)
                    return true;
            }
        }

        // Bishop/Queen on diagonals
        foreach (var (dc, dr) in _bishopDirs)
        {
            int nc = col + dc, nr = row + dr;
            while (nc >= 0 && nc < 8 && nr >= 0 && nr < 8)
            {
                int idx = nr * 8 + nc;
                if (TileHasEffect(idx, TileEffectType.Occupied)) break;
                ChessPiece p = _board[idx];
                if (p != null)
                {
                    if (p.Color == byColor && !p.HasEffect(EffectType.Stun) && (p.PieceType == PieceType.Bishop || p.PieceType == PieceType.Queen))
                        return true;
                    break;
                }
                nc += dc; nr += dr;
            }
        }

        // Rook/Queen on ranks/files
        foreach (var (dc, dr) in _rookDirs)
        {
            int nc = col + dc, nr = row + dr;
            while (nc >= 0 && nc < 8 && nr >= 0 && nr < 8)
            {
                int idx = nr * 8 + nc;
                if (TileHasEffect(idx, TileEffectType.Occupied)) break;
                ChessPiece p = _board[idx];
                if (p != null)
                {
                    if (p.Color == byColor && !p.HasEffect(EffectType.Stun) && (p.PieceType == PieceType.Rook || p.PieceType == PieceType.Queen))
                        return true;
                    break;
                }
                nc += dc; nr += dr;
            }
        }

        // King (adjacent)
        foreach (var (dc, dr) in _allDirs)
        {
            int nc = col + dc, nr = row + dr;
            if (nc >= 0 && nc < 8 && nr >= 0 && nr < 8)
            {
                ChessPiece p = _board[nr * 8 + nc];
                if (p != null && p.Color == byColor && !p.HasEffect(EffectType.Stun) && p.PieceType == PieceType.King)
                    return true;
            }
        }

        return false;
    }

    private bool IsInCheck(PieceColor color)
    {
        int kingIdx = FindKing(color);
        if (kingIdx < 0) return false;
        if (_board[kingIdx].HasEffect(EffectType.Shield)) return false;
        PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(kingIdx, enemy);
    }

    private bool IsMoveLegal(int from, int to)
    {
        ChessPiece piece = _board[from];
        if (piece == null) return false;

        ChessPiece captured = _board[to];
        _board[to] = piece;
        _board[from] = null;

        bool legal = !IsInCheck(piece.Color);

        _board[from] = piece;
        _board[to] = captured;

        return legal;
    }

    private List<int> GetLegalMoves(int fromIndex)
    {
        ChessPiece piece = _board[fromIndex];
        if (piece == null) return new List<int>();
        if (piece.HasEffect(EffectType.Stun)) return new List<int>();

        var moves = piece.GetPossibleMoves(fromIndex, _board);
        moves.RemoveAll(to => !IsMoveLegal(fromIndex, to));
        FilterOccupiedBlocked(fromIndex, piece, moves);

        if (piece.PieceType == PieceType.King)
            AddCastlingMoves(fromIndex, piece, moves);

        return moves;
    }

    private void AddCastlingMoves(int kingIndex, ChessPiece king, List<int> moves)
    {
        if (king.HasMoved) return;
        if (IsInCheck(king.Color)) return;

        int row = kingIndex / 8;
        PieceColor enemy = king.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // Kingside (O-O): rook at col 7
        int rookKS = row * 8 + 7;
        ChessPiece ksRook = _board[rookKS];
        if (ksRook != null && ksRook.PieceType == PieceType.Rook && ksRook.Color == king.Color && !ksRook.HasMoved)
        {
            int f = row * 8 + 5; // square king passes through
            int g = row * 8 + 6; // king destination
            if (_board[f] == null && _board[g] == null
                && !IsSquareAttacked(f, enemy) && !IsSquareAttacked(g, enemy))
                moves.Add(g);
        }

        // Queenside (O-O-O): rook at col 0
        int rookQS = row * 8;
        ChessPiece qsRook = _board[rookQS];
        if (qsRook != null && qsRook.PieceType == PieceType.Rook && qsRook.Color == king.Color && !qsRook.HasMoved)
        {
            int d = row * 8 + 3; // square king passes through
            int c = row * 8 + 2; // king destination
            int b = row * 8 + 1; // must also be empty
            if (_board[d] == null && _board[c] == null && _board[b] == null
                && !IsSquareAttacked(d, enemy) && !IsSquareAttacked(c, enemy))
                moves.Add(c);
        }
    }

    private void FilterOccupiedBlocked(int fromIndex, ChessPiece piece, List<int> moves)
    {
        // Non-sliding pieces: just remove Occupied destinations
        if (piece.PieceType == PieceType.Knight || piece.PieceType == PieceType.King)
        {
            moves.RemoveAll(idx => TileHasEffect(idx, TileEffectType.Occupied));
            return;
        }

        // Pawns: if forward square is Occupied, also block double step
        if (piece.PieceType == PieceType.Pawn)
        {
            int col = fromIndex % 8;
            int row = fromIndex / 8;
            int dir = (piece.Color == PieceColor.White) ? -1 : 1;
            int fwdRow = row + dir;
            if (fwdRow >= 0 && fwdRow < 8 && TileHasEffect(fwdRow * 8 + col, TileEffectType.Occupied))
            {
                int fwd2Row = row + 2 * dir;
                if (fwd2Row >= 0 && fwd2Row < 8)
                    moves.Remove(fwd2Row * 8 + col);
            }
            moves.RemoveAll(idx => TileHasEffect(idx, TileEffectType.Occupied));
            return;
        }

        // Sliding pieces (Bishop, Rook, Queen): block tiles at and beyond Occupied
        int fromCol = fromIndex % 8;
        int fromRow = fromIndex / 8;
        var dirs = piece.PieceType == PieceType.Bishop ? _bishopDirs
                 : piece.PieceType == PieceType.Rook   ? _rookDirs
                 : _allDirs;

        var blocked = new HashSet<int>();
        foreach (var (dc, dr) in dirs)
        {
            bool pastOccupied = false;
            int nc = fromCol + dc, nr = fromRow + dr;
            while (nc >= 0 && nc < 8 && nr >= 0 && nr < 8)
            {
                int idx = nr * 8 + nc;
                if (pastOccupied)
                {
                    blocked.Add(idx);
                }
                else if (TileHasEffect(idx, TileEffectType.Occupied))
                {
                    blocked.Add(idx);
                    pastOccupied = true;
                }
                else if (_board[idx] != null)
                {
                    break;
                }
                nc += dc; nr += dr;
            }
        }
        moves.RemoveAll(idx => blocked.Contains(idx));
    }

    private bool HasAnyLegalMoves(PieceColor color)
    {
        for (int i = 0; i < 64; i++)
        {
            ChessPiece p = _board[i];
            if (p == null || p.Color != color) continue;
            if (p.HasEffect(EffectType.Stun)) continue;

            var moves = GetLegalMoves(i);
            if (moves.Count > 0) return true;
        }
        return false;
    }

    // --- Captured Pieces ---

    private void RecordCapture(ChessPiece piece)
    {
        var info = new CapturedPieceInfo { PieceType = piece.PieceType, Color = piece.Color };
        if (piece.Color == PieceColor.White)
            _capturedWhitePieces.Add(info);
        else
            _capturedBlackPieces.Add(info);
        OnPieceCaptured?.Invoke(info);
    }

    // --- Effects ---

    public void ApplyEffect(int index, ChessEffect effect)
    {
        ChessPiece piece = _board[index];
        if (piece == null) return;

        AudioManager.Instance?.PlayEffect(effect.Type);

        switch (effect.Type)
        {
            case EffectType.Damage:
                if (piece.HasEffect(EffectType.Shield)) return;
                SpawnFloatingText(index, "Destroy!");
                if (index == _selectedIndex) Deselect();
                _board[index] = null;
                RecordCapture(piece);
                Destroy(piece.gameObject);
                return;

            case EffectType.Push:
                SpawnFloatingText(index, "Push!");
                piece.AddEffect(effect);
                ExecutePush(index, effect.PushDirCol, effect.PushDirRow, effect.PushDistance);
                return;

            case EffectType.Convert:
                SpawnFloatingText(index, "Convert!");
                piece.AddEffect(effect);
                PieceColor newColor = (piece.Color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                piece.SetColor(newColor);
                return;

            case EffectType.Poison:
                if (piece.HasEffect(EffectType.Shield)) return;
                SpawnFloatingText(index, "Poison!");
                piece.AddEffect(effect);
                return;

            case EffectType.Transform:
                SpawnFloatingText(index, "Transform!");
                piece.AddEffect(effect);
                piece.SetPieceType(effect.TransformTarget);
                return;

            case EffectType.Stun:
            case EffectType.Shield:
                piece.AddEffect(effect);
                if (effect.Type == EffectType.Stun && index == _selectedIndex)
                    Deselect();
                return;

            case EffectType.Cleanse:
                if (effect.CleansePositive)
                {
                    SpawnFloatingText(index, "Dispel!");
                    piece.RemoveEffect(EffectType.Shield);
                }
                else
                {
                    SpawnFloatingText(index, "Cleanse!");
                    piece.RemoveEffect(EffectType.Stun);
                    piece.RemoveEffect(EffectType.Burning);
                    piece.RemoveEffect(EffectType.Poison);
                    piece.RemoveEffect(EffectType.Plant);
                    piece.RemoveEffect(EffectType.Convert);
                }
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

    // --- Tile Effects ---

    public void AddTileEffect(int index, TileEffect effect)
    {
        _tileEffects[index].Add(effect);
        CreateTileEffectVisual(index, effect);

        // Immediately apply piece effect if a piece is on this tile
        ChessPiece piece = _board[index];
        if (piece != null)
        {
            if (effect.Type == TileEffectType.Burning && !piece.HasEffect(EffectType.Burning))
                piece.AddEffect(new ChessEffect(EffectType.Burning, 3));
            else if (effect.Type == TileEffectType.Plant && !piece.HasEffect(EffectType.Plant))
                piece.AddEffect(new ChessEffect(EffectType.Plant, 2));
        }
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

    private void TickTileEffectDurations(PieceColor ownerColor)
    {
        for (int i = 0; i < 64; i++)
        {
            var list = _tileEffects[i];
            for (int j = list.Count - 1; j >= 0; j--)
            {
                if (list[j].OwnerColor != ownerColor) continue;
                if (list[j].Tick())
                {
                    // Clear piece effects when their tile effect expires
                    if (_board[i] != null)
                    {
                        if (list[j].Type == TileEffectType.Burning)
                            _board[i].RemoveEffect(EffectType.Burning);
                        else if (list[j].Type == TileEffectType.Plant)
                            _board[i].RemoveEffect(EffectType.Plant);
                    }

                    DestroyTileEffectVisual(list[j]);
                    list.RemoveAt(j);
                }
            }
        }
    }

    private void ProcessTileEffectLogic(PieceColor pieceColor)
    {
        for (int i = 0; i < 64; i++)
        {
            var list = _tileEffects[i];
            if (list.Count == 0)
            {
                _burningTurnCount[i] = 0; _tileOccupant[i] = null;
                _plantTurnCount[i] = 0; _plantOccupant[i] = null;
                continue;
            }

            ChessPiece piece = _board[i];

            // --- Burning: count per occupant color ---
            if (TileHasEffect(i, TileEffectType.Burning))
            {
                if (piece != null && piece.Color == pieceColor)
                {
                    if (piece != _tileOccupant[i])
                    {
                        _burningTurnCount[i] = 0;
                        if (_tileOccupant[i] != null)
                            _tileOccupant[i].RemoveEffect(EffectType.Burning);
                    }
                    _tileOccupant[i] = piece;
                    _burningTurnCount[i]++;

                    if (!piece.HasEffect(EffectType.Burning))
                        piece.AddEffect(new ChessEffect(EffectType.Burning, 3 - _burningTurnCount[i]));
                    else
                        piece.UpdateEffectCounter(EffectType.Burning, 3 - _burningTurnCount[i]);

                    if (_burningTurnCount[i] >= 3)
                    {
                        _burningTurnCount[i] = 0;
                        _tileOccupant[i] = null;
                        ApplyEffect(i, new ChessEffect(EffectType.Damage));
                    }
                }
                else if (piece == null)
                {
                    if (_tileOccupant[i] != null)
                        _tileOccupant[i].RemoveEffect(EffectType.Burning);
                    _burningTurnCount[i] = 0;
                    _tileOccupant[i] = null;
                }
            }

            // --- Plant: 1-turn grace, stun refreshes while plant exists ---
            if (TileHasEffect(i, TileEffectType.Plant))
            {
                if (piece != null && piece.Color == pieceColor)
                {
                    if (piece != _plantOccupant[i])
                    {
                        _plantTurnCount[i] = 0;
                        if (_plantOccupant[i] != null)
                            _plantOccupant[i].RemoveEffect(EffectType.Plant);
                    }
                    _plantOccupant[i] = piece;
                    _plantTurnCount[i]++;

                    if (!piece.HasEffect(EffectType.Plant))
                        piece.AddEffect(new ChessEffect(EffectType.Plant, 2 - _plantTurnCount[i]));
                    else
                        piece.UpdateEffectCounter(EffectType.Plant, 2 - _plantTurnCount[i]);

                    if (_plantTurnCount[i] >= 2 && !piece.HasEffect(EffectType.Stun))
                    {
                        SpawnFloatingText(i, "Stun!");
                        piece.AddEffect(new ChessEffect(EffectType.Stun, 1));
                    }
                }
                else if (piece == null)
                {
                    if (_plantOccupant[i] != null)
                        _plantOccupant[i].RemoveEffect(EffectType.Plant);
                    _plantTurnCount[i] = 0;
                    _plantOccupant[i] = null;
                }
            }
        }
    }

    // --- Fight Title ---

    private GameObject SpawnFightTitle(int tileIndex, string atkElement, string defElement)
    {
        Vector3 pos = _tilePositions[tileIndex];

        GameObject go = new GameObject("FightTitle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * 5f;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(8f, 3f);
        label.text = $"{atkElement}\n<size=60%>vs</size>\n{defElement}";
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 6f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 1f, 1f, 0f);
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 2;

        Sequence seq = DOTween.Sequence();
        seq.Append(label.DOFade(0.95f, 0.3f).SetEase(Ease.InOutQuad));
        seq.Join(go.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack));

        return go;
    }

    private void DismissFightTitle(GameObject go)
    {
        if (go == null) return;
        TextMeshPro label = go.GetComponent<TextMeshPro>();
        go.transform.DOKill();
        label.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(label.DOFade(0f, 0.25f).SetEase(Ease.InQuad));
        seq.Join(go.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack));
        seq.OnComplete(() => Destroy(go));
    }

    // --- Move Indicators ---

    private void ShowIndicators()
    {
        int fromCol = _selectedIndex % 8;
        int fromRow = _selectedIndex / 8;

        foreach (int idx in _validMoves)
        {
            GameObject dot = new GameObject("MoveIndicator");
            dot.transform.SetParent(transform, false);
            dot.transform.localPosition = _tilePositions[idx];
            dot.transform.localScale = Vector3.zero;

            SpriteRenderer sr = dot.AddComponent<SpriteRenderer>();
            sr.sprite = _moveIndicatorSprite;
            sr.sortingLayerName = "Front";
            sr.sortingOrder = 0;

            int dc = (idx % 8) - fromCol;
            int dr = (idx / 8) - fromRow;
            float dist = Mathf.Sqrt(dc * dc + dr * dr);
            float delay = dist * 0.012f;

            dot.transform.DOScale(Vector3.one, 0.17f).SetEase(Ease.OutCubic).SetDelay(delay);

            _indicators.Add(dot);
        }
    }

    public void PulseMoveIndicator(int tileIndex)
    {
        int i = _validMoves.IndexOf(tileIndex);
        if (i < 0 || i >= _indicators.Count) return;

        var dot = _indicators[i];
        dot.transform.DOScale(Vector3.one * 1.4f, 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void ClearIndicators()
    {
        foreach (GameObject dot in _indicators)
        {
            GameObject d = dot;
            DOTween.Kill(d.transform);
            d.transform.DOScale(Vector3.zero, 0.057f).SetEase(Ease.OutCubic)
                .OnComplete(() => Destroy(d));
        }
        _indicators.Clear();
    }

    private void Deselect()
    {
        if (_selectedIndex == -1) return;
        _board[_selectedIndex]?.Deselect();
        AudioManager.Instance?.PlayPieceDeselect();
        ClearIndicators();
        _selectedIndex = -1;
        _validMoves.Clear();
        OnPieceDeselected?.Invoke();
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
        go.transform.SetParent(transform, false);
        go.transform.localPosition = _tilePositions[index];

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 0;

        // Fade + scale in
        sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);
        go.transform.localScale = Vector3.one * 0.5f;
        sr.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
        go.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);

        _tileEffectVisuals[effect] = go;
    }

    private void DestroyTileEffectVisual(TileEffect effect)
    {
        if (_tileEffectVisuals.TryGetValue(effect, out GameObject go))
        {
            _tileEffectVisuals.Remove(effect);
            if (go == null) return;
            // Fade + scale out, then destroy
            var sr = go.GetComponent<SpriteRenderer>();
            DOTween.Kill(go.transform);
            if (sr != null) DOTween.Kill(sr);
            var seq = DOTween.Sequence();
            seq.Append(go.transform.DOScale(0.5f, 0.2f).SetEase(Ease.InBack));
            if (sr != null) seq.Join(sr.DOFade(0f, 0.2f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
        }
    }

    // --- Floating Text ---

    private void SpawnFloatingText(int index, string text)
    {
        Vector3 pos = _tilePositions[index];

        GameObject go = new GameObject("FloatingText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 1;

        go.transform.DOLocalMoveY(pos.y + _tileSize * 0.6f, 0.6f).SetEase(Ease.OutCubic);
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

    private void SpawnFloatingTextStyled(Vector3 localPos, string text, Color color, float delay, float yOffset)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos + new Vector3(0f, yOffset, 0f);

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        label.text = text;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 3.5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(color.r, color.g, color.b, 0f);
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 1;

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        seq.Append(label.DOFade(1f, 0.15f));
        seq.AppendInterval(1.6f);
        seq.Append(go.transform.DOLocalMoveY(go.transform.localPosition.y + _tileSize * 0.4f, 0.5f).SetEase(Ease.OutCubic));
        seq.Join(label.DOFade(0f, 0.5f).SetEase(Ease.InCubic));
        seq.OnComplete(() => Destroy(go));
    }

    // ─── Elemental Reaction ─────────────────────────────────────────────

    private static int GetPieceValue(PieceType type) => type switch
    {
        PieceType.Pawn   => 1,
        PieceType.Knight => 3,
        PieceType.Bishop => 3,
        PieceType.Rook   => 5,
        PieceType.Queen  => 9,
        PieceType.King   => 4,
        _                => 1
    };

    private static string GetPowerTier(int power) => power switch
    {
        <= 3  => "minor",
        <= 6  => "moderate",
        <= 9  => "major",
        _     => "massive"
    };

    public static string IndexToAlgebraic(int index)
    {
        int col = index % 8;
        int row = index / 8;
        return $"{(char)('a' + col)}{8 - row}";
    }

    public static int AlgebraicToIndex(string notation)
    {
        if (notation == null || notation.Length != 2) return -1;
        int col = notation[0] - 'a';
        int row = 8 - (notation[1] - '0');
        if (col < 0 || col >= 8 || row < 0 || row >= 8) return -1;
        return row * 8 + col;
    }

    private (int enemies, int friendlies) CountNearbyPieces(int captureIndex, PieceColor attackerColor, int range = 3)
    {
        int capCol = captureIndex % 8;
        int capRow = captureIndex / 8;
        int enemies = 0, friendlies = 0;

        for (int i = 0; i < 64; i++)
        {
            if (i == captureIndex) continue;
            ChessPiece p = _board[i];
            if (p == null) continue;

            int dc = Mathf.Abs((i % 8) - capCol);
            int dr = Mathf.Abs((i / 8) - capRow);
            if (Mathf.Max(dc, dr) > range) continue;

            if (p.Color == attackerColor) friendlies++;
            else enemies++;
        }

        return (enemies, friendlies);
    }

    // ─── Pattern Resolution ─────────────────────────────────────────────

    private static readonly string[] ValidPatterns = { "+", "x", "*", "forward", "l", "ring", "area" };

    private List<int> ResolvePattern(string pattern, int distance, int captureIndex, bool obstructed, PieceColor attackerColor)
    {
        int capCol = captureIndex % 8;
        int capRow = captureIndex / 8;
        var cells = new List<int>();

        switch (pattern)
        {
            case "+":
                ResolveRays(capCol, capRow, _rookDirs, distance, obstructed, cells);
                break;
            case "x":
                ResolveRays(capCol, capRow, _bishopDirs, distance, obstructed, cells);
                break;
            case "*":
                ResolveRays(capCol, capRow, _allDirs, distance, obstructed, cells);
                break;
            case "forward":
                ResolveForward(capCol, capRow, distance, attackerColor, cells);
                break;
            case "l":
                ResolveLShape(capCol, capRow, cells);
                break;
            case "ring":
                ResolveRing(capCol, capRow, distance, cells);
                break;
            case "area":
                ResolveArea(capCol, capRow, distance, cells);
                break;
            default:
                ResolveArea(capCol, capRow, 1, cells);
                break;
        }

        return cells;
    }

    private void ResolveRays(int capCol, int capRow, (int dc, int dr)[] directions, int distance, bool obstructed, List<int> cells)
    {
        foreach (var (dc, dr) in directions)
        {
            for (int step = 1; step <= distance; step++)
            {
                int col = capCol + dc * step;
                int row = capRow + dr * step;
                if (col < 0 || col >= 8 || row < 0 || row >= 8) break;

                int index = row * 8 + col;
                cells.Add(index);

                if (obstructed && _board[index] != null) break;
            }
        }
    }

    private static void ResolveForward(int capCol, int capRow, int distance, PieceColor attackerColor, List<int> cells)
    {
        int dir = (attackerColor == PieceColor.White) ? -1 : 1;

        for (int step = 1; step <= distance; step++)
        {
            int row = capRow + dir * step;
            if (row < 0 || row >= 8) break;
            cells.Add(row * 8 + capCol);
        }
    }

    private static void ResolveLShape(int capCol, int capRow, List<int> cells)
    {
        foreach (var (dc, dr) in _knightOffsets)
        {
            int col = capCol + dc;
            int row = capRow + dr;
            if (col >= 0 && col < 8 && row >= 0 && row < 8)
                cells.Add(row * 8 + col);
        }
    }

    private static void ResolveRing(int capCol, int capRow, int distance, List<int> cells)
    {
        if (distance <= 0) return;

        for (int dc = -distance; dc <= distance; dc++)
        {
            for (int dr = -distance; dr <= distance; dr++)
            {
                if (Mathf.Max(Mathf.Abs(dc), Mathf.Abs(dr)) != distance) continue;
                int col = capCol + dc;
                int row = capRow + dr;
                if (col >= 0 && col < 8 && row >= 0 && row < 8)
                    cells.Add(row * 8 + col);
            }
        }
    }

    private static void ResolveArea(int capCol, int capRow, int distance, List<int> cells)
    {
        for (int dc = -distance; dc <= distance; dc++)
        {
            for (int dr = -distance; dr <= distance; dr++)
            {
                if (dc == 0 && dr == 0 && distance > 0) continue; // exclude center unless d:0
                int col = capCol + dc;
                int row = capRow + dr;
                if (col >= 0 && col < 8 && row >= 0 && row < 8)
                    cells.Add(row * 8 + col);
            }
        }
    }

    private List<int> FilterTargets(List<int> indices, string targetFilter, PieceColor attackerColor, string tradeOutcome, bool isTileEffect = false)
    {
        // Resolve buff/debuff to concrete filters based on trade outcome
        // Piece effects: buff -> friendlies, debuff -> enemies (only occupied cells)
        // Tile effects:  buff -> not_enemies, debuff -> not_friendlies (includes empty cells for terrain placement)
        // Draw: all_pieces (piece) or terrain (tile)
        if (targetFilter == "buff" || targetFilter == "debuff")
        {
            bool isBuff = targetFilter == "buff";
            if (isTileEffect)
            {
                switch (tradeOutcome)
                {
                    case "won":
                        targetFilter = isBuff ? "not_enemies" : "not_friendlies";
                        break;
                    case "lost":
                        targetFilter = isBuff ? "not_friendlies" : "not_enemies";
                        break;
                    default: // draw
                        targetFilter = "terrain";
                        break;
                }
            }
            else
            {
                switch (tradeOutcome)
                {
                    case "won":
                        targetFilter = isBuff ? "friendlies" : "enemies";
                        break;
                    case "lost":
                        targetFilter = isBuff ? "enemies" : "friendlies";
                        break;
                    default: // draw
                        targetFilter = "all_pieces";
                        break;
                }
            }
        }

        var filtered = new List<int>();
        PieceColor enemyColor = (attackerColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        foreach (int idx in indices)
        {
            ChessPiece piece = _board[idx];

            switch (targetFilter)
            {
                case "enemies":
                    if (piece != null && piece.Color == enemyColor) filtered.Add(idx);
                    break;
                case "friendlies":
                    if (piece != null && piece.Color == attackerColor) filtered.Add(idx);
                    break;
                case "all_pieces":
                    if (piece != null) filtered.Add(idx);
                    break;
                case "empty":
                    if (piece == null) filtered.Add(idx);
                    break;
                case "not_friendlies":
                    if (piece == null || piece.Color == enemyColor) filtered.Add(idx);
                    break;
                case "not_enemies":
                    if (piece == null || piece.Color == attackerColor) filtered.Add(idx);
                    break;
                case "terrain":
                case "all":
                default:
                    filtered.Add(idx);
                    break;
            }
        }

        return filtered;
    }

    private static (int dirCol, int dirRow) ComputePushDirection(int targetIndex, int captureIndex, string direction)
    {
        int targetCol = targetIndex % 8;
        int targetRow = targetIndex / 8;
        int capCol = captureIndex % 8;
        int capRow = captureIndex / 8;

        switch (direction)
        {
            case "outwards":
            {
                int dc = targetCol - capCol;
                int dr = targetRow - capRow;
                return (dc == 0 ? 0 : (dc > 0 ? 1 : -1), dr == 0 ? 0 : (dr > 0 ? 1 : -1));
            }
            case "inwards":
            {
                int dc = capCol - targetCol;
                int dr = capRow - targetRow;
                return (dc == 0 ? 0 : (dc > 0 ? 1 : -1), dr == 0 ? 0 : (dr > 0 ? 1 : -1));
            }
            case "clockwise":
            {
                int dc = targetCol - capCol;
                int dr = targetRow - capRow;
                int normDc = dc == 0 ? 0 : (dc > 0 ? 1 : -1);
                int normDr = dr == 0 ? 0 : (dr > 0 ? 1 : -1);
                return (-normDr, normDc);
            }
            case "counter_clockwise":
            {
                int dc = targetCol - capCol;
                int dr = targetRow - capRow;
                int normDc = dc == 0 ? 0 : (dc > 0 ? 1 : -1);
                int normDr = dr == 0 ? 0 : (dr > 0 ? 1 : -1);
                return (normDr, -normDc);
            }
            case "up":    return (0, -1);
            case "down":  return (0, 1);
            case "left":  return (-1, 0);
            case "right": return (1, 0);
            default:
            {
                // Default to outwards
                int dc = targetCol - capCol;
                int dr = targetRow - capRow;
                return (dc == 0 ? 0 : (dc > 0 ? 1 : -1), dr == 0 ? 0 : (dr > 0 ? 1 : -1));
            }
        }
    }

    // ─── Reaction Validation ─────────────────────────────────────────────

    private struct ValidatedEffect
    {
        public int TargetIndex;
        public bool IsTileEffect;
        public EffectType PieceEffectType;
        public TileEffectType TileEffectType;
        public int Duration;
        public int PushDirCol, PushDirRow, PushDistance;
        public PieceType TransformTarget;
        public bool CleansePositive;
    }

    private struct EffectEntryGroup
    {
        public string Pattern;
        public bool Obstructed;
        public string EffectName;
        public List<int> AllPatternCells;
        public List<ValidatedEffect> Effects;
    }

    private List<EffectEntryGroup> ValidateReaction(ElementReactionResult reaction, int captureIndex, PieceColor attackerColor, string tradeOutcome)
    {
        var groups = new List<EffectEntryGroup>();
        int totalEffects = 0;
        if (reaction?.effects == null) return groups;

        foreach (var entry in reaction.effects)
        {
            string pattern = entry.pattern?.ToLower();
            if (string.IsNullOrEmpty(pattern)) continue;

            bool patternValid = false;
            foreach (var vp in ValidPatterns)
                if (pattern == vp) { patternValid = true; break; }
            if (!patternValid) continue;

            bool isTileEffect = entry.effect == "Burning" || entry.effect == "Ice" || entry.effect == "Plant" || entry.effect == "Occupied";

            int dist = Mathf.Clamp(entry.distance, 0, 7);
            var cells = ResolvePattern(pattern, dist, captureIndex, entry.obstructed, attackerColor);

            string targetFilter = entry.target ?? "all";
            var filtered = FilterTargets(cells, targetFilter, attackerColor, tradeOutcome, isTileEffect);

            string dirInfo = entry.direction != null ? $" dir:{entry.direction}" : "";
            string pushInfo = entry.effect == "Push" ? $" push_d:{entry.push_distance}" : "";
            Debug.Log($"[Reaction] pattern:{pattern} d:{dist} obstructed:{entry.obstructed} filter:{targetFilter} effect:{entry.effect}{dirInfo}{pushInfo} | resolved:{cells.Count} -> filtered:{filtered.Count}");

            var entryEffects = new List<ValidatedEffect>();

            foreach (int idx in filtered)
            {
                if (totalEffects >= 8) break;

                if (isTileEffect)
                {
                    TileEffectType tileType;
                    switch (entry.effect)
                    {
                        case "Burning":  tileType = TileEffectType.Burning; break;
                        case "Ice":      tileType = TileEffectType.Ice; break;
                        case "Plant":    tileType = TileEffectType.Plant; break;
                        case "Occupied": tileType = TileEffectType.Occupied; break;
                        default: continue;
                    }

                    int duration = entry.duration;
                    if (tileType == TileEffectType.Burning && duration < 3) duration = 3;
                    if (duration <= 0) duration = 4;
                    duration++; // +1: effects tick on the same turn they're applied

                    entryEffects.Add(new ValidatedEffect
                    {
                        TargetIndex = idx,
                        IsTileEffect = true,
                        TileEffectType = tileType,
                        Duration = duration
                    });
                    totalEffects++;
                }
                else
                {
                    ChessPiece target = _board[idx];
                    if (target == null) continue;

                    EffectType effectType;
                    switch (entry.effect)
                    {
                        case "Damage":  effectType = EffectType.Damage; break;
                        case "Stun":    effectType = EffectType.Stun; break;
                        case "Shield":  effectType = EffectType.Shield; break;
                        case "Push":    effectType = EffectType.Push; break;
                        case "Convert":   effectType = EffectType.Convert; break;
                        case "Poison":    effectType = EffectType.Poison; break;
                        case "Transform": effectType = EffectType.Transform; break;
                        case "Cleanse":   effectType = EffectType.Cleanse; break;
                        default: continue;
                    }

                    // King protection
                    if (target.PieceType == PieceType.King &&
                        (effectType == EffectType.Damage || effectType == EffectType.Convert ||
                         effectType == EffectType.Transform))
                        continue;

                    var fx = new ValidatedEffect
                    {
                        TargetIndex = idx,
                        IsTileEffect = false,
                        PieceEffectType = effectType,
                        Duration = entry.duration
                    };

                    if (fx.Duration <= 0 && effectType != EffectType.Damage && effectType != EffectType.Cleanse)
                        fx.Duration = effectType == EffectType.Poison ? 3 : 1;

                    // +1: effects tick on the same turn they're applied
                    if (effectType != EffectType.Damage && effectType != EffectType.Cleanse)
                        fx.Duration++;

                    if (effectType == EffectType.Push)
                    {
                        string dir = entry.direction ?? "outwards";
                        var (dc, dr) = ComputePushDirection(idx, captureIndex, dir);
                        if (dc == 0 && dr == 0) continue;
                        fx.PushDirCol = dc;
                        fx.PushDirRow = dr;
                        fx.PushDistance = Mathf.Max(entry.push_distance, 1);
                    }

                    if (effectType == EffectType.Transform)
                    {
                        string pt = entry.piece_type ?? "";
                        PieceType tt;
                        switch (pt)
                        {
                            case "Pawn":   tt = PieceType.Pawn; break;
                            case "Knight": tt = PieceType.Knight; break;
                            case "Bishop": tt = PieceType.Bishop; break;
                            case "Rook":   tt = PieceType.Rook; break;
                            case "Queen":  tt = PieceType.Queen; break;
                            default: continue;
                        }
                        fx.TransformTarget = tt;
                    }

                    if (effectType == EffectType.Cleanse)
                        fx.CleansePositive = (targetFilter == "debuff");

                    entryEffects.Add(fx);
                    totalEffects++;
                }
            }

            // Sort Damage effects last within this group
            if (entryEffects.Count > 1)
            {
                entryEffects.Sort((a, b) =>
                {
                    bool aDmg = !a.IsTileEffect && a.PieceEffectType == EffectType.Damage;
                    bool bDmg = !b.IsTileEffect && b.PieceEffectType == EffectType.Damage;
                    return aDmg.CompareTo(bDmg);
                });
            }

            // Always add group so particles play on pattern cells even if no effects matched
            groups.Add(new EffectEntryGroup
            {
                Pattern = pattern,
                Obstructed = entry.obstructed,
                EffectName = entry.effect,
                AllPatternCells = cells,
                Effects = entryEffects
            });

            if (totalEffects >= 8) break;
        }

        return groups;
    }

    // ─── Reaction Fallback ──────────────────────────────────────────────

    private ElementReactionResult GenerateFallbackReaction(int captureIndex, ChessPiece attacker, bool attackerWins, bool isDraw)
    {
        string element = attacker.Element?.ToLower() ?? "";

        string tileEffect = "Burning";
        if (element.Contains("ice") || element.Contains("frost") || element.Contains("water") || element.Contains("cold"))
            tileEffect = "Ice";
        else if (element.Contains("plant") || element.Contains("vine") || element.Contains("nature") || element.Contains("grass"))
            tileEffect = "Plant";
        else if (element.Contains("air") || element.Contains("wind") || element.Contains("storm") || element.Contains("gust") || element.Contains("breeze"))
            tileEffect = "Ice";

        var effects = new[] { new ReactionEffectEntry
        {
            pattern = "area",
            distance = 1,
            obstructed = false,
            target = "terrain",
            effect = tileEffect,
            duration = 4
        }};

        return new ElementReactionResult
        {
            effects = effects,
            flavor = "A surge of elemental energy!"
        };
    }

    // ─── Apply Reaction ─────────────────────────────────────────────────

    private const float EffectSpreadDelay = 0.1f;
    private const float EffectGroupGap = 0.8f;

    private IEnumerator ApplyReaction(int captureIndex, ElementReactionResult reaction, PieceColor attackerColor, string tradeOutcome)
    {
        // Keep flavor for game log, but don't display it
        if (!string.IsNullOrEmpty(reaction.flavor))
            Debug.Log($"[Reaction] {reaction.flavor}");

        var groups = ValidateReaction(reaction, captureIndex, attackerColor, tradeOutcome);
        if (groups.Count == 0) yield break;

        _isPlayingReaction = true;

        for (int i = 0; i < groups.Count; i++)
        {
            yield return StartCoroutine(PlayEffectGroup(groups[i], captureIndex, attackerColor));
            if (i < groups.Count - 1)
                yield return new WaitForSeconds(EffectGroupGap);
        }

        yield return new WaitForSeconds(0.3f);
        _isPlayingReaction = false;
    }

    private static Color GetEffectColor(string effectName)
    {
        switch (effectName)
        {
            case "Stun":      return new Color(0.6f, 0.2f, 0.9f);     // purple
            case "Shield":    return new Color(0.7f, 0.7f, 0.7f);     // grey
            case "Occupied":  return new Color(0.9f, 0.15f, 0.15f);   // red
            case "Burning":   return new Color(1f, 0.45f, 0.1f);      // red-orange
            case "Ice":       return new Color(0.4f, 0.75f, 1f);      // icy blue
            case "Plant":     return new Color(0.2f, 0.85f, 0.3f);    // green
            case "Damage":    return new Color(1f, 0.2f, 0.2f);       // red
            case "Poison":    return new Color(0.4f, 0.9f, 0.2f);     // yellow-green
            case "Push":      return new Color(0.9f, 0.8f, 0.3f);     // gold
            case "Convert":   return new Color(0.9f, 0.5f, 0.9f);     // pink
            case "Transform": return new Color(0.3f, 0.6f, 1f);       // blue
            case "Cleanse":   return new Color(0.95f, 0.95f, 0.6f);  // soft yellow
            default:          return Color.white;
        }
    }

    private IEnumerator PlayEffectGroup(EffectEntryGroup group, int captureIndex, PieceColor attackerColor)
    {
        int capCol = captureIndex % 8;
        int capRow = captureIndex / 8;
        Color particleColor = GetEffectColor(group.EffectName);

        // Build effect lookup by cell index
        var effectsByCell = new Dictionary<int, List<ValidatedEffect>>();
        foreach (var fx in group.Effects)
        {
            if (!effectsByCell.ContainsKey(fx.TargetIndex))
                effectsByCell[fx.TargetIndex] = new List<ValidatedEffect>();
            effectsByCell[fx.TargetIndex].Add(fx);
        }

        bool isInstant = group.Pattern == "l" || group.Pattern == "ring";

        if (isInstant)
        {
            // Spawn all particles at once, apply effects immediately
            AudioManager.Instance?.PlayEffectHit(0);
            foreach (int idx in group.AllPatternCells)
            {
                bool hasEffect = effectsByCell.TryGetValue(idx, out var fxList);

                if (hasEffect)
                {
                    SpawnEffectParticle(idx, particleColor, group.EffectName);
                    foreach (var fx in fxList)
                        ApplyValidatedEffect(fx, attackerColor);
                }
                else if (_board[idx] != null)
                {
                    SpawnMissLabel(idx, particleColor, group.EffectName);
                }
                else
                {
                    SpawnEffectParticle(idx, particleColor, group.EffectName);
                }
            }
        }
        else
        {
            // Group pattern cells by distance from capture, spread outward
            var byDistance = new SortedDictionary<int, List<int>>();
            foreach (int idx in group.AllPatternCells)
            {
                int col = idx % 8, row = idx / 8;
                int dist = Mathf.Max(Mathf.Abs(col - capCol), Mathf.Abs(row - capRow));
                if (!byDistance.ContainsKey(dist))
                    byDistance[dist] = new List<int>();
                byDistance[dist].Add(idx);
            }

            int prevDist = 0;
            bool first = true;
            int waveIndex = 0;
            foreach (var kvp in byDistance)
            {
                if (!first && kvp.Key > prevDist)
                    yield return new WaitForSeconds((kvp.Key - prevDist) * EffectSpreadDelay);
                first = false;
                prevDist = kvp.Key;

                AudioManager.Instance?.PlayEffectHit(waveIndex);
                waveIndex++;

                foreach (int idx in kvp.Value)
                {
                    bool hasEffect = effectsByCell.TryGetValue(idx, out var fxList);

                    if (hasEffect)
                    {
                        SpawnEffectParticle(idx, particleColor, group.EffectName);
                        foreach (var fx in fxList)
                            ApplyValidatedEffect(fx, attackerColor);
                    }
                    else if (_board[idx] != null)
                    {
                        SpawnMissLabel(idx, particleColor, group.EffectName);
                    }
                    else
                    {
                        SpawnEffectParticle(idx, particleColor, group.EffectName);
                    }
                }
            }
        }
    }

    private void SpawnEffectParticle(int cellIndex, Color color, string effectName)
    {
        if (_effectHitPrefab != null)
        {
            var ps = Instantiate(_effectHitPrefab, _tilePositions[cellIndex], Quaternion.identity);
            var main = ps.main;
            main.startColor = color;
            main.startLifetime = main.startLifetime.constantMax + 0.8f;
            ps.Play();
            Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax);
        }

        // Small effect name label
        Vector3 pos = _tilePositions[cellIndex];
        GameObject go = new GameObject("EffectLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(2f, 0.4f);
        label.text = effectName;
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 2.5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 1;

        Sequence seq = DOTween.Sequence();
        seq.Append(go.transform.DOLocalMoveY(pos.y + _tileSize * 0.3f, 0.4f).SetEase(Ease.OutCubic));
        seq.AppendInterval(0.6f);
        seq.Append(go.transform.DOLocalMoveY(pos.y + _tileSize * 0.5f, 0.4f).SetEase(Ease.OutCubic));
        seq.Join(label.DOFade(0f, 0.4f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(go));
    }

    private void SpawnMissLabel(int cellIndex, Color baseColor, string effectName)
    {
        Vector3 pos = _tilePositions[cellIndex];
        GameObject go = new GameObject("MissLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(2f, 0.4f);
        label.text = $"{effectName} Miss!";
        if (_floatingTextFont != null) label.font = _floatingTextFont;
        label.fontSize = 2.5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        label.raycastTarget = false;
        label.sortingLayerID = SortingLayer.NameToID("Front");
        label.sortingOrder = 1;

        Sequence seq = DOTween.Sequence();
        seq.Append(go.transform.DOLocalMoveY(pos.y + _tileSize * 0.3f, 0.4f).SetEase(Ease.OutCubic));
        seq.AppendInterval(0.6f);
        seq.Append(go.transform.DOLocalMoveY(pos.y + _tileSize * 0.5f, 0.4f).SetEase(Ease.OutCubic));
        seq.Join(label.DOFade(0f, 0.4f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(go));
    }

    private void ApplyValidatedEffect(ValidatedEffect fx, PieceColor attackerColor)
    {
        if (fx.IsTileEffect)
        {
            AddTileEffect(fx.TargetIndex, new TileEffect(fx.TileEffectType, fx.Duration, attackerColor));
        }
        else
        {
            var effect = new ChessEffect(fx.PieceEffectType, fx.Duration);
            if (fx.PieceEffectType == EffectType.Push)
            {
                effect.PushDirCol = fx.PushDirCol;
                effect.PushDirRow = fx.PushDirRow;
                effect.PushDistance = fx.PushDistance;
            }
            if (fx.PieceEffectType == EffectType.Transform)
                effect.TransformTarget = fx.TransformTarget;
            if (fx.PieceEffectType == EffectType.Cleanse)
                effect.CleansePositive = fx.CleansePositive;
            ApplyEffect(fx.TargetIndex, effect);
        }
    }
}
