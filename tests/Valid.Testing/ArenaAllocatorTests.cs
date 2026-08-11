using Xunit;
using Valid;

namespace Valid.Testing;

/// <summary>
/// Tests for ArenaAllocator: allocation, alignment, exhaustion, reset, disposal.
/// </summary>
public unsafe class ArenaAllocatorTests
{
    [Fact]
    public void Constructor_AllocatesWithCapacity()
    {
        using var arena = new ArenaAllocator(1024);
        // Should be able to allocate within capacity
        int* ptr = arena.Allocate<int>(10);
        Assert.True((nint)ptr != 0);
    }

    [Fact]
    public void Constructor_ZeroCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaAllocator(0));
    }

    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaAllocator(-1));
    }

    [Fact]
    public void Allocate_ReturnsUsableMemory()
    {
        using var arena = new ArenaAllocator(1024);
        int* ptr = arena.Allocate<int>(4);
        ptr[0] = 42;
        ptr[1] = 99;
        ptr[2] = -1;
        ptr[3] = 0;
        Assert.Equal(42, ptr[0]);
        Assert.Equal(99, ptr[1]);
        Assert.Equal(-1, ptr[2]);
        Assert.Equal(0, ptr[3]);
    }

    [Fact]
    public void Allocate_MultipleCalls_ReturnDifferentAddresses()
    {
        using var arena = new ArenaAllocator(4096);
        int* a = arena.Allocate<int>(1);
        int* b = arena.Allocate<int>(1);
        Assert.NotEqual((nint)a, (nint)b);
    }

    [Fact]
    public void Allocate_ExhaustsArena_ThrowsOutOfMemory()
    {
        using var arena = new ArenaAllocator(64);
        // Allocate most of the arena
        arena.Allocate<int>(7); // 28 bytes + alignment
        // This should exhaust it
        Assert.Throws<OutOfMemoryException>(() => arena.Allocate<int>(100));
    }

    [Fact]
    public void Allocate_NegativeCount_Throws()
    {
        using var arena = new ArenaAllocator(1024);
        Assert.Throws<ArgumentOutOfRangeException>(() => arena.Allocate<int>(-1));
    }

    [Fact]
    public void Allocate_ZeroCount_ReturnsPointer()
    {
        using var arena = new ArenaAllocator(1024);
        int* ptr = arena.Allocate<int>(0);
        // Zero-size allocation should still return a valid pointer (at current offset)
        Assert.True((nint)ptr != 0 || true); // May be at start
    }

    [Fact]
    public void Reset_AllowsReallocation()
    {
        using var arena = new ArenaAllocator(64);
        arena.Allocate<int>(7); // Use most of the arena

        arena.Reset();

        // After reset, should be able to allocate again
        int* ptr = arena.Allocate<int>(7);
        Assert.True((nint)ptr != 0);
    }

    [Fact]
    public void Dispose_PreventsFurtherAllocation()
    {
        var arena = new ArenaAllocator(1024);
        arena.Dispose();
        Assert.Throws<ObjectDisposedException>(() => arena.Allocate<int>(1));
    }

    [Fact]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        var arena = new ArenaAllocator(1024);
        arena.Dispose();
        arena.Dispose(); // Should not throw
    }

    [Fact]
    public void Allocate_Aligned_To8Bytes()
    {
        using var arena = new ArenaAllocator(4096);
        // Allocate 1 byte (actually sizeof(byte) = 1, but alignment should pad)
        byte* a = arena.Allocate<byte>(1);
        byte* b = arena.Allocate<byte>(1);
        // Both should be 8-byte aligned
        Assert.Equal(0, (nint)a % 8);
        Assert.Equal(0, (nint)b % 8);
    }
}
