using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest no destructivo del Taller de Objetos 3D.
///
/// Además de revisar contratos existentes, crea temporalmente una silla
/// visual mínima, la pasa por la Factory con el preset Chair, comprueba
/// la funcionalidad generada y elimina todos los assets de prueba.
/// </summary>
public static class BistroBuilderAssetWorkshopSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Taller de Objetos 3D/Autotest";

    private const string StandardChairProfilePath =
        "Assets/Data/Restaurant/Seating/SeatUseProfiles/" +
        "SeatUseProfile_StandardDiningChair.asset";

    private const string TemporaryFolder =
        "Assets/Temp/BistroBuilder/AssetWorkshopSelfTest";

    [MenuItem(MenuPath, false, 101)]
    public static void RunFromMenu()
    {
        int passed = 0;
        List<string> failures = new List<string>();
        List<string> createdPaths = new List<string>();

        try
        {
            RunStaticChecks(ref passed, failures);
            RunChairFactoryCheck(ref passed, failures, createdPaths);
        }
        catch (Exception exception)
        {
            failures.Add(
                "Excepción inesperada: " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            Cleanup(createdPaths);
        }

        string summary =
            "Correctos: " + passed +
            "\nFallos: " + failures.Count;

        if (failures.Count == 0)
        {
            Debug.Log(
                "[Taller de Objetos 3D] Autotest superado. " +
                summary.Replace("\n", " · ")
            );

            EditorUtility.DisplayDialog(
                "Taller de Objetos 3D · Autotest",
                summary +
                "\n\nLa ruta de silla funcional y el catálogo " +
                "están preparados.",
                "Cerrar"
            );
            return;
        }

        string details = string.Join("\n- ", failures);

        Debug.LogError(
            "[Taller de Objetos 3D] Autotest con fallos.\n- " +
            details
        );

        EditorUtility.DisplayDialog(
            "Taller de Objetos 3D · Autotest",
            summary +
            "\n\n- " +
            details,
            "Cerrar"
        );
    }

    private static void RunStaticChecks(
        ref int passed,
        ICollection<string> failures
    )
    {
        Check(
            AssetDatabase.LoadAssetAtPath<
                RestaurantSeatUseProfileDefinition
            >(StandardChairProfilePath) != null,
            "Existe el perfil standard_dining_chair.",
            ref passed,
            failures
        );

        BistroBuilderPlaceableFactorySettings chairSettings =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = BistroBuilderPlaceableFactoryPreset.Chair,
                RotationStepDegrees = 15f
            };

        BistroBuilderPlaceableFactoryEngine
            .ApplyPresetCapabilities(chairSettings);

        bool hasCustomerSeating = false;

        for (int index = 0;
             index < chairSettings.RequiredCapabilities.Count;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                chairSettings.RequiredCapabilities[index];

            if (capability != null &&
                string.Equals(
                    capability.CapabilityId,
                    "customer_seating",
                    StringComparison.Ordinal
                ))
            {
                hasCustomerSeating = true;
                break;
            }
        }

        Check(
            hasCustomerSeating,
            "El preset Silla exige customer_seating.",
            ref passed,
            failures
        );

        BistroBuilderAssetWorkshopCatalogService.Health catalogHealth =
            BistroBuilderAssetWorkshopCatalogService.Inspect();

        Check(
            catalogHealth.Catalog != null,
            "Existe RestaurantPlaceableCatalog_Main.",
            ref passed,
            failures
        );

        if (catalogHealth.Catalog != null)
        {
            Check(
                catalogHealth.NullReferences == 0,
                "El catálogo no contiene referencias nulas.",
                ref passed,
                failures
            );

            Check(
                catalogHealth.DuplicateItemIds == 0,
                "El catálogo no contiene ItemId duplicados.",
                ref passed,
                failures
            );

            Check(
                catalogHealth.MissingPrefabs == 0,
                "Todos los artículos del catálogo tienen prefab.",
                ref passed,
                failures
            );
        }
    }

    private static void RunChairFactoryCheck(
        ref int passed,
        ICollection<string> failures,
        ICollection<string> createdPaths
    )
    {
        EnsureFolder(TemporaryFolder);

        string token =
            Guid.NewGuid().ToString("N").Substring(0, 8);

        string sourcePath =
            TemporaryFolder +
            "/WorkshopChairSource_" +
            token +
            ".prefab";

        GameObject sourceRoot =
            new GameObject("WorkshopChairSource_" + token);

        try
        {
            GameObject body =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            body.name = "ChairBody";
            body.transform.SetParent(sourceRoot.transform, false);
            body.transform.localPosition =
                new Vector3(0f, 0.43f, 0f);
            body.transform.localScale =
                new Vector3(0.48f, 0.86f, 0.50f);

            GameObject sourcePrefab =
                PrefabUtility.SaveAsPrefabAsset(
                    sourceRoot,
                    sourcePath
                );

            if (sourcePrefab == null)
            {
                failures.Add(
                    "No se pudo crear el prefab visual temporal."
                );
                return;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceRoot);
        }

        createdPaths.Add(sourcePath);
        AssetDatabase.ImportAsset(
            sourcePath,
            ImportAssetOptions.ForceUpdate
        );

        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

        Check(
            source != null,
            "Se crea una fuente visual temporal.",
            ref passed,
            failures
        );

        if (source == null)
        {
            return;
        }

        BistroBuilderPlaceableFactorySettings settings =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = BistroBuilderPlaceableFactoryPreset.Chair,
                PurchasePrice = 1,
                CanMove = true,
                CanRotate = true,
                RotationStepDegrees = 15f,
                MinimumClearance = 0f,
                GenerateColliderWhenMissing = true,
                AddToMainCatalog = false,
                RunProjectHealthAfterCreation = false,
                PreventDuplicateDisplayNames = true,
                SeatHeightMeters = 0.46f,
                SingleDisplayNameOverride =
                    "Workshop Chair SelfTest " + token,
                SingleDescriptionOverride =
                    "Silla temporal del autotest."
            };

        BistroBuilderPlaceableFactoryEngine
            .ApplyPresetCapabilities(settings);

        List<BistroBuilderPlaceableFactoryPlan> plans =
            BistroBuilderPlaceableFactoryEngine.AnalyzeSelection(
                new[] { source },
                settings
            );

        Check(
            plans.Count == 1,
            "La Factory genera un único plan de silla.",
            ref passed,
            failures
        );

        if (plans.Count != 1)
        {
            return;
        }

        BistroBuilderPlaceableFactoryPlan plan = plans[0];

        Check(
            plan.Status ==
                BistroBuilderPlaceableFactoryPlanStatus.Ready,
            "El plan de silla queda Ready.",
            ref passed,
            failures
        );

        if (plan.Status !=
            BistroBuilderPlaceableFactoryPlanStatus.Ready)
        {
            failures.Add(plan.StatusMessage);
            return;
        }

        BistroBuilderPlaceableFactoryBatchResult batch =
            BistroBuilderPlaceableFactoryEngine.ExecutePlans(
                plans,
                settings
            );

        for (int index = 0;
             index < batch.CreatedAssets.Count;
             index++)
        {
            createdPaths.Add(batch.CreatedAssets[index]);
        }

        Check(
            batch.FailedCount == 0 &&
            batch.CreatedCount == 1,
            "La Factory crea la silla temporal sin errores.",
            ref passed,
            failures
        );

        GameObject generatedPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                plan.PrefabPath
            );

        Check(
            generatedPrefab != null,
            "Existe el prefab funcional generado.",
            ref passed,
            failures
        );

        if (generatedPrefab == null)
        {
            return;
        }

        RestaurantSeat seat =
            generatedPrefab.GetComponent<RestaurantSeat>();

        Check(
            seat != null,
            "El prefab contiene RestaurantSeat.",
            ref passed,
            failures
        );

        Check(
            seat != null &&
            seat.ValidateConfiguration(out _),
            "RestaurantSeat supera ValidateConfiguration.",
            ref passed,
            failures
        );

        Check(
            generatedPrefab.transform.Find("PlacementAnchor") != null,
            "Existe PlacementAnchor.",
            ref passed,
            failures
        );

        Transform motionRoot =
            generatedPrefab.transform.Find("OperationalMotionRoot");

        Check(
            motionRoot != null,
            "Existe OperationalMotionRoot.",
            ref passed,
            failures
        );

        Check(
            motionRoot != null &&
            motionRoot.Find("Visual") != null,
            "El visual queda bajo OperationalMotionRoot.",
            ref passed,
            failures
        );

        Check(
            motionRoot != null &&
            motionRoot.Find("SeatPoint") != null,
            "Existe SeatPoint dentro de OperationalMotionRoot.",
            ref passed,
            failures
        );

        Check(
            generatedPrefab.transform.Find("AssociationPoint") != null,
            "Existe AssociationPoint.",
            ref passed,
            failures
        );

        Check(
            generatedPrefab.transform.Find("CustomerApproachPoint") != null,
            "Existe CustomerApproachPoint.",
            ref passed,
            failures
        );

        RestaurantOperationalClearanceSet clearance =
            generatedPrefab.GetComponent<
                RestaurantOperationalClearanceSet
            >();

        Check(
            clearance != null &&
            clearance.ClearanceCount == 1,
            "La silla incluye su espacio operativo de retirada.",
            ref passed,
            failures
        );

        RestaurantEditableObjectDefinition editableDefinition =
            AssetDatabase.LoadAssetAtPath<
                RestaurantEditableObjectDefinition
            >(plan.EditableDefinitionPath);

        Check(
            editableDefinition != null,
            "Existe la definición editable de la silla.",
            ref passed,
            failures
        );

        if (editableDefinition != null)
        {
            SerializedObject serialized =
                new SerializedObject(editableDefinition);

            SerializedProperty customGrid =
                serialized.FindProperty("customGridSize");
            SerializedProperty usesGrid =
                serialized.FindProperty("useCustomGridSize");
            SerializedProperty rotation =
                serialized.FindProperty("customRotationStepDegrees");

            Check(
                usesGrid != null &&
                usesGrid.boolValue &&
                customGrid != null &&
                Mathf.Abs(customGrid.floatValue - 0.05f) < 0.001f,
                "La silla usa cuadrícula fina de 0,05 m.",
                ref passed,
                failures
            );

            Check(
                rotation != null &&
                Mathf.Abs(rotation.floatValue - 15f) < 0.001f,
                "La silla rota en pasos de 15°.",
                ref passed,
                failures
            );
        }
    }

    private static void Cleanup(ICollection<string> createdPaths)
    {
        List<string> paths = new List<string>(createdPaths);
        paths.Sort((left, right) => right.Length.CompareTo(left.Length));

        for (int index = 0;
             index < paths.Count;
             index++)
        {
            string path = paths[index];

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            AssetDatabase.DeleteAsset(path);
        }

        if (AssetDatabase.IsValidFolder(TemporaryFolder))
        {
            AssetDatabase.DeleteAsset(TemporaryFolder);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int index = 1;
             index < parts.Length;
             index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[index]
                );
            }

            current = next;
        }
    }

    private static void Check(
        bool condition,
        string description,
        ref int passed,
        ICollection<string> failures
    )
    {
        if (condition)
        {
            passed++;
            return;
        }

        failures.Add(description);
    }
}
