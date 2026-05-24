namespace Valid.Testing;

/// <summary>
/// Shared helpers for VALID 3.0.0 autonomous tests.
/// </summary>
public static class ValidTestHelpers
{
    /// <summary>
    /// Verifies that an object's state can be perfectly reconstructed after serialization.
    /// Used by generated symmetry tests.
    /// </summary>
    public static void VerifySymmetry<T>(T original, T reconstructed) where T : IValidObject
    {
        // Compare bitmasks
        if (original.DirtyFlags != reconstructed.DirtyFlags)
            throw new Exception("Dirty bitmask mismatch after hydration.");
            
        if (original.ErrorFlags != reconstructed.ErrorFlags)
            throw new Exception("Error bitmask mismatch after hydration.");
    }
}
