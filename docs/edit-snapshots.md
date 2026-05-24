# Edit Snapshots (BeginEdit / CancelEdit)

V.A.L.I.D. provides a clean, reflection-based undo mechanism — the explicit alternative to CSLA's complex n-level `UndoableBase`.

## Concept

```
BeginEdit()    → Snapshot all [ValidProperty] values
User edits...  → Properties change, IsDirty = true
CancelEdit()   → Restore snapshot, IsDirty = false
```

One level, explicit, zero hidden serialization.

## Usage

### In Business Objects

Implement `BeginEdit()` and `CancelEdit()` using reflection over `[ValidProperty]`:

```csharp
public class AxiomBatch : IValidObject
{
    private Dictionary<string, object?>? _editSnapshot;

    [ValidProperty] public string Description { get; set; } = "";
    [ValidProperty] public decimal Amount { get; set; }
    public ValidList<AxiomBatchLine> Lines { get; } = new();

    public void BeginEdit()
    {
        // Snapshot all [ValidProperty] values
        _editSnapshot = GetType()
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ValidPropertyAttribute>() != null)
            .ToDictionary(p => p.Name, p => p.GetValue(this));

        // Cascade to children
        Lines.BeginEdit();
    }

    public void CancelEdit()
    {
        if (_editSnapshot == null) return;

        // Restore all values from snapshot
        foreach (var kvp in _editSnapshot)
        {
            GetType().GetProperty(kvp.Key)?.SetValue(this, kvp.Value);
        }
        _editSnapshot = null;

        // Clear dirty state
        _isDirty = false;
        _propertyDirtyFlags.Clear();

        // Cascade to children
        Lines.CancelEdit();
    }
}
```

### In ValidList\<T\>

`ValidList<T>` snapshots list membership (items added/removed) and cascades to children:

```csharp
// BeginEdit() snapshots the current items, cascades to each child
// CancelEdit() restores original items, cascades cancel to each child
```

This means if a user adds or removes lines during editing, `CancelEdit()` restores the original line collection.

### In the UI

```csharp
// After fetching or creating — take a snapshot
protected override async Task OnInitializedAsync()
{
    _batch = await Shuttle.FetchAsync(batchId);
    _batch.BeginEdit();  // ← snapshot taken here
}

// Discard flow
private async Task HandleClose()
{
    if (_batch.IsDirty)
    {
        var discard = await ShowConfirmDialog("Discard changes?");
        if (discard)
        {
            _batch.CancelEdit();  // ← revert everything
        }
    }
}

// Navigation guard
private void OnBeforeInternalNavigation(LocationChangingContext ctx)
{
    if (_batch.IsDirty)
    {
        _batch.CancelEdit();  // ← revert before navigating away
    }
}
```

## What Gets Snapshotted

| Item | Snapshotted? | How |
|---|---|---|
| `[ValidProperty]` scalar values | ✅ | Reflection dictionary |
| Child object properties | ✅ | Each child's `BeginEdit()` |
| List membership (add/remove) | ✅ | `ValidList._snapshot` |
| Non-`[ValidProperty]` fields | ❌ | Intentionally excluded |
| Computed properties | ❌ | Recalculated from source values |

## CSLA Comparison

| Aspect | CSLA `UndoableBase` | V.A.L.I.D. Edit Snapshots |
|---|---|---|
| Mechanism | Binary serialization stack | Reflection dictionary |
| Levels | N-level (push/pop stack) | Single snapshot |
| Annotations | Must mark `[NotUndoable]` on every transient field | Zero annotations — only `[ValidProperty]` participates |
| Lines of code | ~200 per BO | ~15 per BO |
| Hidden behavior | Serializes entire object graph on each `BeginEdit()` | Only snapshots decorated properties |
| Performance | O(n) serialization per level | O(1) dictionary copy |
