using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado acumulado de la validación 368B.
/// </summary>
public sealed class BistroBuilderCanonicalInventoryValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void AddCorrect(string message)
    {
        correct.Add(message);
    }

    public void AddWarning(string message)
    {
        warnings.Add(message);
    }

    public void AddError(string message)
    {
        errors.Add(message);
    }

    public string BuildReport()
    {
        var builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - INVENTARIO, HUD Y DISTRIBUCIÓN 368B2"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        AppendGroup(builder, "OK", correct);
        AppendGroup(builder, "ADVERTENCIA", warnings);
        AppendGroup(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void AppendGroup(
        StringBuilder builder,
        string prefix,
        List<string> messages
    )
    {
        for (int index = 0; index < messages.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(messages[index]);
        }
    }
}

/// <summary>
/// Validador no destructivo del núcleo de inventario y del dock de tiempo.
/// </summary>
public static class BistroBuilderCanonicalInventoryValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Validate 368B2 Canonical Inventory, HUD & Chair Layout";

    [MenuItem(MenuPath, false, 340)]
    private static void ValidateFromMenu()
    {
        BistroBuilderCanonicalInventoryValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderCanonicalInventoryValidationResult
        ValidateCurrentProject()
    {
        var result =
            new BistroBuilderCanonicalInventoryValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No existe una escena activa válida.");
            return result;
        }

        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.AddError("No se encontró GameSystems.");
            return result;
        }

        result.AddCorrect("GameSystems localizado.");

        BistroBuilderIngredientsRecipesValidationResult previous =
            BistroBuilderIngredientsRecipesValidator
                .ValidateCurrentProject();

        if (previous.ErrorCount > 0)
        {
            result.AddError(
                "La base 368A no supera su validador."
            );
        }
        else
        {
            result.AddCorrect(
                "La base 368A de ingredientes y recetas sigue válida."
            );
        }

        BistroBuilderRecipeCatalogService recipeService =
            gameSystems.GetComponent<BistroBuilderRecipeCatalogService>();

        if (recipeService == null)
        {
            result.AddError(
                "No existe BistroBuilderRecipeCatalogService."
            );
            return result;
        }

        if (!recipeService.ValidateConfiguration(out string error))
        {
            result.AddError(
                "El servicio de recetas no es válido: " + error
            );
            return result;
        }

        BistroBuilderOpeningStockProfile profile =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderOpeningStockProfile
            >(
                BistroBuilderCanonicalInventoryInstaller
                    .OpeningStockProfilePath
            );

        ValidateOpeningProfile(recipeService, profile, result);
        ValidateInventoryService(
            gameSystems,
            recipeService,
            profile,
            result
        );
        ValidateChairsAndLayout(scene, result);
        ValidateHud(scene, result);

        return result;
    }

    private static void ValidateOpeningProfile(
        BistroBuilderRecipeCatalogService recipeService,
        BistroBuilderOpeningStockProfile profile,
        BistroBuilderCanonicalInventoryValidationResult result
    )
    {
        if (profile == null)
        {
            result.AddError(
                "No existe el perfil de existencias iniciales 368B."
            );
            return;
        }

        if (!profile.TryValidate(out string error))
        {
            result.AddError(error);
            return;
        }

        int ingredientCount = recipeService.IngredientCount;

        if (profile.LineCount != ingredientCount)
        {
            result.AddError(
                "El perfil contiene " + profile.LineCount +
                " líneas y el catálogo " + ingredientCount +
                " ingredientes."
            );
            return;
        }

        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderOpeningStockLine> lines = profile.Lines;

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderOpeningStockLine line = lines[index];

            if (line == null ||
                line.Ingredient == null ||
                !recipeService.TryGetIngredient(
                    line.Ingredient.IngredientId,
                    out BistroBuilderIngredientDefinition catalogued
                ) ||
                catalogued != line.Ingredient ||
                !ids.Add(line.Ingredient.IngredientId))
            {
                result.AddError(
                    "El perfil de apertura no coincide con el catálogo."
                );
                return;
            }
        }

        result.AddCorrect(
            "Perfil de apertura válido para los " + ingredientCount +
            " ingredientes canónicos."
        );
    }

    private static void ValidateInventoryService(
        GameObject gameSystems,
        BistroBuilderRecipeCatalogService recipeService,
        BistroBuilderOpeningStockProfile profile,
        BistroBuilderCanonicalInventoryValidationResult result
    )
    {
        BistroBuilderInventoryService[] services =
            gameSystems.GetComponents<BistroBuilderInventoryService>();

        if (services.Length != 1)
        {
            result.AddError(
                "GameSystems debe contener exactamente un " +
                nameof(BistroBuilderInventoryService) + "."
            );
            return;
        }

        BistroBuilderInventoryService service = services[0];

        if (!service.ValidateConfiguration(out string error))
        {
            result.AddError(error);
            return;
        }

        if (service.OpeningStockProfile != profile)
        {
            result.AddError(
                "El servicio runtime no usa el perfil oficial 368B."
            );
            return;
        }

        if (!service.TryInitialize(out error))
        {
            result.AddError(
                "El inventario no puede inicializarse: " + error
            );
            return;
        }

        if (service.StockEntryCount != recipeService.IngredientCount ||
            service.TransactionCount != profile.LineCount)
        {
            result.AddError(
                "El runtime no ha creado todos los balances o movimientos " +
                "iniciales."
            );
            return;
        }

        var stock = new List<BistroBuilderInventoryStockSnapshot>();
        service.CopyStockSnapshotsTo(stock);

        for (int index = 0; index < stock.Count; index++)
        {
            BistroBuilderInventoryStockSnapshot snapshot = stock[index];

            if (snapshot.OnHandCanonicalMilliUnits <= 0L ||
                snapshot.ReservedCanonicalMilliUnits != 0L ||
                snapshot.AvailableCanonicalMilliUnits !=
                    snapshot.OnHandCanonicalMilliUnits ||
                snapshot.ConsumedCanonicalMilliUnits != 0L ||
                snapshot.WastedCanonicalMilliUnits != 0L)
            {
                result.AddError(
                    "El balance inicial de " + snapshot.IngredientId +
                    " no es coherente."
                );
                return;
            }
        }

        if (!service.ValidateRuntimeState(out error))
        {
            result.AddError(error);
            return;
        }

        result.AddCorrect(
            "Inventario runtime inicializado con " +
            service.StockEntryCount + " balances y libro auditable."
        );
        result.AddCorrect(
            "Todos los balances iniciales están libres, sin consumo ni " +
            "merma acumulada."
        );
    }

    private static void ValidateChairsAndLayout(
        Scene scene,
        BistroBuilderCanonicalInventoryValidationResult result
    )
    {
        BistroBuilder368AInstalledChair[] chairs =
            FindSceneObjects<BistroBuilder368AInstalledChair>(scene);
        RestaurantTable[] tables =
            FindSceneObjects<RestaurantTable>(scene);
        var tableAreas = new Dictionary<int, RestaurantArea>();

        for (int index = 0; index < tables.Length; index++)
        {
            RestaurantAreaMember member =
                tables[index].GetComponent<RestaurantAreaMember>();

            if (member == null || member.AssignedArea == null)
            {
                result.AddError(
                    tables[index].name +
                    " no tiene un área válida para sus sillas."
                );
                return;
            }

            tableAreas[tables[index].TableId] = member.AssignedArea;
        }

        for (int index = 0; index < chairs.Length; index++)
        {
            BistroBuilder368AInstalledChair chair = chairs[index];
            RestaurantAreaMember member =
                chair.GetComponent<RestaurantAreaMember>();

            if (!tableAreas.TryGetValue(
                    chair.TableId,
                    out RestaurantArea tableArea
                ) ||
                member == null ||
                member.AssignedArea == null ||
                !ReferenceEquals(member.AssignedArea, tableArea))
            {
                result.AddError(
                    chair.name +
                    " no hereda el área de su mesa."
                );
                return;
            }
        }

        result.AddCorrect(
            "Las " + chairs.Length +
            " sillas operativas heredan correctamente el área de su mesa."
        );

        if (!BistroBuilder368B1SceneLayoutRepair.Validate(
                scene,
                out string layoutError
            ))
        {
            result.AddError(layoutError);
            return;
        }

        result.AddCorrect(
            "La distribución provisional de mesas y sillas respeta " +
            "obstáculos y espacios operativos."
        );
    }

    private static void ValidateHud(
        Scene scene,
        BistroBuilderCanonicalInventoryValidationResult result
    )
    {
        BistroBuilder368BInstalledHudDock[] docks =
            FindSceneObjects<BistroBuilder368BInstalledHudDock>(scene);

        if (docks.Length != 1)
        {
            result.AddError(
                "La escena debe contener un único dock de tiempo 368B."
            );
            return;
        }

        BistroBuilder368BInstalledHudDock dock = docks[0];

        if (!dock.ValidateConfiguration(out string error))
        {
            result.AddError(error);
            return;
        }

        TMP_Text clockText = dock.ClockText;

        if (clockText == null ||
            clockText.font == null ||
            clockText.fontSize < 20f ||
            (clockText.fontStyle & FontStyles.Bold) == 0)
        {
            result.AddError(
                "El reloj no usa la tipografía compacta y moderna 368B."
            );
            return;
        }

        SpeedButtonController[] speeds = dock.SpeedButtons;
        var expected = new[] { 1f, 2f, 3f };

        for (int index = 0; index < expected.Length; index++)
        {
            if (index >= speeds.Length ||
                speeds[index] == null ||
                !Mathf.Approximately(
                    speeds[index].SpeedMultiplier,
                    expected[index]
                ) ||
                speeds[index].ButtonText == null ||
                speeds[index].ButtonText.font == null)
            {
                result.AddError(
                    "El dock no conserva x1, x2 y x3 en orden."
                );
                return;
            }
        }

        if (dock.PauseButton.ButtonText == null ||
            dock.PauseButton.ButtonText.font == null)
        {
            result.AddError(
                "El botón de pausa no tiene tipografía TMP válida."
            );
            return;
        }

        if (clockText.overflowMode != TextOverflowModes.Truncate ||
            dock.PauseButton.ButtonText.overflowMode !=
                TextOverflowModes.Truncate)
        {
            result.AddError(
                "El reloj y la pausa deben usar Truncate para no depender " +
                "del glifo de elipsis de la fuente TMP."
            );
            return;
        }

        for (int index = 0; index < speeds.Length; index++)
        {
            if (speeds[index].ButtonText.overflowMode !=
                TextOverflowModes.Truncate)
            {
                result.AddError(
                    "Los botones de velocidad deben usar Truncate para " +
                    "no generar advertencias TMP por elipsis."
                );
                return;
            }
        }

        result.AddCorrect(
            "Reloj, pausa y velocidades están agrupados abajo a la " +
            "derecha, fuera del centro jugable."
        );
        result.AddCorrect(
            "El HUD utiliza jerarquía tipográfica compacta y estados " +
            "visuales sincronizados."
        );
    }

    private static T[] FindSceneObjects<T>(Scene scene)
        where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            results.AddRange(
                roots[rootIndex].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
    }
}
