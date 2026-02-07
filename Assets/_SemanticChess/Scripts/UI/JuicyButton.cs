using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class JuicyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Attraction")]
    [SerializeField] private float _attractRadius = 400f;
    [SerializeField] private float _attractStrength = 8f;

    [Header("Hover")]
    [SerializeField] private float _hoverScale = 1.08f;
    [SerializeField] private float _popScale = 1.15f;
    [SerializeField] private float _popDuration = 0.08f;

    [Header("Press")]
    [SerializeField] private Vector3 _squishScale = new Vector3(1.06f, 0.92f, 1f);
    [SerializeField] private float _squishDuration = 0.08f;

    private RectTransform _rt;
    private Vector2 _appliedOffset;
    private Vector2 _targetOffset;
    private Tween _scaleTween;
    private bool _hovered;
    private bool _pressed;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        // Undo previous offset to get layout-managed position
        _rt.anchoredPosition -= _appliedOffset;

        // Compute attraction offset toward mouse
        Vector2 mouseScreen = Input.mousePosition;
        Vector2 btnScreen = RectTransformUtility.WorldToScreenPoint(null, _rt.position);
        Vector2 delta = mouseScreen - btnScreen;
        float dist = delta.magnitude;
        float t = 1f - Mathf.Clamp01(dist / _attractRadius);
        _targetOffset = delta.normalized * (t * _attractStrength);

        // Smooth
        float smooth = Time.unscaledDeltaTime * 12f;
        _appliedOffset = Vector2.Lerp(_appliedOffset, _targetOffset, smooth);

        // Apply on top of layout position
        _rt.anchoredPosition += _appliedOffset;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (_pressed) return;
        _scaleTween?.Kill();
        _rt.localScale = Vector3.one * _popScale;
        _scaleTween = _rt.DOScale(Vector3.one * _hoverScale, _popDuration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        if (_pressed) return;
        _scaleTween?.Kill();
        _scaleTween = _rt.DOScale(Vector3.one, _popDuration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        _scaleTween?.Kill();
        _scaleTween = _rt.DOScale(_squishScale, _squishDuration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        _scaleTween?.Kill();
        Vector3 target = _hovered ? Vector3.one * _hoverScale : Vector3.one;
        _scaleTween = _rt.DOScale(target, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }
}
