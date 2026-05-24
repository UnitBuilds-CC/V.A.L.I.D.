# V.A.L.I.D. Browser Extension - Manifest Specification

This document defines the coordination protocol between the V.A.L.I.D. .NET framework and the Browser extension (V.A.V.I.D.).

## Discovery Phase
The extension discovers V.A.L.I.D. objects on a page by querying the DOM for specific data attributes or by executing a global discovery script.

### Global Bridge
Business objects should expose their metadata via a standard hook:
```javascript
window.__VALID_OBJECTS__ = [
  {
    elementId: "batch-form-1",
    metadata: JSON.parse(AxiomBatch.GetValidMetadata())
  }
];
```

## Communication Protocol
1. **Metadata Query**: Extension reads `GetValidMetadata()` to build the HUD.
2. **State Sync**: Extension watches `window` events or polls for `GetDeltaJson()` to show real-time "Dirty" status in the browser toolbar.
3. **Fuzz Testing**: Extension uses the `rules` array in metadata to auto-fill invalid values (e.g., negative amounts) to test client-side resilience.

## Proposed Manifest (v1.0)
```json
{
  "manifest_version": 3,
  "name": "V.A.V.I.D. HUD",
  "version": "1.0.0",
  "description": "Intelligent HUD for V.A.L.I.D. framework business objects.",
  "permissions": ["activeTab", "scripting"],
  "content_scripts": [
    {
      "matches": ["<all_urls>"],
      "js": ["bridge.js", "hud.js"]
    }
  ]
}
```

## Feature Set: The "X-Ray" View
- **Red Border**: Any field where bits are set in `_dirtyFlags` is highlighted in the browser.
- **Rule Inspector**: Hovering over a field shows the .NET validation rules (`[Required]`, `[Range]`) in a tooltip.
- **Deep Diagnostics**: Displays the `SyncWorker` progress and server error codes (e.g., 409 Conflict) directly atop the UI.
