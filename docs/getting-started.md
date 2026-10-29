# Getting Started with V.A.L.I.D.

**Vectorized Asynchronous Logic & Intelligent Diagnostics**

V.A.L.I.D. is a high-performance business object framework for .NET 8+ that replaces CSLA with a fraction of the ceremony. Define objects with attributes, persist with shuttles, validate with rules — all wired through standard NuGet packages. State tracking uses 128-bit `UInt128` bitmasks for zero-allocation dirty/busy/error tracking.

## Installation

> **Read this before copying any snippet below.** The package id is `VALID` (not `Valid`), it is
> **not published to nuget.org** — `dotnet add package VALID` will fail — and it is produced by
> `pack.ps1` / `dotnet pack` into `dist/`, so a consuming project needs a NuGet source pointing at
> that folder. There is no `Valid.Infrastructure.Components` package in this repository.
>
> The `ValidFactory`, `RuleEngine`, `IDataShuttle<T>`, `ValidForm` and `ValidField` types used in the
> examples below are **not in the shipped source** (repo-wide: 0 hits outside `docs/` and this page).
> What exists instead: `new T()` or hydration through the generated `UpdatePropertyFromJson`,
> generated `CalculateValidationState()` + `GetDiagnostics()` for rules, `GetDeltaJson()` for the
> wire payload, and a hand-written DAL/repository behind whatever transport you choose — the
> framework provides no fetch path, no HTTP layer and no auth.

```bash
dotnet build src/Valid/Valid.csproj -c Release
dotnet pack src/Valid/Valid.csproj -c Release -o dist --no-build
# then add dist/ as a NuGet source in the consuming repo
```

If using central package management (`Directory.Packages.props`):

```xml
<PropertyGroup>
  <ValidVersion>3.2.0</ValidVersion>
</PropertyGroup>

<ItemGroup>
  <PackageVersion Include="VALID" Version="$(ValidVersion)" />
</ItemGroup>
```

Then in your module csproj (no version needed):

```xml
<PackageReference Include="VALID" />
```

## Your First Business Object

```csharp
using Valid;
using Valid.Attributes;

[ValidObject]
public partial class Invoice : ValidObjectBase
{
    // The source generator emits backing fields, bitmask tracking, and validation.
    // Each property is assigned a bit index in the UInt128 dirty/busy/error masks.

    [Required]
    public partial string CustomerName { get; set; }

    [Range(0, 1_000_000)]
    public partial decimal Amount { get; set; }

    public partial DateTime InvoiceDate { get; set; }
}
```

> **`ValidId`**: Every `ValidObjectBase` subclass automatically receives a unique runtime identifier
> (e.g., `valid_1`, `valid_2`). Use `invoice.ValidId` for JS bridge registration, MCP tracking,
> and logging — never `GetHashCode()`.

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
