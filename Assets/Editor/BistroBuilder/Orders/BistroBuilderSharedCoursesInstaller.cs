using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente y acumulativo de BistroBuilder 367F.
///
/// Conserva la escena mediante backup binario y restaura también el perfil de
/// composición si una validación posterior falla.
/// </summary>
public static class BistroBuilderSharedCoursesInstaller
{
    public const string DefaultProfilePath =
        "Assets/BistroBuilder/Configuration/Orders/" +
        "BB_Default_Order_Composition_367F.asset";

    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Install or Repair 367F Shared Dishes and Courses";

    [MenuItem(MenuPath, false, 240)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 367F.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador.",
                "Aceptar"
            );
            return;
        }

        string absoluteScenePath = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);
        BistroBuilderOrderCompositionProfile profile = null;
        bool profileCreated = false;
        string profileJsonBackup = string.Empty;

        try
        {
            GameObject gameSystems =
                BistroBuilderCanonicalOrderIntegrationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );
            }

            OrderSystem orderSystem =
                gameSystems.GetComponent<OrderSystem>();
            BistroBuilderRestaurantMenuService menu =
                gameSystems.GetComponent<BistroBuilderRestaurantMenuService>();
            BistroBuilderCanonicalOrderService canonical =
                gameSystems.GetComponent<BistroBuilderCanonicalOrderService>();
            BistroBuilderCanonicalOrderIntegrationService integration =
                gameSystems.GetComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            BistroBuilderOrderLineExecutionService execution =
                gameSystems.GetComponent<BistroBuilderOrderLineExecutionService>();
            BistroBuilderCustomerDiningService dining =
                gameSystems.GetComponent<BistroBuilderCustomerDiningService>();

            if (orderSystem == null || menu == null || canonical == null ||
                integration == null || execution == null || dining == null ||
                !integration.IndividualLineExecutionEnabled)
            {
                throw new InvalidOperationException(
                    "367F requiere 367E completamente instalado y validado."
                );
            }

            EnsureAssetFolders();

            profile = AssetDatabase.LoadAssetAtPath<
                BistroBuilderOrderCompositionProfile
            >(DefaultProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    BistroBuilderOrderCompositionProfile
                >();
                ConfigureDefaultProfile(profile);
                AssetDatabase.CreateAsset(profile, DefaultProfilePath);
                profileCreated = true;
            }
            else
            {
                profileJsonBackup = EditorJsonUtility.ToJson(profile);

                if (!profile.TryValidate(out _))
                {
                    ConfigureDefaultProfile(profile);
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            if (!profile.TryValidate(out string profileError))
            {
                throw new InvalidOperationException(profileError);
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar platos compartidos y pases BistroBuilder 367F"
            );

            BistroBuilderOrderCompositionService composition =
                gameSystems.GetComponent<BistroBuilderOrderCompositionService>();

            if (composition == null)
            {
                composition = Undo.AddComponent<
                    BistroBuilderOrderCompositionService
                >(gameSystems);
            }

            SerializedObject compositionSerialized =
                new SerializedObject(composition);
            RequireProperty(compositionSerialized, "menuService")
                .objectReferenceValue = menu;
            RequireProperty(compositionSerialized, "compositionProfile")
                .objectReferenceValue = profile;
            compositionSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(composition);

            BistroBuilderCourseAndSharingService courses =
                gameSystems.GetComponent<BistroBuilderCourseAndSharingService>();

            if (courses == null)
            {
                courses = Undo.AddComponent<
                    BistroBuilderCourseAndSharingService
                >(gameSystems);
            }

            SerializedObject coursesSerialized =
                new SerializedObject(courses);
            RequireProperty(coursesSerialized, "orderSystem")
                .objectReferenceValue = orderSystem;
            RequireProperty(coursesSerialized, "canonicalOrderService")
                .objectReferenceValue = canonical;
            RequireProperty(coursesSerialized, "compositionService")
                .objectReferenceValue = composition;
            RequireProperty(coursesSerialized, "customerDiningService")
                .objectReferenceValue = dining;
            coursesSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(courses);

            SerializedObject integrationSerialized =
                new SerializedObject(integration);
            RequireProperty(integrationSerialized, "orderCompositionService")
                .objectReferenceValue = composition;
            RequireProperty(integrationSerialized, "courseAndSharingService")
                .objectReferenceValue = courses;
            RequireProperty(
                integrationSerialized,
                "courseAndSharingExecutionEnabled"
            ).boolValue = true;
            integrationSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(integration);

            SerializedObject diningSerialized = new SerializedObject(dining);
            SerializedProperty stagger = RequireProperty(
                diningSerialized,
                "perCustomerEatingDurationOffsetSeconds"
            );

            if (float.IsNaN(stagger.floatValue) ||
                float.IsInfinity(stagger.floatValue) ||
                stagger.floatValue <= 0f)
            {
                stagger.floatValue = 0.75f;
            }

            diningSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dining);

            KitchenSystem[] kitchens =
                BistroBuilderIndividualDishFlowValidator
                    .FindSceneObjects<KitchenSystem>(scene);

            if (kitchens.Length == 0)
            {
                throw new InvalidOperationException(
                    "No se encontró ninguna cocina operativa."
                );
            }

            for (int index = 0; index < kitchens.Length; index++)
            {
                KitchenSystem kitchen = kitchens[index];
                Undo.RecordObject(kitchen, "Enlazar cocina con 367F");
                SerializedObject kitchenSerialized =
                    new SerializedObject(kitchen);
                RequireProperty(kitchenSerialized, "canonicalOrderService")
                    .objectReferenceValue = canonical;
                kitchenSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(kitchen);
            }

            if (!canonical.RebuildRuntimeIndex(out string canonicalError))
            {
                throw new InvalidOperationException(canonicalError);
            }

            if (!dining.RebuildRuntimeIndex(out string diningError))
            {
                throw new InvalidOperationException(diningError);
            }

            if (!courses.RebuildRuntimeIndex(out string courseError))
            {
                throw new InvalidOperationException(courseError);
            }

            if (!composition.ValidateConfiguration(out string compositionError))
            {
                throw new InvalidOperationException(compositionError);
            }

            if (!courses.ValidateConfiguration(out courseError))
            {
                throw new InvalidOperationException(courseError);
            }

            if (!integration.ValidateConfiguration(out string integrationError))
            {
                throw new InvalidOperationException(integrationError);
            }

            for (int index = 0; index < kitchens.Length; index++)
            {
                if (!kitchens[index].ValidateConfiguration(
                        out string kitchenError
                    ))
                {
                    throw new InvalidOperationException(kitchenError);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException(
                    "Unity no pudo guardar la escena tras instalar 367F."
                );
            }

            BistroBuilderSharedCoursesValidationResult result =
                BistroBuilderSharedCoursesValidator.ValidateCurrentScene();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "BISTRO BUILDER - INSTALACIÓN 367F\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                result.BuildReport(),
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllBytes(absoluteScenePath, sceneBackup);

                if (profileCreated)
                {
                    AssetDatabase.DeleteAsset(DefaultProfilePath);
                }
                else if (profile != null &&
                         !string.IsNullOrEmpty(profileJsonBackup))
                {
                    EditorJsonUtility.FromJsonOverwrite(
                        profileJsonBackup,
                        profile
                    );
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                }

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
                "La instalación 367F falló y se restauró la escena.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ConfigureDefaultProfile(
        BistroBuilderOrderCompositionProfile profile
    )
    {
        SerializedObject serialized = new SerializedObject(profile);
        SerializedProperty policy = RequireProperty(
            serialized,
            "coordinationPolicy"
        );
        policy.enumValueIndex =
            (int)BistroBuilderCourseCoordinationPolicy.PerTable;

        SerializedProperty rules = RequireProperty(serialized, "rules");
        rules.arraySize = 2;

        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            true,
            1,
            BistroBuilderOrderLineCompositionMode.SharedAllCustomers,
            0,
            2
        );

        ConfigureRule(
            rules.GetArrayElementAtIndex(1),
            true,
            2,
            BistroBuilderOrderLineCompositionMode.IndividualPerCustomer,
            1,
            2
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRule(
        SerializedProperty rule,
        bool enabled,
        int courseIndex,
        BistroBuilderOrderLineCompositionMode mode,
        int menuOffset,
        int sharedGroupSize
    )
    {
        RequireRelative(rule, "enabled").boolValue = enabled;
        RequireRelative(rule, "courseIndex").intValue = courseIndex;
        RequireRelative(rule, "compositionMode").enumValueIndex = (int)mode;
        RequireRelative(rule, "menuDisplayOffset").intValue = menuOffset;
        RequireRelative(rule, "sharedGroupSize").intValue = sharedGroupSize;
    }

    private static void EnsureAssetFolders()
    {
        EnsureFolder("Assets", "BistroBuilder");
        EnsureFolder("Assets/BistroBuilder", "Configuration");
        EnsureFolder("Assets/BistroBuilder/Configuration", "Orders");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(
                serializedObject.targetObject.GetType().Name,
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
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(propertyName);
        }

        return property;
    }
}
