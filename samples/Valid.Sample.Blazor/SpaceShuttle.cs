using Valid;
using System.Runtime.InteropServices;

namespace Valid.Sample.Blazor;

/// <summary>
/// A high-performance business object using VALID 3.0.0.
/// Uses C# 13 Partial Properties for the cleanest intent definition.
/// </summary>
[ValidObject]
public partial class SpaceShuttle
{
    [ValidProperty]
    public partial string ShuttleName { get; set; }

    [ValidProperty]
    [Range(0, 50000, "Velocity must be between 0 and 50,000 km/h")]
    public partial double Velocity { get; set; }

    [ValidProperty]
    public partial bool IsDocked { get; set; }
}

