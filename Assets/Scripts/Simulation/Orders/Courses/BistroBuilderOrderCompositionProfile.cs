using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regla de datos que genera una o varias líneas físicas dentro de un pase.
/// </summary>
[Serializable]
public sealed class BistroBuilderCourseCompositionRule
{
    [SerializeField]
    private bool enabled = true;

    [SerializeField, Range(0, 20)]
    private int courseIndex = 1;

    [SerializeField]
    private BistroBuilderOrderLineCompositionMode compositionMode =
        BistroBuilderOrderLineCompositionMode.IndividualPerCustomer;

    [SerializeField, Min(0)]
    private int menuDisplayOffset;

    [SerializeField, Range(2, 32)]
    private int sharedGroupSize = 2;

    public bool Enabled => enabled;
    public int CourseIndex => courseIndex;
    public BistroBuilderOrderLineCompositionMode CompositionMode =>
        compositionMode;
    public int MenuDisplayOffset => menuDisplayOffset;
    public int SharedGroupSize => sharedGroupSize;

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                courseIndex
            ))
        {
            error = "Una regla contiene un CourseIndex inválido.";
            return false;
        }

        if (menuDisplayOffset < 0 || menuDisplayOffset > 1024)
        {
            error = "Una regla contiene un desplazamiento de carta inválido.";
            return false;
        }

        if (compositionMode ==
                BistroBuilderOrderLineCompositionMode.SharedGroups &&
            (sharedGroupSize < 2 ||
             sharedGroupSize >
                 BistroBuilderCourseAndSharingPolicy
                     .MaximumCustomersPerSharedGroup))
        {
            error = "El tamaño de grupo compartido queda fuera de rango.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Perfil de composición de comandas por datos.
///
/// No contiene DishId concretos. Cada regla selecciona platos disponibles por
/// su orden de carta, de modo que el perfil sobrevive a cambios de catálogo.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_Order_Composition_Profile",
    menuName = "Bistro Builder/Orders/Order Composition Profile"
)]
public sealed class BistroBuilderOrderCompositionProfile : ScriptableObject
{
    [SerializeField]
    private BistroBuilderCourseCoordinationPolicy coordinationPolicy =
        BistroBuilderCourseCoordinationPolicy.PerTable;

    [SerializeField]
    private List<BistroBuilderCourseCompositionRule> rules =
        new List<BistroBuilderCourseCompositionRule>();

    public BistroBuilderCourseCoordinationPolicy CoordinationPolicy =>
        coordinationPolicy;
    public IReadOnlyList<BistroBuilderCourseCompositionRule> Rules => rules;

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                coordinationPolicy
            ))
        {
            error = "La política de coordinación de pases no es válida.";
            return false;
        }

        if (rules == null || rules.Count == 0)
        {
            error = "El perfil no contiene reglas de composición.";
            return false;
        }

        bool hasEnabledRule = false;
        int estimatedMinimumLines = 0;

        for (int index = 0; index < rules.Count; index++)
        {
            BistroBuilderCourseCompositionRule rule = rules[index];

            if (rule == null)
            {
                error = "El perfil contiene una regla nula.";
                return false;
            }

            if (!rule.TryValidate(out error))
            {
                return false;
            }

            if (!rule.Enabled)
            {
                continue;
            }

            hasEnabledRule = true;
            estimatedMinimumLines++;
        }

        if (!hasEnabledRule)
        {
            error = "El perfil no contiene ninguna regla activa.";
            return false;
        }

        if (estimatedMinimumLines >
            BistroBuilderCourseAndSharingPolicy.MaximumLinesPerOrder)
        {
            error = "El perfil excede el máximo de líneas por comanda.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
