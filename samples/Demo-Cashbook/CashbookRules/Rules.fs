namespace CashbookRules

open System
open Valid.FSharp

module Rules =

    [<Struct>]
    type ValidationResult = {
        Property: string
        Message: string
        Code: string
    }

    // Evaluate rules for cashbook lines
    let evaluateLineRules (line: CashbookLineRecord) =
        [
            if line.Deposit < 0.0 then
                yield { Property = "Deposit"; Message = "Deposit cannot be negative"; Code = "VAL-NEG-DEP" }
            if line.Payment < 0.0 then
                yield { Property = "Payment"; Message = "Payment cannot be negative"; Code = "VAL-NEG-PAY" }
            if line.Deposit > 0.0 && line.Payment > 0.0 then
                yield { Property = "Deposit"; Message = "An entry cannot be both a Deposit and a Payment"; Code = "VAL-MUTEX" }
                yield { Property = "Payment"; Message = "An entry cannot be both a Deposit and a Payment"; Code = "VAL-MUTEX" }
            if String.IsNullOrWhiteSpace(line.Reference) then
                yield { Property = "Reference"; Message = "Reference is required"; Code = "VAL-REQ-REF" }
            else if line.Reference.Length > 50 then
                yield { Property = "Reference"; Message = "Reference cannot exceed 50 characters"; Code = "VAL-LEN-REF" }
            if String.IsNullOrWhiteSpace(line.Description) then
                yield { Property = "Description"; Message = "Description is required"; Code = "VAL-REQ-DESC" }
            else if line.Description.Length > 255 then
                yield { Property = "Description"; Message = "Description cannot exceed 255 characters"; Code = "VAL-LEN-DESC" }
            if line.ExchangeRate <= 0.0 then
                yield { Property = "ExchangeRate"; Message = "Exchange rate must be positive and non-zero"; Code = "VAL-RATE" }
            if line.TaxAmount < 0.0 then
                yield { Property = "TaxAmount"; Message = "Tax amount cannot be negative"; Code = "VAL-NEG-TAX" }
            else
                let maxAmt = Math.Max(line.Deposit, line.Payment)
                if line.TaxAmount > maxAmt then
                    yield { Property = "TaxAmount"; Message = "Tax amount cannot exceed the transaction amount"; Code = "VAL-LIMIT-TAX" }
            if String.IsNullOrWhiteSpace(line.AccountCode) then
                yield { Property = "AccountCode"; Message = "Account code is required"; Code = "VAL-REQ-ACC" }
        ]

    // Evaluate rules for cashbooks
    let evaluateCashbookRules (cashbook: CashbookRecord) =
        [
            if String.IsNullOrWhiteSpace(cashbook.CashbookNumber) then
                yield { Property = "CashbookNumber"; Message = "Cashbook number is required"; Code = "VAL-REQ-CNUM" }
            if String.IsNullOrWhiteSpace(cashbook.Description) then
                yield { Property = "Description"; Message = "Cashbook description is required"; Code = "VAL-REQ-CDESC" }
            if String.IsNullOrWhiteSpace(cashbook.CurrencyCode) then
                yield { Property = "CurrencyCode"; Message = "Currency code is required"; Code = "VAL-REQ-CURR" }
            if cashbook.ValidationTotalDeposits < 0.0 then
                yield { Property = "ValidationTotalDeposits"; Message = "Validation total deposits cannot be negative"; Code = "VAL-NEG-VDEP" }
            if cashbook.ValidationTotalPayments < 0.0 then
                yield { Property = "ValidationTotalPayments"; Message = "Validation total payments cannot be negative"; Code = "VAL-NEG-VPAY" }
        ]

    // CRDT collision resolver for cashbook lines
    let resolveLineLogicalCollision (nodeAState: CashbookLineRecord) (nodeBState: CashbookLineRecord) =
        let aworSet = CrdtEngine.createAWORSet ()
        let setWithA = CrdtEngine.addAw "edit_nodeA" nodeAState.Id aworSet
        let setWithAandB = 
            if nodeBState.LogicalState = CashbookRules.EntityState.Tombstoned then 
                CrdtEngine.removeAw nodeBState.Id setWithA 
            else 
                setWithA
        
        let fsState = CrdtEngine.evaluateLogicalState setWithAandB nodeAState.Id
        let finalState = 
            match fsState with
            | Active -> CashbookRules.EntityState.Active
            | Tombstoned -> CashbookRules.EntityState.Tombstoned

        { nodeAState with LogicalState = finalState }
