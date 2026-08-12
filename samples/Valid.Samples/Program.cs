// =============================================================================
// V.A.L.I.D. Interactive Samples
// =============================================================================
// Run this program to see V.A.L.I.D. in action. Each sample demonstrates
// a core capability of the framework.
// =============================================================================

using Valid;
using Valid.Samples.Models;
using System.Text.Json;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     V.A.L.I.D. Framework — Interactive Samples             ║");
Console.WriteLine("║     Version 3.1.0                                          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 1: Basic Dirty Tracking
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 1: Basic Dirty Tracking                             │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var customer = new Customer
{
    Name = "Alice Johnson",
    Email = "alice@example.com",
    Age = 30,
    LoyaltyScore = 500
};

Console.WriteLine($"Created customer: {customer.Name}");
Console.WriteLine($"IsDirty: {customer.IsDirty} (expected: False — just initialized)");
Console.WriteLine();

// Change a property — DirtyFlags automatically updates
customer.Age = 31;
Console.WriteLine("Changed Age from 30 to 31");
Console.WriteLine($"IsDirty: {customer.IsDirty} (expected: True)");
Console.WriteLine($"DirtyFlags: {customer.DirtyFlags:X} (bit 2 set for Age)");
Console.WriteLine($"Delta JSON: {customer.GetDeltaJson()}");
Console.WriteLine();

// Reset dirty flags (call after save/sync)
customer.ResetDirty();
Console.WriteLine("Called ResetDirty()");
Console.WriteLine($"IsDirty: {customer.IsDirty} (expected: False)");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 2: Validation Rules
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 2: Validation Rules                                 │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var invalidCustomer = new Customer();
Console.WriteLine("Created empty customer (all validation should fail)");
Console.WriteLine($"IsValid: {invalidCustomer.IsValid} (expected: False)");
Console.WriteLine($"ErrorFlags: {invalidCustomer.ErrorFlags:X}");
Console.WriteLine();

Console.WriteLine("Diagnostics:");
foreach (var diag in invalidCustomer.GetDiagnostics())
{
    Console.WriteLine($"  [{diag.Code}] {diag.Property}: {diag.Message}");
}
Console.WriteLine();

// Fix the validation errors
invalidCustomer.Name = "Bob Smith";
invalidCustomer.Email = "bob@example.com";
invalidCustomer.Age = 25;
invalidCustomer.LoyaltyScore = 100;

Console.WriteLine("Fixed all properties");
Console.WriteLine($"IsValid: {invalidCustomer.IsValid} (expected: True)");
Console.WriteLine($"ErrorFlags: {invalidCustomer.ErrorFlags:X} (expected: 0)");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 3: Time-Travel Debugging (State History)
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 3: Time-Travel Debugging                            │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var timeTravelCustomer = new Customer { Name = "Charlie", Age = 40 };
Console.WriteLine($"Initial state: Name={timeTravelCustomer.Name}, Age={timeTravelCustomer.Age}");

timeTravelCustomer.Age = 41;
Console.WriteLine("Changed Age to 41");

timeTravelCustomer.Age = 42;
Console.WriteLine("Changed Age to 42");

timeTravelCustomer.Name = "Charles";
Console.WriteLine("Changed Name to 'Charles'");

Console.WriteLine();
Console.WriteLine("State history (circular buffer, up to 16 snapshots):");
var history = timeTravelCustomer.GetStateHistory();
for (int i = 0; i < history.Length; i++)
{
    if (!string.IsNullOrEmpty(history[i]))
    {
        Console.WriteLine($"  [{i}] {history[i]}");
    }
}

Console.WriteLine();
Console.WriteLine("Restoring 1 step back...");
timeTravelCustomer.RestoreHistory(1);
Console.WriteLine($"Current state: Name={timeTravelCustomer.Name}, Age={timeTravelCustomer.Age}");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 4: JSON Serialization (Delta Sync)
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 4: JSON Serialization (Delta Sync)                  │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var syncCustomer = new Customer { Name = "Diana", Age = 28, Email = "diana@example.com" };
syncCustomer.ResetDirty(); // Start clean

Console.WriteLine("Modified Age and LoyaltyScore...");
syncCustomer.Age = 29;
syncCustomer.LoyaltyScore = 750;

var delta = syncCustomer.GetDeltaJson();
Console.WriteLine($"Delta JSON (only dirty properties): {delta}");
Console.WriteLine();

// Restore from JSON
var newCustomer = new Customer();
newCustomer.UpdatePropertyFromJson("", delta);
Console.WriteLine("Restored from delta JSON:");
Console.WriteLine($"  Name: {newCustomer.Name}");
Console.WriteLine($"  Age: {newCustomer.Age}");
Console.WriteLine($"  Email: {newCustomer.Email}");
Console.WriteLine($"  LoyaltyScore: {newCustomer.LoyaltyScore}");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 5: Metadata Introspection
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 5: Metadata Introspection                           │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var metadataCustomer = new Customer();
var metadata = metadataCustomer.GetValidMetadata();
Console.WriteLine("Object metadata (schema, properties, validation rules):");
Console.WriteLine(JsonSerializer.Serialize(JsonDocument.Parse(metadata), new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 6: Field Proxy ([ValidField])
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 6: Field Proxy ([ValidField])                       │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var product = new Product
{
    Sku = "PROD-001",
    Name = "Widget",
    Price = 29.99m,
    StockQuantity = 100
};

Console.WriteLine("Created product using [ValidField] proxy properties:");
Console.WriteLine($"  SKU: {product.Sku}");
Console.WriteLine($"  Name: {product.Name}");
Console.WriteLine($"  Price: ${product.Price}");
Console.WriteLine($"  Stock: {product.StockQuantity}");
Console.WriteLine($"  IsDirty: {product.IsDirty}");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Sample 7: BitPulse Events (Surgical UI Updates)
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
Console.WriteLine("│ Sample 7: BitPulse Events (Surgical UI Updates)            │");
Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

var pulseCustomer = new Customer { Name = "Eve", Age = 35 };
pulseCustomer.BitPulse += (bitIndex, maskType) =>
{
    var maskName = maskType switch
    {
        0 => "Dirty",
        1 => "Error",
        2 => "Busy",
        3 => "State",
        _ => "Unknown"
    };
    Console.WriteLine($"  BitPulse: bit {bitIndex} in {maskName} mask");
};

Console.WriteLine("Subscribed to BitPulse event. Changing properties...");
pulseCustomer.Age = 36;
pulseCustomer.Name = "Evelyn";
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Summary
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    Samples Complete                        ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  You've seen:                                              ║");
Console.WriteLine("║  • Dirty tracking with UInt128 bitmasks                    ║");
Console.WriteLine("║  • Validation rules ([Required], [Range], [StringLength])  ║");
Console.WriteLine("║  • Time-travel debugging (state history)                   ║");
Console.WriteLine("║  • JSON delta serialization                                ║");
Console.WriteLine("║  • Metadata introspection                                  ║");
Console.WriteLine("║  • Field proxy ([ValidField])                              ║");
Console.WriteLine("║  • BitPulse events for surgical UI updates                 ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Next steps:                                               ║");
Console.WriteLine("║  • Explore the MCP tools (valid_list_instances, etc.)      ║");
Console.WriteLine("║  • Try the Blazor components (VavidComponentBase)          ║");
Console.WriteLine("║  • Read the API docs via valid_get_api_docs()              ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
