using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _localButton;
    [SerializeField] private Button _aiButton;
    [SerializeField] private Button _onlineButton;

    [Header("AI Difficulty (children of AI button)")]
    [SerializeField] private Button _easyButton;
    [SerializeField] private Button _mediumButton;
    [SerializeField] private Button _hardButton;

    [Header("Online (children of Online button)")]
    [SerializeField] private Button _createRoomButton;
    [SerializeField] private Button _joinRoomButton;

    [Header("Online Panels")]
    [SerializeField] private GameObject _joinPanel;
    [SerializeField] private TMP_InputField _codeInput;
    [SerializeField] private Button _joinConfirmButton;
    [SerializeField] private Button _joinBackButton;
    [SerializeField] private GameObject _waitingPanel;
    [SerializeField] private TMP_Text _waitingText;
    [SerializeField] private Button _waitingCancelButton;

    [Header("Board Config")]
    [SerializeField] private BoardConfigUI _boardConfigUI;
    [SerializeField] private Button _editLayoutButton;

    private readonly List<(RectTransform rt, Vector2 target)> _diffButtons = new();
    private readonly List<(RectTransform rt, Vector2 target)> _onlineButtons = new();
    private bool _difficultyOpen;
    private bool _onlineOpen;
    private Sequence _difficultyTween;
    private Sequence _onlineTween;

    private PieceColor _assignedColor;
    private GameModeType _pendingMode;
    private int _pendingDifficulty;
    private BoardLayoutData _onlineLayout;

    private void Awake()
    {
        _localButton.onClick.AddListener(() =>
        {
            FoldAll();
            StartWithConfig(GameModeType.Local, ConfigMode.Local);
        });
        _aiButton.onClick.AddListener(ToggleDifficulty);

        _easyButton.onClick.AddListener(() => StartAI(0));
        _mediumButton.onClick.AddListener(() => StartAI(1));
        _hardButton.onClick.AddListener(() => StartAI(2));

        // Online sub-buttons
        _onlineButton.onClick.AddListener(ToggleOnline);
        _onlineButton.interactable = true;

        if (_createRoomButton != null)
            _createRoomButton.onClick.AddListener(OnCreateRoom);
        if (_joinRoomButton != null)
            _joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        if (_joinConfirmButton != null)
            _joinConfirmButton.onClick.AddListener(OnJoinConfirm);
        if (_joinBackButton != null)
            _joinBackButton.onClick.AddListener(OnJoinBack);
        if (_waitingCancelButton != null)
            _waitingCancelButton.onClick.AddListener(OnWaitingCancel);

        // Edit Layout button
        if (_editLayoutButton != null)
            _editLayoutButton.onClick.AddListener(OnEditLayout);

        // Board config events
        if (_boardConfigUI != null)
        {
            _boardConfigUI.OnConfigReady += OnConfigReady;
            _boardConfigUI.OnConfigCancelled += OnConfigCancelled;
        }

        // Set up AI difficulty sub-buttons (hidden behind parent)
        Button[] diffBtns = { _easyButton, _mediumButton, _hardButton };
        foreach (var btn in diffBtns)
        {
            var rt = btn.GetComponent<RectTransform>();
            _diffButtons.Add((rt, rt.anchoredPosition));
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            btn.gameObject.SetActive(false);
        }

        // Set up online sub-buttons (same pattern)
        Button[] onlineBtns = { _createRoomButton, _joinRoomButton };
        foreach (var btn in onlineBtns)
        {
            if (btn == null) continue;
            var rt = btn.GetComponent<RectTransform>();
            _onlineButtons.Add((rt, rt.anchoredPosition));
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            btn.gameObject.SetActive(false);
        }

        // Hide panels
        if (_joinPanel != null) _joinPanel.SetActive(false);
        if (_waitingPanel != null) _waitingPanel.SetActive(false);
    }

    // --- AI Difficulty ---

    private void ToggleDifficulty()
    {
        if (_onlineOpen) FoldOnline();
        if (_difficultyOpen) FoldDifficulty();
        else UnfoldDifficulty();
    }

    private void UnfoldDifficulty()
    {
        _difficultyOpen = true;
        _difficultyTween?.Kill();
        _difficultyTween = DOTween.Sequence();

        for (int i = 0; i < _diffButtons.Count; i++)
        {
            var (rt, target) = _diffButtons[i];
            rt.gameObject.SetActive(true);
            float delay = i * 0.04f;
            _difficultyTween.Insert(delay, rt.DOAnchorPos(target, 0.25f).SetEase(Ease.OutBack));
            _difficultyTween.Insert(delay, rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
        }
    }

    private void FoldDifficulty()
    {
        _difficultyOpen = false;
        _difficultyTween?.Kill();
        _difficultyTween = DOTween.Sequence();

        for (int i = _diffButtons.Count - 1; i >= 0; i--)
        {
            var (rt, _) = _diffButtons[i];
            float delay = (_diffButtons.Count - 1 - i) * 0.03f;
            _difficultyTween.Insert(delay, rt.DOAnchorPos(Vector2.zero, 0.18f).SetEase(Ease.InBack));
            _difficultyTween.Insert(delay, rt.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));
        }

        _difficultyTween.OnComplete(() =>
        {
            foreach (var (rt, _) in _diffButtons)
                rt.gameObject.SetActive(false);
        });
    }

    private void StartAI(int difficulty)
    {
        _difficultyOpen = false;
        _difficultyTween?.Kill();
        _pendingDifficulty = difficulty;
        GameManager.Instance.AIDifficulty = difficulty;
        StartWithConfig(GameModeType.VsAI, ConfigMode.VsAI);
    }

    // --- Online ---

    private void ToggleOnline()
    {
        if (_difficultyOpen) FoldDifficulty();
        if (_onlineOpen) FoldOnline();
        else UnfoldOnline();
    }

    private void UnfoldOnline()
    {
        _onlineOpen = true;
        _onlineTween?.Kill();
        _onlineTween = DOTween.Sequence();

        for (int i = 0; i < _onlineButtons.Count; i++)
        {
            var (rt, target) = _onlineButtons[i];
            rt.gameObject.SetActive(true);
            float delay = i * 0.04f;
            _onlineTween.Insert(delay, rt.DOAnchorPos(target, 0.25f).SetEase(Ease.OutBack));
            _onlineTween.Insert(delay, rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
        }
    }

    private void FoldOnline()
    {
        _onlineOpen = false;
        _onlineTween?.Kill();
        _onlineTween = DOTween.Sequence();

        for (int i = _onlineButtons.Count - 1; i >= 0; i--)
        {
            var (rt, _) = _onlineButtons[i];
            float delay = (_onlineButtons.Count - 1 - i) * 0.03f;
            _onlineTween.Insert(delay, rt.DOAnchorPos(Vector2.zero, 0.18f).SetEase(Ease.InBack));
            _onlineTween.Insert(delay, rt.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));
        }

        _onlineTween.OnComplete(() =>
        {
            foreach (var (rt, _) in _onlineButtons)
                rt.gameObject.SetActive(false);
        });
    }

    private void FoldAll()
    {
        if (_difficultyOpen) FoldDifficulty();
        if (_onlineOpen) FoldOnline();
    }

    // --- Online Flow ---

    private void OnCreateRoom()
    {
        FoldAll();
        StartWithConfig(GameModeType.Online, ConfigMode.OnlineWhite);
    }

    private void OnJoinRoomClicked()
    {
        FoldAll();
        StartWithConfig(GameModeType.Online, ConfigMode.OnlineBlack);
    }

    private void OnJoinBack()
    {
        if (_joinPanel != null) _joinPanel.SetActive(false);
        _panel.SetActive(true);
        PopChildButtons(_panel);
    }

    private void OnJoinConfirm()
    {
        string code = _codeInput != null ? _codeInput.text.Trim().ToUpper() : "";
        if (code.Length != 4)
        {
            Debug.LogWarning("[MenuUI] Room code must be 4 characters");
            return;
        }

        if (_joinPanel != null) _joinPanel.SetActive(false);

        var room = GameManager.Instance.RoomManager;
        room.OnRoomJoined += OnRoomJoined;
        room.OnGameStart += OnGameStart;
        room.OnError += OnOnlineError;
        room.JoinRoom(code);

        ShowWaiting("...");
    }

    private void OnWaitingCancel()
    {
        HideWaiting();
        UnsubscribeRoom();
        GameManager.Instance.RoomManager.Disconnect();
        _panel.SetActive(true);
        PopChildButtons(_panel);
    }

    private void OnRoomJoined(string code, PieceColor color)
    {
        _assignedColor = color;
        ShowWaiting($"{code}");
    }

    private void OnGameStart()
    {
        // Exchange board configs before starting the match
        var room = GameManager.Instance.RoomManager;

        // Send our layout to opponent
        var ourSlots = BoardLayout.GetSlotsForColor(_assignedColor);
        room.SendBoardConfig(ourSlots);

        // Wait for opponent's config
        room.OnOpponentBoardConfig += OnOpponentBoardConfig;
        ShowWaiting("Syncing...");
    }

    private void OnOpponentBoardConfig(List<PieceSlotConfig> opponentSlots)
    {
        var room = GameManager.Instance.RoomManager;
        room.OnOpponentBoardConfig -= OnOpponentBoardConfig;

        // Build merged layout: our color's slots + opponent's slots
        _onlineLayout = new BoardLayoutData();
        var ourSlots = BoardLayout.GetSlotsForColor(_assignedColor);

        if (_assignedColor == PieceColor.White)
        {
            _onlineLayout.whiteSlots = new List<PieceSlotConfig>(ourSlots);
            _onlineLayout.blackSlots = new List<PieceSlotConfig>(opponentSlots);
        }
        else
        {
            _onlineLayout.blackSlots = new List<PieceSlotConfig>(ourSlots);
            _onlineLayout.whiteSlots = new List<PieceSlotConfig>(opponentSlots);
        }

        HideWaiting();
        UnsubscribeRoom();
        GameManager.Instance.StartOnlineMatch(_assignedColor, _onlineLayout);
    }

    private void OnOnlineError(string message)
    {
        Debug.LogError($"[MenuUI] Online error: {message}");
        HideWaiting();
        UnsubscribeRoom();
        _panel.SetActive(true);
        PopChildButtons(_panel);
    }

    private void UnsubscribeRoom()
    {
        var room = GameManager.Instance.RoomManager;
        room.OnRoomJoined -= OnRoomJoined;
        room.OnGameStart -= OnGameStart;
        room.OnError -= OnOnlineError;
    }

    private static System.Collections.IEnumerator FixCaretOrder(TMP_InputField input)
    {
        yield return null; // wait one frame for caret to be created
        Transform caret = input.transform.Find("Caret");
        if (caret != null)
            caret.SetAsLastSibling();
    }

    private void ShowWaiting(string text)
    {
        if (_waitingPanel != null)
        {
            _waitingPanel.SetActive(true);
            if (_waitingText != null)
                _waitingText.text = text;
            PopChildButtons(_waitingPanel);
        }
    }

    private void HideWaiting()
    {
        if (_waitingPanel != null)
            _waitingPanel.SetActive(false);
    }

    private static void PopChildButtons(GameObject panel)
    {
        var buttons = panel.GetComponentsInChildren<JuicyButton>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].PlayAppear(i * 0.04f);
    }

    // --- Board Config ---

    private ConfigMode _pendingConfigMode;

    private void StartWithConfig(GameModeType mode, ConfigMode configMode)
    {
        _pendingMode = mode;
        _pendingConfigMode = configMode;

        // First game ever: skip config (except Edit Layout which always shows)
        if (!ElementCollection.HasPlayedBefore && configMode != ConfigMode.EditOnly)
        {
            if (mode == GameModeType.Online)
            {
                _panel.SetActive(false);
                if (configMode == ConfigMode.OnlineWhite)
                    DoCreateRoom();
                else
                    ShowJoinCodePanel();
            }
            else
            {
                GameManager.Instance.StartMatch(mode);
            }
            return;
        }

        // Show config screen
        _panel.SetActive(false);
        _boardConfigUI.Show(configMode, GameManager.Instance.Board);
    }

    private void OnConfigReady(BoardLayoutData layout)
    {
        switch (_pendingConfigMode)
        {
            case ConfigMode.Local:
                GameManager.Instance.StartMatch(GameModeType.Local, layout);
                break;
            case ConfigMode.VsAI:
                GameManager.Instance.StartMatch(GameModeType.VsAI, layout);
                break;
            case ConfigMode.OnlineWhite:
                DoCreateRoom();
                break;
            case ConfigMode.OnlineBlack:
                ShowJoinCodePanel();
                break;
            case ConfigMode.EditOnly:
                _panel.SetActive(true);
                PopChildButtons(_panel);
                break;
        }
    }

    private void OnConfigCancelled()
    {
        _panel.SetActive(true);
        PopChildButtons(_panel);
    }

    private void OnEditLayout()
    {
        FoldAll();
        StartWithConfig(default, ConfigMode.EditOnly);
    }

    private void DoCreateRoom()
    {
        _panel.SetActive(false);

        var room = GameManager.Instance.RoomManager;
        room.OnRoomJoined += OnRoomJoined;
        room.OnGameStart += OnGameStart;
        room.OnError += OnOnlineError;
        room.CreateRoom();

        ShowWaiting("Creating...");
    }

    private void ShowJoinCodePanel()
    {
        _panel.SetActive(false);

        if (_joinPanel != null)
        {
            _joinPanel.SetActive(true);
            PopChildButtons(_joinPanel);
            if (_codeInput != null)
            {
                _codeInput.text = "";
                _codeInput.ActivateInputField();
                GameManager.Instance.RunCoroutine(FixCaretOrder(_codeInput));
            }
        }
    }

    // --- Show/Hide ---

    public void Show()
    {
        _panel.SetActive(true);
        PopChildButtons(_panel);
        _difficultyOpen = false;
        _difficultyTween?.Kill();
        foreach (var (rt, _) in _diffButtons)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            rt.gameObject.SetActive(false);
        }

        _onlineOpen = false;
        _onlineTween?.Kill();
        foreach (var (rt, _) in _onlineButtons)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            rt.gameObject.SetActive(false);
        }

        HideWaiting();
        if (_joinPanel != null) _joinPanel.SetActive(false);
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
