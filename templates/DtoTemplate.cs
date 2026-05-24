using System;
using Valid;

namespace YourApp.Models;

/// <summary>
/// Template for a V.A.L.I.D. business DTO.
/// </summary>
[ValidObject]
public partial class DtoTemplate : ValidObjectBase
{
    [ValidProperty]
    public partial int Id { get; set; }

    [ValidProperty]
    [Required("Name is required", "VAL-REQ-NAME")]
    public partial string Name { get; set; }

    [ValidProperty]
    [Range(0.0, 10000.0, "Value out of range", "VAL-LIMIT-VALUE")]
    public partial double Value { get; set; }

    public DtoTemplate()
    {
        Name = string.Empty;
    }

    public override System.UInt128 CalculateValidationState()
    {
        System.UInt128 newErrors = 0;
        
        if (string.IsNullOrWhiteSpace(Name))
        {
            int bit = GetBitIndex(nameof(Name));
            newErrors |= ((System.UInt128)1 << bit);
        }

        if (Value < 0.0 || Value > 10000.0)
        {
            int bit = GetBitIndex(nameof(Value));
            newErrors |= ((System.UInt128)1 << bit);
        }

        return newErrors;
    }

    public override IEnumerable<DiagnosticResult> GetDiagnostics()
    {
        var diagnostics = new List<DiagnosticResult>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            diagnostics.Add(new DiagnosticResult(nameof(Name), "Name is required", "VAL-REQ-NAME", null));
        }

        if (Value < 0.0 || Value > 10000.0)
        {
            diagnostics.Add(new DiagnosticResult(nameof(Value), "Value out of range", "VAL-LIMIT-VALUE", null));
        }

        return diagnostics;
    }
}
