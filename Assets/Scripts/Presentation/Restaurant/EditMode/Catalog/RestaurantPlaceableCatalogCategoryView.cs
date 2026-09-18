using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botón reutilizable de categoría del catálogo.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogCategoryView :
    MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Text labelText;

    [SerializeField]
    private Color normalBackground =
        new Color32(243, 240, 233, 255);

    [SerializeField]
    private Color selectedBackground =
        new Color32(113, 143, 77, 255);

    [SerializeField]
    private Color normalText =
        new Color32(48, 52, 50, 255);

    [SerializeField]
    private Color selectedText =
        Color.white;

    private int categoryCode;

    private Action<int> selectionCallback;

    public int CategoryCode => categoryCode;
    public bool IsSelected { get; private set; }

    public void Bind(
        int newCategoryCode,
        string label,
        bool selected,
        Action<int> onSelected
    )
    {
        categoryCode =
            newCategoryCode;

        selectionCallback =
            onSelected;

        if (labelText != null)
        {
            labelText.text =
                label;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(
                HandleButtonClicked
            );

            button.onClick.AddListener(
                HandleButtonClicked
            );
        }

        Transform oldRuntimeIcon = transform.Find("BB_Icon21B");
        if (oldRuntimeIcon != null)
        {
            Destroy(oldRuntimeIcon.gameObject);
        }

        SetSelected(
            selected
        );
    }

    public void SetSelected(
        bool selected
    )
    {
        IsSelected = selected;

        BistroBuilderInteractionSurface feedback =
            GetComponent<BistroBuilderInteractionSurface>();
        if (feedback != null)
        {
            feedback.enabled = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color =
                selected
                    ? selectedBackground
                    : normalBackground;
        }

        if (labelText != null)
        {
            labelText.color =
                selected
                    ? selectedText
                    : normalText;
        }
    }

    public void SetInteractable(
        bool interactable
    )
    {
        if (button != null)
        {
            button.interactable =
                interactable;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(
                HandleButtonClicked
            );
        }
    }

    private void HandleButtonClicked()
    {
        selectionCallback?.Invoke(
            categoryCode
        );
    }
}
