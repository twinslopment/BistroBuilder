using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de los elementos asociados a las áreas
/// del restaurante.
///
/// Permite:
/// - Registrar y retirar miembros.
/// - Consultar miembros por área.
/// - Mantener los índices actualizados cuando un miembro cambia
///   de zona.
/// - Obtener componentes concretos dentro de una zona.
///
/// No utiliza Update ni realiza búsquedas continuas.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantAreaMemberRegistry : MonoBehaviour
{
    [Header("Descubrimiento inicial")]

    [Tooltip(
        "Busca una sola vez al iniciar la escena los miembros " +
        "que ya existen."
    )]
    [SerializeField]
    private bool discoverSceneMembersOnStart = true;

    /// <summary>
    /// Todos los miembros registrados, incluidos los que todavía
    /// no tienen un área asignada.
    /// </summary>
    private readonly HashSet<RestaurantAreaMember>
        registeredMembers =
            new HashSet<RestaurantAreaMember>();

    /// <summary>
    /// Índice de miembros por área.
    /// </summary>
    private readonly Dictionary<
        RestaurantArea,
        HashSet<RestaurantAreaMember>
    > membersByArea =
        new Dictionary<
            RestaurantArea,
            HashSet<RestaurantAreaMember>
        >();

    public event Action<RestaurantAreaMember>
        MemberRegistered;

    public event Action<RestaurantAreaMember>
        MemberUnregistered;

    public event Action<
        RestaurantAreaMember,
        RestaurantArea,
        RestaurantArea
    > MemberAreaChanged;

    public int RegisteredMemberCount =>
        registeredMembers.Count;

    public IReadOnlyCollection<RestaurantAreaMember>
        RegisteredMembers =>
            registeredMembers;

    private void Start()
    {
        if (discoverSceneMembersOnStart)
        {
            DiscoverExistingSceneMembers();
        }

        Debug.Log(
            $"RestaurantAreaMemberRegistry ha registrado " +
            $"{registeredMembers.Count} miembro(s).",
            this
        );
    }

    private void OnDisable()
    {
        UnsubscribeFromRegisteredMembers();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRegisteredMembers();

        registeredMembers.Clear();
        membersByArea.Clear();
    }

    /// <summary>
    /// Registra un elemento espacial.
    ///
    /// También pueden registrarse miembros todavía sin área.
    /// Esto permite que objetos móviles o recién construidos
    /// reciban posteriormente una asignación.
    /// </summary>
    public bool RegisterMember(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return false;
        }

        if (!registeredMembers.Add(member))
        {
            return false;
        }

        SubscribeToMember(member);
        AddMemberToAreaIndex(
            member,
            member.AssignedArea
        );

        MemberRegistered?.Invoke(member);

        return true;
    }

    /// <summary>
    /// Retira un elemento del registro y de su índice de área.
    /// </summary>
    public bool UnregisterMember(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return false;
        }

        if (!registeredMembers.Remove(member))
        {
            return false;
        }

        UnsubscribeFromMember(member);

        RemoveMemberFromAreaIndex(
            member,
            member.AssignedArea
        );

        MemberUnregistered?.Invoke(member);

        return true;
    }

    /// <summary>
    /// Comprueba si un elemento está registrado.
    /// </summary>
    public bool ContainsMember(
        RestaurantAreaMember member
    )
    {
        return member != null &&
               registeredMembers.Contains(member);
    }

    /// <summary>
    /// Copia los miembros de un área en una lista reutilizable.
    ///
    /// La lista se limpia antes de rellenarse para evitar crear
    /// colecciones nuevas en cada consulta.
    /// </summary>
    public int CopyMembersInArea(
        RestaurantArea area,
        List<RestaurantAreaMember> results
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

        if (!membersByArea.TryGetValue(
                area,
                out HashSet<RestaurantAreaMember> members
            ))
        {
            return 0;
        }

        foreach (RestaurantAreaMember member in members)
        {
            if (member != null)
            {
                results.Add(member);
            }
        }

        return results.Count;
    }

    /// <summary>
    /// Copia los componentes de un tipo concreto presentes
    /// en los miembros de un área.
    ///
    /// Ejemplos:
    /// - RestaurantTable dentro de un comedor.
    /// - KitchenSystem dentro de una cocina.
    ///
    /// No utiliza LINQ y no crea listas internas.
    /// </summary>
    public int CopyComponentsInArea<T>(
        RestaurantArea area,
        List<T> results
    )
        where T : Component
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

        if (!membersByArea.TryGetValue(
                area,
                out HashSet<RestaurantAreaMember> members
            ))
        {
            return 0;
        }

        foreach (RestaurantAreaMember member in members)
        {
            if (member == null)
            {
                continue;
            }

            if (member.TryGetComponent(
                    out T component
                ))
            {
                results.Add(component);
            }
        }

        return results.Count;
    }

    /// <summary>
    /// Devuelve el número de miembros asociados a un área.
    /// </summary>
    public int GetMemberCount(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return 0;
        }

        return membersByArea.TryGetValue(
            area,
            out HashSet<RestaurantAreaMember> members
        )
            ? members.Count
            : 0;
    }

    /// <summary>
    /// Descubre una sola vez los miembros existentes en la escena.
    /// </summary>
    private void DiscoverExistingSceneMembers()
    {
        RestaurantAreaMember[] sceneMembers =
            FindObjectsByType<RestaurantAreaMember>(
                FindObjectsSortMode.None
            );

        foreach (RestaurantAreaMember member
                 in sceneMembers)
        {
            RegisterMember(member);
        }
    }

    private void AddMemberToAreaIndex(
        RestaurantAreaMember member,
        RestaurantArea area
    )
    {
        if (member == null ||
            area == null)
        {
            return;
        }

        if (!membersByArea.TryGetValue(
                area,
                out HashSet<RestaurantAreaMember> members
            ))
        {
            members =
                new HashSet<RestaurantAreaMember>();

            membersByArea.Add(
                area,
                members
            );
        }

        members.Add(member);
    }

    private void RemoveMemberFromAreaIndex(
        RestaurantAreaMember member,
        RestaurantArea area
    )
    {
        if (member == null ||
            area == null)
        {
            return;
        }

        if (!membersByArea.TryGetValue(
                area,
                out HashSet<RestaurantAreaMember> members
            ))
        {
            return;
        }

        members.Remove(member);

        if (members.Count == 0)
        {
            membersByArea.Remove(area);
        }
    }

    private void SubscribeToMember(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return;
        }

        member.AreaChanged -=
            HandleMemberAreaChanged;

        member.AreaChanged +=
            HandleMemberAreaChanged;
    }

    private void UnsubscribeFromMember(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return;
        }

        member.AreaChanged -=
            HandleMemberAreaChanged;
    }

    private void UnsubscribeFromRegisteredMembers()
    {
        foreach (RestaurantAreaMember member
                 in registeredMembers)
        {
            UnsubscribeFromMember(member);
        }
    }

    private void HandleMemberAreaChanged(
        RestaurantAreaMember member,
        RestaurantArea previousArea,
        RestaurantArea newArea
    )
    {
        if (member == null ||
            !registeredMembers.Contains(member))
        {
            return;
        }

        RemoveMemberFromAreaIndex(
            member,
            previousArea
        );

        AddMemberToAreaIndex(
            member,
            newArea
        );

        MemberAreaChanged?.Invoke(
            member,
            previousArea,
            newArea
        );
    }
}