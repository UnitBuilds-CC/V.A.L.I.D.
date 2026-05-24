using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Valid.Sample.Blazor;
using Serilog;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.BrowserConsole()
    .CreateLogger();

Log.Information("V.A.V.I.D. Industrial Logger Initialized");

// MISSION 24: Initialize Unmanaged State Slab
Valid.WebWorkerBridge.Initialize(1000000);

// MISSION 25 VALIDATION
Log.Information("\n--- MISSION 25: INDUSTRIAL RESILIENCE VALIDATION ---");
try
{
    // 1. Arena Allocator Test
    Log.Information("1. Testing C# Arena Allocator...");
    long tenMB = 10 * 1024 * 1024;
    unsafe
    {
        using (var arena = new Valid.ArenaAllocator(tenMB))
        {
            Log.Information($"   Arena allocated {tenMB / 1024 / 1024}MB contiguous block.");
            int count = 100000;
            var ptr = arena.Allocate<System.UInt128>(count);
            var slab = new Valid.UnmanagedSlab<System.UInt128>(ptr, count);
            slab[0] = 0xFF; // Test write
            Log.Information($"   Successfully allocated {count} elements from Arena (No GC Fragmentation). Written verify: {slab[0]}");
            
            arena.Reset();
            Log.Information("   Arena reset. Memory cryptographically wiped and logically freed, but OS heap remains contiguous.");
        }
    }
    
    // 2. CRDT Logical Resolver Test
    Log.Information("\n2. Testing F# CRDT Logical Resolver...");
    var nodeAState = Valid.FSharp.AxiomCore.createEntry(1, "ACC_123", "Purchase", 100.0, false, "EXP");
    var nodeBState = nodeAState; // Exact replica
    
    // Node A modifies
    nodeAState = new Valid.FSharp.AxiomEntryRecord(nodeAState.LineId, nodeAState.AccountCode, nodeAState.Description, 200.0, nodeAState.IsProcessed, nodeAState.EntryType, Valid.FSharp.EntityState.Active);
    // Node B logically deletes
    nodeBState = new Valid.FSharp.AxiomEntryRecord(nodeBState.LineId, nodeBState.AccountCode, nodeBState.Description, nodeBState.Amount, nodeBState.IsProcessed, nodeBState.EntryType, Valid.FSharp.EntityState.Tombstoned);
    
    Log.Information("   Node A (Offline): Modifies Amount -> 200.0 (Active)");
    Log.Information("   Node B (Server): Deletes Entry (Tombstoned)");
    
    var resolvedState = Valid.FSharp.AxiomCore.resolveLogicalCollision(nodeAState, nodeBState);
    Log.Information($"   [RESOLVER OUTPUT] Math Merged Amount: {resolvedState.Amount}, Final Business State: {resolvedState.LogicalState}");
    if (resolvedState.LogicalState.IsTombstoned)
        Log.Information("   [SUCCESS] Logical deletion overruled mathematical edit convergence.");
    else
        Log.Error("   [FAILURE] Logical override failed.");
}
catch (Exception ex)
{
    Log.Error(ex, "Mission 25 Validation Failed.");
}
Log.Information("----------------------------------------------------\n");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
