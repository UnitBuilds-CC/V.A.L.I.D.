# Migration from CSLA

A side-by-side guide for developers moving from CSLA to V.A.L.I.D.

## Concept Mapping

| CSLA Concept | V.A.L.I.D. Equivalent | Notes |
|---|---|---|
| `BusinessBase<T>` | `IValidObject` | Interface, not base class |
| `BusinessListBase<T,C>` | `ValidList<T>` | Observable collection with dirty tracking |
| `DataPortal` | `IDataShuttle<T>` | 3 methods: Fetch, Save, Delete |
| `ChildDataPortal` | Direct loop in shuttle | No separate child portal |
| `UndoableBase` | `BeginEdit()`/`CancelEdit()` | Reflection-based snapshot |
| `ManagedProperties` / `RegisterProperty` | `[ValidProperty]` attribute | Declarative, no FieldManager |
| `AuthorizationRules` | `[Authorized("Role")]` + `ValidSecurity.IsInRole` | Thread-safe `AsyncLocal` isolation |
| `BusinessRules` | `RuleEngine` + `RuleAttribute` | Attribute-based rules |
| `ValidationRules` | `RuleEngine.Validate()` | Single static method |
| `INotifyPropertyChanged` | `INotifyPropertyChanged` | Same .NET interface |
| `ObjectFactory` | `ValidFactory` | `Create<T>()` and `Create<T>(dto)` |
| `[NotUndoable]` | Not needed | Only `[ValidProperty]` participates |
| `MarkDirty()` | Automatic | Property setter triggers dirty |
| `MarkClean()` | `ResetDirtyFlags(true)` | Explicit reset |
| `AddRule()` | `[Required]`, `[Range]`, custom `RuleAttribute` | Declarative |
| `BinaryFormatter` | CRDT / JSON | Recursive sync with `MAX_RECURSION_DEPTH` |

## Business Object: Before & After

### CSLA

```csharp
[Serializable]
public class Invoice : BusinessBase<Invoice>
{
    public static readonly PropertyInfo<string> CustomerNameProperty =
        RegisterProperty<string>(nameof(CustomerName));
    public string CustomerName
    {
        get => GetProperty(CustomerNameProperty);
        set => SetProperty(CustomerNameProperty, value);
    }

    public static readonly PropertyInfo<decimal> AmountProperty =
        RegisterProperty<decimal>(nameof(Amount));
    public decimal Amount
    {
        get => GetProperty(AmountProperty);
        set => SetProperty(AmountProperty, value);
    }

    protected override void AddBusinessRules()
    {
        BusinessRules.AddRule(new Required(CustomerNameProperty));
        BusinessRules.AddRule(new MinValue<decimal>(AmountProperty, 0));
        BusinessRules.AddRule(new MaxValue<decimal>(AmountProperty, 1_000_000));
    }

    [Fetch]
    private async Task DataPortal_Fetch(int id, [Inject] IInvoiceDal dal)
    {
        var dto = await dal.GetAsync(id);
        using (BypassPropertyChecks)
        {
            CustomerName = dto.CustomerName;
            Amount = dto.Amount;
        }
    }

    [Insert]
    private async Task DataPortal_Insert([Inject] IInvoiceDal dal)
    {
        var dto = new InvoiceDto { CustomerName = CustomerName, Amount = Amount };
        await dal.InsertAsync(dto);
    }

    [Update]
    private async Task DataPortal_Update([Inject] IInvoiceDal dal)
    {
        var dto = new InvoiceDto { CustomerName = CustomerName, Amount = Amount };
        await dal.UpdateAsync(dto);
    }
}
```

### V.A.L.I.D.

```csharp
[ValidObject]
public class Invoice : IValidObject
{
    [Required]
    public string CustomerName { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Amount { get; set; }

    // IValidObject plumbing (auto-generated via source generator)
    public bool IsDirty { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsInvalid => Diagnostics.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public DiagnosticResult Diagnostics { get; } = new();
    // ...
}

// Separate shuttle (clean single-responsibility)
public class InvoiceShuttle : IDataShuttle<Invoice>
{
    private readonly IInvoiceDal _dal;
    public InvoiceShuttle(IInvoiceDal dal) => _dal = dal;

    public async Task<Invoice> FetchAsync(object id)
    {
        var dto = await _dal.GetAsync((int)id);
        return ValidFactory.Create<Invoice>(dto);
    }

    public async Task SaveAsync(Invoice obj)
    {
        if (!obj.IsDirty) return;
        await _dal.SaveAsync(new InvoiceDto
        {
            CustomerName = obj.CustomerName,
            Amount = obj.Amount
        });
        obj.ResetDirtyFlags(true);
    }

    public async Task DeleteAsync(object id) => await _dal.DeleteAsync((int)id);
}
```

