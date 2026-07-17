using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista reutilizable de un artículo dentro del catálogo.
///
/// Solo presenta datos y transmite la pulsación al controlador del
/// panel. No conoce servicios de creación ni reglas del juego.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogItemView :
    MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private Text iconFallbackText;

    [SerializeField]
    private Text nameText;

    [SerializeField]
    private Text descriptionText;

    [SerializeField]
    private Text priceText;

    private RestaurantPlaceableItemDefinition definition;

    private Action<RestaurantPlaceableItemDefinition>
        selectionCallback;

    public RestaurantPlaceableItemDefinition Definition
    {
        get
        {
            return definition;
        }
    }

    public void Bind(
        RestaurantPlaceableItemDefinition itemDefinition,
        Action<RestaurantPlaceableItemDefinition> onSelected
    )
    {
        definition =
            itemDefinition;

        selectionCallback =
            onSelected;

        if (button != null)
        {
            button.onClick.RemoveListener(
                HandleButtonClicked
            );

            button.onClick.AddListener(
                HandleButtonClicked
            );
        }

        RefreshPresentation();
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
        if (definition == null)
        {
            return;
        }

        selectionCallback?.Invoke(
            definition
        );
    }

    private void RefreshPresentation()
    {
        string displayName =
            definition != null
                ? definition.DisplayName
                : "Artículo";

        if (nameText != null)
        {
            nameText.text =
                displayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text =
                definition != null &&
                !string.IsNullOrWhiteSpace(
                    definition.Description
                )
                    ? definition.Description.Trim()
                    : "Sin descripción.";
        }

        if (priceText != null)
        {
            int price =
                definition != null
                    ? definition.PurchasePrice
                    : 0;

            priceText.text =
                price > 0
                    ? price.ToString("N0") + " €"
                    : "Disponible";
        }

        Sprite icon =
            definition != null
                ? definition.CatalogIcon
                : null;

        if (iconImage != null)
        {
            iconImage.sprite =
                icon;

            iconImage.enabled =
                icon != null;
        }

        if (iconFallbackText != null)
        {
            iconFallbackText.gameObject.SetActive(
                icon == null
            );

            iconFallbackText.text =
                string.IsNullOrWhiteSpace(displayName)
                    ? "?"
                    : displayName
                        .Substring(0, 1)
                        .ToUpperInvariant();
        }
    }
}
