using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BB_ServiceTimingCatalog",
    menuName = "Bistro Builder/Service/Service Timing Catalog"
)]
public sealed class BistroBuilderServiceTimingCatalog : ScriptableObject
{
    public const string ResourcesPath =
        "BistroBuilder/Service/BB_ServiceTimingCatalog";

    [SerializeField]
    private List<BistroBuilderServiceTimingProfile> profiles =
        new List<BistroBuilderServiceTimingProfile>();

    public IReadOnlyList<BistroBuilderServiceTimingProfile> Profiles => profiles;

    public bool TryGetProfile(
        BistroBuilderServiceTimingPhase phase,
        out BistroBuilderServiceTimingProfile profile)
    {
        profile = null;

        if (profiles == null)
            return false;

        for (int index = 0; index < profiles.Count; index++)
        {
            BistroBuilderServiceTimingProfile candidate = profiles[index];
            if (candidate != null && candidate.Phase == phase)
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryEvaluate(
        BistroBuilderServiceTimingPhase phase,
        float elapsedSeconds,
        out BistroBuilderServiceTimingState state)
    {
        state = BistroBuilderServiceTimingState.Normal;

        if (!TryGetProfile(phase, out BistroBuilderServiceTimingProfile profile))
            return false;

        state = profile.Evaluate(elapsedSeconds);
        return true;
    }

    public bool Validate(out string error)
    {
        if (profiles == null || profiles.Count == 0)
        {
            error = "ServiceTimingCatalog no contiene perfiles.";
            return false;
        }

        var phases = new HashSet<BistroBuilderServiceTimingPhase>();

        for (int index = 0; index < profiles.Count; index++)
        {
            BistroBuilderServiceTimingProfile profile = profiles[index];

            if (profile == null)
            {
                error = "ServiceTimingCatalog contiene un perfil nulo.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BistroBuilderServiceTimingPhase), profile.Phase))
            {
                error = "ServiceTimingCatalog contiene una fase desconocida.";
                return false;
            }

            if (!phases.Add(profile.Phase))
            {
                error =
                    "ServiceTimingCatalog contiene una fase duplicada: " +
                    profile.Phase + ".";
                return false;
            }

            if (!profile.Validate(out error))
            {
                error = profile.Phase + ": " + error;
                return false;
            }
        }

        if (!phases.Contains(BistroBuilderServiceTimingPhase.BillDelivery))
        {
            error = "ServiceTimingCatalog no define BillDelivery.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
