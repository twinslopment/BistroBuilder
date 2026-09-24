using System;
using System.Collections.Generic;
using UnityEngine;

public static class ActivityIconResolver
{
    public const string ResourceRoot = "BistroBuilder/UI/ActivityIcons/";

    private static readonly Dictionary<string, Sprite> Cache =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Cache.Clear();
    }

    public static string GetResourceName(string familyKey)
    {
        return ActivityIconFamilyCatalog.ToRuntimeResourceName(familyKey);
    }

    public static string GetResourcePath(string familyKey)
    {
        return ResourceRoot + GetResourceName(familyKey);
    }

    public static bool TryResolve(string familyKey, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(familyKey))
            return false;

        string normalized = familyKey.Trim();
        if (Cache.TryGetValue(normalized, out sprite))
            return sprite != null;

        sprite = Resources.Load<Sprite>(GetResourcePath(normalized));
        Cache[normalized] = sprite;
        return sprite != null;
    }

    public static Sprite ResolveOrNull(string familyKey)
    {
        TryResolve(familyKey, out Sprite sprite);
        return sprite;
    }

}
