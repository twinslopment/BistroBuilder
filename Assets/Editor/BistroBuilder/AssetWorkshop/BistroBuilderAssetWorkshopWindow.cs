using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using BistroBuilder.AssetStudioBB;
using BistroBuilder.AssetStudioBB.Editor;

/// <summary>
/// Ventana principal y orientada a usuarios básicos para llevar un asset
/// visual hasta un artículo jugable de Bistro Builder.
///
/// Unifica el recorrido de Asset Studio BB y la antigua Item Factory sin
/// mezclar sus responsabilidades internas: Asset Studio prepara el visual;
/// la Factory lo convierte en objeto funcional y lo registra en catálogo.
/// </summary>
public sealed class BistroBuilderAssetWorkshopWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Taller de Objetos 3D/Abrir Taller";

    private static readonly string[] ItemTypeLabels =
    {
        "Otro mueble sin función especial",
        "Mesa de restaurante",
        "Silla de restaurante",
        "Decoración",
        "Lámpara de suelo",
        "Equipamiento de cocina",
        "Equipamiento de servicio",
        "Elemento estructural"
    };

    private enum WorkshopItemType
    {
        GenericFurniture = 0,
        Table = 1,
        Chair = 2,
        Decoration = 3,
        FloorLamp = 4,
        KitchenEquipment = 5,
        ServiceEquipment = 6,
        Structural = 7
    }

    private sealed class SourceEntry
    {
        public GameObject Source;
        public string VariantId = string.Empty;
        public string VariantDisplayName = string.Empty;
        public float PriceMultiplier = 1f;
    }

    private sealed class CreationRequest
    {
        public SourceEntry SourceEntry;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public int Price;
        public BistroBuilderPlaceableFactorySettings Settings;
        public BistroBuilderPlaceableFactoryPlan Plan;
    }

    [SerializeField]
    private UnityEngine.Object primarySource;

    [SerializeField]
    private WorkshopItemType itemType =
        WorkshopItemType.Chair;

    [SerializeField]
    private string baseDisplayName = string.Empty;

    [SerializeField]
    private string descriptionTemplate = string.Empty;

    [SerializeField]
    private int basePurchasePrice = 60;

    [SerializeField]
    private int tableCapacity = 2;

    [SerializeField]
    private bool addToMainCatalog = true;

    [SerializeField]
    private bool runProjectHealthAfterCreation = true;

    [SerializeField]
    private bool preventDuplicateDisplayNames = true;

    [SerializeField]
    private bool canMove = true;

    [SerializeField]
    private bool canRotate = true;

    [SerializeField]
    private float rotationStepDegrees = 15f;

    [SerializeField]
    private float minimumClearance;

    [SerializeField]
    private bool generateColliderWhenMissing = true;

    [SerializeField]
    private float chairSeatHeightMeters;

    [SerializeField]
    private bool showTechnicalOptions;

    [SerializeField]
    private bool showCatalogMaintenance;

    private readonly List<SourceEntry> sourceEntries =
        new List<SourceEntry>();

    private readonly List<CreationRequest> creationRequests =
        new List<CreationRequest>();

    private AssetStudioBBFacade.Inspection studioInspection;
    private AssetStudioBBVariantSet variantSet;
    private Vector2 scrollPosition;
    private bool planIsCurrent;
    private string operationMessage = string.Empty;
    private MessageType operationMessageType = MessageType.None;

    [MenuItem(MenuPath, false, 100)]
    public static void OpenWindow()
    {
        BistroBuilderAssetWorkshopWindow window =
            GetWindow<BistroBuilderAssetWorkshopWindow>(
                "Taller de Objetos 3D"
            );

        window.minSize = new Vector2(720f, 650f);
        window.Show();

        if (window.primarySource == null)
        {
            window.CaptureCurrentSelection();
        }
    }

    private void OnEnable()
    {
        if (primarySource != null)
        {
            ResolvePrimarySource(false);
        }
        else
        {
            CaptureCurrentSelection();
        }
    }

    private void OnGUI()
    {
        DrawHeader();

        scrollPosition =
            EditorGUILayout.BeginScrollView(scrollPosition);

        DrawSourceStep();
        EditorGUILayout.Space(10f);
        DrawFunctionStep();
        EditorGUILayout.Space(10f);
        DrawCatalogStep();
        EditorGUILayout.Space(10f);
        DrawReviewStep();
        EditorGUILayout.Space(10f);
        DrawCatalogHealth();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField(
            "Crear un objeto para Bistro Builder",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Sigue los cuatro pasos. Selecciona el modelo 3D que hayas " +
            "preparado en Blender (por ejemplo, desde Meshy Bridge), " +
            "indica qué objeto es y el Taller hará la parte técnica por ti. " +
            "El modelo original nunca se modifica.",
            MessageType.Info
        );

        DrawQuickStatus();
    }

    private void DrawQuickStatus()
    {
        bool hasModel =
            sourceEntries.Count > 0 ||
            studioInspection != null ||
            variantSet != null;

        bool hasName = !string.IsNullOrWhiteSpace(baseDisplayName);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            hasModel ? "✓ Modelo" : "1 · Modelo",
            GUILayout.Width(120f)
        );
        EditorGUILayout.LabelField(
            hasModel ? "✓ Tipo" : "2 · Tipo",
            GUILayout.Width(120f)
        );
        EditorGUILayout.LabelField(
            hasName ? "✓ Ficha" : "3 · Ficha",
            GUILayout.Width(120f)
        );
        EditorGUILayout.LabelField(
            planIsCurrent ? "✓ Revisado" : "4 · Crear"
        );
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSourceStep()
    {
        EditorGUILayout.LabelField(
            "1. Elige el modelo 3D",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();

        UnityEngine.Object newSource =
            EditorGUILayout.ObjectField(
                "Modelo 3D",
                primarySource,
                typeof(UnityEngine.Object),
                false
            );

        if (EditorGUI.EndChangeCheck())
        {
            primarySource = newSource;
            ResolvePrimarySource(true);
        }

        if (GUILayout.Button("Usar el modelo seleccionado en Project", GUILayout.Height(30f)))
        {
            CaptureCurrentSelection();
        }

        if (studioInspection != null)
        {
            DrawAssetStudioInspection();
            return;
        }

        if (variantSet != null)
        {
            DrawVariantSetSummary();
            return;
        }

        if (sourceEntries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Todavía no hay ningún modelo seleccionado. En la ventana " +
                "Project de Unity selecciona el FBX, OBJ o prefab que sale " +
                "de Blender y pulsa el botón de arriba. También puedes " +
                "arrastrarlo al campo 'Modelo 3D'.",
                MessageType.Warning
            );
            return;
        }

        EditorGUILayout.HelpBox(
            sourceEntries.Count == 1
                ? "✓ Modelo detectado. Puedes continuar al paso 2."
                : "✓ Modelos detectados: " +
                  sourceEntries.Count +
                  ". Se crearán como artículos independientes.",
            MessageType.Info
        );

        int visible = Mathf.Min(6, sourceEntries.Count);
        for (int index = 0; index < visible; index++)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    sourceEntries[index].Source,
                    typeof(GameObject),
                    false
                );
            }
        }

        if (sourceEntries.Count > visible)
        {
            EditorGUILayout.LabelField(
                "... y " + (sourceEntries.Count - visible) + " más."
            );
        }
    }

    private void DrawAssetStudioInspection()
    {
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField(
            studioInspection.DisplayName,
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "Familia",
            studioInspection.Family +
            " / " +
            studioInspection.Subtype
        );

        EditorGUILayout.LabelField(
            "Medidas",
            string.Format(
                "{0:F3} × {1:F3} × {2:F3} m",
                studioInspection.DimensionsMeters.x,
                studioInspection.DimensionsMeters.z,
                studioInspection.DimensionsMeters.y
            )
        );

        EditorGUILayout.LabelField(
            "Variantes previstas",
            studioInspection.VariantCount.ToString()
        );

        if (studioInspection.SeatHeightMeters > 0f)
        {
            EditorGUILayout.LabelField(
                "Altura de asiento",
                studioInspection.SeatHeightMeters.ToString("F3") + " m"
            );
        }

        for (int index = 0;
             index < studioInspection.Validation.Count;
             index++)
        {
            AssetStudioBBFacade.PublicValidationMessage message =
                studioInspection.Validation[index];

            MessageType type = MessageType.Info;

            if (message.Severity ==
                AssetStudioBBFacade.PublicSeverity.Warning)
            {
                type = MessageType.Warning;
            }
            else if (message.Severity ==
                     AssetStudioBBFacade.PublicSeverity.Error)
            {
                type = MessageType.Error;
            }

            EditorGUILayout.HelpBox(message.Text, type);
        }

        using (new EditorGUI.DisabledScope(studioInspection.HasErrors))
        {
            if (GUILayout.Button(
                    "Preparar acabados y variantes",
                    GUILayout.Height(34f)
                ))
            {
                GenerateAssetStudioVariants();
            }
        }

        if (studioInspection.HasErrors)
        {
            EditorGUILayout.HelpBox(
                "La preparación está bloqueada hasta corregir los " +
                "errores marcados arriba.",
                MessageType.Error
            );
        }
    }

    private void DrawVariantSetSummary()
    {
        EditorGUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "✓ Hay " +
            sourceEntries.Count +
            " acabado(s) preparado(s). El Taller puede convertirlos " +
            "todos en artículos jugables en una sola operación.",
            MessageType.Info
        );

        for (int index = 0;
             index < sourceEntries.Count;
             index++)
        {
            SourceEntry entry = sourceEntries[index];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(entry.VariantDisplayName)
                    ? entry.Source.name
                    : entry.VariantDisplayName
            );

            EditorGUILayout.LabelField(
                "×" + entry.PriceMultiplier.ToString("F2"),
                GUILayout.Width(52f)
            );
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawFunctionStep()
    {
        EditorGUILayout.LabelField(
            "2. ¿Qué objeto es?",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();

        int newTypeIndex =
            EditorGUILayout.Popup(
                "Es un/una",
                (int)itemType,
                ItemTypeLabels
            );

        WorkshopItemType newType =
            (WorkshopItemType)newTypeIndex;

        if (newType != itemType)
        {
            itemType = newType;
            ApplyItemTypeDefaults();
        }

        DrawItemTypeExplanation();

        if (itemType == WorkshopItemType.Table)
        {
            tableCapacity =
                Mathf.Clamp(
                    EditorGUILayout.IntField(
                        "Plazas de la mesa",
                        tableCapacity
                    ),
                    1,
                    20
                );
        }

        if (EditorGUI.EndChangeCheck())
        {
            planIsCurrent = false;
        }
    }

    private void DrawItemTypeExplanation()
    {
        switch (itemType)
        {
            case WorkshopItemType.Chair:
                EditorGUILayout.HelpBox(
                    "El Taller la preparará automáticamente como una silla " +
                    "real del restaurante: se acercará y orientará a las " +
                    "mesas, mostrará la colocación válida y podrá moverse y " +
                    "girarse correctamente. No necesitas crear puntos ni " +
                    "componentes técnicos a mano.",
                    MessageType.Info
                );
                break;

            case WorkshopItemType.Table:
                EditorGUILayout.HelpBox(
                    "El Taller la preparará automáticamente como una mesa " +
                    "jugable, con sus plazas, zonas de aproximación, " +
                    "colocación y edición.",
                    MessageType.Info
                );
                break;

            case WorkshopItemType.GenericFurniture:
                EditorGUILayout.HelpBox(
                    "Para muebles que solo necesitan colocarse, moverse y " +
                    "girarse, sin comportamiento especial de silla o mesa.",
                    MessageType.None
                );
                break;

            default:
                EditorGUILayout.HelpBox(
                    "El Taller preparará la colocación básica y la ficha del " +
                    "catálogo. Si este tipo necesita comportamiento especial, " +
                    "la revisión te avisará antes de crear nada.",
                    MessageType.None
                );
                break;
        }
    }

    private void DrawCatalogStep()
    {
        EditorGUILayout.LabelField(
            "3. Cómo aparecerá en el catálogo",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();

        baseDisplayName =
            EditorGUILayout.TextField(
                "Nombre para el jugador",
                baseDisplayName
            );

        EditorGUILayout.LabelField("Descripción para el jugador");
        descriptionTemplate =
            EditorGUILayout.TextArea(
                descriptionTemplate,
                GUILayout.MinHeight(58f)
            );

        if (variantSet != null &&
            sourceEntries.Count > 1)
        {
            EditorGUILayout.HelpBox(
                "Se crearán todas las variantes a la vez. El Taller añadirá " +
                "automáticamente el nombre de cada acabado al nombre base. " +
                "Si quieres mencionarlo dentro de la descripción puedes usar " +
                "{variante}.",
                MessageType.None
            );
        }

        basePurchasePrice =
            Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    "Precio de compra",
                    basePurchasePrice
                )
            );

        addToMainCatalog =
            EditorGUILayout.Toggle(
                "Mostrar en el catálogo",
                addToMainCatalog
            );

        showTechnicalOptions =
            EditorGUILayout.Foldout(
                showTechnicalOptions,
                "Ajustes técnicos (normalmente no necesitas tocarlos)",
                true
            );

        if (showTechnicalOptions)
        {
            EditorGUI.indentLevel++;

            preventDuplicateDisplayNames =
                EditorGUILayout.Toggle(
                    "Evitar artículos duplicados",
                    preventDuplicateDisplayNames
                );

            runProjectHealthAfterCreation =
                EditorGUILayout.Toggle(
                    "Comprobar el proyecto al terminar",
                    runProjectHealthAfterCreation
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
                        "Grados por cada giro",
                        rotationStepDegrees
                    ),
                    1f,
                    180f
                );

            minimumClearance =
                Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "Espacio libre mínimo",
                        minimumClearance
                    )
                );

            generateColliderWhenMissing =
                EditorGUILayout.Toggle(
                    "Crear zona física si falta",
                    generateColliderWhenMissing
                );

            if (itemType == WorkshopItemType.Chair)
            {
                chairSeatHeightMeters =
                    Mathf.Max(
                        0f,
                        EditorGUILayout.FloatField(
                            "Altura del asiento (0 = automática)",
                            chairSeatHeightMeters
                        )
                    );
            }

            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
        {
            planIsCurrent = false;
        }
    }

    private void DrawReviewStep()
    {
        EditorGUILayout.LabelField(
            "4. Comprobar y crear",
            EditorStyles.boldLabel
        );

        bool canAnalyze =
            sourceEntries.Count > 0 &&
            studioInspection == null;

        using (new EditorGUI.DisabledScope(!canAnalyze))
        {
            if (GUILayout.Button(
                    "Comprobar que está listo",
                    GUILayout.Height(34f)
                ))
            {
                AnalyzeRequests();
            }
        }

        if (!string.IsNullOrWhiteSpace(operationMessage))
        {
            EditorGUILayout.HelpBox(
                operationMessage,
                operationMessageType
            );
        }

        if (!planIsCurrent)
        {
            EditorGUILayout.HelpBox(
                "Pulsa 'Comprobar que está listo'. Antes de crear nada, el " +
                "Taller revisará el modelo y te dirá claramente si puede " +
                "continuar o qué falta corregir.",
                MessageType.None
            );
            return;
        }

        int readyCount = 0;

        for (int index = 0;
             index < creationRequests.Count;
             index++)
        {
            CreationRequest request = creationRequests[index];
            BistroBuilderPlaceableFactoryPlan plan = request.Plan;

            if (plan == null)
            {
                continue;
            }

            MessageType type;
            string prefix;

            switch (plan.Status)
            {
                case BistroBuilderPlaceableFactoryPlanStatus.Ready:
                    type = MessageType.Info;
                    prefix = "✓ ";
                    readyCount++;
                    break;

                case BistroBuilderPlaceableFactoryPlanStatus
                    .AlreadyConfigured:
                    type = MessageType.Warning;
                    prefix = "↷ ";
                    break;

                default:
                    type = MessageType.Error;
                    prefix = "✕ ";
                    break;
            }

            StringBuilder text = new StringBuilder();
            text.Append(prefix);
            text.Append(request.DisplayName);
            text.Append(" · ");
            text.Append(request.Price);
            text.Append(" €\n");
            text.Append(plan.StatusMessage);

            if (showTechnicalOptions &&
                !string.IsNullOrWhiteSpace(plan.PrefabPath))
            {
                text.Append("\nPrefab: ");
                text.Append(plan.PrefabPath);
            }

            EditorGUILayout.HelpBox(text.ToString(), type);
        }

        using (new EditorGUI.DisabledScope(readyCount == 0))
        {
            if (GUILayout.Button(
                    readyCount == 1
                        ? "Crear en Bistro Builder"
                        : "Crear " + readyCount + " artículos en Bistro Builder",
                    GUILayout.Height(42f)
                ))
            {
                ExecuteReadyRequests(readyCount);
            }
        }
    }

    private void DrawCatalogHealth()
    {
        showCatalogMaintenance =
            EditorGUILayout.Foldout(
                showCatalogMaintenance,
                "Mantenimiento del catálogo",
                true
            );

        if (!showCatalogMaintenance)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Esta zona es de mantenimiento. Para crear muebles normalmente " +
            "no necesitas tocar nada aquí.",
            MessageType.None
        );

        BistroBuilderAssetWorkshopCatalogService.Health health =
            BistroBuilderAssetWorkshopCatalogService.Inspect();

        if (health.Catalog == null)
        {
            EditorGUILayout.HelpBox(
                "No se encuentra RestaurantPlaceableCatalog_Main.",
                MessageType.Error
            );
            return;
        }

        if (health.ProblemCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Catálogo correcto · " +
                health.ItemCount +
                " artículos · 0 problemas detectados.",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Artículos: " + health.ItemCount +
                " · referencias nulas: " + health.NullReferences +
                " · ItemId duplicados: " + health.DuplicateItemIds +
                " · nombres repetidos: " + health.DuplicateDisplayNames +
                " · prefabs ausentes: " + health.MissingPrefabs + ".",
                MessageType.Warning
            );
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Abrir catálogo"))
        {
            BistroBuilderAssetWorkshopCatalogService
                .SelectMainCatalog();
        }

        using (new EditorGUI.DisabledScope(
                   health.NullReferences == 0 &&
                   health.DuplicateItemIds == 0
               ))
        {
            if (GUILayout.Button("Corregir problemas seguros"))
            {
                BistroBuilderAssetWorkshopCatalogService
                    .RepairSafeIssues(out string message);

                operationMessage = message;
                operationMessageType = MessageType.Info;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void CaptureCurrentSelection()
    {
        primarySource = Selection.activeObject;

        if (Selection.objects != null &&
            Selection.objects.Length > 1)
        {
            sourceEntries.Clear();
            creationRequests.Clear();
            studioInspection = null;
            variantSet = null;
            planIsCurrent = false;
            operationMessage = string.Empty;

            List<GameObject> selected =
                BistroBuilderPlaceableFactoryEngine
                    .CollectSelectedSourceAssets();

            for (int index = 0;
                 index < selected.Count;
                 index++)
            {
                if (selected[index] == null)
                {
                    continue;
                }

                sourceEntries.Add(
                    new SourceEntry
                    {
                        Source = selected[index]
                    }
                );
            }

            return;
        }

        ResolvePrimarySource(true);
    }

    private void ResolvePrimarySource(bool applyDefaults)
    {
        sourceEntries.Clear();
        creationRequests.Clear();
        studioInspection = null;
        variantSet = null;
        planIsCurrent = false;
        operationMessage = string.Empty;

        if (primarySource == null)
        {
            return;
        }

        variantSet = primarySource as AssetStudioBBVariantSet;

        if (variantSet != null)
        {
            PopulateFromVariantSet(variantSet);

            if (applyDefaults)
            {
                if (string.IsNullOrWhiteSpace(baseDisplayName))
                {
                    baseDisplayName = variantSet.DisplayName;
                }

                GuessTypeFromText(
                    variantSet.Category + " " +
                    variantSet.Family + " " +
                    variantSet.Subtype
                );
            }

            return;
        }

        if (AssetStudioBBFacade.TryInspect(
                primarySource,
                out AssetStudioBBFacade.Inspection inspection
            ))
        {
            studioInspection = inspection;

            if (applyDefaults)
            {
                baseDisplayName = inspection.DisplayName;
                descriptionTemplate = inspection.Description;

                if (inspection.SeatHeightMeters > 0f)
                {
                    chairSeatHeightMeters =
                        inspection.SeatHeightMeters;
                }

                GuessTypeFromText(
                    inspection.Category + " " +
                    inspection.Family + " " +
                    inspection.Subtype
                );
            }

            return;
        }

        if (primarySource is GameObject directGameObject)
        {
            sourceEntries.Add(
                new SourceEntry
                {
                    Source = directGameObject
                }
            );
        }
        else
        {
            string primaryPath =
                AssetDatabase.GetAssetPath(primarySource);

            if (AssetDatabase.IsValidFolder(primaryPath))
            {
                string[] guids =
                    AssetDatabase.FindAssets(
                        "t:GameObject",
                        new[] { primaryPath }
                    );

                for (int index = 0;
                     index < guids.Length;
                     index++)
                {
                    string path =
                        AssetDatabase.GUIDToAssetPath(guids[index]);

                    if (!path.EndsWith(
                            ".prefab",
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        !path.EndsWith(
                            ".fbx",
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        !path.EndsWith(
                            ".obj",
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        continue;
                    }

                    GameObject candidate =
                        AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (candidate != null)
                    {
                        sourceEntries.Add(
                            new SourceEntry
                            {
                                Source = candidate
                            }
                        );
                    }
                }
            }
        }

        if (applyDefaults && sourceEntries.Count == 1)
        {
            baseDisplayName =
                HumanizeName(sourceEntries[0].Source.name);

            if (string.IsNullOrWhiteSpace(descriptionTemplate))
            {
                descriptionTemplate =
                    BuildSuggestedDescription();
            }
        }
    }

    private void PopulateFromVariantSet(
        AssetStudioBBVariantSet set
    )
    {
        if (set == null || set.Variants == null)
        {
            return;
        }

        for (int index = 0;
             index < set.Variants.Count;
             index++)
        {
            AssetStudioBBVariantSet.VariantEntry variant =
                set.Variants[index];

            if (variant == null || variant.VisualPrefab == null)
            {
                continue;
            }

            sourceEntries.Add(
                new SourceEntry
                {
                    Source = variant.VisualPrefab,
                    VariantId = variant.Id,
                    VariantDisplayName = variant.DisplayName,
                    PriceMultiplier = Mathf.Max(
                        0.01f,
                        variant.PriceMultiplier
                    )
                }
            );
        }
    }

    private void GenerateAssetStudioVariants()
    {
        try
        {
            AssetStudioBBFacade.Inspection previousInspection =
                studioInspection;

            AssetStudioBBVariantSet generated =
                AssetStudioBBFacade.GenerateVariants(primarySource);

            primarySource = generated;
            variantSet = generated;
            studioInspection = null;
            sourceEntries.Clear();
            PopulateFromVariantSet(generated);

            if (previousInspection != null)
            {
                if (!string.IsNullOrWhiteSpace(
                        previousInspection.DisplayName
                    ))
                {
                    baseDisplayName =
                        previousInspection.DisplayName;
                }

                if (!string.IsNullOrWhiteSpace(
                        previousInspection.Description
                    ))
                {
                    descriptionTemplate =
                        previousInspection.Description;
                }

                if (previousInspection.SeatHeightMeters > 0f)
                {
                    chairSeatHeightMeters =
                        previousInspection.SeatHeightMeters;
                }
            }

            operationMessage =
                "✓ Acabados preparados. Ya puedes continuar con el tipo " +
                "de objeto y su ficha de catálogo.";
            operationMessageType = MessageType.Info;
            planIsCurrent = false;

            Selection.activeObject = generated;
            EditorGUIUtility.PingObject(generated);
        }
        catch (Exception exception)
        {
            operationMessage = exception.Message;
            operationMessageType = MessageType.Error;
            Debug.LogException(exception);
        }
    }

    private void AnalyzeRequests()
    {
        creationRequests.Clear();
        operationMessage = string.Empty;

        if (sourceEntries.Count == 0)
        {
            planIsCurrent = false;
            return;
        }

        for (int index = 0;
             index < sourceEntries.Count;
             index++)
        {
            SourceEntry sourceEntry = sourceEntries[index];

            string displayName =
                BuildDisplayName(sourceEntry, index);

            string description =
                BuildDescription(sourceEntry);

            int price =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        basePurchasePrice *
                        Mathf.Max(
                            0.01f,
                            sourceEntry.PriceMultiplier
                        )
                    )
                );

            BistroBuilderPlaceableFactorySettings settings =
                BuildFactorySettings(
                    displayName,
                    description,
                    price,
                    index == sourceEntries.Count - 1
                );

            List<BistroBuilderPlaceableFactoryPlan> plans =
                BistroBuilderPlaceableFactoryEngine
                    .AnalyzeSelection(
                        new[] { sourceEntry.Source },
                        settings
                    );

            creationRequests.Add(
                new CreationRequest
                {
                    SourceEntry = sourceEntry,
                    DisplayName = displayName,
                    Description = description,
                    Price = price,
                    Settings = settings,
                    Plan = plans.Count > 0
                        ? plans[0]
                        : null
                }
            );
        }

        planIsCurrent = true;
    }

    private void ExecuteReadyRequests(int expectedReadyCount)
    {
        if (!EditorUtility.DisplayDialog(
                "Taller de Objetos 3D",
                "Se crearán " +
                expectedReadyCount +
                " artículo(s) nuevos listos para Bistro Builder.\n\n" +
                "El modelo original no se modificará y, si algo falla, " +
                "la operación deshará automáticamente los cambios.\n\n" +
                "¿Continuar?",
                "Crear",
                "Cancelar"
            ))
        {
            return;
        }

        int created = 0;
        int skipped = 0;
        int failed = 0;
        StringBuilder messages = new StringBuilder();

        for (int index = 0;
             index < creationRequests.Count;
             index++)
        {
            CreationRequest request = creationRequests[index];

            if (request.Plan == null ||
                request.Plan.Status !=
                    BistroBuilderPlaceableFactoryPlanStatus.Ready)
            {
                skipped++;
                continue;
            }

            BistroBuilderPlaceableFactoryBatchResult result =
                BistroBuilderPlaceableFactoryEngine.ExecutePlans(
                    new[] { request.Plan },
                    request.Settings
                );

            created += result.CreatedCount;
            skipped += result.SkippedCount;
            failed += result.FailedCount;

            for (int messageIndex = 0;
                 messageIndex < result.Messages.Count;
                 messageIndex++)
            {
                if (messages.Length > 0)
                {
                    messages.AppendLine();
                }

                messages.Append(result.Messages[messageIndex]);
            }
        }

        operationMessage =
            "Creados: " + created +
            " · Omitidos: " + skipped +
            " · Errores: " + failed;

        operationMessageType =
            failed > 0
                ? MessageType.Error
                : MessageType.Info;

        if (messages.Length > 0)
        {
            Debug.Log(
                "[Taller de Objetos 3D]\n" +
                messages
            );
        }

        planIsCurrent = false;
        Repaint();

        EditorUtility.DisplayDialog(
            "Taller de Objetos 3D",
            operationMessage +
            (failed == 0
                ? "\n\nLos artículos listos ya pueden probarse " +
                  "desde el catálogo."
                : "\n\nRevisa la Console para ver el detalle."),
            "Cerrar"
        );
    }

    private BistroBuilderPlaceableFactorySettings BuildFactorySettings(
        string displayName,
        string description,
        int price,
        bool isLastRequest
    )
    {
        BistroBuilderPlaceableFactorySettings settings =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = MapPreset(itemType),
                PurchasePrice = price,
                TableCapacity = Mathf.Clamp(tableCapacity, 1, 20),
                CanMove = canMove,
                CanRotate = canRotate,
                RotationStepDegrees = rotationStepDegrees,
                MinimumClearance = minimumClearance,
                GenerateColliderWhenMissing =
                    generateColliderWhenMissing,
                AddToMainCatalog = addToMainCatalog,
                RunProjectHealthAfterCreation =
                    runProjectHealthAfterCreation &&
                    isLastRequest,
                PreventDuplicateDisplayNames =
                    preventDuplicateDisplayNames,
                SeatHeightMeters =
                    itemType == WorkshopItemType.Chair
                        ? chairSeatHeightMeters
                        : 0f,
                SingleDisplayNameOverride = displayName,
                SingleDescriptionOverride = description
            };

        BistroBuilderPlaceableFactoryEngine
            .ApplyPresetCapabilities(settings);

        return settings;
    }

    private string BuildDisplayName(
        SourceEntry entry,
        int index
    )
    {
        string baseName =
            string.IsNullOrWhiteSpace(baseDisplayName)
                ? HumanizeName(entry.Source.name)
                : baseDisplayName.Trim();

        if (variantSet != null &&
            !string.IsNullOrWhiteSpace(
                entry.VariantDisplayName
            ))
        {
            return baseName +
                   " — " +
                   entry.VariantDisplayName.Trim();
        }

        if (sourceEntries.Count > 1 &&
            !string.IsNullOrWhiteSpace(entry.Source.name))
        {
            return HumanizeName(entry.Source.name);
        }

        return baseName;
    }

    private string BuildDescription(SourceEntry entry)
    {
        string description =
            string.IsNullOrWhiteSpace(descriptionTemplate)
                ? BuildSuggestedDescription()
                : descriptionTemplate.Trim();

        string variantName =
            entry != null
                ? entry.VariantDisplayName
                : string.Empty;

        if (string.IsNullOrWhiteSpace(variantName))
        {
            return description.Replace("{variante}", string.Empty)
                .Trim();
        }

        if (description.IndexOf(
                "{variante}",
                StringComparison.OrdinalIgnoreCase
            ) >= 0)
        {
            return ReplaceIgnoreCase(
                description,
                "{variante}",
                variantName.Trim()
            );
        }

        description = description.TrimEnd();
        description = description.TrimEnd('.', ';', ':', ',');

        return description +
               ". Acabado: " +
               variantName.Trim().ToLowerInvariant() +
               ".";
    }

    private string BuildSuggestedDescription()
    {
        switch (itemType)
        {
            case WorkshopItemType.Chair:
                return "Silla de restaurante preparada para el servicio.";

            case WorkshopItemType.Table:
                return "Mesa de restaurante preparada para el servicio.";

            case WorkshopItemType.Decoration:
                return "Elemento decorativo para el restaurante.";

            case WorkshopItemType.FloorLamp:
                return "Lámpara de suelo para el restaurante.";

            case WorkshopItemType.KitchenEquipment:
                return "Equipamiento para la cocina del restaurante.";

            case WorkshopItemType.ServiceEquipment:
                return "Equipamiento de apoyo para el servicio.";

            case WorkshopItemType.Structural:
                return "Elemento estructural del restaurante.";

            default:
                return "Mueble colocable del restaurante.";
        }
    }

    private void ApplyItemTypeDefaults()
    {
        canMove = true;
        canRotate = true;
        minimumClearance = 0f;
        generateColliderWhenMissing = true;

        switch (itemType)
        {
            case WorkshopItemType.Chair:
                rotationStepDegrees = 15f;
                basePurchasePrice =
                    basePurchasePrice <= 0
                        ? 60
                        : basePurchasePrice;
                break;

            default:
                rotationStepDegrees = 90f;
                break;
        }

        if (string.IsNullOrWhiteSpace(descriptionTemplate))
        {
            descriptionTemplate = BuildSuggestedDescription();
        }

        planIsCurrent = false;
    }

    private void GuessTypeFromText(string rawText)
    {
        string text =
            (rawText ?? string.Empty).ToLowerInvariant();

        WorkshopItemType guessed = itemType;

        if (ContainsAny(text, "chair", "silla", "seat", "seating"))
        {
            guessed = WorkshopItemType.Chair;
        }
        else if (ContainsAny(text, "table", "mesa"))
        {
            guessed = WorkshopItemType.Table;
        }
        else if (ContainsAny(text, "decor", "decoration", "planta"))
        {
            guessed = WorkshopItemType.Decoration;
        }
        else if (ContainsAny(text, "lamp", "light", "lámpara", "lampara"))
        {
            guessed = WorkshopItemType.FloorLamp;
        }
        else if (ContainsAny(text, "kitchen", "cocina"))
        {
            guessed = WorkshopItemType.KitchenEquipment;
        }

        if (guessed != itemType)
        {
            itemType = guessed;
            ApplyItemTypeDefaults();
        }
    }

    private static BistroBuilderPlaceableFactoryPreset MapPreset(
        WorkshopItemType type
    )
    {
        switch (type)
        {
            case WorkshopItemType.Table:
                return BistroBuilderPlaceableFactoryPreset.Table;

            case WorkshopItemType.Chair:
                return BistroBuilderPlaceableFactoryPreset.Chair;

            case WorkshopItemType.Decoration:
                return BistroBuilderPlaceableFactoryPreset.Decoration;

            case WorkshopItemType.FloorLamp:
                return BistroBuilderPlaceableFactoryPreset.FloorLamp;

            case WorkshopItemType.KitchenEquipment:
                return BistroBuilderPlaceableFactoryPreset.KitchenEquipment;

            case WorkshopItemType.ServiceEquipment:
                return BistroBuilderPlaceableFactoryPreset.ServiceEquipment;

            case WorkshopItemType.Structural:
                return BistroBuilderPlaceableFactoryPreset.Structural;

            default:
                return BistroBuilderPlaceableFactoryPreset.GenericFurniture;
        }
    }

    private static string HumanizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Artículo";
        }

        StringBuilder builder = new StringBuilder();
        char previous = '\0';

        for (int index = 0; index < rawName.Length; index++)
        {
            char character = rawName[index];

            if (character == '_' || character == '-')
            {
                if (builder.Length > 0 &&
                    builder[builder.Length - 1] != ' ')
                {
                    builder.Append(' ');
                }

                previous = character;
                continue;
            }

            if (index > 0 &&
                char.IsUpper(character) &&
                char.IsLower(previous) &&
                builder.Length > 0 &&
                builder[builder.Length - 1] != ' ')
            {
                builder.Append(' ');
            }

            builder.Append(character);
            previous = character;
        }

        string result = builder.ToString().Trim();

        if (string.IsNullOrWhiteSpace(result))
        {
            return "Artículo";
        }

        return char.ToUpperInvariant(result[0]) +
               (result.Length > 1
                   ? result.Substring(1)
                   : string.Empty);
    }

    private static string ReplaceIgnoreCase(
        string source,
        string token,
        string replacement
    )
    {
        if (string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(token))
        {
            return source ?? string.Empty;
        }

        int index = source.IndexOf(
            token,
            StringComparison.OrdinalIgnoreCase
        );

        if (index < 0)
        {
            return source;
        }

        return source.Substring(0, index) +
               replacement +
               source.Substring(index + token.Length);
    }

    private static bool ContainsAny(
        string text,
        params string[] values
    )
    {
        if (string.IsNullOrEmpty(text) || values == null)
        {
            return false;
        }

        for (int index = 0;
             index < values.Length;
             index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]) &&
                text.IndexOf(
                    values[index],
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
