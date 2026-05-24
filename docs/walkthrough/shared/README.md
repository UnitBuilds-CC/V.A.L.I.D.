# Shared Layer

The shared layer contains everything that is framework-agnostic — the core interfaces, base classes, attributes, source generator, and supporting infrastructure. Any .NET project (console, Blazor, API) consumes this layer.

---

## Valid (Core Library)

The foundational NuGet package. Contains no UI code.

### Attributes (`Valid/Attributes/Attributes.cs`)

Defines the declarative attribute system. This is the **only file a developer touches** to define validation rules.

| Attribute | Purpose |
|---|---|
| `[ValidObject]` | Marks a class for source generation. The generator emits a partial class with bitmask infrastructure. |
| `[ValidProperty]` | Marks a property to be included in the bitmask engine. Each property gets a unique bit index (0-127). |
| `[Range(min, max)]` | Numeric boundary validation. Compiled into both `Validate()` and `GetDiagnostics()`. |
| `[Required]` | Marks a string property as non-null/non-whitespace. |
| `[StringLength(max, MinimumLength = min)]` | Constrains string length. |

All validation attributes inherit from `ValidationAttribute(message, code)`.

### Core Interfaces and Base Classes (`Valid/Core/`)

| File | Purpose |
|---|---|
| `IValidObject.cs` | **The contract.** Defines `DirtyFlags`, `BusyFlags`, `ErrorFlags`, `StateFlags` (all `UInt128`), plus `GetDiagnostics()`, `GetValidMetadata()`, `SetPropertyValue()`, `GetDeltaJson()`, `GetPropertyType()`, `GetBitIndex()`. Also defines `DiagnosticResult` record struct. |
| `ValidObjectBase.cs` | **Abstract base class.** Stores the four `UInt128` bitmask fields. Implements `SetProperty<T>()` which flips the dirty bit on mutation. Implements `INotifyPropertyChanged`. |
| `VavidComponentBase.cs` | **Blazor component base.** Manages object lifecycle: hooks `PropertyChanged`, registers with JS bridge, forwards bitmask snapshots via `vavid.updateState()`, cascades the model via `CascadingValue<IValidObject>`. Contains the generic `VavidComponent<T>` subclass for zero-boilerplate component binding. |
| `VectorClock.cs` | Logical clock record struct for CRDT causal ordering. Fields: `NodeId`, `Version`. |
| `CrdtMerger.cs` | Last-Write-Wins field-level merge logic using `VectorClock`. Also provides `MergeMask()` for bitmask OR-merging. |
| `WebWorkerBridge.cs` | Serialization bridge for WebWorker communication. Defines `MutationCommand` record struct with `UInt128` bitmask fields. |
| `ValidList.cs` | Thin wrapper over `UnmanagedSlab<T>` for GC-invisible collections. |
| `UnmanagedSlab.cs` | Unsafe unmanaged memory allocator using `Marshal.AllocHGlobal`. Thread-safe via lock. Bounds-checked indexer. Implements `IDisposable` with finalizer. |

### Queue (`Valid/Queue/`)

| File | Purpose |
|---|---|
| `SqliteOutbox.cs` | Hash-chained persistent outbox for offline-first sync. Each entry stores `Payload`, `Hash` (SHA-256), and `PrevHash` for tamper detection. Uses `SemaphoreSlim` for thread safety. |

### JavaScript Runtime (`Valid/wwwroot/js/vavid.js`)

The 246-line micro-runtime injected into Blazor. This is the **bridge between .NET bitmasks and the DOM**.

**Key Methods:**

| Method | Purpose |
|---|---|
| `registerObject(id, metadataJson, dotNetRef)` | Stores object metadata + .NET ref. Pre-caches DOM elements. |
| `updateState(id, dirtyHex, busyHex, errorHex)` | Parses hex strings into BigInt. Applies CSS classes (`vavid-dirty`, `vavid-error`) to cached DOM elements by matching `[vavid-bit]` attributes. Fires security toasts on error bits. |
| `logDelta(id, deltaJson)` | Receives JSON delta, looks up property→BitIndex via metadata, and surgically updates DOM element content by querying `[vavid-bit="N"]`. |
| `pushValue(id, propName, jsonValue)` | Bi-directional: calls `.NET` via `dotNetRef.invokeMethodAsync('UpdateProperty', ...)`. |

---

## Valid.Generator (Roslyn Source Generator)

A single file: `PropertyWeirGenerator.cs` (~395 lines).

**What it generates for each `[ValidObject]` class:**

| Generated Member | Description |
|---|---|
| Backing field + partial property | `private string _shuttlename;` with getter/setter that calls `SetProperty(ref field, value, bitIndex)` |
| `Child_PropertyChanged` handler | Only emitted if nested `[ValidObject]` properties exist. Auto-subscribes/unsubscribes on set. |
| `GetDiagnostics()` | Returns `List<DiagnosticResult>` by checking each validation attribute. Recursively includes nested object diagnostics. |
| `Validate()` | Computes `UInt128 newErrors` by evaluating all validation rules. Propagates nested `ErrorFlags`. |
| `Hydrate(ref Utf8JsonReader)` | Switch-based JSON deserialization. Uses `JsonSerializer.Deserialize<T>()` for nested objects. |
| `GetDeltaJson()` | Emits only dirty properties as JSON. Resets `_dirtyFlags` after serialization. |
| `SetPropertyValue(string, object)` | Reflection-free property setter by name. |
| `GetValidMetadata()` | Returns JSON schema with property names, types, bit indices, and validation rules. |
| `GetPropertyType(string)` | Returns `System.Type` by property name. |
| `GetBitIndex(string)` | Returns integer bit index by property name. |
| `{ClassName}Tests` | Auto-generated xUnit tests verifying bitmask integrity for every property. |

**Key internal helper:**
- `IsValidObject(ITypeSymbol)` — detects if a property type is itself a `[ValidObject]` for nested DTO support.

---

## Valid.FSharp (F# Mutation Engine)

Provides an immutable, functional alternative to C# mutation. Used by `ComparisonGrid.razor` as a selectable "F# Engine".

| File | Purpose |
|---|---|
| `AxiomCore.fs` | Defines `AxiomEntryRecord` (F# record matching C# `AxiomEntry`). `mutateEntry` applies random mutations. `getDeltaJson` computes field-level diffs. |
| `Library.fs` | Module stub. |

---

## Valid.Testing (Test Infrastructure)

| File | Purpose |
|---|---|
| `ValidTestHelpers.cs` | `VerifySymmetry<T>()` — asserts that `DirtyFlags` and `ErrorFlags` survive serialization roundtrips. Used by source-generated tests. |
