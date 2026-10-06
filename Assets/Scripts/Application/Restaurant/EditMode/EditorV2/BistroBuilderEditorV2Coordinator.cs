using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Coordinator")]
public sealed class BistroBuilderEditorV2Coordinator : MonoBehaviour
{
    [SerializeField] private RestaurantEditModeService editModeService;
    [Tooltip("Optional explicit adapter list. Empty = discover scene adapters.")]
    [SerializeField] private MonoBehaviour[] adapterSources = Array.Empty<MonoBehaviour>();

    private readonly Dictionary<BistroBuilderEditorV2ToolFamily, IBistroBuilderEditorV2ToolAdapter>
        adapters = new Dictionary<BistroBuilderEditorV2ToolFamily, IBistroBuilderEditorV2ToolAdapter>();

    private IBistroBuilderEditorV2ToolAdapter activeAdapter;
    private BistroBuilderEditorV2ToolFamily activeFamily = BistroBuilderEditorV2ToolFamily.None;
    private string activeToolId = string.Empty;
    private long transitionSequence;
    private string lastTransition = "Coordinator created.";
    private string lastError = string.Empty;

    public event Action<BistroBuilderEditorV2Snapshot> StateChanged;

    public BistroBuilderEditorV2ToolFamily ActiveFamily => activeFamily;
    public string ActiveToolId => activeToolId;
    public int RegisteredAdapterCount => adapters.Count;
    public long TransitionSequence => transitionSequence;
    public string LastError => lastError;

    public BistroBuilderEditorV2OperationState State
    {
        get
        {
            if (editModeService == null || !editModeService.IsEditModeActive)
                return BistroBuilderEditorV2OperationState.Inactive;
            if (activeAdapter == null)
                return BistroBuilderEditorV2OperationState.Ready;
            return activeAdapter.HasActiveOperation
                ? BistroBuilderEditorV2OperationState.OperationActive
                : BistroBuilderEditorV2OperationState.ToolActive;
        }
    }

    private void Awake()
    {
        CacheDependencies();
        RebuildAdapterRegistry(out _);
    }

    private void OnEnable()
    {
        CacheDependencies();
        RebuildAdapterRegistry(out _);
        Subscribe();
        PublishState("Coordinator enabled.", string.Empty, false);
    }

    private void OnDisable()
    {
        Unsubscribe();
        activeAdapter = null;
        activeFamily = BistroBuilderEditorV2ToolFamily.None;
        activeToolId = string.Empty;
    }

    public void Configure(RestaurantEditModeService service, params MonoBehaviour[] sources)
    {
        Unsubscribe();
        editModeService = service;
        adapterSources = sources ?? Array.Empty<MonoBehaviour>();
        RebuildAdapterRegistry(out _);
        Subscribe();
        PublishState("Coordinator configured.", string.Empty, false);
    }

