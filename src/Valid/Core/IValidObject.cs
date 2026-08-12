// V.A.L.I.D. Naming Convention:
// - "Valid" prefix: Core engine types (ValidObjectBase, ValidProperty, ValidList)
// - "Vavid" prefix: Blazor UI component types (VavidComponentBase, VavidSurgicalComponentBase)  
// - "VAVID" (all caps): Chrome extension / HUD branding
// - "vavid" (lowercase): JavaScript bridge global (window.vavid)

namespace Valid;

/// <summary>
/// Core interface for VALID 3.0.0 objects. Defines the technical contract for bitmask state management.
/// </summary>
/// <example>
/// <code>
/// // Define a VALID object with bitmask-tracked properties
/// [ValidObject]
/// public partial class Customer : ValidObjectBase
/// {
///     [ValidProperty] public string Name { get; set; } = "";
///     [ValidProperty] public int Age { get; set; }
///     [ValidProperty, Range(0, 150)] public int Score { get; set; }
/// }
/// 
/// // Use it — changes are automatically tracked in DirtyFlags
/// var customer = new Customer { Name = "Alice", Age = 30 };
/// customer.Age = 31;  // DirtyFlags now has bit 1 set
/// Console.WriteLine(customer.IsDirty);  // true
/// Console.WriteLine(customer.GetDeltaJson());  // {"Age":31}
/// </code>
/// </example>
/// <remarks>
/// <para>
/// VALID uses a <see cref="System.UInt128"/> bitmask to track up to 128 properties per object.
/// Each property gets a unique bit index (0-127) assigned by the source generator.
/// </para>
/// <para>
/// Key capabilities:
/// <list type="bullet">
/// <item><description>Dirty tracking — which properties changed since last sync</description></item>
/// <item><description>Busy tracking — which properties are in async operations</description></item>
/// <item><description>Error tracking — which properties have validation errors</description></item>
/// <item><description>State tracking — object lifecycle (New, Deleted, etc.)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="ValidObjectAttribute"/>
/// <seealso cref="ValidPropertyAttribute"/>
/// <seealso cref="ValidObjectBase"/>
public interface IValidObject
{
    /// <summary>
    /// Unique runtime identifier for this object instance.
    /// Used for JS bridge registration and MCP tracking.
    /// </summary>
    string ValidId { get; }

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

/// <summary>
/// Represents a single validation diagnostic with property context.
/// </summary>
/// <param name="Property">The property name that triggered the diagnostic.</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Code">Machine-readable error code (e.g., "CUST-001").</param>
/// <param name="FixSuggestion">Optional suggestion for fixing the error.</param>
/// <param name="CurrentValue">The current property value at the time of validation (serialized as string).</param>
/// <param name="ExpectedConstraint">Description of the expected constraint (e.g., "Required", "Range(18, 65)", "Length(3, 100)").</param>
public readonly record struct DiagnosticResult(
    string Property,
    string Message,
    string Code,
    string? FixSuggestion = null,
    string? CurrentValue = null,
    string? ExpectedConstraint = null);
