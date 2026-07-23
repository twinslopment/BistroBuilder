using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina movimientos y rotaciones atómicos de conjuntos enlazados.
///
/// Mantiene las relaciones lógicas sin modificar la jerarquía permanente:
/// - Captura las poses iniciales de todos los seguidores.
/// - Calcula cada pose candidata a partir del delta de la raíz.
/// - Permite validar el conjunto completo antes de confirmar.
/// - Restaura todo el conjunto al cancelar.
/// - Construye un único comando de historial para Undo/Redo atómico.
///
/// El núcleo no contiene reglas específicas de mesas, sillas, muebles o
/// decoración.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Linked Group Service"
)]
public sealed class RestaurantPlacementLinkedGroupService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlacementValidationService validationService;

    [SerializeField]
    private RestaurantPlacementTransactionService transactionService;

    [Header("Inicialización")]

    [SerializeField]
    private bool discoverProvidersAutomatically = true;

    [Header("Depuración")]

    [SerializeField]
    private bool logGroupSummary = true;

    private readonly List<IRestaurantPlacementLinkedGroupProvider>
        providers =
            new List<IRestaurantPlacementLinkedGroupProvider>(8);

    private readonly List<RestaurantAreaMember>
        activeFollowers =
            new List<RestaurantAreaMember>(16);

    private readonly List<RestaurantPlacementStateSnapshot>
        activeFollowerBeforeStates =
            new List<RestaurantPlacementStateSnapshot>(16);

    private readonly List<Vector3>
        activeFollowerRelativePositions =
            new List<Vector3>(16);

    private readonly List<Quaternion>
        activeFollowerRelativeRotations =
            new List<Quaternion>(16);

    private readonly HashSet<int>
        followerInstanceIds =
            new HashSet<int>();

    private readonly List<RestaurantAreaMember>
        scratchFollowers =
            new List<RestaurantAreaMember>(16);

    private RestaurantAreaMember activeRoot;
    private Vector3 activeRootOriginalPosition;
    private Quaternion activeRootOriginalRotation =
        Quaternion.identity;

    public event Action<RestaurantAreaMember, int>
        LinkedGroupSessionStarted;

    public event Action LinkedGroupSessionEnded;

    public bool HasActiveGroup =>
        activeRoot != null &&
        activeFollowers.Count > 0;

    public RestaurantAreaMember ActiveRoot =>
        activeRoot;

    public int ActiveFollowerCount =>
        activeFollowers.Count;

    public int RegisteredProviderCount =>
        providers.Count;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        RefreshProviders();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void Start()
    {
        if (logGroupSummary)
        {
            Debug.Log(
                nameof(RestaurantPlacementLinkedGroupService) +
                " ha registrado " +
                providers.Count +
                " proveedor(es) de grupos enlazados.",
                this
            );
        }
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (activeRoot != null)
        {
            RestoreFollowersToInitialState();
        }

        EndSessionInternal();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (validationService == null)
        {
            error =
                name +
                " necesita RestaurantPlacementValidationService.";

            return false;
        }

        if (transactionService == null)
        {
            error =
                name +
                " necesita RestaurantPlacementTransactionService.";

            return false;
        }

        if (providers.Count <= 0)
        {
            error =
                name +
                " no ha registrado proveedores de grupos enlazados.";

            return false;
        }

        return true;
    }

    /// <summary>
    /// Reconstruye la lista de proveedores instalados en GameSystems.
    /// </summary>
    public void RefreshProviders()
    {
        providers.Clear();

        if (!discoverProvidersAutomatically)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        for (int index = 0;
             index < behaviours.Length;
             index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour == null ||
                ReferenceEquals(behaviour, this) ||
                !(behaviour is
                    IRestaurantPlacementLinkedGroupProvider provider))
            {
                continue;
            }

            providers.Add(provider);
        }

        providers.Sort(CompareProviders);
    }

    /// <summary>
    /// Captura una sesión para la raíz indicada. Una raíz sin seguidores
    /// continúa usando el movimiento individual existente.
    /// </summary>
    public void BeginSession(
        RestaurantAreaMember rootMember
    )
    {
        EndSessionInternal();

        activeRoot = rootMember;

        if (rootMember == null)
        {
            return;
        }

        activeRootOriginalPosition =
            rootMember.transform.position;

        activeRootOriginalRotation =
            rootMember.transform.rotation;

        CollectFollowers(
            rootMember,
            activeFollowers
        );

        Quaternion inverseRootRotation =
            Quaternion.Inverse(
                activeRootOriginalRotation
            );

        for (int index = 0;
             index < activeFollowers.Count;
             index++)
        {
            RestaurantAreaMember follower =
                activeFollowers[index];

            if (follower == null)
            {
                activeFollowerBeforeStates.Add(default);
                activeFollowerRelativePositions.Add(default);
                activeFollowerRelativeRotations.Add(
                    Quaternion.identity
                );

                continue;
            }

            Transform followerTransform =
                follower.transform;

            activeFollowerBeforeStates.Add(
                RestaurantPlacementStateSnapshot.Capture(
                    follower
                )
            );

            activeFollowerRelativePositions.Add(
                inverseRootRotation *
                (
                    followerTransform.position -
                    activeRootOriginalPosition
                )
            );

            activeFollowerRelativeRotations.Add(
                inverseRootRotation *
                followerTransform.rotation
            );
        }

        if (activeFollowers.Count > 0)
        {
            LinkedGroupSessionStarted?.Invoke(
                rootMember,
                activeFollowers.Count
            );
        }
    }

    /// <summary>
    /// Preaplica la pose candidata a la raíz y a sus seguidores antes de
    /// que la transacción ejecute la validación definitiva.
    ///
    /// Las poses se calculan siempre desde la captura inicial, evitando
    /// acumulación de error o deriva tras muchos movimientos del cursor.
    /// </summary>
    public void PreparePreviewPose(
        RestaurantAreaMember rootMember,
        Vector3 candidateWorldPosition,
        Quaternion candidateWorldRotation
    )
    {
        if (!HasActiveGroup ||
            !ReferenceEquals(activeRoot, rootMember))
        {
            return;
        }

        rootMember.transform.SetPositionAndRotation(
            candidateWorldPosition,
            candidateWorldRotation
        );

        for (int index = 0;
             index < activeFollowers.Count;
             index++)
        {
            RestaurantAreaMember follower =
                activeFollowers[index];

            if (follower == null)
            {
                continue;
            }

            Vector3 followerPosition =
                candidateWorldPosition +
                candidateWorldRotation *
                activeFollowerRelativePositions[index];

            Quaternion followerRotation =
                candidateWorldRotation *
                activeFollowerRelativeRotations[index];

            follower.transform.SetPositionAndRotation(
                followerPosition,
                followerRotation
            );
        }

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Copia los miembros enlazados a una raíz sin realizar asignaciones.
    /// </summary>
    public int CopyLinkedMembers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results
    )
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();

        if (rootMember == null)
        {
            return 0;
        }

        if (ReferenceEquals(activeRoot, rootMember))
        {
            for (int index = 0;
                 index < activeFollowers.Count;
                 index++)
            {
                RestaurantAreaMember follower =
                    activeFollowers[index];

                if (follower != null &&
                    follower.gameObject.activeInHierarchy)
                {
                    results.Add(follower);
                }
            }

            return results.Count;
        }

        CollectFollowers(rootMember, results);
        return results.Count;
    }

    /// <summary>
    /// Construye un único comando de historial para la raíz y todos sus
    /// seguidores. Se invoca durante PlacementCommittedWithHistory,
    /// antes de cerrar la sesión desde el controlador.
    /// </summary>
    public bool TryBuildHistoryCommand(
        RestaurantPlacementCommittedChange rootChange,
        bool validateDestinationBeforeApplying,
        out IRestaurantEditHistoryCommand command
    )
    {
        command = null;

        if (!HasActiveGroup ||
            rootChange.Member == null ||
            !ReferenceEquals(
                rootChange.Member,
                activeRoot
            ) ||
            rootChange.TransactionKind !=
                RestaurantPlacementTransactionKind.MoveExisting)
        {
            return false;
        }

        int followerCount =
            activeFollowers.Count;

        RestaurantAreaMember[] members =
            new RestaurantAreaMember[followerCount + 1];

        RestaurantPlacementStateSnapshot[] before =
            new RestaurantPlacementStateSnapshot[
                followerCount + 1
            ];

        RestaurantPlacementStateSnapshot[] after =
            new RestaurantPlacementStateSnapshot[
                followerCount + 1
            ];

        members[0] = rootChange.Member;
        before[0] = rootChange.Before;
        after[0] = rootChange.After;

        for (int index = 0;
             index < followerCount;
             index++)
        {
            RestaurantAreaMember follower =
                activeFollowers[index];

            members[index + 1] = follower;
            before[index + 1] =
                activeFollowerBeforeStates[index];
            after[index + 1] =
                RestaurantPlacementStateSnapshot.Capture(
                    follower
                );
        }

        RestaurantMoveLinkedGroupHistoryCommand
            groupCommand =
                new RestaurantMoveLinkedGroupHistoryCommand(
                    activeRoot,
                    members,
                    before,
                    after,
                    validationService,
                    this,
                    validateDestinationBeforeApplying
                );

        if (!groupCommand.IsValid)
        {
            return false;
        }

        command = groupCommand;
        return true;
    }

    /// <summary>
    /// Completa una confirmación válida y actualiza áreas y sistemas
    /// especializados antes de liberar la sesión.
    /// </summary>
    public void CompleteSession(
        RestaurantAreaMember rootMember
    )
    {
        if (activeRoot == null ||
            !ReferenceEquals(activeRoot, rootMember))
        {
            return;
        }

        UpdateFollowerAreas(activeFollowers);
        NotifyProviders(activeRoot, activeFollowers);
        EndSessionInternal();
    }

    /// <summary>
    /// Restaura los seguidores cuando la transacción principal se cancela.
    /// La raíz es restaurada por RestaurantPlacementTransactionService.
    /// </summary>
    public void CancelSession(
        RestaurantAreaMember rootMember
    )
    {
        if (activeRoot == null ||
            !ReferenceEquals(activeRoot, rootMember))
        {
            return;
        }

        RestoreFollowersToInitialState();
        NotifyProviders(activeRoot, activeFollowers);
        EndSessionInternal();
    }

    /// <summary>
    /// Libera una sesión ya resuelta sin volver a aplicar transformaciones.
    /// Se utiliza como salvaguarda al limpiar el estado de presentación.
    /// </summary>
    public void ReleaseSession()
    {
        EndSessionInternal();
    }

    /// <summary>
    /// Notificación utilizada por el comando compuesto tras Undo/Redo.
    /// </summary>
    public void NotifyConfirmedGroupPoseApplied(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> linkedMembers
    )
    {
        if (rootMember == null)
        {
            return;
        }

        scratchFollowers.Clear();

        if (linkedMembers != null)
        {
            for (int index = 0;
                 index < linkedMembers.Count;
                 index++)
            {
                RestaurantAreaMember member =
                    linkedMembers[index];

                if (member != null &&
                    !ReferenceEquals(member, rootMember))
                {
                    scratchFollowers.Add(member);
                }
            }
        }

        UpdateFollowerAreas(scratchFollowers);
        NotifyProviders(rootMember, scratchFollowers);
        scratchFollowers.Clear();
    }

    private void HandlePlacementCancelled(
        RestaurantAreaMember member
    )
    {
        CancelSession(member);
    }

    private void RestoreFollowersToInitialState()
    {
        for (int index = 0;
             index < activeFollowers.Count;
             index++)
        {
            RestaurantAreaMember follower =
                activeFollowers[index];

            if (follower == null ||
                index >= activeFollowerBeforeStates.Count)
            {
                continue;
            }

            activeFollowerBeforeStates[index].Restore(
                follower
            );
        }

        Physics.SyncTransforms();
    }

    private void UpdateFollowerAreas(
        IReadOnlyList<RestaurantAreaMember> followers
    )
    {
        if (validationService == null ||
            followers == null)
        {
            return;
        }

        for (int index = 0;
             index < followers.Count;
             index++)
        {
            RestaurantAreaMember follower =
                followers[index];

            if (follower == null ||
                !follower.gameObject.activeInHierarchy)
            {
                continue;
            }

            RestaurantPlacementValidationResult result =
                validationService.ValidateCurrentPlacement(
                    follower
                );

            if (result.IsValid &&
                result.CandidateArea != null)
            {
                follower.SetArea(
                    result.CandidateArea
                );
            }
        }
    }

    private void NotifyProviders(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> followers
    )
    {
        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IRestaurantPlacementLinkedGroupProvider provider =
                providers[index];

            if (provider == null ||
                !provider.IsLinkEnabled)
            {
                continue;
            }

            provider.NotifyLinkedGroupPoseApplied(
                rootMember,
                followers
            );
        }
    }

    private void CollectFollowers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results
    )
    {
        results.Clear();
        followerInstanceIds.Clear();

        for (int providerIndex = 0;
             providerIndex < providers.Count;
             providerIndex++)
        {
            IRestaurantPlacementLinkedGroupProvider provider =
                providers[providerIndex];

            if (provider == null ||
                !provider.IsLinkEnabled)
            {
                continue;
            }

            int previousCount = results.Count;

            provider.CollectLinkedMembers(
                rootMember,
                results
            );

            for (int index = previousCount;
                 index < results.Count;
                 index++)
            {
                RestaurantAreaMember follower =
                    results[index];

                if (follower == null ||
                    ReferenceEquals(follower, rootMember) ||
                    !followerInstanceIds.Add(
                        follower.GetInstanceID()
                    ))
                {
                    results.RemoveAt(index);
                    index--;
                }
            }
        }
    }

    private void EndSessionInternal()
    {
        bool hadSession =
            activeRoot != null ||
            activeFollowers.Count > 0;

        activeRoot = null;
        activeFollowers.Clear();
        activeFollowerBeforeStates.Clear();
        activeFollowerRelativePositions.Clear();
        activeFollowerRelativeRotations.Clear();
        followerInstanceIds.Clear();

        activeRootOriginalPosition = default;
        activeRootOriginalRotation = Quaternion.identity;

        if (hadSession)
        {
            LinkedGroupSessionEnded?.Invoke();
        }
    }

    private void Subscribe()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCancelled -=
            HandlePlacementCancelled;

        transactionService.PlacementCancelled +=
            HandlePlacementCancelled;
    }

    private void Unsubscribe()
    {
        if (transactionService != null)
        {
            transactionService.PlacementCancelled -=
                HandlePlacementCancelled;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (validationService == null)
        {
            TryGetComponent(out validationService);
        }

        if (transactionService == null)
        {
            TryGetComponent(out transactionService);
        }
    }

    private static int CompareProviders(
        IRestaurantPlacementLinkedGroupProvider first,
        IRestaurantPlacementLinkedGroupProvider second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        return first.Priority.CompareTo(second.Priority);
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
