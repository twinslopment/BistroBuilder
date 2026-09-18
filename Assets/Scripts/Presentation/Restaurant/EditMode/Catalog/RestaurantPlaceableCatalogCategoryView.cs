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
            BBIconographyRuntime.Decorate(button, IconForCategory(newCategoryCode));
            var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 158;
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 158);
            labelText.font = BistroBuilderTypography.LegacyBody;
            labelText.fontStyle = FontStyle.Normal;
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.offsetMin = new Vector2(40, 4);
            labelText.rectTransform.offsetMax = new Vector2(-10, -4);
            BistroBuilderSurface.Apply(backgroundImage, BistroBuilderSurfaceLevel.Card);
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
        BistroBuilderInteractionSurface.Attach(button)?.SetSelected(selected);
        if (backgroundImage != null)
        {
            backgroundImage.color =
                selected
                    ? selectedBackground
                    : normalBackground;
        }
    }

    public static BBIconId IconForCategory(int code) => code < 0 ? BBIconId.NavActivity : (RestaurantPlaceableItemCategory)code switch
    {
        RestaurantPlaceableItemCategory.Furniture => BBIconId.ObjectTable,
        RestaurantPlaceableItemCategory.Seating => BBIconId.ObjectChair,
        RestaurantPlaceableItemCategory.Lighting => BBIconId.ObjectLighting,
        RestaurantPlaceableItemCategory.Decoration => BBIconId.ObjectDecoration,
        RestaurantPlaceableItemCategory.KitchenEquipment => BBIconId.ObjectEquipment,
        RestaurantPlaceableItemCategory.ServiceEquipment => BBIconId.ObjectWaiter,
        RestaurantPlaceableItemCategory.Structural => BBIconId.NavEditMode,
        _ => BBIconId.NavInventory
    };

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
