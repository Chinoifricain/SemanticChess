using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ElementSelectorUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_InputField _searchInput;
    [SerializeField] private RectTransform _contentParent;
    [SerializeField] private ScrollRect _scrollRect;
    [Header("Breadcrumb")]
    [SerializeField] private TMP_Text _breadcrumbTop;
    [SerializeField] private TMP_Text _breadcrumbBottom;

    [Header("Entry")]
    [SerializeField] private ElementEntryUI _entryPrefab;
    [SerializeField] private float _entrySpacing = 4f;

    public event Action<ElementEntry> OnElementSelected;
    public bool IsOpen => _isOpen;
    public RectTransform PanelRect => _panel.GetComponent<RectTransform>();

    private readonly List<string> _breadcrumbPath = new List<string>();
    private readonly List<GameObject> _entries = new List<GameObject>();
    private EmojiLoader _emojiLoader;
    private GameObject _backdrop;
    private bool _showAbove;
    private bool _isOpen;
    private bool _suppressSearchCallback;

    private void Awake()
    {
        if (_searchInput != null)
            _searchInput.onValueChanged.AddListener(OnSearchChanged);

        CreateBackdrop();
        _panel.SetActive(false);
    }

    private void CreateBackdrop()
    {
        // Fullscreen transparent image behind the panel — blocks UI raycasts only
        _backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        _backdrop.transform.SetParent(_panel.transform.parent, false);

        var rt = _backdrop.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = _backdrop.GetComponent<Image>();
        img.color = Color.clear;

        // Backdrop must render behind the panel
        _backdrop.transform.SetSiblingIndex(_panel.transform.GetSiblingIndex());
        _backdrop.SetActive(false);
    }

    public void Show(EmojiLoader emojiLoader, Vector2 screenPos, float tileHalfHeight)
    {
        _emojiLoader = emojiLoader;
        bool wasOpen = _isOpen;

        _backdrop.SetActive(true);
        _panel.SetActive(true);
        _isOpen = true;

        var panelRt = _panel.GetComponent<RectTransform>();
        DOTween.Kill(panelRt);
        PositionPanel(panelRt, screenPos, tileHalfHeight);

        if (!wasOpen)
        {
            _breadcrumbPath.Clear();
            if (_searchInput != null)
                _searchInput.text = "";

            // Fix caret rendering order (same as room code input)
            if (_searchInput != null)
            {
                _searchInput.ActivateInputField();
                StartCoroutine(FixCaretOrder(_searchInput));
            }

            // Animate in: scale + slide from the piece direction
            float slideOffset = _showAbove ? -15f : 15f;
            var targetPos = panelRt.anchoredPosition;
            panelRt.anchoredPosition = targetPos + new Vector2(0, slideOffset);
            panelRt.localScale = Vector3.one * 0.9f;
            DOTween.Sequence()
                .Join(panelRt.DOAnchorPos(targetPos, 0.2f).SetEase(Ease.OutCubic))
                .Join(panelRt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack))
                .SetUpdate(true);

            ShowLevel(null);
        }
        else
        {
            // Already open — subtle pop to acknowledge the reposition
            panelRt.localScale = Vector3.one * 0.97f;
            panelRt.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    private void PositionPanel(RectTransform panelRt, Vector2 screenPos, float tileHalfHeight)
    {
        var parentRt = panelRt.parent as RectTransform;
        if (parentRt == null) return;

        Canvas canvas = _panel.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        _showAbove = screenPos.y < Screen.height / 2f;

        // Offset screen position to the tile edge so the panel doesn't overlap the piece
        float edgeOffset = _showAbove ? tileHalfHeight : -tileHalfHeight;
        Vector2 edgeScreenPos = screenPos + new Vector2(0, edgeOffset);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, edgeScreenPos, cam, out Vector2 localPos);

        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, _showAbove ? 0f : 1f);

        float gap = 8f;
        panelRt.anchoredPosition = localPos + new Vector2(0, _showAbove ? gap : -gap);

        // Clamp so the panel stays on screen
        ClampToScreen(panelRt, canvas != null ? canvas.scaleFactor : 1f);
    }

    private static void ClampToScreen(RectTransform rt, float scaleFactor)
    {
        Vector3[] corners = new Vector3[4]; // BL, TL, TR, BR
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

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _backdrop.SetActive(false);

        var panelRt = _panel.GetComponent<RectTransform>();
        DOTween.Kill(panelRt);

        float slideDir = _showAbove ? -15f : 15f;
        DOTween.Sequence()
            .Join(panelRt.DOAnchorPos(panelRt.anchoredPosition + new Vector2(0, slideDir), 0.15f)
                .SetEase(Ease.InCubic))
            .Join(panelRt.DOScale(Vector3.one * 0.9f, 0.15f).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                _panel.SetActive(false);
                panelRt.localScale = Vector3.one;
            })
            .SetUpdate(true);
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (Input.GetMouseButtonDown(0))
        {
            string linkId = FindClickedLink(_breadcrumbTop)
                         ?? FindClickedLink(_breadcrumbBottom);
            if (linkId != null)
                OnBreadcrumbClicked(linkId);
        }
    }

    private static string FindClickedLink(TMP_Text text)
    {
        if (text == null) return null;
        Canvas canvas = text.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, cam);
        if (linkIndex >= 0)
            return text.textInfo.linkInfo[linkIndex].GetLinkID();
        return null;
    }

    private void OnBreadcrumbClicked(string id)
    {
        // Clear search without triggering the callback
        if (_searchInput != null && !string.IsNullOrEmpty(_searchInput.text))
        {
            _suppressSearchCallback = true;
            _searchInput.text = "";
            _suppressSearchCallback = false;
        }

        _breadcrumbPath.Clear();
        if (id == "root")
        {
            ShowLevel(null);
        }
        else
        {
            _breadcrumbPath.Add(id);
            ShowLevel(id);
        }
    }

    private void OnSearchChanged(string query)
    {
        if (_suppressSearchCallback) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            string parent = _breadcrumbPath.Count > 0 ? _breadcrumbPath[^1] : null;
            ShowLevel(parent);
        }
        else
        {
            ShowSearchResults(query);
        }
    }

    private void ShowLevel(string parentName)
    {
        ClearEntries();

        List<ElementEntry> elements;
        if (parentName == null)
        {
            elements = ElementCollection.GetRoots();
            UpdateBreadcrumb();
        }
        else
        {
            // Show the parent itself as selectable, then its children
            var parentEntry = ElementCollection.GetElement(parentName);
            if (parentEntry != null)
                CreateEntry(parentEntry, false);

            elements = ElementCollection.GetChildrenOf(parentName);
            UpdateBreadcrumb();
        }

        foreach (var element in elements)
        {
            bool hasChildren = ElementCollection.GetChildrenOf(element.name).Count > 0;
            CreateEntry(element, hasChildren);
        }

        UpdateContentSize();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ShowSearchResults(string query)
    {
        ClearEntries();

        var results = ElementCollection.Search(query);
        SetBreadcrumbTexts($"<link=\"root\"><color=#8888FF>Elements</color></link> <color=#666666>></color> Search: \"{query}\"", "");

        foreach (var element in results)
            CreateEntry(element, false);

        UpdateContentSize();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreateEntry(ElementEntry element, bool hasChildren)
    {
        int index = _entries.Count;

        var entry = Instantiate(_entryPrefab, _contentParent);
        var go = entry.gameObject;
        entry.Setup(element, hasChildren, _emojiLoader);

        // Position in scroll list
        var rt = go.GetComponent<RectTransform>();
        float entryHeight = rt.sizeDelta.y;
        float y = -(index * (entryHeight + _entrySpacing));
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, entryHeight);

        // Wire buttons
        var captured = element;
        entry.SelectButton.onClick.AddListener(() => SelectElement(captured));
        if (hasChildren && entry.ExpandButton != null)
            entry.ExpandButton.onClick.AddListener(() => DrillInto(captured.name));

        // Appear animation
        var juicy = go.GetComponent<JuicyButton>();
        if (juicy != null)
            juicy.PlayAppear(index * 0.03f);

        _entries.Add(go);
    }

    private void SelectElement(ElementEntry element)
    {
        OnElementSelected?.Invoke(element);
        Hide();
    }

    private void DrillInto(string elementName)
    {
        _breadcrumbPath.Clear();
        _breadcrumbPath.Add(elementName);
        ShowLevel(elementName);
    }

    private void UpdateBreadcrumb()
    {
        string currentName = _breadcrumbPath.Count > 0 ? _breadcrumbPath[^1] : null;

        if (currentName == null)
        {
            SetBreadcrumbTexts("Elements", "");
            return;
        }

        var entry = ElementCollection.GetElement(currentName);

        if (entry != null && !string.IsNullOrEmpty(entry.parentA))
        {
            // Derived element — show both parent lineages
            SetBreadcrumbTexts(
                BuildLineageText(entry.parentA),
                BuildLineageText(entry.parentB));
        }
        else
        {
            // Root element
            SetBreadcrumbTexts(
                $"<link=\"root\"><color=#8888FF>Elements</color></link> <color=#666666>></color> {currentName}",
                "");
        }
    }

    private void SetBreadcrumbTexts(string top, string bottom)
    {
        if (_breadcrumbTop != null)
        {
            _breadcrumbTop.text = top;
            _breadcrumbTop.gameObject.SetActive(!string.IsNullOrEmpty(top));
            ClampRight(_breadcrumbTop);
        }
        if (_breadcrumbBottom != null)
        {
            _breadcrumbBottom.text = bottom;
            _breadcrumbBottom.gameObject.SetActive(!string.IsNullOrEmpty(bottom));
            ClampRight(_breadcrumbBottom);
        }
    }

    /// <summary>
    /// Left-aligned when text fits. When overflowing, switches to right-alignment
    /// so the rightmost part (closest ancestors) stays visible.
    /// TMP Masking mode clips characters outside the rect via shader.
    /// </summary>
    private static void ClampRight(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeSelf) return;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Masking;
        text.ForceMeshUpdate();

        float preferred = text.preferredWidth;
        float available = text.rectTransform.rect.width;

        text.alignment = available > 0 && preferred > available
            ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft;
    }

    private static string BuildLineageText(string elementName)
    {
        if (string.IsNullOrEmpty(elementName)) return "";

        var chain = GetLineage(elementName);
        string text = "<link=\"root\"><color=#8888FF>Elements</color></link>";
        foreach (var name in chain)
            text += $" <color=#666666>></color> <link=\"{name}\"><color=#8888FF>{name}</color></link>";
        text += " <color=#666666>></color>";
        return text;
    }

    /// <summary>
    /// Traces an element's ancestry by following parentA at each step, returning
    /// the chain from the root ancestor down to the given element.
    /// </summary>
    private static List<string> GetLineage(string elementName)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();
        string current = elementName;
        while (current != null && !visited.Contains(current))
        {
            var entry = ElementCollection.GetElement(current);
            if (entry == null) break;
            chain.Add(current);
            visited.Add(current);
            if (string.IsNullOrEmpty(entry.parentA)) break;
            current = entry.parentA;
        }
        chain.Reverse();
        return chain;
    }

    private void UpdateContentSize()
    {
        float entryHeight = _entryPrefab.GetComponent<RectTransform>().sizeDelta.y;
        float totalHeight = _entries.Count * (entryHeight + _entrySpacing);
        _contentParent.sizeDelta = new Vector2(_contentParent.sizeDelta.x, totalHeight);
    }

    private void ClearEntries()
    {
        foreach (var go in _entries)
            Destroy(go);
        _entries.Clear();
    }

    private static IEnumerator FixCaretOrder(TMP_InputField input)
    {
        yield return null;
        Transform caret = input.transform.Find("Caret");
        if (caret != null)
            caret.SetAsLastSibling();
    }
}
