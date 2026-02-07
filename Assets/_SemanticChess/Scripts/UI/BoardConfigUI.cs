using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ConfigMode
{
    Local,
    VsAI,
    OnlineWhite,
    OnlineBlack,
    EditOnly
}

public class BoardConfigUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private ElementSelectorUI _selectorUI;
    [SerializeField] private TMP_Text _titleText;

    public event Action<BoardLayoutData> OnConfigReady;
    public event Action OnConfigCancelled;

    private ChessBoard _board;
    private ConfigMode _mode;
    private int _selectedSlotIndex = -1;
    private GameObject _slotHighlight;

    private void Awake()
    {
        _readyButton.onClick.AddListener(OnReady);
        _resetButton.onClick.AddListener(OnReset);
        if (_backButton != null)
            _backButton.onClick.AddListener(OnCancel);
        _selectorUI.OnElementSelected += OnElementPicked;

        _panel.SetActive(false);
    }

    public void Show(ConfigMode mode, ChessBoard board)
    {
        _board = board;
        _mode = mode;
        _selectedSlotIndex = -1;

        // Set up the board visually with the current saved layout
        var layout = BoardLayout.GetLayout();

        bool flipBoard = mode == ConfigMode.OnlineBlack;
        _board.SetFlipped(flipBoard);
        _board.SetupForConfig(layout);

        // Title
        if (_titleText != null)
        {
            _titleText.text = mode switch
            {
                ConfigMode.EditOnly => "Edit Layout",
                ConfigMode.OnlineWhite => "Set White Elements",
                ConfigMode.OnlineBlack => "Set Black Elements",
                _ => "Set Elements"
            };
        }

        _panel.SetActive(true);
        PopChildButtons(_panel);
    }

    public void Hide()
    {
        _panel.SetActive(false);
        ClearHighlight();
    }

    private void Update()
    {
        if (!_panel.activeSelf || _board == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            // If click lands on the selector panel itself, let the UI handle it
            if (_selectorUI.IsOpen && IsPointerOverPanel())
                return;

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            int index = _board.WorldToTileIndex(mouseWorld);

            if (index >= 0)
            {
                ChessPiece piece = _board.GetPieceAt(index);
                if (piece != null && CanEditPiece(piece))
                {
                    _selectedSlotIndex = index;
                    ShowHighlight(index);

                    Vector3 tileWorld = _board.transform.TransformPoint(GetTileLocalPos(index));
                    Vector2 screenPos = Camera.main.WorldToScreenPoint(tileWorld);
                    float tileHalfHeight = GetTileScreenHalfHeight(tileWorld);
                    _selectorUI.Show(_board.EmojiService, screenPos, tileHalfHeight);
                    return;
                }
            }

            // Clicked empty space or non-editable piece — close selector
            if (_selectorUI.IsOpen)
            {
                _selectorUI.Hide();
                ClearHighlight();
                _selectedSlotIndex = -1;
            }
        }
    }

    private bool IsPointerOverPanel()
    {
        Canvas canvas = _selectorUI.PanelRect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            _selectorUI.PanelRect, Input.mousePosition, cam);
    }

    private float GetTileScreenHalfHeight(Vector3 tileWorld)
    {
        float boardWorldSize = _board.BoardSprite != null ? _board.BoardSprite.bounds.size.x : 8f;
        float halfTile = boardWorldSize / 16f;
        Vector3 top = Camera.main.WorldToScreenPoint(tileWorld + Vector3.up * halfTile);
        Vector3 center = Camera.main.WorldToScreenPoint(tileWorld);
        return top.y - center.y;
    }

    private bool CanEditPiece(ChessPiece piece)
    {
        return _mode switch
        {
            ConfigMode.Local => true,
            ConfigMode.VsAI => true,
            ConfigMode.OnlineWhite => piece.Color == PieceColor.White,
            ConfigMode.OnlineBlack => piece.Color == PieceColor.Black,
            ConfigMode.EditOnly => true,
            _ => false
        };
    }

    private void OnElementPicked(ElementEntry element)
    {
        if (_selectedSlotIndex < 0 || _board == null) return;

        ChessPiece piece = _board.GetPieceAt(_selectedSlotIndex);
        if (piece == null) return;

        // Update the piece's visual element
        piece.SetElement(element.name, element.emoji, _board.EmojiService, _board.FloatingTextFont);

        // Store in layout
        BoardLayout.SetSlot(piece.Color, _selectedSlotIndex, element.name, element.emoji);

        ClearHighlight();
        _selectedSlotIndex = -1;
    }

    private void OnReady()
    {
        BoardLayout.Save();
        var layout = BoardLayout.GetLayout();
        Hide();
        OnConfigReady?.Invoke(layout);
    }

    private void OnReset()
    {
        BoardLayout.ResetToDefaults();
        // Refresh the board with defaults
        var layout = BoardLayout.GetLayout();
        _board.SetupForConfig(layout);
    }

    private void OnCancel()
    {
        Hide();
        OnConfigCancelled?.Invoke();
    }

    private void ShowHighlight(int index)
    {
        ClearHighlight();

        // Create a simple highlight square at the tile position
        _slotHighlight = new GameObject("SlotHighlight");
        _slotHighlight.transform.SetParent(_board.transform, false);

        // Get tile position from the board
        Vector3 tileWorld = _board.transform.TransformPoint(GetTileLocalPos(index));
        _slotHighlight.transform.position = tileWorld;

        var sr = _slotHighlight.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 1f, 0.3f, 0.3f);
        sr.sortingLayerName = "Pieces";
        sr.sortingOrder = 0;

        // Create a 1x1 white sprite for the highlight
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        float ppu = _board.BoardSprite != null ? _board.BoardSprite.sprite.pixelsPerUnit : 100f;
        float boardWorldSize = _board.BoardSprite != null ? _board.BoardSprite.bounds.size.x : 8f;
        float tileWorldSize = boardWorldSize / 8f;
        float spriteSize = tileWorldSize * ppu;
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f / tileWorldSize);

        // Pulse animation
        _slotHighlight.transform.DOScale(Vector3.one * 1.05f, 0.5f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void ClearHighlight()
    {
        if (_slotHighlight != null)
        {
            DOTween.Kill(_slotHighlight.transform);
            Destroy(_slotHighlight);
            _slotHighlight = null;
        }
    }

    private Vector3 GetTileLocalPos(int index)
    {
        // Reconstruct tile position from board geometry
        // This matches ChessBoard's ComputeTilePositions logic
        if (_board.BoardSprite == null) return Vector3.zero;

        float boardSize = _board.BoardSprite.bounds.size.x;
        float tileSize = boardSize / 8f;
        float halfBoard = boardSize / 2f;

        int col = index % 8;
        int row = index / 8;

        if (_board.IsFlipped)
        {
            col = 7 - col;
            row = 7 - row;
        }

        float x = -halfBoard + tileSize * (col + 0.5f);
        float y = halfBoard - tileSize * (row + 0.5f);

        return new Vector3(x, y, 0f);
    }

    private static void PopChildButtons(GameObject panel)
    {
        var buttons = panel.GetComponentsInChildren<JuicyButton>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].PlayAppear(i * 0.04f);
    }
}
