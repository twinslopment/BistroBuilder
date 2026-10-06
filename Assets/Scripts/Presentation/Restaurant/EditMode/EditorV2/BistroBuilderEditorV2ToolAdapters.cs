using System;
using System.Globalization;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Furniture Adapter")]
public sealed class BistroBuilderEditorV2FurnitureAdapter :
    MonoBehaviour,
    IBistroBuilderEditorV2ToolAdapter,
    IBistroBuilderEditorV2SelectionSource
{
    [SerializeField] private RestaurantEditInteractionController furnitureController;
    [SerializeField] private BistroBuilderConstructionAuthoringRuntimeTool constructionTool;

    private string activeToolId = string.Empty;

    public BistroBuilderEditorV2ToolFamily Family => BistroBuilderEditorV2ToolFamily.Furniture;
    public bool IsAvailable
    {
        get
        {
            CacheDependencies();
            return furnitureController != null;
        }
    }
    public bool IsActive =>
        furnitureController != null &&
        !furnitureController.IsWorldInputSuppressed &&
        (constructionTool == null ||
         constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Furniture);
    public bool HasActiveOperation =>
        furnitureController != null && furnitureController.HasActivePlacement;
    public string ActiveToolId => activeToolId;

    private void Awake() => CacheDependencies();
    private void OnEnable() => CacheDependencies();

    public bool TryActivate(string toolId, out string error)
    {
        error = string.Empty;
        CacheDependencies();

        string normalized = Normalize(toolId);
        if (normalized != "furniture" && normalized != "select")
        {
            error = "Herramienta de mobiliario no soportada: " + normalized + ".";
            return false;
        }
        if (furnitureController == null)
        {
            error = "Falta RestaurantEditInteractionController.";
            return false;
        }

        if (constructionTool != null)
        {
            constructionTool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
            if (constructionTool.Mode != BistroBuilderConstructionRuntimeMode.Furniture)
            {
                error = "Construction Authoring no pudo volver a modo mobiliario.";
                return false;
            }
        }

        furnitureController.SetWorldInputSuppressed(false);
        activeToolId = normalized;
        return true;
    }

    public bool TryCancelActiveOperation(out string error)
    {
        error = string.Empty;
        if (!HasActiveOperation)
            return true;
        if (!furnitureController.CancelActivePlacement())
        {
            error = "No se pudo cancelar la colocación de mobiliario.";
            return false;
        }
        return !HasActiveOperation;
    }

    public void Deactivate()
    {
        if (HasActiveOperation)
            TryCancelActiveOperation(out _);
        if (furnitureController != null)
            furnitureController.SetWorldInputSuppressed(true);
        activeToolId = string.Empty;
    }

    public bool TryReadSelection(out BistroBuilderEditorV2Selection selection)
    {
        selection = BistroBuilderEditorV2Selection.None;
        CacheDependencies();

        RestaurantEditableObject editable = furnitureController != null
            ? furnitureController.SelectedEditableObject
            : null;
        if (editable == null ||
            !editable.TryGetComponent(out RestaurantPlaceableObject placeable))
            return false;

        string identity = placeable.InstanceId;
        bool persistent = !string.IsNullOrWhiteSpace(identity);
        if (!persistent)
            identity = "runtime:" + placeable.GetInstanceID().ToString(CultureInfo.InvariantCulture);

        BistroBuilderEditorV2SelectionCapability capabilities =
            BistroBuilderEditorV2SelectionCapability.Inspect |
            BistroBuilderEditorV2SelectionCapability.Delete;
        if (editable.CanMove)
            capabilities |= BistroBuilderEditorV2SelectionCapability.Move;
        if (editable.CanRotate)
            capabilities |= BistroBuilderEditorV2SelectionCapability.Rotate;
        if (placeable.ItemDefinition != null)
            capabilities |= BistroBuilderEditorV2SelectionCapability.Duplicate;

        selection = new BistroBuilderEditorV2Selection
        {
            family = Family,
            kind = BistroBuilderEditorV2SelectionKind.Furniture,
            stableId = identity,
            displayName = placeable.DisplayName,
            capabilities = capabilities,
            persistentIdentity = persistent
        };
        return true;
    }

    public bool ClearSelection()
    {
        CacheDependencies();
        return furnitureController != null && furnitureController.ClearSelection();
    }

    private void CacheDependencies()
    {
        if (furnitureController == null)
            furnitureController = FindFirstObjectByType<RestaurantEditInteractionController>();
        if (constructionTool == null)
            constructionTool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
    }

    private static string Normalize(string toolId)
    {
        string value = string.IsNullOrWhiteSpace(toolId) ? "furniture" : toolId.Trim().ToLowerInvariant();
        return value == "furniture-select" ? "select" : value;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Construction Adapter")]
public sealed class BistroBuilderEditorV2ConstructionAdapter :
    MonoBehaviour,
    IBistroBuilderEditorV2ToolAdapter,
    IBistroBuilderEditorV2SelectionSource
{
    [SerializeField] private BistroBuilderConstructionAuthoringRuntimeTool constructionTool;
    [SerializeField] private RestaurantEditInteractionController furnitureController;

    private string activeToolId = string.Empty;

    public BistroBuilderEditorV2ToolFamily Family => BistroBuilderEditorV2ToolFamily.Construction;
    public bool IsAvailable
    {
        get
        {
            CacheDependencies();
            return constructionTool != null;
        }
    }
    public bool IsActive =>
        constructionTool != null &&
        !string.IsNullOrEmpty(activeToolId) &&
        constructionTool.Mode == ResolveMode(activeToolId, out _);
    public bool HasActiveOperation =>
        constructionTool != null && constructionTool.HasActiveGesture;
    public string ActiveToolId => activeToolId;

    private void Awake() => CacheDependencies();
    private void OnEnable() => CacheDependencies();

    public bool TryActivate(string toolId, out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (constructionTool == null)
        {
            error = "Falta BistroBuilderConstructionAuthoringRuntimeTool.";
            return false;
        }

        string normalized = Normalize(toolId);
        BistroBuilderConstructionRuntimeMode mode = ResolveMode(normalized, out bool supported);
        if (!supported)
        {
            error = "Herramienta de construcción no soportada: " + normalized + ".";
            return false;
        }

        if (furnitureController != null)
            furnitureController.SetWorldInputSuppressed(true);

        constructionTool.SetMode(mode);
        if (constructionTool.Mode != mode)
        {
            error = "Construction Authoring rechazó la herramienta " + normalized + ".";
            return false;
        }

        activeToolId = normalized;
        return true;
    }

    public bool TryCancelActiveOperation(out string error)
    {
        error = string.Empty;
        if (!HasActiveOperation)
            return true;

        // SetMode(same) uses the specialist's own cancellation path.
        BistroBuilderConstructionRuntimeMode mode = constructionTool.Mode;
        constructionTool.SetMode(mode);
        if (constructionTool.HasActiveGesture)
        {
            error = "Construction Authoring no pudo cancelar el gesto activo.";
            return false;
        }

        return true;
    }

    public void Deactivate()
    {
        if (HasActiveOperation)
            TryCancelActiveOperation(out _);
        activeToolId = string.Empty;
    }

    public bool TryReadSelection(out BistroBuilderEditorV2Selection selection)
    {
        selection = BistroBuilderEditorV2Selection.None;
        CacheDependencies();
        if (constructionTool == null ||
            constructionTool.SelectedKind == EntityKind.None ||
            !constructionTool.SelectedId.IsValid)
            return false;

        BistroBuilderEditorV2SelectionKind kind;
        BistroBuilderEditorV2SelectionCapability capabilities =
            BistroBuilderEditorV2SelectionCapability.Inspect;
        string label;

        switch (constructionTool.SelectedKind)
        {
            case EntityKind.Wall:
                kind = BistroBuilderEditorV2SelectionKind.Wall;
                label = "Pared";
                capabilities |=
                    BistroBuilderEditorV2SelectionCapability.Move |
                    BistroBuilderEditorV2SelectionCapability.Rotate |
                    BistroBuilderEditorV2SelectionCapability.Delete |
                    BistroBuilderEditorV2SelectionCapability.Duplicate;
                break;
            case EntityKind.Opening:
                kind = BistroBuilderEditorV2SelectionKind.Opening;
                label = "Puerta / ventana";
                capabilities |=
                    BistroBuilderEditorV2SelectionCapability.Delete |
                    BistroBuilderEditorV2SelectionCapability.Duplicate;
                break;
            case EntityKind.Room:
                kind = BistroBuilderEditorV2SelectionKind.Room;
                label = "Habitación";
                break;
            default:
                return false;
        }

        selection = new BistroBuilderEditorV2Selection
        {
            family = Family,
            kind = kind,
            stableId = constructionTool.SelectedId.Value,
            displayName = label,
            capabilities = capabilities,
            persistentIdentity = true
        };
        return true;
    }

    public bool ClearSelection()
    {
        CacheDependencies();
        return constructionTool != null && constructionTool.ClearArchitectureSelection();
    }

    private void CacheDependencies()
    {
        if (constructionTool == null)
            constructionTool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        if (furnitureController == null)
            furnitureController = FindFirstObjectByType<RestaurantEditInteractionController>();
    }

    private static string Normalize(string toolId)
    {
        string value = string.IsNullOrWhiteSpace(toolId) ? "select" : toolId.Trim().ToLowerInvariant();
        return value switch
        {
            "wallmodule" => "wall-module",
            "wall_module" => "wall-module",
            _ => value
        };
    }

    private static BistroBuilderConstructionRuntimeMode ResolveMode(string toolId, out bool supported)
    {
        supported = true;
        switch (Normalize(toolId))
        {
            case "select": return BistroBuilderConstructionRuntimeMode.Select;
            case "wall": return BistroBuilderConstructionRuntimeMode.Wall;
            case "room": return BistroBuilderConstructionRuntimeMode.Room;
            case "door": return BistroBuilderConstructionRuntimeMode.Door;
            case "window": return BistroBuilderConstructionRuntimeMode.Window;
            case "wall-module": return BistroBuilderConstructionRuntimeMode.WallModule;
            default:
                supported = false;
                return BistroBuilderConstructionRuntimeMode.Select;
        }
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Surfaces Adapter")]
public sealed class BistroBuilderEditorV2SurfacesAdapter :
    MonoBehaviour,
    IBistroBuilderEditorV2ToolAdapter,
    IBistroBuilderEditorV2SelectionSource
{
    [SerializeField] private BistroBuilderConstructionAuthoringRuntimeTool constructionTool;
    [SerializeField] private RestaurantEditInteractionController furnitureController;

    private string activeToolId = string.Empty;

    public BistroBuilderEditorV2ToolFamily Family => BistroBuilderEditorV2ToolFamily.Surfaces;
    public bool IsAvailable
    {
        get
        {
            CacheDependencies();
            return constructionTool != null;
        }
    }
    public bool IsActive =>
        constructionTool != null &&
        !string.IsNullOrEmpty(activeToolId) &&
        constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Select;
    public bool HasActiveOperation => false;
    public string ActiveToolId => activeToolId;

    private void Awake() => CacheDependencies();
    private void OnEnable() => CacheDependencies();

    public bool TryActivate(string toolId, out string error)
    {
        error = string.Empty;
        CacheDependencies();

        string normalized = Normalize(toolId);
        if (normalized != "floor" && normalized != "select")
        {
            error = "Herramienta de superficies no soportada: " + normalized + ".";
            return false;
        }
        if (constructionTool == null)
        {
            error = "Falta Construction Authoring para seleccionar superficies.";
            return false;
        }

        if (furnitureController != null)
            furnitureController.SetWorldInputSuppressed(true);

        constructionTool.SetMode(BistroBuilderConstructionRuntimeMode.Select);
        if (constructionTool.Mode != BistroBuilderConstructionRuntimeMode.Select)
        {
            error = "Construction Authoring no pudo activar selección de superficies.";
            return false;
        }

        activeToolId = normalized;
        return true;
    }

    public bool TryCancelActiveOperation(out string error)
    {
        error = string.Empty;
        return true;
    }

    public void Deactivate()
    {
        activeToolId = string.Empty;
    }

    public bool TryReadSelection(out BistroBuilderEditorV2Selection selection)
    {
        selection = BistroBuilderEditorV2Selection.None;
        CacheDependencies();
        if (constructionTool == null ||
            constructionTool.SelectedKind != EntityKind.Room ||
            !constructionTool.SelectedId.IsValid)
            return false;

        selection = new BistroBuilderEditorV2Selection
        {
            family = Family,
            kind = BistroBuilderEditorV2SelectionKind.Surface,
            stableId = constructionTool.SelectedId.Value,
            displayName = "Superficie de habitación",
            capabilities =
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.ApplySurface,
            persistentIdentity = true
        };
        return true;
    }

    public bool ClearSelection()
    {
        CacheDependencies();
        return constructionTool != null && constructionTool.ClearArchitectureSelection();
    }

    private void CacheDependencies()
    {
        if (constructionTool == null)
            constructionTool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        if (furnitureController == null)
            furnitureController = FindFirstObjectByType<RestaurantEditInteractionController>();
    }

    private static string Normalize(string toolId)
    {
        string value = string.IsNullOrWhiteSpace(toolId) ? "floor" : toolId.Trim().ToLowerInvariant();
        return value switch
        {
            "surfaces" => "floor",
            "floor-finish" => "floor",
            _ => value
        };
    }
}
