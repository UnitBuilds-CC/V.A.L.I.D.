# V.A.L.I.D. Architectural Tiers

This document describes the structural tiers of the V.A.L.I.D. framework, their responsibilities, and how they interact.

## 1. Core Tier (`Valid.Core`)
The "Brain" of the framework. Zero dependencies on UI or specific databases.
- **Responsibilities**: 
    - `IValidObject` definition and state management.
    - `RuleEngine` for synchronous attribute-based validation.
    - `AsyncRuleEngine` for background/remote validation.
    - `CrdtMerger` for multi-master conflict resolution.
    - `ValidSecurity` for thread-safe `AsyncLocal` role checks.
- **Implementation**: Pure C# logic, heavily optimized for performance with O(1) state counters.

## 2. Infrastructure Tier (`Valid.Infrastructure.Components`)
The "Body" of the framework. Connects Core logic to MudBlazor/Web components.
- **Responsibilities**:
    - `ValidField`, `ValidForm`, and specialized inputs.
    - Handling `INotifyPropertyChanged` to drive UI refreshes.
    - Triggering `StateHasChanged` on surgical property updates.
    - Binding to `Diagnostics` for real-time error reporting.
- **Implementation**: Razor Components and shared base classes like `ValidInputBase`.

## 3. Transport & Persistence Tier (`Valid.Queue`, `Valid.Generator`)
The "Nerves" of the framework. Moves data between memory and storage.
- **Responsibilities**:
    - `PropertyWeirGenerator`: Source code generation for boilerplate-free property management.
    - `SqliteOutboxStore`: Local-first persistence with atomic `INSERT OR REPLACE`.
    - `IDataShuttle`: Clean Fetch/Save/Delete contracts between Client and Server.
- **Implementation**: Source Generators (Roslyn) and SQLite-backed outbox queue.

## 4. Extension Tier (V.A.V.I.D. HUD)
The "Eyes" of the framework. Visualized diagnostics overlay.
- **Responsibilities**:
    - Real-time bitmask synchronization from memory to DOM.
    - Color-coded glow effects (Red=Error, Blue=Busy, Yellow=Dirty).
    - Chaos Engine for automated pathfinding and vulnerability testing.
- **Implementation**: Chrome/Edge browser extension using `vavidBridge.js`.

## 5. Metadata & Bitmask Engine (The "Nervous System")
The core performance and UX secret of V.A.L.I.D. is the surgery-style state tracking.
- **Bitmask Synchronization**: Every `ValidObject` maintains a `ulong _dirtyFlags`. Each property is assigned a bit (0-63). Setting a property flips its bit.
- **Agentic Context Injection**: The `McpBridge` encodes these status bits into JSON that the LLM/Agent can read directly, allowing it to "see" what is dirty, busy, or invalid without querying the UI.
- **Zero-Allocation Tracking**: By using bitwise operations instead of collections of "DirtyFields", V.A.L.I.D. achieves near-zero GC pressure during rapid data entry.

---

## Interaction Flow
1. **User Input** → `ValidField` sets property on `ValidObject`.
2. **Core** → Triggers bitmask update and O(1) counter refresh. Runs `RuleEngine`.
3. **Internal** → `INotifyPropertyChanged` fires.
4. **UI** → `ValidInputBase` calls `StateHasChanged`.
5. **Extension** → `vavidBridge` reads the state bitmask and applies CSS glows to DOM elements.
6. **Persistence** → `DataShuttle` pushes delta to `SqliteOutbox`.
