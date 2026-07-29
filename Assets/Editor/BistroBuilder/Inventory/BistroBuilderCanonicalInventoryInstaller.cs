using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador acumulativo, idempotente y con rollback de 368B.
///
/// Instala el perfil de apertura, el servicio de inventario canónico y
/// reagrupa reloj/pausa/velocidades en un dock compacto abajo a la derecha.
/// </summary>
public static class BistroBuilderCanonicalInventoryInstaller
{
    public const string OpeningStockProfilePath =
        "Assets/Data/BistroBuilder/Inventory/" +
        "BistroBuilderOpeningStockProfile.asset";

    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Install or Repair 368B2 Canonical Inventory, HUD & Chair Layout";

    private const string HudDockName = "BB_368B_TimeControlsDock";

    private static TMP_FontAsset cachedModernFontAsset;

    private readonly struct OpeningSeed
    {
        public readonly string IngredientId;
        public readonly double Amount;
        public readonly BistroBuilderMeasurementUnit Unit;

        public OpeningSeed(
            string ingredientId,
            double amount,
            BistroBuilderMeasurementUnit unit
        )
        {
            IngredientId = ingredientId;
            Amount = amount;
            Unit = unit;
        }
    }

    private static readonly OpeningSeed[] OpeningSeeds =
    {
        new OpeningSeed("ingredient_fabes", 5d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_chorizo", 3d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_morcilla", 3d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_panceta", 3d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_cebolla", 5d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_ajo", 1d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_aceite_oliva", 10d, BistroBuilderMeasurementUnit.Liter),
        new OpeningSeed("ingredient_sal", 5d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_agua_cocina", 100d, BistroBuilderMeasurementUnit.Liter),
        new OpeningSeed("ingredient_merluza", 10d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_limon", 3d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_queso_crema", 8d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_nata", 8d, BistroBuilderMeasurementUnit.Liter),
        new OpeningSeed("ingredient_huevo", 120d, BistroBuilderMeasurementUnit.Unit),
        new OpeningSeed("ingredient_azucar", 10d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_galleta", 5d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_mantequilla", 5d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_botella_agua_mineral", 120d, BistroBuilderMeasurementUnit.Unit),
        new OpeningSeed("ingredient_refresco_lata", 120d, BistroBuilderMeasurementUnit.Unit),
        new OpeningSeed("ingredient_vino_casa", 30d, BistroBuilderMeasurementUnit.Liter),
        new OpeningSeed("ingredient_aceitunas_alinadas", 8d, BistroBuilderMeasurementUnit.Kilogram),
        new OpeningSeed("ingredient_patata", 15d, BistroBuilderMeasurementUnit.Kilogram)
    };

    [MenuItem(MenuPath, false, 330)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 368B2.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes de " +
                "instalar 368B2.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador 368B.",
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();
        FileBackup backup = FileBackup.Capture(
            scene.path,
            OpeningStockProfilePath
        );

        try
        {
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );
            }

            BistroBuilderRecipeCatalogService recipeService =
                gameSystems.GetComponent<
                    BistroBuilderRecipeCatalogService
                >();

            if (recipeService == null)
            {
                throw new InvalidOperationException(
                    "368A no está correctamente instalado: falta " +
                    nameof(BistroBuilderRecipeCatalogService) + "."
                );
            }

            if (!recipeService.ValidateConfiguration(out string error))
            {
                throw new InvalidOperationException(
                    "368A no está correctamente instalado: " + error
                );
            }

            BistroBuilderOpeningStockProfile profile =
                EnsureOpeningStockProfileForCatalog(
                    recipeService.IngredientCatalog
                );
            ConfigureInventoryService(
                gameSystems,
                recipeService,
                profile
            );
            BistroBuilder368B1SceneLayoutRepair.Repair(scene);
            ConfigureModernHud(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 368B2."
                );
            }

            AssetDatabase.Refresh();

            BistroBuilderCanonicalInventoryValidationResult result =
                BistroBuilderCanonicalInventoryValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(result.BuildReport());

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Inventario canónico, HUD y distribución 368B2 instalados.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 368B.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                backup.Restore();
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(
                    scene.path,
                    OpenSceneMode.Single
                );
            }
            catch (Exception rollbackException)
            {
                Debug.LogException(rollbackException);
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 368B2 ha fallado y se ha restaurado el " +
                "estado anterior.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    public static BistroBuilderOpeningStockProfile
        EnsureOpeningStockProfileForCatalog(
            BistroBuilderIngredientCatalog ingredientCatalog
        )
    {
        if (ingredientCatalog == null)
        {
            throw new InvalidOperationException(
                "No existe un catálogo de ingredientes para preparar " +
                "el stock inicial."
            );
        }

        if (!ingredientCatalog.TryRebuildIndex(out string catalogError))
        {
            throw new InvalidOperationException(catalogError);
        }

        string folder = Path.GetDirectoryName(OpeningStockProfilePath)
            ?.Replace('\\', '/');
        BistroBuilderIngredientsRecipesEditorUtility.EnsureFolder(folder);

        BistroBuilderOpeningStockProfile profile =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderOpeningStockProfile
            >(OpeningStockProfilePath);

        if (profile == null)
        {
            if (File.Exists(Path.GetFullPath(OpeningStockProfilePath)))
            {
                throw new InvalidOperationException(
                    "Existe un asset incompatible en " +
                    OpeningStockProfilePath + "."
                );
            }

            profile = ScriptableObject.CreateInstance<
                BistroBuilderOpeningStockProfile
            >();
            AssetDatabase.CreateAsset(profile, OpeningStockProfilePath);
        }

        var existing = new Dictionary<
            string,
            BistroBuilderOpeningStockLine
        >(StringComparer.Ordinal);

        IReadOnlyList<BistroBuilderOpeningStockLine> currentLines =
            profile.Lines;

        if (currentLines != null)
        {
            for (int index = 0; index < currentLines.Count; index++)
            {
                BistroBuilderOpeningStockLine line = currentLines[index];

                if (line != null &&
                    line.Ingredient != null &&
                    line.TryValidate(out _))
                {
                    existing[line.Ingredient.IngredientId] = line;
                }
            }
        }

        Undo.RecordObject(profile, "Actualizar stock inicial 368B");
        SerializedObject serialized = new SerializedObject(profile);
        RequireProperty(serialized, "profileId").stringValue =
            "opening_stock_default";
        SerializedProperty lines = RequireProperty(serialized, "lines");
        IReadOnlyList<BistroBuilderIngredientDefinition> ingredients =
            ingredientCatalog.Definitions;
        lines.arraySize = ingredients.Count;

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientDefinition ingredient = ingredients[index];
            double amount;
            BistroBuilderMeasurementUnit unit;
            string location;

            if (existing.TryGetValue(
                    ingredient.IngredientId,
                    out BistroBuilderOpeningStockLine existingLine
                ))
            {
                amount = existingLine.Amount;
                unit = existingLine.Unit;
                location = existingLine.StorageLocationId;
            }
            else if (TryGetSeed(
                    ingredient.IngredientId,
                    out OpeningSeed seed
                ))
            {
                amount = seed.Amount;
                unit = seed.Unit;
                location = BistroBuilderInventoryStorageLocationIds
                    .FromIngredientStorage(ingredient.StorageType);
            }
            else
            {
                amount = ingredient.ReferencePackAmount;
                unit = ingredient.ReferencePackUnit;
                location = BistroBuilderInventoryStorageLocationIds
                    .FromIngredientStorage(ingredient.StorageType);
            }

            SerializedProperty line = lines.GetArrayElementAtIndex(index);
            RequireRelative(line, "ingredient").objectReferenceValue =
                ingredient;
            RequireRelative(line, "amount").doubleValue = amount;
            RequireRelative(line, "unit").enumValueIndex = (int)unit;
            RequireRelative(line, "storageLocationId").stringValue =
                location;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);

        if (!profile.TryValidate(out string error))
        {
            throw new InvalidOperationException(
                OpeningStockProfilePath + ": " + error
            );
        }

        return profile;
    }

    private static void ConfigureInventoryService(
        GameObject gameSystems,
        BistroBuilderRecipeCatalogService recipeService,
        BistroBuilderOpeningStockProfile profile
    )
    {
        BistroBuilderInventoryService[] existing =
            gameSystems.GetComponents<BistroBuilderInventoryService>();
        BistroBuilderInventoryService service;

        if (existing.Length == 0)
        {
            service = Undo.AddComponent<BistroBuilderInventoryService>(
                gameSystems
            );
        }
        else
        {
            service = existing[0];

            for (int index = 1; index < existing.Length; index++)
            {
                Undo.DestroyObjectImmediate(existing[index]);
            }
        }

        Undo.RecordObject(service, "Configurar inventario canónico 368B");
        service.enabled = true;
        SerializedObject serialized = new SerializedObject(service);
        RequireProperty(serialized, "recipeCatalogService")
            .objectReferenceValue = recipeService;
        RequireProperty(serialized, "openingStockProfile")
            .objectReferenceValue = profile;
        RequireProperty(serialized, "logInitialization").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(service);
    }

    private static void ConfigureModernHud(Scene scene)
    {
        ClockDisplay[] clocks = FindSceneObjects<ClockDisplay>(scene);
        PauseButtonController[] pauseButtons =
            FindSceneObjects<PauseButtonController>(scene);
        SpeedButtonController[] speedButtons =
            FindSceneObjects<SpeedButtonController>(scene);
        GameClock[] gameClocks = FindSceneObjects<GameClock>(scene);

        if (clocks.Length != 1 || pauseButtons.Length != 1 ||
            speedButtons.Length != 3 || gameClocks.Length != 1)
        {
            throw new InvalidOperationException(
                "El HUD provisional debe contener exactamente un reloj, " +
                "un GameClock, un botón de pausa y exactamente tres " +
                "velocidades."
            );
        }

        ClockDisplay clock = clocks[0];
        PauseButtonController pause = pauseButtons[0];
        GameClock gameClock = gameClocks[0];
        Undo.RecordObject(clock, "Activar reloj 368B");
        Undo.RecordObject(pause, "Activar pausa 368B");
        clock.enabled = true;
        pause.enabled = true;
        clock.gameObject.SetActive(true);
        pause.gameObject.SetActive(true);
        SetObjectReference(clock, "gameClock", gameClock);
        SetObjectReference(pause, "gameClock", gameClock);
        ConfigurePausePalette(pause);
        Array.Sort(
            speedButtons,
            (first, second) => first.SpeedMultiplier.CompareTo(
                second.SpeedMultiplier
            )
        );

        Canvas canvas = clock.GetComponentInParent<Canvas>(true);

        if (canvas == null ||
            pause.GetComponentInParent<Canvas>(true) != canvas)
        {
            throw new InvalidOperationException(
                "Los controles de tiempo no comparten un Canvas válido."
            );
        }

        for (int index = 0; index < speedButtons.Length; index++)
        {
            Undo.RecordObject(speedButtons[index], "Activar velocidad 368B");
            speedButtons[index].enabled = true;
            speedButtons[index].gameObject.SetActive(true);
            SetObjectReference(speedButtons[index], "gameClock", gameClock);
            ConfigureSpeedPalette(speedButtons[index]);

            if (speedButtons[index].GetComponentInParent<Canvas>(true) !=
                canvas)
            {
                throw new InvalidOperationException(
                    "Las velocidades no comparten el Canvas del reloj."
                );
            }
        }

        BistroBuilder368BInstalledHudDock[] existingMarkers =
            FindSceneObjects<BistroBuilder368BInstalledHudDock>(scene);
        BistroBuilder368BInstalledHudDock marker = null;

        for (int index = 0; index < existingMarkers.Length; index++)
        {
            if (marker == null)
            {
                marker = existingMarkers[index];
            }
            else
            {
                Undo.DestroyObjectImmediate(
                    existingMarkers[index].gameObject
                );
            }
        }

        GameObject dockObject;

        if (marker == null)
        {
            Transform existingTransform = canvas.transform.Find(HudDockName);

            if (existingTransform != null)
            {
                dockObject = existingTransform.gameObject;
                marker = dockObject.GetComponent<
                    BistroBuilder368BInstalledHudDock
                >();

                if (marker == null)
                {
                    marker = Undo.AddComponent<
                        BistroBuilder368BInstalledHudDock
                    >(dockObject);
                }
            }
            else
            {
                dockObject = new GameObject(
                    HudDockName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(HorizontalLayoutGroup),
                    typeof(BistroBuilder368BInstalledHudDock)
                );
                Undo.RegisterCreatedObjectUndo(
                    dockObject,
                    "Crear dock de tiempo 368B"
                );
                Undo.SetTransformParent(
                    dockObject.transform,
                    canvas.transform,
                    "Anclar dock de tiempo 368B"
                );
                marker = dockObject.GetComponent<
                    BistroBuilder368BInstalledHudDock
                >();
            }
        }
        else
        {
            dockObject = marker.gameObject;
        }

        dockObject.name = HudDockName;
        dockObject.SetActive(true);

        if (dockObject.transform.parent != canvas.transform)
        {
            Undo.SetTransformParent(
                dockObject.transform,
                canvas.transform,
                "Reanclar dock de tiempo 368B"
            );
        }

        RectTransform dockRect = dockObject.GetComponent<RectTransform>();
        dockRect.anchorMin = new Vector2(1f, 0f);
        dockRect.anchorMax = new Vector2(1f, 0f);
        dockRect.pivot = new Vector2(1f, 0f);
        dockRect.anchoredPosition = new Vector2(-18f, 18f);
        dockRect.sizeDelta = new Vector2(324f, 56f);
        dockRect.localScale = Vector3.one;
        dockRect.localRotation = Quaternion.identity;
        dockRect.SetAsLastSibling();

        Image dockImage = dockObject.GetComponent<Image>();
        dockImage.color = new Color32(28, 31, 35, 238);
        dockImage.raycastTarget = false;

        HorizontalLayoutGroup layout =
            dockObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.reverseArrangement = false;

        MoveAndStyleClock(clock, dockRect);
        MoveAndStyleButton(
            pause.gameObject,
            dockRect,
            80f,
            44f,
            12f
        );

        var selectedSpeeds = new List<SpeedButtonController>(3);

        for (int index = 0;
             index < speedButtons.Length && selectedSpeeds.Count < 3;
             index++)
        {
            float value = speedButtons[index].SpeedMultiplier;

            if (Mathf.Approximately(value, 1f) ||
                Mathf.Approximately(value, 2f) ||
                Mathf.Approximately(value, 3f))
            {
                selectedSpeeds.Add(speedButtons[index]);
            }
        }

        if (selectedSpeeds.Count != 3)
        {
            throw new InvalidOperationException(
                "No se localizaron las velocidades x1, x2 y x3."
            );
        }

        selectedSpeeds.Sort(
            (first, second) => first.SpeedMultiplier.CompareTo(
                second.SpeedMultiplier
            )
        );

        for (int index = 0; index < selectedSpeeds.Count; index++)
        {
            MoveAndStyleButton(
                selectedSpeeds[index].gameObject,
                dockRect,
                40f,
                44f,
                15f
            );
        }

        clock.transform.SetSiblingIndex(0);
        pause.transform.SetSiblingIndex(1);

        for (int index = 0; index < selectedSpeeds.Count; index++)
        {
            selectedSpeeds[index].transform.SetSiblingIndex(index + 2);
        }

        SerializedObject markerSerialized = new SerializedObject(marker);
        RequireProperty(markerSerialized, "installedRevision").stringValue =
            "368B";
        RequireProperty(markerSerialized, "dockRect")
            .objectReferenceValue = dockRect;
        RequireProperty(markerSerialized, "background")
            .objectReferenceValue = dockImage;
        RequireProperty(markerSerialized, "clockDisplay")
            .objectReferenceValue = clock;
        RequireProperty(markerSerialized, "clockText")
            .objectReferenceValue = clock.ClockText;
        RequireProperty(markerSerialized, "pauseButton")
            .objectReferenceValue = pause;
        SerializedProperty speedArray =
            RequireProperty(markerSerialized, "speedButtons");
        speedArray.arraySize = selectedSpeeds.Count;

        for (int index = 0; index < selectedSpeeds.Count; index++)
        {
            speedArray.GetArrayElementAtIndex(index).objectReferenceValue =
                selectedSpeeds[index];
        }

        markerSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(marker);
        EditorUtility.SetDirty(dockObject);
    }

    private static void MoveAndStyleClock(
        ClockDisplay clock,
        RectTransform parent
    )
    {
        Undo.SetTransformParent(
            clock.transform,
            parent,
            "Mover reloj al dock 368B"
        );
        RectTransform rect = clock.GetComponent<RectTransform>();

        if (rect == null)
        {
            throw new InvalidOperationException(
                "El reloj no tiene RectTransform."
            );
        }

        ResetChildRect(rect);
        LayoutElement layout = GetOrAddComponent<LayoutElement>(
            clock.gameObject
        );
        layout.minWidth = 80f;
        layout.preferredWidth = 80f;
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        TMP_Text text = clock.ClockText != null
            ? clock.ClockText
            : clock.GetComponent<TMP_Text>();

        if (text == null)
        {
            throw new InvalidOperationException(
                "El reloj no contiene TMP_Text."
            );
        }

        SetObjectReference(clock, "clockText", text);
        ApplyModernTypography(text, 25f, FontStyles.Bold);
        text.color = new Color32(244, 241, 231, 255);
        text.characterSpacing = 1.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }

    private static void MoveAndStyleButton(
        GameObject buttonObject,
        RectTransform parent,
        float width,
        float height,
        float fontSize
    )
    {
        Undo.SetTransformParent(
            buttonObject.transform,
            parent,
            "Mover control al dock 368B"
        );
        RectTransform rect = buttonObject.GetComponent<RectTransform>();

        if (rect == null)
        {
            throw new InvalidOperationException(
                buttonObject.name + " no tiene RectTransform."
            );
        }

        ResetChildRect(rect);
        LayoutElement layout = GetOrAddComponent<LayoutElement>(buttonObject);
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();

        if (button == null || image == null)
        {
            throw new InvalidOperationException(
                buttonObject.name + " no es un botón UI completo."
            );
        }

        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image;
        image.color = Color.white;
        image.raycastTarget = true;

        TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);

        if (text == null)
        {
            throw new InvalidOperationException(
                buttonObject.name + " no contiene TMP_Text."
            );
        }

        PauseButtonController pause =
            buttonObject.GetComponent<PauseButtonController>();
        SpeedButtonController speed =
            buttonObject.GetComponent<SpeedButtonController>();

        if (pause != null)
        {
            SetObjectReference(pause, "buttonText", text);
        }
        else if (speed != null)
        {
            SetObjectReference(speed, "buttonText", text);
        }

        ApplyModernTypography(text, fontSize, FontStyles.Bold);

        Color baseColor = new Color32(48, 53, 59, 255);
        Color textColor = new Color32(236, 238, 240, 255);

        if (pause != null)
        {
            text.text = "PAUSA";
        }
        else if (speed != null)
        {
            text.text = speed.SpeedMultiplier.ToString("0.#") + "×";

            if (Mathf.Approximately(speed.SpeedMultiplier, 1f))
            {
                baseColor = new Color32(72, 113, 94, 255);
                textColor = new Color32(247, 248, 246, 255);
            }
            else
            {
                textColor = new Color32(190, 195, 199, 255);
            }
        }

        ApplyButtonColors(button, baseColor);
        text.color = textColor;
        text.characterSpacing = 0.8f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(image);
    }

