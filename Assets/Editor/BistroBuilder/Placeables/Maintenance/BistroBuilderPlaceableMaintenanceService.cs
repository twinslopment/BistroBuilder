using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Analiza, repara y actualiza artículos colocables existentes.
///
/// Las reparaciones automáticas se limitan a cambios seguros:
/// - referencias entre definición, prefab y definición editable;
/// - InstanceId vacío en prefab;
/// - PlacementAnchor explícito cuando falta;
/// - PositionReference interno;
/// - catálogo principal normalizado;
/// - miniaturas generadas.
///
/// Nunca cambia automáticamente un ItemId ni sustituye un icono manual.
/// </summary>
public static class BistroBuilderPlaceableMaintenanceService
{
    private const string MainCatalogPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    private const string PlacementAnchorName =
        "PlacementAnchor";

    /// <summary>
    /// Analiza todos los artículos y el catálogo sin modificar assets.
    /// </summary>
    public static BistroBuilderPlaceableMaintenanceReport
        AnalyzeProject()
    {
        BistroBuilderPlaceableMaintenanceReport report =
            new BistroBuilderPlaceableMaintenanceReport();

        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        report.Definitions.AddRange(definitions);

        Dictionary<string, List<RestaurantPlaceableItemDefinition>>
            byItemId =
                new Dictionary<
                    string,
                    List<RestaurantPlaceableItemDefinition>
                >(
                    StringComparer.Ordinal
                );

        Dictionary<
            RestaurantEditableObjectDefinition,
            int
        > editableUsage =
            CountEditableDefinitionUsage(definitions);

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            string itemId =
                definition.ItemId;

            if (!byItemId.TryGetValue(
                    itemId,
                    out List<RestaurantPlaceableItemDefinition>
                        sameIdDefinitions
                ))
            {
                sameIdDefinitions =
                    new List<RestaurantPlaceableItemDefinition>();

                byItemId.Add(
                    itemId,
                    sameIdDefinitions
                );
            }

            sameIdDefinitions.Add(definition);

            AnalyzeDefinition(
                report,
                definition,
                editableUsage
            );
        }

