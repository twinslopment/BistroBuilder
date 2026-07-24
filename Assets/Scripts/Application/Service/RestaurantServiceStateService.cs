using System;
using UnityEngine;

/// <summary>
/// Mantiene el estado operativo global del servicio del restaurante.
///
/// Este servicio distingue claramente:
/// - Restaurante cerrado.
/// - Preparación previa.
/// - Servicio abierto.
/// - Cierre operativo.
///
/// La pausa del reloj no modifica este estado. Un servicio pausado
/// continúa siendo un servicio activo y sigue bloqueando el modo edición.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Restaurant Service State Service"
)]
public sealed class RestaurantServiceStateService :
    MonoBehaviour
{
    [Header("Estado inicial")]

    [Tooltip(
        "Estado con el que comienza la escena. Para el flujo final " +
        "debe ser Closed hasta completar validación y briefing."
    )]
    [SerializeField]
    private RestaurantServiceState initialState =
        RestaurantServiceState.Closed;

    [Header("Depuración")]

    [Tooltip(
        "Escribe en la Console cada transición de estado."
    )]
    [SerializeField]
    private bool logStateTransitions = true;

    private RestaurantServiceState currentState =
        RestaurantServiceState.Closed;

    /// <summary>
    /// Se ejecuta después de cambiar el estado.
    /// Recibe el estado anterior y el nuevo.
    /// </summary>
    public event Action<
        RestaurantServiceState,
        RestaurantServiceState
    > StateChanged;

    /// <summary>
    /// Se ejecuta al comenzar a aceptar clientes.
    /// </summary>
    public event Action ServiceOpened;

    /// <summary>
    /// Se ejecuta cuando el restaurante queda completamente cerrado.
    /// </summary>
    public event Action ServiceClosed;

    public RestaurantServiceState CurrentState
    {
        get
        {
            return currentState;
        }
    }

    public bool IsClosed
    {
        get
        {
            return currentState ==
                   RestaurantServiceState.Closed;
        }
    }

    /// <summary>
    /// Indica si existe un bloque operativo en curso.
    ///
    /// Preparación, apertura y cierre impiden modificar físicamente
    /// el restaurante.
    /// </summary>
    public bool IsServiceInProgress
    {
        get
        {
            return currentState !=
                   RestaurantServiceState.Closed;
        }
    }

    /// <summary>
    /// Solo el estado Open permite generar nuevas llegadas.
    /// </summary>
    public bool AcceptsNewCustomers
    {
        get
        {
            return currentState ==
                   RestaurantServiceState.Open;
        }
    }

    /// <summary>
    /// El modo edición solo está permitido estando Closed.
    /// </summary>
    public bool BlocksEditMode
    {
        get
        {
            return currentState !=
                   RestaurantServiceState.Closed;
        }
    }

    private void Awake()
    {
        currentState = initialState;
    }

    private void Start()
    {
        if (!logStateTransitions)
        {
            return;
        }

        Debug.Log(
            "Estado inicial del servicio: " +
            currentState +
            ".",
            this
        );
    }

    /// <summary>
    /// Pasa de Closed a Preparing.
    /// </summary>
    public bool TryBeginPreparation()
    {
        if (currentState !=
            RestaurantServiceState.Closed)
        {
            return false;
        }

        return ChangeState(
            RestaurantServiceState.Preparing
        );
    }

    /// <summary>
    /// Abre el servicio.
    ///
    /// Durante el prototipo se permite abrir directamente desde
    /// Closed. El flujo final podrá pasar antes por Preparing.
    /// </summary>
    public bool TryOpenService()
    {
        if (currentState !=
                RestaurantServiceState.Closed &&
            currentState !=
                RestaurantServiceState.Preparing)
        {
            return false;
        }

        return ChangeState(
            RestaurantServiceState.Open
        );
    }

    /// <summary>
    /// Detiene nuevas llegadas y comienza el cierre operativo.
    /// </summary>
    public bool TryBeginClosing()
    {
        if (currentState !=
            RestaurantServiceState.Open)
        {
            return false;
        }

        return ChangeState(
            RestaurantServiceState.Closing
        );
    }

    /// <summary>
    /// Completa un cierre iniciado previamente.
    /// </summary>
    public bool TryCompleteClosing()
    {
        if (currentState !=
            RestaurantServiceState.Closing)
        {
            return false;
        }

        return ChangeState(
            RestaurantServiceState.Closed
        );
    }

    /// <summary>
    /// Cierra inmediatamente cualquier fase activa.
    ///
    /// Se reserva para reinicios, pruebas y recuperación segura.
    /// El cierre normal debe utilizar TryBeginClosing y
    /// TryCompleteClosing.
    /// </summary>
    public bool TryCloseServiceImmediately()
    {
        if (currentState ==
            RestaurantServiceState.Closed)
        {
            return false;
        }

        return ChangeState(
            RestaurantServiceState.Closed
        );
    }

    /// <summary>
    /// Restaura directamente un estado persistente sin simular la cadena
    /// de transiciones jugables que llevó hasta él.
    ///
    /// La carga compleja puede aplicarlo al final, una vez reconstruidos
    /// clientes, comandas, cocina y tareas de personal.
    /// </summary>
    public bool TryRestoreState(
        RestaurantServiceState restoredState,
        bool publishEvents = true
    )
    {
        if (!Enum.IsDefined(
                typeof(RestaurantServiceState),
                restoredState
            ))
        {
            return false;
        }

        if (currentState == restoredState)
        {
            return true;
        }

        ApplyState(restoredState, publishEvents, true);
        return true;
    }

    /// <summary>
    /// Aplica una transición jugable normal y publica eventos.
    /// </summary>
    private bool ChangeState(
        RestaurantServiceState nextState
    )
    {
        if (currentState == nextState)
        {
            return false;
        }

        ApplyState(nextState, true, false);
        return true;
    }

    private void ApplyState(
        RestaurantServiceState nextState,
        bool publishEvents,
        bool isRestoration
    )
    {
        RestaurantServiceState previousState =
            currentState;

        currentState = nextState;

        if (logStateTransitions)
        {
            Debug.Log(
                isRestoration
                    ? "Estado del servicio restaurado: " +
                      previousState + " -> " + currentState + "."
                    : "Estado del servicio: " +
                      previousState + " -> " + currentState + ".",
                this
            );
        }

        if (!publishEvents)
        {
            return;
        }

        StateChanged?.Invoke(
            previousState,
            currentState
        );

        if (currentState ==
            RestaurantServiceState.Open)
        {
            ServiceOpened?.Invoke();
        }

        if (currentState ==
            RestaurantServiceState.Closed)
        {
            ServiceClosed?.Invoke();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Acción temporal del Inspector para probar la apertura.
    /// </summary>
    [ContextMenu("Debug/Open Service")]
    private void DebugOpenService()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "La acción Debug/Open Service solo debe usarse " +
                "durante Play.",
                this
            );

            return;
        }

        if (!TryOpenService())
        {
            Debug.LogWarning(
                "No se pudo abrir el servicio desde el estado " +
                currentState +
                ".",
                this
            );
        }
    }

    /// <summary>
    /// Acción temporal del Inspector para iniciar el cierre.
    /// </summary>
    [ContextMenu("Debug/Begin Closing")]
    private void DebugBeginClosing()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "La acción Debug/Begin Closing solo debe usarse " +
                "durante Play.",
                this
            );

            return;
        }

        if (!TryBeginClosing())
        {
            Debug.LogWarning(
                "No se pudo iniciar el cierre desde el estado " +
                currentState +
                ".",
                this
            );
        }
    }

    /// <summary>
    /// Acción temporal del Inspector para completar el cierre.
    /// </summary>
    [ContextMenu("Debug/Complete Closing")]
    private void DebugCompleteClosing()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "La acción Debug/Complete Closing solo debe usarse " +
                "durante Play.",
                this
            );

            return;
        }

        if (!TryCompleteClosing())
        {
            Debug.LogWarning(
                "No se pudo completar el cierre desde el estado " +
                currentState +
                ".",
                this
            );
        }
    }

    /// <summary>
    /// Acción temporal para devolver el prototipo a Closed.
    /// </summary>
    [ContextMenu("Debug/Close Immediately")]
    private void DebugCloseImmediately()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "La acción Debug/Close Immediately solo debe usarse " +
                "durante Play.",
                this
            );

            return;
        }

        if (!TryCloseServiceImmediately())
        {
            Debug.LogWarning(
                "El servicio ya está cerrado.",
                this
            );
        }
    }

    private void OnValidate()
    {
        if (!Enum.IsDefined(
                typeof(RestaurantServiceState),
                initialState
            ))
        {
            initialState =
                RestaurantServiceState.Closed;
        }
    }
#endif
}

/// <summary>
/// Fase operativa actual del restaurante.
/// </summary>
public enum RestaurantServiceState
{
    Closed = 0,
    Preparing = 1,
    Open = 2,
    Closing = 3
}
