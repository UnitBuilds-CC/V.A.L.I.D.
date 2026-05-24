# Core Concepts

## IValidObject

Every V.A.L.I.D. business object implements `IValidObject`. This is the single interface that gives your object reactive state tracking, validation, and undo:

```csharp
public interface IValidObject : INotifyPropertyChanged
{
    bool IsDirty { get; }              // Has any property changed since last save?
    bool IsBusy { get; }               // Is an async operation running?
    bool IsInvalid { get; }            // Are there validation errors?
    DiagnosticResult Diagnostics { get; }  // All validation messages

    void ResetDirtyFlags(bool cascade = true);
    void BeginEdit();                  // Snapshot state for undo
    void CancelEdit();                // Restore snapshot

    IParent? Parent { get; set; }     // Parent-child graph wiring
    void SetBusy(string prop, bool isBusy);
    void SetError(string prop, bool hasError);

    // Reflection-based property access
    T GetPropertyValue<T>(string propertyName);
    void SetPropertyValue<T>(string propertyName, T value);
    bool IsPropertyDirty(string propertyName);
}
```

## Dirty Tracking

Track changes per-property with a simple dictionary:

```csharp
private Dictionary<string, bool> _propertyDirtyFlags = new();
private bool _isDirty;

public bool IsDirty => _isDirty || Lines.IsDirty;

protected void OnPropertyChanged(string name)
{
    _isDirty = true;
    _propertyDirtyFlags[name] = true;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    Parent?.OnChildChanged(this);
}
```

## ValidList\<T\>

A reactive observable collection for child objects. Tracks additions, removals, and deletions:

```csharp
public class AxiomBatch : IValidObject
{
    public ValidList<AxiomBatchLine> Lines { get; } = new();

    // IsDirty cascades: batch is dirty if any line is dirty
    public bool IsDirty => _isDirty || Lines.IsDirty;
}
```

`ValidList<T>` automatically:
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
