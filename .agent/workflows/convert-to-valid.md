---
description: How to convert any C# class or module to the V.A.L.I.D. 3.0 framework (1-shot, zero bug hunting)
---

# Convert a Module to V.A.L.I.D. 3.0

This workflow converts any plain C# class (DTO, BO, ViewModel, CSLA BusinessBase, etc.) into a fully source-generated V.A.L.I.D. 3.0.5 object. Follow every step exactly to guarantee a clean compile on the first attempt.

---

## Architecture Overview

V.A.L.I.D. uses a Roslyn source generator (`PropertyWeirGenerator`) to emit all boilerplate at compile time. The developer writes a minimal "intent declaration" (class + attributes), and the generator produces:

- Backing fields and property implementations with bitmask dirty tracking
- `INotifyPropertyChanged` event firing
- JSON hydration (`Hydrate`, `UpdatePropertyFromJson`)
- Zero-copy delta serialization (`GetDeltaJson`) — only serializes dirty properties
- Reflection-free property access (`SetPropertyValue`, `GetPropertyValue`, `GetPropertyType`, `GetBitIndex`)
- Schema metadata (`GetValidMetadata`) for the VAVID browser HUD
- Nested child object change propagation
- Validation state calculation (`CalculateValidationState`, `GetDiagnostics`)
- Automated xUnit tests (`Bitmask_Integrity_Verification`, `Chaos_Monkey_Bitmask_Fuzzer`)
- VAVID Playwright AutoPilot scripts

The base class `ValidObjectBase` provides the quad-mask storage (`_dirtyFlags`, `_busyFlags`, `_errorFlags`, `_stateFlags`, `_immutableFlags`), time-travel history, and the `SetProperty<T>` method.

---

## Prerequisites

1. The target project MUST reference the `Valid` project or NuGet package.
2. The `.csproj` MUST use `<LangVersion>13.0</LangVersion>` or later for `partial` properties.
3. The file MUST have `using Valid;` at the top.

---

## Step 1: Class Declaration

```csharp
using Valid;

namespace MyApp;

[ValidObject]
public partial class Invoice
{
}
```

### Rules

- `[ValidObject]` marks the class for the source generator.
- The class MUST be `partial`.
- Do NOT manually write `: ValidObjectBase` — the generator emits this in the generated partial file.
- Do NOT manually implement `INotifyPropertyChanged` — the base class already does this.
- Do NOT manually implement any `IValidObject` members — the generator handles all of them.

## Step 2: Define Fields (Modern 3.0.5+ Pattern)

Use `[ValidField]` on private fields. This is the **preferred pattern** as it minimizes boilerplate and allows for explicit initialization. The generator automatically emits the public property with bitmask dirty tracking.

```csharp
[ValidObject]
public partial class Invoice
{
    [ValidField]
    private string _invoiceNumber = "";

    [ValidField]
    private double _totalAmount;
    
    [ValidField]
    private bool _isPaid;
}
```

The generator strips the leading `_` and capitalizes, creating public properties `InvoiceNumber`, `TotalAmount`, and `IsPaid`.

### Rules

- Every field MUST be `private T _fieldName;`.
- Use a leading underscore (`_`).
- You MUST provide initializers for reference types (e.g., `= ""` for strings).
- Maximum 128 fields per object.

---

## Step 3: Legacy Pattern — Partial Properties

If you prefer to declare properties explicitly, use C# 13 `partial` properties with `[ValidProperty]`:

```csharp
[ValidProperty]
public partial string InvoiceNumber { get; set; }

[ValidProperty]
public partial double TotalAmount { get; set; }
```

### Rules

- Every tracked property MUST be `public partial T Name { get; set; }`.
- Do NOT add initializers to partial property declarations.
- Do NOT add a body to the getter or setter — the generator provides the full implementation.
- The generator assigns bit indexes automatically based on declaration order (0, 1, 2, ...).
- Maximum 128 properties per object (the bitmask is `System.UInt128`).

### Supported Types

| C# Type | JSON Method | Auto-Test Value |
|---|---|---|
| `string` | `reader.GetString()` | `"Valid Test"` |
| `int` | `reader.GetInt32()` | `42` |
| `long` | `reader.GetInt64()` | `42L` |
| `double` | `reader.GetDouble()` | `42.5` |
| `decimal` | `JsonSerializer.Deserialize` | `42.5m` |
| `bool` | `reader.GetBoolean()` | `true` |
| `DateTime` | `JsonSerializer.Deserialize` | `DateTime.Now` |
| `DateOnly` | `JsonSerializer.Deserialize` | `DateOnly.FromDateTime(DateTime.Now)` |
| `Guid` | `JsonSerializer.Deserialize` | `Guid.NewGuid()` |
| `enum` | `JsonSerializer.Deserialize` | `(EnumType)1` |
| `Nullable<T>` | Unwraps to inner type | Same as inner type |
| Another `[ValidObject]` | `JsonSerializer.Deserialize<T>` | `new T()` |

