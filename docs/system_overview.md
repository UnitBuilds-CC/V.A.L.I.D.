# V.A.L.I.D. System Overview

**Vectorized Asynchronous Logic & Intelligent Diagnostics — Version 3.0.3**

V.A.L.I.D. 3.0 is a next-generation business logic framework for .NET 8 applications that replaces traditional, heavy object tracking systems with a high-performance, bitmask-driven architecture using `System.UInt128` state masks.

---

## Core Architecture

The system is divided into three primary domains:

### 1. V.A.L.I.D. (.NET Framework)
Located in `/src`, this is the backend logic engine. It uses **Roslyn Source Generators** to emit surgical, reflection-free code at compile time.
- **`Valid`**: The main library providing `ValidObjectBase`, `ValidId` identity, 128-bit bitmask manipulation, and Blazor component bases.
- **`Valid.Generator`**: The "Property Weir" that generates backing fields, property implementations, bitmask tracking, and JSON hydration logic.
- **`Valid.FSharp`**: High-resilience mathematical models for CRDT (Conflict-free Replicated Data Types) state merging with `Tick()` vector clock helper.
- **`Valid.Mcp`**: Model Context Protocol integration with a bounded registry (10K cap, LRU eviction), allowing AI agents to inspect and mutate live business objects in real-time.

### 2. V.A.V.I.D. (Visual Diagnostics HUD)
Enabled by `/src/Valid.Extension`, this provides a browser-based diagnostic layer that links directly to the .NET state.
- **JS Bridge**: Synchronizes the state between the .NET WASM host and the DOM using `ValidId`-based object registration.
- **Surgical Pulse**: When a field is changed in .NET, only that specific property "pulses" visually in the browser.
- **Rule Inspector**: Allows developers to hover over any UI field to see exactly which validation rules are failing or passing.
- **XSS Protection**: All interpolated HUD values are HTML-entity-escaped via `esc()`.
- **Controlled Polling**: `startPolling()`/`stopPolling()` functions for precise lifecycle control.
- **Origin Security**: `postMessage` restricted to `window.location.origin`.

### 3. Offline-First Persistence (`SqliteOutbox`)
The outbox provides tamper-resistant, authenticated persistence for offline-first data synchronization.
- **SHA-256 Hash Chaining**: Every entry is cryptographically linked to its predecessor, ensuring causal ordering and tamper detection.
- **AES-GCM Authenticated Encryption**: Payloads are encrypted with AES-256-GCM (nonce + authentication tag + ciphertext), providing both confidentiality and integrity.
- **Batch Dequeue**: `DequeueAsync<T>()` processes unprocessed entries with `Processed` flag tracking.

---

## Key Capabilities

### Vectorized State Tracking
Instead of dictionaries or lists of dirty property names, V.A.L.I.D. uses **`System.UInt128` bitmasks** to track object state across four orthogonal dimensions (Dirty, Busy, Error, State). This allows for O(1) state checks and extremely small payloads with up to 128 properties per mask.

### Deterministic Identity (`ValidId`)
Every `ValidObjectBase` subclass receives a unique, auto-incremented identifier (e.g., `valid_1`, `valid_42`) via `Interlocked.Increment`. This replaces the previous pattern of using `GetHashCode().ToString()` and provides:
- Stable identity unaffected by hash code collisions or override semantics
- Reliable JS bridge registration
- Deterministic MCP agent references

### Surgical Delta Serialization
The framework only transmits the **exact fields** that have changed. This "Surgical Sync" pattern minimizes bandwidth and server-side CPU overhead during large-scale data updates.

### Zero-Trust Shadow Validation
Validation logic is compiled into the object itself. The server can re-calculate the validation bitmask and compare it against the client-reported state to detect tampering or logical inconsistencies instantly.

### Time-Travel Snapshots
Every `ValidObject` maintains a circular buffer of its previous states. `GetStateHistory()` returns a **defensive copy** — callers cannot corrupt internal state. This allows for instant "Undo/Redo" logic and makes debugging complex UI interactions trivial.

### Bounded Memory Management
The `ValidObjectRegistry` caps at 10,000 entries with LRU eviction, preventing memory exhaustion in long-running MCP hosts. The `SpinLock` used for bitmask updates is intentionally not `readonly` to avoid the mutable-struct defensive copy trap.

---

## Business Value

1. **Transparency**: The V.A.V.I.D. HUD removes the "Black Box" of business logic, allowing QA, developers, and even BA's to see the state of an application visually.
2. **Resilience**: The CRDT merge logic ensures that data changed offline can be converged predictably without data loss, even if multiple users edit the same record.
3. **Performance**: By stripping away reflection and managed heap overhead, V.A.L.I.D. enables enterprise-grade logic with a near-zero performance footprint.
4. **Security**: SHA-256 hash chaining and AES-GCM authenticated encryption protect offline payloads from tampering and eavesdropping.
5. **AI-Ready**: With built-in MCP tools, the framework is ready to be inspected and tutored by agentic AIs, accelerating the development lifecycle.

---

## Workspace Organization

- **`/src`**: Production source code and framework logic.
- **`/samples`**: Demonstration applications (Blazor WASM, Console, and Demo-Cashbook with benchmarks).
- **`/tests`**: Automated xUnit testing suite and chaos-monkey fuzzers.
- **`/docs`**: Technical manuals, system overviews, and mission logs.
- **`/.github/workflows`**: CI pipeline (build, test, security scan).
