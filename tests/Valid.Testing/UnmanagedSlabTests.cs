using Xunit;
using Valid;

namespace Valid.Testing;

/// <summary>
/// Tests for UnmanagedSlab: allocation, indexing, enumeration, disposal.
/// </summary>
public unsafe class UnmanagedSlabTests
{
    [Fact]
    public void Constructor_AllocatesWithCorrectLength()
    {
        using var slab = new UnmanagedSlab<int>(10);
        Assert.Equal(10, slab.Length);
    }

    [Fact]
    public void Constructor_ZeroInitializes()
    {
        using var slab = new UnmanagedSlab<long>(5);
        for (int i = 0; i < slab.Length; i++)
            Assert.Equal(0L, slab[i]);
    }

    [Fact]
    public void Indexer_ReadWrite()
    {
        using var slab = new UnmanagedSlab<int>(4);
        slab[0] = 42;
        slab[3] = 99;
        Assert.Equal(42, slab[0]);
        Assert.Equal(99, slab[3]);
    }

    [Fact]
    public void Indexer_ByRef_CanModify()
    {
        using var slab = new UnmanagedSlab<int>(2);
        ref int val = ref slab[0];
        val = 777;
        Assert.Equal(777, slab[0]);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        using var slab = new UnmanagedSlab<int>(3);
        Assert.Throws<IndexOutOfRangeException>(() => slab[3]);
        Assert.Throws<IndexOutOfRangeException>(() => slab[-1]);
    }

    [Fact]
    public void Enumerator_IteratesAllElements()
    {
        using var slab = new UnmanagedSlab<int>(3);
        slab[0] = 10;
        slab[1] = 20;
        slab[2] = 30;

        var values = new List<int>();
        foreach (ref int val in slab)
            values.Add(val);

        Assert.Equal(new[] { 10, 20, 30 }, values);
    }

    [Fact]
    public void GetUnsafePointer_ReturnsNonNull()
    {
        using var slab = new UnmanagedSlab<int>(5);
        int* ptr = slab.GetUnsafePointer();
        Assert.True((nint)ptr != 0);
    }

    [Fact]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        var slab = new UnmanagedSlab<int>(5);
        slab.Dispose();
        slab.Dispose(); // Should not throw
    }

    [Fact]
    public void UInt128_RoundTrip()
    {
        using var slab = new UnmanagedSlab<UInt128>(2);
        var val = new UInt128(0xDEADBEEF, 0xCAFEBABE);
        slab[0] = val;
        Assert.Equal(val, slab[0]);
    }
}
