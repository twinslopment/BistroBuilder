using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro de los contratos, formato y migración 2.1F.
/// </summary>
public static class BistroBuilderMenuPreparation21FSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1F Preparation Self-Test";

    [MenuItem(MenuPath, false, 182)]
    private static void RunFromMenu()
    {
        TestReport report = new TestReport();
        GameObject root = null;

        try
        {
            TestParsing(report);
            TestRuntimeState(report);

            root = new GameObject("BB_2_1F_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            BistroBuilderMenuStateV2ToV3Migration migration =
                root.AddComponent<BistroBuilderMenuStateV2ToV3Migration>();
            TestMigration(migration, report);
        }
        catch (Exception exception)
        {
            report.Fail("Excepción no controlada: " + exception);
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        string finalReport = report.BuildReport();

        if (report.Failed > 0)
        {
            Debug.LogError(finalReport);
        }
        else
        {
            Debug.Log(finalReport);
        }

        EditorUtility.DisplayDialog("Bistro Builder", finalReport, "Aceptar");
    }

    private static void TestParsing(TestReport report)
    {
        report.Check(
            BistroBuilderMenuEditorUtility.TryParsePreparationDifficulty(
                "7",
                out int difficulty,
                out _
            ) && difficulty == 7,
            "Dificultad 1-10 parseada."
        );
        report.Check(
            !BistroBuilderMenuEditorUtility.TryParsePreparationDifficulty(
                "11",
                out _,
                out _
            ),
            "Dificultad fuera de rango rechazada."
        );
        report.Check(
            BistroBuilderMenuEditorUtility.TryParsePreparationDuration(
                "12:30",
                out int seconds,
                out _
            ) && seconds == 750,
            "Duración mm:ss parseada."
        );
        report.Check(
            BistroBuilderMenuEditorUtility.TryParsePreparationDuration(
                "2,5",
                out seconds,
                out _
            ) && seconds == 150,
            "Minutos decimales parseados."
        );
        report.Check(
            BistroBuilderMenuEditorUtility.FormatPreparationDuration(750) ==
                "12:30",
            "Duración editable formateada sin pérdida."
        );
    }

    private static void TestRuntimeState(TestReport report)
    {
        BistroBuilderMenuItemRuntimeState inherited =
            new BistroBuilderMenuItemRuntimeState(
                "dish_test",
                1000,
                true,
                true,
                false,
                false,
                BistroBuilderMealServiceAvailability.All,
                0
            );
        report.Check(
            inherited.InheritsPreparationFromCatalog &&
            inherited.TryValidateStructure(out _),
            "Estado histórico 0/0 conserva herencia válida."
        );

        BistroBuilderMenuItemRuntimeState configured =
            new BistroBuilderMenuItemRuntimeState(
                "dish_test",
                1000,
                true,
                true,
                false,
                false,
                BistroBuilderMealServiceAvailability.All,
                0,
                8,
                900
            );
        BistroBuilderMenuItemRuntimeState clone = configured.Clone();
        report.Check(
            configured.TryValidateStructure(out _) &&
            clone.PreparationDifficulty == 8 &&
            clone.BasePreparationSeconds == 900,
            "Configuración explícita se valida y clona."
        );

        BistroBuilderMenuItemRuntimeState invalid =
            new BistroBuilderMenuItemRuntimeState(
                "dish_test",
                1000,
                true,
                true,
                false,
                false,
                BistroBuilderMealServiceAvailability.All,
                0,
                0,
                300
            );
        report.Check(
            !invalid.TryValidateStructure(out _),
            "Mezcla de herencia y valor explícito rechazada."
        );
    }

    private static void TestMigration(
        BistroBuilderMenuStateV2ToV3Migration migration,
        TestReport report
    )
    {
        BistroBuilderMenuSaveDataV2 legacy = new BistroBuilderMenuSaveDataV2
        {
            schemaVersion = 2,
            activeRestaurantId =
                BistroBuilderRestaurantMenuCollectionService
                    .DefaultRestaurantId,
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>
            {
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId =
                        BistroBuilderRestaurantMenuCollectionService
                            .DefaultRestaurantId,
                    revision = 4,
                    items = new List<BistroBuilderMenuItemSaveData>
                    {
                        new BistroBuilderMenuItemSaveData
                        {
                            dishId = "dish_test",
                            currentPriceCents = 1250,
                            unlocked = true,
                            enabled = true,
                            availableServices =
                                (int)BistroBuilderMealServiceAvailability.All,
                            displayOrder = 0
                        }
                    }
                }
            }
        };

        bool migrated = migration.TryMigrate(
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy)),
            out byte[] payload,
            out _
        );
        BistroBuilderMenuSaveDataV3 current = migrated
            ? JsonUtility.FromJson<BistroBuilderMenuSaveDataV3>(
                Encoding.UTF8.GetString(payload)
            )
            : null;
        report.Check(
            migrated && current != null && current.schemaVersion == 3,
            "Migración consecutiva v2 -> v3 completada."
        );
        report.Check(
            current != null &&
            current.restaurants[0].items[0].preparationDifficulty == 0 &&
            current.restaurants[0].items[0].basePreparationSeconds == 0,
            "La migración conserva herencia del catálogo sin inventar datos."
        );
        report.Check(
            current != null &&
            current.restaurants[0].items[0].currentPriceCents == 1250 &&
            current.restaurants[0].revision == 4,
            "La migración conserva precio y revisión históricos."
        );
    }

    private sealed class TestReport
    {
        private readonly List<string> failures = new List<string>();
        public int Passed { get; private set; }
        public int Failed => failures.Count;

        public void Check(bool condition, string description)
        {
            if (condition)
            {
                Passed++;
            }
            else
            {
                Fail(description);
            }
        }

        public void Fail(string description)
        {
            failures.Add(description ?? "Fallo sin descripción.");
        }

        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine(
                "BISTRO BUILDER - AUTOTEST 2.1F PREPARACIÓN CONFIGURABLE"
            );
            builder.AppendLine("Pruebas superadas: " + Passed);
            builder.AppendLine("Pruebas fallidas: " + Failed);

            for (int index = 0; index < failures.Count; index++)
            {
                builder.Append("- FALLO: ");
                builder.AppendLine(failures[index]);
            }

            return builder.ToString().TrimEnd();
        }
    }
}
