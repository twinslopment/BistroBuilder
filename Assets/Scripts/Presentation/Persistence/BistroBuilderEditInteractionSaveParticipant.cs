using UnityEngine;

/// <summary>
/// Bloquea la interacción del modo edición mientras se captura o aplica
/// una partida.
///
/// El guard comprueba que no exista una transacción incompleta antes de
/// comenzar. Este participante evita que el jugador inicie una nueva
/// modificación durante una captura distribuida entre varios frames.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Edit Interaction Save Participant"
)]
public sealed class BistroBuilderEditInteractionSaveParticipant :
    MonoBehaviour,
    IBistroBuilderSaveOperationParticipant
{
    [SerializeField]
    private RestaurantEditInteractionController
        editInteractionController;

    [SerializeField]
    private int priority = 1000;

    private bool operationActive;
    private bool controllerWasEnabled;

    public int Priority => priority;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnDisable()
    {
        RestoreControllerIfNeeded();
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
                "La interacción de edición ya está bloqueada por otra " +
                "operación de persistencia.";
            return false;
        }

        if (editInteractionController == null)
        {
            rejectionMessage =
                "No está disponible el controlador del modo edición.";
            return false;
        }

        controllerWasEnabled = editInteractionController.enabled;
        editInteractionController.enabled = false;
        operationActive = true;
        rejectionMessage = string.Empty;
        return true;
    }

    public void EndSaveOperation(
        BistroBuilderSaveOperationKind operationKind,
        bool succeeded
    )
    {
        RestoreControllerIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (editInteractionController == null)
        {
            error = "Falta RestaurantEditInteractionController.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RestoreControllerIfNeeded()
    {
        if (!operationActive)
        {
            return;
        }

        if (editInteractionController != null)
        {
            editInteractionController.enabled = controllerWasEnabled;
        }

        operationActive = false;
        controllerWasEnabled = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editInteractionController == null)
        {
            TryGetComponent(out editInteractionController);
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
