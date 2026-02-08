using System.Collections;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameLog : MonoBehaviour
{
    [SerializeField] private ChessBoard _board;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private TMP_Text _logText;
    [SerializeField] private RectTransform _panel;

    [Header("Slide")]
    [SerializeField] private float _peekWidth = 20f;
    [SerializeField] private float _enterThreshold = 80f;
    [SerializeField] private float _exitThreshold = 250f;
    [SerializeField] private float _slideDuration = 0.25f;

    private readonly StringBuilder _sb = new StringBuilder();
    private bool _pendingCapture;
    private bool _dirty;
    private int _moveNumber;
    private bool _isOpen;
    private Tween _slideTween;
    private float _panelWidth;
    private float _openX;

    private void OnEnable()
    {
        _board.OnMoveMade += OnMoveMade;
        _board.OnCaptureResult += OnCaptureResult;
        _board.OnBoardReset += OnBoardReset;
    }

    private void OnDisable()
    {
        _board.OnMoveMade -= OnMoveMade;
        _board.OnCaptureResult -= OnCaptureResult;
        _board.OnBoardReset -= OnBoardReset;
    }

    private void Start()
    {
        _panelWidth = _panel.rect.width;
        _openX = _panel.anchoredPosition.x;
        // Start folded: shift panel right so only _peekWidth is visible
        SetPanelOffset(_openX + _panelWidth - _peekWidth);
    }

    private void Update()
    {
        float distFromRight = Screen.width - Input.mousePosition.x;

        if (!_isOpen && distFromRight < _enterThreshold)
            SlideOpen();
        else if (_isOpen && distFromRight > _exitThreshold)
            SlideClosed();
    }

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;

        _logText.text = _sb.ToString();
        StartCoroutine(ScrollNextFrame());
    }

    private void SlideOpen()
    {
        _isOpen = true;
        AudioManager.Instance?.PlayPanelOpen();
        _slideTween?.Kill();
        _slideTween = _panel.DOAnchorPosX(_openX, _slideDuration).SetEase(Ease.OutBack);
    }

    private void SlideClosed()
    {
        _isOpen = false;
        AudioManager.Instance?.PlayPanelClose();
        _slideTween?.Kill();
        _slideTween = _panel.DOAnchorPosX(_openX + _panelWidth - _peekWidth, _slideDuration).SetEase(Ease.OutBack);
    }

    private void SetPanelOffset(float x)
    {
        var pos = _panel.anchoredPosition;
        pos.x = x;
        _panel.anchoredPosition = pos;
    }

    private IEnumerator ScrollNextFrame()
    {
        yield return null;
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    private void OnMoveMade(int from, int to)
    {
        ChessPiece attacker = _board.GetPieceAt(from);
        ChessPiece defender = _board.GetPieceAt(to);
        if (attacker == null) return;

        _moveNumber++;

        string fromSq = ChessBoard.IndexToAlgebraic(from);
        string toSq = ChessBoard.IndexToAlgebraic(to);
        string atkName = attacker.PieceType.ToString();
        string atkElem = attacker.Element;

        bool isCapture = defender != null && !defender.HasEffect(EffectType.Shield);

        if (isCapture)
        {
            string defName = defender.PieceType.ToString();
            string defElem = defender.Element;
            Append($"{_moveNumber}. {atkElem} {atkName} {fromSq} x {defElem} {defName} {toSq}");
        }
        else
        {
            Append($"{_moveNumber}. {atkName} {fromSq} > {toSq}");
        }

        _pendingCapture = isCapture;
    }

    private void OnCaptureResult(int from, int to, ElementMixResult mix, ElementReactionResult reaction)
    {
        if (!_pendingCapture) return;
        _pendingCapture = false;

        if (mix != null && !string.IsNullOrEmpty(mix.newElement))
            Append($"  > {mix.newElement}");

        if (reaction != null && !string.IsNullOrEmpty(reaction.flavor))
            Append($"  <i><color=#999>{reaction.flavor}</color></i>");
    }

    private void OnBoardReset()
    {
        _sb.Clear();
        _logText.text = "";
        _pendingCapture = false;
        _moveNumber = 0;
    }

    private void Append(string line)
    {
        if (_sb.Length > 0) _sb.Append('\n');
        _sb.Append(line);
        _dirty = true;
    }
}
