using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina reglas especializadas de colocación.
///
/// Descubre una sola vez componentes que implementan
/// IRestaurantPlacementConstraintRule en el mismo GameObject.
/// No utiliza Update ni búsquedas continuas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Constraint Service"
)]
public sealed class RestaurantPlacementConstraintService :
    MonoBehaviour
{
    [Header("Inicialización")]

    [SerializeField]
    private bool discoverRulesAutomatically = true;

    [Header("Depuración")]

    [SerializeField]
    private bool logRuleSummary = true;

    private readonly List<IRestaurantPlacementConstraintRule>
        rules =
            new List<IRestaurantPlacementConstraintRule>(8);

    public int RegisteredRuleCount =>
        rules.Count;

    private void Awake()
    {
        RefreshRules();
    }

    private void Start()
    {
        if (!logRuleSummary)
        {
            return;
        }

        Debug.Log(
            nameof(RestaurantPlacementConstraintService) +
            " ha registrado " +
            rules.Count +
            " regla(s) especializada(s).",
            this
        );
    }

    /// <summary>
    /// Reconstruye la lista de reglas de forma determinista.
    /// </summary>
    public void RefreshRules()
    {
        rules.Clear();

        if (!discoverRulesAutomatically)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        for (int index = 0;
             index < behaviours.Length;
             index++)
        {
            MonoBehaviour behaviour =
                behaviours[index];

            if (behaviour == null ||
                ReferenceEquals(behaviour, this) ||
                !(behaviour is IRestaurantPlacementConstraintRule rule))
            {
                continue;
            }

            rules.Add(rule);
        }

        rules.Sort(CompareRules);
    }

    /// <summary>
    /// Evalúa reglas en orden de prioridad y devuelve el primer
    /// incumplimiento bloqueante.
    /// </summary>
    public RestaurantPlacementConstraintEvaluation Evaluate(
        RestaurantPlacementConstraintContext context
    )
    {
        for (int index = 0;
             index < rules.Count;
             index++)
        {
            IRestaurantPlacementConstraintRule rule =
                rules[index];

            if (rule == null ||
                !rule.IsConstraintEnabled)
            {
                continue;
            }

            RestaurantPlacementConstraintEvaluation evaluation =
                rule.Evaluate(context);

            if (!evaluation.IsValid)
            {
                return evaluation;
            }
        }

        return RestaurantPlacementConstraintEvaluation.Valid();
    }

    private static int CompareRules(
        IRestaurantPlacementConstraintRule first,
        IRestaurantPlacementConstraintRule second
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

        int priorityComparison =
            first.Priority.CompareTo(second.Priority);

        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        return string.Compare(
            first.GetType().FullName,
            second.GetType().FullName,
            StringComparison.Ordinal
        );
    }
}
