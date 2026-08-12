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

---

# Workflow Recording & E2E Test Generation

Record browser workflows via MCP-Lite and generate runnable E2E tests.

## Workflow Recording

### 1. Start Recording

```csharp
valid_start_workflow_recording("customer_validation", "Tests customer form validation flow")
// Returns: { success: true, workflowName: "customer_validation" }
```

### 2. Record Steps

```csharp
// Navigate to page
valid_record_workflow_step("navigate", url: "https://localhost:5001/customer")

// Type into a field
valid_record_workflow_step("type_text", nodeId: "280", text: "John Doe")

// Click a button
valid_record_workflow_step("click", nodeId: "295")

// Wait for validation
valid_record_workflow_step("wait", waitMs: 1000)

// Take a screenshot at key moments
valid_capture_screenshot("guides/customer/01-filled-form.png")
```

### 3. Stop & Save

```csharp
valid_stop_workflow_recording(
    description: "Validates customer form with required fields",
    baseUrl: "https://localhost:5001")
// Returns: { success: true, path: "workflows/customer_validation.json", stepCount: 5 }
```

## Workflow JSON Format

Saved workflows are stored in `workflows/` as JSON:

```json
{
  "Name": "customer_validation",
  "Description": "Validates customer form with required fields",
  "CreatedAt": "2026-08-12T07:00:00Z",
  "BaseUrl": "https://localhost:5001",
  "Steps": [
    { "Action": "navigate", "Url": "https://localhost:5001/customer" },
    { "Action": "type_text", "NodeId": "280", "Text": "John Doe" },
    { "Action": "click", "NodeId": "295" },
    { "Action": "wait", "WaitMs": 1000 }
  ]
}
```

## E2E Test Generation

Generate runnable test code from recorded workflows:

### bUnit (default)

```csharp
valid_generate_e2e_test("customer_validation", "bunit")
// Generates: tests/E2E/customer_validationTests.cs
```

### Playwright

```csharp
valid_generate_e2e_test("customer_validation", "playwright")
// Generates headless browser test using Microsoft.Playwright
```

### Selenium

```csharp
valid_generate_e2e_test("customer_validation", "selenium")
// Generates Selenium WebDriver test
```

## Sitemap Export

Capture and save the application's page structure:

```csharp
// After crawling with MCP-Lite's graph_start_crawl
valid_save_sitemap("https://localhost:5001", "[
  { \"Url\": \"/\", \"Title\": \"Home\", \"Depth\": 0, \"ChildUrls\": [\"/customer\", \"/product\"] },
  { \"Url\": \"/customer\", \"Title\": \"Customer\", \"Depth\": 1, \"ChildUrls\": [] },
  { \"Url\": \"/product\", \"Title\": \"Product\", \"Depth\": 1, \"ChildUrls\": [] }
]")
// Saves to: sitemaps/sitemap_20260812_070000.json
```

## Complete E2E Pipeline

```
1. valid_initialize_visual_guides()     → Launch browser + Blazor app
2. valid_start_workflow_recording(...)   → Begin recording
3. valid_navigate_visual_guide(...)      → Navigate pages
4. valid_record_workflow_step(...)       → Record interactions
5. valid_capture_screenshot(...)         → Capture key moments
6. valid_stop_workflow_recording(...)    → Save workflow JSON
7. valid_generate_e2e_test(...)          → Generate test code
8. valid_save_sitemap(...)               → Export page structure
9. valid_shutdown_visual_guides()        → Clean up
```

## Directory Structure

```
V.A.L.I.D/
├── workflows/                    # Recorded workflow JSON files
│   ├── customer_validation.json
│   └── product_creation.json
├── sitemaps/                     # Captured site structure
│   └── sitemap_20260812.json
├── tests/
│   ├── Valid.Testing/            # Unit tests
│   └── E2E/                      # Generated E2E tests
│       ├── customer_validationTests.cs
│       └── product_creationTests.cs
└── guides/                       # Screenshots and visual guides
    ├── customer/
    └── product/
```
