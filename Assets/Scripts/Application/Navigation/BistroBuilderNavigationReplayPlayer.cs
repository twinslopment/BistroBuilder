using System;

/// <summary>
/// Reproductor determinista de un bundle diagnóstico.
/// Recorre eventos sin reinyectarlos en gameplay ni modificar Navigation.
/// </summary>
public sealed class BistroBuilderNavigationReplayPlayer
{
    private BistroBuilderNavigationReplayBundle bundle;
    private int index = -1;

    public bool IsLoaded => bundle != null;
    public int Count => bundle != null && bundle.events != null ? bundle.events.Count : 0;
    public int Index => index;
    public string Digest => bundle != null ? bundle.deterministicDigest : string.Empty;
    public BistroBuilderNavigationReplayEvent Current =>
        bundle != null && bundle.events != null && index >= 0 && index < bundle.events.Count
            ? bundle.events[index]
            : null;

    public bool Load(BistroBuilderNavigationReplayBundle source, out string error)
    {
        if (!BistroBuilderNavigationReplayRecorder.ValidateBundle(source, out error))
            return false;
        bundle = source;
        index = source.events.Count > 0 ? 0 : -1;
        return true;
    }

    public void Reset() => index = Count > 0 ? 0 : -1;

    public bool StepForward()
    {
        if (!IsLoaded || index >= Count - 1) return false;
        index++;
        return true;
    }
    public bool StepBackward()
    {
        if (!IsLoaded || index <= 0) return false;
        index--;
        return true;
    }

    public bool Seek(int requestedIndex)
    {
        if (!IsLoaded || Count == 0) return false;
        index = Math.Max(0, Math.Min(Count - 1, requestedIndex));
        return true;
    }
}