---

## Step 3: Alternative Pattern — Field-Backed Properties

Use `[ValidField]` on private fields when you want to keep the DTO shape explicit (e.g., mapping from an existing database record):

```csharp
[ValidObject]
public partial class Invoice
{
    [ValidField]
    private string _invoiceNumber = "";

    [ValidField]
    private double _totalAmount;
}
```

The generator strips the leading `_` and capitalizes, creating public properties `InvoiceNumber` and `TotalAmount`.

### Rules

- Do NOT mix `[ValidProperty]` and `[ValidField]` for the same logical property.
- The field MUST have a leading underscore (`_fieldName`).
- You MUST provide initializers for reference types (e.g., `= ""` for strings, `= default!` for objects).

---

## Step 4: Validation Attributes

V.A.L.I.D. has its OWN validation attributes (NOT `System.ComponentModel.DataAnnotations`). They live in `src/Valid/Attributes/Attributes.cs`.

```csharp
// Numeric range constraint
[ValidProperty]
[Range(0, 500000, "Amount must be between 0 and 500,000")]
public partial double Amount { get; set; }

// Required string (non-null, non-whitespace)
[ValidProperty]
[Required("Account code is required")]
public partial string AccountCode { get; set; }

// String length constraint
[ValidProperty]
[StringLength(20, MinimumLength = 3, message: "Code must be 3-20 chars")]
public partial string Code { get; set; }
```

### Available Validation Attributes

| Attribute | Target | Parameters |
|---|---|---|
| `[Range(min, max, message?, code?)]` | Numeric properties | `double min`, `double max` |
| `[Required(message?, code?)]` | String properties | None required |
| `[StringLength(max, MinimumLength?, message?, code?)]` | String properties | `int maximumLength` |

### Rules

- Always use `using Valid;` — NOT `using System.ComponentModel.DataAnnotations;`.
- If converting from DataAnnotations, replace every `[System.ComponentModel.DataAnnotations.Range]` with `[Valid.Range]`.
- Multiple validation attributes can be stacked on the same property.

---

## Step 5: Nested ValidObjects (Parent-Child)

When a property type is itself a `[ValidObject]`, the generator automatically:
1. Subscribes to `Child_PropertyChanged` when the property is set.
2. Unsubscribes from the old value when replaced.
3. Propagates validation: if the child has errors, the parent's `_errorFlags` bit for that property flips to 1.

```csharp
[ValidObject]
public partial class InvoiceHeader
{
    [ValidProperty]
    public partial string InvoiceNumber { get; set; }

    [ValidProperty]
    public partial InvoiceLine Line { get; set; } // Auto-subscribes
}

[ValidObject]
public partial class InvoiceLine
{
    [ValidProperty]
    public partial double Amount { get; set; }
}
```

---

## Step 6: VAVID UI Integration (Blazor)

### Option A: Use V* UI Components (Recommended)

The standard `V*` library components (`VMudTextField`, `VDataGridCell`, etc.) are **VAVID-Aware**. They automatically resolve the `vavid-obj` and `vavid-bit` attributes from the model and property expressions.

```razor
<VDataGrid TItem="Employee">
    <VDataGridCell TItem="Employee" TValue="string" Property="x => x.Name" ColumnName="Name">
        <EditTemplate>
            <VMudTextField Model="@context.Item" Property="x => x.Name" />
        </EditTemplate>
    </VDataGridCell>
</VDataGrid>
```

### Option B: Manual HUD Integration (Custom Components)

If building custom UI, you MUST manually inject the `vavid` attributes to enable HUD inspection and surgical pulses.

```razor
<div vavid-obj="@myInvoice.GetHashCode().ToString()">
    <input vavid-bit="@myInvoice.GetBitIndex("InvoiceNumber")" value="@myInvoice.InvoiceNumber" />
</div>
```

### VAVID HTML Attributes Reference

| Attribute | Purpose | Example |
|---|---|---|
| `vavid-obj="id"` | Groups elements under a specific object | `vavid-obj="@obj.GetHashCode()"` |
| `vavid-bit="N"` | Links element to bit index N in the bitmask | `vavid-bit="0"` |
| `vavid-bool` | Renders boolean values as ✓/○ instead of true/false | `vavid-bool` |
| `vavid-format="currency"` | Formats numbers as currency ($1,234.56) | `vavid-format="currency"` |
| `data-valid-prop="Name"` | Marks input for AutoPilot fuzzing | `data-valid-prop="Amount"` |

