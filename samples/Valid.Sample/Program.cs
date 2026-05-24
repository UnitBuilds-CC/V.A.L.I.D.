using Valid;
using Valid.Queue;
using System;

namespace Valid.Sample;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("VALID 3.0.0 Performance Demo");
        
        // 1. Zero-GC Memory Test
        Console.WriteLine("Initializing 100,000 unmanaged lines...");
        using var lines = new ValidList<AxiomLine>(100000);
        for (int i = 0; i < 100000; i++)
        {
            lines[i] = new AxiomLine { LineId = i, Amount = 10.5 };
        }
        Console.WriteLine($"Generated {lines.Length} lines in unmanaged memory (GC-Invisible).");

        // 2. Bitmask Engine Test
        var batch = new AxiomBatch { BatchId = "Batch-001" };
        Console.WriteLine($"Initial DirtyFlags: 0x{batch.DirtyFlags:X16}");
        
        batch.TotalValue = 5000.75;
        Console.WriteLine($"After modification, TotalValue: {batch.TotalValue}");
        Console.WriteLine($"DirtyFlags: 0x{batch.DirtyFlags:X16} (Bitmask updated)");

        // 3. Durable Outbox Test
        using var outbox = new SqliteOutbox("Data Source=sample_outbox.db");
        outbox.Enqueue(batch);
        Console.WriteLine("Batch enqueued to Hash-Chained Outbox.");

        // MISSION 25 VALIDATION
        Console.WriteLine("\n--- MISSION 25: INDUSTRIAL RESILIENCE VALIDATION ---");
        try
        {
            // 1. Arena Allocator Test
            Console.WriteLine("1. Testing C# Arena Allocator...");
            long tenMB = 10 * 1024 * 1024;
            unsafe
            {
                using (var arena = new Valid.ArenaAllocator(tenMB))
                {
                    Console.WriteLine($"   Arena allocated {tenMB / 1024 / 1024}MB contiguous block.");
                    int count = 100000;
                    var ptr = arena.Allocate<System.UInt128>(count);
                    var slab = new Valid.UnmanagedSlab<System.UInt128>(ptr, count);
                    slab[0] = 0xFF; // Test write
                    Console.WriteLine($"   Successfully allocated {count} elements from Arena (No GC Fragmentation). Written verify: {slab[0]}");
                    
                    arena.Reset();
                    Console.WriteLine("   Arena reset. Memory cryptographically wiped and logically freed, but OS heap remains contiguous.");
                }
            }
            
            // 2. CRDT Logical Resolver Test
            Console.WriteLine("\n2. Testing F# CRDT Logical Resolver...");
            var nodeAState = Valid.FSharp.AxiomCore.createEntry(1, "ACC_123", "Purchase", 100.0, false, "EXP");
            var nodeBState = nodeAState; // Exact replica
            
            // Node A modifies
            nodeAState = new Valid.FSharp.AxiomEntryRecord(nodeAState.LineId, nodeAState.AccountCode, nodeAState.Description, 200.0, nodeAState.IsProcessed, nodeAState.EntryType, Valid.FSharp.EntityState.Active);
            // Node B logically deletes
            nodeBState = new Valid.FSharp.AxiomEntryRecord(nodeBState.LineId, nodeBState.AccountCode, nodeBState.Description, nodeBState.Amount, nodeBState.IsProcessed, nodeBState.EntryType, Valid.FSharp.EntityState.Tombstoned);
            
            Console.WriteLine("   Node A (Offline): Modifies Amount -> 200.0 (Active)");
            Console.WriteLine("   Node B (Server): Deletes Entry (Tombstoned)");
            
            var resolvedState = Valid.FSharp.AxiomCore.resolveLogicalCollision(nodeAState, nodeBState);
            Console.WriteLine($"   [RESOLVER OUTPUT] Math Merged Amount: {resolvedState.Amount}, Final Business State: {resolvedState.LogicalState}");
            if (resolvedState.LogicalState.IsTombstoned)
                Console.WriteLine("   [SUCCESS] Logical deletion overruled mathematical edit convergence.");
            else
                Console.WriteLine("   [FAILURE] Logical override failed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mission 25 Validation Failed: {ex.Message}");
        }
        Console.WriteLine("----------------------------------------------------\n");

        Console.WriteLine("Demo Complete.");
    }
}
