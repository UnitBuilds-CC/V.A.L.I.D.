using Valid;
using System.ComponentModel.DataAnnotations;

namespace Valid.Sample.Blazor;

[ValidObject]
public partial class AxiomEntry
{
    [ValidProperty]
    public partial int LineId { get; set; }

    [ValidProperty]
    [Required]
    [StringLength(10, MinimumLength = 3)]
    public partial string AccountCode { get; set; }

    [ValidProperty]
    public partial string Description { get; set; }

    [ValidProperty]
    [Range(0, 500000)]
    public partial double Amount { get; set; }

    [ValidProperty]
    public partial bool IsProcessed { get; set; }

    [ValidProperty]
    [StringLength(20)]
    public partial string EntryType { get; set; }
}
