namespace Valid;

/// <summary>
/// Merging logic for CRDT resolution.
/// Uses a simplified last-writer-wins strategy with vector clock versioning.
/// For true distributed vector clock semantics, a full per-node vector comparison
/// would be needed. This implementation handles the common case of single-version
/// ordering with deterministic tiebreaking.
/// </summary>
public static class CrdtMerger
{
    /// <summary>
    /// Merges remote field value into local field using version-based resolution.
    /// - Higher version wins.
    /// - Equal versions: higher NodeId wins (deterministic tiebreak).
    /// - Equal version and NodeId: values are considered converged, no update needed.
    /// </summary>
    public static void MergeField<T>(
        ref T localValue, 
        ref VectorClock localClock, 
        T remoteValue, 
        VectorClock remoteClock)
    {
        if (remoteClock.Version > localClock.Version)
        {
            localValue = remoteValue;
            localClock = remoteClock;
        }
        else if (remoteClock.Version == localClock.Version && remoteClock.NodeId > localClock.NodeId)
        {
            // Deterministic tiebreak: higher NodeId wins at same version
            localValue = remoteValue;
            localClock = remoteClock;
        }
        // If remoteClock.Version < localClock.Version, local wins (no-op).
        // If both version and NodeId are equal, values have converged (no-op).
    }

    /// <summary>
    /// Merges bitmasks via union (any dirty/error bit set on either side is preserved).
    /// </summary>
    public static System.UInt128 MergeMask(System.UInt128 localMask, System.UInt128 remoteMask)
    {
        return localMask | remoteMask;
    }

    /// <summary>
    /// Advances a vector clock after a local update.
    /// </summary>
    public static VectorClock Tick(VectorClock clock) => clock with { Version = clock.Version + 1 };
}