**Lines of code: CSLA ~65 → V.A.L.I.D. ~35** (46% reduction)

## DataPortal → Shuttle

### CSLA

```csharp
// Startup
services.AddCsla(o => o
    .AddBlazorWasmSupport()
    .DataPortal(dp => dp.AddClientSideDataPortal(o => o
        .UseHttpProxy(o => o.DataPortalUrl = "/api/dataportal")))
);

// Usage
@inject IDataPortal<Invoice> Portal

var invoice = await Portal.FetchAsync(42);
invoice.CustomerName = "New Name";
await invoice.SaveAsync(); // hidden DataPortal call
```

### V.A.L.I.D.

```csharp
// Startup
services.AddScoped<IDataShuttle<Invoice>, InvoiceShuttle>();

// Usage
@inject IDataShuttle<Invoice> Shuttle

var invoice = await Shuttle.FetchAsync(42);
invoice.CustomerName = "New Name";
await Shuttle.SaveAsync(invoice); // explicit
```

## Undo: N-Level → Single Snapshot

### CSLA

```csharp
// Must mark every non-undoable field manually:
[NotUndoable] private IInvoiceDal _dal;
[NotUndoable] private bool _isLoading;

// N-level stack
invoice.BeginEdit();  // Level 1
invoice.BeginEdit();  // Level 2
invoice.CancelEdit(); // Back to Level 1
invoice.CancelEdit(); // Back to original
```

### V.A.L.I.D.

```csharp
// No annotations needed. Only [ValidProperty] participates.
invoice.BeginEdit();  // Snapshot
// ... user edits ...
invoice.CancelEdit(); // Restore. Done.
```

## UI Components

### CSLA

```razor
<EditForm Model="@_invoice">
    <DataAnnotationsValidator />
    <InputText @bind-Value="_invoice.CustomerName" />
    <ValidationMessage For="() => _invoice.CustomerName" />
</EditForm>
```

### V.A.L.I.D.

```razor
<ValidForm Model="@_invoice" OnValidSubmit="HandleSave">
    <ValidField TValue="string" Model="@_invoice" PropertyName="CustomerName" Label="Customer" />
</ValidForm>
```

## Deep Architecture Mapping

### Tiers comparison
- **CSLA**: Monolithic Base Classes. Logic is tightly coupled to `BusinessBase`.
- **V.A.L.I.D.**: Decoupled Interfaces. Logic is separated into **Core** (Logic), **Shuttle** (IO), and **Infrastructure** (UI).

### Dependency Injection
- **CSLA**: Uses static `DataPortal` or complex internal DI wire-ups (`[Inject]`).
- **V.A.L.I.D.**: Standard .NET Scoped/Transient registration for everything. Clean, mockable, and testable.

### State Persistence
- **CSLA**: Full object serialization with `BinaryFormatter` or `MobileFormatter`.
- **V.A.L.I.D.**: Surgical **Vector Deltas**. Only changed properties are sent over the wire as JSON, drastically reducing bandwidth and improving CRDT merge safety.

## Checklist: Migrating a Business Object

1. ☐ Remove `[Serializable]` and `BusinessBase<T>` inheritance
2. ☐ Implement `IValidObject` (or use `[ValidObject]` + source generator)
3. ☐ Replace `RegisterProperty<T>` + `GetProperty`/`SetProperty` with plain properties
4. ☐ Move `AddBusinessRules()` rules to property attributes (`[Required]`, `[Range]`)
5. ☐ Extract `DataPortal_Fetch`/`DataPortal_Insert`/`DataPortal_Update` into a `IDataShuttle<T>`
6. ☐ Replace `BusinessListBase<T,C>` with `ValidList<T>`
7. ☐ Remove `[NotUndoable]` annotations
8. ☐ Add `BeginEdit()`/`CancelEdit()` if undo is needed
9. ☐ Register shuttle in DI: `services.AddScoped<IDataShuttle<T>, TShuttle>()`
10. ☐ Replace `IDataPortal<T>` injection with `IDataShuttle<T>`