### VAVID CSS Classes (Auto-Applied by JS Bridge)

| Class | Trigger | Visual |
|---|---|---|
| `vavid-dirty` | DirtyFlags bit is 1 | Gold glow |
| `vavid-busy` | BusyFlags bit is 1 | Cyan glow |
| `vavid-error` | ErrorFlags bit is 1 | Red glow + security toast |

---

## Step 7: Server-Side Shadow Validation

For zero-trust security, call `ValidSecurity.AssertShadowValidation()` on the server before saving:

```csharp
ValidSecurity.AssertShadowValidation(invoice);
// Throws SecurityException if client ErrorFlags were tampered with
```

---

## Step 8: Using Generated APIs

After compilation, every `[ValidObject]` class automatically has these methods:

```csharp
var invoice = new Invoice();

// Property access (reflection-free)
invoice.SetPropertyValue("Amount", 100.50);
object? val = invoice.GetPropertyValue("Amount");

// Bitmask queries
bool dirty = invoice.IsDirty;
bool valid = invoice.IsValid;
int bit = invoice.GetBitIndex("Amount"); // returns 0, 1, 2...

// JSON delta (only dirty properties)
string delta = invoice.GetDeltaJson(); // e.g. {"Amount": 100.50}

// JSON hydration (full object)
invoice.UpdatePropertyFromJson("Amount", "200.75");
invoice.UpdatePropertyFromJson("", fullJsonObject); // Root hydration

// Metadata for VAVID HUD
string schema = invoice.GetValidMetadata();

// Diagnostics
var diags = invoice.GetDiagnostics();

// State management
invoice.ResetDirty(); // After save
invoice.RestoreHistory(1); // Time-travel undo
```

---

## What Gets Auto-Generated Per Class

| Generated File | Contents |
|---|---|
| `{Class}.g.cs` | Full implementation: fields, properties, Hydrate, GetDeltaJson, SetPropertyValue, GetPropertyValue, GetValidMetadata, GetBitIndex, UpdatePropertyFromJson, CalculateValidationState, GetDiagnostics |
| `{Class}.g.tests.cs` | xUnit: `{Class}_Bitmask_Integrity_Verification` (if xUnit referenced) |
| `{Class}.g.fuzz.cs` | xUnit: `{Class}_Chaos_Monkey_Bitmask_Fuzzer` — 1000 random boundary mutations (if xUnit referenced) |
| `{Class}.g.bunit.cs` | bUnit: Headless UI render test (if bUnit referenced) |
| `{Class}.g.autopilot.cs` | `{Class}VavidAutoPilot.GetPlaywrightScript()` (always generated) |

---

## Common Pitfalls — MUST AVOID

| # | Pitfall | Consequence | Fix |
|---|---|---|---|
| 1 | Forgetting `partial` on the class | Generator cannot emit code; missing interface members | Add `partial` keyword |
| 2 | Forgetting `partial` on properties | CS9248: Partial property must have implementation | Add `partial` to every `[ValidProperty]` property |
| 3 | Using `System.ComponentModel.DataAnnotations` attributes | Generator ignores them; validation rules not applied | Use `Valid.Range`, `Valid.Required`, `Valid.StringLength` |
| 4 | Manually inheriting `ValidObjectBase` | CS0263: conflicting base class in generated partial | Remove manual inheritance |
| 5 | Manually implementing `INotifyPropertyChanged` | Duplicate event; double-fire | Remove manual implementation |
| 6 | Adding initializers to partial properties | CS8050: Cannot initialize partial properties | Remove initializers (generator handles defaults) |
| 7 | Adding property bodies | CS9248: Partial property must have implementation vs definition mismatch | Use auto-property syntax only: `{ get; set; }` |
| 8 | Non-partial fully-implemented `[ValidProperty]` | Generator skips it entirely; no bitmask tracking | Either make it `partial` or use `[ValidField]` on a backing field |
| 9 | Naming a property with a leading underscore | Field name collision in generated code | Use PascalCase for property names |
| 10 | Exceeding 128 properties | `UInt128` overflow; undefined behavior | Split into nested child objects |
| 11 | Passing .NET refs to JSON | `DataCloneError` in JS Bridge | Use Reference Hygiene (WeakMap) in `vavid.js` |
| 12 | Misaligned BitIndex in Metadata | HUD highlights wrong fields | Use VERSION 3.0.5 of `PropertyWeirGenerator` |

---

## Conversion Checklist (For Converting Existing Classes)

When converting from CSLA, MVVM, or plain C#:

