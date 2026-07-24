using System;
using UnityEngine;

/// <summary>
/// Congela el reloj mediante un bloqueo apilable durante snapshots y
/// cargas. El valor persistente de pausa y velocidad puede restaurarse
/// mientras el bloqueo permanece activo, sin reanudar el mundo antes de
/// finalizar todas las secciones.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Simulation Save Participant"
)]
public sealed class BistroBuilderSimulationSaveParticipant :
    MonoBehaviour,
    IBistroBuilderSaveOperationParticipant
{
    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private int priority = 2000;

    private IDisposable simulationLock;
    private bool operationActive;

    public int Priority => priority;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseLockIfNeeded();
    }

    public bool TryBeginSaveOperation(
        BistroBuilderSaveOperationKind operationKind,
        out string rejectionMessage
    )
    {
        CacheDependenciesIfNeeded();

        if (operationActive)
        {
            rejectionMessage =
                "La simulación ya está bloqueada por otra operación.";
            return false;
        }

        if (operationKind == BistroBuilderSaveOperationKind.Delete)
        {
            operationActive = true;
            rejectionMessage = string.Empty;
            return true;
        }

        if (gameClock == null)
        {
            rejectionMessage = "No está disponible GameClock.";
            return false;
        }

        simulationLock = gameClock.AcquireSimulationLock(
            "BistroBuilder persistence operation"
        );
        operationActive = true;
        rejectionMessage = string.Empty;
        return true;
    }

    public void EndSaveOperation(
        BistroBuilderSaveOperationKind operationKind,
        bool succeeded
    )
    {
        ReleaseLockIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (gameClock == null)
        {
            error = "Falta GameClock.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ReleaseLockIfNeeded()
    {
        simulationLock?.Dispose();
        simulationLock = null;
        operationActive = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (gameClock == null)
        {
            TryGetComponent(out gameClock);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
