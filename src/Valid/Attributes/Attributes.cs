using System;

namespace Valid;

// NOTE: This namespace defines RangeAttribute, RequiredAttribute, and StringLengthAttribute
// which share names with types in System.ComponentModel.DataAnnotations.
// If you need both, use fully qualified names or namespace aliases to avoid ambiguity:
//   using ValidAttr = Valid.RangeAttribute;
//   using DataAnnotations = System.ComponentModel.DataAnnotations;

/// <summary>
/// Marks a class to be processed by the VALID source generator.
/// The generator emits boilerplate for bitmask state tracking (Dirty, Busy, Error masks).
/// </summary>
/// <example>
/// <code>
/// [ValidObject]
/// public partial class Product : ValidObjectBase
/// {
///     [ValidProperty] public string Sku { get; set; } = "";
///     [ValidProperty, Range(0, 10000)] public decimal Price { get; set; }
///     [ValidProperty, Required] public string Name { get; set; } = "";
/// }
/// </code>
/// </example>
/// <remarks>
/// <para>
/// The class must be <c>partial</c> and inherit from <see cref="ValidObjectBase"/>.
/// Maximum 128 properties per object (UInt128 bitmask limit).
/// </para>
/// </remarks>
/// <seealso cref="ValidPropertyAttribute"/>
/// <seealso cref="ValidObjectBase"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ValidObjectAttribute : Attribute
{
}

/// <summary>
/// Marks a property to be included in the bitmask state engine.
/// Each property gets a unique bit index (0-127) for dirty/busy/error tracking.
/// </summary>
/// <example>
/// <code>
/// [ValidObject]
/// public partial class User : ValidObjectBase
/// {
///     [ValidProperty] public string Email { get; set; } = "";
///     [ValidProperty] public DateTime CreatedAt { get; set; }
///     [ValidProperty, StringLength(100)] public string DisplayName { get; set; } = "";
/// }
/// </code>
/// </example>
/// <remarks>
/// <para>
/// Properties are assigned bit indices in declaration order (first property = bit 0).
/// Use <see cref="BitIndex"/> to override the automatic assignment.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidPropertyAttribute : Attribute
{
    /// <summary>
    /// Optional explicit bit index (0-127). If not set, assigned automatically.
    /// </summary>
    public int BitIndex { get; set; } = -1;
}

/// <summary>
/// Marks a private field to be wrapped in a source-generated proxy property.
/// Eliminates DTO boilerplate by auto-generating public partial properties.
/// </summary>
/// <example>
/// <code>
/// [ValidObject]
/// public partial class Order : ValidObjectBase
/// {
///     [ValidField] private string _orderNumber = "";
///     [ValidField] private decimal _total;
///     // Generator emits: public string OrderNumber { get; set; }
///     // Generator emits: public decimal Total { get; set; }
/// }
/// </code>
/// </example>
/// <remarks>
/// <para>
/// The generator creates a public property with the same name (PascalCase) and
/// wires it into the bitmask tracking system.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ValidFieldAttribute : Attribute
{
}

/// <summary>
/// Base class for validation attributes. Specifies validation rules that compile
/// to both C# IL and JavaScript-equivalent logic.
/// </summary>
/// <example>
/// <code>
/// [ValidObject]
/// public partial class Employee : ValidObjectBase
/// {
///     [ValidProperty, Required("Name is required", "EMP-001")]
///     public string Name { get; set; } = "";
///     
///     [ValidProperty, Range(18, 65, "Age must be 18-65", "EMP-002")]
///     public int Age { get; set; }
/// }
/// </code>
/// </example>
/// <seealso cref="RangeAttribute"/>
/// <seealso cref="RequiredAttribute"/>
/// <seealso cref="StringLengthAttribute"/>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public class ValidationAttribute : Attribute
{
    /// <summary>
    /// Error message displayed when validation fails.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Unique error code for programmatic handling.
    /// </summary>
    public string Code { get; }
    
    /// <summary>
    /// Creates a validation rule with a message and error code.
    /// </summary>
    /// <param name="message">Error message shown on validation failure.</param>
    /// <param name="code">Unique error code (e.g., "VAL-001").</param>
    public ValidationAttribute(string message, string code)
    {
        Message = message;
        Code = code;
    }
}

/// <summary>
/// Defines a numeric range constraint. Validates that the value is between Min and Max (inclusive).
/// </summary>
/// <example>
/// <code>
/// [ValidProperty, Range(0, 100, "Score must be 0-100", "SCORE-RANGE")]
/// public int Score { get; set; }
/// 
/// [ValidProperty, Range(0.01, 999999.99, "Invalid price", "PRICE-RANGE")]
/// public decimal Price { get; set; }
/// </code>
/// </example>
public sealed class RangeAttribute : ValidationAttribute
{
    /// <summary>
    /// Minimum allowed value (inclusive).
    /// </summary>
    public double Min { get; }
    
    /// <summary>
    /// Maximum allowed value (inclusive).
    /// </summary>
    public double Max { get; }

    /// <summary>
    /// Creates a range validation rule.
    /// </summary>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="message">Error message on failure.</param>
    /// <param name="code">Error code.</param>
    public RangeAttribute(double min, double max, string message = "Value out of range", string code = "VAL-001") 
        : base(message, code)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>
/// Marks a string property as required (non-null, non-whitespace).
/// </summary>
/// <example>
/// <code>
/// [ValidProperty, Required("Email is required", "USER-EMAIL-REQ")]
/// public string Email { get; set; } = "";
/// </code>
/// </example>
public sealed class RequiredAttribute : ValidationAttribute
{
    /// <summary>
    /// Creates a required-field validation rule.
    /// </summary>
    /// <param name="message">Error message on failure.</param>
    /// <param name="code">Error code.</param>
    public RequiredAttribute(string message = "Field is required", string code = "VAL-REQ")
        : base(message, code)
    {
    }
}

/// <summary>
/// Constrains the length of a string property (MinimumLength to MaximumLength).
/// </summary>
/// <example>
/// <code>
/// [ValidProperty, StringLength(50, MinimumLength = 3, Message = "Name must be 3-50 chars", Code = "NAME-LEN")]
/// public string Name { get; set; } = "";
/// </code>
/// </example>
public sealed class StringLengthAttribute : ValidationAttribute
{
    /// <summary>
    /// Maximum allowed string length.
    /// </summary>
    public int MaximumLength { get; }
    
    /// <summary>
    /// Minimum allowed string length (default 0).
    /// </summary>
    public int MinimumLength { get; set; } = 0;

    /// <summary>
    /// Creates a string length validation rule.
    /// </summary>
    /// <param name="maximumLength">Maximum length.</param>
    /// <param name="message">Error message on failure.</param>
    /// <param name="code">Error code.</param>
    public StringLengthAttribute(int maximumLength, string message = "Length out of bounds", string code = "VAL-LEN")
        : base(message, code)
    {
        MaximumLength = maximumLength;
    }
}
