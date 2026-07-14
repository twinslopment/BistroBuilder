using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de huellas colocables del restaurante.
///
/// Mantiene:
/// - Las huellas registradas.
/// - La relación entre huella y miembro de área.
/// - Un índice de huellas por área.
/// - La actualización del índice cuando un objeto cambia de área.
///
/// El modo edición lo utilizará para consultar únicamente los
/// objetos relevantes de la zona candidata.
///
/// No utiliza Update ni realiza búsquedas continuas.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlacementRegistry :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Registro de miembros espaciales del restaurante."
    )]
    [SerializeField]
    private RestaurantAreaMemberRegistry memberRegistry;

    [Header("Inicialización")]

    [Tooltip(
        "Descubre una sola vez las huellas pertenecientes a los " +
        "miembros ya registrados."
    )]
    [SerializeField]
    private bool discoverRegisteredMembersOnStart = true;

    private readonly HashSet<
        RestaurantPlacementFootprint
    > registeredFootprints =
        new HashSet<RestaurantPlacementFootprint>();

    private readonly Dictionary<
        RestaurantArea,
        HashSet<RestaurantPlacementFootprint>
    > footprintsByArea =
        new Dictionary<
            RestaurantArea,
            HashSet<RestaurantPlacementFootprint>
        >();

    private readonly Dictionary<
        RestaurantPlacementFootprint,
        RestaurantAreaMember
    > memberByFootprint =
        new Dictionary<
            RestaurantPlacementFootprint,
            RestaurantAreaMember
        >();

    private readonly Dictionary<
        RestaurantAreaMember,
        RestaurantPlacementFootprint
    > footprintByMember =
        new Dictionary<
            RestaurantAreaMember,
            RestaurantPlacementFootprint
        >();

    private Coroutine initializationRoutine;

    public event Action<RestaurantPlacementFootprint>
        FootprintRegistered;

    public event Action<RestaurantPlacementFootprint>
        FootprintUnregistered;

    public int RegisteredFootprintCount =>
        registeredFootprints.Count;

    public IReadOnlyCollection<
        RestaurantPlacementFootprint
    > RegisteredFootprints =>
        registeredFootprints;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToMemberRegistry();
        RebuildAreaIndex();
    }

    private void Start()
    {
        if (!discoverRegisteredMembersOnStart)
        {
            return;
        }

        initializationRoutine =
            StartCoroutine(
                InitializeAfterRegistriesRoutine()
            );
    }

    private void OnDisable()
    {
        UnsubscribeFromMemberRegistry();

        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromMemberRegistry();

        registeredFootprints.Clear();
        footprintsByArea.Clear();
        memberByFootprint.Clear();
        footprintByMember.Clear();
    }

    /// <summary>
    /// Registra una huella colocable.
    ///
    /// La huella debe compartir GameObject con un
    /// RestaurantAreaMember registrado.
    /// </summary>
    public bool RegisterFootprint(
        RestaurantPlacementFootprint footprint
    )
    {
        if (footprint == null)
        {
            return false;
        }

        if (registeredFootprints.Contains(footprint))
        {
            return false;
        }

        if (!footprint.TryGetComponent(
                out RestaurantAreaMember member
            ))
        {
            Debug.LogError(
                $"{footprint.name} tiene una huella de " +
                $"colocación, pero no tiene un " +
                $"{nameof(RestaurantAreaMember)}.",
                footprint
            );

            return false;
        }

        if (memberRegistry == null ||
            !memberRegistry.ContainsMember(member))
        {
            Debug.LogWarning(
                $"{footprint.name} no puede registrar su huella " +
                "porque su miembro de área todavía no está " +
                "registrado.",
                footprint
            );

            return false;
        }

        registeredFootprints.Add(footprint);

        memberByFootprint.Add(
            footprint,
            member
        );

        footprintByMember.Add(
            member,
            footprint
        );

        AddFootprintToAreaIndex(
            footprint,
            member.AssignedArea
        );

        FootprintRegistered?.Invoke(footprint);

        return true;
    }

    /// <summary>
    /// Retira una huella del registro y de su índice de área.
    /// </summary>
    public bool UnregisterFootprint(
        RestaurantPlacementFootprint footprint
    )
    {
        if (footprint == null ||
            !registeredFootprints.Remove(footprint))
        {
            return false;
        }

        if (memberByFootprint.TryGetValue(
                footprint,
                out RestaurantAreaMember member
            ))
        {
            RemoveFootprintFromAreaIndex(
                footprint,
                member.AssignedArea
            );

            footprintByMember.Remove(member);
            memberByFootprint.Remove(footprint);
        }

        FootprintUnregistered?.Invoke(footprint);

        return true;
    }

    public bool ContainsFootprint(
        RestaurantPlacementFootprint footprint
    )
    {
        return footprint != null &&
               registeredFootprints.Contains(footprint);
    }

    /// <summary>
    /// Sincroniza el registro con los miembros existentes.
    ///
    /// Se ejecutará al entrar en modo edición o antes de una
    /// validación global, nunca continuamente por frame.
    /// </summary>
    public void RefreshFromRegisteredMembers()
    {
        DiscoverFootprintsFromRegisteredMembers();
        RebuildAreaIndex();
    }

    /// <summary>
    /// Obtiene el miembro espacial asociado a una huella.
    /// </summary>
    public bool TryGetMember(
        RestaurantPlacementFootprint footprint,
        out RestaurantAreaMember member
    )
    {
        member = null;

        return footprint != null &&
               memberByFootprint.TryGetValue(
                   footprint,
                   out member
               );
    }

    /// <summary>
    /// Copia las huellas de un área en una lista reutilizable.
    ///
    /// Puede excluir la huella del objeto que se está moviendo
    /// y omitir huellas que no bloquean otras colocaciones.
    /// </summary>
    public int CopyFootprintsInArea(
        RestaurantArea area,
        List<RestaurantPlacementFootprint> results,
        RestaurantPlacementFootprint excludedFootprint = null,
        bool blockingOnly = true
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        if (area == null)
        {
            return 0;
        }

        if (!footprintsByArea.TryGetValue(
                area,
                out HashSet<
                    RestaurantPlacementFootprint
                > footprints
            ))
        {
            return 0;
        }

        foreach (RestaurantPlacementFootprint footprint
                 in footprints)
        {
            if (footprint == null ||
                ReferenceEquals(
                    footprint,
                    excludedFootprint
                ))
            {
                continue;
            }

            if (blockingOnly &&
                !footprint.BlocksOtherPlacements)
            {
                continue;
            }

            results.Add(footprint);
        }

        return results.Count;
    }

    public int GetFootprintCountInArea(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return 0;
        }

        return footprintsByArea.TryGetValue(
            area,
            out HashSet<
                RestaurantPlacementFootprint
            > footprints
        )
            ? footprints.Count
            : 0;
    }

    private IEnumerator
        InitializeAfterRegistriesRoutine()
    {
        /*
         * Espera un frame para no depender del orden de Start
         * entre los distintos registros de GameSystems.
         */
        yield return null;

        initializationRoutine = null;

        DiscoverFootprintsFromRegisteredMembers();

        Debug.Log(
            $"{nameof(RestaurantPlacementRegistry)} ha " +
            $"registrado {registeredFootprints.Count} huella(s).",
            this
        );
    }

    private void DiscoverFootprintsFromRegisteredMembers()
    {
        if (memberRegistry == null)
        {
            return;
        }

        foreach (RestaurantAreaMember member
                 in memberRegistry.RegisteredMembers)
        {
            TryRegisterMemberFootprint(member);
        }
    }

    private void TryRegisterMemberFootprint(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return;
        }

        if (member.TryGetComponent(
                out RestaurantPlacementFootprint footprint
            ))
        {
            RegisterFootprint(footprint);
        }
    }

    private void AddFootprintToAreaIndex(
        RestaurantPlacementFootprint footprint,
        RestaurantArea area
    )
    {
        if (footprint == null ||
            area == null)
        {
            return;
        }

        if (!footprintsByArea.TryGetValue(
                area,
                out HashSet<
                    RestaurantPlacementFootprint
                > footprints
            ))
        {
            footprints =
                new HashSet<
                    RestaurantPlacementFootprint
                >();

            footprintsByArea.Add(
                area,
                footprints
            );
        }

        footprints.Add(footprint);
    }

    private void RemoveFootprintFromAreaIndex(
        RestaurantPlacementFootprint footprint,
        RestaurantArea area
    )
    {
        if (footprint == null ||
            area == null)
        {
            return;
        }

        if (!footprintsByArea.TryGetValue(
                area,
                out HashSet<
                    RestaurantPlacementFootprint
                > footprints
            ))
        {
            return;
        }

        footprints.Remove(footprint);

        if (footprints.Count == 0)
        {
            footprintsByArea.Remove(area);
        }
    }

    /// <summary>
    /// Reconstruye el índice a partir del área actual de cada
    /// miembro. Esto recupera correctamente cambios producidos
    /// mientras el registro estuvo desactivado.
    /// </summary>
    private void RebuildAreaIndex()
    {
        footprintsByArea.Clear();

        foreach (RestaurantPlacementFootprint footprint
                 in registeredFootprints)
        {
            if (footprint == null)
            {
                continue;
            }

            if (!memberByFootprint.TryGetValue(
                    footprint,
                    out RestaurantAreaMember member
                ))
            {
                continue;
            }

            AddFootprintToAreaIndex(
                footprint,
                member.AssignedArea
            );
        }
    }

    private void SubscribeToMemberRegistry()
    {
        if (memberRegistry == null)
        {
            return;
        }

        memberRegistry.MemberRegistered -=
            HandleMemberRegistered;

        memberRegistry.MemberUnregistered -=
            HandleMemberUnregistered;

        memberRegistry.MemberAreaChanged -=
            HandleMemberAreaChanged;

        memberRegistry.MemberRegistered +=
            HandleMemberRegistered;

        memberRegistry.MemberUnregistered +=
            HandleMemberUnregistered;

        memberRegistry.MemberAreaChanged +=
            HandleMemberAreaChanged;
    }

    private void UnsubscribeFromMemberRegistry()
    {
        if (memberRegistry == null)
        {
            return;
        }

        memberRegistry.MemberRegistered -=
            HandleMemberRegistered;

        memberRegistry.MemberUnregistered -=
            HandleMemberUnregistered;

        memberRegistry.MemberAreaChanged -=
            HandleMemberAreaChanged;
    }

    private void HandleMemberRegistered(
        RestaurantAreaMember member
    )
    {
        TryRegisterMemberFootprint(member);
    }

    private void HandleMemberUnregistered(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return;
        }

        if (footprintByMember.TryGetValue(
                member,
                out RestaurantPlacementFootprint footprint
            ))
        {
            UnregisterFootprint(footprint);
        }
    }

    private void HandleMemberAreaChanged(
        RestaurantAreaMember member,
        RestaurantArea previousArea,
        RestaurantArea newArea
    )
    {
        if (member == null)
        {
            return;
        }

        if (!footprintByMember.TryGetValue(
                member,
                out RestaurantPlacementFootprint footprint
            ))
        {
            return;
        }

        RemoveFootprintFromAreaIndex(
            footprint,
            previousArea
        );

        AddFootprintToAreaIndex(
            footprint,
            newArea
        );
    }

    private void CacheDependenciesIfNeeded()
    {
        if (memberRegistry == null)
        {
            TryGetComponent(out memberRegistry);
        }
    }

    private void ValidateDependencies()
    {
        if (memberRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantPlacementRegistry)} necesita " +
                $"un {nameof(RestaurantAreaMemberRegistry)} en el " +
                "mismo GameObject o asignado en el Inspector.",
                this
            );
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