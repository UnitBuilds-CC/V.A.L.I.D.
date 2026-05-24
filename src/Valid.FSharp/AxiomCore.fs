namespace Valid.FSharp

open System
open System.Text.Json

module AxiomCore =
    let createEntry id account desc amount processed entryType =
        { LineId = id; AccountCode = account; Description = desc; Amount = amount; IsProcessed = processed; EntryType = entryType; LogicalState = EntityState.Active }

    let mutateEntry (entry: AxiomEntryRecord) =
        let rand = Random.Shared
        let amountDelta = (rand.NextDouble() - 0.5) * 5.0
        let newAmount =
            if rand.Next(0, 100) < 2 then -5.0
            else Math.Max(0.0, entry.Amount + amountDelta)

        let newLogicalState = if rand.Next(0, 100) < 5 then EntityState.Tombstoned else entry.LogicalState

        { entry with
            Amount = newAmount
            AccountCode = if rand.Next(0, 100) < 10 then sprintf "ACC_%d" (rand.Next(1000, 9999)) else entry.AccountCode
            IsProcessed = if rand.Next(0, 100) < 5 then not entry.IsProcessed else entry.IsProcessed
            LogicalState = newLogicalState }

    let getDeltaJson (oldEntry: AxiomEntryRecord) (newEntry: AxiomEntryRecord) =
        let mutable delta = Map.empty<string, obj>
        if oldEntry.Amount <> newEntry.Amount then delta <- delta.Add("Amount", newEntry.Amount)
        if oldEntry.AccountCode <> newEntry.AccountCode then delta <- delta.Add("AccountCode", newEntry.AccountCode)
        if oldEntry.IsProcessed <> newEntry.IsProcessed then delta <- delta.Add("IsProcessed", newEntry.IsProcessed)
        if oldEntry.Description <> newEntry.Description then delta <- delta.Add("Description", newEntry.Description)
        
        if delta.IsEmpty then "{}"
        else JsonSerializer.Serialize(delta)

    let resolveLogicalCollision (nodeAState: AxiomEntryRecord) (nodeBState: AxiomEntryRecord) =
        let aworSet = CrdtEngine.createAWORSet ()
        let setWithA = CrdtEngine.addAw "edit_nodeA" nodeAState.LineId aworSet
        let setWithAandB = if nodeBState.LogicalState = EntityState.Tombstoned then CrdtEngine.removeAw nodeBState.LineId setWithA else setWithA
        
        let finalLogicalState = CrdtEngine.evaluateLogicalState setWithAandB nodeAState.LineId
        { nodeAState with LogicalState = finalLogicalState }
