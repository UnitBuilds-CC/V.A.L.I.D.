namespace Valid.FSharp

[<Struct>]
type EntityState = 
    | Active
    | Tombstoned

[<Struct>]
type AxiomEntryRecord = {
    LineId: int
    AccountCode: string
    Description: string
    Amount: double
    IsProcessed: bool
    EntryType: string
    LogicalState: EntityState // MISSION 25: Business Logic Resolver
}
