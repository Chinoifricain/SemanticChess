using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ElementEntryUI : MonoBehaviour
{
    [SerializeField] private Image _emojiImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _selectButton;
    [SerializeField] private Button _expandButton;
    [SerializeField] private GameObject _expandArrow;

    public Button SelectButton => _selectButton;
    public Button ExpandButton => _expandButton;

    public void Setup(ElementEntry element, bool hasChildren, EmojiLoader emojiLoader)
    {
        _nameText.text = element.name;

        // Expand arrow
        if (_expandArrow != null)
            _expandArrow.SetActive(hasChildren);

        // Emoji
        if (_emojiImage != null && emojiLoader != null && !string.IsNullOrEmpty(element.emoji))
        {
            // Try cached first for instant display
            var cached = emojiLoader.GetCached(element.emoji);
            if (cached != null)
            {
                _emojiImage.sprite = cached;
            }
            else
            {
                _emojiImage.color = Color.clear;
                var img = _emojiImage; // capture for lambda
                GameManager.Instance.RunCoroutine(emojiLoader.Load(element.emoji, sprite =>
                {
                    if (img != null)
                    {
                        img.sprite = sprite;
                        img.color = Color.white;
                    }
                }));
            }
        }
    }
}
