using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representación visual básica de 2.2B.
///
/// Escucha recepciones ya confirmadas por la capa de aplicación y muestra,
/// de una en una, un único repartidor temporal: entra por suministros con
/// cajas, llega al almacén, descarga y sale por el mismo acceso.
///
/// La cola es exclusivamente visual. No representa pedidos, personal,
/// vehículos, rutas logísticas ni existencias en tránsito.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Goods Receiving Presentation")]
public sealed class BistroBuilderGoodsReceivingPresentation : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderGoodsReceivingService receivingService;

    [SerializeField]
    private BistroBuilderGoodsReceivingRoute route;

    [Header("Representación temporal")]

    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2.4f;

    [SerializeField, Min(0f)]
    private float unloadDurationSeconds = 1.25f;

    [SerializeField, Min(0.25f)]
    private float exteriorSpawnDistance = 1.5f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    [SerializeField]
    private bool logVisualFlow = true;

    private readonly Queue<BistroBuilderGoodsReceiptSnapshot> visualQueue =
        new Queue<BistroBuilderGoodsReceiptSnapshot>();

    private Coroutine activeRoutine;
    private BistroBuilderSupplyDeliveryVisual activeVisual;
    private BistroBuilderGoodsReceiptSnapshot activeReceipt;

    public event Action<
        BistroBuilderGoodsReceivingPresentation,
        BistroBuilderGoodsReceiptSnapshot,
        BistroBuilderGoodsReceivingVisualState
    > VisualStateChanged;

    public BistroBuilderGoodsReceivingVisualState CurrentState { get; private set; }
        = BistroBuilderGoodsReceivingVisualState.Idle;

    public BistroBuilderSupplyDeliveryVisual ActiveVisual => activeVisual;

    public BistroBuilderGoodsReceiptSnapshot ActiveReceipt => activeReceipt;

    public bool IsBusy => activeRoutine != null || visualQueue.Count > 0;

    public int ActiveVisualCount => activeVisual != null ? 1 : 0;

    public int PendingVisualCount => visualQueue.Count;

    public BistroBuilderGoodsReceivingRoute Route => route;

    public BistroBuilderGoodsReceivingService ReceivingService =>
        receivingService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        visualQueue.Clear();

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        DestroyActiveVisual();
        activeReceipt = null;
        CurrentState = BistroBuilderGoodsReceivingVisualState.Idle;
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (receivingService == null)
        {
            error = "Falta BistroBuilderGoodsReceivingService.";
            return false;
        }

        if (!receivingService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (route == null)
        {
            error = "Falta BistroBuilderGoodsReceivingRoute.";
            return false;
        }

        if (!route.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!IsFinitePositive(movementSpeed) ||
            !IsFiniteNonNegative(unloadDurationSeconds) ||
            !IsFinitePositive(exteriorSpawnDistance) ||
            !IsFinitePositive(arrivalDistance))
        {
            error = "La configuración temporal del reparto contiene valores " +
                    "de movimiento o duración inválidos.";
            return false;
        }

        return true;
    }

    private void HandleReceiptAccepted(BistroBuilderGoodsReceiptSnapshot receipt)
    {
        if (receipt == null || receipt.WasReplayed)
        {
            return;
        }

        visualQueue.Enqueue(receipt);
        TryStartNextVisual();
    }

    private void TryStartNextVisual()
    {
        if (!isActiveAndEnabled || activeRoutine != null ||
            visualQueue.Count == 0)
        {
            return;
        }

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(
                "No se puede representar la recepción 2.2B. " + error,
                this
            );
            visualQueue.Clear();
            return;
        }

        activeReceipt = visualQueue.Dequeue();
        activeRoutine = StartCoroutine(PlayVisualRoutine(activeReceipt));
    }

    private IEnumerator PlayVisualRoutine(BistroBuilderGoodsReceiptSnapshot receipt)
    {
        Vector3 exterior = route.GetExteriorSpawnPosition(
            exteriorSpawnDistance
        );
        activeVisual = BistroBuilderSupplyDeliveryVisual.Create(
            transform,
            exterior
        );
        activeVisual.SetBoxesVisible(true);

        SetState(receipt, BistroBuilderGoodsReceivingVisualState.Entering);
        yield return activeVisual.MoveTo(
            route.SupplyAccessPoint.position,
            movementSpeed,
            arrivalDistance
        );

        SetState(
            receipt,
            BistroBuilderGoodsReceivingVisualState.GoingToWarehouse
        );
        yield return activeVisual.MoveTo(
            route.WarehouseDropPoint.position,
            movementSpeed,
            arrivalDistance
        );

        SetState(receipt, BistroBuilderGoodsReceivingVisualState.Unloading);
        if (unloadDurationSeconds > 0f)
        {
            yield return new WaitForSeconds(unloadDurationSeconds);
        }
        activeVisual.SetBoxesVisible(false);

        SetState(
            receipt,
            BistroBuilderGoodsReceivingVisualState.ReturningToSupplyAccess
        );
        yield return activeVisual.MoveTo(
            route.SupplyAccessPoint.position,
            movementSpeed,
            arrivalDistance
        );

        SetState(receipt, BistroBuilderGoodsReceivingVisualState.Exiting);
        yield return activeVisual.MoveTo(
            exterior,
            movementSpeed,
            arrivalDistance
        );

        DestroyActiveVisual();
        SetState(receipt, BistroBuilderGoodsReceivingVisualState.Completed);

        activeReceipt = null;
        activeRoutine = null;
        CurrentState = BistroBuilderGoodsReceivingVisualState.Idle;
        TryStartNextVisual();
    }

    private void SetState(
        BistroBuilderGoodsReceiptSnapshot receipt,
        BistroBuilderGoodsReceivingVisualState state
    )
    {
        CurrentState = state;

        if (logVisualFlow)
        {
            Debug.Log(
                "Recepción visual " +
                (receipt != null ? receipt.ReceiptId : "sin_id") +
                ": " + state + ".",
                this
            );
        }

        Action<
            BistroBuilderGoodsReceivingPresentation,
            BistroBuilderGoodsReceiptSnapshot,
            BistroBuilderGoodsReceivingVisualState
        > handlers = VisualStateChanged;

        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<
                    BistroBuilderGoodsReceivingPresentation,
                    BistroBuilderGoodsReceiptSnapshot,
                    BistroBuilderGoodsReceivingVisualState
                >)invocationList[index]).Invoke(this, receipt, state);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void DestroyActiveVisual()
    {
        if (activeVisual != null)
        {
            Destroy(activeVisual.gameObject);
            activeVisual = null;
        }
    }

    private void Subscribe()
    {
        if (receivingService != null)
        {
            receivingService.ReceiptAccepted -= HandleReceiptAccepted;
            receivingService.ReceiptAccepted += HandleReceiptAccepted;
        }
    }

    private void Unsubscribe()
    {
        if (receivingService != null)
        {
            receivingService.ReceiptAccepted -= HandleReceiptAccepted;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (receivingService == null)
        {
            receivingService = GetComponent<BistroBuilderGoodsReceivingService>();
        }

        if (route == null)
        {
            BistroBuilderGoodsReceivingRoute[] routes =
                UnityEngine.Object.FindObjectsByType<
                    BistroBuilderGoodsReceivingRoute
                >(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );
            if (routes.Length > 0)
            {
                route = routes[0];
            }
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0.1f, movementSpeed);
        unloadDurationSeconds = Mathf.Max(0f, unloadDurationSeconds);
        exteriorSpawnDistance = Mathf.Max(0.25f, exteriorSpawnDistance);
        arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
        CacheDependenciesIfNeeded();
    }
#endif
}
