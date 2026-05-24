# Demo-Cashbook (V.A.L.I.D. 3.0 Sync Demo)

This repository contains a high-performance Cashbook Editor designed for corporate ERP synchronization. It is built using the **V.A.L.I.D. 3.0** framework, utilizing reactive bitmasks, F# business rule engines, and an unmanaged state bypass bridge for ultra-low latency WebAssembly UI updates.

The UI is styled to match a clean corporate theme with custom module-specific highlights (General Ledger, Accounts Receivable, and Accounts Payable).

---

## Architecture & Core Concepts

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Blazor WebAssembly UI                           │
│                                                                        │
│  [ Theme Layout ] ◄───(VAVID JS Bypass reads memory)                  │
│  [ CashbookLineRow.razor  ] ◄───(Direct HTML Input updates)            │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                         (Initializes / Syncs pointer)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        WebWorkerBridge (WASM)                          │
│                                                                        │
│  [ Unmanaged Memory State Slab ] ◄──(Zero allocation updates)          │
│  Pointer Address: 0xXXXXXX                                             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                               (Evaluates)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        F# Rules Engine (CRDT)                          │
│                                                                        │
│  - F# Business Rules Match (evaluateLineRules / evaluateCashbookRules) │
│  - AWORSet Logical Conflict Resolution (resolveLineLogicalCollision)  │
└────────────────────────────────────────────────────────────────────────┘
```

The system is split into three main components:

1. **Blazor WebAssembly Frontend (`Demo-Cashbook`)**:
   - Manages the main workspace layout, offline convergence inputs, and telemetry diagnostics.
   
2. **F# Business Logic & CRDT Engine (`CashbookRules`)**:
   - Pure functional domain representations of transaction records (`CashbookLineRecord`).
   - Bit-registered validations mapping validation codes to bit indexes.
   - An **AWORSet CRDT logical resolver** converging offline conflicting data modifications.

3. **VAVID Surgical Bypass Bridge (`wwwroot/js/vavid-bypass.js`)**:
   - Bypasses Blazor's virtual DOM (VDOM) diffing and StateHasChanged cycles.
   - Writes/reads numeric offsets directly in the WebAssembly shared linear memory heap (`HEAPU8`).

---

## Performance Benchmarks

All metrics were captured using **BenchmarkDotNet v0.13.12** on a `.NET 8.0` environment:

| Method | Mean | Error | StdDev | Gen 0 / 1000 Ops | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: |
| **VALID Slab direct memory write** | **6.59 ns** | 0.07 ns | 0.07 ns | - | **0 B** |
| **F# Rule Evaluation** | **15.64 ns** | 0.32 ns | 0.28 ns | - | **0 B** |
| **F# CRDT Convergence** | **86.91 ns** | 1.77 ns | 4.93 ns | 0.0391 | **328 B** |
| **Blazor VDOM Mutation & Validation** | **171.18 ns** | 3.13 ns | 2.92 ns | 0.0048 | **40 B** |

---

## Getting Started

### Prerequisites
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download/dotnet/8.0)

### 1. Running the Web Application
Restore, build, and run the developer server:
```bash
dotnet run --project Demo-Cashbook --urls "http://localhost:5200"
```
Navigate to `http://localhost:5200` in your web browser.

### 2. Running the Benchmarks
To run the performance suite, execute:
```bash
dotnet run -c Release --project Demo-Cashbook.Benchmarks
```

---

## Licensing

This demonstration project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.
