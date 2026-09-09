using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BistroBuilderMotionVariantScheduler
{
    private readonly Dictionary<string, Queue<string>> recentByActor = new Dictionary<string, Queue<string>>(StringComparer.Ordinal);
    private readonly int historyDepth;

    public BistroBuilderMotionVariantScheduler(int configuredHistoryDepth = 3)
    {
        historyDepth = Mathf.Clamp(configuredHistoryDepth, 1, 8);
    }

    public bool TrySelect(BistroBuilderMotionRecipe recipe, string actorId, BistroBuilderAnimationQualityTier qualityTier, string executionId, out BistroBuilderMotionRecipeVariant selected)
    {
        selected = null;
        if (recipe == null || recipe.Variants == null || recipe.Variants.Count == 0) return false;
        string actorKey = actorId ?? string.Empty;
        Queue<string> history = GetHistory(actorKey);
        List<BistroBuilderMotionRecipeVariant> eligible = new List<BistroBuilderMotionRecipeVariant>();
        for (int i = 0; i < recipe.Variants.Count; i++)
        {
            BistroBuilderMotionRecipeVariant variant = recipe.Variants[i];
            if (variant != null && (int)qualityTier <= (int)variant.MinimumQualityTier) eligible.Add(variant);
        }
        if (eligible.Count == 0)
        {
            for (int i = 0; i < recipe.Variants.Count; i++) if (recipe.Variants[i] != null) eligible.Add(recipe.Variants[i]);
        }
        if (eligible.Count == 0) return false;

        int seed = StableHash((executionId ?? string.Empty) + "|" + actorKey + "|" + recipe.MotionId);
        float bestScore = float.MinValue;
        for (int i = 0; i < eligible.Count; i++)
        {
            BistroBuilderMotionRecipeVariant variant = eligible[i];
            int recencyPenalty = Contains(history, variant.VariantId) ? 1000 : 0;
            int jitter = Math.Abs(StableHash(seed.ToString() + "|" + variant.VariantId)) % 100;
            float score = variant.Weight * 100f + jitter - recencyPenalty - variant.EstimatedCpuCost * Mathf.Max(0, (int)qualityTier) * 25f;
            if (score > bestScore) { bestScore = score; selected = variant; }
        }
        if (selected == null) return false;
        Remember(history, selected.VariantId);
        return true;
    }

    private Queue<string> GetHistory(string actorId)
    {
        if (!recentByActor.TryGetValue(actorId, out Queue<string> history))
        {
            history = new Queue<string>(historyDepth);
            recentByActor.Add(actorId, history);
        }
        return history;
    }

    private void Remember(Queue<string> history, string variantId)
    {
        history.Enqueue(variantId);
        while (history.Count > historyDepth) history.Dequeue();
    }

    private static bool Contains(Queue<string> history, string id)
    {
        foreach (string value in history) if (string.Equals(value, id, StringComparison.Ordinal)) return true;
        return false;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }
}
