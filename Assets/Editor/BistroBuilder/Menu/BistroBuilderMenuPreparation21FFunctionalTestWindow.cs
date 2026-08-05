using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional runtime de 2.1F. Modifica un plato, valida aislamiento del
/// borrador, descarte, commit, consumo por cocina y captura menu.state v3.
/// Restaura la carta original antes de terminar.
/// </summary>
public sealed class BistroBuilderMenuPreparation21FFunctionalTestWindow :
    EditorWindow
{
    [MenuItem(
        "Tools/Bistro Builder/Menu/2.1F Preparation Functional Test",
        false,
        183
    )]
    private static void Open()
    {
        GetWindow<BistroBuilderMenuPreparation21FFunctionalTestWindow>(
            "Prueba 2.1F"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.1F — Preparación configurable",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Ejecuta la prueba durante Play Mode. La carta original se " +
            "restaura al terminar.",
            MessageType.Info
        );
        GUI.enabled = EditorApplication.isPlaying;

        if (GUILayout.Button("Ejecutar prueba funcional 2.1F"))
        {
            RunFunctionalTest();
        }

        GUI.enabled = true;
    }

    private static void RunFunctionalTest()
    {
        List<string> correct = new List<string>();
        List<string> failures = new List<string>();
        BistroBuilderMenuEditorService editor =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMenuEditorService
            >();
        BistroBuilderRestaurantMenuService menu =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRestaurantMenuService
            >();
        BistroBuilderRestaurantMenuCollectionService collection =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRestaurantMenuCollectionService
            >();
        BistroBuilderMenuSaveSectionProvider provider =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMenuSaveSectionProvider
            >();
        BistroBuilderOrderLineExecutionService execution =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOrderLineExecutionService
            >();
        List<BistroBuilderMenuItemRuntimeState> original =
            new List<BistroBuilderMenuItemRuntimeState>();
        bool restoreRequired = false;

        try
        {
            if (editor == null || menu == null || collection == null ||
                provider == null || execution == null)
            {
                throw new InvalidOperationException(
                    "Faltan autoridades runtime necesarias para 2.1F."
                );
            }

            if (!menu.TryGetSnapshot(original, out string error) ||
                original.Count == 0)
            {
                throw new InvalidOperationException(error);
            }

            if (editor.IsOpen && !editor.TryClose(true, out error))
            {
                throw new InvalidOperationException(error);
            }

            if (!editor.TryOpen(out error))
            {
                throw new InvalidOperationException(error);
            }

            List<BistroBuilderMenuEditorDishSnapshot> snapshots =
                new List<BistroBuilderMenuEditorDishSnapshot>();

            if (!editor.TryBuildSnapshot(
                    snapshots,
                    out _,
                    out error
                ))
            {
                throw new InvalidOperationException(error);
            }

            BistroBuilderMenuEditorDishSnapshot target =
                snapshots.Find(item => item != null && item.Included);

            if (target == null)
            {
                throw new InvalidOperationException(
                    "No existe un plato incluido para probar."
                );
            }

            string targetDishId = target.DishId;
            int originalDifficulty = target.PreparationDifficulty;
            int originalSeconds = target.PreparationSeconds;
            int nextDifficulty = originalDifficulty < 10
                ? originalDifficulty + 1
                : originalDifficulty - 1;
            int nextSeconds = originalSeconds <= 86340
                ? originalSeconds + 60
                : originalSeconds - 60;

            Check(
                editor.TrySetPreparationDifficulty(
                    targetDishId,
                    nextDifficulty
                ).Succeeded &&
                editor.TrySetBasePreparationSeconds(
                    targetDishId,
                    nextSeconds
                ).Succeeded,
                "El borrador acepta dificultad y tiempo exactos.",
                correct,
                failures
            );
            Check(
                menu.TryResolvePreparationSettings(
                    targetDishId,
                    out int operationalDifficulty,
                    out int operationalSeconds,
                    out error
                ) &&
                operationalDifficulty == originalDifficulty &&
                operationalSeconds == originalSeconds,
                "La carta operativa permanece intacta antes de Aplicar.",
                correct,
                failures
            );
            Check(
                editor.TryDiscardAndContinue(out error),
                "Descartar elimina los cambios de preparación.",
                correct,
                failures
            );

            if (!editor.TryBuildSnapshot(snapshots, out _, out error))
            {
                throw new InvalidOperationException(error);
            }

            target = snapshots.Find(
                item => item != null && item.DishId == targetDishId
            );
            Check(
                target != null &&
                target.PreparationDifficulty == originalDifficulty &&
                target.PreparationSeconds == originalSeconds,
                "Tras descartar se abre un borrador limpio.",
                correct,
                failures
            );

            editor.TrySetPreparationDifficulty(
                targetDishId,
                nextDifficulty
            );
            editor.TrySetBasePreparationSeconds(
                targetDishId,
                nextSeconds
            );
            Check(
                editor.TryApplyAndContinue(
                    out BistroBuilderMenuEditCommitResult commit,
                    out error
                ) && commit.Succeeded && commit.HadChanges,
                "Aplicar confirma ambos valores en una transacción.",
                correct,
                failures
            );
            restoreRequired = true;
            Check(
                menu.TryResolvePreparationSettings(
                    targetDishId,
                    out operationalDifficulty,
                    out operationalSeconds,
                    out error
                ) &&
                operationalDifficulty == nextDifficulty &&
                operationalSeconds == nextSeconds,
                "La carta operativa recibe la preparación confirmada.",
                correct,
                failures
            );
            Check(
                execution.TryResolveDishPreparationDurationSeconds(
                    targetDishId,
                    0.01f,
                    0.25f,
                    30f,
                    out float scaled,
                    out error
                ) &&
                Mathf.Approximately(
                    scaled,
                    Mathf.Clamp(nextSeconds * 0.01f, 0.25f, 30f)
                ),
                "La cocina calcula la duración desde la carta 2.1F.",
                correct,
                failures
            );

            BistroBuilderSaveCaptureContext capture =
                new BistroBuilderSaveCaptureContext(2101001);
            RunEnumerator(provider.CaptureState(capture));
            BistroBuilderMenuSaveData data =
                capture.State as BistroBuilderMenuSaveData;
            BistroBuilderMenuItemSaveData saved = FindSaved(
                data,
                collection.ActiveRestaurantId,
                targetDishId
            );
            Check(
                !capture.HasFailed && data != null &&
                data.schemaVersion == 3 && saved != null &&
                saved.preparationDifficulty == nextDifficulty &&
                saved.basePreparationSeconds == nextSeconds,
                "menu.state v3 captura las decisiones editables.",
                correct,
                failures
            );
        }
        catch (Exception exception)
        {
            failures.Add("Excepción no controlada: " + exception.Message);
        }
        finally
        {
            if (editor != null && editor.IsOpen)
            {
                editor.TryClose(true, out _);
            }

            if (restoreRequired && collection != null && menu != null)
            {
                if (!collection.TryGetRestaurantSnapshot(
                        collection.ActiveRestaurantId,
                        out BistroBuilderRestaurantMenuRuntimeState current,
                        out string snapshotError
                    ))
                {
                    failures.Add(
                        "No se pudo preparar la restauración: " +
                        snapshotError
                    );
                }
                else if (!collection.TryReplaceActiveRestaurantItems(
                             original,
                             current.Revision,
                             menu.Revision,
                             true,
                             out _,
                             out _,
                             out string restoreError
                         ))
                {
                    failures.Add(
                        "No se pudo restaurar la carta original: " +
                        restoreError
                    );
                }
                else
                {
                    correct.Add(
                        "La carta original se restaura antes de terminar."
                    );
                }
            }
        }

        string report =
            "PRUEBA FUNCIONAL 2.1F " +
            (failures.Count == 0 ? "SUPERADA" : "CON FALLOS") +
            "\nCorrectos: " + correct.Count +
            "\nFallos: " + failures.Count;

        for (int index = 0; index < correct.Count; index++)
        {
            report += "\n- OK: " + correct[index];
        }

        for (int index = 0; index < failures.Count; index++)
        {
            report += "\n- FALLO: " + failures[index];
        }

        if (failures.Count == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
    }

    private static BistroBuilderMenuItemSaveData FindSaved(
        BistroBuilderMenuSaveData data,
        string restaurantId,
        string dishId
    )
    {
        if (data?.restaurants == null)
        {
            return null;
        }

        for (int restaurantIndex = 0;
             restaurantIndex < data.restaurants.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                data.restaurants[restaurantIndex];

            if (restaurant == null ||
                restaurant.restaurantId != restaurantId ||
                restaurant.items == null)
            {
                continue;
            }

            for (int itemIndex = 0;
                 itemIndex < restaurant.items.Count;
                 itemIndex++)
            {
                BistroBuilderMenuItemSaveData item =
                    restaurant.items[itemIndex];

                if (item != null && item.dishId == dishId)
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static void Check(
        bool condition,
        string description,
        List<string> correct,
        List<string> failures
    )
    {
        (condition ? correct : failures).Add(description);
    }

    private static void RunEnumerator(IEnumerator routine)
    {
        while (routine != null && routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
            {
                RunEnumerator(nested);
            }
        }
    }
}
