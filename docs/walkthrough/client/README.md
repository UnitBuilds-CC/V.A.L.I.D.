# Client Layer

The client layer contains the Blazor WebAssembly application and the browser extension. This is where the V.A.L.I.D. framework meets the user.

---

## Valid.Sample.Blazor (Blazor WASM App)

### Model Definitions

These are the **only files a developer writes** to define data objects. The source generator does the rest.

| File | Purpose |
|---|---|
| `SpaceShuttle.cs` | 3 properties: `ShuttleName` (string), `Velocity` (double, `[Range(0, 50000)]`), `IsDocked` (bool). Used by `Home.razor`. |
| `AxiomEntry.cs` | 6 properties with `[Required]`, `[StringLength]`, and `[Range]` validation. Used by grids. |
| `Ledger.cs` | Defines `LedgerLine` (4 props) and `LedgerHeader` (3 props). Used by `Grid.razor`. |

### Helper Utilities

| File | Purpose |
|---|---|
| `GuiGenerator.cs` | Reflection-based column generator. `GenerateColumns<T>()` creates `GridColumnDefinition` list by instantiating a dummy `T`, iterating its properties, and calling `GetBitIndex()` for deterministic bit-index mapping. Used by `ComparisonGrid` and `Grid` pages. |
| `Program.cs` | Blazor WASM entry point. Registers `HttpClient` and `Serilog` for browser console logging. |
| `App.razor` | Root component with routing. |
| `_Imports.razor` | Global `@using` directives for the Blazor app. |

### Pages (`Pages/`)

| File | Lines | Purpose |
|---|---|---|
| `Home.razor` | 138 | **SpaceShuttle demo.** Manual `VavidComponentBase` usage with input fields bound to `vavid-obj`, `vavid-bit`. Shows telemetry dashboard and real-time diagnostics via `ValidErrorStrip`. |
| `Grid.razor` | 278 | **Ledger grid demo.** 10,000-row virtualized grid with `LedgerLine` objects. Uses `VavidComponentBase` and dynamic `GetBitIndex()` for header/row binding. |
| `ProGrid.razor` | 234 | **1M-row stress test.** Same architecture as Grid but with 1,000,000 `AxiomEntry` rows and a chaos fuzzer. Shows MPS throughput and surgical latency. |
| `ComparisonGrid.razor` | 446 | **Head-to-head demo.** Split view: standard Blazor (full `StateHasChanged`) vs. Vavid (surgical bitmask). Toggleable C#/F# engines, independent fuzzers, security audit log. |
| `Counter.razor` | 12 | Default Blazor template counter (unchanged). |
| `Weather.razor` | 47 | Default Blazor template weather page (unchanged). |

### Shared Components (`Shared/`)

All custom components follow the zero-boilerplate pattern by inheriting from `VavidComponent<T>`.

| File | Inherits | Purpose |
|---|---|---|
| `VavidValue.razor` | — | **Leaf renderer.** Receives a `Property` string, looks up `BitIndex` from the cascaded `IValidObject`, and renders a `<span vavid-bit="N">` with formatted value. Supports `currency` and `bool` formatting. |
| `ValidDataGridRow.razor` | `VavidComponent<AxiomEntry>` | Full-featured row with `VavidValue` cells, expandable footer with `ValidErrorStrip`, edit/delete buttons. Used by `ProGrid`. |
| `ValidDataGridRowShort.razor` | `VavidComponent<AxiomEntry>` | Compact row. Nests `AxiomAmountCell` and `AxiomStatusCell` sub-components. Used by `ComparisonGrid`. |
| `AxiomAmountCell.razor` | `VavidComponent<AxiomEntry>` | Single `<td>` wrapping `<VavidValue Property="Amount" Format="currency" />`. |
| `AxiomStatusCell.razor` | `VavidComponent<AxiomEntry>` | Single `<td>` wrapping `<VavidValue Property="IsProcessed" />`. |
| `ValidErrorStrip.razor` | — | Displays validation errors by calling `GetDiagnostics()` on the model. Each error chip is tagged with `vavid-bit` for surgical highlighting. |

### Component Hierarchy

```
VavidComponentBase
  ├── registers Model with JS bridge
  ├── cascades IValidObject via CascadingValue
  └── forwards PropertyChanged → vavid.updateState() + vavid.logDelta()

VavidComponent<T> : VavidComponentBase
  └── maps Value parameter → Model (zero boilerplate)

VavidValue (leaf)
  └── reads CascadingValue<IValidObject>, calls GetBitIndex(Property)
  └── emits <span vavid-bit="N" vavid-format="...">
```

---

## Valid.Extension (Browser DevTools)

A Chrome/Edge extension that provides a real-time debugging HUD overlaid on any Blazor app using V.A.L.I.D.

| File | Lines | Purpose |
|---|---|---|
| `manifest.json` | 15 | Extension manifest. Injects `bridge.js` and `hud.js` + `hud.css` into pages. |
| `bridge.js` | 21 | Discovery bridge. Polls `window.__VALID_OBJECTS__` every 1s and fires `vavid-ext-discovery` events. |
| `hud.js` | 425 | **The full HUD.** 4 tabs: X-RAY (property inspection + value push + time travel), SECURITY (violation analysis), DELTAS (surgical delta stream), PERF (latency sparkline). Includes inspect mode with global click interceptor. |
| `hud.css` | ~80 | Glassmorphism styling for the floating HUD panel. |

### HUD Tabs

| Tab | Features |
|---|---|
| **X-RAY** | 128-bit grid visualization, property type/name display, bi-directional value push, time-travel slider, value history log (last 10 mutations) |
| **SECURITY** | Real-time threat level indicator, per-bit violation breakdown with property name resolution |
| **DELTAS** | Scrollable log of the last 10 surgical JSON deltas with timestamps |
| **PERF** | Live bit-flip latency display, canvas sparkline chart, memory/precision metrics |
