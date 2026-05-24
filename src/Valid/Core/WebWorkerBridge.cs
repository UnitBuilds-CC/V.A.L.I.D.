using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.CompilerServices;

namespace Valid;

/// <summary>
/// Bridge for JS UI synchronization.
/// </summary>
public static partial class WebWorkerBridge
{
    private static UnmanagedSlab<System.UInt128>? _stateSlab;

    public static void Initialize(int capacity)
    {
        _stateSlab = new UnmanagedSlab<System.UInt128>(capacity);
    }

    [JSExport]
    public static bool IsSlabReady()
    {
        return _stateSlab != null;
    }

    /// <summary>
    /// Sets object state in slab.
    /// </summary>
    public static void SetObjectState(int slabIndex, System.UInt128 dirty, System.UInt128 busy, System.UInt128 error)
    {
        if (_stateSlab == null) return;
        if (slabIndex <= 0) return;

        int baseIndex = slabIndex * 4;
        if (baseIndex + 2 >= _stateSlab.Length) return;

        _stateSlab[baseIndex] = dirty;
        _stateSlab[baseIndex + 1] = busy;
        _stateSlab[baseIndex + 2] = error;
    }

    /// <summary>
    /// Sets object values in slab.
    /// </summary>
    public static void SetObjectValues(int slabIndex, int quantity, int price, int total)
    {
        if (_stateSlab == null || slabIndex <= 0) return;

        int baseIndex = slabIndex * 4;
        if (baseIndex + 3 >= _stateSlab.Length) return;

        ulong low = (uint)quantity | ((ulong)(uint)price << 32);
        ulong high = (uint)total;
        
        _stateSlab[baseIndex + 3] = new System.UInt128(high, low);
    }

    /// <summary>
    /// Gets object values from slab.
    /// </summary>
    public static void GetObjectValues(int slabIndex, out int quantity, out int price, out int total)
    {
        quantity = 0;
        price = 0;
        total = 0;
        if (_stateSlab == null || slabIndex <= 0) return;

        int baseIndex = slabIndex * 4;
        if (baseIndex + 3 >= _stateSlab.Length) return;

        System.UInt128 val = _stateSlab[baseIndex + 3];
        ulong low = (ulong)(val & new System.UInt128(0, 0xFFFFFFFFFFFFFFFF));
        ulong high = (ulong)(val >> 64);

        quantity = (int)(uint)(low & 0xFFFFFFFF);
        price = (int)(uint)(low >> 32);
        total = (int)(uint)high;
    }

    [JSExport]
    public static unsafe nint GetStatePointer()
    {
        if (_stateSlab == null) return 0;
        return (nint)_stateSlab.GetUnsafePointer();
    }

    [JSExport]
    public static int GetSlabLength()
    {
        return _stateSlab?.Length ?? 0;
    }

    /// <summary>
    /// Sets magic pulse header.
    /// </summary>
    public static void SetMagicPulse()
    {
        if (_stateSlab == null) return;
        System.UInt128 magic = new System.UInt128(0x4242424242424242, 0x4242424242424242);
        _stateSlab[0] = magic;
    }

    [JSExport]
    public static unsafe bool IsDirty(int index)
    {
        if (_stateSlab == null || index < 0 || index >= _stateSlab.Length) return false;
        return _stateSlab[index] != System.UInt128.Zero;
    }
}
