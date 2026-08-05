using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba funcional runtime de 2.1G1/2. Edita una receta existente, demuestra
/// descarte, crea un plato nuevo con receta, aplica el lote y restaura carta y
/// capas runtime antes de terminar.
/// </summary>
public sealed class BistroBuilderMenuDishRecipe21G12FunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/2.1G1-2 Dish and Recipe Functional Test";

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode y ejecuta la prueba. También puedes abrir el " +
        "editor real y usar Nuevo plato o Editar plato y receta.";

    [MenuItem(MenuPath, false, 193)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderMenuDishRecipe21G12FunctionalTestWindow>(
            "BB 2.1G1-2 Test"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "BistroBuilder 2.1G1/2 — Prueba funcional",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba modifica únicamente estado runtime, comprueba " +
            "Descartar y Aplicar, y restaura carta, platos y recetas.",
            MessageType.Info
        );
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        if (GUILayout.Button("Abrir editor jugable", GUILayout.Height(34f)))
        {
            OpenRuntimeEditor();
        }

        if (GUILayout.Button(
                "Ejecutar prueba funcional 2.1G1/2",
                GUILayout.Height(42f)
            ))
        {
            RunFunctionalTest();
        }

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void OpenRuntimeEditor()
    {
        if (!TryResolve(
                out _,
                out BistroBuilderMenuEditorRuntimeView view,
                out string error
            ) ||
            !view.TryOpenFromInterface(out error))
        {
            report = "FALLO: " + error;
            return;
        }

        report = "Editor abierto. Usa Nuevo plato o Editar plato y receta.";
    }

    private void RunFunctionalTest()
    {
        StringBuilder builder = new StringBuilder(6144);
        int passed = 0;
        int failed = 0;
        BistroBuilderMenuEditorService editor = null;
        BistroBuilderRestaurantMenuService menu = null;
        BistroBuilderDishCatalogService dishes = null;
        BistroBuilderRecipeCatalogService recipes = null;
        List<BistroBuilderMenuItemRuntimeState> originalMenu =
            new List<BistroBuilderMenuItemRuntimeState>();
        List<BistroBuilderDishDefinition> originalRuntimeDishes =
            new List<BistroBuilderDishDefinition>();
        List<BistroBuilderRecipeDefinition> originalRuntimeRecipes =
            new List<BistroBuilderRecipeDefinition>();
        List<BistroBuilderDishDefinition> appliedRuntimeDishes =
            new List<BistroBuilderDishDefinition>();
        List<BistroBuilderRecipeDefinition> appliedRuntimeRecipes =
            new List<BistroBuilderRecipeDefinition>();

        void Expect(bool condition, string message)
        {
            if (condition)
            {
                passed++;
                builder.AppendLine("- OK: " + message);
            }
            else
            {
                failed++;
                builder.AppendLine("- FALLO: " + message);
            }
        }

        try
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "Entra en Play Mode antes de ejecutar la prueba."
                );
            }

            if (!TryResolve(
                    out editor,
                    out _,
                    out string resolveError
                ))
            {
                throw new InvalidOperationException(resolveError);
            }

            menu = editor.MenuService;
            dishes = editor.CatalogService;
            recipes = editor.RecipeCatalogService;

            if (editor.IsOpen && !editor.TryClose(true, out string closeError))
            {
                throw new InvalidOperationException(closeError);
            }

            if (!menu.TryGetSnapshot(originalMenu, out string snapshotError))
            {
                throw new InvalidOperationException(snapshotError);
            }

            dishes.CopyRuntimeDefinitionsTo(originalRuntimeDishes);
            recipes.CopyRuntimeRecipesTo(originalRuntimeRecipes);
            Expect(
                editor.TryOpen(out string openError),
                string.IsNullOrWhiteSpace(openError)
                    ? "Se abre una sesión conjunta de carta y autoría."
                    : openError
            );

            List<BistroBuilderMenuEditorDishSnapshot> snapshots =
                new List<BistroBuilderMenuEditorDishSnapshot>();

            if (!editor.TryBuildSnapshot(snapshots, out _, out string buildError) ||
                snapshots.Count == 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(buildError)
                        ? "No existen platos para la prueba."
                        : buildError
                );
            }

            BistroBuilderMenuEditorDishSnapshot candidate = snapshots[0];

            if (!editor.TryGetDishAuthoringRequest(
                    candidate.DishId,
                    out BistroBuilderDishRecipeAuthoringRequest editRequest,
                    out string requestError
                ))
            {
                throw new InvalidOperationException(requestError);
            }

            Expect(
                editRequest.Ingredients.Count > 0,
                "Un plato canónico abre su receta completa en el formulario."
            );

            string originalName = editRequest.DisplayName;
            double originalAmount = editRequest.Ingredients[0].Amount;
            editRequest.DisplayName = originalName + " · G12";
            editRequest.Ingredients[0].Amount = originalAmount + 1d;
            BistroBuilderDishRecipeAuthoringResult editResult =
                editor.TryCreateOrUpdateDishRecipe(editRequest);
            Expect(
                editResult.Succeeded,
                editResult.Succeeded
                    ? "La edición de datos e ingredientes entra en el borrador."
                    : editResult.Message
            );
            Expect(
                dishes.TryGetDefinition(
                    candidate.DishId,
                    out BistroBuilderDishDefinition beforeDiscardDish
                ) && beforeDiscardDish.DisplayName == originalName,
                "El catálogo operativo permanece intacto antes de Aplicar."
            );

            snapshots.Clear();
            bool previewBuilt = editor.TryBuildSnapshot(
                snapshots,
                out _,
                out string previewError
            );
            BistroBuilderMenuEditorDishSnapshot preview =
                FindSnapshot(snapshots, candidate.DishId);
            Expect(
                previewBuilt && preview != null &&
                preview.DisplayName == editRequest.DisplayName &&
                preview.HasValidEconomics,
                previewBuilt
                    ? "La vista previa recalcula nombre, receta y escandallo."
                    : previewError
            );
            Expect(
                editor.TryDiscardAndContinue(out string discardError),
                string.IsNullOrWhiteSpace(discardError)
                    ? "Descartar elimina la edición completa de plato y receta."
                    : discardError
            );
            Expect(
                dishes.TryGetDefinition(
                    candidate.DishId,
                    out BistroBuilderDishDefinition afterDiscardDish
                ) && afterDiscardDish.DisplayName == originalName,
                "Descartar conserva el plato y receta operativos originales."
            );

            BistroBuilderDishRecipeAuthoringRequest createRequest =
                editor.CreateNewDishAuthoringRequest();
            createRequest.DisplayName = "Plato funcional G12";
            createRequest.Description =
                "Plato temporal creado por la prueba funcional.";
            createRequest.CategoryId = editRequest.CategoryId;
            createRequest.Course = editRequest.Course;
            createRequest.RequiredStation = editRequest.RequiredStation;
            createRequest.DefaultAvailability =
                editRequest.DefaultAvailability;
            createRequest.AllowedServiceModes =
                editRequest.AllowedServiceModes;
            createRequest.BasePriceCents = Math.Min(
                editor.CommercialPolicy.MaximumPriceCents,
                Math.Max(
                    editor.CommercialPolicy.MinimumPriceCents,
                    editRequest.BasePriceCents + 37
                )
            );
            createRequest.PreparationDifficulty = 7;
            createRequest.BasePreparationSeconds = 437;
            createRequest.YieldPortions = Math.Max(1, editRequest.YieldPortions);
            createRequest.WasteBasisPoints = editRequest.WasteBasisPoints;
            createRequest.Notes = "Creado durante la prueba 2.1G1/2.";
            createRequest.Ingredients.Clear();

            for (int index = 0; index < editRequest.Ingredients.Count; index++)
            {
                createRequest.Ingredients.Add(
                    editRequest.Ingredients[index].Clone()
                );
            }

            BistroBuilderDishRecipeAuthoringResult createResult =
                editor.TryCreateOrUpdateDishRecipe(createRequest);
            string createdDishId = createResult.DishId;
            Expect(
                createResult.Succeeded &&
                BistroBuilderMenuIdUtility.IsValidStableId(createdDishId),
                createResult.Succeeded
                    ? "El jugador crea un plato con DishId estable."
                    : createResult.Message
            );
            Expect(
                !dishes.Contains(createdDishId) &&
                !recipes.TryGetRecipeByDishId(createdDishId, out _),
                "El plato nuevo y su receta siguen aislados antes de Aplicar."
            );

            bool applied = editor.TryApplyAndContinue(
                out BistroBuilderMenuEditCommitResult commit,
                out string applyError
            );
            Expect(
                applied && commit.Succeeded && commit.HadChanges,
                applied
                    ? "Aplicar confirma carta, plato y receta en una transacción."
                    : applyError
            );
            Expect(
                dishes.TryGetDefinition(
                    createdDishId,
                    out BistroBuilderDishDefinition createdDish
                ) &&
                recipes.TryGetRecipeByDishId(
                    createdDishId,
                    out BistroBuilderRecipeDefinition createdRecipe
                ) &&
                ReferenceEquals(createdRecipe.Dish, createdDish) &&
                createdRecipe.TryValidate(out _),
                "Los catálogos efectivos resuelven el plato y la receta creados."
            );
            Expect(
                menu.TryGetItemSnapshot(
                    createdDishId,
                    out BistroBuilderMenuItemRuntimeState createdMenuItem
                ) &&
                createdMenuItem.ResolvePreparationDifficulty(createdDish) == 7 &&
                createdMenuItem.ResolveBasePreparationSeconds(createdDish) == 437,
                "La carta operativa recibe precio y preparación del plato nuevo."
            );
            Expect(
                recipes.TryGetEconomics(
                    createdDishId,
                    out BistroBuilderRecipeEconomicsSnapshot economics,
                    out string economicsError
                ) && economics.CostPerPortionCents >= 0,
                string.IsNullOrWhiteSpace(economicsError)
                    ? "El escandallo del plato creado se calcula correctamente."
                    : economicsError
            );
            Expect(
                editor.EditSessionService.HasOpenSession &&
                editor.AuthoringService.HasOpenSession &&
                !editor.HasPendingChanges,
                "Tras aplicar, ambas sesiones continúan con borradores limpios."
            );

            dishes.CopyRuntimeDefinitionsTo(appliedRuntimeDishes);
            recipes.CopyRuntimeRecipesTo(appliedRuntimeRecipes);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("- FALLO: " + exception.Message);
        }
        finally
        {
            if (editor != null && editor.IsOpen)
            {
                editor.TryClose(true, out _);
            }

            bool catalogsRestored = true;
            string restoreError = string.Empty;

            if (dishes != null && recipes != null)
            {
                catalogsRestored = dishes.TryReplaceRuntimeDefinitions(
                    originalRuntimeDishes,
                    out restoreError,
                    false
                );

                if (catalogsRestored)
                {
                    catalogsRestored = recipes.TryReplaceRuntimeRecipes(
                        originalRuntimeRecipes,
                        out restoreError,
                        false
                    );
                }

                if (catalogsRestored)
                {
                    dishes.PublishChanged();
                    recipes.PublishChanged();
                }
            }

            Expect(
                catalogsRestored,
                catalogsRestored
                    ? "Las capas runtime originales se restauran."
                    : restoreError
            );

            if (menu != null && originalMenu.Count > 0)
            {
                bool menuRestored = menu.TryReplaceAll(
                    originalMenu,
                    true,
                    out string menuRestoreError
                );
                Expect(
                    menuRestored,
                    menuRestored
                        ? "La carta original se restaura antes de terminar."
                        : menuRestoreError
                );
            }

            DestroyNewRuntimeObjects(
                appliedRuntimeRecipes,
                originalRuntimeRecipes
            );
            DestroyNewRuntimeObjects(
                appliedRuntimeDishes,
                originalRuntimeDishes
            );
        }

        report = "PRUEBA FUNCIONAL 2.1G1/2 " +
                 (failed == 0 ? "SUPERADA" : "FALLIDA") + "\n" +
                 "Correctos: " + passed + "\n" +
                 "Fallos: " + failed + "\n" + builder;

        if (failed == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
    }

    private static bool TryResolve(
        out BistroBuilderMenuEditorService editor,
        out BistroBuilderMenuEditorRuntimeView view,
        out string error
    )
    {
        Scene scene = SceneManager.GetActiveScene();
        List<BistroBuilderMenuEditorService> editors =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuEditorService
            >(scene);
        List<BistroBuilderMenuEditorRuntimeView> views =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuEditorRuntimeView
            >(scene);

        editor = editors.Count == 1 ? editors[0] : null;
        view = views.Count == 1 ? views[0] : null;

        if (editor == null || view == null)
        {
            error = "2.1G1/2 no está instalado de forma única en la escena.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderMenuEditorDishSnapshot FindSnapshot(
        List<BistroBuilderMenuEditorDishSnapshot> snapshots,
        string dishId
    )
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            if (string.Equals(
                    snapshots[index].DishId,
                    dishId,
                    StringComparison.Ordinal
                ))
            {
                return snapshots[index];
            }
        }

        return null;
    }

    private static void DestroyNewRuntimeObjects<T>(
        List<T> applied,
        List<T> original
    ) where T : UnityEngine.Object
    {
        HashSet<T> preserved = new HashSet<T>(original);

        for (int index = 0; index < applied.Count; index++)
        {
            T value = applied[index];

            if (value != null && !preserved.Contains(value))
            {
                UnityEngine.Object.Destroy(value);
            }
        }
    }
}
