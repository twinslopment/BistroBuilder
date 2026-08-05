using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba funcional runtime de 2.1E. Comprueba cancelación, commit atómico y
/// propagación inmediata a la oferta 2.1C, y restaura la carta antes de
/// terminar.
/// </summary>
public sealed class BistroBuilderMenuEditor21EFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/2.1E Functional Runtime Menu Editor Test";

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode con el servicio cerrado. Puedes abrir la interfaz " +
        "real o ejecutar la prueba funcional automática.";

    [MenuItem(MenuPath, false, 173)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderMenuEditor21EFunctionalTestWindow>(
            "BB 2.1E Test"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "BistroBuilder 2.1E — Prueba funcional del editor jugable",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba usa un borrador real, demuestra que Cancelar no toca " +
            "la carta, aplica un precio temporal, comprueba la oferta y " +
            "restaura la carta antes de terminar.",
            MessageType.Info
        );
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        if (GUILayout.Button("Abrir editor jugable 2.1E", GUILayout.Height(34f)))
        {
            OpenRuntimeEditor();
        }

        if (GUILayout.Button("Ejecutar prueba funcional 2.1E", GUILayout.Height(40f)))
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
            ))
        {
            report = "FALLO: " + error;
            return;
        }

        if (!view.TryOpenFromInterface(out error))
        {
            report = "FALLO: " + error;
            return;
        }

        report = "Editor jugable abierto. Prueba búsqueda, filtros, detalle, " +
                 "precio, servicios, plato firma, Aplicar y Descartar.";
    }

    private void RunFunctionalTest()
    {
        StringBuilder builder = new StringBuilder(4096);
        int passed = 0;
        int failed = 0;
        BistroBuilderMenuEditorService editor = null;
        BistroBuilderRestaurantMenuService menu = null;
        List<BistroBuilderMenuItemRuntimeState> original =
            new List<BistroBuilderMenuItemRuntimeState>();

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
                    out BistroBuilderMenuEditorRuntimeView view,
                    out string resolveError
                ))
            {
                throw new InvalidOperationException(resolveError);
            }

            menu = editor.MenuService;

            if (editor.IsOpen && !editor.TryClose(true, out string closeError))
            {
                throw new InvalidOperationException(closeError);
            }

            if (!menu.TryGetSnapshot(original, out string originalError))
            {
                throw new InvalidOperationException(originalError);
            }

            Expect(editor.TryOpen(out string openError),
                string.IsNullOrEmpty(openError)
                    ? "Se abre una sesión transaccional real."
                    : openError);
            Expect(
                view.TryValidateVisibleContent(out string visualError),
                string.IsNullOrEmpty(visualError)
                    ? "Categorías, filas y detalle usan viewports visibles y filas activas."
                    : visualError
            );

            List<BistroBuilderMenuEditorDishSnapshot> snapshots =
                new List<BistroBuilderMenuEditorDishSnapshot>();

            if (!editor.TryBuildSnapshot(
                    snapshots,
                    out _,
                    out string buildError
                ))
            {
                throw new InvalidOperationException(buildError);
            }

            BistroBuilderMenuEditorDishSnapshot candidate =
                FindOrderableCandidate(snapshots);

            if (candidate == null)
            {
                throw new InvalidOperationException(
                    "No existe un plato pedible para la prueba funcional."
                );
            }

            int cancelledPrice = ResolveAlternatePrice(
                candidate.CurrentPriceCents,
                17
            );
            BistroBuilderMenuMutationResult cancelMutation =
                editor.TrySetPriceCents(candidate.DishId, cancelledPrice);
            Expect(cancelMutation.Succeeded,
                "El borrador acepta un precio temporal exacto.");
            Expect(
                menu.TryGetItemSnapshot(
                    candidate.DishId,
                    out BistroBuilderMenuItemRuntimeState beforeDiscard
                ) &&
                beforeDiscard.CurrentPriceCents == candidate.CurrentPriceCents,
                "La carta operativa permanece intacta antes de Aplicar."
            );
            Expect(
                editor.TryDiscardAndContinue(out string discardError),
                string.IsNullOrEmpty(discardError)
                    ? "Descartar elimina todos los cambios del borrador."
                    : discardError
            );
            Expect(
                menu.TryGetItemSnapshot(
                    candidate.DishId,
                    out BistroBuilderMenuItemRuntimeState afterDiscard
                ) &&
                afterDiscard.CurrentPriceCents == candidate.CurrentPriceCents,
                "Cancelar no modifica precio ni revisión operativa del plato."
            );

            int appliedPrice = ResolveAlternatePrice(
                candidate.CurrentPriceCents,
                29
            );
            BistroBuilderMenuMutationResult applyMutation =
                editor.TrySetPriceCents(candidate.DishId, appliedPrice);
            Expect(applyMutation.Succeeded,
                "El borrador prepara el cambio que se aplicará.");
            bool applied = editor.TryApplyAndContinue(
                out BistroBuilderMenuEditCommitResult commit,
                out string applyError
            );
            Expect(applied && commit.Succeeded && commit.HadChanges,
                applied
                    ? "Aplicar confirma el lote en una única transacción."
                    : applyError);
            Expect(
                menu.TryGetItemSnapshot(
                    candidate.DishId,
                    out BistroBuilderMenuItemRuntimeState afterApply
                ) && afterApply.CurrentPriceCents == appliedPrice,
                "La carta operativa recibe el precio confirmado."
            );

            bool offerResolved = editor.OfferService.TryEvaluateDish(
                candidate.DishId,
                editor.PreviewMealService,
                editor.PreviewServiceMode,
                out BistroBuilderMenuOfferItemSnapshot offer,
                out string offerError
            );
            Expect(
                offerResolved && offer.PriceCents == appliedPrice,
                offerResolved
                    ? "Mesa/barra leen inmediatamente el precio desde la oferta 2.1C."
                    : offerError
            );
            Expect(
                editor.EditSessionService.HasOpenSession &&
                !editor.EditSessionService.HasPendingChanges,
                "Tras aplicar, el editor continúa con un borrador limpio."
            );
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

            if (menu != null && original.Count > 0)
            {
                bool restored = menu.TryReplaceAll(
                    original,
                    true,
                    out string restoreError
                );
                Expect(
                    restored,
                    restored
                        ? "La carta original se restaura antes de terminar."
                        : restoreError
                );
            }
        }

        report = "PRUEBA FUNCIONAL 2.1E " +
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
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuEditorService>(scene);
        List<BistroBuilderMenuEditorRuntimeView> views =
            BistroBuilderMenuEditor21EInstaller
                .FindSceneComponents<BistroBuilderMenuEditorRuntimeView>(
                    scene
                );

        editor = editors.Count == 1 ? editors[0] : null;
        view = views.Count == 1 ? views[0] : null;

        if (editor == null || view == null)
        {
            error = "2.1E no está instalado de forma única en la escena.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderMenuEditorDishSnapshot FindOrderableCandidate(
        List<BistroBuilderMenuEditorDishSnapshot> snapshots
    )
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            BistroBuilderMenuEditorDishSnapshot item = snapshots[index];

            if (item.Included && item.IsOrderable &&
                item.CurrentPriceCents >= 0)
            {
                return item;
            }
        }

        return null;
    }

    private static int ResolveAlternatePrice(int current, int delta)
    {
        if (current <= BistroBuilderDishDefinition.MaximumPriceCents - delta)
        {
            return current + delta;
        }

        return Math.Max(0, current - delta);
    }
}