    private static void ApplyButtonColors(
        Button button,
        Color baseColor
    )
    {
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            0.45f
        );
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void ApplyModernTypography(
        TMP_Text text,
        float fontSize,
        FontStyles fontStyle
    )
    {
        TMP_FontAsset modernFont = ResolveModernFontAsset();

        if (modernFont != null)
        {
            text.font = modernFont;
        }

        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
#pragma warning disable 0618
        text.enableWordWrapping = false;
#pragma warning restore 0618
        // LiberationSans SDF no contiene necesariamente el glifo U+2026.
        // Truncate evita que TMP intente renderizar una elipsis inexistente y
        // mantiene el HUD silencioso incluso con fuentes fallback mínimas.
        text.overflowMode = TextOverflowModes.Truncate;
    }

    private static TMP_FontAsset ResolveModernFontAsset()
    {
        if (cachedModernFontAsset != null)
        {
            return cachedModernFontAsset;
        }

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int bestScore = int.MinValue;

        for (int index = 0; index < fontGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuids[index]);
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<
                TMP_FontAsset
            >(path);

            if (candidate == null)
            {
                continue;
            }

            string normalizedName = candidate.name
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            int score = 0;

            if (normalizedName.Contains("inter"))
            {
                score = 100;
            }
            else if (normalizedName.Contains("montserrat"))
            {
                score = 90;
            }
            else if (normalizedName.Contains("roboto"))
            {
                score = 80;
            }
            else if (normalizedName.Contains("notosans"))
            {
                score = 70;
            }
            else if (normalizedName.Contains("liberationsans"))
            {
                score = 60;
            }
            else if (normalizedName.Contains("sans"))
            {
                score = 20;
            }

            if (score > 0 && score > bestScore)
            {
                bestScore = score;
                cachedModernFontAsset = candidate;
            }
        }

