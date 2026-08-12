# V.A.L.I.D. Visual Guide System

Create annotated screenshots and visual guides for the V.A.L.I.D. framework using MCP-Lite browser automation.

## Overview

The visual guide system combines:
- **MCP-Lite**: Agentic browser automation with screenshot capabilities
- **Blazor Sample App**: Running V.A.L.I.D. components in a real browser
- **MCP Tools**: Orchestration layer for LLMs to capture visual guides

## Prerequisites

1. **MCP-Lite** (cloned and built):
   ```bash
   git clone https://github.com/UnitBuilds-CC/MCP-Lite.git tools/mcp-lite
   cd tools/mcp-lite
   go build -o mcp-server.exe ./cmd/mcp/...
   ```

2. **Google Chrome** installed (standard path)

3. **.NET 8 SDK** for running the Blazor sample app

## Usage

### 1. Initialize the Visual Guide System

```csharp
// Via MCP tool
valid_initialize_visual_guides()
// Returns: { success: true, blazorAppUrl: "https://localhost:5001", mcpLitePid: 12345 }
```

This launches:
- The Blazor sample app (`samples/Valid.Sample.Blazor`)
- MCP-Lite browser automation server

### 2. Navigate to a Page

```csharp
valid_navigate_visual_guide("customer")
// Navigates to https://localhost:5001/customer
```

Available pages:
- `customer` — Customer form with validation
- `product` — Product form with field proxy
- `order` — Order form with explicit bit indexes
- `time-travel` — State history visualization
- `delta-sync` — Dirty tracking demo

### 3. Capture Screenshots

**Full viewport:**
```csharp
valid_capture_screenshot("guides/customer-form.png")
// Returns: { success: true, path: "guides/customer-form.png", size: 245678 }
```

**Specific element (node):**
```csharp
// First, get the AOM to find node IDs
// (Use MCP-Lite's get_aom tool via direct MCP call)

// Then capture the element
valid_capture_node_screenshot("280", "guides/name-field.png")
// Returns: { success: true, path: "guides/name-field.png", nodeId: "280" }
```

### 4. Shut Down

```csharp
valid_shutdown_visual_guides()
// Terminates MCP-Lite and Blazor app
```

## Example: Creating a Validation Guide

```python
# LLM workflow for creating a visual guide
valid_initialize_visual_guides()
valid_navigate_visual_guide("customer")
valid_capture_screenshot("guides/01-customer-form.png")

# Highlight validation error
# (Type invalid data via MCP-Lite's type_text tool)
valid_capture_screenshot("guides/02-validation-error.png")

# Show dirty tracking
# (Modify a field via MCP-Lite's click/type tools)
valid_capture_screenshot("guides/03-dirty-tracking.png")

valid_shutdown_visual_guides()
```

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `valid_initialize_visual_guides()` | Launch MCP-Lite + Blazor app |
| `valid_navigate_visual_guide(path)` | Navigate to a sample page |
| `valid_capture_screenshot(outputPath)` | Capture full viewport |
| `valid_capture_node_screenshot(nodeId, outputPath)` | Capture specific element |
| `valid_shutdown_visual_guides()` | Terminate the system |

## Integration with MCP-Lite Tools

The visual guide system works alongside MCP-Lite's native tools:

| MCP-Lite Tool | Purpose |
|---------------|---------|
| `get_aom(withSpatial, withStyles)` | Get accessibility tree with node IDs |
| `click(nodeId)` | Click on an element |
| `type_text(nodeId, text)` | Type into a textbox |
| `scroll(direction, amount)` | Scroll the page |
| `inspect_node(nodeId)` | Get CSS and coordinates |

## Output Structure

```
guides/
├── customer/
│   ├── 01-initial-form.png
│   ├── 02-validation-error.png
│   ├── 03-dirty-tracking.png
│   └── 04-delta-sync.png
├── product/
│   ├── 01-field-proxy.png
│   └── 02-generated-properties.png
└── architecture/
    ├── bitmask-engine.png
    └── blazor-integration.png
```

## Troubleshooting

**MCP-Lite not found:**
```
Error: MCP-Lite not found at tools/mcp-lite/mcp-server.exe
```
Solution: Clone and build MCP-Lite (see Prerequisites).

**Blazor app won't start:**
```
Error: Failed to launch Blazor app
```
Solution: Ensure .NET 8 SDK is installed and `samples/Valid.Sample.Blazor` exists.

**Chrome not found:**
```
Error: Chrome browser not found
```
Solution: Install Google Chrome in the standard location.

## Architecture

```
┌─────────────────┐
│   LLM Client    │
│  (Claude, etc.) │
└────────┬────────┘
         │ MCP Protocol
         ▼
┌─────────────────┐
│  Valid.Mcp      │
│  (ValidTools)   │
└────────┬────────┘
         │ Orchestrates
         ▼
┌─────────────────┐      ┌─────────────────┐
│ MCP-Lite        │◄────►│ Blazor Sample   │
│ (Browser MCP)   │      │ App (WASM)      │
└─────────────────┘      └─────────────────┘
         │
         ▼
┌─────────────────┐
│ Google Chrome   │
│ (CDP Control)   │
└─────────────────┘
```

## Next Steps

- Create visual guides for each validation attribute
- Document the bitmask engine with annotated diagrams
- Build interactive tutorials with step-by-step screenshots
