using Valid;
using System.Runtime.InteropServices;

namespace Valid.Sample;

/// <summary>
/// A high-performance batch object using VALID 3.0.0.
/// Demonstrates C# 13 partial properties for zero-boilerplate intent.
/// </summary>
[ValidObject]
public partial class AxiomBatch
{
    [ValidProperty]
    public partial string BatchId { get; set; }

    [ValidProperty]
    [Range(0, 1000000, "Value exceeds processing limit")]
    public partial double TotalValue { get; set; }

    [ValidProperty]
    public partial bool IsProcessed { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public struct AxiomLine
{
    public int LineId;
    public double Amount;
}
