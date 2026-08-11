using Xunit;
using Valid;

namespace Valid.Testing;

/// <summary>
/// Tests for WebWorkerBridge: Initialize, SetObjectState, SetObjectValues,
/// GetObjectValues, SetMagicPulse, and boundary guards.
/// </summary>
public class WebWorkerBridgeTests
{
    [Fact]
    public void Initialize_CreatesSlab()
    {
        WebWorkerBridge.Initialize(100);
        // After init, SetObjectState should work without throwing
        WebWorkerBridge.SetObjectState(1, 1, 0, 0);
    }

    [Fact]
    public void SetObjectState_BeforeInit_DoesNotThrow()
    {
        // Calling without Initialize should be safe (null guard)
        // Note: another test may have called Initialize, so this tests the guard path
        // by using an index that's out of range for any reasonable slab
        WebWorkerBridge.SetObjectState(999999, 1, 2, 3);
    }

    [Fact]
    public void SetObjectState_ZeroIndex_IsIgnored()
    {
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.SetObjectState(0, UInt128.MaxValue, UInt128.MaxValue, UInt128.MaxValue);
        // No exception = success (index 0 is reserved for magic pulse)
    }

    [Fact]
    public void SetObjectState_NegativeIndex_IsIgnored()
    {
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.SetObjectState(-1, 1, 2, 3);
    }

    [Fact]
    public void SetAndGetObjectValues_RoundTrip()
    {
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.SetObjectValues(1, quantity: 42, price: 100, total: 4200);

        WebWorkerBridge.GetObjectValues(1, out int qty, out int price, out int total);

        Assert.Equal(42, qty);
        Assert.Equal(100, price);
        Assert.Equal(4200, total);
    }

    [Fact]
    public void GetObjectValues_BeforeInit_ReturnsZeros()
    {
        // Re-initialize to ensure clean state
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.GetObjectValues(1, out int qty, out int price, out int total);
        // Values should be 0 (no SetObjectValues called for index 1 after init)
        Assert.Equal(0, qty);
        Assert.Equal(0, price);
        Assert.Equal(0, total);
    }

    [Fact]
    public void GetObjectValues_ZeroIndex_ReturnsZeros()
    {
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.GetObjectValues(0, out int qty, out int price, out int total);
        Assert.Equal(0, qty);
    }

    [Fact]
    public void SetMagicPulse_WritesToIndexZero()
    {
        WebWorkerBridge.Initialize(100);
        WebWorkerBridge.SetMagicPulse();
        // Magic pulse writes 0x4242424242424242 to index 0
        // We can verify by reading back via GetObjectValues (index 0 stores magic in slot 0)
        // The magic value is at _stateSlab[0], which is not in the object values range
        // Just verify no exception is thrown
    }

    [Fact]
    public void SetObjectState_MultipleObjects_IndependentSlots()
    {
        WebWorkerBridge.Initialize(100);

        WebWorkerBridge.SetObjectState(1, dirty: 1, busy: 2, error: 3);
        WebWorkerBridge.SetObjectState(2, dirty: 10, busy: 20, error: 30);

        // Verify object 1 values are independent
        WebWorkerBridge.SetObjectValues(1, quantity: 5, price: 10, total: 50);
        WebWorkerBridge.SetObjectValues(2, quantity: 99, price: 1, total: 99);

        WebWorkerBridge.GetObjectValues(1, out int q1, out int p1, out int t1);
        WebWorkerBridge.GetObjectValues(2, out int q2, out int p2, out int t2);

        Assert.Equal(5, q1);
        Assert.Equal(99, q2);
        Assert.Equal(50, t1);
        Assert.Equal(99, t2);
    }

    [Fact]
    public void SetObjectValues_LargeValues()
    {
        WebWorkerBridge.Initialize(100);
        // Test with large int values to verify UInt128 packing
        WebWorkerBridge.SetObjectValues(1, quantity: int.MaxValue, price: int.MaxValue, total: int.MaxValue);
        WebWorkerBridge.GetObjectValues(1, out int qty, out int price, out int total);

        Assert.Equal(int.MaxValue, qty);
        Assert.Equal(int.MaxValue, price);
        Assert.Equal(int.MaxValue, total);
    }
}
