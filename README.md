# V.A.L.I.D. (Vectorized Asynchronous Logic & Intelligent Diagnostics)

[![Framework Status](https://img.shields.io/badge/Status-Stable-brightgreen)](https://github.com/UnitBuilds-CC/V.A.L.I.D)
[![Performance](https://img.shields.io/badge/Performance-Vectorized-blue)](https://github.com/UnitBuilds-CC/V.A.L.I.D)
[![Diagnostics](https://img.shields.io/badge/HUD-V.A.V.I.D.-orange)](https://github.com/UnitBuilds-CC/V.A.L.I.D)
[![CI](https://img.shields.io/badge/CI-GitHub%20Actions-2088FF)](https://github.com/UnitBuilds-CC/V.A.L.I.D/actions)
![Version](https://img.shields.io/badge/Version-3.0.3-blueviolet)

V.A.L.I.D. is a high-performance, low-latency business logic framework for .NET 8. It replaces standard reflection-based change tracking with a compile-time, bitmask-driven, and compiler-integrated architecture using **128-bit `UInt128` state masks** for dirty, busy, error, and lifecycle tracking.

Built specifically for complex enterprise data management, V.A.L.I.D. ensures surgical precision in synchronization and real-time visibility in the browser. Every object carries a unique `ValidId` for deterministic identity, JS bridge registration, and MCP agent tracking.

---

## Performance Benchmarks

V.A.L.I.D. is engineered for absolute zero-allocation on core operations. Below are the official BenchmarkDotNet results comparing V.A.L.I.D.'s direct memory write speed against other state-management components:

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.29591.1000)
.NET SDK 10.0.200-preview.0.26103.119
  [Host]     : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
```

| Method | Mean | Error | StdDev | Gen0 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: |
| **VALID Slab direct memory write** | **6.619 ns** | 0.1573 ns | 0.2305 ns | - | - (0 B) |
| **F# Rule Evaluation** | **15.800 ns** | 0.3659 ns | 0.7308 ns | - | - (0 B) |
| **F# CRDT Convergence** | **86.478 ns** | 1.7367 ns | 3.7384 ns | 0.0391 | 328 B |
| **Blazor VDOM Mutation & Validation** | **172.783 ns** | 3.4228 ns | 5.6238 ns | 0.0048 | 40 B |

*Note: The core "Slab" direct memory write operates at registers-level efficiency (~6.6 nanoseconds), producing **0 bytes of garbage collection overhead**.*

---

## Key Features

### 1. 128-Bit Vectorized Delta Tracking
Forget heavy dictionaries or multiple boolean flags. V.A.L.I.D. uses **`System.UInt128` bitmasks** to track up to 128 properties across four orthogonal state dimensions (Dirty, Busy, Error, State).
- **Micro-Payloads**: Only dirty fields are transmitted via surgical delta JSON.
- **Zero-Allocate Logic**: Powered by Roslyn Source Generators — no reflection at runtime.
- **Deterministic Identity**: Every object gets a unique `ValidId` (e.g., `valid_1`, `valid_2`) for JS bridge registration and MCP tracking.

### 2. Active Intelligence (Preflight Rules)
Validate locally, validate fast.
- **RuleEngine**: Executes `[Required]` and `[Range]` rules locally before hitting the network.
- **Bandwidth Shield**: Blocks invalid data at the Outbox level, preserving server CPU and network traffic.

### 3. Hierarchical Resilience
State management that understands your object relationships.
- **Hierarchy Wiring**: Child changes automatically bubble up to parents.
- **Atomic Resets**: `ResetDirtyFlags(cascade: true)` handles the entire object lifecycle in a single call.
- **Defensive State**: `GetStateHistory()` returns a safe copy — callers cannot corrupt internal state.

### 4. Offline-First Security
The SQLite outbox provides tamper-resistant, authenticated persistence for offline-first scenarios.
- **SHA-256 Hash Chaining**: Every entry is cryptographically linked to its predecessor, ensuring causal ordering and tamper detection.
- **AES-GCM Authenticated Encryption**: Payloads are encrypted with AES-256-GCM, providing both confidentiality and integrity (nonce + authentication tag + ciphertext).
- **Batch Dequeue**: `DequeueAsync<T>()` processes unprocessed entries in batches with `Processed` flag tracking.

### 5. V.A.V.I.D. HUD (X-Ray Vision)
The final bridge between .NET and the Browser.
- **Visual Pulse**: Dirty fields glow red in the browser via `bridge.js`.
- **Rule Inspector**: Hover over any field to see a tooltip derived directly from your .NET attributes.
- **XSS Protection**: All interpolated HUD values are HTML-entity-escaped to prevent injection via crafted property names.
- **Controlled Polling**: `startPolling()`/`stopPolling()` functions allow precise lifecycle control of the bridge.

### 6. AI-Ready (MCP Integration)
Built-in Model Context Protocol tools allow AI agents to inspect and mutate live business objects.
- **Bounded Registry**: The `ValidObjectRegistry` caps at 10,000 entries with LRU eviction, preventing memory exhaustion in long-running hosts.
- **Live State Inspection**: Agents read dirty/busy/error bitmasks directly via MCP tools.

---

## Project Structure

The workspace is organized into logical categories to separate core logic, samples, and testing.

- **`src/`**: Core framework and library source code.
    - `Valid`: Main logic, base classes, attributes, outbox queue, and Blazor components.
    - `Valid.Generator`: Roslyn Source Generators ("Property Weir") for bitmask logic.
    - `Valid.FSharp`: Mathematical CRDT and merge logic (F#).
    - `Valid.Mcp`: Model Context Protocol integration for AI agent interaction.
    - `Valid.Extension`: V.A.V.I.D. browser extension source (Chrome/Edge).
- **`samples/`**: Demonstration and verification projects.
    - `Valid.Sample`: Console-based integration tests.
    - `Valid.Sample.Blazor`: WebAssembly demonstration app.
    - `Demo-Cashbook`: Full enterprise demo with benchmarks.
- **`tests/`**: Automated testing suites.
    - `Valid.Testing`: XUnit test project.
- **`docs/`**: Technical documentation and mission history.
- **`.github/workflows/`**: CI pipeline (build, test, security scan).

## Architecture Overview

| Component | Role | Legacy Comparison |
| :--- | :--- | :--- |
| **`Valid.Generator`** | The "Property Weir" | Replaces thousands of lines of manual Get/SetProperty. |
| **`Valid.Queue`** | Resilient Outbox (SHA-256 + AES-GCM) | Replaces manual "Save-and-Wait" UI patterns. |
| **`Valid.Core`** | Intelligent Rules & CRDT Merge | Replaces opaque, nested InnerExceptions. |
| **`Valid.Mcp`** | AI Agent Integration | Replaces manual debugging and inspection. |
| **`V.A.V.I.D. HUD`** | Visual Diagnostics | Replaces hours of Playwright/Selenium scripting. |

---

## Summary of Capabilities

V.A.L.I.D. is a high-performance framework optimized for modern web interfaces:
- **Surgical**: Sends only changed bits via `UInt128` bitmasked deltas.
- **Resilient**: Works offline via a SHA-256 hash-chained, AES-GCM encrypted Outbox.
- **Visible**: Glows red in the browser when dirty via the V.A.V.I.D. Bridge.
- **Safe**: Fails the build if you forget a Fetch method or break DataShuttle parity.
- **Secure**: Authenticated encryption and hash-chain integrity for all persisted payloads.
- **AI-Ready**: MCP tools for real-time AI agent inspection of live business objects.
- **Bounded**: LRU eviction on object registry prevents memory leaks in long-running hosts.

---

## Quick Start (The 5-Step Path)

1. **Define DTO**: Use `[ValidObject]` and `[Rule]` attributes.
2. **Generate BO**: The "Property Weir" writes your business object with `ValidId`, bitmask tracking, and validation.
3. **Save via Shuttle**: Implement `IDataShuttle` for your API.
4. **Queue Sync**: Use `SqliteOutbox` for reliable, encrypted, hash-chained delivery.
5. **HUD Integration**: Add `bridge.js` to your Blazor/HTML UI for real-time diagnostics.

---

## Development & CI

V.A.L.I.D. uses GitHub Actions for continuous integration:
- **Build & Test**: Restores, builds (Release), and runs all tests on every push/PR to `main`.
- **Security Scan**: Runs `dotnet-security-scan` against the core library after build passes.

---

## Licensing

V.A.L.I.D. is open-source software licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. 
- Anyone can use, modify, and distribute this software for free under the terms of the AGPL.
- **Copyleft Obligation**: If you host this library on a network or integrate it into a web application, you must release your application's source code under the same AGPL license terms.
- **Commercial Exception**: If you wish to use this framework in a closed-source commercial or proprietary setting without the copyleft obligations, you must obtain a commercial license. Contact the repository owner for commercial licensing options.

---

## Design Principles
> *The code is the documentation, and the metadata is the test suite.*
