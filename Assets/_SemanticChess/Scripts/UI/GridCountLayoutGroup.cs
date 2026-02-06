using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Grid Count Layout Group")]
public class GridCountLayoutGroup : LayoutGroup
{
    [SerializeField] private int m_GridCountX = 1;
    [SerializeField] private int m_GridCountY = 1;
    [SerializeField] private Vector2 m_Spacing;

    public int gridCountX
    {
        get => m_GridCountX;
        set { SetProperty(ref m_GridCountX, Mathf.Max(1, value)); }
    }

    public int gridCountY
    {
        get => m_GridCountY;
        set { SetProperty(ref m_GridCountY, Mathf.Max(1, value)); }
    }

    public Vector2 spacing
    {
        get => m_Spacing;
        set { SetProperty(ref m_Spacing, value); }
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        float min = padding.horizontal;
        float preferred = padding.horizontal;
        SetLayoutInputForAxis(min, preferred, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float min = padding.vertical;
        float preferred = padding.vertical;
        SetLayoutInputForAxis(min, preferred, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        SetCellsAlongAxis(0);
    }

    public override void SetLayoutVertical()
    {
        SetCellsAlongAxis(1);
    }

    private void SetCellsAlongAxis(int axis)
    {
        Rect rect = rectTransform.rect;
        float availableWidth = rect.width - padding.horizontal - m_Spacing.x * (m_GridCountX - 1);
        float availableHeight = rect.height - padding.vertical - m_Spacing.y * (m_GridCountY - 1);
        float cellWidth = availableWidth / m_GridCountX;
        float cellHeight = availableHeight / m_GridCountY;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            int col = i % m_GridCountX;
            int row = i / m_GridCountX;

            if (row >= m_GridCountY)
                break;

            RectTransform child = rectChildren[i];

            if (axis == 0)
            {
                float x = padding.left + col * (cellWidth + m_Spacing.x);
                SetChildAlongAxis(child, 0, x, cellWidth);
            }
            else
            {
                float y = padding.top + row * (cellHeight + m_Spacing.y);
                SetChildAlongAxis(child, 1, y, cellHeight);
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        m_GridCountX = Mathf.Max(1, m_GridCountX);
        m_GridCountY = Mathf.Max(1, m_GridCountY);
    }
#endif
}
