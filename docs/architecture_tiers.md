# V.A.L.I.D. Architectural Tiers

This document describes the structural tiers of the V.A.L.I.D. framework, their responsibilities, and how they interact.

## 1. Core Tier (`Valid.Core`)
The "Brain" of the framework. Zero dependencies on UI or specific databases.
- **Responsibilities**: 
    - `IValidObject` definition and 128-bit bitmask state management (`UInt128` Dirty/Busy/Error/State flags).
    - `ValidObjectBase` with `ValidId`, defensive `GetStateHistory()`, and SpinLock-safe bitmask operations.
    - `RuleEngine` for synchronous attribute-based validation.
    - `AsyncRuleEngine` for background/remote validation.
    - `CrdtMerger` for multi-master conflict resolution with `Tick()` vector clock helper.
    - `ValidSecurity` for thread-safe `AsyncLocal` role checks.
- **Implementation**: Pure C# logic, heavily optimized for performance with O(1) state checks via bitwise operations.

## 2. Infrastructure Tier (`Valid.Infrastructure.Components`)
The "Body" of the framework. Connects Core logic to MudBlazor/Web components.
- **Responsibilities**:
    - `ValidField`, `ValidForm`, and specialized inputs.
    - `VavidComponentBase<T>` with proper `DotNetObjectReference` lifecycle and `ValidId`-based JS bridge registration.
    - `VavidSurgicalComponentBase` for direct memory slab bypass rendering.
    - Handling `INotifyPropertyChanged` to drive UI refreshes.
    - Triggering `StateHasChanged` on surgical property updates.
    - Binding to `ErrorFlags` for real-time error reporting.
- **Implementation**: Razor Components and shared base classes.

## 3. Transport & Persistence Tier (`Valid.Queue`, `Valid.Generator`)
The "Nerves" of the framework. Moves data between memory and storage.
- **Responsibilities**:
    - `PropertyWeirGenerator`: Source code generation for boilerplate-free property management.
    - `SqliteOutbox`: Local-first persistence with **SHA-256 hash chaining** and **AES-GCM authenticated encryption**.
    - `DequeueAsync<T>()`: Batch processing of outbox entries with `Processed` flag tracking.
    - `IDataShuttle`: Clean Fetch/Save/Delete contracts between Client and Server.
- **Implementation**: Source Generators (Roslyn) and SQLite-backed outbox queue with cryptographic integrity.

## 4. Extension Tier (V.A.V.I.D. HUD)
The "Eyes" of the framework. Visualized diagnostics overlay.
- **Responsibilities**:
    - Real-time bitmask synchronization from memory to DOM.
    - Color-coded glow effects (Red=Error, Blue=Busy, Yellow=Dirty).
    - HTML entity escaping (`esc()`) for all interpolated values to prevent XSS.
    - Controlled polling via `startPolling()`/`stopPolling()` lifecycle functions.
    - `postMessage` restricted to `window.location.origin` for cross-origin security.
- **Implementation**: Chrome/Edge browser extension using `vavidBridge.js`.

## 5. AI Integration Tier (`Valid.Mcp`)
The "Voice" of the framework. Model Context Protocol integration for AI agents.
- **Responsibilities**:
    - `ValidObjectRegistry`: Bounded registry (10,000 cap) with LRU eviction to prevent memory exhaustion.
    - MCP tools for AI agents to inspect dirty/busy/error bitmasks and mutate live objects.
    - `ValidId`-based object identification for stable agent references.
- **Implementation**: MCP server with tool definitions for object inspection and mutation.

## 6. Metadata & Bitmask Engine (The "Nervous System")
The core performance and UX secret of V.A.L.I.D. is the surgery-style state tracking.
- **Bitmask Synchronization**: Every `ValidObject` maintains `UInt128 _dirtyFlags`, `_busyFlags`, `_errorFlags`, and `_stateFlags`. Each property is assigned a bit index (0–127). Setting a property flips its bit.
- **Identity**: Each object has a unique `ValidId` (e.g., `valid_1`) assigned via `Interlocked.Increment` — stable, collision-free, and suitable for JS bridge registration and MCP tracking.
- **Agentic Context Injection**: The `McpBridge` encodes these status bits into JSON that the LLM/Agent can read directly, allowing it to "see" what is dirty, busy, or invalid without querying the UI.
- **Zero-Allocation Tracking**: By using bitwise operations on `UInt128` instead of collections of "DirtyFields", V.A.L.I.D. achieves near-zero GC pressure during rapid data entry.
- **SpinLock Safety**: Bitmask updates use `SpinLock` for thread safety. The lock field is intentionally NOT readonly (marking a mutable struct as readonly causes the runtime to operate on a defensive copy, silently breaking the lock).

---

## Interaction Flow
1. **User Input** → `ValidField` sets property on `ValidObject`.
2. **Core** → Triggers `UInt128` bitmask update and O(1) counter refresh. Runs `RuleEngine`.
3. **Internal** → `INotifyPropertyChanged` fires.
4. **UI** → `VavidComponentBase` calls `StateHasChanged`.
5. **Extension** → `vavidBridge` reads the state bitmask and applies CSS glows to DOM elements.
6. **Persistence** → `DataShuttle` pushes delta to `SqliteOutbox` (SHA-256 chained, AES-GCM encrypted).
