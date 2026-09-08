using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BBMotionCatalog",
    menuName = "Bistro Builder/Animation/Motion Catalog")]
public sealed class BistroBuilderMotionCatalog : ScriptableObject
{
    [SerializeField] private List<BistroBuilderMotionProfile> motions = new List<BistroBuilderMotionProfile>();

    public IReadOnlyList<BistroBuilderMotionProfile> Motions => motions;

    public bool TryResolve(string requestedMotionId, out BistroBuilderMotionProfile profile)
    {
        profile = null;
        string current = BistroBuilderMotionProfile.NormalizeId(requestedMotionId);
        if (string.IsNullOrWhiteSpace(current)) return false;

        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (!visited.Add(current)) return false;
            BistroBuilderMotionProfile candidate = FindDirect(current);
            if (candidate == null) return false;
            if (candidate.ValidateConfiguration(out _))
            {
                profile = candidate;
                return true;
            }
            current = candidate.FallbackMotionId;
        }
        return false;
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < motions.Count; i++)
        {
            BistroBuilderMotionProfile motion = motions[i];
            if (motion == null)
            {
                error = name + " contiene una motion nula.";
                return false;
            }
            if (!motion.ValidateConfiguration(out error)) return false;
            if (!ids.Add(motion.MotionId))
            {
                error = name + " contiene MotionId duplicado: " + motion.MotionId;
                return false;
            }
        }
        return true;
    }

    private BistroBuilderMotionProfile FindDirect(string motionId)
    {
        for (int i = 0; i < motions.Count; i++)
        {
            BistroBuilderMotionProfile motion = motions[i];
            if (motion != null && motion.MotionId == motionId) return motion;
        }
        return null;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(List<BistroBuilderMotionProfile> configuredMotions)
    {
        motions = configuredMotions ?? new List<BistroBuilderMotionProfile>();
    }
#endif
}
