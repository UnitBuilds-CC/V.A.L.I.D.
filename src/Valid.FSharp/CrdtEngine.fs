namespace Valid.FSharp

open System

module CrdtEngine =

    [<Struct>]
    type PNCounter = {
        Id: string
        P: Map<string, int64>
        N: Map<string, int64>
    }

    let createPNCounter id = { Id = id; P = Map.empty; N = Map.empty }

    let increment count (pnc: PNCounter) =
        let current = pnc.P.TryFind pnc.Id |> Option.defaultValue 0L
        { pnc with P = pnc.P.Add(pnc.Id, current + count) }

    let decrement count (pnc: PNCounter) =
        let current = pnc.N.TryFind pnc.Id |> Option.defaultValue 0L
        { pnc with N = pnc.N.Add(pnc.Id, current + count) }

    let value (pnc: PNCounter) =
        let sumP = pnc.P |> Map.toSeq |> Seq.sumBy snd
        let sumN = pnc.N |> Map.toSeq |> Seq.sumBy snd
        sumP - sumN

    let merge (a: PNCounter) (b: PNCounter) =
        let mergeMaps m1 m2 =
            Map.fold (fun acc key value ->
                match Map.tryFind key acc with
                | Some v -> Map.add key (max v value) acc
                | None -> Map.add key value acc
            ) m1 m2
        { a with P = mergeMaps a.P b.P; N = mergeMaps a.N b.N }

    [<Struct>]
    type GSet<'T when 'T : comparison> = {
        Elements: Set<'T>
    }

    let createGSet () = { Elements = Set.empty }

    let add item (gs: GSet<'T>) = { Elements = gs.Elements.Add(item) }

    let mergeGSets (a: GSet<'T>) (b: GSet<'T>) = { Elements = Set.union a.Elements b.Elements }

    [<Struct>]
    type AWORSet<'T when 'T : comparison> = {
        Added: Map<'T, Set<string>>
        Removed: Map<'T, Set<string>>
    }

    let createAWORSet () = { Added = Map.empty; Removed = Map.empty }

    let addAw tag item (set: AWORSet<'T>) =
        let currentTags = set.Added.TryFind item |> Option.defaultValue Set.empty
        { set with Added = set.Added.Add(item, currentTags.Add(tag)) }

    let removeAw item (set: AWORSet<'T>) =
        match set.Added.TryFind item with
        | Some tags -> { set with Removed = set.Removed.Add(item, tags) }
        | None -> set

    let evaluateLogicalState (set: AWORSet<'T>) item =
        let addedTags = set.Added.TryFind item |> Option.defaultValue Set.empty
        let removedTags = set.Removed.TryFind item |> Option.defaultValue Set.empty
        let survivingTags = Set.difference addedTags removedTags
        if Set.isEmpty survivingTags then EntityState.Tombstoned else EntityState.Active

    let mergeAWORSets (a: AWORSet<'T>) (b: AWORSet<'T>) =
        let mergeMaps m1 m2 =
            Map.fold (fun acc key tags ->
                match Map.tryFind key acc with
                | Some existingTags -> Map.add key (Set.union existingTags tags) acc
                | None -> Map.add key tags acc
            ) m1 m2
            
        { 
            Added = mergeMaps a.Added b.Added
            Removed = mergeMaps a.Removed b.Removed
        }

    [<Struct>]
    type ValidationResult = {
        Property: string
        Message: string
        Code: string
    }

    let evaluateRules (entry: AxiomEntryRecord) =
        [
            if entry.Amount < 0.0 then 
                { Property = "Amount"; Message = "Negative flow detected"; Code = "SEC-VIOL" }
            if String.IsNullOrWhiteSpace(entry.AccountCode) then
                { Property = "AccountCode"; Message = "Account code missing"; Code = "VAL-REQ" }
            if entry.Description.Length > 255 then
                { Property = "Description"; Message = "Description too long"; Code = "VAL-LEN" }
        ]
