using UnityEngine;

/// <summary>
/// Evita Save/Load mientras exista una sesión semántica de edición activa.
/// El documento comprometido sigue siendo la única autoridad persistible.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Edit Session Save Guard")]
public sealed class BistroBuilderEditSessionSaveGuard :
    MonoBehaviour,
    IBistroBuilderSaveOperationGuard
{
    [SerializeField] private BistroBuilderEditRuntimeCoordinator coordinator;
    [SerializeField] private int priority = 110;

    public int Priority => priority;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool CanSave(out string rejectionMessage)
    {
        return Evaluate("guardar", out rejectionMessage);
    }

    public bool CanLoad(out string rejectionMessage)
    {
        return Evaluate("cargar", out rejectionMessage);
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (coordinator == null)
        {
            error = "Falta BistroBuilderEditRuntimeCoordinator.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private bool Evaluate(string operation, out string rejectionMessage)
    {
        CacheDependencies();
        if (coordinator == null)
        {
            rejectionMessage =
                "El control de la sesión de edición no está disponible.";
            return false;
        }
        if (coordinator.HasSession)
        {
            rejectionMessage =
                "Confirma o cancela la edición actual antes de " + operation + ".";
            return false;
        }
        rejectionMessage = string.Empty;
        return true;
    }

    private void CacheDependencies()
    {
        if (coordinator == null)
            coordinator = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
    }
}
