using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderEditFinanceGateway))]
[AddComponentMenu("Bistro Builder/Finance/Edit Mode Finance Gateway Binder")]
public sealed class BistroBuilderEditFinanceGatewayBinder : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditFinanceGateway gateway;
    [SerializeField] private BistroBuilderEditRuntimeCoordinator editCoordinator;

    public bool IsBound { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    private void OnEnable()
    {
        TryBind();
    }

    private void OnDisable()
    {
        if (IsBound && editCoordinator != null && gateway != null)
            editCoordinator.UnbindEconomyGateway(gateway);
        IsBound = false;
    }

    public bool TryBind()
    {
        CacheDependencies();
        LastError = string.Empty;
        if (gateway == null || editCoordinator == null)
        {
            LastError = "No se pudo resolver la pasarela financiera o el coordinador de edición.";
            IsBound = false;
            return false;
        }

        IsBound = editCoordinator.TryBindEconomyGateway(gateway, out string error);
        LastError = error ?? string.Empty;
        return IsBound;
    }

    public void ConfigureRuntime(
        BistroBuilderEditFinanceGateway configuredGateway,
        BistroBuilderEditRuntimeCoordinator configuredCoordinator)
    {
        gateway = configuredGateway;
        editCoordinator = configuredCoordinator;
    }

    private void CacheDependencies()
    {
        if (gateway == null)
            gateway = GetComponent<BistroBuilderEditFinanceGateway>();
        if (editCoordinator == null)
            editCoordinator = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
    }
}
