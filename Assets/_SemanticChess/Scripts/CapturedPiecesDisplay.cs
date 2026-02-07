using System.Collections.Generic;
using UnityEngine;

public class CapturedPiecesDisplay : MonoBehaviour
{
    [SerializeField] private ChessBoard _board;
    [SerializeField] private PieceSpriteEntry[] _spriteEntries;
    [SerializeField] private float _pieceScale = 0.4f;
    [SerializeField] private float _spacing = 0.35f;
    [SerializeField] private float _margin = 0.3f;

    private static readonly Vector3 ShadowOffset = new Vector3(-0.027f, -0.052f, 0f);
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.75f);

    private readonly List<GameObject> _whiteIcons = new List<GameObject>();
    private readonly List<GameObject> _blackIcons = new List<GameObject>();

    private readonly List<CapturedPieceInfo> _capturedWhite = new List<CapturedPieceInfo>();
    private readonly List<CapturedPieceInfo> _capturedBlack = new List<CapturedPieceInfo>();

    private void OnEnable()
    {
        _board.OnPieceCaptured += OnPieceCaptured;
        _board.OnBoardReset += OnBoardReset;
    }

    private void OnDisable()
    {
        _board.OnPieceCaptured -= OnPieceCaptured;
        _board.OnBoardReset -= OnBoardReset;
    }

    private void OnPieceCaptured(CapturedPieceInfo info)
    {
        if (info.Color == PieceColor.White)
            _capturedWhite.Add(info);
        else
            _capturedBlack.Add(info);

        RebuildDisplay();
    }

    private void OnBoardReset()
    {
        _capturedWhite.Clear();
        _capturedBlack.Clear();
        ClearIcons(_whiteIcons);
        ClearIcons(_blackIcons);
    }

    private void RebuildDisplay()
    {
        Bounds b = _board.BoardSprite.bounds;
        bool flipped = _board.IsFlipped;

        float leftX = b.min.x - _margin;
        float rightX = b.max.x + _margin;
        float bottomY = b.min.y;

        // White losses on left, black losses on right (swap when flipped)
        float whiteX = flipped ? rightX : leftX;
        float blackX = flipped ? leftX : rightX;

        RebuildColumn(_capturedWhite, _whiteIcons, PieceColor.White, whiteX, bottomY);
        RebuildColumn(_capturedBlack, _blackIcons, PieceColor.Black, blackX, bottomY);
    }

    private void RebuildColumn(List<CapturedPieceInfo> captured, List<GameObject> icons,
        PieceColor color, float x, float bottomY)
    {
        ClearIcons(icons);

        captured.Sort((a, b) => PieceOrder(a.PieceType).CompareTo(PieceOrder(b.PieceType)));

        for (int i = 0; i < captured.Count; i++)
        {
            Sprite sprite = GetSprite(captured[i]);
            if (sprite == null) continue;

            var go = new GameObject($"Captured_{color}_{i}");
            go.transform.SetParent(transform);
            Vector3 worldPos = new Vector3(x, bottomY + i * _spacing, 0f);
            go.transform.localPosition = transform.InverseTransformPoint(worldPos);
            go.transform.localScale = Vector3.one * _pieceScale;

            // Shadow
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform);
            shadowGo.transform.localPosition = ShadowOffset;
            shadowGo.transform.localScale = Vector3.one;
            var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
            shadowSr.sprite = sprite;
            shadowSr.color = ShadowColor;
            shadowSr.sortingLayerName = "Pieces";
            shadowSr.sortingOrder = 0;

            // Piece sprite
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Pieces";
            sr.sortingOrder = 1;

            icons.Add(go);
        }
    }

    private static void ClearIcons(List<GameObject> icons)
    {
        foreach (var go in icons)
            Destroy(go);
        icons.Clear();
    }

    private Sprite GetSprite(CapturedPieceInfo info)
    {
        foreach (var entry in _spriteEntries)
        {
            if (entry.pieceType == info.PieceType)
                return info.Color == PieceColor.White ? entry.whiteSprite : entry.blackSprite;
        }
        return null;
    }

    private static int PieceOrder(PieceType type) => type switch
    {
        PieceType.Pawn => 0,
        PieceType.Knight => 1,
        PieceType.Bishop => 2,
        PieceType.Rook => 3,
        PieceType.Queen => 4,
        PieceType.King => 5,
        _ => 99
    };
}