        foreach (
            KeyValuePair<
                string,
                List<RestaurantPlaceableItemDefinition>
            > pair in byItemId
        )
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value.Count <= 1)
            {
                continue;
            }

            for (int index = 0;
                 index < pair.Value.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition duplicate =
                    pair.Value[index];

                report.Add(
                    BistroBuilderPlaceableMaintenanceSeverity.Error,
                    "BB-MAINT-ID-001",
                    duplicate.name +
                    " comparte ItemId '" +
                    pair.Key +
                    "' con otro artículo.",
                    "El ItemId es una identidad estable y requiere " +
                    "una decisión manual antes de modificarlo.",
                    duplicate,
                    AssetDatabase.GetAssetPath(duplicate),
                    false
                );
            }
        }

        AnalyzeMainCatalog(
            report,
            definitions
        );

        SortFindings(report.Findings);

        return report;
    }

    /// <summary>
    /// Repara todas las incidencias estructurales seguras y genera
    /// miniaturas faltantes. Cada artículo dispone de rollback propio.
    /// </summary>
    public static BistroBuilderPlaceableMaintenanceResult
        RepairSafeIssuesAndGenerateMissingThumbnails()
    {
        BistroBuilderPlaceableMaintenanceResult result =
            new BistroBuilderPlaceableMaintenanceResult();

        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        Dictionary<
            RestaurantEditableObjectDefinition,
            int
        > editableUsage =
            CountEditableDefinitionUsage(definitions);

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            bool changed =
                RepairSingleDefinition(
                    definition,
                    editableUsage,
                    true,
                    out string message
                );

            result.Messages.Add(message);

            if (message.StartsWith(
                    "ERROR:",
                    StringComparison.Ordinal
                ))
            {
                result.FailedCount++;
            }
            else if (changed)
            {
                result.ChangedCount++;
            }
            else
            {
                result.PreservedCount++;
            }
        }

        try
        {
            bool catalogChanged =
                NormalizeMainCatalog(
                    definitions,
                    out string catalogMessage
                );

            result.Messages.Add(
                catalogMessage
            );

            if (catalogChanged)
            {
                result.ChangedCount++;
            }
        }
        catch (Exception exception)
        {
            result.FailedCount++;

            result.Messages.Add(
                "ERROR: catálogo principal: " +
                exception.Message
            );

            Debug.LogException(exception);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ScheduleProjectHealth();

        return result;
    }

    /// <summary>
    /// Carga un borrador editable desde una definición existente.
    /// </summary>
    public static BistroBuilderPlaceableEditDraft
        CreateDraft(
            RestaurantPlaceableItemDefinition definition
        )
    {
        BistroBuilderPlaceableEditDraft draft =
            new BistroBuilderPlaceableEditDraft
            {
                Definition = definition
            };

        if (definition == null)
        {
            return draft;
        }

        draft.DisplayName =
            definition.DisplayName;

        draft.Description =
            definition.Description ?? string.Empty;

        draft.Category =
            definition.Category;

        draft.PurchasePrice =
            definition.PurchasePrice;

        RestaurantEditableObjectDefinition editableDefinition =
            definition.EditableDefinition;

        if (editableDefinition != null)
        {
            draft.CanMove =
                editableDefinition.CanMove;

            draft.CanRotate =
                editableDefinition.CanRotate;

            draft.UseCustomGridSize =
                editableDefinition.UsesCustomGridSize;

            draft.CustomGridSize =
                editableDefinition.CustomGridSize;

            draft.UseCustomRotationStep =
                editableDefinition.UsesCustomRotationStep;

            draft.RotationStepDegrees =
                editableDefinition.CustomRotationStepDegrees;
        }

        if (definition.Prefab != null)
        {
            RestaurantPlacementFootprint footprint =
                definition.Prefab.GetComponent<
                    RestaurantPlacementFootprint
                >();

            if (footprint != null)
            {
                draft.MinimumClearance =
                    footprint.MinimumClearance;
            }

            RestaurantAreaMember areaMember =
                definition.Prefab.GetComponent<
                    RestaurantAreaMember
                >();

            if (areaMember != null)
            {
                IReadOnlyList<
                    RestaurantAreaCapabilityDefinition
                > capabilities =
                    areaMember.RequiredCapabilities;

                for (int index = 0;
                     index < capabilities.Count;
                     index++)
                {
                    RestaurantAreaCapabilityDefinition capability =
                        capabilities[index];

                    if (capability != null &&
                        !draft.RequiredCapabilities.Contains(
                            capability
                        ))
                    {
                        draft.RequiredCapabilities.Add(
                            capability
                        );
                    }
                }
            }
        }

        draft.RegenerateThumbnail =
            definition.CatalogIcon == null ||
            BistroBuilderCatalogThumbnailService
                .IsGeneratedIcon(
                    definition.CatalogIcon
                );

        return draft;
    }

    /// <summary>
    /// Describe los cambios del borrador sin modificar assets.
    /// </summary>
    public static List<string> PreviewDraftChanges(
        BistroBuilderPlaceableEditDraft draft
    )
    {
        List<string> changes =
            new List<string>();

        if (draft == null ||
            draft.Definition == null)
        {
            changes.Add(
                "No hay un artículo cargado."
            );

            return changes;
        }

        RestaurantPlaceableItemDefinition definition =
            draft.Definition;

        if (!string.Equals(
                definition.DisplayName,
                draft.DisplayName?.Trim(),
                StringComparison.Ordinal
            ))
        {
            changes.Add(
                "Nombre visible: '" +
                definition.DisplayName +
                "' → '" +
                draft.DisplayName?.Trim() +
                "'."
            );
        }

        if (!string.Equals(
                definition.Description ?? string.Empty,
                draft.Description ?? string.Empty,
                StringComparison.Ordinal
            ))
        {
            changes.Add(
                "Actualizar descripción."
            );
        }

        if (definition.Category != draft.Category)
        {
            changes.Add(
                "Categoría: " +
                definition.Category +
                " → " +
                draft.Category +
                "."
            );
        }

        if (definition.PurchasePrice !=
            Mathf.Max(0, draft.PurchasePrice))
        {
            changes.Add(
                "Precio: " +
                definition.PurchasePrice +
                " → " +
                Mathf.Max(0, draft.PurchasePrice) +
                "."
            );
        }

        RestaurantEditableObjectDefinition editable =
            definition.EditableDefinition;

        if (editable == null)
        {
            changes.Add(
                "La definición editable falta y no se puede " +
                "crear automáticamente sin una decisión de ruta."
            );
        }
        else
        {
            if (editable.CanMove != draft.CanMove)
            {
                changes.Add(
                    "Cambiar permiso de movimiento."
                );
            }

            if (editable.CanRotate != draft.CanRotate)
            {
                changes.Add(
                    "Cambiar permiso de rotación."
                );
            }

            if (editable.UsesCustomGridSize !=
                draft.UseCustomGridSize ||
                !Mathf.Approximately(
                    editable.CustomGridSize,
                    Mathf.Max(0.01f, draft.CustomGridSize)
                ))
            {
                changes.Add(
                    "Actualizar configuración de cuadrícula."
                );
            }

            if (editable.UsesCustomRotationStep !=
                draft.UseCustomRotationStep ||
                !Mathf.Approximately(
                    editable.CustomRotationStepDegrees,
                    Mathf.Clamp(
                        draft.RotationStepDegrees,
                        1f,
                        180f
                    )
                ))
            {
                changes.Add(
                    "Actualizar paso de rotación."
                );
            }
        }

        if (definition.Prefab == null)
        {
            changes.Add(
                "El artículo no tiene prefab; no puede actualizarse " +
                "su configuración espacial."
            );
        }
        else
        {
            RestaurantPlacementFootprint footprint =
                definition.Prefab.GetComponent<
                    RestaurantPlacementFootprint
                >();

            if (footprint == null ||
                !Mathf.Approximately(
                    footprint.MinimumClearance,
                    Mathf.Max(0f, draft.MinimumClearance)
                ))
            {
                changes.Add(
                    "Actualizar separación mínima."
                );
            }

            if (!CapabilitiesMatch(
                    definition.Prefab.GetComponent<
                        RestaurantAreaMember
                    >(),
                    draft.RequiredCapabilities
                ))
            {
                changes.Add(
                    "Actualizar capacidades de área requeridas."
                );
            }
        }

        if (draft.RegenerateThumbnail)
        {
            changes.Add(
                definition.CatalogIcon == null
                    ? "Generar miniatura de catálogo."
                    : "Regenerar miniatura administrada."
            );
        }

        if (changes.Count == 0)
        {
            changes.Add(
                "No hay cambios pendientes."
            );
        }

        return changes;
    }

    /// <summary>
    /// Aplica un borrador con copia de seguridad de los assets
    /// modificados y rollback automático ante cualquier excepción.
    /// </summary>
    public static bool TryApplyDraft(
        BistroBuilderPlaceableEditDraft draft,
        out string message
    )
    {
        if (draft == null ||
            draft.Definition == null)
        {
            message =
                "No hay un artículo válido cargado.";

            return false;
        }

        RestaurantPlaceableItemDefinition definition =
            draft.Definition;

        if (string.IsNullOrWhiteSpace(
                draft.DisplayName
            ))
        {
            message =
                "El nombre visible no puede estar vacío.";

            return false;
        }

        if (definition.EditableDefinition == null)
        {
            message =
                "El artículo no tiene definición editable. " +
                "Ejecuta primero el análisis para decidir su reparación.";

            return false;
        }

        List<RestaurantPlaceableItemDefinition> allDefinitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        int sharedEditableUsage = 0;

        for (int index = 0;
             index < allDefinitions.Count;
             index++)
        {
            if (allDefinitions[index] != null &&
                ReferenceEquals(
                    allDefinitions[index].EditableDefinition,
                    definition.EditableDefinition
                ))
            {
                sharedEditableUsage++;
            }
        }

        if (sharedEditableUsage > 1)
        {
            message =
                "La definición editable está compartida por " +
                sharedEditableUsage +
                " artículos. No se modificará automáticamente.";

            return false;
        }

        if (definition.Prefab == null)
        {
            message =
                "El artículo no tiene prefab.";

            return false;
        }

        string itemPath =
            AssetDatabase.GetAssetPath(definition);

        string editablePath =
            AssetDatabase.GetAssetPath(
                definition.EditableDefinition
            );

        string prefabPath =
            AssetDatabase.GetAssetPath(
                definition.Prefab.gameObject
            );

        AssetFileBackupSet backups =
            new AssetFileBackupSet();

        backups.Capture(itemPath);
        backups.Capture(editablePath);
        backups.Capture(prefabPath);
        backups.Capture(MainCatalogPath);

        try
        {
            ApplyItemMetadata(
                definition,
                draft
            );

            ApplyEditableMetadata(
                definition.EditableDefinition,
                definition.ItemId,
                draft
            );

            ApplyPrefabDraft(
                definition,
                draft,
                prefabPath
            );

            NormalizeMainCatalog(
                BistroBuilderCatalogThumbnailService
                    .LoadAllDefinitions(),
                out string catalogMessage
            );

            string thumbnailMessage =
                "Miniatura conservada.";

            if (draft.RegenerateThumbnail)
            {
                BistroBuilderCatalogThumbnailService
                    .ThumbnailResult thumbnailResult =
                        BistroBuilderCatalogThumbnailService
                            .GenerateAndAssign(
                                definition,
                                false,
                                true
                            );

                thumbnailMessage =
                    thumbnailResult.Message;

                if (!thumbnailResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        thumbnailMessage
                    );
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ScheduleProjectHealth();

            message =
                "Artículo actualizado correctamente.\n" +
                catalogMessage +
                "\n" +
                thumbnailMessage;

            return true;
        }
        catch (Exception exception)
        {
            backups.Restore();

            Debug.LogException(exception);

            message =
                "No se pudo actualizar el artículo. " +
                "Se restauraron sus archivos.\n" +
                exception.Message;

            return false;
        }
    }

    /// <summary>
    /// Devuelve la definición asociada a la selección actual cuando
    /// sea un asset de artículo, un prefab colocable o uno de sus
    /// componentes.
    /// </summary>
    public static RestaurantPlaceableItemDefinition
        ResolveDefinitionFromSelection()
    {
        UnityEngine.Object selected =
            Selection.activeObject;

        if (selected == null)
        {
            return null;
        }

        RestaurantPlaceableItemDefinition directDefinition =
            selected as RestaurantPlaceableItemDefinition;

        if (directDefinition != null)
        {
            return directDefinition;
        }

        GameObject selectedGameObject =
            selected as GameObject;

        if (selectedGameObject != null)
        {
            RestaurantPlaceableObject placeable =
                selectedGameObject.GetComponentInChildren<
                    RestaurantPlaceableObject
                >(true);

            if (placeable != null)
            {
                return placeable.ItemDefinition;
            }
        }

        Component selectedComponent =
            selected as Component;

        if (selectedComponent != null)
        {
            RestaurantPlaceableObject placeable =
                selectedComponent.GetComponentInParent<
                    RestaurantPlaceableObject
                >();

            if (placeable != null)
            {
                return placeable.ItemDefinition;
            }
        }

        return null;
    }

    private static void AnalyzeDefinition(
        BistroBuilderPlaceableMaintenanceReport report,
        RestaurantPlaceableItemDefinition definition,
        Dictionary<
            RestaurantEditableObjectDefinition,
            int
        > editableUsage
    )
    {
        string definitionPath =
            AssetDatabase.GetAssetPath(definition);

        if (string.IsNullOrWhiteSpace(
                definition.ItemId
            ))
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Error,
                "BB-MAINT-ITEM-001",
                definition.name +
                " no tiene ItemId válido.",
                "La identidad requiere revisión manual.",
                definition,
                definitionPath,
                false
            );
        }

        if (definition.Prefab == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Blocker,
                "BB-MAINT-ITEM-002",
                definition.DisplayName +
                " no tiene prefab.",
                "Asigna o reconstruye su prefab antes de usarlo.",
                definition,
                definitionPath,
                false
            );
        }

        if (definition.EditableDefinition == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Error,
                "BB-MAINT-ITEM-003",
                definition.DisplayName +
                " no tiene definición editable.",
                "No se crea automáticamente porque debe decidirse " +
                "su ruta e identidad.",
                definition,
                definitionPath,
                false
            );
        }
        else
        {
            RestaurantEditableObjectDefinition editable =
                definition.EditableDefinition;

            if (!string.Equals(
                    editable.DefinitionId,
                    definition.ItemId,
                    StringComparison.Ordinal
                ))
            {
                bool canSafelyRepair =
                    editableUsage.TryGetValue(
                        editable,
                        out int usageCount
                    ) &&
                    usageCount == 1;

                report.Add(
                    canSafelyRepair
                        ? BistroBuilderPlaceableMaintenanceSeverity.Warning
                        : BistroBuilderPlaceableMaintenanceSeverity.Error,
                    "BB-MAINT-ITEM-004",
                    definition.DisplayName +
                    " y su definición editable usan IDs distintos.",
                    canSafelyRepair
                        ? "La reparación segura sincronizará la " +
                          "definición editable."
                        : "La definición editable está compartida; " +
                          "requiere revisión manual.",
                    editable,
                    AssetDatabase.GetAssetPath(editable),
                    canSafelyRepair
                );
            }
        }

        if (definition.CatalogIcon == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Info,
                "BB-MAINT-ICON-001",
                definition.DisplayName +
                " no tiene miniatura de catálogo.",
                "Puede generarse automáticamente desde el prefab.",
                definition,
                definitionPath,
                definition.Prefab != null
            );
        }

        if (definition.Prefab != null)
        {
            AnalyzePrefab(
                report,
                definition
            );
        }
    }

    private static void AnalyzePrefab(
        BistroBuilderPlaceableMaintenanceReport report,
        RestaurantPlaceableItemDefinition definition
    )
    {
        RestaurantPlaceableObject prefab =
            definition.Prefab;

        string prefabPath =
            AssetDatabase.GetAssetPath(
                prefab.gameObject
            );

        if (!ReferenceEquals(
                prefab.ItemDefinition,
                definition
            ))
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Error,
                "BB-MAINT-PREFAB-001",
                definition.DisplayName +
                ": el prefab apunta a otra definición o a ninguna.",
                "La reparación segura sincronizará la referencia.",
                prefab.gameObject,
                prefabPath,
                true
            );
        }

        if (prefab.HasInstanceId)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Warning,
                "BB-MAINT-PREFAB-002",
                definition.DisplayName +
                ": el prefab contiene InstanceId.",
                "Los prefabs deben dejarlo vacío; se asigna en runtime.",
                prefab.gameObject,
                prefabPath,
                true
            );
        }

        SerializedObject serializedPrefab =
            new SerializedObject(prefab);

        SerializedProperty anchorProperty =
            serializedPrefab.FindProperty(
                "placementAnchor"
            );

        if (anchorProperty == null ||
            anchorProperty.objectReferenceValue == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Warning,
                "BB-MAINT-PREFAB-003",
                definition.DisplayName +
                ": falta PlacementAnchor explícito.",
                "Se calculará la base desde los Renderer o Collider.",
                prefab.gameObject,
                prefabPath,
                true
            );
        }

        RestaurantEditableObject editableObject =
            prefab.GetComponent<RestaurantEditableObject>();

        if (editableObject == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Blocker,
                "BB-MAINT-PREFAB-004",
                definition.DisplayName +
                ": falta RestaurantEditableObject.",
                "El prefab debe reconstruirse con la fábrica.",
                prefab.gameObject,
                prefabPath,
                false
            );
        }
        else if (!ReferenceEquals(
                     editableObject.Definition,
                     definition.EditableDefinition
                 ))
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Error,
                "BB-MAINT-PREFAB-005",
                definition.DisplayName +
                ": el prefab usa otra definición editable.",
                "La reparación segura sincronizará la referencia.",
                prefab.gameObject,
                prefabPath,
                definition.EditableDefinition != null
            );
        }

        RestaurantPlacementFootprint footprint =
            prefab.GetComponent<RestaurantPlacementFootprint>();

        if (footprint == null ||
            footprint.Size.x <= 0f ||
            footprint.Size.y <= 0f)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Blocker,
                "BB-MAINT-PREFAB-006",
                definition.DisplayName +
                ": la huella no existe o no es válida.",
                "Reconfigura el prefab antes de colocarlo.",
                prefab.gameObject,
                prefabPath,
                false
            );
        }

        RestaurantAreaMember areaMember =
            prefab.GetComponent<RestaurantAreaMember>();

        if (areaMember == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Blocker,
                "BB-MAINT-PREFAB-007",
                definition.DisplayName +
                ": falta RestaurantAreaMember.",
                "El prefab debe reconstruirse con la fábrica.",
                prefab.gameObject,
                prefabPath,
                false
            );
        }
        else
        {
            SerializedObject serializedAreaMember =
                new SerializedObject(areaMember);

            SerializedProperty positionReference =
                serializedAreaMember.FindProperty(
                    "positionReference"
                );

            if (positionReference == null ||
                positionReference.objectReferenceValue == null)
            {
                report.Add(
                    BistroBuilderPlaceableMaintenanceSeverity.Warning,
                    "BB-MAINT-PREFAB-008",
                    definition.DisplayName +
                    ": PositionReference no está serializado.",
                    "La reparación segura asignará la raíz.",
                    prefab.gameObject,
                    prefabPath,
                    true
                );
            }
        }
    }

    private static void AnalyzeMainCatalog(
        BistroBuilderPlaceableMaintenanceReport report,
        IReadOnlyList<RestaurantPlaceableItemDefinition> definitions
    )
    {
        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        if (catalog == null)
        {
            report.Add(
                BistroBuilderPlaceableMaintenanceSeverity.Blocker,
                "BB-MAINT-CATALOG-001",
                "No existe el catálogo principal.",
                "La reparación segura puede crearlo y registrar los " +
                "artículos válidos.",
                null,
                MainCatalogPath,
                true
            );

            return;
        }

        HashSet<RestaurantPlaceableItemDefinition> registered =
            new HashSet<RestaurantPlaceableItemDefinition>();

        IReadOnlyList<RestaurantPlaceableItemDefinition> items =
            catalog.Items;

        for (int index = 0;
             index < items.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition item =
                items[index];

            if (item != null)
            {
                registered.Add(item);
            }
        }

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (!registered.Contains(definition))
            {
                report.Add(
                    BistroBuilderPlaceableMaintenanceSeverity.Warning,
                    "BB-MAINT-CATALOG-002",
                    definition.DisplayName +
                    " no está en el catálogo principal.",
                    "La reparación segura lo añadirá sin duplicar IDs.",
                    definition,
                    AssetDatabase.GetAssetPath(definition),
                    true
                );
            }
        }
    }

    private static bool RepairSingleDefinition(
        RestaurantPlaceableItemDefinition definition,
        Dictionary<
            RestaurantEditableObjectDefinition,
            int
        > editableUsage,
        bool generateMissingThumbnail,
        out string message
    )
    {
        if (definition == null)
        {
            message =
                "ERROR: definición nula.";

            return false;
        }

        if (definition.Prefab == null ||
            definition.EditableDefinition == null)
        {
            message =
                "ERROR: " +
                definition.DisplayName +
                " no tiene prefab o definición editable.";

            return false;
        }

        string itemPath =
            AssetDatabase.GetAssetPath(definition);

        string editablePath =
            AssetDatabase.GetAssetPath(
                definition.EditableDefinition
            );

        string prefabPath =
            AssetDatabase.GetAssetPath(
                definition.Prefab.gameObject
            );

        AssetFileBackupSet backups =
            new AssetFileBackupSet();

        backups.Capture(itemPath);
        backups.Capture(editablePath);
        backups.Capture(prefabPath);

        bool changed = false;
        List<string> details =
            new List<string>();

        try
        {
            if (editableUsage.TryGetValue(
                    definition.EditableDefinition,
                    out int usageCount
                ) &&
                usageCount == 1)
            {
                changed |=
                    SynchronizeEditableIdentity(
                        definition,
                        details
                    );
            }

            changed |=
                RepairPrefabConsistency(
                    definition,
                    prefabPath,
                    details
                );

            if (generateMissingThumbnail &&
                definition.CatalogIcon == null)
            {
                BistroBuilderCatalogThumbnailService
                    .ThumbnailResult thumbnailResult =
                        BistroBuilderCatalogThumbnailService
                            .GenerateAndAssign(
                                definition,
                                false,
                                false,
                                BistroBuilderCatalogThumbnailService
                                    .DefaultThumbnailSize,
                                false
                            );

                details.Add(
                    thumbnailResult.Message
                );

                if (!thumbnailResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        thumbnailResult.Message
                    );
                }

                changed |=
                    thumbnailResult.Changed;
            }

            message =
                definition.DisplayName +
                ": " +
                (
                    changed
                        ? string.Join(" ", details)
                        : "sin cambios."
                );

            return changed;
        }
        catch (Exception exception)
        {
            backups.Restore();

            Debug.LogException(exception);

            message =
                "ERROR: " +
                definition.DisplayName +
                ": " +
                exception.Message;

            return false;
        }
    }

    private static bool SynchronizeEditableIdentity(
        RestaurantPlaceableItemDefinition definition,
        List<string> details
    )
    {
        RestaurantEditableObjectDefinition editable =
            definition.EditableDefinition;

        if (editable == null)
        {
            return false;
        }

        bool needsChange =
            !string.Equals(
                editable.DefinitionId,
                definition.ItemId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                editable.DisplayName,
                definition.DisplayName,
                StringComparison.Ordinal
            );

        if (!needsChange)
        {
            return false;
        }

        SerializedObject serialized =
            new SerializedObject(editable);

        SetString(
            serialized,
            "definitionId",
            definition.ItemId
        );

        SetString(
            serialized,
            "displayName",
            definition.DisplayName
        );

        SetString(
            serialized,
            "description",
            definition.Description
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(editable);

        details.Add(
            "Definición editable sincronizada."
        );

        return true;
    }

    private static bool RepairPrefabConsistency(
        RestaurantPlaceableItemDefinition definition,
        string prefabPath,
        List<string> details
    )
    {
        GameObject prefabRoot = null;
        bool changed = false;

        try
        {
            prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );

            if (prefabRoot == null)
            {
                throw new InvalidOperationException(
                    "Unity no pudo cargar el prefab " +
                    prefabPath +
                    "."
                );
            }

            RestaurantPlaceableObject placeable =
                prefabRoot.GetComponent<
                    RestaurantPlaceableObject
                >();

            RestaurantEditableObject editableObject =
                prefabRoot.GetComponent<
                    RestaurantEditableObject
                >();

            RestaurantAreaMember areaMember =
                prefabRoot.GetComponent<
                    RestaurantAreaMember
                >();

            RestaurantPlacementFootprint footprint =
                prefabRoot.GetComponent<
                    RestaurantPlacementFootprint
                >();

            if (placeable == null ||
                editableObject == null ||
                areaMember == null ||
                footprint == null)
            {
                throw new InvalidOperationException(
                    "El prefab no contiene el núcleo universal " +
                    "completo. Debe reconstruirse con la fábrica."
                );
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(placeable);

            SerializedProperty itemDefinitionProperty =
                RequireProperty(
                    serializedPlaceable,
                    "itemDefinition"
                );

            if (!ReferenceEquals(
                    itemDefinitionProperty.objectReferenceValue,
                    definition
                ))
            {
                itemDefinitionProperty.objectReferenceValue =
                    definition;

                changed = true;
                details.Add(
                    "Referencia de artículo corregida."
                );
            }

            SerializedProperty instanceIdProperty =
                RequireProperty(
                    serializedPlaceable,
                    "instanceId"
                );

            if (!string.IsNullOrWhiteSpace(
                    instanceIdProperty.stringValue
                ))
            {
                instanceIdProperty.stringValue =
                    string.Empty;

                changed = true;
                details.Add(
                    "InstanceId del prefab vaciado."
                );
            }

            SerializedProperty anchorProperty =
                RequireProperty(
                    serializedPlaceable,
                    "placementAnchor"
                );

            if (anchorProperty.objectReferenceValue == null)
            {
                Transform anchor =
                    ResolveOrCreatePlacementAnchor(
                        prefabRoot.transform
                    );

                if (!TryCalculateLocalBounds(
                        prefabRoot.transform,
                        out Bounds bounds
                    ))
                {
                    throw new InvalidOperationException(
                        "No se pudieron calcular los límites " +
                        "para PlacementAnchor."
                    );
                }

                anchor.localPosition =
                    new Vector3(
                        bounds.center.x,
                        bounds.min.y,
                        bounds.center.z
                    );

                anchor.localRotation =
                    Quaternion.identity;

                anchor.localScale =
                    Vector3.one;

                anchorProperty.objectReferenceValue =
                    anchor;

                changed = true;
                details.Add(
                    "PlacementAnchor configurado."
                );
            }

            if (serializedPlaceable.hasModifiedProperties)
            {
                serializedPlaceable.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject serializedEditable =
                new SerializedObject(editableObject);

            SerializedProperty editableDefinitionProperty =
                RequireProperty(
                    serializedEditable,
                    "definition"
                );

            if (!ReferenceEquals(
                    editableDefinitionProperty.objectReferenceValue,
                    definition.EditableDefinition
                ))
            {
                editableDefinitionProperty.objectReferenceValue =
                    definition.EditableDefinition;

                serializedEditable.ApplyModifiedPropertiesWithoutUndo();

                changed = true;
                details.Add(
                    "Definición editable del prefab sincronizada."
                );
            }

            SerializedObject serializedAreaMember =
                new SerializedObject(areaMember);

            SerializedProperty positionReference =
                RequireProperty(
                    serializedAreaMember,
                    "positionReference"
                );

            if (positionReference.objectReferenceValue == null)
            {
                positionReference.objectReferenceValue =
                    prefabRoot.transform;

                serializedAreaMember.ApplyModifiedPropertiesWithoutUndo();

                changed = true;
                details.Add(
                    "PositionReference asignado."
                );
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath
                );
            }

            return changed;
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );
            }
        }
    }

    private static void ApplyItemMetadata(
        RestaurantPlaceableItemDefinition definition,
        BistroBuilderPlaceableEditDraft draft
    )
    {
        SerializedObject serialized =
            new SerializedObject(definition);

        SetString(
            serialized,
            "displayName",
            draft.DisplayName.Trim()
        );

        SetString(
            serialized,
            "description",
            draft.Description ?? string.Empty
        );

        RequireProperty(
            serialized,
            "category"
        ).enumValueIndex =
            (int)draft.Category;

        RequireProperty(
            serialized,
            "purchasePrice"
        ).intValue =
            Mathf.Max(0, draft.PurchasePrice);

        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(definition);
    }

    private static void ApplyEditableMetadata(
        RestaurantEditableObjectDefinition editable,
        string stableItemId,
        BistroBuilderPlaceableEditDraft draft
    )
    {
        SerializedObject serialized =
            new SerializedObject(editable);

        SetString(
            serialized,
            "definitionId",
            stableItemId
        );

        SetString(
            serialized,
            "displayName",
            draft.DisplayName.Trim()
        );

        SetString(
            serialized,
            "description",
            draft.Description ?? string.Empty
        );

        RequireProperty(
            serialized,
            "canMove"
        ).boolValue =
            draft.CanMove;

        RequireProperty(
            serialized,
            "canRotate"
        ).boolValue =
            draft.CanRotate;

        RequireProperty(
            serialized,
            "useCustomGridSize"
        ).boolValue =
            draft.UseCustomGridSize;

        RequireProperty(
            serialized,
            "customGridSize"
        ).floatValue =
            Mathf.Max(0.01f, draft.CustomGridSize);

        RequireProperty(
            serialized,
            "useCustomRotationStep"
        ).boolValue =
            draft.UseCustomRotationStep;

        RequireProperty(
            serialized,
            "customRotationStepDegrees"
        ).floatValue =
            Mathf.Clamp(
                draft.RotationStepDegrees,
                1f,
                180f
            );

        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(editable);
    }

    private static void ApplyPrefabDraft(
        RestaurantPlaceableItemDefinition definition,
        BistroBuilderPlaceableEditDraft draft,
        string prefabPath
    )
    {
        GameObject prefabRoot = null;

        try
        {
            prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );

            if (prefabRoot == null)
            {
                throw new InvalidOperationException(
                    "Unity no pudo cargar el prefab."
                );
            }

            RestaurantPlacementFootprint footprint =
                prefabRoot.GetComponent<
                    RestaurantPlacementFootprint
                >();

            RestaurantAreaMember areaMember =
                prefabRoot.GetComponent<
                    RestaurantAreaMember
                >();

            RestaurantPlaceableObject placeable =
                prefabRoot.GetComponent<
                    RestaurantPlaceableObject
                >();

            RestaurantEditableObject editableObject =
                prefabRoot.GetComponent<
                    RestaurantEditableObject
                >();

            if (footprint == null ||
                areaMember == null ||
                placeable == null ||
                editableObject == null)
            {
                throw new InvalidOperationException(
                    "El prefab no contiene el núcleo universal completo."
                );
            }

            SerializedObject serializedFootprint =
                new SerializedObject(footprint);

            RequireProperty(
                serializedFootprint,
                "minimumClearance"
            ).floatValue =
                Mathf.Max(0f, draft.MinimumClearance);

            serializedFootprint.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedAreaMember =
                new SerializedObject(areaMember);

            SerializedProperty requiredCapabilities =
                RequireProperty(
                    serializedAreaMember,
                    "requiredCapabilities"
                );

            List<RestaurantAreaCapabilityDefinition> uniqueCapabilities =
                draft.RequiredCapabilities
                    .Where(capability => capability != null)
                    .Distinct()
                    .ToList();

            requiredCapabilities.arraySize =
                uniqueCapabilities.Count;

            for (int index = 0;
                 index < uniqueCapabilities.Count;
                 index++)
            {
                requiredCapabilities
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue =
                        uniqueCapabilities[index];
            }

            SerializedProperty positionReference =
                RequireProperty(
                    serializedAreaMember,
                    "positionReference"
                );

            if (positionReference.objectReferenceValue == null)
            {
                positionReference.objectReferenceValue =
                    prefabRoot.transform;
            }

            serializedAreaMember.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedPlaceable =
                new SerializedObject(placeable);

            RequireProperty(
                serializedPlaceable,
                "itemDefinition"
            ).objectReferenceValue =
                definition;

            RequireProperty(
                serializedPlaceable,
                "instanceId"
            ).stringValue =
                string.Empty;

            SerializedProperty anchorProperty =
                RequireProperty(
                    serializedPlaceable,
                    "placementAnchor"
                );

            if (anchorProperty.objectReferenceValue == null)
            {
                Transform anchor =
                    ResolveOrCreatePlacementAnchor(
                        prefabRoot.transform
                    );

                if (!TryCalculateLocalBounds(
                        prefabRoot.transform,
                        out Bounds bounds
                    ))
                {
                    throw new InvalidOperationException(
                        "No se pudo calcular PlacementAnchor."
                    );
                }

                anchor.localPosition =
                    new Vector3(
                        bounds.center.x,
                        bounds.min.y,
                        bounds.center.z
                    );

                anchor.localRotation =
                    Quaternion.identity;

                anchor.localScale =
                    Vector3.one;

                anchorProperty.objectReferenceValue =
                    anchor;
            }

            serializedPlaceable.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedEditable =
                new SerializedObject(editableObject);

            RequireProperty(
                serializedEditable,
                "definition"
            ).objectReferenceValue =
                definition.EditableDefinition;

            serializedEditable.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                prefabPath
            );
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );
            }
        }
    }

    private static bool NormalizeMainCatalog(
        IReadOnlyList<RestaurantPlaceableItemDefinition> definitions,
        out string message
    )
    {
        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        bool createdCatalog = false;

        if (catalog == null)
        {
            EnsureAssetFolderForPath(
                MainCatalogPath
            );

            catalog =
                ScriptableObject.CreateInstance<
                    RestaurantPlaceableCatalogDefinition
                >();

            AssetDatabase.CreateAsset(
                catalog,
                MainCatalogPath
            );

            createdCatalog = true;
        }

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        SerializedProperty items =
            RequireProperty(
                serializedCatalog,
                "items"
            );

        List<RestaurantPlaceableItemDefinition> normalized =
            definitions
                .Where(
                    definition =>
                        definition != null &&
                        !string.IsNullOrWhiteSpace(
                            definition.ItemId
                        )
                )
                .GroupBy(
                    definition => definition.ItemId,
                    StringComparer.Ordinal
                )
                .Select(group => group.First())
                .OrderBy(
                    definition => definition.Category
                )
                .ThenBy(
                    definition => definition.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase
                )
                .ToList();

        bool changed =
            createdCatalog ||
            items.arraySize != normalized.Count;

        if (!changed)
        {
            for (int index = 0;
                 index < normalized.Count;
                 index++)
            {
                if (!ReferenceEquals(
                        items
                            .GetArrayElementAtIndex(index)
                            .objectReferenceValue,
                        normalized[index]
                    ))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            items.arraySize =
                normalized.Count;

            for (int index = 0;
                 index < normalized.Count;
                 index++)
            {
                items
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue =
                        normalized[index];
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(catalog);
        }

        message =
            changed
                ? "Catálogo principal normalizado con " +
                  normalized.Count +
                  " artículo(s)."
                : "Catálogo principal ya estaba normalizado.";

        return changed;
    }

    private static Dictionary<
        RestaurantEditableObjectDefinition,
        int
    > CountEditableDefinitionUsage(
        IReadOnlyList<RestaurantPlaceableItemDefinition> definitions
    )
    {
        Dictionary<
            RestaurantEditableObjectDefinition,
            int
        > result =
            new Dictionary<
                RestaurantEditableObjectDefinition,
                int
            >();

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantEditableObjectDefinition editable =
                definitions[index] != null
                    ? definitions[index].EditableDefinition
                    : null;

            if (editable == null)
            {
                continue;
            }

            if (!result.ContainsKey(editable))
            {
                result.Add(editable, 0);
            }

            result[editable]++;
        }

        return result;
    }

    private static bool CapabilitiesMatch(
        RestaurantAreaMember areaMember,
        IReadOnlyList<RestaurantAreaCapabilityDefinition> expected
    )
    {
        if (areaMember == null)
        {
            return false;
        }

        HashSet<RestaurantAreaCapabilityDefinition> current =
            new HashSet<RestaurantAreaCapabilityDefinition>();

        IReadOnlyList<RestaurantAreaCapabilityDefinition> capabilities =
            areaMember.RequiredCapabilities;

        for (int index = 0;
             index < capabilities.Count;
             index++)
        {
            if (capabilities[index] != null)
            {
                current.Add(capabilities[index]);
            }
        }

        HashSet<RestaurantAreaCapabilityDefinition> expectedSet =
            new HashSet<RestaurantAreaCapabilityDefinition>();

        if (expected != null)
        {
            for (int index = 0;
                 index < expected.Count;
                 index++)
            {
                if (expected[index] != null)
                {
                    expectedSet.Add(expected[index]);
                }
            }
        }

        return current.SetEquals(expectedSet);
    }

    private static Transform ResolveOrCreatePlacementAnchor(
        Transform root
    )
    {
        Transform existing =
            root.Find(PlacementAnchorName);

        if (existing != null)
        {
            if (existing.childCount > 0 ||
                existing.GetComponents<Component>().Length > 1)
            {
                throw new InvalidOperationException(
                    "Existe un hijo PlacementAnchor con contenido " +
                    "adicional y no se modificará automáticamente."
                );
            }

            return existing;
        }

        GameObject anchorObject =
            new GameObject(
                PlacementAnchorName
            );

        anchorObject.transform.SetParent(
            root,
            false
        );

        return anchorObject.transform;
    }

    private static bool TryCalculateLocalBounds(
        Transform root,
        out Bounds localBounds
    )
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (renderer == null ||
                string.Equals(
                    renderer.transform.name,
                    PlacementAnchorName,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            EncapsulateWorldBoundsInLocalSpace(
                root,
                renderer.bounds,
                ref hasBounds,
                ref minimum,
                ref maximum
            );
        }

        if (!hasBounds)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(
                    true
                );

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider =
                    colliders[index];

                if (collider == null ||
                    string.Equals(
                        collider.transform.name,
                        PlacementAnchorName,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                EncapsulateWorldBoundsInLocalSpace(
                    root,
                    collider.bounds,
                    ref hasBounds,
                    ref minimum,
                    ref maximum
                );
            }
        }

        if (!hasBounds)
        {
            localBounds = default(Bounds);
            return false;
        }

        localBounds = new Bounds();
        localBounds.SetMinMax(minimum, maximum);

        return true;
    }

    private static void EncapsulateWorldBoundsInLocalSpace(
        Transform root,
        Bounds worldBounds,
        ref bool hasBounds,
        ref Vector3 minimum,
        ref Vector3 maximum
    )
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)
                        );

                    Vector3 localCorner =
                        root.InverseTransformPoint(
                            worldCorner
                        );

                    if (!hasBounds)
                    {
                        minimum = localCorner;
                        maximum = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum =
                            Vector3.Min(
                                minimum,
                                localCorner
                            );

                        maximum =
                            Vector3.Max(
                                maximum,
                                localCorner
                            );
                    }
                }
            }
        }
    }

    private static void SortFindings(
        List<BistroBuilderPlaceableMaintenanceFinding> findings
    )
    {
        findings.Sort(
            (first, second) =>
            {
                int severityComparison =
                    second.Severity.CompareTo(
                        first.Severity
                    );

                if (severityComparison != 0)
                {
                    return severityComparison;
                }

                int codeComparison =
                    string.Compare(
                        first.Code,
                        second.Code,
                        StringComparison.Ordinal
                    );

                if (codeComparison != 0)
                {
                    return codeComparison;
                }

                return string.Compare(
                    first.Message,
                    second.Message,
                    StringComparison.CurrentCultureIgnoreCase
                );
            }
        );
    }

    private static void ScheduleProjectHealth()
    {
        EditorApplication.delayCall +=
            RunProjectHealthWhenReady;
    }

    private static void RunProjectHealthWhenReady()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall +=
                RunProjectHealthWhenReady;

            return;
        }

        BistroBuilderValidationReport report =
            BistroBuilderProjectValidator
                .RunFullValidation(true);

        BistroBuilderProjectHealthWindow.SetReport(
            report
        );
    }

    private static void EnsureAssetFolderForPath(
        string assetPath
    )
    {
        string normalized =
            assetPath
                .Trim()
                .Replace('\\', '/');

        int lastSlash =
            normalized.LastIndexOf('/');

        if (lastSlash <= 0)
        {
            throw new ArgumentException(
                "Ruta de asset no válida: " +
                assetPath
            );
        }

        string folder =
            normalized.Substring(
                0,
                lastSlash
            );

        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        if (!folder.StartsWith(
                "Assets/",
                StringComparison.Ordinal
            ))
        {
            throw new ArgumentException(
                "La ruta debe comenzar por Assets/: " +
                assetPath
            );
        }

        string[] segments =
            folder.Split('/');

        string current = "Assets";

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string next =
                current +
                "/" +
                segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                string guid =
                    AssetDatabase.CreateFolder(
                        current,
                        segments[index]
                    );

                if (string.IsNullOrWhiteSpace(guid) ||
                    !AssetDatabase.IsValidFolder(next))
                {
                    throw new InvalidOperationException(
                        "Unity no pudo crear " +
                        next +
                        "."
                    );
                }
            }

            current = next;
        }
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).stringValue =
            value ?? string.Empty;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property =
            serializedObject.FindProperty(
                propertyName
            );

        if (property == null)
        {
            throw new InvalidOperationException(
                serializedObject.targetObject.name +
                " no contiene la propiedad " +
                propertyName +
                "."
            );
        }

        return property;
    }

    /// <summary>
    /// Copia de seguridad binaria de assets existentes. No toca sus
    /// archivos .meta y, por tanto, conserva GUID y referencias.
    /// </summary>
    private sealed class AssetFileBackupSet
    {
        private readonly Dictionary<string, byte[]> files =
            new Dictionary<string, byte[]>(
                StringComparer.Ordinal
            );

        public void Capture(
            string assetPath
        )
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                files.ContainsKey(assetPath))
            {
                return;
            }

            string absolutePath =
                ToAbsolutePath(assetPath);

            if (!File.Exists(absolutePath))
            {
                return;
            }

            files.Add(
                assetPath,
                File.ReadAllBytes(absolutePath)
            );
        }

        public void Restore()
        {
            foreach (
                KeyValuePair<string, byte[]> pair in files
            )
            {
                try
                {
                    string absolutePath =
                        ToAbsolutePath(pair.Key);

                    File.WriteAllBytes(
                        absolutePath,
                        pair.Value
                    );

                    AssetDatabase.ImportAsset(
                        pair.Key,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate
                    );
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            AssetDatabase.Refresh();
        }

        private static string ToAbsolutePath(
            string assetPath
        )
        {
            string normalized =
                assetPath
                    .Trim()
                    .Replace('\\', '/');

            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal
                ) &&
                !string.Equals(
                    normalized,
                    "Assets",
                    StringComparison.Ordinal
                ))
            {
                throw new ArgumentException(
                    "Ruta fuera de Assets: " +
                    assetPath
                );
            }

            string relative =
                normalized.Substring(
                    "Assets".Length
                );

            return
                Application.dataPath +
                relative.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                );
        }
    }
}
