using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila reutilizable de un plato en la lista 2.1E. Solo presenta una
/// fotografía inmutable y comunica la selección al panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMenuEditorDishRowView : MonoBehaviour
{
    private Button button;
    private Image background;
    private Text nameText;
    private Text categoryText;
    private Text priceText;
    private Text marginText;
    private Text servicesText;
    private Text stateText;
    private Text badgesText;
    private Action<string> selectionCallback;
    private string dishId = string.Empty;

    public string DishId => dishId;

    public void Initialize()
    {
        if (button != null)
        {
            return;
        }

        RectTransform root = transform as RectTransform;
        background = root.GetComponent<Image>();

        if (background == null)
        {
            background = BistroBuilderMenuEditorUiFactory.AddImage(
                root,
                BistroBuilderMenuEditorUiFactory.SurfaceRaised
            );
        }

        button = gameObject.GetComponent<Button>();

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        button.targetGraphic = background;
        button.onClick.AddListener(HandleClicked);
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(this, 74f);

        nameText = CreateColumnText("Name", 0f, 0.31f, 16, true);
        categoryText = CreateColumnText("Category", 0.31f, 0.46f, 13, false);
        priceText = CreateColumnText("Price", 0.46f, 0.57f, 14, true);
        marginText = CreateColumnText("Margin", 0.57f, 0.69f, 13, false);
        servicesText = CreateColumnText("Services", 0.69f, 0.82f, 12, false);
        stateText = CreateColumnText("State", 0.82f, 0.94f, 12, true);
        badgesText = CreateColumnText("Badges", 0.94f, 1f, 16, true);
    }

    public void Bind(
        BistroBuilderMenuEditorDishSnapshot snapshot,
        bool selected,
        Action<string> onSelected
    )
    {
        Initialize();
        selectionCallback = onSelected;
        dishId = snapshot != null ? snapshot.DishId : string.Empty;
        bool valid = snapshot != null;
        button.interactable = valid;

        if (!valid)
        {
            nameText.text = string.Empty;
            categoryText.text = string.Empty;
            priceText.text = string.Empty;
            marginText.text = string.Empty;
            servicesText.text = string.Empty;
            stateText.text = string.Empty;
            badgesText.text = string.Empty;
            return;
        }

        background.color = selected
            ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
            : snapshot.NeedsAttention
                ? Color.Lerp(
                    BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                    BistroBuilderMenuEditorUiFactory.Warning,
                    0.16f
                )
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised;

        nameText.text = snapshot.DisplayName;
        categoryText.text = snapshot.CategoryName;
        priceText.text = BistroBuilderMenuEditorUtility.FormatMoney(
            snapshot.CurrentPriceCents
        );
        marginText.text = snapshot.HasValidEconomics
            ? (snapshot.GrossMarginBasisPoints / 100f).ToString("0.0") + "%"
            : "—";
        marginText.color = ResolveMarginColor(snapshot);
        servicesText.text = BistroBuilderMenuEditorUtility.GetServicesLabel(
            snapshot.AvailableServices
        );
        stateText.text = ResolveStateLabel(snapshot);
        stateText.color = snapshot.IsOrderable
            ? BistroBuilderMenuEditorUiFactory.Positive
            : snapshot.Included
                ? BistroBuilderMenuEditorUiFactory.Warning
                : BistroBuilderMenuEditorUiFactory.TextSecondary;
        badgesText.text =
            (snapshot.SignatureDish ? "★" : string.Empty) +
            (snapshot.IsModified ? " •" : string.Empty);
        badgesText.color = snapshot.SignatureDish
            ? BistroBuilderMenuEditorUiFactory.Accent
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private Text CreateColumnText(
        string name,
        float minX,
        float maxX,
        int fontSize,
        bool bold
    )
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            name,
            transform,
            string.Empty,
            fontSize,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            bold ? FontStyle.Bold : FontStyle.Normal
        );
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.offsetMin = new Vector2(8f, 5f);
        rect.offsetMax = new Vector2(-6f, -5f);
        return text;
    }

    private void HandleClicked()
    {
        if (!string.IsNullOrEmpty(dishId))
        {
            selectionCallback?.Invoke(dishId);
        }
    }

    private static string ResolveStateLabel(
        BistroBuilderMenuEditorDishSnapshot snapshot
    )
    {
        if (!snapshot.Included)
        {
            return "Fuera de carta";
        }

        if (snapshot.IsOrderable)
        {
            return snapshot.IsLowStock
                ? "Últimas raciones"
                : "Disponible";
        }

        switch (snapshot.PrimaryRejectionReason)
        {
            case BistroBuilderMenuOfferRejectionReason.Locked:
                return "Bloqueado";
            case BistroBuilderMenuOfferRejectionReason.Disabled:
                return "Desactivado";
            case BistroBuilderMenuOfferRejectionReason.ManuallySoldOut:
                return "Agotado manual";
            case BistroBuilderMenuOfferRejectionReason
                .UnavailableForMealService:
                return "Fuera de servicio";
            case BistroBuilderMenuOfferRejectionReason
                .UnsupportedServiceMode:
                return "No compatible";
            case BistroBuilderMenuOfferRejectionReason.OutOfStock:
                return "Sin stock";
            case BistroBuilderMenuOfferRejectionReason.InvalidRecipe:
                return "Receta inválida";
            case BistroBuilderMenuOfferRejectionReason.InvalidPrice:
                return "Precio inválido";
            default:
                return "No disponible";
        }
    }

    private static Color ResolveMarginColor(
        BistroBuilderMenuEditorDishSnapshot snapshot
    )
    {
        if (!snapshot.HasValidEconomics)
        {
            return BistroBuilderMenuEditorUiFactory.TextSecondary;
        }

        switch (snapshot.MarginBand)
        {
            case BistroBuilderRecipeMarginBand.Loss:
                return BistroBuilderMenuEditorUiFactory.Negative;
            case BistroBuilderRecipeMarginBand.Low:
                return BistroBuilderMenuEditorUiFactory.Warning;
            case BistroBuilderRecipeMarginBand.Correct:
                return BistroBuilderMenuEditorUiFactory.TextPrimary;
            default:
                return BistroBuilderMenuEditorUiFactory.Positive;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }
}
