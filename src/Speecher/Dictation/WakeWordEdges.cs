namespace Speecher.Dictation;

public readonly record struct WakeWordEdges(bool AtStart, bool AtEnd)
{
    public static WakeWordEdges None => default;

    public bool Any => AtStart || AtEnd;
}
