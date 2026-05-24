# V.A.L.I.D. System Overview

**Vectorized Asynchronous Logic & Intelligent Diagnostics**

V.A.L.I.D. 3.0 is a next-generation business logic framework for .NET applications that replaces traditional, heavy object tracking systems with a high-performance, bitmask-driven architecture.

---

## 🏗️ Core Architecture

The system is divided into two primary domains:

### 1. V.A.L.I.D. (.NET Framework)
Located in `/src`, this is the backend logic engine. It uses **Roslyn Source Generators** to emit surgical, reflection-free code at compile time.
- **`Valid`**: The main library providing the `ValidObjectBase` and bitmask manipulation logic.
- **`Valid.Generator`**: The "Property Weir" that generates backing fields, property implementations, and JSON hydration logic.
- **`Valid.FSharp`**: High-resilience mathematical models for CRDT (Conflict-free Replicated Data Types) state merging.
- **`Valid.Mcp`**: Model Context Protocol integration, allowing AI agents to inspect and mutate live business objects in real-time.

### 2. V.A.V.I.D. (Visual Diagnostics HUD)
Enabled by `/src/Valid.Extension`, this provides a browser-based diagnostic layer that links directly to the .NET state.
- **JS Bridge**: Synchronizes the state between the .NET WASM host and the DOM.
- **Surgical Pulse**: When a field is changed in .NET, only that specific property "pulses" visually in the browser.
- **Rule Inspector**: Allows developers to hover over any UI field to see exactly which validation rules are failing or passing.

---

## 🚀 Key Capabilities

### ⚡ Vectorized State Tracking
Instead of dictionaries or lists of dirty property names, V.A.L.I.D. uses a single **128-bit bitmask** (`System.UInt128`) to track object state (Dirty, Busy, Error, etc.). This allows for O(1) state checks and extremely small payloads.

### 🧬 Surgical Delta Serialization
The framework only transmits the **exact fields** that have changed. This "Surgical Sync" pattern minimizes bandwidth and server-side CPU overhead during large-scale data updates.

### 🛡️ Zero-Trust Shadow Validation
Validation logic is compiled into the object itself. The server can re-calculate the validation bitmask and compare it against the client-reported state to detect tampering or logical inconsistencies instantly.

### 🕒 Time-Travel Snapshots
Every `ValidObject` maintains a circular buffer of its previous states. This allows for instant "Undo/Redo" logic and makes debugging complex UI interactions trivial.

---

## 💼 Business Value

1. **Transparency**: The V.A.V.I.D. HUD removes the "Black Box" of business logic, allowing QA, developers, and even BA's to see the state of an application visually.
2. **Resilience**: The CRDT merge logic ensures that data changed offline can be converged predictably without data loss, even if multiple users edit the same record.
3. **Performance**: By stripping away reflection and managed heap overhead, V.A.L.I.D. enables enterprise-grade logic with a near-zero performance footprint.
4. **AI-Ready**: With built-in MCP tools, the framework is ready to be inspected and tutored by agentic AIs, accelerating the development lifecycle.

---

## 📂 Workspace Organization

- **`/src`**: Production source code and framework logic.
- **`/samples`**: Demonstration applications (Blazor WASM and Console).
- **`/tests`**: Automated xUnit testing suite and chaos-monkey fuzzers.
- **`/docs`**: Technical manuals, system overviews, and mission logs.
- **`/diagnostics`**: Build logs and automated error traces.
