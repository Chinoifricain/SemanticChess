using System.Collections.Generic;
using DG.Tweening;
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

    private readonly List<(RectTransform rt, Vector2 target)> _diffButtons = new();
    private bool _difficultyOpen;
    private Sequence _difficultyTween;

    private void Awake()
    {
        _localButton.onClick.AddListener(() => GameManager.Instance.StartMatch(GameModeType.Local));
        _aiButton.onClick.AddListener(ToggleDifficulty);

        _easyButton.onClick.AddListener(() => StartAI(0));
        _mediumButton.onClick.AddListener(() => StartAI(1));
        _hardButton.onClick.AddListener(() => StartAI(2));

        _onlineButton.interactable = false;

        // Buttons are children of the AI button — local (0,0) = hidden behind parent
        Button[] btns = { _easyButton, _mediumButton, _hardButton };
        foreach (var btn in btns)
        {
            var rt = btn.GetComponent<RectTransform>();
            _diffButtons.Add((rt, rt.anchoredPosition));
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            btn.gameObject.SetActive(false);
        }
    }

    private void ToggleDifficulty()
    {
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
        GameManager.Instance.AIDifficulty = difficulty;
        GameManager.Instance.StartMatch(GameModeType.VsAI);
    }

    public void Show()
    {
        _panel.SetActive(true);
        _difficultyOpen = false;
        _difficultyTween?.Kill();
        foreach (var (rt, _) in _diffButtons)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            rt.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
