using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describe el origen comercial de una llegada sin acoplar el flujo de
/// clientes a Marketing, Reservas ni a ningún proveedor concreto.
/// </summary>
[Serializable]
public sealed class BistroBuilderCustomerAcquisitionProfile
{
    public string segmentId = "general";
    public string sourceSystemId = "service.baseline";
    public string sourceReferenceId = string.Empty;
    public bool marketingInfluenced;

    public BistroBuilderCustomerAcquisitionProfile DeepClone() =>
        (BistroBuilderCustomerAcquisitionProfile)MemberwiseClone();

    public static BistroBuilderCustomerAcquisitionProfile CreateBaseline()
    {
        return new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = "general",
            sourceSystemId = "service.baseline",
            sourceReferenceId = string.Empty,
            marketingInfluenced = false
        };
    }

    public bool TryValidate(out string error)
    {
        segmentId = NormalizeId(segmentId, "general");
        sourceSystemId = NormalizeId(sourceSystemId, "service.baseline");
        sourceReferenceId = NormalizeId(sourceReferenceId, string.Empty);
        if (!IsSafeId(segmentId) || !IsSafeId(sourceSystemId) ||
            (!string.IsNullOrEmpty(sourceReferenceId) &&
             !IsSafeId(sourceReferenceId)))
        {
            error = "El perfil de captación contiene identidades inválidas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeId(string value, string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
        return normalized ?? string.Empty;
    }

    private static bool IsSafeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= '0' && character <= '9' ||
                character == '_' || character == '-' || character == '.';
            if (!allowed)
                return false;
        }

        return true;
    }
}

/// <summary>
/// Plan de llegadas preparado por una política externa para el siguiente
/// servicio. El Spawner conserva la autoridad de materializar cada grupo.
/// </summary>
[Serializable]
public sealed class BistroBuilderCustomerDemandPlan
{
    public string planId = string.Empty;
    public int walkInGroupCount = 1;
    public List<BistroBuilderCustomerAcquisitionProfile> profiles =
        new List<BistroBuilderCustomerAcquisitionProfile>();

    public BistroBuilderCustomerDemandPlan DeepClone()
    {
        var clone = new BistroBuilderCustomerDemandPlan
        {
            planId = planId,
            walkInGroupCount = walkInGroupCount,
            profiles = new List<BistroBuilderCustomerAcquisitionProfile>()
        };

        if (profiles != null)
            for (int index = 0; index < profiles.Count; index++)
                clone.profiles.Add(profiles[index]?.DeepClone());
        return clone;
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(planId) || walkInGroupCount < 1 ||
            walkInGroupCount > 100 || profiles == null ||
            profiles.Count != walkInGroupCount)
        {
            error = "El plan de demanda tiene cabecera o cardinalidad inválidas.";
            return false;
        }

        for (int index = 0; index < profiles.Count; index++)
        {
            if (profiles[index] == null ||
                !profiles[index].TryValidate(out error))
                return false;
        }

        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Etiqueta runtime consultiva asociada al CustomerGroup materializado.
/// No decide servicio ni seating; conserva el perfil que originó la llegada.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCustomerAcquisitionTag : MonoBehaviour
{
    [SerializeField] private string segmentId = "general";
    [SerializeField] private string sourceSystemId = "service.baseline";
    [SerializeField] private string sourceReferenceId = string.Empty;
    [SerializeField] private bool marketingInfluenced;

    public string SegmentId => segmentId;
    public string SourceSystemId => sourceSystemId;
    public string SourceReferenceId => sourceReferenceId;
    public bool MarketingInfluenced => marketingInfluenced;

    public bool TryConfigure(
        BistroBuilderCustomerAcquisitionProfile profile,
        out string error)
    {
        error = string.Empty;
        if (profile == null)
        {
            error = "El perfil de captación es nulo.";
            return false;
        }
        if (!profile.TryValidate(out error))
            return false;

        segmentId = profile.segmentId;
        sourceSystemId = profile.sourceSystemId;
        sourceReferenceId = profile.sourceReferenceId;
        marketingInfluenced = profile.marketingInfluenced;
        error = string.Empty;
        return true;
    }

    public BistroBuilderCustomerAcquisitionProfile CreateSnapshot()
    {
        return new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = segmentId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            marketingInfluenced = marketingInfluenced
        };
    }
}
