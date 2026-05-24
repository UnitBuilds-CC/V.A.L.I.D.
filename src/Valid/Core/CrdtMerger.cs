namespace Valid;

/// <summary>
/// Merging logic for CRDT resolution.
/// </summary>
public static class CrdtMerger
{
    /// <summary>
    /// Merges remote field value into local field using vector clocks.
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
            localValue = remoteValue;
            localClock = remoteClock;
        }
    }

    /// <summary>
    /// Merges bitmasks.
    /// </summary>
    public static System.UInt128 MergeMask(System.UInt128 localMask, System.UInt128 remoteMask)
    {
        return localMask | remoteMask;
    }
}
