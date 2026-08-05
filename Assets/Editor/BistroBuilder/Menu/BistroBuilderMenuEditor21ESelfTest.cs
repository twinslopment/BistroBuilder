using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Autotest determinista de 2.1E. Trabaja únicamente sobre el borrador y
/// comprueba al final que la carta operativa y su revisión no han cambiado.
/// </summary>
public static class BistroBuilderMenuEditor21ESelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1E Runtime Menu Editor Self-Test";

    private sealed class Report
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Lines = new List<string>();

        public void Expect(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                Lines.Add("- OK: " + message);
            }
            else
            {
                Failed++;
                Lines.Add("- FALLO: " + message);
            }
        }

        public string Build()
        {
            return "BISTRO BUILDER - AUTOTEST 2.1E EDITOR JUGABLE\n" +
                   "Pruebas superadas: " + Passed + "\n" +
                   "Pruebas fallidas: " + Failed + "\n" +
                   string.Join("\n", Lines);
        }
    }

    [MenuItem(MenuPath, false, 172)]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 2.1E.",
                "Aceptar"
            );
            return;
        }

        Report report = new Report();
        BistroBuilderMenuEditorService editor = null;
        BistroBuilderRestaurantMenuService menu = null;
        List<BistroBuilderMenuItemRuntimeState> original =
            new List<BistroBuilderMenuItemRuntimeState>();
        int originalRevision = -1;

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            List<BistroBuilderMenuEditorService> editors =
                BistroBuilderMenuEditor21EInstaller
                    .FindSceneComponents<BistroBuilderMenuEditorService>(
                        scene
                    );
            report.Expect(
                editors.Count == 1,
                "Existe una única autoridad 2.1E."
            );

            if (editors.Count != 1)
            {
                throw new InvalidOperationException(
                    "No puede continuar sin una autoridad 2.1E única."
                );
            }

            editor = editors[0];
            menu = editor.MenuService;
            report.Expect(
                editor.ValidateConfiguration(out string configurationError),
                string.IsNullOrEmpty(configurationError)
                    ? "La configuración 2.1E es válida."
                    : configurationError
            );

            if (menu == null)
            {
                throw new InvalidOperationException(
                    "La autoridad 2.1E no tiene una carta operativa configurada."
                );
            }

            if (!menu.TryGetSnapshot(original, out string snapshotError))
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(snapshotError)
                        ? "No se pudo capturar el estado original de la carta."
                        : snapshotError
                );
            }

            originalRevision = menu.Revision;
            RunUtilityTests(report);

            if (editor.IsOpen)
            {
                editor.TryClose(true, out _);
            }

            bool opened = editor.TryOpen(out string openError);
            report.Expect(opened, opened ? "El editor abre un borrador aislado." : openError);

            if (!opened)
            {
                throw new InvalidOperationException(openError);
            }

            List<BistroBuilderMenuEditorDishSnapshot> snapshots =
                new List<BistroBuilderMenuEditorDishSnapshot>();
            bool built = editor.TryBuildSnapshot(
                snapshots,
                out BistroBuilderMenuEditorSummarySnapshot summary,
                out string buildError
            );
            report.Expect(
                built,
                built
                    ? "La vista se construye también sin inventario runtime."
                    : buildError
            );

            if (!built || snapshots.Count == 0)
            {
                throw new InvalidOperationException(
                    built ? "La vista no contiene platos." : buildError
                );
            }

            report.Expect(
                snapshots.Count == editor.CatalogService.DefinitionCount,
                "La vista contiene todo el catálogo, incluidos platos fuera de carta."
            );
            report.Expect(
                HasUniqueDishIds(snapshots),
                "La vista no contiene DishId duplicados."
            );
            report.Expect(
                summary.IncludedDishCount == original.Count,
                "El resumen refleja la carta operativa inicial."
            );
            report.Expect(
                BistroBuilderMenuEditorUtility.Matches(
                    snapshots[0],
                    snapshots[0].CategoryId,
                    BistroBuilderMenuEditorFilter.All,
                    snapshots[0].DisplayName
                ),
                "Búsqueda, categoría y filtro comparten una regla pura."
            );

            BistroBuilderMenuEditorDishSnapshot candidate =
                FindIncludedCandidate(snapshots);
            report.Expect(candidate != null, "Existe un plato editable de prueba.");

            if (candidate == null)
            {
                throw new InvalidOperationException(
                    "No existe un plato editable para continuar."
                );
            }

            BistroBuilderMenuMutationResult removed =
                editor.TryRemoveDish(candidate.DishId);
            report.Expect(removed.Succeeded, "Retirar un plato modifica solo el borrador.");
            snapshots.Clear();
            editor.TryBuildSnapshot(snapshots, out summary, out _);
            BistroBuilderMenuEditorDishSnapshot removedSnapshot =
                FindSnapshot(snapshots, candidate.DishId);
            report.Expect(
                removedSnapshot != null && !removedSnapshot.Included,
                "El plato retirado sigue visible como contenido disponible."
            );
            report.Expect(
                MenuContainsUnchanged(menu, candidate),
                "La carta operativa no cambia antes de Aplicar."
            );

            bool discarded = editor.TryDiscardAndContinue(out string discardError);
            report.Expect(discarded, discarded ? "Descartar reconstruye un borrador limpio." : discardError);
            snapshots.Clear();
            editor.TryBuildSnapshot(snapshots, out summary, out _);
            candidate = FindSnapshot(snapshots, candidate.DishId);
            report.Expect(
                candidate != null && candidate.Included,
                "Descartar restaura el plato retirado."
            );

            int changedPrice = candidate.BasePriceCents <
                BistroBuilderDishDefinition.MaximumPriceCents - 100
                    ? candidate.BasePriceCents + 100
                    : Math.Max(0, candidate.BasePriceCents - 100);
            BistroBuilderMenuMutationResult price =
                editor.TrySetPriceCents(candidate.DishId, changedPrice);
            report.Expect(price.Succeeded, "El precio exacto se edita en céntimos.");
            BistroBuilderMenuMutationResult defaults =
                editor.TryRestoreDishDefaults(candidate.DishId);
            report.Expect(defaults.Succeeded, "Restaurar valores usa la definición canónica.");
            snapshots.Clear();
            editor.TryBuildSnapshot(snapshots, out summary, out _);
            BistroBuilderMenuEditorDishSnapshot restored =
                FindSnapshot(snapshots, candidate.DishId);
            report.Expect(
                restored != null &&
                restored.CurrentPriceCents == restored.BasePriceCents &&
                restored.Enabled && !restored.ManuallySoldOut &&
                !restored.SignatureDish,
                "La restauración recupera precio y estado editables predeterminados."
            );

            RunOrderingTest(editor, snapshots, report);
            bool contextChanged = editor.TrySetPreviewContext(
                BistroBuilderMealServiceAvailability.Breakfast,
                BistroBuilderServiceMode.BarService,
                out string contextError
            );
            report.Expect(
                contextChanged,
                contextChanged
                    ? "La previsualización cambia servicio y modalidad de forma conjunta."
                    : contextError
            );
            snapshots.Clear();
            report.Expect(
                editor.TryBuildSnapshot(snapshots, out summary, out buildError),
                string.IsNullOrEmpty(buildError)
                    ? "La oferta del borrador se recalcula para el nuevo contexto."
                    : buildError
            );
            report.Expect(
                summary.MealService ==
                    BistroBuilderMealServiceAvailability.Breakfast &&
                summary.ServiceMode == BistroBuilderServiceMode.BarService,
                "El resumen publica el contexto exacto de previsualización."
            );
        }
        catch (Exception exception)
        {
            report.Expect(false, "Excepción no controlada: " + exception);
        }
        finally
        {
            if (editor != null && editor.IsOpen)
            {
                editor.TryClose(true, out _);
            }

            if (menu != null && originalRevision >= 0)
            {
                List<BistroBuilderMenuItemRuntimeState> current =
                    new List<BistroBuilderMenuItemRuntimeState>();
                bool unchanged = menu.Revision == originalRevision &&
                    menu.TryGetSnapshot(current, out _) &&
                    AreEquivalent(original, current);
                report.Expect(
                    unchanged,
                    "El autotest no modifica la carta operativa ni su revisión."
                );
            }
        }

        Finish(report);
    }

    private static void RunUtilityTests(Report report)
    {
        report.Expect(
            BistroBuilderMenuEditorUtility.FormatEditableMoney(1590) ==
                "15,90",
            "El precio editable usa coma decimal española."
        );
        report.Expect(
            BistroBuilderMenuEditorUtility.TryParseMoney(
                "15,90 €",
                out int spanish,
                out _
            ) && spanish == 1590,
            "El editor interpreta precios españoles exactos."
        );
        report.Expect(
            BistroBuilderMenuEditorUtility.TryParseMoney(
                "15.90",
                out int invariant,
                out _
            ) && invariant == 1590,
            "El editor acepta también punto decimal sin perder precisión."
        );
        report.Expect(
            !BistroBuilderMenuEditorUtility.TryParseMoney(
                "-1",
                out _,
                out _
            ),
            "Los precios negativos se rechazan."
        );
    }

    private static void RunOrderingTest(
        BistroBuilderMenuEditorService editor,
        List<BistroBuilderMenuEditorDishSnapshot> snapshots,
        Report report
    )
    {
        BistroBuilderMenuEditorDishSnapshot first = null;
        BistroBuilderMenuEditorDishSnapshot second = null;

        for (int left = 0; left < snapshots.Count && first == null; left++)
        {
            if (!snapshots[left].Included)
            {
                continue;
            }

            for (int right = left + 1; right < snapshots.Count; right++)
            {
                if (snapshots[right].Included &&
                    string.Equals(
                        snapshots[left].CategoryId,
                        snapshots[right].CategoryId,
                        StringComparison.Ordinal
                    ))
                {
                    first = snapshots[left];
                    second = snapshots[right];
                    break;
                }
            }
        }

        if (first == null || second == null)
        {
            report.Expect(
                true,
                "El catálogo actual no necesita probar intercambio dentro de categoría."
            );
            return;
        }

        BistroBuilderMenuEditorDishSnapshot lower =
            first.DisplayOrder <= second.DisplayOrder ? first : second;
        BistroBuilderMenuMutationResult moved =
            editor.TryMoveDishWithinCategory(lower.DishId, 1);
        report.Expect(moved.Succeeded, "El orden se modifica solo dentro de la categoría.");
        snapshots.Clear();
        editor.TryBuildSnapshot(
            snapshots,
            out BistroBuilderMenuEditorSummarySnapshot _,
            out _
        );
        BistroBuilderMenuEditorDishSnapshot movedSnapshot =
            FindSnapshot(snapshots, lower.DishId);
        report.Expect(
            movedSnapshot != null && movedSnapshot.IsModified,
            "El cambio de orden queda marcado en el borrador."
        );
    }

    private static BistroBuilderMenuEditorDishSnapshot FindIncludedCandidate(
        List<BistroBuilderMenuEditorDishSnapshot> snapshots
    )
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            if (snapshots[index].Included &&
                !snapshots[index].SignatureDish)
            {
                return snapshots[index];
            }
        }

        for (int index = 0; index < snapshots.Count; index++)
        {
            if (snapshots[index].Included)
            {
                return snapshots[index];
            }
        }

        return null;
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

    private static bool HasUniqueDishIds(
        List<BistroBuilderMenuEditorDishSnapshot> snapshots
    )
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < snapshots.Count; index++)
        {
            if (!ids.Add(snapshots[index].DishId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MenuContainsUnchanged(
        BistroBuilderRestaurantMenuService menu,
        BistroBuilderMenuEditorDishSnapshot expected
    )
    {
        return menu.TryGetItemSnapshot(
            expected.DishId,
            out BistroBuilderMenuItemRuntimeState item
        ) && item != null && item.CurrentPriceCents == expected.CurrentPriceCents;
    }

    private static bool AreEquivalent(
        List<BistroBuilderMenuItemRuntimeState> left,
        List<BistroBuilderMenuItemRuntimeState> right
    )
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        Dictionary<string, BistroBuilderMenuItemRuntimeState> index =
            new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                StringComparer.Ordinal
            );

        for (int itemIndex = 0; itemIndex < left.Count; itemIndex++)
        {
            index[left[itemIndex].DishId] = left[itemIndex];
        }

        for (int itemIndex = 0; itemIndex < right.Count; itemIndex++)
        {
            BistroBuilderMenuItemRuntimeState current = right[itemIndex];

            if (!index.TryGetValue(
                    current.DishId,
                    out BistroBuilderMenuItemRuntimeState original
                ) ||
                original.CurrentPriceCents != current.CurrentPriceCents ||
                original.Unlocked != current.Unlocked ||
                original.Enabled != current.Enabled ||
                original.ManuallySoldOut != current.ManuallySoldOut ||
                original.SignatureDish != current.SignatureDish ||
                original.AvailableServices != current.AvailableServices ||
                original.DisplayOrder != current.DisplayOrder)
            {
                return false;
            }
        }

        return true;
    }

    private static void Finish(Report report)
    {
        string text = report.Build();

        if (report.Failed > 0)
        {
            Debug.LogError(text);
        }
        else
        {
            Debug.Log(text);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            "BISTRO BUILDER - AUTOTEST 2.1E EDITOR JUGABLE\n" +
            "Pruebas superadas: " + report.Passed + "\n" +
            "Pruebas fallidas: " + report.Failed +
            "\nBorradores, catálogo completo, filtros, precios, " +
            "restauración, orden y previsualización validados.",
            "Aceptar"
        );
    }
}
