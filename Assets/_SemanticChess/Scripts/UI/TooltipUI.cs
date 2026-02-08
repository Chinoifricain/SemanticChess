using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform _panelRt;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private ChessBoard _board;
    private Camera _cam;
    private Canvas _canvas;

    private int _currentIndex = -1;
    private bool _isShowing;
    private Tween _fadeTween;
    private Tween _scaleTween;

    private static readonly StringBuilder _sb = new StringBuilder();

    public void Init(ChessBoard board)
    {
        _board = board;
        _cam = Camera.main;
        _canvas = GetComponentInParent<Canvas>();

        _canvasGroup.alpha = 0f;
        _panelRt.localScale = Vector3.zero;

        _board.OnTileHoverChanged += OnTileHoverChanged;
        _board.OnTurnChanged += OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (_board != null)
        {
            _board.OnTileHoverChanged -= OnTileHoverChanged;
            _board.OnTurnChanged -= OnTurnChanged;
        }
        _fadeTween?.Kill();
        _scaleTween?.Kill();
    }

    private void OnTurnChanged(PieceColor _) => RebuildIfShowing();

    private void Update()
    {
        if (_board == null || _board.IsPlayingReaction || _board.IsGameOver)
        {
            HideTooltip();
            return;
        }

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (_board.IsInfoIconHovered(mouseWorld))
        {
            if (!_isShowing)
                ShowTooltip();
            PositionPanel();
        }
        else
        {
            if (_isShowing)
                HideTooltip();
        }
    }

    private void OnTileHoverChanged(int index)
    {
        _currentIndex = index;
        if (_isShowing && index < 0)
            HideTooltip();
        else if (_isShowing)
            RebuildContent(index);
    }

    private void RebuildIfShowing()
    {
        if (_isShowing && _currentIndex >= 0)
            RebuildContent(_currentIndex);
    }

    private void ShowTooltip()
    {
        if (_currentIndex < 0) return;

        _isShowing = true;
        RebuildContent(_currentIndex);

        _fadeTween?.Kill();
        _scaleTween?.Kill();

        _canvasGroup.alpha = 0f;
        _panelRt.localScale = new Vector3(1.1f, 0.85f, 1f);

        var seq = DOTween.Sequence();
        seq.Join(DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, 0.08f)
            .SetEase(Ease.OutCubic));
        seq.Join(_panelRt.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack));
        seq.SetUpdate(true);
        _scaleTween = seq;

        _board.SetInfoIconHovered(true);
    }

    private void HideTooltip()
    {
        if (!_isShowing) return;
        _isShowing = false;

        _fadeTween?.Kill();
        _scaleTween?.Kill();

        var seq = DOTween.Sequence();
        seq.Join(DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, 0.06f)
            .SetEase(Ease.OutCubic));
        seq.Join(_panelRt.DOScale(new Vector3(0.95f, 1.05f, 1f), 0.06f).SetEase(Ease.InCubic));
        seq.SetUpdate(true);
        _scaleTween = seq;

        _board.SetInfoIconHovered(false);
    }

    private void PositionPanel()
    {
        if (_currentIndex < 0) return;

        Vector3 tileWorld = _board.GetTileWorldPosition(_currentIndex);
        Vector2 screenPos = _cam.WorldToScreenPoint(tileWorld);

        float tileScreenSize = _cam.WorldToScreenPoint(tileWorld + Vector3.right * _board.TileSize).x - screenPos.x;
        float gap = 4f;

        bool showAbove = screenPos.y < Screen.height * 0.5f;
        float yOffset = showAbove ? tileScreenSize * 0.5f + gap : -(tileScreenSize * 0.5f + gap);
        float xOffset = -tileScreenSize * 0.5f;

        Vector2 targetScreen = screenPos + new Vector2(xOffset, yOffset);

        float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform, targetScreen, null, out Vector2 localPos);

        _panelRt.pivot = new Vector2(0f, showAbove ? 0f : 1f);
        _panelRt.anchoredPosition = localPos;

        ClampToScreen(_panelRt, scaleFactor);
    }

    private static void ClampToScreen(RectTransform rt, float scaleFactor)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        float dx = 0f;
        if (corners[0].x < 0) dx = -corners[0].x;
        else if (corners[2].x > Screen.width) dx = Screen.width - corners[2].x;

        float dy = 0f;
        if (corners[0].y < 0) dy = -corners[0].y;
        else if (corners[1].y > Screen.height) dy = Screen.height - corners[1].y;

        if (dx != 0 || dy != 0)
            rt.anchoredPosition += new Vector2(dx, dy) / scaleFactor;
    }

    private void RebuildContent(int index)
    {
        if (index < 0 || index >= 64) return;

        _sb.Clear();
        bool hasContent = false;

        // Tile effects
        var tileEffects = _board.GetTileEffects(index);
        if (tileEffects.Count > 0)
        {
            _sb.Append("<color=#888888>\u2500\u2500 Tile \u2500\u2500</color>");
            for (int i = 0; i < tileEffects.Count; i++)
            {
                var te = tileEffects[i];
                string color = GetTileEffectColor(te.Type);
                string dur = FormatDuration(te.Duration);
                _sb.Append($"\n<color={color}>{te.Type}</color> \u00b7 {dur}");
                _sb.Append($"\n  <color=#AAAAAA>{GetTileEffectDesc(te.Type)}</color>");
            }
            hasContent = true;
        }

        // Piece info
        ChessPiece piece = _board.GetPieceAt(index);
        if (piece != null)
        {
            string pieceName = piece.Color == PieceColor.White ? "White" : "Black";
            string typeName = piece.PieceType.ToString();
            if (piece.PieceType != piece.OriginalType)
                typeName += $" <color=#AAAAAA>(was {piece.OriginalType})</color>";

            if (hasContent) _sb.Append("\n\n");
            _sb.Append($"<color=#888888>\u2500\u2500 {pieceName} {typeName} \u2500\u2500</color>");

            if (!string.IsNullOrEmpty(piece.Element))
                _sb.Append($"\nElement: {piece.Element}");

            var effects = piece.Effects;
            if (effects.Count > 0)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var eff = effects[i];
                    string color = GetEffectColor(eff.Type);
                    string dur = FormatDuration(eff.Duration);
                    _sb.Append($"\n<color={color}>{eff.Type}</color> \u00b7 {dur}");
                    _sb.Append($"\n  <color=#AAAAAA>{GetEffectDesc(eff)}</color>");
                }
            }
        }

        _contentText.text = _sb.ToString();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRt);
    }

    private static string FormatDuration(int duration)
    {
        if (duration < 0) return "permanent";
        if (duration == 1) return "1 turn";
        return $"{duration} turns";
    }

    private static string GetEffectColor(EffectType type) => type switch
    {
        EffectType.Stun => "#FFDD44",
        EffectType.Shield => "#88EE66",
        EffectType.Poison => "#CC66FF",
        EffectType.Transform => "#66CCFF",
        EffectType.Burning => "#FF9944",
        EffectType.Plant => "#66DD66",
        EffectType.Convert => "#FF6688",
        _ => "#444444"
    };

    private static string GetEffectDesc(ChessEffect eff) => eff.Type switch
    {
        EffectType.Stun => "Cannot move until effect expires",
        EffectType.Shield => "Blocks damage, convert and transform effects",
        EffectType.Poison => "Destroys this piece when timer reaches 0",
        EffectType.Transform => $"Transformed into {eff.TransformTarget}, reverts on expiry",
        EffectType.Burning => "Destroyed if still on burning tile after 3 turns",
        EffectType.Plant => "Stunned if still on plant tile after 2 turns",
        EffectType.Convert => "Switched to the opposite side",
        _ => ""
    };

    private static string GetTileEffectColor(TileEffectType type) => type switch
    {
        TileEffectType.Burning => "#FF9944",
        TileEffectType.Ice => "#88DDFF",
        TileEffectType.Plant => "#66DD66",
        TileEffectType.Occupied => "#DD4444",
        _ => "#444444"
    };

    private static string GetTileEffectDesc(TileEffectType type) => type switch
    {
        TileEffectType.Burning => "Destroys pieces that stay here for 3 consecutive turns",
        TileEffectType.Ice => "Pieces slide through in their movement direction",
        TileEffectType.Plant => "Stuns pieces that stay here for 2 consecutive turns",
        TileEffectType.Occupied => "Tile is blocked",
        _ => ""
    };
}
