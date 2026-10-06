using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Furniture Adapter")]
public sealed class BistroBuilderEditorV2FurnitureAdapter :
    MonoBehaviour,
    IBistroBuilderEditorV2ToolAdapter
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
    IBistroBuilderEditorV2ToolAdapter
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
    IBistroBuilderEditorV2ToolAdapter
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
