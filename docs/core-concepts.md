# Core Concepts

## Naming Conventions

V.A.L.I.D. uses a strict naming convention to distinguish framework layers:

| Prefix | Scope | Examples |
|---|---|---|
| `Valid` | Core engine types | `ValidObjectBase`, `ValidProperty`, `ValidList`, `ValidSlab` |
| `Vavid` | Blazor UI component types | `VavidComponentBase`, `VavidSurgicalComponentBase` |
| `VAVID` (all caps) | Chrome extension / HUD branding | `VAVID Bridge`, `VAVID HUD` |
| `vavid` (lowercase) | JavaScript bridge global | `window.vavid`, `vavid-obj`, `vavid-bit` |

## IValidObject

Every V.A.L.I.D. business object implements `IValidObject`. This is the single interface that gives your object reactive 128-bit bitmask state tracking, validation, and undo:

```csharp
public interface IValidObject : INotifyPropertyChanged
{
    // Deterministic runtime identity (e.g., "valid_1", "valid_2")
    string ValidId { get; }

    // 128-bit bitmask state (replaces old bool flags)
    UInt128 DirtyFlags { get; }       // Properties changed since last sync
    UInt128 BusyFlags { get; }        // Properties in async operations
    UInt128 ErrorFlags { get; }       // Properties with validation errors
    UInt128 StateFlags { get; }       // Object lifecycle state

    // Convenience derived properties
    bool IsDirty { get; }             // DirtyFlags != 0
    bool IsValid { get; }             // ErrorFlags == 0
    bool IsNew { get; }

    void ResetDirtyFlags(bool cascade = true);
    void BeginEdit();                 // Snapshot state for undo
    void CancelEdit();                // Restore snapshot

    IParent? Parent { get; set; }     // Parent-child graph wiring
    void SetBusy(string prop, bool isBusy);
    void SetError(string prop, bool hasError);

    // Reflection-based property access
    T GetPropertyValue<T>(string propertyName);
    void SetPropertyValue<T>(string propertyName, T value);
    bool IsPropertyDirty(string propertyName);

    // Validation
    void Validate();
    UInt128 CalculateValidationState();
    IEnumerable<DiagnosticResult> GetDiagnostics();

    // State history (returns defensive copy)
    string[] GetStateHistory();
}
```

> **Note on `ValidId`**: Every `ValidObjectBase` subclass receives a unique, auto-incremented identifier
> (e.g., `valid_1`, `valid_42`). This replaces the previous pattern of using `GetHashCode().ToString()`
> for JS bridge registration and MCP tracking. `ValidId` is stable, unique, and not affected by
> hash code collisions or override semantics.

## Dirty Tracking

Track changes per-property with a **128-bit bitmask**. Each property is assigned a bit index (0–127).
Setting a property flips its bit in `_dirtyFlags`:

```csharp
protected System.UInt128 _dirtyFlags;

public bool IsDirty => _dirtyFlags != System.UInt128.Zero;

protected void OnPropertyChanged(string name, [CallerMemberName] string? propName = null)
{
    int bit = GetBitIndex(propName!);
    _dirtyFlags |= (System.UInt128)1 << bit;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    Parent?.OnChildChanged(this);
}
```

Checking whether any property is dirty is an O(1) comparison — no iteration, no allocation.
Checking whether a *specific* property is dirty is a single bitwise AND.

## ValidSlab\<T\> and ValidList\<T\>

`ValidSlab<T>` is the correctly-named reactive collection for child objects, backed by the
unmanaged slab allocator. It tracks additions, removals, and deletions:

```csharp
public class AxiomBatch : IValidObject
{
    public ValidSlab<AxiomBatchLine> Lines { get; } = new();

    // IsDirty cascades: batch is dirty if any line is dirty
    public bool IsDirty => _dirtyFlags != System.UInt128.Zero || Lines.IsDirty;
}
```

> **Note**: `ValidList<T>` has been marked `[Obsolete]` in favor of `ValidSlab<T>`.
> The name "ValidList" was misleading — it implied a simple list, but the type is
> actually a slab-allocated, bitmask-tracked collection. New code should use `ValidSlab<T>`.

`ValidSlab<T>` automatically:
- Sets `Parent` on added items
- Tracks removed items in `DeletedItems` for DAL delete operations
- Fires `CollectionChanged` and `PropertyChanged` on mutations
- Cascades `BeginEdit()`/`CancelEdit()` to all children

## DiagnosticResult

Structured error reporting with property-level granularity:

```csharp
var result = RuleEngine.Validate(invoice);

// Check overall validity
if (!result.Success) { /* has errors */ }

// Iterate diagnostics
foreach (var diag in result.Diagnostics)
{
    Log.Information("[V.A.L.I.D.] {Severity} on {PropertyName}: {Message}", diag.Severity, diag.PropertyName, diag.Message);
    // Optional: diag.Code, diag.FixSuggestion
}
```

Severities: `Information`, `Warning`, `Error`.

## ValidFactory

Creates new objects and hydrates from DTOs:

```csharp
// Create empty object (all defaults, IsDirty = false)
var batch = ValidFactory.Create<AxiomBatch>();

// Hydrate from DTO (reflection-based property matching)
var batch = ValidFactory.Create<AxiomBatch>(dto);
```

The DTO hydration matches properties by name and type — no manual mapping needed for the fetch path.

## Parent-Child Graph

V.A.L.I.D. objects form a reactive graph via `IParent`:

```csharp
public interface IParent
{
    void OnChildChanged(IValidObject child);  // Child notifies parent of changes
    void ReportBusy(int delta);               // Busy state bubbles up
}
```

When a line changes, the batch knows. When a batch is busy, the editor knows. No manual wiring.

## ValidSecurity

Role-based authorization without CSLA's `AuthorizationRules`:

```csharp
// Set the role resolver (once, at app startup)
ValidSecurity.IsInRole = role => currentUser.Roles.Contains(role);

// Use on properties
[Authorized("Admin")]
public decimal CreditLimit { get; set; }
```
