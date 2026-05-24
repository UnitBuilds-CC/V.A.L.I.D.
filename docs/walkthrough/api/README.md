# API Layer

The API layer demonstrates headless (non-UI) usage of the V.A.L.I.D. framework. This is where you'd see server-side or console applications consuming the same `[ValidObject]` models with full bitmask tracking and outbox persistence.

---

## Valid.Sample (Console Demo)

A minimal console application that exercises every tier of the framework without any UI.

### Model Definition

#### `AxiomBatch.cs`

```csharp
[ValidObject]
public partial class AxiomBatch
{
    [ValidProperty]
    public partial string BatchId { get; set; }

    [ValidProperty]
    [Range(0, 1000000, "Value exceeds processing limit")]
    public partial double TotalValue { get; set; }

    [ValidProperty]
    public partial bool IsProcessed { get; set; }
}
```

This is the **complete developer intent**. The source generator produces `AxiomBatch.g.cs` with:
- Backing fields (`_batchid`, `_totalvalue`, `_isprocessed`)
- Property implementations calling `SetProperty()` with bit indices 0, 1, 2
- `Validate()` with range check on `TotalValue`
- `GetDiagnostics()`, `GetDeltaJson()`, `GetValidMetadata()`, etc.
- `AxiomBatch.g.tests.cs` with bitmask integrity tests

Also defines `AxiomLine` as an `unmanaged` struct for `ValidList<T>` / `UnmanagedSlab<T>` usage:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct AxiomLine
{
    public int LineId;
    public double Amount;
}
```

### Entry Point

#### `Program.cs`

Demonstrates three capabilities:

| Step | What It Does |
|---|---|
| **1. Zero-GC Memory** | Creates a `ValidList<AxiomLine>(100000)` and populates it. These 100K structs live in unmanaged memory — completely invisible to the .NET garbage collector. |
| **2. Bitmask Engine** | Creates an `AxiomBatch`, modifies `TotalValue`, and prints the `DirtyFlags` hex to show the bit flip. |
| **3. Durable Outbox** | Creates a `SqliteOutbox` and enqueues the batch. The payload is SHA-256 hash-chained for tamper detection. |

### What This Proves

- The V.A.L.I.D. framework works identically without Blazor, without JavaScript, without a UI
- The same `[ValidObject]` models can be used in APIs, background services, or console tools
- Bitmask tracking and validation fire on every property mutation regardless of runtime context
- The outbox provides durable, offline-first persistence with cryptographic integrity

---

## Using V.A.L.I.D. in Your Own API

To add V.A.L.I.D. to a new project:

### 1. Reference the packages
```xml
<ProjectReference Include="..\Valid\Valid.csproj" />
<ProjectReference Include="..\Valid.Generator\Valid.Generator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### 2. Define your model
```csharp
using Valid;

[ValidObject]
public partial class Invoice
{
    [ValidProperty]
    [Required]
    public partial string InvoiceNumber { get; set; }

    [ValidProperty]
    [Range(0, 999999)]
    public partial double Amount { get; set; }

    [ValidProperty]
    public partial bool IsPaid { get; set; }
}
```

### 3. Use it
```csharp
var invoice = new Invoice();
invoice.InvoiceNumber = "INV-001";
invoice.Amount = 1500.00;

// DirtyFlags now has bits 0 and 1 set
Console.WriteLine($"Dirty: 0x{invoice.DirtyFlags:X}");

// Get only changed fields as JSON
var delta = invoice.GetDeltaJson();
// {"InvoiceNumber":"INV-001","Amount":1500.0}

// Check validation
var diagnostics = invoice.GetDiagnostics();
```

No base class registration, no DI, no configuration. Just attributes and partial properties.
