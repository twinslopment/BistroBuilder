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
        new Color(0.15f, 0.17f, 0.16f, 1f);

    [SerializeField]
    private Color selectedBackground =
        new Color(0.31f, 0.43f, 0.35f, 1f);

    private int categoryCode;

    private Action<int> selectionCallback;

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

        SetSelected(
            selected
        );
    }

    public void SetSelected(
        bool selected
    )
    {
        if (backgroundImage != null)
        {
            backgroundImage.color =
                selected
                    ? selectedBackground
                    : normalBackground;
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
