using System;
using System.Collections.Generic;
using Valid;

namespace DemoCashbook.Models;

[ValidObject]
public partial class CashbookLine : ValidObjectBase
{
    [ValidProperty]
    public partial int Id { get; set; }

    [ValidProperty]
    public partial int LineNo { get; set; }

    [ValidProperty]
    public partial int CashbookId { get; set; }

    [ValidProperty]
    public partial string BankReference { get; set; }

    [ValidProperty]
    public partial DateTime TxDate { get; set; }

    [ValidProperty]
    public partial CashbookModule Module { get; set; }

    [ValidProperty]
    public partial string AccountCode { get; set; }

    [ValidProperty]
    public partial string Description { get; set; }

    [ValidProperty]
    public partial string Reference { get; set; }

    [ValidProperty]
    public partial double Deposit { get; set; }

    [ValidProperty]
    public partial double Payment { get; set; }

    [ValidProperty]
    public partial double ExchangeRate { get; set; }

    [ValidProperty]
    public partial double ForeignDeposit { get; set; }

    [ValidProperty]
    public partial double ForeignPayment { get; set; }

    [ValidProperty]
    public partial double TaxAmount { get; set; }

    [ValidProperty]
    public partial bool Reconciled { get; set; }

    public CashbookLine()
    {
        BankReference = string.Empty;
        TxDate = DateTime.Now;
        Module = CashbookModule.GeneralLedger;
        AccountCode = string.Empty;
        Description = string.Empty;
        Reference = string.Empty;
        ExchangeRate = 1.0;
    }

    public CashbookRules.EntityState LogicalState { get; set; } = CashbookRules.EntityState.Active;

    private readonly List<DiagnosticResult> _diagnostics = new();

    public override System.UInt128 CalculateValidationState()
    {
        _diagnostics.Clear();
        System.UInt128 newErrors = 0;

        var fsharpRecord = new CashbookRules.CashbookLineRecord(
            Id,
            LineNo,
            CashbookId,
            BankReference ?? string.Empty,
            TxDate,
            (CashbookRules.CashbookModule)(int)Module,
            AccountCode ?? string.Empty,
            Description ?? string.Empty,
            Reference ?? string.Empty,
            Deposit,
            Payment,
            ExchangeRate,
            ForeignDeposit,
            ForeignPayment,
            TaxAmount,
            Reconciled,
            LogicalState
        );

        var fsharpErrors = CashbookRules.Rules.evaluateLineRules(fsharpRecord);

        foreach (var error in fsharpErrors)
        {
            int bit = GetBitIndex(error.Property);
            if (bit >= 0 && bit < 128)
            {
                newErrors |= ((System.UInt128)1 << bit);
            }
            _diagnostics.Add(new DiagnosticResult(error.Property, error.Message, error.Code, null));
        }

        return newErrors;
    }

    public override IEnumerable<DiagnosticResult> GetDiagnostics()
    {
        return _diagnostics;
    }
}
