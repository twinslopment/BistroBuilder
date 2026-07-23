using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regla universal que bloquea una operación cuando cualquiera de los
/// miembros enlazados queda en una colocación inválida.
///
/// El controlador aplica previamente la pose candidata al conjunto.
/// Por ello cada seguidor puede reutilizar el validador completo:
/// áreas, capacidades, colisiones, obstáculos, separación y reglas
/// funcionales especializadas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Linked Group Placement Rule"
)]
public sealed class RestaurantLinkedGroupPlacementConstraintRule :
    MonoBehaviour,
    IRestaurantPlacementConstraintRule
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlacementLinkedGroupService linkedGroupService;

    [SerializeField]
    private RestaurantPlacementValidationService validationService;

    [Header("Regla")]

    [SerializeField]
    private bool constraintEnabled = true;

    [SerializeField]
    private int priority = 20;

    private readonly List<RestaurantAreaMember>
        followerBuffer =
            new List<RestaurantAreaMember>(16);

    public int Priority => priority;

    public bool IsConstraintEnabled => constraintEnabled;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context
    )
    {
        if (!constraintEnabled ||
            context.Member == null ||
            linkedGroupService == null ||
            validationService == null)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        int followerCount =
            linkedGroupService.CopyLinkedMembers(
                context.Member,
                followerBuffer
            );

        if (followerCount <= 0)
        {
            return RestaurantPlacementConstraintEvaluation.Valid();
        }

        for (int index = 0;
             index < followerBuffer.Count;
             index++)
        {
            RestaurantAreaMember follower =
                followerBuffer[index];

            if (follower == null ||
                !follower.gameObject.activeInHierarchy)
            {
                continue;
            }

            RestaurantPlacementValidationResult result =
                validationService.ValidateCurrentPlacement(
                    follower
                );

            if (result.IsValid)
            {
                continue;
            }

            return RestaurantPlacementConstraintEvaluation.Invalid(
                "linked_group_member_invalid",
                BuildUserMessage(follower, result),
                BuildTechnicalMessage(
                    context.Member,
                    follower,
                    result
                ),
                follower,
                true
            );
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }

    private static string BuildUserMessage(
        RestaurantAreaMember follower,
        RestaurantPlacementValidationResult result
    )
    {
        if (!string.IsNullOrWhiteSpace(result.UserMessage))
        {
            return result.UserMessage;
        }

        switch (result.Status)
        {
            case RestaurantPlacementValidationStatus
                .PhysicalOverlap:

                return
                    "No se puede mover el conjunto porque uno de " +
                    "sus elementos choca con un obstáculo.";

            case RestaurantPlacementValidationStatus
                .MinimumClearanceViolation:

                return
                    "No se puede mover el conjunto: falta espacio " +
                    "operativo alrededor de uno de sus elementos.";

            case RestaurantPlacementValidationStatus
                .OutsideRegisteredAreas:

            case RestaurantPlacementValidationStatus
                .FootprintOutsideCandidateArea:

                return
                    "No se puede mover el conjunto porque uno de " +
                    "sus elementos queda fuera del área válida.";

            case RestaurantPlacementValidationStatus
                .MissingRequiredCapability:

                return
                    "No se puede mover el conjunto a una zona que " +
                    "no admite todos sus elementos.";

            default:

                return
                    "No se puede mover el conjunto completo a esta " +
                    "posición.";
        }
    }

    private static string BuildTechnicalMessage(
        RestaurantAreaMember root,
        RestaurantAreaMember follower,
        RestaurantPlacementValidationResult result
    )
    {
        string rootName =
            root != null
                ? root.name
                : "raíz no disponible";

        string followerName =
            follower != null
                ? follower.name
                : "seguidor no disponible";

        return
            "El grupo enlazado de " +
            rootName +
            " no es válido porque " +
            followerName +
            " devuelve " +
            result.Status +
            ". Regla: " +
            result.ConstraintEvaluation.RuleId +
            ". Diagnóstico: " +
            result.TechnicalMessage;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (linkedGroupService == null)
        {
            TryGetComponent(out linkedGroupService);
        }

        if (validationService == null)
        {
            TryGetComponent(out validationService);
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
