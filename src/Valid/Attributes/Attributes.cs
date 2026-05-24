using System;

namespace Valid;

/// <summary>
/// Marks a class to be processed by the VALID 3.0.0 generator.
/// Encapsulates triple-mask state tracking (Dirty, Busy, Error).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ValidObjectAttribute : Attribute
{
}

/// <summary>
/// Marks a property to be included in the bitmask state engine.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidPropertyAttribute : Attribute
{
    public int BitIndex { get; set; } = -1;
}

/// <summary>
/// Marks a private field to be wrapped in a source-generated proxy property.
/// Eradicates DTO boilerplate by automatically emitting public partial properties.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ValidFieldAttribute : Attribute
{
}

/// <summary>
/// Specifies validation rules for a property.
/// Compiles to both C# IL and JS-equivalent logic.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public class ValidationAttribute : Attribute
{
    public string Message { get; }
    public string Code { get; }
    
    public ValidationAttribute(string message, string code)
    {
        Message = message;
        Code = code;
    }
}

/// <summary>
/// Defines a range constraint for numeric properties.
/// </summary>
public sealed class RangeAttribute : ValidationAttribute
{
    public double Min { get; }
    public double Max { get; }

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
public sealed class RequiredAttribute : ValidationAttribute
{
    public RequiredAttribute(string message = "Field is required", string code = "VAL-REQ")
        : base(message, code)
    {
    }
}

/// <summary>
/// Constrains the length of a string property.
/// </summary>
public sealed class StringLengthAttribute : ValidationAttribute
{
    public int MaximumLength { get; }
    public int MinimumLength { get; set; } = 0;

    public StringLengthAttribute(int maximumLength, string message = "Length out of bounds", string code = "VAL-LEN")
        : base(message, code)
    {
        MaximumLength = maximumLength;
    }
}
