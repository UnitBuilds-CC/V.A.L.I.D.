# V.A.L.I.D. (Vectorized Asynchronous Logic & Intelligent Diagnostics)

[![Framework Status](https://img.shields.io/badge/Status-Stable-brightgreen)](https://github.com/UnitBuilds-CC/V.A.L.I.D)
[![Performance](https://img.shields.io/badge/Performance-Vectorized-blue)](https://github.com/UnitBuilds-CC/V.A.L.I.D)
[![Diagnostics](https://img.shields.io/badge/HUD-V.A.V.I.D.-orange)](https://github.com/UnitBuilds-CC/V.A.L.I.D)

V.A.L.I.D. is a high-performance, low-latency business logic framework for .NET. It replaces standard reflection-based change tracking with a compile-time, bitmask-driven, and compiler-integrated architecture.

Built specifically for complex enterprise data management, V.A.L.I.D. ensures surgical precision in synchronization and real-time visibility in the browser.

---

## ⚡ Performance Benchmarks

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

## 🚀 Key Features

### 1. Vectorized Delta Tracking
Forget heavy dictionaries or multiple boolean flags. V.A.L.I.D. uses a single **8-byte `ulong` bitmask** to track up to 64 properties.
- **Micro-Payloads**: Only dirty fields are transmitted.
- **Zero-Allocate Logic**: Powered by Roslyn Source Generators.

### 2. Active Intelligence (Preflight Rules)
Validate locally, validate fast.
- **RuleEngine**: Executes `[Required]` and `[Range]` rules locally before hitting the network.
- **Bandwidth Shield**: Blocks invalid data at the Outbox level, preserving server CPU and network traffic.

### 3. Hierarchical Resilience
State management that understands your object relationships.
- **Hierarchy Wiring**: Child changes automatically bubble up to parents.
- **Atomic Resets**: `ResetDirtyFlags(cascade: true)` handles the entire object lifecycle in a single call.

### 4. V.A.V.I.D. HUD (X-Ray Vision)
The final bridge between .NET and the Browser.
- **Visual Pulse**: Dirty fields glow red in the browser via `bridge.js`.
- **Rule Inspector**: Hover over any field to see a tooltip derived directly from your .NET attributes.

---

## 📂 Project Structure

The workspace is organized into logical categories to separate core logic, samples, and testing.

- **`src/`**: Core framework and library source code.
    - `Valid`: Main logic and base classes.
    - `Valid.Generator`: Roslyn Source Generators for bitmask logic.
    - `Valid.FSharp`: Mathematical CRDT and merge logic.
    - `Valid.Mcp`: Model Context Protocol integration.
    - `Valid.Extension`: V.A.V.I.D. browser extension source.
- **`samples/`**: Demonstration and verification projects.
    - `Valid.Sample`: Console-based integration tests.
    - `Valid.Sample.Blazor`: WebAssembly demonstration app.
- **`tests/`**: Automated testing suites.
    - `Valid.Testing`: XUnit test project.
- **`docs/`**: Technical documentation and mission history.
- **`diagnostics/`**: Build logs and error reports.

## 📂 Architecture Overview

| Component | Role | Legacy Comparison |
| :--- | :--- | :--- |
| **`Valid.Generator`** | The "Property Weir" | Replaces thousands of lines of manual Get/SetProperty. |
| **`Valid.Analyzer`** | Preflight Checks | Replaces runtime "Method Not Found" crashes. |
| **`Valid.Queue`** | Resilience | Replaces manual "Save-and-Wait" UI patterns. |
| **`Valid.Core`** | Intelligent Rules | Replaces opaque, nested InnerExceptions. |
| **`V.A.V.I.D. HUD`** | Visual Diagnostics | Replaces hours of Playwright/Selenium scripting. |

---

## 🏁 Summary of Capabilities

V.A.L.I.D. is a high-performance framework optimized for modern web interfaces:
- **Surgical**: Sends only changed bits via bitmasked deltas.
- **Resilient**: Works offline via a high-performance Outbox.
- **Visible**: Glows red in the browser when dirty via the V.A.V.I.D. Bridge.
- **Safe**: Fails the build if you forget a Fetch method or break DataShuttle parity.

---

## 📋 Quick Start (The 5-Step Path)

1. **Define DTO**: Use `[ValidObject]` and `[Rule]` attributes.
2. **Generate BO**: The "Property Weir" writes your business object.
3. **Save via Shuttle**: Implement `IDataShuttle` for your API.
4. **Queue Sync**: Use `SyncWorker` for reliable delivery.
5. **HUD Integration**: Add `bridge.js` to your Blazor/HTML UI.

---

## 📜 Licensing

V.A.L.I.D. is open-source software licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. 
- Anyone can use, modify, and distribute this software for free under the terms of the AGPL.
- **Copyleft Obligation**: If you host this library on a network or integrate it into a web application, you must release your application's source code under the same AGPL license terms.
- **Commercial Exception**: If you wish to use this framework in a closed-source commercial or proprietary setting without the copyleft obligations, you must obtain a commercial license. Contact the repository owner for commercial licensing options.

---

## 📜 Design Principles
> *The code is the documentation, and the metadata is the test suite.*
