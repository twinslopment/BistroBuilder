using System;
using UnityEngine;

/// <summary>
/// Fachada de presentación del Bloque 12. Solo proyecta la cocina avanzada y
/// delega sus tres acciones jugables a la autoridad operativa.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedKitchenPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedKitchenService kitchenService;

    public event Action Changed;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        if (kitchenService != null)
        {
            kitchenService.Changed -= HandleChanged;
            kitchenService.Changed += HandleChanged;
        }
    }

    private void OnDisable()
    {
        if (kitchenService != null)
            kitchenService.Changed -= HandleChanged;
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (kitchenService == null)
        {
            error = "La UI de cocina necesita AdvancedKitchenService.";
            return false;
        }
        return kitchenService.ValidateConfiguration(out error);
    }

    public bool TryBuildSnapshot(
        out BistroBuilderAdvancedKitchenSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error)) return false;
        return kitchenService.TryBuildSnapshot(out snapshot, out error);
    }

    public bool SetIntakeMode(
        BistroBuilderKitchenIntakeMode mode,
        out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        return kitchenService.TrySetIntakeMode(mode, out error);
    }

    public bool Prioritize(string lineId, out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        return kitchenService.TryPrioritizeLine(lineId, out error);
    }

    private void HandleChanged() => Changed?.Invoke();

    private void CacheDependencies()
    {
        if (kitchenService == null)
            TryGetComponent(out kitchenService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
