using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado independiente de validación del sistema de asientos.
/// </summary>
public sealed class
    BistroBuilderSeatingFoundationValidationResult
{
    public int ErrorCount;

    public int WarningCount;

    public readonly List<string> Messages =
        new List<string>();

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "BISTRO BUILDER - VALIDACIÓN DE ASIENTOS"
        );

        builder.AppendLine(
            "Errores: " +
            ErrorCount
        );

        builder.AppendLine(
            "Advertencias: " +
            WarningCount
        );

        for (int index = 0;
             index < Messages.Count;
             index++)
        {
            builder.AppendLine(
                "- " +
                Messages[index]
            );
        }

        return builder.ToString();
    }
}

/// <summary>
/// Validador aislado de asientos y colocación asistida.
///
/// No modifica Project Health hasta que el sistema haya superado
/// compilación, instalación, autotest y prueba real en Play.
/// </summary>
public static class
    BistroBuilderSeatingFoundationValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Seating/" +
        "Validate Seating Foundation";

    private const string ChairPrefabPath =
        "Assets/Prefabs/Restaurant/Generated/Furniture/" +
        "SillaBistroDeMadera.prefab";

    private const string TablePrefabPath =
        "Assets/Prefabs/Restaurant/Furniture/Table_Basic.prefab";

    private const string ChairEditableDefinitionPath =
        "Assets/Data/Restaurant/EditMode/EditableDefinitions/" +
        "EditableObjectDefinition_SillaBistroDeMadera.asset";

    private const string ProfileAssetPath =
        "Assets/Data/Restaurant/Seating/SeatUseProfiles/" +
        "SeatUseProfile_StandardDiningChair.asset";

    private const string StandardsAssetPath =
        "Assets/Data/Restaurant/Seating/" +
        "RestaurantSeatingStandards.asset";

    private const string TableBasicConfigurationPath =
        "Assets/Data/Restaurant/Seating/TableConfigurations/" +
        "TableSeatingConfiguration_TableBasic2.asset";

    [MenuItem(MenuPath, false, 101)]
    private static void ValidateFromMenu()
    {
        BistroBuilderSeatingFoundationValidationResult result =
            ValidateCurrentProject();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(result.BuildReport());
        }
        else if (result.WarningCount > 0)
        {
            Debug.LogWarning(result.BuildReport());
        }
        else
        {
            Debug.Log(result.BuildReport());
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderSeatingFoundationValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderSeatingFoundationValidationResult result =
            new BistroBuilderSeatingFoundationValidationResult();

        ValidateDataAssets(result);
        ValidateChairEditableDefinition(result);
        ValidateChairPrefab(result);
        ValidateTablePrefab(result);
        ValidateScene(result);

        if (result.ErrorCount == 0)
        {
            result.Messages.Add(
                "OK: la base universal de asientos y snapping está completa."
            );
        }

        return result;
    }

    private static void ValidateDataAssets(
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        RestaurantSeatUseProfileDefinition profile =
            AssetDatabase.LoadAssetAtPath<
                RestaurantSeatUseProfileDefinition
            >(ProfileAssetPath);

        if (profile == null)
        {
            AddError(
                result,
                "No existe el perfil de silla estándar."
            );
        }
        else if (!profile.ValidateConfiguration(out string error))
        {
            AddError(result, error);
        }

        RestaurantSeatingStandardsDefinition standards =
            AssetDatabase.LoadAssetAtPath<
                RestaurantSeatingStandardsDefinition
            >(StandardsAssetPath);

        if (standards == null)
        {
            AddError(
                result,
                "No existe RestaurantSeatingStandards."
            );
        }
        else
        {
            int[] requiredRectangular =
            {
                2,
                4,
                6
            };

            for (int index = 0;
                 index < requiredRectangular.Length;
                 index++)
            {
                if (!standards.SupportsRectangularCapacity(
                        requiredRectangular[index]
                    ))
                {
                    AddError(
                        result,
                        "Falta la capacidad rectangular " +
                        requiredRectangular[index] +
                        "."
                    );
                }
            }

            ValidateRoundStandard(
                standards,
                4,
                1.00f,
                true,
                result
            );

            ValidateRoundStandard(
                standards,
                6,
                1.20f,
                true,
                result
            );

            ValidateRoundStandard(
                standards,
                8,
                1.50f,
                true,
                result
            );

            if (!standards.TryGetRoundTableStandard(
                    10,
                    out RestaurantRoundTableStandard tenStandard
                ))
            {
                AddError(
                    result,
                    "Falta la capacidad redonda de 10 clientes."
                );
            }
            else if (!tenStandard.DiameterIsApproved)
            {
                AddWarning(
                    result,
                    "La mesa redonda de 10 clientes está soportada, " +
                    "pero su diámetro sigue pendiente de aprobación."
                );
            }
        }

        RestaurantTableSeatingConfigurationDefinition tableDefinition =
            AssetDatabase.LoadAssetAtPath<
                RestaurantTableSeatingConfigurationDefinition
            >(TableBasicConfigurationPath);

        if (tableDefinition == null)
        {
            AddError(
                result,
                "No existe la configuración fija de Table_Basic."
            );
        }
    }


    private static void ValidateChairEditableDefinition(
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        RestaurantEditableObjectDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                RestaurantEditableObjectDefinition
            >(ChairEditableDefinitionPath);

        if (definition == null)
        {
            AddError(
                result,
                "No existe la definición editable de la silla."
            );

            return;
        }

        if (!definition.UsesCustomGridSize ||
            !Mathf.Approximately(
                definition.CustomGridSize,
                0.05f
            ))
        {
            AddError(
                result,
                "La silla necesita una cuadrícula específica de 0,05 m."
            );
        }

        if (!definition.UsesCustomRotationStep ||
            !Mathf.Approximately(
                definition.CustomRotationStepDegrees,
                15f
            ))
        {
            AddError(
                result,
                "La silla necesita un giro específico de 15 grados."
            );
        }
    }

    private static void ValidateChairPrefab(
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        GameObject chair =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                ChairPrefabPath
            );

        if (chair == null)
        {
            AddError(
                result,
                "No existe SillaBistroDeMadera.prefab."
            );

            return;
        }

        RestaurantSeat seat =
            chair.GetComponent<RestaurantSeat>();

        if (seat == null)
        {
            AddError(
                result,
                "La silla no contiene RestaurantSeat."
            );
        }
        else if (!seat.ValidateConfiguration(out string error))
        {
            AddError(result, error);
        }

        RestaurantOperationalClearanceSet clearanceSet =
            chair.GetComponent<
                RestaurantOperationalClearanceSet
            >();

        if (clearanceSet == null ||
            clearanceSet.ClearanceCount < 1)
        {
            AddError(
                result,
                "La silla no contiene su espacio operativo de " +
                "extracción y aproximación."
            );
        }

        if (chair.transform.Find(
                "OperationalMotionRoot/Visual"
            ) == null)
        {
            AddError(
                result,
                "Visual no está bajo OperationalMotionRoot."
            );
        }
    }

    private static void ValidateTablePrefab(
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        GameObject table =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                TablePrefabPath
            );

        if (table == null)
        {
            AddError(
                result,
                "No existe Table_Basic.prefab."
            );

            return;
        }

        RestaurantTableSeatingConfiguration configuration =
            table.GetComponent<
                RestaurantTableSeatingConfiguration
            >();

        if (configuration == null)
        {
            AddError(
                result,
                "Table_Basic no contiene su configuración fija " +
                "de plazas."
            );
        }
        else if (!configuration.ValidateConfiguration(
                out string error
            ))
        {
            AddError(result, error);
        }
    }

    private static void ValidateScene(
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            AddError(
                result,
                "No hay una escena guardada y cargada."
            );

            return;
        }

        RestaurantPlacementConstraintService constraintService =
            FindSceneComponent<
                RestaurantPlacementConstraintService
            >(scene);

        RestaurantOperationalClearanceConstraintRule clearanceRule =
            FindSceneComponent<
                RestaurantOperationalClearanceConstraintRule
            >(scene);

        RestaurantSeatRegistry seatRegistry =
            FindSceneComponent<RestaurantSeatRegistry>(scene);

        RestaurantSeatingPlacementConstraintRule seatingRule =
            FindSceneComponent<
                RestaurantSeatingPlacementConstraintRule
            >(scene);

        RestaurantSeatingTopologyService topologyService =
            FindSceneComponent<
                RestaurantSeatingTopologyService
            >(scene);

        RestaurantPlacementSnapVisualizer snapVisualizer =
            FindSceneComponent<
                RestaurantPlacementSnapVisualizer
            >(scene);

        RestaurantSeatingSnapProvider seatingSnapProvider =
            FindSceneComponent<
                RestaurantSeatingSnapProvider
            >(scene);

        RestaurantPlacementSnapService snapService =
            FindSceneComponent<
                RestaurantPlacementSnapService
            >(scene);

        RestaurantEditInteractionController interactionController =
            FindSceneComponent<
                RestaurantEditInteractionController
            >(scene);

        if (constraintService == null)
        {
            AddError(
                result,
                "GameSystems no contiene " +
                "RestaurantPlacementConstraintService."
            );
        }
        else
        {
            constraintService.RefreshRules();

            if (constraintService.RegisteredRuleCount < 2)
            {
                AddError(
                    result,
                    "El servicio modular no ha registrado las dos " +
                    "reglas iniciales."
                );
            }
        }

        if (clearanceRule == null)
        {
            AddError(
                result,
                "GameSystems no contiene la regla de espacio operativo."
            );
        }

        if (seatRegistry == null)
        {
            AddError(
                result,
                "GameSystems no contiene RestaurantSeatRegistry."
            );
        }

        if (seatingRule == null)
        {
            AddError(
                result,
                "GameSystems no contiene la regla silla-mesa."
            );
        }

        if (topologyService == null)
        {
            AddError(
                result,
                "GameSystems no contiene " +
                "RestaurantSeatingTopologyService."
            );
        }

        if (snapVisualizer == null)
        {
            AddError(
                result,
                "GameSystems no contiene " +
                "RestaurantPlacementSnapVisualizer."
            );
        }
        else if (snapVisualizer.MaximumIndicatorCount < 4)
        {
            AddError(
                result,
                "El visualizador de snapping no tiene un pool válido."
            );
        }

        if (seatingSnapProvider == null)
        {
            AddError(
                result,
                "GameSystems no contiene " +
                "RestaurantSeatingSnapProvider."
            );
        }
        else if (!seatingSnapProvider.ValidateConfiguration(
                out string snapProviderError
            ))
        {
            AddError(result, snapProviderError);
        }

        if (snapService == null)
        {
            AddError(
                result,
                "GameSystems no contiene " +
                "RestaurantPlacementSnapService."
            );
        }
        else
        {
            snapService.RefreshProviders();

            if (snapService.RegisteredProviderCount < 1)
            {
                AddError(
                    result,
                    "El servicio de snapping no ha registrado " +
                    "ningún proveedor."
                );
            }
        }

        if (interactionController == null)
        {
            AddError(
                result,
                "La escena no contiene " +
                "RestaurantEditInteractionController."
            );
        }
        else if (!object.ReferenceEquals(
                interactionController.PlacementSnapService,
                snapService
            ))
        {
            AddError(
                result,
                "RestaurantEditInteractionController no está " +
                "conectado al servicio universal de snapping."
            );
        }

        RestaurantTable[] tables =
            FindSceneComponents<RestaurantTable>(scene);

        for (int index = 0;
             index < tables.Length;
             index++)
        {
            RestaurantTable table = tables[index];

            RestaurantTableSeatingConfiguration configuration =
                table.GetComponent<
                    RestaurantTableSeatingConfiguration
                >();

            if (configuration == null)
            {
                AddError(
                    result,
                    table.name +
                    " no contiene una configuración fija de plazas."
                );

                continue;
            }

            if (!configuration.ValidateConfiguration(
                    out string error
                ))
            {
                AddError(result, error);
            }
        }
    }

    private static void ValidateRoundStandard(
        RestaurantSeatingStandardsDefinition standards,
        int capacity,
        float expectedDiameter,
        bool expectedApproved,
        BistroBuilderSeatingFoundationValidationResult result
    )
    {
        if (!standards.TryGetRoundTableStandard(
                capacity,
                out RestaurantRoundTableStandard standard
            ))
        {
            AddError(
                result,
                "Falta la capacidad redonda " +
                capacity +
                "."
            );

            return;
        }

        if (standard.DiameterIsApproved != expectedApproved ||
            !Mathf.Approximately(
                standard.DiameterMetres,
                expectedDiameter
            ))
        {
            AddError(
                result,
                "La mesa redonda de " +
                capacity +
                " clientes no conserva el estándar de " +
                expectedDiameter.ToString("0.00") +
                " m."
            );
        }
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = FindSceneComponents<T>(scene);

        return components.Length > 0
            ? components[0]
            : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            results.AddRange(
                roots[index].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
    }

    private static void AddError(
        BistroBuilderSeatingFoundationValidationResult result,
        string message
    )
    {
        result.ErrorCount++;
        result.Messages.Add("ERROR: " + message);
    }

    private static void AddWarning(
        BistroBuilderSeatingFoundationValidationResult result,
        string message
    )
    {
        result.WarningCount++;
        result.Messages.Add("AVISO: " + message);
    }
}
