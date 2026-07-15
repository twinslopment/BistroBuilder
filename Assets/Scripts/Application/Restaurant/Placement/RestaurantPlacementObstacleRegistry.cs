using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantiene el registro de obstáculos fijos que afectan a la
/// colocación de mobiliario y equipamiento.
///
/// No realiza búsquedas cada fotograma. Los obstáculos se
/// descubren al iniciar y después pueden registrarse o
/// eliminarse mediante llamadas explícitas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Obstacle Registry"
)]
public sealed class RestaurantPlacementObstacleRegistry :
    MonoBehaviour
{
    [Header("Inicialización")]

    [Tooltip(
        "Busca automáticamente los obstáculos presentes en la " +
        "escena al iniciar."
    )]
    [SerializeField]
    private bool discoverSceneObstaclesOnStart = true;

    [Tooltip(
        "Incluye objetos inicialmente inactivos durante el " +
        "descubrimiento."
    )]
    [SerializeField]
    private bool includeInactiveObjects;

    [Header("Depuración")]

    [Tooltip(
        "Muestra en la Console el número de obstáculos registrados."
    )]
    [SerializeField]
    private bool logStartupSummary = true;

    private readonly HashSet<RestaurantPlacementObstacle>
        registeredObstacles =
            new HashSet<RestaurantPlacementObstacle>();

    /// <summary>
    /// Se ejecuta cuando se registra un obstáculo.
    /// </summary>
    public event Action<RestaurantPlacementObstacle>
        ObstacleRegistered;

    /// <summary>
    /// Se ejecuta cuando se elimina un obstáculo del registro.
    /// </summary>
    public event Action<RestaurantPlacementObstacle>
        ObstacleUnregistered;

    /// <summary>
    /// Se ejecuta cuando cambia la configuración de un obstáculo.
    /// </summary>
    public event Action<RestaurantPlacementObstacle>
        ObstacleChanged;

    public IReadOnlyCollection<RestaurantPlacementObstacle>
        RegisteredObstacles
    {
        get
        {
            return registeredObstacles;
        }
    }

    public int RegisteredObstacleCount
    {
        get
        {
            return registeredObstacles.Count;
        }
    }

    private void Start()
    {
        if (discoverSceneObstaclesOnStart)
        {
            RefreshFromScene();
        }

        if (logStartupSummary)
        {
            Debug.Log(
                nameof(RestaurantPlacementObstacleRegistry) +
                " ha registrado " +
                registeredObstacles.Count +
                " obstáculo(s).",
                this
            );
        }
    }

    private void OnDestroy()
    {
        ClearRegistry();
    }

    /// <summary>
    /// Elimina el registro actual y vuelve a descubrir todos los
    /// obstáculos existentes en la escena.
    /// </summary>
    public void RefreshFromScene()
    {
        ClearRegistry();

        FindObjectsInactive inactiveMode;

        if (includeInactiveObjects)
        {
            inactiveMode =
                FindObjectsInactive.Include;
        }
        else
        {
            inactiveMode =
                FindObjectsInactive.Exclude;
        }

        RestaurantPlacementObstacle[] obstacles =
            FindObjectsByType<RestaurantPlacementObstacle>(
                inactiveMode,
                FindObjectsSortMode.None
            );

        for (int index = 0;
             index < obstacles.Length;
             index++)
        {
            RegisterObstacle(
                obstacles[index]
            );
        }
    }

    /// <summary>
    /// Registra un obstáculo concreto.
    /// </summary>
    public bool RegisterObstacle(
        RestaurantPlacementObstacle obstacle
    )
    {
        if (obstacle == null)
        {
            return false;
        }

        if (!registeredObstacles.Add(obstacle))
        {
            return false;
        }

        obstacle.ObstacleChanged -=
            HandleObstacleChanged;

        obstacle.ObstacleChanged +=
            HandleObstacleChanged;

        ObstacleRegistered?.Invoke(
            obstacle
        );

        return true;
    }

    /// <summary>
    /// Elimina un obstáculo concreto del registro.
    /// </summary>
    public bool UnregisterObstacle(
        RestaurantPlacementObstacle obstacle
    )
    {
        if (obstacle == null)
        {
            return false;
        }

        if (!registeredObstacles.Remove(obstacle))
        {
            return false;
        }

        obstacle.ObstacleChanged -=
            HandleObstacleChanged;

        ObstacleUnregistered?.Invoke(
            obstacle
        );

        return true;
    }

    /// <summary>
    /// Copia todos los obstáculos registrados a una lista externa.
    /// La lista se limpia antes de rellenarse.
    /// </summary>
    public void CopyAllObstacles(
        List<RestaurantPlacementObstacle> results
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        foreach (
            RestaurantPlacementObstacle obstacle
            in registeredObstacles
        )
        {
            if (obstacle == null)
            {
                continue;
            }

            results.Add(
                obstacle
            );
        }
    }

    /// <summary>
    /// Copia únicamente los obstáculos que bloquean actualmente
    /// la colocación.
    /// </summary>
    public void CopyBlockingObstacles(
        List<RestaurantPlacementObstacle> results
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        foreach (
            RestaurantPlacementObstacle obstacle
            in registeredObstacles
        )
        {
            if (obstacle == null ||
                !obstacle.IsBlocking)
            {
                continue;
            }

            results.Add(
                obstacle
            );
        }
    }

    /// <summary>
    /// Indica si un obstáculo pertenece al registro.
    /// </summary>
    public bool ContainsObstacle(
        RestaurantPlacementObstacle obstacle
    )
    {
        return obstacle != null &&
               registeredObstacles.Contains(
                   obstacle
               );
    }

    private void HandleObstacleChanged(
        RestaurantPlacementObstacle obstacle
    )
    {
        if (obstacle == null ||
            !registeredObstacles.Contains(obstacle))
        {
            return;
        }

        ObstacleChanged?.Invoke(
            obstacle
        );
    }

    /// <summary>
    /// Elimina suscripciones y vacía completamente el registro.
    /// </summary>
    private void ClearRegistry()
    {
        foreach (
            RestaurantPlacementObstacle obstacle
            in registeredObstacles
        )
        {
            if (obstacle == null)
            {
                continue;
            }

            obstacle.ObstacleChanged -=
                HandleObstacleChanged;
        }

        registeredObstacles.Clear();
    }
}