    public bool RebuildAdapterRegistry(out string error)
    {
        error = string.Empty;
        adapters.Clear();

        MonoBehaviour[] sources = adapterSources;
        if (sources == null || sources.Length == 0)
        {
            sources = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        for (int i = 0; i < sources.Length; i++)
        {
            if (!(sources[i] is IBistroBuilderEditorV2ToolAdapter adapter))
                continue;
            if (adapter.Family == BistroBuilderEditorV2ToolFamily.None)
                continue;
            if (adapters.ContainsKey(adapter.Family))
            {
                error = "Existe más de un adaptador Editor V2 para " + adapter.Family + ".";
                lastError = error;
                adapters.Clear();
                return false;
            }
            adapters.Add(adapter.Family, adapter);
        }

        lastError = string.Empty;
        return true;
    }

    public bool TryActivateTool(BistroBuilderEditorV2ToolFamily family, string toolId, out string error)
    {
        error = string.Empty;
        CacheDependencies();

        if (editModeService == null)
            return Fail("Falta RestaurantEditModeService.", out error);
        if (!editModeService.IsEditModeActive)
            return Fail("Activa el modo edición antes de elegir una herramienta.", out error);
        if (family == BistroBuilderEditorV2ToolFamily.None)
            return Fail("La familia de herramienta es inválida.", out error);

        if (!adapters.TryGetValue(family, out IBistroBuilderEditorV2ToolAdapter target))
            return Fail("No existe adaptador Editor V2 para " + family + ".", out error);
        if (target == null || !target.IsAvailable)
            return Fail("El especialista de " + family + " no está disponible.", out error);

        string normalizedTool = string.IsNullOrWhiteSpace(toolId) ? string.Empty : toolId.Trim();

        if (ReferenceEquals(activeAdapter, target) &&
            target.IsActive &&
            string.Equals(target.ActiveToolId, normalizedTool, StringComparison.OrdinalIgnoreCase))
        {
            activeToolId = target.ActiveToolId;
            PublishState("Activación idempotente de " + family + "/" + activeToolId + ".", string.Empty);
            return true;
        }

        IBistroBuilderEditorV2ToolAdapter previousAdapter = activeAdapter;
        BistroBuilderEditorV2ToolFamily previousFamily = activeFamily;
        string previousTool = activeToolId;

        if (previousAdapter != null)
        {
            if (previousAdapter.HasActiveOperation &&
                !previousAdapter.TryCancelActiveOperation(out string cancelError))
            {
                return Fail(
                    "No se puede cambiar de herramienta porque la operación activa no pudo cancelarse: " +
                    cancelError,
                    out error);
            }
            previousAdapter.Deactivate();
        }

        if (!target.TryActivate(normalizedTool, out string activationError))
        {
            if (previousAdapter != null &&
                previousAdapter.IsAvailable &&
                previousAdapter.TryActivate(previousTool, out _))
            {
                activeAdapter = previousAdapter;
                activeFamily = previousFamily;
                activeToolId = previousAdapter.ActiveToolId;
            }
            else
            {
                activeAdapter = null;
                activeFamily = BistroBuilderEditorV2ToolFamily.None;
                activeToolId = string.Empty;
            }

            return Fail(activationError, out error);
        }

        activeAdapter = target;
        activeFamily = family;
        activeToolId = target.ActiveToolId;
        lastError = string.Empty;
        PublishState("Herramienta activa: " + family + "/" + activeToolId + ".", string.Empty);
        return true;
    }

    public bool TryCancelActiveOperation(out string error)
    {
        error = string.Empty;
        if (activeAdapter == null || !activeAdapter.HasActiveOperation)
        {
            PublishState("No hay operación provisional que cancelar.", string.Empty);
            return true;
        }

        if (!activeAdapter.TryCancelActiveOperation(out error))
            return Fail(error, out error);

        lastError = string.Empty;
        PublishState("Operación provisional cancelada.", string.Empty);
        return true;
    }

    public bool TryClearActiveTool(bool cancelOperation, out string error)
    {
        error = string.Empty;
        if (activeAdapter == null)
            return true;

        if (activeAdapter.HasActiveOperation)
        {
            if (!cancelOperation)
                return Fail("Existe una operación provisional activa.", out error);
            if (!activeAdapter.TryCancelActiveOperation(out error))
                return Fail(error, out error);
        }

        activeAdapter.Deactivate();
        activeAdapter = null;
        activeFamily = BistroBuilderEditorV2ToolFamily.None;
        activeToolId = string.Empty;
        lastError = string.Empty;
        PublishState("Herramienta desactivada.", string.Empty);
        return true;
    }

    public BistroBuilderEditorV2Snapshot CreateSnapshot()
    {
        return new BistroBuilderEditorV2Snapshot
        {
            editModeActive = editModeService != null && editModeService.IsEditModeActive,
            state = State,
            activeFamily = activeFamily,
            activeToolId = activeToolId ?? string.Empty,
            hasActiveOperation = activeAdapter != null && activeAdapter.HasActiveOperation,
            registeredAdapterCount = adapters.Count,
            transitionSequence = transitionSequence,
            lastTransition = lastTransition ?? string.Empty,
            lastError = lastError ?? string.Empty
        };
    }

    private void HandleEditModeEntered()
    {
        PublishState("Modo edición activo.", string.Empty);
    }

    private void HandleEditModeExited()
    {
        if (activeAdapter != null)
            activeAdapter.Deactivate();
        activeAdapter = null;
        activeFamily = BistroBuilderEditorV2ToolFamily.None;
        activeToolId = string.Empty;
        PublishState("Modo edición cerrado; coordinación limpiada.", string.Empty);
    }

    private void CacheDependencies()
    {
        if (editModeService == null)
            editModeService = FindFirstObjectByType<RestaurantEditModeService>();
    }

    private void Subscribe()
    {
        if (editModeService == null)
            return;
        editModeService.EditModeEntered -= HandleEditModeEntered;
        editModeService.EditModeEntered += HandleEditModeEntered;
        editModeService.EditModeExited -= HandleEditModeExited;
        editModeService.EditModeExited += HandleEditModeExited;
    }

    private void Unsubscribe()
    {
        if (editModeService == null)
            return;
        editModeService.EditModeEntered -= HandleEditModeEntered;
        editModeService.EditModeExited -= HandleEditModeExited;
    }

    private bool Fail(string message, out string error)
    {
        error = string.IsNullOrWhiteSpace(message)
            ? "La coordinación de Editor V2 rechazó la operación."
            : message;
        lastError = error;
        PublishState("Operación rechazada.", error);
        return false;
    }

    private void PublishState(string transition, string error, bool increment = true)
    {
        if (increment)
            transitionSequence++;
        lastTransition = transition ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(error))
            lastError = error;
        StateChanged?.Invoke(CreateSnapshot());
    }
}
