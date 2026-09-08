using UnityEngine;

/// <summary>
/// Adapta el preflight BBSIS al contrato modular del Modo Edición.
/// La UI y la transacción siguen perteneciendo al sistema de colocación.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialPlacementConstraintRule :
    MonoBehaviour,
    IRestaurantPlacementConstraintRule
{
    [SerializeField]
    private BistroBuilderSpatialPlacementAssessmentService assessment;

    [SerializeField]
    private bool constraintEnabled = true;

    [SerializeField]
    private int priority = 40;

    public int Priority => priority;
    public bool IsConstraintEnabled => constraintEnabled;
    public int EvaluatedCandidateCount { get; private set; }
    public BistroBuilderSpatialPlacementEvaluation LastEvaluation
    {
        get;
        private set;
    }

    private void Awake()
    {
        ResolveDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (assessment == null)
        {
            error = "La regla de colocación BBSIS necesita su assessment.";
            return false;
        }

        return assessment.ValidateConfiguration(out error);
    }

    public RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context)
    {
        if (context.Member == null)
            return RestaurantPlacementConstraintEvaluation.Valid();

        BistroBuilderSpatialSubject subject =
            context.Member.GetComponent<BistroBuilderSpatialSubject>();
        if (subject == null)
            return RestaurantPlacementConstraintEvaluation.Valid();

        ResolveDependencies();
        if (assessment == null)
            return RestaurantPlacementConstraintEvaluation.Invalid(
                "bbsis.system_unavailable",
                "No se puede validar todavía el espacio funcional.",
                "Falta BistroBuilderSpatialPlacementAssessmentService.",
                subject,
                true);

        EvaluatedCandidateCount++;
        LastEvaluation = assessment.EvaluateCandidate(
            subject,
            context.CandidateRootPosition,
            context.CandidateRootRotation,
            context.CandidateArea,
            context.ObstacleRegistry);

        if (LastEvaluation.IsValid)
            return RestaurantPlacementConstraintEvaluation.Valid();

        return RestaurantPlacementConstraintEvaluation.Invalid(
            LastEvaluation.RuleId,
            LastEvaluation.UserMessage,
            LastEvaluation.TechnicalMessage,
            LastEvaluation.RelatedObject,
            true);
    }

    private void ResolveDependencies()
    {
        if (assessment == null)
            assessment = FindFirstObjectByType<
                BistroBuilderSpatialPlacementAssessmentService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderSpatialPlacementAssessmentService service)
    {
        assessment = service;
    }
#endif
}
