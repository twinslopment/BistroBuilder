using System;
using System.Collections.Generic;

/// <summary>
/// Planner corto para episodios de 3-12 agentes en un cuello crítico.
/// Solo ordena precedencia; no genera trayectorias ni reservas espaciales.
/// </summary>
public sealed class BistroBuilderNavigationMicroConflictPlanner
{
    private const int MaxParticipants = 12;
    private const int MaxDirectionalBurst = 3;
    private readonly List<BistroBuilderNavigationMicroConflictIntent> positive =
        new List<BistroBuilderNavigationMicroConflictIntent>(MaxParticipants);
    private readonly List<BistroBuilderNavigationMicroConflictIntent> negative =
        new List<BistroBuilderNavigationMicroConflictIntent>(MaxParticipants);

    public int BuildOrder(IReadOnlyList<BistroBuilderNavigationMicroConflictIntent> intents,
        List<string> order)
    {
        if (order == null) return 0;
        order.Clear();
        positive.Clear();
        negative.Clear();
        if (intents == null) return 0;
        int count = Math.Min(MaxParticipants, intents.Count);
        for (int i = 0; i < count; i++)
        {
            var intent = intents[i];
            if (intent.direction > 0) InsertRanked(positive, intent);
            else if (intent.direction < 0) InsertRanked(negative, intent);
        }

        float posPressure = Pressure(positive);
        float negPressure = Pressure(negative);
        bool servePositive = posPressure > negPressure + 0.001f ||
            (Math.Abs(posPressure - negPressure) <= 0.001f && StablePositiveFirst());
        int pi = 0, ni = 0;
        while (pi < positive.Count || ni < negative.Count)
        {
            if (servePositive)
            {
                int burst = 0;
                while (pi < positive.Count && burst++ < MaxDirectionalBurst)
                    order.Add(positive[pi++].ownerId);
            }
            else
            {
                int burst = 0;
                while (ni < negative.Count && burst++ < MaxDirectionalBurst)
                    order.Add(negative[ni++].ownerId);
            }
            servePositive = !servePositive;
            if (servePositive && pi >= positive.Count) servePositive = false;
            if (!servePositive && ni >= negative.Count) servePositive = true;
        }
        return order.Count;
    }

    private bool StablePositiveFirst()
    {
        if (positive.Count == 0) return false;
        if (negative.Count == 0) return true;
        return string.CompareOrdinal(positive[0].ownerId, negative[0].ownerId) <= 0;
    }

    private static float Pressure(List<BistroBuilderNavigationMicroConflictIntent> list)
    {
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
            total += 1f + list[i].priority + list[i].waitingAge * 4f;
        return total;
    }
    private static void InsertRanked(List<BistroBuilderNavigationMicroConflictIntent> list,
        BistroBuilderNavigationMicroConflictIntent value)
    {
        int index = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (Compare(value, list[i]) < 0) { index = i; break; }
        }
        list.Insert(index, value);
    }

    private static int Compare(BistroBuilderNavigationMicroConflictIntent a,
        BistroBuilderNavigationMicroConflictIntent b)
    {
        float aScore = a.priority + a.waitingAge * 4f;
        float bScore = b.priority + b.waitingAge * 4f;
        if (aScore > bScore + 0.001f) return -1;
        if (bScore > aScore + 0.001f) return 1;
        return string.CompareOrdinal(a.ownerId ?? string.Empty, b.ownerId ?? string.Empty);
    }
}

[Serializable]
public struct BistroBuilderNavigationMicroConflictIntent
{
    public string ownerId;
    public int direction;
    public float priority;
    public float waitingAge;
}
