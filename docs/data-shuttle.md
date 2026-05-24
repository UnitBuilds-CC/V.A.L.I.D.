# Data Shuttle

The **Data Shuttle** is V.A.L.I.D.'s persistence layer — a clean replacement for CSLA's DataPortal. One class per aggregate root, explicit methods, no hidden magic.

## Interface

```csharp
public interface IDataShuttle<T> where T : IValidObject
{
    Task<T> FetchAsync(object id);
    Task SaveAsync(T obj);
    Task DeleteAsync(object id);
}
```

## Implementation Pattern

```csharp
public class InvoiceShuttle : IDataShuttle<Invoice>
{
    private readonly IInvoiceDal _dal;

    public InvoiceShuttle(IInvoiceDal dal)
    {
        _dal = dal;
    }

    public async Task<Invoice> FetchAsync(object id)
    {
        // 1. Fetch DTO from DAL
        var dto = await _dal.GetAsync((int)id);

        // 2. Hydrate V.A.L.I.D. object (convention-based mapping)
        var invoice = ValidFactory.Create<Invoice>(dto);

        // 3. Mark as clean
        invoice.ResetDirtyFlags(true);
        return invoice;
    }

    public async Task SaveAsync(Invoice obj)
    {
        if (!obj.IsDirty) return;

        // Map V.A.L.I.D. object → DTO explicitly
        // (Required when DTO has ID fields like CustomerId
        //  that V.A.L.I.D. objects carry as full objects like Customer)
        var dto = MapToDto(obj);
        await _dal.SaveAsync(dto);

        obj.ResetDirtyFlags(true);
    }

    public async Task DeleteAsync(object id)
    {
        await _dal.DeleteAsync((int)id);
    }

    private InvoiceDto MapToDto(Invoice obj) => new()
    {
        Id = obj.Id,
        CustomerName = obj.CustomerName,
        Amount = obj.Amount,
        CustomerId = obj.Customer?.Id,  // Resolve ID from DTO object
    };
}
```

## Fetch Path: Convention Mapping

`ValidFactory.Create<T>(dto)` uses reflection to map matching property names:

```
DTO.Description  →  Object.Description     ✅ name match
DTO.GlAccount    →  Object.GlAccount       ✅ name match (full DTO object)
DTO.GlAccountId  →  Object.GlAccountId     ❌ V.A.L.I.D. objects don't carry ID fields
```

This works because V.A.L.I.D. objects carry full DTO reference objects (not just IDs).

## Save Path: Explicit Mapping

On save, you map back explicitly because the DAL/DB layer expects ID-based fields:

```csharp
// V.A.L.I.D. carries: line.GlAccount (full CGlAccountDto with .Id, .Name, etc.)
// DAL expects:         lineDto.GlAccountId (int — foreign key)
// Your shuttle maps:   GlAccountId = line.GlAccount?.Id
```

This replaces CSLA's `SyncAccountIdsFromObjects()` pattern.

## Registration

Register in your module's initializer:

```csharp
services.AddScoped<IDataShuttle<Invoice>, InvoiceShuttle>();
services.AddScoped<IDataShuttle<AxiomBatch>, AxiomShuttle>();
```

## Child Objects

Lines/children are handled inside the parent shuttle:

```csharp
public async Task<AxiomBatch> FetchAsync(object id)
{
    var dto = await _dal.FetchBatchAsync((int)id);
    var batch = ValidFactory.Create<AxiomBatch>(dto);

    foreach (var lineDto in dto.Lines)
    {
        var line = ValidFactory.Create<AxiomBatchLine>(lineDto);
        batch.Lines.Add(line);
    }

    batch.ResetDirtyFlags(true);
    return batch;
}
```

## CSLA DataPortal vs V.A.L.I.D. Shuttle

| Feature | CSLA DataPortal | V.A.L.I.D. Shuttle |
|---|---|---|
| Interface | `IDataPortal<T>` + `[Fetch]`/`[Insert]`/`[Update]`/`[Delete]` | `IDataShuttle<T>` with 3 methods |
| Child handling | `ChildDataPortal<T>`, `UpdateChild()` | Direct loop in parent shuttle |
| Mapping | ManagedProperties + FieldManager | `ValidFactory.Create<T>(dto)` + explicit `MapToDto()` |
| Configuration | `builder.DataPortal(o => o.AddServerSideDataPortal())` | None — just register the shuttle |
| Lines of code | ~100+ per BO | ~30-50 per shuttle |
