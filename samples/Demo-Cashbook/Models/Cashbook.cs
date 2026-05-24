using System;
using System.Collections.Generic;
using Valid;

namespace DemoCashbook.Models;

[ValidObject]
public partial class Cashbook : ValidObjectBase
{
    [ValidProperty]
    public partial int Id { get; set; }

    [ValidProperty]
    [Required("Cashbook number is required", "VAL-REQ-CNUM")]
    public partial string CashbookNumber { get; set; }

    [ValidProperty]
    [Required("Cashbook description is required", "VAL-REQ-CDESC")]
    public partial string Description { get; set; }

    [ValidProperty]
    public partial DateTime UpdateDate { get; set; }

    [ValidProperty]
    [Required("Currency code is required", "VAL-REQ-CURR")]
    public partial string CurrencyCode { get; set; }

    [ValidProperty]
    [Range(0.0, double.MaxValue, "Validation total deposits cannot be negative", "VAL-NEG-VDEP")]
    public partial double ValidationTotalDeposits { get; set; }

    [ValidProperty]
    [Range(0.0, double.MaxValue, "Validation total payments cannot be negative", "VAL-NEG-VPAY")]
    public partial double ValidationTotalPayments { get; set; }

    [ValidProperty]
    public partial string BankAccountCode { get; set; }

    public Cashbook()
    {
        CashbookNumber = string.Empty;
        Description = "Cashbook App";
        UpdateDate = DateTime.Now;
        CurrencyCode = "ZAR";
        BankAccountCode = string.Empty;
    }
}
