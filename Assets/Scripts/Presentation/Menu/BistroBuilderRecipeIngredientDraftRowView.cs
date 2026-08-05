using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila reutilizable del editor runtime de recetas 2.1G2.
/// </summary>
public sealed class BistroBuilderRecipeIngredientDraftRowView : MonoBehaviour
{
    private Button ingredientButton;
    private InputField amountInput;
    private Button unitButton;
    private Button removeButton;
    private BistroBuilderIngredientOptionSnapshot ingredient;
    private BistroBuilderMeasurementUnit unit;
    private Action<BistroBuilderRecipeIngredientDraftRowView> selectRequested;
    private Action<BistroBuilderRecipeIngredientDraftRowView> removeRequested;

    public string IngredientId => ingredient.IngredientId;

    public BistroBuilderMeasurementUnit Unit => unit;

    public void Initialize(
        Action<BistroBuilderRecipeIngredientDraftRowView> onSelect,
        Action<BistroBuilderRecipeIngredientDraftRowView> onRemove
    )
    {
        selectRequested = onSelect;
        removeRequested = onRemove;

        RectTransform root = transform as RectTransform;
        HorizontalLayoutGroup layout =
            gameObject.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 3, 3);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        ingredientButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Ingredient",
            root,
            "Seleccionar ingrediente",
            () => selectRequested?.Invoke(this),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutWidth(
            ingredientButton,
            230f
        );

        amountInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Amount",
            root,
            "Cantidad",
            null,
            null
        );
        amountInput.contentType = InputField.ContentType.Standard;
        amountInput.characterValidation =
            InputField.CharacterValidation.None;
        BistroBuilderMenuEditorUiFactory.SetLayoutWidth(amountInput, 100f);

        unitButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Unit",
            root,
            "g",
            CycleUnit,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutWidth(unitButton, 80f);

        removeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Remove",
            root,
            "Quitar",
            () => removeRequested?.Invoke(this),
            new Color(0.34f, 0.18f, 0.16f, 1f),
            12
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutWidth(removeButton, 78f);
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(this, 44f);
    }

    public void SetData(
        BistroBuilderIngredientOptionSnapshot option,
        double amount,
        BistroBuilderMeasurementUnit selectedUnit
    )
    {
        ingredient = option;
        unit = BistroBuilderMeasurementUtility.AreCompatible(
            option.BaseUnit,
            selectedUnit
        )
            ? selectedUnit
            : option.BaseUnit;
        amountInput.SetTextWithoutNotify(
            amount.ToString(
                "0.###",
                CultureInfo.GetCultureInfo("es-ES")
            )
        );
        RefreshLabels();
    }

    public void SetIngredient(BistroBuilderIngredientOptionSnapshot option)
    {
        ingredient = option;
        unit = option.BaseUnit;
        RefreshLabels();
    }

    public bool TryBuildDraft(
        out BistroBuilderRecipeIngredientDraft draft,
        out string error
    )
    {
        draft = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ingredient.IngredientId))
        {
            error = "Selecciona un ingrediente en todas las líneas.";
            return false;
        }

        string normalized = amountInput.text != null
            ? amountInput.text.Trim().Replace(',', '.')
            : string.Empty;

        if (!double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double amount
            ) ||
            double.IsNaN(amount) ||
            double.IsInfinity(amount) ||
            amount <= 0d)
        {
            error = "La cantidad de " + ingredient.DisplayName +
                    " debe ser mayor que cero.";
            return false;
        }

        if (!BistroBuilderMeasurementUtility.TryConvertToCanonicalMilliUnits(
                amount,
                unit,
                out _,
                out error
            ))
        {
            return false;
        }

        draft = new BistroBuilderRecipeIngredientDraft(
            ingredient.IngredientId,
            amount,
            unit
        );
        return true;
    }

    private void CycleUnit()
    {
        if (!BistroBuilderMeasurementUtility.TryGetDimension(
                ingredient.BaseUnit,
                out BistroBuilderMeasurementDimension dimension
            ))
        {
            return;
        }

        switch (dimension)
        {
            case BistroBuilderMeasurementDimension.Mass:
                unit = unit == BistroBuilderMeasurementUnit.Gram
                    ? BistroBuilderMeasurementUnit.Kilogram
                    : BistroBuilderMeasurementUnit.Gram;
                break;

            case BistroBuilderMeasurementDimension.Volume:
                unit = unit == BistroBuilderMeasurementUnit.Milliliter
                    ? BistroBuilderMeasurementUnit.Liter
                    : BistroBuilderMeasurementUnit.Milliliter;
                break;

            case BistroBuilderMeasurementDimension.Count:
                unit = BistroBuilderMeasurementUnit.Unit;
                break;

            case BistroBuilderMeasurementDimension.Portion:
                unit = BistroBuilderMeasurementUnit.Portion;
                break;
        }

        RefreshLabels();
    }

    private void RefreshLabels()
    {
        SetButtonLabel(
            ingredientButton,
            string.IsNullOrWhiteSpace(ingredient.DisplayName)
                ? "Seleccionar ingrediente"
                : ingredient.DisplayName
        );
        SetButtonLabel(
            unitButton,
            BistroBuilderMeasurementUtility.GetSymbol(unit)
        );
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>();

        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }
}
