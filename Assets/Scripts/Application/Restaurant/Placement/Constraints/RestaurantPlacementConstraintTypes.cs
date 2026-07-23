using UnityEngine;

/// <summary>
/// Contexto inmutable de una regla especializada de colocación.
///
/// Permite ampliar el sistema genérico sin introducir reglas de
/// sillas, puertas, equipamiento o accesos dentro del validador base.
/// </summary>
public readonly struct RestaurantPlacementConstraintContext
{
    public RestaurantAreaMember Member { get; }

    public Vector3 CandidateRootPosition { get; }

    public Quaternion CandidateRootRotation { get; }

    public RestaurantArea CandidateArea { get; }

    public RestaurantPlacementFootprint CandidateFootprint { get; }

    public RestaurantPlacementRegistry PlacementRegistry { get; }

    public RestaurantPlacementObstacleRegistry ObstacleRegistry { get; }

    public RestaurantPlacementConstraintContext(
        RestaurantAreaMember member,
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        RestaurantArea candidateArea,
        RestaurantPlacementFootprint candidateFootprint,
        RestaurantPlacementRegistry placementRegistry,
        RestaurantPlacementObstacleRegistry obstacleRegistry
    )
    {
        Member = member;
        CandidateRootPosition = candidateRootPosition;
        CandidateRootRotation = candidateRootRotation;
        CandidateArea = candidateArea;
        CandidateFootprint = candidateFootprint;
        PlacementRegistry = placementRegistry;
        ObstacleRegistry = obstacleRegistry;
    }
}

/// <summary>
/// Resultado de una regla especializada de colocación.
///
/// UserMessage es breve y está destinado al jugador.
/// TechnicalMessage conserva el diagnóstico para Console y QA.
/// </summary>
public readonly struct RestaurantPlacementConstraintEvaluation
{
    public bool IsValid { get; }

    public string RuleId { get; }

    public string UserMessage { get; }

    public string TechnicalMessage { get; }

    public Object RelatedObject { get; }

    /// <summary>
    /// Indica que esta causa funcional es más informativa que un
    /// posible conflicto físico genérico. Se reserva para reglas como
    /// superar la capacidad fija de una mesa.
    /// </summary>
    public bool ShouldOverrideGenericConflicts { get; }

    public RestaurantPlacementConstraintEvaluation(
        bool isValid,
        string ruleId,
        string userMessage,
        string technicalMessage,
        Object relatedObject,
        bool shouldOverrideGenericConflicts
    )
    {
        IsValid = isValid;
        RuleId = string.IsNullOrWhiteSpace(ruleId)
            ? string.Empty
            : ruleId.Trim();
        UserMessage = userMessage ?? string.Empty;
        TechnicalMessage = technicalMessage ?? string.Empty;
        RelatedObject = relatedObject;
        ShouldOverrideGenericConflicts =
            shouldOverrideGenericConflicts;
    }

    public static RestaurantPlacementConstraintEvaluation Valid()
    {
        return new RestaurantPlacementConstraintEvaluation(
            true,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            false
        );
    }

    public static RestaurantPlacementConstraintEvaluation Invalid(
        string ruleId,
        string userMessage,
        string technicalMessage,
        Object relatedObject = null,
        bool shouldOverrideGenericConflicts = false
    )
    {
        return new RestaurantPlacementConstraintEvaluation(
            false,
            ruleId,
            userMessage,
            technicalMessage,
            relatedObject,
            shouldOverrideGenericConflicts
        );
    }
}

/// <summary>
/// Contrato de una regla modular de colocación.
///
/// Cada familia funcional aporta su regla sin modificar el núcleo.
/// </summary>
public interface IRestaurantPlacementConstraintRule
{
    int Priority { get; }

    bool IsConstraintEnabled { get; }

    RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context
    );
}
