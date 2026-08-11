using Xunit;
using Valid;

namespace Valid.Testing;

/// <summary>
/// Tests for BitmaskOperations: SIMD-accelerated AnyBitSet and AllZero.
/// </summary>
public unsafe class BitmaskOperationsTests
{
    [Fact]
    public void AnyBitSet_AllZeros_ReturnsFalse()
    {
        var blocks = new UInt128[4];
        fixed (UInt128* ptr = blocks)
        {
            Assert.False(BitmaskOperations.AnyBitSet(ptr, 4));
        }
    }

    [Fact]
    public void AnyBitSet_OneBitSet_ReturnsTrue()
    {
        var blocks = new UInt128[4];
        blocks[2] = (UInt128)1;
        fixed (UInt128* ptr = blocks)
        {
            Assert.True(BitmaskOperations.AnyBitSet(ptr, 4));
        }
    }

    [Fact]
    public void AnyBitSet_HighBitSet_ReturnsTrue()
    {
        var blocks = new UInt128[2];
        blocks[0] = UInt128.MaxValue;
        fixed (UInt128* ptr = blocks)
        {
            Assert.True(BitmaskOperations.AnyBitSet(ptr, 2));
        }
    }

    [Fact]
    public void AnyBitSet_Empty_ReturnsFalse()
    {
        var blocks = new UInt128[0];
        fixed (UInt128* ptr = blocks)
        {
            Assert.False(BitmaskOperations.AnyBitSet(ptr, 0));
        }
    }

    [Fact]
    public void AllZero_AllZeros_ReturnsTrue()
    {
        var blocks = new UInt128[4];
        fixed (UInt128* ptr = blocks)
        {
            Assert.True(BitmaskOperations.AllZero(ptr, 4));
        }
    }

    [Fact]
    public void AllZero_OneBitSet_ReturnsFalse()
    {
        var blocks = new UInt128[4];
        blocks[1] = (UInt128)1 << 64;
        fixed (UInt128* ptr = blocks)
        {
            Assert.False(BitmaskOperations.AllZero(ptr, 4));
        }
    }

    [Fact]
    public void AnyBitSet_LargeBlock_WorksCorrectly()
    {
        // Test with enough blocks to exercise the SIMD path (Vector256 = 4 ulongs = 2 UInt128s)
        var blocks = new UInt128[16];
        fixed (UInt128* ptr = blocks)
        {
            Assert.False(BitmaskOperations.AnyBitSet(ptr, 16));

            // Set a bit in the last block
            blocks[15] = (UInt128)1 << 127;
            Assert.True(BitmaskOperations.AnyBitSet(ptr, 16));
        }
    }

    [Fact]
    public void AllZero_SingleBlock_WorksCorrectly()
    {
        var blocks = new UInt128[1];
        fixed (UInt128* ptr = blocks)
        {
            Assert.True(BitmaskOperations.AllZero(ptr, 1));
            blocks[0] = UInt128.MaxValue;
            Assert.False(BitmaskOperations.AllZero(ptr, 1));
        }
    }
}
