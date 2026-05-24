# V.A.L.I.D. Performance Benchmarks

This directory contains the micro-benchmarks for the **V.A.L.I.D. (Vectorized Asynchronous Logic & Intelligent Diagnostics)** framework. It compares the performance and memory footprint of unmanaged direct memory writes (WASM Bypass) against standard Blazor VDOM mutation/validation and the F# rules/CRDT engine.

## Benchmarks Executed

The benchmark suite (`ValidPerformanceBenchmarks.cs`) measures four critical runtime operations:
1. **VALID Slab Direct Memory Write**: Simulates unmanaged zero-allocation surgical state writes bypassing the Blazor VDOM.
2. **F# Rule Evaluation**: Measures the local evaluation of business rules inside the F# logic core.
3. **F# CRDT Convergence**: Simulates conflict resolution between two offline replicas (active vs. tombstoned collision).
4. **Blazor VDOM Mutation & Validation**: Measures standard C# POCO property modification followed by standard declarative validation.

---

## Performance Results

Executed on `.NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2` under Windows 11:

| Method | Mean | Error | StdDev | Gen0 | Allocated | Speedup |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **VALID Slab Direct Memory Write** | **6.667 ns** | 0.149 ns | 0.287 ns | - | **0 B** | **26.7x** |
| **F# Rule Evaluation** | **17.136 ns** | 0.402 ns | 0.939 ns | - | **0 B** | **10.4x** |
| **F# CRDT Convergence** | **97.359 ns** | 1.980 ns | 5.147 ns | 0.0391 | 328 B | 1.8x |
| **Blazor VDOM Mutation & Validation** | **178.158 ns** | 3.594 ns | 6.295 ns | 0.0048 | 40 B | *Baseline* |

### Key Takeaways
- **Direct Unmanaged Slab Write (6.67 ns)**: Bypassing the managed heap and writing directly to the shared memory slab is **26.7x faster** than the Blazor VDOM baseline and features **zero allocations**. This enables sub-millisecond updates (under 0.4 ms cycles) and prevents Garbage Collection (GC) pauses during high-frequency stress testing.
- **F# Rule Evaluation (17.14 ns)**: The rule evaluation engine compiles to highly optimized logic that runs in **17 ns** with **zero memory allocation**, demonstrating that preflight enterprise rule checks can run at hardware speeds.
- **Zero Allocations**: Both the slab direct write and the F# rule evaluation run with 0 bytes allocated, avoiding memory fragmentation and GC churn.

---

## Running the Benchmarks

To execute the benchmarks locally:

```bash
cd CashbookBatchApp.Benchmarks
dotnet run -c Release
```

Make sure that you execute the run command in **Release mode** as BenchmarkDotNet will refuse to run on debug builds to prevent inaccurate measurements.
