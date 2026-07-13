using System.Collections;
using UnityEngine;

/// <summary>
/// Resuelve y valida la pertenencia espacial de los elementos
/// del restaurante.
///
/// Responsabilidades:
/// - Averiguar qué área contiene la posición de un miembro.
/// - Validar que el área asignada coincide con su posición.
/// - Actualizar la asignación cuando un objeto sea movido.
/// - Informar de elementos mal configurados.
///
/// No utiliza Update ni realiza comprobaciones continuas.
/// El futuro modo construcción llamará al servicio únicamente
/// cuando termine de colocar o mover un elemento.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantAreaAssignmentService : MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Registro central de las áreas del restaurante."
    )]
    [SerializeField]
    private RestaurantAreaRegistry areaRegistry;

    [Tooltip(
        "Registro central de los elementos asociados a áreas."
    )]
    [SerializeField]
    private RestaurantAreaMemberRegistry memberRegistry;

    [Header("Validación inicial")]

    [Tooltip(
        "Comprueba una sola vez, al iniciar la escena, que los " +
        "miembros están situados dentro del área que tienen asignada."
    )]
    [SerializeField]
    private bool validateMembersOnStart = true;

    [Tooltip(
        "Cuando está activado, solo se consideran las áreas que " +
        "están operativas. Para validación física normalmente debe " +
        "permanecer desactivado."
    )]
    [SerializeField]
    private bool operationalAreasOnly;

    private Coroutine initialValidationRoutine;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void Start()
    {
        if (!validateMembersOnStart)
        {
            return;
        }

        initialValidationRoutine =
            StartCoroutine(
                ValidateMembersAfterStartupRoutine()
            );
    }

    private void OnDisable()
    {
        if (initialValidationRoutine == null)
        {
            return;
        }

        StopCoroutine(initialValidationRoutine);
        initialValidationRoutine = null;
    }

    /// <summary>
    /// Intenta localizar el área que contiene la posición
    /// de referencia de un miembro.
    /// </summary>
    public bool TryResolveArea(
        RestaurantAreaMember member,
        out RestaurantArea resolvedArea
    )
    {
        resolvedArea = null;

        if (member == null ||
            areaRegistry == null)
        {
            return false;
        }

        return areaRegistry.TryFindAreaContainingPosition(
            member.ReferencePosition,
            out resolvedArea,
            operationalAreasOnly
        );
    }

    /// <summary>
    /// Calcula el área correspondiente a un miembro y actualiza
    /// su asignación.
    ///
    /// Este método podrá llamarse cuando el jugador termine
    /// de mover o colocar un objeto en el modo construcción.
    /// </summary>
    public bool TryAssignResolvedArea(
        RestaurantAreaMember member,
        bool clearAreaWhenOutside = false
    )
    {
        if (member == null)
        {
            return false;
        }

        if (TryResolveArea(
                member,
                out RestaurantArea resolvedArea
            ))
        {
            member.SetArea(resolvedArea);
            return true;
        }

        if (clearAreaWhenOutside)
        {
            member.ClearArea();
        }

        return false;
    }

    /// <summary>
    /// Comprueba si el área asignada al miembro coincide con
    /// el área que contiene su posición actual.
    /// </summary>
    public bool IsAssignmentValid(
        RestaurantAreaMember member,
        out RestaurantArea resolvedArea
    )
    {
        resolvedArea = null;

        if (member == null ||
            member.AssignedArea == null)
        {
            return false;
        }

        if (!TryResolveArea(
                member,
                out resolvedArea
            ))
        {
            return false;
        }

        return ReferenceEquals(
            member.AssignedArea,
            resolvedArea
        );
    }

    /// <summary>
    /// Valida todos los miembros registrados y devuelve
    /// un resumen sin crear colecciones temporales.
    /// </summary>
    public RestaurantAreaValidationResult
        ValidateAllRegisteredMembers(
            bool logDetails = true
        )
    {
        if (memberRegistry == null)
        {
            return default;
        }

        int totalCount = 0;
        int validCount = 0;
        int unassignedCount = 0;
        int outsideAreaCount = 0;
        int mismatchedCount = 0;

        foreach (RestaurantAreaMember member
                 in memberRegistry.RegisteredMembers)
        {
            if (member == null)
            {
                continue;
            }

            totalCount++;

            if (member.AssignedArea == null)
            {
                unassignedCount++;

                if (logDetails)
                {
                    Debug.LogWarning(
                        $"{member.name} no tiene un área asignada.",
                        member
                    );
                }

                continue;
            }

            if (!TryResolveArea(
                    member,
                    out RestaurantArea resolvedArea
                ))
            {
                outsideAreaCount++;

                if (logDetails)
                {
                    Debug.LogWarning(
                        $"{member.name} está fuera de todas las " +
                        $"áreas registradas. Área asignada: " +
                        $"{member.AssignedArea.AreaId}.",
                        member
                    );
                }

                continue;
            }

            if (!ReferenceEquals(
                    member.AssignedArea,
                    resolvedArea
                ))
            {
                mismatchedCount++;

                if (logDetails)
                {
                    Debug.LogWarning(
                        $"{member.name} tiene asignada el área " +
                        $"'{member.AssignedArea.AreaId}', pero su " +
                        $"posición pertenece a " +
                        $"'{resolvedArea.AreaId}'.",
                        member
                    );
                }

                continue;
            }

            validCount++;
        }

        RestaurantAreaValidationResult result =
            new RestaurantAreaValidationResult(
                totalCount,
                validCount,
                unassignedCount,
                outsideAreaCount,
                mismatchedCount
            );

        Debug.Log(
            $"Validación espacial completada. " +
            $"Total: {result.TotalCount}, " +
            $"correctos: {result.ValidCount}, " +
            $"sin asignar: {result.UnassignedCount}, " +
            $"fuera de áreas: {result.OutsideAreaCount}, " +
            $"asignación incorrecta: {result.MismatchedCount}.",
            this
        );

        return result;
    }

    /// <summary>
    /// Espera un frame para garantizar que los dos registros
    /// hayan realizado su descubrimiento inicial.
    ///
    /// Es una operación única, no una comprobación continua.
    /// </summary>
    private IEnumerator
        ValidateMembersAfterStartupRoutine()
    {
        yield return null;

        initialValidationRoutine = null;

        ValidateAllRegisteredMembers();
    }

    /// <summary>
    /// Recupera únicamente componentes del mismo GameObject.
    /// No busca objetos por toda la escena.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (areaRegistry == null)
        {
            TryGetComponent(out areaRegistry);
        }

        if (memberRegistry == null)
        {
            TryGetComponent(out memberRegistry);
        }
    }

    private void ValidateDependencies()
    {
        if (areaRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantAreaAssignmentService)} " +
                $"necesita un {nameof(RestaurantAreaRegistry)}.",
                this
            );
        }

        if (memberRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantAreaAssignmentService)} " +
                $"necesita un " +
                $"{nameof(RestaurantAreaMemberRegistry)}.",
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

/// <summary>
/// Resultado inmutable de una validación espacial completa.
/// </summary>
public readonly struct RestaurantAreaValidationResult
{
    public int TotalCount { get; }
    public int ValidCount { get; }
    public int UnassignedCount { get; }
    public int OutsideAreaCount { get; }
    public int MismatchedCount { get; }

    public bool IsValid =>
        TotalCount == ValidCount;

    public RestaurantAreaValidationResult(
        int totalCount,
        int validCount,
        int unassignedCount,
        int outsideAreaCount,
        int mismatchedCount
    )
    {
        TotalCount = totalCount;
        ValidCount = validCount;
        UnassignedCount = unassignedCount;
        OutsideAreaCount = outsideAreaCount;
        MismatchedCount = mismatchedCount;
    }
}