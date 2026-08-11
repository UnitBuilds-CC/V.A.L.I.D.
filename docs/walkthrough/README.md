# V.A.L.I.D. Architecture Walkthrough

> **V**ectorized **A**synchronous **L**ogic & **I**ntelligent **D**iagnostics — A zero-boilerplate, 128-bit bitmask-driven validation and change-tracking framework for .NET 8.

## Solution Map

```
                    ┌─────────────────────────────────┐
                    │         SHARED LAYER             │
                    │  Valid (Core Library)             │
                    │  Valid.Generator (Source Gen)     │
                    │  Valid.FSharp (F# Engine)         │
                    │  Valid.Mcp (AI Agent Tools)       │
                    │  Valid.Testing (Test Helpers)     │
                    └──────────┬──────────────────────-─┘
                               │
              ┌────────────────┼────────────────┐
              ▼                                 ▼
   ┌──────────────────┐              ┌──────────────────┐
   │   CLIENT LAYER   │              │    API LAYER     │
   │ Valid.Sample.Blazor│             │  Valid.Sample     │
   │ Valid.Extension   │              │  (Console Demo)  │
   │ Demo-Cashbook     │              │                    │
   └──────────────────┘              └──────────────────┘
```

| Layer | Purpose |
|---|---|
| **Shared** | Core framework: attributes, base classes, interfaces, source generator, CRDT, outbox (SHA-256 + AES-GCM), F# engine, MCP tools |
| **Client** | Blazor WebAssembly app: pages, components, JS bridge, browser extension, enterprise demo |
| **API** | Console demo and headless usage: model definitions, outbox integration |

## How It Works

1. You define a `partial class` with `[ValidObject]` and `[ValidProperty]` attributes
2. The Roslyn source generator (`Valid.Generator`) emits backing fields, `SetProperty` calls with bit indices, `Validate()`, `GetDiagnostics()`, `GetDeltaJson()`, and `GetValidMetadata()`
3. At runtime, every property mutation flips a bit in a `UInt128` bitmask — zero allocations
4. Each object receives a unique `ValidId` (e.g., `valid_1`) for JS bridge registration and MCP tracking
5. The Blazor client (`VavidComponentBase`) forwards these bitmask snapshots to the JavaScript bridge (`vavid.js`), which surgically updates only the affected DOM nodes via `[vavid-bit="N"]` selectors
6. The browser extension (`Valid.Extension`) polls `__VALID_OBJECTS__` and renders a real-time HUD with XSS-safe HTML escaping

## File Inventory

| Project | Files | Lines |
|---|---|---|
| `Valid` | 12+ source files | ~600+ |
| `Valid.Generator` | 1 source file | ~917 |
| `Valid.FSharp` | 2 source files | ~40 |
| `Valid.Mcp` | 2+ source files | ~150+ |
| `Valid.Sample` | 2 source files | ~70 |
| `Valid.Sample.Blazor` | 14 source files + 6 Razor | ~1,800 |
| `Valid.Extension` | 4 files | ~450 |
| `Valid.Testing` | 1 source file | ~22 |
| `Demo-Cashbook` | Multiple projects | ~2,000+ |
