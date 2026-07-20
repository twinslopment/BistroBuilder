using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Interfaz de simulación previa y ejecución de la fábrica universal.
///
/// Ningún asset se modifica hasta pulsar Crear y aceptar la
/// confirmación final.
/// </summary>
public sealed class BistroBuilderPlaceableFactoryWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Placeables/" +
        "Open Item Factory";

    [SerializeField]
    private BistroBuilderPlaceableFactoryPreset preset =
        BistroBuilderPlaceableFactoryPreset.GenericFurniture;

    [SerializeField]
    private int purchasePrice;

    [SerializeField]
    private int tableCapacity = 2;

    [SerializeField]
    private bool canMove = true;

    [SerializeField]
    private bool canRotate = true;

    [SerializeField]
    private float rotationStepDegrees = 90f;

    [SerializeField]
    private float minimumClearance;

    [SerializeField]
    private bool generateColliderWhenMissing = true;

    [SerializeField]
    private bool addToMainCatalog = true;

    [SerializeField]
    private bool runProjectHealthAfterCreation = true;

    [SerializeField]
    private string singleDisplayNameOverride = string.Empty;

    [SerializeField]
    private string singleDescriptionOverride = string.Empty;

    private readonly List<RestaurantAreaCapabilityDefinition>
        requiredCapabilities =
            new List<RestaurantAreaCapabilityDefinition>();

    private readonly List<GameObject> selectedSources =
        new List<GameObject>();

    private readonly List<BistroBuilderPlaceableFactoryPlan> plans =
        new List<BistroBuilderPlaceableFactoryPlan>();

    private Vector2 scrollPosition;

    private bool planIsCurrent;

    [MenuItem(MenuPath, false, 130)]
    public static void OpenWindow()
    {
        BistroBuilderPlaceableFactoryWindow window =
            GetWindow<
                BistroBuilderPlaceableFactoryWindow
            >(
                "Bistro Builder Item Factory"
            );

        window.minSize =
            new Vector2(680f, 580f);

        window.Show();
        window.RefreshSelection();
    }

    private void OnEnable()
    {
        Selection.selectionChanged +=
            HandleSelectionChanged;

        ApplyPresetDefaults();
        RefreshSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -=
            HandleSelectionChanged;
    }

    private void HandleSelectionChanged()
    {
        RefreshSelection();
        Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();

        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition
            );

        DrawSourceSelection();
        EditorGUILayout.Space(8f);
        DrawConfiguration();
        EditorGUILayout.Space(8f);
        DrawCapabilities();
        EditorGUILayout.Space(8f);
        DrawPlan();
        EditorGUILayout.Space(12f);
        DrawActions();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField(
            "Fábrica universal de artículos colocables",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Crea un nuevo prefab de juego sin modificar el modelo " +
            "o prefab visual seleccionado. Primero analiza y muestra " +
            "el plan; después ejecuta de forma atómica con rollback.",
            MessageType.Info
        );
    }

    private void DrawSourceSelection()
    {
        EditorGUILayout.LabelField(
            "1. Assets de origen",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "Selecciona en Project uno o varios prefabs, FBX, OBJ " +
            "o una carpeta que los contenga.",
            EditorStyles.wordWrappedLabel
        );

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(
                "Assets detectados",
                selectedSources.Count
            );
        }

        int maximumVisible =
            Mathf.Min(
                8,
                selectedSources.Count
            );

        for (int index = 0;
             index < maximumVisible;
             index++)
        {
            GameObject source =
                selectedSources[index];

            EditorGUILayout.ObjectField(
                source,
                typeof(GameObject),
                false
            );
        }

        if (selectedSources.Count > maximumVisible)
        {
            EditorGUILayout.LabelField(
                "... y " +
                (
                    selectedSources.Count -
                    maximumVisible
                ) +
                " más."
            );
        }

        if (GUILayout.Button(
                "Actualizar selección"
            ))
        {
            RefreshSelection();
        }
    }

    private void DrawConfiguration()
    {
        EditorGUILayout.LabelField(
            "2. Configuración compartida",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();

        BistroBuilderPlaceableFactoryPreset newPreset =
            (BistroBuilderPlaceableFactoryPreset)
            EditorGUILayout.EnumPopup(
                "Preset",
                preset
            );

        if (newPreset != preset)
        {
            preset =
                newPreset;

            ApplyPresetDefaults();
            planIsCurrent = false;
        }

        purchasePrice =
            Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    "Precio de compra",
                    purchasePrice
                )
            );

        canMove =
            EditorGUILayout.Toggle(
                "Se puede mover",
                canMove
            );

        canRotate =
            EditorGUILayout.Toggle(
                "Se puede rotar",
                canRotate
            );

        rotationStepDegrees =
            Mathf.Clamp(
                EditorGUILayout.FloatField(
                    "Paso de rotación",
                    rotationStepDegrees
                ),
                1f,
                180f
            );

        minimumClearance =
            Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "Separación mínima",
                    minimumClearance
                )
            );

        generateColliderWhenMissing =
            EditorGUILayout.Toggle(
                "Crear collider si falta",
                generateColliderWhenMissing
            );

        addToMainCatalog =
            EditorGUILayout.Toggle(
                "Añadir al catálogo principal",
                addToMainCatalog
            );

        runProjectHealthAfterCreation =
            EditorGUILayout.Toggle(
                "Ejecutar Project Health",
                runProjectHealthAfterCreation
            );

        if (preset ==
            BistroBuilderPlaceableFactoryPreset.Table)
        {
            tableCapacity =
                Mathf.Max(
                    1,
                    EditorGUILayout.IntField(
                        "Capacidad de la mesa",
                        tableCapacity
                    )
                );
        }

        if (selectedSources.Count == 1)
        {
            singleDisplayNameOverride =
                EditorGUILayout.TextField(
                    "Nombre visible",
                    singleDisplayNameOverride
                );

            EditorGUILayout.LabelField(
                "Descripción"
            );

            singleDescriptionOverride =
                EditorGUILayout.TextArea(
                    singleDescriptionOverride,
                    GUILayout.MinHeight(48f)
                );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "En procesamiento por lotes, el nombre y la " +
                "descripción se derivan automáticamente de cada asset.",
                MessageType.None
            );
        }

        if (EditorGUI.EndChangeCheck())
        {
            planIsCurrent = false;
        }

        DrawPresetNote();
    }

    private void DrawCapabilities()
    {
        EditorGUILayout.LabelField(
            "3. Capacidades de área",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Estas capacidades determinan en qué áreas puede " +
            "colocarse el artículo. Los valores se proponen según " +
            "el preset, pero puedes revisarlos antes de crear.",
            MessageType.None
        );

        for (int index = 0;
             index < requiredCapabilities.Count;
             index++)
        {
            EditorGUILayout.BeginHorizontal();

            RestaurantAreaCapabilityDefinition previous =
                requiredCapabilities[index];

            RestaurantAreaCapabilityDefinition current =
                (RestaurantAreaCapabilityDefinition)
                EditorGUILayout.ObjectField(
                    "Capacidad " + (index + 1),
                    previous,
                    typeof(
                        RestaurantAreaCapabilityDefinition
                    ),
                    false
                );

            if (!ReferenceEquals(previous, current))
            {
                requiredCapabilities[index] = current;
                planIsCurrent = false;
            }

            if (GUILayout.Button(
                    "Quitar",
                    GUILayout.Width(60f)
                ))
            {
                requiredCapabilities.RemoveAt(index);
                planIsCurrent = false;
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button(
                "Añadir capacidad"
            ))
        {
            requiredCapabilities.Add(null);
            planIsCurrent = false;
        }

        if (GUILayout.Button(
                "Restaurar capacidades del preset"
            ))
        {
            ApplyPresetDefaults();
            planIsCurrent = false;
        }
    }

    private void DrawPlan()
    {
        EditorGUILayout.LabelField(
            "4. Simulación previa",
            EditorStyles.boldLabel
        );

        if (!planIsCurrent)
        {
            EditorGUILayout.HelpBox(
                "La configuración ha cambiado. Pulsa Analizar " +
                "selección para actualizar el plan.",
                MessageType.Warning
            );
        }

        if (plans.Count == 0)
        {
            EditorGUILayout.LabelField(
                "Todavía no hay ningún plan."
            );

            return;
        }

        int readyCount = 0;
        int configuredCount = 0;
        int blockedCount = 0;

        for (int index = 0;
             index < plans.Count;
             index++)
        {
            BistroBuilderPlaceableFactoryPlan plan =
                plans[index];

            if (plan == null)
            {
                continue;
            }

            switch (plan.Status)
            {
                case BistroBuilderPlaceableFactoryPlanStatus.Ready:
                    readyCount++;
                    break;

                case BistroBuilderPlaceableFactoryPlanStatus
                    .AlreadyConfigured:
                    configuredCount++;
                    break;

                default:
                    blockedCount++;
                    break;
            }
        }

        EditorGUILayout.LabelField(
            "Listos: " +
            readyCount +
            " | Ya configurados: " +
            configuredCount +
            " | Bloqueados: " +
            blockedCount
        );

        for (int index = 0;
             index < plans.Count;
             index++)
        {
            DrawPlanEntry(
                plans[index]
            );
        }
    }

    private void DrawPlanEntry(
        BistroBuilderPlaceableFactoryPlan plan
    )
    {
        if (plan == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        MessageType messageType;

        switch (plan.Status)
        {
            case BistroBuilderPlaceableFactoryPlanStatus.Ready:
                messageType = MessageType.Info;
                break;

            case BistroBuilderPlaceableFactoryPlanStatus
                .AlreadyConfigured:
                messageType = MessageType.None;
                break;

            default:
                messageType = MessageType.Error;
                break;
        }

        EditorGUILayout.LabelField(
            plan.DisplayName,
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            plan.SourcePath,
            EditorStyles.miniLabel
        );

        EditorGUILayout.HelpBox(
            plan.StatusMessage,
            messageType
        );

        if (plan.Status ==
            BistroBuilderPlaceableFactoryPlanStatus.Ready)
        {
            EditorGUILayout.LabelField(
                "ItemId",
                plan.ItemId
            );

            EditorGUILayout.LabelField(
                "Prefab",
                plan.PrefabPath
            );

            EditorGUILayout.LabelField(
                "Definición editable",
                plan.EditableDefinitionPath
            );

            EditorGUILayout.LabelField(
                "Definición de catálogo",
                plan.ItemDefinitionPath
            );

            EditorGUILayout.LabelField(
                "Huella calculada",
                plan.LocalBounds.size.ToString("0.###")
            );

            EditorGUILayout.LabelField(
                "Collider",
                plan.WillGenerateCollider
                    ? "Se creará automáticamente"
                    : "Se conservará el existente"
            );
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Analizar selección",
                GUILayout.Height(34f)
            ))
        {
            Analyze();
        }

        bool hasReadyPlans =
            planIsCurrent &&
            plans.Exists(
                plan =>
                    plan != null &&
                    plan.Status ==
                        BistroBuilderPlaceableFactoryPlanStatus
                            .Ready
            );

        using (new EditorGUI.DisabledScope(
                   !hasReadyPlans ||
                   EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling
               ))
        {
            if (GUILayout.Button(
                    "Crear artículos listos",
                    GUILayout.Height(34f)
                ))
            {
                Execute();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void RefreshSelection()
    {
        selectedSources.Clear();

        selectedSources.AddRange(
            BistroBuilderPlaceableFactoryEngine
                .CollectSelectedSourceAssets()
        );

        plans.Clear();
        planIsCurrent = false;
    }

    private void Analyze()
    {
        if (selectedSources.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Selecciona en Project al menos un prefab, modelo " +
                "o carpeta compatible.",
                "Aceptar"
            );

            return;
        }

        BistroBuilderPlaceableFactorySettings settings =
            BuildSettings();

        plans.Clear();

        plans.AddRange(
            BistroBuilderPlaceableFactoryEngine
                .AnalyzeSelection(
                    selectedSources,
                    settings
                )
        );

        planIsCurrent = true;
        Repaint();
    }

    private void Execute()
    {
        if (!planIsCurrent)
        {
            Analyze();
        }

        int readyCount =
            plans.FindAll(
                plan =>
                    plan != null &&
                    plan.Status ==
                        BistroBuilderPlaceableFactoryPlanStatus
                            .Ready
            ).Count;

        if (readyCount == 0)
        {
            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se crearán " +
                readyCount +
                " artículo(s) nuevos.\n\n" +
                "Los assets de origen no se modificarán.\n" +
                "Cada fallo ejecutará rollback de sus archivos.",
                "Crear",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        BistroBuilderPlaceableFactoryBatchResult result =
            BistroBuilderPlaceableFactoryEngine.ExecutePlans(
                plans,
                BuildSettings()
            );

        StringBuilder details =
            new StringBuilder();

        details.AppendLine(
            result.BuildSummary()
        );

        for (int index = 0;
             index < result.Messages.Count;
             index++)
        {
            details.AppendLine();
            details.AppendLine(
                result.Messages[index]
            );
        }

        Debug.Log(
            "BISTRO BUILDER - RESULTADO DE FÁBRICA\n" +
            details
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildSummary() +
            "\n\nConsulta Console para ver el detalle.",
            "Aceptar"
        );

        RefreshSelection();
    }

    private BistroBuilderPlaceableFactorySettings BuildSettings()
    {
        BistroBuilderPlaceableFactorySettings settings =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = preset,
                PurchasePrice = purchasePrice,
                TableCapacity = tableCapacity,
                CanMove = canMove,
                CanRotate = canRotate,
                RotationStepDegrees = rotationStepDegrees,
                MinimumClearance = minimumClearance,
                GenerateColliderWhenMissing =
                    generateColliderWhenMissing,
                AddToMainCatalog =
                    addToMainCatalog,
                RunProjectHealthAfterCreation =
                    runProjectHealthAfterCreation,
                SingleDisplayNameOverride =
                    singleDisplayNameOverride,
                SingleDescriptionOverride =
                    singleDescriptionOverride
            };

        for (int index = 0;
             index < requiredCapabilities.Count;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                requiredCapabilities[index];

            if (capability != null &&
                !settings.RequiredCapabilities.Contains(capability))
            {
                settings.RequiredCapabilities.Add(capability);
            }
        }

        return settings;
    }

    private void ApplyPresetDefaults()
    {
        BistroBuilderPlaceableFactorySettings settings =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = preset
            };

        BistroBuilderPlaceableFactoryEngine
            .ApplyPresetCapabilities(settings);

        requiredCapabilities.Clear();

        requiredCapabilities.AddRange(
            settings.RequiredCapabilities
        );

        switch (preset)
        {
            case BistroBuilderPlaceableFactoryPreset.Table:
                canMove = true;
                canRotate = true;
                rotationStepDegrees = 90f;
                minimumClearance = 0f;
                break;

            case BistroBuilderPlaceableFactoryPreset.Structural:
                canMove = true;
                canRotate = true;
                rotationStepDegrees = 90f;
                minimumClearance = 0f;
                break;

            default:
                canMove = true;
                canRotate = true;
                rotationStepDegrees = 90f;
                minimumClearance = 0f;
                break;
        }
    }

    private void DrawPresetNote()
    {
        switch (preset)
        {
            case BistroBuilderPlaceableFactoryPreset.Table:
                EditorGUILayout.HelpBox(
                    "Este preset añade RestaurantTable, capacidad, " +
                    "CustomerApproachPoint, WaiterServicePoint y " +
                    "TableStateView cuando existe Renderer.",
                    MessageType.Info
                );
                break;

            case BistroBuilderPlaceableFactoryPreset.Chair:
            case BistroBuilderPlaceableFactoryPreset.FloorLamp:
            case BistroBuilderPlaceableFactoryPreset
                .KitchenEquipment:
            case BistroBuilderPlaceableFactoryPreset
                .ServiceEquipment:
                EditorGUILayout.HelpBox(
                    "Se creará el artículo universal y su categoría. " +
                    "El componente funcional específico se añadirá " +
                    "cuando exista el sistema operativo correspondiente; " +
                    "no se inventarán componentes provisionales.",
                    MessageType.Info
                );
                break;

            default:
                EditorGUILayout.HelpBox(
                    "El artículo utilizará únicamente el núcleo " +
                    "universal de colocación.",
                    MessageType.None
                );
                break;
        }
    }
}
