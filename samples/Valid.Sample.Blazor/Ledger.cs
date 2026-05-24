using Valid;

namespace Valid.Sample.Blazor;

[ValidObject]
public partial class LedgerLine
{
    [ValidProperty]
    public partial int LineId { get; set; }

    [ValidProperty]
    public partial double Credit { get; set; }

    [ValidProperty]
    public partial double Debit { get; set; }

    [ValidProperty]
    public partial string Memo { get; set; }
}

[ValidObject]
public partial class LedgerHeader
{
    [ValidProperty]
    public partial double TotalCredit { get; set; }

    [ValidProperty]
    public partial double TotalDebit { get; set; }

    [ValidProperty]
    public partial string Status { get; set; }
}
