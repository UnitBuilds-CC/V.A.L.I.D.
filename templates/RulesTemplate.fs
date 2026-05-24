namespace YourApp.Rules

open System

type EntityState =
    | Active = 0
    | Tombstoned = 1

[<Struct>]
type RecordTemplate = {
    Id: int
    Name: string
    Value: double
    LogicalState: EntityState
}

module RulesTemplate =
    [<Struct>]
    type ValidationResult = {
        Property: string
        Message: string
        Code: string
    }

    // Evaluates rules for the record
    let evaluateRules (record: RecordTemplate) =
        [
            if String.IsNullOrWhiteSpace(record.Name) then
                yield { Property = "Name"; Message = "Name is required"; Code = "VAL-REQ-NAME" }
            if record.Value < 0.0 || record.Value > 10000.0 then
                yield { Property = "Value"; Message = "Value out of range"; Code = "VAL-LIMIT-VALUE" }
        ]
