# V.A.L.I.D. Samples

Interactive demonstrations of the V.A.L.I.D. framework capabilities.

## Quick Start

```bash
cd samples/Valid.Samples
dotnet run
```

## What You'll See

### Sample 1: Basic Dirty Tracking
- How property changes are automatically tracked in `DirtyFlags`
- Using `GetDeltaJson()` to get only changed properties
- Calling `ResetDirty()` after save/sync

### Sample 2: Validation Rules
- `[Required]`, `[Range]`, `[StringLength]` attributes
- Checking `IsValid` and `ErrorFlags`
- Reading diagnostics via `GetDiagnostics()`

### Sample 3: Time-Travel Debugging
- State history (circular buffer, 16 snapshots)
- Using `RestoreHistory()` to undo changes
- Inspecting historical states

### Sample 4: JSON Serialization
- Delta sync with `GetDeltaJson()`
- Restoring state with `UpdatePropertyFromJson()`
- Efficient partial updates

### Sample 5: Metadata Introspection
- Reading object schema via `GetValidMetadata()`
- Property names, types, bit indexes, validation rules

### Sample 6: Field Proxy
- Using `[ValidField]` to eliminate boilerplate
- Auto-generated properties from private fields

### Sample 7: BitPulse Events
- Subscribing to `BitPulse` for surgical UI updates
- Understanding mask types (Dirty, Error, Busy, State)

## Models

- **Customer** — Basic object with validation rules
- **Product** — Uses `[ValidField]` for auto-generated properties
- **Order** — Explicit `BitIndex` assignment for API stability

## Next Steps

- Explore the MCP tools (`valid_list_instances`, `valid_inspect_state`, etc.)
- Try the Blazor components (`VavidComponentBase`, `VavidSurgicalComponentBase`)
- Read API docs via `valid_get_api_docs()` MCP tool
- Check the [main README](../../README.md) for full documentation
