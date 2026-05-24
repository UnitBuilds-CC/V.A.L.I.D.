namespace Valid;

/// <summary>
/// Logical version clock for causal ordering.
/// </summary>
public record struct VectorClock(long NodeId, long Version)
{
    public static VectorClock Zero => new(0, 0);

    public bool IsNewerThan(VectorClock other) => Version > other.Version;
}
