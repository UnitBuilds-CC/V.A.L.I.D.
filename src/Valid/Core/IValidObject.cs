namespace Valid;

/// <summary>
/// Core interface for VALID 3.0.0 objects.
/// Defines the technical contract for bitmask state management.
/// </summary>
public interface IValidObject
{
    /// <summary>
    /// Bitmask representing properties changed since last sync.
    /// </summary>
    System.UInt128 DirtyFlags { get; }

    /// <summary>
    /// Bitmask representing properties currently in an asynchronous operation.
    /// </summary>
    System.UInt128 BusyFlags { get; }

    /// <summary>
    /// Bitmask representing properties with validation errors.
    /// </summary>
    System.UInt128 ErrorFlags { get; }

    /// <summary>
    /// Bitmask representing object lifecycle state (New, Deleted, etc.).
    /// </summary>
    System.UInt128 StateFlags { get; }

    /// <summary>
    /// Returns a list of diagnostics for the current object state.
    /// </summary>
    IEnumerable<DiagnosticResult> GetDiagnostics();

    /// <summary>
    /// Returns the object's schema metadata as JSON.
    /// </summary>
    string GetValidMetadata();

    /// <summary>
    /// Sets a property value by name. Use sparingly (mostly for dev tools).
    /// </summary>
    void SetPropertyValue(string propertyName, object value);

    /// <summary>
    /// Returns a JSON delta of all changed properties based on DirtyFlags.
    /// </summary>
    string GetDeltaJson();

    /// <summary>
    /// Returns the .NET type of a property by name.
    /// </summary>
    System.Type GetPropertyType(string propertyName);

    /// <summary>
    /// Returns the bit index (0-127) for a given property name.
    /// </summary>
    int GetBitIndex(string propertyName);

    /// <summary>
    /// Returns a property value by name without reflection.
    /// </summary>
    object? GetPropertyValue(string propertyName);

    /// <summary>
    /// Calculates the validation state (ErrorFlags) without modifying the object instance.
    /// Used for server-side shadow validation.
    /// </summary>
    System.UInt128 CalculateValidationState();

    /// <summary>
    /// True if ErrorFlags == 0 (no validation errors).
    /// </summary>
    bool IsValid => ErrorFlags == System.UInt128.Zero;

    /// <summary>
    /// True if DirtyFlags != 0 (at least one property changed).
    /// </summary>
    bool IsDirty => DirtyFlags != System.UInt128.Zero;

    /// <summary>
    /// Checks if a specific bit index is dirty.
    /// </summary>
    bool IsDirtyAt(int bitIndex) => (DirtyFlags & ((System.UInt128)1 << bitIndex)) != System.UInt128.Zero;

    /// <summary>
    /// Checks if a specific bit index has an error.
    /// </summary>
    bool HasErrorAt(int bitIndex) => (ErrorFlags & ((System.UInt128)1 << bitIndex)) != System.UInt128.Zero;

    /// <summary>
    /// Resets DirtyFlags to zero (call after save/sync).
    /// </summary>
    void ResetDirty();

    /// <summary>
    /// Updates a property from a JSON value string using source-generated, reflection-free logic.
    /// </summary>
    void UpdatePropertyFromJson(string propertyName, string jsonValue);

    /// <summary>
    /// Returns the circular state history for time-travel debugging.
    /// </summary>
    string[] GetStateHistory();
    
    /// <summary>
    /// Event fired when a specific bit index in any mask (Dirty, error, Busy) changes.
    /// Passes: bitIndex, maskType (0=Dirty, 1=Error, 2=Busy, 3=State).
    /// </summary>
    event Action<int, int>? BitPulse;
}

public readonly record struct DiagnosticResult(string Property, string Message, string Code, string? FixSuggestion = null);
