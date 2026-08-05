using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Modal runtime de creación y edición de platos y recetas 2.1G1/2.
/// Trabaja únicamente contra el borrador de
/// BistroBuilderDishRecipeAuthoringService.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderDishRecipeAuthoringRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1G12-UI";

    [SerializeField]
    private BistroBuilderMenuEditorService editorService;

    private readonly List<BistroBuilderDishCategoryDefinition> categories =
        new List<BistroBuilderDishCategoryDefinition>(16);

    private readonly List<BistroBuilderIngredientOptionSnapshot>
        ingredientOptions =
            new List<BistroBuilderIngredientOptionSnapshot>(32);

    private readonly Dictionary<string, BistroBuilderIngredientOptionSnapshot>
        ingredientById =
            new Dictionary<string, BistroBuilderIngredientOptionSnapshot>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderRecipeIngredientDraftRowView>
        ingredientRows =
            new List<BistroBuilderRecipeIngredientDraftRowView>(24);

    private RectTransform modalRoot;
    private RectTransform ingredientContent;
    private RectTransform pickerRoot;
    private RectTransform pickerContent;
    private Text titleText;
    private Text identityText;
    private Text statusText;
    private InputField nameInput;
    private InputField descriptionInput;
    private InputField priceInput;
    private InputField difficultyInput;
    private InputField timeInput;
    private InputField yieldInput;
    private InputField wasteInput;
    private InputField notesInput;
    private Button categoryButton;
    private Button courseButton;
    private Button stationButton;
    private Toggle breakfastToggle;
    private Toggle lunchToggle;
    private Toggle dinnerToggle;
    private Toggle tableToggle;
    private Toggle barToggle;
    private Toggle waitingBarToggle;
    private Button saveButton;

    private BistroBuilderDishRecipeAuthoringRequest workingRequest;
    private BistroBuilderRecipeIngredientDraftRowView pickerTarget;
    private int categoryIndex;
    private int courseIndex;
    private int stationIndex;
    private bool visualTreeBuilt;
    private bool open;

    public event Action<string> Saved;

    public BistroBuilderMenuEditorService EditorService => editorService;

    public bool IsOpen => open;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVisualTree();
        SetVisible(false);
    }

    private void Update()
    {
        if (!open)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (pickerRoot != null && pickerRoot.gameObject.activeSelf)
        {
            CloseIngredientPicker();
        }
        else
        {
            Close();
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (editorService == null)
        {
            error = "Falta BistroBuilderMenuEditorService.";
            return false;
        }

        if (editorService.AuthoringService == null)
        {
            error = "El editor no tiene autoría de platos y recetas 2.1G.";
            return false;
        }

        if (GetComponentInParent<Canvas>() == null)
        {
            error = "La vista de autoría 2.1G debe estar bajo un Canvas.";
            return false;
        }

        if (Application.isPlaying)
        {
            EnsureVisualTree();

            if (modalRoot == null || ingredientContent == null ||
                pickerRoot == null)
            {
                error = "La interfaz de autoría 2.1G no está construida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public void OpenNew()
    {
        if (!PrepareOpen(out string error))
        {
            ShowStatus(error, true);
            return;
        }

        workingRequest = editorService.CreateNewDishAuthoringRequest();
        PopulateFromRequest(workingRequest);
        titleText.text = "Nuevo plato y receta";
        identityText.text =
            "El DishId se generará al guardar a partir del nombre.";
        SetVisible(true);
    }

    public void OpenExisting(string dishId)
    {
        if (!PrepareOpen(out string error))
        {
            ShowStatus(error, true);
            return;
        }

        if (!editorService.TryGetDishAuthoringRequest(
                dishId,
                out workingRequest,
                out error
            ))
        {
            ShowStatus(error, true);
            return;
        }

        PopulateFromRequest(workingRequest);
        titleText.text = "Editar plato y receta";
        identityText.text = "DishId: " + workingRequest.DishId;
        SetVisible(true);
    }

    public void Close()
    {
        CloseIngredientPicker();
        SetVisible(false);
        workingRequest = null;
        ClearIngredientRows();
    }

    private bool PrepareOpen(out string error)
    {
        ResolveDependencies();
        EnsureVisualTree();

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!editorService.IsOpen)
        {
            error = "Abre primero el editor de carta.";
            return false;
        }

        ReloadOptions();
        error = string.Empty;
        return true;
    }

    private void EnsureVisualTree()
    {
        if (visualTreeBuilt)
        {
            return;
        }

        RectTransform host = transform as RectTransform;

        if (host == null)
        {
            return;
        }

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DishRecipeAuthoringModal",
            host,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            modalRoot,
            new Color(0.01f, 0.015f, 0.013f, 0.92f)
        );

        RectTransform card = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Card",
            modalRoot,
            new Vector2(0.07f, 0.055f),
            new Vector2(0.93f, 0.945f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            card,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        BuildHeader(card);
        BuildBody(card);
        BuildFooter(card);
        BuildIngredientPicker(modalRoot);
        visualTreeBuilt = true;
    }

    private void BuildHeader(RectTransform card)
    {
        RectTransform header = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Header",
            card,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(14f, -66f),
            new Vector2(-14f, -10f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            header,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        titleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            header,
            "Plato y receta",
            23,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        titleText.rectTransform.anchorMin = new Vector2(0f, 0.42f);
        titleText.rectTransform.anchorMax = new Vector2(0.76f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(16f, 0f);
        titleText.rectTransform.offsetMax = Vector2.zero;

        identityText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Identity",
            header,
            string.Empty,
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        identityText.rectTransform.anchorMin = Vector2.zero;
        identityText.rectTransform.anchorMax = new Vector2(0.78f, 0.44f);
        identityText.rectTransform.offsetMin = new Vector2(16f, 2f);
        identityText.rectTransform.offsetMax = Vector2.zero;

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            header,
            "Cancelar",
            Close,
            new Color(0.30f, 0.17f, 0.15f, 1f),
            14
        );
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.81f, 0.16f);
        closeRect.anchorMax = new Vector2(0.985f, 0.84f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;
    }

    private void BuildBody(RectTransform card)
    {
        RectTransform body = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Body",
            card,
            Vector2.zero,
            Vector2.one,
            new Vector2(14f, 72f),
            new Vector2(-14f, -76f)
        );

        RectTransform left = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DishColumn",
            body,
            Vector2.zero,
            new Vector2(0.43f, 1f),
            Vector2.zero,
            new Vector2(-6f, 0f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            left,
            new Color(0.055f, 0.062f, 0.058f, 1f)
        );

        RectTransform right = BistroBuilderMenuEditorUiFactory.CreateRect(
            "RecipeColumn",
            body,
            new Vector2(0.43f, 0f),
            Vector2.one,
            new Vector2(6f, 0f),
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            right,
            new Color(0.055f, 0.062f, 0.058f, 1f)
        );

        BuildDishColumn(left);
        BuildRecipeColumn(right);
    }

    private void BuildDishColumn(RectTransform parent)
    {
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "DishScroll",
            parent,
            out RectTransform content
        );
        RectTransform rect = scroll.GetComponent<RectTransform>();
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);

        AddSectionTitle(content, "DATOS DEL PLATO");
        nameInput = AddLabeledInput(content, "Nombre", "Nombre del plato", 40f);
        descriptionInput = AddLabeledInput(
            content,
            "Descripción",
            "Descripción visible",
            82f,
            true
        );

        AddLabel(content, "Categoría");
        categoryButton = AddCycleButton(
            content,
            "Category",
            "Categoría",
            CycleCategory
        );

        AddLabel(content, "Pase gastronómico");
        courseButton = AddCycleButton(
            content,
            "Course",
            "Pase",
            CycleCourse
        );

        AddLabel(content, "Estación de cocina");
        stationButton = AddCycleButton(
            content,
            "Station",
            "Estación",
            CycleStation
        );

        priceInput = AddLabeledInput(
            content,
            "Precio base",
            "0,00",
            40f
        );
        difficultyInput = AddLabeledInput(
            content,
            "Dificultad (1-10)",
            "1-10",
            40f
        );
        timeInput = AddLabeledInput(
            content,
            "Tiempo de preparación",
            "mm:ss",
            40f
        );

        AddSectionTitle(content, "SERVICIOS DEL DÍA");
        breakfastToggle = AddToggle(content, "Desayuno");
        lunchToggle = AddToggle(content, "Comida");
        dinnerToggle = AddToggle(content, "Cena");

        AddSectionTitle(content, "MODALIDADES");
        tableToggle = AddToggle(content, "Servicio en mesa");
        barToggle = AddToggle(content, "Servicio completo en barra");
        waitingBarToggle = AddToggle(content, "Barra mientras espera mesa");
    }

    private void BuildRecipeColumn(RectTransform parent)
    {
        Text heading = BistroBuilderMenuEditorUiFactory.CreateText(
            "Heading",
            parent,
            "RECETA Y ESCANDALLO",
            14,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        heading.rectTransform.anchorMin = new Vector2(0f, 1f);
        heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.offsetMin = new Vector2(12f, -38f);
        heading.rectTransform.offsetMax = new Vector2(-12f, -8f);

        RectTransform meta = BistroBuilderMenuEditorUiFactory.CreateRect(
            "RecipeMeta",
            parent,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(10f, -102f),
            new Vector2(-10f, -42f)
        );
        HorizontalLayoutGroup metaLayout =
            meta.gameObject.AddComponent<HorizontalLayoutGroup>();
        metaLayout.spacing = 8f;
        metaLayout.childControlHeight = true;
        metaLayout.childControlWidth = true;
        metaLayout.childForceExpandHeight = true;
        metaLayout.childForceExpandWidth = true;

        yieldInput = CreateCompactLabeledInput(
            meta,
            "Rendimiento",
            "Raciones"
        );
        wasteInput = CreateCompactLabeledInput(
            meta,
            "Merma (%)",
            "0-100"
        );

        ScrollRect ingredients =
            BistroBuilderMenuEditorUiFactory.CreateScrollView(
                "Ingredients",
                parent,
                out ingredientContent
            );
        RectTransform ingredientsRect =
            ingredients.GetComponent<RectTransform>();
        ingredientsRect.anchorMin = new Vector2(0f, 0.25f);
        ingredientsRect.anchorMax = new Vector2(1f, 1f);
        ingredientsRect.offsetMin = new Vector2(10f, 8f);
        ingredientsRect.offsetMax = new Vector2(-10f, -110f);

        Button addIngredient =
            BistroBuilderMenuEditorUiFactory.CreateButton(
                "AddIngredient",
                parent,
                "+ Añadir ingrediente",
                AddIngredientRow,
                BistroBuilderMenuEditorUiFactory.Positive,
                14
            );
        RectTransform addRect = addIngredient.GetComponent<RectTransform>();
        addRect.anchorMin = new Vector2(0f, 0.18f);
        addRect.anchorMax = new Vector2(0.44f, 0.245f);
        addRect.offsetMin = new Vector2(10f, 0f);
        addRect.offsetMax = new Vector2(-4f, 0f);

        Text notesLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "NotesLabel",
            parent,
            "Notas de receta",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        notesLabel.rectTransform.anchorMin = new Vector2(0.46f, 0.18f);
        notesLabel.rectTransform.anchorMax = new Vector2(1f, 0.245f);
        notesLabel.rectTransform.offsetMin = new Vector2(4f, 0f);
        notesLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

        notesInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Notes",
            parent,
            "Notas internas",
            null,
            null
        );
        ConfigureMultiline(notesInput);
        RectTransform notesRect = notesInput.GetComponent<RectTransform>();
        notesRect.anchorMin = new Vector2(0.46f, 0.02f);
        notesRect.anchorMax = new Vector2(1f, 0.18f);
        notesRect.offsetMin = new Vector2(4f, 0f);
        notesRect.offsetMax = new Vector2(-10f, -4f);
    }

    private void BuildFooter(RectTransform card)
    {
        RectTransform footer = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Footer",
            card,
            Vector2.zero,
            new Vector2(1f, 0f),
            new Vector2(14f, 10f),
            new Vector2(-14f, 62f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            footer,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            footer,
            "Los cambios se añadirán al borrador de la carta.",
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        statusText.rectTransform.anchorMin = Vector2.zero;
        statusText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(14f, 0f);
        statusText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        saveButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "SaveDraft",
            footer,
            "Guardar en borrador",
            Save,
            BistroBuilderMenuEditorUiFactory.Positive,
            14
        );
        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(0.74f, 0.14f);
        saveRect.anchorMax = new Vector2(0.985f, 0.86f);
        saveRect.offsetMin = Vector2.zero;
        saveRect.offsetMax = Vector2.zero;
    }

    private void BuildIngredientPicker(RectTransform parent)
    {
        pickerRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "IngredientPicker",
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            pickerRoot,
            new Color(0.01f, 0.01f, 0.01f, 0.86f)
        );

        RectTransform card = BistroBuilderMenuEditorUiFactory.CreateRect(
            "PickerCard",
            pickerRoot,
            new Vector2(0.31f, 0.12f),
            new Vector2(0.69f, 0.88f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            card,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            card,
            "Seleccionar ingrediente",
            20,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(14f, -52f);
        title.rectTransform.offsetMax = new Vector2(-100f, -8f);

        Button close = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            card,
            "Cerrar",
            CloseIngredientPicker,
            new Color(0.30f, 0.17f, 0.15f, 1f),
            13
        );
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.76f, 1f);
        closeRect.anchorMax = new Vector2(0.97f, 1f);
        closeRect.pivot = new Vector2(0.5f, 1f);
        closeRect.anchoredPosition = new Vector2(0f, -10f);
        closeRect.sizeDelta = new Vector2(0f, 38f);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "IngredientOptions",
            card,
            out pickerContent
        );
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.offsetMin = new Vector2(10f, 10f);
        scrollRect.offsetMax = new Vector2(-10f, -60f);
        pickerRoot.gameObject.SetActive(false);
    }

    private void ReloadOptions()
    {
        categories.Clear();
        editorService.CategoryCatalogService.CopyDefinitionsTo(categories);
        categories.RemoveAll(category => category == null || !category.Visible);
        categories.Sort((left, right) =>
            left.DisplayOrder.CompareTo(right.DisplayOrder));

        ingredientOptions.Clear();
        ingredientById.Clear();
        editorService.CopyIngredientOptionsTo(ingredientOptions);

        for (int index = 0; index < ingredientOptions.Count; index++)
        {
            BistroBuilderIngredientOptionSnapshot option =
                ingredientOptions[index];
            ingredientById[option.IngredientId] = option;
        }

        RebuildIngredientPicker();
    }

    private void PopulateFromRequest(
        BistroBuilderDishRecipeAuthoringRequest request
    )
    {
        nameInput.SetTextWithoutNotify(request.DisplayName);
        descriptionInput.SetTextWithoutNotify(request.Description);
        priceInput.SetTextWithoutNotify(
            BistroBuilderMenuEditorUtility.FormatEditableMoney(
                request.BasePriceCents
            )
        );
        difficultyInput.SetTextWithoutNotify(
            request.PreparationDifficulty.ToString()
        );
        timeInput.SetTextWithoutNotify(
            BistroBuilderMenuEditorUtility.FormatPreparationDuration(
                request.BasePreparationSeconds
            )
        );
        yieldInput.SetTextWithoutNotify(request.YieldPortions.ToString());
        wasteInput.SetTextWithoutNotify(
            (request.WasteBasisPoints / 100d).ToString(
                "0.##",
                CultureInfo.GetCultureInfo("es-ES")
            )
        );
        notesInput.SetTextWithoutNotify(request.Notes);

        categoryIndex = FindCategoryIndex(request.CategoryId);
        courseIndex = FindEnumIndex(request.Course);
        stationIndex = FindEnumIndex(request.RequiredStation);
        RefreshCycleLabels();

        breakfastToggle.SetIsOnWithoutNotify(
            (request.DefaultAvailability &
             BistroBuilderMealServiceAvailability.Breakfast) != 0
        );
        lunchToggle.SetIsOnWithoutNotify(
            (request.DefaultAvailability &
             BistroBuilderMealServiceAvailability.Lunch) != 0
        );
        dinnerToggle.SetIsOnWithoutNotify(
            (request.DefaultAvailability &
             BistroBuilderMealServiceAvailability.Dinner) != 0
        );
        tableToggle.SetIsOnWithoutNotify(
            (request.AllowedServiceModes &
             BistroBuilderDishServiceModeAvailability.TableService) != 0
        );
        barToggle.SetIsOnWithoutNotify(
            (request.AllowedServiceModes &
             BistroBuilderDishServiceModeAvailability.BarService) != 0
        );
        waitingBarToggle.SetIsOnWithoutNotify(
            (request.AllowedServiceModes &
             BistroBuilderDishServiceModeAvailability.WaitingAtBar) != 0
        );

        ClearIngredientRows();

        for (int index = 0; index < request.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientDraft line =
                request.Ingredients[index];

            if (line == null ||
                !ingredientById.TryGetValue(
                    line.IngredientId,
                    out BistroBuilderIngredientOptionSnapshot option
                ))
            {
                continue;
            }

            CreateIngredientRow(option, line.Amount, line.Unit);
        }

        if (ingredientRows.Count == 0)
        {
            AddIngredientRow();
        }

        ShowStatus(
            "Los cambios se añadirán al borrador de la carta.",
            false
        );
    }

    private void Save()
    {
        if (workingRequest == null)
        {
            ShowStatus("No existe un formulario activo.", true);
            return;
        }

        if (!TryCollectRequest(out string error))
        {
            ShowStatus(error, true);
            return;
        }

        saveButton.interactable = false;
        BistroBuilderDishRecipeAuthoringResult result =
            editorService.TryCreateOrUpdateDishRecipe(workingRequest);
        saveButton.interactable = true;

        if (!result.Succeeded)
        {
            ShowStatus(result.Message, true);
            return;
        }

        string savedDishId = result.DishId;
        ShowStatus(result.Message, false);
        Saved?.Invoke(savedDishId);
        Close();
    }

    private bool TryCollectRequest(out string error)
    {
        workingRequest.DisplayName = nameInput.text != null
            ? nameInput.text.Trim()
            : string.Empty;
        workingRequest.Description = descriptionInput.text != null
            ? descriptionInput.text.Trim()
            : string.Empty;

        if (categories.Count == 0)
        {
            error = "No hay categorías disponibles.";
            return false;
        }

        workingRequest.CategoryId = categories[categoryIndex].CategoryId;
        workingRequest.Course = GetEnumValue<BistroBuilderDishCourse>(
            courseIndex
        );
        workingRequest.RequiredStation =
            GetEnumValue<BistroBuilderKitchenStationType>(stationIndex);

        if (!BistroBuilderMenuEditorUtility.TryParseMoney(
                priceInput.text,
                out int priceCents,
                out error
            ) ||
            !BistroBuilderMenuEditorUtility.TryParsePreparationDifficulty(
                difficultyInput.text,
                out int difficulty,
                out error
            ) ||
            !BistroBuilderMenuEditorUtility.TryParsePreparationDuration(
                timeInput.text,
                out int preparationSeconds,
                out error
            ))
        {
            return false;
        }

        if (!int.TryParse(
                yieldInput.text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int yieldPortions
            ) ||
            yieldPortions < 1 ||
            yieldPortions > BistroBuilderRecipeDefinition.MaximumYieldPortions)
        {
            error = "El rendimiento debe ser un entero entre 1 y " +
                    BistroBuilderRecipeDefinition.MaximumYieldPortions + ".";
            return false;
        }

        string wasteRaw = wasteInput.text != null
            ? wasteInput.text.Trim().Replace(',', '.')
            : string.Empty;

        if (!double.TryParse(
                wasteRaw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double wastePercent
            ) ||
            double.IsNaN(wastePercent) ||
            double.IsInfinity(wastePercent) ||
            wastePercent < 0d || wastePercent > 100d)
        {
            error = "La merma debe estar entre 0 y 100 %.";
            return false;
        }

        BistroBuilderMealServiceAvailability services =
            BistroBuilderMealServiceAvailability.None;
        services |= breakfastToggle.isOn
            ? BistroBuilderMealServiceAvailability.Breakfast
            : BistroBuilderMealServiceAvailability.None;
        services |= lunchToggle.isOn
            ? BistroBuilderMealServiceAvailability.Lunch
            : BistroBuilderMealServiceAvailability.None;
        services |= dinnerToggle.isOn
            ? BistroBuilderMealServiceAvailability.Dinner
            : BistroBuilderMealServiceAvailability.None;

        BistroBuilderDishServiceModeAvailability modes =
            BistroBuilderDishServiceModeAvailability.None;
        modes |= tableToggle.isOn
            ? BistroBuilderDishServiceModeAvailability.TableService
            : BistroBuilderDishServiceModeAvailability.None;
        modes |= barToggle.isOn
            ? BistroBuilderDishServiceModeAvailability.BarService
            : BistroBuilderDishServiceModeAvailability.None;
        modes |= waitingBarToggle.isOn
            ? BistroBuilderDishServiceModeAvailability.WaitingAtBar
            : BistroBuilderDishServiceModeAvailability.None;

        if (services == BistroBuilderMealServiceAvailability.None)
        {
            error = "Selecciona al menos un servicio del día.";
            return false;
        }

        if (modes == BistroBuilderDishServiceModeAvailability.None)
        {
            error = "Selecciona al menos una modalidad de servicio.";
            return false;
        }

        workingRequest.BasePriceCents = priceCents;
        workingRequest.PreparationDifficulty = difficulty;
        workingRequest.BasePreparationSeconds = preparationSeconds;
        workingRequest.YieldPortions = yieldPortions;
        workingRequest.WasteBasisPoints = (int)Math.Round(
            wastePercent * 100d,
            MidpointRounding.AwayFromZero
        );
        workingRequest.DefaultAvailability = services;
        workingRequest.AllowedServiceModes = modes;
        workingRequest.Notes = notesInput.text != null
            ? notesInput.text.Trim()
            : string.Empty;
        workingRequest.Ingredients.Clear();

        if (ingredientRows.Count == 0)
        {
            error = "La receta necesita al menos un ingrediente.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            if (!ingredientRows[index].TryBuildDraft(
                    out BistroBuilderRecipeIngredientDraft line,
                    out error
                ))
            {
                return false;
            }

            if (!ids.Add(line.IngredientId))
            {
                error = "El ingrediente está repetido: " +
                        ingredientById[line.IngredientId].DisplayName + ".";
                return false;
            }

            workingRequest.Ingredients.Add(line);
        }

        error = string.Empty;
        return true;
    }

    private void AddIngredientRow()
    {
        if (ingredientOptions.Count == 0)
        {
            ShowStatus("No hay ingredientes canónicos disponibles.", true);
            return;
        }

        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(ingredientRows[index].IngredientId))
            {
                used.Add(ingredientRows[index].IngredientId);
            }
        }

        BistroBuilderIngredientOptionSnapshot option =
            default(BistroBuilderIngredientOptionSnapshot);
        bool found = false;

        for (int index = 0; index < ingredientOptions.Count; index++)
        {
            if (!used.Contains(ingredientOptions[index].IngredientId))
            {
                option = ingredientOptions[index];
                found = true;
                break;
            }
        }

        if (!found)
        {
            ShowStatus(
                "La receta ya contiene todos los ingredientes disponibles.",
                true
            );
            return;
        }

        CreateIngredientRow(option, 1d, option.BaseUnit);
    }

    private void CreateIngredientRow(
        BistroBuilderIngredientOptionSnapshot option,
        double amount,
        BistroBuilderMeasurementUnit unit
    )
    {
        RectTransform row = BistroBuilderMenuEditorUiFactory.CreateRect(
            "IngredientRow",
            ingredientContent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            row,
            new Color(0.09f, 0.10f, 0.095f, 1f)
        );
        BistroBuilderRecipeIngredientDraftRowView view =
            row.gameObject.AddComponent<
                BistroBuilderRecipeIngredientDraftRowView
            >();
        view.Initialize(OpenIngredientPicker, RemoveIngredientRow);
        view.SetData(option, amount, unit);
        ingredientRows.Add(view);
    }

    private void RemoveIngredientRow(
        BistroBuilderRecipeIngredientDraftRowView row
    )
    {
        if (row == null)
        {
            return;
        }

        ingredientRows.Remove(row);
        Destroy(row.gameObject);
    }

    private void ClearIngredientRows()
    {
        for (int index = 0; index < ingredientRows.Count; index++)
        {
            if (ingredientRows[index] != null)
            {
                Destroy(ingredientRows[index].gameObject);
            }
        }

        ingredientRows.Clear();
    }

    private void OpenIngredientPicker(
        BistroBuilderRecipeIngredientDraftRowView row
    )
    {
        pickerTarget = row;
        pickerRoot.gameObject.SetActive(true);
        pickerRoot.SetAsLastSibling();
    }

    private void CloseIngredientPicker()
    {
        if (pickerRoot != null)
        {
            pickerRoot.gameObject.SetActive(false);
        }

        pickerTarget = null;
    }

    private void SelectIngredient(
        BistroBuilderIngredientOptionSnapshot option
    )
    {
        pickerTarget?.SetIngredient(option);
        CloseIngredientPicker();
    }

    private void RebuildIngredientPicker()
    {
        if (pickerContent == null)
        {
            return;
        }

        for (int index = pickerContent.childCount - 1; index >= 0; index--)
        {
            Destroy(pickerContent.GetChild(index).gameObject);
        }

        for (int index = 0; index < ingredientOptions.Count; index++)
        {
            BistroBuilderIngredientOptionSnapshot option =
                ingredientOptions[index];
            BistroBuilderIngredientOptionSnapshot captured = option;
            Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
                "Ingredient_" + option.IngredientId,
                pickerContent,
                option.DisplayName + " · " +
                BistroBuilderMeasurementUtility.GetSymbol(option.BaseUnit),
                () => SelectIngredient(captured),
                BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                13
            );
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 38f);
        }
    }

    private void CycleCategory()
    {
        if (categories.Count == 0)
        {
            return;
        }

        categoryIndex = (categoryIndex + 1) % categories.Count;
        RefreshCycleLabels();
    }

    private void CycleCourse()
    {
        int count = Enum.GetValues(typeof(BistroBuilderDishCourse)).Length;
        courseIndex = (courseIndex + 1) % count;
        RefreshCycleLabels();
    }

    private void CycleStation()
    {
        int count = Enum.GetValues(
            typeof(BistroBuilderKitchenStationType)
        ).Length;
        stationIndex = (stationIndex + 1) % count;
        RefreshCycleLabels();
    }

    private void RefreshCycleLabels()
    {
        SetButtonLabel(
            categoryButton,
            categories.Count > 0
                ? categories[Mathf.Clamp(
                    categoryIndex,
                    0,
                    categories.Count - 1
                )].DisplayName
                : "Sin categorías"
        );
        SetButtonLabel(
            courseButton,
            GetCourseLabel(
                GetEnumValue<BistroBuilderDishCourse>(courseIndex)
            )
        );
        SetButtonLabel(
            stationButton,
            GetStationLabel(
                GetEnumValue<BistroBuilderKitchenStationType>(stationIndex)
            )
        );
    }

    private int FindCategoryIndex(string categoryId)
    {
        for (int index = 0; index < categories.Count; index++)
        {
            if (string.Equals(
                    categories[index].CategoryId,
                    categoryId,
                    StringComparison.Ordinal
                ))
            {
                return index;
            }
        }

        return 0;
    }

    private static int FindEnumIndex<T>(T value) where T : struct
    {
        Array values = Enum.GetValues(typeof(T));

        for (int index = 0; index < values.Length; index++)
        {
            if (EqualityComparer<T>.Default.Equals((T)values.GetValue(index), value))
            {
                return index;
            }
        }

        return 0;
    }

    private static T GetEnumValue<T>(int index) where T : struct
    {
        Array values = Enum.GetValues(typeof(T));

        if (values.Length == 0)
        {
            return default(T);
        }

        int safe = Mathf.Clamp(index, 0, values.Length - 1);
        return (T)values.GetValue(safe);
    }

    private void SetVisible(bool visible)
    {
        open = visible;

        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);

            if (visible)
            {
                modalRoot.SetAsLastSibling();
            }
        }
    }

    private void ShowStatus(string message, bool warning)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message ?? string.Empty;
        statusText.color = warning
            ? BistroBuilderMenuEditorUiFactory.Warning
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private static void ConfigureMultiline(InputField input)
    {
        input.lineType = InputField.LineType.MultiLineNewline;
        input.textComponent.alignment = TextAnchor.UpperLeft;
        input.textComponent.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static Text AddSectionTitle(Transform parent, string value)
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            "Section",
            parent,
            value,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.Accent,
            FontStyle.Bold
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(text, 28f);
        return text;
    }

    private static Text AddLabel(Transform parent, string value)
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            "Label",
            parent,
            value,
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(text, 22f);
        return text;
    }

    private static InputField AddLabeledInput(
        Transform parent,
        string label,
        string placeholder,
        float height,
        bool multiline = false
    )
    {
        AddLabel(parent, label);
        InputField input = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Input_" + label,
            parent,
            placeholder,
            null,
            null
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(input, height);

        if (multiline)
        {
            ConfigureMultiline(input);
        }

        return input;
    }

    private static Button AddCycleButton(
        Transform parent,
        string name,
        string label,
        UnityEngine.Events.UnityAction callback
    )
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name,
            parent,
            label,
            callback,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 38f);
        return button;
    }

    private static Toggle AddToggle(Transform parent, string label)
    {
        Toggle toggle = BistroBuilderMenuEditorUiFactory.CreateToggle(
            "Toggle_" + label,
            parent,
            label,
            null
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(toggle, 30f);
        return toggle;
    }

    private static InputField CreateCompactLabeledInput(
        Transform parent,
        string label,
        string placeholder
    )
    {
        RectTransform root = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Field_" + label,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        VerticalLayoutGroup layout =
            root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        AddLabel(root, label);
        InputField input = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Input",
            root,
            placeholder,
            null,
            null
        );
        return input;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>() : null;

        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static string GetCourseLabel(BistroBuilderDishCourse course)
    {
        switch (course)
        {
            case BistroBuilderDishCourse.Welcome:
                return "Bienvenida";
            case BistroBuilderDishCourse.Starter:
                return "Entrante";
            case BistroBuilderDishCourse.Main:
                return "Principal";
            case BistroBuilderDishCourse.Dessert:
                return "Postre";
            case BistroBuilderDishCourse.Beverage:
                return "Bebida";
            default:
                return "Sin pase";
        }
    }

    private static string GetStationLabel(
        BistroBuilderKitchenStationType station
    )
    {
        switch (station)
        {
            case BistroBuilderKitchenStationType.ColdPreparation:
                return "Preparación fría";
            case BistroBuilderKitchenStationType.HotKitchen:
                return "Cocina caliente";
            case BistroBuilderKitchenStationType.Grill:
                return "Parrilla";
            case BistroBuilderKitchenStationType.Fryer:
                return "Freidora";
            case BistroBuilderKitchenStationType.Oven:
                return "Horno";
            case BistroBuilderKitchenStationType.Pastry:
                return "Pastelería";
            case BistroBuilderKitchenStationType.Bar:
                return "Barra";
            default:
                return "Sin estación";
        }
    }

    private void ResolveDependencies()
    {
        if (editorService == null)
        {
            TryGetComponent(out editorService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
