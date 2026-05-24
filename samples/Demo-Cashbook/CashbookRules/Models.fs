namespace CashbookRules

open System

type CashbookModule =
    | GeneralLedger = 1
    | AccountsReceivable = 2
    | AccountsPayable = 3

type EntityState =
    | Active = 0
    | Tombstoned = 1

[<Struct>]
type CashbookLineRecord = {
    Id: int
    LineNo: int
    CashbookId: int
    BankReference: string
    TxDate: DateTime
    Module: CashbookModule
    AccountCode: string
    Description: string
    Reference: string
    Deposit: double
    Payment: double
    ExchangeRate: double
    ForeignDeposit: double
    ForeignPayment: double
    TaxAmount: double
    Reconciled: bool
    LogicalState: EntityState
}

[<Struct>]
type CashbookRecord = {
    Id: int
    CashbookNumber: string
    Description: string
    UpdateDate: DateTime
    CurrencyCode: string
    ValidationTotalDeposits: double
    ValidationTotalPayments: double
    BankAccountCode: string
    LogicalState: EntityState
}
