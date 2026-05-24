# V.A.L.I.D. 3.0 Implementation Guide

Welcome to the **Virtualized Asynchronous Layer for Integrated Data (V.A.L.I.D.) 3.0** implementation guide. This document explains how to set up, define, validate, and build applications using the V.A.L.I.D. high-performance framework.

---

## 🏛️ Core Architecture Overview

V.A.L.I.D. 3.0 is a zero-allocation, ultra-high-performance business object framework designed for modern web applications running on WebAssembly. It operates on three key layers:

1. **Unmanaged memory state slab**: A contiguous memory block allocated outside the GC heap. Each object instance occupies 4 slots (64 bytes) for state tracking (Dirty, Busy, and Error flags, plus numeric values).
2. **128-Bit Quad-Mask tracking**: Instead of dirty-tracking through heavy events or reflection, V.A.L.I.D. utilizes standard C# `System.UInt128` bitmasks. This enables $O(1)$ performance and reduces tracking overhead to zero garbage collection.
3. **JS Surgical Bypass**: Directly reads and writes memory offsets in the WebAssembly shared linear memory heap (`HEAPU8`), bypassing Blazor's virtual DOM (VDOM) diffing and `StateHasChanged` cycles for massive speed gains.
4. **F# Validation & CRDT Engine**: Pure functional rule evaluation and Observed-Remove CRDT sets for offline conflict resolution.

---

## ⚙️ Step 1: Framework Installation & Setup

To integrate V.A.L.I.D. into a project, add references to the generated NuGet packages in your Blazor WebAssembly app:

### Project References
Add the following projects or NuGet references to your main project file:

```xml
<ItemGroup>
  <ProjectReference Include="..\V.A.L.I.D\src\Valid\Valid.csproj" />
  <ProjectReference Include="..\V.A.L.I.D\src\Valid.FSharp\Valid.FSharp.fsproj" />
  <ProjectReference Include="..\V.A.L.I.D\src\Valid.Generator\Valid.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

### JS Bypass Connection
Add the bypass script to your `index.html` file after the Blazor WebAssembly framework script:

```html
<script src="_framework/blazor.webassembly.js"></script>
<script src="js/vavid-bypass.js"></script>
```

### Slab Initialization
Initialize the unmanaged memory slab in your `Program.cs` before running the WebAssembly host:

```csharp
// Initialize Unmanaged Slab with 10,000 slots
Valid.WebWorkerBridge.Initialize(10000);
```

---

## 📝 Step 2: Defining the Business Object (C#)

Use the `[ValidObject]` and `[ValidProperty]` attributes. The Source Generator will automatically create backing fields, 128-bit property mapping, and change notification code.

```csharp
using System;
using Valid;

namespace DemoCashbook.Models;

[ValidObject]
public partial class CashbookLine : ValidObjectBase
{
    [ValidProperty]
    public partial int Id { get; set; }

    [ValidProperty]
    public partial string Description { get; set; }

    [ValidProperty]
    public partial double Deposit { get; set; }

    [ValidProperty]
    public partial double Payment { get; set; }

    public CashbookLine()
    {
        Description = string.Empty;
    }

    public override System.UInt128 CalculateValidationState()
    {
        System.UInt128 newErrors = 0;
        // Business validation rules (e.g. check for empty description or negative values)
        if (string.IsNullOrWhiteSpace(Description))
        {
            int bit = GetBitIndex(nameof(Description));
            newErrors |= ((System.UInt128)1 << bit);
        }
        return newErrors;
    }

    public override IEnumerable<DiagnosticResult> GetDiagnostics()
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            yield return new DiagnosticResult(nameof(Description), "Description is required", "VAL-REQ", null);
        }
    }
}
```

---

## 🎛️ Step 3: Writing F# Validation Rules

For advanced business validations and offline convergence, write F# rules targeting a structured record representation.

```fsharp
namespace CashbookRules

open System

[<Struct>]
type CashbookLineRecord = {
    Id: int
    Description: string
    Deposit: double
    Payment: double
}

module Rules =
    [<Struct>]
    type ValidationResult = {
        Property: string
        Message: string
        Code: string
    }

    // Evaluates rules for Cashbook Line
    let evaluateLineRules (line: CashbookLineRecord) =
        [
            if String.IsNullOrWhiteSpace(line.Description) then
                yield { Property = "Description"; Message = "Description is required"; Code = "VAL-REQ" }
            if line.Deposit < 0.0 then
                yield { Property = "Deposit"; Message = "Deposit cannot be negative"; Code = "VAL-NEG" }
        ]
```

---

## 🖥️ Step 4: Blazor Row component Integration

Build components that bind to the memory slab using the `vavid-obj`, `vavid-bit`, and `data-vavid-offset` attributes.

```razor
@inherits VavidComponent<CashbookLine>
@using DemoCashbook.Models
@using Valid

<tr class="cashbook-row"
    data-vavid-slab-index="@Value.SlabIndex"
    vavid-obj="@Value.GetHashCode().ToString()">
    
    <td>
        <input type="text" 
               class="hud-input" 
               vavid-obj="@Value.GetHashCode().ToString()"
               vavid-bit="@Value.GetBitIndex(nameof(CashbookLine.Description))"
               @bind="Value.Description" 
               @bind:event="oninput" />
    </td>

    <td>
        <input type="number" 
               class="hud-input" 
               data-vavid-offset="48"
               vavid-obj="@Value.GetHashCode().ToString()"
               vavid-bit="@Value.GetBitIndex(nameof(CashbookLine.Deposit))"
               @bind="Value.Deposit" 
               @bind:event="oninput" />
    </td>
</tr>
```

---

## 📊 Step 5: parent Page Integration

Configure the main page to register the state slab address with the JS bypass script.

```razor
@page "/"
@inherits VavidComponentBase
@using Valid
@using DemoCashbook.Models

<table class="virtual-table">
    <thead>
        <tr>
            <th>Description</th>
            <th>Deposit</th>
        </tr>
    </thead>
    <tbody>
        <Virtualize Items="@_lines" Context="line">
            <CashbookLineRow Value="@line" />
        </Virtualize>
    </tbody>
</table>

@code {
    private List<CashbookLine> _lines = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            var ptr = WebWorkerBridge.GetStatePointer();
            var len = WebWorkerBridge.GetSlabLength();

            // Notify JS bypass about the memory pointer
            await JSRuntime.InvokeVoidAsync("__VAVID_INITIALIZE_INDUSTRIAL_BYPASS__", (long)ptr, len);
        }
    }
}
```
