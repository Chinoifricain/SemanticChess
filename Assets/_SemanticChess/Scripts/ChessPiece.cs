using System.Collections.Generic;
using DG.Tweening;
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

    [Header("Image References")]
    [SerializeField] private Image _image;
    [SerializeField] private Image _dropShadowImage;

    [Header("Selection Offsets")]
    [SerializeField] private Vector2 _selectSpriteOffset = new Vector2(0f, 8f);
    [SerializeField] private Vector2 _selectShadowOffset = new Vector2(-1.5f, -2f);

    [Header("Effects")]
    [SerializeField] private ParticleSystem _dustParticle;
    [SerializeField] private EffectSpriteEntry[] _effectSpriteEntries;

    public PieceType PieceType { get; private set; }
    public PieceColor Color { get; private set; }
    public PieceColor OriginalColor { get; private set; }
    public bool HasMoved { get; set; }

    // --- Effects ---
    private readonly List<ChessEffect> _effects = new List<ChessEffect>();
    public IReadOnlyList<ChessEffect> Effects => _effects;
    private readonly Dictionary<ChessEffect, GameObject> _effectIcons = new Dictionary<ChessEffect, GameObject>();
    private readonly Dictionary<ChessEffect, Tween> _effectIconTweens = new Dictionary<ChessEffect, Tween>();

    private RectTransform _imageRT;
    private RectTransform _shadowRT;
    private Vector2 _imageRestPos;
    private Vector2 _shadowRestPos;
    private Tween _jitterTween;
    private Tween _shadowJitterTween;

    public void Init(PieceType type, PieceColor color)
    {
        PieceType = type;
        Color = color;
        OriginalColor = color;
        HasMoved = false;
        ClearAllEffectIcons();
        _effects.Clear();

        Sprite sprite = GetSprite(type, color);
        _image.sprite = sprite;
        _dropShadowImage.sprite = sprite;

        _imageRT = _image.rectTransform;
        _shadowRT = _dropShadowImage.rectTransform;
        _imageRestPos = _imageRT.anchoredPosition;
        _shadowRestPos = _shadowRT.anchoredPosition;
    }

    public void Select()
    {
        _imageRT.DOAnchorPos(_imageRestPos + _selectSpriteOffset, 0.1f).SetEase(Ease.OutCubic);
        _shadowRT.DOAnchorPos(_shadowRestPos + _selectShadowOffset, 0.1f).SetEase(Ease.OutCubic);

        _jitterTween = _imageRT.DORotate(new Vector3(0f, 0f, 1.5f), 0.06f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, 0f, -1.5f));

        _shadowJitterTween = _shadowRT.DORotate(new Vector3(0f, 0f, 1.5f), 0.06f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector3(0f, 0f, -1.5f));
    }

    public void Deselect()
    {
        _jitterTween?.Kill();
        _jitterTween = null;
        _imageRT.localRotation = Quaternion.identity;

        _shadowJitterTween?.Kill();
        _shadowJitterTween = null;
        _shadowRT.localRotation = Quaternion.identity;

        _imageRT.DOAnchorPos(_imageRestPos, 0.1f).SetEase(Ease.OutCubic);
        _shadowRT.DOAnchorPos(_shadowRestPos, 0.1f).SetEase(Ease.OutCubic);

        if (_dustParticle != null)
            _dustParticle.Play();
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
        RefreshEffectIcons();
    }

    public bool HasEffect(EffectType type)
    {
        for (int i = 0; i < _effects.Count; i++)
            if (_effects[i].Type == type) return true;
        return false;
    }

    /// <summary>
    /// Ticks all effects, removing expired ones. Returns list of effects that just expired.
    /// </summary>
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
        if (expired != null) RefreshEffectIcons();
        return expired;
    }

    /// <summary>
    /// Changes the piece's team color and updates sprites.
    /// </summary>
    public void SetColor(PieceColor newColor)
    {
        Color = newColor;
        Sprite sprite = GetSprite(PieceType, newColor);
        _image.sprite = sprite;
        _dropShadowImage.sprite = sprite;
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
        go.transform.SetParent(_image.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;

        _effectIcons[effect] = go;

        Tween floatTween = rt.DOAnchorPosY(4f, 2f)
            .SetEase(Ease.InOutCubic)
            .SetLoops(-1, LoopType.Yoyo)
            .From(new Vector2(0f, -3f));
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

    private void RefreshEffectIcons()
    {
        // Icons are full overlays, no repositioning needed
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

    /// <summary>
    /// Returns true if the target piece can be captured (not shielded).
    /// </summary>
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