- [ ] Add `using Valid;`
- [ ] Remove `using System.ComponentModel.DataAnnotations;` (if present, replace attributes)
- [ ] Add `[ValidObject]` to the class
- [ ] Add `partial` to the class declaration
- [ ] Remove any base class (`BusinessBase`, `BindableBase`, `ObservableObject`, `ModelBase`, etc.)
- [ ] Remove any manual `INotifyPropertyChanged` implementation
- [ ] Remove any manual dirty tracking fields/logic
- [ ] Convert each tracked property to `[ValidProperty] public partial T Name { get; set; }`
- [ ] Replace `[System.ComponentModel.DataAnnotations.Range]` → `[Valid.Range]`
- [ ] Replace `[System.ComponentModel.DataAnnotations.Required]` → `[Valid.Required]`
- [ ] Replace `[System.ComponentModel.DataAnnotations.StringLength]` → `[Valid.StringLength]`
- [ ] Remove any manual serialization/deserialization logic (generator provides `Hydrate` and `GetDeltaJson`)
- [ ] Remove any reflection-based property access (generator provides `SetPropertyValue`/`GetPropertyValue`)
- [ ] For Blazor UI: add `vavid-obj` and `vavid-bit` attributes to HTML elements, OR use `<VavidInput>` component
- [ ] Verify the project references `Valid`
- [ ] Build and confirm zero errors

## Demo Cashbook Reference App

For a complete reference application implementing this workflow, see the [Demo-Cashbook](file:///c:/Users/User/Documents/V.A.L.I.D/samples/Demo-Cashbook) project inside the `samples` folder.
- **Model Definition**: [Cashbook.cs](file:///c:/Users/User/Documents/V.A.L.I.D/samples/Demo-Cashbook/Models/Cashbook.cs) and [CashbookLine.cs](file:///c:/Users/User/Documents/V.A.L.I.D/samples/Demo-Cashbook/Models/CashbookLine.cs)
- **F# Active Validation Rules**: [Rules.fs](file:///c:/Users/User/Documents/V.A.L.I.D/samples/Demo-Cashbook/CashbookRules/Rules.fs)
- **Interactive Blazor UI**: [CashbookPage.razor](file:///c:/Users/User/Documents/V.A.L.I.D/samples/Demo-Cashbook/Pages/CashbookPage.razor)

---

## Auto-Generating Model Context Protocol (MCP) Tools

V.A.L.I.D. includes compile-time generator support for the **Model Context Protocol (MCP)**. This allows AI agents to inspect, fetch, and mutate objects directly with type safety.

### Setup
1. Reference the `ModelContextProtocol` package and the `Valid.Mcp` project.
2. The source generator detects the MCP reference and automatically generates a companion `{ClassName}McpTools` static class containing typed tool endpoints for every property of the `[ValidObject]`.
3. In your server setup (e.g. `Program.cs`), register the assembly containing the generated tools:
   ```csharp
   builder.Services
       .AddValidMcpServer(typeof(Cashbook).Assembly);
   ```

### Generated Tool Endpoints
For any class `SpaceShuttle`, the system automatically exposes:
- `valid_create_SpaceShuttle()`: Instantiates and registers a new tracking ID.
- `valid_get_SpaceShuttle_state(instanceId)`: Returns current properties and validation diagnostics.
- `valid_set_SpaceShuttle_ShuttleName(instanceId, string value)`: Type-safe mutation for strings.
- `valid_set_SpaceShuttle_Velocity(instanceId, double value)`: Type-safe mutation for doubles.

---

## Reference: Canonical Examples

### Minimal Object (3 properties)
```csharp
using Valid;

namespace MyApp;

[ValidObject]
public partial class SpaceShuttle
{
    [ValidProperty]
    public partial string ShuttleName { get; set; }

    [ValidProperty]
    [Range(0, 50000, "Velocity must be between 0 and 50,000 km/h")]
    public partial double Velocity { get; set; }

    [ValidProperty]
    public partial bool IsDocked { get; set; }
}
```

### Rich Object (6 properties with validation)
```csharp
using Valid;

namespace MyApp;

[ValidObject]
public partial class AxiomEntry
{
    [ValidProperty]
    public partial int LineId { get; set; }

    [ValidProperty]
    [Required]
    [StringLength(10, MinimumLength = 3)]
    public partial string AccountCode { get; set; }

    [ValidProperty]
    public partial string Description { get; set; }

    [ValidProperty]
    [Range(0, 500000)]
    public partial double Amount { get; set; }

    [ValidProperty]
    public partial bool IsProcessed { get; set; }

    [ValidProperty]
    [StringLength(20)]
    public partial string EntryType { get; set; }
}
```
