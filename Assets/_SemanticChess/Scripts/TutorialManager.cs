using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Card display utility for the tutorial. Handles showing/hiding tutorial cards
/// and camera zoom effects. Controlled by TutorialGameMode.
/// </summary>
public class TutorialManager
{
    private Camera _cam;
    private float _origCamSize;
    private Vector3 _origCamPos;

    // Card UI
    private GameObject _cardInstance;
    private CanvasGroup _cardGroup;
    private RectTransform _cardRect;
    private RectTransform _canvasRect;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;
    private Button _dismissBtn;
    private JuicyButton _juicyBtn;
    private bool _cardDismissed;

    // Dock state
    private bool _isDocked;
    private Vector2 _centerPos;

    private const float ZoomDuration = 0.6f;
    private const float ZoomFactor = 0.45f;
    private const float DockScale = 0.6f;
    private const float DockAlpha = 0.9f;
    private const float DockDuration = 0.35f;
    private const float DockPadding = 30f;

    public void Init(GameObject cardPrefab)
    {
        _cam = Camera.main;
        _origCamSize = _cam.orthographicSize;
        _origCamPos = _cam.transform.position;

        SetupCard(cardPrefab);
    }

    /// <summary>
    /// Show card centered, wait for dismiss, then fully hide it.
    /// Used for post-capture cards.
    /// </summary>
    public IEnumerator ShowCard(string title, string body)
    {
        HideDocked();

        _titleText.text = title;
        _bodyText.text = body;
        _cardDismissed = false;
        _dismissBtn.gameObject.SetActive(true);

        // Pop in centered
        if (_juicyBtn != null) _juicyBtn.SetBaseScale(1f);
        _cardInstance.SetActive(true);
        _cardRect.anchoredPosition = _centerPos;
        _cardGroup.alpha = 0f;
        _cardRect.localScale = Vector3.one * 0.85f;

        var seq = DOTween.Sequence();
        seq.Append(_cardGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        seq.Join(_cardRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack));

        yield return new WaitForSeconds(0.25f);

        while (!_cardDismissed)
            yield return null;

        // Fade out
        var outSeq = DOTween.Sequence();
        outSeq.Append(_cardGroup.DOFade(0f, 0.15f).SetEase(Ease.InQuad));
        outSeq.Join(_cardRect.DOScale(0.9f, 0.15f).SetEase(Ease.InQuad));

        yield return new WaitForSeconds(0.2f);

        _cardInstance.SetActive(false);
        _isDocked = false;
    }

    /// <summary>
    /// Show card centered, wait for dismiss, then dock it to the left side
    /// so the player can still read the instructions while playing.
    /// Used for intro cards.
    /// </summary>
    public IEnumerator ShowCardAndDock(string title, string body)
    {
        HideDocked();

        _titleText.text = title;
        _bodyText.text = body;
        _cardDismissed = false;
        _dismissBtn.gameObject.SetActive(true);

        // Pop in centered
        if (_juicyBtn != null) _juicyBtn.SetBaseScale(1f);
        _cardInstance.SetActive(true);
        _cardRect.anchoredPosition = _centerPos;
        _cardGroup.alpha = 0f;
        _cardRect.localScale = Vector3.one * 0.85f;

        var seq = DOTween.Sequence();
        seq.Append(_cardGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        seq.Join(_cardRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack));

        yield return new WaitForSeconds(0.25f);

        while (!_cardDismissed)
            yield return null;

        // Dock to left — set JuicyButton base scale so hover/exit animate around docked size
        _dismissBtn.gameObject.SetActive(false);
        if (_juicyBtn != null) _juicyBtn.SetBaseScale(DockScale);
        _isDocked = true;

        float cardHalfW = _cardRect.rect.width * DockScale * 0.5f;
        float canvasHalfW = _canvasRect.rect.width * 0.5f;
        float dockX = -canvasHalfW + cardHalfW + DockPadding;

        var dockSeq = DOTween.Sequence();
        dockSeq.Append(_cardRect.DOAnchorPos(new Vector2(dockX, 0f), DockDuration).SetEase(Ease.InOutQuad));
        dockSeq.Join(_cardRect.DOScale(DockScale, DockDuration).SetEase(Ease.InOutQuad));
        dockSeq.Join(_cardGroup.DOFade(DockAlpha, DockDuration).SetEase(Ease.InOutQuad));

        yield return new WaitForSeconds(DockDuration);
    }

    /// <summary>
    /// Immediately hide any docked card (called before showing the next card).
    /// </summary>
    public void HideDocked()
    {
        if (!_isDocked) return;
        _isDocked = false;
        DOTween.Kill(_cardRect);
        DOTween.Kill(_cardGroup);
        _cardInstance.SetActive(false);
    }

    public IEnumerator ZoomToTile(ChessBoard board, int tileIndex)
    {
        HideDocked();

        Vector3 tileWorld = board.GetTilePosition(tileIndex);
        Vector3 targetPos = new Vector3(tileWorld.x, tileWorld.y, _origCamPos.z);
        float targetSize = _origCamSize * ZoomFactor;

        _cam.transform.DOMove(targetPos, ZoomDuration).SetEase(Ease.InOutQuad);
        _cam.DOOrthoSize(targetSize, ZoomDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(ZoomDuration);
    }

    public IEnumerator ZoomOut()
    {
        _cam.transform.DOMove(_origCamPos, ZoomDuration).SetEase(Ease.InOutQuad);
        _cam.DOOrthoSize(_origCamSize, ZoomDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(ZoomDuration);
    }

    public void Destroy()
    {
        if (_cardInstance != null)
        {
            DOTween.Kill(_cardRect);
            DOTween.Kill(_cardGroup);
            Object.Destroy(_cardInstance);
        }
    }

    private void SetupCard(GameObject prefab)
    {
        var canvas = GameManager.Instance.GameUI.GetCanvas();
        _canvasRect = canvas.GetComponent<RectTransform>();
        _cardInstance = Object.Instantiate(prefab, canvas.transform);
        _cardRect = _cardInstance.GetComponent<RectTransform>();
        _cardGroup = _cardInstance.GetComponent<CanvasGroup>();
        if (_cardGroup == null)
            _cardGroup = _cardInstance.AddComponent<CanvasGroup>();

        _centerPos = _cardRect.anchoredPosition;

        _titleText = _cardInstance.transform.Find("Title").GetComponent<TMP_Text>();
        _bodyText = _cardInstance.transform.Find("Body").GetComponent<TMP_Text>();

        _dismissBtn = _cardInstance.GetComponentInChildren<Button>();
        _dismissBtn.onClick.AddListener(() => _cardDismissed = true);

        _juicyBtn = _cardInstance.GetComponentInChildren<JuicyButton>();

        _cardInstance.SetActive(false);
    }
}
