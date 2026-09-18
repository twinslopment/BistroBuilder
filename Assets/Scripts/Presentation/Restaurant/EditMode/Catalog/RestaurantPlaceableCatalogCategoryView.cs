using System;
using BistroBuilder.UI.Iconography;
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

        if (button != null)
        {
            BBIconographyRuntime.Decorate(button, ResolveIcon(newCategoryCode));
        }

        SetSelected(
            selected
        );
    }

    public void SetSelected(
        bool selected
    )
    {
        BistroBuilderInteractionSurface.Attach(button)?.SetSelected(selected);
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

    private static BBIconId ResolveIcon(int code)
    {
        if (code < 0) return BBIconId.NavEditMode;
        switch ((RestaurantPlaceableItemCategory)code)
        {
            case RestaurantPlaceableItemCategory.Furniture: return BBIconId.ObjectTable;
            case RestaurantPlaceableItemCategory.Seating: return BBIconId.ObjectChair;
            case RestaurantPlaceableItemCategory.Lighting: return BBIconId.ObjectLighting;
            case RestaurantPlaceableItemCategory.Decoration: return BBIconId.ObjectDecoration;
            case RestaurantPlaceableItemCategory.KitchenEquipment: return BBIconId.AreaKitchen;
            case RestaurantPlaceableItemCategory.ServiceEquipment: return BBIconId.ObjectEquipment;
            case RestaurantPlaceableItemCategory.Structural: return BBIconId.NavEditMode;
            default: return BBIconId.GeneralMore;
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
