using UnityEngine;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _localButton;
    [SerializeField] private Button _aiButton;
    [SerializeField] private Button _onlineButton;

    private void Awake()
    {
        _localButton.onClick.AddListener(() => GameManager.Instance.StartMatch(GameModeType.Local));

        // Disabled for now - not implemented
        _aiButton.interactable = false;
        _onlineButton.interactable = false;
    }

    public void Show()
    {
        _panel.SetActive(true);
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
