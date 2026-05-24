# Getting Started with V.A.L.I.D.

**Virtualized Asynchronous Layer for Integrated Data**

V.A.L.I.D. is a lightweight business object framework for .NET 8+ that replaces CSLA with a fraction of the ceremony. Define objects with attributes, persist with shuttles, validate with rules — all wired through standard NuGet packages.

## Installation

```bash
dotnet add package Valid.Core                        # Business objects, shuttles, rules
dotnet add package Valid.Infrastructure.Components   # Blazor components (ValidField, ValidForm)
```

If using central package management (`Directory.Packages.Props`):

```xml
<PropertyGroup>
  <ValidVersion>1.1.0</ValidVersion>
</PropertyGroup>

<ItemGroup>
  <PackageVersion Include="Valid.Core" Version="$(ValidVersion)" />
  <PackageVersion Include="Valid.Infrastructure.Components" Version="$(ValidVersion)" />
</ItemGroup>
```

Then in your module csproj (no version needed):

```xml
<PackageReference Include="Valid.Core" />
<PackageReference Include="Valid.Infrastructure.Components" />
```

## Your First Business Object

```csharp
using Valid.Attributes;
using Valid.Core;

[ValidObject]
public class Invoice : IValidObject
{
    // --- Tracked properties ---
    [Required]
    public string CustomerName { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Amount { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    // --- IValidObject plumbing ---
    public bool IsDirty { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsInvalid => !Diagnostics.IsValid;
    public DiagnosticResult Diagnostics { get; } = new();
    public IParent? Parent { get; set; }

    // ... (remaining interface members — see Core Concepts)
}
```

## Create and Validate

```csharp
// Create a new object
var invoice = ValidFactory.Create<Invoice>();
invoice.CustomerName = "Acme Corp";
invoice.Amount = 5000m;

// Validate
var result = RuleEngine.Validate(invoice);
if (result.Success)
    Log.Information("[V.A.L.I.D.] Validation Success!");
else
    foreach (var diag in result.Diagnostics)
        Log.Information("[V.A.L.I.D.] Validation Error: {Property} - {Message}", diag.PropertyName, diag.Message);
```

## Persist with a Shuttle

```csharp
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
        await _dal.SaveAsync(MapToDto(obj));
        obj.ResetDirtyFlags(true);
    }

    public async Task DeleteAsync(object id)
    {
        await _dal.DeleteAsync((int)id);
    }
}
```

## Register in DI (ModuleInitializer)

```csharp
public static class InvoiceModuleInitializer
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IDataShuttle<Invoice>, InvoiceShuttle>();
    }
}
```

## Use in Blazor

```razor
@inject IDataShuttle<Invoice> Shuttle

<ValidForm Model="@_invoice" OnValidSubmit="HandleSave">
    <ValidField TValue="string" Model="@_invoice" PropertyName="CustomerName" Label="Customer" />
    <ValidField TValue="decimal" Model="@_invoice" PropertyName="Amount" Label="Amount" />
    <MudButton ButtonType="ButtonType.Submit" Disabled="@(!_invoice.IsDirty)">Save</MudButton>
</ValidForm>

@code {
    private Invoice _invoice = ValidFactory.Create<Invoice>();

    private async Task HandleSave(EditContext ctx)
    {
        await Shuttle.SaveAsync(_invoice);
    }
}
```

## Next Steps

- [Core Concepts](core-concepts.md) — IValidObject, dirty tracking, diagnostics
- [Data Shuttle](data-shuttle.md) — Fetch/Save/Delete patterns
- [Validation](validation.md) — Rules, async rules, custom rules
- [Edit Snapshots](edit-snapshots.md) — BeginEdit/CancelEdit undo
- [Blazor Components](blazor-components.md) — ValidField, ValidForm, ValidValidator
- [Migration from CSLA](migration-from-csla.md) — Side-by-side comparison