        if (cachedModernFontAsset == null)
        {
            cachedModernFontAsset = TMP_Settings.defaultFontAsset;
        }

        return cachedModernFontAsset;
    }

    private static void ResetChildRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static bool TryGetSeed(
        string ingredientId,
        out OpeningSeed seed
    )
    {
        for (int index = 0; index < OpeningSeeds.Length; index++)
        {
            if (string.Equals(
                    OpeningSeeds[index].IngredientId,
                    ingredientId,
                    StringComparison.Ordinal
                ))
            {
                seed = OpeningSeeds[index];
                return true;
            }
        }

        seed = default;
        return false;
    }

    private static void ConfigurePausePalette(
        PauseButtonController controller
    )
    {
        SerializedObject serialized = new SerializedObject(controller);
        RequireProperty(serialized, "normalBackground").colorValue =
            new Color32(48, 53, 59, 255);
        RequireProperty(serialized, "pausedBackground").colorValue =
            new Color32(188, 139, 62, 255);
        RequireProperty(serialized, "normalText").colorValue =
            new Color32(236, 238, 240, 255);
        RequireProperty(serialized, "pausedText").colorValue =
            new Color32(25, 27, 30, 255);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureSpeedPalette(
        SpeedButtonController controller
    )
    {
        SerializedObject serialized = new SerializedObject(controller);
        RequireProperty(serialized, "normalBackground").colorValue =
            new Color32(48, 53, 59, 255);
        RequireProperty(serialized, "activeBackground").colorValue =
            new Color32(72, 113, 94, 255);
        RequireProperty(serialized, "normalText").colorValue =
            new Color32(190, 195, 199, 255);
        RequireProperty(serialized, "activeText").colorValue =
            new Color32(247, 248, 246, 255);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = RequireProperty(
            serialized,
            propertyName
        );
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(
                serialized.targetObject.GetType().Name,
                propertyName
            );
        }

        return property;
    }

    private static SerializedProperty RequireRelative(
        SerializedProperty parent,
        string propertyName
    )
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(propertyName);
        }

        return property;
    }

    private static T[] FindSceneObjects<T>(Scene scene)
        where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            T[] components = roots[rootIndex].GetComponentsInChildren<T>(
                true
            );
            results.AddRange(components);
        }

        return results.ToArray();
    }

    private sealed class FileBackup
    {
        private readonly List<FileRecord> records;

        private FileBackup(List<FileRecord> records)
        {
            this.records = records;
        }

        public static FileBackup Capture(params string[] assetPaths)
        {
            var records = new List<FileRecord>();

            for (int index = 0; index < assetPaths.Length; index++)
            {
                string assetPath = assetPaths[index];

                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                records.Add(FileRecord.Capture(assetPath));
                records.Add(FileRecord.Capture(assetPath + ".meta"));
            }

            return new FileBackup(records);
        }

        public void Restore()
        {
            for (int index = records.Count - 1; index >= 0; index--)
            {
                records[index].Restore();
            }
        }
    }

    private sealed class FileRecord
    {
        private readonly string path;
        private readonly bool existed;
        private readonly byte[] content;

        private FileRecord(string path, bool existed, byte[] content)
        {
            this.path = path;
            this.existed = existed;
            this.content = content;
        }

        public static FileRecord Capture(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            bool existed = File.Exists(fullPath);
            byte[] content = existed ? File.ReadAllBytes(fullPath) : null;
            return new FileRecord(fullPath, existed, content);
        }

        public void Restore()
        {
            if (existed)
            {
                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(path, content);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
