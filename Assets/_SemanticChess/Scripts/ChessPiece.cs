using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }
public enum PieceColor { White, Black }

[System.Serializable]
public struct PieceSpriteEntry
{
    public PieceType pieceType;
    public Sprite whiteSprite;
    public Sprite blackSprite;
}

[System.Serializable]
public struct EffectSpriteEntry
{
    public EffectType type;
    public Sprite sprite;
}

public class ChessPiece : MonoBehaviour
{
    [Header("Piece to Sprite")]
    [SerializeField] private PieceSpriteEntry[] _spriteEntries;

    [Header("Renderer References")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private SpriteRenderer _shadowRenderer;

    [Header("Selection Offsets")]
    [SerializeField] private Vector2 _selectSpriteOffset = new Vector2(0f, 0.11f);
    [SerializeField] private Vector2 _selectShadowOffset = new Vector2(-0.02f, -0.03f);

    [Header("Effects")]
    [SerializeField] private ParticleSystem _dustParticle;
    [SerializeField] private ParticleSystem _fightParticle;
    [SerializeField] private EffectSpriteEntry[] _effectSpriteEntries;

    public PieceType PieceType { get; private set; }
    public PieceColor Color { get; private set; }
    public PieceColor OriginalColor { get; private set; }
    public bool HasMoved { get; set; }

    // --- Element ---
    public string Element { get; private set; }
    public string Emoji { get; private set; }
    private TextMeshPro _elementLabel;
    private SpriteRenderer _emojiRenderer;
    private Tween _labelPopTween;
    private static readonly Vector3 EmojiRestLocal = new Vector3(0f, 0.45f, 0f);
    private const float EmojiRadius = 6f;
    private const float EmojiMinScale = 0.4f;
    private const float EmojiMaxScale = 1.3f;
    private const float EmojiAttractStrength = 0.08f;
    private const float PopScale = 1.3f;
    private const float PopDuration = 0.08f;

    // --- Piece proximity + hover ---
    private const float PieceRadius = 6f;
    private const float PieceAttractStrength = 0.06f;
    private const float PieceHoverBonus = 0.1f;
    private const float PiecePopBonus = 0.3f;
    private float _hoverBonus;
    private Tween _hoverTween;

    // --- Effects ---
    private readonly List<ChessEffect> _effects = new List<ChessEffect>();
    public IReadOnlyList<ChessEffect> Effects => _effects;
    private readonly Dictionary<ChessEffect, GameObject> _effectIcons = new Dictionary<ChessEffect, GameObject>();
    private readonly Dictionary<ChessEffect, Tween> _effectIconTweens = new Dictionary<ChessEffect, Tween>();

    private Transform _spriteT;
    private Transform _shadowT;
    private Vector3 _spriteRestPos;
    private Vector3 _shadowRestPos;
    private Tween _jitterTween;
    private Tween _shadowJitterTween;

    public void Init(PieceType type, PieceColor color)
    {
        PieceType = type;
        Color = color;
        OriginalColor = color;
        HasMoved = false;
        Element = null;
        Emoji = null;
        if (_elementLabel != null) { Object.Destroy(_elementLabel.gameObject); _elementLabel = null; }
        if (_emojiRenderer != null) { Object.Destroy(_emojiRenderer.gameObject); _emojiRenderer = null; }
        ClearAllEffectIcons();
        _effects.Clear();

        Sprite sprite = GetSprite(type, color);
        _spriteRenderer.sprite = sprite;
        _shadowRenderer.sprite = sprite;

        _spriteT = _spriteRenderer.transform;
        _shadowT = _shadowRenderer.transform;
        _spriteRestPos = _spriteT.localPosition;
        _shadowRestPos = _shadowT.localPosition;

        _shadowRenderer.sortingLayerName = "piece";
        _shadowRenderer.sortingOrder = 0;
        _spriteRenderer.sortingLayerName = "piece";
        _spriteRenderer.sortingOrder = 1;
    }

    public void Select()
    {
        _spriteT.DOLocalMove(_spriteRestPos + (Vector3)_selectSpriteOffset, 0.1f).SetEase(Ease.OutCubic);
        _shadowT.DOLocalMove(_shadowRestPos + (Vector3)_selectShadowOffset, 0.1f).SetEase(Ease.OutCubic);

        _jitterTween = _spriteT.DORotate(new Vector3(0f, 0f, 1.5f), 0.06f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, 0f, -1.5f));

        _shadowJitterTween = _shadowT.DORotate(new Vector3(0f, 0f, 1.5f), 0.06f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, 0f, -1.5f));
    }

    public void Deselect()
    {
        _jitterTween?.Kill();
        _jitterTween = null;
        _spriteT.localRotation = Quaternion.identity;

        _shadowJitterTween?.Kill();
        _shadowJitterTween = null;
        _shadowT.localRotation = Quaternion.identity;

        _spriteT.DOLocalMove(_spriteRestPos, 0.1f).SetEase(Ease.OutCubic);
        _shadowT.DOLocalMove(_shadowRestPos, 0.1f).SetEase(Ease.OutCubic);

        if (_dustParticle != null)
            _dustParticle.Play();
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector3 pieceWorld = transform.position;
        float dist = Vector2.Distance(mouseWorld, pieceWorld);
        float smooth = Time.deltaTime * 12f;

        // --- Piece proximity offset ---
        float pt = 1f - Mathf.Clamp01(dist / PieceRadius);
        Vector3 pieceDir = (mouseWorld - pieceWorld);
        pieceDir.z = 0f;
        Vector3 pieceOffset = pieceDir.normalized * (pt * PieceAttractStrength);
        _spriteT.localPosition = Vector3.Lerp(
            _spriteT.localPosition, _spriteRestPos + pieceOffset, smooth);
        _shadowT.localPosition = Vector3.Lerp(
            _shadowT.localPosition, _shadowRestPos + pieceOffset * 0.5f, smooth);

        // --- Piece hover scale (driven by DOTween on _hoverBonus) ---
        transform.localScale = Vector3.one * (1f + _hoverBonus);

        // --- Emoji proximity scaling + attraction ---
        if (_emojiRenderer != null && _emojiRenderer.sprite != null && _emojiRenderer.gameObject.activeSelf)
        {
            float et = 1f - Mathf.Clamp01(dist / EmojiRadius);

            float emojiScale = Mathf.Lerp(EmojiMinScale, EmojiMaxScale, et);
            _emojiRenderer.transform.localScale = Vector3.Lerp(
                _emojiRenderer.transform.localScale, Vector3.one * emojiScale, smooth);

            Vector3 dir = (mouseWorld - pieceWorld);
            dir.z = 0f;
            Vector3 attract = dir.normalized * (et * EmojiAttractStrength);
            Vector3 targetPos = EmojiRestLocal + attract;
            _emojiRenderer.transform.localPosition = Vector3.Lerp(
                _emojiRenderer.transform.localPosition, targetPos, smooth);
        }
    }

    // --- Effects ---

    public void AddEffect(ChessEffect effect)
    {
        _effects.Add(effect);
        CreateEffectIcon(effect);
    }

    public void RemoveEffect(ChessEffect effect)
    {
        _effects.Remove(effect);
        DestroyEffectIcon(effect);
    }

    public bool HasEffect(EffectType type)
    {
        for (int i = 0; i < _effects.Count; i++)
            if (_effects[i].Type == type) return true;
        return false;
    }

    public List<ChessEffect> TickEffects()
    {
        List<ChessEffect> expired = null;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            if (_effects[i].Tick())
            {
                expired ??= new List<ChessEffect>();
                expired.Add(_effects[i]);
                DestroyEffectIcon(_effects[i]);
                _effects.RemoveAt(i);
            }
        }
        return expired;
    }

    public void SetColor(PieceColor newColor)
    {
        Color = newColor;
        Sprite sprite = GetSprite(PieceType, newColor);
        _spriteRenderer.sprite = sprite;
        _shadowRenderer.sprite = sprite;
    }

    // --- Fight Particles ---

    public void PlayFightParticle() { if (_fightParticle != null) _fightParticle.Play(); }
    public void StopFightParticle() { if (_fightParticle != null) _fightParticle.Stop(); }

    // --- Sorting Layer ---

    public void SetSortingLayer(string layerName)
    {
        _shadowRenderer.sortingLayerName = layerName;
        _spriteRenderer.sortingLayerName = layerName;
    }

    // --- Element ---

    public void SetElement(string element, string emoji, EmojiLoader emojiService, TMP_FontAsset font = null)
    {
        Element = element;
        Emoji = emoji;

        // Create emoji renderer
        if (_emojiRenderer == null)
        {
            var go = new GameObject("EmojiIcon");
            go.transform.SetParent(_spriteT, false);
            go.transform.localPosition = EmojiRestLocal;

            _emojiRenderer = go.AddComponent<SpriteRenderer>();
            _emojiRenderer.sortingLayerName = "piece";
            _emojiRenderer.sortingOrder = 3;
        }

        // Create element name label (hidden by default, shown on hover)
        if (_elementLabel == null)
        {
            var go = new GameObject("ElementLabel");
            go.transform.SetParent(_spriteT, false);
            go.transform.localPosition = EmojiRestLocal;

            _elementLabel = go.AddComponent<TextMeshPro>();
            _elementLabel.fontSize = 3f;
            _elementLabel.fontStyle = FontStyles.Bold;
            _elementLabel.alignment = TextAlignmentOptions.Center;
            _elementLabel.color = new UnityEngine.Color(1f, 1f, 1f, 0.95f);
            _elementLabel.raycastTarget = false;
            _elementLabel.sortingLayerID = SortingLayer.NameToID("piece");
            _elementLabel.sortingOrder = 3;
            _elementLabel.rectTransform.sizeDelta = new Vector2(4f, 0.5f);
            _elementLabel.enableWordWrapping = false;
            _elementLabel.overflowMode = TextOverflowModes.Overflow;
        }

        if (font != null) _elementLabel.font = font;

        _elementLabel.text = element;
        _elementLabel.gameObject.SetActive(false);

        // Load emoji sprite
        if (emojiService != null && !string.IsNullOrEmpty(emoji))
        {
            Sprite cached = emojiService.GetCached(emoji);
            if (cached != null)
            {
                _emojiRenderer.sprite = cached;
            }
            else
            {
                _emojiRenderer.sprite = null;
                emojiService.StartCoroutine(emojiService.Load(emoji, sprite =>
                {
                    if (_emojiRenderer != null)
                    {
                        _emojiRenderer.sprite = sprite;
                        ShowHover(false);
                    }
                }));
            }
        }

        ShowHover(false);
    }

    public void ShowHover(bool show)
    {
        bool hasEmoji = _emojiRenderer != null && _emojiRenderer.sprite != null;

        if (_elementLabel != null)
            _elementLabel.gameObject.SetActive(show || !hasEmoji);
        if (_emojiRenderer != null)
            _emojiRenderer.gameObject.SetActive(!show && hasEmoji);

        // Snappy pop transition (label / emoji)
        _labelPopTween?.Kill();
        if (show && _elementLabel != null && _elementLabel.gameObject.activeSelf)
        {
            _elementLabel.transform.localScale = Vector3.one * PopScale;
            _labelPopTween = _elementLabel.transform.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutCubic);
        }
        else if (!show && hasEmoji)
        {
            _emojiRenderer.transform.localScale *= PopScale;
        }

        // Piece hover scale
        _hoverTween?.Kill();
        if (show)
        {
            _hoverBonus = PiecePopBonus;
            _hoverTween = DOTween.To(() => _hoverBonus, x => _hoverBonus = x, PieceHoverBonus, PopDuration)
                .SetEase(Ease.OutCubic);
        }
        else
        {
            _hoverTween = DOTween.To(() => _hoverBonus, x => _hoverBonus = x, 0f, PopDuration)
                .SetEase(Ease.OutCubic);
        }
    }

    // --- Effect Icon Rendering ---

    private Sprite GetEffectSprite(EffectType type)
    {
        if (_effectSpriteEntries == null) return null;
        foreach (var entry in _effectSpriteEntries)
            if (entry.type == type) return entry.sprite;
        return null;
    }

    private void CreateEffectIcon(ChessEffect effect)
    {
        Sprite sprite = GetEffectSprite(effect.Type);
        if (sprite == null) return;

        GameObject go = new GameObject($"EffectIcon_{effect.Type}");
        go.transform.SetParent(_spriteT, false);
        go.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "piece";
        sr.sortingOrder = 2;

        _effectIcons[effect] = go;

        Tween floatTween = go.transform.DOLocalMoveY(0.06f, 2f)
            .SetEase(Ease.InOutCubic)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, -0.04f, 0f));
        _effectIconTweens[effect] = floatTween;
    }

    private void DestroyEffectIcon(ChessEffect effect)
    {
        if (_effectIconTweens.TryGetValue(effect, out Tween tween))
        {
            tween?.Kill();
            _effectIconTweens.Remove(effect);
        }
        if (_effectIcons.TryGetValue(effect, out GameObject go))
        {
            if (go != null) Object.Destroy(go);
            _effectIcons.Remove(effect);
        }
    }

    private void ClearAllEffectIcons()
    {
        foreach (var kvp in _effectIconTweens)
            kvp.Value?.Kill();
        _effectIconTweens.Clear();
        foreach (var kvp in _effectIcons)
            if (kvp.Value != null) Object.Destroy(kvp.Value);
        _effectIcons.Clear();
    }

    private static bool CanCapture(ChessPiece target)
    {
        return !target.HasEffect(EffectType.Shield);
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
        int dir = (Color == PieceColor.White) ? -1 : 1;
        int startRow = (Color == PieceColor.White) ? 6 : 1;

        int fwdRow = row + dir;
        if (InBounds(col, fwdRow) && board[fwdRow * 8 + col] == null)
        {
            moves.Add(fwdRow * 8 + col);

            int fwd2Row = row + 2 * dir;
            if (row == startRow && board[fwd2Row * 8 + col] == null)
                moves.Add(fwd2Row * 8 + col);
        }

        foreach (int dc in new[] { -1, 1 })
        {
            int nc = col + dc;
            if (InBounds(nc, fwdRow))
            {
                ChessPiece target = board[fwdRow * 8 + nc];
                if (target != null && target.Color != Color && CanCapture(target))
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
            if (target == null)
                moves.Add(nr * 8 + nc);
            else if (target.Color != Color && CanCapture(target))
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
                    if (board[idx].Color != Color && CanCapture(board[idx]))
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
            if (target == null)
                moves.Add(nr * 8 + nc);
            else if (target.Color != Color && CanCapture(target))
                moves.Add(nr * 8 + nc);
        }
    }

    private static bool InBounds(int col, int row)
    {
        return col >= 0 && col < 8 && row >= 0 && row < 8;
    }
}
