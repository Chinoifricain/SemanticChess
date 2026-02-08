using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VideoUI
{
    // Caption UI
    private GameObject _captionObj;
    private RectTransform _captionRect;
    private CanvasGroup _captionGroup;
    private TMP_Text _captionText;

    // Title overlay
    private GameObject _titleObj;
    private CanvasGroup _titleGroup;

    // Fade overlay
    private GameObject _fadeObj;
    private Image _fadeImage;

    // Camera
    private Camera _cam;
    private float _origCamSize;
    private Vector3 _origCamPos;
    private Vector3 _dollyOrigin;
    private bool _hasDollyOrigin;

    private const float FadeDuration = 0.4f;
    private const float CamDuration = 0.7f;
    private const float CamZoomFactor = 0.5f;  // zoom to 50% of original size

    public void Init(Canvas canvas, TMP_FontAsset font)
    {
        _cam = Camera.main;
        _origCamSize = _cam.orthographicSize;
        _origCamPos = _cam.transform.position;

        // Caption container — bottom-anchored, height fits text
        _captionObj = new GameObject("VideoCaption");
        _captionObj.transform.SetParent(canvas.transform, false);
        _captionRect = _captionObj.AddComponent<RectTransform>();
        _captionRect.anchorMin = new Vector2(0f, 0f);
        _captionRect.anchorMax = new Vector2(1f, 0f);
        _captionRect.pivot = new Vector2(0.5f, 0f);
        _captionRect.anchoredPosition = Vector2.zero;

        // Background
        var bgImage = _captionObj.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);

        // Auto-size height to fit text + padding
        var layout = _captionObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(50, 50, 20, 24);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = _captionObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _captionGroup = _captionObj.AddComponent<CanvasGroup>();
        _captionGroup.alpha = 0f;

        // Caption text
        var textObj = new GameObject("CaptionText");
        textObj.transform.SetParent(_captionObj.transform, false);
        _captionText = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) _captionText.font = font;
        _captionText.fontSize = 42;
        _captionText.color = Color.white;
        _captionText.alignment = TextAlignmentOptions.Center;
        _captionText.enableWordWrapping = true;

        _captionObj.SetActive(false);

        // Title overlay — centered
        _titleObj = new GameObject("VideoTitle");
        _titleObj.transform.SetParent(canvas.transform, false);
        _titleGroup = _titleObj.AddComponent<CanvasGroup>();
        _titleGroup.alpha = 0f;
        var titleRect = _titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.3f);
        titleRect.anchorMax = new Vector2(1f, 0.7f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        var titleText = _titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) titleText.font = font;
        titleText.fontSize = 120;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.text = "Semantic Chess";
        _titleObj.SetActive(false);

        // Full-screen fade overlay (hidden until FadeToBlack)
        _fadeObj = new GameObject("VideoFade");
        _fadeObj.transform.SetParent(canvas.transform, false);
        var fadeRect = _fadeObj.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        _fadeImage = _fadeObj.AddComponent<Image>();
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        _fadeImage.raycastTarget = false;
        _fadeObj.SetActive(false);
    }

    // --- Caption ---

    public void ShowCaption(string text)
    {
        _captionText.text = text;

        _captionObj.SetActive(true);
        _captionGroup.DOKill();
        _captionRect.DOKill();
        _captionGroup.alpha = 0f;
        _captionRect.anchoredPosition = new Vector2(0f, -10f);

        var seq = DOTween.Sequence();
        seq.Append(_captionGroup.DOFade(1f, FadeDuration).SetEase(Ease.OutQuad));
        seq.Join(_captionRect.DOAnchorPosY(0f, FadeDuration).SetEase(Ease.OutCubic));
    }

    public void HideCaption()
    {
        _captionGroup.DOKill();
        _captionRect.DOKill();
        _captionGroup.DOFade(0f, 0.25f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            _captionObj.SetActive(false);
        });
    }

    // --- Title ---

    public void ShowTitle()
    {
        _titleObj.SetActive(true);
        _titleGroup.DOKill();
        _titleGroup.alpha = 0f;
        _titleGroup.DOFade(1f, FadeDuration * 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);

        // Scale: start big, settle to 1x
        _titleObj.transform.DOKill();
        _titleObj.transform.localScale = Vector3.one * 1.3f;
        _titleObj.transform.DOScale(1f, 0.6f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void HideTitle()
    {
        if (!_titleObj.activeSelf) return;
        _titleGroup.DOKill();
        _titleObj.transform.DOKill();
        _titleGroup.DOFade(0f, 0.25f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
        {
            _titleObj.SetActive(false);
        });
    }

    // --- Fade ---

    public IEnumerator FadeToBlack(float duration = 1.2f)
    {
        HideCaption();
        _fadeObj.SetActive(true);
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        _fadeImage.DOKill();
        _fadeImage.DOFade(1f, duration).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(duration);
    }

    public IEnumerator FadeFromBlack(float duration = 0.8f)
    {
        _fadeObj.SetActive(true);
        _fadeImage.color = Color.black;
        _fadeImage.DOKill();
        _fadeImage.DOFade(0f, duration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(duration);
        _fadeObj.SetActive(false);
    }

    /// <summary>Starts a non-blocking fade to black (caption stays visible).</summary>
    public void StartGradualFade(float duration)
    {
        _fadeObj.SetActive(true);
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        _fadeImage.DOKill();
        _fadeImage.DOFade(1f, duration).SetEase(Ease.InQuad);
    }

    // --- Camera ---

    public IEnumerator ZoomToTile(ChessBoard board, int tileIndex)
    {
        _hasDollyOrigin = false;
        Vector3 tileWorld = board.GetTilePosition(tileIndex);
        Vector3 targetPos = new Vector3(tileWorld.x, tileWorld.y, _origCamPos.z);
        float targetSize = _origCamSize * CamZoomFactor;

        _cam.transform.DOKill();
        _cam.DOKill();
        _cam.transform.DOMove(targetPos, CamDuration).SetEase(Ease.InOutQuad);
        _cam.DOOrthoSize(targetSize, CamDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(CamDuration);
    }

    public IEnumerator PanAcrossRow(ChessBoard board, int row, float panDuration)
    {
        int leftTile = row * 8;
        int rightTile = row * 8 + 7;

        Vector3 leftWorld = board.GetTilePosition(leftTile);
        Vector3 rightWorld = board.GetTilePosition(rightTile);

        float panZoom = _origCamSize * 0.35f;
        Vector3 startPos = new Vector3(leftWorld.x, leftWorld.y, _origCamPos.z);
        Vector3 endPos = new Vector3(rightWorld.x, rightWorld.y, _origCamPos.z);

        _cam.transform.DOKill();
        _cam.DOKill();

        // Zoom into left side of the row
        _cam.transform.DOMove(startPos, CamDuration).SetEase(Ease.InOutQuad);
        _cam.DOOrthoSize(panZoom, CamDuration).SetEase(Ease.InOutQuad);
        yield return new WaitForSeconds(CamDuration);

        // Pan from left to right
        _cam.transform.DOMove(endPos, panDuration).SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(panDuration);
    }

    /// <summary>Instantly snaps camera back to pre-dolly position. Call before board changes.</summary>
    public void SnapDolly()
    {
        _cam.transform.DOKill();
        if (_hasDollyOrigin)
            _cam.transform.position = _dollyOrigin;
        _hasDollyOrigin = false;
    }

    /// <summary>Starts a subtle camera dolly (realtime, not affected by timeScale).</summary>
    public void StartDolly(float dx, float dy)
    {
        _cam.transform.DOKill();
        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f)) return;
        _dollyOrigin = _cam.transform.position;
        _hasDollyOrigin = true;
        Vector3 target = _dollyOrigin + new Vector3(dx * 2f, dy * 2f, 0f);
        _cam.transform.DOMove(target, 10f).SetEase(Ease.Linear).SetUpdate(true);
    }

    public IEnumerator ZoomOut()
    {
        _hasDollyOrigin = false;
        _cam.transform.DOKill();
        _cam.DOKill();
        _cam.transform.DOMove(_origCamPos, CamDuration).SetEase(Ease.InOutQuad);
        _cam.DOOrthoSize(_origCamSize, CamDuration).SetEase(Ease.InOutQuad);

        yield return new WaitForSeconds(CamDuration);
    }

    public void ResetCamera()
    {
        _hasDollyOrigin = false;
        _cam.transform.DOKill();
        _cam.DOKill();
        _cam.transform.position = _origCamPos;
        _cam.orthographicSize = _origCamSize;
    }

    // --- Cleanup ---

    public void Destroy()
    {
        if (_captionObj != null)
        {
            DOTween.Kill(_captionGroup);
            DOTween.Kill(_captionRect);
            Object.Destroy(_captionObj);
        }
        if (_titleObj != null)
        {
            DOTween.Kill(_titleGroup);
            DOTween.Kill(_titleObj.transform);
            Object.Destroy(_titleObj);
        }
        if (_fadeObj != null)
        {
            DOTween.Kill(_fadeImage);
            Object.Destroy(_fadeObj);
        }

        ResetCamera();
    }
}
