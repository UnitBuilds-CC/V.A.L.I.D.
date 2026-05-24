using System;
using BenchmarkDotNet.Attributes;
using CashbookRules;
using DemoCashbook.Models;

namespace DemoCashbook.Benchmarks;

[MemoryDiagnoser]
public class ValidPerformanceBenchmarks
{
    private CashbookRules.CashbookLineRecord _fsharpRecordA;
    private CashbookRules.CashbookLineRecord _fsharpRecordB;
    private CashbookLine _csharpLine;
    
    [GlobalSetup]
    public void Setup()
    {
        Valid.WebWorkerBridge.Initialize(10000);

        _fsharpRecordA = new CashbookRules.CashbookLineRecord(
            1, 1, 1, "REF-1001", DateTime.Now, CashbookRules.CashbookModule.GeneralLedger,
            "ACC-001", "F# Benchmark Line A", "REF-1001", 500.0, 0.0, 1.0, 0.0, 0.0, 75.0, false,
            CashbookRules.EntityState.Active
        );

        _fsharpRecordB = new CashbookRules.CashbookLineRecord(
            1, 1, 1, "REF-1001", DateTime.Now, CashbookRules.CashbookModule.GeneralLedger,
            "ACC-001", "F# Benchmark Line B (Tombstone Conflict)", "REF-1001", 1000.0, 0.0, 1.0, 0.0, 0.0, 150.0, false,
            CashbookRules.EntityState.Tombstoned
        );

        _csharpLine = new CashbookLine
        {
            Id = 1,
            LineNo = 1,
            CashbookId = 1,
            AccountCode = "ACC-001",
            Description = "C# Benchmark Line",
            Reference = "REF-1001",
            Deposit = 500.0,
            Payment = 0.0,
            TaxAmount = 75.0,
            Reconciled = false,
            SlabIndex = 1
        };
        _csharpLine.Validate();
    }

    [Benchmark(Description = "F# CRDT Convergence")]
    public CashbookRules.CashbookLineRecord BenchmarkCrdtConvergence()
    {
        return CashbookRules.Rules.resolveLineLogicalCollision(_fsharpRecordA, _fsharpRecordB);
    }

    [Benchmark(Description = "F# Rule Evaluation")]
    public Microsoft.FSharp.Collections.FSharpList<CashbookRules.Rules.ValidationResult> BenchmarkLineRuleEvaluation()
    {
        return CashbookRules.Rules.evaluateLineRules(_fsharpRecordA);
    }

    [Benchmark(Description = "Blazor VDOM Mutation & Validation")]
    public System.UInt128 BenchmarkPocoUpdateAndValidate()
    {
        _csharpLine.Deposit = 650.0;
        _csharpLine.Payment = 0.0;
        _csharpLine.TaxAmount = 97.5;
        _csharpLine.Validate();
        return _csharpLine.ErrorFlags;
    }

    [Benchmark(Description = "VALID Slab direct memory write")]
    public void BenchmarkSlabDirectMemoryWrite()
    {
        int slabIndex = 1;
        int newDeposit = 650;
        int newPayment = 0;
        int newTax = 97;
        
        Valid.WebWorkerBridge.SetObjectValues(slabIndex, newDeposit, newPayment, newTax);
        
        System.UInt128 dirtyMask = (System.UInt128)0x01;
        System.UInt128 errorMask = (System.UInt128)0x00;
        Valid.WebWorkerBridge.SetObjectState(slabIndex, dirtyMask, (System.UInt128)0, errorMask);
    }
}
